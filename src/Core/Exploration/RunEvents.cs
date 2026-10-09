using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>The Event in progress: its Node, the block waiting for the player (a story block, or a fight), and the
/// Active Hero.</summary>
public sealed class EventRun(NodeState node, EventDef def, Hero active)
{
    public NodeState Node { get; } = node;
    public EventDef Def { get; } = def;
    public Hero Active { get; internal set; } = active;
    public EventBlock? Block { get; internal set; }

    /// <summary>Set by a complete_dungeon action: the dungeon is complete when this Event ends.</summary>
    internal bool Completes { get; set; }
}

// The event engine (Anchor: Exploration › Events, Event blocks, The Active Hero).
public sealed partial class DungeonRun
{
    /// <summary>The Event in progress (while <see cref="State"/> is Event or Battle), or null.</summary>
    public EventRun? Event { get; private set; }

    /// <summary>Guard against blocks that keep leading to each other without a story block (the loader checks too).</summary>
    const int MaxBlocksInARow = 200;

    /// <summary>The choices the current story block shows: every choice whose conditions hold.</summary>
    public IReadOnlyList<EventChoice> Choices =>
        Event?.Block is StoryBlock s && State == RunState.Event ? [.. s.Choices.Where(c => Holds(c.Conditions))] : [];

    /// <summary>Takes a choice in the current story block.</summary>
    public RunResult Choose(string choiceId)
    {
        if (State != RunState.Event || Event!.Block is not StoryBlock story || story.Choices.Count == 0)
            throw new InvalidOperationException("No choice to make now");
        var choice = Choices.FirstOrDefault(c => c.Id == choiceId)
            ?? throw new InvalidOperationException($"\"{choiceId}\" isn't one of the choices shown");
        var r = new RunResult();
        var hero = PickHero(choice);
        if (hero != Event.Active)
        {
            Event.Active = hero;
            r.Add(new ActiveHeroChanged(hero));
        }
        r.Add(new ChoiceMade(Event.Def, story, choice, hero));
        string? next = choice.Success;
        if (choice.Roll is { } roll)
        {
            var result = rng.Roll(Chance(roll, hero));
            r.Add(new EventRolled(hero, roll.Traits, hero.BestTrait(roll.Traits), hero.RollPenalty, result));
            next = result.Success ? choice.Success : choice.Failure;
        }
        Run(next, r);
        return Record(r);
    }

    /// <summary>Continues past a story block without choices (no link: the Event closes).</summary>
    public RunResult Continue()
    {
        if (State != RunState.Event || Event!.Block is not StoryBlock story || story.Choices.Count > 0)
            throw new InvalidOperationException("Nothing to continue");
        var r = new RunResult();
        Run(story.Success, r);
        return Record(r);
    }

    /// <summary>A roll's chance for <paramref name="hero"/>: base, plus per trait point, plus its Exhaustion penalty,
    /// kept between 0 and 1.</summary>
    public static double Chance(EventRoll roll, Hero hero) =>
        Math.Clamp(roll.Base + roll.PerPoint * hero.BestTrait(roll.Traits) + hero.RollPenalty, 0, 1);

    /// <summary>
    /// The heroes best placed to make <paramref name="choice"/> (Anchor: The Active Hero): for a roll, the best chance;
    /// for a trait, the best level; for a class, those who have it; otherwise the Active Hero. Pure: ties are broken
    /// when the choice is taken.
    /// </summary>
    public IReadOnlyList<Hero> BestHeroes(EventChoice choice)
    {
        var active = Event?.Active;
        // Only heroes who meet the choice's own class and trait conditions can make it.
        var candidates = Available.Where(h => (choice.HeroClass is not { } c || h.Class.Id == c)
            && choice.Conditions.OfType<TraitCondition>().All(t => h.BestTrait(t.Traits) >= t.Min)).ToList();
        if (choice.Roll is { } roll)
            return Best(candidates, h => Chance(roll, h));
        if (choice.HeroTraits.Count > 0)
            return Best(candidates, h => h.BestTrait(choice.HeroTraits));
        if (choice.HeroClass is not null) return candidates.Contains(active!) ? [active!] : candidates;
        return active is null ? [] : [active];
    }

