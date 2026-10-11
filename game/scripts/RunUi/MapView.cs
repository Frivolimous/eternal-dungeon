using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;
using Num = System.Numerics;

namespace EternalDungeon.Game.RunUi;

/// <summary>How much of a Node the player sees (Anchor: Exploration › Fog of war).</summary>
public enum Fog { Hidden, Revealed, Eligible, Explored }

/// <summary>
/// The current Map as a sketched map (Anchor: Exploration › Map screen): each Node is a Region, the part of the Map
/// nearest it (<see cref="MapRegions"/>), split by hand-drawn dashed borders with a gap where a path crosses. Only borders
/// of Regions the player has seen are drawn, so the map grows as it's explored. Each seen Region shows one central icon
/// (<see cref="NodeButton"/>) that says what can be done there, and an explored Region's Interactables have icons of
/// their own beside it (<see cref="InteractableIcon"/>, glowing while usable). The mouse works on the whole Region and
/// moves the focus, so one ring shows at a time; keyboard and controller focus the icons.
/// </summary>
public partial class MapView : Control
{
    readonly RunScreen screen;
    readonly Dictionary<string, NodeButton> buttons = [];
    readonly List<InteractableIcon> things = [];
    readonly PanelContainer label = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    readonly Label labelText = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    MapLayout? layout;
    string? layoutMap;

    /// <summary>The Region under the mouse, if the player can hover it.</summary>
    public NodeButton? Hovered { get; private set; }

    /// <summary>The icon the label is for while the mouse isn't on a Region: the focused one, or a hovered Interactable.</summary>
    Control? marked;

    /// <summary>The sketch's colours: ink on parchment.</summary>
    public static readonly Color Ink = new(0.24f, 0.19f, 0.14f), Parchment = new(0.92f, 0.9f, 0.85f),
        FogTint = new(0.36f, 0.33f, 0.3f, 0.16f), Ring = new(0.3f, 0.85f, 0.95f);

    public MapView(RunScreen screen)
    {
        this.screen = screen;
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        Resized += Place;
        MouseExited += () => SetHovered(null);
        var box = Ui.Box(new Color(0.97f, 0.95f, 0.9f), Ink, 1, 6);
        box.ContentMarginTop = box.ContentMarginBottom = 3;
        label.AddThemeStyleboxOverride("panel", box);
        labelText.AddThemeColorOverride("font_color", Ink);
        labelText.AddThemeFontSizeOverride("font_size", Ui.Px(14));
        label.AddChild(labelText);
    }

    DungeonRun Run => screen.Run;

    public Fog FogOf(NodeState n) =>
        n.Explored ? Fog.Explored : Run.Eligible.Contains(n) ? Fog.Eligible : n.Revealed is not null ? Fog.Revealed : Fog.Hidden;

    public override void _Ready() => AddChild(label);

    public void Refresh()
    {
        foreach (var b in buttons.Values) b.QueueFree();
        foreach (var t in things) t.QueueFree();
        buttons.Clear();
        things.Clear();
        Hovered = null;
        marked = null;
        if (Run.Started)
        {
            if (layoutMap != Run.Map.Id) (layout, layoutMap) = (MapRegions.Of(Run.Map), Run.Map.Id);
            foreach (var node in Run.Map.Nodes.Select(n => Run.Nodes[n.Id]))
            {
                var fog = FogOf(node);
                if (fog == Fog.Hidden) continue;
                var b = new NodeButton(screen, this, node, fog);
                b.Pressed += () => screen.PressNode(node);
                Watch(b);
                buttons[node.Def.Id] = b;
                AddChild(b);
                if (fog != Fog.Explored) continue;
                foreach (var thing in node.Interactables)
                {
                    var icon = new InteractableIcon(screen, node, thing);
                    Watch(icon);
                    icon.MouseEntered += () =>
                    {
                        SetHovered(null);
                        if (icon.FocusMode != FocusModeEnum.None) icon.GrabFocus();
                        Mark(icon);
                    };
                    icon.MouseExited += () =>
                    {
                        if (icon.HasFocus()) icon.ReleaseFocus();
                        Mark(null);
                    };
                    things.Add(icon);
                    AddChild(icon);
                }
            }
        }
        MoveChild(label, -1);
        Place();
        ShowLabel();
    }

