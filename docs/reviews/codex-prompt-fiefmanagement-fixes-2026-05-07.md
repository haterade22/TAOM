# Codex Self-Review: FiefManagement Fix Pass (Review #38b)

You are Codex performing an INDEPENDENT adversarial self-review of the FIXES applied in response to your prior review (Review #38, file: `docs/reviews/codex-adversarial-fiefmanagement-2026-05-06.md`).

The goal is NOT to re-review the feature from scratch — it is to find bugs INTRODUCED BY THE FIXES, or original-bug residue the fixes only partially closed. Look at the fix commits with fresh skepticism. Past self-reviews (Review #28, #35b) caught Claude shipping fixes that introduced new HIGH bugs.

## What was fixed (7 findings from Review #38)

1. **P1 — GameState creation bypass.** Behavior was: `manager.PushState(new FiefManagementGameState(fief))`. Fix: `var state = manager.CreateState<FiefManagementGameState>(); state.Initialize(settlement); manager.PushState(state);`. `FiefManagementGameState` now has parameterless ctor + `Initialize(Settlement)` method.

2. **P1 — Reflection swap restored too early.** Behavior was: Swap inside `OnInitialize`, restore inside `finally` block immediately after `new TownManagementVM()`. Fix: keep swap active for whole screen lifetime — Swap in `OnInitialize` (with try/catch that restores on construction failure), Restore in `OnFinalize`.

3. **P2 — F6 patch missing `IsInMenu` guard.** Fix: Postfix now takes `MapScreen __instance` and adds `if (__instance == null || __instance.IsInMenu) return`.

4. **P2 — Menu registered conditionally on session-launch setting value.** Fix: registration unconditional in `OnSessionLaunched`. Runtime gate moved to F6 patch + each option's condition lambda (`_settings.EnableFiefManagement && _menuFiefs.Count > 1` etc.).

5. **P2 — `FiefSummary` carried sealed `Settlement` ref.** Fix: `FiefSummary` now carries only `Id / Name / IsTown / IsCastle`. New `ISettlementOwnershipAdapter.Resolve(string id)` re-resolves the sealed Settlement at the consequence boundary, with fresh ownership re-validation.

6. **P2 — `FiefHubCampaignBehavior` 184 lines.** Fix: extracted menu state caching + title rendering + step navigation into `IFiefHubMenuPresenter` / `FiefHubMenuPresenter`. Behavior is now ~85 lines.

7. **P3 — Debug-hint HUD promise mismatch.** Fix: hint text changed to "Write diagnostic [FiefManagement] messages to the TAOM file log. Off = silent."

## Files to read (the FIX surface)

- `Main/Features/FiefManagement/Models/FiefManagementGameState.cs` — new parameterless ctor + Initialize
- `Main/Features/FiefManagement/Hooks/FiefHubCampaignBehavior.cs` — uses CreateState + Initialize + PushState; unconditional menu registration; condition lambdas now read presenter state and live setting
- `Main/Features/FiefManagement/Hooks/Patch36_MapScreenF6.cs` — IsInMenu guard, once-per-process exception logger, message + ActivateGameMenu inlined at boundary
- `Main/Features/FiefManagement/Hooks/Patch36_GameStateScreenManager.cs` — unchanged but verify still substitutes correctly
- `Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs` — swap lifetime extended; try/catch restores on ctor failure
- `Main/Features/FiefManagement/IFiefHubMenuPresenter.cs` + `FiefHubMenuPresenter.cs` — NEW presenter
- `Main/Features/FiefManagement/IFiefHubService.cs` + `FiefHubService.cs` — pure query layer, no engine imports
- `Main/Features/FiefManagement/Models/FiefSummary.cs` — DTO without Settlement ref
- `Main/Adapters/ISettlementOwnershipAdapter.cs` + `SettlementOwnershipAdapter.cs` — added Resolve method with ownership re-validation
- `Main/Features/TaomSettings.cs` lines 274-287 — three settings, debug hint updated
- `TAOM.Tests/Features/FiefManagement/FiefHubServiceTests.cs` — test signatures updated for new FiefSummary

## Known Suspects on the FIX surface

CONFIRM or DISPUTE each. These are hypotheses about whether the fixes are CORRECT or introduce NEW bugs:

1. **CreateState<T> requires `new()` constraint at compile time.** `FiefManagementGameState` now has BOTH a parameterless ctor AND parameterless behavior (the user calls `state.Initialize(fief)` after construction). Question: is there a window where the state is pushed before `Initialize` runs? Trace: line in `FiefHubCampaignBehavior.OnSessionLaunched` manage-consequence: `var state = manager.CreateState<FiefManagementGameState>(); state.Initialize(settlement); manager.PushState(state);`. Between CreateState and PushState, OnCreateState fires (synchronously inside HandleCreateState) → CreateScreen fires → our Patch36 prefix instantiates `GauntletFiefManagementScreen(state, swapper)` → screen constructor stores `_state` reference. Then OnInitialize fires (when?). If OnInitialize runs BEFORE Initialize, `_state.Fief` is null when the screen tries to swap. Verify the ordering by reading vanilla.

2. **Swap held across screen lifetime: cross-feature interference.** `MobileParty._currentSettlement` is now swapped from screen open until close. While that screen is active, ANY other code that reads `MobileParty.MainParty.CurrentSettlement` sees the wrong value. Question: is the assumption "IsMenuState=true stops campaign time" airtight? List anything that runs while a menu state is active: 
   - Application tick handlers (`MBSubModuleBase.OnApplicationTick`) — DOES run during menu states
   - `ScreenManager` UI ticks — DOES run during menu states  
   - Other Harmony patches on `MobileParty` properties or `Settlement.CurrentSettlement` reads — could fire from app tick
   Check if any TAOM feature or any vanilla code path reads MainParty.CurrentSettlement from app tick / UI tick / OnFrameTick of MapScreen-while-state-pushed. If yes, the lifetime-swap is unsafe.

3. **Menu unconditional registration: option lambda gating completeness.** With registration unconditional, runtime gate must cover EVERY menu activation path. Question: when `EnableFiefManagement=false`:
   - F6 patch returns early (gate at Patch36_MapScreenF6:30) — confirmed
   - Manage option condition returns false (gate at FiefHubCampaignBehavior:64) — confirmed  
   - Prev/Next options condition returns false (gate at FiefHubCampaignBehavior:46/52) — confirmed
   - But does Leave still work? It returns true unconditionally. If a player somehow has the menu open when they toggle Enable=false, can they get out? Currently leave option always returns true (does NOT check EnableFiefManagement) — this is correct, but verify by reading.
   - Can an external caller activate `fief_hub` directly (e.g., a third-party mod calling `GameMenu.ActivateGameMenu("fief_hub")`)? With registration unconditional, the menu is always reachable. With Enable=false, the player would see only "Leave" (all other options disabled). Cosmetic edge case but verify.

4. **`Resolve` re-validates ownership: TOCTOU corrected, but other gaps?** Adapter's Resolve method:
   ```
   if (s.OwnerClan != clan) return null;
   return s;
   ```
   Question: between `Resolve` returning the Settlement and `manager.CreateState + state.Initialize(settlement) + manager.PushState(state)` running, can the player lose ownership? In single-thread campaign tick context, no — these are all synchronous on the main thread. But if Resolve is called from a click handler that runs on a UI thread separate from Campaign, a race exists. Check whether menu-option consequence callbacks run on Campaign thread or UI thread.

5. **FiefSummary.Id can be empty string.** The constructor coerces null → empty string. If `Settlement.StringId` is ever null (it shouldn't be — `MBObjectBase.StringId` is `readonly string` set on construction — but verify via decompile), an empty Id flows through. `IsPlayerCurrentlyAt` short-circuits on `string.IsNullOrEmpty`, but Resolve's loop uses `s.StringId == settlementId` — if both are empty string, would it match the wrong settlement? Probably no settlement has empty StringId, so the loop falls through to return null. Cosmetic but verify.

6. **Static `_exceptionLogged` retention across session reload.** When the player exits to main menu and starts a new campaign, the static field persists in the static class. A different exception in the new session would not be logged. Question: should this reset on `OnSessionLaunched` or `OnNewGameCreated`? Currently no reset path. Risk: low (silent error in second session of same process), but verify the design intent.

7. **Behavior shrunk to ~85 lines: lost any wiring?** Compare `FiefHubCampaignBehavior` before and after. Before, the behavior had `_menuFiefs / _menuCurrentFief / _menuCurrentAtPlayer` fields and ran the `OnMenuInit` body. After extraction, the presenter owns these. Question: does `FiefHubMenuPresenter.Reset` get called from the right place? Before, `_selectedIndex = 0` was on the behavior; now `_presenter.Reset()` is called from OnNewGameCreated/OnGameLoaded. Verify the presenter is the same instance (Reuse.Singleton) the menu callbacks use. If a different instance, Reset goes to the wrong object.

8. **Patch36_GameStateScreenManager `_swapper ??= IoC.Resolve<IRemoteFiefSettlementSwapper>()` race.** Static cache is set on first call. If two GameStateScreenManager.CreateScreen calls happen concurrently (impossible in vanilla but verify), both threads do `??=` and the second overwrites the first — both refer to same singleton instance from DryIoc anyway, so harmless. But the cache assignment itself: `_swapper ??= IoC.Resolve` is `_swapper = _swapper ?? IoC.Resolve` — IoC.Resolve runs even when `_swapper != null`. Wait — `??=` short-circuits. Verify by reading.

## Prior self-review lessons (these caught real bugs)

- **Review #28** caught the original fix-pass introducing a regression in the very thing it was meant to fix.
- **Review #35b** caught fixes that addressed only 1 of 2 IDs (fix-incompletion residue).
- **Review #29** caught a process violation: created GitHub issue retroactively.

## REQUIRED SECTIONS

### 1. VANILLA CODE — verify the new fix surface

Decompile and paste:

a. `GameStateManager.HandleCreateState` body — confirm the ordering between `state.GameStateManager = this` and `OnCreateState(state)` — is the screen constructor invoked synchronously? When does `OnInitialize` fire on the screen?
b. `ScreenBase.Initialize` lifecycle — when is `OnInitialize()` called by the screen system relative to the listener notification?
c. The body of any TAOM Harmony patch on `MobileParty.CurrentSettlement` getter or `Settlement.CurrentSettlement` getter (grep `Main/` for these).

For each, ANSWER: does the fix interact with this correctly?

### 2. FIX-PASS REGRESSION SCENARIOS

Trace each:

a. **CreateState ordering.** Step through: `manager.CreateState<FiefManagementGameState>()` → `Activator.CreateInstance(typeof(FiefManagementGameState))` (parameterless ctor, Fief is null) → `HandleCreateState` → assigns GameStateManager → loops listeners → `GameStateScreenManager.OnCreateState(state)` → `CreateScreen(state)` → our prefix fires → `new GauntletFiefManagementScreen(fmState, swapper)` (constructor stores _state with Fief=null) → return false from prefix. THEN: control returns to caller. Caller then runs `state.Initialize(settlement)` (sets Fief). THEN: `manager.PushState(state)`. PushState enqueues a job and calls DoGameStateJobs which activates the screen → OnInitialize runs → reads `_state.Fief` (which IS now set). Confirm this ordering is safe.

b. **App-tick reads of MobileParty.MainParty.CurrentSettlement during screen lifetime.** Grep ALL `Main/**/*.cs` for `MainParty.CurrentSettlement` and `MainParty._currentSettlement`. Each call site that runs during a menu state is a potential corruption point. List every match.

c. **TAOM Harmony patches on hot paths during menu state.** Grep for patches on `MapScreen.OnFrameTick`, `MBSubModuleBase.OnApplicationTick`, `ScreenManager` ticks. List those that read MainParty location.

### 3. TEST COVERAGE FOR THE FIXES

Each P1/P2 fix should have a regression test that would FAIL on the unfixed code. Check:

a. Does any `FiefHubServiceTests` test cover the CreateState flow? (No — that's an engine integration concern.)
b. Does any test cover the Reset() being called via the IoC singleton path? (No — but the lifecycle is covered structurally.)
c. Does any test cover Resolve()'s ownership re-validation? (Check `SettlementOwnershipAdapterTests.cs` — does it exist?)

If a fix is untested AND untestable in unit-test context, document why in the feature doc.

### 4. CONFIG CROSS-REFERENCE (delta from #38)

Re-verify the 3 MCM settings + their consumers given the structural changes:

a. `EnableFiefManagement` — list every reader after the refactor (presenter? behavior? F6 patch?). Confirm runtime toggle truly takes effect.
b. `AllowRemoteBuildingQueue` — gating moved into presenter's `ManageOptionEnabled`. Verify the presenter's `Refresh()` re-reads the setting on every menu open (it does — the lambda body checks `_settings.AllowRemoteBuildingQueue` live, not at Refresh time).
c. `FiefManagementDebug` — hint says file log. Verify NO consumer calls `InformationManager.DisplayMessage` (i.e., no HUD).

### 5. FINDINGS

| # | Severity | File:line | Issue | Fix |
|---|----------|-----------|-------|-----|

P1 (must fix), P2 (should fix), P3 (nice to have).

If you have NO findings, write "No P1/P2/P3 findings." Don't pad with cosmetic nitpicks.

## QUALITY GATES

- Did you verify the CreateState → CreateScreen → constructor → Initialize → PushState → OnInitialize ordering by reading vanilla source (not just trusting the fix description)?
- Did you grep ALL `Main/` for `MainParty.CurrentSettlement` to find any reader that runs during the screen lifetime?
- Did you read the new `FiefHubMenuPresenter` implementation end-to-end?
- Did you cross-reference every MCM setting consumer after the refactor?

If you skipped any: state which and why.

## Prior review lessons

SUCCESSES: Config ID cross-ref. Vanilla decompilation of full call chains. Lifecycle tracing with state matrices.

FAILURES: Codex previously skipped Known Suspect tracing. Codex previously trusted the Claude-supplied "what was fixed" summary instead of independently reading the diff. Don't.

Output to: `docs/reviews/codex-adversarial-fiefmanagement-fixes-2026-05-07.md`
