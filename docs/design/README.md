# Eternal Dungeon — Design Anchor

This is the source of truth for Eternal Dungeon's design. Everything here is decided unless it's listed under Open questions or marked as a placeholder. Earlier notes and spreadsheets are retired.

- **Design Anchor (this folder):** design decisions, updated as each design point is settled.
- **Build briefs ([docs/briefs/](../briefs/)):** what to build per milestone, with acceptance criteria. Briefs are written from this Anchor, and the Anchor wins wherever they disagree.
- When a decision changes, the old version goes under Superseded with the reason, so nothing is silently lost.

The Anchor moved here from claude.ai on 2026-10-04 and is versioned with the code: a rule change and its code change land in the same commit. The claude.ai doc is retired.

## Sections

- [Terminology](terminology.md)
- [Stat system](stats.md)
- [Combat](combat.md)
- [Presentation](presentation.md): how battles look and feel on screen
- [Classes & skills](classes.md)
- [Equipment & character power](equipment.md)
- [Dungeons & adventure](dungeons.md)
- [Exploration](exploration.md): Maps, Nodes, Events, Interactables, Stamina, Camps and Sanctuaries
- [Meta progression](meta.md)
- [Event guide](event-guide.md): how to make an Event: shape, trait rolls and locks, roll sizes, endings
- [Tone guide](tone-guide.md): the voice of all player-facing text
- [Lore](lore.md): established world facts (peoples, creatures, places, figures, powers); flagged additions await review
- [Production plan](production.md)
- [Placeholders](placeholders.md): rules Claude chose where the Anchor was silent, for Jeremy to review
- [Open questions](open-questions.md)
- [Superseded decisions](superseded.md)

## Game at a glance

Eternal Dungeon is a roguelike party dungeon crawler, sold as a one-time purchase on Steam (Windows, targeting Steam Deck Verified). There are no ads or in-app purchases. It's built in Godot 4 (.NET) with C#, with Claude as the main developer. The player leads 4 heroes through 5 scripted dungeons, then into the endless Shadow World.

- **Core loop:** enter a dungeon, explore its Maps node by node (events, battles and interactables), manage Health, Mana and Stamina with Camps and Sanctuaries, beat each Map's boss and move on, collect loot and bonuses, then push deeper or leave for Town.
- **Combat:** purely turn-based, with speed deciding turn order, on small grids. The party is 4 heroes plus overflow units, up to 6 on the field.
- **Builds:** each hero combines a Primary class, a Secondary class and a Dabble skill drawn from 15 classes, with masteries unlocked by investing in a class's tree, and class traits that open options in events. Equipment, Blessings and Artifacts layer on top, all feeding a single tag-based stat system.
- **Difficulty:** there are no difficulty settings. Difficulty is simply how deep you can go. Beating a dungeon's scripted Maps completes it, and endless extended Maps follow for anyone who wants to push on.
- **Meta:** heroes keep their levels and gear between dungeons. Reincarnation (prestige) resets a hero in exchange for permanent Artifact power and a new race.
- **Steam features:** mouse and controller input (controller is needed for Steam Deck), Steam Cloud saves, achievements, and a free demo before launch for wishlists and Steam Next Fest.
- **Tech approach:** the rules (stats, tags, combat, effects, progression) live in a plain C# core library with no Godot dependency, covered by automated tests that run without the engine. Godot handles only presentation and input on top of it. Game content (classes, skills, items, enemies) is loaded from data files.
