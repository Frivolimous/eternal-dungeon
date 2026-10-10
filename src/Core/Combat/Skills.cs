using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// Tree skills and masteries on a unit (Anchor: Classes › Skill trees, Masteries). A hero's tree levels decide its
/// masteries (a mastery is held once its class's tree has 1, 6 or 11 points), and every skill held adds its stats (per
/// level × level), grants its actions and grants its procs.
/// </summary>
public static class Skills
{
    /// <summary>The points spent in each class's tree.</summary>
    public static Dictionary<string, int> TreePoints(GameData data, IReadOnlyDictionary<string, int> levels) =>
        levels.Where(kv => kv.Value > 0 && data.Skills[kv.Key].Kind == SkillKind.Tree)
            .GroupBy(kv => data.Skills[kv.Key].Class)
            .ToDictionary(g => g.Key, g => g.Sum(kv => kv.Value));

    /// <summary>Every skill held and its level: the tree levels given, plus each mastery their points unlock.</summary>
    public static Dictionary<string, int> Held(GameData data, IReadOnlyDictionary<string, int> levels)
    {
        var held = levels.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
        foreach (var (cls, points) in TreePoints(data, levels))
            foreach (var m in data.SkillsOf(cls).Where(s => s.Kind == SkillKind.Mastery && s.Points <= points))
                held[m.Id] = 1;
        return held;
    }

    /// <summary>The source key of a skill's stat modifiers on a unit.</summary>
    public static string Source(string skillId) => $"skill:{skillId}";

    /// <summary>
    /// Puts <paramref name="levels"/> (tree skills; masteries follow) on a unit before its battle starts: stats, actions
    /// and procs. Its Act restarts from its Initiative, which a skill may have raised. Health and Mana stay as they were
    /// (the caller sets them: full for a fresh unit, the run's for a hero).
    /// </summary>
    public static void Apply(GameData data, Unit unit, IReadOnlyDictionary<string, int> levels)
    {
        foreach (var (id, level) in Held(data, levels).OrderBy(kv => data.Skills[kv.Key].Order))
        {
            var skill = data.Skills[id];
            foreach (var s in skill.Stats)
                unit.Stats.Add(Source(id), s.Stat, s.PerLevel * level, s.Tag);
            unit.GrantedActions.AddRange(skill.Actions.Where(a => !unit.GrantedActions.Contains(a)));
            unit.GrantedProcs.AddRange(skill.Procs);
        }
        unit.ActTicks = (long)unit.Stats.Get("initiative") * TurnClock.TicksPerTurn;
    }
}
