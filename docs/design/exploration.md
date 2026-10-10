# Exploration

How a party explores a dungeon, and what it meets there (decided 2026-10-07). The map system decides **how the player
discovers content**; the event system decides **what happens when they meet it**. Combat itself is in
[Combat](combat.md); dungeon content and run outcomes are in [Dungeons](dungeons.md).

## Map systems

1. **Overland:** the world map that connects towns, dungeons and other destinations. **Not designed yet** (see
   [Open questions](open-questions.md)). What is fixed: it's part of the metagame, it's about travel and choosing a
   destination, it has its own map and navigation, and moving on it costs no Stamina.
2. **Outdoor Maps:** forests, swamps, ruins, villages and other open areas.
3. **Indoor Maps:** caves, crypts, castles, temples, fortresses and other enclosed places.

A **Dungeon** is the whole set of Maps that make up one expedition, in any mix of Outdoor and Indoor (a dungeon can be
all Outdoor). Outdoor and Indoor Maps share the same exploration, node, event, resource and progression systems; they
differ only in how they look and how their nodes connect.

## Maps and nodes

A Map is a network of connected **Nodes**. A Node is one piece of explorable content:
**Map → Node → Event / Interactables**. A Node can have a Node type, an Event, any number of Interactables,
connections to other Nodes, and other per-map configuration.

- The player **explores Nodes**; the party has no position on the map. An unexplored Node becomes **eligible** once it
  connects to any explored Node (the **explored network**), so the player can explore any eligible Node, and resume
  deferred events or use Interactables anywhere in the explored network, at any time.
- **Events:** a Node can hold an Event, triggered only by exploring the Node (once: a Node can't be explored twice) or
  by resuming a deferred Event. Some Nodes have no Event: they hold only Interactables, or nothing.
- **Order:** if a Node has an Event and Interactables, the Event comes first and must be closed or deferred before its
  Interactables can be used. A deferred Event must still be resolved before that Node's Interactables can be used
  (for example a miniboss guarding the Stairs).

```text
                 [Goblin Camp]
                       |
[River] — [Forest Clearing] — [Monster's Den]
                       |
                  [Old Ruins]
```

Once Forest Clearing is explored, all four neighbours are eligible. After exploring Monster's Den, Goblin Camp, River
and Old Ruins are all still eligible: they still touch the explored network.

### How Nodes look

- **Outdoor:** each Node is a **Region**, and its Node type shows as a **Feature** (Forest Clearing, Giant Swamp Tree,
  Goblin Village, Merchant Camp, Ancient Ruins). Outdoor Maps are a network of Regions under fog of war. They can link
  to neighbouring Outdoor Maps, or lead into Indoor Maps through transition Interactables.
- **Indoor:** Nodes are **Rooms** and **Hallways**. A Room shows its Node type (Treasure Room, Prison, Shrine, Library,
  Barracks, Boss Room), which decides what Events can happen there. Rooms and Hallways link to either.
- **Hallways** are a Node type (`hallway`) used only on Indoor Maps, with nothing special beyond that. Each Indoor Map
  has a **hallway event frequency** from 0% to 100%: that share of its Hallways, rounded down (25% of 5 = 1), gets a
  Hallway Event, placed at random during generation and drawn from the Map's **hallway event pool** (patrols, ambushes,
  traps, hazards, strange discoveries). Hallway Events use the normal event system.

Region, Feature, Room and Hallway are only how a Node is drawn: underneath, everything is a Node.

### Fog of war

What the player sees of a Map (decided 2026-10-08, to try in M3A):

- **Explored** Nodes show everything: their Feature, Interactables and any deferred Event.
- **Eligible** Nodes show their position and Feature, but not their Event.
- **Map reveals** show a Node's position and Feature even before it's eligible. An "enemies" or "boss" reveal adds an
  icon. If this proves too little, reveals and eligible Nodes may also show the Event's type.
- Everything else stays hidden.

### Moving between Maps

**Transition Interactables** move the party on. They share one behaviour with three looks: **Stairs** (Indoor, between
floors or levels), **Pathway** (Outdoor to Outdoor) and **Indoor Entrance** (Outdoor into Indoor, such as a dungeon
entrance). Using one completes the current Map and advances the dungeon. A completed Map can never be re-entered, so
it's a permanent progress point.

## Events

Events are the same on every kind of Map. An Event is a self-contained sequence of **blocks**: blocks point to other
blocks of the same Event, never into another Event. Events can branch on player choices, traits, classes, dungeon
flags, earlier actions, the Event's own state, resources and other conditions.

An Event has a name, a base type, context tags, its blocks and their branching, conditions, its own state, and outcomes
(state changes, rewards, Interactables, or deferral).

