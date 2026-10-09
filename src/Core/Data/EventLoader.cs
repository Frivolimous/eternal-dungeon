namespace EternalDungeon.Core.Data;

/// <summary>
/// Reads and checks the Events in data/events/ (one JSON file per Event, named by its id). Every link must lead to a
/// block of the same Event, every block must be reachable from the start, every encounter, buff, item, trait, class and
/// Node must exist, and every piece of text must have its key in strings.csv. Errors name the file and the block.
/// </summary>
public static class EventLoader
{
    public const string Folder = "events";

    public static IReadOnlyDictionary<string, EventDef> Load(DataSource files, GameData data)
    {
        var events = new Dictionary<string, EventDef>();
        foreach (var file in files.List(Folder))
        {
            var e = Parse(file, files.Read(file)!, data);
            events.Add(e.Id, e);
        }
        foreach (var node in data.Nodes.Values)
            if (node.Event is { } id && !events.ContainsKey(id))
                throw new DataException(Schemas.Nodes.JsonFile, node.Id, $"unknown event \"{id}\" (no {Folder}/{id}.json)");
        return events;
    }

    public static EventDef Parse(string file, string text, GameData data)
    {
        var root = JsonField.Parse(file, text);
        root.OnlyFields("id", "type", "tags", "start", "blocks");
        var id = root["id"].Id();
        if ($"{Folder}/{id}.json" != file) throw root["id"].Error($"the file for event \"{id}\" must be {Folder}/{id}.json");
        var blocks = root["blocks"].Items().Select(b => ReadBlock(b, data)).ToList();
        if (blocks.Count == 0) throw root["blocks"].Error("an event needs at least one block");
        var start = root.Optional("start")?.Id() ?? blocks[0].Id;
        var e = new EventDef(id, root["type"].Id(), root.Optional("tags")?.Items().Select(t => t.Id()).ToList() ?? [], start, blocks);
        Check(file, e, data);
        return e;
    }

    static DataException Error(string file, string block, string problem) => new(file, $"blocks.{block}", problem);

    static void Check(string file, EventDef e, GameData data)
    {
        var ids = new HashSet<string>();
        foreach (var b in e.Blocks)
            if (!ids.Add(b.Id)) throw Error(file, b.Id, "two blocks have this id");
        if (!ids.Contains(e.Start)) throw new DataException(file, "start", $"unknown block \"{e.Start}\"");
        foreach (var b in e.Blocks)
            if (b.Links.FirstOrDefault(l => !ids.Contains(l)) is { } bad)
                throw Error(file, b.Id, $"links to unknown block \"{bad}\"");

        // Every block can be reached from the start.
        var seen = new HashSet<string> { e.Start };
        var queue = new Queue<string>([e.Start]);
        while (queue.TryDequeue(out var next))
            foreach (var l in e.Block(next).Links)
                if (seen.Add(l)) queue.Enqueue(l);
        if (e.Blocks.FirstOrDefault(b => !seen.Contains(b.Id)) is { } lost)
            throw Error(file, lost.Id, "no block leads here");

        // Blocks that run on their own (no story or fight between them) must not loop.
        static bool Auto(EventBlock b) => b is not (StoryBlock or CombatBlock);
        static IEnumerable<string> Continues(EventBlock b) => b is ActionBlock a ? new[] { a.Success }.OfType<string>() : b.Links;
        var done = new HashSet<string>();
        foreach (var b in e.Blocks.Where(Auto))
        {
            var path = new HashSet<string>();
            bool Loops(EventBlock x)
            {
                if (!Auto(x) || done.Contains(x.Id)) return false;
                if (!path.Add(x.Id)) return true;
                var loops = Continues(x).Any(l => Loops(e.Block(l)));
                path.Remove(x.Id);
                done.Add(x.Id);
                return loops;
            }
            if (Loops(b)) throw Error(file, b.Id, "these blocks loop without a story block or fight in between");
        }

        // Text.
        void Key(string key, string block)
        {
            if (!data.Text.Has(key)) throw Error(file, block, $"missing text: no key \"{key}\" in {Strings.FileName}");
        }
        Key(EventDef.NameKey(e.Id), e.Start);
        foreach (var s in e.Blocks.OfType<StoryBlock>())
        {
            Key(EventDef.TextKey(e.Id, s.Id), s.Id);
            foreach (var c in s.Choices)
                Key(EventDef.ChoiceKey(e.Id, s.Id, c.Id), s.Id);
        }
    }

