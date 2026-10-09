using System.Globalization;
using EternalDungeon.Core.Data;

namespace EternalDungeon.Core.Exploration;

/// <summary>The run log in words, from strings.csv (like <see cref="Combat.CombatLog"/>). The simulator prints it; the
/// game's screens show the same text.</summary>
public static class RunLog
{
    static string Pct(double chance) => (chance * 100).ToString("0", CultureInfo.InvariantCulture) + "%";

    static string Key(object value) => JsonField.SnakeCase(value.ToString()!);

    public static string StatusName(Strings s, HeroStatus status) => s["status." + Key(status)];
    public static string ResourceName(Strings s, EventResource resource) => s["resource." + Key(resource)];
    public static string InteractableName(Strings s, InteractableKind kind) => s["interactable." + Key(kind)];
    public static string ScaleName(Strings s, CombatScale scale) => s["scale." + Key(scale)];

    /// <summary>A story block's text, with {hero} as the Active Hero's name.</summary>
    public static string StoryText(Strings s, EventDef e, StoryBlock block, Hero active) =>
        s.Format(EventDef.TextKey(e.Id, block.Id), ("hero", active.Name));

    public static string ChoiceText(Strings s, EventDef e, StoryBlock block, EventChoice choice) =>
        s[EventDef.ChoiceKey(e.Id, block.Id, choice.Id)];

    /// <summary>"Major fight (1 Stamina)", "no fight", "come back later" or "ends the event".</summary>
    public static string OutlookText(Strings s, Outlook o) =>
        o.Fight is { } f ? s.Format("preview.fight", ("scale", ScaleName(s, f.Scale)), ("stamina", f.Scale == CombatScale.Skirmish ? 0 : 1))
            + (f.Initiative == InitiativeModifier.None ? "" : s.Format("preview.initiative", ("initiative", s["initiative." + Key(f.Initiative)])))
        : o.Defers ? s["preview.defers"]
        : o.Ends ? s["preview.ends"]
        : s["preview.story"];

    /// <summary>A choice's preview: "70% (Rogue, Disable 1): no fight / 30%: Major fight (1 Stamina)".</summary>
    public static string PreviewText(GameData data, ChoicePreview p)
    {
        var s = data.Text;
        var who = p.Heroes.Count == 1 ? p.Heroes[0].Name : "";
        if (p.Chance is not { } chance)
            return OutlookText(s, p.Success);
        var traits = p.Choice.Roll!.Traits;
        var detail = traits.Count > 0 && p.Heroes.Count > 0
            ? s.Format("preview.roll_trait", ("hero", who.Length > 0 ? who : p.Heroes[0].Name), ("trait", TraitNames(data, traits, p.Heroes[0])))
            : "";
        return s.Format("preview.roll", ("chance", Pct(chance)), ("detail", detail), ("success", OutlookText(s, p.Success)),
            ("fail_chance", Pct(1 - chance)), ("failure", OutlookText(s, p.Failure!)));
    }

    static string TraitNames(GameData data, IReadOnlyList<string> traits, Hero hero)
    {
        var s = data.Text;
        var best = traits.OrderByDescending(hero.Trait).First();
        return s.Format("preview.trait_level", ("trait", data.Stats[best].Name), ("level", hero.Trait(best)));
    }

    /// <summary>Every line for one run action's result.</summary>
    public static IEnumerable<string> Lines(GameData data, RunResult result)
    {
        var s = data.Text;
        foreach (var o in result.Outcomes)
            if (Describe(data, s, o) is string line) yield return line;
    }

