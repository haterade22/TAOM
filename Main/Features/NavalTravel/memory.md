# NavalTravel — in-folder memory

**Read this before editing this folder.** It is the terse "what a future editor must know" list — the
comprehensive reference is [`docs/features/naval-travel.md`](../../../docs/features/naval-travel.md);
the cross-cutting lesson (applies to *all* GameModels) lives in the global memory
`feedback_gamemodel_capability_engine_propagation`. Issue #296.

## What this is

Lets campaign-map parties sail across **open sea** — and look like boats — for everyone **without the
paid Naval DLC**. **Sea-only by design** (rivers self-exclude — see below). THREE parts:
- **Movement** = override **one** GameModel `TaomPartyNavigationModel : DefaultPartyNavigationModel`
  (the engine's `Helpers.NavigationHelper.CanPlayerNavigateToPosition` routes through the model; no
  Harmony patch for movement).
- **Set sail (player-initiated)** = the engine's auto-pathfinder NEVER chooses sea over land/bridge, so
  sailing is deliberate: the model's `CanPlayerNavigateToPosition` allows a water click *from land* only
  while the `sailModifierKey` (default LeftAlt) is held → the party heads to the coast and the base embark
  transition fires. Disembark = automatic (click land while at sea). **Read the key via
  `Input.IsKeyDownImmediate`, NOT `Input.IsKeyDown`** — see the input gotcha below.
- **Boat visual** = `Patch54_NavalTravelBoatVisual` (TWO Postfixes on SandBox.View `MobilePartyVisual`:
  `OnTransitionEnded` drives the add/remove on the embark/disembark; `AddMobileIconComponents` re-adds on
  rebuild). The base game does NOT render a ship at sea (omits the figure + adds nothing); the campaign
  ship visual is otherwise only in `NavalDLC.View`. So TAOM adds the `boat_sail_on` mesh itself.

## Load-bearing engine facts (NOT obvious from the code)

- The base engine ships the naval **movement** system; it is gated OFF by
  `DefaultPartyNavigationModel.HasNavalNavigationCapability => false`. We flip that gate (config/MCM
  instead of ship-ownership). Water navmesh + embark/disembark transition are native base-engine.
- **The boat VISUAL is NOT native** — `SandBox.View.MobilePartyVisual` omits the figure at sea and adds
  no ship; the `boat_sail_on` campaign-icon rendering lives only in `NavalDLC.View`. TAOM renders it via
  Patch54. The `boat_sail_on` MESH itself is base-game (`Native/AssetPackages/meshes_shared_*.tpac`,
  registered as `map_icon_ship`) — loads with no DLC. (`boatMeshName`/`boatScale` are JSON-swappable.)
  **Boat-hook gotcha:** the at-sea state change does NOT trigger an icon rebuild, so hooking only
  `AddMobileIconComponents` never sees it (the boat never appeared). Must ALSO hook `OnTransitionEnded`
  (fires when embark/disembark completes). `MobilePartyVisual.MapEntity` is public → the PartyBase.
- **Auto-path NEVER sails** — confirmed in-game. `MapDistanceModel.RegionSwitchCostFromLandToSea/SeaToLand`
  are `0` (no penalty) but a land/bridge route is shorter/found-first, so the pathfinder always picks
  land. Sailing must be player-initiated (the modifier). The embark itself is the engine's tick:
  `NavigationHelper.GetEmbarkAndDisembarkDataForPlayer` → `CalculateTransitionStartAndEndPosition`
  projects a transition `embarkThresholdDistance` past the walkable-navmesh edge; it fires only where
  walkable land borders water.
- **Naval distance caches are NOT registered for TAOM_Map.** `SettlementPositionScript` sets
  `useNavalNavigation=true` only for a NavalDLC map or when the NavalDLC module is active → for TAOM_Map
  only the land (`Default`) cache loads, not `Naval`/`All`. So settlement-distance naval routing + AI
  naval routing don't work (#120); direct set-sail uses the navmesh, not these caches.
- **Rivers self-exclude (why sea-only).** TAOM_Map rivers = water-`10` channel with impassable mountain-`7`
  banks (AI-channeling). Walkable land never borders the water → embark can't span the bank → no river
  sailing. Don't try to "fix" rivers; they have bridges. `DisableUnwalkableNavigationMeshes` disables only
  the faces in `GetInvalidTerrainTypesForNavigationType(All)` — TAOM's All set is `[7,13,14,21,22]`
  (water absent), so water navmesh stays ENABLED (the model drives this; no separate patch needed).
- **Donor model:** the official NavalDLC's internal `NavalPartyNavigationModel`. `TaomPartyNavigationModel`
  is a *faithful port* of it with ONLY the capability gate changed. Keep it faithful — re-decompile the
  donor (`ilspycmd` on `…/Modules/NavalDLC/bin/Win64_Shipping_Client/NavalDLC.dll`, type
  `NavalDLC.GameComponents.NavalPartyNavigationModel`) and DIFF before changing
  `CanPlayerNavigateToPosition` or the terrain methods.
- `IMapScene.GetPathDistanceBetweenAIFaces` (1.4.6): arg 7 is **`out float`** (not `ref`); arg order is
  `(startFace, endFace, startPos, endPos, agentRadius=0.3f, distanceLimit=Campaign.PathFindingMaxCostLimit,
  out dist, invalidIds, regionSwitchCostLandToSea, regionSwitchCostSeaToLand)`. **Do not swap the two
  region-switch costs.**
- Installed game is **v1.4.6**; the `E:\Decompiled_Bannerlord` dump is v1.4.5 — verify signatures via
  `ilspycmd` on the installed DLLs.

