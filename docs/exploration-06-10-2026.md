# Exploration System

## Overview

The game uses three related map systems:

1. **Overland Maps** — the large-scale world map used to select and travel between major destinations.
2. **Outdoor Maps** — explorable outdoor or location-based areas.
3. **Indoor Maps** — structured enclosed areas such as caves, crypts, castles, and temples.

Outdoor and Indoor Maps share the same underlying exploration, Node, Event, resource, and progression systems. Their primary difference is **visual representation and map topology**.

The map system determines **how the player discovers content**. The Event System determines **what happens when that content is encountered**.

A **Dungeon** is the complete collection of maps that make up a single dungeon expedition. A Dungeon can contain any combination of Outdoor and Indoor Maps, including a Dungeon consisting entirely of Outdoor Maps.

---

# Overland Maps

Overland is the large-scale world map and is part of the Metagame.

It connects towns, Dungeons, and other major destinations.

* Focused on travel and destination choice.
* Movement on the Overland Map does **not** consume Stamina.
* Uses its own map and navigation system, detailed separately.
* Overland is distinct from Dungeon exploration.

---

# Dungeon Maps

## Map Structure

A Dungeon consists of one or more **Maps**.

Each Map is either:

* **Outdoor**
* **Indoor**

Regardless of type, a Map is fundamentally a network of interconnected **Nodes**.

A Node represents a discrete piece of explorable content within the Map.

The core exploration model is:

**Map → Node → Event / Interactable**

Nodes may also contain other gameplay elements or provide connections to other Nodes.

The player explores Nodes rather than physically moving a party from one Node to another.

An unexplored Node becomes eligible for exploration when it is connected to **any Node the party has already explored**.

The player can therefore explore any eligible unexplored Node without needing to return to a specific previously explored Node.

Players do not have an exploration position on the map, and can therefore resume deferred events or use available interactables across the explored network at any time.

---

## Node Structure

All Nodes share the same underlying gameplay functionality.

A Node may have:

* A Node Type
* An Event
* One or more Interactables
* Connections to other Nodes
* Other configuration relevant to the Map

### Events, Interactables, and Priority

* **Events:** A Node can contain an Event upon initial exploration. The only way to trigger an Event is to **Explore** an unexplored Node, or to **Resume** a previously deferred Event. Completed/closed Events cannot be restarted because a Node cannot be explored a second time.
* **Nodes Without Events:** Some Nodes can appear during generation without Events. Nodes without Events can have Interactables, or they can have nothing (empty rules).
* **Interaction Order:** If a Node contains both an Event and an Interactable, the **Event triggers first** upon exploration and must be resolved (either closed or deferred) before any Interactable at that Node can be accessed. For example, a Node containing Stairs may also feature a miniboss Event that must be resolved before the Stairs interactable becomes usable. If a node contains a Deferred Event and an Interactable, the Deferred Event must still be resolved before the Interactable can be used.
* **Interactables:** Persistent gameplay elements associated with a Node that remain available after its Event closes.

---

# Outdoor Maps

Outdoor Maps represent areas such as:

* Goblin-infested forests
* Swamps
* Ruins
* Villages
* Wilderness areas
* Other open or semi-open environments

### Visual Representation

Outdoor Maps visually represent Nodes as **Regions**.

Each Region displays its **Node Type** as a **Feature** describing what is visually or thematically present there.

Examples:

* Forest Clearing
* Giant Swamp Tree
* Goblin Village
* Merchant Camp
* Ancient Ruins

Conceptually:

**Outdoor Node → Node Type (Feature) → Event / Interactable**

The terms **Region** and **Feature** describe the visual representation of an Outdoor Map. The underlying gameplay object is still a Node.

Outdoor Maps are generally represented as a connected network of Regions with fog of war.

Outdoor Maps can link directly to adjacent outdoor maps or lead to indoor maps via transition interactables.

---

# Indoor Maps

Indoor Maps represent structured enclosed environments such as:

