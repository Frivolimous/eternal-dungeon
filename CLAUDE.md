# CLAUDE.md

Guidance for Claude Code in this repo.

## What this is

Eternal Dungeon: a roguelike party dungeon crawler with speed-driven turn-based combat on small grids. Premium
(one-time purchase) on Steam, Windows + Steam Deck, no monetization. Claude is the main developer; Jeremy owns
design, playtesting, review and art.

- **Design Anchor** (the spec, single source of truth):
  https://claude.ai/code/artifact/9de2c81d-a4b9-4fff-94fb-f0dff1a53720
- **M0 + M1 Build Brief** (what to build first, acceptance criteria):
  https://claude.ai/code/artifact/dabc031f-ab84-4bec-9180-d79209a06613

Both are Claude Docs: read them with the Claude Docs connector, not a web fetch. The Anchor wins over the
brief if they disagree. Earlier spreadsheets and the ChatGPT chat are retired; never reference them.

Milestones: M0 Foundations → M1 Rules core → M2 Playable battle → M3 Dungeon 0 slice (go/no-go) → M4 Town &
build depth → M5 Content & endgame → M6 Steam.

## Pinned versions

| Tool | Version | Where |
|---|---|---|
| Godot (.NET edition) | 4.7.2 stable mono | `C:\Tools\Godot_v4.7.2-stable_mono_win64\` (use the `_console.exe` from scripts) |
| .NET SDK | 10.0.401 (pinned in `global.json`, rolls forward to later 10.0.x) | |
| Target framework | net8.0 for every project (Godot 4.7's default) | |
| Tests | xUnit v3 on Microsoft.Testing.Platform | |

Set `$env:GODOT` to override the Godot path in scripts.

## Commands

```bash
dotnet test                                          # all tests, from the repo root
dotnet build                                         # everything, including the Godot project
dotnet run --project src/Sim -- data                 # load and validate data/*.json
dotnet run --project src/Sim -- assets               # check the asset manifest, list AI placeholders
dotnet test --filter-method "*Bad_enum*"             # one test by name (wildcards allowed)
powershell -ExecutionPolicy Bypass -File tools/build.ps1   # export Windows + Linux builds into builds/
```

Running Godot headless (from the repo root, `$g` = the console exe):
`& $g --headless --path game --import` after adding assets, `& $g --headless --path game --quit-after 30` to
run the main scene and see its prints. Exports need the 4.7.2 .NET export templates
(Editor > Manage Export Templates).

**Git: work and commit on `master`.** Never create a branch unless Jeremy asks.

## Layout

```
data/              JSON content (tags, stats; more in M1). Embedded into the game assembly at build.
src/Core/          rules library, plain C#, no Godot
src/Sim/           command-line tool (assembly name `sim`): data/asset checks now, battle simulator in M1
tests/Core.Tests/  xUnit tests for Core
game/              Godot .NET project: presentation and input only
  assets/manifest.json   every image, its size and its AI-placeholder flag
tools/build.ps1    export script
builds/            export output (not committed)
```

## Architecture

**Core is pure.** No Godot, no I/O beyond reading data files. A test fails if Core references Godot. Godot code
only displays state and sends input.

**Data over code.** Content lives in `data/*.json`, read by `DataLoader` through a `DataSource` (a folder for
Sim and tests, embedded resources for the game, an in-memory set for tests). Reading goes through `JsonField`,
which is strict (wrong types, missing fields and unknown fields all fail) and throws `DataException` naming
the file and field path, e.g. `stats.json [4].combine: expected one of add, dim, mult, got "sum"`. Ids are
lowercase snake_case and unique per file; enums are written in snake_case. Every reference between files is
checked on load. When you add a file: a `Read…` function in DataLoader, a definition record, and tests for its
errors.

**Asset pipeline.** Every image loads through `game/assets/manifest.json` (id, path relative to
`game/assets`, width, height, `aiPlaceholder`, optional note), so final art drops in without code changes.
`sim assets` lists every flagged asset and fails on missing files or wrong PNG/SVG sizes; a test runs the same
check. AI-generated art is for placeholders only and must be flagged; nothing flagged may ship.

## Working agreement (from the brief)

- **Spec first.** Every rule traces back to an Anchor section. If the Anchor is silent or ambiguous, ask
  Jeremy rather than inventing a mechanic. Numbers marked as placeholders may be chosen freely: keep them in
  data where possible and note them in code.
- **Tests first for rules.** Each formula, combine mode or turn-order case gets a unit test with hand-checked
  numbers.
- **Deterministic.** All randomness goes through one seeded RNG that is passed in, never a global one. Same
  seed and data, same combat log.
- **Small, reviewable commits**, each doing one thing. After each M1 system lands, run the full test suite and
  a simulator battle.
- Keep this file current: versions, commands, rules.

## Decided rules worth remembering

- Power factor = 1 + Power / 100; Multiplier factor = 1 + Multiplier.
- Speed, Act and AP are integers on a 100 scale; chance stats are 0–1 doubles and Dim values stay below 1.
- Tenacity's "Control Duration −1" is parked: implement only its Control Deval part.
- Dim: positive and negative modifiers stack separately (each by the Dim formula) and the negative total is
  subtracted; the result can go below 0. Modifiers must be strictly between −1 and 1.
- Compound stats: every recipe row whose tag the action carries counts, adjusters included (Strength gives a
  Heavy action +0.5× even without Melee). Points are percentages: on a chance stat, 1 point = 0.01.
  Placeholder: one compound adds at most ±0.95 to a chance stat per action.
- Heavy and Light are melee-only tags: only melee actions carry them.
- `all_damage` (Add, factor 1 + value) and `all_resist` (Dim) are untagged stats for the damage formula.

- Damage = Base × (1 + Power/100) × (1 + Multiplier) × (1 − Resist × (1 − Penetrate)) × (1 + All Damage) ×
  (1 − All Resist). Base = the action's base damage + the attacker's Base Dmg stat for its tags. An action's
  `allDamage` (Power Attack: 1.0 = ×2) adds to the attacker's All Damage. Damage rounds to the nearest whole
  number, minimum 1 on a successful damaging hit.

- Turn order (TurnClock): sub-ticks of 1/100 turn; Act is kept in hundredths (`ActTicks`, 10 000 = Act 100).
  Same-tick events: buff tick, then cast completions, then turns. A casting unit keeps gaining Act but takes no
  turn until its cast completes or is interrupted (choices of Claude's, not in the Anchor).

## To do at the end of M1 (Jeremy asked)

- **Procs, crit included.** Crit is not a separate roll: it will be one of several procs (damage, buffs,
  special effects) that can apply to any attack. Design them with Jeremy after the other M1 systems. Until
  then there is no crit; the proc-chance formula exists on its own.

## Open (don't build without Jeremy)

Economy; crafted items' minimum dungeon; weight-penalty and room-count placeholders; Hard Mode, global skill
tree, class leveling, one town per dungeon, cosmetic DLC.
