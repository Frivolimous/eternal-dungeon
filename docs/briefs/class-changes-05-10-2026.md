Changes to the class system

Please apply these changes, then dump the file.

# Class Definition
Classes each have:
* Name
* 5 Skills in the Skill Tree
* 3 Class Masteries
* 3 ordered Class Traits
* 1 Exploration Skill

# Class Skills
Every class has 5 skills in the Skill Tree and 3 Class Masteries.
Skill Tree skills have 5 levels each, Masteries have 1 level each and are unlocked in order
Masteries are earned based on total investment into that tree: 1, 5, 10 Skill Points
Skill Points are earned 1 per level for 20 levels

Design space for SKILL TREE SKILLS:
* Boring Stats: simple skills that improve stats
* Triggered Effects: Triggered effects on any condition
* Conditional Effects: Boost stats on specific conditions
* Action Modifiers: modifies basic actions (attack / block / move) or class actions
* Effect Modifiers: modifies standard effects or effects tied to your class
* Rule Exception: special rule or special rule change not covered by above

Design space for CLASS MASTERIES:
* New Active Abilities
* Powerful one-off passives

# Warrior Tree
Bruiser: Burst protection and up-front initiation damage. Strong in short battles but falls off in prolonged conflict.

Skill Tree:
1. Vigor (T1): Strength+, Health+
2. Battle Might (T2): Strength+, CMult+, first basic attack is an automatic crit
3. Weapon Mastery (t2): Hit+, Parry+, Apply stagger on each hit
4. Fortitude (T2): Health+, Fortification+
5. Imposing Presence (T3): Threat+, Block+, Block doubled until first block

Skill Tree Shape:
  1
 /|\
2 3 4
  |
  5

Class Masteries:
1. Power Attack: Active Ability, Damage x2, high AP cost
2. Defensive Blow: Active Ability, Damage x0.5, Block+
3. Colossal Strike: Active Ability, Damage x4, -1 Stamina

# Rogue Tree
Tactical, Versatile:  Move into position, set up attacks and execute, or wait for opportunities to react.

Skill Tree:
1. Shadow Mastery (T1): Accuracy+, CritMult+, CritMult+ when stealth, Stealth Duration+
2. Deadly Shadows (T2): Dexterity+, Crit Rate +, Crit Rate+ when stealth, Crits don't break stealth
3. Dancing Shadows (T2): Dexterity+, Dodge+, Gain Stealth when dodging
4. Executioner (T1): Damage+ when enemy has <50% HP
5. Opportunist (T1): Initiative+, Refund up to 50% of AP cost based on target's Stagger/Stun

Skill Tree Shape:
4 1 5
 / \
2   3 

Class Masteries:
1. Stealth: Move action can also move to enemy board. On Move, gain Stealth for 1 turn (Threat-)
2. Quick Attack: Active Ability, Damage * 0.5, low AP cost
3. Deadly Precision: Active Ability, Accuracy+, Penetration+

# Elementalist Tree
Generic magic user. Spell selection comes from equipped spells, not skills. Specializing in elemental magic and is flexible with what to focus on.

Skill Tree:
1. Magical Aptitude (T1): Magic+, Mana+
2. Elemental Affinity (T2): Magic+, Elemental+
3. Mana Conduit (T2): Mana+, Elemental Spell cost less mana
4. Elemental Attunement (T3): Elemental Resist+, Elemental Devaluation+
5. Elemental Ward (T3): Elemental Turning+, Your Elemental Turning also is also added to adjacent heroes

Skill Tree Shape:
  1
 / \
2   3
|   |
4   5
	
Class Masteries:
1. Elemental Focus: Passive, Elemental Damage+
2. Elemental Expertise: Passive, Elemental Rate+
3. Elemental Mastery: Passive, Elemental Penetration+

# Class Traits
Every class also has 3 traits, ordered by class identity importance.
A hero with a primary class gets all 3 traits, secondary class gets first 2 traits, dabble gets only first trait.
If two identical traits are assigned, it increases the trait level
ie. Warrior + Defender + Beastmaster gets Military 2, Discipline 1, Intimidation 1, Awareness 1, Survival 1

