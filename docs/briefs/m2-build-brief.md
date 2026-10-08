# M2 Build Brief: Playable battle

Written 2026-10-04 from a design session with Jeremy, revised the same day after Claude's review, and updated
2026-10-08 for the class and exploration decisions (see the next section). The Design Anchor (`docs/design/`) wins
wherever this brief disagrees. Prerequisites (the placeholder review and the M1 log review) are done.

## Decisions since the brief (2026-10-07)

The class and exploration decisions changed some of what M2 shows. All of this is built and in the Anchor:

- **Crit stats are C.Rate and C.Mult** (never written out), in data, logs and the details panel.
- **No H.Regen or M.Regen.** Health and Mana don't regenerate in battle (healing or Mana over time exists only as
  effects), and they will carry over through a dungeon. Casters can now run out of Mana mid-fight: the Elementalist
  has 120 Mana (placeholder) and the Goblin Shaman stops casting once dry.
- **AI fallback:** a unit with no usable skill (no target, or no Mana) Attacks if it can, else steps toward the front,
  else Defends.
- **Elemental tag:** Fire, Electric and Ice actions and procs also carry Elemental, and the Elemental compound stat
  is gone: the Elementalist has Elemental Power 10 directly (2026-10-08). The log's damage kinds are unchanged ("Arcane, Fire").
- **The Rogue's Stealth mastery replaces Sneak:** the Rogue's Move (50 AP) reaches a neighbouring tile or any empty
  enemy tile, and every Move grants **Stealth** (the renamed Cloak). There is no separate Sneak action; the Rogue's
  action bar shows Attack, Dagger Attack, Defend and Move. Masteries, skill trees and traits are otherwise M3 work.
- **Floors are now Maps** (exploration.md), which only affects the out-of-scope list here.

## Goal

By the end of M2, Jeremy can launch the game, pick an encounter and a seed, and play a full battle with the
mouse: Warrior, Rogue and Elementalist against goblins, on a 3D card table, with placeholder art in candidate
styles. The checkpoint is about **feel**: is a battle readable and satisfying, and which art style wins?

## In scope

- A front-based board model in Core (section 1).
- The data reorganized into flat tables, ready for the Google Sheets sync (section 2).
- The battle screen: board, cards, timeline, details panel, log and action bar (sections 3–5).
- The hero turn flow, previews and feedback (sections 6–7).
- Input, resolution, UI text and accessibility basics (section 8).
- Tools: debug menu, replays, auto-battle, screenshot mode and art request list (section 9).

## Out of scope

