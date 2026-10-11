using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.RunUi;

/// <summary>How much of a Node the player sees (Anchor: Exploration â€º Fog of war).</summary>
public enum Fog { Hidden, Revealed, Eligible, Explored }

/// <summary>
/// The current Map: its Regions as focusable buttons at their positions, joined by the paths between them, under fog
/// of war. Explored Nodes show everything (Interactables, a waiting Event); eligible ones their name and Feature;
/// revealed ones that plus the reveal's icon; the rest nothing. Pressing an eligible Node explores it, a waiting one
/// resumes its Event, an explored one with Interactables opens them.
/// </summary>
public partial class MapView : Control
{
    readonly RunScreen screen;
    readonly Dictionary<string, NodeButton> buttons = [];

    public MapView(RunScreen screen)
    {
        this.screen = screen;
        ClipContents = true;
        Resized += Place;
    }

    DungeonRun Run => screen.Run;

    public Fog FogOf(NodeState n) =>
        n.Explored ? Fog.Explored : Run.Eligible.Contains(n) ? Fog.Eligible : n.Revealed is not null ? Fog.Revealed : Fog.Hidden;

    public void Refresh()
    {
        foreach (var b in buttons.Values) b.QueueFree();
        buttons.Clear();
        if (Run.Started)
            foreach (var node in Run.Map.Nodes.Select(n => Run.Nodes[n.Id]))
            {
                var fog = FogOf(node);
                if (fog == Fog.Hidden) continue;
                var b = new NodeButton(screen, node, fog);
                b.Pressed += () => screen.PressNode(node);
                b.FocusEntered += () => screen.Hint(b.Details);
                b.MouseEntered += () => screen.Hint(b.Details);
                buttons[node.Def.Id] = b;
                AddChild(b);
            }
        Place();
        QueueRedraw();
    }

    /// <summary>Focus where there's something to do: an eligible Node, else a waiting Event, else a Node with something to
    /// use (the Pathway), else any Node. Keyboard and controller players start there.</summary>
    public void FocusFirst()
    {
        var pick = buttons.Values.FirstOrDefault(b => b.Fog == Fog.Eligible)
            ?? buttons.Values.FirstOrDefault(b => b.Node.Event == EventStatus.Deferred)
            ?? buttons.Values.FirstOrDefault(b => Run.Usable.Any(u => u.Node == b.Node))
            ?? buttons.Values.FirstOrDefault();
        pick?.GrabFocus();
    }

    const float Margin = 70;

    Vector2 Center(NodeDef n) =>
        new(Margin + (Size.X - 2 * Margin) * (float)n.X / 100, Margin * 0.7f + (Size.Y - 1.4f * Margin) * (float)n.Y / 100);

    void Place()
    {
        foreach (var (id, b) in buttons)
            b.Position = Center(Run.Nodes[id].Def) - NodeButton.Box / 2;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var bg = new Rect2(Vector2.Zero, Size);
        if (Run.Started && screen.Main.Art.Get($"map_{Run.Map.Id}") is { } art)
            DrawTextureRect(art, bg, false);
        else
            DrawRect(bg, new Color(0.13f, 0.16f, 0.13f));
        DrawRect(bg, Ui.PanelBorder, false, 2);
        // The paths between visible Nodes; the ones that lead somewhere new are brighter.
        foreach (var (id, b) in buttons)
            foreach (var other in Run.Nodes[id].Def.Links)
            {
                if (string.CompareOrdinal(id, other) > 0 || !buttons.TryGetValue(other, out var c)) continue;
                var open = b.Fog == Fog.Eligible || c.Fog == Fog.Eligible;
                var explored = b.Fog == Fog.Explored && c.Fog == Fog.Explored;
                DrawLine(Center(Run.Nodes[id].Def), Center(Run.Nodes[other].Def),
                    explored ? new Color(Ui.Ink, 0.55f) : open ? new Color(Ui.Gold, 0.7f) : new Color(Ui.Dim, 0.3f), explored ? 4 : 3);
            }
    }
}

