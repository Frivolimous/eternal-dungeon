# M3A Build Brief: Dungeon 0, hand-authored

Written 2026-10-08 from a design session with Jeremy. The Design Anchor (`docs/design/`) wins wherever this brief
disagrees. M3 is split into two parts:

- **M3A (this brief):** Dungeon 0 as a hand-authored dungeon: 2 Outdoor Maps, fixed Events, and the whole
  exploration and progression loop.
- **M3B (its own brief later):** a generated dungeon (a shortened Goblin Woods: 1 Outdoor Map, then 2 Indoor Maps),
  the generator with an event pool twice the size one run needs, Indoor Maps, and the equipment system.

## Goal

By the end of M3A, Jeremy can start a new game, play Dungeon 0 from the first Node to the Goblin Chief, and feel the
core loop: explore, choose, fight, manage Health, Mana and Stamina across fights, level up and spend skill points,
then finish or wipe. Everything saves constantly. **The checkpoint is the go/no-go: is the loop fun?** It sits at the
end of M3A, before the generator is built.

## Out of scope (later)

- **M3B:** the generator (event pools, Danger, tags, Event Groups), Indoor Maps and Hallways, the equipment system and
  inventory, the Map danger modifier (alarms).
- **M4:** Town (and respec), Tavern, Secondary and Dabble picks, other classes, exploration skills, Shops, Forges,
  temporary allies, Blessings, Boons and Trinkets, talents and races. (A minimal Alchemist Station is in M3A, see
  section 4.)
- Overland. Balance beyond "runs cleanly" (dungeon balance comes after M3).

## Anchor updates (decisions from 2026-10-08)

Write these into the Anchor first, in their sections, with superseded rows where they replace something:

1. **M3 is split into M3A and M3B** as described above. The go/no-go checkpoint is at the end of M3A. Equipment
   moves from M4 to M3B. Update `production.md`.
2. **Skill points can be spent anywhere outside combat,** in M3 and in the final game, through the Hero Panel.
   **Respec stays Town-only.** Update `classes.md` (it says points are spent in Town).
3. **Flee** (resolves the open question): see section 5.
4. **Health and Mana potions** are consumables (belt items, limited uses per dungeon). Add them to the belt table in
   `equipment.md`.
5. **Rogue › Opportunist** (resolves the open question): refunds 50% of the action's AP against a Stunned target;
   otherwise refunds the target's Speed reduction from buffs and debuffs, capped at 50% (Chill −30 Speed on a base
   100 → 30%). Stagger and Exhaustion don't count. Lower skill levels scale the cap down; exact numbers come with the
   tree's numbers.
6. **Events are JSON files** (nested blocks don't fit flat tables). Every piece of player-facing text in them is a
   readable string key in `data/strings.csv` (for example `event.goblin_prisoner.intro`), so text stays editable in
   the Sheet. Shared balance constants may be pulled out of events later.
7. **Saving is constant** (section 9).
8. **XP pacing for now:** heroes reach about Level 4 before Dungeon 0's final boss. Later tuning brings the first
   dungeon down to about Level 3.
9. **Stamina rules** (Jeremy, 2026-10-08), into `exploration.md`: **Rest can't raise Stamina above max** (so resting
   early wastes Stamina). A fight's Stamina cost is paid **after** the fight, and Exhaustion penalties use the
   Stamina a hero has when the fight starts. A full bar of 4 therefore covers 4 fights without penalty: the 4th
   starts at 1 and ends at 0 (Exhausted). Dungeon 0's budget is exactly 12 fights (4 + one Camp + one Sanctuary).
10. **Placeholders** (add to `placeholders.md`): max Stamina 4; 1 Camp charge; a Camp restores 50% Health and Mana
   and Rests; Exhausted −10 Speed and −10% to event rolls; Severe Exhaustion −25 Speed and −25%; the potion amounts
   and AP costs in section 4.

## Decisions after review (Jeremy, 2026-10-08)

These are written into the Anchor (with superseded rows), together with the Anchor updates above.

