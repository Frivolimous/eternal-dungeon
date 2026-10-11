using System.Globalization;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using Godot;
using static EternalDungeon.Game.Text;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The Hero Panel (M3A brief §7), between Events: one hero at a time. Left: level and XP, Health, Mana, Stamina, traits
/// with their levels, stats with where they come from (the unit, skills), and run-long buffs and curses. Middle: the
/// skill tree (spending points, prerequisites, levels) and the masteries with their thresholds. Right: the belt and the
/// party pack (using potions, moving charges).
/// </summary>
public partial class HeroPanel : PanelContainer
{
    readonly RunScreen screen;
    Hero hero;
    VBoxContainer root = null!;

    public HeroPanel(RunScreen screen, Hero hero)
    {
        this.screen = screen;
        this.hero = hero;
    }

    DungeonRun Run => screen.Run;
    GameData Data => screen.Main.Data;

    public override void _Ready()
    {
        AddThemeStyleboxOverride("panel", Ui.Box(new Color(Ui.Panel, 0.99f), Ui.Gold));
        root = new VBoxContainer();
        root.AddThemeConstantOverride("separation", 8);
        AddChild(root);
        Place();
        screen.Resized += Place;
        Refresh();
    }

    public override void _ExitTree() => screen.Resized -= Place;

    void Place()
    {
        var size = new Vector2(Math.Min(1180, screen.Size.X - 40), Math.Min(640, screen.Size.Y - 40));
        CustomMinimumSize = size;
        Size = size;
        Position = (screen.Size - size) / 2;
    }

    public void Select(Hero h)
    {
        hero = h;
        Refresh();
    }

    /// <summary>Rebuilds the panel (after any change), keeping focus on the same kind of control where it can.</summary>
    public void Refresh()
    {
        if (root is null) return;
        var focused = GetViewport()?.GuiGetFocusOwner()?.Name.ToString();
        foreach (var c in root.GetChildren()) c.QueueFree();

        // Tabs: one per hero, and Close.
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 6);
        root.AddChild(tabs);
        foreach (var h in Run.Heroes)
        {
            var tab = Ui.Button(h.SkillPoints > 0 && !h.Dead ? F("ui.tab_points", ("hero", h.Name), ("points", h.SkillPoints)) : h.Name, () => Select(h));
            tab.Name = "tab_" + h.Id;
            if (h == hero) tab.AddThemeColorOverride("font_color", Ui.Gold);
            tabs.AddChild(tab);
        }
        var spacer = new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        tabs.AddChild(spacer);
        var close = Ui.Button(T("ui.close"), () => screen.CloseHeroPanel());
        close.Name = "close";
        tabs.AddChild(close);

        var columns = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        columns.AddThemeConstantOverride("separation", 16);
        root.AddChild(columns);
        columns.AddChild(Column(StatsColumn(), 330));
        columns.AddChild(Column(SkillsColumn(), 470));
        columns.AddChild(Column(BeltColumn(), 300));

