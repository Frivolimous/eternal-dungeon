using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Assets;

/// <summary>One image a style provides: its id (the file name, without .png), exact size, and what it shows.</summary>
/// <summary>An image the game asks for, at twice its display size. With <see cref="MinHeight"/> its height is free between
/// that and <see cref="Height"/> (the width stays fixed): the game shows it at the shape it has.</summary>
public sealed record ArtRequest(string Id, int Width, int Height, string Group, string Description, bool Optional, int MinHeight = 0)
{
    public bool FlexibleHeight => MinHeight > 0;

    /// <summary>The size as the art requests list it: 1160×388–580 for a flexible height.</summary>
    public string Size => FlexibleHeight ? $"{Width}×{MinHeight}–{Height}" : $"{Width}×{Height}";

    public bool Fits(int width, int height) => width == Width && (FlexibleHeight ? height >= MinHeight && height <= Height : height == Height);

    /// <summary>The height a source image gets at this width: its own shape, kept within the allowed heights.</summary>
    public int HeightFor(int sourceWidth, int sourceHeight) =>
        FlexibleHeight ? Math.Clamp((int)Math.Round((double)sourceHeight * Width / sourceWidth), MinHeight, Height) : Height;
}

/// <summary>
/// Every styled image the game uses (M2 brief §9), derived from the data so the list never goes stale. A style is a
/// folder <c>game/assets/styles/{style}/</c> with these ids as PNGs at exactly these sizes. Art is drawn at 2× its
/// size on screen at the 1280×800 reference layout, so it stays sharp on larger screens. Images hold no text: names,
/// numbers and bars are drawn by the game. Anything missing falls back to a generated placeholder.
/// </summary>
public static class ArtCatalog
{
    public const string DefaultState = "default";

    /// <summary>Optional portrait states; a missing one falls back to the default portrait. Later they may be
    /// animations instead of stills, with no code changes.</summary>
    public static readonly (string State, string Description)[] PortraitStates =
    [
        ("low_hp", "below a third of its Health"),
        ("hurt", "just took damage"),
        ("attacking", "using an action"),
        ("casting", "casting a spell"),
        ("knocked_out", "fallen (shown before the card flips face down)"),
    ];

    /// <summary>On-screen sizes at the reference layout (the art is twice these).</summary>
    public static (int W, int H) PortraitDisplay(UnitSize size) => size switch
    {
        UnitSize.Small => (110, 110),
        UnitSize.Tall => (110, 220),
        _ => (246, 246),
    };

    public static (int W, int H) CardDisplay(UnitSize size) => size switch
    {
        UnitSize.Small => (124, 140),
        UnitSize.Tall => (124, 252),
        _ => (260, 276),
    };

    public const int ActionIconDisplay = 32, StatusIconDisplay = 18;

    /// <summary>Exploration art at the reference layout (1280×800): the Map area, a Node's Feature icon, the Camp
    /// button's icon.</summary>
    public static readonly (int W, int H) MapDisplay = (1260, 600);

    public const int FeatureIconDisplay = 80, CampIconDisplay = 40, InteractableIconDisplay = 36;

    /// <summary>An Event's banner picture, across the top of the Event screen: a fixed width, and any height from 3:1 to 2:1
    /// (shown at the picture's own shape).</summary>
    public const int EventBannerWidth = 580, EventBannerMinHeight = 194, EventBannerMaxHeight = 290;

    public static string PortraitId(string unitId, string state = DefaultState) =>
        state == DefaultState ? $"portrait_{unitId}" : $"portrait_{unitId}_{state}";

    public static string SizeName(UnitSize size) => JsonField.SnakeCase(size.ToString());

    public static string ActionIconId(string actionId) => $"icon_action_{actionId}";

    public static string StatusIconId(string status) => $"icon_status_{status}";

    /// <summary>The status icons: one per crowd-control kind, plus generic buff, debuff and damage over time.</summary>
    public static readonly string[] Statuses =
        [.. Enum.GetValues<CcKind>().Where(c => c != CcKind.None).Select(c => JsonField.SnakeCase(c.ToString())), "buff", "debuff", "dot"];

