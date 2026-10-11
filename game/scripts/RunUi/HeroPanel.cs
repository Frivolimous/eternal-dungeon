using System.Globalization;
using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using EternalDungeon.Core.Stats;
using Godot;
using static EternalDungeon.Game.Text;
using Side = EternalDungeon.Core.Combat.Side;

namespace EternalDungeon.Game.RunUi;

/// <summary>
/// The Hero Panel (Anchor: Exploration › Hero Panel), over the darkened map with the header and party footer still in
/// reach (pressing another hero's card switches to it). Left: the hero (portrait, name and class, level and XP, Stamina
/// with its status), the stats that matter (the primary ones and any that differ from a default unit's), traits and
/// run-long buffs. Right: the skill tree, drawn as a tree from the skills' prerequisites, with the masteries beside it,
/// or the inventory (belt and party pack). A skill raises with one click; hovering or focusing it shows its current and
/// next level.
/// </summary>
public partial class HeroPanel : Control
{
    readonly RunScreen screen;
    Hero hero;
    HBoxContainer columns = null!;
    readonly PanelContainer tip = new() { Visible = false, MouseFilter = MouseFilterEnum.Ignore, ZIndex = 10 };
    readonly VBoxContainer tipBody = new();

    /// <summary>The right panel's tab, kept as the player left it.</summary>
    static bool showInventory;

    static readonly Color Card = new(0.17f, 0.155f, 0.21f, 0.98f);

    public HeroPanel(RunScreen screen, Hero hero)
    {
        this.screen = screen;
        this.hero = hero;
        AnchorRight = 1;
        AnchorBottom = 1;
        OffsetTop = RunScreen.TopHeight;
        OffsetBottom = -RunScreen.PartyHeight - 6;
    }

    DungeonRun Run => screen.Run;
    GameData Data => screen.Main.Data;

