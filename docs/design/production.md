# Production plan

Claude is the main developer and Jeremy owns design, playtesting, review and art. Milestones are defined by a playable or testable outcome and end with a checkpoint for Jeremy, not by hour estimates.

## Milestones

| Milestone | Outcome | Jeremy's checkpoint |
| --- | --- | --- |
| M0 Foundations | Godot .NET project, separate C# rules library with a test project, data-file format and loader, asset list for placeholder art, Windows and Linux builds, and a project instructions file for Claude | Install Godot and .NET, then confirm the project builds and runs |
| M1 Rules core (no graphics) | Stats, tags, combine modes, effect queue, buffs, turn order, hit, damage and procs, CC and stagger, targeting AI and the battle grid, all covered by tests. Plus a command-line battle simulator that prints combat logs | Read sample combat logs and sanity-check the numbers |
| M2 Playable battle | Godot battle screen with placeholder art: Warrior, Rogue and Elementalist vs. goblins, meter-fill animation, actions, victory and defeat | Play it and judge the feel. Pick the art style |
| M3 Dungeon 0 slice | The full first-time experience: fog-of-war Maps of Nodes, events and interactables, Stamina, Camps and Sanctuaries, Map bosses, XP and skill points, skill trees, masteries and traits for the three starting classes (Warrior, Rogue, Elementalist; the trait system with its levels from class layers, and event conditions and rolls that read traits), basic loot, leave and wipe rules, save and load | **Go/no-go: is it fun?** |
| M4 Town & build depth | Town, Tavern (talents and races), the other classes' trees, masteries and traits, Secondary and Dabble picks, exploration skills, the full equipment system, Blessings, Boons and Trinkets. Goblin Woods and Lich Tower complete | Playtest several full runs |
| M5 Content & endgame | All 15 classes, dungeons 3–5, Shadow World, extended Maps, Reincarnation and Artifacts, the economy, simulator-driven balancing | Balance and pacing passes |
| M6 Steam | Steamworks (achievements, cloud saves), controller and Steam Deck support, settings, demo build, launch or Early Access | Store page, demo, Steam Next Fest |

The battle simulator runs thousands of automated fights to check class power, Dim stacking and the power-share targets before anything is playtested.

## Parallel tracks (Jeremy)

| Track | Needed by |
| --- | --- |
| Art style decision | End of M2 |
| Steam store page and capsule art (first human-made art) | M3–M4, to start collecting wishlists |
| Economy design | M5 |
| Final in-game art | M4 onward, replacing placeholders |
| Audio | M4 onward |

## Art pipeline

- AI-generated art is used for placeholders only. All final art is made by humans.
- Every image loads through an asset list with fixed sizes and names, so final art drops in without code changes.
- Each asset is flagged if it's an AI placeholder. Before release, no flagged assets may remain, since any AI art that ships must be disclosed on the Steam page.
- Sourcing options for final art: a commissioned artist, human-made asset packs with commercial licenses, or a mix (for example, commissioned portraits and key art plus packs for environments and effects).

## Launch content targets

Placeholders, expected to change: 15 classes, 15 races, 100 enemies and 21 bosses, 5 dungeons plus Shadow World, 100 encounters, 300 equipment items, 50 enchantments, 500 crafting recipes, 50 Artifacts, 30 Blessings.
