using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>Where a run stands: exploring (any exploration action), in an Event (waiting for a choice or Continue),
/// in a battle (play it through <see cref="DungeonRun.Battle"/>, then <see cref="DungeonRun.FinishBattle"/>), or over.</summary>
public enum RunState { Exploring, Event, Battle, Completed, Wiped }

/// <summary>Where a Node's Event stands: none, waiting to be found, deferred (it resumes), or closed.</summary>
public enum EventStatus { None, Pending, Deferred, Closed }

/// <summary>A lasting feature of a Node (Anchor: Exploration › Interactables); a Sanctuary is used once.</summary>
public sealed class InteractableState(InteractableKind kind)
{
    public InteractableKind Kind { get; } = kind;
    public bool Used { get; internal set; }
}

/// <summary>One Node during a run: explored or not, revealed by an Event, its Event's state and its Interactables.</summary>
public sealed class NodeState
{
    public NodeDef Def { get; }
    public bool Explored { get; internal set; }
    /// <summary>Set once a map reveal has shown the Node (with its icon), null if none has.</summary>
    public RevealIcon? Revealed { get; internal set; }
    public EventStatus Event { get; internal set; }
    /// <summary>The block a deferred Event resumes at.</summary>
    public string? ResumeBlock { get; internal set; }
    /// <summary>The Event's own state (Anchor: Event state): kept while it's deferred.</summary>
    public Dictionary<string, bool> EventFlags { get; } = [];
    public List<InteractableState> Interactables { get; }

    public NodeState(NodeDef def)
    {
        Def = def;
        Event = def.Event is null ? EventStatus.None : EventStatus.Pending;
        Interactables = [.. def.Interactables.Select(k => new InteractableState(k))];
    }

    /// <summary>The Node's Interactables can be used once its Event is closed (or it has none).</summary>
    public bool Settled => Explored && Event is EventStatus.None or EventStatus.Closed;
}

/// <summary>
/// One expedition into a dungeon (Anchor: Exploration, Dungeons; M3A brief §1), driven like a
/// <see cref="BattleSession"/>: pure, deterministic and seeded. Every action returns a <see cref="RunResult"/> for the
/// screen to show and the log to print. Events run block by block until one needs the player (a story block) or a
/// fight starts; a fight is a <see cref="BattleSession"/> the caller plays, then hands back with
/// <see cref="FinishBattle"/>. All randomness (the Active Hero, rolls, random targets, each battle's seed) comes from
/// the run's one RNG.
/// </summary>
public sealed partial class DungeonRun
{
    public GameData Data { get; }
    public DungeonDef Dungeon { get; }
    public ulong Seed { get; }
    readonly Rng rng;

    public RunState State { get; private set; } = RunState.Exploring;
    public int MapIndex { get; private set; }
    public MapDef Map => Dungeon.Maps[MapIndex];

    public IReadOnlyList<Hero> Heroes { get; }
    public IReadOnlyDictionary<string, NodeState> Nodes { get; }

    /// <summary>Dungeon flags (Anchor: Events): one flat dictionary for the whole expedition. Unset reads as false.</summary>
    public Dictionary<string, bool> Flags { get; } = [];

    /// <summary>Spare item charges, by item id (Anchor: Equipment › Party pack).</summary>
    public Dictionary<string, int> Pack { get; } = [];

    public int Gold { get; private set; }
    public int CampCharges { get; private set; }

    /// <summary>Nodes explored so far (a step each).</summary>
    public int Steps { get; private set; }

    public List<RunResult> Results { get; } = [];
    public RunStats Stats { get; } = new();

    public DungeonRun(GameData data, string dungeon, ulong seed)
    {
        Data = data;
        Dungeon = data.Dungeons[dungeon];
        Seed = seed;
        rng = new Rng(seed);
        Heroes = [.. data.Heroes.Select(h => new Hero(data, h))];
        if (Heroes.Count == 0) throw new InvalidOperationException("The data has no heroes to start a run with");
        Nodes = Dungeon.Maps.SelectMany(m => m.Nodes).ToDictionary(n => n.Id, n => new NodeState(n));
        CampCharges = Dungeon.CampCharges;
    }

    bool started;

    /// <summary>The party arrives at the first Map's start Node (its Event, if any, begins).</summary>
    public RunResult Start()
    {
        if (started) throw new InvalidOperationException("The run has already started");
        started = true;
        var r = new RunResult();
        EnterMap(0, r);
        return Record(r);
    }

    public bool Over => State is RunState.Completed or RunState.Wiped;

    // ---- What the player can do ----

    /// <summary>Nodes of the current Map the party can explore: unexplored and touching the explored network.</summary>
    public IEnumerable<NodeState> Eligible =>
        Map.Nodes.Select(n => Nodes[n.Id]).Where(n => !n.Explored && n.Def.Links.Any(l => Nodes[l].Explored));