    static List<Hero> Best(List<Hero> heroes, Func<Hero, double> value)
    {
        if (heroes.Count == 0) return [];
        var best = heroes.Max(value);
        return [.. heroes.Where(h => value(h) == best)];
    }

    /// <summary>The hero who takes the choice: the best one; on a tie, the Active Hero, otherwise a random one of the tied.</summary>
    Hero PickHero(EventChoice choice)
    {
        var tied = BestHeroes(choice);
        if (tied.Contains(Event!.Active)) return Event.Active;
        return tied.Count == 1 ? tied[0] : tied[rng.NextInt(tied.Count)];
    }

    // ---- Conditions (pure) ----

    public bool Holds(IEnumerable<EventCondition> conditions) => conditions.All(Holds);

    public bool Holds(EventCondition condition) => condition switch
    {
        TraitCondition t => Available.Any(h => h.BestTrait(t.Traits) >= t.Min),
        ClassCondition c => Available.Any(h => h.Class.Id == c.Class),
        FlagCondition { EventOnly: true } f => (Event?.Node.EventFlags.GetValueOrDefault(f.Key) ?? false) == f.Value,
        FlagCondition f => Flags.GetValueOrDefault(f.Key) == f.Value,
        GoldCondition g => Gold >= g.Min,
        ResourceCondition x => x.All
            ? Available.All(h => Amount(h, x.Resource) >= x.Min)
            : Available.Any(h => Amount(h, x.Resource) >= x.Min),
        _ => throw new InvalidOperationException($"Unknown condition {condition}"),
    };

    static int Amount(Hero hero, EventResource resource) => resource switch
    {
        EventResource.Health => hero.Health,
        EventResource.Mana => hero.Mana,
        _ => hero.Stamina,
    };

    // ---- Running blocks ----

    void StartEvent(NodeState node, string block, bool resumed, RunResult r)
    {
        var heroes = Available.ToList();
        if (heroes.Count == 0) return;
        Event = new EventRun(node, Data.Events[node.Def.Event!], heroes[rng.NextInt(heroes.Count)]);
        State = RunState.Event;
        r.Add(new EventStarted(node.Def, Event.Def, Event.Active, resumed));
        Run(block, r);
    }

    /// <summary>Runs blocks from <paramref name="id"/> until one needs the player (a story block), a fight starts, or the
    /// Event ends (no link: it closes).</summary>
    void Run(string? id, RunResult r)
    {
        var e = Event!;
        for (var steps = 0; ; steps++)
        {
            if (steps > MaxBlocksInARow) throw new InvalidOperationException($"{e.Def.Id}: blocks keep leading to each other");
            // A hero who dies or falls unconscious hands the lead on; with nobody left, the Event ends at once.
            if (!e.Active.Available)
            {
                var left = Available.ToList();
                if (left.Count == 0) id = null;
                else
                {
                    e.Active = left[rng.NextInt(left.Count)];
                    r.Add(new ActiveHeroChanged(e.Active));
                }
            }
            if (id is null)
            {
                Close(r);
                return;
            }
            var block = e.Def.Block(id);
            e.Block = block;
            switch (block)
            {
                case StoryBlock s:
                    r.Add(new StoryShown(e.Def, s, e.Active, Choices));
                    return;
                case BranchBlock b:
                    id = b.Options.FirstOrDefault(o => Holds(o.Conditions))?.Success;
                    break;
                case CombatBlock c:
                    StartBattle(c, r);
                    return;
                case ResourceBlock x:
                    ApplyResource(x, r);
                    id = x.Success;
                    break;
                case ActionBlock a:
                    foreach (var action in a.Actions)
                    {
                        if (action is DeferAction d)
                        {
                            Defer(d.Resume, r);
                            return;
                        }
                        Apply(action, r);
                    }
                    id = a.Success;
                    break;
                case RewardBlock w:
                    foreach (var reward in w.Rewards) Apply(reward, r);
                    id = w.Success;
                    break;
            }
        }
    }

