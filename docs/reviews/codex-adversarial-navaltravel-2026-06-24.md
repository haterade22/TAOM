# Codex Adversarial Review - NavalTravel - 2026-06-24

Scope: TAOM NavalTravel feature in the current worktree, Bannerlord v1.4.6 base-engine decompile under `E:\Decompiled_Bannerlord`, and the decompiled NavalDLC reference embedded in `docs/reviews/codex-adversarial-navaltravel-2026-06-24.prompt.md`. The DLC model source itself is not present under `E:\Decompiled_Bannerlord`, so DLC-port comparisons cite the prompt reference; base-engine behavior and signatures cite local decompiled source.

## A. Known Suspects

1. **PORT FIDELITY of `CanPlayerNavigateToPosition` - DISPUTED, no actionable divergence.**
   TAOM matches the DLC branch sequence: disabled fallback aside, it sets `None`, rejects invalid face, blocks land-to-water direct clicks, selects `Naval` vs `NavigationCapability` at sea, uses invalid terrain, returns true for water targets while already at sea, then calls path distance. Evidence: DLC reference `prompt.md:52-61`; TAOM `Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:63-110`. The extra `MobileParty.MainParty == null` guard at `TaomPartyNavigationModel.cs:72-74` is defensive and does not change normal engine behavior. Argument order is correct against `IMapScene.GetPathDistanceBetweenAIFaces` (`E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.Map\IMapScene.cs:34-36`), and TAOM passes land-to-sea then sea-to-land (`TaomPartyNavigationModel.cs:100-110`; engine cost accessors at `MobileParty.cs:3728-3739`).

2. **ARMY / ATTACHED-PARTY CAPABILITY MISMATCH - CONFIRMED, HIGH.**
   TAOM collapses capability to `CanPartySail(mobileParty.IsMainParty)` (`TaomPartyNavigationModel.cs:51-55`; `NavalTravelService.cs:28-32`). With `ApplyToAi=false`, every attached AI party in a player-led army reports no naval capability even though the player leader can sail. The DLC reference explicitly preserves attached-party capability inheritance ("attached-to has capability") at `prompt.md:49-50`. The base engine then makes this mismatch real: `IsCurrentlyAtSea` recursively propagates to attached parties (`MobileParty.cs:481-500`), `NavigationCapability` is recomputed per party from that party's `HasNavalNavigationCapability` (`MobileParty.cs:464-479`), and transition finish recursively calls `FinishNavigationTransitionInternal` for attached parties and then `ComputePath(MoveTargetPoint, NavigationCapability, ...)` per party (`MobileParty.cs:1716-1743`). Minimal guard: before applying the AI toggle, treat an attached party whose `AttachedTo`/army leader can sail as naval-capable, matching the DLC's attached-to inheritance shape.

3. **GAMEMODEL REPLACEMENT - DISPUTED, replacement works.**
   TAOM registers exactly one `TaomPartyNavigationModel` in `Main/SubModule.cs:368-370`, and the recursive scan found no second `PartyNavigationModel` registration in `Main/`. DryIoc registers the service once (`Main/Features/NavalTravel/NavalTravelIoC.cs:7-11`) and the IoC root includes it (`Main/IoC.cs:80-84`). Engine `CampaignGameStarter.AddModel` appends models (`CampaignGameStarter.cs:71-80`), while `GameModelsManager.GetGameModel<T>()` searches from the end of the model list (`GameModelsManager.cs:15-24`), and `GameModels` assigns `PartyNavigationModel = GetGameModel<PartyNavigationModel>()` (`GameModels.cs:301-304` and `GameModels.cs:373-374`). Therefore the later TAOM model shadows the base model.

