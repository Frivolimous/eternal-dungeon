# Placeholders

Rules Claude chose where the Anchor was silent and Jeremy said a placeholder was fine. Each is in the code
today and can be overruled at any time; once Jeremy decides one, it moves into its proper section and out of
this page. Numbers that live in data (unit stats, action damage) are tuned by the simulator and aren't listed.

## Stats

| Rule | Placeholder |
| --- | --- |
| Compound stat on a chance stat | One compound stat adds at most ±0.95 to a chance stat on one action |

## Combat

| Rule | Placeholder |
| --- | --- |
| Events at the same moment | Buff-clock turn first, then completed casts, then turns |
| Casting and turns | A casting unit keeps filling its Act meter but takes no turn until its cast completes or is interrupted |
| Cast target gone | A cast whose target fell before it completes fizzles |
| Both sides fall together | Counts as a party wipe |
| Battle time limit | 300 base-speed turns, then a stalemate (no winner) |
| Damage over time | A flat amount per buff-clock turn (× stacks); Shield absorbs it; no damage formula |
| Instant heals | Scale with the caster's Power for the action's tags, like damage |
| Fear | Skips the unit's turns (later: flee or only defend) |
| Confusion | Picks its target at random among the action's valid targets |
| Stagger drain | 10 per buff-clock turn |

## Battlefield

| Rule | Placeholder |
| --- | --- |
| Sneak | Moves the Rogue to any empty tile in the enemy area. A unit standing in the other side's area can melee anyone there, and anyone there can melee it |
| Push / Pull | One row back / forward, only if the tiles are free |
| Stuck melee units | A unit with no valid action steps toward the front row if it can Move, else it waits (100 AP) |

## Enemy targeting

| Rule | Placeholder |
| --- | --- |
| Threat | Damage dealt + healing done in this battle, plus the Threat stat (Cloak −50) |
| Threat vs. Vulnerability scale | Threat is divided by the highest Threat among the possible targets, so both are 0–1 |
| Vulnerability | The share of Health missing, plus the Vulnerability stat ÷ 100 |
| Ties | The first listed target |
