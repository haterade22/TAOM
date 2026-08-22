# Field Camp

Pitch a visible camp on the campaign map (#506): field and fortified camps with a morale bonus and
grain foraging, terrain-gated ambushes and lookouts, a timed raise with an on-map progress bar, and
authored 3D encampment visuals. Port of the commissioned yotthani FieldCamp module (Bannerlord
1.4.5, behaviourally ported 2026-08-22); provenance:
[provenance-register.md](../reference/provenance-register.md) "Yotthani commissioned modules".

## Architecture

```
Make Camp button (TaomFieldCampOverlay, MapView + GauntletLayer, never IsFocusLayer)
   -> ICampMenuActivationQuery guards -> IGameMenuAdapter.Activate("taom_field_camp_menu")
taom_field_camp_menu (choose type) / taom_fc_camp (manage: forage, fortify, supplies, break)
   -> ICampService state machine (camp book, SyncData "_taomFieldCamps")
        terrain policy  -> ICampTerrainService (pure; every 1.4.8 TerrainType explicitly mapped)
        ambush odds     -> ICampAmbushService (pure)
        visuals         -> ICampVisualService -> CampLayoutBuilder (tpac prefab, vanilla fallback)
        supplies        -> ISupplyOrderService.CancelAll on break; SupplyOrderScreens.Open from menu
        lookout range   -> LookoutSpottingContributor -> TaomMapVisibilityModel contributor seam
        nameplate icon  -> Patch74 postfix (PatchShield-excluded namespace: never-throw body)
```

Camp raise progress derives from `EstablishedAt + BuildHours` (nothing stored), so mid-build saves
resume exactly. Ambush and lookout raise in a quarter of the configured hours, fortified in double.

## Key decisions

- **The lookout does NOT register a MapVisibilityModel.** The engine holds one slot and
  CareerSystem's `TaomMapVisibilityModel` owns it; the +20% + Scouting/200 sight bonus rides the
  `IPartySpottingContributor` seam added for exactly this collision.
- **Ambush checks are throttled** to a half-hour game-time accumulator with a straight-line
  distance prefilter. The source ran pathfinding per hostile per frame while an ambush was armed.
- **The ambush springs without starting a battle** (source behaviour): the camp is spent, the
  target's recent-events morale is halved and it is disorganized; a failed roll spots you instead.
- **Fortify preserves state**: foraging flag, accumulator, tally and readiness all survive; the
  source silently wiped foraging and restarted the raise timer from zero.
- **Terrain sets were re-derived against the real 1.4.8 enum** rather than trusting the source's
  compiled ordinals, which decode against a drifted enum. Every member is explicitly classified
  with default-deny arms, so a future engine terrain cannot silently allow an ambush.
- **Move-away handling**: while camped, a move order holds the party and asks to break camp
  (confirm inquiry with a reentry latch), the source's design with the same UX.

## Configuration

MCM group **Field Camps** (GroupOrder 46): master toggle, `CampSetupHours` (4),
`CampMoralePerHour` (1, x2 fortified), `CampForagePerTroopFactor` (0.1), `CampMaxAmbushRange` (10),
`CampBaseAmbushChance` (0.5), `CampMinTownDistance` (10, ambush/lookout exempt),
`CampFortifiedUpgradeCost` (500). All validated in `CampSettingsProvider`; all coop
simulation-relevant. Master toggle off hides the button and menus; an existing camp stays until
broken so state is never dropped by a toggle.

## Assets

`Main/_Module/AssetPackages/fieldcamp_camp_a.tpac` + `fieldcamp_palisade_ring.tpac`: the FIRST
AssetPackages content in this repo (commissioned art, provenance-cleared). `CopyModule` deploys
`_Module` verbatim, so no csproj change was needed. `CampLayoutBuilder` falls back to the source's
procedural vanilla-mesh layouts (siege-camp tents, barricade rings) when a mesh is missing, so a
stripped install degrades instead of breaking; the fallback is exercised by renaming the tpac away
(smoke checklist).

## Key files

| File | Purpose |
|---|---|
| `Domain/CampState.cs` + `FieldCampSaveDefiner.cs` | Persisted camp record; definer base 726901101 |
| `CampService.cs` | State machine; campaign statics behind protected virtuals (44 tests) |
| `CampTerrainService.cs` / `CampAmbushService.cs` | Pure policy and odds |
| `CampVisualService.cs` + `Visuals/CampLayoutBuilder.cs` | Entities, wind ticker (500 ms throttle), fallback chain |
| `LookoutSpottingContributor.cs` | The sight bonus through the model seam |
| `Hooks/FieldCampCampaignBehavior.cs` + `FieldCampMenuController.cs` | Events, SyncData, menus (index 4 on both menus reserved for Refuge) |
| `Hooks/PartyNameplateCampIconPatch.cs` | Patch74 (registry entry has the never-throw contract) |
| `UI/FieldCampMapView.cs` + `FieldCampOverlayVM.cs` | Overlay button + status panel, 4 Hz refresh |
| `Main/_Module/GUI/PreFabs/FieldCamp/TaomFieldCampOverlay.xml` | Ported prefab, vanilla brushes |

## Traps

- The Refuge insertion point is **index 4 on BOTH menus**; do not take it.
- `CampVisualService` is a process-lifetime singleton: session teardown must call `ClearAll`
  (map-view finalize + game-over handler both do; both idempotent).
- A hand-corrupted save with NaN `BuildHours` renders that camp permanently un-ready; the only
  write sites (Establish/Fortify) sanitize, so this needs deliberate save editing to reach.
- Banner-cloth wind is ticked from `Show`/`IsShown` polling; if the service ever stops polling
  after placement, flags go limp (visuals builder note).

## Owed

- In-game smoke per #506 checklist (all four types, fortify, forage, ambush, save/load mid-build,
  tpac render + fallback).
- 12-language translation run for the 66 `{=taom_fc_*}` keys (English fallbacks registered).
