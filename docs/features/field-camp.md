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
        supplies        -> ISupplyOrderService.CancelCampOrders on break; SupplyOrderScreens.Open from menu
        lookout range   -> LookoutSpottingContributor -> TaomMapVisibilityModel contributor seam
        nameplate icon  -> Patch74 postfix (PatchShield-excluded namespace: never-throw body)
```

Camp raise progress derives from `EstablishedAt + BuildHours` (nothing stored), so mid-build saves
resume exactly. Ambush and lookout raise in a quarter of the configured hours; fortifying a ready
field camp starts a real second raise at double the setup hours. The raise completing announces
`{=taom_fcamp_established}`; breaking announces `{=taom_fcamp_broken}`.

All FieldCamp localization keys use the `taom_fcamp_` prefix. The port initially claimed
`taom_fc_`, which was already FieldCommission's registered prefix (10 colliding ids, round-B
dataflow finding); the 58 keys were renamed mechanically, same suffixes. The game-menu and
menu-option ids (`taom_fc_camp`, `taom_fc_ambush`, ...) are NOT localization keys and keep the
short prefix.

## Key decisions

- **The lookout does NOT register a MapVisibilityModel.** The engine holds one slot and
  CareerSystem's `TaomMapVisibilityModel` owns it; the +20% + LEADER's Scouting/200 sight bonus
  (leader-based per the source, consistent with every other camp skill read) rides the
  `IPartySpottingContributor` seam added for exactly this collision. The contributor returns 0
  while the master toggle is off.
- **Ambush checks are throttled** to a half-hour game-time accumulator with a straight-line
  distance prefilter. The source ran pathfinding per hostile per frame while an ambush was armed.
  The prefilter runs INSIDE the campaign-boundary enumeration, before any candidate allocation,
  and party names are never rendered during the scan; only the winning candidate's name resolves
  (lazily, from its engine handle) when the inquiry is shown (round-B scan-cost finding).
- **The ambush payoff is the source's battle flow**: on trigger the camp is spent and a two-button
  inquiry offers the strike (`{=taom_fcamp_ambush_ok}` on a successful roll,
  `{=taom_fcamp_ambush_fail}` when spotted, either way the player may attack). Confirming starts a
  real battle via `StartBattleAction.ApplyStartBattle`, guarded on BOTH parties being free of a
  map event; only a successful roll softens the target first (recent-events morale halved +
  disorganized). No new scan runs while the inquiry is open. Both map-context inquiries carry the
  source's `pauseGameActiveState: true` (ambush also `prioritize: true`, its `ShowInquiry(data,
  true, true)`): without the pause the world kept marching under the modal (round-B fidelity
  finding).
- **The trigger chance uses the PLAYER's spotting range** (source formula): a wider sight radius
  erodes the concealment term. `ICampAmbushService.TriggerChance(playerSpottingRange, ...)` is
  named for it so nobody re-reads the first argument as a candidate distance.
- **Fortify is a real second raise** (source behaviour): double the setup hours, effects paused
  until the palisade stands. The one fix kept over the source: the foraging flag, accumulator and
  tally survive the upgrade instead of being silently wiped.
- **Terrain tables are the source's, decoded exactly.** Its compiled relative switches were
  decoded against the real 1.4.8 `TerrainType` values (identical to its 1.4.5 target). Every
  member is explicitly classified with default-deny arms, so a future engine terrain cannot
  silently allow an ambush or inherit the source's 0.5 forage default unreviewed. One oddity kept
  for parity: terrain the source never named (Cliff, the restriction faces) forages at its 0.5
  default.
- **Move-away handling**: while camped, a move order holds the party and asks to break camp
  (confirm inquiry with a reentry latch). The FULL order is captured before the hold
  (settlement / engage / escort target + navigation type, not just the point), so "break camp and
  move" still enters the town or presses the attack instead of walking to a stale position.
- **Session reset**: the camp book lives in a process-lifetime singleton and SyncData only runs
  when a save record exists, so the behavior tracks whether a LOADING SyncData ran and calls
  `ICampService.ResetForNewSession()` from OnSessionLaunched / OnGameLoaded otherwise (fresh
  campaign, or a pre-feature save). The reset latches `_syncedThisSession` so a stray later call
  cannot wipe a camp pitched right after launch (the Refuge/SupplyLines shape). `LoadFrom` also
  clears every transient (ambush scan clock, move latch, inquiry latch) and drops null book rows,
  logging once.
- **SyncData branches on direction** (the SupplyLines shape): the save half is SaveInto + SyncData
  only, because `LoadFrom` has load-only semantics and running it on every autosave wiped live
  latches and the scan clock (round-B). The load half nulls the local first, because the engine's
  `BehaviorSaveData.SyncData` leaves the ref UNCHANGED on a missing key; pre-seeding from the live
  singleton would silently keep the previous session's book on a key miss (Codex round-2 #2). A
  missing key therefore loads as an empty book.
- **Player capture breaks the camp cleanly** (visuals removed, camp-placed supply orders
  cancelled) from either tick, before any other camp work.

## Configuration

MCM group **Field Camps** (GroupOrder 46): master toggle, `CampSetupHours` (4),
`CampMoralePerHour` (1, x2 fortified), `CampForagePerTroopFactor` (0.1), `CampMaxAmbushRange` (10),
`CampBaseAmbushChance` (0.5), `CampMinTownDistance` (10, ambush/lookout exempt),
`CampFortifiedUpgradeCost` (500). All validated in `CampSettingsProvider`; all coop
simulation-relevant. Master toggle off hides the button and menus and stops the gameplay effects
(morale, forage, ambush scans, the lookout sight bonus); the state-PROTECTING paths stay live so a
standing camp is never trapped: the settlement-entry fold, the captivity break, and the move guard
(whose inquiry is the remaining way to break the camp while the button is hidden) all keep
running.

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
| `CampService.cs` | State machine; campaign statics behind protected virtuals (CampServiceTests) |
| `CampTerrainService.cs` / `CampAmbushService.cs` | Pure policy and odds |
| `CampVisualService.cs` + `Visuals/CampLayoutBuilder.cs` | Entities, wind ticker (500 ms throttle), fallback chain |
| `LookoutSpottingContributor.cs` | The sight bonus through the model seam |
| `Hooks/FieldCampCampaignBehavior.cs` + `FieldCampMenuController.cs` | Events, SyncData, menus (index 4 on both menus reserved for Refuge) |
| `Hooks/PartyNameplateCampIconPatch.cs` | Patch74 thin guarded body (registry entry has the never-throw contract) |
| `Hooks/CampNameplateIconPresenter.cs` | Patch74's widget half: icon creation, sprite memo (same never-throw posture) |
| `UI/FieldCampMapView.cs` + `FieldCampOverlayVM.cs` | Overlay button + status panel, 4 Hz refresh |
| `Main/_Module/GUI/PreFabs/FieldCamp/TaomFieldCampOverlay.xml` | Ported prefab; brushes are grep-verified vanilla (`Popup.Description.Text`, `Popup.Button.Text`, `Popup.Frame`, `ButtonBrush2`); the source's root `Frame1.Broken` was a phantom too (only `.Left`/`.Right` exist), swapped for the real `Popup.Frame` in round C |

## Menus and time: establish lands on the map, the camp sub-menu is a WAIT menu (field-tested 2026-08-23/25)

A standard game menu stops campaign time unconditionally and deadens the time controls (vanilla
behavior, not a bug), and MapState persists the open menu id into the save. The source module
switched into its camp sub-menu right after establishing, which showed "Raising 0%" in a panel
where the raise could never progress; the very first field camp ever established read as a
frozen game, and saving there made every load resume "frozen" too. Two deliberate deviations
from the source close that class:

- **Establish exits to the map** (`FieldCampMenuController.Establish`): the overlay narrates the
  raise and Camp Options reopens the menu on demand. Verified in-game 2026-08-25: the first camp
  on the fix build raised to completion on a new campaign.
- **The camp sub-menu is a wait menu** (`AddWaitGameMenu`, `WaitMenuHideProgressAndHoursOption`;
  the Enlistment service menu is the precedent): time runs while the panel is open, and the tick
  re-renders the raise percent and forage grain. The forage toggle refreshes in place through
  `IGameMenuAdapter.RefreshCurrent()` (MenuContext.Refresh) instead of SwitchTo(same id), which
  would re-initialise the wait. Break camp exits to the map. Leave stays the only isLeave option
  because the engine calls EndWait BEFORE an isLeave consequence (the Enlistment trap).

Field diagnostics that came out of the incident (DevConsole feature, taom.* commands):
`taom.time_status` dumps every state that can freeze campaign time (menu context, time lock,
engine pause, waiting flag, encounter); `taom.rescue_time` exits a stuck menu context, releases
the lock, unpauses the engine and force-resumes time. Both live in
`Main/Features/DevConsole/Cheats/TimeControlCheats.cs`.

## Traps

- The Refuge insertion point is **index 4 on BOTH menus**; do not take it.
- `CampVisualService` is a process-lifetime singleton: session teardown must call `ClearAll`
  (map-view finalize + game-over handler both do; both idempotent).
- A hand-corrupted save with NaN `BuildHours` renders that camp permanently un-ready; the only
  write sites (Establish/Fortify) sanitize, so this needs deliberate save editing to reach.
- Banner-cloth wind is ticked from `Show`/`IsShown` polling; `CampService.FrameTick` polls
  `IsShown` every pass while the visual stands, and that poll IS the steady-state wind driver: if
  it is ever removed, flags go limp shortly after placement (visuals builder note).
- The behavior's `_syncedThisSession` flag is set by a loading SyncData or by the one-shot reset
  itself (the latch). Do not "simplify" the OnSessionLaunched / OnGameLoaded reset checks away:
  without them a fresh campaign inherits the previous campaign's camp book from the
  process-lifetime singleton and saves it.
- `ICampOverlayContributor` consumers all contain per-contributor (and per-`Lazy` factory)
  exceptions: `CampService.FirstContributorBlockReason` feeds a GameMenuOption condition whose
  engine dispatch is unguarded, so a throwing contributor there is a menu-open crash, not a
  degraded label. Keep the catch when adding consumers.
- `CampLayoutBuilder` placement helpers remove their half-built `GameEntity` in the catch block:
  it is not in `outEntities` yet when a native call throws, so nothing else can ever release it.

## Owed

- In-game smoke per #506 checklist (all four types, fortify re-raise, forage, ambush inquiry +
  battle, save/load mid-build, toggle-off break-via-move-guard, capture-while-camped, tpac render
  + fallback).
- 12-language TRANSLATION run for the `{=taom_fcamp_*}` keys (backlog issue #508). Registration
  is done: batch-2 integration purged every `taom_fc_` row from `taom_module_strings.xml`,
  registered all 58 `taom_fcamp_` keys, and rebuilt the 12 language files with English fallbacks;
  FieldCommission's `taom_fc_` rows live untouched in `taom_enlistment_strings.xml`.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
