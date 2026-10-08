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
\text{Crit} = 1 + \text{tiers} \times \text{C.Mult} \times \big(1 - \text{CritResist} \times (1 - \text{CritPen})\big)
```

```latex
\text{Proc} = \text{Base} \times (1 + \sum\text{Rate}) \div (1 + \sum\text{Deval})
```

Hit is 100% by default: its modifiers combine from 0 (Dim, like every chance stat) and the total is 1 + that. Bonuses push Hit above 100%, curses below. Success is capped at 100%, so Hit above 100% only cancels Avoid: Hit 120% against Avoid 30% succeeds 1.2 × 0.7 = 84% (Jeremy, 2026-10-08).

Crit is a core stat, not a proc. C.Rate (Add, tag-keyed, hard cap 200%) converts into a per-hit crit chance c = (√(1 + 4 × C.Rate) − 1) / 2. A crit rolls again at the same chance for a Brutal crit, so expected damage rises in a straight line with C.Rate. Each tier adds C.Mult (Add, tag-keyed): Damage × (1 + tiers × C.Mult × (1 − Critical Resist × (1 − Critical Pen))). The target's Critical Deval lowers C.Rate before the conversion: C.Rate ÷ (1 + Critical Deval), then the 200% cap. Critical Resist and Critical Pen count only their Critical-keyed parts, since the untagged parts already applied to the hit. Crit rolls only on a successful hit. The character sheet shows C.Rate with both chances as shares of all hits: C.Rate 100% gives a 62% crit chance, and 38% of hits are Brutal (62% of crits).

The crit stats are always written **C.Rate** and **C.Mult**, never spelled out: under the TAG STAT convention "Critical Rate" would mean the Rate stat on Critical-tagged actions (like Critical Resist, Critical Deval and Critical Pen), which is a different thing.

Power and Multiplier scale damage as percentages. Power factor = 1 + total Power / 100, so Fire Power 50 means +50% damage on Fire actions. Multiplier factor = 1 + total Multiplier, so 0.2 means +20%.

Base is the action's base damage plus the attacker's Base Dmg stat for the action's tags. An action's own damage bonus goes into All Damage: Power Attack carries All Damage 1.0, so it deals ×2, and a +0.2 Multiplier ring stacks on top as a separate ×1.2. Damage rounds to the nearest whole number, with a minimum of 1 on a successful damaging hit.

Every character starts with Weapon C.Rate 5% and untagged C.Mult 0.5, so every weapon can crit (×1.5, Brutal ×2.0), and any action that gains C.Rate can crit without needing its own C.Mult. Procs are designed separately: effects with a trigger and a chance, which can trigger on crit and on Brutal.

## Turn order

- Each unit starts combat with Act = Initiative and gains Speed every tick. Speed 100 is the base.
- At 100 Act, it's that unit's turn. Act can go above 100, giving extra turns, or below 0, delaying the turn.
- Actions cost AP out of the 100-point meter, any whole number above 0: 50 is half a turn, 100 a full turn, and 200 works like a cooldown (any cost allowed, Jeremy, 2026-10-08).
- There are no cooldowns: an action’s AP cost is the only limit on how often it can be used (a 200-AP action delays the unit’s next turn).
- Spells have a casting timer. The Arcanist's Meddle staggers and interrupts an enemy while it's casting.
- A unit's Act meter stops filling while it casts, and resumes when the cast completes, fizzles or is interrupted.
- A cast fizzles if its target dies, the caster dies, or it's interrupted. There is no retargeting.
- Buffs run on their own clock at base Speed 100, so a 3-turn buff lasts 3 base-speed turns whatever the buffed hero's own Speed. Speed, Act and AP are integers on the 100 scale. Chance stats are fractions shown as percentages: Hit, Avoid, Resist and Pen use Dim and generally stay between 0 and 1 (curses are the exception: they can push them below 0), while Rate and Deval use Add, so they can exceed 1, and only adjust proc chance through (1 + Rate) ÷ (1 + Deval).
- Events at the same moment resolve in a fixed order: the buff clock first, then completed casts, then turns.
- If both sides fall at the same moment, it counts as a party wipe.
- **No real-time clock.** Ticks are simulated instantly until the next unit reaches 100 Act, and the game waits for the player on each hero turn. A quick animation shows the meters filling. Every "over time" effect (buffs, damage or healing over time) counts ticks, not seconds. There is no passive Health or Mana regeneration: healing or Mana over time exists only as effects, like damage over time.

## Battlefield

- The battlefield is a set of **areas** joined by **fronts**. A front is where an edge of one side's area faces an edge of the other side's. Every rule is relative to a front: a tile's row is its depth from the front edge (row 0 is the front row), and its column runs along that edge. Where areas sit on the table and which way they face is presentation only (see presentation.md), so the same fight plays identically in any layout.
- M2 has one front per area. Ambushed and Surrounding battles (M3) add more fronts; range already counts every front between two areas, while Push/Pull, footprints and collapse for multi-front areas are decided in M3.
- Each side has its own area, 3×2 or 2×3 by default. Abilities can grow it to 4×2 or 3×3.
- At most 6 units are on the map: the 4 party heroes plus overflow from mercenaries and pets.
- Heroes move freely within their area, and enemies generally stay put. Every action has a Range.
- **Melee reach:** from the front row, a melee action reaches the enemy's front row in the same lane or the next lane over: straight ahead or diagonal, never further along the line (decided 2026-10-08). Reach weapons also hit the second row, with the same lanes. A unit covering several lanes (size 2) reaches from, and can be reached in, each of them. Inside one area (a Rogue among the enemies), melee reaches adjacent and diagonal tiles only. Ranged attacks and spells reach any tile.
- Rogues on either side can enter the opposing area: with the Stealth mastery (the Rogue's first), Move can also go to any empty tile in the enemy area, and every Move grants Stealth for 1 turn (less Threat). This replaces the old Sneak action. While a unit stands in the enemy area, its Move can also go to any empty tile in its own area, so it can come back (decided 2026-10-08).
- Enemies usually appear in front. In an Ambushed battle they appear on all sides, and in a Surrounding battle the party flanks them.
- When none of an area's own units is left in its front row, its own units step forward until one is (the forward collapse). Units of the other side standing in it (a Rogue that moved in) don't count and don't step with them: each keeps its tile if it's still free, otherwise it goes to the front-most free tile there (nearest its lane), otherwise to the front-most free tile of its own area, otherwise its own area gains a new back row for it (decided 2026-10-08). Some actions reposition enemies.
- Push and Pull move a unit one row back or forward within its area, only if the tiles are free.
- Enemy size is its footprint: size 1 takes 1 tile (a person), size 1.5 takes 2 tiles in a column, front and back (a troll), and size 2 takes a 2×2 block of 4 tiles (a Balrog).

## Default actions

Every unit, heroes and enemies, has three default actions on top of its own skills (the defaults data table, so they can be tuned without code changes). AP costs are placeholders for tuning.

| Action | AP | Effect |
| --- | --- | --- |
| Attack | 100 | Basic weapon attack |
| Defend | 100 | +0.3 Avoid and a Shield of 10% max Health until the unit's next turn |
| Move | 50 | Any empty tile in the area the unit stands in |

A unit with no usable skill (no valid target, or not enough Mana) uses these: it Attacks if it has a valid target, otherwise it Moves to step into an empty front tile if it can, otherwise it sidesteps along its row to a tile from which its melee reaches someone, otherwise it Defends. This keeps "enemies generally stay put" true while stopping melee enemies from waiting behind their own front line.

## Crowd control

CC can target a character or a tile: Slow, Stun, Stagger, Interrupt, Pull/Push/Move, Root, damage over time, delayed damage, stat reduction, action override (Confusion, Fear, Sleep), action restriction (Root, Silence) and conditional effects.

Confusion: the unit loses all choice, heroes included. On its turn it uses a random usable action at a random target it can reach, allies included (or a random tile, or itself), decided 2026-10-08.

Fear: the unit loses all choice, heroes included (decided 2026-10-08): on its turn it Moves to a tile further from the front if it can, otherwise it Defends. It never attacks or uses skills. If it is its side's only unit in the front row, it can't step back (the forward collapse would only pull it straight back), so it Defends (decided 2026-10-08). It can't attack or use skills. Unlike Stun (and Sleep), Fear doesn't skip the turn.

Damage over time scales with the caster: when it's applied, the caster's Power and Multiplier factors for the tags of the proc that applied it are locked in, and every tick uses them. Shield still absorbs ticks.

**Stagger is Act damage** (decided 2026-10-08; it replaced the stagger bar):

- A stagger knocks the target's Act meter back by its amount, so its next turn comes later. Act can go below 0. Nothing lingers: no bar, no slow, no stun.
- Stagger procs carry the Force tag, and the target resists with its Force Deval (untagged Deval counts too): **stagger taken = amount × (1 − Force Deval)**, kept between 0 and the full amount and rounded to a whole Act. So 50% Force Deval halves it and 100% resists it fully. For stagger, Deval is a straight resist; for proc chances it stays a divisor, ÷ (1 + Deval). Both are intended.
- A stagger proc is marked ignore_deval, so Force Deval reduces only its amount, not also its chance (it would count twice otherwise).
- No guardrails for now: stunlocking by stagger is allowed.
- Bosses (the Goblin Chief, and every boss-scale unit) have Force Deval 50%.

**Interrupt** is a separate effect: it cancels the target's cast in progress, and the spell fizzles. It does nothing to a unit that isn't casting, and Deval doesn't resist it for now. Stun interrupts too. Most stagger sources also interrupt (the Brute's Smash does), and so will the Arcanist's Meddle.

## Enemy targeting

Enemies pick targets by weighing Threat against Vulnerability, and each AI type weights them differently (up to 75% toward one). Threat rises when a hero deals damage or heals. Vulnerability is hidden and rises as Health drops.

Ties between equally scored targets are broken by a pick from the battle's seeded random generator, never a global one.

## Buffs

A buff is a timed bundle on a unit: stat changes, crowd control, damage or healing every buff-clock turn, a Shield that goes when it ends, and procs it grants while it lasts. Procs apply buffs; everything that happens once (damage, heals, stagger, interrupts, pushes) is a proc result, not a buff (decided 2026-10-08).

- A buff lasts until the holder's next turn, a number of buff-clock turns, or a number of the holder's own actions. Exploration buffs and curses (timed in steps or battles, see Exploration) will share the buffs table with their own durations.
- A buff lasting actions counts each action its holder finishes. One applied before damage with 1 action lasts just that hit (Armor Break: extra Penetrate for this hit only). One given by an action counts from the holder's next action.
- Each source can apply a buff only once, unless the buff is explicitly stacking. Source = the action (or the proc, for procs a unit or buff carries) plus the caster, so 3 different poison spells give 3 poisons, and the same buff from 3 casters stacks 3 times.
- Re-stacking a stacking buff resets its timer.
- An action's buffs are applied only after the whole action resolves (its damage and every proc it sets off). Buffs from before-damage procs are the exception: they apply at once, to change the hit.
- The same buff from several sources stacks its stats (Critical from many sources). Merely similar buffs stay separate (two different poison procs).

## Procs

A proc is something that fires on an event, with a chance. Units have procs of their own, buffs grant procs while they last, items and skills will grant more, and **an action's own results are procs too**: Shield Bash's Daze is "on hit: apply Dazed", Defend's Guard is "on action complete, self: apply Guard" (decided 2026-10-08). Crit is not a proc: it is a core stat (see Formulas).

- An action's procs fire only for its user, only on that action's own events: hit, miss, crit, Brutal and action complete. Only enemy-targeted actions roll to hit, so the others use action complete. They roll like any proc, so the target's Deval can resist even a 100% one (Tenacity resists Shield Bash's Daze).
- Every proc carries its own tags, which decide its Rate and Deval and scale its damage, heals and the damage over time of the buffs it applies (Mend's heal uses the Mend proc's tags, not the action's).
- A proc at 100% needs no roll.

| Part | Rule |
| --- | --- |
| Trigger | hit, miss, crit, Brutal, action complete (the owner's actions); struck, avoided, damaged (actions against the owner; damaged counts only damage from actions, to be checked in playtests); turn start, fight start |
| Trigger tags | Optional filter: the event's action must carry at least one of them (Spikey: struck by Melee) |
| Phase | After the hit by default. A hit proc can be marked before damage, to change that hit: it applies a buff lasting 1 action (e.g. extra Penetrate for this hit only) |
| Chance | Base × (1 + Rate) ÷ (1 + Deval), Rate and Deval both summed (Add), using every tag on the proc; the target's Deval applies only when the proc lands on someone else, and not at all on a proc marked ignore_deval (decided 2026-10-08, for stagger procs, which Force Deval resists through their amount). No base chance means 100% |
| Above 100% | The chance stops at 100% and the excess is lost: Rate never scales a proc's amounts. Amounts grow only when several copies of the same proc merge (see below) |
| Target | The owner, or the other unit in the event (the target of the owner's action, or the attacker) |
| Results | Up to 3 per proc, as key and value pairs in data: damage, heal, Shield, heal a share of the hit's damage (lifesteal), stagger (Act knocked back), interrupt (no value), displace (push or pull), and apply buff (stat changes, CC, damage over time…). More pairs can be added if procs need them |
| Proc damage | Goes through the full damage formula with the proc's own tags; heals scale with the owner's Power for the proc's tags. It is not an action: no hit roll, no crit, and it never triggers procs. Nothing a proc causes triggers further procs |
| Area attacks | Procs roll once per target hit |
| Limits | None on procs themselves. A once-per-fight proc is granted by a buff applied at fight start |

When a unit has the same proc more than once (two Flaming weapons), each proc's duplicates rule decides how the copies combine:

- Merge (the default): one roll. Merged chance = 1 − Π(1 − each copy's chance), the chance that at least one copy fires. Amounts (damage, heal, Shield, lifesteal share, stagger) = Σ(chance × amount) ÷ merged chance, so the expected amount is unchanged. States (stun or other CC, interrupt, push, any buff it applies) come from the strongest copy and never add up. Flaming 100% × 15 + 100% × 20 = 100% × 35; two 20% stuns = 36% to stun.
- Separate: each copy rolls on its own and can fire on the same hit (extra-arrow style procs).
- Unique: only the strongest copy counts (signature effects).

A stacking buff applied by a proc gains one stack per firing. A buff's stacking flag (stacks on the target) is separate from a proc's duplicates rule (copies on the owner).
