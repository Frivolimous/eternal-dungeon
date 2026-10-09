using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>What a link leads to, as far as the player is told: a fight (and its scale), a deferral, the Event's end, or
/// more story.</summary>
public sealed record Outlook(CombatBlock? Fight, bool Defers, bool Ends);

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

    /// <summary>Follows <paramref name="id"/> through the blocks that run on their own (branches as things stand now)
    /// to the first fight, deferral, story block or end.</summary>
    Outlook Look(string? id)
    {
        var def = Event!.Def;
        var seen = new HashSet<string>();
        while (id is not null && seen.Add(id))
        {
            switch (def.Block(id))
            {
                case StoryBlock { Choices.Count: 0 } s:
                    id = s.Success;                         // text on the way: look past it
                    break;
                case StoryBlock:
                    return new Outlook(null, false, false);
                case CombatBlock c:
                    return new Outlook(c, false, false);
                case BranchBlock b:
                    id = b.Options.FirstOrDefault(o => Holds(o.Conditions))?.Success;
                    break;
                case ActionBlock a when a.Actions.Any(x => x is DeferAction):
                    return new Outlook(null, true, false);
                case ActionBlock a:
                    id = a.Success;
                    break;
                case ResourceBlock x:
                    id = x.Success;
                    break;
                case RewardBlock w:
                    id = w.Success;
                    break;
            }
        }
        return new Outlook(null, false, true);
    }
}
