namespace EternalDungeon.Core.Data;

// Events (Anchor: Exploration › Events, Event blocks). Each Event is one JSON file in data/events/ (nested blocks
// don't fit flat tables). Its text is in strings.csv by key: the name at event.<id>.name, a story block's text at
// event.<id>.<block>, and a choice's text at event.<id>.<block>.<choice>.

/// <summary>
/// An Event: its base type (shown by fog of war if needed), context tags, the block it starts at, and its blocks.
/// Blocks link to other blocks of the same Event by id; a missing link ends (closes) the Event.
/// </summary>
public sealed record EventDef(string Id, string Type, IReadOnlyList<string> Tags, string Start, IReadOnlyList<EventBlock> Blocks)
{
    public EventBlock Block(string id) => Blocks.First(b => b.Id == id);

    public static string NameKey(string eventId) => $"event.{eventId}.name";
    public static string TextKey(string eventId, string blockId) => $"event.{eventId}.{blockId}";
    public static string ChoiceKey(string eventId, string blockId, string choiceId) => $"event.{eventId}.{blockId}.{choiceId}";
}

public abstract record EventBlock(string Id)
{
    /// <summary>Every block this one can lead to.</summary>
    public abstract IEnumerable<string> Links { get; }
}

/// <summary>Text the player reads, then either choices (every choice whose conditions hold is shown) or, with none,
/// a Continue to <see cref="Success"/> (no link: the Event closes).</summary>
public sealed record StoryBlock(string Id, string? Image, IReadOnlyList<EventChoice> Choices, string? Success) : EventBlock(Id)
{
    public override IEnumerable<string> Links =>
        Choices.SelectMany(c => new[] { c.Success, c.Failure }).Append(Success).OfType<string>();
}

/// <summary>
/// A choice in a story block. It's shown when its conditions hold. With a roll, success and failure lead on (no
/// failure link: a failed roll closes the Event); without, <see cref="Success"/> does (none: the choice closes it).
/// </summary>
public sealed record EventChoice(string Id, IReadOnlyList<EventCondition> Conditions, EventRoll? Roll, string? Success, string? Failure)
{
    /// <summary>The traits that pick the hero who makes this choice: the roll's, else the first trait condition's.</summary>
    public IReadOnlyList<string> HeroTraits =>
        Roll is { Traits.Count: > 0 } r ? r.Traits : Conditions.OfType<TraitCondition>().FirstOrDefault()?.Traits ?? [];

    public string? HeroClass => Conditions.OfType<ClassCondition>().FirstOrDefault()?.Class;
}

/// <summary>
/// A roll (Anchor: Exploration › Rolls): <see cref="Base"/> plus <see cref="PerPoint"/> for each level the rolling
/// hero has in the best of <see cref="Traits"/>, plus that hero's Exhaustion penalty, kept between 0% and 100%.
/// </summary>
public sealed record EventRoll(double Base, IReadOnlyList<string> Traits, double PerPoint);

/// <summary>Routing the player doesn't see: the first option whose conditions hold leads on; none, and the Event
/// closes.</summary>
public sealed record BranchBlock(string Id, IReadOnlyList<BranchOption> Options) : EventBlock(Id)
{
    public override IEnumerable<string> Links => Options.Select(o => o.Success).OfType<string>();
}

public sealed record BranchOption(IReadOnlyList<EventCondition> Conditions, string? Success);

/// <summary>An encounter's scale decides its Stamina cost (Anchor: Exploration › Combat in exploration).</summary>
public enum CombatScale { Skirmish, Major, Boss }

/// <summary>Who acts first (Anchor: Dungeons › Battles): Surprised lowers the party's Initiative, First Strike raises
/// it.</summary>
public enum InitiativeModifier { None, Surprised, FirstStrike }

/// <summary>
/// A fight against a fixed encounter (its enemies; the party comes from the run). Victory leads to
/// <see cref="Success"/>. Fleeing leads to <see cref="Flee"/> if set; otherwise it closes the Event, or for a Boss
/// defers it to this block.
/// </summary>
public sealed record CombatBlock(string Id, string Encounter, CombatScale Scale, InitiativeModifier Initiative, string? Success, string? Flee)
    : EventBlock(Id)
{
    public override IEnumerable<string> Links => new[] { Success, Flee }.OfType<string>();
}