4. **DISABLED-PATH EQUIVALENCE - CONFIRMED PARTIAL, MEDIUM.**
   Normal disabled-from-land behavior is vanilla-equivalent: `HasNavalNavigationCapability` falls through to base false at `TaomPartyNavigationModel.cs:51-55`, `CanPlayerNavigateToPosition` falls through to base at `TaomPartyNavigationModel.cs:63-66`, and the base land-only click gate is at `DefaultPartyNavigationModel.cs:64-71`. But the stronger claim "terrain methods are only ever queried with Default while disabled" is false for already-at-sea or transition state after a live MCM/config change. TAOM's terrain methods remain naval-aware regardless of `_service.IsEnabled` (`TaomPartyNavigationModel.cs:40-44`, `TaomPartyNavigationModel.cs:139-145`). Engine code can directly query `NavigationType.Naval` from transition edge calculation when the position is not on land (`NavigationHelper.cs:128-145`) and `NavigationType.All` from `MovePartyToTheClosestLand` (`MobileParty.cs:2428-2433`) or `SetMoveToNearestLand` (`MobileParty.cs:3949-3957`). This means a party made non-naval-capable by disabling the feature can still hit TAOM's naval-aware `Naval`/`All` terrain sets if it is already at sea or in transition. This is an edge case, not a fresh-disabled startup bug, but it undercuts the "exact vanilla land-only movement" claim for live disable.

5. **TERRAIN SET SEMANTICS - DISPUTED, faithful to NavalDLC; observation only.**
   TAOM's default terrain set `[8,10,11,18,19,23,24,25]` is in JSON at `Main/_Module/ModuleData/naval_travel/naval_travel_config.json:2-6` and code at `NavalTravelConfig.cs:25-35`. The DLC reference uses the same integer set at `prompt.md:40-45`. The installed v1.4.6 enum confirms `23=LandRestriction`, `24=SeaRestriction`, and `25=UnderBridge` (`TerrainType.cs:23-27`). Including `LandRestriction` and `UnderBridge` may look odd, but it is a faithful port of the DLC's naval terrain set, so I am not flagging it as a bug.

6. **CONSTRUCTOR-SNAPSHOT vs LIVE config - DISPUTED, design is internally consistent.**
   `NavalTravelConfigProvider` is lazy/cached (`NavalTravelConfigProvider.cs:24-33`) and documents restart-required JSON edits (`NavalTravelConfigProvider.cs:12-18`; `docs/features/naval-travel.md:61-65`, `docs/features/naval-travel.md:108-111`). `NavalTravelService` snapshots `NavalTerrainTypeIds` into a `HashSet` once (`NavalTravelService.cs:15-21`) and reads booleans live through settings (`NavalTravelService.cs:24-32`). `NavalTravelSettingsProvider` reads MCM booleans from `TaomSettings.Instance` on every call (`NavalTravelSettingsProvider.cs:21-25`) while threshold/terrain remain JSON-only defaults (`NavalTravelSettingsProvider.cs:27-29`). This matches the docs and gives immediate MCM toggle behavior.

## B. Port-Fidelity Diff

**Threshold**

- DLC: `GetEmbarkDisembarkThresholdDistance() => 0.5f` (`prompt.md:38-40`).
- TAOM: enabled path returns `_service.EmbarkThresholdDistance`; shipped/default config is `0.5f` (`TaomPartyNavigationModel.cs:37-38`; `NavalTravelConfig.cs:32`; JSON `naval_travel_config.json:5`).
- Difference: TAOM falls back to base `0f` when disabled (`TaomPartyNavigationModel.cs:37-38`). Intentional.

**Terrain validity**

- DLC: `Naval` is exactly `[8,10,11,18,19,23,24,25]`; `All` is naval OR base; otherwise base (`prompt.md:40-47`).
- TAOM: `ComputeTerrainValid` returns service naval set for `Naval`, naval OR base for `All`, otherwise base (`TaomPartyNavigationModel.cs:139-145`).
- Difference: TAOM makes the naval set JSON-configurable through `NavalTravelService.IsNavalTerrain` (`NavalTravelService.cs:20-35`), but the default set matches DLC exactly (`NavalTravelConfig.cs:35`).