    public override void _Ready()
    {
        // The map goes dark; a click on it closes the panel.
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.8f), AnchorRight = 1, AnchorBottom = 1 };
        dim.GuiInput += e => { if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) screen.CloseHeroPanel(); };
        AddChild(dim);
        columns = new HBoxContainer { AnchorRight = 1, AnchorBottom = 1, OffsetLeft = 60, OffsetRight = -60, OffsetTop = 18, OffsetBottom = -18 };
        columns.AddThemeConstantOverride("separation", 20);
        AddChild(columns);
        tip.AddThemeStyleboxOverride("panel", Ui.Box(new Color(0.1f, 0.09f, 0.13f, 0.98f), Ui.Gold, 1, 6));
        tipBody.AddThemeConstantOverride("separation", 3);
        tip.AddChild(tipBody);
        AddChild(tip);
        Refresh();
    }

    public void Select(Hero h)
    {
        hero = h;
        Refresh();
    }

    /// <summary>Rebuilds the panel (after any change), keeping focus on the same control where it can.</summary>
    public void Refresh()
    {
        if (columns is null) return;
        var focused = GetViewport()?.GuiGetFocusOwner() is { } f && IsAncestorOf(f) ? f.Name.ToString() : null;
        foreach (var c in columns.GetChildren()) c.QueueFree();
        HideTip();
        columns.AddChild(Panel(HeroColumn(), 0.82f));
        columns.AddChild(Panel(RightColumn(), 1f));
        // Keep the focus where it was; a keyboard or controller player opening the panel starts on a skill to raise.
        if (focused is not null)
            Callable.From(() => (FindChild(focused, true, false) as Control)?.GrabFocus()).CallDeferred();
        else if (Ui.KeyboardMode)
            Callable.From(() => (FindChildren("skill_*", "", true, false).OfType<SkillNode>().FirstOrDefault(n => n.Raisable)
                ?? FindChild("tab_skills", true, false) as Control)?.GrabFocus()).CallDeferred();
    }

    static PanelContainer Panel(Control content, float stretch)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = stretch };
        var box = Ui.Box(Card, Ui.PanelBorder, 2, 12);
        box.ContentMarginLeft = box.ContentMarginRight = 18;
        box.ContentMarginTop = box.ContentMarginBottom = 14;
        panel.AddThemeStyleboxOverride("panel", box);
        panel.AddChild(content);
        return panel;
    }

    /// <summary>A label; <paramref name="wrap"/> for long text (a wrapping label needs a width from its container).</summary>
    static Label Line(string text, int size = 13, Color? color = null, bool italic = false, bool wrap = false)
    {
        var l = Ui.Label(text, size, color);
        if (wrap) l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        if (italic) l.AddThemeFontOverride("font", new FontVariation { BaseFont = l.GetThemeDefaultFont(), VariationTransform = new Transform2D(1, 0, 0.2f, 1, 0, 0) });
        return l;
    }

    static string Num(double v) => v.ToString(Math.Abs(v) < 1 && v != 0 ? "0.##" : "0.#", CultureInfo.InvariantCulture);

    // ---- Left: the hero ----

    Control HeroColumn()
    {
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        col.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(col);

        // Portrait, name and class; the level and XP bar under the portrait.
        var top = new HBoxContainer();
        top.AddThemeConstantOverride("separation", 16);
        col.AddChild(top);
        var left = new VBoxContainer();
        left.AddThemeConstantOverride("separation", 4);
        left.AddChild(new HeroPortrait(screen, hero) { CustomMinimumSize = new Vector2(100, 100) });
        var next = Data.Levels.FirstOrDefault(l => l.Level == hero.Level + 1);
        var from = Data.Levels.First(l => l.Level == hero.Level).Xp;
        left.AddChild(new Bar(next is null ? 1 : (float)(hero.Xp - from) / Math.Max(1, next.Xp - from),
            next is null ? T("ui.xp_full") : F("ui.hp", ("hp", hero.Xp), ("max", next.Xp)), Ui.Gold) { CustomMinimumSize = new Vector2(100, 18) });
        left.AddChild(Line(F("ui.level_long", ("level", hero.Level)), 15));
        top.AddChild(left);

        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 2);
        right.AddChild(Line(hero.Name, 24, Ui.Gold));
        right.AddChild(Line(F("ui.hero_class", ("class", hero.Class.Name)), 16, Ui.Ink, italic: true));
        if (hero.Dead) right.AddChild(Line(RunLog.StatusName(Text.Strings, hero.Status), 15, Ui.Enemy));
        right.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
        // Stamina, big, with its status.
        var stamina = new HBoxContainer();
        stamina.AddThemeConstantOverride("separation", 10);
        stamina.AddChild(new Pip(hero.Status == HeroStatus.Ready ? Ui.Act : Ui.Enemy) { CustomMinimumSize = new Vector2(26, 26), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var sv = new VBoxContainer();
        sv.AddThemeConstantOverride("separation", 0);
        sv.AddChild(Line(F("ui.stamina_big", ("stamina", hero.Stamina), ("max", hero.MaxStamina)), 18));
        sv.AddChild(Line(RunLog.StatusName(Text.Strings, hero.Status), 13, hero.Status == HeroStatus.Ready ? Ui.Dim : Ui.Enemy, italic: true));
        stamina.AddChild(sv);
        right.AddChild(stamina);
        top.AddChild(right);

        // Traits at the top right, one a line: what the hero brings to Events.
        var traits = Data.Traits.Where(t => hero.Trait(t.Id) > 0).ToList();
        if (traits.Count > 0)
        {
            var list = new VBoxContainer();
            list.AddThemeConstantOverride("separation", 2);
            var head = Line(T("ui.traits"), 14, Ui.Gold);
            head.HorizontalAlignment = HorizontalAlignment.Right;
            list.AddChild(head);
            foreach (var t in traits)
            {
                var l = Line(F("ui.stat_line", ("stat", t.Name), ("value", hero.Trait(t.Id))), 14);
                l.HorizontalAlignment = HorizontalAlignment.Right;
                list.AddChild(l);
            }
            top.AddChild(list);
        }

        col.AddChild(Stats());
        if (hero.Buffs.Count > 0)
        {
            col.AddChild(Line(T("ui.run_buffs"), 14, Ui.Gold));
            foreach (var b in hero.Buffs)
                col.AddChild(Line(F(RunLog.IsCurse(Data, b.Def) ? "ui.curse_line" : "ui.buff_line", ("buff", b.Def.Name),
                    ("left", F(b.Def.Duration == DurationKind.Steps ? "run.steps" : "run.battles", ("count", b.Remaining)))), 13, wrap: true));
        }
        return scroll;
    }

    /// <summary>The primary stats (Health, Mana, Speed and the compound stats), then every other stat that differs from a
    /// default unit's, in two columns. Hovering one shows how much its skills add.</summary>
    Control Stats()
    {
        var plain = new Unit(hero.Id, hero.Unit, Side.Party, Data);
        var skilled = new Unit(hero.Id, hero.Unit, Side.Party, Data);
        hero.ApplySkills(skilled);
        var defaults = new StatBlock(Data);
        foreach (var m in skilled.Stats.Modifiers.Where(m => m.Source == Unit.DefaultSource)) defaults.Add(m);

        var rows = new List<(string Name, string Value, string? Hint)>();
        string Show(StatDef def, double v) =>
            def.Percent ? Core.Combat.CombatLog.Pct(v) : Num(Math.Round(v, 2));
        void Add(string name, string value, double unit, double total, Func<double, string> show) =>
            rows.Add((name, value, Math.Abs(total - unit) < 1e-9 ? null : F("ui.stat_from_skills", ("unit", show(unit)), ("skills", show(total - unit)))));

        var primary = new List<string> { "health", "speed" };
        if (skilled.MaxMana > 0) primary.Insert(1, "mana");
        foreach (var id in primary)
            Add(Data.Stats[id].Name, Num(skilled.Stats.Get(id)), plain.Stats.Get(id), skilled.Stats.Get(id), v => Num(v));
        foreach (var c in Data.CompoundList.Where(c => skilled.Stats.GetCompound(c.Id) != 0))
            Add(c.Name, Num(skilled.Stats.GetCompound(c.Id)), plain.Stats.GetCompound(c.Id), skilled.Stats.GetCompound(c.Id), v => Num(v));
        foreach (var def in Data.StatList.Where(s => !s.Hidden && s.Group != StatGroup.Trait && !primary.Contains(s.Id) && !Flag(s.Id)))
        {
            var v = skilled.Stats.Get(def.Id);
            if (Math.Abs(v - defaults.Get(def.Id)) < 1e-9) continue;
            Add(def.Name, Show(def, v), plain.Stats.Get(def.Id), v, x => Show(def, x));
        }
        // Tag-keyed stats (Fire Power, Melee C.Rate…): what the unit and its skills add for that tag.
        foreach (var g in skilled.Stats.Modifiers.Where(m => m.Key.Tag is not null && m.Source != Unit.DefaultSource && !Flag(m.Key.Stat)).GroupBy(m => m.Key))
        {
            var def = Data.Stats[g.Key.Stat];
            var sum = g.Sum(m => m.Value);
            rows.Add(($"{Data.Tags[g.Key.Tag!].Name} {def.Name}", (sum > 0 ? "+" : "") + Show(def, sum), null));
        }

        var grid = new GridContainer { Columns = 4 };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 3);
        foreach (var (name, value, hint) in rows)
        {
            var n = Line(name, 14, Ui.Dim);
            n.HorizontalAlignment = HorizontalAlignment.Right;
            n.AutowrapMode = TextServer.AutowrapMode.Off;
            n.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var v = Line(value, 14, hint is null ? Ui.Ink : Ui.Valid);
            v.AutowrapMode = TextServer.AutowrapMode.Off;
            v.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (hint is not null) n.TooltipText = v.TooltipText = hint;
            n.MouseFilter = v.MouseFilter = MouseFilterEnum.Pass;
            grid.AddChild(n);
            grid.AddChild(v);
        }
        return grid;
    }

    /// <summary>A switch rather than an amount (the opening crit): its text says what it does.</summary>
    static bool Flag(string stat) => stat is "opening_crit" or "opening_block" or "crit_keeps_stealth";

    // ---- Right: the skill tree or the inventory ----

    Control RightColumn()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 8);
        col.AddChild(tabs);
        Button Tab(string key, bool inventory)
        {
            var b = Ui.Button(T(key), () => { showInventory = inventory; Refresh(); });
            b.Name = inventory ? "tab_inventory" : "tab_skills";
            b.CustomMinimumSize = new Vector2(150, 40);
            if (showInventory == inventory)
            {
                b.AddThemeStyleboxOverride("normal", Ui.Box(new Color(0.24f, 0.21f, 0.14f), Ui.Gold, 2));
                b.AddThemeColorOverride("font_color", Ui.Gold);
            }
            return b;
        }
        tabs.AddChild(Tab("ui.tab_skills", inventory: false));
        tabs.AddChild(Tab("ui.tab_inventory", inventory: true));
        tabs.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = Ui.Button(T("ui.close"), () => screen.CloseHeroPanel());
        close.Name = "close";
        tabs.AddChild(close);

        if (showInventory)
        {
            var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
            var inv = Inventory();
            inv.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            scroll.AddChild(inv);
            col.AddChild(scroll);
            return col;
        }
        col.AddChild(Line(F("ui.tree_title", ("class", hero.Class.Name), ("points", hero.TreePoints)), 22, Ui.Ink));
        var row = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        row.AddThemeConstantOverride("separation", 24);
        row.AddChild(Tree());
        row.AddChild(Masteries());
        col.AddChild(row);
        col.AddChild(Line(F("ui.skill_points_left", ("points", hero.SkillPoints)), 16, hero.SkillPoints > 0 ? Ui.Gold : Ui.Dim, italic: true));
        return col;
    }

    /// <summary>
    /// The tree skills laid out as a tree: each skill under the one it requires, roots across the top, every subtree as
    /// wide as its leaves (a parent centred over its children), with elbow lines from parent to child.
    /// </summary>
    Control Tree()
    {
        var skills = Data.SkillsOf(hero.Class.Id).Where(s => s.Kind == SkillKind.Tree).OrderBy(s => s.Order).ToList();
        var children = skills.ToLookup(s => s.Requires);
        float Width(SkillDef s) => Math.Max(1, children[s.Id].Sum(Width));
        var at = new Dictionary<string, (float X, int Depth)>();
        void Lay(SkillDef s, float left, int depth)
        {
            at[s.Id] = (left + Width(s) / 2, depth);
            foreach (var c in children[s.Id])
            {
                Lay(c, left, depth + 1);
                left += Width(c);
            }
        }
        var x = 0f;
        foreach (var root in children[null])
        {
            Lay(root, x, 0);
            x += Width(root);
        }
        var view = new TreeLines { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        const float cellW = 88, cellH = 96;
        var depth = at.Count == 0 ? 0 : at.Values.Max(p => p.Depth);
        view.CustomMinimumSize = new Vector2(x * cellW, (depth + 1) * cellH);
        Vector2 Center((float X, int Depth) p) => new(p.X * cellW, p.Depth * cellH + SkillNode.Box.Y / 2 + 4);
        foreach (var s in skills)
        {
            var node = new SkillNode(this, hero, s, Run.Managing) { Name = "skill_" + s.Id };
            node.Position = Center(at[s.Id]) - SkillNode.Box / 2;
            view.AddChild(node);
            if (s.Requires is { } r)
                view.Links.Add((Center(at[r]), Center(at[s.Id]), hero.SkillLevel(r) > 0));
        }
        // Centre the tree in the space it gets.
        view.Resized += () =>
        {
            var offset = new Vector2((view.Size.X - x * cellW) / 2, 0);
            foreach (var n in view.GetChildren().OfType<SkillNode>())
                n.Position = Center(at[n.Skill.Id]) - SkillNode.Box / 2 + offset;
            view.Offset = offset;
            view.QueueRedraw();
        };
        return view;
    }

    Control Masteries()
    {
        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Begin };
        col.AddThemeConstantOverride("separation", 14);
        var i = 0;
        foreach (var m in Data.SkillsOf(hero.Class.Id).Where(s => s.Kind == SkillKind.Mastery).OrderBy(s => s.Points))
            col.AddChild(new MasteryNode(this, hero, m, ++i) { Name = "mastery_" + m.Id });
        return col;
    }

    /// <summary>Raises a skill by one level (one click, no confirmation).</summary>
    public void Raise(SkillNode node) => screen.Act(() => Run.SpendPoint(hero.Id, node.Skill.Id));

    public string SkillName(string id) => Data.Skills[id].Name;

    // ---- The tooltip beside a skill ----

    /// <summary>What a skill gives at a level: its stats (per level and steps), then the actions it grants.</summary>
    public string Gives(SkillDef skill, int level)
    {
        var parts = skill.Stats.Where(s => !Flag(s.Stat) && s.At(level) != 0)
            .GroupBy(s => (s.Stat, s.Tag))
            .Select(g =>
            {
                var v = g.Sum(s => s.At(level));
                var name = Data.Compounds.TryGetValue(g.Key.Stat, out var c) ? c.Name : Data.Stats[g.Key.Stat].Name;
                if (g.Key.Tag is { } tag) name = $"{Data.Tags[tag].Name} {name}";
                return $"{(v > 0 ? "+" : "")}{Num(v)} {name}";
            }).ToList();
        if (level > 0) parts.AddRange(skill.Actions.Select(a => F("ui.grants_action", ("action", Data.Actions[a].Name))));
        return parts.Count == 0 ? T("ui.skill_tip_none") : string.Join(", ", parts);
    }

    public void ShowTip(Control anchor, IEnumerable<(string Text, int Size, Color Color)> lines)
    {
        foreach (var c in tipBody.GetChildren()) c.QueueFree();
        foreach (var (text, size, color) in lines)
        {
            var l = Line(text, size, color, wrap: true);
            l.CustomMinimumSize = new Vector2(300, 0);
            tipBody.AddChild(l);
        }
        tip.Visible = true;
        tip.ResetSize();
        Callable.From(() =>
        {
            if (!IsInstanceValid(anchor) || !tip.Visible) return;
            var size = tip.GetCombinedMinimumSize();
            var r = anchor.GetGlobalRect();
            var pos = new Vector2(r.End.X + 10, r.Position.Y);
            if (pos.X + size.X > GetGlobalRect().End.X - 8) pos.X = r.Position.X - size.X - 10;
            pos.Y = Mathf.Clamp(pos.Y, GlobalPosition.Y + 4, GlobalPosition.Y + Size.Y - size.Y - 4);
            tip.GlobalPosition = pos;
        }).CallDeferred();
    }

    public void HideTip() => tip.Visible = false;

    // ---- Inventory: the belt and the party pack ----

    VBoxContainer Inventory()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        var exploring = Run.Managing && !hero.Dead;
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

    // ---- Small drawn pieces ----

    /// <summary>The hero's portrait (or a placeholder with its initial).</summary>
    partial class HeroPortrait(RunScreen screen, Hero hero) : Control
    {
        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            var state = hero.Dead || hero.Status == HeroStatus.Unconscious ? "knocked_out" : Core.Assets.ArtCatalog.DefaultState;
            if (screen.Main.Art.Portrait(hero.Unit.Id, state) is { } tex)
                DrawTextureRect(tex, r, false, hero.Dead ? new Color(0.4f, 0.4f, 0.4f) : Colors.White);
            else
            {
                DrawRect(r, Ui.Party.Darkened(0.45f));
                DrawString(GetThemeDefaultFont(), new Vector2(0, Size.Y * 0.62f), hero.Name[..1], HorizontalAlignment.Center, Size.X, 40, Ui.Ink);
            }
            DrawRect(r, Ui.Party, false, 2);
        }
    }

    /// <summary>A filled bar with its numbers.</summary>
    partial class Bar(float share, string text, Color color) : Control
    {
        public override void _Draw()
        {
            var r = new Rect2(Vector2.Zero, Size);
            DrawRect(r, new Color(0, 0, 0, 0.6f));
            DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Mathf.Clamp(share, 0, 1), r.Size.Y)), color.Darkened(0.15f));
            DrawString(GetThemeDefaultFont(), new Vector2(0, Size.Y - 4), text, HorizontalAlignment.Center, Size.X, 12, Colors.White);
        }
    }

    partial class Pip(Color color) : Control
    {
        public override void _Draw() => DrawCircle(Size / 2, Size.X / 2, color);
    }

    /// <summary>The tree's elbow lines, parent to child: bright once the parent has a point.</summary>
    partial class TreeLines : Control
    {
        public readonly List<(Vector2 From, Vector2 To, bool Open)> Links = [];
        public Vector2 Offset;

        public override void _Draw()
        {
            foreach (var (from, to, open) in Links)
            {
                var a = from + Offset + new Vector2(0, SkillNode.Box.Y / 2);
                var b = to + Offset - new Vector2(0, SkillNode.Box.Y / 2);
                var mid = (a.Y + b.Y) / 2;
                var color = open ? Ui.Gold : new Color(Ui.Gold, 0.3f);
                DrawPolyline([a, new Vector2(a.X, mid), new Vector2(b.X, mid), b], color, 3);
            }
        }
    }
}

