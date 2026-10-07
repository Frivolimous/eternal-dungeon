using EternalDungeon.Core.Assets;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// Status icons (M2 brief §8): every status has its own shape as well as its own colour, so they read without colour.
/// A style's icon image replaces the shape when it exists.
/// </summary>
public static class StatusIcon
{
    public static Color ColorOf(string status) => status switch
    {
        "stun" => new Color(1f, 0.85f, 0.2f),
        "root" => new Color(0.6f, 0.45f, 0.25f),
        "silence" => new Color(0.65f, 0.45f, 0.95f),
        "sleep" => new Color(0.5f, 0.7f, 1f),
        "fear" => new Color(0.75f, 0.3f, 0.8f),
        "confusion" => new Color(0.95f, 0.5f, 0.75f),
        "dot" => new Color(0.55f, 0.85f, 0.3f),
        "buff" => new Color(0.35f, 0.85f, 0.95f),
        _ => new Color(0.95f, 0.4f, 0.35f),
    };

    public static void Draw(CanvasItem canvas, string status, Rect2 r, Art art)
    {
        if (art.Get(ArtCatalog.StatusIconId(status)) is { } texture)
        {
            canvas.DrawTextureRect(texture, r, false);
            return;
        }
        var c = r.GetCenter();
        var h = r.Size.X / 2;
        canvas.DrawCircle(c, h + 1, new Color(0, 0, 0, 0.75f));
        var color = ColorOf(status);
        switch (status)
        {
            case "stun":       // star
                canvas.DrawColoredPolygon(Star(c, h * 0.95f, h * 0.42f, 5), color);
                break;
            case "root":       // square
                canvas.DrawRect(new Rect2(c - Vector2.One * h * 0.62f, Vector2.One * h * 1.24f), color);
                break;
            case "silence":    // octagon with a bar
                canvas.DrawColoredPolygon(Regular(c, h * 0.85f, 8, Mathf.Pi / 8), color);
                canvas.DrawLine(c + new Vector2(-h * 0.5f, h * 0.5f), c + new Vector2(h * 0.5f, -h * 0.5f), Colors.Black, 2);
                break;
            case "sleep":      // crescent
                canvas.DrawCircle(c, h * 0.8f, color);
                canvas.DrawCircle(c + new Vector2(h * 0.38f, -h * 0.25f), h * 0.62f, new Color(0, 0, 0, 1));
                break;
            case "fear":       // downward triangle
                canvas.DrawColoredPolygon([c + new Vector2(-h * 0.85f, -h * 0.6f), c + new Vector2(h * 0.85f, -h * 0.6f), c + new Vector2(0, h * 0.85f)], color);
                break;
            case "confusion":  // diamond
                canvas.DrawColoredPolygon(Regular(c, h * 0.9f, 4, 0), color);
                break;
            case "dot":        // drop (circle with a point)
                canvas.DrawCircle(c + new Vector2(0, h * 0.2f), h * 0.6f, color);
                canvas.DrawColoredPolygon([c + new Vector2(-h * 0.52f, h * 0.0f), c + new Vector2(h * 0.52f, h * 0.0f), c + new Vector2(0, -h * 0.9f)], color);
                break;
            case "buff":       // upward arrow
                canvas.DrawColoredPolygon(Arrow(c, h, up: true), color);
                break;
            default:           // debuff: downward arrow
                canvas.DrawColoredPolygon(Arrow(c, h, up: false), color);
                break;
        }
    }

    static Vector2[] Regular(Vector2 c, float r, int n, float start) =>
        [.. Enumerable.Range(0, n).Select(i => c + Vector2.FromAngle(start + i * Mathf.Tau / n - Mathf.Pi / 2) * r)];

    static Vector2[] Star(Vector2 c, float outer, float inner, int points) =>
        [.. Enumerable.Range(0, points * 2).Select(i => c + Vector2.FromAngle(i * Mathf.Pi / points - Mathf.Pi / 2) * (i % 2 == 0 ? outer : inner))];

    static Vector2[] Arrow(Vector2 c, float h, bool up)
    {
        var s = up ? -1 : 1;
        return
        [
            c + new Vector2(0, s * h * 0.9f), c + new Vector2(h * 0.8f, s * h * 0.05f), c + new Vector2(h * 0.32f, s * h * 0.05f),
            c + new Vector2(h * 0.32f, -s * h * 0.8f), c + new Vector2(-h * 0.32f, -s * h * 0.8f), c + new Vector2(-h * 0.32f, s * h * 0.05f),
            c + new Vector2(-h * 0.8f, s * h * 0.05f),
        ];
    }
}