* Caves
* Crypts
* Castles
* Temples
* Fortresses
* Other buildings or underground structures

### Visual Representation

Indoor Maps visually represent Nodes and their connections using:

* **Rooms**
* **Hallways**

Rooms and Hallways are two types of visual representation of Nodes.

A Room displays its **Node Type** (e.g., Treasure Room, Prison, Shrine, Library, Barracks, Boss Room) which determines what types of Events can occur there.

Indoor Maps allow Rooms to link to Hallways or other Rooms, and Hallways to link to Rooms or other Hallways.

Conceptually:

**Indoor Node → Node Type (Room / Hallway) → Event / Interactable**

---

# Hallways

Hallways are a special Node type (`type: "hallway"`) configured **only for Indoor Maps**. They have no additional special definition beyond their Node type.

Hallways functionally are Nodes and can lead to Rooms or to other Hallways.

Every Indoor Map has a configured **Hallway Event Frequency** between **0% and 100%**.

This determines the percentage of Hallways that contain a Hallway Event:

* 0% — no Hallways contain Events.
* 25% — approximately one quarter of Hallways contain Events.
* 100% — every Hallway contains an Event.

Hallway Event allocation uses **FLOOR** rounding during generation (e.g., 25% of 5 Hallways = `floor(1.25)` = 1 Hallway Event).

Hallway Events are assigned randomly during Map generation according to the configured frequency.

All Hallway Events are selected from the same **Hallway Event Pool**, determined by the configuration and tags of the current Map.

Possible Hallway Events include:

* Enemy patrols
* Ambushes
* Traps
* Environmental hazards
* Strange discoveries

Hallway Events use the same underlying Event System as standard Node Events.

---

# Progression Interactables (Stairs, Pathways, Indoor Entrances)

Transitioning between maps or completing a map is handled via **Interactables**. Functionally, these transition interactables share the exact same underlying logic with 3 distinct visual skins:

1. **Stairs:** Used in Indoor Maps to move between floors or indoor levels.
2. **Pathway:** Used in Outdoor Maps to lead to another Outdoor Map.
3. **Indoor Entrance:** Used in Outdoor Maps to lead into an Indoor Map (e.g., a dungeon entrance).

### Map Completion

Activating the entrance/transition Interactable to the next map completes the current map and advances the Dungeon.

Once the party leaves a completed Map, **it cannot return to that Map**. Map completion is therefore a permanent progression point within the Dungeon.

---

# Map Exploration

The player explores any eligible unexplored Node.

A Node becomes eligible when it is connected to **any Node that has already been explored**.

For example:

```text
                 [Goblin Camp]
                       |
[River] — [Forest Clearing] — [Monster's Den]
                       |
                  [Old Ruins]
```

Once Forest Clearing has been explored, all four connected Nodes become eligible.

If the party then explores Monster's Den, it can still explore Goblin Camp, River, or Old Ruins because they remain connected to the explored network.

### Explored Network

The **Explored Network** determines which unexplored Nodes are currently eligible for exploration.

This system applies to the underlying Map structure regardless of whether the Map is visually represented as an Outdoor or Indoor Map.

---

# Event System

Outdoor and Indoor Maps use the same underlying Event System. Events cannot create or affect things outside of Dungeons; Dungeons are completely self-contained. For any outcome to impact the metagame world outside a dungeon, it must be tied to overall Dungeon Resolution (which is beyond the scope of this exploration doc).

An Event is a self-contained sequence of interactive **Blocks**.

Events can branch based on:

* Player choices
* Traits
* Classes
* Dungeon Flags
* Previous actions
* Event State
* Resource state
* Other conditions

Events remain self-contained. Blocks reference other Blocks within the same Event rather than jumping into another Event as part of normal control flow.

---

# Event Definition

An Event contains:

