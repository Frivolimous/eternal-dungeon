using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The Event screen (M3A brief §7), over the map: the Event's name, the Active Hero's portrait, what the last choice
/// did (rolls, Health, rewards), the story text, and the choices. A choice that needs a trait or class says which hero
/// takes it; its decision preview (chance, fight, costs, buffs) shows on hover or focus. With no choices, Continue.
/// Number keys pick a choice.
/// </summary>
public partial class EventPanel : PanelContainer
{
    readonly RunScreen screen;
    VBoxContainer col = null!;
    readonly List<Button> choiceButtons = [];
    Label preview = null!;

    public EventPanel(RunScreen screen) => this.screen = screen;

    DungeonRun Run => screen.Run;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(Ui.Panel, 0.98f), Ui.Gold));
        col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        AddChild(col);
        Place();
        screen.Resized += Place;
    }

    public override void _ExitTree() => screen.Resized -= Place;

    void Place()
    {
        var width = Math.Min(780, screen.Size.X - RunScreen.SideWidth - 60);
        CustomMinimumSize = new Vector2(width, 0);
        Size = new Vector2(width, 0);
        Position = new Vector2(30 + (screen.Size.X - RunScreen.SideWidth - 40 - width) / 2, RunScreen.TopHeight + 16);
    }

    /// <summary>Rebuilds the panel for the current story block, with what the last action did above it.</summary>
    public void Show(IReadOnlyList<string> lastLines)
    {
        foreach (var c in col.GetChildren()) c.QueueFree();
        choiceButtons.Clear();
        if (Run.Event is not { Block: StoryBlock block } e) return;

        col.AddChild(Ui.Label(T(EventDef.NameKey(e.Def.Id)), 22, Ui.Gold));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 14);
        col.AddChild(row);
        row.AddChild(new Portrait(screen, e.Active, block.Image) { CustomMinimumSize = new Vector2(132, 150) });
        var body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 8);
        row.AddChild(body);
        if (lastLines.Count > 0)
        {
            var outcome = Ui.Label(string.Join("\n", lastLines), 13, Ui.Dim);
            outcome.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            body.AddChild(outcome);
        }
        var story = Ui.Label(RunLog.StoryText(Text.Strings, e.Def, block, e.Active), 16);
        story.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        story.CustomMinimumSize = new Vector2(300, 0);
        body.AddChild(story);

        var previews = Run.Previews;
        if (previews.Count == 0)
        {
            var go = Ui.Button(T("ui.continue"), () => screen.Act(Run.Continue));
            col.AddChild(go);
            go.GrabFocus();
            return;
        }
        var i = 0;
        foreach (var p in previews)
        {
            var b = Ui.Button($"{++i}. {RunLog.ChoiceText(Text.Strings, e.Def, block, p.Choice)}{Unlocked(p)}", () => screen.Act(() => Run.Choose(p.Choice.Id)));
            b.Alignment = HorizontalAlignment.Left;
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            var text = RunLog.PreviewText(screen.Main.Data, p);
            b.FocusEntered += () => preview.Text = text;
            b.MouseEntered += () => preview.Text = text;
            col.AddChild(b);
            choiceButtons.Add(b);
        }
        preview = Ui.Label("", 13, Ui.Gold);
        preview.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        preview.CustomMinimumSize = new Vector2(0, 36);
        col.AddChild(preview);
        choiceButtons[0].GrabFocus();
    }

    /// <summary>"  (Rogue · Disable 1)" for a trait choice, "  (Warrior)" for a class choice; nothing for an open one.</summary>
    string Unlocked(ChoicePreview p)
    {
        if (p.Heroes.Count == 0) return "";
        var hero = p.Heroes.Contains(Run.Event!.Active) ? Run.Event.Active : p.Heroes[0];
        var traits = p.Choice.HeroTraits;
        if (p.Choice.Conditions.Any(c => c is TraitCondition) && traits.Count > 0)
        {
            var best = traits.OrderByDescending(hero.Trait).First();
            return F("ui.choice_by_trait", ("hero", hero.Name), ("trait", screen.Main.Data.Stats[best].Name), ("level", hero.Trait(best)));
        }
        if (p.Choice.HeroClass is { } cls)
            return F("ui.choice_by_class", ("hero", hero.Name), ("class", screen.Main.Data.Classes[cls].Name));
        return "";
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } key && key.Keycode is >= Key.Key1 and <= Key.Key9)
        {
            var n = (int)(key.Keycode - Key.Key1);
            if (n < choiceButtons.Count)
            {
                choiceButtons[n].EmitSignal(BaseButton.SignalName.Pressed);
                AcceptEvent();
            }
        }
    }
}

/// <summary>The Active Hero's portrait under the Event's picture (each falls back to a placeholder).</summary>
public partial class Portrait(RunScreen screen, Hero hero, string? image) : Control
{
    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, new Vector2(Size.X, Size.X));
        if (image is not null && screen.Main.Art.Get($"event_{image}") is { } art)
            DrawTextureRect(art, r, false);
        else if (screen.Main.Art.Portrait(hero.Unit.Id) is { } tex)
            DrawTextureRect(tex, r, false);
        else
        {
            DrawRect(r, Ui.Party.Darkened(0.45f));
            DrawString(font, r.Position + new Vector2(0, r.Size.Y * 0.62f), hero.Name[..1], HorizontalAlignment.Center, r.Size.X, Ui.Px(48), Ui.Ink);
        }
        DrawRect(r, Ui.Gold, false, 2);
        DrawString(font, new Vector2(0, Size.X + 16), hero.Name, HorizontalAlignment.Center, Size.X, Ui.Px(13), Ui.Ink);
    }
}
