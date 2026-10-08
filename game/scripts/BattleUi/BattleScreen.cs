using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// The battle screen (M2 brief §3–§8): the 3D board in the centre, the turn-order timeline on the left, the details
/// panel and combat log on the right, and the action bar along the bottom. The session runs one clock event at a
/// time; each event's results are animated before the next, and on a hero's turn the screen waits for the player.
/// Everything the player can point at is a focusable 2D control laid over the board, so the mouse, the keyboard
/// and (later) a controller all drive the same focus.
/// </summary>
public partial class BattleScreen : Control
{
    public Main Main { get; }
    public BattleSession Session { get; }
    public ScreenshotJob? Screenshot { get; set; }

    readonly BoardLayout layout;
    BoardView board = null!;
    Timeline timeline = null!;
    DetailsPanel details = null!;
    RichTextLabel log = null!;
    HBoxContainer actionBar = null!;
    Label info = null!;
    Label speedLabel = null!;
    Control overlay = null!;           // target buttons and floating text over the board
    PanelContainer? endPanel;

    enum Mode { Playing, ChooseAction, ChooseTarget, Over }
    Mode mode = Mode.Playing;
    Unit? hero;
    ActionDef? picked;
    readonly Dictionary<Unit, Button> unitButtons = [];
    readonly Dictionary<Tile, Button> tileButtons = [];
    Unit? focusedUnit;
    TurnEstimate? ghost;            // the hovered action's timeline markers
    bool skip;
    int actionsShown;

    Battle Battle => Session.Battle;
    float S => Main.Settings.ActionSeconds;

    public BattleScreen(Main main, BattleSession session, BoardLayout layout)
    {
        Main = main;
        Session = session;
        this.layout = layout;
    }

    // ---- Building the screen ----

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Pass;
        board = new BoardView(this, layout);
        AddChild(board);

        overlay = new Control { MouseFilter = MouseFilterEnum.Pass };
        overlay.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(overlay);

        // Left: the timeline.
        timeline = new Timeline(this);
        timeline.SetAnchorsPreset(LayoutPreset.LeftWide);
        timeline.OffsetRight = Layout.TimelineWidth;
        AddChild(timeline);

        // Right: details above the log.
        var right = new VBoxContainer();
        right.SetAnchorsPreset(LayoutPreset.RightWide);
        right.OffsetLeft = -Layout.PanelWidth;
        right.AddThemeConstantOverride("separation", 6);
        AddChild(right);
        details = new DetailsPanel(this) { CustomMinimumSize = new Vector2(0, 360) };
        right.AddChild(details);
        var logPanel = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        right.AddChild(logPanel);
        var logCol = new VBoxContainer();
        logPanel.AddChild(logCol);
        logCol.AddChild(Ui.Label(T("ui.combat_log"), 13, Ui.Dim));
        log = new RichTextLabel
        {
            ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, BbcodeEnabled = false,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None,
        };
        log.AddThemeFontSizeOverride("normal_font_size", Ui.Px(12));
        logCol.AddChild(log);

