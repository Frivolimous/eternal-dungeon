# Data tables

Every file here is one flat table: a JSON list of rows, one row per line. JSON is the source of truth. For
spreadsheets, `sim export-tsv <folder>` writes one TSV per table and `sim import-tsv <folder>` reads them back:
it validates everything, prints what changed, and writes nothing if anything is invalid. In a TSV, columns
starting with `_` and files that aren't tables are ignored, so calculations can sit beside the data.

Rules for every table:

- Column names are the TSV headers. An empty cell (or a field left out of the JSON) means the column's default.
- Lists of plain ids sit in one cell: `["weapon", "melee"]` in JSON, `weapon, melee` in a TSV.
- Lists of records are **child tables** whose first column is the parent's id. Where order matters, an `order`
  column sets it, so sorting a sheet never changes the game.
- Numbers use a `.` decimal point. Booleans are `true`/`false` in JSON and `TRUE`/`FALSE` in a TSV.
- No comments in the JSON: notes go in a row's `note` column (top-level tables) or here.
- Files are always written in one canonical form. After editing JSON by hand, run `sim format-data`.
- All numbers are placeholders until tuning (combat balance after M2).

## The Google Sheet

The content sheet (address in `tools/google-sheet.json`) has one tab per table, named like the table, plus a
`strings` tab for `strings.csv`. `sim pull-sheets` (or `tools/pull-sheets.bat`) reads it and imports it exactly like
`import-tsv`: everything is validated, the changes are printed, and nothing is written if anything is invalid.
`sim push-sheets` (or `tools/push-sheets.bat`) writes data/ to the sheet, creating missing tabs.

- **What a push keeps.** Rows are matched by their key, so they keep their place: sort the tabs however you like.
  New rows go after the row before them in the data. Cells that already hold the right value aren't touched, so a
  formula that produces the right value survives. Columns starting with `_` (or with no header), and other tabs, are
  never touched. A formula in a data column whose value changes is overwritten, so keep longer calculations on
  separate workshop tabs.
- **Safety.** Each sync records what both sides held (`sheets/last-sync/`, not in git). A push stops if the sheet
  has edits that haven't been pulled, and a pull stops if data/ has changes that haven't been pushed. Both list what
  they found; `--force` (or answering Y in the .bat) goes ahead anyway.
- **Values, not display.** The sync reads cell values, so number formats (%, decimals) don't matter. Numbers are
  read to 15 significant digits.
- **The sheet's side** is a small Apps Script web app, `tools/sheets-sync.gs`. Its setup steps are at the top of
  that file. It runs as the sheet's owner, so the sheet itself doesn't need to be shared, but anyone with the web app
  URL can read and edit the sheet.

## Text

`strings.csv` holds every piece of text the game shows, by key, in Godot's CSV translation format (a `keys`
column, then one column per language). The combat log is built from it, and the game registers the same file with
Godot's translation system, so the simulator and the game print the same lines. Placeholders are named:
`{actor} → {action}`. A missing key shows as `[key]`. Content names (units, actions, effects) stay in the tables.

## Tables