    void Watch(Control c)
    {
        c.FocusEntered += () => Mark(c);
        c.FocusExited += () => { if (marked == c) Mark(null); };
    }

    void Mark(Control? c)
    {
        marked = c;
        ShowLabel();
    }

    /// <summary>Focus where there's something to do: an unexplored Region, else a waiting Event, else something to use
    /// (the Pathway), else any Region the player can look at.</summary>
    public void FocusFirst()
    {
        Control? pick = buttons.Values.FirstOrDefault(b => b.Fog == Fog.Eligible)
            ?? buttons.Values.FirstOrDefault(b => b.Node.Event == EventStatus.Deferred)
            ?? (Control?)things.FirstOrDefault(t => t.Usable)
            ?? buttons.Values.FirstOrDefault(b => b.Hoverable);
        pick?.GrabFocus();
    }

    // ---- Layout space ↔ the screen ----

    Vector2 ToScreen(Num.Vector2 p) => new(p.X * Size.X / MapRegions.Width, p.Y * Size.Y / MapRegions.Height);

    Num.Vector2 ToLayout(Vector2 p) => new(p.X * MapRegions.Width / Size.X, p.Y * MapRegions.Height / Size.Y);

    Vector2 Center(NodeDef n) => ToScreen(MapRegions.Site(n));

    void Place()
    {
        foreach (var (id, b) in buttons)
            b.Position = Center(Run.Nodes[id].Def) - NodeButton.Box / 2;
        // A Region's Interactables sit up and to the right of its main icon, in a row.
        foreach (var group in things.GroupBy(t => t.Node))
        {
            var k = 0;
            foreach (var t in group)
                t.Position = Center(group.Key.Def) + new Vector2(34 + k++ * (InteractableIcon.Box.X + 4), -78);
        }
        ShowLabel();
        QueueRedraw();
    }

    // ---- The mouse: the whole Region is the button ----

