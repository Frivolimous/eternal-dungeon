namespace EternalDungeon.Core.Combat;

/// <summary>
/// Runs a battle with every unit on its AI: takes clock events in order and plays each turn, until one side
/// has no living units or the time limit passes (then neither side has won).
/// </summary>
public static class BattleRunner
{
    /// <summary>Base-speed turns before a battle is called off as a stalemate (placeholder).</summary>
    public const double DefaultTimeLimit = 300;

    public static Side? Run(Battle battle, double timeLimit = DefaultTimeLimit)
    {
        battle.Start();
        while (battle.Winner is null && battle.Clock.Time <= timeLimit)
            Step(battle);
        return battle.Winner;
    }

    public static void Step(Battle battle)
    {
        switch (battle.Clock.Next())
        {
            case BuffTick:
                battle.BuffTick();
                break;
            case CastComplete done:
                battle.CompleteCast(done);
                break;
            case TurnReady turn:
                TakeTurn(battle, turn.Unit);
                break;
        }
    }

    static void TakeTurn(Battle battle, Unit unit)
    {
        battle.StartTurn(unit);
        if (!unit.Alive || battle.Winner is not null) return;
        if (unit.LosesTurn)
        {
            battle.SkipTurn(unit);
            return;
        }
        switch (UnitAi.Decide(battle, unit))
        {
            case null:
                battle.Wait(unit);
                break;
            case { Tile: { } tile } d:
                battle.ActAt(unit, d.Action, tile);
                break;
            case var d:
                battle.Act(unit, d.Action, d.Target);
                break;
        }
    }
}