        Callable.From(() =>
        {
            var target = (focused is null ? null : FindChild(focused, true, false) as Control) ?? FindChild("tab_" + hero.Id, true, false) as Control;
            target?.GrabFocus();
        }).CallDeferred();
    }

    static ScrollContainer Column(Control content, float width)
    {
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(width, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        content.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(content);
        return scroll;
    }

    static Label Line(string text, int size = 13, Color? color = null)
    {
        var l = Ui.Label(text, size, color);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    static string Num(double v) => v.ToString(Math.Abs(v) < 1 && v != 0 ? "0.##" : "0.#", CultureInfo.InvariantCulture);

    // ---- Stats ----

    VBoxContainer StatsColumn()
    {
        var col = new VBoxContainer();
        col.AddChild(Line(F("ui.hero_title", ("hero", hero.Name), ("level", hero.Level)) + (hero.Name == hero.Class.Name ? "" : "  ·  " + hero.Class.Name), 20, Ui.Gold));
        var next = Data.Levels.FirstOrDefault(l => l.Level == hero.Level + 1);
        col.AddChild(Line(next is null ? F("ui.xp_max", ("xp", hero.Xp)) : F("ui.xp", ("xp", hero.Xp), ("next", next.Xp))));
        col.AddChild(Line(F("ui.hero_vitals", ("hp", hero.Health), ("max_hp", hero.MaxHealth), ("mp", hero.Mana), ("max_mp", hero.MaxMana),
            ("stamina", hero.Stamina), ("max_stamina", hero.MaxStamina), ("status", RunLog.StatusName(Text.Strings, hero.Status)))));

        col.AddChild(Line(T("ui.traits"), 15, Ui.Gold));
        foreach (var trait in Data.Traits.Where(t => hero.Trait(t.Id) > 0))
            col.AddChild(Line(F("ui.trait_line", ("trait", trait.Name), ("level", hero.Trait(trait.Id)),
                ("from", hero.Class.Traits.Contains(trait.Id) ? hero.Class.Name : T("ui.from_buff")))));

        // Stats: the unit's own, then what skills add.
        var plain = new Unit(hero.Id, hero.Unit, Side.Party, Data);
        var skilled = new Unit(hero.Id, hero.Unit, Side.Party, Data);
        hero.ApplySkills(skilled);
        col.AddChild(Line(T("ui.stats"), 15, Ui.Gold));
        void Row(string name, double unit, double total)
        {
            if (total == 0 && unit == 0) return;
            col.AddChild(Line(total == unit ? F("ui.stat_line", ("stat", name), ("value", Num(total)))
                : F("ui.stat_line_skills", ("stat", name), ("value", Num(total)), ("unit", Num(unit)), ("skills", Num(total - unit)))));
        }
        foreach (var id in new[] { "health", "mana", "speed", "initiative" })
            Row(Data.Stats[id].Name, plain.Stats.Get(id), skilled.Stats.Get(id));
        foreach (var c in Data.CompoundList)
            Row(c.Name, plain.Stats.GetCompound(c.Id), skilled.Stats.GetCompound(c.Id));
        // Skill effects and anything else skills changed (untagged or keyed).
        foreach (var m in skilled.Stats.Modifiers.Where(m => m.Source.StartsWith("skill:") && !Data.Compounds.ContainsKey(m.Key.Stat) && !Flag(m.Key.Stat)
                     && m.Key.Stat is not ("health" or "mana" or "speed" or "initiative")).GroupBy(m => m.Key))
        {
            var name = Data.Stats[m.Key.Stat].Name;
            col.AddChild(Line(F("ui.stat_line", ("stat", m.Key.Tag is null ? name : $"{Data.Tags[m.Key.Tag].Name} {name}"),
                ("value", (m.Sum(x => x.Value) > 0 ? "+" : "") + Num(m.Sum(x => x.Value))))));
        }

        if (hero.Buffs.Count > 0)
        {
            col.AddChild(Line(T("ui.run_buffs"), 15, Ui.Gold));
            foreach (var b in hero.Buffs)
                col.AddChild(Line(F(RunLog.IsCurse(Data, b.Def) ? "ui.curse_line" : "ui.buff_line", ("buff", b.Def.Name),
                    ("left", F(b.Def.Duration == DurationKind.Steps ? "run.steps" : "run.battles", ("count", b.Remaining))))));
        }
        return col;
    }

    // ---- Skills ----

    VBoxContainer SkillsColumn()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        col.AddChild(Line(F("ui.skill_points", ("points", hero.SkillPoints), ("tree", hero.TreePoints)), 15, Ui.Gold));
        foreach (var skill in Data.SkillsOf(hero.Class.Id).Where(s => s.Kind == SkillKind.Tree))
        {
            var level = hero.SkillLevel(skill.Id);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var why = hero.Dead ? "dead" : hero.CantRaise(skill.Id);
            var plus = Ui.Button("+", () => screen.Act(() => Run.SpendPoint(hero.Id, skill.Id)));
            plus.Name = "raise_" + skill.Id;
            plus.Disabled = why is not null || Run.State != RunState.Exploring;
            plus.CustomMinimumSize = new Vector2(36, 0);
            plus.TooltipText = why is null ? "" : T("reason." + why);
            row.AddChild(plus);
            var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            text.AddChild(Line(F("ui.skill_line", ("skill", skill.Name), ("level", level), ("max", skill.MaxLevel)), 14, level > 0 ? Ui.Ink : Ui.Dim));
            text.AddChild(Line(Describe(skill), 12, Ui.Dim));
            if (skill.Requires is { } req && hero.SkillLevel(req) == 0)
                text.AddChild(Line(F("ui.skill_requires", ("skill", Data.Skills[req].Name)), 12, Ui.Enemy));
            row.AddChild(text);
            col.AddChild(row);
        }
        col.AddChild(Line(T("ui.masteries"), 15, Ui.Gold));
        foreach (var m in Data.SkillsOf(hero.Class.Id).Where(s => s.Kind == SkillKind.Mastery))
        {
            var held = m.Points <= hero.TreePoints;
            col.AddChild(Line(F(held ? "ui.mastery_held" : "ui.mastery_locked", ("mastery", m.Name), ("points", m.Points)), 14, held ? Ui.Ink : Ui.Dim));
            col.AddChild(Line(Describe(m), 12, Ui.Dim));
        }
        return col;
    }

    /// <summary>A skill's text (strings.csv: skill.&lt;id&gt;), then what it raises per level, from the data.</summary>
    string Describe(SkillDef skill)
    {
        var parts = new List<string>();
        if (Text.Strings.Has("skill." + skill.Id)) parts.Add(T("skill." + skill.Id));
        string Amounts(IEnumerable<SkillStat> rows, Func<SkillStat, double> value) => string.Join(", ", rows.Select(s =>
        {
            var name = Data.Compounds.TryGetValue(s.Stat, out var c) ? c.Name : Data.Stats[s.Stat].Name;
            if (s.Tag is { } tag) name = $"{Data.Tags[tag].Name} {name}";
            return $"{(value(s) > 0 ? "+" : "")}{Num(value(s))} {name}";
        }));
        var shown = skill.Stats.Where(s => !Flag(s)).ToList();
        var scaling = shown.Where(s => s.AtLevel is null).ToList();
        if (scaling.Count > 0)
        {
            var stats = Amounts(scaling, s => s.PerLevel);
            parts.Add(skill.Kind == SkillKind.Tree ? F("ui.per_level", ("stats", stats)) : stats);
        }
        // Steps: a flat boost or drop from a level on (a mastery's only level needs no label).
        foreach (var step in shown.Where(s => s.AtLevel is not null).GroupBy(s => s.AtLevel!.Value).OrderBy(g => g.Key))
        {
            var stats = Amounts(step, s => s.Amount);
            parts.Add(skill.Kind == SkillKind.Tree ? F("ui.at_level", ("level", step.Key), ("stats", stats)) : stats);
        }
        foreach (var a in skill.Actions)
            parts.Add(F("ui.grants_action", ("action", Data.Actions[a].Name)));
        return string.Join(" ", parts);
    }

    /// <summary>A switch rather than an amount (the opening crit): its text says what it does.</summary>
    static bool Flag(SkillStat s) => Flag(s.Stat);

    static bool Flag(string stat) => stat is "opening_crit" or "opening_block" or "crit_keeps_stealth";

    // ---- Belt and pack ----

    VBoxContainer BeltColumn()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        var exploring = Run.State == RunState.Exploring && !hero.Dead;
        col.AddChild(Line(F("ui.belt", ("used", hero.Belt.Count), ("slots", hero.BeltSlots)), 15, Ui.Gold));
        foreach (var slot in hero.Belt)
        {
            col.AddChild(Line(F("ui.charges", ("item", slot.Item.Name), ("charges", slot.Charges), ("max", slot.Item.Uses)), 14));
            var row = new HBoxContainer();
            if (slot.Item.OutsideCombat)
            {
                var use = Ui.Button(T("ui.use"), () => screen.Act(() => Run.UseItem(hero.Id, slot.Item.Id)));
                use.Name = "use_" + slot.Item.Id;
                use.Disabled = !exploring || !hero.Available;
                row.AddChild(use);
            }
            var toPack = Ui.Button(T("ui.to_pack"), () => screen.Act(() => Run.MoveToPack(hero.Id, slot.Item.Id)));
            toPack.Name = "pack_" + slot.Item.Id;
            toPack.Disabled = !exploring;
            row.AddChild(toPack);
            col.AddChild(row);
        }
        for (var i = hero.Belt.Count; i < hero.BeltSlots; i++)
            col.AddChild(Line(T("ui.slot_empty"), 13, Ui.Dim));

        col.AddChild(Line(T("ui.pack"), 15, Ui.Gold));
        if (Run.Pack.Count == 0) col.AddChild(Line(T("ui.pack_empty"), 13, Ui.Dim));
        foreach (var item in Data.ItemList.Where(i => Run.Pack.GetValueOrDefault(i.Id) > 0))
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var l = Line(F("ui.pack_line", ("item", item.Name), ("count", Run.Pack[item.Id])), 14);
            l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(l);
            var toBelt = Ui.Button(T("ui.to_belt"), () => screen.Act(() => Run.MoveToBelt(hero.Id, item.Id)));
            toBelt.Name = "belt_" + item.Id;
            toBelt.Disabled = Run.CantMoveToBelt(hero.Id, item.Id) is not null;
            row.AddChild(toBelt);
            col.AddChild(row);
        }
        return col;
    }
}
