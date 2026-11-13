# Circular Battle HUD

## Overview

In a mission the player's health, horse and shield show as concentric full rings in one compact circle
at the bottom right, 160 GUI units across with all three (the size that replaced the 224 unit version
judged too big in game). Each ring fills clockwise from 12 o'clock with its channel's ratio, the rest
showing as track; there are no channel icons. The career ability sits in the centre
disk, which fills from the bottom with the ability's charge and carries the career's glyph and the
player's bound activation key. The native horizontal health, horse and shield bars and the 3D weapon
and shield previews are switched off. The ammo count moves into a small stone capsule hung under the
circle at 6 o'clock, with a leather quiver for a bow or crossbow and a single javelin for javelins.
The lance and brace row, troop count, damage feed and every gameplay rule are unchanged.

## Why This Exists

- **Vanilla behavior:** three horizontal bars, a weapon and shield thumbnail row, and (since #382) a
  separate TAOM career bar stacked above them.
- **TAOM requirement:** one circular composition (#1095): full health, horse and shield rings and
  the career charge in the centre. The first version's athelas, horse and shield icons were removed
  in favour of complete rings (maintainer's decision, 2026-11-08).
- **Without this feature:** the career bar and the native bars compete for the same corner.

## Architecture

### Design Challenge

The installed engine (v1.5.6) has no radial progress widget, and the HUD must not use a child widget
per segment or an image per fill level. The native AgentStatus prefab must keep its root visibility
(`IsCombatUIActive`), its prioritized/passive slide and its event pass-through.

### Solution Approach

- **Injection:** `CareerEnergyBarPrefab` inserts the HUD as the first child of the AgentStatus
  container (same anchor as the #382 bar); its `IsVisible` follows native `ShowAgentHealthBar`.
  `BattleHudNativeSuppressionPrefabs` sets the native bars' `ShowHealthBar` and `IsVisible` to false,
  sizes both weapon previews to zero (the primary keeps its node because the native ammo counter is
  its child), hides that counter (`BattleHudHideNativeAmmoCounter`, `IsVisible` false, node and
  bindings kept) and moves the lance row clear of the circle.
- **Ammo capsule:** the HUD's first child, so the ring widget's backing disk and shell rim draw over
  its top. It is 110 by 26.6 (the art's 4.138 aspect), centred at 6 o'clock with its top
  `BattleHudGeometry.AmmoCapsuleTop(rings)`: the shell of the circle actually drawn, less the 1.1 rim,
  so it tucks under the rim and never covers the health ring, for one, two or three rings. Its number
  is native's own `AgentAmmoTextWidget` bound to `AmmoCount` and `IsAmmoCountAlertEnabled` with the
  `AgentAmmoCount.Text` brush at size 22, so the count and low ammo alert are native. It shows only
  while native `ShowAmmoCount` and `IsAgentStatusPrioritized` (the gate on native's weapon column,
  which held the counter) are both true and the count is 0 or more. `BattleHudAmmoMapper` picks the
  icon from the primary wielded slot's current usage, read fresh each refresh: `Bow` and `Crossbow`
  get `bh_ammo_quiver`, `Javelin` gets `bh_ammo_javelin`, anything else (stones, throwing axes and
  knives) a centred number with no icon. A hidden capsule carries no icon. It sits inside the HUD
  root, so it also follows `ShowAgentHealthBar`. The standalone Gauntlet preview cannot build
  `AgentAmmoTextWidget`, so it shows the number as a `TextWidget` on the same brush with a static red
  for low ammo; the native animated alert has not been checked outside the game.
- **Data:** `MissionAgentStatusBattleHudMixin` reads the numbers the native prefab already binds on
  `MissionAgentStatusVM` (`AgentHealth`, `HorseHealth`, `ShieldHealth`, their maxima,
  `ShowMountHealthBar`, `ShowShieldHealthBar`) and maps them through `BattleHudStateMapper`. A
  missing horse or shield has no ring; present rings pack inward. It needs no campaign,
  so a custom battle shows health. `MissionAgentStatusCareerMixin` feeds the centre through
  `CareerEnergyBarStateMapper` (ready, active and cooldown semantics unchanged) and
  `BattleHudStateMapper.MapCentre`; with no ability the centre is a dark disk with no glyph or key.
- **Drawing:** `TaomBattleHudRingWidget` draws everything in one `OnRender`. Each drawable is a
  fresh `Rectangle2D.Create()` with local position, scale, pivot and rotation in screen pixels and
  `CalculateMatrixFrame` against an invalid parent: the renderer uses the rectangle's cached matrix
  frame, not its public corner fields, so a rectangle copied from `AreaRect` covers the whole widget
  (v1.5.6 IL of `DrawSprite` and `ImageDrawObject.Create`). Arcs are short tangent rectangles; the
  career fill is one disk under `PushScissor`. A fresh material alpha-blends by default (its
  `Blending` is read-only and true), and its nine-patch parameters are cleared, because a sprite's
  nine-slice border would break the thin rims. Each channel is edged inside and out by a fine gold rim;
  the gold leaf sprig (`bh_leaf_sprig`, the right-hand end of the existing `cb_branch.png`) grows from
  the shell at its upper left. At most about 900 quads a frame. The widget draws geometry units times
  the engine's scale, never from its own width, so the markup box (`CareerEnergyBarPrefab`), the glyph
  and the key are sized to `BattleHudGeometry` by hand; `BattleHudGeometryTests` pins both.
- **Ring spacing:** bands are 10.7 units thick on an 11.5 unit pitch, so with all three channels the
  outer edges sit at 53.8 (shield), 65.3 (horse) and 76.8 (health) and the HUD is 160 across. The 0.8
  gap equals the 0.8 channel rim, so neighbouring rims overlap into one gold separator and the
  innermost rim starts on the centre rim (42.3): no dark ring shows between channels. The shell keeps
  its 2.1 dark pad outside the health ring.
- **Full rings:** every ring and both its rims run the whole circle from 12 o'clock. The fill covers
  the channel's ratio clockwise (`FillEndDegrees`: 0 to 360, clamped, NaN empty) and the track the
  rest, so a full channel closes the ring and a partial one leaves its track visible.
- **Stone:** the bands and the career fill overlay the resident `ld_bar_stone` strip on their white
  masks (`SimpleMaterial` overlay, `UseOverlayAlphaAsMask`). Each segment samples only the strip slice
  between its two absolute angles (`BattleHudGeometry.BandTextureU`), with one material per segment,
  so the grain is continuous and does not move with the fill; each ring reads its own third of the
  strip's height, and the centre a fixed square crop. The shader multiplies the stone into the colour,
  so textured paints use `ColorFactor` 7 to stay readable; the leaf and rims keep 1. Without the stone the
  same paints draw flat.
- **Colour option:** the Accessibility dropdown `BattleHudColourMode` (Default, Deuteranopia,
  Protanopia, Tritanopia) recolours only the health, horse and shield rings. MCM persists it by
  index, so the order of `BattleHudColourPalette.Choices` never changes. The mixin reads
  `TaomSettings.Instance` on its first refresh and every 30 refreshes after, holding no settings
  object, and binds the resolved index to the widget's `ColourMode`; no settings, no dropdown or an
  unknown index is Default. Default is a null palette, so the widget draws exactly as before. Each
  other mode draws the three bands with no stone (the flat path, `ColorFactor` 1) in solid paints on
  a dark solid track, with each fill at least 3:1 against the track. Geometry, fill, rims and the
  career centre are the same in every mode, so ring position and length still tell the channels
  apart. The widget reads no settings itself, so the preview can compile it.
- **Images:** the `bh_*` runtime sprites form their own `FrontEndImageGroups.BattleHud` group,
  acquired by `FrontEndMovieService` when the native `MainAgentHUD` movie loads with a
  `MissionAgentStatusVM` (the native movie is kept, only the hold is taken) and released after it
  closes. They are not part of the character-creation group. The stone is an `ld_` image, resident
  for the process once any group loads, so the HUD loads no second copy.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BattleHud/UI/BattleHudStateMapper.cs` | Optional channels, ratios, centre state |
| `Main/Features/BattleHud/UI/BattleHudGeometry.cs` | Radii, packing, fill and texture angles, leaf, centre fill, ammo capsule |
| `Main/Features/BattleHud/UI/BattleHudAmmoMapper.cs` | Ammo capsule visibility, icon and place |
| `Main/Features/BattleHud/UI/BattleHudColourPalette.cs` | Colour option choices, index resolve and the three accessible ring palettes |
| `Client/Features/BattleHud/Widgets/TaomBattleHudRingWidget.cs` | Ring, rim, disk and leaf rendering |
| `Client/Features/BattleHud/UI/MissionAgentStatusBattleHudMixin.cs` | Health, horse, shield and ammo capsule values |
| `Client/Features/BattleHud/UI/BattleHudNativeSuppressionPrefabs.cs` | Native bars, previews and ammo counter off, lance row moved |
| `Client/Features/CareerSystem/UI/CareerEnergyBarPrefab.cs` | The HUD markup injected into AgentStatus |
| `Client/Features/CareerSystem/UI/MissionAgentStatusCareerMixin.cs` | Career centre values |
| `Main/_Module/GUI/FactionUI/RuntimeSprites/bh_*.png` | The leaf sprig, the optional band mask, and `bh_ammo_capsule` (384 wide), `bh_ammo_quiver` and `bh_ammo_javelin` (128 square) |

## Tests

- `TAOM.Tests/Features/BattleHud/BattleHudStateMapperTests.cs`: equipment combinations, missing or
  invalid maxima, NaN, centre with and without an ability or key.
- `TAOM.Tests/Features/BattleHud/BattleHudGeometryTests.cs`: the 160 unit diameter, readable band
  thickness, band and rim spacing, packing for every equipment mix, full-circle rings, fill ratios
  (clamped, NaN empty), stone U once round by absolute angle, and the injected markup's box, margins,
  glyph and key against the geometry.
- `TAOM.Tests/Features/BattleHud/BattleHudAmmoMapperTests.cs`: the icon for every weapon class,
  visibility from native `ShowAmmoCount` and the count (0 shown, -1 hidden), no lingering icon, the
  number's place with and without an icon, and the capsule tucked under the rim for every ring count.
- `TAOM.Tests/Features/BattleHud/BattleHudAmmoMarkupTests.cs`: the capsule markup (sprites, sizes,
  order, `DoNotAcceptEvents`, native bindings and brush), that every binding resolves, that the mixin
  holds no agent or mission handle, and that the native hide patch matches the installed counter once
  and never the HUD's own (the installed-file check is `LiveInstall`).
- `TAOM.Tests/Features/BattleHud/BattleHudColourPaletteTests.cs`: choice order, index resolve with
  unknown and null indices, the exact palettes, fill against track contrast, the MCM attributes and
  hint scope, the mixin's read of a changed or replaced dropdown, and the binding from the mixin
  through the prefab to the widget, which bypasses the stone only for an accessible palette.
- `FrontEndSpriteServiceTests` and `FrontEndMovieServiceTests` (Battle HUD sections): the group's
  load, hold and release, and the `MainAgentHUD` hold.

## How to verify in game

**Preconditions:** a campaign hero with a career, a horse and a shield in the inventory; a custom battle.

1. Start a field battle on foot with a sword and shield: health and shield rings, no horse ring, each
   full ring closed and a damaged one showing its track. Seen by a person only.
2. Mount: the horse ring appears between health and shield. Dismount: it goes.
3. Drop or break the shield: its ring goes.
4. Press the activation key shown in the centre: the disk turns bright and drains, then refills dim
   from the bottom and turns gold when ready.
5. Wield a bow: the capsule hangs under the circle with the quiver and the arrow count, its top
   under the gold rim; shoot down to the low count and it shows the native alert. Switch to a
   sword: the capsule goes. Wield javelins: the javelin icon; throwing axes: a number with no icon.
   Mount and dismount: the capsule follows the circle's size. No native counter shows anywhere.
   Couch a lance: the lance icon shows left of the circle.
6. Custom battle: health shows, the centre is dark with no glyph or key.
7. Mounted with a shield, open Esc, Mod Options, TAOM, Accessibility, Battle HUD Colours. Pick each
   choice and return to the battle: within about half a second the three rings change to flat
   colours on a dark track, with the centre, rims, leaf and ammo capsule unchanged. Pick Default:
   the stone rings come back as in step 1.

## Changelog

- 2026-11-08: replaces the #382 horizontal career bar and the native bars with the circular HUD (#1095).
- 2026-11-08: revised after in-game review: 160 units instead of 224, stone texture on the bands and
  career fill, each band starting next to its own icon. The ammo column and lance row moved in to suit.
- 2026-11-08: the channel icons removed; health, horse and shield are now full rings from 12 o'clock.
- 2026-11-10: the dark gaps between rings closed (bands 10.7 thick, 0.8 gap, same outer edges and
  size), and the health red made richer.
- 2026-11-10: the ammo count moved into a stone capsule under the circle, with a quiver icon for
  bows and crossbows and a javelin icon for javelins; the native counter is hidden.
- 2026-11-12: a Battle HUD Colours option under Accessibility draws the health, horse and shield
  rings in one of three flat palettes for red-green or blue-yellow colour vision. Default is unchanged.

## GitHub Issue

- **Issue:** #1095: [circular battle HUD](https://github.com/haterade22/TAOM/issues/1095)
- **Status:** Open