* **Name** — the Event's name.
* **Base Type** — broad classification of the Event.
* **Context Tags** — tags describing the Event's subject or nature.
* **Event Blocks** — the Blocks that make up the Event.
* **Branching Structure** — the relationships between Blocks.
* **Conditions** — determine whether interactions or branches are available.
* **Event State** — state belonging to the specific Event instance.
* **Outcomes** — state changes, rewards, Interactables, or deferral.

---

# Event State

Event State tracks information specific to an individual Event instance.

Examples include:

* Choices already made
* Actions already performed
* Temporary Event Flags
* The Block from which a deferred Event should resume

Event State is retained when an Event is deferred.

---

# Dungeon Flags

**Dungeon Flags** represent persistent state belonging to the current Dungeon.

All Dungeon Flags share a single flat **string-key dictionary** for the duration of the expedition:

```text
goblin_village_liberated = true
merchant_helped = true
goblin_chief_defeated = true
prisoners_rescued = true
```

Events can read and modify Dungeon Flags.

Events should modify Dungeon Flags rather than directly manipulating Map topology.

The Map and its Nodes respond to the resulting state.

All Dungeon Flags are discarded when the Dungeon is lost or voluntarily abandoned.

---

# Event States: Closed and Deferred

An Event has two possible terminal states:

* **Closed**
* **Deferred**

There is no separate **Completed Event** state.

## Deferred

An Event becomes **Deferred** only when an **Action Block** explicitly executes a deferral action (`deferEvent`), or when a player flees from a **Boss Encounter**.

A deferred Event:

* Remains associated with its Node.
* Retains its Event State.
* Can be resumed at any time by the player from anywhere in the explored network.
* Resumes from its designated continuation block.

## Closed

An Event is **Closed** when its flow reaches an ending condition.

An Event closes when:

* A Block has no continuation.
* A Branch Block has no valid branch.
* A roll fails and has no explicit failure linkage.
* A Combat Block resolves without a `success` connection.
* The player flees from a Skirmish or Major Combat Block (unless an explicit `flee` continuation is configured).
* Any other Block-specific rule explicitly ends the Event.

Once an Event is closed, it cannot trigger again.

---

# Interactables

An **Interactable** is a persistent gameplay element associated with a Node that can be deliberately used by the player.

Examples include:

* Transition Interactables (Stairs, Pathways, Indoor Entrances)
* Sanctuary
* Shop / Merchant
* Forge / Crafting Station
* Alchemist Station
* Shrine

Events and Interactables are distinct.

**Event**
A one-time narrative or gameplay interaction.

**Interactable**
A persistent gameplay element that can be deliberately used, potentially multiple times or until consumed.

Conceptually:

```text
Node
├── Event
└── Interactable[]
```

An Event can spawn an Interactable via an Action Block. For example:

```text
Merchant Event
      ↓
Spawn Sanctuary (or Forge)
      ↓
Interactable exists at the Event's Node
      ↓
Player can use Interactable
```

The Interactable remains available independently of the Event.

---

# Combat Encounter Scales & Flee Rules

Combat is always initiated through a Combat Block within an Event. Encounter scales are decoupled from node types (e.g., a Skirmish can occur in a Room, or a Major fight can occur in a Hallway).

## Encounter Scales

| Encounter Scale | Stamina Cost (Victory or Flee) | Description & Use Cases |
| :--- | :---: | :--- |
| **Skirmish** | **0 Stamina** | Low-difficulty skirmishes, hallway patrols, sleeping sentries, minor beasts. Drains HP/Mana but consumes no expedition endurance. |
| **Major** | **1 Stamina** | Standard/high-difficulty encounters, fortified rooms, elite garrisons, ambushes. Drains HP/Mana and consumes 1 Stamina upon completion or escape. |
| **Boss** | **1 Stamina** | Map culmination bosses and named champions. Consumes 1 Stamina upon completion or escape. |

## Combat Resolutions & Fleeing

There are only three possible resolutions for combat:

