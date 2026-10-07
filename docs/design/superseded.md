# Superseded decisions

| Topic | Old version | Decision |
| --- | --- | --- |
| Primary Class skills | Full access to a larger tree | Every class has exactly 5 skills, and the Primary gives access to all 5. |
| Secondary and Dabble unlock | Unlocked through "Discovery" | Chosen in Town from Level 5 (Secondary) and Level 10 (Dabble). |
| Dabble pick | 1 random skill | Choose 1 of 3 random Dabble-eligible skills. |
| Equipment slots | A "Special" slot | No Special slot. Current slots: Weapon, Off-hand, Armor, Helmet, 1 Accessory, 0–3 Spells and 1–4 Belt slots. |
| "Trinket" naming | Trinket+ class family and Accessory slots | Renamed to the Belt+ family and Belt slots. "Trinket" now means only the dungeon-wide bonus. Accessory is now a separate single slot. |
| "Light" element | Light/Dark element, clashing with the Light weapon tag | Element renamed to Holy. "Light" is now only the weapon-weight tag. |
| "Magic" damage type | Magic as both a damage type and a compound stat | Damage type renamed to Arcane. "Magic" is now only the compound stat that boosts Spell Power. |
| Elemental tags | Electric vs Lightning, plus Water and Earth in the recipe | Fire, Electric and Ice only. Water and Earth are dropped for now and can be added later. |
| "Ambush" | Meant both +Initiative and enemies on all sides | Split into initiative modifiers (Surprised, First Strike) and position modifiers (Ambushed, Surrounding). |
| Buff clock and scales | Speed 1 vs Speed 100 | Same thing: the buff clock runs at base Speed 100. Speed, Act and AP use integers on a 100 scale, and chance stats use 0–1. |
| Death penalty | Dead heroes return to the previous dungeon and lose progress | Only a party wipe counts as a death: the counter goes up by 1 and the party can't advance. A hero who falls in a completed dungeon is revived with no penalty. |
| Stagger | Two designs left open to test: AP damage, or a stagger bar | Stagger bar is the default. AP damage is kept as the fallback for playtesting. |
| Release content | 50 enemies, 10 dungeons | For now: 100 enemies, 21 bosses, 5 dungeons + Shadow World. Expected to change. |
| Multiplier combine mode | Mult (compounding) | Add (summed), for easier balance at endless depth. |
| Platform and business model | Free-to-play mobile and web: ads, ad skipping, VIP points, Day 1 unlocks, bought Legendary gear, exclusive Artifacts and Blessings, bundles, skins | Premium game on Steam. All monetization removed. Every item, Artifact and Blessing is earned in play, and the shop sells for Gold only. |
| Power shares and Blessing reset | Equipment 35%, Artifacts 30%, Class 23%. Reincarnation only reset Blessings. | Equipment 37%, Class 28%, Artifacts 25% (placeholders). Reincarnation fully restores Blessings and clears all decay. |
| Crit | A proc effect that stacks from many sources ("Critical from many sources"), resisted through Fortification | A core stat: Crit Rating converts to a crit chance, a crit can roll again for a Brutal tier, and each tier adds Crit Mult. Procs are a separate system that can trigger on crit and on Brutal. |
| Production plan | Solo-developer roadmap: 8 phases, about 744 estimated hours, systems spread across phases | Claude as main developer: milestones M0–M6 defined by playable outcomes and checkpoints, plus parallel tracks for art, audio, economy and the store page. |
| Effect triggers | Buffs had their own triggers: on being hit, on turn start, on action complete (with a state check) | Replaced by procs, which buffs grant while they last (2026-10-04). |
| Rate and Deval | Dim stats: Proc = Base × (1 + Rate) × (1 − Deval); Rate could never double a proc, Deval approached immunity | Both Add, written as fractions: Proc = Base × (1 + ΣRate) ÷ (1 + ΣDeval); Crit uses Rating ÷ (1 + Critical Deval) (2026-10-04) |
| Compound cap | Compounds capped at 100 points with a warning above 95; one compound's contribution to a chance stat clamped at ±0.95 | No compound cap; each compound source feeds the target stat as its own source; any single source of a Dim stat counts for at most ±0.95 (2026-10-04) |
| Fortification | Physical Resist 1 · Critical Resist 1 · Critical Deval 1 | Critical Deval removed (2026-10-04) |
| Intellect Rate | Gadget Rate 1 · Cryptic Rate +0.5 | Gadget Rate 0.1 · Cryptic Rate +0.05 (2026-10-04) |
| Proc chance above 100% | The excess scaled the proc's amounts (150% for 20 damage fired at 100% for 30) | The excess is lost; amounts grow only when copies of the same proc merge (2026-10-04) |
| Crit stat names | Crit Rating and Crit Mult | C.Rate and C.Mult, never written out: under TAG STAT, "Critical Rate" would mean Rate on Critical-tagged actions (like Critical Resist and Critical Pen). Same formula (2026-10-07) |
| H.Regen and M.Regen | Untagged stats: Health and Mana regenerated every buff-clock turn | Removed (2026-10-07). There is no passive regeneration; Health and Mana are expedition resources that carry between fights. Healing or Mana over time can still be an effect, like damage over time. Robe and Cerulean armor give Mana instead |
| Elemental compound | Fire, Electric and Ice Power 1 each (three rows); no Elemental tag | Elemental is a tag carried by everything Fire, Electric or Ice; the compound is the single row Elemental Power 1, so Elemental Resist, Deval, Turn and Pen are plain tag stats (2026-10-07) |
| Sneak and Cloak | The Rogue's class skill Sneak: a separate 100-AP action to any empty enemy tile, granting the Cloak status | The Rogue's first mastery, Stealth: Move can also enter the enemy area, and moving grants Stealth for 1 turn. Cloak is renamed Stealth (2026-10-07) |
| Tree skill kinds | Every class tree mixed passives and actives: 2–3 active and 2–3 passive skills, each "a passive stat boost or an active ability" | Tree skills are passives and modifiers; active abilities come from masteries (2026-10-07) |
| Class skill | One class skill per class, given free to anyone with the tree | The class skill is now the class's first mastery, unlocked at 1 point in its tree (2026-10-07) |
| Adventure bonuses | "Adventure bonus" and "adventure trait" ideas for out-of-combat class abilities | Ordered class traits (3 / 2 / 1 by layer) and one exploration skill, from the Primary class only (2026-10-07) |
| Skill levels | Skills with 10 levels | 5 levels per tree skill, confirmed; masteries have one level (2026-10-07) |
| Floors and floor flow | v1 floors: a fog-of-war map where every room held a battle or an event, a floor boss guarding the stairs, and floors of 5 / 8 / 12 rooms (small / medium / big) | Floors become Maps (Outdoor or Indoor) of Nodes holding Events and Interactables; combat comes from Events at Skirmish, Major or Boss scale; Maps are left through transition Interactables. See exploration.md (2026-10-07) |
| Retreat | The party leaves for Town and keeps all progress, but the dungeon isn't completed | Leave: the dungeon instance and its state are destroyed; heroes keep gear and XP, the death counter is unchanged and Blessings are kept. A wipe destroys it the same way, plus +1 death counter and Blessings removed (2026-10-07) |
| Between-fight healing | HP and Mana carry over between rooms, with some healing between fights (amount and source open) | No passive regeneration; Health, Mana and Stamina are expedition resources restored by Camps, Sanctuaries, Events, items and skills (2026-10-07) |
