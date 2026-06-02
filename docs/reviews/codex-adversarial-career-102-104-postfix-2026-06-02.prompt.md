# Codex Adversarial Review — Career #102 + #104 POST-FIX Diff (2026-06-02)

## Context

This is the **second** Codex pass on the Career System refactor + repurpose work for issues #102 and #104. The first Codex pass (`codex-adversarial-career-102-refactor-2026-06-02.output.md`) ran against the pre-fix diff and surfaced 1 MED + 1 LOW. A parallel 5-dimension Claude deep-review workflow surfaced 15 confirmed findings (2 HIGH, 4 MED, 9 LOW), of which 7 were actioned and the rest refuted with rationale.

**This review targets the FIXES themselves** — the changes we made in response to Codex pass 1 + the deep-review, plus the #104 implementation. None of the post-fix code has been independently reviewed.

## Diff under review (all relative to working dir `c:/Users/mikew/source/repos/TAOM`)

### Issue #102 fixes layered on top of the original refactor

- `Main/Features/CareerSystem/UI/AbilityHudController.cs` — Codex MED: added `private ScreenBase _attachedScreen;` captured at `TryInitialize()` after `AddLayer`, consumed in `Cleanup()` instead of re-reading `ScreenManager.TopScreen`. Deep-review HIGH: wrapped the engine-touching teardown (RemoveLayer / ReleaseMovie / VM.OnFinalize) in `try { ... } catch (Exception ex) { _logger?.LogWarning(...) } finally { /* field-reset block */ }` so a throw cannot leave `_hudInitialized=true` on the Singleton.
- `Main/Features/CareerSystem/Abilities/IAbilityActivationController.cs` — Deep-review MED: replaced the single-outcome `AbilityActivationOutcome` enum with a flags-style `AbilityActivationResult { bool JustBecameReady; bool Activated; bool Charging; }` struct so the legacy dual-toast UX is preserved when the ability becomes ready AND V is pressed on the same frame.
- `Main/Features/CareerSystem/Abilities/AbilityActivationController.cs` — Tick rewritten to return the struct; sets `JustBecameReady=true` BEFORE returning even when `Activated` also fires on the same frame; deduped `IsAbilityReady` lookups (one local); tightened `dt` semantics comment (Codex LOW: mission-simulation delta, not wall-clock).
- `Main/Features/CareerSystem/CareerPerkMissionBehavior.cs` — Switch over `AbilityActivationOutcome` replaced with three independent `if (result.X)` branches; hoisted `hasCareer` to one call at top of `OnMissionTick`; `OnEndMission` rewritten with **per-step** `try/catch` around each of `_hudController.Cleanup()`, `_activationController.Reset()`, `_abilityService.ClearAll()`, `CareerAbilityBuffTracker.ClearAll()` so a throw in one does not abort the others. Net file is now 139 lines.
- `TAOM.Tests/Features/CareerSystem/AbilityActivationControllerTests.cs` — 13 tests rewritten for the flags struct API; new regression test `Tick_ReadyAndVPressedSameFrame_FlagsBothJustBecameReadyAndActivated` proves the deep-review MED fix.

### Issue #104 Option B implementation (new on this diff)

