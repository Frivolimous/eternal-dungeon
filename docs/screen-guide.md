# Screen guide

A name for every piece of the game's screens, so notes can point at them ("the passage mark is too small"). Each
section has a reference screenshot in `docs/screenshots/`. Claude keeps this current as screens change.

## Map screen ([run_map.png](screenshots/run_map.png))

| Name | What it is |
| --- | --- |
| **Header** | The strip across the top: Map name, Map counter ("Map 1 / 2"), Gold, Journal button |
| **Journal** | The run log, over the right of the map while open (Journal button toggles it) |
| **Map** | The parchment area in the middle |
| **Region** | One Node's area of the Map, the part nearest it |
| **Border** | The dashed hand-drawn line between two Regions |
| **Passage** | The gap in a border, with three dots through it, where two Regions link |
| **Fog** | The faint grey over Regions not seen yet |
| **Region icon** | The central icon of a seen Region: **?** (unexplored), the Feature (lit, dimmed when complete, faded when seen from afar) |
| **Waiting dot** | The red dot on a Region icon whose Event was deferred |
| **Reveal badge** | The red "!" / "!!" (enemies, boss) or blue "+" (Sanctuary) on a Region seen from afar |
| **Interactable icon** | The small icon beside a Region icon (Sanctuary, Pathway, Alchemist Station); glows while usable |
| **Focus ring** | The cyan ring around the icon the mouse or keyboard is on |
| **Name label** | The small label under the focused icon ("Black Bog (complete)") |
| **Found line** | The line that rises from an empty Region and fades ("Nothing here but quiet.") |
| **Interactable popup** | The small panel listing what can be done at a Region (Use the Sanctuary, buy potions…) |
| **Toast** | The gold line across the top for level-ups and masteries |
| **Effect popup** | The small gold-edged card rising above the footer with what an action changed |

## Footer (on the map, Event and Hero Panel screens)

| Name | What it is |
| --- | --- |
| **Camp button** | Bottom left, "Camp ×N"; opens the **Camp confirm** ("Camp now?") |
| **Hero Panel button** | Under the Camp button |
| **Hero card** | A hero's card as in battle: portrait, name banner, Mana bar, Health bar |
| **Level badge** | "Lv 2" at the top right of a hero card |
| **Points dot** | The red dot with a number at a hero card's top left: unspent skill points |
| **Active edge** | The faint gold edge on the Active Hero's card during an Event; strong with a glow on the hero who'd take the hovered choice |
| **Stamina pip** | The round pip and "2/4" beside a hero card (red with the status when Exhausted) |
| **Action slots** | The 2×2 squares beside a hero card: belt items now (charges in the corner), exploration spells and items from M3B |
| **Options button** | Bottom right; opens **Options** (auto-battle, Debug, Save and quit) |

## Event screen ([run_event.png](screenshots/run_event.png))

| Name | What it is |
| --- | --- |
| **Event dim** | The 80% darkening over the map |
| **Event title** | The Event's name in gold |
| **Banner** | The wide picture under the title |
| **Outcome block** | The small italic lines above the story: what the last choice did |
| **Story text** | The Event's text, on its dark box |
| **Choice line** | A choice as a line of text, gold (white when focused) |
| **Specialist line** | The small grey line under a trait or class choice ("Disable 1 · Rogue") |
| **Continue line** | The gold "Continue" when a block has no choices |
| **Choice popup** | The gold-edged box beside a focused choice: who rolls, then the preview |

## Hero Panel ([run_hero_panel.png](screenshots/run_hero_panel.png))

| Name | What it is |
| --- | --- |
| **Hero side** | The left panel |
| **Portrait**, **XP bar**, **Stamina line**, **Traits list** | The top of the hero side (traits at its top right) |
| **Stats grid** | The two columns of stats (green values: skills raise them) |
| **Tabs** | Skill Tree / Inventory, top of the right panel |
| **Tree title** | "Warrior 3": the class and its tree points |
| **Skill node** | A square in the tree: locked (dark), raisable (plain), maxed (bright), focused (lit) |
| **Tree line** | The elbow lines from a skill to the ones it unlocks |
| **Mastery tiles** | The I, II, III squares beside the tree |
| **Skill tooltip** | The box beside a focused skill: name, description, level, Now, next level |
| **Points line** | "Skill points: N" under the tree |

## Battle screen (`docs/screenshots/` battle shots)

| Name | What it is |
| --- | --- |
| **Turn order** | The column of chips down the left |
| **Turn chip** | One upcoming turn: small portrait, name, time ("now" on the current one) |
| **Intent badge** | An enemy chip's bottom right: its planned action and target |
| **Ghost marker** | The faint gold chip showing where the hovered action puts the hero's next turn ("next turn", "cast lands", "Flees") |
| **Cast chip** | A blue-edged chip at the moment a cast in progress lands ("Flees", "Fire Bolt lands") |
| **Board** | The two 3×2 areas, enemies on top |
| **Card** | A unit: portrait, name banner, status icons, Act meter (left edge, blue while casting), Mana gauge, Health bar |
| **Card mark** | A card's highlight: gold (active hero), green (valid target), white (hovered target) |
| **Action bar** | The row of action buttons along the bottom (number keys; AP cost; hourglass for a cast) |
| **Preview line** | The line above the action bar: the hovered action's numbers |
| **Details panel** | The top right: the hovered unit's stats, plan and buffs |
| **Combat log** | The bottom right |
| **Speed and Auto** | "Speed Instant [S] · Auto on [A]" |
| **End panel** | Victory or defeat, with Continue |

## Other screens

| Name | What it is |
| --- | --- |
| **Menu** | The M3A test build's start screen: New game, Continue, and the single-battle sandbox |
| **Arrival panel** | A new game's first panel: Spend skill points / Set out |
| **End screen** | Slice complete, or the party has fallen: the run's numbers and each hero |
