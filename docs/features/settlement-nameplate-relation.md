# Settlement Nameplate Relation Colours

## Overview

Campaign-map settlement nameplates (town, castle, village) show the player's relation to the owner
at a glance: the parchment bar takes a light wash, the name text changes colour and the gold
diamond frame is tinted, red for an enemy, green for the player's own faction, blue for an allied
kingdom, untouched for neutral. The same widget makes the bar and frame follow vanilla's plate
transparency (35% neutral, 50% own faction, 80% tracked, plus TAOM's distance fade), and enemy and
allied plates are lifted to the own-faction 50% so a hostile fief is as prominent as the player's.

## Why This Exists

Players reported that TAOM's plates make it hard to tell enemy from friendly, and that vanilla's
plates look different (translucent, colour coded). Both were true and had one cause.

- **Vanilla behavior:** `SettlementNameplateVM.Relation` (0 neutral, 1 same faction, 2 enemy,
  3 allied kingdom; recomputed on war, peace, kingdom change, owner change and alliance events) is
  bound to `SettlementNameplateWidget.RelationType`, whose setter calls
  `SetNameplateRelationType` and writes `NameplateItem.Color` (black, `#245E05FF`, `#870707FF`,
  `#2986CCFF`). Vanilla's prefab puts `Sprite="enemy_town_9"` directly on the
  `SettlementNameplateItemWidget`, so the colour multiplies the visible plate. Every frame
  `UpdateNameplateTransparencyAndBrightness` lerps the item widget's `AlphaFactor` toward
  `DetermineTargetAlphaValue()` (0.35 neutral, 0.5 own, 0.35 enemy and allied, 0.8 tracked, 0
  off-window) and lerps the name text and banner brushes toward full alpha.
- **TAOM requirement:** Thyrell's restyled prefab clones (parchment bar, black text, gold diamond)
  removed the sprite from the item widget and draw everything on descendants. `Widget.Color`,
  `AlphaFactor` and `ColorFactor` only affect a widget's own sprite (`Widget.Render` passes none of
  them to children, v1.4.8 `TaleWorlds.GauntletUI.cs:23451`), so on TAOM's prefab both vanilla
  signals landed on a widget with nothing to draw.
- **Without this feature:** every plate is opaque parchment with black text regardless of relation,
  and the distance fade (Patch38) has no visible target either.

## Architecture

### Design Challenge

The relation int and the per-frame alpha already reach the root widget; nothing in XML can forward
another widget's `AlphaFactor` to child sprites, and vanilla's colour palette (black for neutral)
is wrong for a parchment sprite that `Color` multiplies. The plate has to consume the two signals
itself without any per-frame cost that scales badly across the 863 settlements on the map.

### Solution Approach

A custom Gauntlet widget, `TaomSettlementPlateWidget : Widget`, replaces the plain
`SettlementNameplateLayout` container in all three prefabs. Custom widgets register automatically
(`WidgetInfo.CollectWidgetTypes()` scans every loaded assembly referencing TaleWorlds.GauntletUI,
keyed by simple type name, hence the `Taom` prefix). It binds `RelationType="@Relation"` (a second
binding of the property the root already binds) and references the bar background, name text,
diamond frame and banner by widget path (`Widget.FindChild(BindingPath)` resolves any
`Widget`-typed attribute).

- **Colour:** on a relation change the palette entry is applied: `bar.Color`, `frame.Color`,
  `text.Brush.FontColor` (per-instance brush clone). Neutral is identity white / black text so a
  neutral plate looks exactly as before; unknown ints resolve to neutral.
