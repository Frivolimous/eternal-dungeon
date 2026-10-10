namespace EternalDungeon.Core.Combat;

// Skill effects that need the battle (Anchor: Classes › The starting classes): Imposing Presence's opening Block,
// Opportunist's AP refund and Elemental Ward's aura. The rest are read where they apply (Resolution, RollCrit, Resolve).
public sealed partial class Battle
{
    /// <summary>The stat source of Imposing Presence's doubled Block.</summary>
    public const string OpeningBlockSource = "opening_block";

    /// <summary>The stat source of what adjacent allies give (Elemental Ward).</summary>
    public const string AuraSource = "aura";

    /// <summary>
    /// Elemental Ward: each unit with it gives every ally next to it (same area, a side touching) its ward as Elemental
    /// Avoid. Recomputed at the start and after every result, so it follows moves and deaths.
    /// </summary>
    void RefreshAuras()
    {
        var givers = Units.Where(u => u.Alive && u.Stats.Get("elemental_ward") > 0).ToList();
        foreach (var u in Units) u.Stats.RemoveSource(AuraSource);
        foreach (var giver in givers)
        {
            var ward = giver.Stats.Get("elemental_ward");
            foreach (var ally in Units.Where(u => u != giver && u.Alive && u.Side == giver.Side && Adjacent(giver, u)))
                ally.Stats.Add(AuraSource, "avoid", ward, "elemental");
        }
    }

    bool Adjacent(Unit a, Unit b)
    {
        if (Grid.AnchorOf(a) is not { } ta || Grid.AnchorOf(b) is not { } tb || ta.Area != tb.Area) return false;
        var fb = Grid.Footprint(b).ToList();
        return Grid.Footprint(a).Any(x => fb.Any(y => Math.Abs(x.Row - y.Row) + Math.Abs(x.Col - y.Col) == 1));
    }

    /// <summary>
    /// Opportunist (decided 2026-10-08): an attack on a Stunned target gives back its cap of the action's AP; on any
    /// other target, the share of Speed the target has lost to buffs and debuffs, up to the cap (Chill's −30 on a base
    /// 100 is 30%). Stagger and Exhaustion aren't buffs, so they don't count. Worked out before the attack lands.
    /// </summary>
    static int OpportunistRefund(Unit actor, EternalDungeon.Core.Data.ActionDef action, Unit? target)
    {
        var cap = actor.Stats.Get("opportunist");
        if (cap <= 0 || action.Target != EternalDungeon.Core.Data.ActionTarget.Enemy || target is null) return 0;
        var share = target.Stunned ? cap : Math.Min(cap, SpeedLostToBuffs(target));
        return (int)Math.Round(share * action.ApCost, MidpointRounding.AwayFromZero);
    }

    /// <summary>The share of its Speed a unit has lost to buffs and debuffs (0 if they add up to a gain).</summary>
    public static double SpeedLostToBuffs(Unit unit)
    {
        var lost = -unit.Stats.Modifiers.Where(m => m.Source.StartsWith("buff:") && m.Key.Stat == "speed" && m.Key.Tag is null)
            .Sum(m => m.Value);
        if (lost <= 0) return 0;
        var without = unit.Stats.Get("speed") + lost;
        return without <= 0 ? 0 : Math.Min(1, lost / without);
    }
}