1. **Victory:** The player defeats all enemies. Advances via `success` and incurs the encounter's Stamina cost.
2. **Flee:** The player successfully escapes. Incurs the **exact same Stamina cost as Victory** (Skirmish = 0, Major = 1, Boss = 1).
   * **Skirmish / Major Combat:** Fleeing closes the Event immediately (unless an explicit `flee` branch ID is configured).
   * **Boss Combat:** Fleeing **defers the Event**, leaving the Boss Node active so the player can rest, regroup, and retry.
3. **Death:** The party is wiped or all heroes become unconscious/dead without rest resources. Triggers global party wipe and abandons the dungeon.

---

# Event Selection & Map Context

Events are selected based on the map and node structure, and are completely **dungeon-agnostic**. Any relevant properties or modifiers from the overarching Dungeon trickle down into each Map and Node during generation.

## Map and Node Context

The Dungeon, Map, and Node each contribute context that is inherited downward.

### Level

The Dungeon establishes a base **Level**. Maps and Nodes can modify that Level.

```text
Dungeon Level
      ↓
Map Level Modifier
      ↓
Node Level Modifier
      ↓
Effective Node Level
```

Events use the resulting **Effective Node Level** when determining eligibility. Events or alarms can also increment `Map Level Modifier` (+1) to represent garrison-wide alerts, scaling enemy strength and checks across remaining nodes on the current map floor.

### Tags

Each level of the hierarchy can contribute two categories of tags:

* **Faction[]**
* **Environment[]**

Tags are inherited: **Dungeon → Map → Node**

Each category is an array and can contain multiple values, modified at each level (`+`, `-`, `=`).

Events evaluate the resulting effective context rather than caring where a particular tag originated.

---

# Event Pools & Selection

Events are selected from **Event Pools** based on Map and Node configuration and context.

Event definitions and Event Groups can define Selection Weight, Level Range, Required Node Types, and Faction/Environment requirements.

### Event Groups

An **Event Group** is a generation-only construct used to ensure that interdependent Events are selected together (e.g., `Goblin Prison` and `Refugee Camp`).

Event Groups ensure both Events exist in compatible Nodes upon generation. Runtime conditions and Dungeon Flags determine their actual availability.

---

# Exploration Resources & Hero Status

Dungeons use four expedition resources:

* **Health**
* **Mana**
* **Stamina**
* **Consumables**

There is no passive HP or Mana regeneration (`H.Regen` and `M.Regen` do not exist).

## Resource Limits & Lethality

* **Mana:** Clamped to a minimum of `0`.
* **Health:** Clamped to a minimum of `0`. If a hero's Health reaches `0` at any time (via Event, Resource Change, or Combat), that hero immediately transitions to **Dead** status.

## Stamina & Exhaustion

* Every hero has a Maximum Stamina.
* Completing or fleeing a **Major** or **Boss** combat encounter consumes **1 Stamina**. Skirmish combat consumes **0 Stamina**.
* Certain powerful actions, rare exploration choices, or Events may consume Stamina.
* Stamina can fall below zero:
  * **0 to -1 Stamina:** Exhausted (Reduces AP generation, worsens random Event rolls)
  * **-2 to -3 Stamina:** Severe Exhaustion (Further reduces AP generation, worsens Event rolls)
  * **-4 Stamina:** Unconscious

## Ineligible Heroes: Dead & Unconscious

There must **always be at least one non-unconscious, non-dead hero** in the party.

Dead and Unconscious heroes:

* Cannot participate in Combat.
* Cannot become the Active Hero during Events.
* Cannot provide Traits, Class abilities, or eligibility to Event options.
* Are **excluded from all `target = all` and `target = random` resource changes or conditions**.
* Cannot lose additional Stamina (unconscious heroes remain capped at -4 Stamina).

### Hero Loss During Events

If the Active Hero or another hero dies (Health <= 0) or becomes unconscious (Stamina == -4) during an Event:

1. Select a new random eligible hero as the Active Hero immediately.
2. If there are **no remaining eligible heroes**, immediately **END the event** and force either an automatic Rest or a Party Wipe.

### Automatic Rest & Party Wipe

