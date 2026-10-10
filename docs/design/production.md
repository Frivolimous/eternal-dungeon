# Production plan

Claude is the main developer and Jeremy owns design, playtesting, review and art. Milestones are defined by a playable or testable outcome and end with a checkpoint for Jeremy, not by hour estimates.

## Milestones

| Milestone | Outcome | Jeremy's checkpoint |
| --- | --- | --- |
| M0 Foundations | Godot .NET project, separate C# rules library with a test project, data-file format and loader, asset list for placeholder art, Windows and Linux builds, and a project instructions file for Claude | Install Godot and .NET, then confirm the project builds and runs |
| M1 Rules core (no graphics) | Stats, tags, combine modes, effect queue, buffs, turn order, hit, damage and procs, CC and stagger, targeting AI and the battle grid, all covered by tests. Plus a command-line battle simulator that prints combat logs | Read sample combat logs and sanity-check the numbers |
| M2 Playable battle | Godot battle screen with placeholder art: Warrior, Rogue and Elementalist vs. goblins, meter-fill animation, actions, victory and defeat | Play it and judge the feel. Pick the art style |
| M3A Dungeon 0, hand-authored | Dungeon 0 as a hand-authored dungeon: 2 Outdoor Maps of Nodes under fog of war, 15 fixed Events and their Interactables, Stamina, Camps and Sanctuaries, Map bosses, potions and a belt, Flee, XP and skill points, skill trees, masteries and traits for the three starting classes (traits with levels from class layers; event conditions and rolls that read them), wipe rules, constant saving. Build brief: `docs/briefs/m3a-build-brief.md` | **Go/no-go: is the loop fun?** |
| M3B Generated dungeon | A shortened Goblin Woods (1 Outdoor Map, then 2 Indoor Maps) built by the generator: event pools (twice what one run needs), Danger, tags and Event Groups, Indoor Maps and Hallways, the Map danger modifier (alarms), and the equipment system with basic loot | Play generated runs |
| M4 Town & build depth | Town (and respec), Tavern (talents and races), the other classes' trees, masteries and traits, Secondary and Dabble picks, exploration skills, Shops and Forges, temporary allies, Blessings, Boons and Trinkets. Goblin Woods and Lich Tower complete | Playtest several full runs |
| M5 Content & endgame | All 15 classes, dungeons 3–5, Shadow World, extended Maps, Reincarnation and Artifacts, the economy, simulator-driven balancing | Balance and pacing passes |
| M6 Steam | Steamworks (achievements, cloud saves), controller and Steam Deck support, settings, demo build, launch or Early Access | Store page, demo, Steam Next Fest |

## Status

- **M0, M1:** done.
- **M2:** closed at Jeremy's checkpoint, 2026-10-08 (see the end of the M2 brief). Ink is the art style for now. Carried
  forward, not blocking M3: a review of the combat log, action hover and selection info and the information screens;
  hero portrait state art; all non-portrait art.
- **Balance:** single fights are not balanced on their own. From M3, fights are balanced for attrition between battles
  and for a dungeon's win rate (Jeremy, 2026-10-08).
- **M3:** split into M3A (Dungeon 0, hand-authored, with the go/no-go checkpoint) and M3B (the generator, Indoor Maps and equipment, which moved here from M4), decided 2026-10-08. The M3A brief is written. Step 1 (Core, headless: the run model, the event engine, the 15 Events, Flee, belt items, run-state battles, `sim event` and `sim dungeon`) is built (2026-10-09; midpoint check done). Step 2 (progression: XP and levels, the three trees with Claude's proposed numbers, masteries, skill effects) is built (2026-10-10), its numbers for Jeremy to review in Placeholders › Progression.

The battle simulator runs thousands of automated fights to check class power, Dim stacking and the power-share targets before anything is playtested.

## Parallel tracks (Jeremy)

| Track | Needed by |
| --- | --- |
| Art style decision | End of M2: Ink for now (2026-10-08) |
| Steam store page and capsule art (first human-made art) | M3–M4, to start collecting wishlists |
| Economy design | M5 |
| Final in-game art | M4 onward, replacing placeholders |
| Audio | M4 onward |

## Content editing

Content lives in data/ as flat JSON tables, the source of truth (it diffs well in git). Jeremy edits it in the
content Google Sheet, one tab per table plus the game's text. `sim pull-sheets` and `sim push-sheets` sync the two,
with validation and a change summary on every pull. Columns starting with `_` hold calculations and are never
synced. The sync never silently overwrites edits made on the other side. The sheet is reached through an Apps
Script web app rather than a service account: simpler, and its URL is the only key (fine for now; revisit if the
repo or URL ever becomes public).

## Art pipeline

- AI-generated art is used for placeholders only. All final art is made by humans.
- Every image loads through an asset list with fixed sizes and names, so final art drops in without code changes.
- Each asset is flagged if it's an AI placeholder. Before release, no flagged assets may remain, since any AI art that ships must be disclosed on the Steam page.
- Sourcing options for final art: a commissioned artist, human-made asset packs with commercial licenses, or a mix (for example, commissioned portraits and key art plus packs for environments and effects).

## Launch content targets

Placeholders, expected to change: 15 classes, 15 races, 100 enemies and 21 bosses, 5 dungeons plus Shadow World, 100 encounters, 300 equipment items, 50 enchantments, 500 crafting recipes, 50 Artifacts, 30 Blessings.