    public override void _GuiInput(InputEvent e)
    {
        if (!Run.Started) return;
        if (e is InputEventMouseMotion m)
            SetHovered(buttons.GetValueOrDefault(MapRegions.At(Run.Map, ToLayout(m.Position)).Id) is { Hoverable: true } b ? b : null);
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && Hovered is { Pressable: true } h)
        {
            AcceptEvent();
            screen.PressNode(h.Node);
        }
    }

    /// <summary>The mouse is on <paramref name="b"/>'s Region (null: on none): it takes the focus, so one ring shows.</summary>
    void SetHovered(NodeButton? b)
    {
        if (b == Hovered) return;
        Hovered?.QueueRedraw();
        Hovered = b;
        if (b is not null) b.GrabFocus();
        else if (GetViewport()?.GuiGetFocusOwner() is NodeButton owner && owner.GetParent() == this) owner.ReleaseFocus();
        b?.QueueRedraw();
        MouseDefaultCursorShape = b is { Pressable: true } ? CursorShape.PointingHand : CursorShape.Arrow;
        ShowLabel();
        QueueRedraw();
    }

    /// <summary>The name label under the hovered Region's icon, else under the focused or hovered icon.</summary>
    void ShowLabel()
    {
        Control? c = Hovered ?? marked;
        var text = c switch { NodeButton b => b.Label, InteractableIcon t => t.Label, _ => null };
        label.Visible = text is not null && IsInstanceValid(c);
        if (!label.Visible) return;
        labelText.Text = text;
        label.ResetSize();
        var size = label.GetCombinedMinimumSize();
        var pos = c!.Position + new Vector2(c.Size.X / 2 - size.X / 2, c.Size.Y - 6);
        label.Position = new Vector2(Mathf.Clamp(pos.X, 4, Size.X - size.X - 4), Mathf.Min(pos.Y, Size.Y - size.Y - 4));
    }

    /// <summary>A line that rises from a Region and fades: what the party found there ("Nothing here but quiet.").</summary>
    public void Fly(NodeDef node, string text)
    {
        var fly = new Label { Text = text, AutoTranslateMode = AutoTranslateModeEnum.Disabled, MouseFilter = MouseFilterEnum.Ignore };
        fly.AddThemeColorOverride("font_color", Ink);
        fly.AddThemeColorOverride("font_outline_color", Parchment);
        fly.AddThemeConstantOverride("outline_size", 8);
        fly.AddThemeFontSizeOverride("font_size", Ui.Px(18));
        AddChild(fly);
        var size = fly.GetCombinedMinimumSize();
        var start = Center(node) + new Vector2(-size.X / 2, NodeButton.Box.Y / 2 - 10);
        fly.Position = new Vector2(Mathf.Clamp(start.X, 4, Size.X - size.X - 4), Mathf.Min(start.Y, Size.Y - size.Y - 30));
        var t = fly.CreateTween();
        t.TweenProperty(fly, "position:y", fly.Position.Y - 34, 2.4).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        t.Parallel().TweenProperty(fly, "modulate:a", 0f, 0.8).SetDelay(screen.Screenshot is null ? 1.6 : 10);
        t.TweenCallback(Callable.From(fly.QueueFree));
    }

    // ---- Drawing ----

    public override void _Draw()
    {
        var bg = new Rect2(Vector2.Zero, Size);
        if (screen.Main.Art.Get("map_background") is { } art) DrawTextureRect(art, bg, false);
        else DrawRect(bg, Parchment);
        if (layout is null || !Run.Started) return;

        bool Seen(string id) => buttons.ContainsKey(id);
        // Unseen Regions lie under a faint fog; the hovered Region lights up if it can be pressed.
        foreach (var (id, cell) in layout.Cells)
        {
            var poly = cell.Select(ToScreen).ToArray();
            if (!Seen(id)) DrawColoredPolygon(poly, FogTint);
            else if (Hovered?.Node.Def.Id == id && Hovered.Pressable) DrawColoredPolygon(poly, new Color(Ring, 0.12f));
        }
        foreach (var border in layout.Borders.Where(x => Seen(x.A) || Seen(x.B)))
        {
            var passage = Run.Map.Node(border.A).Links.Contains(border.B);
            Border(Wobble(border), passage);
        }
        DrawRect(bg, Ink, false, 2);
    }

    /// <summary>A border as a hand-drawn line: the straight edge bent a little, the same way every time (from the
    /// two Regions' ids), and pinned at both ends so borders meet.</summary>
    List<Vector2> Wobble(RegionBorder border)
    {
        var (a, b) = (ToScreen(border.From), ToScreen(border.To));
        var length = a.DistanceTo(b);
        var normal = (b - a).Normalized().Orthogonal();
        var seed = Ui.StableHash(border.A + "|" + border.B);
        var (p1, p2) = ((seed % 628) / 100f, (seed / 628 % 628) / 100f);
        var steps = Math.Max(2, (int)(length / 10));
        var amp = Mathf.Min(7, length * 0.06f);
        var points = new List<Vector2>(steps + 1);
        for (var i = 0; i <= steps; i++)
        {
            var t = (float)i / steps;
            var bend = Mathf.Sin(t * Mathf.Pi) * (Mathf.Sin(t * 5.3f + p1) * 0.7f + Mathf.Sin(t * 11.7f + p2) * 0.3f);
            points.Add(a.Lerp(b, t) + normal * bend * amp);
        }
        return points;
    }

    /// <summary>Draws a dashed line along the points; a passage leaves a gap in the middle with a dotted path across.</summary>
    void Border(List<Vector2> points, bool passage)
    {
        var total = 0f;
        for (var i = 1; i < points.Count; i++) total += points[i - 1].DistanceTo(points[i]);
        const float dash = 7, gap = 5, opening = 46;
        var color = new Color(Ink, 0.75f);
        var walked = 0f;
        for (var i = 1; i < points.Count; i++)
        {
            var (a, b) = (points[i - 1], points[i]);
            var len = a.DistanceTo(b);
            for (var s = 0f; s < len; s += 1.5f)
            {
                var at = walked + s;
                var drawn = at % (dash + gap) < dash && !(passage && Math.Abs(at - total / 2) < opening / 2);
                if (drawn) DrawLine(a.Lerp(b, s / len), a.Lerp(b, Math.Min(1, (s + 1.5f) / len)), color, 2, true);
            }
            walked += len;
        }
        if (!passage) return;
        // The path across: three dots through the gap.
        var (mid, dir) = PointAt(points, total / 2);
        var across = dir.Orthogonal();
        for (var k = -1; k <= 1; k++) DrawCircle(mid + across * k * 10, 2.5f, color);
    }

    static (Vector2 Point, Vector2 Direction) PointAt(List<Vector2> points, float distance)
    {
        for (var i = 1; i < points.Count; i++)
        {
            var len = points[i - 1].DistanceTo(points[i]);
            if (distance <= len || i == points.Count - 1)
                return (points[i - 1].Lerp(points[i], Mathf.Clamp(distance / Mathf.Max(len, 1e-3f), 0, 1)), (points[i] - points[i - 1]).Normalized());
            distance -= len;
        }
        return (points[0], Vector2.Right);
    }
}