**Invalid terrain cache**

- DLC: builds invalid cache for `Default`, `Naval`, and `All` from `IsTerrainTypeValidForNavigationType` (`prompt.md:42`, `prompt.md:48`).
- TAOM: `BuildInvalidTerrainCache` iterates every `TerrainType` and fills `Default`, `Naval`, and `All` complements from `ComputeTerrainValid` (`TaomPartyNavigationModel.cs:113-132`).
- Difference: unknown navigation type returns `Array.Empty<int>()` (`TaomPartyNavigationModel.cs:43-44`), matching the reference's "or new int[0]" behavior.

**`CanPlayerNavigateToPosition` branch-by-branch**

- DLC `n=None; face valid` (`prompt.md:52-53`) matches TAOM `navigationType=None; !vec2.Face.IsValid()` (`TaomPartyNavigationModel.cs:68-70`).
- TAOM adds `mainParty == null` guard (`TaomPartyNavigationModel.cs:72-74`), benign defensive delta.
- DLC land + target naval rejection (`prompt.md:54`) matches TAOM (`TaomPartyNavigationModel.cs:76-79`).
- DLC at-sea selection of `Naval` else `MainParty.NavigationCapability` (`prompt.md:55-57`) matches TAOM (`TaomPartyNavigationModel.cs:81-90`).
- DLC invalid terrain check (`prompt.md:58-59`) matches TAOM (`TaomPartyNavigationModel.cs:92-94`), with `Array.IndexOf` instead of LINQ `Contains`.
- DLC water target while at sea returns true (`prompt.md:60`) matches TAOM (`TaomPartyNavigationModel.cs:95-96`).
- DLC path call order (`prompt.md:61`) matches TAOM (`TaomPartyNavigationModel.cs:98-110`). Installed v1.4.6 confirms the 7th argument is `out float distance`, followed by `excludedFaceIds`, `regionSwitchCostFromLandToSea`, `regionSwitchCostFromSeaToLand` (`IMapScene.cs:34-36`). TAOM passes land-to-sea then sea-to-land (`TaomPartyNavigationModel.cs:109-110`), and the engine accessors confirm those names and values (`MobileParty.cs:3728-3739`).
- Difference: TAOM disables the whole DLC-port branch when `_service.IsEnabled` is false (`TaomPartyNavigationModel.cs:63-66`). Intentional, but see finding on disabled at-sea state.

## C. Config Cross-Reference

- `enabled`: JSON `naval_travel_config.json:2` -> DTO `NavalTravelConfig.cs:10-11` -> sanitized config `NavalTravelConfigProvider.cs:76-83` -> live settings `NavalTravelSettingsProvider.cs:21` -> service/model gates `NavalTravelService.cs:24-32`, `TaomPartyNavigationModel.cs:37-38`, `TaomPartyNavigationModel.cs:51-66`. MCM control exists at `TaomSettings.cs:535-538`.
- `applyToPlayer`: JSON `naval_travel_config.json:3` -> DTO `NavalTravelConfig.cs:13-14` -> provider `NavalTravelConfigProvider.cs:76-83` -> live settings `NavalTravelSettingsProvider.cs:23` -> service gate `NavalTravelService.cs:28-32` -> model `mobileParty.IsMainParty` mapping `TaomPartyNavigationModel.cs:51-55`. MCM control exists at `TaomSettings.cs:540-543`.
- `applyToAi`: JSON `naval_travel_config.json:4` -> DTO `NavalTravelConfig.cs:16-17` -> provider `NavalTravelConfigProvider.cs:76-83` -> live settings `NavalTravelSettingsProvider.cs:25` -> service gate `NavalTravelService.cs:28-32` -> model mapping `TaomPartyNavigationModel.cs:51-55`. MCM control exists at `TaomSettings.cs:545-548`. This key is consumed, but its current semantics cause the attached-party finding above.
- `embarkThresholdDistance`: JSON `naval_travel_config.json:5` -> DTO/default/max `NavalTravelConfig.cs:19-33` -> finite range validation `NavalTravelConfigProvider.cs:65-71` using `FiniteFloatValidator.IsFiniteInRange` (`FiniteFloatValidator.cs:21-34`) -> settings/service/model `NavalTravelSettingsProvider.cs:27`, `NavalTravelService.cs:26`, `TaomPartyNavigationModel.cs:37-38`.
- `navalTerrainTypeIds`: JSON `naval_travel_config.json:6` -> DTO/default `NavalTravelConfig.cs:25-35` -> validation/dedupe `NavalTravelConfigProvider.cs:74`, `NavalTravelConfigProvider.cs:93-124` -> `Enum.IsDefined(typeof(TerrainType), id)` at `NavalTravelConfigProvider.cs:105`, anchored to installed enum values `TerrainType.cs:3-27` -> settings/service/model `NavalTravelSettingsProvider.cs:29`, `NavalTravelService.cs:20-35`, `TaomPartyNavigationModel.cs:139-145`.

