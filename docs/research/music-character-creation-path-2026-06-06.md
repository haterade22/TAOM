# Music Character Creation Path Research - 2026-06-06

Scope: decompile and research the character-creation music path before implementation. This step does not add runtime hooks, services, or tests.

## Sources Checked

- `docs/research/music-integration-plan-2026-06-06.md:350-367` defines this step: inspect source-drop `TaomCharacterCreationMusicController.cs`, inspect the source-drop character-creation reflection installer, decompile the v1.4.5 character-creation culture-selection VM/state path, inspect TAOM `CharacterCreationContent_SetSelectedCulture_Patch.cs`, inspect `CultureSettingService.cs`, and prove an observable selected-culture seam without duplicating scanner behavior.
- `docs/research/music-integration-plan-2026-06-06.md:162-168` already warns that character-creation music must not reuse the source drop's broad reflection scanner and must prefer existing TAOM CharacterCreation/FactionMap seams.
- Source-drop `TaomCharacterCreationMusicController.cs` and `TaomCharacterCreationMusicBehavior.cs` are compile-compatibility shims. Their comments say the runtime CC detection/signal/controller path is intentionally disabled and routed through the world bucket path instead.
- Source-drop `SubModule.cs:1114-1245` installs a broad character-creation selection scanner across vanilla VMs plus TAOM FactionMap VM/service/widget types. It patches `CharacterCreationCultureVM.ExecuteSelectCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `FactionSelectionVM.ExecuteSelectRegion`, `FactionSelectionVM.ExecuteConfirm`, `FactionSelectionService.SelectRegion`, `PolygonWidget.OnPreviewMousePressed`, and `CultureSettingService.SetCultureOnCharacterCreation`.
- Source-drop `SubModule.cs:225-287` drives a character-creation bridge by reading an active-state culture signal, submitting a world-bucket campaign snapshot, and ticking campaign playback.
- Source-drop `TaomSharedPlaybackOwner.cs:713-731` shows `SubmitCharacterCreationSnapshot` is inert and `SubmitWorldBucketSnapshotForMenuLike` is the active menu/CC route in that drop.
- Decompiled installed v1.4.5:
  - `TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationCultureVM`
  - `TaleWorlds.CampaignSystem.ViewModelCollection.CharacterCreation.CharacterCreationCultureStageVM`
  - `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationContent`
  - `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationManager`
  - `TaleWorlds.CampaignSystem.CharacterCreationContent.CharacterCreationState`
  - `SandBox.GauntletUI.CharacterCreation.CharacterCreationCultureStageView`
  - `SandBox.View.CharacterCreation.CharacterCreationScreen`
- Inspected TAOM:
  - `Main/Features/CharacterCreation/Hooks/CharacterCreationContent_SetSelectedCulture_Patch.cs`
  - `Main/Features/FactionMap/CultureSettingService.cs`
  - `Main/Features/FactionMap/Hooks/CultureStageViewCreatedHook.cs`
  - `Main/Features/FactionMap/ViewModels/FactionSelectionVM.cs`
  - `Main/Features/FactionMap/FactionSelectionService.cs`
  - `Main/Features/Music/MusicRouteSnapshot.cs`
  - `Main/Features/Music/MusicTransitionResolver.cs`

## Decompiled v1.4.5 Selection Path

- `CharacterCreationCultureVM` stores a `CultureObject Culture`, exposes `CultureID`, and `ExecuteSelectCulture()` only calls the constructor-injected `Action<CharacterCreationCultureVM> _onSelection`.
- `CharacterCreationCultureStageVM` constructs one `CharacterCreationCultureVM` per `CharacterCreationContent.GetCultures()` item and passes its private `OnCultureSelection` method as the VM selection action.
- `CharacterCreationCultureStageVM.OnCultureSelection(CharacterCreationCultureVM selectedCulture)`:
  - Updates the player preview face/body for the selected culture.
  - Clears previous `IsSelected` flags.
  - Sets `selectedCulture.IsSelected = true`.
  - Sets `CurrentSelectedCulture`.
  - Sets `AnyItemSelected` and `CanAdvance`.
  - Invokes `_onCultureSelected?.Invoke(selectedCulture.Culture)`.
- `CharacterCreationCultureStageVM.OnNextStage()` is the persistence point. It calls `(GameStateManager.Current.ActiveState as CharacterCreationState).CharacterCreationManager.CharacterCreationContent.SetSelectedCulture(CurrentSelectedCulture.Culture, CharacterCreationManager)` and then advances.
- `CharacterCreationContent.SetSelectedCulture(CultureObject culture, CharacterCreationManager characterCreationManager)` sets `SelectedCulture`, resets menu options, resets selected title type, generates a clan name, and changes the player clan name.
- `CharacterCreationState` exposes public `CharacterCreationManager { get; private set; }`; its constructor creates a new manager, `OnActivate()` calls `CharacterCreationManager.OnStateActivated()`, and `Refresh()` delegates to the handler.
- `CharacterCreationScreen` creates vanilla ambient CC audio in its constructor with `SoundEvent.CreateEventFromString("event:/mission/ambient/special/charactercreation", null)` and immediately calls `Play()`. It stops that event in private `StopSound()`, called from `IGameStateListener.OnFinalize()`.
- `CharacterCreationScreen.OnFrameTick(float dt)` calls `_currentStageView?.Tick(dt)`, so it is the exact screen-lifetime tick driver if a future patch needs a character-creation screen tick.
- `CharacterCreationCultureStageView` creates `CharacterCreationCultureStageVM` and supplies its own `OnCultureSelected(CultureObject culture)` callback. That callback only sets vanilla `SoundManager.SetGlobalParameter("MissionCulture", ...)`; it does not persist `CharacterCreationContent.SelectedCulture`.

## TAOM-Owned Observable Seams

- Existing `CharacterCreationContent_SetSelectedCulture_Patch` is already an explicit Harmony postfix on `CharacterCreationContent.SetSelectedCulture`. It receives the exact `CultureObject culture` argument at the persistence point and currently delegates only `culture.StringId` to `ICCBodyPropertiesService`.
- `CultureSettingService.SetCultureOnCharacterCreation(CultureObject culture, object viewInstance, object? originalDataSource)` already handles the FactionMap path. It sets `Hero.MainHero.Culture`, reflectively invokes `CharacterCreationContent.SetSelectedCulture(...)`, fixes the vlandia/Rohan clan-name special case, and syncs the original vanilla VM selection when available.
- `CultureStageViewCreatedHook` wires FactionMap confirmation as `Action<CultureObject> onCultureConfirmed`, which calls `CultureSettingService.SetCultureOnCharacterCreation(...)` and then invokes next-stage progression.
- `FactionSelectionVM.ExecuteConfirm()` resolves `cultureId = IFactionSelectionService.GetCultureIdForRegion(_selectedRegionName)`, resolves that id to a `CultureObject`, and invokes the `Action<CultureObject>`.
- Therefore the strongest first implementation seam is the existing `CharacterCreationContent.SetSelectedCulture` postfix path. It observes both vanilla culture-stage confirmation and TAOM FactionMap confirmation without source-drop scanner behavior.

## 2026-06-07 FactionMap Region Selection Follow-Up

- In-game test showed FactionMap area selection changes the visible faction panel before confirmation, but the music route only changed after the later confirmation path.
- Research basis: the source drop's broad scanner included `FactionSelectionVM.ExecuteSelectRegion`, but this doc rejects porting that scanner wholesale; TAOM must use its own explicit FactionMap boundary instead.
- TAOM path: `FactionSelectionVM.ExecuteSelectRegion()` is the controlled selected-region boundary, while `FactionSelectionVM.ExecuteConfirm()` and `CultureSettingService.SetCultureOnCharacterCreation(...)` remain confirmation/persistence boundaries.
- Implementation rule: `ExecuteSelectRegion()` may pass only a string culture id from `FactionSelectionResult.CultureId` into `ICharacterCreationMusicContextService.SelectCulture(string)`. It must not pass `CultureObject` or duplicate the source-drop reflection scanner.
- Smoke marker for this earlier selection signal: `culture_selected source=faction_map_region_selected culture=<cultureId>`. The later persistence signal remains `culture_confirmed culture=<cultureId>`.

## Music-Layer Constraints

- TAOM already has `MusicBucket.CharacterCreation`, `MusicTrackIndex` parses `character_creation` paths, `MusicRotationPolicy` handles `CharacterCreationRotateIntervalSeconds`, `MusicRouteSettings.IsBucketEnabled(CharacterCreation)` maps it to `TownEnabled`, and `MusicSettingsSnapshot.GetBucketVolume(CharacterCreation)` maps it to town volume.
- `MusicRouteSnapshot` currently has no `CharacterCreation` flag.
- `MusicTransitionResolver.BuildMissionOrder(...)` and `BuildCampaignOrder(...)` currently never yield `MusicBucket.CharacterCreation`.
- A character-creation implementation that intends to play the imported `character_creation/<culture>` tracks must first extend the pure route snapshot/resolver tests to support `MusicBucket.CharacterCreation`. Otherwise a character-creation signal can only be forced through `World` or `Town`, which would not use the `character_creation` bucket.
- Existing `MusicCampaignBehavior` is driven by `CampaignEvents.TickEvent`; it is not proven here that this event is a reliable tick driver while `CharacterCreationState` is active. `SubModule.OnApplicationTick(float dt)` already runs every app tick, and `CharacterCreationScreen.OnFrameTick(float dt)` is also decompiled as a screen-lifetime tick. Either can be a thin boundary, but implementation should test the pure service separately and keep TaleWorlds types at the boundary.

## Rejected Source-Drop Behavior

- Do not port `TryInstallCharacterCreationCultureSelectionPatch()` as-is. It scans and patches multiple vanilla and TAOM types and tries to infer culture from VM fields, map widgets, region/faction display strings, and nested active-state members.
- Do not port the inert `TaomCharacterCreationMusicController`/`TaomCharacterCreationMusicBehavior` as feature code. Their own comments state CC runtime is disabled.
- Do not route character creation through the world bucket unless that is an explicit design decision. TAOM has a parsed `MusicBucket.CharacterCreation`; the source drop's world-bucket bridge was a compatibility workaround, not the clean TAOM route.

## Recommended Next Implementation Boundary

1. Add pure tests for `MusicRouteSnapshot`/`MusicTransitionResolver` support for `MusicBucket.CharacterCreation`.
2. Add a pure character-creation music context service that stores the last confirmed culture id and builds a character-creation `MusicRouteSnapshot`.
3. Extend the existing `CharacterCreationContent_SetSelectedCulture_Patch` postfix to delegate `culture.StringId` to that service. Do not pass `CultureObject` into the service.
4. Add a thin runtime tick/exit boundary. Candidate researched options:
   - `SubModule.OnApplicationTick(float dt)`: check active `CharacterCreationState` at the boundary and delegate to the service/playback.
   - Explicit `CharacterCreationScreen.OnFrameTick(float dt)`/`OnFinalize()` patches: screen-lifetime driver and cleanup, but requires new binding tests for `SandBox.View.CharacterCreation.CharacterCreationScreen`.
5. Decide separately whether TAOM music should suppress or coexist with vanilla `CharacterCreationScreen` ambient `SoundEvent`. If suppressing, research and bind `_cultureAmbientSoundEvent` or `StopSound()` explicitly before implementation.

## Ambient Suppression Decision - 2026-06-06

- Re-decompiled installed v1.4.5 `SandBox.View.CharacterCreation.CharacterCreationScreen` from `A:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\SandBox\bin\Win64_Shipping_Client\SandBox.View.dll`.
- The screen owns a private `SoundEvent _cultureAmbientSoundEvent` field.
- The constructor assigns `_cultureAmbientSoundEvent = SoundEvent.CreateEventFromString("event:/mission/ambient/special/charactercreation", null)` and immediately calls `_cultureAmbientSoundEvent.Play()`.
- Private `StopSound()` calls `SoundManager.SetGlobalParameter("MissionCulture", 0f)`, calls `_cultureAmbientSoundEvent.Stop()` when non-null, and then sets `_cultureAmbientSoundEvent = null`.
- Explicit `IGameStateListener.OnFinalize()` calls `StopSound()` before destroying the generic render scene.
- Decision: TAOM should suppress vanilla character-creation ambient only after TAOM playback successfully starts or continues a `MusicBucket.CharacterCreation` track. This prevents layered vanilla ambient plus TAOM authored character-creation music, while preserving vanilla ambient if TAOM cannot play a character-creation track.
- Binding/test requirement: `CharacterCreationAmbientSuppressor.ResolveStopSoundMethod()` must resolve private `CharacterCreationScreen.StopSound()` against the installed v1.4.5 assembly, and helper tests must prove suppression happens once for started/continued CharacterCreation playback but not for failed playback.

## In-Game Smoke Path - 2026-06-06

- The smoke path is observable through `[Patch46-Music][CCSmoke]` log markers.
- Expected order after selecting and confirming a culture in character creation:
  1. Optional FactionMap early selection: `culture_selected source=faction_map_region_selected culture=<cultureId>` from `FactionSelectionVM.ExecuteSelectRegion()`. This proves map area selection reached the music context before final confirmation.
  2. Vanilla culture list early selection: `culture_selected source=vanilla_culture_vm_execute_select culture=<cultureId>` from `CharacterCreationCultureVM.ExecuteSelectCulture()`.
  3. `culture_confirmed culture=<cultureId>` from the `CharacterCreationContent.SetSelectedCulture` postfix. This proves the TAOM-owned persistence seam fired.
  4. `cc_bucket_owned outcome=Started culture=<cultureId> track=<eventName> channel=<channel>` from the screen tick helper after `IMusicPlaybackService.Update(...)` returns started or continued `MusicBucket.CharacterCreation` playback. This proves the character-creation snapshot routed to the character-creation bucket and the engine adapter accepted the track.
  5. `vanilla_ambient_suppressed after_outcome=Started track=<eventName>` after `CharacterCreationAmbientSuppressor.Suppress(...)` successfully invokes private `CharacterCreationScreen.StopSound()`. This proves vanilla ambient stops only after TAOM owns the character-creation music route.
- A failed TAOM character-creation playback must not emit `vanilla_ambient_suppressed`; vanilla ambient remains as fallback.
