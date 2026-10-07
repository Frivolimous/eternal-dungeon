# Placeholders

Rules Claude chose where the Anchor was silent and Jeremy said a placeholder was fine. Each is in the code
today and can be overruled at any time; once Jeremy decides one, it moves into its proper section and out of
this page. Numbers that live in data (unit stats, action damage) are tuned by the simulator and aren't listed.

## Stats

| Rule | Placeholder |
| --- | --- |
| Single-source cap on Dim stats | One source counts for at most ±0.95 |
| Negative Deval | 1 + Deval is floored at 0.1, so a curse can at most multiply a proc's chance by 10 |

## Combat

| Rule | Placeholder |
| --- | --- |
| Battle time limit | 300 base-speed turns, then a stalemate (no winner). If this ever happens in practice, the encounter is badly designed: investigate rather than tune the limit |
| Confusion | Picks its target at random among the action's valid targets. WIP: undecided whether confused units can target allies |
| Stagger drain | 10 per buff-clock turn. Kept until tuning |
| Stagger break length | A break stuns until the bar drains, about 10 turns (showcase seed 42: the Warrior is out from T 4.15 to T 14.27). Revisit in the combat balance pass after M2: for example a faster drain while white, or a fixed 1–2 turn stun |

## Battlefield

| Rule | Placeholder |
| --- | --- |
| Stealth mastery | Until masteries exist as a system (M3–M4), the Rogue unit has it built in: its Move (50 AP, like Move) reaches a neighbouring tile or any empty tile in the enemy area, and applies Stealth until its next turn. A unit standing in the other side's area can melee anyone there, and anyone there can melee it. There is no way back to its own area yet |
| Stealth | +0.5 Avoid and −50 Threat until the unit's next turn (the old Cloak numbers). Attacking doesn't break it yet; the Rogue tree's "crits don't break Stealth" suggests attacks should, to decide with the skill trees |

## Enemy targeting

Threat, its scale and Vulnerability: kept until after M2, then reviewed from playtests.

| Rule | Placeholder |
| --- | --- |
| Threat | Damage dealt + healing done in this battle, plus the Threat stat (Stealth −50) |
| Threat vs. Vulnerability scale | Threat is divided by the highest Threat among the possible targets, so both are 0–1 |
| Vulnerability | The share of Health missing, plus the Vulnerability stat ÷ 100 |

## Presentation

| Rule | Placeholder |
| --- | --- |
| Camera | Straight down and orthographic (no perspective), so cards show at exactly their design size and text stays sharp. Lift shows as a slightly larger card with its shadow offset |
| Card and tile sizes | Small card 124×140 px on a 136×150 px tile at 1280×800; portraits 110×110 (Tall 110×220, Large 246×246). Art is twice that |
| Enemy lanes | Enemy column 0 is on the left in the vertical layout, as for the party (not mirrored) |
| Timeline | The next 10 turns, assuming each unit keeps its Speed and spends 100 AP a turn |
| Ghost marker | Replaces the hero's next predicted turn on the timeline |
| Statuses on cards | One icon per kind: each crowd-control kind, damage over time, and generic buff and debuff (a buff from an ally is a buff, from an enemy a debuff) |
| Pacing | 0.5 s per action at 1×, 0.25 s at 2×, none at Instant; floating numbers stay at least 0.6 s |
| Keys | S cycles speed, A toggles auto-battle, Space or a click skips an animation |
| Text size | 85%, 100%, 115% or 130% |
| Replays | Saved in the game's user folder as `<date>_<encounter>_<seed>.replay.json`, listed in the debug menu |