/// <summary>
/// A tree skill on the Hero Panel. Four looks: locked (its prerequisite has no point: dark), maxed (bright), raisable
/// (plain), and hovered or focused (lit). One click raises it, with no confirmation.
/// </summary>
public partial class SkillNode : Button
{
    public static readonly Vector2 Box = new(64, 64);
    public SkillDef Skill { get; }
    readonly HeroPanel panel;
    readonly Hero hero;
    readonly string? why;

    public SkillNode(HeroPanel panel, Hero hero, SkillDef skill, bool open)
    {
        this.panel = panel;
        this.hero = hero;
        Skill = skill;
        why = hero.Dead ? "dead" : !open ? "busy" : hero.CantRaise(skill.Id);
        Size = Box;
        CustomMinimumSize = Box;
        FocusMode = FocusModeEnum.All;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        MouseDefaultCursorShape = why is null ? CursorShape.PointingHand : CursorShape.Arrow;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        Pressed += () => { if (why is null) panel.Raise(this); };
        MouseEntered += () => GrabFocus();
        MouseExited += () => { if (HasFocus()) ReleaseFocus(); };
        FocusEntered += () => { QueueRedraw(); ShowTip(); };
        FocusExited += () => { QueueRedraw(); panel.HideTip(); };
    }

    public bool Raisable => why is null;

