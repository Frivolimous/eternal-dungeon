using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

static class TestData
{
    /// <summary>The repo's data/ folder, loaded once.</summary>
    public static GameData Repo { get; } = DataLoader.LoadDirectory(TestPaths.DataDir);

    /// <summary>Loads in-memory tables: (table name, JSON rows). Every table not given is empty.</summary>
    public static GameData LoadTables(params (string Table, string Json)[] tables)
    {
        var files = Schemas.Names.ToDictionary(n => n + ".json", _ => "[]");
        files[Strings.FileName] = "keys,en\n";
        foreach (var (table, json) in tables) files[table + ".json"] = json;
        return DataLoader.Load(DataSource.FromFiles(files));
    }

    public static DataException TablesFail(params (string Table, string Json)[] tables) =>
        Assert.Throws<DataException>(() => LoadTables(tables));

    /// <summary>The repo data plus extra actions and effects (test-only content).</summary>
    public static GameData With(IEnumerable<ActionDef>? actions = null, IEnumerable<EffectDef>? effects = null,
        IEnumerable<ProcDef>? procs = null) =>
        new(Repo.TagList, Repo.StatList, Repo.CompoundList, Repo.UnitList,
            [.. Repo.ActionList, .. actions ?? []],
            [.. Repo.EffectList, .. effects ?? []],
            Repo.AiProfileList,
            Repo.EncounterList,
            Repo.UnitDefaults,
            [.. Repo.ProcList, .. procs ?? []],
            Repo.DefaultActions,
            Repo.Text);

    public static EffectDef Buff(string id, int turns = 3, bool stacking = false, int maxStacks = int.MaxValue,
        StatValue[]? stats = null, int periodicDamage = 0, string[]? procs = null) =>
        new(id, id, DurationKind.Turns, turns, stacking, maxStacks, stats ?? [], 0, 0, periodicDamage, 0, procs ?? []);

    public static EffectDef Instant(string id, double heal = 0, double shieldMaxHealth = 0) =>
        new(id, id, DurationKind.Instant, 0, false, 1, [], heal, shieldMaxHealth, 0, 0, []);

    /// <summary>A proc that does nothing until given building blocks with <c>with</c>.</summary>
    public static ProcDef Proc(string id, ProcTrigger trigger, ProcTarget target, double chance = 1, string[]? tags = null) =>
        new(id, id, trigger, [], tags ?? [], chance, target, ProcPhase.AfterHit, Duplicates.Merge, 0, 0, 0, 0, [], null, null);

    public static ActionDef Action(string id, ActionTarget target, params EffectRef[] effects) =>
        new(id, id, target == ActionTarget.Enemy ? ["physical", "melee"] : ["buff"], target,
            target is ActionTarget.Enemy or ActionTarget.Ally ? ActionRange.Any : null,
            100, 0, target == ActionTarget.Enemy ? 10 : 0, 0, 0, effects);
}