    static EventBlock ReadBlock(JsonField b, GameData data)
    {
        var id = b["id"].Id();
        string? Link(string name) => b.Optional(name)?.Id();
        switch (b["type"].String())
        {
            case "story":
            {
                b.OnlyFields("id", "type", "image", "choices", "success");
                var choices = b.Optional("choices")?.Items().Select(c => ReadChoice(c, data)).ToList() ?? [];
                if (choices.Count > 0 && b.Optional("success") is { } s)
                    throw s.Error("a story block has choices or a success link (Continue), not both");
                if (choices.GroupBy(c => c.Id).FirstOrDefault(g => g.Count() > 1) is { } twice)
                    throw b["choices"].Error($"two choices have the id \"{twice.Key}\"");
                return new StoryBlock(id, b.Optional("image")?.Id(), choices, Link("success"));
            }
            case "branch":
            {
                b.OnlyFields("id", "type", "next");
                var options = b["next"].Items().Select(o =>
                {
                    o.OnlyFields("conditions", "success");
                    return new BranchOption(Conditions(o, data), o.Optional("success")?.Id());
                }).ToList();
                if (options.Count == 0) throw b["next"].Error("a branch needs at least one option");
                return new BranchBlock(id, options);
            }
            case "combat":
            {
                b.OnlyFields("id", "type", "encounter", "scale", "initiative", "success", "flee");
                var encounter = b["encounter"].Id();
                if (!data.Encounters.TryGetValue(encounter, out var def))
                    throw b["encounter"].Error($"unknown encounter \"{encounter}\" (not in {Schemas.Encounters.JsonFile})");
                if (def.Party.Count > 0)
                    throw b["encounter"].Error($"\"{encounter}\" places a party; an Event's encounter has only enemies (the party comes from the run)");
                return new CombatBlock(id, encounter, b["scale"].Enum<CombatScale>(),
                    b.Optional("initiative")?.Enum<InitiativeModifier>() ?? InitiativeModifier.None, Link("success"), Link("flee"));
            }
            case "resource":
            {
                b.OnlyFields("id", "type", "resource", "amount", "share", "target", "success");
                var resource = b["resource"].Enum<EventResource>();
                var amount = b.Optional("amount")?.Int() ?? 0;
                var share = b.Optional("share")?.Number() ?? 0;
                if ((amount != 0) == (share != 0)) throw b.Error("give an amount or a share (not 0), not both");
                if (share != 0 && resource == EventResource.Stamina) throw b["share"].Error("Stamina changes by an amount, not a share");
                if (share is < -1 or > 1) throw b["share"].Error("a share is from -1 to 1");
                return new ResourceBlock(id, resource, amount, share, b["target"].Enum<EventTarget>(), Link("success"));
            }
            case "action":
            {
                b.OnlyFields("id", "type", "actions", "success");
                var actions = b["actions"].Items().Select(a => ReadAction(a, data)).ToList();
                if (actions.Count == 0) throw b["actions"].Error("an action block needs at least one action");
                var defer = actions.FindIndex(a => a is DeferAction);
                if (defer >= 0 && (defer != actions.Count - 1 || b.Optional("success") is not null))
                    throw b["actions"].Error("defer ends the event: it comes last, and the block has no success link");
                return new ActionBlock(id, actions, Link("success"));
            }
            case "reward":
            {
                b.OnlyFields("id", "type", "rewards", "success");
                var rewards = b["rewards"].Items().Select(r => ReadReward(r, data)).ToList();
                if (rewards.Count == 0) throw b["rewards"].Error("a reward block needs at least one reward");
                return new RewardBlock(id, rewards, Link("success"));
            }
            default:
                throw b["type"].Error("expected one of story, branch, combat, resource, action, reward");
        }
    }

    static EventChoice ReadChoice(JsonField c, GameData data)
    {
        c.OnlyFields("id", "conditions", "roll", "success", "failure");
        EventRoll? roll = null;
        if (c.Optional("roll") is { } r)
        {
            r.OnlyFields("base", "trait", "per_point");
            var traits = r.Optional("trait") is { } t ? Traits(t, data) : [];
            var perPoint = r.Optional("per_point")?.Number() ?? 0;
            if (perPoint != 0 && traits.Count == 0) throw r.Error("per_point needs a trait");
            var chance = r["base"].Number();
            if (chance is < 0 or > 1) throw r["base"].Error("a chance is from 0 to 1");
            roll = new EventRoll(chance, traits, perPoint);
        }
        else if (c.Optional("failure") is { } f)
            throw f.Error("only a choice with a roll has a failure link");
        return new EventChoice(c["id"].Id(), Conditions(c, data), roll, c.Optional("success")?.Id(), c.Optional("failure")?.Id());
    }

    static List<EventCondition> Conditions(JsonField parent, GameData data) =>
        parent.Optional("conditions")?.Items().Select(c => ReadCondition(c, data)).ToList() ?? [];

