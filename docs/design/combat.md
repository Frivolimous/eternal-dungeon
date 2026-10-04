# Combat

Combat is purely turn-based and speed-driven, fought on small grids. Each hero is a bundle of tag stats resolved through the formulas below.

## Formulas

```latex
\text{Success} = \text{Hit} \times (1 - \text{Avoid})
```

```latex
\text{Dmg} = \text{Base} \times \Big(1 + \frac{\sum\text{Power}}{100}\Big) \times \big(1 + \sum\text{Mult}\big) \times \big(1 - \text{Resist} \times (1 - \text{Pen})\big) \times (1 + \text{AllDmg}) \times (1 - \text{AllResist}) \times \text{Crit}
```

```latex
\text{Crit} = 1 + \text{tiers} \times \text{CritMult} \times \big(1 - \text{CritResist} \times (1 - \text{CritPen})\big)
```

```latex
\text{Proc} = \text{Base} \times (1 + \sum\text{Rate}) \div (1 + \sum\text{Deval})
```

Crit is a core stat, not a proc. Crit Rating (Add, tag-keyed, hard cap 200%) converts into a per-hit crit chance c = (√(1 + 4 × Rating) − 1) / 2. A crit rolls again at the same chance for a Brutal crit, so expected damage rises in a straight line with Rating. Each tier adds Crit Mult (Add, tag-keyed): Damage × (1 + tiers × Crit Mult × (1 − Critical Resist × (1 − Critical Penetrate))). The target's Critical Deval lowers the Rating before the conversion: Rating ÷ (1 + Critical Deval), then the 200% cap. Critical Resist and Critical Penetrate count only their Critical-keyed parts, since the untagged parts already applied to the hit. Crit rolls only on a successful hit. The character sheet shows the Rating with both chances as shares of all hits: Rating 100% gives a 62% crit chance, and 38% of hits are Brutal (62% of crits).

Power and Multiplier scale damage as percentages. Power factor = 1 + total Power / 100, so Fire Power 50 means +50% damage on Fire actions. Multiplier factor = 1 + total Multiplier, so 0.2 means +20%.

Base is the action's base damage plus the attacker's Base Dmg stat for the action's tags. An action's own damage bonus goes into All Damage: Power Attack carries All Damage 1.0, so it deals ×2, and a +0.2 Multiplier ring stacks on top as a separate ×1.2. Damage rounds to the nearest whole number, with a minimum of 1 on a successful damaging hit.

Every character starts with Weapon Crit Rating 5% and untagged Crit Mult 0.5, so every weapon can crit (×1.5, Brutal ×2.0), and any action that gains Crit Rating can crit without needing its own Crit Mult. Procs are designed separately: effects with a trigger and a chance, which can trigger on crit and on Brutal.

## Turn order

- Each unit starts combat with Act = Initiative and gains Speed every tick. Speed 100 is the base.
- At 100 Act, it's that unit's turn. Act can go above 100, giving extra turns, or below 0, delaying the turn.
- Actions cost AP out of the 100-point meter: 50 is half a turn, 100 a full turn, and 200 works like a cooldown.
- There are no cooldowns: an action’s AP cost is the only limit on how often it can be used (a 200-AP action delays the unit’s next turn).
- Spells have a casting timer. The Arcanist's Meddle staggers an enemy while it's casting.
- A unit's Act meter stops filling while it casts, and resumes when the cast completes, fizzles or is interrupted.
- A cast fizzles if its target dies, the caster dies, or it's interrupted. There is no retargeting.
- Buffs and Regen run on their own clock at base Speed 100, so a 3-turn buff lasts 3 base-speed turns whatever the buffed hero's own Speed. Speed, Act and AP are integers on the 100 scale, while chance-type stats (Avoid, Resist, Rate, Deval) are 0–1 internally and shown as percentages.
- Events at the same moment resolve in a fixed order: the buff clock first, then completed casts, then turns.
- If both sides fall at the same moment, it counts as a party wipe.
- **No real-time clock.** Ticks are simulated instantly until the next unit reaches 100 Act, and the game waits for the player on each hero turn. A quick animation shows the meters filling. Every "over time" effect (stagger drain, buffs, Regen) counts ticks, not seconds.

## Battlefield

- Each side has its own area, 3×2 or 2×3 by default. Abilities can grow it to 4×2 or 3×3.
- At most 6 units are on the map: the 4 party heroes plus overflow from mercenaries and pets.
- Heroes move freely within their area, and enemies generally stay put. Every action has a Range.
- Rogues on either side can teleport into the opposing area.
- Enemies usually appear in front. In an Ambushed battle they appear on all sides, and in a Surrounding battle the party flanks them.
- When an area's front row empties, the area collapses forward. Some actions reposition enemies.
- Push and Pull move a unit one row back or forward within its area, only if the tiles are free.
- Enemy size is its footprint: size 1 takes 1 tile (a person), size 1.5 takes 2 tiles in a column, front and back (a troll), and size 2 takes a 2×2 block of 4 tiles (a Balrog).

