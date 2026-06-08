# RCA: Music Integration Deep Review — 2026-06-08

## Top-line Summary

Deep review of the Music Integration feature (branch `Theboyys-Music-Integration-1.4.5`) found 0 standards violations, 0 API incompatibilities, 2 HIGH efficiency findings, 2 HIGH data-flow findings, and several MEDIUM items. The two efficiency HIGH items were fixed in-session. The two data-flow HIGH items require a user decision (dead code removal vs MCM implementation roadmap). Two MEDIUM items (sticky tavern flag, FactionMap CC music tick) require investigation before ship.

---

## Findings Table

| # | Sev | Category | Bug | Why Missed | Preventive Action |
|---|-----|----------|-----|------------|-------------------|
| 1 | HIGH | Efficiency | `MusicianGroupSuppressionPatchHelper.ShouldSuppressVanillaTavernMusic()` calls `IoC.Resolve<T>()` 3× per invocation, from 4 Harmony patches on `MusicianGroup` methods that fire continuously during tavern gameplay. | Hot-path IoC check was not applied to helper classes — only to patches and service constructors directly. Helper is not a `[HarmonyPatch]` class so Agent 3's "Harmony Patch Overhead" check didn't apply; it slipped past the general hot-path scan. | **FIXED in-session.** Added 3 static lazy-cached `??=` fields. Rule: any `static` class that is called from a per-frame or per-tavern-tick patch is itself on the hot path — IoC caching applies. |
| 2 | HIGH | Efficiency | `CharacterCreationAmbientSuppressor.Suppress()` calls `AccessTools.TypeByName()` + `AccessTools.Method()` on every invocation, from the CC `OnFrameTick` Harmony patch (per-frame on CC screen). | Same scope gap as finding #1: reflection-caching rule focuses on `[HarmonyPatch]` classes; this is a helper class called from one. | **FIXED in-session.** Added static `_cachedStopSoundMethod` field, `??=` init. |
| 3 | HIGH | Data Flow | `MusicSettingsProvider.GetSnapshot()` always returns `MusicSettingsSnapshot.Default`. 10 snapshot properties (`FadesEnabled`, `FadeInSeconds`, `FadeOutSeconds`, `LoggingEnabled`, `LogBucketTransitions`, `LogTrackStarts`, `CustomBattleProfileEnabled`, `CustomBattleVolume`, `CampaignVolume`, `MissionVolume`) are declared and validated but never read by any consumer. | MCM stub was an intentional design decision (settings are designed but not yet wired to MCM). Snapshot properties were authored speculatively for future implementation. Per `simplicity-criterion.md` (YAGNI), these should not ship until they gate real behavior. | **User decision required.** Either (A) delete the 10 dead properties + their constructor params, re-add when MCM is implemented, OR (B) accept them as design debt and record a GitHub issue tracking "implement MCM for music settings." |
| 4 | MEDIUM | Data Flow | `ExitTavernContext()` only called in `MusicMissionBehavior.ClearMissionContexts()`. If `MusicianGroup.SetPlayList` does not fire again on leaving a tavern (edge case: musician group stops before player exits), `IsInTavernContext` is permanently sticky for the session, forcing the tavern music bucket on the world map. | Lifecycle matrix checked mission-start/end and session states; the "enter without corresponding exit" path for the tavern context source was not traced. | Trace all set-but-not-cleared static/session state. Add `ExitTavernContext()` call in a suitable campaign event (e.g., `OnPlayerCharacterChangedEvent` or on `SetPlayList` with empty/null list). |
| 5 | MEDIUM | Data Flow | `FactionMap CultureStageViewCreatedHook` calls `SelectCulture()` but the CC music tick is driven by `CharacterCreationScreen_OnFrameTick_MusicPatch` targeting `CharacterCreationScreen.OnFrameTick`. If the FactionMap GauntletLayer replaces the movie overlay and the original screen's `OnFrameTick` is still called, CC music ticks correctly. If not, CC music never plays in FactionMap. | Cross-feature interaction between FactionMap injection and CC music patch scope was not in scope for any single agent. Data flow agent caught it as a "needs verification" item. | Verify at runtime. If `OnFrameTick` is not called under FactionMap, drive the CC music tick from `CultureStageViewCreatedHook`'s own update path or from `FactionSelectionVM`. |
| 6 | MEDIUM | Efficiency | `NoRepeatShufflePicker.PickDeterministicRoundRobin()` allocates a new `List<MusicTrackDefinition>` + `Sort()` on every round-robin pick. `ComputeSignatureOrderIndependent()` allocates a new `List<string>` per call. | Standard per-method review; `NoRepeatShufflePicker` is not on a tick path (fires on track start/end, not per-frame), so severity is MEDIUM. Not a blocking issue. | Cache the sorted list + signature in `ShuffleBagState`. Low urgency given call frequency. |
| 7 | MEDIUM | Efficiency | `MusicCampaignBehavior.RegisterEvents()` subscribes to 4 campaign events with no `OnGameEnd()` override to unsubscribe. | Event subscription cleanup was checked for `OnRemoveBehavior` (mission behaviors) but not for `CampaignBehaviorBase.OnGameEnd` (campaign behaviors). | Add `OnGameEnd()` override unsubscribing all registered events. |
| 8 | LOW | Data Flow | `MusicRouteSettings.IsBucketEnabled(MusicBucket.CharacterCreation)` returns `TownEnabled`. Disabling Town music silently disables CC music. No dedicated CC enable toggle. | Undocumented coupling, caught by bucket-enable enum coverage trace. | Document in feature doc that CC music is gated by the Town toggle, or add a dedicated CC enable property. |
| 9 | LOW | Data Flow | `MusicCampaignBehavior.OnSessionLaunched` resets timer but does not call `_playback.Stop()`. | Non-blocking; engine destroys the sound channel on session end. Low risk. | Add explicit `_playback.Stop("session_end")` in `OnSessionLaunched` reset path for defensive cleanup. |
| 10 | LOW | Completeness | CHANGELOG not updated. Feature doc `docs/features/music.md` does not exist. | Completeness gate fires after review; these are authored separately. | Write `docs/features/music.md` and CHANGELOG entry before closing issue. |
| 11 | LOW | Tooling | `tools/replace_taom_music_assets.ps1` deletes old OGGs before confirming all new ones are written. Partial ffmpeg failure leaves broken state. | Tooling review catches this class but no backup step was in the original implementation. | Add `Move-Item` backup before delete, `Remove-Item` backup on full success. |