If **all heroes become unconscious** at any point:

1. The party automatically attempts to **Rest** if rest resources (Camp charge or Sanctuary interactable) are available.
2. Resting increases Stamina by `Max Stamina`, raising Stamina above -4 and **immediately removing the Unconscious status** for all heroes. If a hero's Stamina remains below 0 after resting, they transition to Exhausted/Severe Exhaustion.
3. If no rest resources remain, all heroes transition to **Dead** status and this counts as a **Party Wipe** (the Dungeon run fails and is abandoned).

## Rest

Rest restores Stamina equal to the hero's Maximum Stamina. Because resting increases Stamina above -4, any Rest action immediately removes the Unconscious status for all affected heroes.

---

# Camp & Sanctuary

## Camp

* Camp Charge is an expedition resource shared across the entire Dungeon (1 per normal Dungeon, 2 per large Dungeon).
* Certain events can grant **+1 Camp Charge** directly as a reward.
* Camp can be initiated anywhere in Outdoor or Indoor Maps.
* Camping consumes 1 Camp charge.
* Camping restores Stamina via standard Rest rules, restores partial HP/Mana, and clears Exhaustion if Stamina becomes positive.

## Sanctuary

* A persistent Interactable discovered or spawned at a Node.
* Single-use per Sanctuary instance.
* Restores **100% Health** and **100% Mana** to all living heroes, and performs a standard Stamina Rest action (clearing Exhaustion if Stamina rises above 0).

---

# Event Reward & Penalty System Architecture

Event Block outcomes interact with the expedition through an explicit set of allowed reward and penalty mechanisms.

## 1. Allowed Reward Mechanisms

* **Loot & Currencies:** Gold, Equipment, Consumables, and Key Items.
* **Dungeon State:** Setting Dungeon Flags (`merchant_rescued = true`) or executing **Map Reveals** (un-fogging or highlighting specific node types in the UI: `stairs`, `boss`, `sanctuary`, `enemies`).
* **Persistent Buffs:** Temporary positive status effects tracked by duration in **Steps** (node exploration) or **Battles** (combat completion). Buffs can modify combat stats or **temporarily increase Trait values (+Trait)** to raise success odds in Story Blocks.
* **Expedition Resources:** Direct Health, Mana, and Stamina recovery, or **+1 Camp Charge**.
* **Spawn Interactables:** Action Blocks can spawn persistent Interactables at the current node (`Sanctuary`, `Forge`, `Alchemist Station`, `Shop`).
* **Temporary Allies:** Recruits a temporary 5th party slot helper (duration: `end_of_map`, `end_of_dungeon`, or $N$ `battles`).

## 2. Allowed Penalty Mechanisms

* **Direct Resource Costs:** Direct Health loss, Mana drain, Gold cost, or **-1 Stamina**.
* **Persistent Curses:** Temporary negative status effects tracked by duration in **Steps** (e.g., Limping, Poison, Toxic) or **Battles** (e.g., Demoralized, Bleed, Resource Cost Inflation like +50% MP cost). Curses can also **temporarily reduce Trait values (-Trait)**, lowering success odds on Story Block choices without removing player choice eligibility.
* **Expedition Strain:** Incrementing **Map Level Modifier (+1)** to reflect garrison-wide alarm levels.

## 3. Excluded Mechanics & Design Constraints

To protect player agency and prevent system friction, the following mechanics are explicitly **BANNED**:

* **NO Equipment Durability or Item Loss:** Traps and curses never break weapons, rot rations, or destroy inventory potions.
* **NO Node or Path Locking:** The Explored Network map graph remains 100% open and readable; traps never permanently seal nodes or connections.
* **NO Trait or Choice Disabling:** Player choices in Story Blocks remain selectable; curses modify roll probabilities via Trait reductions rather than removing choices.
* **NO Passive Resource Max Modifiers:** Exploration events do not alter permanent or temporary maximum HP/Mana/Stamina caps.
* **NO Direct Refinements in Event Blocks:** Equipment or potion upgrades are handled by spawning a `Forge` or `Alchemist Station` Interactable rather than modifying items directly inside event block text.