    IEnumerable<Hero> Targets(EventTarget target)
    {
        var heroes = Available.ToList();
        if (heroes.Count == 0) return [];
        return target switch
        {
            EventTarget.Active => [Event!.Active],
            EventTarget.Random => [heroes[rng.NextInt(heroes.Count)]],
            _ => heroes,
        };
    }

    void ApplyResource(ResourceBlock x, RunResult r)
    {
        foreach (var hero in Targets(x.Target).ToList())
        {
            var max = x.Resource == EventResource.Health ? hero.MaxHealth : hero.MaxMana;
            ChangeResource(hero, x.Resource, x.Share != 0 ? Share(x.Share, max) : x.Amount, r);
        }
    }

    void Apply(EventAction action, RunResult r)
    {
        var e = Event!;
        switch (action)
        {
            case SetFlagAction { EventOnly: true } f:
                e.Node.EventFlags[f.Key] = f.Value;
                r.Add(new FlagSet(f.Key, f.Value, true));
                break;
            case SetFlagAction f:
                Flags[f.Key] = f.Value;
                r.Add(new FlagSet(f.Key, f.Value, false));
                break;
            case RevealAction v:
                foreach (var id in v.Nodes)
                {
                    var node = Nodes[id];
                    if (node.Explored) continue;
                    node.Revealed = v.Icon;
                    r.Add(new NodeRevealed(node.Def, v.Icon));
                }
                break;
            case BuffAction b:
                var def = Data.Buffs[b.Buff];
                foreach (var hero in Targets(b.Target).ToList())
                {
                    // The same buff again starts its count over.
                    if (hero.Buffs.FirstOrDefault(x => x.Def == def) is { } held) held.Remaining = def.Length;
                    else hero.Buffs.Add(new RunBuff(def));
                    r.Add(new BuffGained(hero, def));
                }
                break;
            case GoldAction g:
                var change = Math.Max(-Gold, g.Amount);
                Gold += change;
                if (change != 0) r.Add(new GoldChanged(change, Gold));
                break;
            case CampAction c:
                var camps = Math.Max(-CampCharges, c.Amount);
                CampCharges += camps;
                if (camps != 0) r.Add(new CampChargesChanged(camps, CampCharges));
                break;
            case SpawnAction s:
                e.Node.Interactables.Add(new InteractableState(s.Kind));
                r.Add(new InteractableSpawned(e.Node.Def, s.Kind));
                break;
            case CompleteDungeonAction:
                e.Completes = true;
                break;
        }
    }

    void Apply(EventReward reward, RunResult r)
    {
        switch (reward.Kind)
        {
            case RewardKind.Gold:
                Gold += reward.Amount;
                r.Add(new GoldChanged(reward.Amount, Gold));
                break;
            case RewardKind.Camp:
                CampCharges += reward.Amount;
                r.Add(new CampChargesChanged(reward.Amount, CampCharges));
                break;
            default:
                AddToPack(Data.Items[reward.Item!], reward.Amount, r);
                break;
        }
    }

    /// <summary>The Event stays on its Node and resumes at <paramref name="block"/> (Anchor: Closed and deferred).</summary>
    void Defer(string block, RunResult r)
    {
        var e = Event!;
        e.Node.Event = EventStatus.Deferred;
        e.Node.ResumeBlock = block;
        r.Add(new EventDeferred(e.Node.Def, e.Def));
        End(r);
    }

    void Close(RunResult r)
    {
        var e = Event!;
        e.Node.Event = EventStatus.Closed;
        e.Node.ResumeBlock = null;
        r.Add(new EventClosed(e.Node.Def, e.Def));
        End(r);
    }

    void End(RunResult r)
    {
        var completes = Event!.Completes;
        Event = null;
        State = RunState.Exploring;
        CheckParty(r);
        if (completes && !Over)
        {
            State = RunState.Completed;
            r.Add(new DungeonCompleted(Dungeon));
        }
    }
}