- `Main/Features/CareerSystem/Domain/AbilityTemplateData.cs` — new `public float CooldownReduction { get; set; }` property (default 0), copied in copy ctor.
- `Main/Features/CareerSystem/Domain/AbilityTuningConfig.cs` — `GlobalTuning` gained `public float MinCooldownSeconds { get; }` (default 5f) and an optional ctor param.
- `Main/Features/CareerSystem/CareerConfigProvider.cs` — `ParseGlobalTuning` now parses an optional `min_cooldown_seconds` XML attribute. Validations: rejects non-finite (`NaN`/`±Infinity`), negative, and values > the cooldown itself. Falls back to default with warning. Pattern mirrors the existing `cooldown_seconds` parser (see `feedback_clamp_nan_infinity_propagates.md` — bug has shipped three times).
- `Main/Features/CareerSystem/Abilities/CareerAbility.cs` — new `public void AdjustCooldown(float reductionSeconds, float minCooldownSeconds)`. No-op for charge-based abilities, NaN/Infinity-safe, negative-reduction-rejected (treat as zero), negative-min-clamped to 0. Sets `CooldownRemaining = Math.Max(min, CooldownRemaining - reduction)`.
- `Main/Features/CareerSystem/Abilities/ICareerAbilityService.cs` + `CareerAbilityService.cs` — new `ApplyCooldownAdjustment(string heroStringId, float reductionSeconds, float minCooldownSeconds)` method that delegates to the ability's `AdjustCooldown`. Logs warning for unknown hero.
- `Main/Features/CareerSystem/Abilities/AbilityEffectExecutor.cs` — `Execute` reads `template.CooldownReduction` from the post-mutation template; if `> 0f`, calls `_abilityService.ApplyCooldownAdjustment(heroId, reduction, MinCooldownSeconds)` BEFORE running the per-archetype effect executor + the registerContext callback. Added `ICareerAbilityService _abilityService` to ctor.
- `Main/_Module/ModuleData/career_system/taom_career_choices.xml` — 98 `<Mutation property="MaxCharge" ... value="-20"/-30"/>` entries rewritten to `<Mutation property="CooldownReduction" ... value="-6"/-9"/>` via `tools/rename_maxcharge_to_cooldownreduction.py` (50× -20→-6 + 48× -9). Preserves the 1.5× tier ratio.
- `TAOM.Tests/Features/CareerSystem/CooldownReductionTests.cs` — 15 new tests covering: AbilityTemplateData default + copy-ctor, GlobalTuning default + ctor overload, CareerAbility.AdjustCooldown happy + floor + zero + negative + NaN + Infinity + NaN min + charge-based no-op, CareerAbilityService.ApplyCooldownAdjustment unknown-hero + happy + floor.

## Adversarial review surface — attack these specifically

1. **Flags-struct caller audit.** The replacement of `AbilityActivationOutcome` enum with `AbilityActivationResult` flags struct is a public API change. Confirm:
   - There are NO other callers of `IAbilityActivationController.Tick` besides `CareerPerkMissionBehavior.OnMissionTick` (grep the repo).
   - There are NO other references to the now-deleted `AbilityActivationOutcome` enum.
   - `CareerPerkMissionBehavior.OnMissionTick` correctly handles the new mutually-non-exclusive semantics. Specifically: the legacy switch-case ensured `Activated` and `Charging` were mutually exclusive (one switch case fires); the new code uses `if (result.Activated) {...} else if (result.Charging) {...}`. Confirm `Activated` and `Charging` are still mutually exclusive *by construction in the controller* — i.e., the controller cannot set both true on the same tick. Read `AbilityActivationController.cs:48-72` carefully.
   - The new test `Tick_ReadyAndVPressedSameFrame_FlagsBothJustBecameReadyAndActivated` is the only flag-coincidence test. Are there other simultaneous-flag scenarios I should test? (`JustBecameReady + Charging`? — is that physically possible if the ability is ready but V is pressed while charging? Should be impossible because "ready" and "on cooldown" are mutually exclusive.)

2. **OnEndMission per-step try/catch ordering.** Read `CareerPerkMissionBehavior.cs:113-130`. The order is `_hudController.Cleanup()` → `_activationController.Reset()` → `_abilityService.ClearAll()` → `CareerAbilityBuffTracker.ClearAll()`. Is there any ordering dependency where a failure in step N would CORRUPT step N+1 if N+1 still runs? (Example: if HUD cleanup throws partway through, does subsequent ClearAll see a corrupted dict?) The previous straight-line order had implicit "fail-fast"; per-step try/catch makes the cleanup more resilient but exposes ordering assumptions that may not hold.

3. **AbilityEffectExecutor cooldown adjustment ordering.** Read `AbilityEffectExecutor.cs:47-95`. The flow is:
   ```
   1. Lookup careerId, career, raw template, mutated template.
   2. Compute duration + radius.
   3. If template.CooldownReduction > 0: _abilityService.ApplyCooldownAdjustment(heroId, reduction, min)
   4. Construct MissionAbilityExecutionContext.
   5. registerContext.Invoke(context)  -- adds to host's _activeContexts
   6. executor.Execute(context)  -- runs the per-archetype effect (may throw)
   7. InformationManager.DisplayMessage(...)  -- yellow toast
   8. PlaySound / PlayParticle if non-empty
   ```
   
   Question: the `ApplyCooldownAdjustment` runs in step 3 — BEFORE the `executor.Execute(context)` at step 6 that may throw. If `executor.Execute` throws, the cooldown has already been shortened but the buffs/particles never applied. Is this a real regression vs the legacy (where cooldown was set to full `CooldownDuration` and never adjusted)? Walk the failure timeline.