    int Level => hero.SkillLevel(Skill.Id);
    bool Locked => Skill.Requires is { } r && hero.SkillLevel(r) == 0;
    bool Maxed => Level >= Skill.MaxLevel;

    void ShowTip()
    {
        var lines = new List<(string, int, Color)> { (Skill.Name, 17, Ui.Gold) };
        if (EternalDungeon.Game.Text.Strings.Has("skill." + Skill.Id)) lines.Add((T("skill." + Skill.Id), 13, Ui.Ink));
        lines.Add((F("ui.skill_tip_level", ("level", Level), ("max", Skill.MaxLevel)), 13, Ui.Dim));
        lines.Add((F("ui.skill_tip_now", ("stats", panel.Gives(Skill, Level))), 13, Ui.Ink));
        if (!Maxed) lines.Add((F("ui.skill_tip_next", ("level", Level + 1), ("stats", panel.Gives(Skill, Level + 1))), 13, Ui.Valid));
        if (Locked) lines.Add((F("ui.skill_requires", ("skill", panel.SkillName(Skill.Requires!))), 13, Ui.Enemy));
        else if (why is "no_skill_points") lines.Add((T("reason.no_skill_points"), 13, Ui.Dim));
        panel.ShowTip(this, lines);
    }

    public override void _Draw()
    {
        var font = GetThemeDefaultFont();
        var r = new Rect2(Vector2.Zero, Size);
        var lit = HasFocus() && !Locked;
        var (fill, border, width) = Locked ? (new Color(0.2f, 0.16f, 0.12f), new Color(Ui.Gold, 0.25f), 2f)
            : Maxed ? (new Color(0.72f, 0.5f, 0.14f), new Color(1f, 0.85f, 0.45f), 3f)
            : (new Color(0.42f, 0.26f, 0.08f), Ui.Gold, 3f);
        if (lit) (fill, border, width) = (fill.Lightened(0.25f), Colors.White, 4f);
        DrawRect(r, fill);
        DrawRect(r.Grow(-width / 2), border, false, width);
        var initials = string.Concat(Skill.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(w => w[0]));
        DrawString(font, new Vector2(0, 38), initials, HorizontalAlignment.Center, Size.X, 22, Locked ? new Color(Ui.Ink, 0.35f) : Ui.Ink);
        DrawString(font, new Vector2(0, Size.Y - 6), $"{Level}/{Skill.MaxLevel}", HorizontalAlignment.Right, Size.X - 6, 11,
            Locked ? new Color(Ui.Ink, 0.35f) : Colors.White);
    }
}

