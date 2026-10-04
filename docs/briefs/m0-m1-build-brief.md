# Eternal Dungeon — M0 + M1 Build Brief

2026-10-03 · Jeremy. Moved here from claude.ai on 2026-10-04; the claude.ai doc is retired.

This brief is kept as written. Some of it has since been decided differently (crit is a core stat, not a proc;
procs, damage and Power details): the Design Anchor is current and wins wherever they differ.

## Goal and scope

By the end of M1, Eternal Dungeon's combat rules run as a tested C# library, and a command-line simulator can fight a 3-hero party against goblins and print a readable combat log. Nothing is drawn on screen yet, apart from an empty Godot project that builds and launches.

The design spec is the [Design Anchor](../design/README.md). This brief says what to build first and how to tell it's done.

**In scope**

- **M0 Foundations:** tooling, repo layout, Godot .NET project, rules library, test project, data loading, builds, and the project instructions file for Claude.
- **M1 Rules core:** stats, tags, combine modes, compound stats, actions, the hit, damage and proc formulas, turn order, the effect queue, buffs, CC and stagger, the battle grid, enemy targeting, plus the battle simulator.

**Out of scope (later milestones)**

- Any battle screen, UI or art (M2).
- Dungeon floors, events, XP, levels, skill trees, loot and saving (M3).
- Town, Tavern, equipment, Blessings, Boons, Trinkets, races and talents (M4).
- Steam integration (M6).

## Working agreement

Claude writes the code. Jeremy makes design calls, reviews and playtests. The Anchor wins over this brief if they ever disagree.

- **Spec first:** every rule traces back to a section of the Anchor. If the Anchor is silent or ambiguous, Claude asks rather than inventing a mechanic. Numbers marked as placeholders can be chosen freely and noted in the code.
- **Tests first for rules:** each rule (a formula, a combine mode, a turn-order case) gets a unit test with hand-checked numbers before or alongside its code.
- **Deterministic:** all randomness goes through one seeded random number generator that's passed in, never a global one. The same seed and data always produce the same combat log.
- **No Godot in the rules:** the rules library never references Godot. Godot code only displays the state and sends input.
- **Data over code:** classes, actions, enemies and tags live in data files, so adding content doesn't mean changing code.
- **Small, reviewable steps:** work in commits that each do one thing. After each M1 system lands, run the full test suite and a simulator battle.
- **Project instructions file:** a `CLAUDE.md` at the repo root holds these rules, the pinned tool versions, build and test commands, and a link to the Anchor. Claude keeps it current.

## M0 Foundations

M0 gives Claude a project where it can build, test and run everything from the command line on Jeremy's Windows PC.

### Tools (Jeremy installs)

- Godot 4, latest stable **.NET** edition. Record the exact version in `CLAUDE.md`.
- The .NET SDK version that Godot release requires.
- Git, VS Code with the C# Dev Kit extension, and Claude Code.
- Suggested location: a new folder next to the other projects, such as `C:\JS Projects\EternalDungeon`.

### Repo layout

```
EternalDungeon/
  CLAUDE.md               project rules, versions, commands, link to the Anchor
  EternalDungeon.sln
  src/
    Core/                 rules library (plain C#, no Godot)
    Sim/                  command-line battle simulator
  tests/
    Core.Tests/           xUnit tests for the rules
  data/                   JSON content: tags, stats, actions, units
  game/                   Godot .NET project (presentation and input only)
    assets/manifest.json  every image, with size and an AI-placeholder flag
  builds/                 export output (not committed)
```

### Deliverables

1. The solution with Core, Sim, Core.Tests and the Godot project. Core is referenced by both Sim and the Godot project.
2. A data loader in Core that reads `data/*.json` and fails with a clear message naming the file and field when data is invalid.
3. One passing placeholder test, and `dotnet test` running from the repo root.
4. A Godot scene that launches and shows a title label, proving the Godot project can call into Core.
5. Windows and Linux export presets, with one command (a script) that produces both builds.
6. The asset manifest format, with the AI-placeholder flag, and a check that lists every flagged asset.
7. A Git repo with a `.gitignore` for Godot and .NET. Optionally a private GitHub repo with a workflow that runs the tests on every push.

## M1 Rules core

Build the systems in this order. Each one is testable on its own before the next starts. Section names in brackets point to the Anchor.

1. **Stats and combine modes** [Stat system › Combine modes]
    - Each stat has a combine mode: Add, Dim or Mult, as listed in the Anchor's table.
    - Store modifiers per source and recompute totals from the list. This gives the same result as the Anchor's add/remove formulas, without floating-point drift.
    - Speed, Act and AP are integers. Chance stats (Hit, Avoid, Rate, Deval, Resist, Penetrate) are 0–1 doubles, and Dim values must stay below 1.
2. **Tags and tag stats** [Stat system › Tags]
    - A tag stat is (tag, stat, value), written like "Fire Power 50". Untagged stats apply to every action.
    - An action's effective stat = the untagged value combined with every tag stat whose tag the action carries, using that stat's combine mode.
