# Placeholders

Rules Claude chose where the Anchor was silent and Jeremy said a placeholder was fine. Each is in the code
today and can be overruled at any time; once Jeremy decides one, it moves into its proper section and out of
this page. Numbers that live in data (unit stats, action damage) are tuned by the simulator and aren't listed.

## Combat

| Rule | Placeholder |
| --- | --- |
| Battle time limit | 300 base-speed turns, then a stalemate (no winner). If this ever happens in practice, the encounter is badly designed: investigate rather than tune the limit |
| Fear | Skips the unit's turns (later: flee or only defend) |
| Confusion | Picks its target at random among the action's valid targets. WIP: undecided whether confused units can target allies |
| Stagger drain | 10 per buff-clock turn. Kept until tuning |

## Battlefield

| Rule | Placeholder |
| --- | --- |
| Sneak | Kept; tune later. Moves the Rogue to any empty tile in the enemy area. A unit standing in the other side's area can melee anyone there, and anyone there can melee it |
| Stuck melee units | A unit with no valid action steps toward the front row if it can Move, else it waits (100 AP) |

## Enemy targeting

Threat, its scale and Vulnerability: review after M2 playtests.

| Rule | Placeholder |
| --- | --- |
| Threat | Damage dealt + healing done in this battle, plus the Threat stat (Cloak −50) |
| Threat vs. Vulnerability scale | Threat is divided by the highest Threat among the possible targets, so both are 0–1 |
| Vulnerability | The share of Health missing, plus the Vulnerability stat ÷ 100 |
