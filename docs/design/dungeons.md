# Dungeons & adventure

There are 5 scripted dungeons with 100 enemies and 21 bosses between them, followed by the endless Shadow World. Each floor has 6 enemy types plus a floor boss. Scripted bosses are unique, and later floors use variants.

| Dungeon | Floors | Enemies | Bosses | Identity |
| --- | --- | --- | --- | --- |
| Goblin Woods | 3 medium | 20 | 3 | Numerous mixed groups, low defense, little or no CC |
| Lich Tower | 5 small | 20 | 5 | Specific weaknesses, specialized groups and abilities |
| Beast Kingdom | 3 big | 20 | 3 | Small groups, defensive, tough, dodgy, buffed, large monsters |
| Cultist Temple | 5, big to small | 20 | 5 | Magic, summoning, heavy CC, heavy curses |
| Demonic Underworld | 5, small to big | 20 | 5 | Specialized groups, mixed weaknesses and themes |
| Shadow World (bonus) | Unlimited | Shadow versions of all groups | — | Each floor gets a different random set |

Floor size is the number of rooms (encounters and events) on a floor. Placeholder counts: small = 5 rooms, medium = 8, big = 12.

## Dungeon 0: first-time experience

- A new game starts with 3 preset heroes: Warrior, Rogue and Elementalist. There's no character setup. The 4th hero is recruited from the Tavern in the first Town.
- The player goes straight into Dungeon 0, a short, easy version of Goblin Woods, to learn the game.
- **Different death rule:** if all 3 heroes die, the player starts over. If at least 1 survives, all 3 move on to the first Town and the meta game begins.
- Dungeon 0 is never repeated. Heroes recruited later always start after it.

## Floor flow

**v1 (simple, old-school dungeon crawl). Experiments come later.**

1. Each floor is a 2D overview map of rooms, hallways and doors. Only explored areas are visible, and the rest is fog of war.
2. The player explores by tapping arrows off known doors.
3. Every room holds either a battle or an event.
4. The stairs to the next floor are guarded by the floor boss. Find the stairs and beat the boss to move on.

Known weakness: players can explore everything, so there's little real choice about where to go. To revisit after v1.

- **Event:** a text description with multiple-choice options. Stats or classes can unlock extra options, and choices lead to different rewards or results.
- **Battle:** a fixed enemy layout on the enemies' own 2×3 grid. Battles can roll encounter modifiers. Initiative: Surprised (party −Initiative) or First Strike (party +Initiative). Position: Ambushed (enemies on all sides of the party) or Surrounding (the party flanks the enemy). Candidate additions: Terrain, Elite, Reinforcements, Fatigued.

## Run outcomes

- **Attrition:** HP and Mana carry over between rooms on a floor, with some healing between fights (how much and from what is open).
- **Completing a dungeon:** beating the boss of its last scripted floor completes the dungeon. That unlocks the next dungeon and raises the dungeon level of every participating hero.
- **Extended floors:** instead of leaving, the party can push on into endless floors that get harder each time, with better loot.
- **Retreat:** the party leaves for Town and keeps all progress, but the dungeon isn't completed.
- **A hero falls, but the party completes the dungeon:** the hero is revived as if nothing happened. The death counter doesn't increase.
- **Party wipe:** all heroes are revived, and each hero's death counter goes up by 1. The party isn't eligible for the next dungeon. Current Blessings are removed, and future Blessings are weaker (−5% per death). Heroes keep all XP, levels and loot from the run.
- **Wipe on an extended floor:** counts as a normal wipe, but the heroes still gain the dungeon level, since the dungeon was already completed.
- **XP:** every hero alive at the end of a battle gets an equal share, and heroes who are dead get none. Each hero levels up separately. All level-up decisions (skill points, class picks) are made manually.
- **Temples** grant a Blessing.
- **Boons** (lasting a set number of battles) and **Trinkets** (lasting the whole dungeon) are gained inside a dungeon and lost on leaving.
