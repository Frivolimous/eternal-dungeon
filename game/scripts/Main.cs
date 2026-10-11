using EternalDungeon.Core;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using EternalDungeon.Game.BattleUi;
using Godot;

namespace EternalDungeon.Game;

/// <summary>
/// The app root: loads the data and text, registers the text with Godot's translation system, applies settings, and
/// switches between the debug menu and the battle screen. With <c>--screenshot</c> on the command line it plays a
/// battle at instant speed, saves a PNG and quits (M2 brief Â§9).
/// </summary>
public partial class Main : Control
{
    public GameData Data { get; private set; } = null!;
    public Settings Settings { get; } = new();
    public Art Art { get; private set; } = null!;

    Control? screen;
    ScreenshotJob? screenshot;

    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(Ui.Background);
        try
        {
            Data = DataLoader.Load(DataSource.FromAssembly(typeof(Main).Assembly));
        }
        catch (DataException e)
        {
            ShowFatal($"{GameInfo.Title}\n{e.Message}");
            GD.PushError(e.Message);
            return;
        }
        var args = OS.GetCmdlineUserArgs();
        if (Array.IndexOf(args, "--resize-art") is var at and >= 0)
        {
            if (at + 2 < args.Length) ArtResizer.Run(Data, args[at + 1], args[at + 2]);
            else GD.PrintErr("Usage: --resize-art <masters folder> <style>");
            GetTree().Quit();
            return;
        }
        Text.Use(Data.Text);
        var translation = new Translation { Locale = Data.Text.Language };
        foreach (var (key, value) in Data.Text.All)
            translation.AddMessage(key, value);
        TranslationServer.AddTranslation(translation);
        TranslationServer.SetLocale(Data.Text.Language);