    /// <summary>Deferred Events on the current Map, which can be resumed from anywhere.</summary>
    public IEnumerable<NodeState> Deferred => Map.Nodes.Select(n => Nodes[n.Id]).Where(n => n.Event == EventStatus.Deferred);

    /// <summary>Interactables the party can use now on the current Map (Sanctuaries not yet used, Pathways, stations).</summary>
    public IEnumerable<(NodeState Node, InteractableState Interactable)> Usable =>
        Map.Nodes.Select(n => Nodes[n.Id]).Where(n => n.Settled)
            .SelectMany(n => n.Interactables.Where(i => !i.Used).Select(i => (n, i)));

    /// <summary>Heroes neither dead nor unconscious.</summary>
    public IEnumerable<Hero> Available => Heroes.Where(h => h.Available);

    public Hero Hero(string id) => Heroes.FirstOrDefault(h => h.Id == id) ?? throw new InvalidOperationException($"No hero \"{id}\"");

    /// <summary>Why the party can't explore <paramref name="nodeId"/> now, or null.</summary>
    public string? CantExplore(string nodeId)
    {
        if (State != RunState.Exploring) return "busy";
        if (!Nodes.TryGetValue(nodeId, out var node) || node.Def.Map != Map.Id) return "not_on_this_map";
        if (node.Explored) return "explored";
        return Eligible.Contains(node) ? null : "not_eligible";
    }

    public RunResult Explore(string nodeId)
    {
        if (CantExplore(nodeId) is string why) throw new InvalidOperationException($"Can't explore {nodeId}: {why}");
        var r = new RunResult();
        ExploreNode(Nodes[nodeId], r);
        return Record(r);
    }

    public RunResult Resume(string nodeId)
    {
        if (State != RunState.Exploring) throw new InvalidOperationException("Can't resume an Event now");
        if (!Deferred.Any(n => n.Def.Id == nodeId)) throw new InvalidOperationException($"No deferred Event at {nodeId}");
        var r = new RunResult();
        var node = Nodes[nodeId];
        StartEvent(node, node.ResumeBlock!, resumed: true, r);
        return Record(r);
    }

    /// <summary>Camp (Anchor: Rest, Camp and Sanctuary): uses a charge, Rests, and restores part of Health and Mana.</summary>
    public RunResult Camp()
    {
        if (State != RunState.Exploring) throw new InvalidOperationException("Can't camp now");
        if (CampCharges <= 0) throw new InvalidOperationException("No Camp charges left");
        var r = new RunResult();
        UseCamp(automatic: false, r);
        return Record(r);
    }

    /// <summary>Uses a Sanctuary or a Pathway (an Alchemist Station sells through <see cref="Buy"/>).</summary>
    public RunResult UseInteractable(string nodeId, InteractableKind kind)
    {
        var (node, thing) = UsableAt(nodeId, kind);
        var r = new RunResult();
        switch (kind)
        {
            case InteractableKind.Sanctuary:
                UseSanctuary(node, thing, automatic: false, r);
                break;
            case InteractableKind.Pathway:
                r.Add(new MapCompleted(Map));
                EnterMap(MapIndex + 1, r);
                break;
            default:
                throw new InvalidOperationException($"Use Buy at an {kind}");
        }
        return Record(r);
    }

    /// <summary>Buys one charge of <paramref name="itemId"/> at an Alchemist Station; it goes into the party pack.</summary>
    public RunResult Buy(string nodeId, string itemId)
    {
        UsableAt(nodeId, InteractableKind.AlchemistStation);
        var item = Data.Items[itemId];
        if (item.Price <= 0) throw new InvalidOperationException($"{item.Name} isn't for sale");
        if (Gold < item.Price) throw new InvalidOperationException("Not enough Gold");
        var r = new RunResult();
        Gold -= item.Price;
        r.Add(new ItemBought(item, item.Price));
        r.Add(new GoldChanged(-item.Price, Gold));
        AddToPack(item, 1, r);
        return Record(r);
    }

    (NodeState, InteractableState) UsableAt(string nodeId, InteractableKind kind)
    {
        if (State != RunState.Exploring) throw new InvalidOperationException("Can't use that now");
        foreach (var (node, thing) in Usable)
            if (node.Def.Id == nodeId && thing.Kind == kind) return (node, thing);
        throw new InvalidOperationException($"No usable {kind} at {nodeId}");
    }

    // ---- The Hero Panel (between Events) ----

