using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Combat;

/// <summary>
/// One hero decision, as a replay stores it: the unit, the action, and its target unit or tile (an empty action
/// means the hero waited). A Confused unit's enemy target is left out: the battle picks it at random (from its seeded
/// RNG) after the choice. <see cref="Auto"/>: the hero's AI decided (auto-battle); a replay asks the AI again,
/// since the AI's own tie-break rolls are part of the battle's random sequence.
/// </summary>
public sealed record Choice(string Unit, string Action, string? Target = null, Tile? Tile = null, bool Auto = false);

/// <summary>
/// A battle played turn by turn, for the game screen (and replays). <see cref="Advance"/> runs clock events until a
/// hero needs a decision or the battle ends; <see cref="Choose"/> applies the player's decision. Enemies, and heroes
/// while <see cref="AutoBattle"/> is on, act on their AI. Every hero decision is recorded in <see cref="Choices"/>,
/// so the seed plus that list replays the battle exactly (<see cref="Replay"/>).
/// <para>The session never touches the battle's RNG outside the rules themselves: previews (<see cref="Preview"/>)
/// are pure, so looking at them can't change what happens.</para>
/// </summary>
public sealed class BattleSession
{
    public Battle Battle { get; }
    public EncounterDef Encounter { get; }
    public ulong Seed { get; }

    /// <summary>Heroes act on the simulator's scripted AI.</summary>
    public bool AutoBattle { get; set; }

    /// <summary>The hero whose turn it is and who needs a decision, or null.</summary>
    public Unit? Awaiting { get; private set; }

    readonly List<Choice> choices = [];
    public IReadOnlyList<Choice> Choices => choices;

    /// <summary>Decisions to play instead of asking (a replay being played back).</summary>
    readonly Queue<Choice> scripted = new();

    public double TimeLimit { get; init; } = BattleRunner.DefaultTimeLimit;

    public BattleSession(GameData data, EncounterDef encounter, ulong seed, IEnumerable<Choice>? replay = null)
    {
        Encounter = encounter;
        Seed = seed;
        Battle = EncounterSetup.Build(data, encounter, seed);
        foreach (var c in replay ?? []) scripted.Enqueue(c);
    }

    public bool Over => Battle.Winner is not null || Battle.Clock.Time > TimeLimit;

    bool Started;

    /// <summary>
    /// Runs the battle until a hero needs a decision or the battle ends. Returns every result produced, in order,
    /// for the screen to animate.
    /// </summary>
    public List<ActionResult> Advance()
    {
        var results = new List<ActionResult>();
        while (!Over && Awaiting is null)
            results.AddRange(Step());
        return results;
    }

    /// <summary>
    /// Plays one clock event (a buff tick, a cast completing, or a turn) and returns its results, so the screen can
    /// show each event with the state it left. The first step also starts the battle (fight-start procs). Stops short
    /// (returns nothing more) once a hero is waiting or the battle is over.
    /// </summary>
    public List<ActionResult> Step()
    {
        if (Awaiting is not null) throw new InvalidOperationException($"Waiting for {Awaiting.Name}'s decision");
        var first = Battle.Results.Count;
        if (!Started)
        {
            Started = true;
            Battle.Start();
            return Battle.Results.Skip(first).ToList();
        }
        if (Over) return [];
        switch (Battle.Clock.Next())
        {
            case BuffTick:
                Battle.BuffTick();
                break;
            case CastComplete done:
                Battle.CompleteCast(done);
                break;
            case TurnReady turn:
                TurnStarts(turn.Unit);
                break;
        }
        return Battle.Results.Skip(first).ToList();
    }

    void TurnStarts(Unit unit)
    {
        Battle.StartTurn(unit);
        if (!unit.Alive || Battle.Winner is not null) return;
        if (unit.LosesTurn)
        {
            Battle.SkipTurn(unit);
            return;
        }
        var auto = AutoBattle;
        if (unit.Side == Side.Party && scripted.TryDequeue(out var next))
        {
            if (next.Unit != unit.Id)
                throw new InvalidOperationException($"Replay out of step: expected {next.Unit}'s turn, got {unit.Id}'s");
            if (!next.Auto)
            {
                Apply(unit, next);
                return;
            }
            auto = true;
        }
        if (unit.Side == Side.Party && !auto)
        {
            Awaiting = unit;
            return;
        }
        var decision = UnitAi.Decide(Battle, unit);
        if (unit.Side == Side.Party)
            choices.Add(decision is null
                ? new Choice(unit.Id, "", Auto: true)
                : new Choice(unit.Id, decision.Action.Id, decision.Target?.Id, decision.Tile, Auto: true));
        Play(unit, decision);
    }

    void Play(Unit unit, Decision? decision)
    {
        switch (decision)
        {
            case null:
                Battle.Wait(unit);
                break;
            case { Tile: { } tile } d:
                Battle.ActAt(unit, d.Action, tile);
                break;
            case var d:
                Battle.Act(unit, d.Action, d.Target);
                break;
        }
    }