public enum EventResource { Health, Mana, Stamina }

/// <summary>Who a resource change or buff lands on: the Active Hero, a random eligible hero, or every eligible hero.</summary>
public enum EventTarget { Active, Random, All }

/// <summary>Changes Health, Mana or Stamina by <see cref="Amount"/>, or by <see cref="Share"/> of the maximum (Health and
/// Mana only).</summary>
public sealed record ResourceBlock(string Id, EventResource Resource, int Amount, double Share, EventTarget Target, string? Success)
    : EventBlock(Id)
{
    public override IEnumerable<string> Links => new[] { Success }.OfType<string>();
}

/// <summary>Things that happen, in order. A <see cref="DeferAction"/> comes last and ends the Event there.</summary>
public sealed record ActionBlock(string Id, IReadOnlyList<EventAction> Actions, string? Success) : EventBlock(Id)
{
    public override IEnumerable<string> Links =>
        Actions.OfType<DeferAction>().Select(d => d.Resume).Append(Success).OfType<string>();
}

/// <summary>Loot for the party: Gold, item charges (into the party pack) and Camp charges.</summary>
public sealed record RewardBlock(string Id, IReadOnlyList<EventReward> Rewards, string? Success) : EventBlock(Id)
{
    public override IEnumerable<string> Links => new[] { Success }.OfType<string>();
}

public enum RewardKind { Gold, Item, Camp }

public sealed record EventReward(RewardKind Kind, int Amount, string? Item = null);

public abstract record EventAction;

/// <summary>Sets a Dungeon flag (or, <see cref="EventOnly"/>, a flag in this Event's own state).</summary>
public sealed record SetFlagAction(string Key, bool Value, bool EventOnly) : EventAction;

/// <summary>A map reveal: these Nodes show their position and Feature even before they're eligible, with an icon.</summary>
public sealed record RevealAction(IReadOnlyList<string> Nodes, RevealIcon Icon) : EventAction;

/// <summary>A buff or curse lasting steps or battles.</summary>
public sealed record BuffAction(string Buff, EventTarget Target) : EventAction;

/// <summary>Gold gained or lost (it never goes below 0).</summary>
public sealed record GoldAction(int Amount) : EventAction;

public sealed record CampAction(int Amount) : EventAction;

/// <summary>Spawns an Interactable at this Event's Node.</summary>
public sealed record SpawnAction(InteractableKind Kind) : EventAction;

/// <summary>Defers the Event: it stays on its Node and resumes at <see cref="Resume"/>.</summary>
public sealed record DeferAction(string Resume) : EventAction;

/// <summary>The dungeon is complete (its last Map's boss is beaten).</summary>
public sealed record CompleteDungeonAction : EventAction;

/// <summary>What a map reveal marks a Node with (Anchor: Exploration › Fog of war).</summary>
public enum RevealIcon { None, Enemies, Boss, Sanctuary }

public abstract record EventCondition;

/// <summary>Some eligible hero has at least <see cref="Min"/> in one of <see cref="Traits"/>.</summary>
public sealed record TraitCondition(IReadOnlyList<string> Traits, int Min) : EventCondition;

/// <summary>Some eligible hero has this class.</summary>
public sealed record ClassCondition(string Class) : EventCondition;

/// <summary>A Dungeon flag (or a flag of this Event, <see cref="EventOnly"/>) has <see cref="Value"/>; unset flags are
/// false.</summary>
public sealed record FlagCondition(string Key, bool Value, bool EventOnly) : EventCondition;

/// <summary>The party has at least <see cref="Min"/> Gold.</summary>
public sealed record GoldCondition(int Min) : EventCondition;

/// <summary>Some eligible hero (or, <see cref="All"/>, every one) has at least <see cref="Min"/> of a resource.</summary>
public sealed record ResourceCondition(EventResource Resource, int Min, bool All) : EventCondition;