/// <summary>One Region on the Map: its Feature icon (or a placeholder), name, and what the player knows of it.</summary>
public partial class NodeButton : Button
{
    public static readonly Vector2 Box = new(172, 62);
    public NodeState Node { get; }
    public Fog Fog { get; }
    readonly RunScreen screen;

    public NodeButton(RunScreen screen, NodeState node, Fog fog)
    {
        this.screen = screen;
        Node = node;
        Fog = fog;
        Size = Box;
        CustomMinimumSize = Box;
        FocusMode = FocusModeEnum.All;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        AddThemeStyleboxOverride("focus", Ui.Focus());
        TooltipText = Details;
    }

    DungeonRun Run => screen.Run;

    /// <summary>What's shown under the name: the state of the Node as the player knows it.</summary>
    public string Status
    {
        get
        {
            switch (Fog)
            {
                case Fog.Eligible: return T("ui.node_eligible");
                case Fog.Revealed:
                    return Node.Revealed is { } icon and not RevealIcon.None ? F("ui.node_revealed_icon", ("icon", T("reveal." + JsonField.SnakeCase(icon.ToString()))))
                        : T("ui.node_revealed");
            }
            if (Node.Event == EventStatus.Deferred) return T("ui.node_waiting");
            var usable = Run.Usable.Where(u => u.Node == Node).Select(u => RunLog.InteractableName(EternalDungeon.Game.Text.Strings, u.Interactable.Kind)).ToList();
            if (usable.Count > 0) return string.Join(T("preview.separator"), usable);
            return Node.Interactables.Any(i => i.Used) ? T("ui.node_used") : T("ui.node_explored");
        }
    }

    /// <summary>The longer line for the info box on hover or focus.</summary>
    public string Details => Fog switch
    {
        Fog.Eligible => F("ui.node_details_eligible", ("node", Node.Def.Name)),
        Fog.Revealed => F("ui.node_details_revealed", ("node", Node.Def.Name), ("status", Status)),
        _ when Node.Event == EventStatus.Deferred => F("ui.node_details_waiting", ("node", Node.Def.Name),
            ("event", T(EventDef.NameKey(Node.Def.Event!)))),
        _ when Run.Usable.Any(u => u.Node == Node) => F("ui.node_details_use", ("node", Node.Def.Name), ("status", Status)),
        _ => F("ui.node_details_explored", ("node", Node.Def.Name)),
    };

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, Size);
        var (fill, border) = Fog switch
        {
            Fog.Explored => (new Color(0.2f, 0.19f, 0.25f, 0.95f), Node.Event == EventStatus.Deferred ? Ui.Enemy : Ui.Ink),
            Fog.Eligible => (new Color(0.24f, 0.21f, 0.14f, 0.95f), Ui.Gold),
            _ => (new Color(0.1f, 0.1f, 0.12f, 0.8f), new Color(Ui.Dim, 0.6f)),
        };
        DrawRect(r, fill);
        DrawRect(r, IsHovered() ? Ui.Gold : border, false, Fog == Fog.Eligible || IsHovered() ? 3 : 2);
        var icon = new Rect2(5, 5, 40, 40);
        if (screen.Main.Art.Get($"icon_feature_{Node.Def.Feature}") is { } tex)
            DrawTextureRect(tex, icon, false);
        else
        {
            DrawRect(icon, border.Darkened(0.55f));
            DrawString(font, icon.Position + new Vector2(0, 28), Node.Def.Name[..1], HorizontalAlignment.Center, icon.Size.X, Ui.Px(20), Ui.Ink);
        }
        var ink = Fog == Fog.Revealed ? Ui.Dim : Ui.Ink;
        DrawString(font, new Vector2(50, 22), Node.Def.Name, HorizontalAlignment.Left, Size.X - 54, Ui.Px(13), ink);
        DrawString(font, new Vector2(50, 42), Status, HorizontalAlignment.Left, Size.X - 54, Ui.Px(11),
            Node.Event == EventStatus.Deferred && Fog == Fog.Explored ? Ui.Enemy : Fog == Fog.Eligible ? Ui.Gold : Ui.Dim);
    }
}