4. **`CareerAbility.AdjustCooldown` semantic ambiguity.** Read `CareerAbility.cs:62-77`. Current implementation: `if (reductionSeconds <= 0f) return;`. This rejects negative AND zero. But also: `if (float.IsNaN(reductionSeconds) || float.IsNaN(minCooldownSeconds)) return;` — and `if (float.IsInfinity(reductionSeconds) || float.IsInfinity(minCooldownSeconds)) return;`. Order matters: the NaN check must precede the `<=` check because NaN comparisons always return false (NaN <= 0f is false, so a NaN would slip past the early return WITHOUT the explicit NaN guard above). Confirm the guard ordering is correct.

5. **MinCooldownSeconds parsing — sign-flipped designer typo.** `CareerConfigProvider.ParseGlobalTuning:444-456` rejects `< 0f`. Confirm a designer who typos `min_cooldown_seconds="-5"` (intending to "allow no minimum") gets the rejection-with-warning, not silently mis-applied. Also confirm `min_cooldown_seconds > cooldown_seconds` (e.g., min=60s but cooldown=30s) is rejected — current code has `else if (parsedMin > seconds)`.

6. **XML rewrite script audit.** Read `tools/rename_maxcharge_to_cooldownreduction.py`. The regex is `r'property="MaxCharge" calculator="flat" value="(-20|-30)"'`. Is there any sibling attribute in the XML that could have matched? Run `git diff Main/_Module/ModuleData/career_system/taom_career_choices.xml` and visually confirm the only diff lines are the 98 mutations — no neighbor lines were accidentally re-flowed. Also confirm the regex is anchored tightly enough that a future `value="-20.5"` (parsed by a sibling Mutation type) wouldn't be touched.

7. **CHANGELOG of the surviving legacy `MaxCharge` property.** `AbilityTemplateData.MaxCharge` was NOT removed (the property is still on the model). The mutation pipeline (`MutationService.ApplyMutation` via reflection) would still write to it if any future XML mutation targets it. Is this an acceptable "dead but harmless" state, or should we remove the property entirely to forbid future regressions? Consider the rule `feedback_no_aspirational_enum_values.md` ("don't keep reserved fields without a producer + consumer").

## Cross-feature regression risks

8. **HUD Cleanup try/finally and the `_hudInitialized` reset.** Read `AbilityHudController.cs:95-126` carefully. In the `finally` block, every field is nulled INCLUDING `_hudInitialized = false`. The `try` block runs three engine calls in sequence. If `RemoveLayer` throws, do we ever WANT `_hudInitialized` to remain true? Argue both sides — there's an outside chance that leaving `_hudInitialized=true` post-throw would let the next mission's `TryInitialize` short-circuit and at least keep the player from seeing a worse error. (Current design is defensively "reset everything and try fresh next mission" — confirm this is correct.)

9. **First Codex review's `_attachedScreen` ownership.** When `TryInitialize` runs the second time (idempotent guard via `_hudInitialized`), the early-return is at line 54. But on the NEW path (post-Cleanup), `_attachedScreen` is now null (cleared in finally). If somehow `_hudInitialized` is true but `_attachedScreen` is null (no path makes this true under normal flow, but defensive review): does Cleanup crash on the null deref? Current code is `if (_attachedScreen != null && _hudLayer != null) _attachedScreen.RemoveLayer(_hudLayer);` — short-circuit handles it. Confirm.

10. **MinCooldownSeconds = 0 edge case.** If a designer sets `min_cooldown_seconds="0"`, can a stack of CooldownReductions cause `CooldownRemaining = 0`, causing `IsReady` to immediately return true on the same frame, causing the player to fire the ability instantly with no cooldown ever displayed? Walk: `AdjustCooldown` clamps to `max(0, CooldownRemaining - reduction)`. The HUD updates next tick. Activation-controller's `JustBecameReady` would fire on the next tick. The player CAN spam-activate, but the cooldown does run between activations. Is this acceptable behavior? (Probably yes — if a designer chooses 0 floor, they get a 0 floor.)

## Output format

Per the project review-guide:
- Severity HIGH / MED / LOW per finding
- File + line range
- Evidence (vanilla decompile excerpt OR test trace OR rule citation OR repo grep)
- Concrete proposed fix
- Phase 3e RCA per confirmed finding (why the first Codex + the deep-review missed it)

Focus on what's actionable. Decline non-findings or speculative concerns. The goal of pass 2 is to catch bugs the first pass + deep-review introduced via their own fix recommendations, NOT to re-find the same surface area.
