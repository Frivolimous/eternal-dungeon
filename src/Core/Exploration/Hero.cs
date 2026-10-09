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

    public int MaxHealth { get; }
    public int MaxMana { get; }
    public int MaxStamina { get; }
    public int BeltSlots => DataLoader.BeltSlots(Class);

    readonly RunRules rules;

    public Hero(GameData data, HeroDef def)
    {
        Def = def;
        Class = data.Classes[def.Class];
        Unit = data.Units[def.Unit];
        rules = data.RunRules;
        // The maximums are the unit's own (exploration never changes them: Anchor › Never).
        var probe = new Unit(def.Id, Unit, Side.Party, data);
        MaxHealth = probe.MaxHealth;
        MaxMana = probe.MaxMana;
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
