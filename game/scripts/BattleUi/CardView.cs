using EternalDungeon.Core.Assets;
using EternalDungeon.Core.Combat;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// A unit's card on the 3D table: a flat quad showing its <see cref="CardFace"/>, a back for when it falls, and a
/// shadow. All movement is card transforms (M2 brief §3): lift, tilt, flip, shake, lunge. World units are 100 screen
/// pixels at the 1280×800 reference, so the card shows at exactly its design size.
/// </summary>
public partial class CardView : Node3D
{
    public const float PixelsPerUnit = 100;

    public Unit Unit { get; }
    public CardFace Face { get; }
    public Vector2 SizePx { get; }

    readonly SubViewport viewport;
    readonly Node3D pivot = new();
    readonly MeshInstance3D shadow;
    bool lifted, faceDown;
    Tween? moveTween;
    Tween? turnTween;   // the one tween allowed to turn the card (shake, tilt, flip), so they never fight

    /// <summary>Where the card rests on the table (its centre), in world units.</summary>
    public Vector3 Home { get; private set; }

    public CardView(BattleScreen screen, Unit unit, Vector2 sizePx, bool rotatePortrait)
    {
        Unit = unit;
        SizePx = sizePx;
        viewport = new SubViewport
        {
            Size = (Vector2I)(sizePx * 2),
            Size2DOverride = (Vector2I)sizePx,
            Size2DOverrideStretch = true,
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        Face = new CardFace(screen, unit, sizePx, rotatePortrait);
        viewport.AddChild(Face);
        AddChild(viewport);

        var size = sizePx / PixelsPerUnit;
        shadow = Quad(size * 1.04f, new StandardMaterial3D
        {
            AlbedoColor = new Color(0, 0, 0, 0.45f),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        });
        shadow.Position = new Vector3(0, 0.001f, 0);
        AddChild(shadow);
        AddChild(pivot);

        var front = Quad(size, new StandardMaterial3D
        {
            AlbedoTexture = viewport.GetTexture(),
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
        });
        front.Position = new Vector3(0, 0.002f, 0);
        pivot.AddChild(front);

        var backTexture = screen.Main.Art.Get($"card_back_{ArtCatalog.SizeName(unit.Def.Size)}");
        var back = Quad(size, new StandardMaterial3D
        {
            AlbedoColor = backTexture is null ? new Color(0.28f, 0.22f, 0.18f) : Colors.White,
            AlbedoTexture = backTexture,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        });
        back.RotationDegrees = new Vector3(90, 0, 0);   // faces down until the card flips
        back.Position = new Vector3(0, -0.002f, 0);
        pivot.AddChild(back);
    }

    /// <summary>A quad lying flat on the table, facing up.</summary>
    static MeshInstance3D Quad(Vector2 size, Material material) => new()
    {
        Mesh = new QuadMesh { Size = size },
        MaterialOverride = material,
        RotationDegrees = new Vector3(-90, 0, 0),
    };

    public void Redraw() => Face.QueueRedraw();

    /// <summary>Moves the card to rest at <paramref name="home"/>, sliding there over <paramref name="seconds"/>.</summary>
    public void MoveTo(Vector3 home, float seconds)
    {
        Home = home;
        moveTween?.Kill();
        if (seconds <= 0)
        {
            Position = home;
            return;
        }
        moveTween = CreateTween();
        moveTween.TweenProperty(this, "position", home, seconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>The active hero's card lifts: it grows a little and its shadow drops away.</summary>
    public void Lift(bool on, float seconds)
    {
        if (lifted == on) return;
        lifted = on;
        var scale = on ? Vector3.One * 1.07f : Vector3.One;
        var shadowAt = on ? new Vector3(0.06f, 0.001f, 0.08f) : new Vector3(0, 0.001f, 0);
        if (seconds <= 0)
        {
            pivot.Scale = scale;
            shadow.Position = shadowAt;
            pivot.Position = new Vector3(0, on ? 0.2f : 0, 0);
            return;
        }
        var t = CreateTween().SetParallel();
        t.TweenProperty(pivot, "scale", scale, seconds);
        t.TweenProperty(shadow, "position", shadowAt, seconds);
        t.TweenProperty(pivot, "position", new Vector3(0, on ? 0.2f : 0, 0), seconds);
    }

    /// <summary>Lunges toward <paramref name="target"/> and snaps back.</summary>
    public void Lunge(Vector3 target, float seconds)
    {
        if (seconds <= 0) return;
        var toward = Home + (target - Home) * 0.28f + new Vector3(0, 0.3f, 0);
        var t = CreateTween();
        t.TweenProperty(this, "position", toward, seconds * 0.45f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
        t.TweenProperty(this, "position", Home, seconds * 0.55f).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
    }

    /// <summary>The target shakes and tilts when struck.</summary>
    public void Shake(float seconds, float strength = 1)
    {
        if (seconds <= 0 || faceDown) return;
        turnTween?.Kill();
        var t = turnTween = CreateTween();
        var step = seconds / 6;
        foreach (var dx in new[] { 0.08f, -0.07f, 0.05f, -0.03f, 0.015f, 0f })
            t.TweenProperty(pivot, "rotation_degrees", new Vector3(dx * 60 * strength, 0, dx * 40 * strength), step);
    }

    /// <summary>A miss: the target sidesteps and comes back.</summary>
    public void Sidestep(float seconds)
    {
        if (seconds <= 0) return;
        var t = CreateTween();
        t.TweenProperty(this, "position", Home + new Vector3(0.25f, 0, 0), seconds * 0.4f);
        t.TweenProperty(this, "position", Home, seconds * 0.6f);
    }

    /// <summary>Stunned cards sit tilted.</summary>
    public void SetTilted(bool on)
    {
        if (faceDown) return;
        var to = new Vector3(0, on ? 12 : 0, 0);
        if (pivot.RotationDegrees.IsEqualApprox(to) || turnTween?.IsRunning() == true) return;
        turnTween = CreateTween();
        turnTween.TweenProperty(pivot, "rotation_degrees", to, 0.2f);
    }

    /// <summary>A fallen unit's card flips face down and stays on the board.</summary>
    public void Flip(bool down, float seconds)
    {
        if (faceDown == down) return;
        faceDown = down;
        turnTween?.Kill();
        var to = new Vector3(0, 0, down ? 180 : 0);
        if (seconds <= 0)
        {
            pivot.RotationDegrees = to;
            pivot.Position = Vector3.Zero;
            return;
        }
        var t = turnTween = CreateTween();
        t.TweenProperty(pivot, "position", new Vector3(0, 0.6f, 0), seconds * 0.3f);
        t.TweenProperty(pivot, "rotation_degrees", to, seconds * 0.4f);
        t.TweenProperty(pivot, "position", Vector3.Zero, seconds * 0.3f);
    }

    public bool FaceDown => faceDown;
}
