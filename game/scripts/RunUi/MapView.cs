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
/// (<see cref="NodeButton"/>) that says what can be done there. The mouse works on the whole Region; keyboard and
/// controller focus the icons.
/// </summary>
public partial class MapView : Control
{
    readonly RunScreen screen;
    readonly Dictionary<string, NodeButton> buttons = [];
    readonly PanelContainer label = new() { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
    readonly Label labelText = new() { AutoTranslateMode = AutoTranslateModeEnum.Disabled };
    MapLayout? layout;
    string? layoutMap;

    /// <summary>The Region under the mouse, if the player can hover it.</summary>
    public NodeButton? Hovered { get; private set; }
    NodeButton? focused;

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
        buttons.Clear();
        Hovered = focused = null;
        if (Run.Started)
        {
            if (layoutMap != Run.Map.Id) (layout, layoutMap) = (MapRegions.Of(Run.Map), Run.Map.Id);
            foreach (var node in Run.Map.Nodes.Select(n => Run.Nodes[n.Id]))
            {
                var fog = FogOf(node);
                if (fog == Fog.Hidden) continue;
                var b = new NodeButton(screen, this, node, fog);
                b.Pressed += () => screen.PressNode(node);
                b.FocusEntered += () => { focused = b; ShowLabel(); };
                b.FocusExited += () => { if (focused == b) focused = null; ShowLabel(); };
                buttons[node.Def.Id] = b;
                AddChild(b);
            }
        }
        MoveChild(label, -1);
        Place();
        ShowLabel();
    }

    /// <summary>Focus where there's something to do: an unexplored Region, else a waiting Event, else a Region with
    /// something to use (the Pathway), else any Region the player can look at.</summary>
    public void FocusFirst()
    {
        var pick = buttons.Values.FirstOrDefault(b => b.Fog == Fog.Eligible)
            ?? buttons.Values.FirstOrDefault(b => b.Node.Event == EventStatus.Deferred)
            ?? buttons.Values.FirstOrDefault(b => b.Clickable)
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
        ShowLabel();
        QueueRedraw();
    }

    // ---- The mouse: the whole Region is the button ----

    public override void _GuiInput(InputEvent e)
    {
        if (!Run.Started) return;
        if (e is InputEventMouseMotion m)
            SetHovered(buttons.GetValueOrDefault(MapRegions.At(Run.Map, ToLayout(m.Position)).Id) is { Hoverable: true } b ? b : null);
        else if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } && Hovered is { Clickable: true } h)
        {
            AcceptEvent();
            h.GrabFocus();
            screen.PressNode(h.Node);
        }
    }

    void SetHovered(NodeButton? b)
    {
        if (b == Hovered) return;
        Hovered?.QueueRedraw();
        Hovered = b;
        b?.QueueRedraw();
        MouseDefaultCursorShape = b is { Clickable: true } ? CursorShape.PointingHand : CursorShape.Arrow;
        ShowLabel();
        QueueRedraw();
    }

    /// <summary>The name label under the hovered (else focused) Region's icon.</summary>
    void ShowLabel()
    {
        var b = Hovered ?? focused;
        label.Visible = b is not null;
        if (b is null) return;
        labelText.Text = b.Label;
        label.ResetSize();
        var size = label.GetCombinedMinimumSize();
        var pos = b.Position + new Vector2(NodeButton.Box.X / 2 - size.X / 2, NodeButton.Box.Y - 6);
        label.Position = new Vector2(Mathf.Clamp(pos.X, 4, Size.X - size.X - 4), Mathf.Min(pos.Y, Size.Y - size.Y - 4));
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
            else if (Hovered?.Node.Def.Id == id && Hovered.Clickable) DrawColoredPolygon(poly, new Color(Ring, 0.12f));
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
        var seed = (uint)StringComparer.Ordinal.GetHashCode(border.A + "|" + border.B);
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
/// can reach (or its Feature, if a reveal showed it), the Feature at full strength where something can be done (a
/// waiting Event, with a red dot; a Sanctuary, Pathway or Alchemist Station), dimmed once complete, and faded (no hover)
/// for a Region seen from afar but not reachable yet.
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
    List<InteractableState> Usable => Run.Usable.Where(u => u.Node == Node).Select(u => u.Interactable).ToList();

    /// <summary>The player can point at it (a Region seen from afar can't be).</summary>
    public bool Hoverable => Fog is Fog.Eligible or Fog.Explored;

    /// <summary>Pressing it does something: explore, resume a waiting Event, or use what's there.</summary>
    public bool Clickable => Fog == Fog.Eligible || (Fog == Fog.Explored && (Waiting || Usable.Count > 0));

    /// <summary>The name label's text.</summary>
    public string Label
    {
        get
        {
            if (Fog == Fog.Eligible)
                return Node.Revealed is null ? T("ui.node_unexplored") : F("ui.node_unexplored_named", ("node", Node.Def.Name));
            if (Waiting) return F("ui.node_waiting", ("node", Node.Def.Name), ("event", T(EventDef.NameKey(Node.Def.Event!))));
            var usable = Usable.Select(u => RunLog.InteractableName(EternalDungeon.Game.Text.Strings, u.Kind)).ToList();
            if (usable.Count > 0) return F("ui.node_use", ("node", Node.Def.Name), ("status", string.Join(T("preview.separator"), usable)));
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