- The Google Sheets sync itself: a small separate task right after M2, built on section 2.
- Multi-front layouts (Ambushed, Surrounding) and encounter modifiers: M3.
- Dungeons, Maps and exploration, XP, loot and saving: M3.
- Skill trees, masteries (beyond the Rogue's built-in Stealth) and traits: M3 for the three starting classes.
- Audio: a separate pass once the art style is chosen.
- Full controller polish: M6. M2 must not rule it out (section 8).
- Damage variance: damage stays static (no damage roll). Only hit, crit and Brutal are random.

## Anchor updates

Write the presentation decisions below into the Anchor, in a new `docs/design/presentation.md` linked from the
README, and update `combat.md` › Battlefield for areas and fronts. Anything Claude chooses beyond this brief
goes into `placeholders.md`.

## 1. Areas and fronts (Core), layout (presentation)

The battlefield is a set of **areas** connected by **fronts**, not two fixed 3×2 grids.

- **Core** knows only logic: each area has a side (party or enemy) and a size (columns × rows), and tiles are in
  front-relative coordinates (row 0 is the front). A **front** links an edge of one area to an edge of an opposing
  area. "Front row", melee reach, Push/Pull and the forward collapse are all defined relative to a front.
- **Presentation** decides where each area sits on the table and which way it faces. Encounters give their layout
  in data (Godot reads it; Core ignores it). Default: **vertical**, party at the bottom, enemies at the top, front
  edges facing each other. Side-on: party left, enemies right.
- Unit footprints (1, 2 or 4 tiles) are front-relative, so they work in any layout.
- M2 supports **one front per area**. Multiple fronts (enemies on two sides) are M3, but the model must not assume
  a single front in a way that blocks them: with several fronts, a tile's row becomes its depth from a given front.
- Tests: Core's existing range, Push/Pull and collapse tests run on the front model. On the Godot side, the
  tile ↔ screen mapping round-trips in both layouts, and the same replay (section 9) gives the same log in the
  vertical and side-on layouts.

## 2. Data as flat tables

Every data table must be exportable to a spreadsheet tab and importable back exactly. **JSON stays the source of
truth** (it diffs well in git); TSV is the exchange format.

- Each file is a list of flat records (one row each). Nested lists become **child tables** keyed by the parent
  id and an order column, for example `actions` + `action_effects`, `units` + `unit_actions`,
  `encounters` + `encounter_units` (with position), and `effects` / `procs` with their own child tables for
  stat changes and results.
- Units keep one column per untagged stat and one per compound stat. **Tag-keyed stats** (Fire Power 50) go in a
  child table `unit_tag_stats`: unit, stat, tag, value, one row each. The same pattern applies anywhere else a
  record carries tagged stats.
- `defaults.json` is a single record, so it becomes a key/value table.
- **No comments in JSON.** The `//` comments move out: design notes go to the Anchor, and per-row notes go in a
  `note` column where a table needs one.
- Data files are always written by **one canonical writer** (fixed key order, number format and indentation),
  so files round-trip byte for byte.
- Field names are stable and spreadsheet-friendly (they become column headers).
- TSV conventions: numbers with a `.` decimal point regardless of locale, booleans as `TRUE` / `FALSE` (as Sheets
  writes them), enums as their names, an empty cell means the field's default.
- Add the **TSV export and import** now: `sim export-tsv <dir>` and `sim import-tsv <dir>`, one TSV per table.
  Import runs the full strict validation, prints a change summary ("Goblin Grunt: health 85 → 95"), and writes
  nothing if anything is invalid. Columns starting with `_` and unknown files are ignored, so Jeremy can keep
  calculations alongside the data.
- Test: exporting then importing reproduces the data files byte for byte.

## 3. The table and cards

- The battlefield is a **3D scene seen from above**: a table with each unit as a flat 2D card. All "3D" is card
  transforms: lift, tilt, flip, shake, shadow. The camera is orthographic or nearly so, so card text stays sharp.
- **Images are portraits only.** No text is ever baked into art: names, numbers, bars and icons are drawn by the
  game from engine data.
- **Card face:** portrait, name, HP bar with numbers, Shield, the Act meter along one edge, the stagger bar
  (white while breaking) and status icons.
- Card size follows footprint, and the portrait follows its shape: **square** for size 1 (one tile) and size 2
  (a 2×2 block), **1:2** for size 1.5 (two tiles, front and back). In a layout where a Tall unit's two tiles run
  across the screen, only the **portrait image is rotated** 90° to fit; the card frame and its text stay upright.
- **Portrait states:** each unit can have optional portraits for states such as low HP, hurt, attacking,
  casting and knocked out. Only `default` is required, and a missing state falls back to it. States may later be
  animations instead of stills, with no code changes. Add this to the asset manifest format.
- **Death:** the card flips face down and stays on the board.

## 4. Screen layout

Designed at **1280×800** (Steam Deck's native 16:10) and scaled up for larger screens. 16:9 adds side space.

- **Center:** the board (vertical default: enemies top, party bottom).
- **Left edge:** the turn-order timeline, upcoming turns as small portraits. Portraits slide into their new
  order after each action.
- **Right edge:** the details panel for the selected or hovered unit (stats, buffs, resistances) above a
  scrolling combat log.
- **Bottom:** the action bar for the active hero.
- The camera frames whatever layout the encounter uses.
- Cards are small on a 7" screen (about 110 px wide for a size-1 card). Check readability with screenshot mode
  early, before polishing.

## 5. Action bar

- Actions are **buttons styled as small card frames**: icon, name, AP cost, Mana cost and cast time. They're
  not a hand of cards. The default actions (Attack, Defend, Move) are included; where a unit has its own version of
  one (the Rogue's Move), it takes the default's place.
- Unusable actions (no Mana, Silenced, Rooted, Feared, no valid target) are shown disabled with the reason on
  hover.

## 6. A hero's turn

1. The active hero's card lifts and glows, and the action bar shows its actions.
2. Hovering an action shows a **ghost marker on the timeline** where the hero's next turn would land, given that
   action's AP cost and the hero's current Speed. For a cast it shows two points: when the cast completes and when
   the next turn lands. It's an estimate: a buff expiring can move it.
3. Picking an action highlights valid targets (or empty tiles for Move; the Rogue's Move also lights the enemy area's
   empty tiles). Hovering a target shows the **preview**:
   the hit chance as a percentage, the damage on a normal hit, a crit and a Brutal crit, each with its chance, and
   any effects it would apply. Procs aren't included in the numbers; procs that could trigger are listed with their
   chance.
4. Click to confirm. Right-click or Escape steps back.
5. Enemy turns play automatically.

**Heroes the player can't fully control:**

- Stunned or Sleeping: the turn is skipped, with a short visible beat so the player sees why.
- Feared: the action bar allows only Defend and Move away from the front; everything else is disabled with the
  reason.
- Confused: the player picks the action; the target is chosen at random after confirming, and the preview says so.

The combat log uses the same text as the simulator's brief log, and shows every damage tag (for example
"Arcane, Fire").

**The UI never uses the battle's random generator.** Previews and the ghost marker call pure functions only, and
a test checks that previewing changes nothing (the battle plays identically with and without previews).

## 7. Feedback (placeholder quality)

- **Attack:** the attacker's card lunges toward the target and snaps back, and the target shakes and tilts.
- **Damage numbers** float up from the target: bigger for crits, a distinct style for Brutal. A miss shows
  "Miss" and the target sidesteps.
- **Casting:** a progress ring on the caster's card, then a simple effect travels to the target.
- **Statuses:** icons on the card edge. Stun tilts the card, and a stagger break flashes the bar white.
- **Pacing:** about 0.5 s per enemy action at 1×. Speed settings: 1×, 2× and near-instant, plus click to skip the
  current animation.

## 8. Input, text and accessibility

- **Mouse first**, with keyboard shortcuts: 1–9 pick an action, Tab cycles targets, Space or Enter confirms,
  Escape cancels, and one key cycles battle speed.
- **Built on focus navigation**, so a controller can drive everything later by mapping buttons (D-pad moves
  focus, A confirms, B cancels, bumpers switch actions). Nothing may depend on mouse hover alone: every hover
  preview must also show when the element has focus.
- **UI text** lives in string tables from day one, never typed directly in scenes or code. One `strings.csv` in
  Godot's CSV translation format: Godot imports it, and Core reads the same file to format the combat log, so the
  simulator and the game print identical lines. Content names (units, actions, effects) stay in the data tables
  and are translated there later.
- **Accessibility:** statuses use shape plus color, never color alone. Add a text-size setting.

## 9. Tools

- **Debug menu:** the start screen lists encounters, a seed field, and Start. After a battle: Restart (same
  seed), Next seed, Save replay, and Back to menu.
- **Replays:** a battle is fully determined by its seed and the choices made, so a replay is a small file: the
  encounter, the seed and the list of choices. The game can save one after a battle and load one from the debug
  menu, and `sim replay <file>` plays it in the simulator. Uses: proving the UI matches the simulator, attaching
  to bug reports, and driving screenshot mode.
- **Auto-battle toggle:** heroes act on the simulator's scripted AI, so whole fights can be watched.
- **Screenshot mode:** a command-line option that loads an encounter and seed (or a replay), plays a given number
  of actions at instant speed (heroes on auto-battle unless a replay drives them), saves a PNG of the screen and
  quits. It needs a real window (Godot's headless mode doesn't render), so a window flashes open briefly. Use it
  to check UI work visually, and keep a few reference screenshots in `docs/screenshots/`.
- **Art request list:** `sim art-requests` generates `docs/art-requests.md` from the data and the manifest, so it
  never goes stale. It lists every image M2 needs, with its manifest id, exact pixel size and a one-line
  description: portraits for each unit (square, or 1:2 for Tall), portrait states (optional), the card frame,
  action icons and status icons. Card pixel sizes are fixed at the 1280×800 reference layout, and art is drawn at
  **2× display size** so it stays sharp on larger screens. Jeremy makes the images (AI placeholders, flagged in
  the manifest) in 2–3 candidate styles.
- **Style switch:** the manifest lists images by id, and each style is a folder holding the same ids at the same
  sizes. The debug menu switches styles, so they can be compared in the same fight. `sim assets` checks every
  style folder.

## Acceptance criteria

- [x] `dotnet test` passes, including the front-model board tests, the export-then-import byte-for-byte round
      trip, and the previews-change-nothing test.
- [x] `sim export-tsv` / `sim import-tsv` work, with a change summary and nothing written on invalid data.
- [x] The game launches to the debug menu. Every encounter, including the showcase encounter, can be played to
      victory or defeat with the mouse alone, and with the keyboard alone.
- [x] The timeline, ghost marker, target previews, details panel and log all show correct values: a replay saved
      from the game gives the same log in `sim replay`.
- [x] Hero deaths flip face down. Speed settings and skip work. Auto-battle completes fights.
- [x] One encounter also runs in a side-on layout and plays correctly (same replay, same log).
- [x] Screenshot mode produces PNGs, and reference screenshots are in `docs/screenshots/`.
- [x] `sim art-requests` writes `docs/art-requests.md`, and the game runs with whatever art is present (missing
      art falls back to a generated placeholder card, never a crash).
- [x] No UI text is hard-coded, and no text is baked into images. Statuses are readable without color.
- [x] The Anchor has `presentation.md` and an updated Battlefield section, and `CLAUDE.md` lists any new commands.
- [x] The 2026-10-07 decisions show in the game: C.Rate and C.Mult in the details panel, no regeneration, casters
      that run dry fall back on Attack, and the Rogue's Move enters the enemy area with Stealth (no Sneak button).
- [x] **Checkpoint:** Jeremy plays several fights, judges the feel, and picks an art style. Also worth judging:
      whether fights feel right with finite Mana (Elementalist 120), and whether the Rogue's 50-AP Stealth Move is
      too cheap compared with the old 100-AP Sneak.

**Checkpoint, 2026-10-08 (Jeremy):** M2 is closed. Mouse and keyboard play both work. Finite Mana and the Rogue's
50-AP Stealth Move are fine. Intents, the taunt and the reworked stagger feel good. Animation pace is fine for now
(revisit with visual polish; a 0.5× speed was added). The Ink style stays for now. Fight balance is deliberately left
alone: M3 balances for attrition across a dungeon and the dungeon's win rate, not single fights. Carried forward,
to do later: a review of the combat log, the information on action hover and selection, and the information screens;
testing hero portrait state art; all non-portrait art.
