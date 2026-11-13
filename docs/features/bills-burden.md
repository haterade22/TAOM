# Bill's Burden

## Overview

A row in the map bar's secondary line shows how full the main party's cargo is, as a percent of
inventory capacity. It turns red above 100%. Hovering it shows the fill, then vanilla's own weight
and capacity breakdowns.

## Why This Exists

- **Vanilla behavior:** the inventory screen shows weight and capacity, but the map bar has no cargo
  indicator, so an overloaded party is only discovered by the speed penalty.
- **TAOM requirement:** a glanceable, read-only fill figure on the map.
- **Without this feature:** players open the inventory to learn whether they can carry more loot.

## Architecture

### Design Challenge

The row has to live in vanilla's `MapInfoVM` without a Harmony patch, and the value moves with more
than the roster: capacity also depends on perks, prisoners, healthy troop count and career passives.

### Solution Approach

A UIExtenderEx mixin (`[ViewModelMixin("Refresh")]`) on `MapInfoVM`, modelled on the special-resource
mixin. It inserts one `MapInfoItemVM` with id `taom_cargo` right after the `troop_wage` item (or at
the end when that item is absent), and sets Value, IntValue and HasWarning on every refresh; the
engine setters skip unchanged values. `MapBarVM.Initialize` runs the first refresh before the view
loads, so the row is there from the first frame. The mixin lives in `TAOM.Client`, because a view-model
mixin cannot load on the dedicated server ([ADR-013](../adrs/013-server-safe-core-client-ui-submodule.md));
the pure calculator stays in core. The client's assembly scan registers the mixin; there is no IoC or
SubModule line.

**Formula:** `percent = round(100 x TotalWeightCarried / InventoryCapacity)`, rounded away from zero
and clamped to 999. Capacity is the engine's int. Negative weight counts as 0. The row shows `-`
when capacity is not above 0 or the weight is not finite, and an invalid fill is never red. The red
warning uses the raw values (`weight > capacity`), so 100.4% shows `100%` and is red. The inventory
screen warns on the same test.

**Armies:** the row shows the main party's own cargo, like the inventory screen. In an army, the
engine's overburdened speed penalty weighs the cargo of every attached party against their combined
capacity, so the row's red state can differ from the army's penalty.

**Flash:** a change in the percent briefly flashes the text green (up) or red (down), as vanilla's
wage row does (`MapTextBrushWithAnim`).

**Why it recomputes every refresh:** no roster stamp covers every input to capacity, and the weight
is already cached by the engine (`MobileParty.TotalWeightCarried`). Vanilla runs its gold
calculation on the same map-bar refresh: every 0.2 s, or 0.1 s in the unstoppable fast-forward used
while holding, waiting or following another lord's army. After a time-button click, the next refresh
waits 2 s. The percent text is formatted each refresh, which also corrects it after a language switch.

**Party access:** the mixin reads `Campaign.Current?.MainParty`, never `MobileParty.MainParty`,
which throws without a campaign.

