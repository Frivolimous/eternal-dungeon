using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// The 3D table seen from straight above (orthographic, so cards show at exactly their design size). Where each area
/// sits and which way it faces is decided here: Core's grid is front-relative and knows nothing of layouts (M2 brief
/// §1). Vertical: party at the bottom, enemies at the top. Side-on: party left, enemies right.
/// </summary>
public partial class BoardView : Node3D
{
    /// <summary>One tile's pitch on screen at the reference layout, along a lane and along the depth.</summary>
    public const float LanePitch = 136, DepthPitch = 150, Gap = 34;

    readonly BattleScreen screen;
    readonly BoardLayout layout;
    public Camera3D Camera { get; } = new();
    public Dictionary<Unit, CardView> Cards { get; } = [];
    readonly Dictionary<Tile, MeshInstance3D> tiles = [];
    readonly StandardMaterial3D tileMaterial = Flat(new Color(1, 1, 1, 0.025f));
    readonly StandardMaterial3D validMaterial = Flat(new Color(0.45f, 0.95f, 0.55f, 0.28f));
    readonly StandardMaterial3D hoverMaterial = Flat(new Color(1, 1, 1, 0.35f));

    /// <summary>The part of the screen the board should fill (between the side panels and above the action bar).</summary>
    public Rect2 Region { get; set; }

    public BoardView(BattleScreen screen, BoardLayout layout)
    {
        this.screen = screen;
        this.layout = layout;
    }

    BattleGrid Grid => screen.Session.Battle.Grid;

    public override void _Ready()
    {
        Camera.Projection = Camera3D.ProjectionType.Orthogonal;
        Camera.RotationDegrees = new Vector3(-90, 0, 0);
        Camera.Current = true;
        AddChild(Camera);

        var table = Quad(new Vector2(40, 40), Flat(new Color(0.09f, 0.13f, 0.12f), alpha: false));
        table.Position = new Vector3(0, -0.01f, 0);
        AddChild(table);

        foreach (var area in Grid.Areas)
        {
            var cells = Grid.TilesOf(area.Id).Select(CellRect).ToList();
            var bounds = cells.Aggregate((a, b) => a.Merge(b)).Grow(10);
            var mat = Quad(bounds.Size / CardView.PixelsPerUnit, Flat(new Color(Ui.Side(area.Side).Darkened(0.65f), 0.55f)));
            mat.Position = World(bounds.GetCenter(), -0.005f);
            AddChild(mat);
            foreach (var tile in Grid.TilesOf(area.Id))
            {
                var r = CellRect(tile);
                var quad = Quad((r.Size - new Vector2(10, 10)) / CardView.PixelsPerUnit, tileMaterial);
                quad.Position = World(r.GetCenter(), -0.003f);
                tiles[tile] = quad;
                AddChild(quad);
            }
        }

        foreach (var unit in screen.Session.Battle.Units)
        {
            var size = CardSize(unit.Def.Size);
            var card = new CardView(screen, unit, size, RotatePortrait(unit.Def.Size));
            Cards[unit] = card;
            AddChild(card);
        }
        PlaceCards(0);
    }

    // ---- Layout ----

    bool SideOn => layout == BoardLayout.SideOn;

    /// <summary>A Tall card lies across the screen in the side-on layout, so only its portrait turns.</summary>
    bool RotatePortrait(UnitSize size) => SideOn && size == UnitSize.Tall;

    /// <summary>A card's size on screen. A side-on Tall card spans two cells across and is as high as a small card.</summary>
    Vector2 CardSize(UnitSize size)
    {
        var (w, h) = ArtCatalog.CardDisplay(size);
        return RotatePortrait(size) ? new Vector2(LanePitch + w, ArtCatalog.CardDisplay(UnitSize.Small).H) : new Vector2(w, h);
    }

    /// <summary>A tile's cell on screen, in pixels relative to the board's centre.</summary>
    public Rect2 CellRect(Tile tile)
    {
        var (depth, lane) = Grid.Relative(tile);
        var side = Grid.SideOf(tile);
        var sign = side == Side.Party ? 1 : -1;
        Vector2 centre = SideOn
            ? new Vector2(-sign * (Gap / 2 + (depth + 0.5f) * LanePitch), (lane - 1) * DepthPitch)
            : new Vector2((lane - 1) * LanePitch, sign * (Gap / 2 + (depth + 0.5f) * DepthPitch));
        var size = new Vector2(LanePitch, DepthPitch);
        return new Rect2(centre - size / 2, size);
    }

    /// <summary>Where a unit's card rests: the centre of its footprint's cells.</summary>
    public Vector3 HomeOf(Unit unit)
    {
        var cells = Grid.Footprint(unit).Select(CellRect).ToList();
        if (cells.Count == 0) return Vector3.Zero;
        return World(cells.Aggregate((a, b) => a.Merge(b)).GetCenter(), 0);
    }

    static Vector3 World(Vector2 px, float y) => new(px.X / CardView.PixelsPerUnit, y, px.Y / CardView.PixelsPerUnit);

    public void PlaceCards(float seconds)
    {
        foreach (var (unit, card) in Cards)
            if (Grid.AnchorOf(unit) is not null)
                card.MoveTo(HomeOf(unit), seconds);
    }

    /// <summary>Frames the board in <see cref="Region"/>: one world unit is 100 screen pixels.</summary>
    public void Frame()
    {
        var view = GetViewport().GetVisibleRect().Size;
        Camera.Size = view.Y / CardView.PixelsPerUnit;
        var offset = (view / 2 - Region.GetCenter()) / CardView.PixelsPerUnit;
        Camera.Position = new Vector3(offset.X, 20, offset.Y);
    }

    public override void _Process(double delta) => Frame();

    /// <summary>A world point on screen, in the screen's (stretched) 2D coordinates.</summary>
    public Vector2 ToScreen(Vector3 world) => Camera.UnprojectPosition(world);

    /// <summary>A card's rectangle on screen.</summary>
    public Rect2 ScreenRect(CardView card)
    {
        var size = card.SizePx * (card.Scale.X);
        var centre = ToScreen(card.GlobalPosition);
        return new Rect2(centre - size / 2, size);
    }

    public Rect2 ScreenRect(Tile tile)
    {
        var r = CellRect(tile);
        var centre = ToScreen(World(r.GetCenter(), 0));
        return new Rect2(centre - r.Size / 2, r.Size);
    }

    /// <summary>Lights up tiles: the valid ones green, the hovered one white.</summary>
    public void MarkTiles(IReadOnlyCollection<Tile> valid, Tile? hovered)
    {
        foreach (var (tile, quad) in tiles)
            quad.MaterialOverride = tile == hovered ? hoverMaterial : valid.Contains(tile) ? validMaterial : tileMaterial;
    }

    static MeshInstance3D Quad(Vector2 size, Material material) => new()
    {
        Mesh = new QuadMesh { Size = size },
        MaterialOverride = material,
        RotationDegrees = new Vector3(-90, 0, 0),
    };

    static StandardMaterial3D Flat(Color color, bool alpha = true) => new()
    {
        AlbedoColor = color,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = alpha ? BaseMaterial3D.TransparencyEnum.Alpha : BaseMaterial3D.TransparencyEnum.Disabled,
    };
}
