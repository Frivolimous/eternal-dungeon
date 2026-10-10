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

    /// <summary>The repo data plus extra actions, buffs and procs (test-only content). A test action's "apply:" procs
    /// (from <see cref="Action"/>) become 100% procs that apply the buff, with the action's tags.</summary>
    public static GameData With(IEnumerable<ActionDef>? actions = null, IEnumerable<BuffDef>? buffs = null,
        IEnumerable<ProcDef>? procs = null)
    {
        var extra = (actions ?? []).ToList();
        var applies = extra.SelectMany(a => a.Procs.Where(p => p.StartsWith("apply:")).Select(p => Applies(a, p))).ToList();
        return new(Repo.TagList, Repo.StatList, Repo.CompoundList, Repo.UnitList,
            [.. Repo.ActionList, .. extra],
            [.. Repo.BuffList, .. buffs ?? []],
            Repo.AiProfileList,
            Repo.EncounterList,
            Repo.UnitDefaults,
            [.. Repo.ProcList, .. applies, .. procs ?? []],
            Repo.DefaultActions,
            Repo.Text,
            [.. Repo.Classes.Values],
            Repo.Heroes,
            Repo.ItemList,
            Repo.DungeonList,
            Repo.RunRules,
            Repo.SkillList,
            Repo.Levels) { Events = Repo.Events };
    }

    static ProcDef Applies(ActionDef action, string id) =>
        Proc(id, action.Target == ActionTarget.Enemy ? ProcTrigger.Hit : ProcTrigger.ActionComplete,
            action.Target is ActionTarget.Enemy or ActionTarget.Ally ? ProcTarget.Other : ProcTarget.Self, tags: [.. action.Tags])
        with { Buff = id.Split(':')[2] };

    public static BuffDef Buff(string id, int turns = 3, int maxStacks = 1,
        StatValue[]? stats = null, int periodicDamage = 0, string[]? procs = null) =>
        new(id, id, DurationKind.Turns, turns, maxStacks, stats ?? [], 0, periodicDamage, 0, procs ?? []);

    /// <summary>A proc that does nothing until given results with <c>with</c>.</summary>
    public static ProcDef Proc(string id, ProcTrigger trigger, ProcTarget target, double chance = 1, string[]? tags = null) =>
        new(id, id, trigger, [], tags ?? [], chance, target, ProcPhase.AfterHit, Duplicates.Merge, null);

    /// <summary>A test action that applies <paramref name="buffs"/> to its target (or its user, for self actions);
    /// give it other procs with <c>with { Procs = … }</c>.</summary>
    public static ActionDef Action(string id, ActionTarget target, params string[] buffs) =>
        new(id, id, target == ActionTarget.Enemy ? ["physical", "melee"] : ["buff"], target,
            target is ActionTarget.Enemy or ActionTarget.Ally ? ActionRange.Any : null,
            100, 0, target == ActionTarget.Enemy ? 10 : 0, 0, 0, [.. buffs.Select(b => $"apply:{id}:{b}")]);
}
