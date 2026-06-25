# Naval Travel

> Issue #296. Related: #120 (land-only distance caches).

## Overview

Lets campaign-map parties sail across **open sea** — and look like boats while they do — for **every
player without the Naval DLC ("War Sails")**. Two halves: the **movement** reuses the base engine's own
naval system (water pathing, embark/disembark transitions) by overriding one GameModel; the **boat
visual** is added by TAOM (a Harmony patch), because the base game renders *no* ship for an at-sea party
— the campaign ship visual is otherwise only provided by `NavalDLC.View`. No naval combat (per design).

**Sailing is player-initiated.** The engine's auto-pathfinder always prefers a land/bridge route over a
sea route (region-switch costs are `0`, but a land path is shorter or simply found first), so it never
chooses to sail on its own. The player deliberately sets sail by **holding a modifier key (default Left
Alt) and clicking water** — the party then heads to the coast and embarks. **Scope is sea-only by
design:** rivers are not sailable (their impassable banks block the land↔water embark adjacency, and
every river has a bridge) — see Known Limitations.

## Why This Exists

Bannerlord v1.4.6 ships the entire naval-travel system inside the base engine assemblies that every
copy of the game has — but it is switched **off** by `DefaultPartyNavigationModel.HasNavalNavigationCapability`
returning `false`. The official `NavalDLC` module swaps in a model that returns `true` only when a
party owns a ship (DLC content), and bundles a large Calradia payload (naval cultures, lords, ports,
shipyards, ship crafting, a storyline). TAOM is a LOTR total conversion and cannot depend on that
module — it would require the DLC enabled and inject conflicting content.

TAOM instead reuses the maximum that works **without** the DLC: the base-engine naval system itself.
`TaomPartyNavigationModel` is a faithful port of NavalDLC's internal `NavalPartyNavigationModel`
(same naval terrain rules, embark threshold, and `CanPlayerNavigateToPosition`), with **one change** —
the ship-ownership capability gate becomes a TAOM config/MCM gate, so any party can sail with no ship
and no DLC.

## Architecture

### Design challenge

The player click-to-sail flow is entirely base-engine: the map move handler calls
`Helpers.NavigationHelper.CanPlayerNavigateToPosition(...)` → `Campaign.Current.Models.PartyNavigationModel.CanPlayerNavigateToPosition(...)`,
then `MobileParty.SetMoveGoToPoint(point, navigationType)`. The engine also embarks/disembarks
(`MobileParty.SetSailAtPosition` / `DisembarkToPosition` / `FinishNavigationTransitionInternal`). The
only gate on *movement* is the `PartyNavigationModel` GameModel — so unlocking sailing = overriding that
one model. No navmesh hack.

**The boat *visual* is a separate problem the GameModel does NOT solve.** Reading `SandBox.View`'s
`MobilePartyVisual.AddCharacterToPartyIcon`: at sea it *omits* the leader figure but adds no ship —
the campaign party-as-ship visual (`boat_sail_on` mesh, wake particle, sail sound) lives entirely in
`NavalDLC.View.dll`'s `NavalMobilePartyVisual`, which loads only with the DLC. So a non-DLC sailing
party would render as a bare banner. TAOM fills that gap itself (see Boat visual below).

### Solution

`TaomPartyNavigationModel : DefaultPartyNavigationModel` overrides five methods:

