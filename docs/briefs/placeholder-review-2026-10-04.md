# Placeholder review and log fixes (2026-10-04)

Jeremy reviewed every rule in `docs/design/placeholders.md` and the M1 sample logs. This file lists what to
change. Follow `CLAUDE.md` as usual: Anchor and code change in the same commit, tests first for rules, small
commits. When it's all done, delete this file.

## 1. Anchor updates

For each decided rule: move it out of `placeholders.md` into its Anchor section. Kept placeholders stay in
`placeholders.md` with the note given.

| # | Rule | Decision | Goes to |
| --- | --- | --- | --- |
| 1 | Compound stat cap | **Changed.** Compound stats are hard-capped at 100 points. A value that high means a design error, so `sim data` (and the data test) should warn when any unit or content gives a compound stat above 95. Keep the existing safety clamp so a compound contribution to a Dim stat stays at ±0.95 or less. | stats.md › Compound stats |
| 2 | Events at the same moment | Decided as is: buff clock, then casts, then turns. | combat.md › Turn order |
| 3 | Casting and turns | **Changed.** A casting unit's Act meter stops filling while it casts and resumes when the cast completes, fizzles or is interrupted. This avoids the race between the meter and the cast. | combat.md › Turn order |
| 4 | Cast fizzles | **Decided:** a cast fizzles if its target dies, the caster dies, or it's interrupted. No retargeting. | combat.md › Turn order |
| 5 | Both sides fall together | Decided as is: counts as a party wipe. | combat.md (new line under Battlefield or Turn order) |
| 6 | Battle time limit | Keep the placeholder (300 turns, stalemate). Add the note: "If this ever happens in practice, the encounter is badly designed. Investigate rather than tune the limit." | stays in placeholders.md |
| 7 | Damage over time | **Changed:** DoT scales with the caster. When the DoT is applied, snapshot the caster's Power and Multiplier factors for the DoT's tags, and every tick uses them. Shield still absorbs ticks. | combat.md › Crowd control |
| 8 | Instant heals | Decided as is: scale with the caster's Power for the action's tags. | combat.md › Buffs and effects |
| 9 | Fear | **Changed:** a feared unit can only Defend or Move away from the front. No attacks or skills. Different from Stun, which skips the turn. | combat.md › Crowd control |
| 10 | Confusion | Keep the placeholder. Add a WIP note: "Undecided whether confused units can target allies." | stays in placeholders.md |
| 11 | Stagger drain | Keep the placeholder until tuning. | stays |
| 12 | Sneak | Keep the placeholder, tune later. | stays |
| 13 | Push / Pull | Decided as is. | combat.md › Battlefield |
| 14 | Stuck melee units | **Replaced** by the default action list (section 2). Remove the "steps toward the front row, else waits" rule. | combat.md › Battlefield |
| 15–17 | Threat, Threat scale, Vulnerability | Keep the placeholders. Add the note: "Review after M2 playtests." | stays |
| 18 | Targeting ties | **Changed:** ties are broken by a pick from the battle's seeded RNG, never a global one. | combat.md › Enemy targeting |

## 2. Default actions for every unit

Every unit, heroes and enemies, has these actions in addition to its own skills. Put them in data (for example
a `defaultActions` list in `defaults.json`) so they can be tuned without code changes.

| Action | AP | Effect |
| --- | --- | --- |
| Attack | 100 | Basic weapon attack with the unit's weapon tags |
| Defend | 100 | The existing Defend/Guard behavior |
| Move | 50 | One tile within the unit's own area |

- Enemy AI mostly uses Move to step into an empty front tile when it has no valid target. This keeps the
  Anchor's "enemies generally stay put" true and fixes the Goblin Grunt that waits through the whole Chief fight.
- The heroes' scripted AI should use Attack between skills (the Warrior currently idles between 200-AP Power
  Attacks) and Defend when it makes sense.
- Write it into combat.md › Battlefield (or a new "Actions" subsection) as decided, with the AP costs marked
  as placeholders for tuning.

## 3. From the log review

- **Systems showcase encounter.** Add an encounter, plus starter skills if needed, in which every M1 system fires
  at least once in a typical seed: Slow (Frost Shard is missing from the Elementalist's action list), Sneak, Move,
  Defend, a stagger break (the Brute's stagger never fills the bar today), a cast interrupt, a proc, Fear and
  Push/Pull. Add a test that runs it and checks each one appears in the log or batch summary.
- **Damage tags in the log.** Fire Bolt logs as "(Arcane)". Show all damage-relevant tags, for example
  "(Arcane, Fire)".
- **Doc comment.** In `Resolution.cs`, the `<summary>` for `Damage()` sits above `ProcDamage()`, so `ProcDamage`
  has two summaries. Move it.
- After the changes, rerun `sim batch` on all encounters, keep the starter encounters at a 70–90% win rate, and
  refresh `docs/sample-logs/`.
