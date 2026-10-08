# Dungeon 0 events (M3A)

Event outlines for Dungeon 0, from Jeremy's draft (2026-10-08). Claude writes the full Events from these: the choices,
the text (as keys in `strings.csv`), and the numbers (placeholders). Part of the M3A brief. Keep this file until M3A
closes.

**The story:** goblins raided a merchant family. The merchant is caged at the forest edge, his wife escapes into
goblin territory, and their son is about to go into the Goblin Chief's pot. The flags `merchant_freed`,
`wife_saved` and `son_rescued` carry the story across both Maps and change the ending.

**Party traits** (Primary only in M3A): Warrior: Military, Discipline, Intimidation. Rogue: Streetwise, Deception,
Disable. Elementalist: Arcana, Scholar, Awareness. Every one is checked at least once below.

**Combat budget (Jeremy, 2026-10-08):** every fight in Dungeon 0 is a **Major** (or Boss), costing 1 Stamina. The
party has max Stamina 4, one base Camp charge and one Sanctuary, so **12 Stamina** for the dungeon: 12 fights if it
fights everything, with no slack. Most fights can be avoided or won early through traits and choices, which is what
makes those options valuable. The Abandoned Campsite can add a second Camp charge (or a second Sanctuary). Skirmish
scale stays in the engine for M3B (Hallway Events).

**Initiative modifiers** (Surprised, First Strike) are used by several Events and are part of M3A. Multi-front
Ambushed battles wait for M3B.

## Map 1: Forest Edge (8 Nodes; no Sanctuary, so the Camp charge is the only rest unless the Campsite adds one)

| # | Node | Event | Tags | Outline | Combat | Reward | Penalty | Traits and classes checked | Features shown | Length |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 | Start | **Caged Merchant** | GOBLIN, OUTDOORS, FOREST | A wounded merchant locked in a wooden cage; goblins took his wife and son. **Warrior** (class option) tears the bars apart, but the noise brings two goblin guards back. **Disable** picks the lock quietly (roll, better per point). Or leave him for now and come back: the Gatekeeper carries the key. Freeing him sets `merchant_freed`, and he points the way. | Major (if noisy): the tutorial's first fight | Small Gold; map reveal of the Gatekeeper Node | None | Warrior (class), Disable | Class option, trait roll, **defer** and resume on a flag (`gatekeeper_key`), Dungeon flag, map reveal, first fight | Medium |
| 2 | Standard | **Bog Crocodile** | WILD_BEAST, OUTDOORS, SWAMP | A giant reptile lunges from the murk as the party crosses. **Awareness** spots the ripples in time (First Strike); otherwise the party is Surprised. | Major (forced) | Small Gold, a potion (found in its gullet) | Health loss (combat) | Awareness | Roll deciding the initiative modifier, first Major | Just combat |
| 3 | Standard | **Wolf Den Entrance** | WILD_BEAST, OUTDOORS, FOREST | A shallow cave ringed with fresh bones and snarls. **Intimidation** drives the pack off (small loot, no fight). Or go in and fight for the den's scraps, or walk away. | Major (optional) | Small Gold, consumables | Health loss (combat) | Intimidation | Trait that avoids a fight, choosing a risk | Medium |
| 4 | Standard | **Overgrown Shrine** | OUTDOORS, FOREST, RUINS | A mossy altar to a forgotten wilderness god. **Arcana** or **Scholar** reads the rite and earns the blessing safely. Without them: pray anyway (a 50/50 roll) or leave. | None | Buff: +1 Awareness for 5 steps | Curse: −25% Power for 3 battles | Arcana, Scholar | Two traits for one option, an unmodified gamble, buff and curse, +Trait | Short |
| 5 | Standard | **Muddy Crossing** | OUTDOORS, SWAMP | A deep mud pit across the trail. **Discipline** pushes through, or **Awareness** finds firmer ground, at no cost. Otherwise choose: wade through (−1 Stamina, whole party) or go around slowly (Curse: Sluggish, −Speed for 3 steps). | None | None | −1 Stamina (all) or Sluggish (3 steps) | Discipline, Awareness | Event Stamina cost, `all` target, choosing a penalty | Short |
| 6 | Standard | **Abandoned Campsite** | OUTDOORS, FOREST | A ransacked camp with scattered supplies. **Awareness** spots a hidden snare (else a random hero takes a little damage). Then pick one: pack the supplies (+1 Camp charge) **or** rekindle the fire and rest here (spawns a Sanctuary on this Node: the only Sanctuary in Map 1). Either way, scavenge potions and some Gold. | None | Consumables, Gold, and +1 Camp charge **or** a Sanctuary | Small Health loss (trap, `random`) | Awareness | **Spawn an Interactable** (alternate ending), +1 Camp charge, `random` target | Short |
| 7 | Empty | *(no Event)* | | A dried-up spring. Nothing here. | | | | | Empty Node | |
| 8 | Map boss | **Gatekeeper Garrison** | GOBLIN, OUTDOORS, RUINS | A fortified guard post with an elite goblin squad, blocking the Pathway to Map 2. **Military** spots the weak point (First Strike). **Deception** bluffs past the outer guard (First Strike plus a weaker squad). Or attack head-on. Or don't approach yet (**defer**). Winning drops `gatekeeper_key` and opens the Pathway. | Boss | Gold, the key, access to Map 2 | Health and Mana loss (combat) | Military, Deception | **Defer** before engaging, Boss flee defers, Interactable unlocked after the Event | Long |