| Method | Behavior |
|--------|----------|
| `HasNavalNavigationCapability(party)` | **The gate.** Boundary extracts `IsMainParty`, `IsCurrentlyAtSea`, and the army leader's capability (`AttachedTo?.HasNavalNavigationCapability`) and delegates to `service.HasNavalCapability(...)`. Replaces NavalDLC's "party owns a ship" check. **An already-at-sea party keeps capability** (reach land, never strand mid-voyage); **an attached party inherits its army leader's capability** (mirrors NavalDLC) so a player-led army sails together even when *Apply To AI* is off; otherwise the per-party MCM gates govern new embarks from land. |
| `IsTerrainTypeValidForNavigationType(t, nav)` | `Naval` → configured naval terrain set; `All` → naval OR vanilla land; else base. |
| `GetInvalidTerrainTypesForNavigationType(nav)` | Cached complement of the above (built once at construction). |
| `GetEmbarkDisembarkThresholdDistance()` | `0.5` (NavalDLC's value; vanilla `0` blocks embarking). |
| `CanPlayerNavigateToPosition(vec2, out nav)` | Faithful port of NavalDLC's naval-aware version, plus the **set-sail override**: from land a water target is normally rejected, but allowed while the `sailModifierKey` is held → the click routes the party toward the water (full `All` capability) so it walks to the coast and the engine's embark transition fires there. Falls through to vanilla when disabled. |

Layer stack (ADR-002/007): thin model → `INavalTravelService` (pure decisions: sail permission,
naval-terrain membership, threshold) → `INavalTravelSettingsProvider` (MCM-over-JSON) →
`INavalTravelConfigProvider` (validated JSON). The model holds the inherently engine-coupled
navigation glue (mirrors how `TaomPartySpeedModel` keeps its boundary code in the model).

### Set sail (deliberate embark)

The engine's auto-pathfinder won't route over sea (it prefers land/bridge), so sailing is **explicitly
player-initiated**. `TaomPartyNavigationModel.CanPlayerNavigateToPosition` allows a water target *from
land* **only while the `sailModifierKey` is held** (default `LeftAlt`, any TaleWorlds `InputKey` via the
config). Hold it + click water → the existing move handler calls `SetMoveGoToPoint(water, All)`; the
party walks to the coast and the base engine's embark transition (`NavigationHelper.GetEmbarkAndDisembarkDataForPlayer`,
fired in `MobileParty`'s movement tick) carries it onto the water. Without the modifier, water-from-land
is rejected as vanilla — so neither the player's normal clicks nor AI ever sail by accident.
**Disembarking is automatic:** once at sea, a normal click on land routes the party to the coast and
the disembark transition fires. The input read is a thin boundary in the model: it uses
**`Input.IsKeyDownImmediate`** (raw device state), not the buffered `Input.IsKeyDown` — the model polls
from *outside* the map's input layer (during the navigation query), and the active map layer owns/consumes
key routing, so the buffered poll returns `false` even while the key is physically held. The read accepts
*either* source for robustness; the modifier key parses once at construction (unknown name → `LeftAlt`).

### Boat visual (`Patch54_NavalTravelBoatVisual`)

Adds the configured boat mesh (`boat_sail_on` by default — a **base-game `Native` shared mesh**, also
registered as `map_icon_ship`, so it loads with no DLC) scaled `0.4` to the party's `StrategicEntity`
when at sea, mirroring NavalDLC's own no-ship recipe (`ShipVisualHelper.GetFlagshipEntity`). **Two
Harmony hooks share one idempotent `UpdateBoat`:** `MobilePartyVisual.OnTransitionEnded` (fires when an
embark/disembark *completes* — the actual at-sea change) drives add/remove, and `AddMobileIconComponents`
(the icon-rebuild chokepoint) re-adds the boat after any rebuild. The rebuild hook **alone is
insufficient** — the at-sea state change does NOT trigger an icon rebuild, so it never observes the
transition (the bug that made the boat never appear). Idempotent via a `taom_naval_boat` tag — found/
created/removed by tag so it never accumulates, follows the party as a child of `StrategicEntity`, and
despawns with it. The show/hide decision (`ShouldRenderBoat`) + mesh + scale come from the service; only
engine entity work is in the patch (ADR-008, in-game-only). Whole body try/caught — never breaks the map
render. If the mesh fails to resolve, it no-ops and the party still moves.

### When disabled
Master toggle off ⇒ no on-land party gains naval capability ⇒ `CanPlayerNavigateToPosition` falls
through to the vanilla land-only model ⇒ identical to stock movement. **Exception — live toggle-off
mid-voyage:** a party already at sea keeps capability and `CanPlayerNavigateToPosition` stays
naval-aware, so the player can sail to shore instead of soft-locking (the base model rejects any move
from a non-land position). Once back on land it loses capability ⇒ no re-embark. The boat visual is
independently gated by `renderBoatVisual` (default on) — turn it off to sail with the vanilla
(figure-less) at-sea icon.

### At-sea crash guard (`Patch57_NavalAtSeaLandRescueGuard`)

Enabling naval movement has a side effect: once a party can reach `IsCurrentlyAtSea`, it **activates a
vanilla campaign behavior that is dormant in stock TAOM** — `AIMoveToNearestLandBehavior.AiHourlyTick`,
which fires only for an at-sea party and tries to path it back to land. That behavior calls the native
cross-region pathfind `MapScene.GetNearestFaceCenterForPositionWithPath` (`maxDist = MapDiagonal/2`,
`excludedFaceIds = GetInvalidTerrainTypesForNavigationType(All) = {7,13,14,21,22}`), which dereferences
the **naval region-map navmesh TAOM_Map never builds** (#120) → a native access violation
(`0xC0000005` reading `0x4`) on the hourly AI tick. It hits *any* at-sea party — AI parties (which can
drift to sea when *Apply To AI* is on) and the player once sailing works.

A native AV is a corrupted-state exception that a managed Finalizer cannot reliably catch (unlike the
managed-NRE finalizers Patch49/50), so the fix is the **prevent-the-call Prefix** pattern used by the
spider guards (Patch47/48): `Patch57` skips `AiHourlyTick` whenever the feature is enabled. This is
behavior-neutral apart from preventing the crash — player disembark routes through
`CanPlayerNavigateToPosition` (not this behavior), and for non-at-sea parties the behavior already
early-returns. The behavior's only job (auto-pathing an at-sea party to land) can *only* crash on
TAOM_Map until #120 is solved, so there is nothing valid to lose. The skip decision is the pure
`INavalTravelService.ShouldSuppressAtSeaLandRescue` (= `IsEnabled`); the patch targets the internal
vanilla type by name and is drift-safe (a bind failure logs + no-ops rather than failing module load).

## Configuration

**JSON:** `Main/_Module/ModuleData/naval_travel/naval_travel_config.json` (validated by
`NavalTravelConfigProvider`; invalid values revert to defaults with a warning; cached for the process
lifetime — edits need an app restart).

| Key | Default | Meaning |
|-----|---------|---------|
| `enabled` | `true` | Master gate (MCM-overridable). |
| `applyToPlayer` | `true` | Player party can sail (MCM-overridable). |
| `applyToAi` | `true` | AI parties can sail (MCM-overridable). Set false for the conservative player-only option. |
| `embarkThresholdDistance` | `0.5` | Embark/disembark proximity. Validated finite within `[0, 50]`. |
| `navalTerrainTypeIds` | `[8,10,11,18,19,23,24,25]` | `TerrainType` ids a ship may navigate (Lake/Water/River/CoastalSea/OpenSea/LandRestriction/SeaRestriction/UnderBridge). Unknown ids dropped + warned; empty → default set. |
| `renderBoatVisual` | `true` | Swap an at-sea party's icon to a boat mesh (Patch54). Off = vanilla figure-less at-sea icon, movement still works. |
| `boatMeshName` | `"boat_sail_on"` | Map mesh for the boat icon (base-game `Native`). Swap for any loadable mesh; empty/whitespace → default. |
| `boatScale` | `0.4` | Uniform boat scale. Validated finite within `(0, 100]`. |
| `sailModifierKey` | `"LeftAlt"` | Key the player holds to set sail (hold + click water). Any TaleWorlds `InputKey` name; unknown/empty → `LeftAlt`. |

**MCM:** group **World → Naval Travel** (GroupOrder 37): *Enable Naval Travel*, *Apply To Player*,
*Apply To AI Lords* — independent toggles per `feedback_player_mcm_optout_toggle`. Threshold + terrain
set are advanced JSON-only tuning.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs` | The GameModel override (the movement mechanism). |
| `Main/Features/NavalTravel/Hooks/Patch54_NavalTravelBoatVisual.cs` | Two Postfixes (`MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents`) sharing `UpdateBoat` — renders the boat at sea. |
| `Main/Features/NavalTravel/Hooks/Patch57_NavalAtSeaLandRescueGuard.cs` | Prefix that skips the vanilla `AIMoveToNearestLandBehavior.AiHourlyTick` while enabled — prevents the native at-sea→land pathfind AV (#120). |
| `Main/Features/NavalTravel/INavalTravelService.cs` / `NavalTravelService.cs` | Pure sail/terrain/threshold decisions. |
| `Main/Features/NavalTravel/INavalTravelSettingsProvider.cs` / `NavalTravelSettingsProvider.cs` | MCM-over-JSON live settings. |
| `Main/Features/NavalTravel/NavalTravelConfig.cs` / `INavalTravelConfigProvider.cs` / `NavalTravelConfigProvider.cs` | JSON DTO + validating loader. |
| `Main/Features/NavalTravel/NavalTravelIoC.cs` | DryIoc registrations (all Singleton). |
| `Main/_Module/ModuleData/naval_travel/naval_travel_config.json` | Shipped defaults. |
| `Main/Features/TaomSettings.cs` | MCM toggles (group "World/Naval Travel"). |
| `Main/IoC.cs`, `Main/SubModule.cs` | Feature registration + model registration. |

## Dependencies

- Movement — base engine only: `PartyNavigationModel` / `DefaultPartyNavigationModel`, `MobileParty` naval
  members (`IsCurrentlyAtSea`, `NavigationCapability`, `SetSailAtPosition`/`DisembarkToPosition`,
  `GetRegionSwitchCostFromLandToSea/SeaToLand`), `Helpers.NavigationHelper`, `IMapScene.GetPathDistanceBetweenAIFaces`,
  `Campaign.PathFindingMaxCostLimit`, `TerrainType`.
- Set sail — `TaleWorlds.InputSystem.Input.IsKeyDownImmediate` (modifier read — raw device state, since the
  buffered `IsKeyDown` misses keys polled outside the map input layer), and the base move handler's
  `SetMoveGoToPoint` / `NavigationHelper.GetEmbarkAndDisembarkDataForPlayer` (embark trigger).
- Crash guard — Harmony Prefix on the internal `AIMoveToNearestLandBehavior.AiHourlyTick`
  (`AccessTools.TypeByName`), to suppress its native `MapScene.GetNearestFaceCenterForPositionWithPath` call.
- Boat visual — `SandBox.View`'s `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents`
  (Harmony targets) + the base-game **`Native` `boat_sail_on` mesh** (in `meshes_shared_*.tpac`;
  registered as `map_icon_ship`) + `GameEntity`/`MetaMesh`/`MatrixFrame` (`TaleWorlds.Engine`/`TaleWorlds.Library`).
- **No dependency on the NavalDLC module or DLC entitlement** for any part.
- TAOM infra: `IPathService`, `IModLogger`, `FiniteFloatValidator`, MCM (`TaomSettings`).

## Tests

`TAOM.Tests/Features/NavalTravel/` — `NavalTravelServiceTests` (sail-permission decision matrix incl.
independent player/AI gates; the `HasNavalCapability` full-gate matrix covering at-sea grace + army-leader
inheritance + disabled gate; naval-terrain membership; passthroughs) and `NavalTravelConfigProviderTests`
(valid/missing/malformed/empty parse; threshold NaN/negative/too-large reversion; terrain-id
unknown-drop / dedupe / empty / all-invalid reversion; caching; `ShouldRenderBoat` matrix + boat-field
validation; `ShouldSuppressAtSeaLandRescue` enabled-gate). 57 tests, all green. The model + Patch54/57
are thin entry points — verified in-game, not unit-tested (ADR-008).

## How-To

**Sail (in game):** stand on a sea coast, **hold the sail key (Left Alt) and click the water** (or the
far shore across the sea). The party walks to the coast, embarks (boat appears), and sails. To land,
release the key and click land while at sea — it disembarks on the coast.

**Change the sail key:** edit `sailModifierKey` in the JSON to any TaleWorlds `InputKey` name (e.g.
`LeftControl`) and restart.

**Change which water is sailable:** edit `navalTerrainTypeIds` in the JSON (ids must be valid
`TerrainType` values) and restart the app.

**Make it player-only:** MCM *World → Naval Travel → Apply To AI Lords* = off (or `applyToAi: false`).

**Disable entirely:** MCM *Enable Naval Travel* = off ⇒ exact vanilla land-only movement.

## Known Limitations

- **Sea-only by design; rivers are not sailable.** TAOM_Map rivers are authored as a water-`10`
  channel with impassable mountain-`7` banks (to keep AI from getting stuck), so the walkable land never
  borders the water — the embark transition (which only spans `embarkThresholdDistance` past the navmesh
  edge) lands on the bank, not the water. Rivers all have bridges anyway. The naval terrain set still
  includes river/lake ids, but rivers self-exclude via their banks; widen a specific crossing by raising
  `embarkThresholdDistance` or authoring a direct land↔water adjacency there (out of C# scope).
- **Auto-pathing never sails.** The engine prefers land/bridge routes (region-switch costs are `0`, so
  there's no penalty, but a land route is shorter/found first). Sailing is therefore the deliberate
  `sailModifierKey` action, not something the pathfinder picks. This is intentional.
- **Naval settlement-distance routing is not set up.** The engine only registers the `Naval`/`All`
  navigation distance caches when it detects a NavalDLC map or the NavalDLC module active
  (`SettlementPositionScript`, `useNavalNavigation`); for TAOM_Map it registers only the land cache. So
  "travel to a settlement across the sea" (which uses `MapDistanceModel`) and AI naval routing are
  unsupported — direct sea travel via the set-sail action uses the navmesh, not these caches. This is
  issue **#120** (extend `NavigationType` iteration / generate the naval caches).
- **AI also sails by default** — but only via the (non-existent) auto-naval path, so in practice AI
  stays on land. The independent *Apply To AI Lords* toggle exists if needed.
- **Sea encounters use the engine's default battle handling** (no naval combat scenes) — by design.
- **Depends on TAOM_Map having water navmesh.** Confirmed in-game that it does (parties reach the
  at-sea state); a sea coast must have walkable land touching the water for the embark to fire.
- **In-game verification owed:** model + capability + water navmesh are **confirmed in-game** (diag log:
  `HasNavalNavigationCapability=True`, water absent from `invalidForAll`, `mainAtSea=True` reached). The
  set-sail modifier + the boat visual (dual-hook) are built but **not yet confirmed** end-to-end —
  temporary `[NavalTravel][diag]` logging is in `TaomPartyNavigationModel` + `Patch54` to verify the
  embark + boat render; **strip it after sign-off** (per `feedback_comprehensive_diag_logging_then_remove`).

## Known interactions

- **Caravan party naming.** Flipping `HasNavalNavigationCapability` on globally makes the engine's
  `CaravanPartyComponent.CacheName` take its naval branch, which looks up `str_convoy_party_name` /
  `str_armed_convoy_party_name` — strings that ship **only** in the unloaded NavalDLC. Unaddressed this
  renders `ERROR: Text with id … doesn't exist!` on every AI caravan (they all report naval capability,
  even idle on land, while *Apply To AI* is on). Fixed by defining those two ids in
  `taom_module_strings.xml`, mirroring the vanilla caravan text and reusing its translation keys
  (`{=LjUhEJxz}` / `{=l4pRw7pO}`) so caravans read "Caravan of {name}" uniformly and all 12 languages
  resolve for free. The clan-finance line is unaffected — it gates on `CanHaveNavalNavigationCapability`
  (culture ship-hulls, false for TAOM cultures), not `HasNavalNavigationCapability`.

## Changelog

- 2026-06-24 — Initial feature: `TaomPartyNavigationModel` unlocks base-engine naval movement without the DLC + `Patch54_NavalTravelBoatVisual` renders the at-sea boat icon; MCM World → Naval Travel toggles (Enable / Apply To Player / Apply To AI Lords); Codex review fixed at-sea/army-leader capability inheritance (HIGH) and live-disable mid-voyage (MED). Issue #296.
- 2026-06-24 — In-game iteration: confirmed via diag that capability + water navmesh + at-sea state all work, but (a) the auto-pathfinder always prefers land/bridge over sea and (b) the boat never appeared. Added the **set-sail modifier** (`sailModifierKey`, default LeftAlt — hold + click water to embark deliberately, since auto-pathing won't sail), scoped to **sea-only** (rivers self-exclude via mountain-`7` banks + bridges), and **fixed the boat hook** (added `MobilePartyVisual.OnTransitionEnded` — the rebuild hook alone never saw the at-sea change). Boat assets `boat_sail_on`/`boatScale`/`renderBoatVisual` + the naval-distance-cache gap (#120) documented. End-to-end sail + boat render still in-game-pending; `[diag]` logging temporary.
- 2026-06-24 — Fix: caravans showed `ERROR: Text with id str_convoy_party_name doesn't exist!` because the naval-name branch in `CaravanPartyComponent.CacheName` resolves NavalDLC-only strings. Re-provided both convoy ids in `taom_module_strings.xml` mirroring the vanilla caravan text + keys (no patch, free localization). See "Known interactions".
- 2026-06-25 — In-game iteration #2 (two fixes): (1) **Set-sail key never registered** — `Input.IsKeyDown(LeftAlt)` returns false polled from the model (outside the map input layer, which consumes key routing); switched to `Input.IsKeyDownImmediate` (raw device state), accepting either source. (2) **Native AV CTD** (`0xC0000005` reading `0x4`) on the hourly AI tick — an at-sea party activates the dormant vanilla `AIMoveToNearestLandBehavior`, whose native cross-region land-pathfind dereferences TAOM_Map's missing naval region navmesh (#120). Added `Patch57_NavalAtSeaLandRescueGuard` (Prefix skip while enabled; native AV ≠ Finalizer-catchable, so prevent-the-call like Patch47/48). +2 tests (57 total). Crash report 2026-06-25, #296. Decompile-confirmed root cause; in-game verification of the fix still pending.
