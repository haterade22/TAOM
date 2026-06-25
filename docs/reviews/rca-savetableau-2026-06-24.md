# RCA — Save/Load Hero Preview CTD Guard (issue #299), 2026-06-24

## Top-line

The save-load CTD fix (`BasicTableauRaceGuard` + a Harmony prefix coercing the preview `_race` to human) was correct in its *logic* but shipped past `/deep-review` (5 agents, PASS) with a **CRITICAL patch-application-timing bug**: the patch reused `Patch2_RefreshTableau`, which `SubModule` applies in `OnGameInitializationFinished` (campaign init). The crash it guards renders on the **cold main menu** (Load Game save list), *before* any game-init callback — so the prefix was not attached when the protected screen first renders. The fix would not have prevented the reported crash. Caught by the Codex adversarial pass (C1), which decompiled the engine's initial-screen flow.

Fix: split into its own `Patch55_BasicTableauRaceGuard` category, applied in `OnBeforeInitialModuleScreenSetAsRoot` (after `IoC.Configure` sets the guard, before the initial module screen is pushed), process-static one-shot, fail-open.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| C1 | CRITICAL | Patch in `Patch2_RefreshTableau` (applied in `OnGameInitializationFinished`) can't guard the cold-menu Save/Load preview, which renders before campaign init → reported crash stays live | Patch-application-timing / lifecycle | Deep-review's init-ordering trace verified the *guard object* was set before the patch applied, and that the category was *registered*, but never checked that the *application lifecycle point* precedes the *earliest render of the patched target* (a main-menu screen) | LESSONS-LEARNED entry + new deep-review sub-check: a patch on a main-menu / pre-campaign screen must apply in `OnSubModuleLoad`/`OnBeforeInitialModuleScreenSetAsRoot`, not `OnGameInitializationFinished` |
| L1 | LOW | No test/assertion pins the guard category to a pre-`InitialState` application point; the unit tests pass while the integration timing is wrong | Test coverage gap (lifecycle) | Service logic is pure + 100% covered, but the actual integration risk (patch attached too late) is a SubModule lifecycle concern with no cheap unit test | Documented in the patch doc-comment + this RCA + LESSONS-LEARNED; the lifecycle correctness is asserted by in-game test (ADR-008 boundary) |

## Root-cause pattern

**"Registered" ≠ "applied in time."** TAOM applies Harmony categories at two lifecycle points: an early batch in `OnSubModuleLoad` (+ pre-menu work in `OnBeforeInitialModuleScreenSetAsRoot`) and a late batch in `OnGameInitializationFinished` (gated `_gameInitPatchesApplied`). The late batch exists because most View-type patches protect in-game / character-creation screens that only appear *after* a campaign starts — so campaign-init application is both safe (View assembly initialized) and in time. The Save/Load hero preview is the one View tableau that renders on the **cold main menu**, so it is the lone exception to "View patches go in the late batch." Reusing the late category for it (chosen to avoid a `SubModule.cs` edit — a scope-minimization convenience) silently mis-timed it.

The deep-review *did* run an init-ordering trace (Data Flow agent TRACE 1) and a Harmony-category-registration check — and both passed — because both asked the wrong question:
- The ordering trace proved `_guard` is set (in `IoC.Configure`, `OnSubModuleLoad`) before the patch applies (`OnGameInitializationFinished`). True, but irrelevant to the real risk. It then asserted the menu render happens "after both," conflating *after module load* with *after game-init*. The save list renders after module load but **before** game-init.
- The category-registration check confirmed `[HarmonyPatchCategory("Patch2_RefreshTableau")]` matches a `_harmony.PatchCategory("Patch2_RefreshTableau")` call. It verifies the patch isn't *dead*, not that it's *early enough*.

## Why each deep-review agent missed it

- **Agent 1 (Standards):** scope is ADR compliance; timing isn't a standards rule. Correctly out of scope.
- **Agent 2 (Compatibility):** verified the Harmony target/field/signature against installed v1.4.6 — all correct. Did not model the module lifecycle / when the category is applied.
- **Agent 3 (Efficiency):** no perf issue; timing isn't a perf concern.
- **Agent 4 (Completeness):** confirmed the category string is registered in `SubModule.cs` and concluded "no SubModule edit needed" — which is exactly the trap. "Registered" was treated as sufficient.
- **Agent 5 (Data Flow):** the only agent that *tried* — its TRACE 1 explicitly reasoned about init-before-use and even noted `BasicCharacterTableau` renders on the Load Game screen, but then mis-placed `OnGameInitializationFinished` as firing before the menu. The single false premise ("the menu render happens after `OnGameInitializationFinished`") flipped a real bug into a "CONNECTED — no window" pass.

Codex caught it by decompiling the engine flow (`Module.OnApplicationTick` → `SetInitialModuleScreenAsRootScreen` → `OnBeforeInitialModuleScreenSetAsRoot` → push `InitialState`) and seeing the cold-menu Save/Load screen is reachable before `MBGameManager.OnGameInitializationFinished` dispatches.

## Feedback memory / LESSONS-LEARNED

Systemic, first-time-in-this-form. Added to `docs/reviews/LESSONS-LEARNED.md` (Harmony / patch-lifecycle category):

> **A Harmony patch that protects a main-menu / pre-campaign screen must have its category applied before the initial module screen is pushed — `OnSubModuleLoad` or `OnBeforeInitialModuleScreenSetAsRoot`, never `OnGameInitializationFinished`.** Verifying the category is *registered* (string match in `SubModule.cs`) is NOT enough — verify the *lifecycle method* it's applied in precedes the earliest render of the patched target. TAOM's late batch (`OnGameInitializationFinished`, `_gameInitPatchesApplied`) is correct only for in-game / CC screens that appear after campaign start; the Save/Load hero preview (`BasicCharacterTableau`, via `SaveLoadHeroTableauTextureProvider`) is the cold-menu exception.

Preventive rule wired into `/deep-review` (Agent 5 + the Harmony-category check): add the "applied-before-the-protected-screen-renders" sub-check to the existing category-registration rule.

## Status

Fixed (own category, pre-menu application). Build clean, guard tests 5/5. In-game validation (load the offending custom-race save from the main menu, confirm campaign map reached) is the final boundary check (ADR-008) and is owed from the user before close-out.
