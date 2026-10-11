using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The Event screen (Anchor: Exploration › Event screen), over the map, which it darkens (the header and the party stay
/// lit: Camp is off, the Hero Panel opens): the Event's name, its banner picture, what the last choice did (in italics),
/// the story text, and the choices as lines of text (gold; white when hovered or focused), a trait or class choice with
/// a grey line under it naming its trait and hero. Pointing at a choice focuses it (so one line is white at a time);
/// the first choice is focused at once only for keyboard and controller players. A focused choice shows its preview
/// beside it and lights up the hero who would take it in the party bar. With no choices, a Continue line. Number keys pick.
/// </summary>
public partial class EventPanel : Control
{
    readonly RunScreen screen;
    VBoxContainer col = null!;
    readonly List<Button> choiceButtons = [];
    readonly PanelContainer popup = new() { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
    readonly Label popupText = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };

    public const float Width = Core.Assets.ArtCatalog.EventBannerWidth;

    public EventPanel(RunScreen screen)
    {
        this.screen = screen;
        AnchorRight = 1;
        AnchorBottom = 1;
        OffsetTop = RunScreen.TopHeight;
        OffsetBottom = -RunScreen.PartyHeight - 6;
    }

    DungeonRun Run => screen.Run;

    public override void _Ready()
    {
        AddChild(new ColorRect { Color = new Color(0, 0, 0, 0.8f), AnchorRight = 1, AnchorBottom = 1 });
        var scroll = new ScrollContainer
        {
            AnchorBottom = 1, OffsetLeft = 40, OffsetRight = 40 + Width + 14, OffsetTop = 14, OffsetBottom = -8,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        AddChild(scroll);
        col = new VBoxContainer { CustomMinimumSize = new Vector2(Width, 0) };
        col.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(col);

        popup.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.13f, 0.12f, 0.17f, 0.98f), Ui.Gold, 2, 10));
        popupText.AddThemeFontSizeOverride("font_size", Ui.Px(15));
        popupText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        popupText.CustomMinimumSize = new Vector2(280, 0);
        popup.AddChild(popupText);
        AddChild(popup);
    }

    /// <summary>Rebuilds the panel for the current story block, with what the last choice did above its text.</summary>
    /// <param name="focus">Focus the first choice for a keyboard or controller player (not while the Hero Panel is open
    /// over the Event).</param>
    public void Show(IReadOnlyList<string> lastLines, bool focus = true)
    {
        foreach (var c in col.GetChildren()) c.QueueFree();
        choiceButtons.Clear();
        HidePopup();
        if (Run.Event is not { Block: StoryBlock block } e) return;

        col.AddChild(Ui.Label(T(EventDef.NameKey(e.Def.Id)), 22, Ui.Gold));
        col.AddChild(new Banner(screen, Picture(e.Def, block)));

        var text = new VBoxContainer();
        text.AddThemeConstantOverride("separation", 6);
        if (lastLines.Count > 0)
        {
            var outcome = Ui.Label(string.Join("\n", lastLines), 13, Ui.Dim);
            outcome.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            outcome.AddThemeFontOverride("font", Italic(outcome));
            text.AddChild(outcome);
        }
        var story = Ui.Label(RunLog.StoryText(Text.Strings, e.Def, block, e.Active), 16);
        story.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        text.AddChild(story);
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.1f, 0.09f, 0.13f, 0.95f), Colors.Transparent, 0, 0));
        box.AddChild(text);
        col.AddChild(box);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        var previews = Run.Previews;
        if (previews.Count == 0)
        {
            var go = Line(T("ui.continue"), () => screen.Act(Run.Continue));
            go.FocusEntered += () => screen.HighlightHeroes(null);
            col.AddChild(go);
            choiceButtons.Add(go);
        }
        foreach (var p in previews)
        {
            var b = Line(RunLog.ChoiceText(Text.Strings, e.Def, block, p.Choice), () => screen.Act(() => Run.Choose(p.Choice.Id)));
            var takers = Takers(p);
            var popupLines = PopupText(p, takers);
            b.FocusEntered += () => Hover(b, popupLines, takers);
            b.FocusExited += Unhover;
            var group = new VBoxContainer();
            group.AddThemeConstantOverride("separation", -2);
            group.AddChild(b);
            if (Specialist(p, takers) is { } sub) group.AddChild(Ui.Label(sub, 12, Ui.Dim));
            col.AddChild(group);
            choiceButtons.Add(b);
        }
        // Keyboard and controller players get the first choice focused; with the mouse, nothing is lit until pointed at.
        if (focus && Ui.KeyboardMode) choiceButtons[0].GrabFocus();
    }

    public void FocusFirst()
    {
        if (choiceButtons.Count > 0) choiceButtons[0].GrabFocus();
    }

    /// <summary>Focus the first choice for a keyboard or controller player (back from the Hero Panel, say).</summary>
    public void FocusIfKeyboard()
    {
        if (Ui.KeyboardMode) FocusFirst();
    }

    /// <summary>The banner: the block's own picture, else the latest one before it in the Event, else its first.</summary>
    static string? Picture(EventDef def, StoryBlock block)
    {
        if (block.Image is { } own) return own;
        var stories = def.Blocks.OfType<StoryBlock>().ToList();
        var at = stories.IndexOf(block);
        return stories.Take(at).LastOrDefault(s => s.Image is not null)?.Image ?? stories.FirstOrDefault(s => s.Image is not null)?.Image;
    }

    /// <summary>A choice as a line of text: gold, white when hovered or focused.</summary>
    static Button Line(string text, Action pressed)
    {
        var b = Ui.Button(text, pressed);
        // The pointer moves the focus, so one line is white at a time (and its preview and hero follow).
        b.MouseEntered += () => b.GrabFocus();
        b.MouseExited += () => { if (b.HasFocus()) b.ReleaseFocus(); };
        b.Alignment = HorizontalAlignment.Left;
        b.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        b.CustomMinimumSize = new Vector2(Width, 0);
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled", "focus", "hover_pressed" })
            b.AddThemeStyleboxOverride(style, new StyleBoxEmpty { ContentMarginTop = 2, ContentMarginBottom = 2 });
        b.AddThemeFontSizeOverride("font_size", Ui.Px(17));
        b.AddThemeColorOverride("font_color", Ui.Gold);
        foreach (var state in new[] { "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            b.AddThemeColorOverride(state, Colors.White);
        return b;
    }

    static FontVariation Italic(Control c) =>
        new() { BaseFont = c.GetThemeDefaultFont(), VariationTransform = new Transform2D(1, 0, 0.2f, 1, 0, 0) };

    // ---- Who takes a choice, and its preview ----

    /// <summary>The heroes who could take a choice: the Active Hero if it's among the best placed, else all of them (a
    /// tie is broken at random when it's taken).</summary>
    List<Hero> Takers(ChoicePreview p) =>
        p.Heroes.Contains(Run.Event!.Active) ? [Run.Event.Active] : [.. p.Heroes];

    static string Names(List<Hero> heroes) => string.Join(T("ui.or"), heroes.Select(h => h.Name));

    /// <summary>The grey line under a trait or class choice: its trait (or class) and who takes it.</summary>
    string? Specialist(ChoicePreview p, List<Hero> takers)
    {
        if (takers.Count == 0) return null;
        var hero = takers[0];
        if (p.Choice.Conditions.Any(c => c is TraitCondition) && p.Choice.HeroTraits.Count > 0)
        {
            var best = p.Choice.HeroTraits.OrderByDescending(hero.Trait).First();
            return F("ui.choice_trait", ("trait", screen.Main.Data.Stats[best].Name), ("level", hero.Trait(best)), ("hero", Names(takers)));
        }
        if (p.Choice.HeroClass is { } cls)
        {
            var name = screen.Main.Data.Classes[cls].Name;
            var who = Names(takers);
            return who == name ? name : F("ui.choice_class", ("class", name), ("hero", who));
        }
        return null;
    }

    /// <summary>The popup: who rolls (for a roll), then the whole decision preview.</summary>
    string PopupText(ChoicePreview p, List<Hero> takers)
    {
        var preview = RunLog.PreviewText(screen.Main.Data, p);
        return p.Chance is not null && takers.Count > 0 ? F("ui.choice_roller", ("hero", Names(takers))) + "\n" + preview : preview;
    }

    void Hover(Button b, string text, List<Hero> takers)
    {
        screen.HighlightHeroes(takers);
        if (text.Length == 0) { HidePopup(); return; }
        popupText.Text = text;
        popup.Visible = true;
        popup.ResetSize();
        hovered = b;
        PlacePopup();
    }

    void Unhover()
    {
        HidePopup();
        screen.HighlightHeroes(null);
    }

    Button? hovered;

    /// <summary>Beside its choice, every frame while shown: the choices settle a frame after the panel is built, and can scroll.</summary>
    public override void _Process(double delta)
    {
        if (popup.Visible) PlacePopup();
    }

    void PlacePopup()
    {
        if (hovered is null || !IsInstanceValid(hovered)) return;
        var size = popup.GetCombinedMinimumSize();
        var rect = hovered.GetGlobalRect();
        var x = GlobalPosition.X + 40 + Width + 24;
        var y = Mathf.Clamp(rect.GetCenter().Y - size.Y / 2, GlobalPosition.Y + 8, GlobalPosition.Y + Size.Y - size.Y - 8);
        popup.GlobalPosition = new Vector2(x, y);
    }

    void HidePopup() => popup.Visible = false;

    public override void _UnhandledInput(InputEvent e)
    {
        if (screen.HeroPanelOpen) return;                        // keys belong to the panel on top
        // Nothing focused (the mouse was in use): the first move or accept focuses the first choice.
        if (choiceButtons.Count > 0 && !choiceButtons.Any(b => b.HasFocus())
            && (e.IsActionPressed("ui_up") || e.IsActionPressed("ui_down") || e.IsActionPressed("ui_accept")
                || e.IsActionPressed("ui_focus_next") || e.IsActionPressed("ui_focus_prev")))
        {
            choiceButtons[0].GrabFocus();
            AcceptEvent();
            return;
        }
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

/// <summary>The Event's banner picture at its own shape (its height is free within the art request's range), or a dark
/// placeholder until its art arrives.</summary>
public partial class Banner : Control
{
    readonly RunScreen screen;
    readonly string? image;

    public Banner(RunScreen screen, string? image)
    {
        this.screen = screen;
        this.image = image;
        var width = (float)Core.Assets.ArtCatalog.EventBannerWidth;
        var height = 240f;                                          // the placeholder's
        if (Art is { } art)
            height = Mathf.Clamp(width * art.GetHeight() / art.GetWidth(), Core.Assets.ArtCatalog.EventBannerMinHeight, Core.Assets.ArtCatalog.EventBannerMaxHeight);
        CustomMinimumSize = new Vector2(width, height);
    }

    Texture2D? Art => image is null ? null : screen.Main.Art.Get($"event_{image}");

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        if (Art is { } art)
            DrawTextureRect(art, r, false);
        else
        {
            DrawRect(r, new Color(0.16f, 0.15f, 0.12f));
            for (var i = 0; i < 6; i++)                                   // a soft vignette
                DrawRect(r.Grow(-i * 10), new Color(0.3f, 0.27f, 0.2f, 0.05f));
        }
        DrawRect(r, new Color(0, 0, 0, 0.8f), false, 2);
    }
}