## Default actions

Every unit, heroes and enemies, has three default actions on top of its own skills (defaults.json, so they can be tuned without code changes). AP costs are placeholders for tuning.

| Action | AP | Effect |
| --- | --- | --- |
| Attack | 100 | Basic weapon attack |
| Defend | 100 | +0.3 Avoid and a Shield of 10% max Health until the unit's next turn |
| Move | 50 | One tile within the unit's own area |

A unit with no valid target for its skills uses these: it Moves to step into an empty front tile if it can, otherwise it Defends. This keeps "enemies generally stay put" true while stopping melee enemies from waiting behind their own front line.

## Crowd control

CC can target a character or a tile: Slow, Stun, Stagger, Pull/Push/Move, Root, damage over time, delayed damage, stat reduction, action override (Confusion, Fear, Sleep), action restriction (Root, Silence) and conditional effects.

Fear: a feared unit can only Defend, or Move one tile away from the front. It can't attack or use skills. Unlike Stun (and Sleep), Fear doesn't skip the turn.

Damage over time scales with the caster: when it's applied, the caster's Power and Multiplier factors for the tags of the action (or proc) that applied it are locked in, and every tick uses them. Shield still absorbs ticks.

**Stagger uses a stagger bar** (the default, to be confirmed in playtesting):

- Each unit has a stagger bar that starts at 0. Stagger damage fills it, and it drains over time.
- While the bar is above 0, the unit's Speed is halved.
- At 100% the unit is stunned (Speed × 0) and can't take stagger damage. The bar turns white while it drains back down.
- Fallback if playtesting doesn't support it: stagger damage reduces the target's AP directly.

## Enemy targeting

Enemies pick targets by weighing Threat against Vulnerability, and each AI type weights them differently (up to 75% toward one). Threat rises when a hero deals damage or heals. Vulnerability is hidden and rises as Health drops.

Ties between equally scored targets are broken by a pick from the battle's seeded random generator, never a global one.

## Buffs and effects

- Each source can apply a buff only once, unless the buff is explicitly stacking. Source = action plus caster, so 3 different poison spells give 3 poisons, and the same buff from 3 casters stacks 3 times.
- Re-stacking a stacking buff resets its timer.
- Buffs can deal damage or heal on every buff-clock turn, and can grant procs while they last (see Procs).
- Instant heals scale with the caster's Power for the action's tags, like damage.
- All effects queue into one IActionResult. Buffs created by effects are applied only after every effect resolves.
- The same effect from several sources stacks its stats (Critical from many sources). Merely similar effects stay separate (two different poison procs).

## Procs

A proc is an effect that fires on an event, with a chance. Units have procs of their own, and buffs, items and skills grant more. Crit is not a proc: it is a core stat (see Formulas).

| Part | Rule |
| --- | --- |
| Trigger | hit, miss, crit, Brutal, action complete (the owner's actions); struck, avoided, damaged (actions against the owner; damaged counts only damage from actions, to be checked in playtests); turn start, fight start |
| Trigger tags | Optional filter: the event's action must carry at least one of them (Spikey: struck by Melee) |
| Phase | After the hit by default. A hit proc can be marked before damage, to change that hit (e.g. extra Penetrate for this hit only) |
| Chance | Base × (1 + Rate) ÷ (1 + Deval), Rate and Deval both summed (Add), using every tag on the proc; the target's Deval applies only when the proc lands on someone else. No base chance means 100% |
| Above 100% | The chance stops at 100% and the excess is lost: Rate never scales a proc's amounts. Amounts grow only when several copies of the same proc merge (see below) |
| Target | The owner, or the other unit in the event (the target of the owner's action, or the attacker) |
| Results | Building blocks in data: damage, heal, Shield, heal a share of the hit's damage (lifesteal), stat changes for this hit (before damage), and applying any effect or buff (CC, push, buffs) |
| Proc damage | Goes through the full damage formula with the proc's own tags. It is not an action: no hit roll, no crit, and it never triggers procs. Nothing a proc causes triggers further procs |
| Area attacks | Procs roll once per target hit |
| Limits | None on procs themselves. A once-per-fight proc is granted by a buff applied at fight start |

When a unit has the same proc more than once (two Flaming weapons), each proc's duplicates rule decides how the copies combine:

- Merge (the default): one roll. Merged chance = 1 − Π(1 − each copy's chance), the chance that at least one copy fires. Amounts (damage, heal, Shield, lifesteal share, this-hit stat changes) = Σ(chance × amount) ÷ merged chance, so the expected amount is unchanged. States (stun or other CC, push, any buff it applies) come from the strongest copy and never add up. Flaming 100% × 15 + 100% × 20 = 100% × 35; two 20% stuns = 36% to stun.
- Separate: each copy rolls on its own and can fire on the same hit (extra-arrow style procs).
- Unique: only the strongest copy counts (signature effects).

A stacking buff applied by a proc gains one stack per firing. A buff's stacking flag (stacks on the target) is separate from a proc's duplicates rule (copies on the owner).
