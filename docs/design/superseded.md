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