    static string? Describe(GameData data, Strings s, RunOutcome o) => o switch
    {
        MapEntered m => s.Format("run.map_entered", ("map", m.Map.Name)),
        NodeExplored n => s.Format("run.explored", ("node", n.Node.Name)),
        EventStarted e => s.Format(e.Resumed ? "run.event_resumed" : "run.event_started",
            ("event", s[EventDef.NameKey(e.Event.Id)]), ("hero", e.Active.Name)),
        StoryShown x => StoryText(s, x.Event, x.Block, x.Active),
        ChoiceMade c => s.Format("run.choice", ("hero", c.Hero.Name), ("choice", ChoiceText(s, c.Event, c.Block, c.Choice))),
        EventRolled r => s.Format(r.Roll.Success ? "run.roll_success" : "run.roll_failure", ("hero", r.Hero.Name),
            ("chance", Pct(r.Roll.Chance)),
            ("detail", (r.Traits.Count > 0 ? s.Format("run.roll_trait", ("trait", data.Stats[r.Traits.OrderByDescending(r.Hero.Trait).First()].Name), ("level", r.Level)) : "")
                + (r.Penalty < 0 ? s.Format("run.roll_penalty", ("penalty", Pct(r.Penalty))) : ""))),
        ActiveHeroChanged a => s.Format("run.active_changed", ("hero", a.Hero.Name)),
        ResourceChanged c => s.Format("run.resource", ("hero", c.Hero.Name), ("resource", ResourceName(s, c.Resource)),
            ("change", (c.After - c.Before > 0 ? "+" : "") + (c.After - c.Before)), ("before", c.Before), ("after", c.After)),
        StatusChanged c => s.Format("run.status", ("hero", c.Hero.Name), ("status", StatusName(s, c.After))),
        FlagSet f => s.Format("run.flag", ("key", f.Key), ("value", f.Value ? "true" : "false")),
        NodeRevealed n => s.Format(n.Icon == RevealIcon.None ? "run.revealed" : "run.revealed_icon", ("node", n.Node.Name),
            ("icon", s["reveal." + Key(n.Icon)])),
        BuffGained b => s.Format("run.buff_gained", ("hero", b.Hero.Name), ("buff", b.Buff.Name), ("length", BuffLength(s, b.Buff))),
        BuffEnded b => s.Format("run.buff_ended", ("hero", b.Hero.Name), ("buff", b.Buff.Name)),
        GoldChanged g => s.Format("run.gold", ("change", (g.Amount > 0 ? "+" : "") + g.Amount), ("total", g.Total)),
        ItemGained i => s.Format("run.item_gained", ("item", i.Item.Name), ("amount", i.Amount)),
        CampChargesChanged c => s.Format("run.camp_charges", ("change", (c.Amount > 0 ? "+" : "") + c.Amount), ("total", c.Total)),
        InteractableSpawned x => s.Format("run.spawned", ("interactable", InteractableName(s, x.Kind)), ("node", x.Node.Name)),
        EventDeferred e => s.Format("run.deferred", ("event", s[EventDef.NameKey(e.Event.Id)])),
        EventClosed e => s.Format("run.closed", ("event", s[EventDef.NameKey(e.Event.Id)])),
        BattleStarted b => s.Format("run.battle_started", ("encounter", b.Encounter.Name), ("scale", ScaleName(s, b.Scale)),
            ("initiative", b.Initiative == InitiativeModifier.None ? "" : s.Format("preview.initiative", ("initiative", s["initiative." + Key(b.Initiative)])))),
        BattleEnded b => s.Format("run.battle_ended", ("outcome", s["battle." + Key(b.Outcome)]), ("cost", b.StaminaCost),
            ("party", string.Join(", ", b.Fought.Select(h => $"{h.Name} {h.Health}/{h.MaxHealth}")))),
        HeroDied d => s.Format("run.hero_died", ("hero", d.Hero.Name)),
        Rested x => s[x.Automatic ? (x.Camp ? "run.rested_camp_auto" : "run.rested_sanctuary_auto") : (x.Camp ? "run.rested_camp" : "run.rested_sanctuary")],
        SanctuaryUsed => null,
        ItemUsed i => s.Format("run.item_used", ("hero", i.Hero.Name), ("item", i.Item.Name)),
        ItemMoved m => s.Format(m.ToBelt ? "run.item_to_belt" : "run.item_to_pack", ("hero", m.Hero.Name), ("item", m.Item.Name), ("amount", m.Amount)),
        ItemBought b => s.Format("run.item_bought", ("item", b.Item.Name), ("price", b.Price)),
        MapCompleted m => s.Format("run.map_completed", ("map", m.Map.Name)),
        DungeonCompleted d => s.Format("run.dungeon_completed", ("dungeon", d.Dungeon.Name)),
        PartyWiped => s["run.wiped"],
        _ => null,
    };

    static string BuffLength(Strings s, BuffDef b) =>
        s.Format(b.Duration == DurationKind.Steps ? "run.steps" : "run.battles", ("count", b.Length));
}
