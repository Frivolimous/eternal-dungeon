using EternalDungeon.Core.Data;
using EternalDungeon.Core.Stats;

namespace EternalDungeon.Core.Combat;

public enum Side { Party, Enemy }

/// <summary>What one hit did to a unit: how much its Shield soaked up and how much reached Health.</summary>
public readonly record struct DamageTaken(int Absorbed, int ToHealth, bool Killed);

/// <summary>
/// A unit in battle (Anchor: Stat system › Stat types). Vitals are whole numbers: Health and Mana up to their
/// stat maximums, Shield on top of Health (it absorbs damage first), and Act on the 100-point meter. Threat
/// and Vulnerability are hidden and used by enemy targeting.
/// </summary>
public sealed class Unit
{
    public const string BaseSource = "base";
    public const string DefaultSource = "default";

    public string Id { get; }
    public UnitDef Def { get; }
    public Side Side { get; }
    public StatBlock Stats { get; }

    public int Health { get; private set; }
    public int Mana { get; private set; }
    public int Shield { get; private set; }

    /// <summary>The action meter in hundredths: 10 000 = Act 100, a turn. Kept in hundredths so units of any
    /// Speed interleave exactly (each sub-tick, 1/100 of a turn, adds Speed).</summary>
    public long ActTicks { get; set; }

    /// <summary>The spell being cast, if any (Anchor: Combat › Turn order).</summary>
    public Cast? Casting { get; set; }

    public List<Buff> Buffs { get; } = [];

    /// <summary>The name in combat logs, numbered when a battle has several of the same unit ("Goblin Grunt #2").</summary>
    public string Name { get; set; }

    /// <summary>Threat earned in this battle: damage dealt plus healing done (Anchor: Enemy targeting).</summary>
    public double ThreatEarned { get; set; }

    /// <summary>Hidden. Threat earned plus the Threat stat (Cloak lowers it).</summary>
    public double Threat => ThreatEarned + Stats.Get("threat");

    /// <summary>Hidden. Rises as Health drops: the share of Health missing, plus the Vulnerability stat in percent.</summary>
    public double Vulnerability => 1 - (double)Health / Math.Max(1, MaxHealth) + Stats.Get("vulnerability") / 100;
    public bool Alive => Health > 0;
    public double Act => ActTicks / 100.0;
    public int MaxHealth => (int)Math.Round(Stats.Get("health"));
    public int MaxMana => (int)Math.Round(Stats.Get("mana"));

    /// <summary>Speed as the turn clock uses it: whole, never below 0. Stunned (by CC or a full stagger bar)
    /// it's 0; with stagger on the bar it's halved (Anchor: Combat › Crowd control).</summary>
    public int Speed
    {
        get
        {
            if (Stunned) return 0;
            var speed = Math.Max(0, (int)Stats.Get("speed"));
            return Stagger > 0 ? speed / 2 : speed;
        }
    }

    // ---- Stagger bar ----

    public const int StaggerMax = 100;

    /// <summary>0–100. Stagger damage fills it and it drains each buff-clock turn.</summary>
    public int Stagger { get; private set; }

    /// <summary>The bar hit 100: the unit is stunned and takes no stagger until the bar drains to 0
    /// (the bar shows white meanwhile).</summary>
    public bool StaggerBroken { get; private set; }

    /// <summary>Adds stagger damage; returns true when this fills the bar.</summary>
    public bool TakeStagger(int amount)
    {
        if (!Alive || StaggerBroken || amount <= 0) return false;
        Stagger = Math.Min(StaggerMax, Stagger + amount);
        if (Stagger < StaggerMax) return false;
        StaggerBroken = true;
        return true;
    }

    public void DrainStagger(int amount)
    {
        Stagger = Math.Max(0, Stagger - amount);
        if (Stagger == 0) StaggerBroken = false;
    }

    // ---- Crowd control ----

    public bool Has(CcKind cc) => Buffs.Any(b => b.Def.Cc == cc);

    public bool Stunned => StaggerBroken || Has(CcKind.Stun);

    /// <summary>Sleep and Fear take the whole turn away.</summary>
    public bool LosesTurn => Has(CcKind.Sleep) || Has(CcKind.Fear);

    /// <summary>Why the unit can't use <paramref name="action"/> right now, or null if it can.</summary>
    public string? CantUse(ActionDef action)
    {
        if (action.ManaCost > Mana) return "not enough Mana";
        if (action.Target == ActionTarget.Tile && Has(CcKind.Root)) return "rooted";
        if (action.Tags.Contains("spell") && Has(CcKind.Silence)) return "silenced";
        return null;
    }

    /// <param name="id">Unique in the battle, such as <c>goblin_grunt#2</c>.</param>
    public Unit(string id, UnitDef def, Side side, GameData data)
    {
        Id = id;
        Def = def;
        Name = def.Name;
        Side = side;
        Stats = new StatBlock(data);
        foreach (var v in data.UnitDefaults)
            Stats.Add(DefaultSource, v.Stat, v.Value, v.Tag);
        foreach (var v in def.Stats)
            Stats.Add(BaseSource, v.Stat, v.Value, v.Tag);
        foreach (var (compound, value) in def.Compounds)
            Stats.Add(BaseSource, compound, value);

        Health = MaxHealth;
        Mana = MaxMana;
        ActTicks = (long)Stats.Get("initiative") * TurnClock.TicksPerTurn;
    }

    /// <summary>Applies damage: Shield first, then Health, which stops at 0.</summary>
    public DamageTaken TakeDamage(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Alive) return new DamageTaken(0, 0, false);
        var absorbed = Math.Min(Shield, amount);
        Shield -= absorbed;
        var toHealth = Math.Min(Health, amount - absorbed);
        Health -= toHealth;
        return new DamageTaken(absorbed, toHealth, !Alive);
    }

    /// <summary>Restores Health up to the maximum; returns how much was healed. The dead can't be healed.</summary>
    public int Heal(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Alive) return 0;
        var healed = Math.Min(amount, MaxHealth - Health);
        Health += healed;
        return healed;
    }

    public void AddShield(int amount)
    {
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Shield += amount;
    }

    public void RemoveShield(int amount) => Shield = Math.Max(0, Shield - amount);

    /// <summary>Spends Mana; returns false (and spends nothing) when there isn't enough.</summary>
    public bool SpendMana(int amount)
    {
        if (amount > Mana) return false;
        Mana -= amount;
        return true;
    }

    public void RestoreMana(int amount) => Mana = Math.Min(MaxMana, Mana + Math.Max(0, amount));

    public override string ToString() => $"{Id} ({Health}/{MaxHealth})";
}

public static class BattleRules
{
    /// <summary>The winning side once one side has no living units, else null (Anchor: a battle ends when
    /// one side has no living units). If both sides fall together the party has wiped, so the enemy wins.</summary>
    public static Side? Winner(IEnumerable<Unit> units)
    {
        var list = units as IReadOnlyCollection<Unit> ?? [.. units];
        var partyAlive = list.Any(u => u.Side == Side.Party && u.Alive);
        var enemyAlive = list.Any(u => u.Side == Side.Enemy && u.Alive);
        return (partyAlive, enemyAlive) switch
        {
            (false, _) => Side.Enemy,
            (true, false) => Side.Party,
            _ => null,
        };
    }
}
