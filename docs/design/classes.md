# Classes & skills

There are 15 classes in three families of five. Each family's bonus shapes the equipment loadout: Primary+ classes let a hero use higher-tier weapons and armor without a weight penalty, Belt+ classes add belt slots, and Spell+ classes add spell slots.

## What a class is

Every class has:

- A name and a **family** (Primary+, Belt+ or Spell+). Each class layer a hero takes adds one point to its family's bonus (see [Equipment](equipment.md)).
- A **skill tree** of 5 skills, each with 5 levels.
- 3 **masteries**, unlocked in order by the points spent in that class's tree.
- 3 **class traits**, ordered by how central they are to the class.
- 1 **exploration skill**.

| Class | Family | First mastery (was the class skill) | Theme | Mode | Role | Resource | Range |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Warrior | Primary+ | Power Attack: deals ×2 damage | Balanced melee attacker | Balanced | Bruiser | Basic | Melee |
| Berserker | Primary+ | Enrage: temporary +damage | Offensive melee attacker | Offensive | Bruiser | Rage | Melee |
| Defender | Primary+ | Shield Wall: shields the whole party | Defensive meat stick | Defensive | Tank | Basic | Melee |
| Captain | Primary+ | Inspire Precision: +Accuracy and C.Rate | Defensive support | Defensive | Support | Allies | Flexible |
| Beastmaster | Primary+ | Companion: adds another unit | Versatile companion | Utility | Support | Companion | Flexible |
| Rogue | Belt+ | Stealth: Move can enter the enemy area and grants Stealth | Sneaking and big strikes | Offensive | Carry | Stealth | Flexible |
| Ranger | Belt+ | Mark Target: +damage vs. the target | Ranged and crits | Offensive | Carry | Basic | Ranged |
| Monk | Belt+ | Counter Attack: strikes on dodge | Mopping up small enemies | Balanced | Brawler | Unequipped | Melee |
| Energist | Belt+ | Meditate: recovers HP and MP | Mana-fueled balanced melee | Balanced | Brawler | Mana | Melee |
| Tinkerer | Belt+ | Doubles consumable charges | Belt items and support | Utility | Support | Consumables | Ranged |
| Elementalist | Spell+ | Elemental Focus: +Elemental damage | Offensive caster | Offensive | Carry | Mana | Ranged |
| Arcanist | Spell+ | Meddle: staggers and interrupts an enemy while it casts | Utility caster | Utility | Carry | Mana | Ranged |
| Cleric | Spell+ | Healing Burst: heals everyone, once per combat | Unblockable damage and healing | Defensive | Support | Buffs | Flexible |
| Acolyte | Spell+ | Blood Magic: damages self to buff self | Curses and self-infliction | Offensive | Carry | Curses | Flexible |
| Bard | Spell+ | Inspire Haste: +speed and dodge | Offensive support | Utility | Support | Allies | Ranged |

Only the Warrior, Rogue and Elementalist (the starting three) have full trees and masteries so far. For the other classes the first mastery is the old class skill, and the rest is designed with each class.

## Class layers

- **Primary (starting class):** permanent, and assigned before recruitment. The player picks a starting class by picking a character in the Tavern, which can offer several characters of the same class. The hero can learn all 5 tree skills, following the tree's prerequisites.
- **Secondary:** chosen in Town from Level 5 on, and permanent once chosen. Uses the same class definitions as Primary. The hero gets access to 3 random tree skills from that class, picked only from combinations that make sense under the prerequisite chain. Prerequisites still apply among those 3 when spending points.
- **Dabble:** chosen in Town from Level 10 on, and permanent once chosen. The player picks a class, the game offers 3 random skills from that class's Dabble-eligible list, and the player keeps 1. Ignores prerequisites.
- **Total:** 5 + 3 + 1 = 9 tree skills per hero.

| Layer | Tree skills | Most points in the tree | Masteries reachable | Traits | Exploration skill |
| --- | --- | --- | --- | --- | --- |
| Primary | 5 | 25 | 3 | All 3 | Yes |
| Secondary | 3 | 15 | 3 (the third costs 11 of the 15) | First 2 | No |
| Dabble | 1 | 5 | 1 | First 1 | No |

## Skill points

- 1 point per level, Level 1 included. The max level is 20, so a hero gets 20 points. The cap may be revisited when XP pacing is tuned.
- Each tree skill has 5 levels. 9 skills × 5 levels = 45 points to max everything, so a hero can max fewer than half.
- Points can be banked, and spent anywhere outside combat, between Events, through the Hero Panel. Respec is Town-only (decided 2026-10-08).
- Each class tree has its own structure. A prerequisite is met with 1 or more points in the required skill.

