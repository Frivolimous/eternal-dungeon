using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Stats;

/// <summary>A stat, optionally keyed to a tag: "Power" (untagged) or "Fire Power".</summary>
public readonly record struct StatKey(string Stat, string? Tag = null)
{
    public override string ToString() => Tag is null ? Stat : $"{Tag} {Stat}";
}

/// <summary>One contribution to a stat from one source (base stats, a buff, an item...).</summary>
public sealed record Modifier(string Source, StatKey Key, double Value);

/// <summary>
/// Every modifier on one unit, stored per source. Totals are recomputed from the list in insertion order
/// (Anchor: Stat system). An action's effective stat combines the untagged modifiers, every tag modifier
/// whose tag the action carries (Anchor: Stat system › Tags), and each compound stat's contribution.
/// </summary>
public sealed class StatBlock(GameData data)
{
    /// <summary>Compound points are percentages, so a chance (Dim) stat gets points ÷ 100.</summary>
    public const double ChancePerPoint = 0.01;

    /// <summary>Placeholder: the most one compound stat can add to (or take from) a chance stat on one action,
    /// so huge compound values can't push a Dim modifier to ±1.</summary>
    public const double MaxCompoundChance = 0.95;

    readonly List<Modifier> modifiers = [];
    readonly List<Modifier> compoundModifiers = [];

    public IReadOnlyList<Modifier> Modifiers => modifiers;
    public IReadOnlyList<Modifier> CompoundModifiers => compoundModifiers;

    /// <summary>Adds to a stat or, when <paramref name="stat"/> names a compound stat such as "strength", to that.</summary>
    public void Add(string source, string stat, double value, string? tag = null) =>
        Add(new Modifier(source, new StatKey(stat, tag), value));

    public void Add(Modifier modifier)
    {
        if (data.Compounds.TryGetValue(modifier.Key.Stat, out var compound))
        {
            if (modifier.Key.Tag is not null)
                throw new ArgumentException($"{compound.Name} is a compound stat and can't be keyed to a tag");
            if (!double.IsFinite(modifier.Value))
                throw new ArgumentException($"{compound.Name} modifier must be a finite number, got {modifier.Value}");
            compoundModifiers.Add(modifier);
            return;
        }

        var def = Def(modifier.Key.Stat);
        if (modifier.Key.Tag is string tag)
        {
            if (!def.TagKeyed)
                throw new ArgumentException($"{def.Name} is a character stat and can't be keyed to a tag ({tag})");
            if (!data.Tags.ContainsKey(tag))
                throw new ArgumentException($"Unknown tag \"{tag}\"");
        }
        Combine.Validate(def, modifier.Value);
        modifiers.Add(modifier);
    }

    /// <summary>Removes every modifier from <paramref name="source"/>; returns how many were removed.</summary>
    public int RemoveSource(string source) =>
        modifiers.RemoveAll(m => m.Source == source) + compoundModifiers.RemoveAll(m => m.Source == source);

    /// <summary>The untagged total: the whole value of a character stat, or the part of an attack/defense stat
    /// that applies to every action. Compound stats only feed tag stats, so they never count here.</summary>
    public double Get(string stat) => Total(Def(stat), m => m.Key.Tag is null);

    /// <summary>The stat as it applies to an action carrying <paramref name="actionTags"/>.</summary>
    public double Get(string stat, IReadOnlyCollection<string> actionTags)
    {
        var def = Def(stat);
        var values = Values(stat, m => m.Key.Tag is null || actionTags.Contains(m.Key.Tag))
            .Concat(CompoundContributions(def, actionTags));
        return Combine.Total(def.Combine, values);
    }

    /// <summary>The total of exactly one key, e.g. just "Fire Power", for display.</summary>
    public double Get(StatKey key) => Total(Def(key.Stat), m => m.Key.Tag == key.Tag);

    /// <summary>A compound stat's own total (compound stats add).</summary>
    public double GetCompound(string compound) =>
        data.Compounds.ContainsKey(compound)
            ? compoundModifiers.Where(m => m.Key.Stat == compound).Sum(m => m.Value)
            : throw new ArgumentException($"Unknown compound stat \"{compound}\"");

    /// <summary>
    /// Each compound stat adds one value to <paramref name="def"/> for an action: its total × the sum of the
    /// coefficients of every recipe row for that stat whose tag the action carries. Strength 10 on a
    /// Melee + Heavy action: 10 × (1 + 0.5) = 15 Power.
    /// </summary>
    IEnumerable<double> CompoundContributions(StatDef def, IReadOnlyCollection<string> actionTags)
    {
        foreach (var compound in data.CompoundList)
        {
            var coef = compound.Rows.Where(r => r.Stat == def.Id && actionTags.Contains(r.Tag)).Sum(r => r.Coef);
            if (coef == 0) continue;
            var points = compoundModifiers.Where(m => m.Key.Stat == compound.Id).Sum(m => m.Value);
            if (points == 0) continue;
            var value = points * coef;
            if (def.Combine == CombineMode.Dim)
                value = Math.Clamp(value * ChancePerPoint, -MaxCompoundChance, MaxCompoundChance);
            yield return value;
        }
    }

    double Total(StatDef def, Func<Modifier, bool> applies) => Combine.Total(def.Combine, Values(def.Id, applies));

    IEnumerable<double> Values(string stat, Func<Modifier, bool> applies) =>
        modifiers.Where(m => m.Key.Stat == stat && applies(m)).Select(m => m.Value);

    StatDef Def(string stat) =>
        data.Stats.TryGetValue(stat, out var def) ? def : throw new ArgumentException($"Unknown stat \"{stat}\"");
}