- **Alpha:** every `OnLateUpdate` the widget reads the ancestor `SettlementNameplateItemWidget`'s
  `AlphaFactor` and `ColorFactor` (written by vanilla's parallel update earlier in the frame) and
  copies them to the bar and frame when they changed. Text and banner get
  `clamp01(plateAlpha / anchor)`, where the anchor is vanilla's 0.35 minimum in-window plate
  alpha, or the plate's own configured resting opacity when a player has set that lower (#596): 1
  whenever the plate sits at its resting value, exactly as vanilla keeps them, and following the
  plate down only under the distance fade so no name floats over the map without its plate. A
  tracked plate, an unknown relation or missing settings keep the vanilla anchor. That write
  repeats whenever the brush disagrees, because vanilla lerps the text toward full alpha every frame. The tracked ring, the party icons under the plate
  and the event icons beside it follow the same text alpha: at close range that is 1, exactly
  vanilla, and under the fade it takes them down with the plate. Without it a far tracked
  settlement (which vanilla never culls) kept an opaque ring after its plate had faded, and the
  icons sat at vanilla's pre-late-update lerp value (Codex review F1).
- **Non-finite destinations:** every alpha write goes through `PlateAlphaPolicy.NeedsWrite`, which
  treats a NaN or infinite value already in the destination as a reason to write; a plain
  tolerance compare against NaN is always false and would never recover it (Codex review F2).
- **Relation floor:** `NameplateRelationAlphaService.Adjust` replaces an untracked in-window
  target with the configured opacity for its relation (defaults: neutral 0.35, the three coloured
  states 0.5, so enemy and allied plates come up from vanilla's 0.35); the Patch38 postfix applies
  it before the distance multiplier.
- Never throws: the whole late update sits in one try/catch (the UI layer is PatchShield-excluded).

### Component Diagram

```
SettlementNameplateVM.Relation (vanilla, event driven)
        |  @Relation
TaomSettlementPlateWidget (prefab container, engine constructed)
        |-- NameplateRelationPalette.Select(relation, 4 XML-overridable entries) -> bar / text / frame colours
        |-- PlateAlphaPolicy.TryTakeChange(item.AlphaFactor, item.ColorFactor) -> bar + frame
        '-- PlateAlphaPolicy.TextAlphaFor(plateAlpha) -> name text + banner brushes

SettlementNameplateWidget.DetermineTargetAlphaValue (vanilla, per frame)
        |  Patch38 postfix
        |-- INameplateRelationAlphaService.Adjust(target, RelationType, IsTracked)   (this feature)
        '-- INameplateFadeService.ComputeAlphaMultiplier(DistanceToCamera)          (SettlementNameplateFade)
```

## Configuration

### MCM: `Map UI / Settlement Nameplates` (#596)

Four controls beside the fade sliders, all applied live (no restart, no map reload):

| Setting | Type | Range | Default | What it does |
|---|---|---|---|---|
| `EnableNameplateRelationColors` (Colour Nameplates by Relation) | bool | | on | Off paints every plate as neutral. |
| `NameplateRelationTintStrength` (Relation Tint Strength %) | float | 0 to 100 | 100 | Blends each palette entry toward identity: 100 is the palette as authored, 0 is parchment and black text for everyone. |
| `NameplateNeutralPlateOpacity` (Neutral Plate Opacity %) | float | 10 to 100 | 35 | Target alpha of an untracked neutral plate; vanilla's 35 can be faint over dark terrain. |
| `NameplateRelationPlateOpacity` (Coloured Plate Opacity %) | float | 10 to 100 | 50 | Target alpha of an own-faction, enemy or allied plate; vanilla gives own faction 50 and the other two 35. |

Tracked plates keep vanilla's 80 (and 100 at the screen edge) whatever the sliders say, and the
distance fade still multiplies the chosen value. An opacity below 35 also moves the text-curve
anchor, so the name, banner, ring and icons stay fully opaque while the plate sits at its chosen
value and follow it down only under the fade. `NameplateRelationSettingsProvider` turns the
percentages into fractions and reverts a non-finite or out-of-range value to the compiled default
with one warning per property in the TAOM log (MCM's slider clamps to the range, but its JSON
loader assigns a hand-edited `TAOM.json` value as is); the alpha service additionally refuses a
non-positive value and leaves vanilla's target alone. The engine-constructed plate widget reads
the toggle and strength through the static `TaomSettlementPlateWidget.Settings`, captured once in
the IoC end block; each plate compares the effective strength against the one it last painted,
once per frame, so a change repaints on the first frame back on the map. MCM writes every slider
move straight into the live settings object (Cancel undoes them), and raises only a save-time
`PropertyChanged` on the settings object (the pattern `AiPartySizeSettingsWatcher` uses), so the
per-frame compare is the mechanism rather than an event. The map does not tick while the options
screen is open. The provider caches `TaomSettings.Instance` on first successful read rather than
in its constructor, because the container is built before MCM has created the instance.

### Prefab attributes (the artist's reference)

The colours themselves are `Color`-typed attributes on the widget, so the artist tunes them in the
prefab without a rebuild; the compiled defaults apply when an attribute is absent, and the MCM
strength slider scales whatever the prefab says. Every value must be exactly `#RRGGBBAA`: `Color.ConvertStringToColor` reads
`Substring(7, 2)`, and a 7-character value throws inside the attribute loader, which catches it per
attribute with a failed assert and keeps the compiled default, so a mistyped override is silently
ignored rather than applied (a test pins the format of every default and of any attribute present;
the loader behaviour was verified by the Codex pass against the installed v1.4.8 DLLs).

### Current Values

| Relation | Bar wash (`*BarColor`) | Name text (`*TextColor`) | Diamond frame (`*FrameColor`) |
|---|---|---|---|
| Neutral | `#FFFFFFFF` | `#000000FF` | `#FFFFFFFF` |
| SameFaction | `#B8DCA0FF` | `#1E5410FF` | `#90E070FF` |
| Enemy | `#F0A090FF` | `#7A0C0CFF` | `#FF7060FF` |
| Ally | `#A8C8F0FF` | `#174C8CFF` | `#80B8FFFF` |

Bar and frame values are multiplied into the sprite, so a pastel keeps the parchment texture and
a saturated value darkens it. These are starting points; the in-game pass with Thyrell decides
the final ones. The plate opacities are the two MCM sliders above; their defaults reproduce
vanilla's own-faction level for every coloured plate.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/SettlementNameplateRelation/TaomSettlementPlateWidget.cs` | The custom container widget: XML properties, reference resolution, never throws |
| `Main/Features/SettlementNameplateRelation/SettlementPlatePresenter.cs` | Finds the item widget, paints the palette, mirrors alpha and colour factor onto the children |
| `Main/Features/SettlementNameplateRelation/NameplateRelationPalette.cs` | Relation int to colours, defaults, unknown resolves to neutral |
| `Main/Features/SettlementNameplateRelation/NameplatePaletteEntry.cs` | Bar / text / frame colour triple |
| `Main/Features/SettlementNameplateRelation/PlateAlphaPolicy.cs` | Per-frame change detector with finite guard; text alpha curve |
| `Main/Features/SettlementNameplateRelation/INameplateRelationAlphaService.cs`, `NameplateRelationAlphaService.cs` | Plate target alpha by relation from the two opacity settings (tracked, zero and NaN untouched) |
| `Main/Features/SettlementNameplateRelation/INameplateRelationSettingsProvider.cs`, `NameplateRelationSettingsProvider.cs` | MCM bridge: percent to fraction, finite and range validation, lazy `TaomSettings.Instance` cache |
| `Main/Features/TaomSettings.cs` | The four `Map UI/Settlement Nameplates` properties (#596) |
| `Main/Features/SettlementNameplateRelation/NameplateRelationIoC.cs` | DryIoc registration |
| `Main/Features/SettlementNameplateFade/Hooks/SettlementNameplateWidget_DetermineTargetAlphaValue_Patch.cs` | Patch38 postfix: relation floor, then distance fade |
| `Main/_Module/GUI/Prefabs/Nameplate/SettlementNameplateItem{Large,Medium,Small}.xml` | The `SettlementNameplateLayout` element is the custom widget |
| `Main/IoC.cs`, `Main/SubModule.cs` | Registration; `Initialize(fade, relationAlpha)` before `Patch38_SettlementNameplateFade` applies |

## Dependencies

- `INameplateFadeService` (SettlementNameplateFade): the distance multiplier the same postfix applies after the floor.
- TaleWorlds: `SettlementNameplateItemWidget` (read `AlphaFactor` / `ColorFactor`), `SettlementNameplateWidget` (`RelationType`, `IsTracked`), `TextWidget.Brush`, `MaskedTextureWidget.Brush`, `Widget.FindChild`.

## Tests

- `TAOM.Tests/Features/SettlementNameplateRelation/NameplateRelationPaletteTests.cs`: 18 tests, every relation, unknown and negative ints, custom entries, `#RRGGBBAA` format of all twelve defaults, neutral is white not vanilla's black, `Blend` (full, zero, midpoint, clamped, non-finite, neutral unchanged) and `EffectiveStrength` (off, clamp, non-finite).
- `NameplateRelationAlphaServiceTests.cs`: 11 tests against a substituted provider: neutral and coloured opacities by relation, custom values above and below vanilla, tracked untouched, zero and NaN pass through, unknown relation unchanged, a non-positive setting leaves vanilla's target.
- `NameplateRelationSettingsProviderTests.cs`: 12 tests, defaults with no MCM instance match the slider defaults and log nothing, percent to fraction, NaN / Infinity / out-of-range revert to the default, and the reversion warning fires once per property (a null logger is tolerated).
- `PlateAlphaPolicyTests.cs`: 19 tests, first call, unchanged, changed, NaN and Infinity refused, text alpha curve, the 0.35 pin, the resting-alpha anchor (below vanilla, at or above, non-finite or non-positive, NaN plate alpha), and `NeedsWrite` (equal, within tolerance, beyond, non-finite destination).
- `SettlementPlatePresenterTests.cs`: 4 tests, the text-curve anchor by relation, tracked, unknown relation and missing settings.
- `NameplateRelationPrefabTests.cs`: 6 tests over the three prefabs, one widget with the right Id and binding, every widget path (bar, text, frame, banner, tracked ring) resolves by Id, colour attributes well formed, the item widget's own paths intact, the item widget within the ancestor depth the widget walks, no state cascade on the capsule.
- `NameplateRelationBindingTests.cs`: 6 tests, engine members pinned against the installed DLLs (`BindingVerification`), widget constructor and simple-name uniqueness.

76 tests in the feature; `dotnet test TAOM.Tests --filter FullyQualifiedName~SettlementNameplateRelation`.
The four MCM properties also move the settings fingerprint pin (`SettingsFingerprintTests`, 229 in
`TaomSettings`) and sit in `CoopSettingsRelevance`'s presentation list.

The widget's render and the postfix body need the live game. In-game checklist: neutral plate
unchanged at 35% with opaque text; own green, enemy red, allied blue at 50%; declaring war
recolours plates without reopening the map; tracking brightens to 80%; zooming out fades bar,
frame, text and banner together; all three sizes identical; hover shows no flicker.

## How to Tune a Colour

1. Open the size's prefab (`SettlementNameplateItemLarge.xml` and siblings) and find the
   `TaomSettlementPlateWidget` element.
2. Add or edit the attribute, e.g. `EnemyBarColor="#F0A090FF"`. Bar and frame are multiplied into
   the sprite; text is the font colour.
3. Restart the game (prefabs load once). No code change; `NameplateRelationPrefabTests` checks the format.

## Performance

`OnLateUpdate` runs for every plate whether visible or not (enrolment is by override detection),
roughly 52,000 calls per second at 60 FPS across 863 plates. After the references resolve, a
settled plate costs a handful of null and float compares (resolve check, dirty check, two item
reads, the change pair, the text-alpha decision, five destination compares) and no allocations;
only a plate whose alpha is moving writes anything, and only a plate inside the fade band walks
its party and event icon lists. The Id fallback for a missing path attribute allocates a child
list per attempt and stops after 120 attempts. The Patch38 postfix adds one int and one bool read
per call to a path already running at ~3000 calls/sec.

## Changelog

- 2026-09-13: fix(map-ui) #596 review follow-up: the text curve anchors on the plate's configured
  resting opacity when that is below 0.35, so a low slider value no longer dims the name at close
  range; an invalid hand-edited setting warns once per property.
- 2026-09-13: feat(map-ui) #596: four MCM controls (colour toggle, tint strength, neutral and
  coloured plate opacity), live, through a validated settings provider and a static on the widget.
- 2026-09-13: feat(map-ui) #591: relation colour on bar, text and frame; bar and frame follow
  vanilla alpha; enemy and allied targets raised to 0.5 in the Patch38 postfix.

## GitHub Issue

- **Issue:** #591 [feat(map-ui): settlement nameplates show relation colour and vanilla transparency](https://github.com/haterade22/TAOM/issues/591)
- **Issue:** #596 [feat(map-ui): MCM controls for settlement nameplate relation colour and plate opacity](https://github.com/haterade22/TAOM/issues/596)
- **Status:** Open (both await the in-game pass)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/settlement-nameplate-fade.md](./settlement-nameplate-fade.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