Validation is sound for the declared types: floats reject NaN/infinity before range comparison, terrain IDs must be real enum values, duplicate terrain IDs are dropped, empty/all-invalid sets revert to defaults. I found no parsed-but-unused JSON key. Tests cover all parse/validation branches and service gates (`NavalTravelConfigProviderTests.cs:43-184`; `NavalTravelServiceTests.cs:26-89`).

## D. Additional Findings

No kingdom/culture/troop/settlement IDs are involved, and I found no ID/config cross-reference issue.

Null-safety: production IoC registers the config provider, settings provider, and service as singletons (`NavalTravelIoC.cs:7-11`) before model registration resolves `INavalTravelService` (`SubModule.cs:368-370`). `CanPlayerNavigateToPosition` adds a `MainParty` null guard (`TaomPartyNavigationModel.cs:72-74`) beyond the DLC reference. No actionable null finding.

Threading/lifecycle: `_invalidTerrainCache` and `_navalTerrainIds` are built once and then read only (`TaomPartyNavigationModel.cs:29-35`, `TaomPartyNavigationModel.cs:113-132`; `NavalTravelService.cs:13-21`). Live MCM booleans are read through `TaomSettings.Instance` per call (`NavalTravelSettingsProvider.cs:21-25`). I did not find mutable shared per-tick state or save-field additions.

AI routing limitation is documented: AI sailing is on by default and distance caches are land-only (`docs/features/naval-travel.md:117-121`). That is a known gameplay limitation, not a hidden code bug.

## E. Findings Or Observations

### HIGH

[HIGH] Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:51 - Attached-party capability parity - `HasNavalNavigationCapability` keys only on `mobileParty.IsMainParty`, so `ApplyToAi=false` makes player-led army member parties non-naval while the engine still propagates sea state and transition/path recomputation to those attached parties; this diverges from the DLC's attached-to capability inheritance and can desync or strand attached parties at sea - Fix: treat an attached party whose leader/`AttachedTo` can sail as naval-capable before applying the AI gate.

### MEDIUM

[MEDIUM] Main/Features/NavalTravel/Models/TaomPartyNavigationModel.cs:40 - Disabled-path equivalence - terrain validity/cache methods remain naval-aware while disabled, and v1.4.6 has direct `NavigationType.Naval`/`All` terrain queries for at-sea or transition state, so live-disabling the feature after sailing is not exact vanilla land-only behavior and can leave non-naval-capable parties using TAOM naval terrain or unable to accept player clicks - Fix: either make disabling/de-applying naval travel wait until parties are on land/transition complete, or explicitly handle/document the at-sea disable path.

### LOW

None.

## Summary

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0

VERDICT: ISSUES FOUND
