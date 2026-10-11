using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The exploration screen (Anchor: Exploration › Map screen): a header (the Map, which Map of the dungeon, Gold, the
/// journal's toggle), the Map under fog of war, and a footer (Camp, Hero Panel, the heroes, Options); the journal, the
/// Event panel, the Hero Panel and small popups open over the map. It drives a Core
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
    Label title = null!, progress = null!, purse = null!;
    Button journalButton = null!;
    PanelContainer journal = null!;
    Control overlayLayer = null!;
    EventPanel? eventPanel;
    HeroPanel? heroPanel;
    ColorRect? backdrop;
    Control? popup;

    /// <summary>The journal stays open or shut across screens (a battle replaces this one).</summary>
    static bool journalOpen;

    /// <summary>What the last action did, shown above the next story block (rolls, damage, rewards).</summary>
    List<string> lastLines = [];

    public RunScreen(Main main, DungeonRun run)
    {
        Main = main;
        Run = run;
    }

    /// <summary>The header, the footer (party), and the journal's width when open (it covers the right of the map).</summary>
    public const float TopHeight = 44, PartyHeight = 140, SideWidth = 360;

    /// <summary>Exploration action slots per hero (M3A: the belt; M3B: the first 4 exploration spells or items).</summary>
    public const int ActionSlots = 4;

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = Ui.Background, AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });

        // Header: the Map's name and which Map of the dungeon it is, then Gold and the journal's toggle.
        var top = new HBoxContainer { AnchorRight = 1, OffsetLeft = 12, OffsetRight = -12, OffsetTop = 4, OffsetBottom = TopHeight - 2 };
        top.AddThemeConstantOverride("separation", 14);
        AddChild(top);
        title = Ui.Label("", 20, Ui.Gold);
        top.AddChild(title);
        progress = Ui.Label("", 15, Ui.Dim);
        top.AddChild(progress);
        top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });
        purse = Ui.Label("", 15);
        top.AddChild(purse);
        journalButton = Ui.Button(T("ui.run_log"), ToggleJournal);
        journalButton.ToggleMode = true;
        top.AddChild(journalButton);
        foreach (var l in new[] { title, progress, purse }) l.SizeFlagsVertical = SizeFlags.ShrinkCenter;

        // The Map fills the middle.
        map = new MapView(this)
        {
            AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = 10, OffsetTop = TopHeight, OffsetRight = -10, OffsetBottom = -PartyHeight - 6,
        };
        AddChild(map);

        // The journal (the run log), over the right of the map while open.
        journal = new PanelContainer
        {
            AnchorLeft = 1, AnchorRight = 1, AnchorBottom = 1,
            OffsetLeft = -SideWidth - 10, OffsetRight = -10, OffsetTop = TopHeight, OffsetBottom = -PartyHeight - 6,
        };
        AddChild(journal);
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 6);
        journal.AddChild(side);
        side.AddChild(Ui.Label(T("ui.run_log"), 15, Ui.Gold));
        log = new RichTextLabel
        {
            ScrollFollowing = true, SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = true,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None,
        };
        log.AddThemeFontSizeOverride("normal_font_size", Ui.Px(12));
        side.AddChild(log);
        journal.Visible = journalButton.ButtonPressed = journalOpen;

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

    void ToggleJournal()
    {
        journalOpen = !journalOpen;
        journal.Visible = journalButton.ButtonPressed = journalOpen;
    }

    // ---- Acting ----

    /// <summary>Does a run action, saves, and shows what it did.</summary>
    public void Act(Func<RunResult> action)
    {
        RunResult r;
        try { r = action(); }
        catch (InvalidOperationException e)
        {
            Toast(e.Message);                                   // the screen offered something the rules refuse
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
        progress.Text = F("ui.map_progress", ("map", Run.MapIndex + 1), ("maps", Run.Dungeon.Maps.Count));
        purse.Text = F("ui.purse", ("gold", Run.Gold));
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

    // ---- The Map: what pressing a Node does ----

    public void PressNode(NodeState node)
    {
        if (Run.State != RunState.Exploring) return;
        if (Run.Eligible.Contains(node)) { Act(() => Run.Explore(node.Def.Id)); return; }
        if (node.Event == EventStatus.Deferred) { Act(() => Run.Resume(node.Def.Id)); return; }
        var usable = Run.Usable.Where(u => u.Node == node).ToList();
        if (usable.Count > 0) OpenInteractables(node, usable.Select(u => u.Interactable).ToList());
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

    /// <summary>Opens a small panel (one at a time) with a title; <paramref name="fill"/> adds its contents. Centred over
    /// the map, or above the footer's right end for the Options and Debug panels.</summary>
    VBoxContainer OpenPopup(string title, float width, bool atOptions = false)
    {
        ClosePopup();
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(width, 0) };
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        panel.AddChild(col);
        col.AddChild(Ui.Label(title, 18, Ui.Gold));
        popup = panel;
        Overlay(panel);
        Callable.From(() =>
        {
            var size = panel.GetCombinedMinimumSize();
            panel.Position = atOptions
                ? new Vector2(Size.X - size.X - 14, Size.Y - PartyHeight - size.Y - 10)
                : new Vector2((Size.X - size.X) / 2, TopHeight + (Size.Y - TopHeight - PartyHeight - size.Y) / 2);
        }).CallDeferred();
        return col;
    }

    // ---- Camp: confirm first ----

    public void ConfirmCamp()
    {
        if (Run.State != RunState.Exploring || Run.CampCharges <= 0) return;
        var col = OpenPopup(T("ui.camp_confirm_title"), 380);
        var text = Ui.Label(F("ui.camp_confirm_text", ("share", Math.Round(Main.Data.RunRules.CampRestore * 100)),
            ("count", Run.CampCharges), ("after", Run.CampCharges - 1)), 14);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        col.AddChild(text);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var yes = Ui.Button(T("ui.camp_confirm"), () => { ClosePopup(); Act(Run.Camp); });
        yes.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(yes);
        var no = Ui.Button(T("ui.cancel"), () => { ClosePopup(); FocusMap(); });
        no.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(no);
        col.AddChild(row);
        yes.GrabFocus();
    }

    // ---- Options: settings, debug tools, save and quit ----

    public void OpenOptions()
    {
        var col = OpenPopup(T("ui.options"), 300, atOptions: true);
        var auto = new CheckButton { Text = T("ui.auto_battle"), ButtonPressed = Main.Settings.AutoBattle, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        auto.Toggled += on => Main.Settings.AutoBattle = on;
        col.AddChild(auto);
        col.AddChild(Ui.Button(T("ui.debug"), OpenDebug));
        col.AddChild(Ui.Button(T("ui.save_and_quit"), Main.ShowMenu));
        col.AddChild(Ui.Button(T("ui.close"), () => { ClosePopup(); FocusMap(); }));
        auto.GrabFocus();
    }

    // ---- The Hero Panel ----

    public void OpenHeroPanel(Hero? hero = null)
    {
        if (Run.State != RunState.Exploring) return;
        ClosePopup();
        if (heroPanel is null)
        {
            // A dimmed backdrop: the panel is modal, and a click outside it closes it.
            backdrop = new ColorRect { Color = new Color(0, 0, 0, 0.6f), AnchorRight = 1, AnchorBottom = 1 };
            backdrop.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true }) CloseHeroPanel(); };
            Overlay(backdrop);
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
        backdrop?.QueueFree();
        backdrop = null;
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
        arrival.Position = new Vector2(Size.X / 2 - 240, Size.Y / 2 - 160);
        col.GetChild<Button>(2).GrabFocus();
    }

    // ---- Debug tools (M3A brief §8) ----

    void OpenDebug()
    {
        var col = OpenPopup(T("ui.debug"), 320, atOptions: true);
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
        col.AddChild(Ui.Button(T("ui.close"), () => { ClosePopup(); FocusMap(); }));
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
        label.Size = new Vector2(Size.X, 30);
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