**Tooltip:** a title row "Inventory fill" with `WEIGHT / CAPACITY (PERCENT)` (WEIGHT in the
inventory screen's `#0` format), a separator, vanilla's
`CampaignUIHelper.GetPartyInventoryWeightTooltip`, a separator, then
`CampaignUIHelper.GetPartyInventoryCapacityTooltip`. A throw logs once per exception type and
returns an empty list.

## Configuration

None, by design: the row is passive and read-only, and the secondary row only shows when the bar is
extended. The shared value widget has only normal and warning colours, so there are no amber bands.

## Icon

The `MapBar.Right.Icons` brush in `Main/_Module/GUI/Brushes/MapBar.xml` has one layer named
`taom_cargo` with sprite `MapBar\taom_cargo` (file `ui_taom/MapBar/taom_cargo.png`, a 64x64 glyph).
The existing icon widget falls through to that brush for every id except the special resource.

**Art provenance:** generated 2026-10-20 with the ImagineArt MCP (model `gpt-image-2.5-flare`, transparent
background, 1024x1024, asset `ebc6d2ee-2a91-4195-8926-e2a278ca91ad`), prompt: "A single flat game UI icon:
a laden traveller's pack sack tied shut with a rope, slightly bulging, seen from the front. Monochrome
ivory parchment colour (#E8DCC0) filled glyph with a thin dark brown outline, minimal interior shading
lines, no text, no background, centered, bold readable silhouette that stays clear when shrunk to 32
pixels. Medieval fantasy style matching a strategy game's map bar icons (coin, food, morale)." Cropped to
its alpha bounds with about 3% margin and resized to 64x64 (Lanczos).

**Owed:** step 5 below. `Main/_Module/GUI/TAOMSpriteData.xml` now declares `MapBar\taom_cargo` (a SpritePart and a GenericSprite entry, landed in commit 9985f2db3 with the repacked banner sheets), so steps 1 to 4 are done as far as the repo shows. `CargoSprite_IsDeclaredInTheTaomSpriteSheet` keeps its `[Ignore]`. Before the sheet declared the sprite, the row drew an empty icon slot, with no error and no log line. The steps:

1. Deploy this change, so the install has the PNG.
2. In the editor, run the sprite generation and the texture compile.
3. Run `pwsh tools/sync_sprite_bake.ps1` from this checkout. If you skip it, the next build overwrites
   the bake with the old repo manifest.
4. Exit and relaunch the game.
5. Remove the `[Ignore]` from `CargoSprite_IsDeclaredInTheTaomSpriteSheet` and run it.

Keep #876 open, and keep this change out of a player release, until step 5 passes.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/BillsBurden/CargoFillCalculator.cs` | Pure percent and warning maths |
| `Client/Features/BillsBurden/UI/CargoMapBarMixin.cs` | The map-bar row and tooltip (`TAOM.Client`) |
| `Main/_Module/GUI/Brushes/MapBar.xml` | The `taom_cargo` icon layer |
| `Main/_Module/GUI/SpriteParts/ui_taom/MapBar/taom_cargo.png` | The icon art (64x64) |
| `Main/_Module/ModuleData/taom_module_strings.xml` | `taom_cargo_tt_fill`, `taom_cargo_tt_fill_value` |

## Dependencies

- UIExtenderEx (mixin host), `IModLogger` (resolved once in the constructor).
- Vanilla `CampaignUIHelper` tooltip helpers for the two breakdowns.

## Tests

- `TAOM.Tests/Features/BillsBurden/CargoFillCalculatorTests.cs`: one test per gate (percent, exact
  100, 100.4 red, 150, midpoint, invalid capacity and weight with no warning, negative weight, 999
  clamp).
- `TAOM.Tests/Features/BillsBurden/CargoMapBarWiringTests.cs`: one brush layer named by the mixin's
  `ItemId`, both vanilla helpers called, `Campaign.Current?.MainParty` and no `MobileParty.MainParty`,
  the insert on the first refresh after only the no-party guard, the `Contains` guard with no one-shot
  flag (#958), plus the ignored sprite pin (SpritePart and GenericSprite).

## Translation

The two strings are translated in all 12 language files (2026-11-12, in the
translation backlog, #763).

## Owed in-game checks

- The icon renders after the bake, and the other `ui_taom` map-bar icons still render.
- The row sits right after the troop wage item, exactly once, also after a save load.
- The value updates after looting, selling and gaining or losing troops (up to 2 s after a speed
  change).
- The row is red when over capacity, and the colour returns to normal below it.
- Tooltip order: fill row, weight breakdown, capacity breakdown. WEIGHT matches the inventory screen.
- In one non-English language, both tooltip rows show the translated text, not a raw key.
- While you lead an army, compare the red state with the speed tooltip's "Overburdened" line.

## Changelog

- 2026-10-20: map-bar cargo fill row added (#876).

## GitHub Issue

- **Issue:** #876: [Map bar shows how full the party's cargo is](https://github.com/haterade22/TAOM/issues/876)
- **Status:** Open