---

## Root-Cause Patterns

### Pattern A: Helper classes inherit hot-path classification from their callers

Findings #1 and #2 share the same root cause: `[HarmonyPatch]` class reviews check the patch class itself for hot-path IoC and reflection calls. Helper classes invoked from within those patches are equally on the hot path but escape per-class review. The pattern is identical to `feedback_native_port_hot_path_audit.md` (port reviews that don't apply the same audit to inlined helpers).

**Preventive rule extension:** Agent 3's Harmony Patch Overhead check should explicitly read 1-2 levels of call depth from each patch method, not just the patch class body.

### Pattern B: YAGNI violations in "design-ready" stub infrastructure

Finding #3 follows the same pattern as career system Phase-2 features and RevoltTuning config: properties are declared "for MCM" but not connected. Per `simplicity-criterion.md` this is YAGNI. But it's also a deliberate design pattern in TAOM (stub the public API surface, implement wire-up later). The tension: stub-then-wire is pragmatic if the MCM implementation immediately follows; it's YAGNI if it sits for months.

**Rule:** Any stub property in a public settings snapshot must have a corresponding GitHub issue with "MCM: wire up [setting]" tracking it. Zero-issue stubs become YAGNI at the next deep review.

### Pattern C: Lifecycle exit missing for session-scope context sources

Finding #4 (tavern context sticky) and a prior similar finding in SiegeDefense share a root: service state is set by an entry signal but cleared only by a mission-end event. The gap fires when the session exits the state via a path that doesn't trigger mission-end (the "graceful exit through non-mission path" case).

**Rule:** For every `EnterXxxContext()` call, verify a corresponding `ExitXxxContext()` is reachable from ALL exit paths, not just the primary one. This should be a dedicated check in Agent 5.

---

## Why Each Agent Missed These

| Agent | Missed | Why |
|-------|--------|-----|
| Agent 1 (Standards) | #1, #2 | Standards checks don't cover hot-path classification of helper classes. |
| Agent 2 (Compatibility) | — | No missed findings. |
| Agent 3 (Efficiency) | #3, #4, #5, #7 | Efficiency checks are per-class; cross-class and data-flow issues are Agent 5's domain. |
| Agent 4 (Completeness) | #3 (partial) | Flagged MCM stub correctly; didn't enumerate all dead properties. |
| Agent 5 (Data Flow) | #1, #2 | Data flow doesn't cover implementation-level performance; hot-path analysis is Agent 3's domain. |

Findings #1 and #2 were caught by Agent 3 specifically because it was instructed to trace call depth. This confirms the value of Agent 3's "IoC.Resolve in Hot Paths" check, but highlights that it needs to recurse into helper classes.

---

## Feedback Memories to Codify

- `feedback_hotpath_helper_class_ioc_and_reflection.md` — static helpers called from per-frame patches are equally on the hot path; apply IoC caching and reflection caching rules to them, not just to the patch class body.
- `feedback_exit_context_all_paths.md` — for every `EnterXxx()` call in a context source, verify `ExitXxx()` is reachable from ALL session-exit paths (mission end, session end, player leave).

---

## Post-Review Deep Review Findings (2026-06-08 second pass)

Two additional findings found during the second deep-review pass (after vanilla psai suppression fix):

| # | Sev | Bug | Fixed |
|---|-----|-----|-------|
| 12 | MED | `MusicMissionBehavior.ClearMissionContexts()` called `IoC.Resolve<IMusicTavernContextSource>()` in a private method body — service locator in a non-boundary class. | **FIXED** — injected via constructor as 5th optional param. |
| 13 | LOW | `MusicCampaignBehavior.OnSessionLaunched` was `private` but referenced in `MusicRuntimeHookTests` — tests project failed to build. | **FIXED** — access modifier changed to `internal`. |

Documented known limitations (not bugs — design choices for initial ship):
- `MusicSettingsProvider` always returns `MusicEnabled = true` (no MCM toggle yet)
- `MBMusicManager.StartTheme` suppression is global — silence rather than vanilla fallback when TAOM has no culture track during a mission