## Map 2: Goblin Ruins (10 Nodes)

| # | Node | Event | Tags | Outline | Combat | Reward | Penalty | Traits and classes checked | Features shown | Length |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 9 | Start | **Merchant's Wife** | GOBLIN, OUTDOORS, FOREST | A woman screams for help: they've taken her son to be eaten. It's bait: raiders wait in the brush. **Awareness** sees the trap (normal start); otherwise the party is Surprised. After the fight, if `merchant_freed`, she recognises her husband's description and gives potions in thanks. Sets `wife_saved`. | Major (Surprised unless spotted) | Information, potions (if `merchant_freed`) | Health loss (combat) | Awareness | Branch on an earlier flag, Surprised modifier, story chain | Medium |
| 10 | Standard | **Goblin Tripwire** | GOBLIN, OUTDOORS, FOREST | A hidden wire rigged to spiked logs. **Awareness** spots it, then **Disable** disarms it and salvages the spikes (Throwing Spikes, a Gadget belt item). If it's missed, the logs hit a random hero and Bleed sets in, and a goblin patrol comes to check. | Major (if triggered) | Throwing Spikes (if disarmed) | Health loss (`random`), Curse: Bleed for 3 battles | Awareness, Disable | Roll chain, curse timed in battles, a fight as a failure result | Short |
| 11 | Standard | **Goblin Sentry Tower** | GOBLIN, OUTDOORS, FOREST | A crude lookout platform. **Deception** or **Streetwise** gets the party up unseen to study the camp (map reveal, no fight). Or rush the tower (fight, then the same reveal). Or ignore it. | Major (if rushed) | Map reveal (enemies and boss), small Gold | Health loss (arrows) | Deception, Streetwise | **Map reveal**, two traits for one option | Medium |
| 12 | Standard | **Poacher's Snare** | OUTDOORS, SWAMP | A rope snare dangling over the path. **Streetwise** reads the poacher's marks and finds his stash (Gold). **Disable** resets the snare for its catch. Fumble it and scavengers arrive while a purse spills into the bog. | Major (if fumbled) | Gold, small loot | Gold loss | Streetwise, Disable | Gold reward and **Gold loss** | Medium |
| 13 | Standard | **Wild Boar Charge** | WILD_BEAST, OUTDOORS, FOREST | An enraged boar guards its feeding ground. **Discipline** holds the line (First Strike). Afterwards the party roasts it: everyone recovers a little Health. | Major (forced) | Health recovery for all (rations) | Health loss (combat) | Discipline | Resource change for `all`, First Strike | Short |
| 14 | Standard | **Runed Boundary Stone** | OUTDOORS, RUINS | A carved monolith humming with warding magic. **Elementalist** (class option) draws its power: Mana restored and +Magic for 3 battles. **Arcana** attunes safely (Mana restored). **Scholar** deciphers the runes (reveals the Sanctuary). Touching it blind is a roll: Mana restored, or a Mana Drain curse. | None | Mana recovery, buff +Magic (3 battles), map reveal (Sanctuary) | Curse: Mana Drain for 2 battles | Elementalist (class), Arcana, Scholar | Second class option, three different outcomes by trait | Short |
| 15 | Standard | **Wandering Herbalist** | OUTDOORS, FOREST | A friendly hermit offers field medicine for a fee. Pay, and she sets up her kit: spawns an **Alchemist Station** (buy potions for Gold). **Scholar** helps her identify rare herbs, so the fee is waived. Not enough Gold? She'll wait (**defer**) until the party comes back with coin. | None | Spawns an Alchemist Station | Gold cost (fee) | Scholar | **Spawn an Interactable**, a Gold condition, **defer** until a condition holds, Gold sink | Short |
| 16 | Sanctuary | *(no Event)* | | A ruined chapel, still warded: a Sanctuary Interactable. | | | | | Sanctuary | |
| 17 | Empty | *(no Event)* | | A burned-out goblin camp, empty. | | | | | Empty Node | |
| 18 | Final boss | **Goblin Camp** | GOBLIN, OUTDOORS, RUINS | The barricaded fortress. Not ready yet (**defer**). **Deception** sneaks in, skipping the outer patrol. **Disable** sabotages the barricade (First Strike against the Chief). Or storm the gate: the outer patrol first, then the Chief. Inside, the boy is being shoved into a pot. Winning sets `son_rescued`, and the ending text and reward change with `merchant_freed` and `wife_saved` (the whole family reunited is the best ending). Ends the dungeon. | Major (if storming) + Boss | Gold, the ending | Health and Mana loss (combat) | Deception, Disable | **Defer**, branches on three flags, two fights in one Event, dungeon end | Long |

