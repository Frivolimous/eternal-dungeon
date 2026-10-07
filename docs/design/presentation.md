# Presentation

How battles look and feel on screen (decided for M2, 2026-10-04). Rules live in [Combat](combat.md); this page is
only how they're shown. Choices Claude made beyond these are under Presentation in [Placeholders](placeholders.md).

## The table

- The battlefield is a **3D table seen from above**, with each unit as a flat 2D card. All "3D" is card transforms:
  lift, tilt, flip, shake, shadow.
- **Layouts:** where the areas sit on the table is presentation only; the rules are front-relative, so a fight plays
  the same in any layout. **Vertical** (default): party at the bottom, enemies at the top, front rows facing.
  **Side-on**: party left, enemies right. Each encounter names its layout.
- **Cards:** the face shows the portrait, name, HP bar with numbers, Shield, the Act meter along one edge (a cast's
  progress while casting), the stagger bar (white while broken) and status icons. Images hold no text: everything
  written on a card comes from the engine.
- **Card size follows footprint:** a single card for size 1, a double card over 2 tiles for size 1.5, a large card
  over a 2×2 block for size 2. Portraits are square for sizes 1 and 2 and 1:2 for size 1.5. When a size-1.5 card lies
  across the screen (side-on), only its portrait turns 90°; the card's frame and text stay upright.
- **Portrait states:** optional portraits for low HP, hurt, attacking, casting and knocked out; only the default is
  required, and a missing state falls back to it. States may later be animations, with no code changes.
- **Death:** the card flips face down and stays on the board.

## Screen layout

Designed at **1280×800** (Steam Deck's native 16:10) and scaled up for larger screens; 16:9 adds side space.

- **Centre:** the board. **Left edge:** the turn-order timeline, upcoming turns as small portraits that slide into
  their new order after each action. **Right edge:** the details panel for the selected or hovered unit above a
  scrolling combat log. **Bottom:** the action bar for the active hero.
- **Action bar:** actions are buttons styled as small card frames (icon, name, AP cost, Mana cost, cast time), not a
  hand of cards. Unusable actions show disabled, with the reason.

## A hero's turn

1. The active hero's card lifts and glows, and the action bar shows its actions.
2. Hovering an action shows a **ghost marker** on the timeline where the hero's next turn would land (and where a
   cast completes). It's an estimate at the hero's current Speed.
3. Picking an action highlights valid targets (or empty tiles for Move). Hovering a target shows the **preview**: hit
   chance, damage on a normal hit, a crit and a Brutal crit with their chances, effects it would apply, and procs that
   could trigger with their chances. Damage is static (no damage roll), so the numbers are exact; procs aren't in them.
4. Click to confirm; right-click or Escape steps back. Enemy turns play automatically.
5. **Heroes the player can't fully control:** Stunned or Sleeping heroes lose the turn (shown briefly). Feared heroes
   can only Defend or Move away from the front. A Confused hero's player picks the action, and the target is random.

The combat log uses the same text as the simulator's brief log, with every damage tag. Previews never roll anything,
so looking can't change a battle.

## Feedback

- Attacks lunge and snap back; ranged attacks and spells send a projectile. The target shakes and tilts.
- Damage numbers float up: bigger for crits, a distinct style for Brutal. A miss shows "Miss" and the target sidesteps.
- Casting shows on the card's meter, then the effect travels to the target. Statuses are icons on the card; Stun
  tilts the card, and a stagger break flashes the bar white.
- **Pacing:** about 0.5 s per enemy action at 1×. Speeds 1×, 2× and near-instant; a click skips the current animation.

## Input, text and accessibility

- Mouse first, with keyboard: 1–9 pick an action, Tab cycles targets, Space or Enter confirms, Escape cancels, and one
  key cycles battle speed. Everything runs on focus, so a controller can drive it later by mapping buttons; nothing
  depends on hover alone.
- All UI text lives in string tables (data/strings.csv, Godot's translation format), never in scenes or code.
- Statuses use shape plus colour, never colour alone. There is a text-size setting.

## Art

- Art is drawn at **2× display size** at the 1280×800 reference, so it stays sharp on larger screens.
- Each candidate **style** is a folder of images with the same ids and sizes (docs/art-requests.md lists them,
  generated from the data). Missing art falls back to a generated placeholder card, never a crash.
