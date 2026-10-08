# Commands

Everything you run by hand. Start with **`ed.bat`** in the repo root: double-click it (or type `ed` in a terminal at
the repo root) and pick a number. `ed 2` runs option 2 straight away.

## The menu (`ed.bat`)

| # | Option | When to use it |
| --- | --- | --- |
| 1 | Play the game | Builds the game, then opens it at the debug menu |
| 2 | Pull the Google Sheet into data | After editing the content sheet. Validates everything first and lists what changed; nothing is written if the sheet has a mistake |
| 3 | Push data to the Google Sheet | After data changed here (by Claude, or an import). Keeps your `_` columns and the sheet's row order |
| 4 | Export data to TSV files | Writes every table to `sheets\` as TSV, for any spreadsheet program |
| 5 | Import TSV files into data | Reads `sheets\` back: validates, lists the changes, writes `data\` |
| 6 | Resize art masters into a style | Turns full-size masters in `art-source\<style>\` into the game's exact sizes in `game\assets\styles\<style>\` |
| 7 | Check data and art | Loads and validates all data, checks every art style for wrong sizes and missing portraits |
| 8 | Simulate one battle | Asks for an encounter, a seed and the log detail (brief or full), then prints the battle |
| 9 | Batch-test an encounter | Plays many seeded battles (1000 by default): win rate, battle length, damage, actions used |
| 10 | Play a saved replay in the simulator | Lists the replays saved by the game, then prints the chosen one's log |
| 11 | Run the tests | Every automated test |
| 12 | Export the Windows build | Writes the game to `builds\windows\` (copy the whole folder to another PC) |

The Google Sheet rule of thumb: **pull before** changing data here, **push after**. Both stop and ask before
overwriting edits that exist on only one side (the sheet or `data\`).

## Good to know

- **Art:** name each master by its id from `docs\art-requests.md` (for example `portrait_warrior_hurt.png`), put it in
  `art-source\<style>\`, run option 6. Masters can be any size; the script crops from the centre and resizes. Frames
  need transparency (PNG or WebP). `art-source\` isn't in git, so keep your own copy of the masters.
- **New style:** after its first resize, add it to `game\assets\manifest.json` under `"styles"` (the script reminds
  you), then option 7 checks it.
- **Replays** are saved by the game's Save replay button into
  `%APPDATA%\Godot\app_userdata\Eternal Dungeon\replays\`. Send one with a bug report: it replays the battle exactly.
- **Godot elsewhere:** the scripts expect Godot at `C:\Tools\Godot_v4.7.2-stable_mono_win64\`. If it moves, set the
  `GODOT` environment variable to the console exe's path.

## Less common (type these in a terminal at the repo root)

| Command | What it does |
| --- | --- |
| `dotnet run --project src\Sim -- format-data` | Rewrites `data\*.json` in their standard form, after editing JSON by hand |
| `dotnet run --project src\Sim -- art-requests` | Regenerates `docs\art-requests.md` (after adding units or actions) |
| `dotnet run --project src\Sim -- encounters` | Lists the encounter ids |
| `dotnet run --project src\Sim -- pull-sheets --force` | Pull even though `data\` has changes not on the sheet (they're dropped) |
| `dotnet run --project src\Sim -- push-sheets --force` | Push even though the sheet has edits not in `data\` (they're overwritten) |
| `dotnet run --project src\Sim -- help` | Every simulator command |

**Screenshot mode** plays a battle at instant speed, saves a picture of the screen and quits (handy for checking the
look without playing):

```
& "C:\Tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe" --path game -- --screenshot shot.png --encounter goblin_patrol --seed 42 --actions 6
```

Add `--style ink` for an art style, `--layout side_on` for the side-on board, or `--select-hero` to stop at the next
hero turn with a target preview showing.

Claude keeps this page current whenever a command changes. The full developer list is in `CLAUDE.md`.