**Writing:** Event text follows the [tone guide](tone-guide.md): a setup of 50 words or fewer, choices of 10 words or
fewer that make clear what they do and roughly what they risk, voiceless heroes (`{hero}` or "the party", with no
thoughts beyond the player's choice), and no exact numbers in the prose (the decision previews carry them). Peoples,
places and figures come from the [lore](lore.md). A trait choice uses the trait for what it opens
([Classes](classes.md) › What each trait opens). How to shape an Event, when to roll or lock a trait, roll sizes and endings are in the [Event guide](event-guide.md).

- **Event state** belongs to one Event instance: choices made, actions taken, temporary flags, and the block a deferred
  Event resumes from. It's kept while the Event is deferred.
- **Dungeon flags** are the dungeon's persistent state: one flat dictionary of string keys for the whole expedition
  (`goblin_village_liberated = true`). Events read and set flags, and the Map and its Nodes respond to them; Events
  never edit the map topology directly.
- **Self-contained dungeons:** Events can't create or change anything outside the dungeon. Anything that reaches the
  metagame goes through the dungeon's resolution (completed, left or wiped; see [Dungeons](dungeons.md)).

### Closed and deferred

An Event ends in one of two states (there is no separate "completed"):

- **Deferred:** only when an Action block defers it (`deferEvent`), or when the party flees a Boss combat. It stays on
  its Node with its state, and the player can resume it from anywhere in the explored network; it continues from its
  resume block.
- **Closed:** when the flow ends: a block with no continuation, a Branch block with no valid branch, a failed roll with
  no failure link, a Combat block won with no `success` link, fleeing a Skirmish or Major combat (unless a `flee` link
  is set), or any block rule that ends the Event. A closed Event never triggers again.

## Interactables

An **Interactable** is a lasting feature of a Node that the player chooses to use, possibly many times or until used up:
transitions (Stairs, Pathway, Indoor Entrance), Sanctuary, Shop or Merchant, Forge or crafting station, Alchemist
Station, Shrine. An Event is one-time; an Interactable stays. An Event can spawn one at its Node (a Merchant Event
spawns a Forge), and it stays usable after the Event closes.

## Combat in exploration

Combat always starts from a Combat block in an Event. An encounter's scale doesn't depend on the Node type (a Skirmish
can be in a Room, a Major fight in a Hallway).

| Scale | Stamina on victory or flee | Use |
| --- | --- | --- |
| Skirmish | 0 | Easy fights: hallway patrols, sleeping sentries, small beasts. Costs HP and Mana, not endurance |
| Major | 1 | Standard and hard fights: fortified rooms, elite garrisons, ambushes |
| Boss | 1 | Map bosses and named champions |

A combat ends in one of three ways:

1. **Victory:** follows `success` and costs the scale's Stamina.
2. **Flee:** costs the same Stamina as victory. Fleeing a Skirmish or Major combat closes the Event (unless a `flee`
   link is set). Fleeing a Boss **defers** the Event, so the boss stays to retry after resting: resuming starts that
   Combat block again with the enemies at full strength, while earlier blocks stay done (a beaten outer patrol stays
   beaten).
3. **Death:** the whole party is dead, or all heroes are dead or unconscious with no rest left: a party wipe.

**Fleeing** (decided 2026-10-08): Flee is a hero action (100 AP, from any tile) and always works: the hero leaves the
battle at once, and enemies whose plan aimed at it plan again. When every hero has fled or died, the party has fled.
If the enemies all die while some heroes have fled, it's a victory: the fled heroes rejoin but get no XP for that
battle. Fled heroes pay the battle's Stamina cost like everyone else, and dead heroes stay dead. So a party can send one
hero away to avoid a wipe, at the cost of that hero's XP, the fight and its Stamina; that's intended.

**Battles start from the run's state:** current Health and Mana, Exhaustion penalties, and run-long buffs and curses.
Dead and Unconscious heroes are absent. Each battle takes its random seed from the run's random generator when it
starts, so a battle replays on its own.

## Map context and event selection

Events don't know which dungeon they're in: they're picked from the Map's and Node's context, and whatever the dungeon
adds flows down into its Maps and Nodes during generation.

- **Danger:** the dungeon sets a base Danger, and Maps and Nodes modify it: Dungeon Danger → Map danger modifier →
  Node danger modifier = **effective Node Danger**, which decides which Events are eligible. Events or alarms can
  raise the Map danger modifier (+1) for a garrison-wide alert, making the rest of that Map's fights and checks
  harder. (Danger is not a level: "level" means only hero level and item level.)
- **Tags:** each layer adds **Faction[]** and **Environment[]** tags, inherited Dungeon → Map → Node, each layer adding,
  removing or replacing values (`+`, `-`, `=`). Events check the resulting tags, not where they came from.
- **Event pools:** Events are drawn from pools by the Map's and Node's configuration and context. An Event or Event
  Group can set a selection weight, a Danger range, required Node types and Faction/Environment requirements.
- **Event Groups** exist only for generation: they make sure Events that depend on each other are generated together in
  compatible Nodes (`Goblin Prison` and `Refugee Camp`). Flags and conditions decide at runtime whether each is
  available.

## Expedition resources

A dungeon uses four resources: **Health, Mana, Stamina and consumables**. There is no passive Health or Mana
regeneration. Health and Mana carry between fights.

- **Consumables** (belt items) have limited uses per dungeon, not per fight: they don't refill between fights. Camps,
  Sanctuaries, Events and exploration skills (such as the Tinkerer's) can restore charges. Talismans are passive and
  never used up (see [Equipment](equipment.md)).

- **Mana** never goes below 0. **Health** never goes below 0, and a hero at 0 Health at any time (from an Event, a
  resource change or combat) is immediately **Dead**.
- **Stamina:** every hero has a max Stamina. Major and Boss fights cost 1 Stamina (won or fled), Skirmishes cost none,
  and some strong actions, rare exploration choices or Events cost Stamina too. Stamina can go negative:

| Stamina | State |
| --- | --- |
| 0 to −1 | **Exhausted:** lower Speed in combat, worse Event rolls |
| −2 to −3 | **Severe Exhaustion:** Speed lower still, Event rolls worse still |
| −4 | **Unconscious** (it can't go lower) |

- A fight's Stamina cost is paid **after** the fight, and Exhaustion penalties in a fight use the Stamina each hero has
  when it starts (decided 2026-10-08). A full bar of 4 therefore covers 4 fights without penalty: the 4th starts at 1
  and ends at 0 (Exhausted).

The penalties are placeholders (see Placeholders): a Speed reduction in combat and a flat penalty to that hero's Event
rolls.

### Dead and unconscious heroes

There must always be at least one hero who is neither dead nor unconscious. Dead and unconscious heroes can't fight,
can't be the Active Hero, don't give traits, classes or eligibility to Event options, are left out of every `all` and
`random` resource change or condition, and lose no more Stamina.

- If a hero dies or falls unconscious during an Event, a new Active Hero is picked at random from the eligible ones. If
  none is left, the Event ends at once and the party rests automatically or is wiped (below).
- If **all heroes are unconscious**, the party automatically Rests if it can (a Camp charge or an unused Sanctuary).
  If it can't, every hero becomes Dead: a **party wipe**.
- **Fallen heroes revive when the dungeon is completed** (see [Dungeons](dungeons.md) › Run outcomes). The Acolyte's
  exploration skill can revive one sooner.

### Rest, Camp and Sanctuary

- **Rest** restores Stamina equal to the hero's max Stamina, but **never above max** (decided 2026-10-08), so resting
  early wastes Stamina. It always ends Unconscious; a hero still below 0 is Exhausted or Severely Exhausted.
- **Camp charges** are shared by the party for the whole dungeon: 1 for a normal dungeon, 2 for a large one. Some Events
  grant +1. Camping works anywhere on any Map, uses 1 charge, Rests, restores part of Health and Mana (amount to tune),
  and clears Exhaustion if Stamina ends above 0.
- A **Sanctuary** is an Interactable, found or spawned at a Node, usable once. It restores 100% Health and Mana to every
  living hero and Rests (clearing Exhaustion if Stamina ends above 0).

## Rewards and penalties

Event outcomes affect the expedition only through these:

**Rewards**

- Loot and currency: Gold, equipment, consumables and key items.
- Dungeon state: setting flags, and **map reveals** (lifting fog or highlighting Node types: stairs, boss, sanctuary,
  enemies).
- Lasting buffs, timed in **steps** (Nodes explored) or **battles**. They can change combat stats, or raise a trait
  (+Trait) to improve Event odds. A step is one Node explored. A battle-timed buff or curse joins every battle as a buff lasting
  the whole fight, and counts down when the battle ends.
- Health, Mana or Stamina, or +1 Camp charge.
- Spawning an Interactable at the current Node (Sanctuary, Forge, Alchemist Station, Shop).
- A **temporary ally** in a 5th party slot, for the rest of the Map, the rest of the dungeon, or N battles.
- Blessings, Boons and Trinkets will join these once designed (see Open questions).

**Penalties**

- Losing Health, Mana, Gold or 1 Stamina.
- Lasting curses, timed in steps (Limping, Poison) or battles (Demoralized, Bleed, +50% Mana costs). Curses never lower
  a trait: there are no −Trait curses, ever (Jeremy, 2026-10-08).
- Raising the Map danger modifier (+1) for an alarm.

**Never**, to protect player agency:

- No durability or item loss: traps and curses never break, rot or destroy items.
- No locked Nodes or paths: the explored network stays fully open and readable.
- No disabled choices and no lowered traits: nothing lowers a trait (no −Trait curses, ever), so a trait-gated choice
  a hero has once stays available. +Trait buffs can raise a trait, improving odds and unlocking gated choices
  (decided 2026-10-08).
- No changes to max Health, Mana or Stamina from exploration Events, temporary or lasting.
- No item upgrades inside Event text: upgrades happen at a spawned Forge or Alchemist Station.

## Exploration actions

1. **Explore** an eligible Node, triggering its Event if it has one.
2. **Use an Interactable** anywhere in the explored network (shops, Sanctuaries, Forges, stations, transitions).
3. **Camp**, using a Camp charge.
4. **Leave** the dungeon (see Dungeons › Run outcomes).
5. **Resume** a deferred Event.
6. **Go to the next Map:** a shortcut button shown when a transition Interactable is usable.
7. **Hero Panel,** between Events (not in the middle of one): stats, skills (spending points), traits, the belt and
   party pack, and using potions. Equipment joins it in M3B.
8. **Use an exploration consumable** outside combat.
9. **Cast an exploration spell.**
10. **Use an exploration skill** (the Primary class's; see [Classes](classes.md)).

## Event blocks

Every block has an `id`, a `type` and a `config`, and blocks link to each other by id. Branches that lead on use
`success`; `failure` and `flee` appear only on rolls and combat.

| Block | Purpose |
| --- | --- |
| Story | Text, the player's choices, and a **decision preview** for each choice |
| Branch | Routing on conditions, not shown to the player |
| Combat | Starts a fight (Skirmish, Major or Boss) |
| Resource Change | Changes Health, Mana or Stamina for eligible heroes |
| Action | Sets Event or dungeon flags, spawns Interactables, reveals the map, applies buffs or curses (steps or battles; buffs can raise a trait, nothing lowers one), changes the Map danger modifier, changes resources (including +1 Camp), recruits a temporary ally, or defers the Event (resuming at a given block, or this one) |
| Reward | Hands rewards to the reward system for display and assignment |

- **Story:** every choice whose conditions hold is shown. Hovering one shows its decision preview: for a fight, its
  scale and Stamina cost; for a risk, the chance and what success and failure lead to ("70%: avoid the encounter
  (0 Stamina) / 30%: ambushed (Major combat, 1 Stamina)").
- **Rolls:** a choice can carry a roll: a base chance plus modifiers (per trait point, a flat Exhaustion penalty…),
  with `success` and `failure` links. A failed roll with no failure link closes the Event. The chance is kept between 0% and 100%,
  and the Exhaustion penalty is the rolling hero's own. Rolls use the run's seeded random generator.
- **Resource Change targets:** `active` (the Active Hero), `random` (a random eligible hero) or `all` (every eligible
  hero). A hero brought to 0 Health dies at once, and a new Active Hero is picked if needed.

### The Active Hero

During an Event there is always an Active Hero, picked at random from the eligible heroes when the Event starts. When a
condition or roll needs a hero, the game picks the eligible hero with the best relevant value (buffs and curses
included); on a tie, the current Active Hero, otherwise a random one of the tied. That hero then becomes the Active
Hero.

### Block shapes (for implementation)

The format as built (M3A; the full reference is in `data/README.md` › Events): a block is a flat object with its
`id`, `type` and fields, in snake_case like every other data file, and text is never inline: it's a key in
`strings.csv` derived from the ids (`event.<id>.<block>`, `event.<id>.<block>.<choice>`). Previews are worked out from
the blocks (a fight's scale and Stamina, a roll's chance), so they need no text of their own.

```json
{ "id": "intro", "type": "story", "choices": [
    { "id": "pick", "conditions": [{ "type": "trait", "trait": "disable" }],
      "roll": { "base": 0.4, "trait": "disable", "per_point": 0.3 }, "success": "picked", "failure": "snapped" },
    { "id": "leave", "success": "wait" } ] }

{ "id": "guards", "type": "combat", "encounter": "d0_cage_guards", "scale": "major", "initiative": "surprised", "success": "freed" }

{ "id": "freed", "type": "action", "actions": [ { "type": "flag", "key": "merchant_freed" },
    { "type": "reveal", "nodes": ["fe_post"], "icon": "boss" } ], "success": "thanks" }

{ "id": "wait", "type": "action", "actions": [{ "type": "defer", "resume": "check" }] }
```

Events are the one exception to flat tables (decided 2026-10-08): nested blocks don't fit them, so each Event is a JSON
file in `data/events/`. Every piece of player-facing text in an Event is a string key in `data/strings.csv` (for example
`event.goblin_prisoner.intro`), so the text stays editable in the Sheet. Shared balance constants may be pulled out of
Events later.