| Table | One row is | Notes |
| --- | --- | --- |
| `tags` | A tag (Fire, Melee, Spell…) | `group`: damage_type, delivery, style, function, element, source or family. `implies`: tags every action and proc with this tag also carries (Fire, Electric and Ice imply Elemental); implied tags can't imply others. Proc trigger filters don't get them |
| `stats` | A stat | `combine`: add, dim or mult. `point_value`: what one compound point adds (Power 1, others 0.01). `base`: added after the modifiers combine, wherever the stat is read (Hit 1: everyone hits 100% by default, and Hit modifiers start from 0) |
| `compound_stats` | A compound stat (Strength…) | Its recipe is in `compound_stat_rows` |
| `compound_stat_rows` | One recipe row | Points × `coef` go to that tag's stat |
| `buffs` | A buff: a timed bundle on a unit | `duration`: until_next_turn, `turns` or `actions` (the holder's own actions), with `length` for turns and actions. `max_stacks`: above 1 the buff stacks (each application by the same source adds a stack, up to that many); default 1. `procs`: procs it grants while it lasts. What it does is up to 3 pairs `key_1`/`value_1` to `key_3`/`value_3`: `shield` (a Shield worth that share of max Health, gone when the buff ends), `periodic_damage` and `periodic_heal` (every buff-clock turn), `delayed_damage` (when it runs its course) take a number; `cc` takes stun, root, silence, sleep, fear or confusion; `break_on_attack` (it ends when its holder attacks) takes true or nothing. Stat changes are in `buff_stats` |
| `buff_stats` | A stat a buff changes | `tag` is optional |
| `procs` | A proc: when something happens, with a chance, do up to 3 things | `chance`: the base chance (default 1 = 100%). `tags`: its own, for Rate, Deval and scaling. `ignore_deval`: true for a proc whose chance the target's Deval doesn't lower (stagger procs, which Force Deval resists through their amount). Results are `key_1`/`value_1` to `key_3`/`value_3`: `damage`, `heal`, `shield`, `lifesteal` (a share of the hit's damage) and `stagger` (Act knocked back, resisted by Force Deval) take a number; `threat` (adds to the target's Threat score; a taunt targets `self`) takes a number; `interrupt` (cancels a cast) takes true or nothing; `displace` takes push or pull; `apply_buff` takes a buff id. Amounts add up only across merged copies; a buff never adds up. An action's own results are procs too (see `actions`). The example procs (Flaming…) are ported from EternalQuestMobile and aren't on any starter unit yet |
| `actions` | An action | `ap_cost`: any whole number above 0 (100 = a full turn). `cast_time` is on the 100 scale: 50 = half a base-speed turn. `procs`: what it does besides its own damage, in order: procs that fire for the user on this action's hit, miss, crit, brutal or action_complete (only enemy actions roll to hit, so the others use action_complete). `move_to`: own (Move) or own_or_enemy (also any empty tile in the enemy area) for tile actions. `replaces`: attack, defend or move: for a unit that has this action, it takes that default's place (the Rogue's Move, from the Stealth mastery) |
| `ai_profiles` | How a unit picks actions and targets | `threat_weight` w: targets score w × scaled threat + (1 − w) × Vulnerability, between 0.25 and 0.75, and the pick is weighted by score² (see design/combat.md › Enemy targeting). Hero profiles are the simulator's scripted AI |
| `ai_rules` | One rule, in `order` | The first usable rule with a valid target wins. `to_enemy_area`: a move that can enter the enemy area goes there. `ally_health_below`: on an ally action, picks an ally that hurt; on a self action, used only while some other ally is that hurt. Enemies check their rules when they plan their next turn, not when they act. With no rule usable, a unit Attacks, else steps toward the front, else Defends |
| `units` | A hero or enemy | `size` 1, 1.5 or 2. Then one column per stat (untagged values) and one per compound stat. Stat values add to the default stats (Health 50 + the unit's 90 = 140), so a unit can leave Health empty |
| `unit_tag_stats` | A tag-keyed stat on a unit | Such as Fire Power 50 |
| `encounters` | A fixed battle | `layout`: vertical (default) or side_on; presentation only |
| `encounter_units` | A unit in an encounter, per `side` in `order` | `row` 0 is the front (front-relative); Tall and Large units anchor at their front-left tile. Each area is 3 columns × 2 rows |
| `defaults` | A setting (`key`, `value`) | `attack_action`, `defend_action`, `move_action`: every unit's default actions (Fear allows only Defend and Move) |
| `default_stats` | A stat every unit starts with | Each row is an ordinary modifier: a unit's own value combines with it by the stat's mode (a default Avoid 0.05 and a unit's 0.3 give 0.335 by Dim). Only `base` in `stats` offsets a stat |
