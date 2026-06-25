# NavalTravel — CHANGELOG

Feature-scoped change log. The canonical project log is [`../../../CHANGELOG.md`](../../../CHANGELOG.md);
keep new entries here in sync with it (this file is the per-feature curated view, not a replacement).

## 2026-06-25 — in-game iteration #2: input read + native at-sea crash guard (issue #296)

Two issues surfaced while testing the set-sail modifier in-game (both decompile/log-confirmed; the fixes
themselves are in-game-pending a redeploy):

- **Set-sail key never registered.** `Input.IsKeyDown(LeftAlt)` returns `false` when polled from
  `TaomPartyNavigationModel` — the model runs *outside* the campaign map's input layer (during the
  navigation query), and the active layer owns/consumes key routing, so the buffered poll misses the held
  key. `IsSailModifierHeld` now reads **`Input.IsKeyDownImmediate`** (raw device state, bypasses the layer
  gate) and accepts *either* source; the `[diag]` line logs both for verifiability.
- **Native AV CTD (`0xC0000005` reading `0x4`) on the hourly AI tick** → `Patch57_NavalAtSeaLandRescueGuard`.
  Granting naval capability lets a party reach `IsCurrentlyAtSea`, which **activates** the vanilla
  `AIMoveToNearestLandBehavior.AiHourlyTick` (inert in stock TAOM since nothing reaches sea). It calls the
  native cross-region land-pathfind `MapScene.GetNearestFaceCenterForPositionWithPath`
  (`maxDist=MapDiagonal/2`, `excludedFaceIds={7,13,14,21,22}`), which dereferences the naval region-map
  navmesh **TAOM_Map never builds** (#120) → CTD for any at-sea party (AI now, the player once sailing
  works). A native AV is a corrupted-state exception a managed Finalizer can't reliably catch, so the fix
  is a **Prefix that skips the behavior** while the feature is enabled (prevent-the-call, like the spider
  Patch47/48). Behavior-neutral apart from the crash — player disembark routes through
  `CanPlayerNavigateToPosition`, and non-at-sea parties already early-return. Decision = pure
  `INavalTravelService.ShouldSuppressAtSeaLandRescue` (= `IsEnabled`); the patch targets the internal
  vanilla type by name and is drift-safe. **+2 tests (57 total).** Root cause confirmed by decompiling
  `AIMoveToNearestLandBehavior` (installed v1.4.6): `maxDist` + `excludedFaceIds` matched the crash data
  exactly. Crash report 2026-06-25.

## 2026-06-24 — initial implementation (issue #296)

- `TaomPartyNavigationModel : DefaultPartyNavigationModel` unlocks the base-engine naval system without
  the Naval DLC — a faithful port of NavalDLC's `NavalPartyNavigationModel` with the ship-ownership gate
  swapped for a TAOM config/MCM gate. Thin model → pure `INavalTravelService` → MCM-over-JSON
  `INavalTravelSettingsProvider` → validated `NavalTravelConfigProvider`. Registered in `Main/IoC.cs` +
  `Main/SubModule.cs`. MCM group **World → Naval Travel** (master *Enable* + *Apply To Player* + *Apply
  To AI*). Config `_Module/ModuleData/naval_travel/naval_travel_config.json`
  (`enabled`/`applyToPlayer`/`applyToAi`/`embarkThresholdDistance`/`navalTerrainTypeIds`). No dependency
  on the NavalDLC module or DLC entitlement.
- **Reviewed before commit:** `/deep-review` (clean after 2 efficiency micro-opts — `HashSet` terrain
  lookup, `Array.Empty<int>()`) + `/review-codex` (gpt-5.5 xhigh: 1 HIGH + 1 MED, both verified real and
  fixed):
  - **HIGH** — `HasNavalNavigationCapability` keyed only on `IsMainParty` stranded a player-led army's
    attached AI parties at sea (engine propagates `IsCurrentlyAtSea` + recomputes `NavigationCapability`
    per party). Fixed: attached parties inherit the army leader's capability (mirrors NavalDLC).
  - **MED** — live-disabling mid-voyage soft-locked an at-sea party. Fixed: an already-at-sea party keeps
    capability to reach land; gates govern only new embarks from land.
  - Both encoded in pure `INavalTravelService.HasNavalCapability(isMain, isAtSea, attachedLeaderCanSail)`
    + a 9-cell matrix test. RCA `../../../docs/reviews/rca-navaltravel-2026-06-24.md`.
- **Boat visual** — `Patch54_NavalTravelBoatVisual` (Postfix on `SandBox.View.MobilePartyVisual.AddMobileIconComponents`).
  The base game omits the leader figure at sea but renders no ship (the campaign ship visual is otherwise only in
  `NavalDLC.View`), so this adds the configured boat mesh — `boat_sail_on`, a **base-game `Native` shared mesh** (also
  `map_icon_ship`; loads with no DLC), scale 0.4, mirroring NavalDLC's no-ship recipe — to the at-sea party's
  `StrategicEntity`. Idempotent via a `taom_naval_boat` tag (never accumulates; follows + despawns with the party).
  New JSON: `renderBoatVisual` (toggle) / `boatMeshName` (swap the asset) / `boatScale` (finite `(0,100]`).
- Tests: 55 (`NavalTravelServiceTests` gate + `HasNavalCapability` + `ShouldRenderBoat` matrices;
  `NavalTravelConfigProviderTests` validation incl. boat fields). All green.

## 2026-06-24 — in-game iteration (set-sail modifier + boat-hook fix; sea-only scope)

In-game diag confirmed the model side works (capability granted, water navmesh enabled —
`invalidForAll=[7,13,14,21,22]`, party reaches `mainAtSea=True`), but surfaced two gaps:

- **Auto-pathfinder never sails** — it always prefers a land/bridge route (`RegionSwitchCost*` are `0`,
  but a land path is shorter/found-first). So sailing is now **player-initiated**: `CanPlayerNavigateToPosition`
  allows a water click *from land* only while the **`sailModifierKey`** (default `LeftAlt`, `Input.IsKeyDown`)
  is held → the party routes to the coast and the engine's embark transition fires. Disembark stays
  automatic. New JSON `sailModifierKey`.
- **Boat never appeared** — the at-sea state change does NOT trigger an icon rebuild, so the lone
  `AddMobileIconComponents` hook never observed it. Added a second Patch54 hook on
  `MobilePartyVisual.OnTransitionEnded` (fires when the embark/disembark completes); both share
  `UpdateBoat`.
- **Scope set to sea-only.** TAOM_Map rivers are a water-`10` channel with impassable mountain-`7` banks
  (AI-channeling) + bridges, so walkable land never borders the river water → embark can't span the bank.
  Rivers self-exclude; no change needed. Documented the naval-distance-cache gap (#120 — `useNavalNavigation`
  is false for TAOM_Map, so `Naval`/`All` caches aren't registered → no settlement/AI naval routing).
- Temporary `[NavalTravel][diag]` logging added to `TaomPartyNavigationModel` + `Patch54` to verify the
  embark + boat render in-game; **strip after sign-off**. End-to-end sail + boat render still pending.