3. **Compound stats** [Stat system › Compound stats]
    - Each compound stat is a recipe of (tag, stat, coefficient) rows, loaded from data.
    - Contribution to an action = compound value × the sum of coefficients for recipe tags the action carries. For example, Strength 10 on a Melee + Heavy action gives 10 × (1 + 0.5) = 15 Power.
    - Tenacity's "Control Duration −1" is parked. Implement only its Control Deval part.
4. **Units and vitals** [Stat system › Stat types]
    - Health, Mana, Shield (absorbs damage before Health), Act, and the hidden Threat and Vulnerability.
    - A unit is dead at 0 Health. A battle ends when one side has no living units.
5. **Actions** [Combat]
    - An action has tags, AP cost, Mana cost, range, base damage, optional cast time, and a list of effects.
    - AP is spent from the action meter: 50, 100 or 200 (200 acts as a cooldown).
6. **Resolution formulas** [Combat › Formulas]
    - Success = Hit × (1 − Avoid). Roll once per target.
    - Damage = Base × Power factor × Multiplier factor × (1 − Resist × (1 − Penetrate)) × All-damage × (1 − All-resist). Power factor = 1 + Power / 100, and Multiplier factor = 1 + Multiplier.
    - Proc chance = Base × (1 + Rate) × (1 − Deval). Critical is a proc effect, not a separate roll.
7. **Turn order** [Combat › Turn order]
    - Act starts at Initiative and gains Speed every tick. A unit acts at 100 or more, and the action's AP cost is subtracted. Act can exceed 100 (extra turns) or drop below 0 (delays).
    - Use integer sub-ticks so units with different Speeds interleave correctly. Break ties by higher Act, then higher Speed, then a fixed unit order.
    - Buffs, Regen and the stagger bar run on a separate clock equal to Speed 100.
    - Casting: a spell with a cast time takes effect when its timer completes, and can be interrupted.
8. **Effects and buffs** [Combat › Buffs and effects]
    - Triggers: on being hit (with a state check), on turn start, on action complete (with a state check), periodic.
    - All effects queue into one IActionResult. Buffs created by effects apply only after every effect has resolved.
    - A buff is unique per source (action + caster) unless it's marked stacking. Re-applying a stacking buff resets its timer.
    - The same effect from several sources stacks its stats. Similar but different effects stay separate.
9. **Crowd control** [Combat › Crowd control]
    - Implement: Slow, Stun, Root, Silence, damage over time, delayed damage, stat reduction, Push/Pull/Move.
    - Stagger bar: stagger damage fills it, and it drains each buff-clock turn. Above 0 the unit's Speed is halved. At 100 the unit is stunned, can't take stagger, and the bar drains back down.
    - Action overrides (Confusion, Fear, Sleep) and tile-targeted CC: define the effect types, but simple behavior is enough for M1.
10. **Battle grid** [Combat › Battlefield]
    - Each side has its own area, 3×2 by default. At most 6 units per side.
    - Enemy footprints: size 1 = 1 tile, 1.5 = 2 tiles in a column, 2 = a 2×2 block.
    - Heroes can move within their area as an action. When an area's front row empties, the area collapses forward.
11. **Enemy targeting** [Combat › Enemy targeting]
    - Score each target as w × Threat + (1 − w) × Vulnerability, where w comes from the AI profile (at most 0.75 either way).
    - Threat rises with damage dealt and healing done. Vulnerability rises as Health drops.
    - Hero actions in the simulator use a simple scripted AI, since there's no player input yet.

## Data files

All content lives in JSON under `data/`, loaded by Core and validated on load. Ids are lowercase snake_case strings, and every reference between files is checked.

| File | Contents |
| --- | --- |
| `tags.json` | Every tag and its group (damage type, delivery, style, function, element) |
| `stats.json` | Every stat, its group and its combine mode |
| `compound_stats.json` | Recipes: compound stat → (tag, stat, coefficient) rows |
| `actions.json` | Actions: tags, AP and Mana cost, range, base damage, cast time, effects |
| `effects.json` | Reusable effects and buffs: trigger, duration, stacking, stat changes, CC type |
| `units.json` | Heroes and enemies: base stats, compound stats, action list, size, AI profile |
| `ai_profiles.json` | Targeting weights and simple action-choice rules |
| `encounters.json` | Fixed encounters: enemy units and their grid positions, for example goblin_patrol |

