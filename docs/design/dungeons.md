# Dungeons & adventure

There are 5 scripted dungeons with 100 enemies and 21 bosses between them, followed by the endless Shadow World. Each Map has 6 enemy types plus a Map boss. Scripted bosses are unique, and later Maps use variants. How a party explores a dungeon (Maps, Nodes, Events, Stamina, Camps) is in [Exploration](exploration.md).

| Dungeon | Maps | Enemies | Bosses | Identity |
| --- | --- | --- | --- | --- |
| Goblin Woods | 3 medium | 20 | 3 | Numerous mixed groups, low defense, little or no CC |
| Lich Tower | 5 small | 20 | 5 | Specific weaknesses, specialized groups and abilities |
| Beast Kingdom | 3 big | 20 | 3 | Small groups, defensive, tough, dodgy, buffed, large monsters |
| Cultist Temple | 5, big to small | 20 | 5 | Magic, summoning, heavy CC, heavy curses |
| Demonic Underworld | 5, small to big | 20 | 5 | Specialized groups, mixed weaknesses and themes |
| Shadow World (bonus) | Unlimited | Shadow versions of all groups | — | Each Map gets a different random set |

Map sizes (small, medium, big) are set when Maps are built (M3).

## Dungeon 0: first-time experience

- A new game starts with 3 preset heroes: Warrior, Rogue and Elementalist. There's no character setup. The 4th hero is recruited from the Tavern in the first Town.
- The player goes straight into Dungeon 0, a short, easy version of Goblin Woods, to learn the game.
- **Different death rule:** if all 3 heroes die, the player starts over. If at least 1 survives, all 3 move on to the first Town and the meta game begins.
- Dungeon 0 is never repeated. Heroes recruited later always start after it.
- There's no Leave in Dungeon 0 (there's no Town to leave to). Until Town exists (M4), completing it shows a "slice complete" screen, and a wipe restarts the game from the beginning of Dungeon 0.

## Battles

A battle is started by an Event's Combat block (see Exploration). Enemies are placed on their own area (3×2 or 2×3 by default, growing with abilities), in a layout that is either fixed by the encounter or generated (see Combat › Battlefield and the Combat block in Exploration). Battles can roll encounter modifiers. Initiative: Surprised (party −Initiative) or First Strike (party +Initiative). Position: Ambushed (enemies on all sides of the party) or Surrounding (the party flanks the enemy). Candidate additions: Terrain, Elite, Reinforcements, Fatigued.

## Run outcomes

- **Attrition:** Health, Mana and Stamina carry over through the whole dungeon. There is no passive regeneration: Camps, Sanctuaries, Events, items and skills restore them (see Exploration).
- **Completing a dungeon:** beating the boss of its last scripted Map completes the dungeon. That unlocks the next dungeon and raises the dungeon level of every participating hero.
- **A hero falls, but the party completes the dungeon:** the hero is revived as if nothing happened. The death counter doesn't increase.
- **Extended Maps:** instead of leaving, the party can push on into endless Maps that get harder each time, with better loot. How this fits the Map model is open.
- **Leave:** the party leaves for Town. The dungeon instance and all its state (Maps, flags, deferred Events) are destroyed, and the dungeon isn't completed. Heroes keep all gear and XP gained in it, the death counter doesn't change, and the party keeps its current Blessings.
- **Party wipe:** the dungeon instance and all its state are destroyed in the same way, and heroes keep all gear and XP gained in it. On top of that, all heroes are revived, each hero's death counter goes up by 1, current Blessings are removed, and future Blessings are weaker (−5% per death). The party isn't eligible for the next dungeon.
- **Wipe on an extended Map:** counts as a normal wipe, but the heroes still gain the dungeon level, since the dungeon was already completed.
- **XP:** every hero alive at the end of a won battle gets an equal share; heroes who are dead or fled get none. Each hero levels up separately. All level-up decisions (skill points, class picks) are made manually.
- **Temples** grant a Blessing.
- **Boons** (lasting a set number of battles) and **Trinkets** (lasting the whole dungeon) are gained inside a dungeon and lost on leaving.

## Saving

Saving is constant (decided 2026-10-08): one save slot, written after every Node, every Event block, every Hero Panel
change and at every battle start. A battle in progress saves as its seed plus the decisions made so far (the replay
format), so quitting mid-fight and loading replays it to the same point. Loading restores the exact state: the same
random generator position, the same enemy intents, the same Event block. While the game is in development, a save made
with older data warns and offers a restart instead of loading wrongly.
