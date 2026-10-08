using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using Godot;
using Side = EternalDungeon.Core.Combat.Side;
using static EternalDungeon.Game.Text;

namespace EternalDungeon.Game.BattleUi;

/// <summary>
/// The selected or hovered unit, in detail (M2 brief §4): health, Shield, Mana, Act and Speed, its main
/// stats, resistances by damage type, its cast, and its buffs with what they do.
/// </summary>
public partial class DetailsPanel : PanelContainer
{
    readonly BattleScreen screen;
    Unit? unit;
    RichTextLabel body = null!;

    public DetailsPanel(BattleScreen screen) => this.screen = screen;

    public override void _Ready()
    {
        body = new RichTextLabel
        {
            BbcodeEnabled = true, FitContent = false, ScrollActive = true, SizeFlagsVertical = SizeFlags.ExpandFill,
            AutoTranslateMode = AutoTranslateModeEnum.Disabled, FocusMode = FocusModeEnum.None, MouseFilter = MouseFilterEnum.Ignore,
        };
        body.AddThemeFontSizeOverride("normal_font_size", Ui.Px(13));
        body.AddThemeFontSizeOverride("bold_font_size", Ui.Px(17));
        AddChild(body);
        Refresh();
    }

    public void Show(Unit u)
    {
        unit = u;
        Refresh();
    }

    public void Refresh()
    {
        if (body is null) return;
        if (unit is null)
        {
            body.Text = $"[color=#{Ui.Dim.ToHtml(false)}]{Escape(T("ui.details_hint"))}[/color]";
            return;
        }
        var u = unit;
        var battle = screen.Session.Battle;
        var data = battle.Data;
        var sb = new System.Text.StringBuilder();
        var side = Ui.Side(u.Side).ToHtml(false);
        sb.Append($"[b][color=#{side}]{Escape(u.Name)}[/color][/b]\n");
        if (!u.Alive) sb.Append(Escape(T("ui.fallen"))).Append('\n');
        sb.Append(Escape(F("ui.detail_hp", ("hp", u.Health), ("max", u.MaxHealth))));
        if (u.Shield > 0) sb.Append("   ").Append(Escape(F("ui.detail_shield", ("shield", u.Shield))));
        if (u.MaxMana > 0) sb.Append("   ").Append(Escape(F("ui.detail_mana", ("mana", u.Mana), ("max", u.MaxMana))));
        sb.Append('\n');
        sb.Append(Escape(F("ui.detail_act", ("act", Math.Round(u.Act)), ("speed", u.Speed))));
        sb.Append('\n');
        if (u.Casting is { } cast)
            sb.Append(Escape(F("ui.detail_casting", ("action", data.Actions[cast.ActionId].Name), ("time", CombatLog.T(cast.CompletesAt))))).Append('\n');

        // Main stats, untagged (each action's tags can add more; the target preview shows the exact numbers).
        sb.Append($"\n[color=#{Ui.Dim.ToHtml(false)}]{Escape(T("ui.detail_stats"))}[/color]\n");
        var stats = new List<string>
        {
            Stat(data, "hit", u.Stats.Get("hit"), pct: true),
            Stat(data, "avoid", u.Stats.Get("avoid"), pct: true),
            Stat(data, "power", u.Stats.Get("power"), pct: false),
            Stat(data, "c_rate", u.Stats.Get("c_rate", ["weapon"]), pct: true),
            Stat(data, "c_mult", u.Stats.Get("c_mult"), pct: true),
        };
        sb.Append(Escape(string.Join("   ", stats))).Append('\n');

        // Resistances by damage type.
        var resists = data.TagList.Where(t => t.Group == TagGroup.DamageType)
            .Select(t => $"{t.Name} {CombatLog.Pct(u.Stats.Get("resist", [t.Id]))}")
            .Append($"{data.Stats["all_resist"].Name} {CombatLog.Pct(u.Stats.Get("all_resist"))}");
        sb.Append($"[color=#{Ui.Dim.ToHtml(false)}]{Escape(T("ui.detail_resist"))}[/color]\n");
        sb.Append(Escape(string.Join("   ", resists))).Append('\n');

        // Buffs.
        if (u.Buffs.Count > 0)
        {
            sb.Append($"[color=#{Ui.Dim.ToHtml(false)}]{Escape(T("ui.detail_buffs"))}[/color]\n");
            foreach (var b in u.Buffs)
            {
                var applied = new BuffApplied(u, b, false, b.Stacks, b.Remaining, b.ShieldGranted);
                sb.Append("• ").Append(Escape(CombatLog.BuffText(data.Text, applied))).Append('\n');
            }
        }
        body.Text = sb.ToString();
    }

    static string Stat(GameData data, string id, double value, bool pct) =>
        $"{data.Stats[id].Name} {(pct ? CombatLog.Pct(value) : Math.Round(value).ToString())}";

    static string Escape(string s) => s.Replace("[", "[lb]");
}