    /// <summary>Why the awaiting hero can't make <paramref name="choice"/>, or null if it can.</summary>
    public string? CantChoose(Choice choice)
    {
        if (Awaiting is not { } unit) return "no hero is waiting for a decision";
        if (choice.Unit != unit.Id) return $"it's {unit.Name}'s turn";
        if (choice.Action.Length == 0) return null;
        if (!Battle.Data.Actions.TryGetValue(choice.Action, out var action)) return $"unknown action {choice.Action}";
        if (!Battle.Data.ActionsOf(unit.Def).Contains(action.Id)) return $"{unit.Name} doesn't have {action.Name}";
        if (unit.CantUse(action) is string why) return why;
        if (action.Target == ActionTarget.Tile)
            return choice.Tile is { } t && Options.TilesFor(Battle, unit, action).Contains(t) ? null : "can't move there";
        if (action.Target == ActionTarget.Self) return null;
        if (Confused(unit, action)) return Options.TargetsFor(Battle, unit, action).Count > 0 ? null : "no_valid_target";
        if (choice.Target is null) return "needs a target";
        var target = Battle.Units.FirstOrDefault(u => u.Id == choice.Target);
        return target is null ? "unknown target" : Battle.Grid.CantTarget(unit, action, target);
    }

    /// <summary>The awaiting hero makes <paramref name="choice"/>. Returns the results it produced; call
    /// <see cref="Advance"/> next.</summary>
    public List<ActionResult> Choose(Choice choice)
    {
        if (CantChoose(choice) is string why) throw new InvalidOperationException(why);
        var unit = Awaiting!;
        Awaiting = null;
        var first = Battle.Results.Count;
        Apply(unit, choice);
        return Battle.Results.Skip(first).ToList();
    }

    /// <summary>The waiting hero's turn goes to its AI (auto-battle switched on mid-turn). Recorded as an AI decision,
    /// exactly as if auto-battle had been on when the turn started.</summary>
    public List<ActionResult> ChooseByAi()
    {
        var unit = Awaiting ?? throw new InvalidOperationException("no hero is waiting for a decision");
        Awaiting = null;
        var first = Battle.Results.Count;
        var decision = UnitAi.Decide(Battle, unit);
        choices.Add(decision is null
            ? new Choice(unit.Id, "", Auto: true)
            : new Choice(unit.Id, decision.Action.Id, decision.Target?.Id, decision.Tile, Auto: true));
        Play(unit, decision);
        return Battle.Results.Skip(first).ToList();
    }

    /// <summary>A Confused unit's enemy-targeted action gets a random valid target (Anchor: placeholder).</summary>
    public static bool Confused(Unit unit, ActionDef action) =>
        unit.Has(CcKind.Confusion) && action.Target == ActionTarget.Enemy;

    void Apply(Unit unit, Choice choice)
    {
        if (choice.Action.Length == 0)
        {
            choices.Add(choice);
            Battle.Wait(unit);
            return;
        }
        var action = Battle.Data.Actions[choice.Action];
        Unit? target = null;
        if (Confused(unit, action))
        {
            var options = Options.TargetsFor(Battle, unit, action);
            target = options[Battle.Rng.NextInt(options.Count)];
            choice = choice with { Target = null };
        }
        else if (choice.Target is string id)
            target = Battle.Unit(id);
        choices.Add(choice);
        Play(unit, new Decision(action, target, choice.Tile));
    }

    /// <summary>The seed and choices so far, as a replay.</summary>
    public Replay ToReplay() => new(Encounter.Id, Seed, [.. choices]);
}

/// <summary>What a unit can do right now: valid targets and tiles for each action.</summary>
public static class Options
{
    public static List<Unit> TargetsFor(Battle battle, Unit unit, ActionDef action) =>
        action.Target switch
        {
            ActionTarget.Self => [unit],
            ActionTarget.Tile => [],
            _ => UnitAi.ValidTargets(battle, unit, action),
        };

    /// <summary>Tiles a tile action can go to: Move's neighbours (only away from the front when Feared), plus the
    /// other side's empty tiles for the Rogue's Move.</summary>
    public static List<Tile> TilesFor(Battle battle, Unit unit, ActionDef action)
    {
        if (action.Target != ActionTarget.Tile) return [];
        if (unit.Afraid) return [.. battle.Grid.RetreatOptions(unit)];
        return [.. battle.Grid.TileOptions(unit, action)];
    }

    /// <summary>Why <paramref name="unit"/> can't use <paramref name="action"/> now (no Mana, Silenced, Rooted,
    /// Feared, or nothing to aim at), or null.</summary>
    public static string? Unusable(Battle battle, Unit unit, ActionDef action)
    {
        if (unit.CantUse(action) is string why) return why;
        return action.Target switch
        {
            ActionTarget.Tile when TilesFor(battle, unit, action).Count == 0 => "nowhere_to_move",
            ActionTarget.Enemy or ActionTarget.Ally when TargetsFor(battle, unit, action).Count == 0 => "no_valid_target",
            _ => null,
        };
    }
}
