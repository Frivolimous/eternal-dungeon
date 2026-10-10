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
| Stealth mastery | The Rogue's first mastery (1 point in its tree; built in until M3A) grants its Move (50 AP, like Move) reaches any empty tile in its area or in the enemy area, and applies Stealth until its next turn. A unit standing in the other side's area melees, and is meleed by, units on adjacent or diagonal tiles there. While in the enemy area, Move can also go to any empty tile in its own area (Jeremy, 2026-10-08) |
| Stealth | +0.5 Avoid and Threatening −100% until the unit's next turn. Using an enemy-targeted action breaks it, hit or miss (Jeremy, 2026-10-08), through the effect's break_on_attack flag. Exception still to build: with 1+ points in Deadly Shadows a crit doesn't break it (skill points arrive in M3) |

## Enemy targeting

| Rule | Placeholder |
| --- | --- |
| Overheal | Counts in full toward the healer's Threat score, to match overkill |
| Starting Threat | The Warrior 20 (so the tank draws the first plans instead of a coin flip); everyone else 0. Gear and skill amounts to come |
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
| Taunt | Removed from the Warrior in M3A (it was a placeholder for the M2 showcase); Imposing Presence gives Starting Threat. The action stays in data, on no unit, to test threat effects |

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

## Dungeon 0 and runs (M3A step 1)

| Rule | Placeholder |
| --- | --- |
| Belt charges | A belt slot holds up to an item's uses (2) of one item; spare charges, found or bought, go to the party pack. One found potion is one charge |
| Starting belts | Warrior and Rogue: 2 Health Potion charges; Elementalist: 2 Mana Potion charges. The Rogue's second slot starts empty (Throwing Spikes fill it) |
| Throwing Spikes | 12 base damage; found as 2 charges at the Goblin Tripwire |
| Alchemist prices | 20 Gold a charge (Health or Mana Potion); the Herbalist's fee is 25 |
| Gold | 10 for small finds, 15 for a beast's hoard, 25 for a boss, 20 to 50 at the end by how much of the family was saved |
| Run buffs and curses | Threadsight +1 Awareness (5 steps); Knotted Muscles −25 Power (3 battles); Sluggish −10 Speed (3 steps); Bleeding 3 damage a turn (3 battles); Rune-Charged +15 Spell Power (3 battles); Mana Drain −3 Mana a turn (2 battles) |
| Event damage | 6 (the campsite snare, a random hero), 8 (the tripwire's logs, plus Bleeding) |
| Rolls | On the [Event guide](event-guide.md)'s scale: pick the merchant's lock Easy (50% + 20% per Disable); spot the crocodile Normal (30% + 20% per Awareness); spot the tripwire Normal, then disarm it Easy (Disable); gambles 50% (2026-10-10) |
| Encounters | Dungeon 0's fights reuse the goblins and add a Wolf, a Bog Crocodile (Tall), a Wild Boar and the Gatekeeper (unit `goblin_captain`; numbers in data). The Gatekeeper fight: Gatekeeper, Grunt and Archer (intimidated: Gatekeeper and Archer). The Chief: Chief, Grunt and Shaman |
| Goblin Camp, Military | Closing in through the guards' blind spot fights the outer patrol with First Strike, then the Chief as usual (Jeremy moved Military here from the Gatekeeper; Claude chose the effect, 2026-10-09) |
| Goblin Camp, the Chief's last words | A story beat after the Chief falls and before the ending, so every ending starts from the same scene |
| Trait definitions | What each of the 13 traits opens, with a "not this" column to stop overlap: [Classes](classes.md) › What each trait opens (2026-10-10) |
| Roll scale | A base by difficulty (Easy 50%, Normal 30%, Hard 10%) plus 20% per trait level ([Event guide](event-guide.md) › Rolls, 2026-10-10) |
| Decision previews | A path shows what it does before it stops: resource changes ("−1 Stamina (everyone)"), buffs and curses with their length (a curse lowers a stat or hurts or drains), Gold, Camp charges, items, a spawned Interactable, a map reveal; then the fight with its scale, Stamina, enemy count and initiative, "more choices", "come back later", or "no fight" when nothing happens. Loot after a fight isn't shown, and flags never are (2026-10-10) |
| A battle that times out | Counts as a flee |
| Flags in the log | Not shown: neither Event flags ("visited") nor Dungeon flags ("merchant_freed"); the story text says what changed. `sim event` lists the Dungeon flags set at the end (2026-10-10) |
| Auto-play (`sim dungeon`) | The first specialist option shown (a trait or class condition), else the first choice (it doesn't rely on choice order); auto-battle; belts filled from the pack; potions bought; a Sanctuary or Camp used when a hero's Stamina is at 0 or the party is below 40% Health, and before a boss when Stamina is 2 or less or Health below 80%; standard Nodes in map order, then deferred Events once something changed, then the boss |

## Progression (M3A step 2)

| Rule | Placeholder |
| --- | --- |
| XP per battle | A won Major fight gives 45 XP, a Boss 90, a Skirmish 15, shared equally among the heroes standing at the end (data: defaults). In Dungeon 0's auto-play, heroes are level 2 at the Gatekeeper and level 4 at the Chief |
| Level curve | Level 2 at 20 XP, then each level takes 10 more than the last: 20, 50, 90, 140, 200… (2090 for level 20; data: levels) |
| First point | A new game's heroes start with their one point unspent; it can be spent before the first Event |
| Maximums from skills | A skill that raises max Health or Mana raises the current value by the same amount |
| Skill numbers | Each skill raises its stats by a fixed amount per level (data: skill_stats). Warrior: Vigor +3 Strength, +8 Health; Battle Might +3 Strength, +0.06 C.Mult; Weapon Mastery +3 Accuracy, +3 Parry; Fortitude +10 Health, +3 Fortification; Imposing Presence +10 Starting Threat, +4 Block. Rogue: Shadow Mastery +3 Accuracy, +0.05 C.Mult; Deadly Shadows +3 Dexterity, +2% Weapon C.Rate; Dancing Shadows +3 Dexterity, +3 Dodge; Opportunist +5 Initiative. Elementalist: Magical Aptitude +3 Magic, +10 Mana; Elemental Affinity +3 Magic, +4 Elemental Power; Mana Conduit +10 Mana; Elemental Attunement +4% Elemental Resist, +0.06 Elemental Deval; Elemental Ward +3% Elemental Avoid. Each skill's special effect is listed separately |
| Masteries | Power Attack as before (×2, 200 AP). Defensive Blow: half damage, then +20% Avoid against melee and +10% against projectiles until the next turn. Colossal Strike: ×4 damage, 250 AP, 1 Stamina paid after the battle. Stealth: the Rogue's Move as before. Quick Attack: half damage, 40 AP. Deadly Precision: a dagger strike with +25% Weapon Hit and +50% Physical Penetrate. Elemental Focus +20% Elemental Multiplier; Elemental Expertise +30% Elemental Rate; Elemental Mastery +20% Elemental Penetrate |
| Auto-play spending | Each point goes into the least-raised skill the hero can raise, earliest in the tree first |
| M2 encounters | Their party rows give each hero its first tree skill, so Power Attack, Stealth and Elemental Focus still show in `sim run` and the showcase |
