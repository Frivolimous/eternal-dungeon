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
/// (Anchor: Stat system). An action's effective stat combines the untagged modifiers with every tag modifier
/// whose tag the action carries (Anchor: Stat system › Tags).
/// </summary>
public sealed class StatBlock(GameData data)
{
    readonly List<Modifier> modifiers = [];

    public IReadOnlyList<Modifier> Modifiers => modifiers;

    public void Add(string source, string stat, double value, string? tag = null) =>
        Add(new Modifier(source, new StatKey(stat, tag), value));

    public void Add(Modifier modifier)
    {
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
    public int RemoveSource(string source) => modifiers.RemoveAll(m => m.Source == source);

    /// <summary>The untagged total: the whole value of a character stat, or the part of an attack/defense stat
    /// that applies to every action.</summary>
    public double Get(string stat) => Total(stat, m => m.Key.Tag is null);

    /// <summary>The stat as it applies to an action carrying <paramref name="actionTags"/>.</summary>
    public double Get(string stat, IReadOnlyCollection<string> actionTags) =>
        Total(stat, m => m.Key.Tag is null || actionTags.Contains(m.Key.Tag));

    /// <summary>The total of exactly one key, e.g. just "Fire Power", for display.</summary>
    public double Get(StatKey key) => Total(key.Stat, m => m.Key.Tag == key.Tag);

    double Total(string stat, Func<Modifier, bool> applies)
    {
        var def = Def(stat);
        return Combine.Total(def.Combine, modifiers.Where(m => m.Key.Stat == stat && applies(m)).Select(m => m.Value));
    }

    StatDef Def(string stat) =>
        data.Stats.TryGetValue(stat, out var def) ? def : throw new ArgumentException($"Unknown stat \"{stat}\"");
}
