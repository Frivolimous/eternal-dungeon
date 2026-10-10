using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>
/// Where a hero stands on Stamina, or dead (Anchor: Exploration › Expedition resources): Exhausted from 0 to −1, Severe
/// from −2 to −3, Unconscious at −4.
/// </summary>
public enum HeroStatus { Ready, Exhausted, Severe, Unconscious, Dead }

/// <summary>
/// A hero during a dungeon run: Health, Mana and Stamina carry from fight to fight (no regeneration), plus its belt
/// and the run-long buffs and curses Events gave it. Changed only by <see cref="DungeonRun"/>.
/// </summary>
public sealed class Hero
{
    public HeroDef Def { get; }
    public ClassDef Class { get; }
    public UnitDef Unit { get; }
    public string Id => Def.Id;
    public string Name => Def.Name;

    public int Health { get; internal set; }
    public int Mana { get; internal set; }
    public int Stamina { get; internal set; }
    public bool Dead { get; internal set; }

    /// <summary>Belt slots holding items (an emptied slot is removed). At most <see cref="BeltSlots"/>.</summary>
    public List<BeltSlot> Belt { get; } = [];
    public List<RunBuff> Buffs { get; } = [];

    public int MaxHealth { get; private set; }
    public int MaxMana { get; private set; }
    public int MaxStamina { get; }
    public int BeltSlots => DataLoader.BeltSlots(Class);

    readonly RunRules rules;
    readonly GameData data;

    // ---- Progression (Anchor: Classes › Skill points; M3A brief §6) ----

    public int Xp { get; internal set; }
    public int Level => data.LevelFor(Xp);

    /// <summary>Tree skill levels bought with points (masteries follow from them: <see cref="Masteries"/>).</summary>
    public Dictionary<string, int> SkillLevels { get; } = [];

    /// <summary>1 point per level, Level 1 included; points can be banked.</summary>
    public int SkillPoints => Level - SkillLevels.Values.Sum();

    /// <summary>Points spent in its Primary class's tree.</summary>
    public int TreePoints => SkillLevels.Where(kv => data.Skills[kv.Key].Class == Class.Id).Sum(kv => kv.Value);

    /// <summary>The masteries its tree points have unlocked, in order.</summary>
    public IEnumerable<SkillDef> Masteries =>
        data.SkillsOf(Class.Id).Where(s => s.Kind == SkillKind.Mastery && s.Points <= TreePoints);

    public int SkillLevel(string skill) => SkillLevels.GetValueOrDefault(skill);

    /// <summary>Why a point can't go into <paramref name="skill"/> now, as a reason code, or null if it can.</summary>
    public string? CantRaise(string skill)
    {
        if (!data.Skills.TryGetValue(skill, out var s) || s.Kind != SkillKind.Tree || s.Class != Class.Id) return "not_in_tree";
        if (SkillPoints <= 0) return "no_skill_points";
        if (SkillLevel(skill) >= s.MaxLevel) return "skill_maxed";
        if (s.Requires is { } r && SkillLevel(r) == 0) return "needs_prerequisite";
        return null;
    }

    /// <summary>Puts the hero's skills on a battle unit (see <see cref="Skills.Apply"/>).</summary>
    public void ApplySkills(Unit unit) => Skills.Apply(data, unit, SkillLevels);

    /// <summary>Recomputes the maximums from the unit and its skills. A higher maximum raises the current value by the
    /// same amount (Events never change maximums: Anchor › Never; skills do).</summary>
    internal void RefreshMaximums()
    {
        var probe = new Unit(Def.Id, Unit, Side.Party, data);
        ApplySkills(probe);
        Health = Math.Max(0, Health + probe.MaxHealth - MaxHealth);
        Mana = Math.Max(0, Mana + probe.MaxMana - MaxMana);
        MaxHealth = probe.MaxHealth;
        MaxMana = probe.MaxMana;
    }

    public Hero(GameData data, HeroDef def)
    {
        Def = def;
        Class = data.Classes[def.Class];
        Unit = data.Units[def.Unit];
        rules = data.RunRules;
        this.data = data;
        RefreshMaximums();
        MaxStamina = rules.MaxStamina;
        Health = MaxHealth;
        Mana = MaxMana;
        Stamina = MaxStamina;
        foreach (var item in def.Belt.Select(i => data.Items[i]))
            Belt.Add(new BeltSlot(item, item.Uses));
    }

    /// <summary>The lowest Stamina can go: Unconscious.</summary>
    public const int MinStamina = -4;

    public HeroStatus Status => Dead ? HeroStatus.Dead
        : Stamina <= MinStamina ? HeroStatus.Unconscious
        : Stamina <= -2 ? HeroStatus.Severe
        : Stamina <= 0 ? HeroStatus.Exhausted
        : HeroStatus.Ready;

    /// <summary>Neither dead nor unconscious: the hero can fight, be the Active Hero, and give traits and classes to
    /// Event options.</summary>
    public bool Available => Status is not (HeroStatus.Dead or HeroStatus.Unconscious);

    /// <summary>The hero's level in a trait: 1 for each of its Primary class's traits, plus +Trait buffs.</summary>
    public int Trait(string trait) =>
        (Class.Traits.Contains(trait) ? 1 : 0)
        + (int)Buffs.SelectMany(b => b.Def.Stats).Where(s => s.Stat == trait && s.Tag is null).Sum(s => s.Value);

    /// <summary>The best of <paramref name="traits"/>.</summary>
    public int BestTrait(IEnumerable<string> traits) => traits.Select(Trait).DefaultIfEmpty(0).Max();

    /// <summary>The Exhaustion penalty on this hero's Event rolls (0, or negative).</summary>
    public double RollPenalty => Status switch
    {
        HeroStatus.Exhausted => rules.ExhaustedRoll,
        HeroStatus.Severe => rules.SevereRoll,
        _ => 0,
    };

    /// <summary>The Exhaustion penalty on this hero's Speed in battle (0, or negative).</summary>
    public int SpeedPenalty => Status switch
    {
        HeroStatus.Exhausted => rules.ExhaustedSpeed,
        HeroStatus.Severe => rules.SevereSpeed,
        _ => 0,
    };

    /// <summary>Charges of <paramref name="item"/> in the belt.</summary>
    public int Charges(string item) => Belt.Where(s => s.Item.Id == item).Sum(s => s.Charges);

    public override string ToString() => $"{Name} (HP {Health}/{MaxHealth}, MP {Mana}/{MaxMana}, Stamina {Stamina}, {Status})";
}

/// <summary>A run-long buff or curse on a hero (an Event's): <see cref="Remaining"/> steps or battles left.</summary>
public sealed class RunBuff(BuffDef def)
{
    public BuffDef Def { get; } = def;
    public int Remaining { get; set; } = def.Length;

    /// <summary>Whether it changes anything in battle (combat stats, effects, procs), so it joins each battle. A
    /// buff that only raises a trait stays out.</summary>
    public bool InBattle(GameData data) =>
        Def.Stats.Any(s => data.Stats[s.Stat].Group != StatGroup.Trait) || Def.Procs.Count > 0 || Def.ShieldMaxHealth > 0
        || Def.PeriodicDamage > 0 || Def.PeriodicHeal > 0 || Def.DelayedDamage > 0 || Def.Cc != CcKind.None || Def.ManaDrain > 0;
}