        Art = new Art(Settings);
        Theme = Ui.MakeTheme(Settings.TextScale);
        screenshot = ScreenshotJob.FromCommandLine(OS.GetCmdlineUserArgs());
        if (screenshot is { } job)
        {
            Settings.Speed = BattleSpeed.Instant;
            Settings.AutoBattle = true;
            if (job.Style is { } style) Settings.Style = style;
            if (job.Run is { } dungeon)
            {
                // A dungeon run: auto-play the steps headless, then show where it stands.
                autopilot = job.Autopilot;
                Run = new DungeonRun(Data, dungeon, job.Seed);
                var policy = new RunPolicy();
                if (job.Steps > 0) policy.Play(Run, maxActions: 0);
                for (var i = 1; i < job.Steps && !Run.Over; i++)
                    if (!policy.Step(Run)) break;
                // A panel shows between Events: play on to the next moment the party is exploring.
                while (job.Panel is not null && Run.Started && Run.State != RunState.Exploring && !Run.Over && policy.Step(Run)) { }
                ShowRun();
            }
            else
                StartBattle(job.Encounter, job.Seed, job.Replay, job.Layout);
        }
        else
            ShowMenu();
    }

    void ShowFatal(string message)
    {
        var label = new Label { Text = message, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        label.SetAnchorsPreset(LayoutPreset.FullRect);
        label.Modulate = Colors.OrangeRed;
        AddChild(label);
    }

    void Show(Control next)
    {
        screen?.QueueFree();
        screen = next;
        next.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(next);
    }

    public void ShowMenu() => Show(new Menu(this));

    public void ApplyTextScale() => Theme = Ui.MakeTheme(Settings.TextScale);

    // ---- Dungeon runs (M3A) ----

    /// <summary>The dungeon run in progress, if any.</summary>
    public DungeonRun? Run { get; private set; }

    /// <summary>Seconds played in this run (saved with it, for the end screen).</summary>
    public double RunSeconds { get; private set; }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey or InputEventJoypadButton { Pressed: true }) Ui.KeyboardMode = true;
        else if (e is InputEventMouseButton || (e is InputEventMouseMotion m && m.Relative.LengthSquared() > 4)) Ui.KeyboardMode = false;
    }

    public override void _Process(double delta)
    {
        if (Run is { Over: false }) RunSeconds += delta;
        if (autopilot > 0 && (autopilotTimer += delta) > 0.2)
        {
            autopilotTimer = 0;
            Autopilot();
        }
    }

    // ---- Screenshot mode's autopilot: press the focused button, as a keyboard player would ----

    int autopilot;
    double autopilotTimer;

    /// <summary>The screens get the screenshot job only when they should shoot it themselves (not under autopilot).</summary>
    ScreenshotJob? ShotFor => autopilot > 0 ? null : screenshot;

    void Autopilot()
    {
        if (screen is RunUi.EndScreen)                         // the run is over: show its end
        {
            autopilot = 0;
            ShootNow(screenshot!);
            return;
        }
        var focus = GetViewport().GuiGetFocusOwner();
        // The Hero Panel is browsing, not playing: close it.
        if (focus is not null && FindParent<RunUi.HeroPanel>(focus) is { } panel)
            focus = panel.FindChild("close", true, false) as Control;
        if (focus is BaseButton { Disabled: false } b)
        {
            b.EmitSignal(BaseButton.SignalName.Pressed);
            autopilot--;
        }
        else if (screen is RunUi.RunScreen rs) rs.FocusMap(always: true);
        if (autopilot == 0) ShootNow(screenshot!);
    }

    static T? FindParent<T>(Node node) where T : Node
    {
        for (var n = node; n is not null; n = n.GetParent())
            if (n is T t) return t;
        return null;
    }

    async void ShootNow(ScreenshotJob job)
    {
        await ToSignal(GetTree().CreateTimer(0.6), SceneTreeTimer.SignalName.Timeout);
        for (var i = 0; i < 3; i++)
            await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(job.Path)!);
        GetViewport().GetTexture().GetImage().SavePng(job.Path);
        GD.Print($"Screenshot saved: {job.Path} ({Run?.Commands.Count ?? 0} run actions)");
        GetTree().Quit();
    }

    /// <summary>The one save slot, in the user folder.</summary>
    public static string SavePath => ProjectSettings.GlobalizePath("user://" + SaveGame.FileName);

    public static bool HasSave => System.IO.File.Exists(SavePath);

    /// <summary>A new game: the preset party at the start of Dungeon 0, with its first skill points to spend.</summary>
    public void NewRun(ulong? seed = null, string dungeon = "dungeon_0")
    {
        Run = new DungeonRun(Data, dungeon, seed ?? (ulong)Random.Shared.NextInt64(1, long.MaxValue));
        RunSeconds = 0;
        SaveRun();
        ShowRun();
    }

    /// <summary>Restores the saved run (callers check <see cref="SaveGame.DataChanged"/> first and warn).</summary>
    public void LoadRun(SaveGame save)
    {
        Run = save.Restore(Data);
        RunSeconds = save.Seconds;
        ShowRun();
    }

    /// <summary>Writes the save: called after every run action, at every battle's start and at each hero decision.</summary>
    public void SaveRun()
    {
        if (Run is null || screenshot is not null) return;
        var save = SaveGame.Of(Run) with { Seconds = (int)RunSeconds };
        System.IO.File.WriteAllText(SavePath, save.ToJson());
    }

    /// <summary>The run's screen for its state: the battle if one is on, the end screen once it's over, else the map.</summary>
    public void ShowRun()
    {
        if (Run is null) { ShowMenu(); return; }
        if (Run.Battle is { } fight)
        {
            var battle = new BattleScreen(this, fight.Session, fight.Session.Encounter.Layout)
            {
                Checkpoint = SaveRun,
                Finished = FinishRunBattle,
            };
            fight.Session.AutoBattle = fight.Session.AutoBattle || Settings.AutoBattle;
            if (ShotFor is { } job) battle.Screenshot = job;
            Show(battle);
            return;
        }
        Show(Run.Over ? new RunUi.EndScreen(this, Run) : new RunUi.RunScreen(this, Run) { Screenshot = ShotFor });
    }

    /// <summary>A run battle ended: its result goes back to the run (and its Event goes on).</summary>
    void FinishRunBattle()
    {
        var result = Run!.FinishBattle();
        SaveRun();
        ShowRun();
        if (screen is RunUi.RunScreen rs) rs.Show(result, logged: true);
    }

    public void StartBattle(string encounterId, ulong seed, Replay? replay = null, BoardLayout? layout = null)
    {
        var encounter = Data.Encounters[encounterId];
        var session = new BattleSession(Data, encounter, seed, replay?.Choices) { AutoBattle = Settings.AutoBattle };
        var battle = new BattleScreen(this, session, layout ?? encounter.Layout);
        if (screenshot is { } job) battle.Screenshot = job;
        Show(battle);
    }
}
