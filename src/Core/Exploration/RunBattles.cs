using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>A run's fight in progress: the Combat block that started it, the session to play, and the heroes in it.</summary>
public sealed class RunBattle(CombatBlock block, BattleSession session, IReadOnlyList<Hero> heroes)
{
    public CombatBlock Block { get; } = block;
    public BattleSession Session { get; } = session;
    public IReadOnlyList<Hero> Heroes { get; } = heroes;

    public Unit UnitOf(Hero hero) => Session.Battle.Unit(hero.Id);
}

// Fights in a run (Anchor: Exploration › Combat in exploration; M3A brief §5).
public sealed partial class DungeonRun
{
    /// <summary>The fight in progress (while <see cref="State"/> is Battle), or null.</summary>
    public RunBattle? Battle { get; private set; }

    /// <summary>
    /// Starts a Combat block's fight from the run's state: every hero neither dead nor unconscious, with its current
    /// Health and Mana, its belt, its Exhaustion (from its Stamina now; the fight's cost is paid after), the
    /// initiative modifier, and its run-long buffs and curses for the whole fight. The seed comes from the run's RNG.
    /// </summary>
    void StartBattle(CombatBlock block, RunResult r)
    {
        var encounter = Data.Encounters[block.Encounter];
        var seed = rng.NextULong();
        var grid = new BattleGrid(DataLoader.AreaCols, DataLoader.AreaRows);
        var heroes = Available.ToList();
        var initiative = block.Initiative switch
        {
            InitiativeModifier.Surprised => -Data.RunRules.InitiativeModifier,
            InitiativeModifier.FirstStrike => Data.RunRules.InitiativeModifier,
            _ => 0,
        };
        var units = new List<Unit>();
        foreach (var hero in heroes)
        {
            var unit = new Unit(hero.Id, hero.Unit, Side.Party, Data) { Name = hero.Name };
            unit.SetVitals(hero.Health, hero.Mana);
            foreach (var slot in hero.Belt) unit.Belt.Add(new BeltSlot(slot.Item, slot.Charges));
            if (hero.SpeedPenalty != 0) unit.Stats.Add("exhaustion", "speed", hero.SpeedPenalty);
            if (initiative != 0)
            {
                unit.Stats.Add("initiative_modifier", "initiative", initiative);
                unit.ActTicks = (long)unit.Stats.Get("initiative") * TurnClock.TicksPerTurn;
            }
            grid.Place(unit, grid.TileAt(grid.HomeOf(Side.Party).Id, hero.Def.Row, hero.Def.Col));
            units.Add(unit);
        }
        units.AddRange(EncounterSetup.Place(Data, Side.Enemy, encounter.Enemies, grid));
        var battle = new Combat.Battle(Data, units, seed, grid);
        foreach (var hero in heroes)
            foreach (var buff in hero.Buffs.Where(b => b.InBattle(Data)))
                battle.JoinRunBuff(battle.Unit(hero.Id), buff.Def);

        Battle = new RunBattle(block, new BattleSession(battle, encounter, seed), heroes);
        State = RunState.Battle;
        Stats.Battles++;
        r.Add(new BattleStarted(encounter, block.Scale, block.Initiative, seed));
    }

    /// <summary>
    /// Ends the fight once its session is over and carries the result back to the run: Health, Mana and belt charges;
    /// heroes who fell are Dead; every hero who fought and lives pays the scale's Stamina (fled ones too);
    /// battle-timed buffs count down. Then the Event goes on: victory follows success; a flee follows the flee link,
    /// or closes the Event (a Boss defers it to this fight instead); a defeat ends it.
    /// </summary>
    public RunResult FinishBattle()
    {
        if (State != RunState.Battle || Battle is not { } fight) throw new InvalidOperationException("No battle in progress");
        if (!fight.Session.Over) throw new InvalidOperationException("The battle isn't over");
        var r = new RunResult();
        var battle = fight.Session.Battle;
        var outcome = battle.Winner == Side.Party ? BattleOutcome.Victory
            : fight.Heroes.Any(h => battle.Unit(h.Id).Fled) || battle.Winner is null ? BattleOutcome.Fled   // a stalemate counts as a flee
            : BattleOutcome.Defeat;
        var cost = fight.Block.Scale == CombatScale.Skirmish ? 0 : 1;

        foreach (var hero in fight.Heroes)
        {
            var unit = battle.Unit(hero.Id);
            hero.Health = unit.Health;
            hero.Mana = unit.Mana;
            Stats.ItemsUsed += hero.Belt.Sum(s => s.Charges) - unit.Belt.Sum(s => s.Charges);
            hero.Belt.Clear();
            hero.Belt.AddRange(unit.Belt.Where(s => s.Charges > 0));
            if (unit.Health == 0) Kill(hero, r);
        }
        r.Add(new BattleEnded(fight.Session.Encounter, outcome, cost, fight.Heroes));
        foreach (var hero in fight.Heroes)
            ChangeResource(hero, EventResource.Stamina, -cost, r);
        foreach (var hero in Heroes.Where(h => !h.Dead))
            CountDown(hero, DurationKind.Battles, r);
        if (outcome == BattleOutcome.Victory) Stats.Victories++;
        if (outcome == BattleOutcome.Fled) Stats.Flees++;

        Battle = null;
        State = RunState.Event;
        switch (outcome)
        {
            case BattleOutcome.Victory:
                Run(fight.Block.Success, r);
                break;
            case BattleOutcome.Fled when fight.Block.Flee is { } link:
                Run(link, r);
                break;
            case BattleOutcome.Fled when fight.Block.Scale == CombatScale.Boss:
                Defer(fight.Block.Id, r);
                break;
            default:
                Close(r);
                break;
        }
        return Record(r);
    }
}