## Skill trees

Tree skills are **passives and modifiers**. Active abilities come from masteries. The design space for tree skills:

- **Plain stats:** simple skills that raise stats.
- **Triggered effects:** effects that fire on a condition (procs).
- **Conditional effects:** stat boosts under specific conditions.
- **Action modifiers:** change the basic actions (Attack, Defend, Move) or the class's own actions.
- **Effect modifiers:** change standard effects, or effects tied to the class.
- **Rule exceptions:** a special rule, or a change to a rule, not covered above.

## Masteries

Each class has 3 masteries, unlocked in order by the **total points spent in that class's tree: 1, 6 and 11**. Masteries have one level. A Dabble holds at most 5 points, so it gets exactly the first mastery; a Secondary reaching its third (11 of its 15 possible points) is a deliberate sacrifice.

Design space for masteries: new active abilities, and powerful one-off passives. The first mastery is the class's signature (it replaces the old "class skill" that every hero with the tree got for free).

## The starting classes

Tier labels (T1–T3) are the skill's depth in the tree. Numbers are designed in M3, when these three trees, their masteries and traits are built.

### Warrior

Bruiser: burst protection and up-front damage to start fights. Strong in short battles, but falls off in long ones.

| # | Skill | Tier | Effect |
| --- | --- | --- | --- |
| 1 | Vigor | T1 | Strength+, Health+ |
| 2 | Battle Might | T2 | Strength+, C.Mult+; the first basic attack is an automatic crit |
| 3 | Weapon Mastery | T2 | Hit+, Parry+; each hit applies stagger |
| 4 | Fortitude | T2 | Health+, Fortification+ |
| 5 | Imposing Presence | T3 | Starting Threat+, Block+; Block is doubled until the first block |

```text
    1
  / | \
 2  3  4
    |
    5
```

| Mastery | Points | Effect |
| --- | --- | --- |
| Power Attack | 1 | Active: damage ×2, high AP cost |
| Defensive Blow | 6 | Active: damage ×0.5, Block+ |
| Colossal Strike | 11 | Active: damage ×4, costs 1 Stamina |

### Rogue

Tactical and versatile: move into position, set up attacks and execute, or wait for openings to react.

| # | Skill | Tier | Effect |
| --- | --- | --- | --- |
| 1 | Shadow Mastery | T1 | Accuracy+, C.Mult+, more C.Mult in Stealth, Stealth lasts longer |
| 2 | Deadly Shadows | T2 | Dexterity+, C.Rate+, more C.Rate in Stealth; crits don't break Stealth |
| 3 | Dancing Shadows | T2 | Dexterity+, Dodge+; dodging grants Stealth |
| 4 | Executioner | T1 | Damage+ against enemies below 50% HP |
| 5 | Opportunist | T1 | Initiative+; against a Stunned target, refunds 50% of the action's AP; otherwise refunds the target's Speed reduction from buffs and debuffs, capped at 50% (Chill −30 Speed on a base 100 → 30%). Stagger and Exhaustion don't count. Lower skill levels scale the cap down (decided 2026-10-08) |

```text
 4   1   5
    / \
   2   3
```

| Mastery | Points | Effect |
| --- | --- | --- |
| Stealth | 1 | Move can also go into the enemy area. Every Move grants Stealth for 1 turn (Threatening ×0) |
| Quick Attack | 6 | Active: damage ×0.5, low AP cost |
| Deadly Precision | 11 | Active: Accuracy+, Pen+ |

### Elementalist

A generic magic user. Its spells come from equipped spells, not skills. It specializes in elemental magic and is flexible in what it focuses on.

| # | Skill | Tier | Effect |
| --- | --- | --- | --- |
| 1 | Magical Aptitude | T1 | Magic+, Mana+ |
| 2 | Elemental Affinity | T2 | Magic+, Elemental Power+ |
| 3 | Mana Conduit | T2 | Mana+; Elemental spells cost less Mana |
| 4 | Elemental Attunement | T3 | Elemental Resist+, Elemental Deval+ |
| 5 | Elemental Ward | T3 | Elemental Turn+; the hero's Elemental Turn is also added to adjacent heroes |

```text
   1
  / \
 2   3
 |   |
 4   5
```

| Mastery | Points | Effect |
| --- | --- | --- |
| Elemental Focus | 1 | Passive: Elemental damage+ |
| Elemental Expertise | 6 | Passive: Elemental Rate+ (procs on Elemental actions fire more often) |
| Elemental Mastery | 11 | Passive: Elemental Pen+ |

