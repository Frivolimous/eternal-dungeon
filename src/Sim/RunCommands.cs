using EternalDungeon.Core.Combat;
using EternalDungeon.Core.Data;
using EternalDungeon.Core.Exploration;

// sim event and sim dungeon: Events and whole dungeon runs, headless (M3A brief §8).
static class RunCommands
{
    /// <summary>
    /// Plays one Event from its Node with the preset party: the given choices in order (by id), then the first one
    /// shown. Prints each block's text, the choices with their previews, rolls, fights (auto-battle) and the outcome.
    /// </summary>
    public static int Event(GameData data, string? eventId, Options options)
    {
        if (eventId is null || !data.Events.ContainsKey(eventId))
            throw new OptionException($"Give an event id, for example: sim event caged_merchant. Known: {string.Join(", ", data.Events.Keys.Order())}");
        var node = data.Nodes.Values.FirstOrDefault(n => n.Event == eventId)
            ?? throw new OptionException($"No Node holds the event \"{eventId}\"");
        var dungeon = data.DungeonList.First(d => d.Maps.Any(m => m.Id == node.Map));
        var seed = options.GetULong("seed", 1);
        var choices = new Queue<string>((options.Get("choices") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        var battleLog = options.Flag("battle-log");
        var run = new DungeonRun(data, dungeon.Id, seed);
        // A Dungeon flag can be set beforehand to see a branch: --flags merchant_freed,wife_saved
        foreach (var flag in (options.Get("flags") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            run.Flags[flag] = true;
        if (options.Get("gold") is not null) run.DebugSetGold(options.GetInt("gold", 0));

        Console.WriteLine($"{data.Text[EventDef.NameKey(eventId)]} · {node.Name} · seed {seed}");
        Console.WriteLine(Party(run));
        Console.WriteLine();
        Print(data, run.DebugExplore(node.Id));
        while (run.State is RunState.Event or RunState.Battle)
        {
            if (run.State == RunState.Battle)
            {
                var session = run.Battle!.Session;
                session.AutoBattle = true;
                session.Advance();
                if (battleLog) Console.Write(Indent(CombatLog.Write(session.Battle, LogLevel.Brief)));
                Print(data, run.FinishBattle());
                continue;
            }
            if (run.Choices.Count == 0)
            {
                Print(data, run.Continue());
                continue;
            }
            PrintChoices(data, run);
            var pick = choices.Count > 0 ? choices.Dequeue() : RunPolicy.Pick(run.Choices).Id;
            if (run.Choices.All(c => c.Id != pick))
                throw new OptionException($"\"{pick}\" isn't one of the choices shown ({string.Join(", ", run.Choices.Select(c => c.Id))})");
            Print(data, run.Choose(pick));
        }
        Console.WriteLine();
        Console.WriteLine(Party(run));
        Console.WriteLine($"Gold {run.Gold}, Camp charges {run.CampCharges}, pack: {Pack(data, run)}");
        var flags = run.Flags.Where(f => f.Value).Select(f => f.Key).ToList();
        if (flags.Count > 0) Console.WriteLine($"Flags: {string.Join(", ", flags)}");
        return 0;
    }

    /// <summary>
    /// Auto-plays a whole dungeon (<see cref="RunPolicy"/>): one run with its log, or with --runs a summary over many
    /// seeds.
    /// </summary>
    public static int Dungeon(GameData data, string? dungeonId, Options options)
    {
        dungeonId ??= data.DungeonList.FirstOrDefault()?.Id;
        if (dungeonId is null || !data.Dungeons.ContainsKey(dungeonId))
            throw new OptionException($"Give a dungeon id. Known: {string.Join(", ", data.Dungeons.Keys)}");
        var seed = options.GetULong("seed", 1);
        if (options.Get("runs") is not null)
        {
            var runs = options.GetInt("runs", 100);
            var summary = new DungeonSummary();
            for (var i = 0; i < runs; i++) summary.Play(data, dungeonId, seed + (ulong)i);
            Console.WriteLine($"{data.Dungeons[dungeonId].Name} · {runs} runs · seeds {seed}–{seed + (ulong)runs - 1} · auto-play");
            Console.Write(summary.Report());
            return 0;
        }

        var run = new DungeonRun(data, dungeonId, seed);
        var battleLog = options.Flag("battle-log");
        var policy = new RunPolicy
        {
            OnResult = r => Print(data, r),
            OnBattle = run =>
            {
                Console.WriteLine($"  {Party(run)}");
                if (!battleLog) return;
                var s = run.Battle!.Session;
                s.AutoBattle = true;
                s.Advance();
                Console.Write(Indent(CombatLog.Write(s.Battle, LogLevel.Brief)));
            },
        };
        Console.WriteLine($"{data.Dungeons[dungeonId].Name} · seed {seed} · auto-play");
        Console.WriteLine(Party(run));
        policy.Play(run);
        Console.WriteLine();
        Console.WriteLine($"Result: {run.State}. {Party(run)}");
        var s = run.Stats;
        Console.WriteLine($"{s.Steps} Nodes, {s.Battles} battles ({s.Victories} won, {s.Flees} fled), {s.Camps} Camps, " +
            $"{s.Sanctuaries} Sanctuaries, {s.ItemsUsed} belt charges used, {run.Gold} Gold left");
        return 0;
    }

    static void Print(GameData data, RunResult r)
    {
        foreach (var line in RunLog.Lines(data, r))
            Console.WriteLine(line);
    }

    static void PrintChoices(GameData data, DungeonRun run)
    {
        var e = run.Event!;
        var block = (StoryBlock)e.Block!;
        foreach (var p in run.Previews)
        {
            var who = p.Heroes.Count == 1 && p.Heroes[0] != e.Active && p.Chance is null ? $" [{p.Heroes[0].Name}]" : "";
            Console.WriteLine($"  · {p.Choice.Id}: {RunLog.ChoiceText(data.Text, e.Def, block, p.Choice)}{who} → {RunLog.PreviewText(data, p)}");
        }
    }

    static string Party(DungeonRun run) =>
        string.Join(" · ", run.Heroes.Select(h =>
            $"{h.Name} {h.Health}/{h.MaxHealth} HP" + (h.MaxMana > 0 ? $" {h.Mana}/{h.MaxMana} MP" : "") +
            $" St {h.Stamina}" + (h.Status is HeroStatus.Ready ? "" : $" {h.Status}")));

    static string Pack(GameData data, DungeonRun run) =>
        run.Pack.Count == 0 ? "empty" : string.Join(", ", run.Pack.Select(p => $"{data.Items[p.Key].Name} ×{p.Value}"));

    static string Indent(string text) =>
        string.Concat(text.Split('\n').Select(l => l.Length > 0 ? "    | " + l + "\n" : ""));
}