/// <summary>A mastery on the Hero Panel (I, II, III): bright once held, dark until its tree points are reached.</summary>
public partial class MasteryNode : Button
{
    readonly HeroPanel panel;
    readonly Hero hero;
    readonly SkillDef mastery;
    readonly int number;

    public MasteryNode(HeroPanel panel, Hero hero, SkillDef mastery, int number)
    {
        this.panel = panel;
        this.hero = hero;
        this.mastery = mastery;
        this.number = number;
        CustomMinimumSize = SkillNode.Box;
        FocusMode = FocusModeEnum.All;
        AutoTranslateMode = AutoTranslateModeEnum.Disabled;
        foreach (var style in new[] { "normal", "hover", "pressed", "disabled", "focus" })
            AddThemeStyleboxOverride(style, new StyleBoxEmpty());
        MouseEntered += () => GrabFocus();
        MouseExited += () => { if (HasFocus()) ReleaseFocus(); };
        FocusEntered += () => { QueueRedraw(); ShowTip(); };
        FocusExited += () => { QueueRedraw(); panel.HideTip(); };
    }

    bool Held => mastery.Points <= hero.TreePoints;

    void ShowTip()
    {
        var lines = new List<(string, int, Color)> { (mastery.Name, 17, Ui.Gold) };
        if (EternalDungeon.Game.Text.Strings.Has("skill." + mastery.Id)) lines.Add((T("skill." + mastery.Id), 13, Ui.Ink));
        lines.Add((panel.Gives(mastery, 1), 13, Ui.Ink));
        lines.Add(Held ? (T("ui.mastery_tip_held"), 13, Ui.Valid)
            : (F("ui.mastery_tip_locked", ("points", mastery.Points), ("now", hero.TreePoints)), 13, Ui.Dim));
        panel.ShowTip(this, lines);
    }

    public override void _Draw()
    {
        var r = new Rect2(Vector2.Zero, Size);
        var (fill, border) = Held ? (new Color(0.72f, 0.5f, 0.14f), new Color(1f, 0.85f, 0.45f)) : (new Color(0.2f, 0.16f, 0.12f), new Color(Ui.Gold, 0.35f));
        if (HasFocus()) (fill, border) = (fill.Lightened(0.2f), Colors.White);
        DrawRect(r, fill);
        DrawRect(r.Grow(-1.5f), border, false, 3);
        var numeral = number switch { 1 => "I", 2 => "II", 3 => "III", _ => number.ToString() };
        DrawString(GetThemeDefaultFont(), new Vector2(0, 41), numeral, HorizontalAlignment.Center, Size.X, 24, Held ? Colors.White : new Color(Ui.Gold, 0.5f));
    }
}
