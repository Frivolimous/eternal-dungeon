using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>One thing that happened during a run, in order (like a battle's <see cref="Outcome"/>).</summary>
public abstract record RunOutcome;

public sealed record MapEntered(MapDef Map) : RunOutcome;

public sealed record NodeExplored(NodeDef Node) : RunOutcome;

public sealed record EventStarted(NodeDef Node, EventDef Event, Hero Active, bool Resumed) : RunOutcome;

/// <summary>A story block is shown: its text and the choices the party can take (empty: Continue).</summary>
public sealed record StoryShown(EventDef Event, StoryBlock Block, Hero Active, IReadOnlyList<EventChoice> Choices) : RunOutcome;

/// <summary>The player took a choice; <see cref="Hero"/> made it (and is now the Active Hero).</summary>
public sealed record ChoiceMade(EventDef Event, StoryBlock Block, EventChoice Choice, Hero Hero) : RunOutcome;

/// <summary>A roll: the hero's best trait level among the roll's traits, its Exhaustion penalty, the chance (0–1) and
/// the result.</summary>
public sealed record EventRolled(Hero Hero, IReadOnlyList<string> Traits, int Level, double Penalty, Roll Roll) : RunOutcome;

public sealed record ActiveHeroChanged(Hero Hero) : RunOutcome;

public sealed record ResourceChanged(Hero Hero, EventResource Resource, int Before, int After) : RunOutcome;

public sealed record StatusChanged(Hero Hero, HeroStatus Before, HeroStatus After) : RunOutcome;

public sealed record FlagSet(string Key, bool Value, bool EventOnly) : RunOutcome;

public sealed record NodeRevealed(NodeDef Node, RevealIcon Icon) : RunOutcome;

public sealed record BuffGained(Hero Hero, BuffDef Buff) : RunOutcome;

public sealed record BuffEnded(Hero Hero, BuffDef Buff) : RunOutcome;

public sealed record GoldChanged(int Amount, int Total) : RunOutcome;

public sealed record ItemGained(ItemDef Item, int Amount) : RunOutcome;

public sealed record CampChargesChanged(int Amount, int Total) : RunOutcome;

public sealed record InteractableSpawned(NodeDef Node, InteractableKind Kind) : RunOutcome;

public sealed record EventDeferred(NodeDef Node, EventDef Event) : RunOutcome;

public sealed record EventClosed(NodeDef Node, EventDef Event) : RunOutcome;

public sealed record BattleStarted(EncounterDef Encounter, CombatScale Scale, InitiativeModifier Initiative, ulong Seed) : RunOutcome;

public enum BattleOutcome { Victory, Fled, Defeat }

/// <summary>A battle ended: how, and the Stamina it cost each hero who fought (paid after the fight).</summary>
public sealed record BattleEnded(EncounterDef Encounter, BattleOutcome Outcome, int StaminaCost, IReadOnlyList<Hero> Fought) : RunOutcome;

public sealed record HeroDied(Hero Hero) : RunOutcome;

/// <summary>The party Rested: at a Camp (<see cref="Camp"/>), at a Sanctuary, and whether it happened automatically
/// (every hero was unconscious).</summary>
public sealed record Rested(bool Camp, bool Automatic) : RunOutcome;

public sealed record SanctuaryUsed(NodeDef Node) : RunOutcome;

public sealed record ItemUsed(Hero Hero, ItemDef Item) : RunOutcome;

public sealed record ItemMoved(Hero Hero, ItemDef Item, int Amount, bool ToBelt) : RunOutcome;

public sealed record ItemBought(ItemDef Item, int Price) : RunOutcome;

public sealed record MapCompleted(MapDef Map) : RunOutcome;

public sealed record DungeonCompleted(DungeonDef Dungeon) : RunOutcome;

public sealed record PartyWiped : RunOutcome;

public sealed record XpGained(Hero Hero, int Amount, int Total) : RunOutcome;

/// <summary>A hero reached a new level (and a skill point with it).</summary>
public sealed record LevelUp(Hero Hero, int Level) : RunOutcome;

public sealed record SkillRaised(Hero Hero, SkillDef Skill, int Level) : RunOutcome;

/// <summary>A hero's tree points unlocked a mastery.</summary>
public sealed record MasteryUnlocked(Hero Hero, SkillDef Mastery) : RunOutcome;

/// <summary>Everything one run action did, in order.</summary>
public sealed class RunResult
{
    public List<RunOutcome> Outcomes { get; } = [];

    public void Add(RunOutcome outcome) => Outcomes.Add(outcome);

    public IEnumerable<T> Of<T>() where T : RunOutcome => Outcomes.OfType<T>();
}
