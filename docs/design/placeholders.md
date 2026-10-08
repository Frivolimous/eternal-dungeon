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
| Stagger amounts | Every stagger source knocks back 20 Act (the Brute's Smash, the showcase). Decided 2026-10-08, to tune |
| Boss stagger resist | Bosses have Force Deval 50% (the Goblin Chief), so they take half of any stagger |
| A buff's length | One `length` column counts turns or actions, by the buff's duration (rather than a column per kind) |
| Counting actions | A buff lasting actions counts each action its holder finishes. One applied by the action itself counts from the next action; one applied before damage counts the current one, so 1 action = just that hit |
| Certain procs | A proc at 100% doesn't draw from the battle RNG, and an action's certain procs don't get their own log line (their results do) |
| Stagger as a proc result | An amount: copies that merge add up their stagger, like damage. Interrupt is a state |
| Move and action complete | A tile action (Move) sets off action-complete procs, the unit's own included |

## Battlefield

| Rule | Placeholder |
| --- | --- |
| Stealth mastery | Until masteries exist as a system (M3), the Rogue unit has it built in: its Move (50 AP, like Move) reaches any empty tile in its area or in the enemy area, and applies Stealth until its next turn. A unit standing in the other side's area melees, and is meleed by, units on adjacent or diagonal tiles there. While in the enemy area, Move can also go to any empty tile in its own area (Jeremy, 2026-10-08) |
| Stealth | +0.5 Avoid and Threatening −100% until the unit's next turn. Using an enemy-targeted action breaks it, hit or miss (Jeremy, 2026-10-08), through the effect's break_on_attack flag. Exception still to build: with 1+ points in Deadly Shadows a crit doesn't break it (skill points arrive in M3) |

## Enemy targeting

| Rule | Placeholder |
| --- | --- |
| Overheal | Counts in full toward the healer's Threat score, to match overkill |
| Starting Threat | The Warrior 20 (so the tank draws the first plans instead of a coin flip); everyone else 0. Gear and skill amounts to come |
| Taunt | A Warrior action (50 AP): +60 Threat score and Taunting (Threatening +50%, 2 turns). Its AI uses it while an ally is below 50% Health and it isn't already Taunting. Added so the systems showcase shows threat effects and plans changing; replace when the Defender kit is designed |
| Damage over time and delayed damage | Count toward the buff caster's Threat score |
| Ally Health on a self action | An AI rule's ally_health_below on a self action means "while some other ally is below that share" (used by the Taunt rule) |
| Vulnerability | The lowest current Health among the reachable targets ÷ this one's current Health, plus the Vulnerability stat ÷ 100 |
| Weighted pick | A target's chance is its score squared over the total (the square keeps the favourite the favourite) |

## Exploration and progression (M3A)

| Rule | Placeholder |
| --- | --- |
| Max Stamina | 4 for every hero |
| Camp | 1 charge per dungeon; a Camp Rests and restores 50% Health and Mana |
| Exhaustion | Exhausted: −10 Speed and −10% to that hero's Event rolls. Severe: −25 Speed and −25% |
| Potions | Health and Mana Potions restore 40% of max, cost 50 AP in battle, 2 uses each |
| Throwing Spikes | A Gadget belt item: a ranged physical hit (amount to set), 50 AP, 2 uses |
| Initiative modifiers | Surprised: the party −30 Initiative. First Strike: +30 |
| XP pacing | Heroes reach about Level 4 before Dungeon 0's final boss; later tuning brings the first dungeon down to about Level 3 |
| Steps | One step = one Node explored (for buffs and curses timed in steps) |
| Event damage in Dungeon 0 | Small, since a hero at 0 Health outside battle is Dead at once |
| Shield Bash | Stays as the Warrior's starter-shield action until equipment arrives (M3B); it isn't in his tree |
| Taunt | Removed in M3A (it was a placeholder for the M2 showcase); Imposing Presence gives Starting Threat |

## Presentation

| Rule | Placeholder |
| --- | --- |
| Camera | Straight down and orthographic (no perspective), so cards show at exactly their design size and text stays sharp. Lift shows as a slightly larger card with its shadow offset |
| Card and tile sizes | Small card 124×140 px on a 136×150 px tile at 1280×800; portraits 110×110 (Tall 110×220, Large 246×246). Art is twice that |
| Enemy lanes | Enemy column 0 is on the left in the vertical layout, as for the party (not mirrored) |
| Timeline | The next 10 turns, assuming each unit keeps its Speed and spends 100 AP a turn |
| Ghost marker | Replaces the hero's next predicted turn on the timeline |
| Intents | On each enemy's next turn-order chip, bottom right (Jeremy, 2026-10-08; the timeline is 152 px wide to fit it): the action's icon (its initial until icons exist) and the target's face; "↑" for a Move, "?" while Confused, "…" when it will wait. A changed plan flashes the box gold. The details panel shows an enemy's plan in words and a hero's share of the party's threat |
| Stagger and interrupt feedback | Stagger: "−N Act" floats on the card and rides on the unit's timeline chip as it slides back. Interrupt: the card shakes and "Interrupted" floats (the brief's "cast ring breaking" waits for a cast ring; the cast shows on the card's side meter today) |
| Statuses on cards | One icon per kind: each crowd-control kind, damage over time, and generic buff and debuff (a buff from an ally is a buff, from an enemy a debuff) |
| Pacing | 1 s per action at 0.5×, 0.5 s at 1×, 0.25 s at 2×, none at Instant; floating numbers stay at least 0.6 s. Fine for now (Jeremy, 2026-10-08); revisit when visuals and effects are polished |
| Keys | S cycles speed, A toggles auto-battle, Space or a click skips an animation |
| Text size | 85%, 100%, 115% or 130% |
| Replays | Saved in the game's user folder as `<date>_<encounter>_<seed>.replay.json`, listed in the debug menu |
