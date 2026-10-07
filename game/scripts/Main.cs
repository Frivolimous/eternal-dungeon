using EternalDungeon.Core;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Game.BattleUi;
using Godot;

namespace EternalDungeon.Game;

/// <summary>
/// The app root: loads the data and text, registers the text with Godot's translation system, applies settings, and
/// switches between the debug menu and the battle screen. With <c>--screenshot</c> on the command line it plays a
/// battle at instant speed, saves a PNG and quits (M2 brief §9).
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

    public void StartBattle(string encounterId, ulong seed, Replay? replay = null, BoardLayout? layout = null)
    {
        var encounter = Data.Encounters[encounterId];
        var session = new BattleSession(Data, encounter, seed, replay?.Choices) { AutoBattle = Settings.AutoBattle };
        var battle = new BattleScreen(this, session, layout ?? encounter.Layout);
        if (screenshot is { } job) battle.Screenshot = job;
        Show(battle);
    }
}