/// <summary>
/// The central icon of a seen Region, which says what the player can do there: a "?" for an unexplored Region the party
/// can reach (or its Feature, if a reveal showed it), the Feature at full strength while its Event waits (with a red
/// dot), dimmed once complete (its Interactables have their own icons), and faded (no hover) for a Region seen from
/// afar but not reachable yet.
/// </summary>
public partial class NodeButton : Button
{
    public static readonly Vector2 Box = new(96, 96);
    public NodeState Node { get; }
    public Fog Fog { get; }
    readonly RunScreen screen;
    readonly MapView map;

    public NodeButton(RunScreen screen, MapView map, NodeState node, Fog fog)
    {
        this.screen = screen;
        this.map = map;
        Node = node;
        Fog = fog;
        Size = Box;
        CustomMinimumSize = Box;
        MouseFilter = MouseFilterEnum.Ignore;                     // the map handles the mouse for the whole Region
        FocusMode = Hoverable ? FocusModeEnum.All : FocusModeEnum.None;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
    }

    DungeonRun Run => screen.Run;

    bool Waiting => Node.Event == EventStatus.Deferred;

    /// <summary>Its Interactables the party can use now (they have icons of their own).</summary>
    public bool HasUsable => Run.Usable.Any(u => u.Node == Node);

    /// <summary>The player can point at it (a Region seen from afar can't be).</summary>
    public bool Hoverable => Fog is Fog.Eligible or Fog.Explored;

    /// <summary>Its icon is lit: the Region can be explored, or its Event resumed.</summary>
    public bool Clickable => Fog == Fog.Eligible || (Fog == Fog.Explored && Waiting);

    /// <summary>Pressing the Region does something: what <see cref="Clickable"/> says, or using an Interactable there.</summary>
    public bool Pressable => Clickable || (Fog == Fog.Explored && HasUsable);

    /// <summary>The name label's text.</summary>
    public string Label
    {
        get
        {
            if (Fog == Fog.Eligible)
                return Node.Revealed is null ? T("ui.node_unexplored") : F("ui.node_unexplored_named", ("node", Node.Def.Name));
            if (Waiting) return F("ui.node_waiting", ("node", Node.Def.Name), ("event", T(EventDef.NameKey(Node.Def.Event!))));
            return F("ui.node_complete", ("node", Node.Def.Name));
        }
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var center = Box / 2;
        const float icon = Core.Assets.ArtCatalog.FeatureIconDisplay;
        var rect = new Rect2(center - new Vector2(icon, icon) / 2, new Vector2(icon, icon));
        var lit = HasFocus() || map.Hovered == this;
        if (lit) DrawArc(center, icon / 2 + 6, 0, Mathf.Tau, 48, MapView.Ring, 5, true);

        if (Fog == Fog.Eligible && Node.Revealed is null)
        {
            DrawString(font, new Vector2(0, center.Y + 24), "?", HorizontalAlignment.Center, Box.X, Ui.Px(64), MapView.Ink);
            return;
        }
        // Full strength where there's something to do; dimmed once complete; faded when only seen from afar.
        var alpha = Fog == Fog.Revealed ? 0.25f : Clickable ? 1f : 0.5f;
        var tint = new Color(1, 1, 1, alpha);
        if (screen.Main.Art.Get($"icon_feature_{Node.Def.Feature}") is { } tex)
            DrawTextureRect(tex, rect, false, tint);
        else
        {
            // Placeholder: an ink circle with the Region's initial.
            var ink = new Color(MapView.Ink, alpha);
            DrawCircle(center, icon / 2 - 4, new Color(MapView.Parchment.Darkened(0.06f), alpha));
            DrawArc(center, icon / 2 - 4, 0, Mathf.Tau, 40, ink, 2, true);
            DrawString(font, new Vector2(0, center.Y + 13), Node.Def.Name[..1], HorizontalAlignment.Center, Box.X, Ui.Px(36), ink);
        }
        // Marks at the top right: a waiting Event (red dot), or what a reveal showed (enemies, a boss, a Sanctuary).
        var mark = rect.Position + new Vector2(icon - 6, 6);
        if (Fog == Fog.Explored && Waiting) DrawCircle(mark, 9, Ui.Enemy);
        else if (Fog != Fog.Explored && Node.Revealed is { } reveal and not RevealIcon.None)
        {
            DrawCircle(mark, 11, reveal == RevealIcon.Sanctuary ? Ui.Party : Ui.Enemy);
            var glyph = reveal switch { RevealIcon.Boss => "!!", RevealIcon.Enemies => "!", _ => "+" };
            DrawString(font, mark + new Vector2(-11, 6), glyph, HorizontalAlignment.Center, 22, Ui.Px(14), Colors.White);
        }
    }
}

