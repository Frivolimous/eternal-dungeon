using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>Something a choice's path does before it stops (a fight, more choices, a deferral or the end): a resource
/// change, an action (a buff or curse, Gold, a Camp charge, a spawned Interactable, a map reveal) or a reward.</summary>
public abstract record PreviewEffect;

public sealed record ResourceEffect(ResourceBlock Block) : PreviewEffect;

public sealed record ActionEffect(EventAction Action) : PreviewEffect;

public sealed record RewardEffect(EventReward Reward) : PreviewEffect;

/// <summary>What a link leads to, as far as the player is told: what happens on the way (<see cref="Effects"/>), then
/// a fight (its scale, and how many enemies), a deferral, the Event's end, or more choices.</summary>
public sealed record Outlook(CombatBlock? Fight, bool Defers, bool Ends, IReadOnlyList<PreviewEffect> Effects);

/// <summary>
/// A choice's decision preview (Anchor: Event blocks › Story): who would make it, its chance (for a roll), and what
/// success and failure lead to. Pure, like battle previews: it never rolls.
/// </summary>
public sealed record ChoicePreview(EventChoice Choice, IReadOnlyList<Hero> Heroes, double? Chance, Outlook Success, Outlook? Failure);

public sealed partial class DungeonRun
{
    /// <summary>Previews of the choices shown now.</summary>
    public IReadOnlyList<ChoicePreview> Previews => [.. Choices.Select(Preview)];

    public ChoicePreview Preview(EventChoice choice)
    {
        var heroes = BestHeroes(choice);
        double? chance = choice.Roll is { } roll && heroes.Count > 0 ? Chance(roll, heroes[0]) : null;
        return new ChoicePreview(choice, heroes, chance, Look(choice.Success), choice.Roll is null ? null : Look(choice.Failure));
    }

    /// <summary>
    /// Follows <paramref name="id"/> through the blocks that run on their own (text, branches as things stand now,
    /// resource changes, actions, rewards), collecting what they do, to the first fight, deferral, choice or end. Loot
    /// after a fight isn't shown: the preview stops at the fight.
    /// </summary>
    Outlook Look(string? id)
    {
        var def = Event!.Def;
        var effects = new List<PreviewEffect>();
        var seen = new HashSet<string>();
        while (id is not null && seen.Add(id))
        {
            switch (def.Block(id))
            {
                case StoryBlock { Choices.Count: 0 } s:
                    id = s.Success;                         // text on the way: look past it
                    break;
                case StoryBlock:
                    return new Outlook(null, false, false, effects);
                case CombatBlock c:
                    return new Outlook(c, false, false, effects);
                case BranchBlock b:
                    id = b.Options.FirstOrDefault(o => Holds(o.Conditions))?.Success;
                    break;
                case ActionBlock a:
                    effects.AddRange(a.Actions.Where(Shown).Select(x => new ActionEffect(x)));
                    if (a.Actions.Any(x => x is DeferAction)) return new Outlook(null, true, false, effects);
                    id = a.Success;
                    break;
                case ResourceBlock x:
                    effects.Add(new ResourceEffect(x));
                    id = x.Success;
                    break;
                case RewardBlock w:
                    effects.AddRange(w.Rewards.Select(x => new RewardEffect(x)));
                    id = w.Success;
                    break;
            }
        }
        return new Outlook(null, false, true, effects);
    }

    /// <summary>Flags are the story's bookkeeping, and the deferral and the dungeon's end show in their own way.</summary>
    static bool Shown(EventAction a) => a is not (SetFlagAction or DeferAction or CompleteDungeonAction);
}
