using EternalDungeon.Core.Combat;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// The turn order down the left edge (M2 brief §4, §6): upcoming turns as small portraits, sliding into their new order
/// after each action. While an action is hovered, a ghost marker shows where the hero's next turn would land (and
/// where its cast completes).
/// </summary>
public partial class Timeline : Control
{
    const int Count = 10;
    const float ChipHeight = 56, Top = 30;

    readonly BattleScreen screen;
    readonly Dictionary<string, Chip> chips = [];

    public Timeline(BattleScreen screen)
    {
        this.screen = screen;
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Ready()
    {
        var bg = new Panel { MouseFilter = MouseFilterEnum.Ignore };
        bg.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Ui.Panel, 0.85f), Ui.PanelBorder, 0, 0));
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);
        var title = Ui.Label(Text.T("ui.turn_order"), 12, Ui.Dim);
        title.Position = new Vector2(8, 6);
        AddChild(title);
        Refresh(null);
    }

    /// <summary>Rebuilds the order; <paramref name="ghost"/> adds the hovered action's markers.</summary>
    public void Refresh(TurnEstimate? ghost)
    {
        if (!IsInsideTree()) return;
        var battle = screen.Session.Battle;
        var entries = Preview.Timeline(battle, Count)
            .Select(t => (Key: "", t.Unit, t.Tick, Ghost: (string?)null)).ToList();
        if (ghost is not null && screen.Session.Awaiting is { } hero)
        {
            // The ghost replaces the hero's next predicted turn (its current turn stays first).
            var next = entries.FindIndex(1, e => e.Unit == hero);
            if (next > 0) entries.RemoveAt(next);
            if (ghost.CastCompletes is { } cast) entries.Add(("", hero, cast, Text.T("ui.ghost_cast")));
            if (ghost.NextTurn is { } turn) entries.Add(("", hero, turn, Text.T("ui.ghost_next")));
            var first = entries[0];
            entries = [first, .. entries.Skip(1).OrderBy(e => e.Tick).ThenBy(e => e.Ghost is null ? 0 : 1)];
            entries = [.. entries.Take(Count)];
        }

        // Key each chip by unit and occurrence, so a unit's chips slide rather than jump.
        var seen = new Dictionary<string, int>();
        var keep = new HashSet<string>();
        for (var i = 0; i < entries.Count; i++)
        {
            var (_, unit, tick, ghostLabel) = entries[i];
            var baseKey = (ghostLabel is null ? "" : "ghost:") + unit.Id;
            var n = seen[baseKey] = seen.GetValueOrDefault(baseKey) + 1;
            var key = $"{baseKey}#{n}";
            keep.Add(key);
            if (!chips.TryGetValue(key, out var chip))
            {
                chip = new Chip(screen) { Position = new Vector2(6, Top + i * ChipHeight + 30), Modulate = new Color(1, 1, 1, 0) };
                chips[key] = chip;
                AddChild(chip);
            }
            chip.Set(unit, tick, ghostLabel, i == 0 && ghostLabel is null && screen.Session.Awaiting is not null);
            var t = chip.CreateTween().SetParallel();
            t.TweenProperty(chip, "position", new Vector2(6, Top + i * ChipHeight), 0.25f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            t.TweenProperty(chip, "modulate:a", ghostLabel is null ? 1f : 0.75f, 0.2f);
        }
        foreach (var key in chips.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            chips[key].QueueFree();
            chips.Remove(key);
        }
    }

    /// <summary>One upcoming turn: a small portrait, the unit's name and when.</summary>
    partial class Chip(BattleScreen screen) : Control
    {
        Unit? unit;
        long tick;
        string? ghost;
        bool now;

        public void Set(Unit u, long t, string? ghostLabel, bool current)
        {
            unit = u;
            tick = t;
            ghost = ghostLabel;
            now = current;
            Size = new Vector2(100, ChipHeight - 4);
            MouseFilter = MouseFilterEnum.Ignore;
            QueueRedraw();
        }

        public override void _Draw()
        {
            if (unit is null) return;
            var font = GetThemeDefaultFont();
            var side = Ui.Side(unit.Side);
            var box = new Rect2(Vector2.Zero, Size);
            DrawRect(box, ghost is null ? new Color(side.Darkened(0.7f), 0.95f) : new Color(0, 0, 0, 0.3f));
            DrawRect(box, ghost is not null ? Ui.Gold : now ? Ui.Gold : new Color(side, 0.8f), false, ghost is not null || now ? 2 : 1);
            var portrait = new Rect2(4, 4, 34, 34);
            if (screen.Main.Art.Portrait(unit.Def.Id) is { } tex)
                DrawTextureRect(tex, portrait, false);
            else
            {
                DrawRect(portrait, side.Darkened(0.45f));
                var initials = string.Concat(unit.Def.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
                DrawString(font, portrait.Position + new Vector2(0, portrait.Size.Y * 0.62f), initials, HorizontalAlignment.Center, portrait.Size.X, 14, Ui.Ink);
            }
            var x = portrait.End.X + 4;
            var w = Size.X - x - 2;
            var name = unit.Name.Length > 11 ? unit.Name[..10] + "…" : unit.Name;
            DrawString(font, new Vector2(x, 16), ghost ?? name, HorizontalAlignment.Left, w, 10, ghost is null ? Ui.Ink : Ui.Gold);
            DrawString(font, new Vector2(x, 30), Text.F("ui.at_time", ("time", Core.Combat.CombatLog.T(tick))), HorizontalAlignment.Left, w, 10, Ui.Dim);
            if (now) DrawString(font, new Vector2(x, 44), Text.T("ui.now"), HorizontalAlignment.Left, w, 10, Ui.Gold);
        }
    }
}
