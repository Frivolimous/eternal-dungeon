using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;
using static EternalDungeon.Core.Tests.TestData;

namespace EternalDungeon.Core.Tests;

/// <summary>Dungeon 0's content (docs/briefs/m3a-dungeon0-events.md) loads, and every choice of every Event plays.</summary>
public class Dungeon0Tests
{
    [Fact]
    public void Dungeon_0_has_its_two_maps_and_fifteen_events()
    {
        var d = Repo.Dungeons["dungeon_0"];
        Assert.Equal([8, 10], d.Maps.Select(m => m.Nodes.Count));
        var events = d.Maps.SelectMany(m => m.Nodes).Select(n => n.Event).OfType<string>().ToList();
        Assert.Equal(15, events.Count);
        Assert.All(events, e => Assert.True(Repo.Events.ContainsKey(e)));
    }

    [Fact]
    public void Every_party_trait_is_checked_somewhere()
    {
        var used = Repo.Events.Values.SelectMany(e => e.Blocks).OfType<StoryBlock>().SelectMany(s => s.Choices)
            .SelectMany(c => c.Conditions.OfType<TraitCondition>().SelectMany(t => t.Traits).Concat(c.Roll?.Traits ?? []))
            .ToHashSet();
        foreach (var trait in Repo.Heroes.SelectMany(h => Repo.Classes[h.Class].Traits))
            Assert.Contains(trait, used);
    }

    /// <summary>Every choice of each Event's first story block, then the first choice shown, fights on auto: no
    /// errors, and the Event ends closed or deferred.</summary>
    [Fact]
    public void Every_first_choice_of_every_event_plays_to_its_end()
    {
        foreach (var node in Repo.Nodes.Values.Where(n => n.Event is not null))
        {
            var first = Repo.Events[node.Event!].Blocks.OfType<StoryBlock>().First(s => s.Choices.Count > 0);
            foreach (var choice in first.Choices)
                for (ulong seed = 1; seed <= 3; seed++)
                {
                    var run = new DungeonRun(Repo, "dungeon_0", seed);
                    run.Flags["merchant_freed"] = run.Flags["wife_saved"] = true;
                    run.DebugSetGold(100);
                    run.DebugExplore(node.Id);
                    var policy = new RunPolicy();
                    var picked = false;
                    for (var i = 0; i < 50 && run.State is RunState.Event or RunState.Battle; i++)
                    {
                        if (!picked && run.Event?.Block == first)
                        {
                            Assert.Contains(choice.Id, run.Choices.Select(c => c.Id));
                            run.Choose(choice.Id);
                            picked = true;
                        }
                        else policy.Step(run);
                    }
                    Assert.True(picked, $"{node.Event}: never reached {first.Id}");
                    Assert.False(run.State is RunState.Event or RunState.Battle, $"{node.Event}.{choice.Id} didn't end");
                    Assert.NotEqual(EventStatus.Pending, run.Nodes[node.Id].Event);
                }
        }
    }

    [Fact]
    public void Auto_play_finishes_every_run_one_way_or_the_other()
    {
        var summary = new DungeonSummary();
        for (ulong seed = 1; seed <= 10; seed++)
        {
            var run = summary.Play(Repo, "dungeon_0", seed);
            Assert.True(run.Over, $"seed {seed} got stuck in state {run.State}");
        }
        Assert.Contains("Completed", summary.Report());
    }

    [Fact]
    public void A_run_logs_every_line_from_the_strings_table()
    {
        var run = new DungeonRun(Repo, "dungeon_0", 4);
        var lines = new List<string>();
        new RunPolicy { OnResult = r => lines.AddRange(RunLog.Lines(Repo, r)) }.Play(run);
        Assert.NotEmpty(lines);
        Assert.DoesNotContain(lines, l => l.Contains("[event.") || l.Contains("[run.") || l.Contains("[status."));
    }
}