        // Bottom: the info line and the action bar.
        var bottom = new VBoxContainer();
        bottom.SetAnchorsPreset(LayoutPreset.BottomWide);
        bottom.OffsetLeft = Layout.TimelineWidth + 8;
        bottom.OffsetRight = -Layout.PanelWidth - 8;
        bottom.OffsetTop = -Layout.BarHeight;
        bottom.OffsetBottom = -6;
        AddChild(bottom);
        var infoRow = new HBoxContainer();
        bottom.AddChild(infoRow);
        info = Ui.Label("", 14);
        info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        infoRow.AddChild(info);
        speedLabel = Ui.Label("", 12, Ui.Dim);
        infoRow.AddChild(speedLabel);
        actionBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, SizeFlagsVertical = SizeFlags.ExpandFill };
        actionBar.AddThemeConstantOverride("separation", 8);
        bottom.AddChild(actionBar);

        foreach (var unit in Battle.Units)
        {
            var b = OverlayButton();
            b.Pressed += () => UnitPressed(unit);
            b.MouseEntered += () => b.GrabFocus();
            b.FocusEntered += () => UnitFocused(unit);
            b.FocusExited += () => UnitFocused(null);
            unitButtons[unit] = b;
            overlay.AddChild(b);
        }
        AddTileButtons();

        ShowSpeed();
        Resized += Layout0;
        Layout0();
        PlayOn();
    }

    /// <summary>A pointer target for every tile that doesn't have one yet (the board can gain a row).</summary>
    void AddTileButtons()
    {
        foreach (var area in Battle.Grid.Areas)
            foreach (var tile in Battle.Grid.TilesOf(area.Id))
            {
                if (tileButtons.ContainsKey(tile)) continue;
                var b = OverlayButton();
                b.Visible = false;
                b.Pressed += () => TilePressed(tile);
                b.MouseEntered += () => b.GrabFocus();
                b.FocusEntered += () => board.MarkTiles(ValidTiles(), tile);
                tileButtons[tile] = b;
                overlay.AddChild(b);
            }
    }

    static class Layout
    {
        public const float TimelineWidth = 112, PanelWidth = 320, BarHeight = 124;
    }

    void Layout0()
    {
        var size = GetViewportRect().Size;
        board.Region = new Rect2(Layout.TimelineWidth, 0, size.X - Layout.TimelineWidth - Layout.PanelWidth, size.Y - Layout.BarHeight);
    }

    static Button OverlayButton()
    {
        var b = new Button { Flat = true, FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Stop, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        b.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
        b.AddThemeStyleboxOverride("hover", new StyleBoxEmpty());
        b.AddThemeStyleboxOverride("pressed", new StyleBoxEmpty());
        b.AddThemeStyleboxOverride("disabled", new StyleBoxEmpty());
        b.AddThemeStyleboxOverride("focus", Ui.Focus());
        return b;
    }

    public override void _Process(double delta)
    {
        // Keep the pointer targets over the cards, wherever the cards are.
        foreach (var (unit, b) in unitButtons)
        {
            var r = board.ScreenRect(board.Cards[unit]);
            b.Position = r.Position;
            b.Size = r.Size;
        }
        foreach (var (tile, b) in tileButtons)
        {
            if (!b.Visible) continue;
            var r = board.ScreenRect(tile).Grow(-4);
            b.Position = r.Position;
            b.Size = r.Size;
        }
    }

    // ---- Playing the battle ----

    /// <summary>Runs clock events, animating each, until a hero needs a decision or the battle ends.</summary>
    async void PlayOn()
    {
        mode = Mode.Playing;
        while (true)
        {
            if (Screenshot is { } job && actionsShown >= job.Actions)
            {
                if (!job.StopAtHero || Session.Awaiting is not null || Session.Over)
                {
                    if (job.StopAtHero && Session.Awaiting is { } waiting && job.Keys.Length > 0 && !keysSent)
                    {
                        keysSent = true;
                        EnterChooseAction(waiting);
                        PressKeys(job);
                        return;
                    }
                    if (job.StopAtHero && Session.Awaiting is { } w && !keysSent) EnterChooseAction(w, demo: true);
                    else if (Session.Awaiting is { } w2 && mode != Mode.ChooseAction) EnterChooseAction(w2);
                    TakeScreenshot(job);
                    return;
                }
                Session.AutoBattle = false;
            }
            if (Session.Over)
            {
                ShowEnd();
                return;
            }
            if (Session.Awaiting is { } h)
            {
                EnterChooseAction(h);
                return;
            }
            foreach (var r in Session.Step())
                await Animate(r);
            if (!IsInsideTree()) return;
        }
    }

    async void Confirm(Choice choice)
    {
        if (Session.CantChoose(choice) is not null) return;
        LeaveChoice();
        mode = Mode.Playing;
        foreach (var r in Session.Choose(choice))
            await Animate(r);
        if (IsInsideTree()) PlayOn();
    }

    /// <summary>Auto-battle switched on during a hero's turn: the AI takes this turn too.</summary>
    async void HandToAi()
    {
        LeaveChoice();
        mode = Mode.Playing;
        foreach (var r in Session.ChooseByAi())
            await Animate(r);
        if (IsInsideTree()) PlayOn();
    }

    /// <summary>Waits for <paramref name="seconds"/>, or less if the player skips.</summary>
    async Task Wait(float seconds)
    {
        var end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
        while (!skip && Time.GetTicksMsec() < end && IsInsideTree())
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    async Task Animate(ActionResult r)
    {
        skip = false;
        foreach (var line in CombatLog.Lines(Battle, r, LogLevel.Brief))
            log.AddText(line + "\n");
        var visible = r.Action is not null || r.Outcomes.Count > 0;
        if (r.Action is not null) actionsShown++;
        if (!visible) return;

        var s = Settings.ActionSeconds == 0 ? 0 : S;
        var actor = board.Cards[r.Actor];
        if (r.Action is { } action)
        {
            details.Show(r.Actor);
            actor.Lift(true, s * 0.2f);
            if (r.Outcomes.FirstOrDefault() is not CastStarted && action.Target != ActionTarget.Tile) SetMoment(r.Actor, "attacking");
            if (r.Outcomes.FirstOrDefault() is CastStarted)
            {
                Float(r.Actor, T("ui.casting"), Ui.Mana);
            }
            else if (r.Target is { } target && target != r.Actor && action.Target != ActionTarget.Tile)
            {
                var targetCard = board.Cards[target];
                if (action.Range is ActionRange.Melee or ActionRange.Reach)
                    actor.Lunge(targetCard.Home, s * 0.5f);
                else
                    Projectile(actor.Home, targetCard.Home, s * 0.4f);
                await Wait(s * 0.25f);
            }
        }

        foreach (var o in r.Outcomes)
            Show(o, s);
        RefreshAll(s * 0.4f);
        await Wait(r.Action is null ? s * 0.4f : s * 0.75f);
        actor.Lift(false, s * 0.15f);
        SetMoment(r.Actor, null);
    }

    Settings Settings => Main.Settings;

    /// <summary>Feedback for one outcome: numbers, shakes, sidesteps, flips.</summary>
    void Show(Outcome o, float s)
    {
        switch (o)
        {
            case Attempt { Roll.Success: false } miss:
                Float(miss.Target, T("ui.miss"), Ui.Dim);
                board.Cards[miss.Target].Sidestep(s * 0.4f);
                break;
            case Damaged d:
                Hurt(d.Target, s);
                var crit = d.Breakdown.CritTiers;
                Float(d.Target, (d.Taken.Absorbed + d.Taken.ToHealth).ToString(), crit == 2 ? Ui.Gold : crit == 1 ? new Color(1, 0.6f, 0.3f) : Colors.White,
                    crit == 2 ? 34 : crit == 1 ? 28 : 22, crit == 2 ? T("ui.brutal") : crit == 1 ? T("ui.crit") : null);
                board.Cards[d.Target].Shake(s * 0.35f, crit + 1);
                break;
            case ProcDamaged pd:
                Hurt(pd.Target, s);
                Float(pd.Target, (pd.Taken.Absorbed + pd.Taken.ToHealth).ToString(), new Color(1, 0.75f, 0.4f), 18, pd.Proc.Name);
                break;
            case PeriodicDamaged p:
                Hurt(p.Target, s);
                Float(p.Target, (p.Taken.Absorbed + p.Taken.ToHealth).ToString(), new Color(0.6f, 0.9f, 0.35f), 18);
                break;
            case DelayedDamaged p:
                Float(p.Target, (p.Taken.Absorbed + p.Taken.ToHealth).ToString(), new Color(0.6f, 0.9f, 0.35f), 22);
                board.Cards[p.Target].Shake(s * 0.3f);
                break;
            case Healed { Amount: > 0 } h:
                Float(h.Target, "+" + h.Amount, Ui.Health, 18);
                break;
            case Shielded sh:
                Float(sh.Target, "+" + sh.Amount, Ui.Shield, 16, T("ui.shield"));
                break;
            case BuffApplied b:
                Float(b.Target, b.Buff.Def.Name, Ui.Ink, 13);
                break;
            case Staggered { Broke: true } st:
                Float(st.Target, T("ui.broken"), Colors.White, 18);
                break;
            case Interrupted i:
                Float(i.Unit, T("ui.interrupted"), Ui.Enemy, 14);
                break;
            case TurnLost l:
                Float(l.Unit, T("ui.turn_lost"), Ui.Dim, 14);
                break;
            case Died d:
                // A moment on the knocked-out portrait, then the card flips face down.
                SetMoment(d.Unit, "knocked_out");
                if (s <= 0)
                    board.Cards[d.Unit].Flip(true, 0);
                else
                {
                    falling.Add(d.Unit);
                    GetTree().CreateTimer(s * 1.2f).Timeout += () =>
                    {
                        if (!IsInsideTree()) return;
                        falling.Remove(d.Unit);
                        board.Cards[d.Unit].Flip(true, s * 0.8f);
                    };
                }
                break;
        }
    }

    readonly HashSet<Unit> falling = [];      // fallen units still showing their knocked-out portrait

    /// <summary>Shows a moment's portrait state on a card (null clears it).</summary>
    void SetMoment(Unit unit, string? moment)
    {
        if (!unit.Alive && moment != "knocked_out") return;
        board.Cards[unit].Face.Moment = moment;
        board.Cards[unit].Redraw();
    }

    /// <summary>The "hurt" portrait for a moment after damage.</summary>
    void Hurt(Unit unit, float s)
    {
        if (!unit.Alive) return;
        SetMoment(unit, "hurt");
        GetTree().CreateTimer(Math.Max(0.35f, s * 0.9f)).Timeout += () =>
        {
            if (IsInsideTree() && board.Cards[unit].Face.Moment == "hurt") SetMoment(unit, null);
        };
    }

    void RefreshAll(float seconds)
    {
        if (board.SyncTiles()) AddTileButtons();
        board.PlaceCards(seconds);
        foreach (var (unit, card) in board.Cards)
        {
            if (!unit.Alive && !falling.Contains(unit)) card.Flip(true, seconds);
            else card.SetTilted(unit.Stunned);
            card.Redraw();
        }
        timeline.Refresh(ghost);
        details.Refresh();
    }

    // ---- Floating text and projectiles ----

    void Float(Unit unit, string text, Color color, int size = 20, string? caption = null)
    {
        var r = board.ScreenRect(board.Cards[unit]);
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        var label = Ui.Label(text, size, color);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.AddThemeConstantOverride("outline_size", 5);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        box.AddChild(label);
        if (caption is not null)
        {
            var c = Ui.Label(caption, 12, color);
            c.HorizontalAlignment = HorizontalAlignment.Center;
            c.AddThemeConstantOverride("outline_size", 4);
            c.AddThemeColorOverride("font_outline_color", Colors.Black);
            box.AddChild(c);
        }
        overlay.AddChild(box);
        box.Size = new Vector2(r.Size.X, 0);
        var start = new Vector2(r.Position.X, r.GetCenter().Y - 20);
        box.Position = start;
        var seconds = Math.Max(0.6f, Main.Settings.ActionSeconds * 2.2f);
        var t = box.CreateTween().SetParallel();
        t.TweenProperty(box, "position", start - new Vector2(0, 46), seconds).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        t.TweenProperty(box, "modulate:a", 0f, seconds * 0.5f).SetDelay(seconds * 0.5f);
        t.Chain().TweenCallback(Callable.From(box.QueueFree));
    }

    void Projectile(Vector3 from, Vector3 to, float seconds)
    {
        if (seconds <= 0) return;
        var orb = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f },
            MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(1, 0.85f, 0.5f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            Position = from + new Vector3(0, 0.5f, 0),
        };
        board.AddChild(orb);
        var t = orb.CreateTween();
        t.TweenProperty(orb, "position", to + new Vector3(0, 0.5f, 0), seconds);
        t.TweenCallback(Callable.From(orb.QueueFree));
    }

    // ---- A hero's turn ----

    void EnterChooseAction(Unit h, bool demo = false)
    {
        mode = Mode.ChooseAction;
        hero = h;
        picked = null;
        board.Cards[h].Lift(true, 0.15f);
        board.Cards[h].Face.Mark = CardMark.Active;
        details.Show(h);
        BuildActionBar(h);
        RefreshAll(0.15f);
        ClearMarks();
        board.Cards[h].Face.Mark = CardMark.Active;
        info.Text = F("ui.your_turn", ("hero", h.Name));
        if (demo)
        {
            // Screenshot mode: show the first usable enemy-targeted action and its first target's preview.
            var action = Battle.Data.ActionsOf(h.Def).Select(id => Battle.Data.Actions[id])
                .FirstOrDefault(a => a.Target == ActionTarget.Enemy && Options.Unusable(Battle, h, a) is null);
            if (action is not null)
            {
                Pick(action);
                if (Options.TargetsFor(Battle, h, action).FirstOrDefault() is { } t) unitButtons[t].GrabFocus();
            }
        }
        else if (actionBar.GetChildCount() > 0)
            ((Control)actionBar.GetChild(0)).GrabFocus();
    }

    void BuildActionBar(Unit h)
    {
        ClearActionBar();
        var i = 0;
        foreach (var id in Battle.Data.ActionsOf(h.Def))
        {
            var action = Battle.Data.Actions[id];
            var why = Options.Unusable(Battle, h, action);
            var button = new ActionButton(this, action, ++i, why);
            button.Pressed += () => Pick(action);
            button.FocusEntered += () => ActionFocused(action, why);
            button.MouseEntered += () => button.GrabFocus();
            actionBar.AddChild(button);
        }
    }

    void ActionFocused(ActionDef action, string? why)
    {
        if (mode != Mode.ChooseAction || hero is null) return;
        var parts = new List<string> { action.Name, F("ui.ap_cost", ("ap", action.ApCost)) };
        if (action.ManaCost > 0) parts.Add(F("ui.mana_cost", ("mana", action.ManaCost)));
        if (action.CastTime > 0) parts.Add(F("ui.cast_time", ("cast", action.CastTime / 100.0)));
        if (why is not null) parts.Add(F("ui.unusable", ("reason", Text.Reason(why))));
        info.Text = string.Join(T("log.separator"), parts);
        ghost = why is null ? Preview.NextTurn(Battle, hero, action) : null;
        timeline.Refresh(ghost);
    }

    void Pick(ActionDef action)
    {
        if (hero is null || mode is not (Mode.ChooseAction or Mode.ChooseTarget)) return;
        if (Options.Unusable(Battle, hero, action) is string why)
        {
            info.Text = F("ui.unusable", ("reason", Text.Reason(why)));
            return;
        }
        picked = action;
        mode = Mode.ChooseTarget;
        ghost = Preview.NextTurn(Battle, hero, action);
        timeline.Refresh(ghost);
        ClearMarks();
        board.Cards[hero].Face.Mark = CardMark.Active;
        if (action.Target == ActionTarget.Tile)
        {
            var tiles = ValidTiles();
            foreach (var (tile, b) in tileButtons) b.Visible = tiles.Contains(tile);
            board.MarkTiles(tiles, null);
            info.Text = F("ui.pick_tile", ("action", action.Name));
            if (tiles.Count > 0) tileButtons[tiles[0]].GrabFocus();
        }
        else
        {
            var targets = Options.TargetsFor(Battle, hero, action);
            foreach (var t in targets) board.Cards[t].Face.Mark = CardMark.Valid;
            info.Text = F("ui.pick_target", ("action", action.Name));
            if (targets.Count > 0) unitButtons[targets[0]].GrabFocus();
        }
        foreach (var c in board.Cards.Values) c.Redraw();
    }

    List<Tile> ValidTiles() => hero is not null && picked is not null ? Options.TilesFor(Battle, hero, picked) : [];

    void UnitFocused(Unit? unit)
    {
        focusedUnit = unit;
        if (unit is null) return;
        details.Show(unit);
        if (mode != Mode.ChooseTarget || hero is null || picked is null) return;
        foreach (var (u, card) in board.Cards)
            if (card.Face.Mark == CardMark.Hovered) card.Face.Mark = Options.TargetsFor(Battle, hero, picked).Contains(u) ? CardMark.Valid : CardMark.None;
        if (!Options.TargetsFor(Battle, hero, picked).Contains(unit))
        {
            info.Text = Battle.Grid.CantTarget(hero, picked, unit) is string why ? F("ui.cant_target", ("reason", Text.Reason(why))) : "";
            return;
        }
        board.Cards[unit].Face.Mark = CardMark.Hovered;
        foreach (var c in board.Cards.Values) c.Redraw();
        info.Text = PreviewText(Preview.Target(Battle, hero, picked, unit));
    }

    /// <summary>"Goblin Grunt: hit 86% · 21 dmg · crit 32 (5%) · Brutal 42 (5% of crits) · applies Chill · procs: Flaming 100%".</summary>
    string PreviewText(TargetPreview p)
    {
        var parts = new List<string> { p.Target.Name };
        if (picked!.Target == ActionTarget.Enemy) parts.Add(F("ui.preview_hit", ("chance", CombatLog.Pct(p.HitChance))));
        if (p.Damage is { } dmg)
        {
            parts.Add(F("ui.preview_damage", ("amount", dmg)));
            if (p.CritChance > 0)
            {
                parts.Add(F("ui.preview_crit", ("amount", p.CritDamage), ("chance", CombatLog.Pct(p.CritChance))));
                parts.Add(F("ui.preview_brutal", ("amount", p.BrutalDamage), ("chance", CombatLog.Pct(p.BrutalChance))));
            }
        }
        foreach (var e in p.Effects)
            parts.Add(F("ui.preview_applies", ("effect", Battle.Data.Effects[e].Name)));
        if (p.Procs.Count > 0)
            parts.Add(F("ui.preview_procs", ("procs", string.Join(T("log.list_separator"), p.Procs.Select(x => $"{x.Proc.Name} {CombatLog.Pct(x.Chance)}")))));
        return string.Join(T("log.separator"), parts);
    }

    void UnitPressed(Unit unit)
    {
        if (mode == Mode.Playing) { skip = true; return; }
        if (mode != Mode.ChooseTarget || hero is null || picked is null) return;
        if (!Options.TargetsFor(Battle, hero, picked).Contains(unit)) return;
        Confirm(new Choice(hero.Id, picked.Id, unit.Id));
    }

    void TilePressed(Tile tile)
    {
        if (mode != Mode.ChooseTarget || hero is null || picked is null) return;
        Confirm(new Choice(hero.Id, picked.Id, Tile: tile));
    }

    void StepBack()
    {
        if (mode == Mode.ChooseTarget && hero is not null)
        {
            var h = hero;
            mode = Mode.ChooseAction;
            ClearMarks();
            board.Cards[h].Face.Mark = CardMark.Active;
            foreach (var c in board.Cards.Values) c.Redraw();
            var index = Battle.Data.ActionsOf(h.Def).ToList().IndexOf(picked!.Id);
            picked = null;
            info.Text = F("ui.your_turn", ("hero", h.Name));
            if (index >= 0 && index < actionBar.GetChildCount()) ((Control)actionBar.GetChild(index)).GrabFocus();
        }
    }

    /// <summary>Removes the action buttons now (freeing is deferred, and stale buttons would take focus).</summary>
    void ClearActionBar()
    {
        foreach (var child in actionBar.GetChildren())
        {
            actionBar.RemoveChild(child);
            child.QueueFree();
        }
    }

    void ClearMarks()
    {
        foreach (var c in board.Cards.Values) c.Face.Mark = CardMark.None;
        foreach (var b in tileButtons.Values) b.Visible = false;
        board.MarkTiles([], null);
    }

    void LeaveChoice()
    {
        if (hero is not null) board.Cards[hero].Lift(false, 0.15f);
        ClearMarks();
        foreach (var c in board.Cards.Values) c.Redraw();
        ClearActionBar();
        info.Text = "";
        hero = null;
        picked = null;
        ghost = null;
        timeline.Refresh(null);
    }

    // ---- Input ----

    /// <summary>Tab cycles targets; it runs before the GUI, which would otherwise move focus with Tab.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.Tab } key && mode == Mode.ChooseTarget && hero is not null && picked is not null)
        {
            // Tab cycles the valid targets (or tiles).
            var shift = key.ShiftPressed ? -1 : 1;
            if (picked.Target == ActionTarget.Tile)
            {
                var tiles = ValidTiles();
                if (tiles.Count == 0) { GetViewport().SetInputAsHandled(); return; }
                var at = tiles.FindIndex(t => tileButtons[t].HasFocus());
                tileButtons[tiles[((at + shift) % tiles.Count + tiles.Count) % tiles.Count]].GrabFocus();
            }
            else
            {
                var targets = Options.TargetsFor(Battle, hero, picked);
                if (targets.Count == 0) { GetViewport().SetInputAsHandled(); return; }
                var at = focusedUnit is null ? -1 : targets.IndexOf(focusedUnit);
                unitButtons[targets[((at + shift) % targets.Count + targets.Count) % targets.Count]].GrabFocus();
            }
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true } mb)
        {
            if (mb.ButtonIndex == MouseButton.Right) { StepBack(); AcceptEvent(); }
            else if (mode == Mode.Playing) { skip = true; AcceptEvent(); }
            return;
        }
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        if (e.IsActionPressed("ui_cancel")) { StepBack(); AcceptEvent(); return; }
        if (key.Keycode == Key.S) { Main.Settings.NextSpeed(); ShowSpeed(); AcceptEvent(); return; }
        if (key.Keycode == Key.A && mode != Mode.Over)
        {
            Session.AutoBattle = !Session.AutoBattle;
            Main.Settings.AutoBattle = Session.AutoBattle;
            ShowSpeed();
            if (Session.AutoBattle && mode is Mode.ChooseAction or Mode.ChooseTarget && hero is not null)
                HandToAi();
            AcceptEvent();
            return;
        }
        if (mode == Mode.Playing && key.Keycode is Key.Space or Key.Enter) { skip = true; AcceptEvent(); return; }
        if (key.Keycode is >= Key.Key1 and <= Key.Key9 && mode is Mode.ChooseAction or Mode.ChooseTarget)
        {
            var n = (int)(key.Keycode - Key.Key1);
            if (n < actionBar.GetChildCount() && actionBar.GetChild(n) is ActionButton ab)
            {
                StepBack();
                ab.GrabFocus();
                Pick(ab.Action);
            }
            AcceptEvent();
            return;
        }
    }

    void ShowSpeed() => speedLabel.Text = F("ui.speed_hint", ("speed", T(Settings.SpeedKey(Main.Settings.Speed))),
        ("auto", T(Session.AutoBattle ? "ui.on" : "ui.off")));

    // ---- The end ----

    void ShowEnd()
    {
        mode = Mode.Over;
        RefreshAll(0.2f);
        log.AddText(CombatLog.Ending(Battle) + "\n");
        endPanel = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        endPanel.AddChild(col);
        var title = Battle.Winner switch
        {
            Side.Party => T("ui.victory"),
            Side.Enemy => T("ui.defeat"),
            _ => T("ui.stalemate"),
        };
        col.AddChild(Ui.Label(title, 30, Battle.Winner == Side.Party ? Ui.Gold : Ui.Enemy));
        col.AddChild(Ui.Label(F("ui.end_summary", ("encounter", Session.Encounter.Name), ("seed", Session.Seed), ("time", CombatLog.T(Battle.Clock.Tick))), 13, Ui.Dim));
        var restart = Ui.Button(T("ui.restart"), () => Main.StartBattle(Session.Encounter.Id, Session.Seed, layout: layout));
        col.AddChild(restart);
        col.AddChild(Ui.Button(T("ui.next_seed"), () => Main.StartBattle(Session.Encounter.Id, Session.Seed + 1, layout: layout)));
        var save = Ui.Button(T("ui.save_replay"), () => { });
        save.Pressed += () => save.Text = F("ui.replay_saved", ("file", SaveReplay()));
        col.AddChild(save);
        col.AddChild(Ui.Button(T("ui.back_to_menu"), Main.ShowMenu));
        AddChild(endPanel);
        endPanel.Position = board.Region.GetCenter() - new Vector2(130, 120);
        endPanel.CustomMinimumSize = new Vector2(260, 0);
        restart.GrabFocus();
        if (Screenshot is { } job) TakeScreenshot(job);
    }

    string SaveReplay()
    {
        System.IO.Directory.CreateDirectory(Settings.ReplayFolder);
        var name = $"{DateTime.Now:yyyy-MM-dd_HH-mm-ss}_{Session.Encounter.Id}_{Session.Seed}{Replay.Extension}";
        System.IO.File.WriteAllText(System.IO.Path.Combine(Settings.ReplayFolder, name), Session.ToReplay().ToJson());
        return name;
    }

    bool keysSent;

    /// <summary>Screenshot mode with --keys: press the keys at the hero's turn, as a player would, a few frames apart.
    /// Confirming an action plays on; the screenshot is taken at the next hero turn (or the end).</summary>
    async void PressKeys(ScreenshotJob job)
    {
        foreach (var name in job.Keys)
        {
            for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            var code = OS.FindKeycodeFromString(name);
            if (code == Key.None) { GD.PushError($"Unknown key {name}"); continue; }
            Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = true });
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            Input.ParseInputEvent(new InputEventKey { Keycode = code, PhysicalKeycode = code, Pressed = false });
        }
        for (var i = 0; i < 4; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (mode is Mode.ChooseAction or Mode.ChooseTarget) TakeScreenshot(job);
    }

    async void TakeScreenshot(ScreenshotJob job)
    {
        RefreshAll(0);
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);   // let slides and floats settle
        for (var i = 0; i < 3; i++)
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(job.Path)!);
        image.SavePng(job.Path);
        if (job.SaveReplay is { } replayPath) System.IO.File.WriteAllText(replayPath, Session.ToReplay().ToJson());
        if (job.SaveLog is { } logPath) System.IO.File.WriteAllText(logPath, log.GetParsedText());
        GD.Print($"Screenshot saved: {job.Path}");
        GetTree().Quit();
    }
}
