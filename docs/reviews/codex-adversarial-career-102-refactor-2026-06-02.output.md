OpenAI Codex v0.128.0 (research preview)
--------
workdir: C:\Users\mikew\source\repos\TAOM
model: gpt-5.5
provider: openai
approval: never
sandbox: workspace-write [workdir, /tmp, $TMPDIR, C:\Users\mikew\.codex\memories]
reasoning effort: xhigh
reasoning summaries: none
session id: 019e8a40-5c5b-7580-b92e-22f26e05523c
--------
user
# Codex Adversarial Review — Issue #102 CareerPerkMissionBehavior Decomposition

## Context

TAOM is a Mount & Blade II: Bannerlord v1.4.5 total-conversion mod (.NET Framework 4.7.2). This change closes GitHub issue #102: the legacy `CareerPerkMissionBehavior.cs` was 302 lines (>2× the ADR-002 thin-entry-point ceiling of 150 lines) with four inline state machines (per-frame ability tick, V-key + ready-state notification + charging-throttle, GauntletUI HUD lifecycle, per-activation effect dispatch). None were unit-testable because every dependency was a sealed TaleWorlds static (`Mission.Current`, `Campaign.Current`, `CharacterObject.PlayerCharacter`, `ScreenManager.TopScreen`, `Input`, `InformationManager`).

The refactor extracts three controllers + two adapters and reduces the behavior to a 126-line thin delegator.

## Diff under review

Three modified files + 11 new files (see below). Vanilla DLL targets unchanged.

### Modified

- `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` — 302 → 126 lines, thin delegator only
- `Main/Features/CareerSystem/CareerSystemIoC.cs` — +5 Singleton registrations
- `Main/SubModule.cs` — 8-arg constructor → 6-arg constructor at line 683

### New

- `Main/Features/CareerSystem/Abilities/IAbilityInputAdapter.cs` — `bool IsActivationKeyPressed()`
- `Main/Features/CareerSystem/Abilities/AbilityInputAdapter.cs` — wraps `Input.IsKeyPressed(InputKey.V)`
- `Main/Features/CareerSystem/Abilities/IMissionTimeProvider.cs` — `float CurrentTime { get; }`
- `Main/Features/CareerSystem/Abilities/MissionTimeProvider.cs` — wraps `Mission.Current?.CurrentTime ?? 0f`
- `Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs` — `AbilityActivationOutcome Tick(float dt, string heroStringId, bool hasCareer); void Reset()` + enum `{None, JustBecameReady, Activated, Charging}`
- `Main/Features/CareerSystem/Abilities/AbilityActivationController.cs` — pure state machine, 75 lines
- `Main/Features/CareerSystem/UI/IAbilityHudController.cs` — `TryInitialize() / Refresh(string) / Cleanup()`
- `Main/Features/CareerSystem/UI/AbilityHudController.cs` — owns GauntletLayer + ScreenManager attach, 115 lines
- `Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs` — `Execute(string heroStringId, Action<MissionAbilityExecutionContext> registerContext)`
- `Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs` — per-activation pipeline (template mutation + executor dispatch + toast), 90 lines
- `TAOM.Tests/Features/CareerSystem/AbilityActivationControllerTests.cs` — 12 unit tests

## Mandatory verification surface

1. **Vanilla decompilation.** Confirm against installed `TaleWorlds.MountAndBlade.dll` / `TaleWorlds.Core.dll` / `TaleWorlds.InputSystem.dll` / `TaleWorlds.Engine.GauntletUI.dll`:
   - `MissionBehavior.OnMissionTick(float dt)` is invoked once per frame from `Mission.Tick` and `dt` is the wall-clock delta. ✓ if true; flag if there's now a fixed-step variant in 1.4.5.
   - `Mission.Current.CurrentTime` is the mission-relative clock (resets per mission). Flag if it ever wraps or jumps in 1.4.5.
   - `Input.IsKeyPressed(InputKey.V)` returns `true` only on the edge frame the key was pressed (NOT held). The state machine assumes edge-trigger semantics — if 1.4.5 changed this to held-key reporting, the activation+charging logic breaks.
   - `ScreenManager.TopScreen` semantics: when does this become non-null for a battle mission? `AbilityHudController.TryInitialize()` is idempotent and called every tick — confirm it's safe to keep calling `topScreen.AddLayer(_hudLayer)` if `_hudInitialized` is already true (it shouldn't, but verify the guard).
   - `GauntletLayer` ctor + `LoadMovie` + `topScreen.AddLayer` + the symmetric `RemoveLayer` + `ReleaseMovie` + `OnFinalize` sequence is the v1.4.5-correct teardown order. Codex Review #31 (CareerScreen) caught a similar finalize-ordering bug — re-verify here.

2. **Adapter pattern compliance.** Per ADR-007, services may never accept sealed TaleWorlds types. The new controllers/adapters are tight. Audit:
   - `AbilityActivationController` — confirm it touches NO `Mission`/`Campaign`/`CharacterObject`/`Input` statics directly (it should only see `ICareerAbilityService` + `IAbilityInputAdapter` + `IMissionTimeProvider`).
   - `AbilityEffectExecutor` — boundary-allowed reaches: `Mission.Current?.MainAgent`, `Campaign.Current`, `CharacterObject.PlayerCharacter`, `InformationManager.DisplayMessage`. Flag any deeper drift.
   - `AbilityHudController` — boundary-allowed reaches: `ScreenManager.TopScreen`, `GauntletLayer`, `TextObject`. Flag any deeper drift.

3. **Thin entry-point compliance.** Per ADR-002, `CareerPerkMissionBehavior` overrides must be thin delegators. Review the new `OnMissionTick` switch (lines 67-83) — is the per-outcome branching too much logic for an entry point, or is the translation from `AbilityActivationOutcome` to `InformationManager.DisplayMessage` the legitimate "convert at boundary" responsibility? The plan accepts this as boundary code. Push back if you disagree.

4. **State machine correctness.** The activation controller has two flags: `_abilityReadyNotified` (re-armed on each `Activated`) and `_lastChargingMessageTime = -2f` sentinel for first-throttle-eligible. Confirm:
   - Reset path on `OnEndMission` clears both flags before the next mission.
   - Same mission, ability re-armed after activation, then V pressed while still-charging: expected — `Charging` if 2s throttle window elapsed since the *last* Charging emission, else `None`. Verify the test `Tick_VPressedTwiceWithinThrottle_SecondReturnsNone` actually exercises this. (Note: throttle resets on Reset(), so a mission restart shows the first Charging immediately even at `CurrentTime=0`.)
   - There's no path where `JustBecameReady` is emitted multiple times for the same cooldown completion (the `_abilityReadyNotified` flag must hold across frames until re-armed).
   - **The Tick CALLS `_abilityService.Tick(heroStringId, dt)` BEFORE checking `IsAbilityReady` — so if a tick crosses the cooldown boundary, the ready check sees the post-tick state. Confirm this is correct (it is, per the legacy behavior).**

