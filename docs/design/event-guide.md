# Event guide

How to make an Event: its shape, its choices, its rolls and how it ends. The rules engine is in
[Exploration](exploration.md) › Events, the words in the [tone guide](tone-guide.md), the world in [lore](lore.md), and
the file format in `data/README.md` › Events. This page is the checklist that joins them (gathered 2026-10-10; the
roll and lock rules moved here from Classes).

## Shape

- **Default shape:** a short setup, then one open choice everyone can take (usually a roll), one locked specialist
  option (a trait or class), and, where it makes sense, a way to leave or come back later.
- **Order the choices** usually with the specialist options first, then the plain way in, then leaving or
  deferring. It's a habit, not a rule: change the order when the story reads better another way (Jeremy, 2026-10-10).
- **Who writes what:** Jeremy and the narrative agent own Event writing and every revision of it. A development agent
  may write a first pass of a new Event, then leaves rewrites to them (Jeremy, 2026-10-10).
- **One trait, one job.** A trait choice uses the trait for what it opens ([Classes](classes.md) › What each trait
  opens). When two could fit, use the more specific one; Awareness is the fallback, not the default.
- **Every choice must say what it does and roughly what it risks** in its words (tone guide › Player clarity). The
  decision preview adds the numbers: the chance, what happens on the way (Health, Stamina, buffs and curses, Gold,
  Camp, items, a spawned Interactable, a map reveal) and any fight with its scale, Stamina and enemy count. So the
  prose never needs numbers.

## Traits: roll or lock

- **Roll by default.** If an untrained hero could plausibly try it (look around, climb, sneak, bluff, scare, hold
  their nerve), the choice is open to everyone and rolls, and the trait adds to the chance per level. Awareness
  especially: anyone can spot something, the trained spot it more easily.
- **Lock when training is needed even to try:** reading old runes (Scholar), channeling magic (Arcana), a rite
  (Spiritual), picking a lock (Disable), class options. A locked choice can still roll on its trait.
- **Gate at a higher level only for a different or better choice,** not just better odds (Awareness 1 avoids being
  Surprised; Awareness 2 turns the ambush into First Strike). Common traits (Awareness, Discipline, Scholar) can use
  level 2 as their usual threshold; rare ones stay at level 1.
- **No disabled choices:** a choice whose conditions don't hold is simply not shown, and nothing ever lowers a trait.

## Rolls

A roll's chance is its **base, by difficulty, plus 20% per level** the rolling hero has in the trait (its best one if
the roll lists several), plus that hero's Exhaustion penalty, kept between 0% and 100%. The best-placed hero rolls.

| Difficulty | Base | Level 0 | Level 1 | Level 2 | Level 3 |
| --- | --- | --- | --- | --- | --- |
| Easy | 50% | 50% | 70% | 90% | 100% |
| Normal | 30% | 30% | 50% | 70% | 90% |
| Hard | 10% | 10% | 30% | 50% | 70% |
| A gamble (no trait) | 50% | | | | |

In data: `"roll": { "base": 0.3, "trait": "awareness", "per_point": 0.2 }`. Exhaustion takes 10% (Exhausted) or 25%
(Severe) off. All of these are placeholders until dungeon balance (after M3).

- **Easy** for the specialist doing their job (a Rogue picking a simple lock) or for a mild risk.
- **Normal** for an open roll anyone might pass, where the trait is a real edge (spotting an ambush).
- **Hard** for a long shot, where even a trained hero often fails.

## Failure and endings

- **Failure leads somewhere** worth reading: a fight, a cost, a curse or a joke. More players will fail more rolls
  than in a game with locks everywhere.
- **Every ending is either closed or deferred.** Defer (and say so in the choice: "come back later") when the party
  may want to return better prepared or richer. Fleeing a Boss defers its Event on its own.
- **An unimportant fight can end the Event in silence:** a Combat block with no `success` link closes the Event when
  the party wins, with no closing text (Jeremy, 2026-10-10). Give a fight a closing beat when it matters to the story,
  when there's loot to explain, or when the win changes something (a flag, a reveal, an Interactable).
- **Loot after a fight** goes in a Reward block after it; previews stop at the fight, so the player finds it after.

## Fights, flags and other tools

- **Scale:** in Dungeon 0 every fight is Major (1 Stamina) or a Boss. Skirmishes (no Stamina) are for M3B's
  Hallways.
- **Initiative:** a successful spot or a trait that sees it coming gives First Strike; a failed one, or walking into
  bait, can leave the party Surprised. An easier route can also mean a smaller encounter (the Gatekeeper's frightened
  guard): a separate encounter id with fewer enemies, which the preview shows.
- **Dungeon flags** carry the story across Events (`merchant_freed`); Event flags are an Event's own bookkeeping
  (`visited`). Players never see flag ids: the text says what changed.
- **Event damage stays small** in Dungeon 0: a hero at 0 Health outside battle is Dead at once.
- **Buffs and curses** from Events last steps (Nodes explored) or battles; a battle-timed one joins every fight. A
  curse lowers a stat, or hurts or drains its holder; it never lowers a trait.

## Checking an Event

- `sim data` (and every test run) checks every link, id and text key, and that every block can be reached.
- `sim event <id> --choices a,b` plays it with its previews; `--flags` and `--gold` set the state to see other
  branches. A test plays every first choice of every Dungeon 0 Event to its end.
