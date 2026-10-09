using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>
/// A simple player for whole runs (M3A brief §8: the attrition balancing tool), with every decision on fixed rules:
/// <list type="bullet">
/// <item>Choices: the first one shown. Events list their trait and class options first, then the plain way in, then
/// leaving, so this uses the party's traits whenever it can and otherwise faces what's there.</item>
/// <item>Fights: auto-battle (the heroes' scripted AI, which drinks potions when low).</item>
/// <item>Between Events: fill belts from the pack (Mana Potions only for heroes with Mana), buy potions at an
/// Alchemist Station, use a Sanctuary or Camp when someone is low on Stamina or Health (before a boss, sooner).</item>
/// <item>Where to go: every eligible standard Node first, in map order, then deferred Events that might now go
/// differently, then the boss, then the Pathway.</item>
/// </list>
/// </summary>
public sealed class RunPolicy
{
    /// <summary>The state each deferred Event was last resumed in, so it's tried again only after something changed.</summary>
    readonly Dictionary<string, string> tried = [];

    /// <summary>Called with each result as it happens (for logs).</summary>
    public Action<RunResult>? OnResult { get; init; }

    /// <summary>Called when a fight starts, before it's played.</summary>
    public Action<DungeonRun>? OnBattle { get; init; }

    /// <summary>Plays the run until it's over or the policy has nothing left to do. Returns whether it ended.</summary>
    public bool Play(DungeonRun run, int maxActions = 2000)
    {
        if (run.Results.Count == 0) Report(run.Start());
        for (var i = 0; i < maxActions && !run.Over; i++)
            if (!Step(run)) return false;
        return run.Over;
    }

    void Report(RunResult r) => OnResult?.Invoke(r);

    /// <summary>One action. False when there's nothing to do.</summary>
    public bool Step(DungeonRun run)
    {
        switch (run.State)
        {
            case RunState.Event:
                Report(run.Choices.Count > 0 ? run.Choose(run.Choices[0].Id) : run.Continue());
                return true;
            case RunState.Battle:
                OnBattle?.Invoke(run);
                run.Battle!.Session.AutoBattle = true;
                run.Battle.Session.Advance();
                Report(run.FinishBattle());
                return true;
            case RunState.Exploring:
                return Explore(run);
            default:
                return false;
        }
    }

    static bool IsBoss(NodeState n) => n.Def.Type is NodeType.MapBoss or NodeType.FinalBoss;

    bool Explore(DungeonRun run)
    {
        Housekeeping(run);

        var next = run.Eligible.Where(n => !IsBoss(n)).FirstOrDefault();
        if (next is not null)
        {
            RestIfNeeded(run, beforeBoss: false);
            Report(run.Explore(next.Def.Id));
            return true;
        }
        var state = $"{run.Gold}|{string.Join(",", run.Flags.Where(f => f.Value).Select(f => f.Key).Order())}";
        foreach (var d in run.Deferred.Where(n => !IsBoss(n)))
        {
            if (tried.GetValueOrDefault(d.Def.Id) == state) continue;
            tried[d.Def.Id] = state;
            Report(run.Resume(d.Def.Id));
            return true;
        }
        var boss = run.Eligible.FirstOrDefault(IsBoss) ?? run.Deferred.FirstOrDefault(IsBoss);
        if (boss is not null)
        {
            RestIfNeeded(run, beforeBoss: true);
            Report(boss.Explored ? run.Resume(boss.Def.Id) : run.Explore(boss.Def.Id));
            return true;
        }
        if (run.Usable.FirstOrDefault(u => u.Interactable.Kind == InteractableKind.Pathway) is { Node: { } node })
        {
            RestIfNeeded(run, beforeBoss: true);
            Report(run.UseInteractable(node.Def.Id, InteractableKind.Pathway));
            return true;
        }
        return false;
    }

    void Housekeeping(DungeonRun run)
    {
        foreach (var (node, thing) in run.Usable.Where(u => u.Interactable.Kind == InteractableKind.AlchemistStation).ToList())
            foreach (var item in run.Data.ItemList.Where(i => i.Price > 0 && i.OutsideCombat))
                while (run.Gold >= item.Price && run.Pack.GetValueOrDefault(item.Id) < 2)
                    Report(run.Buy(node.Def.Id, item.Id));
        foreach (var item in run.Data.ItemList)
            foreach (var hero in run.Heroes.Where(h => !h.Dead))
            {
                if (UsesMana(run.Data, item) && hero.MaxMana == 0) continue;
                if (run.CantMoveToBelt(hero.Id, item.Id) is null) Report(run.MoveToBelt(hero.Id, item.Id));
            }
    }

    static bool UsesMana(GameData data, ItemDef item) =>
        data.Actions[item.Action].Procs.Select(p => data.Procs[p]).Any(p => p.ManaShare > 0);

    void RestIfNeeded(DungeonRun run, bool beforeBoss)
    {
        var heroes = run.Heroes.Where(h => !h.Dead).ToList();
        var lowStamina = heroes.Any(h => h.Stamina <= (beforeBoss ? 2 : 0));
        var health = heroes.Sum(h => h.Health) / (double)heroes.Sum(h => h.MaxHealth);
        var hurt = health < (beforeBoss ? 0.8 : 0.4);
        if (!lowStamina && !hurt) return;
        if (run.Usable.FirstOrDefault(u => u.Interactable.Kind == InteractableKind.Sanctuary) is { Node: { } node })
            Report(run.UseInteractable(node.Def.Id, InteractableKind.Sanctuary));
        else if (run.CampCharges > 0 && (lowStamina || beforeBoss))
            Report(run.Camp());
    }
}