1. **No −Trait curses, ever.** Nothing lowers a trait, so a trait-gated choice a hero has once stays available. +Trait
   buffs can raise a trait, improving odds and unlocking gated choices.
2. **Hero kits at level 1:** Attack, Defend, Move, Flee and belt items. Power Attack, Stealth and Elemental Focus come
   with the first point in each tree. The Warrior keeps **Shield Bash** as a starter-shield action until equipment
   (M3B). The placeholder **Taunt** is removed.
3. **Throwing Spikes:** a Gadget belt item whose combat action is tagged Gadget, Ranged, Projectile and Physical
   (50 AP, 2 uses; the amount is a placeholder). The Bog Crocodile's "1 consumable" is a potion.
4. **A Boss after a flee:** resuming starts that Combat block again with the enemies at full strength; earlier blocks
   stay done (the Goblin Camp's beaten outer patrol stays beaten).
5. **Flee as a safety net is intended:** one hero can flee so the party avoids a wipe, at the cost of that hero's XP,
   the fight and the battle's Stamina.
6. **Fog of war:** explored Nodes show everything; eligible Nodes show position and Feature, not their Event; map
   reveals show position and Feature before a Node is eligible, plus an icon for "enemies" or "boss". If that proves
   too little, also show the Event's type.
7. **The Hero Panel** opens between Events, not in the middle of one.
8. **Unconscious heroes** are absent from battles, like dead ones.
9. Claude's additions, also in the Anchor: run-long buffs and curses (a step is one Node explored; a battle-timed one
   joins each battle for the whole fight and counts down when it ends); each battle's seed comes from the run's random
   generator; Event rolls are kept between 0% and 100% and use the rolling hero's Exhaustion; Surprised and First
   Strike are −30 and +30 Initiative; saves made with older data warn and offer a restart; Event damage in Dungeon 0
   stays small; a party pack holds spare consumables.

## Build order

M3A is several times the size of M2, so it's built in this order, with a midpoint check:

1. **Core, headless:** the run model, the dungeon and Map tables, the event engine and the 15 Events, Flee, belt items
   and run-state battles, then `sim event` and `sim dungeon`. **Midpoint check (Jeremy):** read the event text and the
   dungeon summaries, and play Events through `sim event`, so writing and pacing problems show before any screen.
