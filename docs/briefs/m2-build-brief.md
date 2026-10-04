# M2 Build Brief: Playable battle

Written 2026-10-04 from a design session with Jeremy. The Design Anchor (`docs/design/`) wins wherever this
brief disagrees. Prerequisite: `placeholder-review-2026-10-04.md` is done (default actions, cast and DoT
changes, showcase encounter).

## Goal

By the end of M2, Jeremy can launch the game, pick an encounter and a seed, and play a full battle with the
mouse: Warrior, Rogue and Elementalist against goblins, on a 3D card table, with placeholder art in candidate
styles. The checkpoint is about **feel**: is a battle readable and satisfying, and which art style wins?

## In scope

- A flexible board model in Core (section 1).
- The data reorganized into flat tables, ready for the Google Sheets sync (section 2).
- The battle screen: board, cards, timeline, details panel, log and action bar (sections 3–5).
- The hero turn flow, previews and feedback (sections 6–7).
- Input, resolution, UI text and accessibility basics (section 8).
- Tools: debug menu, auto-battle, screenshot mode and art request list (section 9).

## Out of scope

- The Google Sheets sync itself: a small separate task right after M2, built on section 2.
- Multi-front layouts (Ambushed, Surrounding) and encounter modifiers: M3.
- Dungeons, floors, XP, loot and saving: M3.
- Audio: a separate pass once the art style is chosen.
- Full controller polish: M6. M2 must not rule it out (section 8).

## Anchor updates

Write the presentation decisions below into the Anchor, in a new `docs/design/presentation.md` linked from the
README, and update `combat.md` › Battlefield for the flexible board. Anything Claude chooses beyond this brief
goes into `placeholders.md`.

## 1. Flexible board (Core)

The battlefield is a set of **areas placed on a shared table**, not two fixed 3×2 grids.

- Each area has a side (party or enemy), a size (columns × rows), a position on the table and an orientation.
  Its **front edge** is the edge that faces the opposing area.
- "Front row", melee reach, Push/Pull and the forward collapse are all defined relative to the front edge, so
  they work in any orientation.
- Unit footprints (1, 2 or 4 tiles) keep working in any orientation.
- Encounters specify their layout in data. Default: **vertical**, with the party area at the bottom and the
  enemy area at the top, front edges facing each other.
- M2 supports **one front per area**. Multiple fronts (enemies on two sides) are M3, but the model must not
  assume a single front in a way that blocks them.
- Tests: the same fight gives identical results in the vertical layout and in a rotated side-on layout
  (party left, enemies right). Range, Push/Pull and collapse tests run in both orientations.

## 2. Data as flat tables

Every data table must be exportable to a spreadsheet tab and importable back exactly. Reorganize the JSON now
so each file maps cleanly to flat tables:

- Each file is a list of flat records (one row each). Nested lists become **child tables** keyed by the parent
  id and an order column, for example `actions` + `action_effects`, `units` + `unit_actions`,
  `encounters` + `encounter_units` (with position), and `effects` / `procs` with their own child tables for
  stat changes and results.
- Units keep one column per stat.
- Field names are stable and spreadsheet-friendly (they become column headers).
- Add the **TSV export and import** now: `sim export-tsv <dir>` and `sim import-tsv <dir>`, one TSV per table.
  Import runs the full strict validation, prints a change summary ("Goblin Grunt: health 85 → 95"), and writes
  nothing if anything is invalid. Columns starting with `_` and unknown files are ignored, so Jeremy can keep
  calculations alongside the data.
- Test: exporting then importing reproduces the data files exactly.

## 3. The table and cards

- The battlefield is a **3D scene seen from above** (slight perspective is fine): a table with each unit as a
  flat 2D card. All "3D" is card transforms: lift, tilt, flip, shake, shadow.
- **Card face:** portrait, name, HP bar with numbers, Shield, the Act meter along one edge, the stagger bar
  (white while breaking) and status icons.
- Card size follows footprint: a single card for size 1, a double-height card covering 2 tiles for size 1.5, and
  a large card covering a 2×2 block for size 2.
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

## 5. Action bar

- Actions are **buttons styled as small card frames**: icon, name, AP cost, Mana cost and cast time. They're
  not a hand of cards.
- Unusable actions (no Mana, Silenced, no valid target) are shown disabled with the reason on hover.

## 6. A hero's turn

1. The active hero's card lifts and glows, and the action bar shows its actions.
2. Hovering an action shows a **ghost marker on the timeline** where the hero's next turn would land, given that
   action's AP cost and any cast time.
3. Picking an action highlights valid targets (or empty tiles for Move). Hovering a target shows the hit chance,
   the damage range, the crit and Brutal chances, and any effects it would apply.
4. Click to confirm. Right-click or Escape steps back.
5. Enemy turns play automatically.

The combat log uses the same text as the simulator's brief log, and shows every damage tag (for example
"Arcane, Fire").

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
- **UI text** lives in string tables (Godot's translation system) from day one, never typed directly in scenes
  or code.
- **Accessibility:** statuses use shape plus color, never color alone. Add a text-size setting.

## 9. Tools

- **Debug menu:** the start screen lists encounters, a seed field, and Start. After a battle: Restart (same
  seed), Next seed, and Back to menu.
- **Auto-battle toggle:** heroes act on the simulator's scripted AI, so whole fights can be watched.
- **Screenshot mode:** a command-line option that loads an encounter and seed, plays a given number of actions
  at instant speed, saves a PNG of the screen and quits. Use it to check UI work visually, and keep a few
  reference screenshots in `docs/screenshots/`.
- **Art request list:** generate `docs/art-requests.md` listing every image M2 needs, with its manifest id, exact
  pixel size and a one-line description: portraits for each unit and card size, portrait states (optional), the
  card frame, action icons and status icons. Art is drawn at **2× display size** so it stays sharp on larger
  screens. Jeremy makes the images (AI placeholders, flagged in the manifest) in 2–3 candidate styles.
- **Style switch:** the debug menu can switch between the candidate art styles, so they can be compared in the
  same fight. Each style is a folder of images with the same ids and sizes.

## Acceptance criteria

- [ ] `dotnet test` passes, including board tests in both orientations and the export-then-import round trip.
- [ ] `sim export-tsv` / `sim import-tsv` work, with a change summary and nothing written on invalid data.
- [ ] The game launches to the debug menu. Every encounter, including the showcase encounter, can be played to
      victory or defeat with the mouse alone, and with the keyboard alone.
- [ ] The timeline, ghost marker, target previews, details panel and log all show correct values (the same as
      the simulator for the same seed and choices).
- [ ] Hero deaths flip face down. Speed settings and skip work. Auto-battle completes fights.
- [ ] One encounter also runs in a side-on layout and plays correctly.
- [ ] Screenshot mode produces PNGs, and reference screenshots are in `docs/screenshots/`.
- [ ] `docs/art-requests.md` exists, and the game runs with whatever art is present (missing art falls back to a
      generated placeholder card, never a crash).
- [ ] No UI text is hard-coded. Statuses are readable without color.
- [ ] The Anchor has `presentation.md` and an updated Battlefield section, and `CLAUDE.md` lists any new commands.
- [ ] **Checkpoint:** Jeremy plays several fights, judges the feel, and picks an art style.