---

# Default Exploration Actions

1. **Explore:** Reveals an eligible unexplored Node and triggers its Event (if present).
2. **Use Interactable:** Interacts with a persistent feature at the current or explored Node (Shops, Sanctuaries, Forges, Alchemist Stations, Crafting Stations, Map Transitions like Stairs/Pathways/Entrances).
3. **Camp:** Restores resources using an available Camp charge.
4. **Leave:** Exit and abandon the Dungeon run. Voluntarily abandoning a Dungeon cleans up and discards all Dungeon state, flags, and progress identical to a Party Wipe.
5. **Resume Event:** Continue a previously deferred Event at its saved state.
6. **Go to Next Map:** Dedicated macro button in the UI that appears when a transition interactable is available.
7. **Open Hero Panels:** UI management (Equipment, Stats, Skills).
8. **Use Exploration Consumable:** Use consumable items outside of combat.
9. **Cast Exploration Spell:** Cast non-combat spells.
10. **Use Exploration Ability:** Class-specific exploration mechanics.

---

# Event Blocks & Decision Previews

Events are composed of self-contained **Blocks**.

Every Block has:

```text
Block
├── id
├── type
└── config
```

Blocks reference other Blocks by ID to create the Event's flow.

---

# Story Block & Decision Previews

Handles narrative presentation and player-facing choices.

In a Story Block, **all choices whose conditions evaluate to true are displayed on screen simultaneously** as valid options for the player to select.

When hovering over an option in a Story Block, the UI renders a **Decision Preview** panel based on the option's configuration:

```json
{
  "id": "intro",
  "type": "story",
  "config": {
    "heroImage": "goblin_prisoner",
    "text": "A wounded goblin begs you for help.",
    "next": [
      {
        "text": "Attack the Guard",
        "success": "guard_combat",
        "preview": {
          "type": "combat",
          "encounterType": "skirmish",
          "staminaCost": 0,
          "details": "Low Difficulty Combat"
        }
      },
      {
        "text": "Try to slip past in the shadows",
        "resolution": {
          "type": "roll",
          "chance": { "base": 0.7 },
          "success": "bypass_success",
          "failure": "ambush_combat"
        },
        "preview": {
          "type": "risk",
          "successText": "70% Chance: Avoid Encounter (0 Stamina)",
          "failureText": "30% Chance: Ambushed (Major Combat - 1 Stamina)"
        }
      }
    ]
  }
}
```

Player-facing branches with outcomes use **`success`** when referencing positive branch outcomes. **`failure`** or **`flee`** are only used when there is an explicit roll or combat branch.

---

# Active Hero & Hero Selection

While interacting with an Event, there is always an **Active Hero** (selected randomly from eligible non-dead, non-unconscious heroes at the start of the Event).

When an interaction requires a hero based on a Condition or Roll:

1. Evaluate all non-dead, non-unconscious heroes.
2. Select the **best** eligible hero (highest relevant stat value, modified by active Buffs or Curses).
3. If tied, prefer the current Active Hero; otherwise pick randomly among tied heroes.
4. Once the interaction is used, make the selected hero the Active Hero.

---

# Resolution (Rolls)

If a choice branch involves a check, it contains a `resolution` object defining success/failure flow:

```json
{
  "resolution": {
    "type": "roll",
    "chance": {
      "base": 0.5,
      "modifiers": [
        {
          "source": "trait",
          "key": "awareness",
          "mode": "perPoint",
          "value": 0.1
        },
        {
          "source": "exhaustion",
          "mode": "flat",
          "value": -0.2
        }
      ]
    },
    "success": "hidden_pass",
    "failure": "hidden_fail"
  }
}
```

If a Roll has no explicit `failure` linkage, a failed Roll closes the Event.

---

# Branch Block

Provides non-player-facing conditional routing.