2. **Progression:** XP, the three trees (Claude's proposed numbers, for Jeremy to review), masteries and traits.
3. **Saving,** with the reload-anywhere test.
4. **Screens:** exploration, the event screen, the Hero Panel and transitions, then the debug menu and screenshots.
5. **Checkpoint:** the go/no-go.

## 1. The dungeon run (Core)

A pure Core model of one expedition, driven like `BattleSession` (no Godot, deterministic, seeded):

- **State:** the dungeon and its Maps, each Map's Nodes and connections, the explored network, the Event state of
  every Node (unexplored, closed, deferred with its resume block), Dungeon flags, Interactables and whether they're
  used, the party (each hero's Health, Mana, Stamina, status: alive, Exhausted, Severe, Unconscious or Dead), Camp
  charges, consumables, Gold, XP and levels, and the current Map.
- **Actions** (exploration.md › Exploration actions, minus what's out of scope): Explore an eligible Node, Resume a
  deferred Event, Use an Interactable (Sanctuary, Pathway), Camp, Go to the next Map, Use a consumable, Spend skill
  points. Leave is hidden in Dungeon 0 (there's no Town to leave to).
- **Rules from the Anchor:** a Node becomes eligible once it touches the explored network; an Event comes before its
  Node's Interactables; a completed Map can't be re-entered; Health and Mana carry over, with no regeneration; dead
  heroes stay dead until the dungeon is completed; if all are unconscious, rest automatically if possible, otherwise
  wipe.
- **Dungeon 0 rules:** a wipe restarts the game from the beginning of Dungeon 0. Completing it shows the end screen
  (section 10).
- Every action returns a result (like `ActionResult`) that the screen animates and the log records.

## 2. Maps and Nodes (hand-authored data)

- Dungeon 0 is defined in data: the dungeon, its 2 Outdoor Maps, their Nodes (id, Node type and Feature, a string key
  for its name, a screen position, connections, its Event id if any, its Interactables) and the starting Node.
- Flat tables where they fit (maps, nodes, node connections, node interactables), so they sync with the Sheet like
  everything else. Events are the JSON exception (section 3).
- **Map 1 (8 Nodes)** and **Map 2 (10 Nodes)**, each ending in a Map boss guarding a Pathway (Map 1) or the dungeon's
  end (Map 2, the Goblin Chief). See the content manifest (section 12).

## 3. Events (Core)

The event engine from exploration.md, with these parts in M3A:

- **Blocks:** Story (text, choices, decision previews), Branch, Combat, Resource Change, Reward, and Action.
- **Action types in M3A:** set Event or Dungeon flags, map reveal, apply a buff or curse (timed in steps or
  battles, including +Trait buffs), resource changes (including +1 Camp charge and Gold), spawn a Sanctuary or an
  Alchemist Station, defer the Event. Not yet: the Map danger modifier, recruit ally, spawning Shops or Forges
  (M3B or M4).
- **Conditions:** trait level, class, Dungeon flag, Event state, a hero's or the party's resources (including Gold:
  the Wandering Herbalist defers until the party can pay).
- **Rolls:** a base chance plus modifiers (per trait point, flat Exhaustion penalties), with `success` and
  `failure` links. The roll uses the run's seeded random generator.
- **Combat blocks** start a fixed encounter by id at Skirmish, Major or Boss scale, and apply its Stamina cost on
  victory or flee. A Combat block can set an **initiative modifier**: Surprised (party −Initiative) or First Strike
  (party +Initiative), usually from a roll just before it. Multi-front Ambushed battles wait for M3B.
- A single Event can hold more than one Combat block (the Goblin Camp's patrol, then the Chief).
- **The Active Hero** and best-hero selection for conditions and rolls, as in exploration.md.
- **Closed and deferred** as in exploration.md, including fleeing a Boss deferring its Event.
- **Format:** one JSON file per Event in `data/events/`, text by string key. On load, validate every link, every
  string key and every encounter id, and report errors by file and block id, like the other data.
- **Previews never roll anything,** like battle previews.
- **To do (found while writing, 2026-10-09):** decision previews only describe fights, so outcomes that differ by
  buff, curse or reward read the same. The Overgrown Shrine's "Pray anyway" shows "50%: no fight / 50%: no fight"
  where it should say something like "50%: blessing / 50%: curse". Previews should also name the buffs, curses and
  other outcomes a branch leads to (Stamina, Gold or Camp changes, a spawned Sanctuary or Alchemist Station; the
  Herbalist's text no longer names her fee, since flavor text carries no numbers), and show when a fight is easier
  than the head-on one (the Gatekeeper's Intimidation choice fights a smaller squad, but its preview reads the same
  as attacking).

## 4. Resources and consumables

- **Stamina:** max 4 per hero. Major and Boss fights cost 1, won or fled, and Skirmishes cost 0. Exhausted from 0 to
  −1, Severe from −2 to −3, Unconscious at −4 (it can't go lower). The Speed penalty applies in battle, and the
  event-roll penalty in rolls.
- **Rest** restores Stamina equal to max Stamina, never above max. Fight costs are paid after the fight, and Exhaustion
  penalties in a fight use the Stamina at its start. **Camp:** 1 charge per dungeon, usable anywhere outside combat;
  it Rests and restores 50% Health and Mana. **Sanctuary:** single use; it restores 100% Health and Mana and Rests.
- **Consumables** are limited per dungeon. Each hero carries belt items in its belt slots (1 base, plus 1 per Belt+
  class, so the Rogue has 2). Spare consumables sit in a party pack and are moved into belts through the Hero Panel
  outside combat.
- **Potions** (new belt items):
  - Health Potion: restores 40% of max Health, 50 AP in battle. 2 uses.
  - Mana Potion: restores 40% of max Mana, 50 AP in battle. 2 uses.
  - Both can also be used outside combat, from the Hero Panel.
- **Gold** is collected and counted. In M3A it buys potions at an **Alchemist Station** (spawned by the Wandering
  Herbalist) and pays her fee. A minimal Alchemist Station: an Interactable that sells Health and Mana Potions for
  Gold (placeholder prices), usable many times. Brewing and other Alchemist features wait for later.

## 5. Combat additions

- **Flee** is a hero action (100 AP) usable from any tile: the hero leaves the battle at once.
  - Enemies whose plan targeted that hero re-plan (intent trigger 3).
  - When every hero has fled or died, the party has fled: the battle's Stamina cost applies, a Skirmish or Major
    Event closes (unless it has a `flee` link), and a Boss Event is deferred.
  - If the enemies all die while some heroes have fled, it's a victory: fled heroes rejoin, but get no XP for that
    battle.
  - Fled heroes pay the battle's Stamina cost like everyone else. Dead heroes stay dead.
- **Battles start from the run's state:** current Health and Mana, Exhaustion penalties, run-long buffs and curses, and dead and Unconscious heroes absent.
- **Using a potion** in battle is an action.
- **XP:** every hero alive (and not fled) at the end of a won battle gets an equal share.
- Heroes keep fixed **starter gear**, and the Elementalist's spells are fixed, until equipment arrives in M3B.

## 6. Progression

- **XP and levels:** a placeholder curve so heroes reach about Level 4 before the final boss. 1 skill point per level,
  including Level 1, so a new hero starts with 1 point to spend (Dungeon 0 should prompt the player to spend it).
- **Skill trees for the Warrior, Rogue and Elementalist** as in classes.md. Claude proposes the numbers for every
  level (5 per skill) as placeholders and lists them in `placeholders.md` for Jeremy to review. Prerequisites: 1+
  point in the parent skill.
- **Masteries** at 1, 6 and 11 points in the tree. Power Attack, Stealth and Elemental Focus are the first. Stealth
  replaces the Rogue's built-in placeholder (the unit no longer has it before its first point). Masteries 2 and 3
  can't be reached in Dungeon 0 (about 4 points), so test them with the debug tools.
- **Opportunist** as decided above.
- **Traits:** each hero gets its Primary class's 3 traits at level 1 (Secondary and Dabble come in M4). The trait
  system includes levels from class layers and from +Trait buffs (nothing lowers a trait).
- **Spending points:** anywhere outside combat, through the Hero Panel. There's no respec in M3A.

## 7. Screens (Godot)

- **Exploration screen:** the Outdoor Map as Regions with fog of war. Eligible Nodes are highlighted, and explored
  Nodes show their Feature and any Interactables or deferred Events. A party bar shows each hero's Health, Mana,
  Stamina and status, plus Camp charges and Gold. The actions are Explore (click a Node), Resume, Use Interactable,
  Camp, Next Map and Hero Panel.
- **Event screen:** the Active Hero's portrait, the story text, and the choices with decision previews on hover or
  focus. It shows roll results ("Awareness 1: 70% → success"), resource changes and rewards. Trait- or class-gated
  choices show which hero and trait unlocked them.
- **Hero Panel** (anywhere outside combat): stats with breakdowns (base, compounds, buffs), the skill tree with
  spending, prerequisites, masteries and their thresholds, traits with levels, and the belt and pack with potion use.
  Equipment joins it in M3B.
- **Transitions:** exploration → event → battle → back, with the result shown. Level-up and new-mastery
  announcements.
- Same rules as M2: focus-driven input (mouse, keyboard, later controller), all text from string tables, statuses
  readable without color.
- **Art:** update `docs/art-requests.md` with what M3A needs (Map backgrounds or Region art, Feature icons, event
  hero images, potion and status icons). Missing art falls back to placeholders.

## 8. Tools

- **Debug menu:** start Dungeon 0 at any Map or Node, grant XP or skill points, set resources and Stamina, reveal the
  map, open any Event directly, toggle auto-battle.
- **`sim event <id>`:** play one Event headless with given choices and seed, printing its blocks and outcome.
- **`sim dungeon dungeon_0 --seed N`** and **`--runs N`:** an auto-play policy (explore every reachable Node, a simple
  choice and spending rule, auto-battle) runs whole dungeons and prints a summary: completion rate, where wipes
  happen, Health, Mana and Stamina at each boss, potions and Camps used, levels reached. This is the attrition
  balancing tool for after M3.
- **Screenshot mode** extended to the exploration screen, event screen and Hero Panel.

## 9. Saving

- One save slot, versioned JSON in the user folder, written **after every Node, every Event block step, every Hero
  Panel change and at every battle start**.
- **Mid-battle resume:** a battle saves as its seed plus the decisions so far (the replay format), so quitting
  mid-fight and reloading replays it to the same point.
- Loading restores the exact state: same RNG position, same intents, same Event block.
- A test plays a dungeon, saves and reloads at many points, and checks the rest of the run is identical.

## 10. End of Dungeon 0

There's no Town yet, so completing Dungeon 0 shows a "slice complete" screen: time, battles, damage dealt and taken,
potions and Camps used, levels, and each hero's skills. A wipe shows a defeat screen and restarts Dungeon 0 from
the beginning.

## 11. Placeholders Claude chooses

List in `placeholders.md`: the XP curve and per-battle XP, skill numbers per level, Gold amounts, potion numbers if
changed, encounter compositions, and anything the Anchor is silent on. Ask Jeremy before inventing a mechanic.

## 12. Content manifest

The Events, their order, traits, fights and features are in **`docs/briefs/m3a-dungeon0-events.md`** (Jeremy's
outlines, 2026-10-08): 15 Events across Map 1 (8 Nodes) and Map 2 (10 Nodes), every party trait checked at least
once, two class options, four deferrable Events, two spawned Interactables (Sanctuary, Alchemist Station), every
fight at Major or Boss scale (12 Stamina if all are fought, against a budget of 4 + one Camp + one Sanctuary Node), new
wild-beast enemies, and a family story told through Dungeon flags. Claude writes the Events from those outlines.

## Acceptance criteria

- [x] The Anchor updates above are written, with superseded rows (2026-10-08).
- [ ] A new game starts Dungeon 0. Both Maps can be fully explored, and the Goblin Chief can be beaten, with the mouse
      alone and with the keyboard alone.
- [ ] Every Event block type works, including trait and class conditions, rolls with modifiers, Gold conditions,
      initiative modifiers, defer and resume, spawned Interactables (Sanctuary, Alchemist Station), and Boss flee →
      defer. All 15 Events from the manifest are playable and validate on load.
- [ ] Stamina, Exhaustion, Unconscious with auto-Rest, Camp, Sanctuary, potions and consumables follow the rules.
      The Flee rules hold, including the victory-with-fled-heroes case.
- [ ] XP, levels, spending points anywhere outside combat, prerequisites, masteries at 1, 6 and 11 and traits work.
      Heroes reach about Level 4 before the final boss in a typical run.
- [ ] Constant saving: quit and reload anywhere, including mid-battle and mid-Event, and the run continues identically
      (tested).
- [ ] A wipe restarts Dungeon 0. Completion shows the end screen.
- [ ] `sim event` and `sim dungeon` work. The dungeon summary is in `docs/sample-logs/`.
- [ ] No hard-coded text: every event string is a key in `strings.csv`. Reference screenshots of the new screens are
      in `docs/screenshots/`. The art request list is updated.
- [ ] `CLAUDE.md` and `docs/commands.md` list the new commands. `ed.bat` has them.
- [ ] **Checkpoint (go/no-go):** Jeremy plays Dungeon 0 start to finish a few times. Is the loop fun?
