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

    /// <summary>
    /// What a path leads to, in a few words: what happens on the way ("−25 Gold, Alchemist Station here"), then the
    /// fight ("Boss fight (1 Stamina, 2 enemies)"), "more choices" or "come back later". A path that only ends says
    /// "no fight".
    /// </summary>
    public static string OutlookText(GameData data, Outlook o)
    {
        var s = data.Text;
        var parts = o.Effects.Select(e => EffectText(data, e)).OfType<string>().ToList();
        if (o.Fight is { } f)
        {
            var enemies = data.Encounters[f.Encounter].Enemies.Count;
            parts.Add(s.Format("preview.fight", ("scale", ScaleName(s, f.Scale)), ("stamina", f.Scale == CombatScale.Skirmish ? 0 : 1),
                    ("enemies", s.Format(enemies == 1 ? "preview.enemy" : "preview.enemies", ("count", enemies))))
                + (f.Initiative == InitiativeModifier.None ? "" : s.Format("preview.initiative", ("initiative", s["initiative." + Key(f.Initiative)]))));
        }
        else if (o.Defers) parts.Add(s["preview.defers"]);
        else if (!o.Ends) parts.Add(s["preview.story"]);
        else if (parts.Count == 0) parts.Add(s["preview.ends"]);
        return string.Join(s["preview.separator"], parts);
    }

    static string Signed(int n) => (n > 0 ? "+" : "−") + Math.Abs(n);

    static string? EffectText(GameData data, PreviewEffect e)
    {
        var s = data.Text;
        switch (e)
        {
            case ResourceEffect { Block: var x }:
                var amount = x.Share != 0 ? (x.Share > 0 ? "+" : "−") + Pct(Math.Abs(x.Share)) : Signed(x.Amount);
                return s.Format(x.Target == EventTarget.All ? "preview.resource_all" : "preview.resource_one",
                    ("amount", amount), ("resource", ResourceName(s, x.Resource)));
            case ActionEffect { Action: BuffAction b }:
                var buff = data.Buffs[b.Buff];
                return s.Format(IsCurse(data, buff) ? "preview.curse" : "preview.buff", ("buff", buff.Name), ("length", BuffLength(s, buff)));
            case ActionEffect { Action: GoldAction g }:
                return s.Format("preview.gold", ("amount", Signed(g.Amount)));
            case ActionEffect { Action: CampAction c }:
                return s.Format("preview.camp", ("amount", Signed(c.Amount)));
            case ActionEffect { Action: SpawnAction x }:
                return s.Format("preview.spawn", ("interactable", InteractableName(s, x.Kind)));
            case ActionEffect { Action: RevealAction }:
                return s["preview.reveal"];
            case RewardEffect { Reward: var r }:
                return r.Kind switch
                {
                    RewardKind.Gold => s.Format("preview.gold", ("amount", Signed(r.Amount))),
                    RewardKind.Camp => s.Format("preview.camp", ("amount", Signed(r.Amount))),
                    _ => s.Format("preview.item", ("amount", Signed(r.Amount)), ("item", data.Items[r.Item!].Name)),
                };
            default:
                return null;
        }
    }

    /// <summary>A curse rather than a buff: it lowers a stat, or hurts or drains its holder.</summary>
    public static bool IsCurse(GameData data, BuffDef b) =>
        b.Stats.Any(x => x.Value < 0) || b.PeriodicDamage > 0 || b.DelayedDamage > 0 || b.ManaDrain > 0 || b.Cc != CcKind.None;

    /// <summary>A choice's preview: "70% (Rogue, Disable 1): no fight / 30%: Major fight (1 Stamina)".</summary>
    public static string PreviewText(GameData data, ChoicePreview p)
    {
        var s = data.Text;
        var who = p.Heroes.Count == 1 ? p.Heroes[0].Name : "";
        if (p.Chance is not { } chance)
            return OutlookText(data, p.Success);
        var traits = p.Choice.Roll!.Traits;
        var detail = traits.Count > 0 && p.Heroes.Count > 0
            ? s.Format("preview.roll_trait", ("hero", who.Length > 0 ? who : p.Heroes[0].Name), ("trait", TraitNames(data, traits, p.Heroes[0])))
            : "";
        return s.Format("preview.roll", ("chance", Pct(chance)), ("detail", detail), ("success", OutlookText(data, p.Success)),
            ("fail_chance", Pct(1 - chance)), ("failure", OutlookText(data, p.Failure!)));
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
        FlagSet => null,                            // the story's bookkeeping: the text tells the player what changed
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
        XpGained x => s.Format("run.xp", ("hero", x.Hero.Name), ("amount", x.Amount), ("total", x.Total)),
        LevelUp l => s.Format("run.level_up", ("hero", l.Hero.Name), ("level", l.Level)),
        SkillRaised k => s.Format("run.skill_raised", ("hero", k.Hero.Name), ("skill", k.Skill.Name), ("level", k.Level)),
        MasteryUnlocked m => s.Format("run.mastery", ("hero", m.Hero.Name), ("mastery", m.Mastery.Name)),
        _ => null,
    };

    static string BuffLength(Strings s, BuffDef b) =>
        s.Format(b.Duration == DurationKind.Steps ? "run.steps" : "run.battles", ("count", b.Length));
}