5. **HUD per-mission cache invalidation.** `AbilityHudController` caches `_cachedHudHeroId/Name/Sprite` and only refreshes when `heroStringId` changes (Codex Review #31 perf fix). If career changes WITHOUT hero changing (player picks a new career in the same battle — implausible but possible via dev console), the cache stays stale. Flag if this is a real risk; otherwise accept as bounded.

6. **`Cleanup()` ordering.** `AbilityHudController.Cleanup` calls `RemoveLayer` BEFORE `ReleaseMovie`. Confirm against vanilla — Codex #31 found that `OnFinalize` must come after the layer is detached, and `ReleaseMovie` after the layer is detached too. The current order is `RemoveLayer` → `ReleaseMovie` → `_hudVM?.OnFinalize`. Validate.

7. **DI lifetime correctness.** All five new services are registered `Reuse.Singleton`. The activation controller HAS per-mission state (`_abilityReadyNotified`, `_lastChargingMessageTime`). The behavior calls `_activationController.Reset()` in `OnEndMission`. **Question: if two missions ever overlap (start of next mission before `OnEndMission` fires on the previous), does the singleton state carry over?** Mission lifecycle in 1.4.5: confirm `OnEndMission` ALWAYS fires before the next mission's `OnMissionTick` begins. Same question for `AbilityHudController` — its `_hudLayer / _hudVM / _hudMovie / _hudInitialized` fields are also singleton state. If `Cleanup()` ever throws or is skipped, the next mission starts with stale GauntletLayer references.
   - **Alternative consideration:** would Transient lifetime + per-mission resolution be cleaner here? The behavior is per-mission, so resolving its dependencies fresh per mission would auto-reset state. Argue your preferred approach.

8. **`MissionAbilityExecutionContext` allocation in executor.** `AbilityEffectExecutor.Execute` creates `new MissionAbilityExecutionContext(..., Mission.Current, _logger)` and hands it to the registerContext callback (which the behavior wires to `_activeContexts.Add`). Confirm:
   - The context is added to `_activeContexts` BEFORE `executor.Execute(context)` runs. If the executor.Execute throws, is the partially-initialized context still in the list? — Look at the ordering.
   - `_activeContexts` is cleared on `OnEndMission` and on `affectedAgent == mainAgent` in `OnAgentRemoved`. Good. Is there a missing clear path? (E.g., player switches careers mid-mission, retire, etc.)

9. **`OnAgentDeleted` does NOT clear the context for the deleted agent.** Only `CareerAbilityBuffTracker.ClearAllyBuff(agent.Index)`. Is this correct? — confirm the contexts in `_activeContexts` are owned by the PLAYER (mainAgent), not allies. If an ally dies mid-Activated, no context cleanup needed. ✓ if true.

10. **Logging discipline.** Per CLAUDE.md "C++ Native Hook Standards" — that's for C++. This is C#. Still, the controllers + behavior emit InfoLog on every mission start ("CareerSystem: Mission started — ...") and every Activated. Confirm these are not per-frame; they should be edge-triggered. Verify by reading the call sites.

## Known Suspects (TAOM-historic class)

Apply these from `docs/reviews/REVIEW-GUIDE.md`:

- **Float NaN propagation through Clamp** (`simplicity-criterion.md`, `feedback_clamp_nan_infinity_propagates.md`). The state machine has `now - _lastChargingMessageTime >= ChargingMessageThrottleSeconds`. If `Mission.Current.CurrentTime` ever returns NaN (unlikely but check 1.4.5 cctor behavior), the comparison is `false` and Charging is never re-emitted. Acceptable degradation if true.
- **Reflection alloc per call** (`feedback_hotpath_private_method_open_delegate.md`). The new controllers use NO reflection. ✓
- **MovementOrder cctor crash in OnSubModuleLoad** (`feedback_movementorder_cctor_mission_current.md`). The behavior is per-mission, not subscribed in OnSubModuleLoad. ✓ Confirm.
- **Cross-feature handshake** (`feedback_cross_feature_handshake_via_shared_adapter.md`). No shared adapter touched. ✓
- **Editor-fields-are-config** (`feedback_editor_fields_are_config.md`). No new editor fields. ✓
- **Worktree base stale** (`feedback_worktree_base_stale_in_parallel_agents.md`). Single-session, no parallel agents. ✓

## What you should NOT do

- Do not refactor for style. ADR-002 ceiling met (126 lines), and the controllers are TDD-mandated.
- Do not propose adding `IAbilityHudController` unit tests. It's a boundary class (per ADR-008 "Entry Points: Not required") and verifies in-battle only.
- Do not suggest replacing `Reuse.Singleton` with `Reuse.Transient` without strong evidence — the singleton + explicit Reset() pattern is established for other CareerSystem services.

## Output

Per `docs/reviews/REVIEW-GUIDE.md` Phase 3 format: emit a numbered findings list (HIGH/MED/LOW), each with file:line, evidence (vanilla decompile excerpt or test trace), and a concrete fix recommendation. Then a Phase 3e RCA section: for each confirmed finding, "why did 12 controller tests + Claude's deep-review pass not catch this?"

ERROR: The process "53364" not found.
ERROR: The process "84816" not found.
codex
I’ll review this as an adversarial pass only: read the changed CareerSystem files, verify the TaleWorlds lifecycle/API claims from local decompiled sources or installed DLLs, then report findings without editing anything.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "rg -n \"class CareerPerkMissionBehavior|class AbilityActivationController|class AbilityHudController|class AbilityEffectExecutor|interface IAbility|MissionAbilityExecutionContext|OnAgentDeleted|OnAgentRemoved|CareerSystemIoC|CreateGameModels|new CareerPerkMissionBehavior|Reuse\\.Singleton\" Main/Features/CareerSystem Main/SubModule.cs TAOM.Tests/Features/CareerSystem" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'rg --files Main/Features/CareerSystem TAOM.Tests/Features/CareerSystem | sort' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git status --short' in C:\Users\mikew\source\repos\TAOM
2026-06-02T21:32:28.826719Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 1.2 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/CareerSystem TAOM.Tests/Features/CareerSyste …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 1171ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m --files Main/Features/CareerSystem TAOM.Tests/Features/CareerSyste …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

2026-06-02T21:32:29.649635Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 2 seconds
Output:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CareerPerkMissionBehavior|class AbilityActivationControl …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 exited 1 in 2005ms:
[31;1mrg: [0m
[31;1m[36;1mLine |[0m
[31;1m[36;1m[36;1m   2 | [0m [36;1mrg[0m -n "class CareerPerkMissionBehavior|class AbilityActivationControl …[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m     | [31;1m ~~[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m     | [31;1mThe term 'rg' is not recognized as a name of a cmdlet, function, script file, or executable program.[0m
[31;1m[36;1m[36;1m[0m[36;1m[0m[36;1m[31;1m[31;1m[36;1m[31;1mCheck the spelling of the name, or if a path was included, verify that the path is correct and try again.[0m

 succeeded in 2129ms:
 M Main/Features/CareerSystem/CareerPerkMissionBehavior.cs
 M Main/Features/CareerSystem/CareerSystemIoC.cs
 M Main/SubModule.cs
?? Main/Features/CareerSystem/Abilities/AbilityActivationController.cs
?? Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs
?? Main/Features/CareerSystem/Abilities/AbilityInputAdapter.cs
?? Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs
?? Main/Features/CareerSystem/Abilities/IAbilityEffectExecutor.cs
?? Main/Features/CareerSystem/Abilities/IAbilityInputAdapter.cs
?? Main/Features/CareerSystem/Abilities/IMissionTimeProvider.cs
?? Main/Features/CareerSystem/Abilities/MissionTimeProvider.cs
?? Main/Features/CareerSystem/UI/AbilityHudController.cs
?? Main/Features/CareerSystem/UI/IAbilityHudController.cs
?? TAOM.Tests/Features/CareerSystem/AbilityActivationControllerTests.cs
?? docs/reviews/codex-adversarial-career-102-refactor-2026-06-02.output.md
?? docs/reviews/codex-adversarial-career-102-refactor-2026-06-02.prompt.md
warning: could not open directory '.codex_tmp/tmp1fft9w7e/': Permission denied
warning: could not open directory '.codex_tmp/tmp1fyfj2_j/': Permission denied
warning: could not open directory '.codex_tmp/tmp29jamgyn/': Permission denied
warning: could not open directory '.codex_tmp/tmp2owwhlqy/': Permission denied
warning: could not open directory '.codex_tmp/tmp5516wbdv/': Permission denied
warning: could not open directory '.codex_tmp/tmp78qze76y/': Permission denied
warning: could not open directory '.codex_tmp/tmp8gds9xfp/': Permission denied
warning: could not open directory '.codex_tmp/tmpbwbm4o8d/': Permission denied
warning: could not open directory '.codex_tmp/tmpc85yzrbw/': Permission denied
warning: could not open directory '.codex_tmp/tmpi3maldm8/': Permission denied
warning: could not open directory '.codex_tmp/tmpkr2gpims/': Permission denied
warning: could not open directory '.codex_tmp/tmpktgkfgay/': Permission denied
warning: could not open directory '.codex_tmp/tmplcnrd81l/': Permission denied
warning: could not open directory '.codex_tmp/tmplorpih3w/': Permission denied
warning: could not open directory '.codex_tmp/tmpm39f2s15/': Permission denied
warning: could not open directory '.codex_tmp/tmpov4yy7nx/': Permission denied
warning: could not open directory '.codex_tmp/tmpsanxk9fp/': Permission denied
warning: could not open directory '.codex_tmp/tmpwy0s04pv/': Permission denied
warning: could not open directory '.codex_tmp/tmpyqy6lg5o/': Permission denied
warning: could not open directory '.codex_tmp2/tmpulsj5ei5/': Permission denied

codex
`rg` is not available in this shell, so I’m switching to native PowerShell search/listing. First pass shows only the expected files are modified/new, plus review prompt/output artifacts under `docs/reviews/`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/CareerPerkMissionBehavior.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/AbilityActivationController.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/UI/AbilityHudController.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 529ms:
   1: namespace TAOM.Features.CareerSystem.Abilities;
   2: 
   3: // Per-frame V-key + cooldown state machine extracted from CareerPerkMissionBehavior.OnMissionTick
   4: // (Issue #102). Pure logic + injected adapters -- no TaleWorlds statics, fully unit-testable.
   5: //
   6: // State transitions (per-frame outcome from Tick):
   7: //   no career               -> None
   8: //   cooldown finished       -> JustBecameReady (one-shot until next Activated)
   9: //   V pressed, ready        -> Activated (re-arms the JustBecameReady flag)
  10: //   V pressed, on cooldown  -> Charging (throttled to once per 2s)
  11: //   any other frame         -> None
  12: public class AbilityActivationController : IAbilityActivationController
  13: {
  14:     private const float ChargingMessageThrottleSeconds = 2f;
  15: 
  16:     private readonly ICareerAbilityService _abilityService;
  17:     private readonly IAbilityInputAdapter _input;
  18:     private readonly IMissionTimeProvider _time;
  19: 
  20:     private bool _abilityReadyNotified;
  21:     private float _lastChargingMessageTime = -ChargingMessageThrottleSeconds;
  22: 
  23:     public AbilityActivationController(
  24:         ICareerAbilityService abilityService,
  25:         IAbilityInputAdapter input,
  26:         IMissionTimeProvider time)
  27:     {
  28:         _abilityService = abilityService;
  29:         _input = input;
  30:         _time = time;
  31:     }
  32: 
  33:     public AbilityActivationOutcome Tick(float dt, string heroStringId, bool hasCareer)
  34:     {
  35:         if (!hasCareer) return AbilityActivationOutcome.None;
  36: 
  37:         // Per-frame Tick(dt) -- never batched (see Codex Review #31, single-bucket accumulator
  38:         // was dropping elapsed time on long frames).
  39:         _abilityService.Tick(heroStringId, dt);
  40: 
  41:         var justBecameReady = false;
  42:         if (_abilityService.IsAbilityReady(heroStringId) && !_abilityReadyNotified)
  43:         {
  44:             _abilityReadyNotified = true;
  45:             justBecameReady = true;
  46:         }
  47: 
  48:         if (_input.IsActivationKeyPressed())
  49:         {
  50:             if (_abilityService.IsAbilityReady(heroStringId))
  51:             {
  52:                 _abilityService.ActivateAbility(heroStringId);
  53:                 _abilityReadyNotified = false; // re-arm for the NEXT cooldown completion
  54:                 return AbilityActivationOutcome.Activated;
  55:             }
  56: 
  57:             var now = _time.CurrentTime;
  58:             if (now - _lastChargingMessageTime >= ChargingMessageThrottleSeconds)
  59:             {
  60:                 _lastChargingMessageTime = now;
  61:                 return AbilityActivationOutcome.Charging;
  62:             }
  63: 
  64:             return AbilityActivationOutcome.None;
  65:         }
  66: 
  67:         return justBecameReady ? AbilityActivationOutcome.JustBecameReady : AbilityActivationOutcome.None;
  68:     }
  69: 
  70:     public void Reset()
  71:     {
  72:         _abilityReadyNotified = false;
  73:         _lastChargingMessageTime = -ChargingMessageThrottleSeconds;
  74:     }
  75: }

 succeeded in 475ms:
   1: using System;
   2: using TaleWorlds.Engine.GauntletUI;
   3: using TaleWorlds.Localization;
   4: using TaleWorlds.ScreenSystem;
   5: using TAOM.Core.Logging;
   6: using TAOM.Features.CareerSystem.Abilities;
   7: 
   8: namespace TAOM.Features.CareerSystem.UI;
   9: 
  10: // Boundary class for the in-battle CareerAbilityHUD Gauntlet layer (Issue #102).
  11: // Owns the layer/movie/VM, the per-mission HUD string cache, and the screen attach + detach
  12: // dance. Per the test-coverage rule "Entry Points: Not required" -- verified in-battle.
  13: public class AbilityHudController : IAbilityHudController
  14: {
  15:     private readonly ICareerDataService _dataService;
  16:     private readonly ICareerRegistry _registry;
  17:     private readonly ICareerAbilityService _abilityService;
  18:     private readonly IModLogger _logger;
  19: 
  20:     private GauntletLayer _hudLayer;
  21:     private CareerAbilityHudVM _hudVM;
  22:     private GauntletMovieIdentifier _hudMovie;
  23:     private bool _hudInitialized;
  24: 
  25:     // Per-mission cache so per-frame Refresh does not allocate a new TextObject + string
  26:     // interpolation for the ability name and sprite path on every tick (Codex Review #31).
  27:     private string _cachedHudHeroId;
  28:     private string _cachedHudAbilityName;
  29:     private string _cachedHudAbilitySprite;
  30: 
  31:     public AbilityHudController(
  32:         ICareerDataService dataService,
  33:         ICareerRegistry registry,
  34:         ICareerAbilityService abilityService,
  35:         IModLogger logger)
  36:     {
  37:         _dataService = dataService;
  38:         _registry = registry;
  39:         _abilityService = abilityService;
  40:         _logger = logger;
  41:     }
  42: 
  43:     public void TryInitialize()
  44:     {
  45:         if (_hudInitialized) return;
  46: 
  47:         var topScreen = ScreenManager.TopScreen;
  48:         if (topScreen == null) return;
  49: 
  50:         _hudVM = new CareerAbilityHudVM();
  51:         _hudLayer = new GauntletLayer("CareerAbilityHUD", 50);
  52:         _hudMovie = _hudLayer.LoadMovie("AbilityHUD", _hudVM);
  53:         topScreen.AddLayer(_hudLayer);
  54:         _hudInitialized = true;
  55:         _logger?.LogInfo("CareerSystem: HUD layer initialized");
  56:     }
  57: 
  58:     public void Refresh(string heroStringId)
  59:     {
  60:         if (_hudVM == null) return;
  61: 
  62:         if (!_dataService.HasCareer(heroStringId))
  63:         {
  64:             _hudVM.Update(false, null, null, 0f, false);
  65:             return;
  66:         }
  67: 
  68:         var ability = _abilityService.GetOrCreateAbility(heroStringId, _registry, _dataService);
  69:         if (ability == null)
  70:         {
  71:             _hudVM.Update(false, null, null, 0f, false);
  72:             return;
  73:         }
  74: 
  75:         if (!string.Equals(heroStringId, _cachedHudHeroId, StringComparison.Ordinal))
  76:             RefreshHudCache(heroStringId, ability);
  77: 
  78:         _hudVM.Update(true, _cachedHudAbilityName, _cachedHudAbilitySprite, ability.ReadyProgress01, ability.IsReady);
  79:     }
  80: 
  81:     public void Cleanup()
  82:     {
  83:         if (!_hudInitialized) return;
  84: 
  85:         var topScreen = ScreenManager.TopScreen;
  86:         if (topScreen != null && _hudLayer != null)
  87:             topScreen.RemoveLayer(_hudLayer);
  88: 
  89:         if (_hudMovie != null && _hudLayer != null)
  90:             _hudLayer.ReleaseMovie(_hudMovie);
  91: 
  92:         _hudVM?.OnFinalize();
  93:         _hudLayer = null;
  94:         _hudVM = null;
  95:         _hudMovie = null;
  96:         _hudInitialized = false;
  97: 
  98:         _cachedHudHeroId = null;
  99:         _cachedHudAbilityName = null;
 100:         _cachedHudAbilitySprite = null;
 101:     }
 102: 
 103:     private void RefreshHudCache(string heroStringId, CareerAbility ability)
 104:     {
 105:         var careerId = _dataService.GetCareerStringId(heroStringId);
 106:         var career = careerId != null ? _registry.GetCareer(careerId) : null;
 107:         var rawName = career?.DisplayName ?? ability.TemplateId;
 108: 
 109:         _cachedHudHeroId = heroStringId;
 110:         _cachedHudAbilityName = new TextObject(rawName).ToString();
 111:         _cachedHudAbilitySprite = career != null
 112:             ? $"CareerSystem\\Abilities\\{career.AbilityTemplateId}"
 113:             : null;
 114:     }
 115: }

 succeeded in 545ms:
   1: using System.Collections.Generic;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.Core;
   4: using TaleWorlds.Library;
   5: using TaleWorlds.MountAndBlade;
   6: using TAOM.Core.Logging;
   7: using TAOM.Features.CareerSystem.Abilities;
   8: using TAOM.Features.CareerSystem.UI;
   9: 
  10: namespace TAOM.Features.CareerSystem;
  11: 
  12: // Thin mission-scoped boundary that wires sealed TaleWorlds APIs (Mission, InformationManager,
  13: // CharacterObject) to the testable controllers. Per ADR-002 / issue #102 the state machines
  14: // live in IAbilityActivationController + IAbilityHudController + IAbilityEffectExecutor;
  15: // this class only owns the mission-scoped _activeContexts expiration list + the OnEndMission
  16: // teardown sequencing.
  17: public class CareerPerkMissionBehavior : MissionBehavior
  18: {
  19:     private readonly ICareerDataService _dataService;
  20:     private readonly ICareerAbilityService _abilityService;
  21:     private readonly IAbilityActivationController _activationController;
  22:     private readonly IAbilityHudController _hudController;
  23:     private readonly IAbilityEffectExecutor _effectExecutor;
  24:     private readonly IModLogger _logger;
  25: 
  26:     private bool _loggedMissionStart;
  27:     private readonly List<MissionAbilityExecutionContext> _activeContexts = new List<MissionAbilityExecutionContext>();
  28: 
  29:     public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
  30: 
  31:     public CareerPerkMissionBehavior(
  32:         ICareerDataService dataService,
  33:         ICareerAbilityService abilityService,
  34:         IAbilityActivationController activationController,
  35:         IAbilityHudController hudController,
  36:         IAbilityEffectExecutor effectExecutor,
  37:         IModLogger logger)
  38:     {
  39:         _dataService = dataService;
  40:         _abilityService = abilityService;
  41:         _activationController = activationController;
  42:         _hudController = hudController;
  43:         _effectExecutor = effectExecutor;
  44:         _logger = logger;
  45:     }
  46: 
  47:     public override void OnMissionTick(float dt)
  48:     {
  49:         if (Campaign.Current == null) return;
  50:         var hero = CharacterObject.PlayerCharacter?.HeroObject;
  51:         if (hero == null) return;
  52: 
  53:         var heroId = hero.StringId;
  54: 
  55:         if (!_loggedMissionStart)
  56:         {
  57:             _loggedMissionStart = true;
  58:             var careerId = _dataService.GetCareerStringId(heroId);
  59:             _logger?.LogInfo($"CareerSystem: Mission started — hero='{heroId}' hasCareer={_dataService.HasCareer(heroId)} career='{careerId ?? "none"}'");
  60:         }
  61: 
  62:         _hudController.TryInitialize();
  63:         _hudController.Refresh(heroId);
  64: 
  65:         var hasCareer = _dataService.HasCareer(heroId);
  66:         var outcome = _activationController.Tick(dt, heroId, hasCareer);
  67:         switch (outcome)
  68:         {
  69:             case AbilityActivationOutcome.JustBecameReady:
  70:                 InformationManager.DisplayMessage(new InformationMessage(
  71:                     "Career ability is ready! Press V to activate.", Colors.Green));
  72:                 break;
  73:             case AbilityActivationOutcome.Activated:
  74:                 _logger?.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
  75:                 _effectExecutor.Execute(heroId, _activeContexts.Add);
  76:                 break;
  77:             case AbilityActivationOutcome.Charging:
  78:                 var remaining = (int)System.Math.Ceiling(_abilityService.GetCooldownRemaining(heroId));
  79:                 if (remaining < 1) remaining = 1;
  80:                 InformationManager.DisplayMessage(new InformationMessage(
  81:                     $"Career ability still charging — {remaining}s remaining.", Colors.Gray));
  82:                 break;
  83:         }
  84: 
  85:         var currentTime = Mission.Current?.CurrentTime ?? 0f;
  86:         for (var i = _activeContexts.Count - 1; i >= 0; i--)
  87:         {
  88:             _activeContexts[i].Tick(currentTime);
  89:             if (_activeContexts[i].IsExpired)
  90:                 _activeContexts.RemoveAt(i);
  91:         }
  92:     }
  93: 
  94:     public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
  95:     {
  96:         if (agentState != AgentState.Killed && agentState != AgentState.Unconscious) return;
  97:         if (Campaign.Current == null) return;
  98: 
  99:         var hero = CharacterObject.PlayerCharacter?.HeroObject;
 100:         if (hero == null) return;
 101: 
 102:         var mainAgent = Mission.Current?.MainAgent;
 103:         if (affectedAgent == mainAgent)
 104:         {
 105:             CareerAbilityBuffTracker.ClearBuff(hero.StringId);
 106:             CareerAbilityBuffTracker.ClearAllAllyBuffs();
 107:             _activeContexts.Clear();
 108:         }
 109:     }
 110: 
 111:     protected override void OnEndMission()
 112:     {
 113:         _hudController.Cleanup();
 114:         _activationController.Reset();
 115:         _logger?.LogInfo("CareerSystem: Mission ended — clearing abilities");
 116:         _loggedMissionStart = false;
 117:         _activeContexts.Clear();
 118:         CareerAbilityBuffTracker.ClearAll();
 119:         _abilityService.ClearAll();
 120:     }
 121: 
 122:     public override void OnAgentDeleted(Agent affectedAgent)
 123:     {
 124:         CareerAbilityBuffTracker.ClearAllyBuff(affectedAgent.Index);
 125:     }
 126: }

 succeeded in 485ms:
   1: using System;
   2: using TaleWorlds.CampaignSystem;
   3: using TaleWorlds.Core;
   4: using TaleWorlds.Library;
   5: using TaleWorlds.Localization;
   6: using TaleWorlds.MountAndBlade;
   7: using TAOM.Adapters;
   8: using TAOM.Core.Logging;
   9: using TAOM.Features.CareerSystem.Domain;
  10: using TAOM.Features.CareerSystem.Mutations;
  11: 
  12: namespace TAOM.Features.CareerSystem.Abilities;
  13: 
  14: // Per-activation effect pipeline extracted from CareerPerkMissionBehavior.ExecuteAbilityEffect
  15: // (Issue #102). Boundary class -- allocates MissionAbilityExecutionContext (touches
  16: // Mission.Current.MainAgent) and emits the InformationManager "activated" toast. The host
  17: // MissionBehavior owns the _activeContexts expiration list and passes registerContext so the
  18: // allocated context joins it.
  19: public class AbilityEffectExecutor : IAbilityEffectExecutor
  20: {
  21:     private readonly ICareerDataService _dataService;
  22:     private readonly ICareerRegistry _registry;
  23:     private readonly ICareerConfigProvider _configProvider;
  24:     private readonly CareerAbilityEffectRegistry _effectRegistry;
  25:     private readonly IMutationService _mutationService;
  26:     private readonly ICareerHeroAdapterFactory _adapterFactory;
  27:     private readonly IModLogger _logger;
  28: 
  29:     public AbilityEffectExecutor(
  30:         ICareerDataService dataService,
  31:         ICareerRegistry registry,
  32:         ICareerConfigProvider configProvider,
  33:         CareerAbilityEffectRegistry effectRegistry,
  34:         IMutationService mutationService,
  35:         ICareerHeroAdapterFactory adapterFactory,
  36:         IModLogger logger)
  37:     {
  38:         _dataService = dataService;
  39:         _registry = registry;
  40:         _configProvider = configProvider;
  41:         _effectRegistry = effectRegistry;
  42:         _mutationService = mutationService;
  43:         _adapterFactory = adapterFactory;
  44:         _logger = logger;
  45:     }
  46: 
  47:     public void Execute(string heroStringId, Action<MissionAbilityExecutionContext> registerContext)
  48:     {
  49:         var careerId = _dataService.GetCareerStringId(heroStringId);
  50:         if (string.IsNullOrEmpty(careerId)) return;
  51: 
  52:         var career = _registry.GetCareer(careerId);
  53:         if (career == null) return;
  54: 
  55:         var rawTemplate = _configProvider.GetAbilityTemplate(career.AbilityTemplateId);
  56:         var template = MutateTemplate(rawTemplate, heroStringId);
  57: 
  58:         var duration = template?.Duration ?? 8f;
  59:         var radius = template?.Radius ?? 10f;
  60: 
  61:         var mainAgent = Mission.Current?.MainAgent;
  62:         var context = new MissionAbilityExecutionContext(
  63:             heroStringId, duration, radius, mainAgent, Mission.Current, _logger);
  64:         registerContext?.Invoke(context);
  65: 
  66:         var executor = _effectRegistry.GetExecutor(careerId);
  67:         executor.Execute(context);
  68: 
  69:         var abilityName = new TextObject(career.DisplayName).ToString();
  70:         InformationManager.DisplayMessage(new InformationMessage(
  71:             $"{abilityName} activated!", Colors.Yellow));
  72: 
  73:         if (!string.IsNullOrEmpty(template?.SoundEffect))
  74:             context.PlaySound(template.SoundEffect);
  75:         if (!string.IsNullOrEmpty(template?.ParticleEffect))
  76:             context.PlayParticle(template.ParticleEffect);
  77:     }
  78: 
  79:     private AbilityTemplateData MutateTemplate(AbilityTemplateData rawTemplate, string heroId)
  80:     {
  81:         if (rawTemplate == null) return null;
  82:         if (Campaign.Current == null) return rawTemplate;
  83: 
  84:         var hero = CharacterObject.PlayerCharacter?.HeroObject;
  85:         if (hero == null || hero.StringId != heroId) return rawTemplate;
  86: 
  87:         var heroAdapter = _adapterFactory.Create(hero);
  88:         return _mutationService.MutateAbility(rawTemplate, heroAdapter, _dataService, _registry);
  89:     }
  90: }

codex
I’ve got the main four files in view. The behavior is now structurally thin, but I’m checking for lifecycle and state carry-over issues rather than line-count compliance. Next I’m reading the DI wiring, tests, and the old/new diff around the constructor path.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/SubModule.cs' | ForEach-Object { if ("'$i -ge 640 -and $i -le 710) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/CareerSystemIoC.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'git diff -- Main/Features/CareerSystem/CareerPerkMissionBehavior.cs Main/Features/CareerSystem/CareerSystemIoC.cs Main/SubModule.cs' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -LiteralPath 'Main/Features/CareerSystem/Abilities' -File | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'TAOM.Tests/Features/CareerSystem/AbilityActivationControllerTests.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 374ms:
diff --git a/Main/Features/CareerSystem/CareerPerkMissionBehavior.cs b/Main/Features/CareerSystem/CareerPerkMissionBehavior.cs
index d3b69a8..8190a63 100644
--- a/Main/Features/CareerSystem/CareerPerkMissionBehavior.cs
+++ b/Main/Features/CareerSystem/CareerPerkMissionBehavior.cs
@@ -1,68 +1,46 @@
-using System;
 using System.Collections.Generic;
 using TaleWorlds.CampaignSystem;
 using TaleWorlds.Core;
-using TaleWorlds.Engine.GauntletUI;
-using TaleWorlds.InputSystem;
 using TaleWorlds.Library;
 using TaleWorlds.MountAndBlade;
-using TaleWorlds.ScreenSystem;
-using TAOM.Adapters;
 using TAOM.Core.Logging;
 using TAOM.Features.CareerSystem.Abilities;
-using TAOM.Features.CareerSystem.Domain;
-using TAOM.Features.CareerSystem.Mutations;
 using TAOM.Features.CareerSystem.UI;
 
 namespace TAOM.Features.CareerSystem;
 
+// Thin mission-scoped boundary that wires sealed TaleWorlds APIs (Mission, InformationManager,
+// CharacterObject) to the testable controllers. Per ADR-002 / issue #102 the state machines
+// live in IAbilityActivationController + IAbilityHudController + IAbilityEffectExecutor;
+// this class only owns the mission-scoped _activeContexts expiration list + the OnEndMission
+// teardown sequencing.
 public class CareerPerkMissionBehavior : MissionBehavior
 {
     private readonly ICareerDataService _dataService;
-    private readonly ICareerRegistry _registry;
     private readonly ICareerAbilityService _abilityService;
-    private readonly ICareerConfigProvider _configProvider;
-    private readonly CareerAbilityEffectRegistry _effectRegistry;
-    private readonly IMutationService _mutationService;
-    private readonly ICareerHeroAdapterFactory _adapterFactory;
+    private readonly IAbilityActivationController _activationController;
+    private readonly IAbilityHudController _hudController;
+    private readonly IAbilityEffectExecutor _effectExecutor;
     private readonly IModLogger _logger;
 
-    private const float ChargingMessageThrottleSeconds = 2f;
     private bool _loggedMissionStart;
-    private bool _abilityReadyNotified;
-    private float _lastChargingMessageTime = -ChargingMessageThrottleSeconds;
-
-    private GauntletLayer _hudLayer;
-    private CareerAbilityHudVM _hudVM;
-    private GauntletMovieIdentifier _hudMovie;
-    private bool _hudInitialized;
-
-    // HUD metadata cache (avoid per-frame TextObject + string interpolation in UpdateHud).
-    private string _cachedHudHeroId;
-    private string _cachedHudAbilityName;
-    private string _cachedHudAbilitySprite;
-
     private readonly List<MissionAbilityExecutionContext> _activeContexts = new List<MissionAbilityExecutionContext>();
 
     public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;
 
     public CareerPerkMissionBehavior(
         ICareerDataService dataService,
-        ICareerRegistry registry,
         ICareerAbilityService abilityService,
-        ICareerConfigProvider configProvider,
-        CareerAbilityEffectRegistry effectRegistry,
-        IMutationService mutationService,
-        ICareerHeroAdapterFactory adapterFactory,
+        IAbilityActivationController activationController,
+        IAbilityHudController hudController,
+        IAbilityEffectExecutor effectExecutor,
         IModLogger logger)
     {
         _dataService = dataService;
-        _registry = registry;
         _abilityService = abilityService;
-        _configProvider = configProvider;
-        _effectRegistry = effectRegistry;
-        _mutationService = mutationService;
-        _adapterFactory = adapterFactory;
+        _activationController = activationController;
+        _hudController = hudController;
+        _effectExecutor = effectExecutor;
         _logger = logger;
     }
 
@@ -77,162 +55,40 @@ public class CareerPerkMissionBehavior : MissionBehavior
         if (!_loggedMissionStart)
         {
             _loggedMissionStart = true;
-            var hasCareer = _dataService.HasCareer(heroId);
             var careerId = _dataService.GetCareerStringId(heroId);
-            _logger.LogInfo($"CareerSystem: Mission started — hero='{heroId}' hasCareer={hasCareer} career='{careerId ?? "none"}'");
+            _logger?.LogInfo($"CareerSystem: Mission started — hero='{heroId}' hasCareer={_dataService.HasCareer(heroId)} career='{careerId ?? "none"}'");
         }
 
-        TryInitializeHud();
-        UpdateHud(heroId);
-
-        if (!_dataService.HasCareer(heroId)) return;
+        _hudController.TryInitialize();
+        _hudController.Refresh(heroId);
 
-        // Tick the ability cooldown with the actual frame dt. Any batching pattern (e.g.
-        // `if (_acc >= 1f) Tick(1f)`) drops elapsed time on long frames -- a 2.5s frame would
-        // drain only 1s of cooldown. CareerAbility.Tick handles fractional dt correctly.
-        _abilityService.Tick(heroId, dt);
-
-        // Check ability ready notification (every frame, not gated by tick interval)
-        if (_abilityService.IsAbilityReady(heroId) && !_abilityReadyNotified)
+        var hasCareer = _dataService.HasCareer(heroId);
+        var outcome = _activationController.Tick(dt, heroId, hasCareer);
+        switch (outcome)
         {
-            _abilityReadyNotified = true;
-            InformationManager.DisplayMessage(new InformationMessage(
-                "Career ability is ready! Press V to activate.", Colors.Green));
+            case AbilityActivationOutcome.JustBecameReady:
+                InformationManager.DisplayMessage(new InformationMessage(
+                    "Career ability is ready! Press V to activate.", Colors.Green));
+                break;
+            case AbilityActivationOutcome.Activated:
+                _logger?.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
+                _effectExecutor.Execute(heroId, _activeContexts.Add);
+                break;
+            case AbilityActivationOutcome.Charging:
+                var remaining = (int)System.Math.Ceiling(_abilityService.GetCooldownRemaining(heroId));
+                if (remaining < 1) remaining = 1;
+                InformationManager.DisplayMessage(new InformationMessage(
+                    $"Career ability still charging — {remaining}s remaining.", Colors.Gray));
+                break;
         }
 
-        // Tick all active execution contexts to expire timed buffs; remove finished ones.
         var currentTime = Mission.Current?.CurrentTime ?? 0f;
-        for (int i = _activeContexts.Count - 1; i >= 0; i--)
+        for (var i = _activeContexts.Count - 1; i >= 0; i--)
         {
             _activeContexts[i].Tick(currentTime);
             if (_activeContexts[i].IsExpired)
                 _activeContexts.RemoveAt(i);
         }
-
-        // Check ability activation input (every frame, once per key press)
-        if (Input.IsKeyPressed(InputKey.V))
-        {
-            if (_abilityService.IsAbilityReady(heroId))
-            {
-                _abilityService.ActivateAbility(heroId);
-                _abilityReadyNotified = false;
-                _logger.LogInfo($"CareerSystem: Ability activated for hero '{heroId}' via V key");
-                ExecuteAbilityEffect(heroId);
-            }
-            else
-            {
-                NotifyStillCharging(heroId);
-            }
-        }
-    }
-
-    private void NotifyStillCharging(string heroId)
-    {
-        var now = Mission.Current?.CurrentTime ?? 0f;
-        if (now - _lastChargingMessageTime < ChargingMessageThrottleSeconds) return;
-
-        _lastChargingMessageTime = now;
-        var remaining = (int)System.Math.Ceiling(_abilityService.GetCooldownRemaining(heroId));
-        if (remaining < 1) remaining = 1;
-        InformationManager.DisplayMessage(new InformationMessage(
-            $"Career ability still charging — {remaining}s remaining.", Colors.Gray));
-    }
-
-    private void ExecuteAbilityEffect(string heroId)
-    {
-        var careerId = _dataService.GetCareerStringId(heroId);
-        if (string.IsNullOrEmpty(careerId)) return;
-
-        var career = _registry.GetCareer(careerId);
-        if (career == null) return;
-
-        // Apply hero mutations to the raw template so Duration/Radius/etc reflect choice-tree choices.
-        var rawTemplate = _configProvider.GetAbilityTemplate(career.AbilityTemplateId);
-        var template = MutateTemplate(rawTemplate, heroId);
-
-        var duration = template?.Duration ?? 8f;
-        var radius = template?.Radius ?? 10f;
-
-        var mainAgent = Mission.Current?.MainAgent;
-        var context = new MissionAbilityExecutionContext(
-            heroId, duration, radius, mainAgent, Mission.Current, _logger);
-
-        _activeContexts.Add(context);
-
-        var executor = _effectRegistry.GetExecutor(careerId);
-        executor.Execute(context);
-
-        var abilityName = new TaleWorlds.Localization.TextObject(career.DisplayName).ToString();
-        InformationManager.DisplayMessage(new InformationMessage(
-            $"{abilityName} activated!", Colors.Yellow));
-
-        if (!string.IsNullOrEmpty(template?.SoundEffect))
-            context.PlaySound(template.SoundEffect);
-        if (!string.IsNullOrEmpty(template?.ParticleEffect))
-            context.PlayParticle(template.ParticleEffect);
-    }
-
-    private AbilityTemplateData MutateTemplate(AbilityTemplateData rawTemplate, string heroId)
-    {
-        if (rawTemplate == null) return null;
-        if (Campaign.Current == null) return rawTemplate;
-
-        var hero = CharacterObject.PlayerCharacter?.HeroObject;
-        if (hero == null || hero.StringId != heroId) return rawTemplate;
-
-        var heroAdapter = _adapterFactory.Create(hero);
-        return _mutationService.MutateAbility(rawTemplate, heroAdapter, _dataService, _registry);
-    }
-
-    private void TryInitializeHud()
-    {
-        if (_hudInitialized) return;
-
-        var topScreen = ScreenManager.TopScreen;
-        if (topScreen == null) return;
-
-        _hudVM = new CareerAbilityHudVM();
-        _hudLayer = new GauntletLayer("CareerAbilityHUD", 50);
-        _hudMovie = _hudLayer.LoadMovie("AbilityHUD", _hudVM);
-        topScreen.AddLayer(_hudLayer);
-        _hudInitialized = true;
-        _logger.LogInfo("CareerSystem: HUD layer initialized");
-    }
-
-    private void UpdateHud(string heroId)
-    {
-        if (_hudVM == null) return;
-
-        if (!_dataService.HasCareer(heroId))
-        {
-            _hudVM.Update(false, null, null, 0f, false);
-            return;
-        }
-
-        var ability = _abilityService.GetOrCreateAbility(heroId, _registry, _dataService);
-        if (ability == null)
-        {
-            _hudVM.Update(false, null, null, 0f, false);
-            return;
-        }
-
-        if (!string.Equals(heroId, _cachedHudHeroId, StringComparison.Ordinal))
-            RefreshHudCache(heroId, ability);
-
-        _hudVM.Update(true, _cachedHudAbilityName, _cachedHudAbilitySprite, ability.ReadyProgress01, ability.IsReady);
-    }
-
-    private void RefreshHudCache(string heroId, CareerAbility ability)
-    {
-        var careerId = _dataService.GetCareerStringId(heroId);
-        var career = careerId != null ? _registry.GetCareer(careerId) : null;
-        var rawName = career?.DisplayName ?? ability.TemplateId;
-
-        _cachedHudHeroId = heroId;
-        _cachedHudAbilityName = new TaleWorlds.Localization.TextObject(rawName).ToString();
-        _cachedHudAbilitySprite = career != null
-            ? $"CareerSystem\\Abilities\\{career.AbilityTemplateId}"
-            : null;
     }
 
     public override void OnAgentRemoved(Agent affectedAgent, Agent affectorAgent, AgentState agentState, KillingBlow blow)
@@ -244,8 +100,6 @@ public class CareerPerkMissionBehavior : MissionBehavior
         if (hero == null) return;
 
         var mainAgent = Mission.Current?.MainAgent;
-
-        // Death cleanup: main agent died while buffs were active
         if (affectedAgent == mainAgent)
         {
             CareerAbilityBuffTracker.ClearBuff(hero.StringId);
@@ -256,14 +110,10 @@ public class CareerPerkMissionBehavior : MissionBehavior
 
     protected override void OnEndMission()
     {
-        CleanupHud();
-        _logger.LogInfo("CareerSystem: Mission ended — clearing abilities");
+        _hudController.Cleanup();
+        _activationController.Reset();
+        _logger?.LogInfo("CareerSystem: Mission ended — clearing abilities");
         _loggedMissionStart = false;
-        _abilityReadyNotified = false;
-        _lastChargingMessageTime = -ChargingMessageThrottleSeconds;
-        _cachedHudHeroId = null;
-        _cachedHudAbilityName = null;
-        _cachedHudAbilitySprite = null;
         _activeContexts.Clear();
         CareerAbilityBuffTracker.ClearAll();
         _abilityService.ClearAll();
@@ -271,27 +121,6 @@ public class CareerPerkMissionBehavior : MissionBehavior
 
     public override void OnAgentDeleted(Agent affectedAgent)
     {
-        // Clean up ally buff entry when any agent is removed from the mission
         CareerAbilityBuffTracker.ClearAllyBuff(affectedAgent.Index);
     }
-
-    private void CleanupHud()
-    {
-        if (!_hudInitialized) return;
-
-        var topScreen = ScreenManager.TopScreen;
-        if (topScreen != null && _hudLayer != null)
-        {
-            topScreen.RemoveLayer(_hudLayer);
-        }
-
-        if (_hudMovie != null && _hudLayer != null)
-            _hudLayer.ReleaseMovie(_hudMovie);
-
-        _hudVM?.OnFinalize();
-        _hudLayer = null;
-        _hudVM = null;
-        _hudMovie = null;
-        _hudInitialized = false;
-    }
 }
diff --git a/Main/Features/CareerSystem/CareerSystemIoC.cs b/Main/Features/CareerSystem/CareerSystemIoC.cs
index 7df571d..8456f8d 100644
--- a/Main/Features/CareerSystem/CareerSystemIoC.cs
+++ b/Main/Features/CareerSystem/CareerSystemIoC.cs
@@ -29,6 +29,15 @@ public static class CareerSystemIoC
         container.Register<Abilities.ICareerAbilityService, Abilities.CareerAbilityService>(Reuse.Singleton);
         container.Register<IMutationService, MutationService>(Reuse.Singleton);
 
+        // Issue #102 — CareerPerkMissionBehavior decomposition. Three controllers extracted
+        // from the legacy 302-line mission behavior so the V-key + HUD + effect-execution
+        // state machines are independently unit-testable.
+        container.Register<Abilities.IAbilityInputAdapter, Abilities.AbilityInputAdapter>(Reuse.Singleton);
+        container.Register<Abilities.IMissionTimeProvider, Abilities.MissionTimeProvider>(Reuse.Singleton);
+        container.Register<Abilities.IAbilityActivationController, Abilities.AbilityActivationController>(Reuse.Singleton);
+        container.Register<UI.IAbilityHudController, UI.AbilityHudController>(Reuse.Singleton);
+        container.Register<Abilities.IAbilityEffectExecutor, Abilities.AbilityEffectExecutor>(Reuse.Singleton);
+
         // Phase 9b #142 — agent-stat service extracted out of TaomAgentStatCalculateModel /
         // TaomAgentApplyDamageModel bodies (gamemodels.md rule 4 — no inline branching in
         // override bodies). Reads ICareerPassiveService + the static CareerAbilityBuffTracker.
diff --git a/Main/SubModule.cs b/Main/SubModule.cs
index 8e9c297..2cd62fc 100644
--- a/Main/SubModule.cs
+++ b/Main/SubModule.cs
@@ -682,12 +682,10 @@ public class SubModule : MBSubModuleBase
         {
             mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
                 IoC.Resolve<ICareerDataService>(),
-                IoC.Resolve<ICareerRegistry>(),
                 careerAbilityService,
-                IoC.Resolve<ICareerConfigProvider>(),
-                IoC.Resolve<Features.CareerSystem.Abilities.CareerAbilityEffectRegistry>(),
-                IoC.Resolve<Features.CareerSystem.Mutations.IMutationService>(),
-                IoC.Resolve<ICareerHeroAdapterFactory>(),
+                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
+                IoC.Resolve<Features.CareerSystem.UI.IAbilityHudController>(),
+                IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
                 IoC.Resolve<IModLogger>()));
         }
     }
warning: in the working copy of 'Main/Features/CareerSystem/CareerPerkMissionBehavior.cs', LF will be replaced by CRLF the next time Git touches it

 succeeded in 499ms:
   1: using System.Collections.Generic;
   2: using DryIoc;
   3: using TAOM.Adapters;
   4: using TAOM.Features.CareerSystem.Abilities;
   5: using TAOM.Features.CareerSystem.Abilities.Executors;
   6: using TAOM.Features.CareerSystem.Domain;
   7: using TAOM.Features.CareerSystem.Mutations;
   8: 
   9: namespace TAOM.Features.CareerSystem;
  10: 
  11: public static class CareerSystemIoC
  12: {
  13:     public static void RegisterCareerSystemFeature(IContainer container)
  14:     {
  15:         // Phase 1: Data persistence
  16:         container.Register<ICareerDataService, CareerDataService>(Reuse.Singleton);
  17: 
  18:         // Phase 2: Config, registry, passives, mutations
  19:         container.Register<ICareerConfigProvider, CareerConfigProvider>(Reuse.Singleton);
  20:         container.Register<ICareerRegistry, CareerRegistry>(Reuse.Singleton);
  21:         container.Register<ICareerPassiveService, CareerPassiveService>(Reuse.Singleton);
  22:         container.Register<IMutationCalculatorRegistry, MutationCalculatorRegistry>(Reuse.Singleton);
  23: 
  24:         // Phase 3: Campaign integration
  25:         container.Register<ICareerCreationHandler, CareerCreationHandler>(Reuse.Singleton);
  26:         container.Register<ICareerSwitchService, CareerSwitchService>(Reuse.Singleton);
  27: 
  28:         // Phase 4: Abilities and mutations
  29:         container.Register<Abilities.ICareerAbilityService, Abilities.CareerAbilityService>(Reuse.Singleton);
  30:         container.Register<IMutationService, MutationService>(Reuse.Singleton);
  31: 
  32:         // Issue #102 — CareerPerkMissionBehavior decomposition. Three controllers extracted
  33:         // from the legacy 302-line mission behavior so the V-key + HUD + effect-execution
  34:         // state machines are independently unit-testable.
  35:         container.Register<Abilities.IAbilityInputAdapter, Abilities.AbilityInputAdapter>(Reuse.Singleton);
  36:         container.Register<Abilities.IMissionTimeProvider, Abilities.MissionTimeProvider>(Reuse.Singleton);
  37:         container.Register<Abilities.IAbilityActivationController, Abilities.AbilityActivationController>(Reuse.Singleton);
  38:         container.Register<UI.IAbilityHudController, UI.AbilityHudController>(Reuse.Singleton);
  39:         container.Register<Abilities.IAbilityEffectExecutor, Abilities.AbilityEffectExecutor>(Reuse.Singleton);
  40: 
  41:         // Phase 9b #142 — agent-stat service extracted out of TaomAgentStatCalculateModel /
  42:         // TaomAgentApplyDamageModel bodies (gamemodels.md rule 4 — no inline branching in
  43:         // override bodies). Reads ICareerPassiveService + the static CareerAbilityBuffTracker.
  44:         container.Register<Abilities.ICareerAgentStatService, Abilities.CareerAgentStatService>(Reuse.Singleton);
  45: 
  46:         // Phase 4C: Ability effect execution — all 50 careers mapped to 3 archetypes.
  47:         // GetCareerArchetypeMap() is the single source of truth — the executor registry and
  48:         // the ICareerArchetypeService both read from it.
  49:         container.RegisterDelegate(r => BuildAbilityEffectRegistry(r.Resolve<ICareerConfigProvider>()), Reuse.Singleton);
  50:         container.RegisterDelegate<ICareerArchetypeService>(_ => new CareerArchetypeService(GetCareerArchetypeMap()), Reuse.Singleton);
  51: 
  52:         // Phase 5: GameModel support
  53:         container.Register<ICareerHeroAdapterFactory, CareerHeroAdapterFactory>(Reuse.Singleton);
  54: 
  55:         // Phase 6: Career-tied quest system (verified on 1.4.5)
  56:         container.Register<ICareerQuestConfigProvider, CareerQuestConfigProvider>(Reuse.Singleton);
  57:         container.Register<ICareerQuestService, CareerQuestService>(Reuse.Singleton);
  58:         container.Register<IQuestHeroAdapterFactory, QuestHeroAdapterFactory>(Reuse.Singleton);
  59:     }
  60: 
  61:     private static CareerAbilityEffectRegistry BuildAbilityEffectRegistry(ICareerConfigProvider config)
  62:     {
  63:         var registry = new CareerAbilityEffectRegistry();
  64:         foreach (var pair in GetCareerArchetypeMap())
  65:         {
  66:             switch (pair.Value)
  67:             {
  68:                 case CareerArchetype.Infantry: registry.Register(new InfantryAbilityExecutor(pair.Key, config)); break;
  69:                 case CareerArchetype.Ranged:   registry.Register(new RangedAbilityExecutor(pair.Key, config));   break;
  70:                 case CareerArchetype.Cavalry:  registry.Register(new CavalryAbilityExecutor(pair.Key, config));  break;
  71:             }
  72:         }
  73:         return registry;
  74:     }
  75: 
  76:     // Single source of truth: careerId → archetype. Both the ability executor registry
  77:     // and ICareerArchetypeService (used by CareerStartingEquipmentService) read from this.
  78:     // Cached in a static field — invoked twice during IoC startup (executor builder +
  79:     // archetype service registration), no reason to allocate the dict twice.
  80:     // Disabled careers (troll WIP) are absent — adding them here re-enables their executor.
  81:     private static readonly IReadOnlyDictionary<string, CareerArchetype> CareerArchetypeMap = BuildCareerArchetypeMap();
  82: 
  83:     internal static IReadOnlyDictionary<string, CareerArchetype> GetCareerArchetypeMap() => CareerArchetypeMap;
  84: 
  85:     private static IReadOnlyDictionary<string, CareerArchetype> BuildCareerArchetypeMap() => new Dictionary<string, CareerArchetype>
  86:     {
  87:         // ═══ GONDOR ═══
  88:         ["captain_of_osgiliath"]   = CareerArchetype.Infantry,
  89:         ["ranger_of_ithilien"]     = CareerArchetype.Ranged,
  90:         ["knight_of_belfalas"]     = CareerArchetype.Cavalry,
  91: 
  92:         // ═══ MORDOR ═══
  93:         ["black_uruk_captain"]     = CareerArchetype.Infantry,
  94:         ["olog_hai_warchief"]      = CareerArchetype.Infantry,
  95:         ["mulkerhili_cultist"]     = CareerArchetype.Ranged,
  96:         ["snaga_rider"]            = CareerArchetype.Cavalry,
  97: 
  98:         // ═══ ROHAN ═══
  99:         ["watchman_of_stangard"]   = CareerArchetype.Infantry,
 100:         ["marksman_of_aldburg"]    = CareerArchetype.Ranged,
 101:         ["eotheod_windrider"]      = CareerArchetype.Cavalry,
 102: 
 103:         // ═══ DUNLAND ═══
 104:         ["avanc_luth_raider"]      = CareerArchetype.Infantry,
 105:         ["wolfskin_hunter"]        = CareerArchetype.Ranged,
 106:         ["clanguard_rider"]        = CareerArchetype.Cavalry,
 107: 
 108:         // ═══ KHAND ═══
 109:         ["blademaster_of_ren"]     = CareerArchetype.Infantry,
 110:         ["steppe_bowmaster"]       = CareerArchetype.Ranged,
 111:         ["chariot_warlord"]        = CareerArchetype.Cavalry,
 112: 
 113:         // ═══ HARAD ═══
 114:         ["tribesman_of_jelut"]     = CareerArchetype.Infantry,
 115:         // DISABLED 2026-05-14: Troll careers WIP — re-enable by uncommenting.
 116:         // ["far_harad_halftroll"]    = CareerArchetype.Infantry,
 117:         ["pezarsani_javelineer"]   = CareerArchetype.Ranged,
 118:         ["mahud_beast_rider"]      = CareerArchetype.Cavalry,
 119: 
 120:         // ═══ EASTERLINGS / RHÛN ═══
 121:         ["codyan_legionaire"]      = CareerArchetype.Infantry,
 122:         ["lokhas_drus_marksman"]   = CareerArchetype.Ranged,
 123:         ["balchoth_kan"]           = CareerArchetype.Cavalry,
 124: 
 125:         // ═══ DALE ═══
 126:         ["dale_guardsman"]         = CareerArchetype.Infantry,
 127:         ["dale_marksman"]          = CareerArchetype.Ranged,
 128:         ["dale_outrider"]          = CareerArchetype.Cavalry,
 129: 
 130:         // ═══ EREBOR ═══
 131:         ["ironguard"]              = CareerArchetype.Infantry,
 132:         ["crossbow_master"]        = CareerArchetype.Ranged,
 133:         ["ram_rider"]              = CareerArchetype.Cavalry,
 134: 
 135:         // ═══ RIVENDELL ═══
 136:         ["blade_dancer"]           = CareerArchetype.Infantry,
 137:         ["elven_archer"]           = CareerArchetype.Ranged,
 138:         ["rivendell_knight"]       = CareerArchetype.Cavalry,
 139: 
 140:         // ═══ LOTHLORIEN ═══
 141:         ["warden"]                 = CareerArchetype.Infantry,
 142:         ["galadhrim_archer"]       = CareerArchetype.Ranged,
 143:         ["sentinel"]               = CareerArchetype.Cavalry,
 144: 
 145:         // ═══ MIRKWOOD ═══
 146:         ["shadow_walker"]          = CareerArchetype.Infantry,
 147:         ["silvan_archer"]          = CareerArchetype.Ranged,
 148:         ["elk_rider"]              = CareerArchetype.Cavalry,
 149: 
 150:         // ═══ ISENGARD ═══
 151:         ["uruk_berserker"]         = CareerArchetype.Infantry,
 152:         ["uruk_crossbow"]          = CareerArchetype.Ranged,
 153:         ["warg_scout"]             = CareerArchetype.Cavalry,
 154: 
 155:         // ═══ GUNDABAD ═══
 156:         ["cave_troll_master"]      = CareerArchetype.Infantry,
 157:         ["goblin_sniper"]          = CareerArchetype.Ranged,
 158:         ["warg_pack_leader"]       = CareerArchetype.Cavalry,
 159: 
 160:         // ═══ DOL GULDUR ═══
 161:         ["shadow_warrior"]         = CareerArchetype.Infantry,
 162:         ["necromancer_acolyte"]    = CareerArchetype.Ranged,
 163:         ["fell_rider"]             = CareerArchetype.Cavalry,
 164: 
 165:         // ═══ UMBAR ═══
 166:         ["corsair_boarder"]        = CareerArchetype.Infantry,
 167:         ["corsair_crossbow"]       = CareerArchetype.Ranged,
 168:         ["corsair_captain"]        = CareerArchetype.Cavalry,
 169:     };
 170: 
 171:     public static void InitializeCalculators(IMutationCalculatorRegistry registry)
 172:     {
 173:         BuiltInCalculators.RegisterAll(registry);
 174:     }
 175: }

 succeeded in 524ms:
 640:             _harmony.PatchCategory("Patch_MissionTime_SetMovementOrder");
 641:         }
 642: 
 643:         mission.AddMissionBehavior(new AdvancedCombatBehavior());
 644:         mission.AddMissionBehavior(new BehaviorTreeMissionLogic());
 645:         mission.AddMissionBehavior(new AutonomousMovementPlayerController());
 646:         mission.AddMissionBehavior(new WargMissionBehavior());
 647:         // DISABLED 2026-05-14: Spider feature not ready for live game yet. Re-enable by uncommenting.
 648:         // mission.AddMissionBehavior(new SpiderMissionBehavior());
 649:         mission.AddMissionBehavior(new SiegeDismountMissionBehavior());
 650:         mission.AddMissionBehavior(new MixedFormationsMissionBehavior());
 651:         mission.AddMissionBehavior(new SmartCavalryAIMissionBehavior());
 652:         mission.AddMissionBehavior(new Features.CompanionTactics.BattleActionBar.Hooks.BattleActionBarMissionView());
 653: 
 654:         var colorStore = IoC.Resolve<IAgentColorStore>();
 655:         if (colorStore != null)
 656:             mission.AddMissionBehavior(new AgentColorStoreCleanupBehavior(colorStore));
 657: 
 658:         // MissionDiagnostic: added LAST so it sees all behaviors added by TAOM AND
 659:         // every other mod in the load chain. Dumps MissionBehaviors + MissionLogics
 660:         // on first OnMissionTick to taom_debug_*.log so user-uploaded crash logs
 661:         // contain enough data to identify mod-conflict bugs (BehaviorType=Logic +
 662:         // !MissionLogic null-cast offenders) and action-set anomalies.
 663:         var diagSvc = IoC.Resolve<Features.MissionDiagnostic.IMissionDiagnosticService>();
 664:         var raceMgr = IoC.Resolve<Core.Domain.IRaceManager>();
 665:         var diagLogger = IoC.Resolve<IModLogger>();
 666:         if (diagSvc != null && raceMgr != null && diagLogger != null)
 667:             mission.AddMissionBehavior(new Features.MissionDiagnostic.Hooks.MissionDiagnosticBehavior(diagSvc, raceMgr, diagLogger));
 668: 
 669:         // BattleLoadDiagnostics phase-6: "battle playable" marker on first tick + closes
 670:         // the loading window so the stall watchdog stands down and phase-5 stops logging.
 671:         var battleLoadDiagSvc = IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadDiagnosticsService>();
 672:         if (battleLoadDiagSvc != null && battleLoadDiagSvc.IsEnabled)
 673:             mission.AddMissionBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(battleLoadDiagSvc));
 674: 
 675:         // Dev-trigger behavior watches the CrashReport MCM toggle and throws a tagged
 676:         // TaomDevTriggerException on the next OnMissionTick when the player flips
 677:         // "Throw On Next Mission Tick". QA only — no-op in normal play.
 678:         mission.AddMissionBehavior(new Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior());
 679: 
 680:         var careerAbilityService = IoC.Resolve<Features.CareerSystem.Abilities.ICareerAbilityService>();
 681:         if (careerAbilityService != null && Campaign.Current != null)
 682:         {
 683:             mission.AddMissionBehavior(new Features.CareerSystem.CareerPerkMissionBehavior(
 684:                 IoC.Resolve<ICareerDataService>(),
 685:                 careerAbilityService,
 686:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityActivationController>(),
 687:                 IoC.Resolve<Features.CareerSystem.UI.IAbilityHudController>(),
 688:                 IoC.Resolve<Features.CareerSystem.Abilities.IAbilityEffectExecutor>(),
 689:                 IoC.Resolve<IModLogger>()));
 690:         }
 691:     }
 692: 
 693:     protected override void OnApplicationTick(float dt)
 694:     {
 695:         _timeAccelerationService?.OnTick();
 696: 
 697:         _shaderTickAccumulator += dt;
 698:         if (_shaderTickAccumulator >= 1f)
 699:         {
 700:             _shaderTickAccumulator = 0f;
 701: 
 702:             if (!LoadingWindow.IsLoadingWindowActive)
 703:             {
 704:                 int count = Utilities.GetNumberOfShaderCompilationsInProgress();
 705:                 if (count > 0 && count != _lastShaderCount)
 706:                 {
 707:                     InformationManager.DisplayMessage(new InformationMessage(
 708:                         $"Shader compilation in progress. Remaining: {count}"));
 709:                 }
 710:                 _lastShaderCount = count;

 succeeded in 507ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityActivationController.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityInputAdapter.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ActiveBuffsAlgebra.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbility.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityBuffTracker.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityEffectRegistry.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAgentStatService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityActivationController.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityEffectExecutor.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityExecutionContext.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityInputAdapter.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAbilityEffectExecutor.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAbilityService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAgentStatService.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IMissionTimeProvider.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionTimeProvider.cs

 succeeded in 512ms:
   1: using Microsoft.VisualStudio.TestTools.UnitTesting;
   2: using NSubstitute;
   3: using TAOM.Features.CareerSystem.Abilities;
   4: 
   5: namespace TAOM.Tests.Features.CareerSystem;
   6: 
   7: [TestClass]
   8: public class AbilityActivationControllerTests
   9: {
  10:     private const string HeroId = "hero_1";
  11:     private const float ChargingThrottleSeconds = 2f;
  12: 
  13:     private ICareerAbilityService _abilityService;
  14:     private IAbilityInputAdapter _input;
  15:     private IMissionTimeProvider _time;
  16:     private AbilityActivationController _sut;
  17: 
  18:     [TestInitialize]
  19:     public void Setup()
  20:     {
  21:         _abilityService = Substitute.For<ICareerAbilityService>();
  22:         _input = Substitute.For<IAbilityInputAdapter>();
  23:         _time = Substitute.For<IMissionTimeProvider>();
  24:         _sut = new AbilityActivationController(_abilityService, _input, _time);
  25:     }
  26: 
  27:     [TestMethod]
  28:     public void Tick_NoCareer_ReturnsNoneAndDoesNotTickAbility()
  29:     {
  30:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: false);
  31: 
  32:         Assert.AreEqual(AbilityActivationOutcome.None, outcome);
  33:         _abilityService.DidNotReceive().Tick(Arg.Any<string>(), Arg.Any<float>());
  34:     }
  35: 
  36:     [TestMethod]
  37:     public void Tick_HasCareer_TicksAbilityWithDt()
  38:     {
  39:         _abilityService.IsAbilityReady(HeroId).Returns(false);
  40: 
  41:         _sut.Tick(0.016f, HeroId, hasCareer: true);
  42: 
  43:         _abilityService.Received(1).Tick(HeroId, 0.016f);
  44:     }
  45: 
  46:     [TestMethod]
  47:     public void Tick_ReadyFirstTime_ReturnsJustBecameReady()
  48:     {
  49:         _abilityService.IsAbilityReady(HeroId).Returns(true);
  50: 
  51:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
  52: 
  53:         Assert.AreEqual(AbilityActivationOutcome.JustBecameReady, outcome);
  54:     }
  55: 
  56:     [TestMethod]
  57:     public void Tick_AlreadyNotifiedReady_DoesNotReFireJustBecameReady()
  58:     {
  59:         _abilityService.IsAbilityReady(HeroId).Returns(true);
  60: 
  61:         var first = _sut.Tick(0.016f, HeroId, hasCareer: true);
  62:         var second = _sut.Tick(0.016f, HeroId, hasCareer: true);
  63: 
  64:         Assert.AreEqual(AbilityActivationOutcome.JustBecameReady, first);
  65:         Assert.AreEqual(AbilityActivationOutcome.None, second);
  66:     }
  67: 
  68:     [TestMethod]
  69:     public void Tick_VPressedWhileReady_ReturnsActivatedAndCallsService()
  70:     {
  71:         _abilityService.IsAbilityReady(HeroId).Returns(true);
  72:         _input.IsActivationKeyPressed().Returns(true);
  73: 
  74:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
  75: 
  76:         Assert.AreEqual(AbilityActivationOutcome.Activated, outcome);
  77:         _abilityService.Received(1).ActivateAbility(HeroId);
  78:     }
  79: 
  80:     [TestMethod]
  81:     public void Tick_VPressedWhileReady_ReArmsReadyNotificationForNextCycle()
  82:     {
  83:         // After activation the next time the ability becomes ready, the green toast must fire again.
  84:         _abilityService.IsAbilityReady(HeroId).Returns(true);
  85:         _input.IsActivationKeyPressed().Returns(true);
  86:         _sut.Tick(0.016f, HeroId, hasCareer: true); // Activated; resets _abilityReadyNotified
  87: 
  88:         _input.IsActivationKeyPressed().Returns(false);
  89:         var nextOutcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
  90: 
  91:         Assert.AreEqual(AbilityActivationOutcome.JustBecameReady, nextOutcome);
  92:     }
  93: 
  94:     [TestMethod]
  95:     public void Tick_VPressedWhileCharging_ReturnsCharging()
  96:     {
  97:         _abilityService.IsAbilityReady(HeroId).Returns(false);
  98:         _input.IsActivationKeyPressed().Returns(true);
  99:         _time.CurrentTime.Returns(0f); // _lastChargingMessageTime starts at -2f, so 0 - (-2) = 2 >= throttle
 100: 
 101:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
 102: 
 103:         Assert.AreEqual(AbilityActivationOutcome.Charging, outcome);
 104:     }
 105: 
 106:     [TestMethod]
 107:     public void Tick_VPressedTwiceWithinThrottle_SecondReturnsNone()
 108:     {
 109:         _abilityService.IsAbilityReady(HeroId).Returns(false);
 110:         _input.IsActivationKeyPressed().Returns(true);
 111: 
 112:         _time.CurrentTime.Returns(0f);
 113:         var first = _sut.Tick(0.016f, HeroId, hasCareer: true);
 114: 
 115:         _time.CurrentTime.Returns(1.5f); // 1.5 < 2.0 throttle window
 116:         var second = _sut.Tick(0.016f, HeroId, hasCareer: true);
 117: 
 118:         Assert.AreEqual(AbilityActivationOutcome.Charging, first);
 119:         Assert.AreEqual(AbilityActivationOutcome.None, second);
 120:     }
 121: 
 122:     [TestMethod]
 123:     public void Tick_VPressedAfterThrottleElapsed_ReturnsChargingAgain()
 124:     {
 125:         _abilityService.IsAbilityReady(HeroId).Returns(false);
 126:         _input.IsActivationKeyPressed().Returns(true);
 127: 
 128:         _time.CurrentTime.Returns(0f);
 129:         _sut.Tick(0.016f, HeroId, hasCareer: true);
 130: 
 131:         _time.CurrentTime.Returns(2.1f); // 2.1 - 0 = 2.1 >= 2.0 throttle
 132:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
 133: 
 134:         Assert.AreEqual(AbilityActivationOutcome.Charging, outcome);
 135:     }
 136: 
 137:     [TestMethod]
 138:     public void Tick_VNotPressed_StateMachineDoesNotEmitCharging()
 139:     {
 140:         _abilityService.IsAbilityReady(HeroId).Returns(false);
 141:         _input.IsActivationKeyPressed().Returns(false);
 142: 
 143:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
 144: 
 145:         Assert.AreEqual(AbilityActivationOutcome.None, outcome);
 146:     }
 147: 
 148:     [TestMethod]
 149:     public void Reset_ClearsReadyNotificationFlag()
 150:     {
 151:         _abilityService.IsAbilityReady(HeroId).Returns(true);
 152:         _sut.Tick(0.016f, HeroId, hasCareer: true); // _abilityReadyNotified := true
 153: 
 154:         _sut.Reset();
 155: 
 156:         var after = _sut.Tick(0.016f, HeroId, hasCareer: true);
 157:         Assert.AreEqual(AbilityActivationOutcome.JustBecameReady, after);
 158:     }
 159: 
 160:     [TestMethod]
 161:     public void Reset_ClearsThrottleSentinel()
 162:     {
 163:         // After Reset, the next Charging press should fire immediately even if time hasn't advanced.
 164:         _abilityService.IsAbilityReady(HeroId).Returns(false);
 165:         _input.IsActivationKeyPressed().Returns(true);
 166:         _time.CurrentTime.Returns(100f);
 167:         _sut.Tick(0.016f, HeroId, hasCareer: true); // _lastChargingMessageTime := 100
 168: 
 169:         _sut.Reset();
 170: 
 171:         _time.CurrentTime.Returns(0f); // mission restart; time resets
 172:         var outcome = _sut.Tick(0.016f, HeroId, hasCareer: true);
 173: 
 174:         Assert.AreEqual(AbilityActivationOutcome.Charging, outcome);
 175:     }
 176: }

codex
The new controller tests cover the requested cooldown-throttle cases, including the same-mission double-V case and Reset rearming. I’m now tracing the execution context and effect registry because the refactor changed where `Execute()` sits relative to the per-frame active-context tick loop.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/MissionAbilityExecutionContext.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/CareerAbilityEffectRegistry.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/AbilityInputAdapter.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/CareerAbilityService.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/MissionTimeProvider.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 497ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using TAOM.Core.Logging;
   4: using TaleWorlds.Engine;
   5: using TaleWorlds.Library;
   6: using TaleWorlds.MountAndBlade;
   7: 
   8: namespace TAOM.Features.CareerSystem.Abilities;
   9: 
  10: // Boundary adapter — the only class that touches TaleWorlds Agent/Mission types.
  11: // All ability effect logic in executors is expressed via IAbilityExecutionContext,
  12: // keeping services and executors free of sealed TaleWorlds dependencies.
  13: public sealed class MissionAbilityExecutionContext : IAbilityExecutionContext
  14: {
  15:     private readonly Agent _agent;
  16:     private readonly Mission _mission;
  17:     private readonly IModLogger _logger;
  18: 
  19:     // Tracks timed buff expiry; entries cleared when restore fires.
  20:     private readonly List<PendingRestore> _pendingRestores = new List<PendingRestore>();
  21:     private readonly MBList<Agent> _nearbyAlliesBuffer = new MBList<Agent>();
  22: 
  23:     public string HeroStringId { get; }
  24:     public float Duration { get; }
  25:     public float Radius { get; }
  26: 
  27:     // True when all timed restores have fired — used by CareerPerkMissionBehavior to prune finished contexts.
  28:     public bool IsExpired => _pendingRestores.Count == 0;
  29: 
  30:     public MissionAbilityExecutionContext(
  31:         string heroStringId,
  32:         float duration,
  33:         float radius,
  34:         Agent agent,
  35:         Mission mission,
  36:         IModLogger logger)
  37:     {
  38:         HeroStringId = heroStringId;
  39:         Duration = duration;
  40:         Radius = radius;
  41:         _agent = agent;
  42:         _mission = mission;
  43:         _logger = logger;
  44:     }
  45: 
  46:     public void ApplyMoraleBurst(float radius, float magnitude)
  47:     {
  48:         if (_agent == null || _mission == null) return;
  49: 
  50:         _nearbyAlliesBuffer.Clear();
  51:         _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);
  52: 
  53:         int boosted = 0;
  54:         foreach (var ally in _nearbyAlliesBuffer)
  55:         {
  56:             var ai = ally?.GetComponent<CommonAIComponent>();
  57:             if (ai == null) continue;
  58: 
  59:             ai.Morale = Math.Min(100f, ai.Morale + magnitude);
  60:             boosted++;
  61:         }
  62:     }
  63: 
  64:     public void ApplyStealthMode(float duration)
  65:     {
  66:         // Stealth in Bannerlord is controlled by detection radius on the agent.
  67:         // Full AI detection integration requires a Harmony prefix on the detection model.
  68:         _logger.LogDebug($"CareerSystem: StealthMode for {duration}s applied to '{HeroStringId}' (visual-only)");
  69:     }
  70: 
  71:     public void ApplyAllyBuff(float damageBonusFlat, float damageReductionFlat, float radius, float duration)
  72:     {
  73:         ApplyAoeBuff(radius, duration, new ActiveBuffs
  74:         {
  75:             DamageBonus = damageBonusFlat,
  76:             DamageReductionBonus = damageReductionFlat,
  77:         });
  78:     }
  79: 
  80:     public void ApplyAllyRangedBuff(float speedBonus, float damageBonus, float drawSpeedBonus, float radius, float duration)
  81:     {
  82:         ApplyAoeBuff(radius, duration, new ActiveBuffs
  83:         {
  84:             SpeedMultiplier = speedBonus,
  85:             CombatSpeedMultiplier = speedBonus,
  86:             DamageBonus = damageBonus,
  87:             DrawSpeedBonus = drawSpeedBonus,
  88:         });
  89:     }
  90: 
  91:     public void ApplyAllyCavalryBuff(float mountSpeedBonus, float chargeDamageBonus, float damageBonus, float radius, float duration)
  92:     {
  93:         ApplyAoeBuff(radius, duration, new ActiveBuffs
  94:         {
  95:             MountSpeedBonus = mountSpeedBonus,
  96:             ChargeDamageBonus = chargeDamageBonus,
  97:             DamageBonus = damageBonus,
  98:         });
  99:     }
 100: 
 101:     // Shared AoE dispatch — applies the same buffTemplate to:
 102:     //   - the caster via the hero buff dictionary (single entry)
 103:     //   - all nearby allies (excluding the caster) via the ally buff dictionary
 104:     // Multiple concurrent activations from different archetypes ACCUMULATE their field
 105:     // contributions; each scheduled restore subtracts only its own deltas, so overlapping
 106:     // auras from different sources compose instead of stomping each other.
 107:     private void ApplyAoeBuff(float radius, float duration, ActiveBuffs buffTemplate)
 108:     {
 109:         if (_agent == null || _mission == null) return;
 110: 
 111:         _nearbyAlliesBuffer.Clear();
 112:         // IMPORTANT: Mission.GetNearbyAllyAgents INCLUDES the caller's own agent in the result.
 113:         // Vanilla Bannerlord code post-filters the source agent; we must do the same.
 114:         // See: memory/feedback_collection_api_inclusion.md — Codex caught this as P1 (double-buff).
 115:         _mission.GetNearbyAllyAgents(_agent.Position.AsVec2, radius, _agent.Team, _nearbyAlliesBuffer);
 116: 
 117:         foreach (var ally in _nearbyAlliesBuffer)
 118:         {
 119:             if (ally == null || !ally.IsHuman || !ally.IsActive()) continue;
 120:             if (ally == _agent) continue; // MUST exclude caster — hero buff path handles them
 121: 
 122:             var existing = CareerAbilityBuffTracker.GetAllyBuff(ally.Index) ?? new ActiveBuffs();
 123:             ActiveBuffsAlgebra.Accumulate(existing, buffTemplate);
 124:             existing.ExpiresAt = Math.Max(existing.ExpiresAt, CurrentTime() + duration);
 125:             CareerAbilityBuffTracker.SetAllyBuff(ally.Index, existing);
 126:             ally.UpdateAgentProperties();
 127: 
 128:             var allyIndex = ally.Index;
 129:             var allyRef = ally;
 130:             var deltasCopy = ActiveBuffsAlgebra.Clone(buffTemplate);
 131:             ScheduleRestore(() =>
 132:             {
 133:                 var current = CareerAbilityBuffTracker.GetAllyBuff(allyIndex);
 134:                 if (current == null) return;
 135:                 ActiveBuffsAlgebra.Subtract(current, deltasCopy);
 136:                 if (allyRef.IsActive())
 137:                     allyRef.UpdateAgentProperties();
 138:             }, duration);
 139:         }
 140: 
 141:         // Apply to the caster via the hero buff path (also uses accumulate-and-subtract)
 142:         var heroBuff = CareerAbilityBuffTracker.GetBuff(HeroStringId) ?? new ActiveBuffs();
 143:         ActiveBuffsAlgebra.Accumulate(heroBuff, buffTemplate);
 144:         heroBuff.ExpiresAt = Math.Max(heroBuff.ExpiresAt, CurrentTime() + duration);
 145:         CareerAbilityBuffTracker.SetBuff(HeroStringId, heroBuff);
 146:         _agent.UpdateAgentProperties();
 147: 
 148:         var heroDeltasCopy = ActiveBuffsAlgebra.Clone(buffTemplate);
 149:         ScheduleRestore(() =>
 150:         {
 151:             var current = CareerAbilityBuffTracker.GetBuff(HeroStringId);
 152:             if (current == null) return;
 153:             ActiveBuffsAlgebra.Subtract(current, heroDeltasCopy);
 154:             _agent?.UpdateAgentProperties();
 155:         }, duration);
 156:     }
 157: 
 158:     public void PlaySound(string soundId)
 159:     {
 160:         if (string.IsNullOrEmpty(soundId)) return;
 161:         var eventId = SoundEvent.GetEventIdFromString(soundId);
 162:         if (eventId >= 0)
 163:             SoundEvent.PlaySound2D(eventId);
 164:     }
 165: 
 166:     public void PlayParticle(string particleId)
 167:     {
 168:         // Particle playback requires registered particle definitions (asset files).
 169:         // Guard against missing assets gracefully.
 170:         if (string.IsNullOrEmpty(particleId)) return;
 171:         _logger.LogDebug($"CareerSystem: PlayParticle '{particleId}' requested for '{HeroStringId}'");
 172:     }
 173: 
 174:     // Called by CareerPerkMissionBehavior on each tick to expire timed buffs.
 175:     public void Tick(float currentMissionTime)
 176:     {
 177:         for (int i = _pendingRestores.Count - 1; i >= 0; i--)
 178:         {
 179:             if (currentMissionTime >= _pendingRestores[i].ExpiresAt)
 180:             {
 181:                 _pendingRestores[i].Restore();
 182:                 _pendingRestores.RemoveAt(i);
 183:             }
 184:         }
 185:     }
 186: 
 187:     private float CurrentTime() => _mission?.CurrentTime ?? 0f;
 188: 
 189:     private void ScheduleRestore(Action restore, float duration)
 190:     {
 191:         float expiresAt = CurrentTime() + duration;
 192:         _pendingRestores.Add(new PendingRestore(restore, expiresAt));
 193:     }
 194: 
 195:     private sealed class PendingRestore
 196:     {
 197:         private readonly Action _restore;
 198:         public float ExpiresAt { get; }
 199: 
 200:         public PendingRestore(Action restore, float expiresAt)
 201:         {
 202:             _restore = restore;
 203:             ExpiresAt = expiresAt;
 204:         }
 205: 
 206:         public void Restore() => _restore();
 207:     }
 208: }

 succeeded in 487ms:
   1: using System.Collections.Generic;
   2: using TAOM.Core.Logging;
   3: using TAOM.Features.CareerSystem.Domain;
   4: 
   5: namespace TAOM.Features.CareerSystem.Abilities;
   6: 
   7: public class CareerAbilityService : ICareerAbilityService
   8: {
   9:     private readonly ICareerConfigProvider _config;
  10:     private readonly IModLogger _logger;
  11:     private readonly Dictionary<string, CareerAbility> _abilities = new Dictionary<string, CareerAbility>();
  12: 
  13:     public CareerAbilityService(ICareerConfigProvider config, IModLogger logger)
  14:     {
  15:         _config = config;
  16:         _logger = logger;
  17:     }
  18: 
  19:     public CareerAbility GetOrCreateAbility(string heroStringId, ICareerRegistry registry, ICareerDataService dataService)
  20:     {
  21:         if (_abilities.TryGetValue(heroStringId, out var existing))
  22:             return existing;
  23: 
  24:         var careerId = dataService.GetCareerStringId(heroStringId);
  25:         if (string.IsNullOrEmpty(careerId))
  26:         {
  27:             _logger.LogDebug($"CareerSystem: GetOrCreateAbility — no career for hero '{heroStringId}'");
  28:             return null;
  29:         }
  30: 
  31:         var career = registry.GetCareer(careerId);
  32:         if (career == null)
  33:         {
  34:             _logger.LogWarning($"CareerSystem: GetOrCreateAbility — career '{careerId}' not found in registry for hero '{heroStringId}'");
  35:             return null;
  36:         }
  37: 
  38:         var cooldownSeconds = _config.GetAbilityTuning().Global.CooldownSeconds;
  39: 
  40:         var ability = new CareerAbility(
  41:             career.AbilityTemplateId,
  42:             ChargeType.CooldownOnly,
  43:             maxCharge: 0f,
  44:             cooldownDuration: cooldownSeconds);
  45: 
  46:         _abilities[heroStringId] = ability;
  47:         _logger.LogInfo($"CareerSystem: Created ability for hero '{heroStringId}' — template='{career.AbilityTemplateId}', cooldownSeconds={cooldownSeconds}");
  48:         return ability;
  49:     }
  50: 
  51:     public void Tick(string heroStringId, float dt)
  52:     {
  53:         if (_abilities.TryGetValue(heroStringId, out var ability))
  54:             ability.Tick(dt);
  55:     }
  56: 
  57:     public bool IsAbilityReady(string heroStringId)
  58:     {
  59:         return _abilities.TryGetValue(heroStringId, out var ability) && ability.IsReady;
  60:     }
  61: 
  62:     public float GetCooldownRemaining(string heroStringId)
  63:     {
  64:         return _abilities.TryGetValue(heroStringId, out var ability) ? ability.CooldownRemaining : 0f;
  65:     }
  66: 
  67:     public void ActivateAbility(string heroStringId)
  68:     {
  69:         if (_abilities.TryGetValue(heroStringId, out var ability))
  70:         {
  71:             ability.Activate();
  72:             _logger.LogInfo($"CareerSystem: Ability activated for hero '{heroStringId}'");
  73:         }
  74:         else
  75:         {
  76:             _logger.LogWarning($"CareerSystem: ActivateAbility — no ability found for hero '{heroStringId}'");
  77:         }
  78:     }
  79: 
  80:     public void ClearAll()
  81:     {
  82:         _abilities.Clear();
  83:     }
  84: }

 succeeded in 525ms:
   1: using System.Collections.Generic;
   2: 
   3: namespace TAOM.Features.CareerSystem.Abilities;
   4: 
   5: public class CareerAbilityEffectRegistry
   6: {
   7:     private static readonly ICareerAbilityEffectExecutor NoOp = new NoOpExecutor();
   8: 
   9:     private readonly Dictionary<string, ICareerAbilityEffectExecutor> _executors
  10:         = new Dictionary<string, ICareerAbilityEffectExecutor>();
  11: 
  12:     public void Register(ICareerAbilityEffectExecutor executor)
  13:     {
  14:         _executors[executor.CareerId] = executor;
  15:     }
  16: 
  17:     public ICareerAbilityEffectExecutor GetExecutor(string careerId)
  18:     {
  19:         return _executors.TryGetValue(careerId, out var executor) ? executor : NoOp;
  20:     }
  21: 
  22:     private sealed class NoOpExecutor : ICareerAbilityEffectExecutor
  23:     {
  24:         public string CareerId => "__noop__";
  25:         public void Execute(IAbilityExecutionContext context) { }
  26:     }
  27: }

 succeeded in 530ms:
   1: using TaleWorlds.InputSystem;
   2: 
   3: namespace TAOM.Features.CareerSystem.Abilities;
   4: 
   5: public class AbilityInputAdapter : IAbilityInputAdapter
   6: {
   7:     public bool IsActivationKeyPressed() => Input.IsKeyPressed(InputKey.V);
   8: }

 succeeded in 760ms:
   1: using TaleWorlds.MountAndBlade;
   2: 
   3: namespace TAOM.Features.CareerSystem.Abilities;
   4: 
   5: public class MissionTimeProvider : IMissionTimeProvider
   6: {
   7:     public float CurrentTime => Mission.Current?.CurrentTime ?? 0f;
   8: }

codex
The executor registers the context before dispatching the effect, matching the legacy ordering. One subtle reorder remains: after activation, the new context is now ticked later in the same frame; because expiry is mission-time based and duration is positive, that does not appear to shorten buffs in practice. I’m moving to vanilla verification now.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'Get-Command ilspycmd -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -LiteralPath 'E:\\Decompiled_Bannerlord' -Directory | Select-Object -First 30 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'Mission.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'Input.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'ScreenManager.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 555ms:
C:\Users\mikew\.dotnet\tools\ilspycmd.exe

 succeeded in 560ms:
E:\Decompiled_Bannerlord\Campaign
E:\Decompiled_Bannerlord\Core
E:\Decompiled_Bannerlord\Engine
E:\Decompiled_Bannerlord\Modules
E:\Decompiled_Bannerlord\MountAndBlade
E:\Decompiled_Bannerlord\Network
E:\Decompiled_Bannerlord\Platform
E:\Decompiled_Bannerlord\ThirdParty
E:\Decompiled_Bannerlord\UI

 succeeded in 623ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs

 succeeded in 684ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs

 succeeded in 685ms:
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs

codex
Local decompiled Bannerlord sources are available under `E:\Decompiled_Bannerlord`, and `ilspycmd` is installed. I’ll cite those local decompile excerpts for the API claims rather than infer from memory.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' -Pattern 'void Tick|CurrentTime|OnMissionTick|TickAgentsAndTeams' -Context 3,8" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade' -Recurse -Filter 'MissionBehaviorCollection.cs' -ErrorAction SilentlyContinue | Select-Object -First 5 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter 'GauntletLayer.cs' -ErrorAction SilentlyContinue | Select-Object -First 10 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\Input.cs' -Pattern 'IsKeyPressed|IsKeyDown|Update' -Context 2,6" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenManager.cs' -Pattern 'TopScreen|AddLayer|RemoveLayer|Tick' -Context 2,8" in C:\Users\mikew\source\repos\TAOM
 succeeded in 536ms:

  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1180:
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1181:	public 
MissionMode Mode => _missionMode;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1182:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1183:	public 
float [7mCurrentTime [0m[7m[0m=> [0m[7m[0m_cachedMissionTime;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1184:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1185:	[0m[7m[0mpublic [0m
[7m[0mbool [0m[7m[0mPauseAITick[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1186:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1187:		get[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1188:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1189:	[0m[7m	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mMBAPI.IMBMission.GetPauseAITick(Pointer);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1190:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1191:		set[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1813:
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1814:	
[UsedImplicitly]
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1815:	
[MBCallback(null, false)]
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1816:	internal 
[7mvoid [0m[7mTick[0mAgentsAndTeams(float [0m[7m[0mdt, [0m[7m[0mbool [0m[7m[0mtickPaused)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1817:	{[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1818:		
[7mTickAgentsAndTeams[0mImp(dt, [0m[7m[0mtickPaused);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1819:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1820:[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1821:	public 
[7mvoid [0m[7mTick[0mAgentsAndTeamsAsync(float [0m[7m[0mdt)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1822:	{[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1823:		
MBAPI.IMBMission.[7mTickAgentsAndTeams[0mAsync(Pointer, [0m[7m[0mdt);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1824:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1825:[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1826:	internal 
[7mvoid [0m[7mTick[0m(float [0m[7m[0mdt)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1827:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1828:	[0m[7m	[0m
[7m[0mMBAPI.IMBMission.Tick(Pointer, [0m[7m[0mdt);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1829:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1830:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1831:	[0m[7m[0minternal [0m
[7m[0mvoid [0m[7m[0mIdleTick(float [0m[7m[0mdt)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1832:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1833:	[0m[7m	[0m
[7m[0mMBAPI.IMBMission.IdleTick(Pointer, [0m[7m[0mdt);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1834:	}[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3590:		}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3591:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3592:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3593:	public 
[7mvoid [0m[7mTick[0mAgentsAndTeamsImp(float [0m[7m[0mdt, [0m[7m[0mbool [0m[7m[0mtickPaused)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3594:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3595:	[0m[7m	[0m[7m[0mfloat [0m[7m[0mnum [0m
[7m[0m= [0m[7m[0m(tickPaused [0m[7m[0m? [0m[7m[0m0f [0m[7m[0m: [0m[7m[0mdt);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3596:	[0m[7m	[0m
[7m[0mTWParallel.For(0, [0m[7m[0mAllAgents.Count, [0m[7m[0mnum, [0m[7m[0mAgentTickMT);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3597:	[0m[7m	[0m[7m[0mforeach [0m
[7m[0m(Agent [0m[7m[0mallAgent [0m[7m[0min [0m[7m[0mAllAgents)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3598:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3599:	[0m[7m	[0m[7m	[0m
[7m[0mallAgent.Tick(num);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3600:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3601:	[0m[7m	[0m[7m[0mforeach [0m
[7m[0m(Team [0m[7m[0mteam [0m[7m[0min [0m[7m[0mTeams)[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3706:		}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3707:		
_tickActions.Clear();
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3708:		
MissionTimeTracker.Tick(dt);
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3709:		
CheckMissionEnd([7mCurrentTime[0m);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3710:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(IsFastForward [0m[7m[0m&& [0m[7m[0mMissionEnded)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3711:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3712:	[0m[7m	[0m[7m	[0m
[7m[0mIsFastForward [0m[7m[0m= [0m[7m[0mfalse;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3713:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3714:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(CurrentState [0m[7m[0m!= [0m[7m[0mState.Continuing)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3715:		{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3716:			return;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3717:		}[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3718:		if 
(_inMissionLoadingScreenTimer != null && _inMissionLoadingScreenTimer.Check([7mCurrentTime[0m))[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3719:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3720:	[0m[7m	[0m[7m	[0m
[7m[0m_inMissionLoadingScreenTimer [0m[7m[0m= [0m[7m[0mnull;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3721:	[0m[7m	[0m[7m	[0m
[7m[0m_onLoadingEndedAction?.Invoke();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3722:	[0m[7m	[0m[7m	[0m
[7m[0mLoadingWindow.DisableGlobalLoadingWindow();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3723:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3724:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m
[7m[0mnum [0m[7m[0m= [0m[7m[0mMissionBehaviors.Count [0m[7m[0m- [0m[7m[0m1; [0m[7m[0mnum [0m[7m[0m>= [0m[7m[0m0; [0m[7m[0mnum--)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3725:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3726:	[0m[7m	[0m[7m	[0m
[7m[0mMissionBehaviors[num].OnPreDisplayMissionTick(dt);[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3732:		
tickCompleted = false;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3733:		for (int 
num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3734:		{
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3735:			
MissionBehaviors[num2].[7mOnMissionTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3736:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3737:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m
[7m[0mnum3 [0m[7m[0m= [0m[7m[0m_dynamicEntities.Count [0m[7m[0m- [0m[7m[0m1; [0m[7m[0mnum3 [0m[7m[0m>= [0m[7m[0m0; [0m[7m[0mnum3--)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3738:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3739:	[0m[7m	[0m[7m	[0m
[7m[0mDynamicEntityInfo [0m[7m[0mdynamicEntityInfo [0m[7m[0m= [0m[7m[0m_dynamicEntities[num3];[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3740:			if 
(dynamicEntityInfo.TimerToDisable.Check([7mCurrentTime[0m))[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3741:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3742:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mdynamicEntityInfo.Entity.RemoveEnginePhysics();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3743:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mdynamicEntityInfo.Entity.Remove(79);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3744:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0m_dynamicEntities.RemoveAt(num3);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3745:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3746:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3747:	[0m[7m	[0m
[7m[0mHandleSpawnedItems();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3748:	[0m[7m	[0m
[7m[0mDebugNetworkEventStatistics.EndTick(dt);[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3759:		}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3760:		if 
(doAsyncAITick)
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3761:		{
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3762:			
[7mTickAgentsAndTeams[0mAsync(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3763:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3764:		else[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3765:		{[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3766:			
[7mTickAgentsAndTeams[0mImp(dt, [0m[7m[0mtickPaused: [0m[7m[0mfalse);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3767:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3768:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3769:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3770:	[0m[7m[0mpublic [0m
[7m[0mvoid [0m[7m[0mAddTickAction(MissionTickAction [0m[7m[0maction, [0m[7m[0mAgent [0m[7m[0magent, [0m[7m[0mint [0m[7m[0mparam1, [0m[7m[0mint [0m[7m[0mparam2)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3771:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3772:	[0m[7m	[0m
[7m[0m_tickActions.Add((action, [0m[7m[0magent, [0m[7m[0mparam1, [0m[7m[0mparam2));[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3773:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3774:[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4764:		}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4765:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4766:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4767:	private 
void CheckMissionEnd(float [7mcurrentTime[0m)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4768:	{[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4769:		if 
(!GameNetwork.IsClient && [7mcurrentTime [0m[7m[0m> [0m[7m[0mNextCheckTimeEndMission)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4770:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4771:	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(CurrentState [0m[7m[0m== [0m[7m[0mState.Continuing)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4772:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4773:	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(MissionEnded)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4774:				{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4775:					return;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4776:				}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4777:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mNextCheckTimeEndMission [0m[7m[0m+= [0m[7m[0m0.1f;[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4780:				{
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4781:					return;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4782:				}
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4783:				
_missionEndTime = [7mcurrentTime [0m[7m[0m+ [0m[7m[0mMissionCloseTimeAfterFinish;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4784:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mNextCheckTimeEndMission [0m[7m[0m+= [0m[7m[0m5f;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4785:				{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4786:	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mforeach [0m[7m[0m(MissionLogic [0m[7m[0mmissionLogic [0m[7m[0min [0m[7m[0mMissionLogics)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4787:					{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4788:	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mmissionLogic.ShowBattleResults();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4789:					}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4790:					return;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4791:				}[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4792:			}
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4793:			if 
([7mcurrentTime [0m[7m[0m> [0m[7m[0m_missionEndTime)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4794:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4795:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mEndMissionInternal();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4796:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4797:			else[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4798:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4799:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mNextCheckTimeEndMission [0m[7m[0m+= [0m[7m[0m5f;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4800:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4801:		}[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4802:		else if 
(CurrentState != State.Continuing && [7mcurrentTime [0m[7m[0m> [0m[7m[0mNextCheckTimeEndMission)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4803:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4804:	[0m[7m	[0m[7m	[0m
[7m[0mEndMissionInternal();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4805:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4806:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4807:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4808:	[0m[7m[0mpublic [0m
[7m[0mbool [0m[7m[0mIsPlayerCloseToAnEnemy(float [0m[7m[0mdistance [0m[7m[0m= [0m[7m[0m5f)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4809:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4810:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(MainAgent [0m[7m[0m== [0m[7m[0mnull)[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5253:
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5254:	public 
void ShowInMissionLoadingScreen(int durationInSecond, Action onLoadingEndedAction)
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5255:	{
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5256:		
_inMissionLoadingScreenTimer = new TaleWorlds.Core.Timer([7mCurrentTime[0m, [0m[7m[0mdurationInSecond);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5257:	[0m[7m	[0m
[7m[0m_onLoadingEndedAction [0m[7m[0m= [0m[7m[0monLoadingEndedAction;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5258:	[0m[7m	[0m
[7m[0mLoadingWindow.EnableGlobalLoadingWindow();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5259:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5260:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5261:	[0m[7m[0mpublic [0m
[7m[0mbool [0m[7m[0mCanAgentRout(Agent [0m[7m[0magent)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5262:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5263:	[0m[7m	[0m[7m[0mif [0m
[7m[0m((agent.IsRunningAway [0m[7m[0m|| [0m[7m[0m(agent.CommonAIComponent [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0magent.CommonAIComponent.IsRetreating) [0m[7m[0m|| [0m
[7m[0m(agent.GetAgentFlags().HasAnyFlag(AgentFlag.CanWander) [0m[7m[0m&& [0m[7m[0magent.IsWandering())) [0m[7m[0m&& [0m[7m[0magent.RiderAgent [0m[7m[0m== [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:5264:		{[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6660:		return 
false;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6661:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6662:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6663:	private 
[7mvoid [0m[7mTick[0mDebugAgents()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6664:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6665:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6666:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6667:	[0m[7m[0mpublic [0m
[7m[0mvoid [0m[7m[0mAddTimerToDynamicEntity(GameEntity [0m[7m[0mgameEntity, [0m[7m[0mfloat [0m[7m[0mtimeToKill [0m[7m[0m= [0m[7m[0m10f)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6668:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6669:	[0m[7m	[0m
[7m[0mDynamicEntityInfo [0m[7m[0mitem [0m[7m[0m= [0m[7m[0mnew [0m[7m[0mDynamicEntityInfo[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6670:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6671:	[0m[7m	[0m[7m	[0m[7m[0mEntity [0m[7m[0m= [0m
[7m[0mgameEntity,[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6672:			
TimerToDisable = new TaleWorlds.Core.Timer([7mCurrentTime[0m, [0m[7m[0mtimeToKill)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6673:		};[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6674:	[0m[7m	[0m
[7m[0m_dynamicEntities.Add(item);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6675:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6676:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6677:	[0m[7m[0mpublic [0m
[7m[0mvoid [0m[7m[0mAddListener(IMissionListener [0m[7m[0mlistener)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6678:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6679:	[0m[7m	[0m
[7m[0m_listeners.Add(listener);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:6680:	}[0m


 succeeded in 499ms:
 succeeded in 511ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\GauntletLayer.cs

 succeeded in 563ms:

  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:123:	public static Vec2 
MousePositionPixel => InputState.MousePositionPixel;
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:124:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:125:	public static bool 
IsMousePosition[7mUpdate[0md [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:126:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:127:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsMouseScrollChanged [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:128:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:129:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsPlaystation(this [0m[7m[0mControllerTypes [0m[7m[0mcontrollerType)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:130:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:131:	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mcontrollerType.HasAnyFlag((ControllerTypes)6);[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:141:	}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:142:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:143:	public static void 
[7mUpdate[0mKeyData(byte[] [0m[7m[0mkeyData)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:144:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:145:		
InputManager.[7mUpdate[0mKeyData(keyData);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:146:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:147:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:148:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mfloat [0m
[7m[0mGetMouseMoveX()[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:149:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:150:	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mInputManager.GetMouseMoveX();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:151:	}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:186:	}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:187:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:188:	public static bool 
[7mIsKeyPressed[0m(InputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:189:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:190:		return 
InputManager.[7mIsKeyPressed[0m(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:191:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:192:[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:193:	public static bool 
[7mIsKeyDown[0m(InputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:194:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:195:		return 
InputManager.[7mIsKeyDown[0m(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:196:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:197:[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:198:	public static bool 
[7mIsKeyDown[0mImmediate(InputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:199:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:200:		return 
InputManager.[7mIsKeyDown[0mImmediate(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:201:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:202:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:203:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsKeyReleased(InputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:204:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:205:	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mInputManager.IsKeyReleased(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:206:	}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:225:		for (int i = 
startKeyNo; i < 256; i++)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:226:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:227:			if 
([7mIsKeyPressed[0m((InputKey)i))[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:228:			{[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:229:				result = i;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:230:				break;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:231:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:232:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:233:		return result;[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:239:		for (int i = 
startKeyNo; i < 256; i++)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:240:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:241:			if 
([7mIsKeyDown[0m((InputKey)i))[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:242:			{[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:243:				result = i;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:244:				break;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:245:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:246:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:247:		return result;[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:279:	public static bool 
IsDown(this InputKey key)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:280:	{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:281:		return [7mIsKeyDown[0m(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:282:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:283:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:284:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsPressed(this [0m[7m[0mInputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:285:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:286:		return 
[7mIsKeyPressed[0m(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:287:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:288:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:289:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsReleased(this [0m[7m[0mInputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:290:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:291:	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mIsKeyReleased(key);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:292:	}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:302:	}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:303:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:304:	public static void 
[7mUpdate[0m()[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:305:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:306:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(!IsOnScreenKeyboardActive)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:307:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:308:	[0m[7m	[0m[7m	[0m[7m[0mfloat [0m[7m[0mmousePositionX [0m[7m[0m= [0m
[7m[0mInputManager.GetMousePositionX();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:309:	[0m[7m	[0m[7m	[0m[7m[0mfloat [0m[7m[0mmousePositionY [0m[7m[0m= [0m
[7m[0mInputManager.GetMousePositionY();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:310:	[0m[7m	[0m[7m	[0m[7m[0mfloat [0m[7m[0mmouseScrollValue [0m
[7m[0m= [0m[7m[0mInputManager.GetMouseScrollValue();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:311:			IsMousePosition[7mUpdate[0md [0m
[7m[0m= [0m[7m[0mInputState.UpdateMousePosition(mousePositionX, [0m[7m[0mmousePositionY);[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:312:			IsMouseScrollChanged = 
InputState.[7mUpdate[0mMouseScroll(mouseScrollValue);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:313:	[0m[7m	[0m[7m	[0m[7m[0mIsGamepadActive [0m[7m[0m= [0m
[7m[0mIsControllerConnected [0m[7m[0m&& [0m[7m[0m!IsMouseActive;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:314:	[0m[7m	[0m[7m	[0m[7m[0mIsAnyTouchActive [0m[7m[0m= [0m
[7m[0mInputManager.IsAnyTouchActive();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:315:	[0m[7m	[0m[7m	[0m[7m[0mControllerType [0m[7m[0m= [0m
[7m[0mInputManager.GetControllerType();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:316:			[7mUpdate[0mKeyData(keyData);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:317:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:318:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:319:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:320:	[0m[7m[0mpublic [0m[7m[0mstatic [0m[7m[0mbool [0m
[7m[0mIsControllerKey(InputKey [0m[7m[0mkey)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:321:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:322:		switch (key)[0m


 succeeded in 567ms:

  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:39:	private static 
List<InputKey> _lastMouseActiveKeys;
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:40:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:41:	public static 
bool DisableScreenManager[7mTick[0ms;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:42:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:43:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mbool [0m[7m[0m_activeMouseVisible;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:44:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:45:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mIReadOnlyList<int> [0m[7m[0m_lastPressedKeys;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:46:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:47:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mbool [0m[7m[0m_globalOrderDirty;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:48:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:49:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mScreenLayer [0m[7m[0m_mouseDownLayer;[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:75:	public static 
bool IsEnterButtonRDown => _engineInterface.GetIsEnterButtonRDown();
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:76:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:77:	public static 
bool IsLate[7mTick[0mInProgress [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:78:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:79:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mList<ScreenLayer> [0m[7m[0mSortedLayers[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:80:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:81:		get[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:82:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:83:			if 
(_isSortedActiveLayersDirty || _sortedLayers.Count != [7mTopScreen[0m?.Layers.Count [0m[7m[0m+ [0m[7m[0m_globalLayers?.Count)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:84:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:85:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0m_sortedLayers.Clear();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:86:				if 
([7mTopScreen [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:87:				{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:88:					for (int i 
= 0; i < [7mTopScreen[0m.Layers.Count; [0m[7m[0mi++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:89:					{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:90:						
ScreenLayer screenLayer = [7mTopScreen[0m.Layers[i];[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:91:	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(screenLayer [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:92:						{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:93:	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0m_sortedLayers.Add(screenLayer);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:94:						}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:95:					}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:96:				}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:97:	[0m[7m	[0m[7m	[0m[7m	[0m[7m[0mforeach [0m
[7m[0m(GlobalLayer [0m[7m[0mglobalLayer [0m[7m[0min [0m[7m[0m_globalLayers)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:98:				{[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:106:	}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:107:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:108:	public static 
ScreenBase [7mTopScreen [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:109:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:110:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mScreenLayer [0m[7m[0mFocusedLayer [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:111:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:112:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mScreenLayer [0m[7m[0mFirstHitLayer [0m[7m[0m{ [0m[7m[0mget; [0m[7m[0mprivate [0m[7m[0mset; [0m[7m[0m}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:113:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:114:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mevent [0m[7m[0mOnPushScreenEvent [0m[7m[0mOnPushScreen;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:115:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:116:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mevent [0m[7m[0mOnPopScreenEvent [0m[7m[0mOnPopScreen;[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:129:		
_sortedActiveLayersCopyForUpdate = new ScreenLayer[16];
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:130:		
_isSortedActiveLayersDirty = true;
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:131:		
DisableScreenManager[7mTick[0ms [0m[7m[0m= [0m[7m[0mfalse;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:132:	[0m[7m	[0m
[7m[0m_isRefreshActive [0m[7m[0m= [0m[7m[0mfalse;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:133:	[0m[7m	[0m[7m[0m_globalLayers [0m
[7m[0m= [0m[7m[0mnew [0m[7m[0mObservableCollection<GlobalLayer>();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:134:	[0m[7m	[0m[7m[0m_screenList [0m[7m[0m= [0m
[7m[0mnew [0m[7m[0mObservableCollection<ScreenBase>();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:135:	[0m[7m	[0m
[7m[0m_lastMouseActiveKeys [0m[7m[0m= [0m[7m[0mnew [0m[7m[0mList<InputKey>();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:136:	[0m[7m	[0m
[7m[0m_screenList.CollectionChanged [0m[7m[0m+= [0m[7m[0mOnScreenListChanged;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:137:	[0m[7m	[0m
[7m[0m_globalLayers.CollectionChanged [0m[7m[0m+= [0m[7m[0mOnGlobalListChanged;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:138:	[0m[7m	[0m
[7m[0mScreenLayer.OnLayerActiveStateChanged [0m[7m[0m+= [0m[7m[0mOnLayerActiveStateChanged;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:139:	[0m[7m	[0m[7m[0mFocusedLayer [0m
[7m[0m= [0m[7m[0mnull;[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:282:	}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:283:
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:284:	public static 
void [7mTick[0m(float [0m[7m[0mdt)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:285:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:286:		if 
(DisableScreenManager[7mTick[0ms)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:287:		{[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:288:			return;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:289:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:290:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mi [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mi [0m[7m[0m< [0m[7m[0m_globalLayers.Count; [0m[7m[0mi++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:291:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:292:			
_globalLayers[i]?.Early[7mTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:293:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:294:		Update();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:295:		if ([7mTopScreen [0m
[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:296:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:297:			
[7mTopScreen[0m.FrameTick(dt);[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:298:			
FindPredecessor([7mTopScreen[0m)?.IdleTick(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:299:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:300:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mj [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mj [0m[7m[0m< [0m[7m[0mSortedLayers.Count; [0m[7m[0mj++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:301:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:302:	[0m[7m	[0m[7m	[0m[7m[0mScreenLayer [0m
[7m[0mscreenLayer [0m[7m[0m= [0m[7m[0mSortedLayers[j];[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:303:	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(screenLayer [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0mscreenLayer.IsActive [0m[7m[0m&& [0m[7m[0m!screenLayer.IsFinalized)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:304:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:305:				
screenLayer.[7mTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:306:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:307:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:308:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mk [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mk [0m[7m[0m< [0m[7m[0m_globalLayers.Count; [0m[7m[0mk++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:309:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:310:			
_globalLayers[k]?.[7mTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:311:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:312:	[0m[7m	[0m
[7m[0mLateUpdate(dt);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:313:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0ml [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0ml [0m[7m[0m< [0m[7m[0m_globalLayers.Count; [0m[7m[0ml++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:314:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:315:			
_globalLayers[l]?.Late[7mTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:316:		}[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:317:		if ([7mTopScreen [0m
[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:318:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:319:			
[7mTopScreen[0m.PostFrameTick(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:320:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:321:	[0m[7m	[0m
[7m[0mShowScreenDebugInformation();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:322:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:323:[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:324:	public static 
void Late[7mTick[0m(float [0m[7m[0mdt)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:325:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:326:		
IsLate[7mTick[0mInProgress [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:327:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mi [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mi [0m[7m[0m< [0m[7m[0mSortedLayers.Count; [0m[7m[0mi++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:328:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:329:	[0m[7m	[0m[7m	[0m[7m[0mScreenLayer [0m
[7m[0mscreenLayer [0m[7m[0m= [0m[7m[0mSortedLayers[i];[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:330:	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(screenLayer [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0mscreenLayer.IsActive [0m[7m[0m&& [0m[7m[0m!screenLayer.IsFinalized)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:331:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:332:				
screenLayer.Render[7mTick[0m(dt);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:333:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:334:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:335:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mj [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mj [0m[7m[0m< [0m[7m[0mSortedLayers.Count; [0m[7m[0mj++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:336:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:337:	[0m[7m	[0m[7m	[0m[7m[0mScreenLayer [0m
[7m[0mscreenLayer2 [0m[7m[0m= [0m[7m[0mSortedLayers[j];[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:338:	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(screenLayer2 [0m[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0mscreenLayer2.IsFocusLayer)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:339:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:340:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mscreenLayer2.Input.UnregisterReleasedKeys();[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:341:			}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:342:		}
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:343:		
IsLate[7mTick[0mInProgress [0m[7m[0m= [0m[7m[0mfalse;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:344:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:345:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:346:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mbool [0m[7m[0mOnPlatformScreenKeyboardRequested(string [0m[7m[0minitialText, [0m[7m[0mstring [0m[7m[0mdescriptionText, [0m[7m[0mint [0m[7m[0mmaxLength, [0m[7m[0mint [0m[7m[0mkeyboardTypeEnum)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:347:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:348:	[0m[7m	[0m[7m[0mreturn [0m
[7m[0mScreenManager.PlatformTextRequested?.Invoke(initialText, [0m[7m[0mdescriptionText, [0m[7m[0mmaxLength, [0m[7m[0mkeyboardTypeEnum) [0m[7m[0m?? [0m[7m[0mfalse;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:349:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:350:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:351:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mvoid [0m[7m[0mOnOnscreenKeyboardDone(string [0m[7m[0minputText)[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:369:		}
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:370:		
TaleWorlds.Library.Debug.Print("OnGameWindowFocusChange: " + _isWindowFocused);
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:371:		
TaleWorlds.Library.Debug.Print("[7mTopScreen[0m: [0m[7m[0m" [0m[7m[0m+ [0m[7m[0mTopScreen?.GetType()?.Name);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:372:	[0m[7m	[0m[7m[0mbool [0m[7m[0mflag [0m[7m[0m= [0m
[7m[0mfalse;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:373:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(!Debugger.IsAttached [0m[7m[0m&& [0m[7m[0m!flag)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:374:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:375:			
[7mTopScreen[0m?.OnFocusChangeOnGameWindow(focusGained);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:376:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:377:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(focusGained)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:378:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:379:	[0m[7m	[0m[7m	[0m
[7m[0mScreenManager.FocusGained?.Invoke();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:380:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:381:	[0m[7m	[0m
[7m[0mFocusedLayer?.Input.ResetLastDownKeys();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:382:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:383:[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:384:	public static 
void Replace[7mTopScreen[0m(ScreenBase [0m[7m[0mscreen)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:385:	{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:386:		
TaleWorlds.Library.Debug.Print("ReplaceTo[7mTopScreen[0m");[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:387:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(_screenList.Count [0m[7m[0m> [0m[7m[0m0)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:388:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:389:			
[7mTopScreen[0m.HandlePause();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:390:			
[7mTopScreen[0m.HandleDeactivate();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:391:			
[7mTopScreen[0m.HandleFinalize();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:392:			
ScreenManager.OnPopScreen?.Invoke([7mTopScreen[0m);[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:393:			
_screenList.Remove([7mTopScreen[0m);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:394:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:395:	[0m[7m	[0m
[7m[0m_screenList.Add(screen);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:396:	[0m[7m	[0m
[7m[0mscreen.HandleInitialize();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:397:	[0m[7m	[0m
[7m[0mscreen.HandleActivate();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:398:	[0m[7m	[0m
[7m[0mscreen.HandleResume();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:399:	[0m[7m	[0m
[7m[0m_globalOrderDirty [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:400:	[0m[7m	[0m
[7m[0mScreenManager.OnPushScreen?.Invoke(screen);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:401:	}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:414:	{
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:415:		
TaleWorlds.Library.Debug.Print("SetAndActivateRootScreen");
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:416:		if ([7mTopScreen [0m
[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:417:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:418:			throw new 
Exception("[7mTopScreen [0m[7m[0mis [0m[7m[0mnot [0m[7m[0mnull.");[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:419:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:420:	[0m[7m	[0m
[7m[0m_screenList.Add(screen);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:421:	[0m[7m	[0m
[7m[0mscreen.HandleInitialize();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:422:	[0m[7m	[0m
[7m[0mscreen.HandleActivate();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:423:	[0m[7m	[0m
[7m[0mscreen.HandleResume();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:424:	[0m[7m	[0m
[7m[0m_globalOrderDirty [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:425:	[0m[7m	[0m
[7m[0mScreenManager.OnPushScreen?.Invoke(screen);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:426:	}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:488:		if 
(_screenList.Count > 0)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:489:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:490:			
[7mTopScreen[0m.HandlePause();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:491:			if 
([7mTopScreen[0m.IsActive)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:492:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:493:				
[7mTopScreen[0m.HandleDeactivate();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:494:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:495:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:496:	[0m[7m	[0m
[7m[0m_screenList.Add(screen);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:497:	[0m[7m	[0m
[7m[0mscreen.HandleInitialize();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:498:	[0m[7m	[0m
[7m[0mscreen.HandleActivate();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:499:	[0m[7m	[0m
[7m[0mscreen.HandleResume();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:500:	[0m[7m	[0m
[7m[0m_globalOrderDirty [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:501:	[0m[7m	[0m
[7m[0mScreenManager.OnPushScreen?.Invoke(screen);[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:511:		if 
(_screenList.Count > 0)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:512:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:513:			
[7mTopScreen[0m.HandlePause();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:514:			
[7mTopScreen[0m.HandleDeactivate();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:515:			
[7mTopScreen[0m.HandleFinalize();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:516:			
TaleWorlds.Library.Debug.Print("PopScreen - " + [7mTopScreen[0m.GetType().ToString());[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:517:			
ScreenManager.OnPopScreen?.Invoke([7mTopScreen[0m);[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:518:			
_screenList.Remove([7mTopScreen[0m);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:519:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:520:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(_screenList.Count [0m[7m[0m> [0m[7m[0m0)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:521:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:522:			ScreenBase 
[7mtopScreen [0m[7m[0m= [0m[7m[0mTopScreen;[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:523:			
[7mTopScreen[0m.HandleActivate();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:524:			if 
([7mtopScreen [0m[7m[0m== [0m[7m[0mTopScreen)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:525:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:526:				
[7mTopScreen[0m.HandleResume();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:527:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:528:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:529:	[0m[7m	[0m
[7m[0m_globalOrderDirty [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:530:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:531:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:532:	[0m[7m[0mpublic [0m[7m[0mstatic [0m
[7m[0mvoid [0m[7m[0mCleanScreens()[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:533:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:534:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(!TWParallel.IsMainThread())[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:539:		while 
(_screenList.Count > 0)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:540:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:541:			
[7mTopScreen[0m.HandlePause();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:542:			
[7mTopScreen[0m.HandleDeactivate();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:543:			
[7mTopScreen[0m.HandleFinalize();[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:544:			
ScreenManager.OnPopScreen?.Invoke([7mTopScreen[0m);[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:545:			
_screenList.Remove([7mTopScreen[0m);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:546:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:547:	[0m[7m	[0m
[7m[0m_globalOrderDirty [0m[7m[0m= [0m[7m[0mtrue;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:548:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:549:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:550:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mScreenBase [0m[7m[0mFindPredecessor(ScreenBase [0m[7m[0mscreen)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:551:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:552:	[0m[7m	[0m[7m[0mScreenBase [0m
[7m[0mresult [0m[7m[0m= [0m[7m[0mnull;[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:553:	[0m[7m	[0m[7m[0mint [0m[7m[0mnum [0m[7m[0m= [0m
[7m[0m_screenList.IndexOf(screen);[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:562:	{
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:563:		
_lastPressedKeys = lastKeysPressed;
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:564:		ScreenBase 
[7mtopScreen [0m[7m[0m= [0m[7m[0mTopScreen;[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:565:		if ([7mtopScreen [0m
[7m[0m!= [0m[7m[0mnull [0m[7m[0m&& [0m[7m[0mtopScreen.IsActive)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:566:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:567:			
[7mTopScreen[0m.Update(_lastPressedKeys);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:568:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:569:	[0m[7m	[0m[7m[0mfor [0m[7m[0m(int [0m[7m[0mi [0m[7m[0m= [0m
[7m[0m0; [0m[7m[0mi [0m[7m[0m< [0m[7m[0m_globalLayers.Count; [0m[7m[0mi++)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:570:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:571:	[0m[7m	[0m[7m	[0m[7m[0mGlobalLayer [0m
[7m[0mglobalLayer [0m[7m[0m= [0m[7m[0m_globalLayers[i];[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:572:	[0m[7m	[0m[7m	[0m[7m[0mif [0m
[7m[0m(globalLayer.Layer.IsActive)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:573:			{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:574:	[0m[7m	[0m[7m	[0m[7m	[0m
[7m[0mglobalLayer.Update(_lastPressedKeys);[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:575:			}[0m
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:900:		if 
(screenList != null && screenList.Count > 0)
  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:901:		{
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:902:			if 
([7mTopScreen [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:903:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:904:				
[7mTopScreen[0m.OnAddLayer [0m[7m[0m-= [0m[7m[0mOnLayerAddedToTopLayer;[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:905:				
[7mTopScreen[0m.OnRemoveLayer [0m[7m[0m-= [0m[7m[0mOnLayerRemovedFromTopLayer;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:906:			}[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:907:			[7mTopScreen [0m[7m[0m= [0m
[7m[0m_screenList[_screenList.Count [0m[7m[0m- [0m[7m[0m1];[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:908:			if 
([7mTopScreen [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:909:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:910:				
[7mTopScreen[0m.OnAddLayer [0m[7m[0m+= [0m[7m[0mOnLayerAddedToTopLayer;[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:911:				
[7mTopScreen[0m.OnRemoveLayer [0m[7m[0m+= [0m[7m[0mOnLayerRemovedFromTopLayer;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:912:			}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:913:		}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:914:		else[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:915:		{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:916:			if 
([7mTopScreen [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:917:			{[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:918:				
[7mTopScreen[0m.OnAddLayer [0m[7m[0m-= [0m[7m[0mOnLayerAddedToTopLayer;[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:919:				
[7mTopScreen[0m.OnRemoveLayer [0m[7m[0m-= [0m[7m[0mOnLayerRemovedFromTopLayer;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:920:			}[0m
> E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:921:			[7mTopScreen [0m[7m[0m= [0m
[7m[0mnull;[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:922:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:923:	[0m[7m	[0m
[7m[0mSetSortedLayersDirty();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:924:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:925:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:926:	[0m[7m[0mprivate [0m[7m[0mstatic [0m
[7m[0mvoid [0m[7m[0mOnLayerAddedToTopLayer(ScreenLayer [0m[7m[0mlayer)[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:927:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:928:	[0m[7m	[0m
[7m[0mSetSortedLayersDirty();[0m
[7m[0m  E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenManager.cs:929:	}[0m


codex
Vanilla confirms `OnMissionTick(dt)` is called from the mission frame loop and `CurrentTime` is a cached mission clock, not a fixed-step timer. I’m narrowing the excerpts now so the final review can cite exact lines instead of broad search output.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' | ForEach-Object { if ("'$i -ge 1168 -and $i -le 1188) { '"'{0,5}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' -Pattern '_cachedMissionTime' -Context 2,4" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem' -Recurse -Filter '*InputManager*.cs' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\Input.cs' | ForEach-Object { if ("'$i -ge 184 -and $i -le 206) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' | ForEach-Object { if ("'$i -ge 3698 -and $i -le 3768) { '"'{0,5}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 537ms:
 1168: 
 1169: 	public MissionCombatType CombatType
 1170: 	{
 1171: 		get
 1172: 		{
 1173: 			return (MissionCombatType)MBAPI.IMBMission.GetCombatType(Pointer);
 1174: 		}
 1175: 		set
 1176: 		{
 1177: 			MBAPI.IMBMission.SetCombatType(Pointer, (int)value);
 1178: 		}
 1179: 	}
 1180: 
 1181: 	public MissionMode Mode => _missionMode;
 1182: 
 1183: 	public float CurrentTime => _cachedMissionTime;
 1184: 
 1185: 	public bool PauseAITick
 1186: 	{
 1187: 		get
 1188: 		{

 succeeded in 500ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\EmptyInputManager.cs
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\IInputManager.cs

 succeeded in 521ms:
 184: 	{
 185: 		return InputManager.GetKeyState(key);
 186: 	}
 187: 
 188: 	public static bool IsKeyPressed(InputKey key)
 189: 	{
 190: 		return InputManager.IsKeyPressed(key);
 191: 	}
 192: 
 193: 	public static bool IsKeyDown(InputKey key)
 194: 	{
 195: 		return InputManager.IsKeyDown(key);
 196: 	}
 197: 
 198: 	public static bool IsKeyDownImmediate(InputKey key)
 199: 	{
 200: 		return InputManager.IsKeyDownImmediate(key);
 201: 	}
 202: 
 203: 	public static bool IsKeyReleased(InputKey key)
 204: 	{
 205: 		return InputManager.IsKeyReleased(key);
 206: 	}

 succeeded in 524ms:

  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:924:	private 
MissionMode _missionMode;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:925:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:926:	private 
float [7m_cachedMissionTime[0m;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:927:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:928:	[0m[7m[0mpublic [0m
[7m[0mconst [0m[7m[0mint [0m[7m[0mMaxNavMeshId [0m[7m[0m= [0m[7m[0m1000000;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:929:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:930:	[0m[7m[0mprivate [0m
[7m[0mconst [0m[7m[0mfloat [0m[7m[0mNavigationMeshHeightLimit [0m[7m[0m= [0m[7m[0m1.5f;[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1181:	public 
MissionMode Mode => _missionMode;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1182:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1183:	public 
float CurrentTime => [7m_cachedMissionTime[0m;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1184:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1185:	[0m[7m[0mpublic [0m
[7m[0mbool [0m[7m[0mPauseAITick[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1186:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1187:		get[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1641:	internal 
void UpdateMissionTimeCache(float curTime)
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1642:	{
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1643:		
[7m_cachedMissionTime [0m[7m[0m= [0m[7m[0mcurTime;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1644:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1645:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1646:	[0m[7m[0mpublic [0m
[7m[0mfloat [0m[7m[0mGetAverageFps()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1647:	{[0m


 succeeded in 570ms:
 3698: 				b.InflictedDamage = (int)b.BaseMagnitude;
 3699: 				b.GlobalPosition = item.Position;
 3700: 				b.GlobalPosition.z += item.GetEyeGlobalHeight();
 3701: 				b.DamagedPercentage = 1f;
 3702: 				item.Die(b);
 3703: 				break;
 3704: 			}
 3705: 			}
 3706: 		}
 3707: 		_tickActions.Clear();
 3708: 		MissionTimeTracker.Tick(dt);
 3709: 		CheckMissionEnd(CurrentTime);
 3710: 		if (IsFastForward && MissionEnded)
 3711: 		{
 3712: 			IsFastForward = false;
 3713: 		}
 3714: 		if (CurrentState != State.Continuing)
 3715: 		{
 3716: 			return;
 3717: 		}
 3718: 		if (_inMissionLoadingScreenTimer != null && _inMissionLoadingScreenTimer.Check(CurrentTime))
 3719: 		{
 3720: 			_inMissionLoadingScreenTimer = null;
 3721: 			_onLoadingEndedAction?.Invoke();
 3722: 			LoadingWindow.DisableGlobalLoadingWindow();
 3723: 		}
 3724: 		for (int num = MissionBehaviors.Count - 1; num >= 0; num--)
 3725: 		{
 3726: 			MissionBehaviors[num].OnPreDisplayMissionTick(dt);
 3727: 		}
 3728: 		if (!GameNetwork.IsDedicatedServer && updateCamera)
 3729: 		{
 3730: 			_missionState.Handler.UpdateCamera(this, realDt);
 3731: 		}
 3732: 		tickCompleted = false;
 3733: 		for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
 3734: 		{
 3735: 			MissionBehaviors[num2].OnMissionTick(dt);
 3736: 		}
 3737: 		for (int num3 = _dynamicEntities.Count - 1; num3 >= 0; num3--)
 3738: 		{
 3739: 			DynamicEntityInfo dynamicEntityInfo = _dynamicEntities[num3];
 3740: 			if (dynamicEntityInfo.TimerToDisable.Check(CurrentTime))
 3741: 			{
 3742: 				dynamicEntityInfo.Entity.RemoveEnginePhysics();
 3743: 				dynamicEntityInfo.Entity.Remove(79);
 3744: 				_dynamicEntities.RemoveAt(num3);
 3745: 			}
 3746: 		}
 3747: 		HandleSpawnedItems();
 3748: 		DebugNetworkEventStatistics.EndTick(dt);
 3749: 		if (CurrentState == State.Continuing && IsFriendlyMission && !IsInPhotoMode)
 3750: 		{
 3751: 			if (InputManager.IsGameKeyDown(4))
 3752: 			{
 3753: 				OnEndMissionRequest();
 3754: 			}
 3755: 			else
 3756: 			{
 3757: 				_leaveMissionTimer = null;
 3758: 			}
 3759: 		}
 3760: 		if (doAsyncAITick)
 3761: 		{
 3762: 			TickAgentsAndTeamsAsync(dt);
 3763: 		}
 3764: 		else
 3765: 		{
 3766: 			TickAgentsAndTeamsImp(dt, tickPaused: false);
 3767: 		}
 3768: 	}

codex
Input’s managed wrapper delegates `IsKeyPressed` to `IInputManager.IsKeyPressed`; the managed decompile does not expose the native implementation. There is still useful evidence: v1.4.5 has distinct `IsKeyPressed`, `IsKeyDown`, and `IsKeyReleased` APIs, so the adapter preserves the old edge-trigger API rather than switching semantics.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\EmptyInputManager.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\IInputManager.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\**\\*.cs' -Pattern 'IsKeyPressed\\(' -ErrorAction SilentlyContinue | Select-Object -First 50" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*InputState*.cs' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 512ms:
   1: using TaleWorlds.Library;
   2: 
   3: namespace TaleWorlds.InputSystem;
   4: 
   5: public interface IInputManager
   6: {
   7: 	float GetMousePositionX();
   8: 
   9: 	float GetMousePositionY();
  10: 
  11: 	float GetMouseScrollValue();
  12: 
  13: 	Input.ControllerTypes GetControllerType();
  14: 
  15: 	bool IsMouseActive();
  16: 
  17: 	bool IsControllerConnected();
  18: 
  19: 	bool IsAnyTouchActive();
  20: 
  21: 	void PressKey(InputKey key);
  22: 
  23: 	void ClearKeys();
  24: 
  25: 	int GetVirtualKeyCode(InputKey key);
  26: 
  27: 	void SetClipboardText(string text);
  28: 
  29: 	string GetClipboardText();
  30: 
  31: 	float GetMouseMoveX();
  32: 
  33: 	float GetMouseMoveY();
  34: 
  35: 	float GetNormalizedMouseMoveX();
  36: 
  37: 	float GetNormalizedMouseMoveY();
  38: 
  39: 	float GetGyroX();
  40: 
  41: 	float GetGyroY();
  42: 
  43: 	float GetGyroZ();
  44: 
  45: 	float GetMouseSensitivity();
  46: 
  47: 	float GetMouseDeltaZ();
  48: 
  49: 	void UpdateKeyData(byte[] keyData);
  50: 
  51: 	Vec2 GetKeyState(InputKey key);
  52: 
  53: 	bool IsKeyPressed(InputKey key);
  54: 
  55: 	bool IsKeyDown(InputKey key);
  56: 
  57: 	bool IsKeyDownImmediate(InputKey key);
  58: 
  59: 	bool IsKeyReleased(InputKey key);
  60: 
  61: 	Vec2 GetResolution();
  62: 
  63: 	Vec2 GetDesktopResolution();
  64: 
  65: 	void SetCursorPosition(int x, int y);
  66: 
  67: 	void SetCursorFriction(float frictionValue);
  68: 
  69: 	InputKey[] GetClickKeys();
  70: 
  71: 	void SetRumbleEffect(float[] lowFrequencyLevels, float[] lowFrequencyDurations, int numLowFrequencyElements, float[] highFrequencyLevels, float[] highFrequencyDurations, int numHighFrequencyElements);
  72: 
  73: 	void SetTriggerFeedback(byte leftTriggerPosition, byte leftTriggerStrength, byte rightTriggerPosition, byte rightTriggerStrength);
  74: 
  75: 	void SetTriggerWeaponEffect(byte leftStartPosition, byte leftEnd_position, byte leftStrength, byte rightStartPosition, byte rightEndPosition, byte rightStrength);
  76: 
  77: 	void SetTriggerVibration(float[] leftTriggerAmplitudes, float[] leftTriggerFrequencies, float[] leftTriggerDurations, int numLeftTriggerElements, float[] rightTriggerAmplitudes, float[] rightTriggerFrequencies, float[] rightTriggerDurations, int numRightTriggerElements);
  78: 
  79: 	void SetLightbarColor(float red, float green, float blue);
  80: }

 succeeded in 519ms:
   1: using TaleWorlds.Library;
   2: 
   3: namespace TaleWorlds.InputSystem;
   4: 
   5: internal class EmptyInputManager : IInputManager
   6: {
   7: 	public void ClearKeys()
   8: 	{
   9: 	}
  10: 
  11: 	public InputKey[] GetClickKeys()
  12: 	{
  13: 		return new InputKey[0];
  14: 	}
  15: 
  16: 	public string GetClipboardText()
  17: 	{
  18: 		return string.Empty;
  19: 	}
  20: 
  21: 	public Input.ControllerTypes GetControllerType()
  22: 	{
  23: 		return Input.ControllerTypes.None;
  24: 	}
  25: 
  26: 	public Vec2 GetDesktopResolution()
  27: 	{
  28: 		return Vec2.Zero;
  29: 	}
  30: 
  31: 	public float GetGyroX()
  32: 	{
  33: 		return 0f;
  34: 	}
  35: 
  36: 	public float GetGyroY()
  37: 	{
  38: 		return 0f;
  39: 	}
  40: 
  41: 	public float GetGyroZ()
  42: 	{
  43: 		return 0f;
  44: 	}
  45: 
  46: 	public Vec2 GetKeyState(InputKey key)
  47: 	{
  48: 		return Vec2.Zero;
  49: 	}
  50: 
  51: 	public float GetMouseDeltaZ()
  52: 	{
  53: 		return 0f;
  54: 	}
  55: 
  56: 	public float GetMouseMoveX()
  57: 	{
  58: 		return 0f;
  59: 	}
  60: 
  61: 	public float GetMouseMoveY()
  62: 	{
  63: 		return 0f;
  64: 	}
  65: 
  66: 	public float GetMousePositionX()
  67: 	{
  68: 		return 0f;
  69: 	}
  70: 
  71: 	public float GetMousePositionY()
  72: 	{
  73: 		return 0f;
  74: 	}
  75: 
  76: 	public float GetMouseScrollValue()
  77: 	{
  78: 		return 0f;
  79: 	}
  80: 
  81: 	public float GetMouseSensitivity()
  82: 	{
  83: 		return 0f;
  84: 	}
  85: 
  86: 	public float GetNormalizedMouseMoveX()
  87: 	{
  88: 		return 0f;
  89: 	}
  90: 
  91: 	public float GetNormalizedMouseMoveY()
  92: 	{
  93: 		return 0f;
  94: 	}
  95: 
  96: 	public Vec2 GetResolution()
  97: 	{
  98: 		return Vec2.Zero;
  99: 	}
 100: 
 101: 	public int GetVirtualKeyCode(InputKey key)
 102: 	{
 103: 		return -1;
 104: 	}
 105: 
 106: 	public bool IsAnyTouchActive()
 107: 	{
 108: 		return false;
 109: 	}
 110: 
 111: 	public bool IsControllerConnected()
 112: 	{
 113: 		return false;
 114: 	}
 115: 
 116: 	public bool IsKeyDown(InputKey key)
 117: 	{
 118: 		return false;
 119: 	}
 120: 
 121: 	public bool IsKeyDownImmediate(InputKey key)
 122: 	{
 123: 		return false;
 124: 	}
 125: 
 126: 	public bool IsKeyPressed(InputKey key)
 127: 	{
 128: 		return false;
 129: 	}
 130: 
 131: 	public bool IsKeyReleased(InputKey key)
 132: 	{
 133: 		return false;
 134: 	}
 135: 
 136: 	public bool IsMouseActive()
 137: 	{
 138: 		return false;
 139: 	}
 140: 
 141: 	public void PressKey(InputKey key)
 142: 	{
 143: 	}
 144: 
 145: 	public void SetClipboardText(string text)
 146: 	{
 147: 	}
 148: 
 149: 	public void SetCursorFriction(float frictionValue)
 150: 	{
 151: 	}
 152: 
 153: 	public void SetCursorPosition(int x, int y)
 154: 	{
 155: 	}
 156: 
 157: 	public void SetLightbarColor(float red, float green, float blue)
 158: 	{
 159: 	}
 160: 
 161: 	public void SetRumbleEffect(float[] lowFrequencyLevels, float[] lowFrequencyDurations, int numLowFrequencyElements, float[] highFrequencyLevels, float[] highFrequencyDurations, int numHighFrequencyElements)
 162: 	{
 163: 	}
 164: 
 165: 	public void SetTriggerFeedback(byte leftTriggerPosition, byte leftTriggerStrength, byte rightTriggerPosition, byte rightTriggerStrength)
 166: 	{
 167: 	}
 168: 
 169: 	public void SetTriggerVibration(float[] leftTriggerAmplitudes, float[] leftTriggerFrequencies, float[] leftTriggerDurations, int numLeftTriggerElements, float[] rightTriggerAmplitudes, float[] rightTriggerFrequencies, float[] rightTriggerDurations, int numRightTriggerElements)
 170: 	{
 171: 	}
 172: 
 173: 	public void SetTriggerWeaponEffect(byte leftStartPosition, byte leftEnd_position, byte leftStrength, byte rightStartPosition, byte rightEndPosition, byte rightStrength)
 174: 	{
 175: 	}
 176: 
 177: 	public void UpdateKeyData(byte[] keyData)
 178: 	{
 179: 	}
 180: }

 succeeded in 485ms:
 succeeded in 515ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputState.cs

codex
The installed managed input code only reaches the `IInputManager` boundary; beyond that is engine/native. That means I can confirm the refactor keeps the same `Input.IsKeyPressed(InputKey.V)` call the legacy behavior used, but I cannot honestly prove edge semantics from managed decompile alone.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\InputState.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'Input\\.IsKeyPressed|\\.IsPressed\\(|IsKeyPressed\\(' | Select-Object -First 80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 476ms:
   1: using TaleWorlds.Library;
   2: 
   3: namespace TaleWorlds.InputSystem;
   4: 
   5: public class InputState
   6: {
   7: 	private Vec2 _mousePositionRanged;
   8: 
   9: 	private Vec2 _mousePositionRangedDevice;
  10: 
  11: 	private Vec2 _mousePositionPixel;
  12: 
  13: 	private Vec2 _mousePositionPixelDevice;
  14: 
  15: 	public Vec2 NativeResolution => Input.Resolution;
  16: 
  17: 	public Vec2 MousePositionRanged
  18: 	{
  19: 		get
  20: 		{
  21: 			return _mousePositionRanged;
  22: 		}
  23: 		set
  24: 		{
  25: 			_mousePositionRanged = value;
  26: 			_mousePositionPixel = new Vec2(_mousePositionRanged.x * NativeResolution.x, _mousePositionRanged.y * NativeResolution.y);
  27: 		}
  28: 	}
  29: 
  30: 	public Vec2 OldMousePositionRanged { get; private set; }
  31: 
  32: 	public bool MousePositionChanged { get; private set; }
  33: 
  34: 	public Vec2 MousePositionPixel
  35: 	{
  36: 		get
  37: 		{
  38: 			return _mousePositionPixel;
  39: 		}
  40: 		set
  41: 		{
  42: 			_mousePositionPixel = value;
  43: 			_mousePositionRanged = new Vec2(_mousePositionPixel.x / Input.Resolution.x, _mousePositionPixel.y / NativeResolution.y);
  44: 		}
  45: 	}
  46: 
  47: 	public Vec2 OldMousePositionPixel { get; private set; }
  48: 
  49: 	public float MouseScrollValue { get; private set; }
  50: 
  51: 	public bool MouseScrollChanged { get; private set; }
  52: 
  53: 	public InputState()
  54: 	{
  55: 		MousePositionRanged = default(Vec2);
  56: 		OldMousePositionRanged = default(Vec2);
  57: 		MousePositionPixel = default(Vec2);
  58: 		OldMousePositionPixel = default(Vec2);
  59: 		_mousePositionRanged = new Vec2(0f, 0f);
  60: 		_mousePositionPixel = new Vec2(0f, 0f);
  61: 		_mousePositionPixelDevice = new Vec2(0f, 0f);
  62: 		_mousePositionRangedDevice = new Vec2(0f, 0f);
  63: 	}
  64: 
  65: 	public bool UpdateMousePosition(float mousePositionX, float mousePositionY)
  66: 	{
  67: 		OldMousePositionRanged = new Vec2(_mousePositionRangedDevice.x, _mousePositionRangedDevice.y);
  68: 		_mousePositionRangedDevice = new Vec2(mousePositionX, mousePositionY);
  69: 		OldMousePositionPixel = new Vec2(_mousePositionPixelDevice.x, _mousePositionPixelDevice.y);
  70: 		_mousePositionPixelDevice = new Vec2(_mousePositionRangedDevice.x * NativeResolution.x, _mousePositionRangedDevice.y * NativeResolution.y);
  71: 		if (_mousePositionRangedDevice.x == OldMousePositionRanged.x && _mousePositionRangedDevice.y == OldMousePositionRanged.y)
  72: 		{
  73: 			MousePositionChanged = false;
  74: 		}
  75: 		else
  76: 		{
  77: 			MousePositionChanged = true;
  78: 			MousePositionPixel = _mousePositionPixelDevice;
  79: 			MousePositionRanged = _mousePositionRangedDevice;
  80: 		}
  81: 		return MousePositionChanged;
  82: 	}
  83: 
  84: 	public bool UpdateMouseScroll(float mouseScrollValue)
  85: 	{
  86: 		if (!MouseScrollValue.Equals(mouseScrollValue))
  87: 		{
  88: 			MouseScrollValue = mouseScrollValue;
  89: 			MouseScrollChanged = true;
  90: 		}
  91: 		else
  92: 		{
  93: 			MouseScrollChanged = false;
  94: 		}
  95: 		return MouseScrollChanged;
  96: 	}
  97: }

2026-06-02T21:34:42.217964Z ERROR codex_core::tools::router: error=Exit code: 124
Wall time: 30.1 seconds
Output:
command timed out after 30124 milliseconds

E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine\IInput.cs:91:	bool [7mIsKeyPressed([0mInputKey key);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine.InputSystem\EngineInputManager.cs:114:	bool 
IInputManager.[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine.InputSystem\EngineInputManager.cs:116:		return 
EngineApplicationInterface.I[7mInput.IsKeyPressed[0m(key);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.AutoGenerated\ManagedCallbacks\ScriptingInterfaceObjects.cs:3274:			
ScriptingInterfaceOfIInput.call_IsKeyPressedDelegate = 
(ScriptingInterfaceOfI[7mInput.IsKeyPressed[0mDelegate)Marshal.GetDelegateForFunctionPointer(pointer, [0m
[7m[0mtypeof(ScriptingInterfaceOfIInput.IsKeyPressedDelegate));[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.AutoGenerated\ManagedCallbacks\ScriptingInterfaceOfIInput.cs:346:	
public bool [7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\EmptyInputContext.cs:78:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\EmptyInputManager.cs:126:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\GameKey.cs:101:			flag = flag || 
KeyboardKey[7m.IsPressed([0m);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\GameKey.cs:105:			flag = flag || 
ControllerKey[7m.IsPressed([0m);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\HotKey.cs:182:		if (key[7m.IsPressed([0m))
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\HotKey.cs:216:		return key[7m.IsPressed([0m);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\IInputContext.cs:34:	bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\IInputManager.cs:53:	bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:188:	public static bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:190:		return 
InputManager.[7mIsKeyPressed([0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:227:			if 
([7mIsKeyPressed([0m(InputKey)i))[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:286:		return [7mIsKeyPressed([0mkey);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:149:			if 
(key[7m.IsPressed([0m) [0m[7m[0m&& [0m[7m[0m!_downInputKeys.Contains(key))[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:212:		return 
hotKey[7m.IsPressed([0mIsKeysAllowed, [0m[7m[0mIsMouseButtonAllowed [0m[7m[0m&& [0m[7m[0mMouseOnMe, [0m[7m[0mIsMouseWheelAllowed, [0m[7m[0mIsControllerAllowed);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:226:		return 
gameKey[7m.IsPressed([0mIsKeysAllowed, [0m[7m[0mIsMouseButtonAllowed [0m[7m[0m&& [0m[7m[0mMouseOnMe, [0m[7m[0mIsMouseWheelAllowed, [0m[7m[0mIsControllerAllowed);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:498:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:502:			return 
[7mInput.IsKeyPressed[0m(key);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Key.cs:48:		return 
[7mInput.IsKeyPressed[0m(InputKey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension.Standalone\TaleWorlds.TwoDimension.Standalone\StandaloneInputMa
nager.cs:127:	bool IInputManager.[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets\EncyclopediaTroopScrollablePanel.cs:39:				if ([7mInput.IsKeyPressed[0m(InputKey.LeftMouseButton))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Chat\ChatLogWidget.cs:312:		if ([7mInput.IsKeyPressed[0m(InputKey.LeftMouseButton) [0m[7m[0m&& [0m
[7m[0mbase.EventManager.HoveredWidget [0m[7m[0m== [0m[7m[0mResizerWidget)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Multiplayer.Lobby\MultiplayerLobbyBadgeButtonWidget.cs:17:		if (base.EventManager.HoveredWidget == this && 
[7mInput.IsKeyPressed[0m(InputKey.ControllerRUp))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Multiplayer.Lobby.Armory\MultiplayerLobbyArmoryCosmeticItemButtonWidget.cs:133:		if 
(base.EventManager.HoveredWidget == this && [7mInput.IsKeyPressed[0m(InputKey.ControllerRUp))[0m

 exited 124 in 30124ms:

E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine\IInput.cs:91:	bool [7mIsKeyPressed([0mInputKey key);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine.InputSystem\EngineInputManager.cs:114:	bool 
IInputManager.[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine\TaleWorlds.Engine.InputSystem\EngineInputManager.cs:116:		return 
EngineApplicationInterface.I[7mInput.IsKeyPressed[0m(key);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.AutoGenerated\ManagedCallbacks\ScriptingInterfaceObjects.cs:3274:			
ScriptingInterfaceOfIInput.call_IsKeyPressedDelegate = 
(ScriptingInterfaceOfI[7mInput.IsKeyPressed[0mDelegate)Marshal.GetDelegateForFunctionPointer(pointer, [0m
[7m[0mtypeof(ScriptingInterfaceOfIInput.IsKeyPressedDelegate));[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.AutoGenerated\ManagedCallbacks\ScriptingInterfaceOfIInput.cs:346:	
public bool [7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\EmptyInputContext.cs:78:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\EmptyInputManager.cs:126:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\GameKey.cs:101:			flag = flag || 
KeyboardKey[7m.IsPressed([0m);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\GameKey.cs:105:			flag = flag || 
ControllerKey[7m.IsPressed([0m);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\HotKey.cs:182:		if (key[7m.IsPressed([0m))
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\HotKey.cs:216:		return key[7m.IsPressed([0m);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\IInputContext.cs:34:	bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\IInputManager.cs:53:	bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:188:	public static bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:190:		return 
InputManager.[7mIsKeyPressed([0mkey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:227:			if 
([7mIsKeyPressed([0m(InputKey)i))[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Input.cs:286:		return [7mIsKeyPressed([0mkey);
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:149:			if 
(key[7m.IsPressed([0m) [0m[7m[0m&& [0m[7m[0m!_downInputKeys.Contains(key))[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:212:		return 
hotKey[7m.IsPressed([0mIsKeysAllowed, [0m[7m[0mIsMouseButtonAllowed [0m[7m[0m&& [0m[7m[0mMouseOnMe, [0m[7m[0mIsMouseWheelAllowed, [0m[7m[0mIsControllerAllowed);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:226:		return 
gameKey[7m.IsPressed([0mIsKeysAllowed, [0m[7m[0mIsMouseButtonAllowed [0m[7m[0m&& [0m[7m[0mMouseOnMe, [0m[7m[0mIsMouseWheelAllowed, [0m[7m[0mIsControllerAllowed);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:498:	public bool 
[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\InputContext.cs:502:			return 
[7mInput.IsKeyPressed[0m(key);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.InputSystem\TaleWorlds.InputSystem\Key.cs:48:		return 
[7mInput.IsKeyPressed[0m(InputKey);[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.TwoDimension.Standalone\TaleWorlds.TwoDimension.Standalone\StandaloneInputMa
nager.cs:127:	bool IInputManager.[7mIsKeyPressed([0mInputKey [0m[7m[0mkey)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets\EncyclopediaTroopScrollablePanel.cs:39:				if ([7mInput.IsKeyPressed[0m(InputKey.LeftMouseButton))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Chat\ChatLogWidget.cs:312:		if ([7mInput.IsKeyPressed[0m(InputKey.LeftMouseButton) [0m[7m[0m&& [0m
[7m[0mbase.EventManager.HoveredWidget [0m[7m[0m== [0m[7m[0mResizerWidget)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Multiplayer.Lobby\MultiplayerLobbyBadgeButtonWidget.cs:17:		if (base.EventManager.HoveredWidget == this && 
[7mInput.IsKeyPressed[0m(InputKey.ControllerRUp))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.GauntletUI.Widgets\TaleWorlds.MountAndBlade.GauntletUI.
Widgets.Multiplayer.Lobby.Armory\MultiplayerLobbyArmoryCosmeticItemButtonWidget.cs:133:		if 
(base.EventManager.HoveredWidget == this && [7mInput.IsKeyPressed[0m(InputKey.ControllerRUp))[0m

codex
There is a deeper managed hop through `EngineInputManager` to `EngineApplicationInterface.IInput.IsKeyPressed`, so the final edge/held distinction is engine-facing. I also found the standalone input manager; I’ll use it as supporting evidence only, not as a definitive Bannerlord runtime proof.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.Engine.AutoGenerated\\ManagedCallbacks\\ScriptingInterfaceOfIInput.cs' | ForEach-Object { if ("'$i -ge 336 -and $i -le 356) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.Engine\\TaleWorlds.Engine.InputSystem\\EngineInputManager.cs' | ForEach-Object { if ("'$i -ge 100 -and $i -le 124) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\Key.cs' | ForEach-Object { if ("'$i -ge 38 -and $i -le 58) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.InputSystem\\TaleWorlds.InputSystem\\InputContext.cs' | ForEach-Object { if ("'$i -ge 140 -and $i -le 154) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.TwoDimension.Standalone\\TaleWorlds.TwoDimension.Standalone\\StandaloneInputManager.cs' | ForEach-Object { if ("'$i -ge 112 -and $i -le 140) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 520ms:
 100: 	{
 101: 		return EngineApplicationInterface.IInput.GetMouseDeltaZ();
 102: 	}
 103: 
 104: 	void IInputManager.UpdateKeyData(byte[] keyData)
 105: 	{
 106: 		EngineApplicationInterface.IInput.UpdateKeyData(keyData);
 107: 	}
 108: 
 109: 	Vec2 IInputManager.GetKeyState(InputKey key)
 110: 	{
 111: 		return EngineApplicationInterface.IInput.GetKeyState(key);
 112: 	}
 113: 
 114: 	bool IInputManager.IsKeyPressed(InputKey key)
 115: 	{
 116: 		return EngineApplicationInterface.IInput.IsKeyPressed(key);
 117: 	}
 118: 
 119: 	bool IInputManager.IsKeyDown(InputKey key)
 120: 	{
 121: 		return EngineApplicationInterface.IInput.IsKeyDown(key);
 122: 	}
 123: 
 124: 	bool IInputManager.IsKeyDownImmediate(InputKey key)

 succeeded in 520ms:
 336: 	public bool IsKeyDown(InputKey key)
 337: 	{
 338: 		return call_IsKeyDownDelegate(key);
 339: 	}
 340: 
 341: 	public bool IsKeyDownImmediate(InputKey key)
 342: 	{
 343: 		return call_IsKeyDownImmediateDelegate(key);
 344: 	}
 345: 
 346: 	public bool IsKeyPressed(InputKey key)
 347: 	{
 348: 		return call_IsKeyPressedDelegate(key);
 349: 	}
 350: 
 351: 	public bool IsKeyReleased(InputKey key)
 352: 	{
 353: 		return call_IsKeyReleasedDelegate(key);
 354: 	}
 355: 
 356: 	public bool IsMouseActive()

 succeeded in 516ms:
  38: 	{
  39: 		InputKey = key;
  40: 		IsKeyboardInput = GetInputType(key) == InputType.Keyboard;
  41: 		IsMouseButtonInput = GetInputType(key) == InputType.MouseButton;
  42: 		IsMouseWheelInput = GetInputType(key) == InputType.MouseWheel;
  43: 		IsControllerInput = GetInputType(key) == InputType.Controller;
  44: 	}
  45: 
  46: 	internal bool IsPressed()
  47: 	{
  48: 		return Input.IsKeyPressed(InputKey);
  49: 	}
  50: 
  51: 	internal bool IsDown()
  52: 	{
  53: 		return Input.IsKeyDown(InputKey);
  54: 	}
  55: 
  56: 	internal bool IsDownImmediate()
  57: 	{
  58: 		if (IsKeyboardInput || IsMouseButtonInput)

 succeeded in 528ms:
 140: 		return _allKeysListMemoryCache;
 141: 	}
 142: 
 143: 	public void RegisterDownKeys()
 144: 	{
 145: 		List<Key> allAvailableKeys = GetAllAvailableKeys();
 146: 		for (int i = 0; i < allAvailableKeys.Count; i++)
 147: 		{
 148: 			Key key = allAvailableKeys[i];
 149: 			if (key.IsPressed() && !_downInputKeys.Contains(key))
 150: 			{
 151: 				_downInputKeys.Add(key);
 152: 			}
 153: 		}
 154: 	}

 succeeded in 558ms:
 112: 	}
 113: 
 114: 	void IInputManager.UpdateKeyData(byte[] keyData)
 115: 	{
 116: 	}
 117: 
 118: 	Vec2 IInputManager.GetKeyState(InputKey key)
 119: 	{
 120: 		if (!_graphicsForm.GetKey(key))
 121: 		{
 122: 			return new Vec2(0f, 0f);
 123: 		}
 124: 		return new Vec2(1f, 0f);
 125: 	}
 126: 
 127: 	bool IInputManager.IsKeyPressed(InputKey key)
 128: 	{
 129: 		return _graphicsForm.GetKeyDown(key);
 130: 	}
 131: 
 132: 	bool IInputManager.IsKeyDown(InputKey key)
 133: 	{
 134: 		return _graphicsForm.GetKey(key);
 135: 	}
 136: 
 137: 	bool IInputManager.IsKeyDownImmediate(InputKey key)
 138: 	{
 139: 		return _graphicsForm.GetKey(key);
 140: 	}

codex
For `Input.IsKeyPressed`, the best local evidence is consistent with edge-trigger semantics: standalone maps it to `GetKeyDown`, while `IsKeyDown` maps to held state. Bannerlord’s runtime path is native, but the refactor did not alter that API call from legacy.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*GauntletMovie*.cs' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.Engine.GauntletUI\\TaleWorlds.Engine.GauntletUI\\GauntletLayer.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 160) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem' -Recurse -Filter 'ScreenBase.cs' -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'E:\\Decompiled_Bannerlord\\**\\*.cs' -Pattern 'ReleaseMovie\\(|LoadMovie\\(' -ErrorAction SilentlyContinue | Select-Object -First 80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 582ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\GauntletMovieIdentifier.cs
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.Data\TaleWorlds.GauntletUI.Data\GauntletMovie.cs
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.Data\TaleWorlds.GauntletUI.Data\GeneratedGauntletMovie.cs
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.Data\TaleWorlds.GauntletUI.Data\IGauntletMovie.cs
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI.Data\TaleWorlds.GauntletUI.Data\IGeneratedGauntletMovieRoot.cs

 succeeded in 581ms:
 succeeded in 610ms:
E:\Decompiled_Bannerlord\Engine\TaleWorlds.ScreenSystem\TaleWorlds.ScreenSystem\ScreenBase.cs

 succeeded in 880ms:
   1: using System.Collections.Generic;
   2: using System.Linq;
   3: using System.Numerics;
   4: using TaleWorlds.GauntletUI;
   5: using TaleWorlds.GauntletUI.BaseTypes;
   6: using TaleWorlds.GauntletUI.Data;
   7: using TaleWorlds.Library;
   8: using TaleWorlds.ScreenSystem;
   9: using TaleWorlds.TwoDimension;
  10: 
  11: namespace TaleWorlds.Engine.GauntletUI;
  12: 
  13: public class GauntletLayer : ScreenLayer
  14: {
  15: 	private readonly MBList<GauntletMovieIdentifier> _movieIdentifiers;
  16: 
  17: 	private readonly TwoDimensionContext _twoDimensionContext;
  18: 
  19: 	public readonly TwoDimensionView TwoDimensionView;
  20: 
  21: 	public readonly ITwoDimensionPlatform TwoDimensionPlatform;
  22: 
  23: 	public IGamepadNavigationContext GamepadNavigationContext { get; private set; }
  24: 
  25: 	public UIContext UIContext { get; private set; }
  26: 
  27: 	private void InitializeContext()
  28: 	{
  29: 		UIContext = new UIContext(_twoDimensionContext, base.Input, UIResourceManager.SpriteData, UIResourceManager.FontFactory, UIResourceManager.BrushFactory);
  30: 		UIContext.ScaleModifier = base.Scale;
  31: 		UIContext.Initialize();
  32: 		GamepadNavigationContext = new GauntletGamepadNavigationContext(GetIsBlockedAtPosition, GetLastScreenOrder, GetIsAvailableForGamepadNavigation);
  33: 		UIContext.InitializeGamepadNavigation(GamepadNavigationContext);
  34: 		UIContext.EventManager.OnFocusedWidgetChanged += EventManagerOnFocusedWidgetChanged;
  35: 		UIContext.EventManager.OnGetIsHitThisFrame = GetIsHitThisFrame;
  36: 		UIContext.EventManager.UsableArea = base.UsableArea;
  37: 		RefreshContextName();
  38: 	}
  39: 
  40: 	private void RefreshContextName()
  41: 	{
  42: 		if (UIContext != null)
  43: 		{
  44: 			UIContext.Name = base.Name;
  45: 		}
  46: 	}
  47: 
  48: 	private void ClearContext()
  49: 	{
  50: 		foreach (GauntletMovieIdentifier movieIdentifier in _movieIdentifiers)
  51: 		{
  52: 			movieIdentifier.Movie.Release();
  53: 		}
  54: 		UIContext.EventManager.OnGetIsHitThisFrame = null;
  55: 		UIContext.EventManager.OnFocusedWidgetChanged -= EventManagerOnFocusedWidgetChanged;
  56: 		UIContext.OnFinalize();
  57: 		UIContext = null;
  58: 	}
  59: 
  60: 	public void OnResourceRefreshBegin(out List<GauntletMovieIdentifier> previouslyLoadedMovies)
  61: 	{
  62: 		previouslyLoadedMovies = _movieIdentifiers.ToList();
  63: 		for (int i = 0; i < _movieIdentifiers.Count; i++)
  64: 		{
  65: 			ReleaseMovie(_movieIdentifiers[i]);
  66: 		}
  67: 		ClearContext();
  68: 	}
  69: 
  70: 	public void OnResourceRefreshEnd(List<GauntletMovieIdentifier> previouslyLoadedMovies)
  71: 	{
  72: 		InitializeContext();
  73: 		for (int i = 0; i < previouslyLoadedMovies.Count; i++)
  74: 		{
  75: 			LoadMovie(previouslyLoadedMovies[i]);
  76: 		}
  77: 	}
  78: 
  79: 	public GauntletLayer(string name, int localOrder, bool shouldClear = false)
  80: 		: base(name, localOrder)
  81: 	{
  82: 		_movieIdentifiers = new MBList<GauntletMovieIdentifier>();
  83: 		ResourceDepot resourceDepot = UIResourceManager.ResourceDepot;
  84: 		TwoDimensionView = TwoDimensionView.CreateTwoDimension(name);
  85: 		if (shouldClear)
  86: 		{
  87: 			TwoDimensionView.SetClearColor(255u);
  88: 			TwoDimensionView.SetRenderOption(View.ViewRenderOptions.ClearColor, value: true);
  89: 		}
  90: 		TwoDimensionPlatform = new TwoDimensionEnginePlatform(TwoDimensionView);
  91: 		_twoDimensionContext = new TwoDimensionContext(TwoDimensionPlatform, UIResourceManager.ResourceContext, resourceDepot);
  92: 		InitializeContext();
  93: 	}
  94: 
  95: 	private void EventManagerOnFocusedWidgetChanged()
  96: 	{
  97: 		if (UIContext.EventManager.FocusedWidget != null)
  98: 		{
  99: 			ScreenManager.TrySetFocus(this);
 100: 		}
 101: 		else if (!base.IsFocusLayer)
 102: 		{
 103: 			ScreenManager.TryLoseFocus(this);
 104: 		}
 105: 	}
 106: 
 107: 	public GauntletMovieIdentifier GetMovieIdentifier(string movieName)
 108: 	{
 109: 		for (int i = 0; i < _movieIdentifiers.Count; i++)
 110: 		{
 111: 			if (_movieIdentifiers[i].MovieName == movieName)
 112: 			{
 113: 				return _movieIdentifiers[i];
 114: 			}
 115: 		}
 116: 		return null;
 117: 	}
 118: 
 119: 	public GauntletMovieIdentifier LoadMovie(string movieName, ViewModel dataSource)
 120: 	{
 121: 		GauntletMovieIdentifier gauntletMovieIdentifier = new GauntletMovieIdentifier(movieName, dataSource);
 122: 		LoadMovie(gauntletMovieIdentifier);
 123: 		return gauntletMovieIdentifier;
 124: 	}
 125: 
 126: 	private void LoadMovie(GauntletMovieIdentifier identifier)
 127: 	{
 128: 		identifier.Movie = LoadMovieAux(identifier.MovieName, identifier.DataSource);
 129: 		_movieIdentifiers.Add(identifier);
 130: 		RefreshContextName();
 131: 	}
 132: 
 133: 	private IGauntletMovie LoadMovieAux(string movieName, ViewModel dataSource)
 134: 	{
 135: 		bool isUsingGeneratedPrefabs = UIConfig.GetIsUsingGeneratedPrefabs();
 136: 		bool isHotReloadEnabled = UIConfig.GetIsHotReloadEnabled();
 137: 		return GauntletMovie.Load(UIContext, UIResourceManager.WidgetFactory, movieName, dataSource, !isUsingGeneratedPrefabs, isHotReloadEnabled);
 138: 	}
 139: 
 140: 	public void ReleaseMovie(GauntletMovieIdentifier identifier)
 141: 	{
 142: 		if (_movieIdentifiers.Contains(identifier))
 143: 		{
 144: 			if (!identifier.Movie.IsReleased)
 145: 			{
 146: 				identifier.Movie.Release();
 147: 			}
 148: 			_movieIdentifiers.Remove(identifier);
 149: 			RefreshContextName();
 150: 		}
 151: 		else
 152: 		{
 153: 			Debug.FailedAssert("Failed to release movie from gauntlet layer: " + identifier.MovieName, "C:\\BuildAgent\\work\\mb3\\Source\\Engine\\TaleWorlds.Engine.GauntletUI\\GauntletLayer.cs", "ReleaseMovie", 208);
 154: 		}
 155: 	}
 156: 
 157: 	protected override void OnActivate()
 158: 	{
 159: 		base.OnActivate();
 160: 		TwoDimensionView.SetEnable(value: true);

codex
GauntletLayer release mechanics are visible in managed source: `LoadMovie` adds an identifier, `ReleaseMovie` releases and removes it, and layer finalization releases remaining movies. I’m checking `ScreenBase.RemoveLayer` and layer finalization to validate the teardown order specifically.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenBase.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 180) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.Engine.GauntletUI\\TaleWorlds.Engine.GauntletUI\\GauntletLayer.cs' | ForEach-Object { if ("'$i -ge 157 -and $i -le 230) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenLayer.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 220) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\UI\\TaleWorlds.GauntletUI.Data\\TaleWorlds.GauntletUI.Data\\GauntletMovie.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 140) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 556ms:
   1: using System.Collections.Generic;
   2: using TaleWorlds.InputSystem;
   3: using TaleWorlds.Library;
   4: 
   5: namespace TaleWorlds.ScreenSystem;
   6: 
   7: public abstract class ScreenBase
   8: {
   9: 	public delegate void OnLayerAddedEvent(ScreenLayer addedLayer);
  10: 
  11: 	public delegate void OnLayerRemovedEvent(ScreenLayer removedLayer);
  12: 
  13: 	private readonly List<ScreenComponent> _components;
  14: 
  15: 	private readonly MBList<ScreenLayer> _layers;
  16: 
  17: 	public IInputContext DebugInput => Input.DebugInput;
  18: 
  19: 	public MBReadOnlyList<ScreenLayer> Layers => _layers;
  20: 
  21: 	public bool IsActive { get; private set; }
  22: 
  23: 	public bool IsPaused { get; private set; }
  24: 
  25: 	public bool IsInitialized { get; private set; }
  26: 
  27: 	public bool IsFinalized { get; private set; }
  28: 
  29: 	public virtual bool MouseVisible { get; set; }
  30: 
  31: 	public event OnLayerAddedEvent OnAddLayer;
  32: 
  33: 	public event OnLayerRemovedEvent OnRemoveLayer;
  34: 
  35: 	internal void HandleInitialize()
  36: 	{
  37: 		Debug.Print(string.Concat(this, "::HandleInitialize"));
  38: 		if (!IsInitialized)
  39: 		{
  40: 			IsInitialized = true;
  41: 			OnInitialize();
  42: 			Debug.ReportMemoryBookmark("ScreenBase Initialized: " + GetType().Name);
  43: 		}
  44: 	}
  45: 
  46: 	internal void HandleFinalize()
  47: 	{
  48: 		if (IsFinalized)
  49: 		{
  50: 			Debug.FailedAssert("Screen is already finalized", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.ScreenSystem\\ScreenBase.cs", "HandleFinalize", 64);
  51: 			return;
  52: 		}
  53: 		Debug.Print(string.Concat(this, "::HandleFinalize"));
  54: 		if (IsInitialized)
  55: 		{
  56: 			IsInitialized = false;
  57: 			OnFinalize();
  58: 			for (int num = _layers.Count - 1; num >= 0; num--)
  59: 			{
  60: 				_layers[num].HandleFinalize();
  61: 			}
  62: 		}
  63: 		IsActive = false;
  64: 		this.OnAddLayer = null;
  65: 		this.OnRemoveLayer = null;
  66: 		IsFinalized = true;
  67: 	}
  68: 
  69: 	internal void HandleActivate()
  70: 	{
  71: 		Debug.Print(string.Concat(this, "::HandleActivate"));
  72: 		if (IsActive)
  73: 		{
  74: 			return;
  75: 		}
  76: 		IsActive = true;
  77: 		for (int num = _layers.Count - 1; num >= 0; num--)
  78: 		{
  79: 			ScreenLayer screenLayer = _layers[num];
  80: 			if (!screenLayer.IsActive)
  81: 			{
  82: 				screenLayer.HandleActivate();
  83: 			}
  84: 		}
  85: 		OnActivate();
  86: 	}
  87: 
  88: 	internal void HandleDeactivate()
  89: 	{
  90: 		Debug.Print(string.Concat(this, "::HandleDeactivate"));
  91: 		if (!IsActive)
  92: 		{
  93: 			return;
  94: 		}
  95: 		IsActive = false;
  96: 		for (int num = _layers.Count - 1; num >= 0; num--)
  97: 		{
  98: 			ScreenLayer screenLayer = _layers[num];
  99: 			if (screenLayer.IsActive)
 100: 			{
 101: 				screenLayer.HandleDeactivate();
 102: 			}
 103: 		}
 104: 		OnDeactivate();
 105: 	}
 106: 
 107: 	internal void HandleResume()
 108: 	{
 109: 		Debug.Print(string.Concat(this, "::HandleResume"));
 110: 		if (!IsPaused)
 111: 		{
 112: 			return;
 113: 		}
 114: 		for (int num = _layers.Count - 1; num >= 0; num--)
 115: 		{
 116: 			ScreenLayer screenLayer = _layers[num];
 117: 			if (!screenLayer.IsActive)
 118: 			{
 119: 				screenLayer.HandleActivate();
 120: 			}
 121: 		}
 122: 		IsPaused = false;
 123: 		OnResume();
 124: 	}
 125: 
 126: 	internal void HandlePause()
 127: 	{
 128: 		Debug.Print(string.Concat(this, "::HandlePause"));
 129: 		if (IsPaused)
 130: 		{
 131: 			return;
 132: 		}
 133: 		for (int num = _layers.Count - 1; num >= 0; num--)
 134: 		{
 135: 			ScreenLayer screenLayer = _layers[num];
 136: 			if (screenLayer.IsActive)
 137: 			{
 138: 				screenLayer.HandleDeactivate();
 139: 			}
 140: 		}
 141: 		IsPaused = true;
 142: 		OnPause();
 143: 	}
 144: 
 145: 	internal void FrameTick(float dt)
 146: 	{
 147: 		if (IsActive)
 148: 		{
 149: 			OnFrameTick(dt);
 150: 		}
 151: 		if (DebugInput is InputContext inputContext)
 152: 		{
 153: 			if (IsActive)
 154: 			{
 155: 				inputContext.RegisterDownKeys();
 156: 			}
 157: 			else
 158: 			{
 159: 				inputContext.ResetLastDownKeys();
 160: 			}
 161: 		}
 162: 	}
 163: 
 164: 	internal void PostFrameTick(float dt)
 165: 	{
 166: 		if (IsActive)
 167: 		{
 168: 			OnPostFrameTick(dt);
 169: 		}
 170: 	}
 171: 
 172: 	public void ActivateAllLayers()
 173: 	{
 174: 		foreach (ScreenLayer layer in _layers)
 175: 		{
 176: 			if (!layer.IsActive)
 177: 			{
 178: 				layer.HandleActivate();
 179: 			}
 180: 		}

 succeeded in 567ms:
 157: 	protected override void OnActivate()
 158: 	{
 159: 		base.OnActivate();
 160: 		TwoDimensionView.SetEnable(value: true);
 161: 		UIContext.Activate();
 162: 	}
 163: 
 164: 	protected override void OnDeactivate()
 165: 	{
 166: 		TwoDimensionPlatform.Clear();
 167: 		TwoDimensionView.Clear();
 168: 		TwoDimensionView.SetEnable(value: false);
 169: 		UIContext.Deactivate();
 170: 		base.OnDeactivate();
 171: 	}
 172: 
 173: 	protected override void Tick(float dt)
 174: 	{
 175: 		base.Tick(dt);
 176: 		UIContext.Update(dt);
 177: 		foreach (GauntletMovieIdentifier movieIdentifier in _movieIdentifiers)
 178: 		{
 179: 			movieIdentifier.Movie.Update();
 180: 		}
 181: 	}
 182: 
 183: 	protected override void LateUpdate(float dt)
 184: 	{
 185: 		base.LateUpdate(dt);
 186: 		UIContext.SetIsMouseEnabled(base.IsHitThisFrame);
 187: 		UIContext.LateUpdate(dt);
 188: 		base.ActiveCursor = (CursorType)UIContext.ActiveCursorOfContext;
 189: 	}
 190: 
 191: 	protected override void RenderTick(float dt)
 192: 	{
 193: 		base.RenderTick(dt);
 194: 		TwoDimensionView.BeginFrame();
 195: 		TwoDimensionPlatform.OnFrameBegin();
 196: 		UIContext.RenderTick(dt);
 197: 		TwoDimensionView.EndFrame();
 198: 		TwoDimensionPlatform.OnFrameEnd();
 199: 	}
 200: 
 201: 	protected override void Update(IReadOnlyList<int> lastKeysPressed)
 202: 	{
 203: 		UIContext.EventManager.FocusedWidget?.HandleInput(lastKeysPressed);
 204: 	}
 205: 
 206: 	protected override void OnFinalize()
 207: 	{
 208: 		ClearContext();
 209: 		for (int i = 0; i < _movieIdentifiers.Count; i++)
 210: 		{
 211: 			if (_movieIdentifiers[i].Movie.IsLoaded)
 212: 			{
 213: 				Debug.FailedAssert("Movie was not released before finalizing layer: " + _movieIdentifiers[i].MovieName, "C:\\BuildAgent\\work\\mb3\\Source\\Engine\\TaleWorlds.Engine.GauntletUI\\GauntletLayer.cs", "OnFinalize", 288);
 214: 			}
 215: 		}
 216: 		TwoDimensionView.ManualInvalidate();
 217: 		base.OnFinalize();
 218: 	}
 219: 
 220: 	protected override void RefreshGlobalOrder(ref int currentOrder)
 221: 	{
 222: 		TwoDimensionView.SetRenderOrder(currentOrder);
 223: 		currentOrder++;
 224: 	}
 225: 
 226: 	public override void ProcessEvents()
 227: 	{
 228: 		base.ProcessEvents();
 229: 		UIContext.UpdateInput(base._usedInputs);
 230: 	}

 succeeded in 775ms:
   1: using System;
   2: using System.Collections.Generic;
   3: using System.Numerics;
   4: using TaleWorlds.InputSystem;
   5: using TaleWorlds.Library;
   6: 
   7: namespace TaleWorlds.ScreenSystem;
   8: 
   9: public abstract class ScreenLayer : IComparable
  10: {
  11: 	public string Name { get; private set; }
  12: 
  13: 	public float Scale => ScreenManager.Scale;
  14: 
  15: 	public Vec2 UsableArea => ScreenManager.UsableArea;
  16: 
  17: 	public InputContext Input { get; private set; }
  18: 
  19: 	public InputRestrictions InputRestrictions { get; private set; }
  20: 
  21: 	public bool LastActiveState { get; set; }
  22: 
  23: 	public bool IsFinalized { get; private set; }
  24: 
  25: 	public bool IsActive { get; private set; }
  26: 
  27: 	public bool IsHitThisFrame { get; internal set; }
  28: 
  29: 	public bool IsFocusLayer { get; set; }
  30: 
  31: 	public CursorType ActiveCursor { get; set; }
  32: 
  33: 	protected InputType _usedInputs { get; set; }
  34: 
  35: 	public int ScreenOrderInLastFrame { get; internal set; }
  36: 
  37: 	public InputUsageMask InputUsageMask => InputRestrictions.InputUsageMask;
  38: 
  39: 	public static event Action<ScreenLayer> OnLayerActiveStateChanged;
  40: 
  41: 	protected ScreenLayer(string name, int localOrder)
  42: 	{
  43: 		InputRestrictions = new InputRestrictions(localOrder);
  44: 		Input = new InputContext();
  45: 		Name = name;
  46: 		LastActiveState = true;
  47: 		IsFinalized = false;
  48: 		IsActive = false;
  49: 		IsFocusLayer = false;
  50: 		_usedInputs = InputType.None;
  51: 		ActiveCursor = CursorType.Default;
  52: 	}
  53: 
  54: 	protected internal virtual void Tick(float dt)
  55: 	{
  56: 	}
  57: 
  58: 	protected internal virtual void LateUpdate(float dt)
  59: 	{
  60: 	}
  61: 
  62: 	protected internal virtual void RenderTick(float dt)
  63: 	{
  64: 	}
  65: 
  66: 	protected internal virtual void Update(IReadOnlyList<int> lastKeysPressed)
  67: 	{
  68: 	}
  69: 
  70: 	internal void HandleFinalize()
  71: 	{
  72: 		if (IsFinalized)
  73: 		{
  74: 			Debug.FailedAssert("Screen layer is already finalized", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.ScreenSystem\\ScreenLayer.cs", "HandleFinalize", 74);
  75: 			return;
  76: 		}
  77: 		OnFinalize();
  78: 		IsFinalized = true;
  79: 	}
  80: 
  81: 	internal void HandleGainFocus()
  82: 	{
  83: 		Input.ResetLastDownKeys();
  84: 		OnGainFocus();
  85: 	}
  86: 
  87: 	internal void HandleLoseFocus()
  88: 	{
  89: 		Input.ResetLastDownKeys();
  90: 		OnLoseFocus();
  91: 	}
  92: 
  93: 	protected virtual void OnActivate()
  94: 	{
  95: 		IsFinalized = false;
  96: 	}
  97: 
  98: 	protected virtual void OnDeactivate()
  99: 	{
 100: 	}
 101: 
 102: 	protected internal virtual void OnGainFocus()
 103: 	{
 104: 	}
 105: 
 106: 	protected internal virtual void OnLoseFocus()
 107: 	{
 108: 	}
 109: 
 110: 	internal void HandleActivate()
 111: 	{
 112: 		if (!IsActive)
 113: 		{
 114: 			IsActive = true;
 115: 			OnActivate();
 116: 			ScreenLayer.OnLayerActiveStateChanged?.Invoke(this);
 117: 		}
 118: 	}
 119: 
 120: 	internal void HandleDeactivate()
 121: 	{
 122: 		if (IsActive)
 123: 		{
 124: 			OnDeactivate();
 125: 			IsActive = false;
 126: 			ScreenManager.TryLoseFocus(this);
 127: 			ScreenLayer.OnLayerActiveStateChanged?.Invoke(this);
 128: 		}
 129: 	}
 130: 
 131: 	protected virtual void OnFinalize()
 132: 	{
 133: 	}
 134: 
 135: 	protected internal virtual void RefreshGlobalOrder(ref int currentOrder)
 136: 	{
 137: 	}
 138: 
 139: 	public virtual void DrawDebugInfo()
 140: 	{
 141: 		ScreenManager.EngineInterface.DrawDebugText($"Order: {InputRestrictions.Order}");
 142: 		ScreenManager.EngineInterface.DrawDebugText($"Is Layer Focusable: {IsFocusLayer}");
 143: 		ScreenManager.EngineInterface.DrawDebugText($"Is FocusedLayer: {this == ScreenManager.FocusedLayer}");
 144: 		ScreenManager.EngineInterface.DrawDebugText($"Keys Allowed: {Input.IsKeysAllowed}");
 145: 		ScreenManager.EngineInterface.DrawDebugText($"Controller Allowed: {Input.IsControllerAllowed}");
 146: 		ScreenManager.EngineInterface.DrawDebugText($"Mouse Button Allowed: {Input.IsMouseButtonAllowed}");
 147: 		ScreenManager.EngineInterface.DrawDebugText($"Mouse Wheel Allowed: {Input.IsMouseWheelAllowed}");
 148: 	}
 149: 
 150: 	public virtual void EarlyProcessEvents(InputType handledInputs)
 151: 	{
 152: 		_usedInputs = handledInputs;
 153: 	}
 154: 
 155: 	public virtual void ProcessEvents()
 156: 	{
 157: 		Input.IsKeysAllowed = _usedInputs.HasAnyFlag(InputType.Key);
 158: 		Input.IsMouseButtonAllowed = _usedInputs.HasAnyFlag(InputType.MouseButton);
 159: 		Input.IsMouseWheelAllowed = _usedInputs.HasAnyFlag(InputType.MouseWheel);
 160: 	}
 161: 
 162: 	public virtual bool HitTest(Vector2 position)
 163: 	{
 164: 		return false;
 165: 	}
 166: 
 167: 	public virtual bool HitTest()
 168: 	{
 169: 		return false;
 170: 	}
 171: 
 172: 	public virtual bool FocusTest()
 173: 	{
 174: 		return false;
 175: 	}
 176: 
 177: 	public virtual bool IsFocusedOnInput()
 178: 	{
 179: 		return false;
 180: 	}
 181: 
 182: 	public virtual void OnOnScreenKeyboardDone(string inputText)
 183: 	{
 184: 	}
 185: 
 186: 	public virtual void OnOnScreenKeyboardCanceled()
 187: 	{
 188: 	}
 189: 
 190: 	public int CompareTo(object obj)
 191: 	{
 192: 		if (!(obj is ScreenLayer screenLayer))
 193: 		{
 194: 			return 1;
 195: 		}
 196: 		if (screenLayer == this)
 197: 		{
 198: 			return 0;
 199: 		}
 200: 		if (InputRestrictions.Order == screenLayer.InputRestrictions.Order)
 201: 		{
 202: 			return InputRestrictions.Id.CompareTo(screenLayer.InputRestrictions.Id);
 203: 		}
 204: 		return InputRestrictions.Order.CompareTo(screenLayer.InputRestrictions.Order);
 205: 	}
 206: 
 207: 	public virtual void UpdateLayout()
 208: 	{
 209: 	}
 210: }

 succeeded in 1043ms:
   1: using System.Collections.Generic;
   2: using TaleWorlds.GauntletUI.BaseTypes;
   3: using TaleWorlds.GauntletUI.PrefabSystem;
   4: using TaleWorlds.Library;
   5: 
   6: namespace TaleWorlds.GauntletUI.Data;
   7: 
   8: public class GauntletMovie : IGauntletMovie
   9: {
  10: 	private WidgetPrefab _moviePrefab;
  11: 
  12: 	private IViewModel _viewModel;
  13: 
  14: 	private Widget _movieRootNode;
  15: 
  16: 	private bool _isHotReloadEnabled;
  17: 
  18: 	public WidgetFactory WidgetFactory { get; private set; }
  19: 
  20: 	public BrushFactory BrushFactory { get; private set; }
  21: 
  22: 	public UIContext Context { get; private set; }
  23: 
  24: 	public IViewModel ViewModel => _viewModel;
  25: 
  26: 	public string MovieName { get; private set; }
  27: 
  28: 	public GauntletView RootView { get; private set; }
  29: 
  30: 	public Widget RootWidget
  31: 	{
  32: 		get
  33: 		{
  34: 			if (RootView == null)
  35: 			{
  36: 				return null;
  37: 			}
  38: 			return RootView.Target;
  39: 		}
  40: 	}
  41: 
  42: 	public bool IsLoaded { get; private set; }
  43: 
  44: 	public bool IsReleased { get; private set; }
  45: 
  46: 	private GauntletMovie(string movieName, UIContext context, WidgetFactory widgetFactory, IViewModel viewModel, bool hotReloadEnabled)
  47: 	{
  48: 		WidgetFactory = widgetFactory;
  49: 		BrushFactory = context.BrushFactory;
  50: 		Context = context;
  51: 		_isHotReloadEnabled = hotReloadEnabled;
  52: 		WidgetFactory.PrefabChange += OnResourceChanged;
  53: 		BrushFactory.BrushChange += OnResourceChanged;
  54: 		_viewModel = viewModel;
  55: 		MovieName = movieName;
  56: 		_movieRootNode = new Widget(Context);
  57: 		Context.Root.AddChild(_movieRootNode);
  58: 		_movieRootNode.WidthSizePolicy = SizePolicy.Fixed;
  59: 		_movieRootNode.HeightSizePolicy = SizePolicy.Fixed;
  60: 		_movieRootNode.ScaledSuggestedWidth = Context.TwoDimensionContext.Width;
  61: 		_movieRootNode.ScaledSuggestedHeight = Context.TwoDimensionContext.Height;
  62: 		_movieRootNode.DoNotAcceptEvents = true;
  63: 		IsLoaded = false;
  64: 		IsReleased = false;
  65: 	}
  66: 
  67: 	public void RefreshDataSource(IViewModel dataSourve)
  68: 	{
  69: 		_viewModel = dataSourve;
  70: 		RootView.RefreshBindingWithChildren();
  71: 	}
  72: 
  73: 	private void RefreshResources()
  74: 	{
  75: 		RootView.ClearEventHandlersWithChildren();
  76: 		RootView = null;
  77: 		_movieRootNode.RemoveAllChildren();
  78: 		Context.OnMovieReleased(MovieName);
  79: 		IsLoaded = false;
  80: 		LoadMovie();
  81: 	}
  82: 
  83: 	private void OnResourceChanged()
  84: 	{
  85: 		if (_isHotReloadEnabled)
  86: 		{
  87: 			RefreshResources();
  88: 		}
  89: 	}
  90: 
  91: 	private void LoadMovie()
  92: 	{
  93: 		_moviePrefab = WidgetFactory.GetCustomType(MovieName);
  94: 		if (_moviePrefab != null)
  95: 		{
  96: 			IsLoaded = true;
  97: 			IsReleased = false;
  98: 			WidgetCreationData widgetCreationData = new WidgetCreationData(Context, WidgetFactory);
  99: 			widgetCreationData.AddExtensionData(this);
 100: 			WidgetInstantiationResult widgetInstantiationResult = _moviePrefab.Instantiate(widgetCreationData);
 101: 			RootView = widgetInstantiationResult.GetGauntletView();
 102: 			Widget target = RootView.Target;
 103: 			_movieRootNode.AddChild(target);
 104: 			RootView.RefreshBindingWithChildren();
 105: 			Context.OnMovieLoaded(MovieName);
 106: 		}
 107: 	}
 108: 
 109: 	public void Release()
 110: 	{
 111: 		_movieRootNode.OnBeforeRemovedChild(_movieRootNode);
 112: 		RootView?.ReleaseBindingWithChildren();
 113: 		_moviePrefab.OnRelease();
 114: 		WidgetFactory.OnUnload(MovieName);
 115: 		WidgetFactory.PrefabChange -= OnResourceChanged;
 116: 		BrushFactory.BrushChange -= OnResourceChanged;
 117: 		Context.OnMovieReleased(MovieName);
 118: 		_movieRootNode.ParentWidget = null;
 119: 		IsLoaded = false;
 120: 		IsReleased = true;
 121: 	}
 122: 
 123: 	internal void OnItemRemoved(string type)
 124: 	{
 125: 		WidgetFactory.OnUnload(type);
 126: 	}
 127: 
 128: 	public void Update()
 129: 	{
 130: 		_movieRootNode.ScaledSuggestedWidth = Context.TwoDimensionContext.Width;
 131: 		_movieRootNode.ScaledSuggestedHeight = Context.TwoDimensionContext.Height;
 132: 	}
 133: 
 134: 	internal object GetViewModelAtPath(BindingPath path, bool isListExpected)
 135: 	{
 136: 		if (_viewModel != null && path != null)
 137: 		{
 138: 			BindingPath path2 = path.Simplify();
 139: 			return _viewModel.GetViewModelAtPath(path2, isListExpected);
 140: 		}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenBase.cs' | ForEach-Object { if ("'$i -ge 180 -and $i -le 270) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenManager.cs' | ForEach-Object { if ("'$i -ge 486 -and $i -le 528) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenManager.cs' | ForEach-Object { if ("'$i -ge 896 -and $i -le 924) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 527ms:
 180: 		}
 181: 	}
 182: 
 183: 	public void DeactivateAllLayers()
 184: 	{
 185: 		foreach (ScreenLayer layer in _layers)
 186: 		{
 187: 			if (layer.IsActive)
 188: 			{
 189: 				layer.HandleDeactivate();
 190: 			}
 191: 		}
 192: 	}
 193: 
 194: 	public void Deactivate()
 195: 	{
 196: 		if (IsActive)
 197: 		{
 198: 			HandleDeactivate();
 199: 			IsActive = false;
 200: 		}
 201: 	}
 202: 
 203: 	public void Activate()
 204: 	{
 205: 		if (!IsActive)
 206: 		{
 207: 			HandleActivate();
 208: 			IsActive = true;
 209: 		}
 210: 	}
 211: 
 212: 	public virtual void UpdateLayout()
 213: 	{
 214: 		for (int i = 0; i < _layers.Count; i++)
 215: 		{
 216: 			if (!_layers[i].IsFinalized)
 217: 			{
 218: 				_layers[i].UpdateLayout();
 219: 			}
 220: 		}
 221: 	}
 222: 
 223: 	internal void IdleTick(float dt)
 224: 	{
 225: 		OnIdleTick(dt);
 226: 	}
 227: 
 228: 	protected virtual void OnInitialize()
 229: 	{
 230: 	}
 231: 
 232: 	protected virtual void OnFinalize()
 233: 	{
 234: 	}
 235: 
 236: 	protected virtual void OnPause()
 237: 	{
 238: 	}
 239: 
 240: 	protected virtual void OnResume()
 241: 	{
 242: 	}
 243: 
 244: 	protected virtual void OnActivate()
 245: 	{
 246: 	}
 247: 
 248: 	protected virtual void OnDeactivate()
 249: 	{
 250: 	}
 251: 
 252: 	protected virtual void OnFrameTick(float dt)
 253: 	{
 254: 	}
 255: 
 256: 	protected virtual void OnPostFrameTick(float dt)
 257: 	{
 258: 	}
 259: 
 260: 	protected virtual void OnIdleTick(float dt)
 261: 	{
 262: 	}
 263: 
 264: 	public virtual void OnFocusChangeOnGameWindow(bool focusGained)
 265: 	{
 266: 	}
 267: 
 268: 	public void AddComponent(ScreenComponent component)
 269: 	{
 270: 		_components.Add(component);

 succeeded in 537ms:
 486: 		}
 487: 		TaleWorlds.Library.Debug.Print("PushScreen");
 488: 		if (_screenList.Count > 0)
 489: 		{
 490: 			TopScreen.HandlePause();
 491: 			if (TopScreen.IsActive)
 492: 			{
 493: 				TopScreen.HandleDeactivate();
 494: 			}
 495: 		}
 496: 		_screenList.Add(screen);
 497: 		screen.HandleInitialize();
 498: 		screen.HandleActivate();
 499: 		screen.HandleResume();
 500: 		_globalOrderDirty = true;
 501: 		ScreenManager.OnPushScreen?.Invoke(screen);
 502: 	}
 503: 
 504: 	public static void PopScreen()
 505: 	{
 506: 		if (!TWParallel.IsMainThread())
 507: 		{
 508: 			TaleWorlds.Library.Debug.FailedAssert("Screen should be changed from main thread", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.ScreenSystem\\ScreenManager.cs", "PopScreen", 625);
 509: 		}
 510: 		TaleWorlds.Library.Debug.Print("PopScreen");
 511: 		if (_screenList.Count > 0)
 512: 		{
 513: 			TopScreen.HandlePause();
 514: 			TopScreen.HandleDeactivate();
 515: 			TopScreen.HandleFinalize();
 516: 			TaleWorlds.Library.Debug.Print("PopScreen - " + TopScreen.GetType().ToString());
 517: 			ScreenManager.OnPopScreen?.Invoke(TopScreen);
 518: 			_screenList.Remove(TopScreen);
 519: 		}
 520: 		if (_screenList.Count > 0)
 521: 		{
 522: 			ScreenBase topScreen = TopScreen;
 523: 			TopScreen.HandleActivate();
 524: 			if (topScreen == TopScreen)
 525: 			{
 526: 				TopScreen.HandleResume();
 527: 			}
 528: 		}

 succeeded in 553ms:
 896: 	{
 897: 		TaleWorlds.Library.Debug.Print("OnScreenListChanged");
 898: 		SetSortedLayersDirty();
 899: 		ObservableCollection<ScreenBase> screenList = _screenList;
 900: 		if (screenList != null && screenList.Count > 0)
 901: 		{
 902: 			if (TopScreen != null)
 903: 			{
 904: 				TopScreen.OnAddLayer -= OnLayerAddedToTopLayer;
 905: 				TopScreen.OnRemoveLayer -= OnLayerRemovedFromTopLayer;
 906: 			}
 907: 			TopScreen = _screenList[_screenList.Count - 1];
 908: 			if (TopScreen != null)
 909: 			{
 910: 				TopScreen.OnAddLayer += OnLayerAddedToTopLayer;
 911: 				TopScreen.OnRemoveLayer += OnLayerRemovedFromTopLayer;
 912: 			}
 913: 		}
 914: 		else
 915: 		{
 916: 			if (TopScreen != null)
 917: 			{
 918: 				TopScreen.OnAddLayer -= OnLayerAddedToTopLayer;
 919: 				TopScreen.OnRemoveLayer -= OnLayerRemovedFromTopLayer;
 920: 			}
 921: 			TopScreen = null;
 922: 		}
 923: 		SetSortedLayersDirty();
 924: 	}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\Engine\\TaleWorlds.ScreenSystem\\TaleWorlds.ScreenSystem\\ScreenBase.cs' | ForEach-Object { if ("'$i -ge 268 -and $i -le 360) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*MissionScreen*.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*MissionState*.cs' -ErrorAction SilentlyContinue | Select-Object -First 20 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 494ms:
 268: 	public void AddComponent(ScreenComponent component)
 269: 	{
 270: 		_components.Add(component);
 271: 	}
 272: 
 273: 	public T FindComponent<T>() where T : ScreenComponent
 274: 	{
 275: 		foreach (ScreenComponent component in _components)
 276: 		{
 277: 			if (component is T)
 278: 			{
 279: 				return (T)component;
 280: 			}
 281: 		}
 282: 		return null;
 283: 	}
 284: 
 285: 	public void AddLayer(ScreenLayer layer)
 286: 	{
 287: 		if (layer == null || layer.IsFinalized)
 288: 		{
 289: 			Debug.FailedAssert("Trying to add a null or finalized layer", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.ScreenSystem\\ScreenBase.cs", "AddLayer", 337);
 290: 		}
 291: 		else if (!_layers.Contains(layer))
 292: 		{
 293: 			_layers.Add(layer);
 294: 			_layers.Sort();
 295: 			if (IsActive)
 296: 			{
 297: 				layer.LastActiveState = true;
 298: 				layer.HandleActivate();
 299: 			}
 300: 			this.OnAddLayer?.Invoke(layer);
 301: 		}
 302: 		else
 303: 		{
 304: 			Debug.FailedAssert("Layer is already added to the screen!", "C:\\BuildAgent\\work\\mb3\\TaleWorlds.Shared\\Source\\GauntletUI\\TaleWorlds.ScreenSystem\\ScreenBase.cs", "AddLayer", 356);
 305: 		}
 306: 	}
 307: 
 308: 	public void RemoveLayer(ScreenLayer layer)
 309: 	{
 310: 		if (IsActive)
 311: 		{
 312: 			layer.LastActiveState = false;
 313: 			layer.HandleDeactivate();
 314: 		}
 315: 		layer.HandleFinalize();
 316: 		_layers.Remove(layer);
 317: 		this.OnRemoveLayer?.Invoke(layer);
 318: 		ScreenManager.RefreshGlobalOrder();
 319: 	}
 320: 
 321: 	public bool HasLayer(ScreenLayer layer)
 322: 	{
 323: 		return _layers.Contains(layer);
 324: 	}
 325: 
 326: 	public T FindLayer<T>() where T : ScreenLayer
 327: 	{
 328: 		foreach (ScreenLayer layer in _layers)
 329: 		{
 330: 			if (layer is T)
 331: 			{
 332: 				return (T)layer;
 333: 			}
 334: 		}
 335: 		return null;
 336: 	}
 337: 
 338: 	public T FindLayer<T>(string name) where T : ScreenLayer
 339: 	{
 340: 		foreach (ScreenLayer layer in _layers)
 341: 		{
 342: 			if (layer is T val && val.Name == name)
 343: 			{
 344: 				return val;
 345: 			}
 346: 		}
 347: 		return null;
 348: 	}
 349: 
 350: 	public void SetLayerCategoriesState(string[] categoryIds, bool isActive)
 351: 	{
 352: 		foreach (ScreenLayer layer in _layers)
 353: 		{
 354: 			if (categoryIds.IndexOf(layer.Name) >= 0)
 355: 			{
 356: 				if (isActive && !layer.IsActive)
 357: 				{
 358: 					layer.HandleActivate();
 359: 				}
 360: 				else if (!isActive && layer.IsActive)

 succeeded in 497ms:
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade.ViewModelCollection\TaleWorlds.MountAndBlade.ViewModelCollection\IMissionScreen.cs

 succeeded in 468ms:
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\NetworkMessages.FromServer\MissionStateChange.cs
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MultiplayerIntermissionState.cs

codex
One vanilla detail is important: `ScreenBase.RemoveLayer` itself calls `layer.HandleFinalize()` before removing the layer. So the controller’s explicit `ReleaseMovie()` after `RemoveLayer()` is not the first release path; GauntletLayer finalization already releases remaining movies. I’m checking whether that is only redundant or whether it leaves stale references when `TopScreen` is not the owning mission screen.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'OnEndMission\\(|EndMissionInternal|OnMissionEnded|MissionEnded' | Select-Object -First 120" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionState.cs' | ForEach-Object { if ("'$i -ge 1 -and $i -le 260) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*MissionView*.cs' -ErrorAction SilentlyContinue | Select-Object -First 50 -ExpandProperty FullName" in C:\Users\mikew\source\repos\TAOM
 succeeded in 524ms:
   1: using System.Collections.Generic;
   2: using System.Linq;
   3: using NetworkMessages.FromClient;
   4: using TaleWorlds.Core;
   5: using TaleWorlds.Engine;
   6: using TaleWorlds.Library;
   7: using TaleWorlds.MountAndBlade.Source.Missions.Handlers;
   8: 
   9: namespace TaleWorlds.MountAndBlade;
  10: 
  11: public class MissionState : GameState
  12: {
  13: 	private const int MissionFastForwardSpeedMultiplier = 10;
  14: 
  15: 	private bool _missionInitializing;
  16: 
  17: 	private int _tickCountBeforeLoad;
  18: 
  19: 	public static bool RecordMission;
  20: 
  21: 	public float MissionReplayStartTime;
  22: 
  23: 	public float MissionEndTime;
  24: 
  25: 	private bool _isDelayedDisconnecting;
  26: 
  27: 	private int _missionTickCount;
  28: 
  29: 	public IMissionSystemHandler Handler { get; set; }
  30: 
  31: 	public static MissionState Current { get; private set; }
  32: 
  33: 	public Mission CurrentMission { get; private set; }
  34: 
  35: 	public string MissionName { get; private set; }
  36: 
  37: 	public bool FirstMissionTickAfterLoading { get; private set; }
  38: 
  39: 	public bool Paused { get; set; }
  40: 
  41: 	protected override void OnInitialize()
  42: 	{
  43: 		base.OnInitialize();
  44: 		Current = this;
  45: 		FirstMissionTickAfterLoading = true;
  46: 		LoadingWindow.EnableGlobalLoadingWindow();
  47: 	}
  48: 
  49: 	protected override void OnFinalize()
  50: 	{
  51: 		base.OnFinalize();
  52: 		CurrentMission.OnMissionStateFinalize(CurrentMission.NeedsMemoryCleanup);
  53: 		CurrentMission = null;
  54: 		Current = null;
  55: 	}
  56: 
  57: 	protected override void OnActivate()
  58: 	{
  59: 		base.OnActivate();
  60: 		CurrentMission.OnMissionStateActivate();
  61: 	}
  62: 
  63: 	protected override void OnDeactivate()
  64: 	{
  65: 		base.OnDeactivate();
  66: 		CurrentMission.OnMissionStateDeactivate();
  67: 	}
  68: 
  69: 	protected override void OnIdleTick(float dt)
  70: 	{
  71: 		base.OnIdleTick(dt);
  72: 		if (CurrentMission != null && CurrentMission.CurrentState == Mission.State.Continuing)
  73: 		{
  74: 			CurrentMission.IdleTick(dt);
  75: 		}
  76: 	}
  77: 
  78: 	protected override void OnTick(float realDt)
  79: 	{
  80: 		base.OnTick(realDt);
  81: 		if (_isDelayedDisconnecting && CurrentMission != null && CurrentMission.CurrentState == Mission.State.Continuing)
  82: 		{
  83: 			BannerlordNetwork.EndMultiplayerLobbyMission();
  84: 		}
  85: 		if (CurrentMission == null)
  86: 		{
  87: 			return;
  88: 		}
  89: 		if (CurrentMission.CurrentState == Mission.State.NewlyCreated || CurrentMission.CurrentState == Mission.State.Initializing)
  90: 		{
  91: 			if (CurrentMission.CurrentState == Mission.State.NewlyCreated)
  92: 			{
  93: 				CurrentMission.ClearUnreferencedResources(CurrentMission.NeedsMemoryCleanup);
  94: 			}
  95: 			TickLoading(realDt);
  96: 		}
  97: 		else if (CurrentMission.CurrentState == Mission.State.Continuing || CurrentMission.MissionEnded)
  98: 		{
  99: 			if (MissionReplayStartTime != 0f)
 100: 			{
 101: 				CurrentMission.SkipForwardMissionReplay(MissionReplayStartTime, 0.033f);
 102: 				MissionReplayStartTime = 0f;
 103: 			}
 104: 			bool flag = false;
 105: 			if (MissionEndTime != 0f && CurrentMission.CurrentTime > MissionEndTime)
 106: 			{
 107: 				CurrentMission.EndMission();
 108: 				flag = true;
 109: 			}
 110: 			if (!flag && (Handler == null || Handler.RenderIsReady()))
 111: 			{
 112: 				TickMission(realDt);
 113: 			}
 114: 			if (flag && MBEditor._isEditorMissionOn)
 115: 			{
 116: 				MBEditor.LeaveEditMissionMode();
 117: 				TickMission(realDt);
 118: 			}
 119: 		}
 120: 		if (CurrentMission.CurrentState == Mission.State.Over)
 121: 		{
 122: 			if (MBGameManager.Current.IsEnding)
 123: 			{
 124: 				Game.Current.GameStateManager.CleanStates();
 125: 			}
 126: 			else
 127: 			{
 128: 				Game.Current.GameStateManager.PopState();
 129: 			}
 130: 		}
 131: 	}
 132: 
 133: 	private void TickMission(float realDt)
 134: 	{
 135: 		if (FirstMissionTickAfterLoading && CurrentMission != null && CurrentMission.CurrentState == Mission.State.Continuing && GameNetwork.IsClient)
 136: 		{
 137: 			int currentBattleIndex = GameNetwork.GetNetworkComponent<BaseNetworkComponentData>().CurrentBattleIndex;
 138: 			MBDebug.Print($"Client: I finished loading battle with index: {currentBattleIndex}. Sending confirmation to server.", 0, Debug.DebugColor.White, 17179869184uL);
 139: 			GameNetwork.BeginModuleEventAsClient();
 140: 			GameNetwork.WriteMessage(new FinishedLoading(currentBattleIndex));
 141: 			GameNetwork.EndModuleEventAsClient();
 142: 			GameNetwork.SyncRelevantGameOptionsToServer();
 143: 		}
 144: 		Handler?.BeforeMissionTick(CurrentMission, realDt);
 145: 		CurrentMission.PauseAITick = false;
 146: 		if (GameNetwork.IsSessionActive && CurrentMission.ClearSceneTimerElapsedTime < 0f)
 147: 		{
 148: 			CurrentMission.PauseAITick = true;
 149: 		}
 150: 		float num = realDt;
 151: 		if (Paused || MBCommon.IsPaused)
 152: 		{
 153: 			num = 0f;
 154: 		}
 155: 		else if (CurrentMission.FixedDeltaTimeMode)
 156: 		{
 157: 			num = CurrentMission.FixedDeltaTime;
 158: 		}
 159: 		if (!GameNetwork.IsSessionActive)
 160: 		{
 161: 			CurrentMission.UpdateSceneTimeSpeed();
 162: 			float timeSpeed = CurrentMission.Scene.TimeSpeed;
 163: 			num *= timeSpeed;
 164: 		}
 165: 		if (CurrentMission.ClearSceneTimerElapsedTime < -0.3f && !GameNetwork.IsClientOrReplay)
 166: 		{
 167: 			CurrentMission.ClearAgentActions();
 168: 		}
 169: 		if (CurrentMission.CurrentState == Mission.State.Continuing || CurrentMission.MissionEnded)
 170: 		{
 171: 			if (CurrentMission.IsFastForward)
 172: 			{
 173: 				float num2 = num * 9f;
 174: 				while (num2 > 1E-06f)
 175: 				{
 176: 					if (num2 > 0.1f)
 177: 					{
 178: 						TickMissionAux(0.1f, 0.1f, updateCamera: false, asyncAITick: false);
 179: 						if (CurrentMission.CurrentState == Mission.State.Over)
 180: 						{
 181: 							break;
 182: 						}
 183: 						num2 -= 0.1f;
 184: 					}
 185: 					else
 186: 					{
 187: 						if (num2 > 0.0033333334f)
 188: 						{
 189: 							TickMissionAux(num2, num2, updateCamera: false, asyncAITick: false);
 190: 						}
 191: 						num2 = 0f;
 192: 					}
 193: 				}
 194: 				if (CurrentMission.CurrentState != Mission.State.Over)
 195: 				{
 196: 					TickMissionAux(num, realDt, updateCamera: true, asyncAITick: false);
 197: 				}
 198: 			}
 199: 			else
 200: 			{
 201: 				TickMissionAux(num, realDt, updateCamera: true, asyncAITick: true);
 202: 			}
 203: 		}
 204: 		if (Handler != null)
 205: 		{
 206: 			Handler.AfterMissionTick(CurrentMission, realDt);
 207: 		}
 208: 		FirstMissionTickAfterLoading = false;
 209: 		_missionTickCount++;
 210: 	}
 211: 
 212: 	private void TickMissionAux(float dt, float realDt, bool updateCamera, bool asyncAITick)
 213: 	{
 214: 		CurrentMission.Tick(dt);
 215: 		if (_missionTickCount > 2)
 216: 		{
 217: 			CurrentMission.OnTick(dt, realDt, updateCamera, asyncAITick);
 218: 		}
 219: 	}
 220: 
 221: 	private void TickLoading(float realDt)
 222: 	{
 223: 		_tickCountBeforeLoad++;
 224: 		if (!_missionInitializing && _tickCountBeforeLoad > 0)
 225: 		{
 226: 			LoadMission();
 227: 			Utilities.SetLoadingScreenPercentage(0.01f);
 228: 		}
 229: 		else if (_missionInitializing && CurrentMission.IsLoadingFinished)
 230: 		{
 231: 			FinishMissionLoading();
 232: 		}
 233: 	}
 234: 
 235: 	private void LoadMission()
 236: 	{
 237: 		foreach (MissionBehavior missionBehavior in CurrentMission.MissionBehaviors)
 238: 		{
 239: 			missionBehavior.OnMissionScreenPreLoad();
 240: 		}
 241: 		Utilities.ClearOldResourcesAndObjects();
 242: 		_missionInitializing = true;
 243: 		CurrentMission.Initialize();
 244: 	}
 245: 
 246: 	private void CreateMission(MissionInitializerRecord rec, bool needsMemoryCleanup)
 247: 	{
 248: 		CurrentMission = new Mission(rec, this, needsMemoryCleanup);
 249: 	}
 250: 
 251: 	protected Mission HandleOpenNew(string missionName, MissionInitializerRecord rec, InitializeMissionBehaviorsDelegate handler, bool addDefaultMissionBehaviors, bool needsMemoryCleanup)
 252: 	{
 253: 		MissionName = missionName;
 254: 		CreateMission(rec, needsMemoryCleanup);
 255: 		IEnumerable<MissionBehavior> source = handler(CurrentMission);
 256: 		source = source.Where((MissionBehavior behavior) => behavior != null);
 257: 		if (addDefaultMissionBehaviors)
 258: 		{
 259: 			source = AddDefaultMissionBehaviorsTo(CurrentMission, source);
 260: 		}

 succeeded in 490ms:
 succeeded in 1113ms:

E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1197:	
public override void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventDispatcher.cs:1202:	
		eventReceivers[i].[7mOnMissionEnded[0m(mission);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEventReceiver.cs:541:	
public virtual void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:319:	private 
readonly MbEvent<IMission> _[7monMissionEnded[0mEvent [0m[7m[0m= [0m[7m[0mnew [0m[7m[0mMbEvent<IMission>();[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:875:	public 
static IMbEvent<IMission> [7mOnMissionEnded[0mEvent [0m[7m[0m=> [0m[7m[0mInstance._onMissionEndedEvent;[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:1230:		
_[7monMissionEnded[0mEvent.ClearListeners(obj);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2128:	public 
override void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem\CampaignEvents.cs:2130:		
Instance._[7monMissionEnded[0mEvent.Invoke(mission);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnC
ampaignBehavior.cs:26:		CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener(this, [0m[7m[0mOnMissionEnded);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\HeroAgentSpawnC
ampaignBehavior.cs:233:	private void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotableHelperCh
aracterCampaignBehavior.cs:19:		CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener(this, [0m[7m[0mOnMissionEnded);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\NotableHelperCh
aracterCampaignBehavior.cs:27:	private void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\SiegeAmbushCamp
aignBehavior.cs:25:		CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener(this, [0m[7m[0mOnMissionEnded);[0m
E:\Decompiled_Bannerlord\Campaign\TaleWorlds.CampaignSystem\TaleWorlds.CampaignSystem.CampaignBehaviors\SiegeAmbushCamp
aignBehavior.cs:68:	private void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.MissionLogics\MissionBoardGameLogic.cs:341:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.BoardGames.MissionLogics\MissionBoardGameLogic.cs:343:		
((MissionBehavior)this).[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\BoardGameCampaignBehavior.cs:66:		
CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnd);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\ClanMemberRolesCampaignBehavior.cs:35:		
CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnded);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.CampaignBehaviors\ClanMemberRolesCampaignBehavior.cs:125:	private 
void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Conversation.MissionLogics\ConversationMissionLogic.cs:904:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Conversation.MissionLogics\MissionConversationLogic.cs:339:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\FamilyFeudIssueBehavior.cs:1743:			
CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnd);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\NotableWantsDaughterFoundIssueBehavior.cs:1616:		private void 
[7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Issues\NotableWantsDaughterFoundIssueBehavior.cs:1823:			
CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnded);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions\EavesdroppingMissionLogic.cs:66:	protected override void 
[7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions\SabotageMissionController.cs:38:	protected override void 
[7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionEvents\MissionAIActivationDeactivationEventListenerLog
ic.cs:22:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionEvents\OpenInventoryWithGivenItemsEventListenerLogic.c
s:26:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionEvents\ShowQuickInformationEventListenerLogic.cs:17:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\CampaignMissionComponent.cs:196:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\CampaignMissionComponent.cs:220:		((CampaignEve
ntReceiver)CampaignEventDispatcher.Instance).[7mOnMissionEnded[0m((IMission)(object)((MissionBehavior)this).Mission);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\CampaignSiegeStateHandler.cs:45:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\DisguiseMissionLogic.cs:327:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\EnemyAgentAIDeactivationMissionLogic.cs:15:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\LeaveMissionLogic.cs:18:	public override bool 
[7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\MissionAgentHandler.cs:154:	protected override 
void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\MissionCrimeHandler.cs:9:	protected override 
void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\MissionFightHandler.cs:257:	protected override 
void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\MissionPathGenerationLogic.cs:1463:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics\StealthPatrolPointMissionLogic.cs:37:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Arena\ArenaPracticeFightMissionController.cs:30
0:	public override bool [7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Hideout\HideoutAmbushMissionController.cs:310:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Hideout\HideoutMissionController.cs:619:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Missions.MissionLogics.Towns\PrisonBreakMissionController.cs:323:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments.MissionLogics\TournamentFightMissionController.cs:150:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents.CampaignBehaviors\TrainingFieldCampaignBehavior.cs:
93:		CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnded);[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.GameComponents.CampaignBehaviors\TrainingFieldCampaignBehavior.cs:
221:	private void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\SneakIntoTheVillaMissionController.cs:130:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\TrainingFieldMissionController.cs:491:	protected 
override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\TrainingFieldMissionController.cs:493:		
((MissionBehavior)this).[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\ConspiracyBaseOfOperationsDisc
overedConspiracyQuest.cs:335:		CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m
[7m[0m(Action<IMission>)OnMissionEnded);[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.SecondPhase.ConspiracyQuests\ConspiracyBaseOfOperationsDisc
overedConspiracyQuest.cs:435:	private void [7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.TutorialPhase\VillagersInNeed.cs:105:		
CampaignEvents.[7mOnMissionEnded[0mEvent.AddNonSerializedListener((object)this, [0m[7m[0m(Action<IMission>)OnMissionEnded);[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\StoryMode.Quests.TutorialPhase\VillagersInNeed.cs:117:	private void 
[7mOnMissionEnded[0m(IMission [0m[7m[0mmission)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Agent.cs:1799:		if 
(GameNetwork.IsClientOrReplay || Mission == null || Mission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Agent.cs:5150:		if 
(((HumanAIComponent != null && InteractingWithAnyGameObject()) || IsUsingGameObject) && !GameNetwork.IsClientOrReplay 
&& Mission != null && !Mission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\AgentVictoryLogic.cs:179:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:444:			
if (!_bannerLogic._is[7mMissionEnded [0m[7m[0m&& [0m[7m[0m_mission.Mode [0m[7m[0m== [0m[7m[0mMissionMode.Deployment [0m[7m[0m&& [0m[7m[0mformation.Team.IsPlayerTeam [0m[7m[0m&& [0m
[7m[0mMissionGameModels.Current.BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle())[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:464:			
if (!_bannerLogic._is[7mMissionEnded [0m[7m[0m&& [0m[7m[0m_mission.Mode [0m[7m[0m== [0m[7m[0mMissionMode.Deployment [0m[7m[0m&& [0m[7m[0mformation.Team.IsPlayerTeam [0m[7m[0m&& [0m
[7m[0mMissionGameModels.Current.BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle())[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:521:	
private bool _is[7mMissionEnded[0m;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:657:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:659:		
base.[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:664:		
_is[7mMissionEnded [0m[7m[0m= [0m[7m[0mtrue;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerBearerLogic.cs:670:		
_is[7mMissionEnded [0m[7m[0m= [0m[7m[0mfalse;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerlordNetwork.cs:40:		if 
(Game.Current.GameStateManager.ActiveState is MissionState { CurrentMission: not null } missionState && 
!missionState.CurrentMission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BasicLeaveMissionLogic.cs:28:	
public override bool [7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:44:	private 
bool _[7mmissionEnded[0mMessageShown;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:95:			if 
(_[7mmissionEnded[0mMessageShown [0m[7m[0m&& [0m[7m[0m!_scoreBoardOpenedOnceOnMissionEnd)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:149:				
_[7mmissionEnded[0mMessageShown [0m[7m[0m= [0m[7m[0mtrue;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:198:	public 
override bool [7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:224:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:248:		if 
(base.Mission.[7mMissionEnded [0m[7m[0m|| [0m[7m[0m(!PlayerVictory [0m[7m[0m&& [0m[7m[0m!EnemyVictory))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleEndLogic.cs:253:				if 
(!base.Mission.[7mMissionEnded [0m[7m[0m&& [0m[7m[0m!_isEnemySideRetreating)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BattleReinforcementsSpawnContr
oller.cs:42:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\CommonAIComponent.cs:109:		if 
(IsPanicked && Agent.Mission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\DefaultBattleMissionAgentSpawn
Logic.cs:152:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\HighlightsController.cs:294:	
protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\HighlightsController.cs:296:		
base.[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\HumanAIComponent.cs:214:			if 
(!_itemToPickUp.IsAIMovingTo(Agent) || Agent.Mission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\HumanAIComponent.cs:223:		if 
(!_itemPickUpTickTimer.Check(reset: true) || Agent.Mission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\IMissionListener.cs:11:	void 
[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:942:	private bool 
_[7mmissionEnded[0m;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1130:				if 
(IsNavalBattle || ![7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1235:	public bool 
[7mMissionEnded[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1239:			return 
_[7mmissionEnded[0m;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1243:			if 
(!_[7mmissionEnded [0m[7m[0m&& [0m[7m[0mvalue)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1248:					
missionObject.[7mOnMissionEnded[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1252:			
_[7mmissionEnded [0m[7m[0m= [0m[7m[0mvalue;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1319:				return 
[7mMissionEnded[0m;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:3710:		if 
(IsFastForward && [7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4557:		
[7mMissionEnded [0m[7m[0m= [0m[7m[0mtrue;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4561:	private void 
[7mEndMissionInternal[0m()[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4563:		
MBDebug.Print("I called [7mEndMissionInternal[0m", [0m[7m[0m0, [0m[7m[0mTaleWorlds.Library.Debug.DebugColor.White, [0m[7m[0m17179869184uL);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4568:			
array[i].[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4573:			
missionBehavior.On[7mEndMissionInternal[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4588:			
missionObject.[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4742:	private bool 
Check[7mMissionEnded[0m()[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4747:			if 
(missionLogic.[7mMissionEnded[0m(ref [0m[7m[0mmissionResult))[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4749:				
TaleWorlds.Library.Debug.Print("Check[7mMissionEnded[0m::ended");[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4751:				
[7mMissionEnded [0m[7m[0m= [0m[7m[0mtrue;[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4773:				if 
([7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4778:				
Check[7mMissionEnded[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4779:				if 
(![7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4795:				
[7mEndMissionInternal[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4804:			
[7mEndMissionInternal[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:121:	
public virtual void On[7mEndMissionInternal[0m()[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:123:		
[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:126:	
protected virtual void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionLobbyEquipmentNetworkCo
mponent.cs:32:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionLobbyEquipmentNetworkCo
mponent.cs:41:		base.[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionLogic.cs:17:	public 
virtual bool [7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionNetworkComponent.cs:178
3:	protected override void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionNetworkComponent.cs:179
6:		base.[7mOnEndMission([0m);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionObject.cs:174:	public 
virtual void [7mOnMissionEnded[0m()[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionObject.cs:322:	public 
virtual void [7mOnEndMission([0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:97:		else if 
(CurrentMission.CurrentState == Mission.State.Continuing || CurrentMission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:169:		if 
(CurrentMission.CurrentState == Mission.State.Continuing || CurrentMission.[7mMissionEnded[0m)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\RecordMissionLogic.cs:26:	
public override void On[7mEndMissionInternal[0m()[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\RecordMissionLogic.cs:28:		
base.On[7mEndMissionInternal[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\SallyOutEndLogic.cs:50:	
public override bool [7mMissionEnded[0m(ref [0m[7m[0mMissionResult [0m[7m[0mmissionResult)[0m


codex
Mission lifecycle evidence shows `OnEndMission` is called from `Mission.EndMissionInternal` while the mission state is still active, and the game state is popped only later when `CurrentState == Over`. That supports the singleton reset approach for normal sequential missions.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' | ForEach-Object { if ("'$i -ge 4548 -and $i -le 4592) { '"'{0,5}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionBehavior.cs' | ForEach-Object { if ("'$i -ge 110 -and $i -le 130) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionState.cs' | ForEach-Object { if ("'$i -ge 120 -and $i -le 130) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionState.cs' | ForEach-Object { if ("'$i -ge 246 -and $i -le 285) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 552ms:
 4548: 	}
 4549: 
 4550: 	[UsedImplicitly]
 4551: 	[MBCallback(null, false)]
 4552: 	public void EndMission()
 4553: 	{
 4554: 		TaleWorlds.Library.Debug.Print("I called EndMission", 0, TaleWorlds.Library.Debug.DebugColor.White, 17179869184uL);
 4555: 		_missionEndTime = -1f;
 4556: 		NextCheckTimeEndMission = -1f;
 4557: 		MissionEnded = true;
 4558: 		CurrentState = State.EndingNextFrame;
 4559: 	}
 4560: 
 4561: 	private void EndMissionInternal()
 4562: 	{
 4563: 		MBDebug.Print("I called EndMissionInternal", 0, TaleWorlds.Library.Debug.DebugColor.White, 17179869184uL);
 4564: 		_deploymentPlan.ClearAll();
 4565: 		IMissionListener[] array = _listeners.ToArray();
 4566: 		for (int i = 0; i < array.Length; i++)
 4567: 		{
 4568: 			array[i].OnEndMission();
 4569: 		}
 4570: 		StopSoundEvents();
 4571: 		foreach (MissionBehavior missionBehavior in MissionBehaviors)
 4572: 		{
 4573: 			missionBehavior.OnEndMissionInternal();
 4574: 		}
 4575: 		foreach (Agent agent in Agents)
 4576: 		{
 4577: 			agent.OnRemove();
 4578: 		}
 4579: 		foreach (Agent allAgent in AllAgents)
 4580: 		{
 4581: 			allAgent.OnDelete();
 4582: 			allAgent.Clear();
 4583: 		}
 4584: 		Teams.Clear();
 4585: 		FocusableObjectInformationProvider.OnFinalize();
 4586: 		foreach (MissionObject missionObject in MissionObjects)
 4587: 		{
 4588: 			missionObject.OnEndMission();
 4589: 		}
 4590: 		CurrentState = State.Over;
 4591: 		FreeResources();
 4592: 		FinalizeMission();

 succeeded in 553ms:
 110: 	{
 111: 	}
 112: 
 113: 	public virtual void OnAgentInteraction(Agent userAgent, Agent agent, sbyte agentBoneIndex)
 114: 	{
 115: 	}
 116: 
 117: 	public virtual void OnClearScene()
 118: 	{
 119: 	}
 120: 
 121: 	public virtual void OnEndMissionInternal()
 122: 	{
 123: 		OnEndMission();
 124: 	}
 125: 
 126: 	protected virtual void OnEndMission()
 127: 	{
 128: 	}
 129: 
 130: 	public virtual void OnRemoveBehavior()

 succeeded in 523ms:
 246: 	private void CreateMission(MissionInitializerRecord rec, bool needsMemoryCleanup)
 247: 	{
 248: 		CurrentMission = new Mission(rec, this, needsMemoryCleanup);
 249: 	}
 250: 
 251: 	protected Mission HandleOpenNew(string missionName, MissionInitializerRecord rec, InitializeMissionBehaviorsDelegate handler, bool addDefaultMissionBehaviors, bool needsMemoryCleanup)
 252: 	{
 253: 		MissionName = missionName;
 254: 		CreateMission(rec, needsMemoryCleanup);
 255: 		IEnumerable<MissionBehavior> source = handler(CurrentMission);
 256: 		source = source.Where((MissionBehavior behavior) => behavior != null);
 257: 		if (addDefaultMissionBehaviors)
 258: 		{
 259: 			source = AddDefaultMissionBehaviorsTo(CurrentMission, source);
 260: 		}
 261: 		foreach (MissionBehavior item in source)
 262: 		{
 263: 			item.OnAfterMissionCreated();
 264: 		}
 265: 		AddBehaviorsToMission(source);
 266: 		if (Handler != null)
 267: 		{
 268: 			source = new MissionBehavior[0];
 269: 			source = Handler.OnAddBehaviors(source, CurrentMission, missionName, addDefaultMissionBehaviors);
 270: 			AddBehaviorsToMission(source);
 271: 		}
 272: 		if (GameNetwork.IsDedicatedServer)
 273: 		{
 274: 			GameNetwork.SetServerFrameRate(Module.CurrentModule.StartupInfo.ServerTickRate);
 275: 		}
 276: 		return CurrentMission;
 277: 	}
 278: 
 279: 	private void AddBehaviorsToMission(IEnumerable<MissionBehavior> behaviors)
 280: 	{
 281: 		MissionLogic[] logicBehaviors = (from behavior in behaviors.OfType<MissionLogic>()
 282: 			where !(behavior is MissionNetwork)
 283: 			select behavior).ToArray();
 284: 		MissionBehavior[] otherBehaviors = behaviors.Where((MissionBehavior behavior) => behavior != null && !(behavior is MissionNetwork) && !(behavior is MissionLogic)).ToArray();
 285: 		MissionNetwork[] networkBehaviors = behaviors.OfType<MissionNetwork>().ToArray();

 succeeded in 523ms:
 120: 		if (CurrentMission.CurrentState == Mission.State.Over)
 121: 		{
 122: 			if (MBGameManager.Current.IsEnding)
 123: 			{
 124: 				Game.Current.GameStateManager.CleanStates();
 125: 			}
 126: 			else
 127: 			{
 128: 				Game.Current.GameStateManager.PopState();
 129: 			}
 130: 		}

codex
Vanilla mission creation (`HandleOpenNew`) constructs a new `Mission` and adds behavior instances to that mission before ticking it, while mission end sets state to `Over` and the state pop happens afterward. I do not see a normal overlap path where a new mission ticks before the old mission’s `OnEndMission`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionState.cs' | ForEach-Object { if ("'$i -ge 279 -and $i -le 326) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'OpenNew\\(|PushState\\(.*MissionState|CreateState<MissionState>|MissionState.Open' | Select-Object -First 80" in C:\Users\mikew\source\repos\TAOM
 succeeded in 1504ms:

E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:263:		return [7mMissionState.Open[0mNew("TownCenter", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[28][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:302:		return [7mMissionState.Open[0mNew("TownCenter", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[27][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:347:		return [7mMissionState.Open[0mNew("TownCenter", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:426:		return [7mMissionState.Open[0mNew("Indoor", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mtrue, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[23][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:464:		Mission obj = 
[7mMissionState.Open[0mNew("PrisonBreak", [0m[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:516:		return [7mMissionState.Open[0mNew("Village", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels2, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[27][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:554:		return 
[7mMissionState.Open[0mNew("ArenaPracticeFight", [0m[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m
[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[14][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:579:		return [7mMissionState.Open[0mNew("Retirement", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[26][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:616:		return 
[7mMissionState.Open[0mNew("ArenaDuelMission", [0m[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0msceneLevels, [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m
[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[11][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:638:		return [7mMissionState.Open[0mNew("ArenaDuel", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0m"", [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[12][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:665:		Mission obj = [7mMissionState.Open[0mNew("Battle", [0m
[7m[0mrec, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:770:		return [7mMissionState.Open[0mNew("Battle", [0m[7m[0mrec, [0m
[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:872:		return [7mMissionState.Open[0mNew("AlleyFight", [0m
[7m[0mrec, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:936:		return 
[7mMissionState.Open[0mNew("CombatWithDialogue", [0m[7m[0mrec, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1029:		return 
[7mMissionState.Open[0mNew("EnteringSettlementBattle", [0m[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1146:		return 
[7mMissionState.Open[0mNew("HideoutBattle", [0m[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1259:		return 
[7mMissionState.Open[0mNew("HideoutAmbushMission", [0m[7m[0mval2, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1363:		return [7mMissionState.Open[0mNew("Camp", [0m[7m[0mval, [0m
[7m[0m(InitializeMissionBehaviorsDelegate)obj, [0m[7m[0mtrue, [0m[7m[0mtrue);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1377:		Mission obj = 
[7mMissionState.Open[0mNew("SiegeMissionWithDeployment", [0m[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0mupgradeLevelTag, [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1525:		return 
[7mMissionState.Open[0mNew("SiegeMissionNoDeployment", [0m[7m[0mCreateSandBoxMissionInitializerRecord(scene, [0m[7m[0mupgradeLevelTag, [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1630:		return 
[7mMissionState.Open[0mNew("SiegeLordsHallFightMission", [0m[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1730:		return [7mMissionState.Open[0mNew("Conversation", [0m
[7m[0mCreateSandBoxMissionInitializerRecord(sceneName, [0m[7m[0msceneLevels, [0m[7m[0mdoNotUseLoadingScreen: [0m[7m[0mtrue, [0m[7m[0m(DecalAtlasGroup)3), [0m
[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[5][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1763:		return [7mMissionState.Open[0mNew("Conversation", [0m
[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)obj, [0m[7m[0mtrue, [0m[7m[0mfalse);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1806:		return 
[7mMissionState.Open[0mNew("DisguiseMission", [0m[7m[0mval, [0m[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m
[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[27][0m
E:\Decompiled_Bannerlord\Modules\SandBox\Sandbox\SandBoxMissions.cs:1871:		return 
[7mMissionState.Open[0mNew("SimpleMountedPlayer", [0m[7m[0mval2, [0m[7m[0m(InitializeMissionBehaviorsDelegate)obj, [0m[7m[0mtrue, [0m[7m[0mtrue);[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments\TournamentMissionStarter.cs:23:		return 
[7mMissionState.Open[0mNew("TournamentArchery", [0m[7m[0mSandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, [0m[7m[0m"", [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments\TournamentMissionStarter.cs:66:		return 
[7mMissionState.Open[0mNew("TournamentFight", [0m[7m[0mSandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, [0m[7m[0m"", [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments\TournamentMissionStarter.cs:110:		return 
[7mMissionState.Open[0mNew("TournamentHorseRace", [0m[7m[0mSandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, [0m[7m[0m"", [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\SandBox\SandBox.Tournaments\TournamentMissionStarter.cs:150:		return 
[7mMissionState.Open[0mNew("TournamentJousting", [0m[7m[0mSandBoxMissions.CreateSandBoxMissionInitializerRecord(scene, [0m[7m[0m"", [0m
[7m[0mdoNotUseLoadingScreen: [0m[7m[0mfalse, [0m[7m[0m(DecalAtlasGroup)3), [0m[7m[0m(InitializeMissionBehaviorsDelegate)delegate[0m
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\StoryModeMissions.cs:113:		return 
[7mMissionState.Open[0mNew("TrainingField", [0m[7m[0mSandBoxMissions.CreateSandBoxTrainingMissionInitializerRecord(scene, [0m
[7m[0msceneLevels, [0m[7m[0mfalse), [0m[7m[0m(InitializeMissionBehaviorsDelegate)((Mission [0m[7m[0mmission) [0m[7m[0m=> [0m
[7m[0m(IEnumerable<MissionBehavior>)(object)new [0m[7m[0mMissionBehavior[23][0m
E:\Decompiled_Bannerlord\Modules\StoryMode\Storymode.Missions\StoryModeMissions.cs:204:		return 
[7mMissionState.Open[0mNew("SneakIntoTheVillaMission", [0m[7m[0mval2, [0m[7m[0m(InitializeMissionBehaviorsDelegate)obj, [0m[7m[0mtrue, [0m[7m[0mtrue);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerlordMissions.cs:119:		
Mission mission = [7mMissionState.Open[0mNew("CustomBattle", [0m[7m[0mnew [0m[7m[0mMissionInitializerRecord(scene)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerlordMissions.cs:175:		
Mission mission = [7mMissionState.Open[0mNew("CustomSiegeBattle", [0m[7m[0mnew [0m[7m[0mMissionInitializerRecord(scene)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\BannerlordMissions.cs:267:		
return [7mMissionState.Open[0mNew("CustomBattleLordsHall", [0m[7m[0mnew [0m[7m[0mMissionInitializerRecord(scene)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:251:	
protected Mission Handle[7mOpenNew([0mstring [0m[7m[0mmissionName, [0m[7m[0mMissionInitializerRecord [0m[7m[0mrec, [0m[7m[0mInitializeMissionBehaviorsDelegate [0m
[7m[0mhandler, [0m[7m[0mbool [0m[7m[0maddDefaultMissionBehaviors, [0m[7m[0mbool [0m[7m[0mneedsMemoryCleanup)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:302:	public 
static Mission [7mOpenNew([0mstring [0m[7m[0mmissionName, [0m[7m[0mMissionInitializerRecord [0m[7m[0mrec, [0m[7m[0mInitializeMissionBehaviorsDelegate [0m[7m[0mhandler, [0m
[7m[0mbool [0m[7m[0maddDefaultMissionBehaviors [0m[7m[0m= [0m[7m[0mtrue, [0m[7m[0mbool [0m[7m[0mneedsMemoryCleanup [0m[7m[0m= [0m[7m[0mtrue)[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:310:		
MissionState missionState = Game.Current.GameStateManager.[7mCreateState<MissionState>[0m();[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:311:		Mission 
result = missionState.Handle[7mOpenNew([0mmissionName, [0m[7m[0mrec, [0m[7m[0mhandler, [0m[7m[0maddDefaultMissionBehaviors, [0m[7m[0mneedsMemoryCleanup);[0m
E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionState.cs:312:		
Game.Current.GameStateManager.[7mPushState(missionState[0m);[0m


 succeeded in 1745ms:
 279: 	private void AddBehaviorsToMission(IEnumerable<MissionBehavior> behaviors)
 280: 	{
 281: 		MissionLogic[] logicBehaviors = (from behavior in behaviors.OfType<MissionLogic>()
 282: 			where !(behavior is MissionNetwork)
 283: 			select behavior).ToArray();
 284: 		MissionBehavior[] otherBehaviors = behaviors.Where((MissionBehavior behavior) => behavior != null && !(behavior is MissionNetwork) && !(behavior is MissionLogic)).ToArray();
 285: 		MissionNetwork[] networkBehaviors = behaviors.OfType<MissionNetwork>().ToArray();
 286: 		CurrentMission.InitializeStartingBehaviors(logicBehaviors, otherBehaviors, networkBehaviors);
 287: 	}
 288: 
 289: 	protected static bool IsRecordingActive()
 290: 	{
 291: 		if (GameNetwork.IsServer)
 292: 		{
 293: 			return MultiplayerOptions.OptionType.EnableMissionRecording.GetBoolValue();
 294: 		}
 295: 		if (RecordMission)
 296: 		{
 297: 			return Game.Current.GameType.IsCoreOnlyGameMode;
 298: 		}
 299: 		return false;
 300: 	}
 301: 
 302: 	public static Mission OpenNew(string missionName, MissionInitializerRecord rec, InitializeMissionBehaviorsDelegate handler, bool addDefaultMissionBehaviors = true, bool needsMemoryCleanup = true)
 303: 	{
 304: 		Debug.Print("Opening new mission " + missionName + " " + rec.SceneLevels + ".\n");
 305: 		if (!GameNetwork.IsClientOrReplay && !GameNetwork.IsServer)
 306: 		{
 307: 			MBCommon.CurrentGameType = (IsRecordingActive() ? MBCommon.GameType.SingleRecord : MBCommon.GameType.Single);
 308: 		}
 309: 		Game.Current.OnMissionIsStarting(missionName, rec);
 310: 		MissionState missionState = Game.Current.GameStateManager.CreateState<MissionState>();
 311: 		Mission result = missionState.HandleOpenNew(missionName, rec, handler, addDefaultMissionBehaviors, needsMemoryCleanup);
 312: 		Game.Current.GameStateManager.PushState(missionState);
 313: 		return result;
 314: 	}
 315: 
 316: 	private static IEnumerable<MissionBehavior> AddDefaultMissionBehaviorsTo(Mission mission, IEnumerable<MissionBehavior> behaviors)
 317: 	{
 318: 		List<MissionBehavior> list = new List<MissionBehavior>();
 319: 		if (GameNetwork.IsSessionActive || GameNetwork.IsReplay)
 320: 		{
 321: 			list.Add(new MissionNetworkComponent());
 322: 		}
 323: 		if (IsRecordingActive() && !GameNetwork.IsReplay)
 324: 		{
 325: 			list.Add(new RecordMissionLogic());
 326: 		}

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features\\CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'TaleWorlds\\.|Mission\\.Current|Campaign\\.Current|CharacterObject\\.PlayerCharacter|ScreenManager|Input\\.|InformationManager|GauntletLayer' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features\\CareerSystem\\Abilities','Main\\Features\\CareerSystem\\UI' -Filter '*.cs' | Select-String -Pattern 'new List|new Dictionary|\\.Where\\(|\\.Select\\(|=>.*new|Action<|Func<' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features\\CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'LogInfo|LogDebug|LogWarning|DisplayMessage' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 528ms:

[32;1mPath                                                                                                   [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                   [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                  47    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ActiveBuffsAlgebra.cs                     39    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityBuffTracker.cs                7    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityBuffTracker.cs                8    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityEffectRegistry.cs            10    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   11    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityEffectExecutor.cs                 13    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs         20    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          11    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          12    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          20    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          21    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          43    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                          46    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                                54    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                                71    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerSwitchTargetVM.cs                          14    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerSwitchTargetVM.cs                          16    …


 succeeded in 533ms:

[32;1mPath                                                                                                   [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                                   [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           51    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           55    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           64    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           78    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           85    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           90    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                           92    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                          100    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                          107    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                          117    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                             71    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                             83    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                             89    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                             92    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            147    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            164    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            167    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            288    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            325    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            338    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            381    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            401    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            410    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            416    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs                            422    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs                            28    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs                            49    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                             39    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                             79    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                             87    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                            103    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                        59    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                        70    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                        74    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                        80    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                       115    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPersistenceBehavior.cs                        26    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPersistenceBehavior.cs                        61    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPersistenceBehavior.cs                       119    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                        40    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                        47    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                        83    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                        88    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                        93    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       100    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       116    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       146    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       153    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       162    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       172    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       184    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       196    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       203    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       210    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       219    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestConfigProvider.cs                       226    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestService.cs                               91    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestService.cs                               96    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestService.cs                              117    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerQuestService.cs                              145    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                   71    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                   76    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                   82    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                  100    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                  105    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerRegistry.cs                                  171    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchDialogueBehavior.cs                     83    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              29    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              43    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              49    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              57    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              63    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchService.cs                              75    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                  70    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   27    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   34    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   47    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   72    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs                   76    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs         68    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs        171    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\MutationCalculatorRegistry.cs             28    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\MutationService.cs                        79    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Mutations\MutationService.cs                        85    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                              102    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                              108    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                              224    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs              104    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs              116    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                          55    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                               182    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                               196    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                               269    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs                 41    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs                 60    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                          61    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                          66    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                          70    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                          88    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                         108    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                         115    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                         117    …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                         134    …


 succeeded in 557ms:

[32;1mPath[0m
[32;1m----                                                                                                                   [0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                                  
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs                                  
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                                    
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPassiveService.cs                                    
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPerkMissionBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerPersistenceBehavior.cs                               
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerSwitchDialogueBehavior.cs                            
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\ICareerPassiveService.cs                                   
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityActivationController.cs                   
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs                         
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityInputAdapter.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityInputAdapter.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAgentStatService.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityActivationController.cs                  
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityEffectExecutor.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityInputAdapter.cs                          
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAgentStatService.cs                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAgentStatService.cs                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IMissionTimeProvider.cs                          
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionTimeProvider.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionTimeProvider.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_Career…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Hooks\Patch27_CareerSystem\ViewModel_ExecuteCommand_Career…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomAgentApplyDamageModel.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomAgentApplyDamageModel.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomAgentApplyDamageModel.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomAgentStatCalculateModel.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomAgentStatCalculateModel.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomClanTierModel.cs                                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomClanTierModel.cs                                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomInventoryCapacityModel.cs                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomInventoryCapacityModel.cs                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomInventoryCapacityModel.cs                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomMapVisibilityModel.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomMapVisibilityModel.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Models\TaomMapVisibilityModel.cs                           
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuest.cs                                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestCampaignBehavior.cs                      
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Quests\CareerQuestSaveableTypeDefiner.cs                   
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerAbilityEffectVM.cs                                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerAbilityHudVM.cs                                   
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceGroupObjectVM.cs                            
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceGroupObjectVM.cs                            
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenGameState.cs                                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs                                       
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerSwitchTargetVM.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerSwitchTargetVM.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs                        
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                                 
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\IAbilityHudController.cs                                
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\IAbilityHudController.cs                                


exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/ICareerAbilityEffectExecutor.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -LiteralPath 'Main/Features/CareerSystem/Abilities/Executors' -Filter '*.cs' | ForEach-Object { "'$_.FullName }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -LiteralPath 'Main/Features/CareerSystem/Abilities/Executors' -Filter '*.cs' | Select-String -Pattern 'TaleWorlds|Agent|Mission|Campaign|CharacterObject|Input|ScreenManager|InformationManager' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 530ms:
   1: namespace TAOM.Features.CareerSystem.Abilities;
   2: 
   3: public interface ICareerAbilityEffectExecutor
   4: {
   5:     string CareerId { get; }
   6:     void Execute(IAbilityExecutionContext context);
   7: }

 succeeded in 530ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\CavalryAbilityExecutor.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\InfantryAbilityExecutor.cs
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\RangedAbilityExecutor.cs

 succeeded in 710ms:
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -LiteralPath 'Main/Features/CareerSystem/Abilities/Executors' -Filter '*.cs' | ForEach-Object { "'$path=$_.FullName; $n=1; Get-Content -LiteralPath $path | ForEach-Object { if ($_ -match '"'Execute|Apply|Duration|Radius') { '{0}:{1}: {2}' -f "'$path, $n, $_ }; $n++ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/CareerAbilityBuffTracker.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/IAbilityExecutionContext.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 535ms:
   1: using System.Collections.Generic;
   2: 
   3: namespace TAOM.Features.CareerSystem.Abilities;
   4: 
   5: public static class CareerAbilityBuffTracker
   6: {
   7:     private static readonly Dictionary<string, ActiveBuffs> _buffs = new Dictionary<string, ActiveBuffs>();
   8:     private static readonly Dictionary<int, ActiveBuffs> _allyBuffs = new Dictionary<int, ActiveBuffs>();
   9: 
  10:     public static void SetBuff(string heroId, ActiveBuffs buffs) => _buffs[heroId] = buffs;
  11:     public static ActiveBuffs GetBuff(string heroId) => _buffs.TryGetValue(heroId, out var b) ? b : null;
  12:     public static void ClearBuff(string heroId) => _buffs.Remove(heroId);
  13: 
  14:     public static void SetAllyBuff(int agentIndex, ActiveBuffs buffs) => _allyBuffs[agentIndex] = buffs;
  15:     public static ActiveBuffs GetAllyBuff(int agentIndex) => _allyBuffs.TryGetValue(agentIndex, out var b) ? b : null;
  16:     public static void ClearAllyBuff(int agentIndex) => _allyBuffs.Remove(agentIndex);
  17:     public static void ClearAllAllyBuffs() => _allyBuffs.Clear();
  18: 
  19:     public static void ClearAll()
  20:     {
  21:         _buffs.Clear();
  22:         _allyBuffs.Clear();
  23:     }
  24: }
  25: 
  26: public class ActiveBuffs
  27: {
  28:     public float SpeedMultiplier { get; set; } = 0f;
  29:     public float CombatSpeedMultiplier { get; set; } = 0f;
  30:     public float DamageBonus { get; set; } = 0f;
  31:     public float ArmorReduction { get; set; } = 0f;
  32:     public float DrawSpeedBonus { get; set; } = 0f;
  33:     public float MountSpeedBonus { get; set; } = 0f;
  34:     public float ChargeDamageBonus { get; set; } = 0f;
  35:     public float DamageReductionBonus { get; set; } = 0f;
  36:     public float ExpiresAt { get; set; }
  37:     public bool IsExpired(float currentTime) => currentTime >= ExpiresAt;
  38: }

 succeeded in 559ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\CavalryAbilityExecutor.cs:15:     public void Execute(IAbilityExecutionContext context)
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\CavalryAbilityExecutor.cs:21:         context.ApplyAllyCavalryBuff(
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\CavalryAbilityExecutor.cs:25:             radius: context.Radius,
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\CavalryAbilityExecutor.cs:26:             duration: context.Duration);
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\InfantryAbilityExecutor.cs:15:     public void Execute(IAbilityExecutionContext context)
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\InfantryAbilityExecutor.cs:19:         // Use context.Radius (from mutated ability template) so choice-tree radius upgrades apply
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\InfantryAbilityExecutor.cs:20:         context.ApplyAllyBuff(tuning.DamageBonus / 100f, tuning.DamageReduction / 100f, context.Radius, context.Duration);
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\RangedAbilityExecutor.cs:15:     public void Execute(IAbilityExecutionContext context)
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\RangedAbilityExecutor.cs:21:         context.ApplyAllyRangedBuff(
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\RangedAbilityExecutor.cs:25:             radius: context.Radius,
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\Executors\RangedAbilityExecutor.cs:26:             duration: context.Duration);

 succeeded in 767ms:
   1: namespace TAOM.Features.CareerSystem.Abilities;
   2: 
   3: public interface IAbilityExecutionContext
   4: {
   5:     string HeroStringId { get; }
   6:     float Duration { get; }
   7:     float Radius { get; }
   8: 
   9:     void ApplyMoraleBurst(float radius, float magnitude);
  10:     void ApplyStealthMode(float duration);
  11:     void ApplyAllyBuff(float damageBonusFlat, float damageReductionFlat, float radius, float duration);
  12:     void ApplyAllyRangedBuff(float speedBonus, float damageBonus, float drawSpeedBonus, float radius, float duration);
  13:     void ApplyAllyCavalryBuff(float mountSpeedBonus, float chargeDamageBonus, float damageBonus, float radius, float duration);
  14:     void PlaySound(string soundId);
  15:     void PlayParticle(string particleId);
  16: }

codex
The context ownership is player-centered: `MissionAbilityExecutionContext` stores the main agent and schedules restore callbacks for ally buffs; `OnAgentDeleted` clears only the ally buff dictionary entry, while the context remains needed to subtract hero and surviving ally deltas. That part looks intentional.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/Abilities/CareerAbility.cs' | ForEach-Object { '{0,4}: {1}' -f "'$i++, $_ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features\\CareerSystem' -Recurse -Filter '*.cs' | Select-String -Pattern 'SetCareer|SwitchCareer|AssignCareer|_career|career' | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 473ms:
   1: using System;
   2: using TAOM.Features.CareerSystem.Domain;
   3: 
   4: namespace TAOM.Features.CareerSystem.Abilities;
   5: 
   6: public class CareerAbility
   7: {
   8:     public string TemplateId { get; }
   9:     public ChargeType ChargeType { get; }
  10:     public float MaxCharge { get; private set; }
  11:     public float CurrentCharge { get; private set; }
  12:     public float CooldownDuration { get; }
  13:     public float CooldownRemaining { get; private set; }
  14: 
  15:     public bool IsOnCooldown => CooldownRemaining > 0f;
  16:     public bool IsReady => ChargeType == ChargeType.CooldownOnly
  17:         ? !IsOnCooldown
  18:         : CurrentCharge >= MaxCharge;
  19: 
  20:     public float ReadyProgress01 => ChargeType == ChargeType.CooldownOnly
  21:         ? (CooldownDuration > 0f ? (CooldownDuration - CooldownRemaining) / CooldownDuration : 1f)
  22:         : (MaxCharge > 0f ? CurrentCharge / MaxCharge : 0f);
  23: 
  24:     public CareerAbility(string templateId, ChargeType chargeType, float maxCharge, float cooldownDuration)
  25:     {
  26:         TemplateId = templateId;
  27:         ChargeType = chargeType;
  28:         MaxCharge = maxCharge;
  29:         CooldownDuration = cooldownDuration;
  30:     }
  31: 
  32:     public void AddCharge(float amount, ChargeType sourceType)
  33:     {
  34:         if (ChargeType == ChargeType.CooldownOnly) return;
  35:         if (ChargeType != ChargeType.Custom && ChargeType != sourceType) return;
  36: 
  37:         CurrentCharge = Math.Min(CurrentCharge + amount, MaxCharge);
  38:     }
  39: 
  40:     public void Activate()
  41:     {
  42:         if (ChargeType == ChargeType.CooldownOnly)
  43:             CooldownRemaining = CooldownDuration;
  44:         else
  45:             CurrentCharge = 0f;
  46:     }
  47: 
  48:     public void Tick(float dt)
  49:     {
  50:         if (CooldownRemaining > 0f)
  51:             CooldownRemaining = Math.Max(0f, CooldownRemaining - dt);
  52:     }
  53: 
  54:     public void SetMaxCharge(float newMax)
  55:     {
  56:         MaxCharge = newMax;
  57:         if (CurrentCharge > MaxCharge)
  58:             CurrentCharge = MaxCharge;
  59:     }
  60: }

 succeeded in 480ms:

[32;1mPath                                                                                 [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                                 [0m [32;1m----------[0m [32;1m----[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs          2 using TAOM.Features.C…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs          4 namespace TAOM.Featur…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs          6 public sealed class C…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs          8     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs         10     public CareerArch…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs         12         _careerArchet…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs         15     public bool TryGe…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs         17         if (string.Is…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerArchetypeService.cs         22         return _caree…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs          4 using TAOM.Features.C…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs          6 namespace TAOM.Featur…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs          8 public class CareerCa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         10     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         11     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         12     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         13     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         14     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         17     public CareerCamp…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         18         ICareerDataSe…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         19         ICareerRegist…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         20         ICareerPassiv…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         21         ICareerCreati…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         22         ICareerAbilit…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         48         // player), s…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         51         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         55             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         59         // Legacy sav…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         60         // (New games…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         61         if (!_dataSer…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         64             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         67                 forea…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         70                     f…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         77                      …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         78                      …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         85         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         88         var careerId …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         89         if (careerId …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         90             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         92             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs         98         if (!_dataSer…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs        100             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs        107             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs        115         if (_dataServ…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs        117             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCampaignBehavior.cs        118             _dataServ…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs            8 using TAOM.Features.C…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           10 namespace TAOM.Featur…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           12 public class CareerCo…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           17     private List<Care…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           18     private List<Care…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           19     private List<Care…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           24     public CareerConf…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           30     public IReadOnlyL…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           33         return _caree…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           36     public IReadOnlyL…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           42     public IReadOnlyL…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           69         if (_careers …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           71         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           73         _careers = ne…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           74         _groups = new…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           75         _choices = ne…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           78         LoadCareersXm…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           83         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           86     private void Load…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           88         var path = Pa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           89         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs           92             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          104             foreach (…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          132                     v…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          146                     _…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          147                     _…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          151                     _…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          157             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          163         var path = Pa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          164         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          167             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          201                     v…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          203                      …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          212                     _…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          218             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          222     private CareerCho…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          267             return ne…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          278             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          285         var path = Pa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          288             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          321                     _…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          325             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          329             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          335         var path = Pa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          338             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          381             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          385             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          401             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          406         // (NaN compa…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          410             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          416             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          422             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerConfigProvider.cs          442         // (Career #3…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs           3 namespace TAOM.Featur…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs           5 public class CareerCr…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs           7     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs           8     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs           9     private readonly …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          12     public CareerCrea…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          13         ICareerDataSe…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          14         ICareerRegist…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          15         ICareerPassiv…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          24     public void OnCar…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          26         if (string.Is…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          28             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          32         var career = …
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          33         if (career ==…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          35             _logger.L…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          39         _dataService.…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          42         if (!string.I…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          45             _dataServ…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerCreationHandler.cs          49         _logger.LogIn…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerDataService.cs               2 using TAOM.Features.C…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerDataService.cs               4 namespace TAOM.Featur…
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\CareerDataService.cs               6 public class CareerDa…


codex
I found no new per-frame reflection or hot-path allocation beyond the intended per-tick controller calls. The activation controller remains pure and only depends on `ICareerAbilityService`, `IAbilityInputAdapter`, and `IMissionTimeProvider`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "if (Test-Path -LiteralPath 'docs/reviews/REVIEW-GUIDE.md') { "'$i=1; Get-Content -LiteralPath '"'docs/reviews/REVIEW-GUIDE.md' | ForEach-Object { if ("'$i -le 220) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ } }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'docs\\reviews' -Filter '*career*102*' | Select-Object Name,Length,LastWriteTime" in C:\Users\mikew\source\repos\TAOM
 succeeded in 517ms:
   1: # Codex Adversarial Review Guide
   2: 
   3: How to write effective prompts, what to verify, and what we've learned.
   4: 
   5: ## Status: Full codebase review complete (2026-04-05/06)
   6: 
   7: **25/25 features reviewed** across 16 Codex reviews and 5 waves. 41 bugs found, 37 fixed, 4 deferred. Prompt evolved v1→v6 with accuracy improving from 33% to 81%.
   8: 
   9: | Metric | v1 (start) | v6 (final) |
  10: |--------|-----------|------------|
  11: | Codex accuracy | 33% | 81% |
  12: | False positive rate | 50% | 9% |
  13: | Miss rate | 75% | 15% |
  14: | Prompt iterations | 1 | 6 |
  15: 
  16: ## Process Overview
  17: 
  18: ```
  19: 1. Choose feature to review (highest risk-per-line-of-code)
  20: 2. Gather file list + identify vanilla targets to decompile
  21: 3. Write prompt using template below
  22: 4. Dispatch: /codex:adversarial-review --background
  23: 5. Retrieve: /codex:result
  24: 6. Claude critically reviews Codex output against actual source
  25: 7. Implement confirmed fixes
  26: 8. Log results in REVIEW-LOG.md
  27: ```
  28: 
  29: **Critical rule:** Claude ALWAYS reviews the Codex output. Codex findings are hypotheses, not facts. Every finding must be verified against source code before implementing.
  30: 
  31: ---
  32: 
  33: ## Repeatable Process
  34: 
  35: ### End-to-end workflow:
  36: 
  37: ```
  38: Step 1: WRITE PROMPT (Claude Code or manual)
  39:   Use the v6 template below. Customize sections for the feature.
  40:   For features with prior internal review, add Known Suspects section.
  41: 
  42: Step 2: DISPATCH TO CODEX (terminal -- Codex is a separate CLI tool)
  43:   Option A: Copy prompt, run in Codex CLI terminal
  44:   Option B: /codex:adversarial-review --background (via codex-plugin-cc)
  45:   Codex writes output to: docs/reviews/codex-adversarial-{feature}-{date}.md
  46: 
  47: Step 3: VERIFY OUTPUT (Claude Code)
  48:   /review-codex docs/reviews/codex-adversarial-{feature}-{date}.md
  49:   The skill reads the review, verifies every finding against source code,
  50:   implements confirmed fixes, and updates REVIEW-LOG.md.
  51: ```
  52: 
  53: **Key:** Steps 1 and 2 are manual (you write and dispatch). Step 3 is the `/review-codex` skill which encapsulates ALL lessons from 18 reviews into a repeatable verification workflow. Any new Claude Code session can invoke it without needing prior context.
  54: 
  55: ## Advanced Pattern: Known Suspects
  56: 
  57: For features where you've already done internal review (e.g., `/deep-review`), add a "Known Suspects" section to the Codex prompt. This forces Codex to CONFIRM or DISPUTE specific hypotheses with evidence, rather than finding its own surface-level issues.
  58: 
  59: Format in the prompt:
  60: ```
  61: === KNOWN SUSPECTS (confirm or dispute each with evidence) ===
  62: 1. [TITLE]: [hypothesis]. Read [specific file] to confirm.
  63: 2. [TITLE]: [hypothesis]. Read [specific file] to confirm.
  64: ```
  65: 
  66: Format in the expected output:
  67: ```
  68: ## KNOWN SUSPECTS VERDICT
  69: 1. [TITLE]: CONFIRMED -- [file:line evidence] or DISPUTED -- [counter-evidence]
  70: ```
  71: 
  72: Quality gate: add "Section N skips any suspect or says 'could not verify'" to enforce engagement.
  73: 
  74: This pattern produced the highest-quality Codex output in our review history because it forces deep reading of specific code paths instead of surface scanning.
  75: 
  76: ## Prompt Formatting Note
  77: 
  78: **Avoid indented continuation lines** in prompts sent via `/codex:adversarial-review`. Leading whitespace gets backslash-escaped, triggering a confirmation prompt. Use flat formatting:
  79: - No leading spaces on lines inside sections
  80: - Use `--` or blank lines as visual separators instead of indentation
  81: - Lists use `a)` `b)` `c)` at the start of the line, not indented under a header
  82: 
  83: ## Prompt Template (v6)
  84: 
  85: ```
  86: /codex:adversarial-review --background
  87: 
  88: Adversarial review of {FeatureName}.
  89: 
  90: {1-2 sentences: what the feature does, its risk profile, what's already good}
  91: 
  92: TAOM ID CHEATSHEET (prevents false positives from ID confusion):
  93: Kingdom StringIds: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
  94: Culture StringIds (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
  95: Culture StringIds (XSLT/vanilla): vlandia (Rohan), empire (Dunland), empire_s (Mordor-region), empire_w (Gondor-region), battania (Dunland-alt), aserai (Harad), khuzait (Easterlings), sturgia (Dale)
  96: NOTE: Kingdom IDs and Culture IDs differ! "rohan" is NOT a valid ID. Rohan's kingdom=vlandia, culture=vlandia. Config keys must use the runtime StringId.
  97: 
  98: READ FIRST (required context):
  99: - docs/features/{feature-name}.md
 100: - {any config files: JSON, XML}
 101: 
 102: FILES (service — business logic):
 103: {list service files}
 104: 
 105: FILES (entry points — thin, delegate to service):
 106: {list GameModel and Harmony patch files}
 107: 
 108: FILES (config):
 109: {list JSON/XML config files}
 110: 
 111: FILES (tests):
 112: {list test files with count and coverage note}
 113: 
 114: === REQUIRED SECTIONS (missing section = incomplete review) ===
 115: 
 116: SECTION 1: VANILLA CODE
 117: Read these files from E:\Decompiled_Bannerlord\ and paste the relevant
 118: methods into your output as ``` code blocks:
 119:   - Find {VanillaClass}.cs in {Modules|Campaign|Core}/ — paste {MethodName}()
 120:   - Find {VanillaClass2}.cs — paste {MethodName2}()
 121: 
 122: This section MUST contain ``` code blocks with decompiled C#. Prose
 123: descriptions of vanilla behavior are NOT sufficient — prior reviews
 124: described vanilla behavior without reading the code and produced false
 125: positives. If you cannot find the file, say so explicitly.
 126: 
 127: SECTION 2: VANILLA ANALYSIS
 128: Using the code from Section 1, answer:
 129:   a) {specific question about vanilla behavior}
 130:   b) {specific question about how TAOM interacts with vanilla}
 131: Reference specific line numbers from the code you pasted.
 132: 
 133: SECTION 3: {FEATURE-SPECIFIC DEEP ANALYSIS}
 134: {Concrete scenarios with expected outputs, math walkthroughs, or
 135:  IL verification — whatever is the highest-risk area for this feature}
 136:   a) {specific scenario with numbers — show the formula step by step}
 137:   b) {specific scenario with numbers}
 138:   c) {edge case}
 139: 
 140: SECTION 4: CONFIG CROSS-REFERENCE (required for any config-driven feature)
 141: a) List every string ID key in the config file(s)
 142: b) Cross-reference each against the source-of-truth file. Specify the file:
 143: -- Culture IDs: check against taom_spcultures.xml + spcultures.xslt
 144: -- Kingdom IDs: check against TAOM_spkingdoms.xml
 145: -- Settlement IDs: check against Main/_Module/ModuleData/settlements.xml
 146: -- Troop IDs: check against troops/troops_{culture}.xml
 147: Do NOT claim "config looks valid" without showing which file you checked.
 148: c) Check for DEAD CONFIG -- values that exist in config but are never read at runtime. Search for the config key in the C# codebase. If no code loads or uses a config field, it is dead.
 149: 
 150: SECTION 5: FINDINGS OR OBSERVATIONS
 151: If bugs found — each finding MUST include:
 152:   - TAOM code (file:line)
 153:   - Vanilla code (quote from Section 1)
 154:   - Evidence of divergence
 155:   - Severity: CRITICAL / HIGH / MEDIUM / LOW
 156: 
 157: If approve verdict — you MUST still provide an OBSERVATIONS subsection
 158: listing things worth noting even if not bugs (e.g., high multiplier
 159: magnitudes, silent filtering, exception swallowing, design tradeoffs).
 160: An approve with zero observations suggests shallow analysis.
 161: 
 162: === QUALITY GATES ===
 163: 
 164: Your review is INCOMPLETE if:
 165:   - Section 1 contains no ``` code blocks with decompiled C#
 166:   - Section 4 claims validity without showing cross-reference evidence
 167:   - All findings are the same severity (vary your calibration)
 168:   - Section 5 has no observations on an approve verdict
 169:   - A finding claims "this is wrong" without checking feature docs for
 170:     design intent (Wave 1 produced a false positive from misreading
 171:     kingdom mapping — always check docs/features/ before flagging)
 172: 
 173: Lessons from prior reviews:
 174: SUCCESSES to repeat:
 175: - Config ID cross-reference caught "rohan"/"dol_guldur" mismatches (BattleBalance)
 176: - Vanilla reimplementation diff caught fertility formula drift (RaceAge)
 177: - Garrison wage gate found by comparing TAOM vs vanilla IsGarrison (TroopProgression)
 178: FAILURES to avoid:
 179: - Codex assumed empire=Rohan (it is Dunland). Use the ID cheatsheet above.
 180: - Codex skipped transpiler IL verification despite being focus #1 (BannerColor)
 181: - Codex flagged characterObject.IsMounted as bug -- vanilla uses same check (CulturalFeats)
 182: 
 183: DO NOT flag architecture/pattern compliance {if feature is well-architected}.
 184: 
 185: Output to: docs/reviews/codex-adversarial-{feature}-{date}.md
 186: ```
 187: 
 188: ---
 189: 
 190: ## Prompt Design Principles
 191: 
 192: ### What makes Codex find real bugs vs. surface noise
 193: 
 194: | Principle | Why | Example |
 195: |-----------|-----|---------|
 196: | **Point to E:\Decompiled_Bannerlord\** | Codex has pre-decompiled source but won't use it unless told | "Find DefaultPartySpeedCalculatingModel.cs in Modules/" |
 197: | **Require vanilla code in output** | Forces Codex to actually read vanilla, not guess | "Include the decompiled C# in your output" |
 198: | **Give concrete scenarios** | Forces math walkthrough, catches formula bugs | "Mordor army besieging town_EW3, position 0 of 4, show the multiplier" |
 199: | **Name the required sections** | Codex skips hard sections silently; named sections make gaps visible | "REQUIRED SECTIONS (missing = incomplete)" |
 200: | **Reference feature docs** | Codex needs design intent to distinguish bugs from features | "READ FIRST: docs/features/army-targeting.md" |
 201: | **Say what's already good** | Prevents Codex from filling the review with easy pattern violations | "100% test coverage, proper service/adapter separation" |
 202: | **Include prior failure examples** | Concrete failures are stronger than abstract "DO NOT" rules | "Codex called X a bug but vanilla uses the same pattern" |
 203: | **Vary severity explicitly** | Codex defaults to everything-is-HIGH | "If everything is HIGH, your calibration is off" |
 204: | **Require verification artifacts** | Codex describes instead of showing; prose is unfalsifiable | "Your output MUST contain ``` code blocks with decompiled C#" |
 205: | **Separate "show" from "analyze"** | Codex skips showing code if it can jump to conclusions | "Step A: paste the code. Step B: answer questions about it." |
 206: | **Require observations on approve** | Clean verdicts need evidence of depth, not just absence of findings | "OBSERVATIONS section required even for approve verdicts" |
 207: 
 208: ### What wastes Codex's time
 209: 
 210: | Anti-pattern | Why it fails |
 211: |-------------|-------------|
 212: | "Check for ADR violations" on a well-architected feature | Codex finds pattern violations and inflates them to CRITICAL |
 213: | "Decompile X" without pointing to E:\Decompiled_Bannerlord\ | Codex often skips decompilation entirely |
 214: | Generic focus areas ("null handling", "thread safety") | Gets generic answers; Codex checks superficially |
 215: | No feature documentation reference | Codex can't distinguish design intent from bugs |
 216: | Same severity guidance as AGENTS.md defaults | AGENTS.md rates ADR violations as CRITICAL; prompt needs to override for mature features |
 217: | "Include decompiled code" without structural enforcement | Codex ignored this instruction in 3/3 reviews — words alone don't work |
 218: | No config cross-reference file path | "Validate config" without "against settlements.xml" lets Codex claim validity without checking |
 219: 
 220: ---

 succeeded in 527ms:

[32;1mName                                                      [0m[32;1m Length[0m[32;1m LastWriteTime[0m
[32;1m----                                                      [0m [32;1m------[0m [32;1m-------------[0m
codex-adversarial-career-102-refactor-2026-06-02.output.md  95627 6/2/2026 4:33:07 PM
codex-adversarial-career-102-refactor-2026-06-02.prompt.md  11259 6/2/2026 4:32:07 PM


codex
One remaining lifecycle edge is removal without `OnEndMission`. Vanilla exposes `MissionBehavior.OnRemoveBehavior()`, so I’m checking whether normal mission finalization invokes it or whether the explicit `OnEndMission` reset is the only cleanup path for these singleton controllers.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' -Pattern 'OnRemoveBehavior|RemoveBehavior|MissionBehaviors\\.Clear|ClearBehaviors|FinalizeMission|OnMissionStateFinalize' -Context 2,5" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -Path 'Main\\**\\*.cs' -Pattern 'OnRemoveBehavior|RemoveBehavior' -ErrorAction SilentlyContinue | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionBehavior.cs' | ForEach-Object { if ("'$i -ge 130 -and $i -le 145) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 519ms:

  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1590:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1591:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1592:	private 
void [7mFinalizeMission[0m()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1593:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1594:	[0m[7m	[0m
[7m[0mTeamAISiegeComponent.OnMissionFinalize();[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1595:		
MBAPI.IMBMission.[7mFinalizeMission[0m(Pointer);[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1596:	[0m[7m	[0m[7m[0mPointer [0m[7m[0m= [0m
[7m[0mUIntPtr.Zero;[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1597:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1598:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1599:	[0m[7m[0mpublic [0m
[7m[0mvoid [0m[7m[0mSetMissionCombatType(MissionCombatType [0m[7m[0mmissionCombatType)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:1600:	{[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2201:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2202:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2203:	public 
void [7mOnMissionStateFinalize[0m(bool [0m[7m[0mforceClearGPUResources)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2204:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2205:	[0m[7m	[0m[7m[0mforeach [0m
[7m[0m(MissionBehavior [0m[7m[0mmissionBehavior [0m[7m[0min [0m[7m[0mMissionBehaviors)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2206:		{[0m
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2207:			
missionBehavior.[7mOnMissionStateFinalize[0md();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2208:		}[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2209:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(GameNetwork.IsSessionActive [0m[7m[0m&& [0m[7m[0mGetMissionBehavior<MissionNetworkComponent>() [0m[7m[0m!= [0m[7m[0mnull)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2210:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2211:	[0m[7m	[0m[7m	[0m
[7m[0mRemoveMissionBehavior(GetMissionBehavior<MissionNetworkComponent>());[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:2212:		}[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4590:		
CurrentState = State.Over;
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4591:		
FreeResources();
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4592:		
[7mFinalizeMission[0m();[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4593:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4594:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4595:	[0m[7m[0mprivate [0m
[7m[0mvoid [0m[7m[0mStopSoundEvents()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4596:	{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4597:	[0m[7m	[0m[7m[0mif [0m
[7m[0m(_ambientSoundEvent [0m[7m[0m!= [0m[7m[0mnull)[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4631:	public 
void RemoveMissionBehavior(MissionBehavior missionBehavior)
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4632:	{
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4633:		
missionBehavior.[7mOnRemoveBehavior[0m();[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4634:	[0m[7m	[0m[7m[0mswitch [0m
[7m[0m(missionBehavior.BehaviorType)[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4635:		{[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4636:	[0m[7m	[0m[7m[0mcase [0m
[7m[0mMissionBehaviorType.Logic:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4637:	[0m[7m	[0m[7m	[0m
[7m[0mMissionLogics.Remove(missionBehavior [0m[7m[0mas [0m[7m[0mMissionLogic);[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\Mission.cs:4638:			break;[0m


 succeeded in 480ms:
 succeeded in 480ms:
 130: 	public virtual void OnRemoveBehavior()
 131: 	{
 132: 	}
 133: 
 134: 	public virtual void OnFixedMissionTick(float fixedDt)
 135: 	{
 136: 	}
 137: 
 138: 	public virtual void OnPreMissionTick(float dt)
 139: 	{
 140: 	}
 141: 
 142: 	public virtual void OnPreDisplayMissionTick(float dt)
 143: 	{
 144: 	}
 145: 

exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' | ForEach-Object { if ("'$i -ge 2203 -and $i -le 2225) { '"'{0,5}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Select-String -LiteralPath 'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\MissionBehavior.cs' -Pattern 'OnMissionStateFinalized|OnAfterMissionCreated|Mission =' -Context 2,4" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'E:\\Decompiled_Bannerlord\\MountAndBlade\\TaleWorlds.MountAndBlade\\TaleWorlds.MountAndBlade\\Mission.cs' | ForEach-Object { if ("'$i -ge 4628 -and $i -le 4650) { '"'{0,5}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 580ms:
 2203: 	public void OnMissionStateFinalize(bool forceClearGPUResources)
 2204: 	{
 2205: 		foreach (MissionBehavior missionBehavior in MissionBehaviors)
 2206: 		{
 2207: 			missionBehavior.OnMissionStateFinalized();
 2208: 		}
 2209: 		if (GameNetwork.IsSessionActive && GetMissionBehavior<MissionNetworkComponent>() != null)
 2210: 		{
 2211: 			RemoveMissionBehavior(GetMissionBehavior<MissionNetworkComponent>());
 2212: 		}
 2213: 		for (int num = MissionBehaviors.Count - 1; num >= 0; num--)
 2214: 		{
 2215: 			RemoveMissionBehavior(MissionBehaviors[num]);
 2216: 		}
 2217: 		_deploymentPlan = null;
 2218: 		MissionLogics.Clear();
 2219: 		Scene = null;
 2220: 		Current = null;
 2221: 		ClearUnreferencedResources(forceClearGPUResources);
 2222: 	}
 2223: 
 2224: 	public void ClearUnreferencedResources(bool forceClearGPUResources)
 2225: 	{

 succeeded in 840ms:

  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:15:	
public abstract MissionBehaviorType BehaviorType { get; }
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:16:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:17:	
public virtual void [7mOnAfterMissionCreated[0m()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:18:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:19:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:20:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:21:	[0m
[7m[0mpublic [0m[7m[0mvirtual [0m[7m[0mvoid [0m[7m[0mOnBehaviorInitialize()[0m
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:181:	}
  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:182:
> E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:183:	
public virtual void [7mOnMissionStateFinalized[0m()[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:184:	{[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:185:	}[0m
[7m[0m  E:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:186:[0m
[7m [0m[7m [0m[7m[0mE:\Decompiled_Bannerlord\MountAndBlade\TaleWorlds.MountAndBlade\TaleWorlds.MountAndBlade\MissionBehavior.cs:187:	[0m
[7m[0mpublic [0m[7m[0mvirtual [0m[7m[0mvoid [0m[7m[0mOnMissionStateDeactivated()[0m


 succeeded in 1070ms:
 4628: 		return null;
 4629: 	}
 4630: 
 4631: 	public void RemoveMissionBehavior(MissionBehavior missionBehavior)
 4632: 	{
 4633: 		missionBehavior.OnRemoveBehavior();
 4634: 		switch (missionBehavior.BehaviorType)
 4635: 		{
 4636: 		case MissionBehaviorType.Logic:
 4637: 			MissionLogics.Remove(missionBehavior as MissionLogic);
 4638: 			break;
 4639: 		case MissionBehaviorType.Other:
 4640: 			_otherMissionBehaviors.Remove(missionBehavior);
 4641: 			break;
 4642: 		default:
 4643: 			TaleWorlds.Library.Debug.FailedAssert("Invalid behavior type", "C:\\BuildAgent\\work\\mb3\\Source\\Bannerlord\\TaleWorlds.MountAndBlade\\Missions\\Mission.cs", "RemoveMissionBehavior", 4351);
 4644: 			break;
 4645: 		}
 4646: 		MissionBehaviors.Remove(missionBehavior);
 4647: 		missionBehavior.Mission = null;
 4648: 	}
 4649: 
 4650: 	public void JoinEnemyTeam()

codex
No TAOM `OnRemoveBehavior` override exists for this behavior. Vanilla normal finalization does remove behaviors after `OnEndMission`, but the singleton cleanup depends on the `OnEndMission` path, not `OnRemoveBehavior`. I’m treating that as acceptable for normal mission lifecycle unless I find a concrete skip path.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command 'dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main\\Features\\CareerSystem\\Abilities','Main\\Features\\CareerSystem\\UI' -Filter '*.cs' | ForEach-Object { "'$lines = (Get-Content -LiteralPath $_.FullName).Count; '"'{0}: {1}' -f "'$_.FullName, $lines }' in C:\Users\mikew\source\repos\TAOM
2026-06-02T21:37:58.208069Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.300.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 440ms:
Failed to add 'C:\Users\CodexSandboxOffline\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\Users\CodexSandboxOffline\.dotnet\10.0.300.toolpath.sentinel' is denied.
   at Microsoft.Win32.SafeHandles.SafeFileHandle.CreateFile(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options)
   at Microsoft.Win32.SafeHandles.SafeFileHandle.Open(String fullPath, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.OSFileStreamStrategy..ctor(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.Strategies.FileStreamHelpers.ChooseStrategyCore(String path, FileMode mode, FileAccess access, FileShare share, FileOptions options, Int64 preallocationSize, Nullable`1 unixCreateMode)
   at System.IO.File.Create(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.FileWrapper.CreateEmptyFile(String path)
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 succeeded in 471ms:
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityActivationController.cs: 75
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityEffectExecutor.cs: 90
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\AbilityInputAdapter.cs: 8
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ActiveBuffsAlgebra.cs: 50
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbility.cs: 60
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityBuffTracker.cs: 38
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityEffectRegistry.cs: 27
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAbilityService.cs: 84
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\CareerAgentStatService.cs: 129
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityActivationController.cs: 23
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityEffectExecutor.cs: 14
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityExecutionContext.cs: 16
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IAbilityInputAdapter.cs: 9
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAbilityEffectExecutor.cs: 7
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAbilityService.cs: 13
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\ICareerAgentStatService.cs: 48
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\IMissionTimeProvider.cs: 8
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionAbilityExecutionContext.cs: 208
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\Abilities\MissionTimeProvider.cs: 8
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs: 115
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerAbilityEffectVM.cs: 27
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerAbilityHudVM.cs: 93
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerButtonPrefab.cs: 33
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceGroupObjectVM.cs: 171
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerChoiceObjectVM.cs: 107
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenGameState.cs: 13
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerScreenVM.cs: 578
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CareerSwitchTargetVM.cs: 52
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\CharacterDeveloperCareerMixin.cs: 69
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs: 155
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\IAbilityHudController.cs: 18

codex
The first test run failed before build/test execution because `dotnet` tried to create a first-run sentinel under `C:\Users\CodexSandboxOffline\.dotnet`, which is outside the writable sandbox. I’ll rerun with `DOTNET_CLI_HOME` pointed at `C:\tmp`.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$env:DOTNET_CLI_HOME='"'C:\\tmp\\dotnet-cli-home'; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-02T21:38:04.797977Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.4 seconds
Output:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

 exited 1 in 414ms:
Failed to add 'C:\tmp\dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
System.UnauthorizedAccessException: Access to the path 'C:\tmp\dotnet-cli-home\.dotnet' is denied.
   at System.IO.FileSystem.CreateDirectory(String fullPath, Byte[] securityDescriptor)
   at System.IO.Directory.CreateDirectory(String path)
   at Microsoft.Extensions.EnvironmentAbstractions.DirectoryWrapper.CreateDirectory(String path)
   at Microsoft.DotNet.Configurer.FileSystemExtensions.<>c__DisplayClass0_0.<CreateIfNotExists>b__0()
   at Microsoft.DotNet.Cli.Utils.FileAccessRetrier.RetryOnIOException(Action action)
   at Microsoft.DotNet.Configurer.DotnetFirstTimeUseConfigurer.Configure()
   at Microsoft.DotNet.Cli.Program.ConfigureDotNetForFirstTimeUse(IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel, IAspNetCertificateSentinel aspNetCertificateSentinel, IFileSentinel toolPathSentinel, Boolean isDotnetBeingInvokedFromNativeInstaller, DotnetFirstRunConfiguration dotnetFirstRunConfiguration, IEnvironmentProvider environmentProvider, Dictionary`2 performanceMeasurements, Boolean skipFirstTimeUseCheck)
   at Microsoft.DotNet.Cli.Program.ProcessArgs(String[] args, TimeSpan startupTime)
   at Microsoft.DotNet.Cli.Program.Main(String[] args)

codex
`C:\tmp` also rejected directory creation in this sandbox, so I’m moving the CLI home into the repo workspace for the verification run.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "New-Item -ItemType Directory -Force -Path '.dotnet-cli-home' | Out-Null; "'$env:DOTNET_CLI_HOME=(Resolve-Path '"'.dotnet-cli-home').Path; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-02T21:38:10.386148Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.300\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

 exited 1 in 731ms:
Failed to add 'C:\Users\mikew\source\repos\TAOM\.dotnet-cli-home\.dotnet\tools' to the PATH environment variable. Add this directory to your PATH to use tools installed with 'dotnet tool install'.
C:\Program Files\dotnet\sdk\10.0.300\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

codex
The dotnet command now reaches MSBuild, but MSBuild’s Windows SDK lookup is trying to read `C:\Users\mikew\AppData\Local\Microsoft SDKs`, which the sandbox denies. I’ll avoid burning time on environment workarounds and report the verification limitation unless a local setting bypasses it cleanly.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "New-Item -ItemType Directory -Force -Path '.dotnet-localappdata' | Out-Null; "'$env:DOTNET_CLI_HOME=(Resolve-Path '"'.dotnet-cli-home').Path; "'$env:LOCALAPPDATA=(Resolve-Path '"'.dotnet-localappdata').Path; "'$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE='"'1'; dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore" in C:\Users\mikew\source\repos\TAOM
2026-06-02T21:38:19.174579Z ERROR codex_core::tools::router: error=Exit code: 1
Wall time: 0.7 seconds
Output:
C:\Program Files\dotnet\sdk\10.0.300\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

 exited 1 in 707ms:
C:\Program Files\dotnet\sdk\10.0.300\Microsoft.Common.CurrentVersion.targets(93,5): error MSB4184: The expression "[Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation(Windows, 7.0)" cannot be evaluated. Access to the path 'C:\Users\mikew\AppData\Local\Microsoft SDKs' is denied. [C:\Users\mikew\source\repos\TAOM\TAOM.Tests\TAOM.Tests.csproj]

codex
I have one confirmed API-semantics mismatch: v1.4.5 `OnMissionTick(dt)` is not guaranteed to be one wall-clock frame delta. Before finalizing severity, I’m comparing vanilla/TAOM Gauntlet teardown patterns so I don’t turn a harmless redundant `ReleaseMovie` into a false positive.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'ReleaseMovie\\(' | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'E:\\Decompiled_Bannerlord' -Recurse -Filter '*.cs' -ErrorAction SilentlyContinue | Select-String -Pattern 'RemoveLayer\\(.*\\);' | Select-Object -First 120 Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command "Get-ChildItem -Path 'Main' -Recurse -Filter '*.cs' | Select-String -Pattern 'ReleaseMovie\\(|RemoveLayer\\(' | Select-Object Path,LineNumber,Line" in C:\Users\mikew\source\repos\TAOM
 succeeded in 568ms:

[32;1mPath                                                                                                               [0m[32;1m Lin[0m
[32;1m                                                                                                                   [0m[32;1m eNu[0m
[32;1m                                                                                                                   [0m[32;1m mbe[0m
[32;1m                                                                                                                   [0m[32;1m   r[0m
[32;1m----                                                                                                               [0m [32;1m---[0m
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                               87
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\AbilityHudController.cs                               90
C:\Users\mikew\source\repos\TAOM\Main\Features\CareerSystem\UI\GauntletCareerScreen.cs                              137
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\BattleActionBar\Hooks\BattleActionBarMissionView.cs  98
C:\Users\mikew\source\repos\TAOM\Main\Features\CompanionTactics\FormationPresets\OOBOverlayService.cs                94
C:\Users\mikew\source\repos\TAOM\Main\Features\EquipPresets\Hooks\Patch33_GauntletInventoryScreen.cs                 90
C:\Users\mikew\source\repos\TAOM\Main\Features\FactionMap\Hooks\CultureStageViewCreatedHook.cs                       90
C:\Users\mikew\source\repos\TAOM\Main\Features\FiefManagement\UI\GauntletFiefManagementScreen.cs                     87


 succeeded in 869ms:

[32;1mPath                                                                                                      [0m[32;1m LineNumber[0m[32;1m L[0m
[32;1m                                                                                                          [0m[32;1m           [0m[32;1m i[0m
[32;1m                                                                                                          [0m[32;1m           [0m[32;1m n[0m
[32;1m                                                                                                          [0m[32;1m           [0m[32;1m e[0m
[32;1m----                                                                                                      [0m [32;1m----------[0m [32;1m-[0m
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\GauntletLayer.cs         65 	
E:\Decompiled_Bannerlord\Engine\TaleWorlds.Engine.GauntletUI\TaleWorlds.Engine.GauntletUI\GauntletLayer.cs        140 	


 succeeded in 872ms:

[32;1mPath                                                                            [0m[32;1m LineNumber[0m[32;1m Line[0m
[32;1m----                                                                            [0m [32;1m----------[0m [32;1m----[0m
E:\Decompiled_Bannerlord\UI\TaleWorlds.GauntletUI\TaleWorlds.GauntletUI\Brush.cs        335 			style.RemoveLayer(layer…


codex
The local decompiled vanilla tree does not show feature-level `ReleaseMovie` patterns beyond `GauntletLayer` itself. TAOM sibling screens mostly call `RemoveLayer`/`ReleaseMovie` in their own wrappers; I’m checking those only for consistency, not as vanilla evidence.
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CareerSystem/UI/GauntletCareerScreen.cs' | ForEach-Object { if ("'$i -ge 120 -and $i -le 150) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs' | ForEach-Object { if ("'$i -ge 80 -and $i -le 108) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs' | ForEach-Object { if ("'$i -ge 80 -and $i -le 104) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
exec
"C:\\Program Files\\PowerShell\\7\\pwsh.exe" -Command '$i=1; Get-Content -LiteralPath '"'Main/Features/FiefManagement/UI/GauntletFiefManagementScreen.cs' | ForEach-Object { if ("'$i -ge 74 -and $i -le 96) { '"'{0,4}: {1}' -f "'$i, $_ }; $i++ }' in C:\Users\mikew\source\repos\TAOM
 succeeded in 549ms:
 120:     }
 121: 
 122:     protected override void OnFrameTick(float dt)
 123:     {
 124:         base.OnFrameTick(dt);
 125: 
 126:         if (_gauntletLayer.Input.IsKeyPressed(InputKey.Escape))
 127:         {
 128:             CloseScreen();
 129:         }
 130:     }
 131: 
 132:     private void CloseScreen()
 133:     {
 134:         _logger?.LogInfo("CareerSystem: Closing career screen");
 135:         _gauntletLayer?.InputRestrictions.ResetInputRestrictions();
 136:         if (_movie != null)
 137:             _gauntletLayer?.ReleaseMovie(_movie);
 138:         _viewModel?.OnFinalize();
 139:         Game.Current.GameStateManager.PopState();
 140:     }
 141: 
 142:     protected override void OnFinalize()
 143:     {
 144:         base.OnFinalize();
 145:         _viewModel = null;
 146:         _gauntletLayer = null;
 147:     }
 148: 
 149:     // IGameStateListener — required by GameStateScreenManager.OnCreateState
 150:     // which registers null if the screen doesn't implement this interface.

 succeeded in 510ms:
  80:         {
  81:             _refreshAccumulator = 0f;
  82:             RefreshFromSelectedFormation();
  83:         }
  84: 
  85:         HandleHotkeyInput();
  86:     }
  87: 
  88:     public override void OnMissionScreenFinalize()
  89:     {
  90:         if (_isInitialized)
  91:         {
  92:             try
  93:             {
  94:                 _stances?.ClearAllStances();
  95:                 if (_layer != null)
  96:                 {
  97:                     _layer.InputRestrictions.ResetInputRestrictions();
  98:                     MissionScreen?.RemoveLayer(_layer);
  99:                 }
 100:             }
 101:             catch (System.Exception ex) { _logger?.LogWarning($"[BattleActionBar] cleanup: {ex.Message}"); }
 102:             _vm?.OnFinalize();
 103:             _vm = null;
 104:             _layer = null;
 105:             _isInitialized = false;
 106:         }
 107:         base.OnMissionScreenFinalize();
 108:     }

 succeeded in 525ms:
  74:         _dataSource.SetDoneInputKey(HotKeyManager.GetCategory("GenericPanelGameKeyCategory").GetHotKey("Confirm"));
  75:         _layer.LoadMovie("TownManagement", _dataSource);
  76:         _layer.IsFocusLayer = true;
  77:         ScreenManager.TrySetFocus(_layer);
  78:         _dataSource.Show = true;
  79:     }
  80: 
  81:     protected override void OnFinalize()
  82:     {
  83:         if (_layer != null)
  84:         {
  85:             _layer.IsFocusLayer = false;
  86:             ScreenManager.TryLoseFocus(_layer);
  87:             RemoveLayer(_layer);
  88:             _layer = null;
  89:         }
  90:         _spriteCategory?.Unload();
  91:         _spriteCategory = null;
  92:         _dataSource?.OnFinalize();
  93:         _dataSource = null;
  94: 
  95:         if (_swapActive)
  96:         {

 succeeded in 525ms:
  80:         catch { return; }
  81: 
  82:         if (isActive && !_wasActive) Attach(handler);
  83:         else if (!isActive && _wasActive) Detach();
  84:         _wasActive = isActive;
  85:     }
  86: 
  87:     public void Detach()
  88:     {
  89:         if (_layer != null && _attachedScreen != null)
  90:         {
  91:             try
  92:             {
  93:                 _layer.InputRestrictions.ResetInputRestrictions();
  94:                 _attachedScreen.RemoveLayer(_layer);
  95:             }
  96:             catch (System.Exception ex) { _logger.LogWarning($"[CompanionTactics] Detach layer failed: {ex.Message}"); }
  97:         }
  98:         _vm?.OnFinalize();
  99:         _vm = null;
 100:         _layer = null;
 101:         _attachedScreen = null;
 102:         _wasActive = false;
 103:     }
 104: 

codex
**Findings**

1. LOW [Main/Features/CareerSystem/Abilities/AbilityActivationController.cs](</C:/Users/mikew/source/repos/TAOM/Main/Features/CareerSystem/Abilities/AbilityActivationController.cs:37>) — Vanilla API semantics — `dt` is documented/treated as “per-frame” wall-clock cooldown time, but v1.4.5 can pass simulation/fixed/fast-forward deltas.

Evidence from vanilla:
`MissionState.TickMission` sets `num = realDt`, but then overrides it when paused/fixed-step and scales it by `Scene.TimeSpeed`; in fast-forward it loops `TickMissionAux(0.1f, 0.1f, ...)` multiple times before the final tick. `Mission.OnTick` then calls `MissionBehaviors[num2].OnMissionTick(dt)`.

Fix: if mission-simulation time is intended, update the comment/test wording from wall-clock/per-frame to simulation delta. If real wall-clock cooldown is required, do not use `OnMissionTick(dt)` directly; inject a real-time provider and compute elapsed real time.

2. MED [Main/Features/CareerSystem/UI/AbilityHudController.cs](</C:/Users/mikew/source/repos/TAOM/Main/Features/CareerSystem/UI/AbilityHudController.cs:85>) — HUD lifecycle ownership — `Cleanup()` removes `_hudLayer` from `ScreenManager.TopScreen`, not from the screen it was added to.

Evidence from vanilla:
`ScreenManager.PushScreen` changes `TopScreen` to the last screen in the stack. `ScreenBase.RemoveLayer(layer)` does not verify ownership; it calls `layer.HandleFinalize()` before `_layers.Remove(layer)`. So if a pause/modal/battle-result screen is top when mission cleanup runs, TAOM can finalize the HUD layer through the wrong screen and leave a finalized layer in the owning mission screen’s layer list.

Fix: store the `ScreenBase` used at attach time, e.g. `_attachedScreen = topScreen` after `AddLayer`, and in `Cleanup()` remove from `_attachedScreen` only if it still has the layer. Clear `_attachedScreen` with the other HUD fields.

**Confirmed Clean**

No ADR-007 violation in `AbilityActivationController`; it only sees injected interfaces. `AbilityEffectExecutor` and `AbilityHudController` are boundary classes and their TaleWorlds access stays within the allowed surface.

The activation controller reset path is present in `OnEndMission`, and vanilla mission lifecycle calls `MissionBehavior.OnEndMissionInternal()` before the mission state is popped. I found no normal overlapping-mission path where the next mission ticks before the previous mission’s `OnEndMission`.

`Input.IsKeyPressed(InputKey.V)` is preserved from legacy. Managed v1.4.5 delegates it to the engine input interface; supporting managed evidence distinguishes `IsKeyPressed` from `IsKeyDown`, and standalone maps pressed to `GetKeyDown`.

**Verification**

I attempted `dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore`, but MSBuild was blocked by sandbox access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

**Phase 3e RCA**

Finding 1 was missed because the controller tests mock `dt` directly and never exercise vanilla `MissionState.TickMission`, where fixed-step, time-scale, and fast-forward deltas are introduced.

Finding 2 was missed because HUD cleanup was treated as a boundary/in-battle path and reviewed on the stable-`TopScreen` happy path. The missing check is screen ownership, not the Gauntlet calls themselves.
SUCCESS: The process with PID 20340 (child process of PID 40808) has been terminated.
SUCCESS: The process with PID 40808 (child process of PID 74968) has been terminated.
SUCCESS: The process with PID 74968 (child process of PID 46800) has been terminated.
SUCCESS: The process with PID 46800 (child process of PID 64692) has been terminated.
tokens used
202,228
**Findings**

1. LOW [Main/Features/CareerSystem/Abilities/AbilityActivationController.cs](</C:/Users/mikew/source/repos/TAOM/Main/Features/CareerSystem/Abilities/AbilityActivationController.cs:37>) — Vanilla API semantics — `dt` is documented/treated as “per-frame” wall-clock cooldown time, but v1.4.5 can pass simulation/fixed/fast-forward deltas.

Evidence from vanilla:
`MissionState.TickMission` sets `num = realDt`, but then overrides it when paused/fixed-step and scales it by `Scene.TimeSpeed`; in fast-forward it loops `TickMissionAux(0.1f, 0.1f, ...)` multiple times before the final tick. `Mission.OnTick` then calls `MissionBehaviors[num2].OnMissionTick(dt)`.

Fix: if mission-simulation time is intended, update the comment/test wording from wall-clock/per-frame to simulation delta. If real wall-clock cooldown is required, do not use `OnMissionTick(dt)` directly; inject a real-time provider and compute elapsed real time.

2. MED [Main/Features/CareerSystem/UI/AbilityHudController.cs](</C:/Users/mikew/source/repos/TAOM/Main/Features/CareerSystem/UI/AbilityHudController.cs:85>) — HUD lifecycle ownership — `Cleanup()` removes `_hudLayer` from `ScreenManager.TopScreen`, not from the screen it was added to.

Evidence from vanilla:
`ScreenManager.PushScreen` changes `TopScreen` to the last screen in the stack. `ScreenBase.RemoveLayer(layer)` does not verify ownership; it calls `layer.HandleFinalize()` before `_layers.Remove(layer)`. So if a pause/modal/battle-result screen is top when mission cleanup runs, TAOM can finalize the HUD layer through the wrong screen and leave a finalized layer in the owning mission screen’s layer list.

Fix: store the `ScreenBase` used at attach time, e.g. `_attachedScreen = topScreen` after `AddLayer`, and in `Cleanup()` remove from `_attachedScreen` only if it still has the layer. Clear `_attachedScreen` with the other HUD fields.

**Confirmed Clean**

No ADR-007 violation in `AbilityActivationController`; it only sees injected interfaces. `AbilityEffectExecutor` and `AbilityHudController` are boundary classes and their TaleWorlds access stays within the allowed surface.

The activation controller reset path is present in `OnEndMission`, and vanilla mission lifecycle calls `MissionBehavior.OnEndMissionInternal()` before the mission state is popped. I found no normal overlapping-mission path where the next mission ticks before the previous mission’s `OnEndMission`.

`Input.IsKeyPressed(InputKey.V)` is preserved from legacy. Managed v1.4.5 delegates it to the engine input interface; supporting managed evidence distinguishes `IsKeyPressed` from `IsKeyDown`, and standalone maps pressed to `GetKeyDown`.

**Verification**

I attempted `dotnet test TAOM.Tests --filter AbilityActivationControllerTests --no-restore`, but MSBuild was blocked by sandbox access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`.

**Phase 3e RCA**

Finding 1 was missed because the controller tests mock `dt` directly and never exercise vanilla `MissionState.TickMission`, where fixed-step, time-scale, and fast-forward deltas are introduced.

Finding 2 was missed because HUD cleanup was treated as a boundary/in-battle path and reviewed on the stable-`TopScreen` happy path. The missing check is screen ownership, not the Gauntlet calls themselves.
