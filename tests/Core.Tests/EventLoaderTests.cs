using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Tests;

/// <summary>Events load from data/events/ and every problem is reported by file and block (M3A brief §3).</summary>
public class EventLoaderTests
{
    static DataException Fails(string replace, string with, string file = "events/t_start.json")
    {
        var files = RunTestData.Files();
        Assert.Contains(replace, files[file]);
        files[file] = files[file].Replace(replace, with);
        return Assert.Throws<DataException>(() => RunTestData.Load(files));
    }

    [Fact]
    public void The_test_dungeon_loads()
    {
        var data = RunTestData.Data;
        Assert.Equal(4, data.Events.Count);
        var start = data.Events["t_start"];
        Assert.Equal("intro", start.Start);
        var pick = ((StoryBlock)start.Block("intro")).Choices[0];
        Assert.Equal((0.5, 0.2), (pick.Roll!.Base, pick.Roll.PerPoint));
        Assert.Equal(["disable"], pick.Roll.Traits);
        Assert.Equal(["disable"], pick.HeroTraits);
    }

    [Fact]
    public void A_link_to_a_missing_block_fails()
    {
        var e = Fails("\"success\": \"scout\"", "\"success\": \"scoot\"");
        Assert.Equal(("events/t_start.json", "blocks.intro"), (e.File, e.Field));
        Assert.Contains("unknown block \"scoot\"", e.Message);
    }

    [Fact]
    public void A_block_nothing_leads_to_fails()
    {
        var e = Fails("\"success\": \"scout\"", "\"success\": \"loot\"");
        Assert.Equal("blocks.scout", e.Field);
        Assert.Contains("no block leads here", e.Message);
    }

    [Fact]
    public void Missing_text_fails()
    {
        var e = Fails("{ \"id\": \"later\"", "{ \"id\": \"later2\"");
        Assert.Contains("event.t_start.intro.later2", e.Message);
    }

    [Fact]
    public void Defer_must_end_its_block()
    {
        var e = Fails("{ \"type\": \"defer\", \"resume\": \"back\" }] }", "{ \"type\": \"defer\", \"resume\": \"back\" }], \"success\": \"loot\" }");
        Assert.Contains("defer ends the event", e.Message);
    }

    [Fact]
    public void Unknown_encounters_traits_and_nodes_fail()
    {
        Assert.Contains("unknown encounter", Fails("\"t_archer\"", "\"t_archers\"", "events/t_fight.json").Message);
        Assert.Contains("isn't a trait", Fails("\"trait\": \"disable\" }]", "\"trait\": \"health\" }]").Message);
        Assert.Contains("unknown node", Fails("[\"t_c\"]", "[\"t_z\"]").Message);
    }

    [Fact]
    public void A_buff_from_an_event_lasts_steps_or_battles()
    {
        var e = Fails("\"buff\": \"t_keen\"", "\"buff\": \"chill\"");
        Assert.Contains("lasts steps or battles", e.Message);
    }

    [Fact]
    public void Blocks_that_loop_without_a_story_fail()
    {
        var e = Fails("{ \"success\": \"intro\" }", "{ \"success\": \"back\" }");
        Assert.Equal("blocks.back", e.Field);
        Assert.Contains("loop", e.Message);
    }

    [Fact]
    public void A_node_with_a_missing_event_fails()
    {
        var files = RunTestData.Files();
        files.Remove("events/t_end.json");
        var e = Assert.Throws<DataException>(() => RunTestData.Load(files));
        Assert.Equal(("nodes.json", "t_e"), (e.File, e.Field));
    }

    [Fact]
    public void Nodes_must_be_reachable_and_maps_need_a_pathway()
    {
        var files = RunTestData.Files();
        files["node_links.json"] = files["node_links.json"].Replace("{ \"node\": \"t_b\", \"to\": \"t_c\" },", "");
        Assert.Contains("can't be reached", Assert.Throws<DataException>(() => RunTestData.Load(files)).Message);
        files = RunTestData.Files();
        files["nodes.json"] = files["nodes.json"].Replace(", \"interactables\": [\"pathway\"]", "");
        Assert.Contains("needs a Pathway", Assert.Throws<DataException>(() => RunTestData.Load(files)).Message);
    }
}