Example action entry (field names are Claude's to finalize):

```json
{
  "id": "power_attack",
  "name": "Power Attack",
  "tags": ["physical", "melee", "heavy"],
  "apCost": 100,
  "range": "melee",
  "baseDamage": 20,
  "multiplier": 1.0,
  "effects": []
}
```

### Starter content for M1

Just enough to exercise every system. Numbers are placeholders.

- **Heroes (the Dungeon 0 party):**
    - Warrior: Attack, Defend, Move, and Power Attack (×2 damage, Melee Heavy).
    - Rogue: dagger Attack (Melee Light Finesse), Defend, Move, and Sneak (moves into enemy tiles and cloaks).
    - Elementalist: Fire Bolt (Spell Ranged Fire, with a cast time), Frost Shard (Ice, applies Slow), Defend, Move.
- **Enemies (Goblin Woods):**
    - Goblin Grunt: plain melee.
    - Goblin Archer: ranged Projectile.
    - Goblin Shaman: heals, and applies a damage over time.
    - Goblin Brute: size 1.5, deals stagger damage.
    - Goblin Chief: size 2, the boss, with a stacking buff.
- **One fixed encounter:** the 3 heroes vs. 2 Grunts, 1 Archer and 1 Shaman. A second fixed encounter: the 3 heroes vs. 1 Brute, 1 Grunt and 1 Archer. **One boss encounter:** the 3 heroes vs. the Chief and 2 Grunts.

## Battle simulator

The simulator (`src/Sim`) runs battles from data with no graphics. It's how Jeremy checks the M1 checkpoint and how Claude tests balance later.

### Commands

- `sim run --encounter goblin_patrol --seed 42`: one battle with a full combat log.
- `sim batch --encounter goblin_patrol --runs 1000`: many seeded battles, printing a summary.
- `--log-level brief|full`: brief shows actions and results; full adds every roll, modifier and stat breakdown.

### Combat log (brief)

```
[T 3.40] Warrior → Power Attack → Goblin Grunt #1
         hit (82%) · 34 dmg (Physical) · Grunt HP 46 → 12
[T 3.75] Goblin Shaman → Rotting Hex → Rogue
         hit (90%) · applies Rot (DoT 4/turn, 3 turns)
[T 4.00] Elementalist begins casting Fire Bolt (ready at T 4.50)
```

`T` is battle time in base-speed turns. In full mode, each damage line expands to show base, Power factor, Multiplier factor, resist and penetrate, so any number can be checked by hand.

### Batch summary

Win rate, average battle length in turns, average and minimum party HP left, damage dealt per unit, and how often each action, proc and CC effect fired.

## Acceptance criteria

M0 and M1 are done when every box below is ticked, and Jeremy has passed both checkpoints.

### M0

- [ ] `dotnet test` passes from the repo root on Jeremy's PC.
- [ ] The Godot project opens, launches and shows a label with text that comes from Core.
- [ ] One command produces Windows and Linux builds, and the Windows build runs.
- [ ] Invalid data fails to load with a message naming the file and field.
- [ ] The asset check lists every image flagged as an AI placeholder.
- [ ] `CLAUDE.md` lists versions, commands, project rules and the Anchor link.
- [ ] **Checkpoint:** Jeremy confirms it builds and runs.

### M1

- [ ] Unit tests cover every combine mode (adding and removing modifiers), tag stat matching, every compound-stat recipe, and the hit, damage and proc formulas, using hand-checked numbers.
- [ ] Turn-order tests: units with different Speeds interleave correctly, extra turns, negative Act, a 200-AP cooldown, and cast timers.
- [ ] Effect tests: queued resolution, buffs applied after resolution, uniqueness per source, stacking refresh, and same-effect vs. similar-effect stacking.
- [ ] CC tests: each implemented CC type, plus the stagger bar's slow, stun, white phase and drain.
- [ ] Grid tests: footprints of 1, 2 and 4 tiles, range checks, movement, and the front-row collapse.
- [ ] The same seed always produces an identical combat log (a test runs a battle twice and compares).
- [ ] All three starter encounters run to completion in the simulator without errors, and `sim batch` prints its summary.
- [ ] Core has no Godot references (checked by a test or a build rule).
- [ ] **Checkpoint:** Jeremy reads sample brief and full logs for each encounter and confirms the numbers make sense.

## Assumptions and placeholders

Claude uses these placeholders as written, keeps them in data where possible, and changes them once tuning starts.

### Decided

- **Power scales damage as a percentage:** Power factor = 1 + total Power / 100, so Fire Power 50 means +50% damage on Fire actions.
- **Multiplier works the same way:** factor = 1 + total Multiplier, so 0.2 means +20%.

### Placeholders

| Item | Placeholder |
| --- | --- |
| Base Hit / base Avoid | 0.95 / 0.05 |
| Critical proc | 5% base chance; each stack adds +50% damage |
| Melee range | From the front row, targets the enemy front row only |
| Ranged and spell range | Any enemy tile |
| Reach (spear) | Melee that can also hit the second row |
| Move | 50 AP to move one tile within the party area |
| Defend | 100 AP; +0.3 Avoid and a Shield of 10% max Health until the unit's next turn |
| Stagger bar drain | 10 per buff-clock turn |
| Tick resolution | 1/100 of a turn |
| Starting Act | Initiative value, 0–100 |
| Enemy AI weight w | Grunt 0.5, Archer 0.25, Shaman 0.75, Brute 0.5, Chief 0.6 |

All stat values for heroes and enemies are Claude's to pick so the starter encounters are winnable but not trivial (target win rate around 70–90% in `sim batch`).
