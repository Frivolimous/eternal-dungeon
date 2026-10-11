using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game;

/// <summary>
/// The debug menu (M2 brief §9): pick an encounter and a seed and start, or play a saved replay. Also the settings
/// that matter for the checkpoint: auto-battle, speed, text size, art style and board layout.
/// </summary>
public partial class Menu : Control
{
    readonly Main main;
    ItemList encounters = null!;
    LineEdit seed = null!;
    OptionButton layout = null!;
    ItemList replays = null!;
    List<string> replayFiles = [];

    static ulong lastSeed = 1;
    static int lastEncounter;

    public Menu(Main main) => this.main = main;

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = Ui.Background, AnchorRight = 1, AnchorBottom = 1 });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1 };
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(760, 0) };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        panel.AddChild(col);

        col.AddChild(Ui.Label(T("ui.title"), 32, Ui.Gold));
        col.AddChild(Ui.Label(T("ui.debug_menu"), 14, Ui.Dim));
        col.AddChild(GameRow());

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 16);
        col.AddChild(row);

        // Left: encounter and seed.
        var left = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(left);
        left.AddChild(Ui.Label(T("ui.encounter")));
        encounters = new ItemList { CustomMinimumSize = new Vector2(0, 150), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        foreach (var e in main.Data.EncounterList)
            encounters.AddItem(e.Name);
        encounters.Select(Math.Min(lastEncounter, main.Data.EncounterList.Count - 1));
        encounters.ItemActivated += _ => Start();
        left.AddChild(encounters);

        var seedRow = new HBoxContainer();
        seedRow.AddChild(Ui.Label(T("ui.seed")));
        seed = new LineEdit { Text = lastSeed.ToString(), CustomMinimumSize = new Vector2(120, 0), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        seed.TextSubmitted += _ => Start();
        seedRow.AddChild(seed);
        seedRow.AddChild(Ui.Button(T("ui.random_seed"), () => seed.Text = (Random.Shared.NextInt64(1, 100000)).ToString()));
        left.AddChild(seedRow);

        var start = Ui.Button(T("ui.start"), Start);
        start.CustomMinimumSize = new Vector2(0, 44);
        left.AddChild(start);

        // Right: settings.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(right);
        var auto = new CheckButton { Text = T("ui.auto_battle"), ButtonPressed = main.Settings.AutoBattle, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        auto.Toggled += on => main.Settings.AutoBattle = on;
        right.AddChild(auto);

        right.AddChild(Option(T("ui.speed"), [.. Enum.GetValues<BattleSpeed>().Select(s => T(Settings.SpeedKey(s)))],
            (int)main.Settings.Speed, i => main.Settings.Speed = (BattleSpeed)i));
        right.AddChild(Option(T("ui.text_size"), [.. Settings.TextScales.Select(s => $"{s * 100:0}%")],
            Array.IndexOf(Settings.TextScales, main.Settings.TextScale), i =>
            {
                main.Settings.TextScale = Settings.TextScales[i];
                main.ApplyTextScale();
                main.ShowMenu();
            }));
        var styles = Art.Styles();
        right.AddChild(Option(T("ui.art_style"), [T("ui.style_placeholder"), .. styles],
            styles.IndexOf(main.Settings.Style) + 1, i =>
            {
                main.Settings.Style = i == 0 ? "" : styles[i - 1];
                main.Art.Clear();
            }));
        var layoutRow = Option(T("ui.layout"), [T("ui.layout_encounter"), T("ui.layout_vertical"), T("ui.layout_side_on")], 0, _ => { });
        layout = layoutRow.GetChild<OptionButton>(1);
        right.AddChild(layoutRow);

        right.AddChild(Ui.Label(T("ui.replays")));
        replays = new ItemList { CustomMinimumSize = new Vector2(0, 90), AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        replays.ItemActivated += i => PlayReplay((int)i);
        right.AddChild(replays);
        LoadReplayList();

        col.AddChild(Ui.Label(T("ui.menu_help"), 13, Ui.Dim));
        encounters.GrabFocus();
    }

    // ---- Dungeon 0 (M3A) ----

    Button? continueButton;

    /// <summary>New game, Continue (the one save slot), and the debug start: Dungeon 0 at any Node.</summary>
    Control GameRow()
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        var newGame = Ui.Button(T("ui.new_game"), () => main.NewRun());
        newGame.CustomMinimumSize = new Vector2(150, 44);
        row.AddChild(newGame);
        continueButton = Ui.Button(T("ui.continue_game"), Continue);
        continueButton.CustomMinimumSize = new Vector2(150, 44);
        continueButton.Disabled = !Main.HasSave;
        row.AddChild(continueButton);
        row.AddChild(Ui.Label(T("ui.start_at"), 14, Ui.Dim));
        var nodes = main.Data.DungeonList.SelectMany(d => d.Maps).SelectMany(m => m.Nodes).ToList();
        var pick = new OptionButton { AutoTranslateMode = AutoTranslateModeEnum.Disabled, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var n in nodes)
            pick.AddItem(n.Event is { } e ? $"{n.Name}: {T(EventDef.NameKey(e))}" : n.Name);
        row.AddChild(pick);
        row.AddChild(Ui.Button(T("ui.debug_open"), () =>
        {
            if (pick.Selected < 0) return;
            var node = nodes[pick.Selected];
            main.NewRun(dungeon: main.Data.DungeonList.First(d => d.Maps.Any(m => m.Id == node.Map)).Id);
            main.Run!.DebugExplore(node.Id);
            main.SaveRun();
            main.ShowRun();
        }));
        return row;
    }

    void Continue()
    {
        SaveGame save;
        try { save = SaveGame.Parse(System.IO.File.ReadAllText(Main.SavePath)); }
        catch (Exception e) when (e is SaveException or System.IO.IOException)
        {
            Problem(e.Message);
            return;
        }
        if (save.DataChanged(main.Data))
        {
            Problem(T("ui.save_other_data"), () => Load(save));
            return;
        }
        Load(save);
    }

    void Load(SaveGame save)
    {
        try { main.LoadRun(save); }
        catch (SaveException e) { Problem(e.Message); }
    }

    /// <summary>A save that can't be loaded as it is: say why, and offer a new game (or loading anyway).</summary>
    void Problem(string message, Action? loadAnyway = null)
    {
        var panel = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        panel.AddChild(col);
        var text = Ui.Label(message, 15);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.CustomMinimumSize = new Vector2(420, 0);
        col.AddChild(text);
        var restart = Ui.Button(T("ui.new_game"), () => main.NewRun());
        col.AddChild(restart);
        if (loadAnyway is not null) col.AddChild(Ui.Button(T("ui.load_anyway"), loadAnyway));
        col.AddChild(Ui.Button(T("ui.close"), panel.QueueFree));
        AddChild(panel);
        panel.Position = Size / 2 - new Vector2(220, 80);
        restart.GrabFocus();
    }

    static HBoxContainer Option(string label, string[] items, int selected, Action<int> changed)
    {
        var row = new HBoxContainer();
        var l = Ui.Label(label);
        l.CustomMinimumSize = new Vector2(120, 0);
        row.AddChild(l);
        var o = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, AutoTranslateMode = AutoTranslateModeEnum.Disabled };
        foreach (var item in items) o.AddItem(item);
        o.Selected = Math.Max(0, selected);
        o.ItemSelected += i => changed((int)i);
        row.AddChild(o);
        return row;
    }

    BoardLayout? Layout => layout.Selected switch
    {
        1 => BoardLayout.Vertical,
        2 => BoardLayout.SideOn,
        _ => null,
    };

    void Start()
    {
        var selected = encounters.GetSelectedItems();
        if (selected.Length == 0) return;
        if (!ulong.TryParse(seed.Text.Trim(), out var s))
        {
            seed.Text = lastSeed.ToString();
            return;
        }
        lastSeed = s;
        lastEncounter = selected[0];
        main.StartBattle(main.Data.EncounterList[selected[0]].Id, s, layout: Layout);
    }

    void LoadReplayList()
    {
        replays.Clear();
        replayFiles = System.IO.Directory.Exists(Settings.ReplayFolder)
            ? [.. System.IO.Directory.GetFiles(Settings.ReplayFolder, "*" + Replay.Extension).OrderByDescending(f => f)]
            : [];
        foreach (var f in replayFiles)
            replays.AddItem(System.IO.Path.GetFileName(f));
        if (replayFiles.Count == 0) replays.AddItem(T("ui.no_replays"), selectable: false);
    }

    void PlayReplay(int index)
    {
        if (index >= replayFiles.Count) return;
        var replay = Replay.Parse(System.IO.File.ReadAllText(replayFiles[index]), System.IO.Path.GetFileName(replayFiles[index]));
        main.StartBattle(replay.Encounter, replay.Seed, replay, Layout);
    }
}
