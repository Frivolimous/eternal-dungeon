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
dotnet run --project src/Sim -- export-tsv sheets    # every data table as a TSV in sheets/ (for spreadsheets)
dotnet run --project src/Sim -- import-tsv sheets    # validate the TSVs, print changes, write data/*.json
dotnet run --project src/Sim -- format-data          # rewrite data/*.json in canonical form (after hand edits)
dotnet run --project src/Sim -- replay fight.replay.json   # play a replay saved by the game, print its log
dotnet run --project src/Sim -- assets               # check the asset manifest and art styles, list AI placeholders
dotnet run --project src/Sim -- art-requests         # rewrite docs/art-requests.md (a test checks it's current)
dotnet test --filter-method "*Bad_enum*"             # one test by name (wildcards allowed)
powershell -ExecutionPolicy Bypass -File tools/build.ps1   # export Windows + Linux builds into builds/
```

Running Godot (from the repo root, `$g` = the console exe; `dotnet build` first so the game assembly is current):
`& $g --path game` plays the game (debug menu). `& $g --headless --path game --import` after adding assets.
Exports need the 4.7.2 .NET export templates (Editor > Manage Export Templates).

**Art masters to a style:** `& $g --headless --path game -- --resize-art <masters folder> <style>` crops each master
(named by its art id, any size) from the centre to the right shape, resizes it (Lanczos) to the exact size in
docs/art-requests.md and writes `game/assets/styles/<style>/<id>.png`. Masters stay outside game/.

**Screenshot mode** checks UI work without anyone at the screen (it opens a window briefly: headless doesn't render):
`& $g --path game -- --screenshot docs/screenshots/x.png --encounter goblin_patrol --seed 42 --actions 6`, plus
optionally `--layout side_on`, `--style <folder>`, `--replay f.replay.json`, `--select-hero` (stop at the next hero
turn and show a target preview), `--keys 1,Tab,Enter` (press keys at that turn, to test keyboard play),
`--save-replay f` and `--save-log f` (the in-game log, to compare with `sim replay f`). Look at the PNG after any
visual change.

**Git: work and commit on `master`.** Never create a branch unless Jeremy asks.

## Layout

```
data/              JSON content, one flat table per file (data/README.md lists them). Embedded into the game
                   assembly at build.
src/Core/          rules library, plain C#, no Godot
src/Sim/           command-line tool (assembly name `sim`): battle simulator, data and asset checks
docs/design/       the Design Anchor;  docs/briefs/  build briefs;  docs/sample-logs/  simulator output
tests/Core.Tests/  xUnit tests for Core
game/              Godot .NET project: presentation and input only
  scripts/Main.cs        app root: data, translations, settings, menu ↔ battle; screenshot mode (ScreenshotJob)
  scripts/BattleUi/      the battle screen: BattleScreen (turn flow, input, animation), BoardView (3D table and
                         layouts), CardView/CardFace (cards), Timeline, DetailsPanel, ActionButton, StatusIcon
  assets/manifest.json   fixed images (size, AI-placeholder flag) and the art styles
  assets/styles/<style>/ one folder per candidate art style, images by ArtCatalog id (docs/art-requests.md)
docs/screenshots/  reference screenshots from screenshot mode
tools/build.ps1    export script
builds/            export output (not committed)
```

## Architecture

**Core is pure.** No Godot, no I/O beyond reading data files. A test fails if Core references Godot. Godot code
only displays state and sends input.

**Data over code, as flat tables.** Content lives in `data/*.json`: each file is one table, a list of flat rows,
so it maps to a spreadsheet tab. Lists of records are child tables (first column = the parent's id, an `order`
column where order matters); plain id lists sit in one cell. `Schemas.cs` defines every table's columns (the
units table gets one column per stat and compound stat); `TableFormat` reads JSON and TSV strictly and writes the
one canonical JSON form (rows one per line, defaults left out), so export then import is byte-exact (a test checks
it, and another that the files are canonical: run `sim format-data` after hand edits). No comments in JSON:
notes go in a `note` column or data/README.md. `DataLoader.Build` validates the tables and builds the
definitions; errors name file, row and column (`stats.json [4].combine: …`, or `units.tsv row 6, health: …`).
`DataExchange` does TSV export/import (validates everything first, prints a change summary, writes nothing on
invalid data). When you add a table or column: its schema in `Schemas.cs` (a new table also goes in
`Schemas.BeforeUnits` or `AfterUnits`), the reading in `DataLoader`, data/README.md, and tests for its errors.

**No hard-coded text.** Every word shown to a player, including the combat log, comes from `data/strings.csv`
(`Strings`, `GameData.Text`; Godot's CSV translation format, named placeholders `{actor}`). Core formats the log
from it; the game registers the same file with Godot's TranslationServer. When you add text, add its key there.

**Game screen (game/scripts/BattleUi).** It drives a Core `BattleSession` one clock event at a time and animates
each result; it reads state, never changes it except through the session. Everything the player points at is a
focusable 2D control laid over the 3D cards (`BattleScreen` keeps them aligned each frame), so mouse, keyboard and a
future controller share one focus, and every hover preview also shows on focus. Text: `Text.T`/`Text.F` (or a
`reason.*` key for Core's reason codes). Watch the `Side` (Core vs Godot) and `Text` (helper vs Button.Text) name
clashes: the BattleUi files alias them.

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
- **Game screen side** (`BattleSession.cs`, `Replay.cs`, `Preview.cs`): a session runs the battle turn by turn,
  pausing on each hero's turn (unless auto-battle), and records every hero decision; seed + decisions = a replay
  that reproduces the battle exactly. AI-made hero decisions are marked `auto` and re-asked on replay, because the
  AI's tie-breaks roll the battle RNG. Previews (target numbers, ghost marker, timeline) are pure: UI code must never
  roll the battle RNG or call the AI just to look (a test checks previews change nothing).
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