Events can have special options that require a trait with a level or a specific class. Trait level 1 is the most common, with level 2 being rare and level 3 extremely rare.

Traits may also be acquired from Talents or Races.

List of traits is subject to change once we start creating events and see which traits are over / under represented in advanced decision options.


WIP LIST OF TRAITS:

Class	Trait 1	Trait 2	Trait 3
Warrior	Military	Discipline	Intimidation
Berserker	Survival	Intimidation	Athletics
Defender	Military	Awareness	Survival
Captain	Persuasion	Military	Discipline
Beastmaster	Survival	Awareness	Intimidation
Rogue	Streetwise	Deception	Disable
Ranger	Awareness	Survival	Athletics
Monk	Discipline	Athletics	Awareness
Energist	Discipline	Arcane	Spiritual
Tinkerer	Disable	Scholar	Streetwise
Elemental	Arcane	Scholar	Awareness
Arcanist	Arcane	Scholar	Discipline
Cleric	Spiritual	Scholar	Persuasion
Acolyte	Spiritual	Deception	Intimidation
Bard	Persuasion	Deception	Streetwise


# Exploration Skill
Every class has 1 exploration skill. The hero only gets the exploration skill belonging to their Primary Class. It's intended to be one special rule that alters how the player explores the dungeon. Exploration Skills will be developed in M4, after exploration has been finalized.

Exploration Ability Design Space — Locked

Overland

Reduce or modify travel costs
Modify travel restrictions

Adventure / Dungeon Navigation

Reveal Regions
Reveal Rooms
Modify access/navigation between areas

Information

Reveal Room Types
Reveal Event Types
Reveal Encounter Difficulty
Reveal Reward Information

Stamina

Restore Stamina
Conserve Stamina
Modify Stamina expenditure
Trade Stamina for another resource/effect
Increase Maximum Stamina
Alter Exhaustion effects

Camping

Improve Camp recovery
Modify starting number of Camps
Gain an additional benefit when Camping
Trade another resource to enhance a Camp
Gain additional Camps under specific conditions

Sanctuaries

Improve Sanctuary effects
Add an additional Sanctuary effect
Provide an alternate use for a Sanctuary

Expedition Resources

Recover Health
Recover Mana
Recover Stamina
Recover or conserve Consumables

Exploration Spells / Consumables

Modify effectiveness
Conserve charges/uses
Provide alternate interactions or uses

What it doesn't do:
* Give universal blanket powers or abilities in combat (those are skills)
* Open up more options or improves decisions in events (those are traits)

WIP Ideas:
| Class           | Exploration Ability                                                                                   |
| --------------- | ----------------------------------------------------------------------------------------------------- |
| **Warrior**     | Max Stamina +2                                                                                        |
| **Berserker**   | After each combat encounter, this hero recovers 20% HP and 20% Mana                                   |
| **Defender**    | The first time any hero would become Exhausted each Adventure, restore 2 Stamina to that hero instead |
| **Captain**     | When entering a new Floor, reveal the Room Types of 3 random unexplored Rooms                         |
| **Beastmaster** | After exploring, reveal the Event Type of one adjacent Region/Room                                    |
| **Rogue**       | Collecting a Trinket allows you to choose from 1 additional option                                    |
| **Ranger**      | When in an Adventure Map, reveal the Event Type of all explorable Regions                             |
| **Monk**        | When entering a new Floor, reveal the location of 1 Sanctuary                                         |
| **Energist**    | After each combat encounter, heal 10% of each hero's HP                                               |
| **Tinkerer**    | Sanctuaries and Camping restore up to 3 random charges of Consumables                                 |
| **Elemental**   | Exploration Spells cost 20% less Mana                                                                 |
| **Arcanist**    | When entering a Floor, reveal the location of the most valuable undiscovered reward                   |
| **Cleric**      | Sanctuaries and Resting restore an additional 20% Health and Mana                                     |
| **Acolyte**     | Once per Dungeon, revive one defeated hero at 25% Health                                              |
| **Bard**        | Increase the number of Camps available at the start of each Adventure by 1                            |