## Class traits

Every class has 3 traits, ordered by how central they are to the class. A hero gets all 3 from its Primary class, the first 2 from its Secondary and the first 1 from its Dabble. The same trait from two layers raises its level: Warrior + Defender + Beastmaster gives Military 2, Discipline 1, Intimidation 1, Awareness 1, Survival 1.

Events can have special options that need a trait at some level, or a specific class. Trait level 1 is the most common, level 2 rare and level 3 extremely rare. Traits can also come from Talents or Races. The list will change once events are written and show which traits are over- or under-used.

| Class | Trait 1 | Trait 2 | Trait 3 |
| --- | --- | --- | --- |
| Warrior | Military | Discipline | Intimidation |
| Berserker | Survival | Intimidation | Athletics |
| Defender | Military | Awareness | Survival |
| Captain | Persuasion | Military | Discipline |
| Beastmaster | Survival | Awareness | Intimidation |
| Rogue | Streetwise | Deception | Disable |
| Ranger | Awareness | Survival | Athletics |
| Monk | Discipline | Athletics | Awareness |
| Energist | Discipline | Arcana | Spiritual |
| Tinkerer | Disable | Scholar | Streetwise |
| Elementalist | Arcana | Scholar | Awareness |
| Arcanist | Arcana | Scholar | Discipline |
| Cleric | Spiritual | Scholar | Persuasion |
| Acolyte | Spiritual | Deception | Intimidation |
| Bard | Persuasion | Deception | Streetwise |

## Exploration skills (WIP, M4)

Every class has 1 exploration skill, and a hero gets only its Primary class's. It's one special rule that changes how the player explores (see [Exploration](exploration.md)). Exploration skills are developed in M4, once exploration is final.

Design space (locked):

| Area | What an exploration skill can do |
| --- | --- |
| Overland | Reduce or change travel costs; change travel restrictions |
| Dungeon navigation | Reveal Regions or Rooms; change access and navigation between areas |
| Information | Reveal Room types, Event types, encounter difficulty or reward information |
| Stamina | Restore or conserve Stamina; change Stamina spending; trade Stamina for another resource or effect; raise max Stamina; change Exhaustion effects |
| Camping | Improve Camp recovery; change the starting number of Camps; add a benefit to Camping; trade another resource to improve a Camp; gain extra Camps under conditions |
| Sanctuaries | Improve Sanctuary effects; add a Sanctuary effect; give Sanctuaries another use |
| Expedition resources | Recover Health, Mana or Stamina; recover or conserve consumables |
| Exploration spells and consumables | Change their effectiveness; conserve charges or uses; give other interactions or uses |

What exploration skills don't do: give blanket combat powers (those are skills), or open up or improve event options (those are traits).

WIP ideas:

| Class | Exploration skill |
| --- | --- |
| Warrior | Max Stamina +2 |
| Berserker | After each combat, this hero recovers 20% HP and 20% Mana |
| Defender | The first time any hero would become Exhausted in a dungeon, restore 2 Stamina to that hero instead |
| Captain | On entering a new Map, reveal the Node types of 3 random unexplored Nodes |
| Beastmaster | After exploring, reveal the Event type of one adjacent Node |
| Rogue | Collecting a Trinket offers 1 more option to choose from |
| Ranger | On an Outdoor Map, reveal the Event type of every explorable Region |
| Monk | On entering a new Map, reveal the location of 1 Sanctuary |
| Energist | After each combat, heal each hero for 10% of their HP |
| Tinkerer | Sanctuaries and Camping restore up to 3 random consumable charges |
| Elementalist | Exploration spells cost 20% less Mana |
| Arcanist | On entering a Map, reveal the location of the most valuable undiscovered reward |
| Cleric | Sanctuaries and Rest restore an extra 20% Health and Mana |
| Acolyte | Once per dungeon, revive one fallen hero at 25% Health |
| Bard | +1 Camp at the start of each dungeon |

## Talents & races

Talents and races make heroes of the same class feel different. They change how a hero plays rather than how strong it is.

- **Talents:** random modifiers rolled onto a character when it appears in the Tavern and is hired. For example, *Wild* adds more randomness to damage dealt, and *Deft* multiplies Dexterity but reduces Strength. Talents can also grant traits.
- **Races:** stat bonuses and penalties that overall shouldn't add much power but do alter playstyle. Reincarnation changes a hero's race. Races can also grant traits.
