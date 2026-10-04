using EternalDungeon.Core.Battle;

namespace EternalDungeon.Core.Tests;

// M1 system 7: turn order. Times are in base-speed turns (T); 1 turn = 100 sub-ticks.
public class TurnClockTests
{
    /// <summary>A warrior with the given Speed and starting Act.</summary>
    static Unit U(string id, int speed, double act = 0)
    {
        var u = new Unit(id, TestData.Repo.Units["warrior"], Side.Party, TestData.Repo);
        u.Stats.Add("test", "speed", speed - 100);
        u.ActTicks = (long)(act * 100);
        return u;
    }

    /// <summary>Runs the clock until <paramref name="untilTurn"/>, each unit spending <paramref name="ap"/> per turn;
    /// returns who acted, in order, with the time.</summary>
    static List<(string Id, double T)> Run(TurnClock clock, double untilTurn, int ap = 100)
    {
        var turns = new List<(string, double)>();
        while (true)
        {
            var e = clock.Next();
            if (e.Time > untilTurn) return turns;
            if (e is TurnReady r)
            {
                turns.Add((r.Unit.Id, r.Time));
                TurnClock.Spend(r.Unit, ap);
            }
        }
    }

    [Fact]
    public void Speed_100_acts_once_per_turn()
    {
        var a = U("a", 100);
        var turns = Run(new TurnClock([a]), 3);
        Assert.Equal([("a", 1.0), ("a", 2.0), ("a", 3.0)], turns);
    }

    [Fact]
    public void Different_speeds_interleave_exactly()
    {
        var a = U("a", 100);
        var b = U("b", 150);
        var turns = Run(new TurnClock([a, b]), 6);

        // b reaches 100 at sub-tick 67 (67 × 150 = 10 050), keeps the 50 left over, then every 66–67 sub-ticks.
        Assert.Equal(("b", 0.67), turns[0]);
        Assert.Equal(("a", 1.0), turns[1]);
        Assert.Equal(6, turns.Count(t => t.Id == "a"));
        Assert.Equal(9, turns.Count(t => t.Id == "b"));                 // 1.5 × as many turns
    }

    [Fact]
    public void Initiative_is_the_starting_act()
    {
        var a = U("a", 100, act: 60);
        Assert.Equal(("a", 0.4), Run(new TurnClock([a]), 0.5)[0]);
    }

    [Fact]
    public void Act_above_200_gives_an_extra_turn_at_once()
    {
        var a = U("a", 100, act: 250);
        var turns = Run(new TurnClock([a]), 0.6);
        Assert.Equal([("a", 0.0), ("a", 0.0), ("a", 0.5)], turns);       // 250 → 150 → 50, then +50 by T 0.5
    }

    [Fact]
    public void Negative_act_delays_the_turn()
    {
        var a = U("a", 100, act: -50);
        Assert.Equal(("a", 1.5), Run(new TurnClock([a]), 2)[0]);
    }

    [Fact]
    public void A_200_ap_action_works_like_a_cooldown()
    {
        var a = U("a", 100, act: 100);
        var turns = Run(new TurnClock([a]), 4.5, ap: 200);
        Assert.Equal([("a", 0.0), ("a", 2.0), ("a", 4.0)], turns);
    }

    [Fact]
    public void Ties_go_to_higher_act_then_higher_speed_then_list_order()
    {
        var first = U("first", 100, act: 100);
        var second = U("second", 100, act: 100);
        Assert.Equal("first", ((TurnReady)new TurnClock([first, second]).Next()).Unit.Id);

        var slow = U("slow", 100, act: 100);
        var fast = U("fast", 120, act: 100);
        Assert.Equal("fast", ((TurnReady)new TurnClock([slow, fast]).Next()).Unit.Id);

        var low = U("low", 100, act: 100);
        var high = U("high", 100, act: 130);
        Assert.Equal("high", ((TurnReady)new TurnClock([low, high]).Next()).Unit.Id);
    }

    [Fact]
    public void A_cast_completes_after_its_timer_and_holds_the_turn()
    {
        var mage = U("mage", 100, act: 100);
        var clock = new TurnClock([mage]);

        var turn = (TurnReady)clock.Next();
        TurnClock.Spend(mage, 100);
        clock.BeginCast(mage, "fire_bolt", "goblin", 50);                // ready at T 0.50

        var done = Assert.IsType<CastComplete>(clock.Next());
        Assert.Equal(0.5, done.Time);
        Assert.Equal("fire_bolt", done.Cast.ActionId);
        Assert.Null(mage.Casting);
        Assert.IsType<BuffTick>(clock.Next());                           // T 1.00
        Assert.Equal(1.0, Assert.IsType<TurnReady>(clock.Next()).Time);
    }

    [Fact]
    public void A_slow_cast_holds_the_turn_until_it_completes()
    {
        var mage = U("mage", 100, act: 100);
        var clock = new TurnClock([mage]);
        clock.Next();
        TurnClock.Spend(mage, 100);
        clock.BeginCast(mage, "big_spell", null, 150);

        Assert.IsType<BuffTick>(clock.Next());                           // T 1: Act is 100 but the cast holds it
        Assert.Equal(1.5, Assert.IsType<CastComplete>(clock.Next()).Time);
        Assert.Equal(1.5, Assert.IsType<TurnReady>(clock.Next()).Time);  // the held turn comes right after
    }

    [Fact]
    public void An_interrupted_cast_never_completes()
    {
        var mage = U("mage", 100, act: 100);
        var clock = new TurnClock([mage]);
        clock.Next();
        TurnClock.Spend(mage, 100);
        clock.BeginCast(mage, "fire_bolt", null, 50);

        Assert.NotNull(TurnClock.Interrupt(mage));
        Assert.IsType<BuffTick>(clock.Next());
        Assert.IsType<TurnReady>(clock.Next());
    }

    [Fact]
    public void The_buff_clock_ticks_every_base_turn_whatever_the_speeds()
    {
        var clock = new TurnClock([U("slow", 40), U("fast", 250)]);
        var buffTurns = new List<int>();
        while (clock.Time < 3)
        {
            var e = clock.Next();
            if (e is BuffTick b) buffTurns.Add(b.Turn);
            if (e is TurnReady r) TurnClock.Spend(r.Unit, 100);
        }
        Assert.Equal([1, 2, 3], buffTurns);
    }

    [Fact]
    public void Dead_and_speed_zero_units_never_act()
    {
        var dead = U("dead", 100, act: 100);
        dead.TakeDamage(9999);
        var stunned = U("stunned", 0);
        var clock = new TurnClock([dead, stunned]);
        Assert.IsType<BuffTick>(clock.Next());
        Assert.IsType<BuffTick>(clock.Next());
    }
}
