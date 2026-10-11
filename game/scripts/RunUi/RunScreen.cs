using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The exploration screen (M3A brief §7): the current Map under fog of war, the party bar, the actions (Camp, Next
/// Map, Hero Panel), the run log, and the Event panel and Hero Panel over the map. It drives a Core
/// <see cref="DungeonRun"/>: every action goes through <see cref="Act"/>, which saves the run and shows the result.
/// Like the battle screen, it only reads state and never changes it except through the run.
/// </summary>
public partial class RunScreen : Control
{
    public Main Main { get; }
    public DungeonRun Run { get; }
    public ScreenshotJob? Screenshot { get; set; }

    MapView map = null!;
    PartyBar party = null!;
    RichTextLabel log = null!;
    Label title = null!, purse = null!, info = null!;
    Button camp = null!, nextMap = null!, heroPanelButton = null!;
    Control overlayLayer = null!;
    EventPanel? eventPanel;
    HeroPanel? heroPanel;
    Control? popup;

    /// <summary>What the last action did, shown above the next story block (rolls, damage, rewards).</summary>
    List<string> lastLines = [];

    public RunScreen(Main main, DungeonRun run)
    {
        Main = main;
        Run = run;
    }

    public const float TopHeight = 44, PartyHeight = 118, SideWidth = 360;

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = Ui.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        // Top bar: the Map's name, then Gold, Camp charges and Nodes explored.
        var top = new HBoxContainer { AnchorRight = 1, OffsetLeft = 12, OffsetRight = -12, OffsetTop = 6, OffsetBottom = TopHeight };
        AddChild(top);
        title = Ui.Label("", 20, Ui.Gold);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        top.AddChild(title);
        purse = Ui.Label("", 15);
        top.AddChild(purse);

        // The Map.
        map = new MapView(this)
        {
            AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 10, OffsetTop = TopHeight, OffsetRight = -SideWidth - 10, OffsetBottom = -PartyHeight - 8,
        };
        AddChild(map);

        // Right: actions, what's focused, and the log.
        var side = new VBoxContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -SideWidth, OffsetRight = -10, OffsetTop = TopHeight, OffsetBottom = -PartyHeight - 8,
        };
        side.AddThemeConstantOverride("separation", 6);
        AddChild(side);
        camp = Ui.Button("", () => Act(Run.Camp));
        side.AddChild(camp);
        nextMap = Ui.Button(T("ui.next_map"), NextMap);
        side.AddChild(nextMap);
        heroPanelButton = Ui.Button(T("ui.hero_panel"), () => OpenHeroPanel());
        side.AddChild(heroPanelButton);
        var row = new HBoxContainer();
        row.AddChild(Ui.Button(T("ui.debug"), OpenDebug));
        var menu = Ui.Button(T("ui.save_and_quit"), Main.ShowMenu);
        menu.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(menu);
        side.AddChild(row);
        info = Ui.Label("", 13, Ui.Dim);
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.CustomMinimumSize = new Vector2(0, 54);
        side.AddChild(info);
        side.AddChild(Ui.Label(T("ui.run_log"), 13, Ui.Dim));
        log = new RichTextLabel
        {
            ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None,
        };
        log.AddThemeFontSizeOverride("normal_font_size", Ui.Px(12));
        side.AddChild(log);

        party = new PartyBar(this)
        {
            AnchorTop = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetTop = -PartyHeight, OffsetLeft = 10, OffsetRight = -10, OffsetBottom = -6,
        };
        AddChild(party);

        overlayLayer = new Control { MouseFilter = MouseFilterEnum.Ignore, AnchorRight = 1, AnchorBottom = 1 };
        AddChild(overlayLayer);

        foreach (var r in Run.Results)
            foreach (var line in RunLog.Lines(Main.Data, r))
                log.AddText(line + "\n");
        Refresh();
        if (!Run.Started) ShowArrival();
        if (Screenshot is { } job) TakeScreenshot(job);
    }

    // ---- Acting ----

    /// <summary>Does a run action, saves, and shows what it did.</summary>
    public void Act(Func<RunResult> action)
    {
        RunResult r;
        try { r = action(); }
        catch (InvalidOperationException e)
        {
            info.Text = e.Message;                              // the screen offered something the rules refuse
            return;
        }
        Main.SaveRun();
        Show(r);
    }

    /// <summary>Shows a result: its log lines, level-up and mastery announcements, then the screen for the new state.</summary>
    /// <param name="logged">The log already has it (a fresh screen logs the whole run as it opens).</param>
    public void Show(RunResult r, bool logged = false)
    {
        if (!logged)
            foreach (var line in RunLog.Lines(Main.Data, r))
                log.AddText(line + "\n");
        lastLines = [.. r.Outcomes.Where(o => o is not (StoryShown or EventStarted or NodeExplored or MapEntered or ChoiceMade))
            .SelectMany(o => RunLog.Lines(Main.Data, Single(o)))];
        foreach (var o in r.Outcomes)
        {
            if (o is LevelUp l) Toast(F("ui.toast_level", ("hero", l.Hero.Name), ("level", l.Level)));
            if (o is MasteryUnlocked m) Toast(F("ui.toast_mastery", ("hero", m.Hero.Name), ("mastery", m.Mastery.Name)));
        }
        if (Run.State == RunState.Battle || Run.Over)
        {
            Main.ShowRun();
            return;
        }
        Refresh();
    }

    static RunResult Single(RunOutcome o)
    {
        var r = new RunResult();
        r.Add(o);
        return r;
    }

    /// <summary>Redraws everything from the run's state and opens or closes the Event panel.</summary>
    public void Refresh()
    {
        title.Text = Run.Started ? Run.Map.Name : Run.Dungeon.Name;
        purse.Text = F("ui.purse", ("gold", Run.Gold), ("camps", Run.CampCharges), ("steps", Run.Steps));
        var exploring = Run.State == RunState.Exploring && Run.Started;
        camp.Text = F("ui.camp", ("count", Run.CampCharges));
        camp.Disabled = !exploring || Run.CampCharges <= 0;
        nextMap.Visible = Run.Usable.Any(u => u.Interactable.Kind == InteractableKind.Pathway);
        nextMap.Disabled = !exploring;
        heroPanelButton.Disabled = Run.State != RunState.Exploring;
        map.Refresh();
        party.Refresh();
        heroPanel?.Refresh();

        if (Run.State == RunState.Event)
        {
            if (eventPanel is null)
            {
                CloseHeroPanel(refocus: false);
                ClosePopup();
                eventPanel = new EventPanel(this);
                Overlay(eventPanel);
            }
            eventPanel.Show(lastLines);
        }
        else if (eventPanel is not null)
        {
            eventPanel.QueueFree();
            eventPanel = null;
            FocusMap();
        }
    }

    void Overlay(Control c) => overlayLayer.AddChild(c);

    public void FocusMap()
    {
        Callable.From(map.FocusFirst).CallDeferred();
    }

    public void Hint(string text) => info.Text = text;

    // ---- The Map: what pressing a Node does ----

    public void PressNode(NodeState node)
    {
        if (Run.State != RunState.Exploring) return;
        if (Run.Eligible.Contains(node)) { Act(() => Run.Explore(node.Def.Id)); return; }
        if (node.Event == EventStatus.Deferred) { Act(() => Run.Resume(node.Def.Id)); return; }
        var usable = Run.Usable.Where(u => u.Node == node).ToList();
        if (usable.Count > 0) OpenInteractables(node, usable.Select(u => u.Interactable).ToList());
    }

    void NextMap()
    {
        if (Run.Usable.FirstOrDefault(u => u.Interactable.Kind == InteractableKind.Pathway) is { Node: { } node })
            Act(() => Run.UseInteractable(node.Def.Id, InteractableKind.Pathway));
    }

    /// <summary>A small list of what the party can do at a Node: use its Sanctuary, take its Pathway, buy at its
    /// Alchemist Station.</summary>
    void OpenInteractables(NodeState node, List<InteractableState> things)
    {
        ClosePopup();
        var panel = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        col.AddChild(Ui.Label(node.Def.Name, 18, Ui.Gold));
        Button? first = null;
        void Add(Button b)
        {
            col.AddChild(b);
            first ??= b;
        }
        foreach (var thing in things)
        {
            switch (thing.Kind)
            {
                case InteractableKind.Sanctuary:
                    Add(Ui.Button(T("ui.use_sanctuary"), () => { ClosePopup(); Act(() => Run.UseInteractable(node.Def.Id, InteractableKind.Sanctuary)); }));
                    break;
                case InteractableKind.Pathway:
                    Add(Ui.Button(T("ui.take_pathway"), () => { ClosePopup(); Act(() => Run.UseInteractable(node.Def.Id, InteractableKind.Pathway)); }));
                    break;
                case InteractableKind.AlchemistStation:
                    col.AddChild(Ui.Label(T("interactable.alchemist_station"), 14, Ui.Dim));
                    foreach (var item in Main.Data.ItemList.Where(i => i.Price > 0))
                    {
                        var buy = Ui.Button(F("ui.buy", ("item", item.Name), ("price", item.Price)), () => { Act(() => Run.Buy(node.Def.Id, item.Id)); });
                        buy.Disabled = Run.Gold < item.Price;
                        Add(buy);
                    }
                    break;
            }
        }
        Add(Ui.Button(T("ui.close"), ClosePopup));
        popup = panel;
        Overlay(panel);
        panel.Position = map.GlobalPosition + map.Size / 2 - new Vector2(150, 90);
        panel.CustomMinimumSize = new Vector2(300, 0);
        first?.GrabFocus();
    }

    void ClosePopup()
    {
        popup?.QueueFree();
        popup = null;
    }

    // ---- The Hero Panel ----

    public void OpenHeroPanel(Hero? hero = null)
    {
        if (Run.State != RunState.Exploring) return;
        ClosePopup();
        if (heroPanel is null)
        {
            heroPanel = new HeroPanel(this, hero ?? Run.Heroes[0]);
            Overlay(heroPanel);
        }
        else if (hero is not null) heroPanel.Select(hero);
    }

    /// <param name="refocus">Give focus back (to Set out, or the Map); not when an Event is taking over.</param>
    public void CloseHeroPanel(bool refocus = true)
    {
        if (heroPanel is null) return;
        heroPanel.QueueFree();
        heroPanel = null;
        if (!refocus) return;
        if (arrivalGo is not null) arrivalGo.GrabFocus();     // points spent: set out next
        else FocusMap();
    }

    // ---- A new game: spend the first points, then set out ----

    PanelContainer? arrival;
    Button? arrivalGo;

    void ShowArrival()
    {
        arrival = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        arrival.AddChild(col);
        col.AddChild(Ui.Label(Run.Dungeon.Name, 24, Ui.Gold));
        var text = Ui.Label(T("ui.arrival"), 15);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(460, 0);
        col.AddChild(text);
        var go = Ui.Button(T("ui.set_out"), () =>
        {
            arrival?.QueueFree();
            arrival = null;
            arrivalGo = null;
            Act(Run.Start);
        });
        col.AddChild(Ui.Button(T("ui.spend_points"), () => OpenHeroPanel()));
        col.AddChild(go);
        arrivalGo = go;
        Overlay(arrival);
        arrival.Position = new Vector2((Size.X - SideWidth) / 2 - 240, Size.Y / 2 - 160);
        col.GetChild<Button>(2).GrabFocus();
    }

    // ---- Debug tools (M3A brief §8) ----

    void OpenDebug()
    {
        ClosePopup();
        var panel = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        col.AddChild(Ui.Label(T("ui.debug"), 18, Ui.Gold));
        var exploring = Run.State == RunState.Exploring && Run.Started;
        void Add(string key, Func<RunResult> act)
        {
            var b = Ui.Button(T(key), () => { ClosePopup(); Act(act); });
            b.Disabled = !exploring;
            col.AddChild(b);
        }
        Add("ui.debug_xp", () => Run.DebugXp(50));
        Add("ui.debug_heal", Run.DebugRestore);
        Add("ui.debug_gold", () => { Run.DebugSetGold(Run.Gold + 50); return new RunResult(); });
        Add("ui.debug_reveal", Run.DebugReveal);
        var auto = new CheckButton { Text = T("ui.auto_battle"), ButtonPressed = Main.Settings.AutoBattle, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        auto.Toggled += on => Main.Settings.AutoBattle = on;
        col.AddChild(auto);
        col.AddChild(Ui.Label(T("ui.debug_open_event"), 13, Ui.Dim));
        var events = new OptionButton { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        var nodes = Main.Data.Nodes.Values.Where(n => n.Event is not null && Run.Nodes[n.Id].Event == EventStatus.Pending).ToList();
        foreach (var n in nodes) events.AddItem($"{T(EventDef.NameKey(n.Event!))} ({n.Name})");
        col.AddChild(events);
        var open = Ui.Button(T("ui.debug_open"), () =>
        {
            if (events.Selected < 0) return;
            ClosePopup();
            Act(() => Run.DebugExplore(nodes[events.Selected].Id));
        });
        open.Disabled = !exploring || nodes.Count == 0;
        col.AddChild(open);
        col.AddChild(Ui.Button(T("ui.close"), ClosePopup));
        popup = panel;
        Overlay(panel);
        panel.Position = new Vector2(Size.X - SideWidth - 330, TopHeight + 10);
        panel.CustomMinimumSize = new Vector2(320, 0);
        col.GetChild<Button>(1).GrabFocus();
    }

    // ---- Announcements ----

    int toasts;

    void Toast(string text)
    {
        var label = Ui.Label(text, 20, Ui.Gold);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.HorizontalAlignment = HorizontalAlignment.Center;
        label.Position = new Vector2(0, TopHeight + 10 + 30 * (toasts++ % 4));
        label.Size = new Vector2(Size.X - SideWidth, 30);
        label.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(label);
        var t = label.CreateTween();
        t.TweenInterval(Screenshot is null ? 2.2 : 10);
        t.TweenProperty(label, "modulate:a", 0f, 0.6);
        t.TweenCallback(Callable.From(() => { label.QueueFree(); toasts--; }));
    }

    // ---- Input ----

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("ui_cancel")) return;
        if (popup is not null) { ClosePopup(); FocusMap(); }
        else if (heroPanel is not null) CloseHeroPanel();
        AcceptEvent();
    }

    // ---- Screenshot mode ----

    async void TakeScreenshot(ScreenshotJob job)
    {
        if (job.Panel == "hero") OpenHeroPanel(Run.Heroes.FirstOrDefault(h => h.SkillPoints > 0) ?? Run.Heroes[0]);
        await ToSignal(GetTree().CreateTimer(0.5), SceneTreeTimer.SignalName.Timeout);
        for (var i = 0; i < 3; i++)
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var image = GetViewport().GetTexture().GetImage();
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(job.Path)!);
        image.SavePng(job.Path);
        GD.Print($"Screenshot saved: {job.Path}");
        GetTree().Quit();
    }
}
