using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

static class TestData
{
    /// <summary>The repo's data/ folder, loaded once.</summary>
    public static GameData Repo { get; } = DataLoader.LoadDirectory(TestPaths.DataDir);

    /// <summary>The repo data plus extra actions and effects (test-only content).</summary>
    public static GameData With(IEnumerable<ActionDef>? actions = null, IEnumerable<EffectDef>? effects = null) =>
        new(Repo.TagList, Repo.StatList, Repo.CompoundList, Repo.UnitList,
            [.. Repo.ActionList, .. actions ?? []],
            [.. Repo.EffectList, .. effects ?? []]);

    public static EffectDef Buff(string id, int turns = 3, bool stacking = false, int maxStacks = int.MaxValue,
        StatValue[]? stats = null, int periodicDamage = 0, TriggerDef[]? triggers = null) =>
        new(id, id, DurationKind.Turns, turns, stacking, maxStacks, stats ?? [], 0, 0, periodicDamage, 0, triggers ?? []);

    public static EffectDef Instant(string id, double heal = 0, double shieldMaxHealth = 0) =>
        new(id, id, DurationKind.Instant, 0, false, 1, [], heal, shieldMaxHealth, 0, 0, []);

    public static ActionDef Action(string id, ActionTarget target, params EffectRef[] effects) =>
        new(id, id, target == ActionTarget.Enemy ? ["physical", "melee"] : ["buff"], target,
            target is ActionTarget.Enemy or ActionTarget.Ally ? ActionRange.Any : null,
            100, 0, target == ActionTarget.Enemy ? 10 : 0, 0, 0, effects);
}
