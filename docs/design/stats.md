# Stat system

Every action carries a set of tags. A character stat keyed to a tag boosts any action with that tag. For example, Fire Power 25 adds 25 Power to every action tagged Fire. Every source of power (class, equipment, Blessings, Artifacts, Boons, Trinkets) feeds this same pool.

A tag stat is written TAG STAT value, and it combines the same way as its base stat (see Combine modes). Every matching tag on an action applies.

- **Fire Power 50:** adds 50 Power to any action tagged Fire (Power uses Add).
- **Ranged Hit 0.5:** combines 0.5 into Hit for any action tagged Ranged (Hit uses Dim).

## Stat types

| Group | Stats |
| --- | --- |
| Character (untagged) | Health, H.Regen, Mana, M.Regen, Speed, Initiative, Threat, Vulnerability (hidden), All Damage, All Resist |
| Attack (tag-keyed) | Base Dmg, Power, Multiplier, Penetrate, Hit, Rate, Crit Rating, Crit Mult |
| Defense (tag-keyed) | Resist, Avoid, Devaluation (Deval) |
| Vitals | Health (from skills and armor), Mana (from skills and some gear), Action (from Speed), Shield (from blocking, actions and spells) |

Attack and defense stats pair up: Penetrate works against Resist, Hit against Avoid, and Rate against Deval.

## Tags

- **Primary damage types:** Arcane, Chemical, Physical, Spirit.
- **Delivery:** Melee, Ranged, Projectile, Grenade, Spell, AoE, Gadget.
- **Style:** Light, Heavy, Finesse, Cryptic, Mystic.
- **Function:** Force, Meta, Buff, Curse, Control, Critical.
- **Elements:** Fire, Electric, Ice, Holy, Dark, Toxic.
- **Source:** Weapon (every weapon attack). To monitor: every weapon attack is also Melee or Ranged, so Weapon may turn out unnecessary.

## Compound stats

A compound stat converts into tag stats at a coefficient: 1 means full value, and +0.5 or −0.5 adjusts it. For example, Strength gives Heavy melee 1.5× Power and Light melee 0.5×, while Dexterity adds +0.5× to Light, so light weapons split between the two.

Every recipe row whose tag the action carries counts, adjusters included: Strength 10 gives a Melee Heavy action 10 × (1 + 0.5) = 15 Power, and a Heavy action without Melee 5. Heavy and Light are melee-only tags, so only melee actions carry them. Compound points are percentages, like Power: on a chance stat 1 point is 0.01, so Accuracy 10 adds 0.10 Hit to a melee action. Placeholder: one compound stat adds at most 0.95 to a chance stat on one action.

| Compound stat | Tag → effect |
| --- | --- |
| Strength | Melee Power 1 · Light −0.5 · Heavy +0.5 |
| Dexterity | Ranged Power 1 · Light +0.5 · Finesse +0.5 |
| Intellect | Gadget Power 1 and Rate 1 · Cryptic Power +0.5 and Rate +0.5 |
| Magic | Spell Power 1 · Mystic +0.5 |
| Elemental | Fire, Electric and Ice Power 1 each |
| Accuracy | Melee Hit 1 · Projectile Hit 1 · Grenade Hit +0.5 |
| Parry | Melee Avoid 1 |
| Block | Melee Avoid 1 · Projectile Avoid +0.5 |
| Dodge | Projectile Avoid 1 · Grenade +0.5 · Melee +0.5 |
| Turn | Spell Avoid 1 |
| Tenacity | Control Deval 1 · Control Duration −1* |
| Fortification | Physical Resist 1 · Critical Resist 1 · Critical Deval 1 |

## Combine modes

Each stat combines in one of three ways. Each removal is the exact inverse of its addition, so expiring buffs leave no rounding drift.

| Mode | Increase | Decrease |
| --- | --- | --- |
| Add | Old + New | Old − New |
| Dim (never reaches 100%) | 1 − (1 − Old)(1 − New) | 1 − (1 − Old) / (1 − New) |
| Mult | Old × New | Old / New |

Which stats use each mode:

| Mode | Stats |
| --- | --- |
| Add | Health, H.Regen, Mana, M.Regen, Speed, Initiative, Threat, Vulnerability, Base Dmg, Power, Multiplier |
| Mult | None for now. Reserved for rare, build-defining effects. |
| Dim | Penetrate, Hit, Rate, Resist, Avoid, Deval |

Negative Dim modifiers (debuffs such as a curse giving −0.2 Avoid) stack separately from positive ones, each by the Dim formula, and the negative total is subtracted: Avoid +0.5 and +0.2 give 0.6, two −0.2 curses give 0.36, so Avoid is 0.24. The result can go below 0. Every Dim modifier stays strictly between −1 and 1.

Two untagged stats feed the damage formula: All Damage (Add; factor 1 + All Damage, like Multiplier) and All Resist (Dim).