## Gotchas that bit us (Codex review 2026-06-24 — [RCA](../../../docs/reviews/rca-navaltravel-2026-06-24.md))

- **Cross-party propagation (HIGH).** `HasNavalNavigationCapability` must NOT key only on `IsMainParty`.
  The engine force-propagates `MobileParty.IsCurrentlyAtSea` down the army attachment tree
  (`MobileParty.cs:493-496`) and recomputes `NavigationCapability` per party (`:464-479`) — so an
  attached party must inherit its army leader's capability, or a player-led army strands its attached
  AI parties at sea when *Apply To AI* is off.
- **At-sea grace (MED).** A party already at sea must KEEP capability (to reach land) regardless of the
  toggles; the gates govern only NEW embarks from land. Otherwise live-disabling mid-voyage soft-locks
  the party (base `CanPlayerNavigateToPosition` rejects any move from a non-land position).
- Both decisions live in the **pure** `INavalTravelService.HasNavalCapability(isMain, isAtSea,
  attachedLeaderCanSail)`, pinned by a 9-cell matrix test. Keep the model a thin boundary that extracts
  engine values and delegates (the model itself is in-game-only per ADR-008).

## Config / toggles

- JSON: `_Module/ModuleData/naval_travel/naval_travel_config.json`. **JSON-only** (snapshot at service
  ctor → app restart): `embarkThresholdDistance`, `navalTerrainTypeIds`, `renderBoatVisual`, `boatMeshName`,
  `boatScale`, `sailModifierKey` (any `InputKey` name; unknown→LeftAlt). **Live MCM** (World → Naval Travel):
  `enabled`/`applyToPlayer`/`applyToAi` (master + Apply To Player + Apply To AI; AI-off = player-only).

## Gotchas — caravan naming (fixed 2026-06-24)

- Flipping `HasNavalNavigationCapability` on globally makes the engine's `CaravanPartyComponent.CacheName`
  take its NAVAL branch → it looks up `str_convoy_party_name` / `str_armed_convoy_party_name`, which exist
  **only in the unloaded NavalDLC** → every AI caravan rendered `ERROR: Text with id … doesn't exist!`
  (they all report capability, even idle on land, while *Apply To AI* is on). Fix: define both ids in
  `Main/_Module/ModuleData/taom_module_strings.xml`, mirroring the vanilla CARAVAN text and reusing its
  translation keys (`{=LjUhEJxz}` / `{=l4pRw7pO}`) → "Caravan of {name}" uniformly, 12 languages free, no
  patch. **Do NOT remove those strings.** The clan-finance line gates on the SEPARATE
  `CanHaveNavalNavigationCapability` (culture ship-hulls, false for TAOM) so it was never affected.

## Gotchas — set-sail input + native at-sea crash (fixed 2026-06-25, in-game-pending)

- **Modifier key read: use `Input.IsKeyDownImmediate`, not `Input.IsKeyDown`.** The model polls the key
  from *outside* the map's input layer (during the navigation query). The buffered `IsKeyDown` reflects
  layer-routed/consumed state, so it returns `false` even while the key is physically held → the sail
  modifier appeared dead (water clicks rejected, `[diag]` never logged "DOWN"). `IsKeyDownImmediate` reads
  raw device state and bypasses the gate. `IsSailModifierHeld` reads BOTH and accepts either.
- **Native AV CTD from a DORMANT vanilla behavior (`Patch57_NavalAtSeaLandRescueGuard`).** Granting naval
  capability lets a party reach `IsCurrentlyAtSea`, which **activates** the vanilla
  `AIMoveToNearestLandBehavior.AiHourlyTick` — inert in stock TAOM because nothing ever goes to sea. It
  calls the native `MapScene.GetNearestFaceCenterForPositionWithPath` (cross-region land pathfind,
  `maxDist=MapDiagonal/2`, `excludedFaceIds=GetInvalidTerrainTypesForNavigationType(All)={7,13,14,21,22}`),
  which dereferences TAOM_Map's **missing naval region navmesh** (#120) → `0xC0000005` reading `0x4` on the
  hourly AI tick, for ANY at-sea party. **A native AV is a corrupted-state exception — a managed Finalizer
  can't reliably catch it** (unlike Patch49/50's managed-NRE finalizers), so the fix is a **Prefix that
  skips the behavior** (prevent-the-call, like the spider Patch47/48). Decision = pure
  `INavalTravelService.ShouldSuppressAtSeaLandRescue` (= `IsEnabled`). Behavior-neutral apart from the
  crash: player disembark uses `CanPlayerNavigateToPosition` (not this behavior), non-at-sea parties
  early-return anyway. **Lesson: enabling a dormant engine subsystem can wake OTHER dormant engine
  behaviors that assume infrastructure TAOM_Map lacks — grep the engine for behaviors gated on the state
  you're newly enabling (`IsCurrentlyAtSea` here) before shipping.**

## Known limits / owed

- **CONFIRMED in-game (diag):** capability granted (`HasNavalNavigationCapability=True`), water navmesh
  enabled (`invalidForAll=[7,13,14,21,22]` — no water), party reaches `mainAtSea=True`. Movement works.
- **NOT yet confirmed end-to-end:** the set-sail modifier embark (now via `IsKeyDownImmediate`), the boat
  render, and the `Patch57` crash guard — `[NavalTravel][diag]` logging is in `TaomPartyNavigationModel` +
  `Patch54`; **STRIP after sign-off**.
- Sea-only by design (rivers self-exclude). Sea encounters use default battle handling (no naval combat).
- Naval settlement-distance + AI naval routing unsupported (caches not registered, #120). Because AI-at-sea
  is unsupported AND was the likely crash party, consider defaulting `applyToAi` **off** until #120 lands.
