# Class and exploration decisions (2026-10-07)

Jeremy wrote two design docs offline, and we reviewed them together. This file lists the corrections and
decisions to apply **on top of** them.

- `class-changes-05-10-2026.md`: changes to the class system.
- `exploration-06-10-2026.md`: the exploration and event system.

**When:** finish M2 first. Then apply these three files together: the Anchor changes, plus the small code
changes listed in section C. When done, delete all three files.

## A. Class system: corrections to `class-changes-05-10-2026.md`

1. **Mastery thresholds are 1 / 6 / 11 points** in that class's tree, not 1 / 5 / 10. A Dabble holds at most 5
   points, so it gets exactly one mastery. A Secondary reaching its third mastery (11 of its 15 possible
   points) is a deliberate sacrifice. The level cap stays at 20 (20 skill points) and may be revisited
   when XP pacing is tuned.
2. **Family bonuses still exist** (Primary+: weapon tier, Belt+: Belt slot, Spell+: Spell slot, for every
   class layer). Add them to the class definition list. Leaving them out was an omission.
3. **Superseded** (move to `superseded.md` with the reason):
   - "Each tree has 2–3 active and 2–3 passive skills" → tree skills are passives and modifiers; active
     abilities come from masteries.
   - "Class skill given free to anyone with the tree" → the class skill is now the first mastery.
   - Any "adventure bonus" or "adventure trait" ideas → replaced by ordered class traits (3 / 2 / 1 by layer)
     and the Primary-only exploration skill.
   - Skills have 5 levels. This is unchanged and confirms that the earlier 10-level design is gone.
4. **Naming** (Anchor, data, code and UI):
   - The crit stats are **C.Rate** and **C.Mult**. Never write them out as "Critical Rate" or "Critical
     Multiplier": under the TAG STAT convention, those names would mean the Rate and Multiplier stats on
     Critical-tagged actions, which is a different thing (as with Critical Resist, Critical Deval and
     Critical Pen). C.Rate replaces "Crit Rating" and still converts into a crit chance with the same formula.
   - **Pen** (or Penetrate) and **Turn**. Not "Penetration" or "Turning".
   - Class name **Elementalist** everywhere, including the trait and exploration tables.
   - Trait **Arcane** is renamed **Arcana**, so it doesn't clash with the Arcane damage type.
   - The **Cloak** status is renamed **Stealth**. The Rogue's first mastery (Stealth: Move can also enter the
     enemy area, and moving grants Stealth for 1 turn) replaces the separate Sneak action.
5. **Elemental tag.** Elemental becomes a tag in its own right: every action or effect with Fire, Electric or
   Ice also carries Elemental. The Elemental compound stat becomes the single row "Elemental Power 1". Holy,
   Dark and Toxic are not Elemental. This makes skills like Elemental Resist, Elemental Deval, Elemental Turn
   and Elemental Pen work as plain tag stats.
6. Write the class system into `classes.md` from `class-changes-05-10-2026.md` with the corrections above,
   including the full trait table and the exploration-ability design space. The exploration abilities
   themselves are marked WIP, to be developed in M4.

## B. Exploration: corrections to `exploration-06-10-2026.md`

1. **Leave vs. party wipe.** Both destroy the dungeon instance and all its state (maps, flags, deferred events).
   Both let heroes **keep all gear and XP** gained in it. The differences:
   - **Party wipe:** each hero's death counter goes up by 1, and current Blessings are removed (unchanged
     from the Anchor).
   - **Leave:** no death counter change.
   - Open question for Jeremy: does Leave keep current Blessings? (Suggested: yes, since Leave carries no
     penalty beyond losing the dungeon.)
2. **Fallen heroes** (Dead during a dungeon) revive when the dungeon is completed. This Anchor rule still holds.
   Add it to the exploration section, because the Acolyte's revive ability relies on it.
3. **H.Regen and M.Regen are removed completely**: from `stats.json`, the stat model, the buff-clock Regen
   ticks, the Anchor and the UI. Healing or Mana over time can still exist as effects (like DoT), just not
   as stats.
4. **Floors become Maps.** Maps cover both outdoor areas and indoor floors. Update `terminology.md` (Floor →
   Map, add Node, Region, Room, Hallway, Interactable, Event, Stamina, Camp, Sanctuary) and `dungeons.md`.
   The dungeon table's floor counts become map counts. The v1 floor flow (every room a battle or event,
   floor boss at the stairs, rooms of 5 / 8 / 12) moves to `superseded.md`, replaced by the node/event system.
5. Write the exploration system into a new `docs/design/exploration.md` from `exploration-06-10-2026.md`
   with these corrections, linked from the README. Mark the **Overland** section as "not designed yet".
6. Add to `open-questions.md`:
   - **Flee:** how fleeing combat works (always succeeds, a chance, an AP cost, from which tiles).
   - **Exhaustion numbers:** the penalty is a Speed reduction (Exhausted and Severe). Idea to explore: tie its
     size to the weight penalty from gear.
   - **Blessings, Boons, Trinkets and Temples:** not designed yet. They also need to be added to the reward
     mechanisms and Interactables (Temples may map to the Shrine interactable).
   - **Extended maps:** how endless play after completing a dungeon fits the map model.
   - **Overland and towns:** not designed yet, including whether there is more than one town.
   - **Leave and Blessings** (B.1).

## C. Code changes (after M2)

Keep these small, with tests:

- Rename Crit Rating → C.Rate and Crit Mult → C.Mult (ids, data, logs, UI text). Rename Cloak → Stealth.
- Remove H.Regen and M.Regen.
- Add the Elemental tag to every Fire, Electric and Ice action and effect, and simplify the Elemental compound
  stat to one row.
- Replace the Sneak action with the Stealth mastery behavior on the Rogue (Move into the enemy area and gain
  Stealth). Masteries, skill trees and traits are otherwise M3 or M4 work, not part of this change.
- Rerun the tests and the sample logs.