    static EventCondition ReadCondition(JsonField c, GameData data)
    {
        switch (c["type"].String())
        {
            case "trait":
                c.OnlyFields("type", "trait", "min");
                var min = c.Optional("min")?.Int() ?? 1;
                if (min < 1) throw c["min"].Error("a trait condition needs at least level 1");
                return new TraitCondition(Traits(c["trait"], data), min);
            case "class":
                c.OnlyFields("type", "class");
                var cls = c["class"].Id();
                if (!data.Classes.ContainsKey(cls)) throw c["class"].Error($"unknown class \"{cls}\" (not in {Schemas.Classes.JsonFile})");
                return new ClassCondition(cls);
            case "flag":
            case "event_flag":
                c.OnlyFields("type", "key", "value");
                return new FlagCondition(c["key"].Id(), c.Optional("value")?.Bool() ?? true, c["type"].String() == "event_flag");
            case "gold":
                c.OnlyFields("type", "min");
                return new GoldCondition(c["min"].Int());
            case "resource":
                c.OnlyFields("type", "resource", "min", "who");
                var who = c.Optional("who")?.String() ?? "any";
                if (who is not ("any" or "all")) throw c["who"].Error("expected any or all");
                return new ResourceCondition(c["resource"].Enum<EventResource>(), c["min"].Int(), who == "all");
            default:
                throw c["type"].Error("expected one of trait, class, flag, event_flag, gold, resource");
        }
    }

    /// <summary>One trait id, or a list of them (the best of these counts).</summary>
    static List<string> Traits(JsonField f, GameData data)
    {
        var ids = f.Element.ValueKind == System.Text.Json.JsonValueKind.Array ? f.Items().Select(x => x.Id()).ToList() : [f.Id()];
        if (ids.Count == 0) throw f.Error("needs at least one trait");
        foreach (var t in ids)
            if (!data.Stats.TryGetValue(t, out var s) || s.Group != StatGroup.Trait)
                throw f.Error($"\"{t}\" isn't a trait (a stat in the trait group in {Schemas.Stats.JsonFile})");
        return ids;
    }

    static EventAction ReadAction(JsonField a, GameData data)
    {
        switch (a["type"].String())
        {
            case "flag":
            case "event_flag":
                a.OnlyFields("type", "key", "value");
                return new SetFlagAction(a["key"].Id(), a.Optional("value")?.Bool() ?? true, a["type"].String() == "event_flag");
            case "reveal":
                a.OnlyFields("type", "nodes", "icon");
                var nodes = a["nodes"].Items().Select(n =>
                {
                    var id = n.Id();
                    return data.Nodes.ContainsKey(id) ? id : throw n.Error($"unknown node \"{id}\" (not in {Schemas.Nodes.JsonFile})");
                }).ToList();
                return new RevealAction(nodes, a.Optional("icon")?.Enum<RevealIcon>() ?? RevealIcon.None);
            case "buff":
                a.OnlyFields("type", "buff", "target");
                var buff = a["buff"].Id();
                if (!data.Buffs.TryGetValue(buff, out var def)) throw a["buff"].Error($"unknown buff \"{buff}\" (not in {Schemas.Buffs.JsonFile})");
                if (def.Duration is not (DurationKind.Steps or DurationKind.Battles))
                    throw a["buff"].Error($"an Event's buff lasts steps or battles; \"{buff}\" lasts {JsonField.SnakeCase(def.Duration.ToString())}");
                return new BuffAction(buff, a["target"].Enum<EventTarget>());
            case "gold":
                a.OnlyFields("type", "amount");
                return new GoldAction(a["amount"].Int());
            case "camp":
                a.OnlyFields("type", "amount");
                return new CampAction(a["amount"].Int());
            case "spawn":
                a.OnlyFields("type", "interactable");
                var kind = a["interactable"].Enum<InteractableKind>();
                if (kind == InteractableKind.Pathway) throw a["interactable"].Error("Pathways are part of the map, not spawned");
                return new SpawnAction(kind);
            case "defer":
                a.OnlyFields("type", "resume");
                return new DeferAction(a["resume"].Id());
            case "complete_dungeon":
                a.OnlyFields("type");
                return new CompleteDungeonAction();
            default:
                throw a["type"].Error("expected one of flag, event_flag, reveal, buff, gold, camp, spawn, defer, complete_dungeon");
        }
    }

    static EventReward ReadReward(JsonField r, GameData data)
    {
        switch (r["type"].String())
        {
            case "gold":
            case "camp":
                r.OnlyFields("type", "amount");
                var amount = r["amount"].Int();
                if (amount < 1) throw r["amount"].Error("a reward is at least 1");
                return new EventReward(r["type"].String() == "gold" ? RewardKind.Gold : RewardKind.Camp, amount);
            case "item":
                r.OnlyFields("type", "item", "amount");
                var item = r["item"].Id();
                if (!data.Items.ContainsKey(item)) throw r["item"].Error($"unknown item \"{item}\" (not in {Schemas.Items.JsonFile})");
                var count = r.Optional("amount")?.Int() ?? 1;
                if (count < 1) throw r["amount"].Error("a reward is at least 1");
                return new EventReward(RewardKind.Item, count, item);
            default:
                throw r["type"].Error("expected one of gold, item, camp");
        }
    }
}