## Totals

- **15 Events:** 2 arrival, 11 standard and 2 bosses, plus 1 Sanctuary Node (Map 2) and 2 empty Nodes.
- **Fights if everything is fought:** 10 Majors and 2 Bosses = 12 Stamina, exactly the budget (4 + one Camp + one
  Sanctuary). Only 3 are forced (Bog Crocodile, Wild Boar and the Goblin Chief); the Gatekeeper is needed to reach
  Map 2. The rest happen by choice or on failure, so a party that uses its traits well arrives at the Chief with
  Stamina to spare.
- **Traits checked:** Awareness 5, Disable 4, Scholar 3, Deception 3, Military 1, Discipline 2, Intimidation 1,
  Streetwise 2, Arcana 2. **Classes:** Warrior, Elementalist.
- **Deferred Events:** Caged Merchant, Gatekeeper Garrison, Wandering Herbalist and Goblin Camp.
- **Spawned Interactables:** Sanctuary (Abandoned Campsite) and Alchemist Station (Wandering Herbalist).

## New enemies and encounters

- **Wild beasts** (new WILD_BEAST faction): Bog Crocodile (size 1.5), Wolf (a pack of 3), Wild Boar.
- **Goblins:** goblin guards (2), raiders (the wife's ambush), a patrol, a lookout, scavengers, the Gatekeeper's
  elite squad (a goblin captain plus grunts and an archer), the camp's outer patrol, and the Goblin Chief with his
  guard.
- Reuse today's goblin units where they fit, and add new ones as needed (placeholder numbers).
