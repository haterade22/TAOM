# Adversarial review: tournament-exit-hang fix (issue #331) -- exit-phase diagnostics + Patch60

You are reviewing a TAOM changeset that (a) adds mission-EXIT phase instrumentation to the existing BattleLoadDiagnostics feature and (b) fixes a measured 108-second loading-screen hang when exiting tournaments via a new Harmony patch (Patch60). Your job: find real bugs. Be adversarial -- try to break the state machine, the Harmony patch ordering, and the engine-interaction assumptions.

## Context

Measured root cause (verified against installed Bannerlord 1.4.6): the engine's SandBox.GauntletUI.Missions.MissionGauntletTournamentView.OnMissionScreenFinalize nulls its private _gauntletMovie/_gauntletLayer WITHOUT calling ReleaseMovie/RemoveLayer (the arena practice view, MissionGauntletArenaPracticeFightView, releases both correctly at the same hook). The leaked 'Tournament' movie -- the only mission UI holding live item-tableau/character-tableau widgets (prize item ImageIdentifierWidget, per-round weapon icons, winner CharacterTableauWidget), with a prize tableau render request typically in flight ~0.7s before exit -- is then torn down inside ScreenBase.HandleFinalize's layer loop under the exit loading screen, after the mission frame pump is dead, where it stalled 108 measured seconds (+8,276 gen0 GCs; the native Scene_view clear itself took 4ms). Patch60 replicates the practice view's ReleaseMovie -> RemoveLayer sequence at the same lifecycle point (IMissionListener.OnEndMission, mission renderer still alive) via capture-Prefix + release-Postfix.

## TAOM ID CHEATSHEET (for completeness; this changeset has no culture/kingdom config)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/features/battle-load-diagnostics.md (esp. "The mission-EXIT lifecycle" section)
- docs/features/arena.md (item 4, Patch60)
- docs/reviews/rca-tournament-exit-hang-2026-07-06.md (the deep-review RCA -- two exit-window defects already found and fixed; verify the fixes are correct, do not re-report them)
- CHANGELOG.md top two entries (2026-07-06)

## FILES TO REVIEW

Diagnostics (modified):
- Main/Features/BattleLoadDiagnostics/IBattleLoadDiagnosticsService.cs
- Main/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsService.cs
- Main/Features/BattleLoadDiagnostics/Domain/BattleLoadPhase.cs

Diagnostics hooks (new):
- Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMission_ExitPhase_Patch.cs
- Main/Features/BattleLoadDiagnostics/Hooks/Mission_EndMissionInternal_ExitPhase_Patch.cs
- Main/Features/BattleLoadDiagnostics/Hooks/Mission_ClearUnreferencedResources_ExitPhase_Patch.cs
- Main/Features/BattleLoadDiagnostics/Hooks/MissionState_OnFinalize_ExitPhase_Patch.cs
- Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnActivate_ExitPhase_Patch.cs
- Main/Features/BattleLoadDiagnostics/Hooks/MapState_OnTick_ExitPhase_Patch.cs

The fix (new):
- Main/Features/Arena/Hooks/Patch60_TournamentExitMovieRelease.cs

Wiring: Main/SubModule.cs (search for "Patch43_BattleLoadDiagnostics" and "Patch60_TournamentExitMovieRelease" -- the Initialize calls + PatchCategory calls in OnGameInitializationFinished)

Tests:
- TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadDiagnosticsServiceTests.cs (exit-phase tests at the bottom)
- TAOM.Tests/Features/Arena/Patch60TournamentExitMovieReleaseTests.cs

## VANILLA CODE (decompile and paste as code blocks -- REQUIRED)

Installed DLLs (1.4.6). Main bin: E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/. Note TaleWorlds.MountAndBlade.View.dll lives in Modules/Native/bin/Win64_Shipping_Client/; SandBox.GauntletUI.dll lives in Modules/SandBox/bin/Win64_Shipping_Client/.

1. TaleWorlds.MountAndBlade.Mission: EndMission, EndMissionInternal, ClearUnreferencedResources, OnMissionStateFinalize
2. TaleWorlds.MountAndBlade.MissionState: OnFinalize, OnTick (the State.Over -> PopState branch)
3. TaleWorlds.CampaignSystem.GameState.MapState: OnActivate, OnTick
4. SandBox.GauntletUI.Missions.MissionGauntletTournamentView: full class (OnMissionScreenInitialize, OnMissionScreenFinalize)
5. SandBox.GauntletUI.Missions.MissionGauntletArenaPracticeFightView: OnMissionScreenFinalize (the reference sequence)
6. TaleWorlds.Engine.GauntletUI.GauntletLayer: ReleaseMovie, OnFinalize, ClearContext
7. TaleWorlds.ScreenSystem.ScreenBase: RemoveLayer, HasLayer, HandleFinalize, AddLayer
8. TaleWorlds.MountAndBlade.View.Screens.MissionScreen: the IMissionListener.OnEndMission implementation (the view finalize loop) and UnregisterView

## KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence)

S1. Patch60's Postfix calls ScreenBase.RemoveLayer DURING MissionScreen's mission-view iteration (IMissionListener.OnEndMission ForEach over _missionViewsContainer). RemoveLayer mutates ScreenBase._layers -- verify no collection-modified-during-enumeration hazard: is _layers enumerated anywhere on this call path (HandleFinalize's loop runs LATER; the ForEach is over views not layers)? Also verify RemoveLayer's HandleDeactivate branch (screen still active at EndMission time) is safe for a layer whose focus was already dropped by the original body.

S2. Focus bookkeeping: the original body calls ScreenManager.TryLoseFocus BEFORE nulling fields; our Postfix then calls RemoveLayer which (when screen active) calls HandleDeactivate -> TryLoseFocus again on the same layer. Double TryLoseFocus -- safe/idempotent? Decompile ScreenManager.TryLoseFocus + ScreenLayer.HandleDeactivate and confirm.

S3. Mission_EndMission_ExitPhase_Patch opens the exit window on Mission.EndMission with Campaign.Current != null. Are there CAMPAIGN missions that end WITHOUT MapState ever activating afterward (e.g. mission-to-mission chains: conversation mission -> battle, siege chain, hideout -> map?) where the window would stay open until the next Mission.Initialize closes it -- and could any EXIT phase (ClearUnreferencedResources/MissionState.OnFinalize probes) then stamp lines belonging to the WRONG mission's lifecycle in between? Trace one concrete chain.

S4. LogExitBegin restarts the shared _stopwatch and _seq that the ENTRY phases also use. If PlayerEncounter.Start (ResetLifecycle) for the NEXT encounter fires while the previous mission's exit is still mid-teardown (is that ordering possible? MissionState pops before the map is interactive -- verify), entry stamps could get a clock restarted mid-exit or vice versa. Confirm impossible or bound the damage.

S5. Patch60 forward-compat: if a future engine version fixes the leak (adds ReleaseMovie/RemoveLayer to the original body), our Postfix would call ReleaseMovie on an identifier no longer in _movieIdentifiers (FailedAssert no-op in shipping?) and RemoveLayer guarded by HasLayer -> skip. Confirm degraded behavior is a no-op, not a crash.

S6. The MapState_OnTick postfix runs EVERY map frame forever. Confirm the inactive-window path allocates zero and cannot throw (what does IsExitWindowActive read? any way _service is non-null but partially initialized?). Also: MapState.OnTick has early returns -- Harmony postfix still runs; confirm no path where the postfix observes torn state.

## REQUIRED SECTIONS

1. VANILLA CODE -- paste the decompiled bodies listed above.
2. STATE-MACHINE ANALYSIS -- walk the exit-window latch through: normal tournament exit; battle exit with loot screens; custom battle (window must NOT open -- Campaign.Current gate); chained missions; quit-to-main-menu mid-mission then load save (documented known limitation -- confirm severity is cosmetic); MCM toggle off mid-window then on (fixed -- verify the fix).
3. HARMONY INTERACTION -- Patch60 vs the two other patch classes on Mission teardown surfaces (the 6 diagnostics hooks) and vs Patch16_AtmospherePersistence (Mission.Initialize prefix) and Patch43's Mission_Initialize_BattleLoad_Patch: any ordering hazards.
4. CONFIG CROSS-REFERENCE -- this changeset has no data config; instead cross-reference the LOG CONTRACT: phase names in BattleLoadPhase enum vs docs/features/battle-load-diagnostics.md vs CHANGELOG -- all identical?
5. FINDINGS OR OBSERVATIONS -- numbered, each with severity (P1 blocking / P2 should-fix / P3 nice-to-have), file:line, code-block evidence, and a concrete failure scenario. If a section yields nothing, write "No findings" -- do not pad.

## QUALITY GATES

- Every finding must cite file:line from code you actually read and include the decompiled vanilla evidence where relevant.
- Do not report style preferences as findings.
- Do not re-report the two already-fixed deep-review findings (toggle-gated state transition; stale-window leak) -- but DO verify their fixes are complete and correct.
- Distinguish "bug" from "documented known limitation" (the doc lists one).

## Prior review lessons

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Army-attachment propagation (NavalTravel) caught by decompiling the engine consumer.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

Output your review to stdout (it is redirected to docs/reviews/codex-adversarial-tournament-exit-hang-2026-07-06.md).