    public static List<ArtRequest> All(GameData data)
    {
        var list = new List<ArtRequest>();
        foreach (var unit in data.UnitList)
        {
            var (w, h) = PortraitDisplay(unit.Size);
            var shape = unit.Size switch
            {
                UnitSize.Small => "square",
                UnitSize.Tall => "1:2, tall (rotated 90° by the game when the board is side-on)",
                _ => "square, large",
            };
            var side = data.EncounterList.Any(e => e.Party.Any(p => p.Unit == unit.Id)) ? "hero" : "enemy";
            list.Add(new ArtRequest(PortraitId(unit.Id), w * 2, h * 2, "Portraits", $"{unit.Name} ({side}), {shape}", false));
            foreach (var (state, what) in PortraitStates)
                list.Add(new ArtRequest(PortraitId(unit.Id, state), w * 2, h * 2, "Portrait states", $"{unit.Name}, {what}", true));
        }
        foreach (var size in Enum.GetValues<UnitSize>())
        {
            var (w, h) = CardDisplay(size);
            list.Add(new ArtRequest($"card_frame_{SizeName(size)}", w * 2, h * 2, "Cards",
                $"Card frame for a {SizeName(size)} unit, transparent where the portrait and bars show through", true));
            list.Add(new ArtRequest($"card_back_{SizeName(size)}", w * 2, h * 2, "Cards", $"Card back for a {SizeName(size)} unit (a fallen unit's card lies face down)", true));
        }
        foreach (var action in data.ActionList)
            list.Add(new ArtRequest(ActionIconId(action.Id), ActionIconDisplay * 2, ActionIconDisplay * 2, "Action icons",
                $"{action.Name} ({string.Join(", ", action.Tags)})", true));
        foreach (var status in Statuses)
            list.Add(new ArtRequest(StatusIconId(status), StatusIconDisplay * 2, StatusIconDisplay * 2, "Status icons",
                $"{status.Replace('_', ' ')}: a distinct shape, readable without colour", true));
        // Exploration (M3A): the Map's parchment, the Camp icon, each Node Feature's icon, and each Event picture.
        list.Add(new ArtRequest("map_background", MapDisplay.W * 2, MapDisplay.H * 2, "Maps",
            "Neutral parchment for every Map, like the paper of a sketched map: no drawing on it (the game draws the Regions, borders and icons)", true));
        list.Add(new ArtRequest("icon_camp", CampIconDisplay * 2, CampIconDisplay * 2, "Maps", "Camp (the Camp button): a campfire", true));
        foreach (var kind in Enum.GetValues<InteractableKind>())
            list.Add(new ArtRequest($"icon_interactable_{JsonField.SnakeCase(kind.ToString())}", InteractableIconDisplay * 2, InteractableIconDisplay * 2,
                "Interactable icons", $"{JsonField.SnakeCase(kind.ToString()).Replace('_', ' ')} (beside its Region's icon on the map, glowing while usable): sketched in ink on transparency", true));
        foreach (var feature in data.Nodes.Values.GroupBy(n => n.Feature))
            list.Add(new ArtRequest($"icon_feature_{feature.Key}", FeatureIconDisplay * 2, FeatureIconDisplay * 2, "Feature icons",
                $"{string.Join(", ", feature.Select(n => n.Name))}: sketched in ink on transparency, like a drawing on the map", true));
        foreach (var image in data.Events.Values.SelectMany(e => e.Blocks).OfType<StoryBlock>().Select(b => b.Image).OfType<string>().Distinct())
            list.Add(new ArtRequest($"event_{image}", EventBannerWidth * 2, EventBannerMaxHeight * 2, "Event pictures",
                $"{string.Join(", ", data.Events.Values.Where(e => e.Blocks.OfType<StoryBlock>().Any(b => b.Image == image)).Select(e => data.Text[EventDef.NameKey(e.Id)]))} (the wide banner across the top of the Event; any height in the range, from 3:1 to 2:1)", true, EventBannerMinHeight * 2));
        return list;
    }

    /// <summary>docs/art-requests.md: every image with its id, size and description.</summary>
    public static string Markdown(GameData data)
    {
        var all = All(data);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Art requests");
        sb.AppendLine();
        sb.AppendLine("Generated by `sim art-requests` from the data: don't edit by hand. Every image the battle and exploration screens use, per art style.");
        sb.AppendLine();
        sb.AppendLine("- A style is a folder `game/assets/styles/<style>/` holding these files as `<id>.png`, at exactly these sizes.");
        sb.AppendLine("- Sizes are **2× the size on screen** at the 1280×800 reference layout, so art stays sharp on larger screens.");
        sb.AppendLine("- No text in images: names, numbers, bars and icons are drawn by the game.");
        sb.AppendLine("- Only portraits are needed to start; everything marked optional falls back to a generated placeholder, and a");
        sb.AppendLine("  missing portrait does too (never a crash).");
        sb.AppendLine("- AI-made placeholders: list the style in `game/assets/manifest.json` with `aiPlaceholder: true`.");
        sb.AppendLine("- Masters can be any size: name each file by its id (`portrait_warrior.png`), put them in");
        sb.AppendLine("  `art-source/<style>/` (not in git) and run `tools/resize-art.bat <style>`. It crops each from the centre");
        sb.AppendLine("  to the right shape and resizes it to the exact size below. Frames need transparency (PNG or WebP).");
        foreach (var group in all.GroupBy(r => r.Group))
        {
            sb.AppendLine();
            sb.AppendLine($"## {group.Key}");
            sb.AppendLine();
            sb.AppendLine("| Id | Size | Needed | Shows |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var r in group)
                sb.AppendLine($"| `{r.Id}` | {r.Size} | {(r.Optional ? "optional" : "yes")} | {r.Description} |");
            if (group.Key == "Cards")
            {
                sb.AppendLine();
                sb.AppendLine($"Each frame has a template at its exact size in `{CardLayout.TemplateFolder}/` (`card_frame_<size>_template.png`),");
                sb.AppendLine("showing where the game draws. Paint the frame over the grey and leave the rest transparent:");
                sb.AppendLine();
                foreach (var (name, rgb, means) in CardLayout.TemplateLegend)
                    sb.AppendLine($"- **{name}** (#{rgb:X6}): {means}");
            }
        }
        return sb.ToString();
    }
}
