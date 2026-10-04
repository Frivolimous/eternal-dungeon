# CLAUDE.md

Guidance for Claude Code in this repo.

## What this is

Eternal Dungeon: a roguelike party dungeon crawler with speed-driven turn-based combat on small grids. Premium
(one-time purchase) on Steam, Windows + Steam Deck, no monetization. Claude is the main developer; Jeremy owns
design, playtesting, review and art.

- **Design Anchor** (the spec, single source of truth): [docs/design/](docs/design/README.md), one file per
  area (stats, combat, classes, equipment, dungeons, meta, production), plus
  [placeholders.md](docs/design/placeholders.md) (rules Claude chose, for Jeremy to review),
  [open-questions.md](docs/design/open-questions.md) and [superseded.md](docs/design/superseded.md).
- **Build briefs** (what to build, acceptance criteria): [docs/briefs/](docs/briefs/). M0 + M1 is done; the
  Anchor wins wherever a brief disagrees.

**Keep the Anchor current in the same commit as the code.** When Jeremy settles a design point, write it into
its section; when a decision changes, move the old version to superseded.md with the reason; when Claude picks
a placeholder, add it to placeholders.md. The old claude.ai copies of the Anchor and the brief are retired, as
are the earlier spreadsheets and the ChatGPT chat: never reference them.

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
dotnet run --project src/Sim -- run --encounter goblin_patrol --seed 42 [--log-level full]   # one battle + log
dotnet run --project src/Sim -- batch --encounter goblin_patrol --runs 1000                  # win rate etc.
dotnet run --project src/Sim -- encounters           # list encounter ids
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
data/              JSON content: tags, stats, compound_stats, effects, procs, actions, ai_profiles, units,
                   encounters, defaults (loaded in that order; later files reference earlier ones). Embedded into the
                   game assembly at build.
src/Core/          rules library, plain C#, no Godot
src/Sim/           command-line tool (assembly name `sim`): battle simulator, data and asset checks
docs/design/       the Design Anchor;  docs/briefs/  build briefs;  docs/sample-logs/  simulator output
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

## Where the rules live in code

The rules themselves are in the Anchor; this is the map from rule to code.

- **Stats** (`Core/Stats`): `StatBlock` keeps modifiers per source and recomputes totals (`Combine.Total`);
  `Get(stat, tags)` = untagged + matching tag modifiers + compound contributions; `GetKeyed` leaves out the
  untagged part (Critical Resist etc.). Act is kept in hundredths (`Unit.ActTicks`, 10 000 = Act 100).
- **Formulas** (`Combat/Resolution.cs`): success, damage (`DamageBreakdown`, every factor kept for full
  logs), proc damage, crit chance and rating.
- **Turn order** (`Combat/TurnClock.cs`): jumps straight to the next event (turn, cast completion, buff tick).
- **Battle** (`Combat/Battle.cs` + `BattleProcs.cs`): applies decisions and returns `ActionResult`s. Resolve
  order: hit roll → miss/avoided or before-damage procs → crit → damage → hit/crit/brutal procs →
  struck/damaged procs → action effects → action-complete procs → buffs. Effects queue in order and buffs
  apply last. `FireProcs` is called only from action and clock events, so nothing a proc causes fires procs.
- **Grid** (`BattleGrid.cs`), **AI** (`UnitAi.cs`, profiles in ai_profiles.json; the heroes' profiles are the
  simulator's scripted AI), **runner** (`BattleRunner.cs`), **logs** (`CombatLog.cs`), **batch**
  (`BatchSummary.cs`), **encounters** (`EncounterSetup.cs`).
- `Core.Combat` is the battle namespace (a `Battle` namespace would clash with the `Battle` class).
- **Balance is deferred:** combat balance waits until after M2, dungeon balance until after M3. Until then the
  starter encounters only need to run cleanly (fights chain on a floor, so a single-fight win rate is the wrong
  target). The fourth encounter, `systems_showcase`, exists so every M1 system fires in a typical seed (a test
  checks it). Tests
  read Health and damage from the data, not hard-coded numbers, wherever tuning could change them. The example
  procs (ported from EternalQuestMobile) aren't on any starter unit yet.

## To monitor

- **Is the `weapon` tag needed?** Every weapon attack is exactly one of Melee or Ranged, so Weapon-keyed stats
  (the default 5% Crit Rating, and later equipment) could instead be split between those two. Revisit once
  M4 equipment shows whether anything needs "all weapons" that Melee + Ranged can't express.
- **"Damaged" trigger** fires only when damaged by an action (not by procs or damage over time), for now.
  Check in playtests whether it should fire on all damage.

## Open (don't build without Jeremy)

See [open-questions.md](docs/design/open-questions.md) and the Ideas still open in
[meta.md](docs/design/meta.md).
