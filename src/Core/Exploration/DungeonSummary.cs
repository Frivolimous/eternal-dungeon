using System.Globalization;
using System.Text;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>
/// Totals over many auto-played runs of one dungeon (<c>sim dungeon --runs N</c>): how often it's completed, where
/// parties wipe, what the party has left at each boss, and what it spent. The attrition balancing tool (M3A brief §8).
/// </summary>
public sealed class DungeonSummary
{
    int runs, completed, wiped, stuck;
    readonly Dictionary<string, int> wipes = [];
    readonly Dictionary<string, List<(double Health, double Mana, double Stamina, int Alive, double Level)>> atBoss = [];
    double battles, victories, flees, camps, sanctuaries, items, steps, gold, dead;

    /// <summary>Plays one run with <see cref="RunPolicy"/> and adds it.</summary>
    public DungeonRun Play(GameData data, string dungeon, ulong seed)
    {
        var run = new DungeonRun(data, dungeon, seed);
        string? lastNode = null;
        var policy = new RunPolicy
        {
            OnResult = r =>
            {
                foreach (var e in r.Of<EventStarted>()) lastNode = e.Node.Name;
            },
            OnBattle = Snapshot,
        };
        var ended = policy.Play(run);
        Add(run, ended, lastNode);
        return run;
    }

    void Snapshot(DungeonRun run)
    {
        if (run.Battle!.Block.Scale != CombatScale.Boss) return;
        var name = run.Battle.Session.Encounter.Name;
        var heroes = run.Battle.Heroes;
        if (!atBoss.TryGetValue(name, out var list)) atBoss[name] = list = [];
        var withMana = heroes.Where(h => h.MaxMana > 0).ToList();
        list.Add((heroes.Sum(h => h.Health) / (double)heroes.Sum(h => h.MaxHealth),
            withMana.Count == 0 ? 0 : withMana.Sum(h => h.Mana) / (double)withMana.Sum(h => h.MaxMana),
            heroes.Average(h => h.Stamina), heroes.Count, heroes.Average(h => h.Level)));
    }

    void Add(DungeonRun run, bool ended, string? lastNode)
    {
        runs++;
        if (run.State == RunState.Completed) completed++;
        else if (run.State == RunState.Wiped)
        {
            wiped++;
            var where = lastNode ?? "?";
            wipes[where] = wipes.GetValueOrDefault(where) + 1;
        }
        else if (!ended) stuck++;
        var s = run.Stats;
        battles += s.Battles;
        victories += s.Victories;
        flees += s.Flees;
        camps += s.Camps;
        sanctuaries += s.Sanctuaries;
        items += s.ItemsUsed;
        steps += s.Steps;
        gold += run.Gold;
        if (run.State == RunState.Completed) dead += run.Heroes.Count(h => h.Dead);
    }

    static string Pct(double x) => (x * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
    static string F(double x) => x.ToString("0.0", CultureInfo.InvariantCulture);

    public string Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Completed {completed}/{runs} ({Pct(completed / (double)runs)}), wiped {wiped}{(stuck > 0 ? $", stuck {stuck}" : "")}");
        if (wipes.Count > 0)
        {
            sb.AppendLine("Wipes by Node (the last Event entered):");
            foreach (var (where, n) in wipes.OrderByDescending(w => w.Value))
                sb.AppendLine($"  {where,-28} {n}");
        }
        if (atBoss.Count > 0)
        {
            sb.AppendLine("At each boss fight's start (average party Health, Mana, Stamina, heroes standing, hero level):");
            foreach (var (boss, list) in atBoss)
                sb.AppendLine($"  {boss,-28} HP {Pct(list.Average(x => x.Health))}, MP {Pct(list.Average(x => x.Mana))}, " +
                    $"Stamina {F(list.Average(x => x.Stamina))}, heroes {F(list.Average(x => x.Alive))}, level {F(list.Average(x => x.Level))} ({list.Count} fights)");
        }
        sb.AppendLine($"Per run: {F(steps / runs)} Nodes, {F(battles / runs)} battles ({F(victories / runs)} won, {F(flees / runs)} fled), " +
            $"{F(camps / runs)} Camps, {F(sanctuaries / runs)} Sanctuaries, {F(items / runs)} belt charges used, {F(gold / runs)} Gold left");
        if (completed > 0) sb.AppendLine($"Heroes dead at the end of a completed run: {F(dead / completed)}");
        return sb.ToString();
    }
}