```json
{
  "id": "check_prisoners",
  "type": "branch",
  "config": {
    "next": [
      {
        "condition": {
          "type": "dungeonFlag",
          "key": "prisoners_rescued",
          "operator": "==",
          "value": true
        },
        "success": "reward"
      },
      {
        "success": "nothing"
      }
    ]
  }
}
```

---

# Combat Block

Wraps predefined or generated combat encounters. The `encounterType` explicitly determines the encounter scale and resulting Stamina cost upon completion or escape.

```json
{
  "id": "goblin_patrol",
  "type": "combat",
  "config": {
    "encounter": {
      "type": "generated",
      "encounterType": "skirmish",
      "levelModifier": 1,
      "rarity": "common"
    },
    "success": "victory_block",
    "flee": "closed_event"
  }
}
```

---

# Resource Change Block

Modifies expedition resources (Health, Mana, Stamina) positively (restoration) or negatively (damage/cost).

```json
{
  "id": "injury",
  "type": "resourceChange",
  "config": {
    "resource": "health",
    "amount": -10,
    "target": "active",
    "success": "next_block_id"
  }
}
```

### Targets

* `active`: Current Active Hero (must be non-dead, non-unconscious).
* `random`: A randomly selected eligible hero.
* `all`: All eligible non-dead, non-unconscious heroes in the party.

If a Resource Change reduces a hero's Health to `<= 0`, that hero immediately transitions to **Dead** status and forces Active Hero re-evaluation.

---

# Action Block

Executes precise state modifications:

* **Event Flag:** Modify local event state.
* **Dungeon Flag:** Modify persistent dungeon state.
* **Spawn Interactable:** Create an Interactable at the current Node (`Sanctuary`, `Forge`, `Alchemist Station`, `Shop`).
* **Map Reveal:** Highlight node types on current map UI (`stairs`, `boss`, `sanctuary`, `enemies`).
* **Apply Status / Buff / Curse:** Apply a status effect with duration in `steps` or `battles` (including Trait modifiers).
* **Map Level Modifier:** Modify `Map Level Modifier` (e.g., +1 for alerts).
* **Resource Change:** Modify resources directly (including granting **+1 Camp Charge**).
* **Recruit Ally:** Add temporary 5th party member with duration (`end_of_map`, `end_of_dungeon`, $N$ `battles`).
* **Defer Event:** Explicitly defers the Event (at specified Block ID or current block if left blank).

```json
{
  "id": "defer_choice",
  "type": "action",
  "config": {
    "actionType": "deferEvent",
    "resumeBlockId": "intro"
  }
}
```

---

# Reward Block

Hands reward definitions to the Reward System for UI presentation and item assignment.

```json
{
  "id": "reward",
  "type": "reward",
  "config": {
    "rewards": [
      { "type": "gold", "amount": 50 }
    ],
    "success": "after_reward"
  }
}
```

---

# Event Block Vocabulary

| Block | Purpose |
| :--- | :--- |
| **Story** | Narrative presentation, player choices, and choice previews |
| **Action** | State changes, spawning interactables, map reveals, applying status/curses, and event deferral |
| **Reward** | Item and loot distribution |
| **Combat** | Triggers combat encounters (Skirmish, Major, or Boss scale) |
| **Branch** | Non-player-facing conditional logic |
| **Resource Change** | Modifies Health, Mana, Stamina across eligible targets |

---

# Failure & Voluntary Abandonment

If all heroes become dead/unconscious with no remaining rest resources, or if the player chooses to Leave/Abandon the dungeon:

* The current Dungeon run is permanently lost.
* All Dungeon state, Dungeon Flags, generated Maps, and deferred Events are cleaned up and discarded.
* Unconscious heroes are converted to **Dead** status upon Party Wipe, allowing the metagame layer to increment each hero's death counter.
* Dungeons do not affect or create state outside themselves directly—outcomes external to the dungeon are handled strictly during Dungeon Resolution upon success or failure.