    /// <summary>Uses a belt item outside combat (a potion): its heal and Mana shares.</summary>
    public RunResult UseItem(string heroId, string itemId)
    {
        if (State != RunState.Exploring) throw new InvalidOperationException("Items are used from the Hero Panel, between Events");
        var hero = Hero(heroId);
        var item = Data.Items[itemId];
        if (!hero.Available) throw new InvalidOperationException($"{hero.Name} can't use items now");
        if (!item.OutsideCombat) throw new InvalidOperationException($"{item.Name} is only used in battle");
        var slot = hero.Belt.FirstOrDefault(s => s.Item == item && s.Charges > 0)
            ?? throw new InvalidOperationException($"{hero.Name} has no {item.Name} in the belt");
        var r = new RunResult();
        Spend(hero, slot);
        r.Add(new ItemUsed(hero, item));
        foreach (var proc in Data.Actions[item.Action].Procs.Select(p => Data.Procs[p]))
        {
            if (proc.HealShare > 0) ChangeResource(hero, EventResource.Health, Share(proc.HealShare, hero.MaxHealth), r);
            if (proc.ManaShare > 0) ChangeResource(hero, EventResource.Mana, Share(proc.ManaShare, hero.MaxMana), r);
        }
        return Record(r);
    }

    /// <summary>Moves charges of an item from the party pack into a hero's belt: topping up a slot that holds it, else
    /// a free slot, up to the item's uses per slot.</summary>
    public RunResult MoveToBelt(string heroId, string itemId)
    {
        if (CantMoveToBelt(heroId, itemId) is string why) throw new InvalidOperationException(why);
        var hero = Hero(heroId);
        var item = Data.Items[itemId];
        var slot = hero.Belt.FirstOrDefault(s => s.Item == item && s.Charges < item.Uses);
        if (slot is null) hero.Belt.Add(slot = new BeltSlot(item, 0));
        var moved = Math.Min(Pack[itemId], item.Uses - slot.Charges);
        slot.Charges += moved;
        TakeFromPack(itemId, moved);
        var r = new RunResult();
        r.Add(new ItemMoved(hero, item, moved, ToBelt: true));
        return Record(r);
    }

    public string? CantMoveToBelt(string heroId, string itemId)
    {
        if (State != RunState.Exploring) return "busy";
        var hero = Hero(heroId);
        if (hero.Dead) return "dead";
        if (Pack.GetValueOrDefault(itemId) <= 0) return "not_in_pack";
        var item = Data.Items[itemId];
        if (hero.Belt.Any(s => s.Item == item && s.Charges < item.Uses)) return null;
        return hero.Belt.Count < hero.BeltSlots ? null : "belt_full";
    }

    /// <summary>Empties a hero's belt slot holding <paramref name="itemId"/> into the party pack.</summary>
    public RunResult MoveToPack(string heroId, string itemId)
    {
        if (State != RunState.Exploring) throw new InvalidOperationException("busy");
        var hero = Hero(heroId);
        var slot = hero.Belt.FirstOrDefault(s => s.Item.Id == itemId) ?? throw new InvalidOperationException($"{hero.Name} has no {itemId}");
        hero.Belt.Remove(slot);
        Pack[itemId] = Pack.GetValueOrDefault(itemId) + slot.Charges;
        var r = new RunResult();
        r.Add(new ItemMoved(hero, slot.Item, slot.Charges, ToBelt: false));
        return Record(r);
    }

    // ---- Rules ----

    void EnterMap(int index, RunResult r)
    {
        MapIndex = index;
        r.Add(new MapEntered(Map));
        ExploreNode(Nodes[Map.Start], r);
    }

    /// <summary>Explores a Node: a step (step-timed buffs count down), then its Event, if any.</summary>
    void ExploreNode(NodeState node, RunResult r)
    {
        Steps++;
        Stats.Steps++;
        foreach (var hero in Heroes.Where(h => !h.Dead))
            CountDown(hero, DurationKind.Steps, r);
        node.Explored = true;
        r.Add(new NodeExplored(node.Def));
        if (node.Event == EventStatus.Pending)
            StartEvent(node, Data.Events[node.Def.Event!].Start, resumed: false, r);
    }

    void CountDown(Hero hero, DurationKind kind, RunResult r)
    {
        foreach (var buff in hero.Buffs.Where(b => b.Def.Duration == kind).ToList())
            if (--buff.Remaining <= 0)
            {
                hero.Buffs.Remove(buff);
                r.Add(new BuffEnded(hero, buff.Def));
            }
    }

