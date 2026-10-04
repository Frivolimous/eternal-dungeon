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

## Tables

| Table | One row is | Notes |
| --- | --- | --- |
| `tags` | A tag (Fire, Melee, Spell…) | `group`: damage_type, delivery, style, function, element or source |
| `stats` | A stat | `combine`: add, dim or mult. `point_value`: what one compound point adds (Power 1, others 0.01) |
| `compound_stats` | A compound stat (Strength…) | Its recipe is in `compound_stat_rows` |
| `compound_stat_rows` | One recipe row | Points × `coef` go to that tag's stat |
| `effects` | An effect or buff | `duration`: instant (default), `turns` (set `turns`), or until_next_turn. `shield_max_health`: a Shield worth that share of max Health; a buff's Shield goes when the buff ends. Stat changes are in `effect_stats` |
| `effect_stats` | A stat a buff changes | `tag` is optional |
| `procs` | A proc | `chance`: the base chance (default 1 = 100%). Amounts (damage, heal, shield, lifesteal, hit stats) add up only across merged copies; an effect never adds up. The example procs are ported from EternalQuestMobile and aren't on any starter unit yet |
| `proc_hit_stats` | A this-hit stat of a before_damage proc | |
| `actions` | An action | `cast_time` is on the 100 scale: 50 = half a base-speed turn. `move_to`: own (Move) or enemy (Sneak) for tile actions |
| `action_effects` | An effect an action applies, in `order` | `on`: target (default) or self |
| `ai_profiles` | How a unit picks actions and targets | `threat_weight` w: targets score w × Threat + (1 − w) × Vulnerability, between 0.25 and 0.75. Hero profiles are the simulator's scripted AI |
| `ai_rules` | One rule, in `order` | The first usable rule with a valid target wins |
| `units` | A hero or enemy | `size` 1, 1.5 or 2. Then one column per stat (untagged values) and one per compound stat. Base Hit 0.95 and Avoid 0.05 are placeholders |
| `unit_tag_stats` | A tag-keyed stat on a unit | Such as Fire Power 50 |
| `encounters` | A fixed battle | `layout`: vertical (default) or side_on; presentation only |
| `encounter_units` | A unit in an encounter, per `side` in `order` | `row` 0 is the front (front-relative); Tall and Large units anchor at their front-left tile. Each area is 3 columns × 2 rows |
| `defaults` | A setting (`key`, `value`) | `attack_action`, `defend_action`, `move_action`: every unit's default actions (Fear allows only Defend and Move) |
| `default_stats` | A stat every unit starts with, before its own | |