/// <summary>
/// An Interactable's own icon (a Sanctuary, a Pathway, an Alchemist Station), beside its Region's main icon: glowing
/// while the party can use it, plain until the Region's Event is over, dimmed once used. Pressing it opens what can be
/// done there.
/// </summary>
public partial class InteractableIcon : Button
{
    public static readonly Vector2 Box = new(44, 44);
    public NodeState Node { get; }
    readonly InteractableState thing;
    readonly RunScreen screen;

    public InteractableIcon(RunScreen screen, NodeState node, InteractableState thing)
    {
        this.screen = screen;
        Node = node;
        this.thing = thing;
        Size = Box;
        CustomMinimumSize = Box;
        FocusMode = Usable ? FocusModeEnum.All : FocusModeEnum.None;
        MouseDefaultCursorShape = Usable ? CursorShape.PointingHand : CursorShape.Arrow;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        Pressed += () => { if (Usable) screen.OpenInteractables(node, [thing]); };
    }

    public bool Usable => screen.Run.Usable.Any(u => u.Interactable == thing);

    string Name => RunLog.InteractableName(EternalDungeon.Game.Text.Strings, thing.Kind);

    public string Label => thing.Used ? F("ui.thing_used", ("interactable", Name))
        : Usable ? F("ui.thing_use", ("interactable", Name)) : F("ui.thing_later", ("interactable", Name));

    public override void _Process(double delta)
    {
        if (Usable) QueueRedraw();                                     // the glow pulses
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var center = Box / 2;
        const float r = Core.Assets.ArtCatalog.InteractableIconDisplay / 2f;
        if (Usable)
        {
            var pulse = 0.5f + 0.5f * Mathf.Sin(Time.GetTicksMsec() / 260f);
            for (var i = 4; i >= 1; i--)
                DrawCircle(center, r + 2 + i * 3 * (0.7f + 0.3f * pulse), new Color(Ui.Gold, 0.07f + 0.05f * pulse));
        }
        if (HasFocus()) DrawArc(center, r + 5, 0, Mathf.Tau, 40, MapView.Ring, 4, true);
        var alpha = thing.Used ? 0.4f : Usable ? 1f : 0.65f;
        var rect = new Rect2(center - new Vector2(r, r), new Vector2(r * 2, r * 2));
        if (screen.Main.Art.Get($"icon_interactable_{JsonField.SnakeCase(thing.Kind.ToString())}") is { } tex)
            DrawTextureRect(tex, rect, false, new Color(1, 1, 1, alpha));
        else
        {
            // Placeholder: a gold disc with the Interactable's initial.
            DrawCircle(center, r, new Color(Ui.Gold.Darkened(0.15f), alpha));
            DrawArc(center, r, 0, Mathf.Tau, 32, new Color(MapView.Ink, alpha), 2, true);
            DrawString(font, new Vector2(0, center.Y + 8), Name[..1], HorizontalAlignment.Center, Box.X, Ui.Px(22), new Color(MapView.Ink, alpha));
        }
    }
}