    static int Share(double share, int max) => (int)Math.Round(share * max, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Changes one resource, within its limits: Health and Mana from 0 to the maximum, Stamina from −4 (Unconscious) to
    /// the maximum. A hero at 0 Health is Dead at once (Anchor: Expedition resources). The dead change no more.
    /// </summary>
    void ChangeResource(Hero hero, EventResource resource, int amount, RunResult r)
    {
        if (hero.Dead || amount == 0) return;
        var status = hero.Status;
        var before = resource switch { EventResource.Health => hero.Health, EventResource.Mana => hero.Mana, _ => hero.Stamina };
        var after = resource switch
        {
            EventResource.Health => Math.Clamp(before + amount, 0, hero.MaxHealth),
            EventResource.Mana => Math.Clamp(before + amount, 0, hero.MaxMana),
            _ => Math.Clamp(before + amount, Exploration.Hero.MinStamina, hero.MaxStamina),
        };
        if (after == before) return;
        switch (resource)
        {
            case EventResource.Health: hero.Health = after; break;
            case EventResource.Mana: hero.Mana = after; break;
            default: hero.Stamina = after; break;
        }
        r.Add(new ResourceChanged(hero, resource, before, after));
        if (hero.Health == 0) Kill(hero, r);
        if (hero.Status != status) r.Add(new StatusChanged(hero, status, hero.Status));
    }

    void Kill(Hero hero, RunResult r)
    {
        if (hero.Dead) return;
        hero.Dead = true;
        hero.Health = 0;
        r.Add(new HeroDied(hero));
    }

    /// <summary>
    /// Rest (Anchor: Rest, Camp and Sanctuary): every living hero regains its max Stamina, never above max. It ends
    /// Unconscious; a hero still at 0 or below stays Exhausted.
    /// </summary>
    void Rest(RunResult r)
    {
        foreach (var hero in Heroes.Where(h => !h.Dead))
            ChangeResource(hero, EventResource.Stamina, hero.MaxStamina, r);
    }

    void UseCamp(bool automatic, RunResult r)
    {
        CampCharges--;
        Stats.Camps++;
        r.Add(new Rested(Camp: true, automatic));
        r.Add(new CampChargesChanged(-1, CampCharges));
        var share = Data.RunRules.CampRestore;
        foreach (var hero in Heroes.Where(h => !h.Dead))
        {
            ChangeResource(hero, EventResource.Health, Share(share, hero.MaxHealth), r);
            ChangeResource(hero, EventResource.Mana, Share(share, hero.MaxMana), r);
        }
        Rest(r);
    }

    void UseSanctuary(NodeState node, InteractableState sanctuary, bool automatic, RunResult r)
    {
        sanctuary.Used = true;
        Stats.Sanctuaries++;
        r.Add(new SanctuaryUsed(node.Def));
        r.Add(new Rested(Camp: false, automatic));
        foreach (var hero in Heroes.Where(h => !h.Dead))
        {
            ChangeResource(hero, EventResource.Health, hero.MaxHealth, r);
            ChangeResource(hero, EventResource.Mana, hero.MaxMana, r);
        }
        Rest(r);
    }

    /// <summary>
    /// After an Event or fight ends: if every hero is dead, or every living one unconscious and the party can't Rest
    /// (no Camp charge, no unused Sanctuary), it's a wipe; if it can, it Rests automatically (Anchor: Dead and unconscious
    /// heroes).
    /// </summary>
    void CheckParty(RunResult r)
    {
        if (Over || Available.Any()) return;
        if (Heroes.Any(h => !h.Dead))
        {
            if (CampCharges > 0)
            {
                UseCamp(automatic: true, r);
                return;
            }
            if (Usable.FirstOrDefault(u => u.Interactable.Kind == InteractableKind.Sanctuary) is { Node: { } node } found)
            {
                UseSanctuary(node, found.Interactable, automatic: true, r);
                return;
            }
        }
        foreach (var hero in Heroes) Kill(hero, r);
        State = RunState.Wiped;
        r.Add(new PartyWiped());
    }

    void AddToPack(ItemDef item, int amount, RunResult r)
    {
        Pack[item.Id] = Pack.GetValueOrDefault(item.Id) + amount;
        r.Add(new ItemGained(item, amount));
    }

    void TakeFromPack(string itemId, int amount)
    {
        Pack[itemId] -= amount;
        if (Pack[itemId] <= 0) Pack.Remove(itemId);
    }

    void Spend(Hero hero, BeltSlot slot)
    {
        slot.Charges--;
        Stats.ItemsUsed++;
        if (slot.Charges <= 0) hero.Belt.Remove(slot);
    }

    RunResult Record(RunResult r)
    {
        Results.Add(r);
        return r;
    }
}

/// <summary>Totals for the end screen and the dungeon simulator.</summary>
public sealed class RunStats
{
    public int Steps { get; internal set; }
    public int Battles { get; internal set; }
    public int Victories { get; internal set; }
    public int Flees { get; internal set; }
    public int Camps { get; internal set; }
    public int Sanctuaries { get; internal set; }
    /// <summary>Belt item charges used, in and out of battle.</summary>
    public int ItemsUsed { get; internal set; }
}
