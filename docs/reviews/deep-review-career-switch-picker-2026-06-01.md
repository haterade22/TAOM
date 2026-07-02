# Deep Review — CareerSystem Switch Picker + Effect-Scope Badges — 2026-06-01

Workflow run ID: `wex8pqgcv`. 5 dimension agents (standards, compat, efficiency, completeness, dataflow) + 2-skeptic adversarial verification per finding + completeness critic = 49 agents total, ~820s wall-clock.

**Verdict:** ISSUES FOUND. CRITICAL: 0 | HIGH: 4 | MEDIUM: 6 | LOW: 10 | (3 disputed).

The 20 confirmed findings deduplicate to **8 root causes** (multiple agents flagged the same underlying defects from different angles).

---

## Root-cause clusters (deduplicated)

### Cluster A — `Popup.GreenButton` brush does not exist in vanilla 1.4.5 (HIGH, real)

**File:** `Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml:363`
**Evidence:** `Popup.GreenButton` and `Popup.GreenButton.Text` are not defined in any `Native/`, `SandBox/`, or `SandBoxCore/` brush XML in 1.4.5. They are also not in TAOM's own `Main/_Module/GUI/Brushes/*.xml`. The nearest valid vanilla green-button brush is `ButtonGreenBrush` in `Native/GUI/Brushes/Brush.xml`. `Popup.Frame` (line 370) exists, confirming the developer was using the `Popup.*` family — `GreenButton` was simply missing.
**Runtime impact:** Button background paints unstyled / invisible. `Command.Click="ExecuteChoose"` is brush-independent and would still fire on click, so functionally the action triggers but the player can't easily see/find the Choose button. UX-broken, not crash.
**Fix:** Replace `Brush="Popup.GreenButton"` with `Brush="ButtonGreenBrush"` and `Brush="Popup.GreenButton.Text"` with `Brush="Generic.Button.Text"`.

### Cluster B — Empty switch-picker rendering: outer panel gated on `@IsSwitchMode` instead of `@IsBrowsingTargets`, no empty-state message (HIGH, real but latent)

**Files:** `Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml:318` + `Main/Features/CareerSystem/UI/CareerScreenVM.cs:530`
**Findings consolidated:**
- "IsBrowsingTargets is dead — prefab binds @IsSwitchMode, leaving picker visible when empty"
- "Outer picker panel gated on @IsSwitchMode instead of @IsBrowsingTargets — intent mismatch"
- "Switch-mode picker panel visibility gate omits empty-target-list handling"
- "Empty switch-mode screen has no empty-state message and no auto-close"
- "RebuildEligibleSwitchTargets silently succeeds on empty target list; no diagnostic logging"

**Evidence:** `CareerScreenVM.cs:519-521` comment explicitly states "the picker panel with @IsBrowsingTargets". `IsBrowsingTargets` (line 530) is `_isSwitchMode && targets.Count > 0` — the empty-list guard. But `CareerScreen.xml:318` actually uses `IsVisible="@IsSwitchMode"`. Grep confirms `IsBrowsingTargets` is bound NOWHERE in the prefab. When the switch screen opens with 0 eligible targets, the outer frame renders an empty 760px-tall scroll area with no explanation.
**Mitigation:** The dialogue gate in `CareerSwitchDialogueBehavior.cs:68-79` hides the dialogue option when `targets.Count == 0`, preventing the empty-state from being reachable via the dialogue path. But `OpenCareerScreen(switchMode: true)` is a public static method any future caller could hit without the guard, and the `_heroAdapter == null` path in `RebuildEligibleSwitchTargets` also produces zero targets.
**Fix:**
- Change `CareerScreen.xml:318` from `IsVisible="@IsSwitchMode"` to `IsVisible="@IsSwitchMode"` AND add an empty-state TextWidget gated on `IsVisible="@HasNoEligibleTargets"` (new VM property = `_isSwitchMode && targets.Count == 0`), OR
- Gate the `ScrollablePanel` itself on `@IsBrowsingTargets` (so it hides when empty) and add an empty-state TextWidget gated on `@HasNoEligibleTargets` for clear feedback.
- Add a `LogWarning` in `RebuildEligibleSwitchTargets` for empty result so future blank-screen reports are triagable.

### Cluster C — 8 new loc keys not propagated to 12 language files (HIGH, real but expected workflow)

**File:** `Main/_Module/ModuleData/taom_module_strings.xml:836-844` (new keys)
**Evidence:** `taom_career_switch`, `taom_career_switched_open_screen`, `taom_career_switch_title`, `taom_career_switch_choose`, `taom_career_switch_subtitle`, `taom_career_choice_while_active`, `taom_career_choice_passive_tooltip`, `taom_career_choice_keystone_tooltip` are in the English source but absent from all 12 `Languages/<lang>/std_taom_module_strings.xml` files.
**Runtime impact:** Non-English players see raw `{=taom_career_switch}I wish to discuss my career path.` text (engine fallback to the post-`}` default). Functional but cosmetically broken.
**Fix:** Run `python tools/translate_with_claude.py` to propagate. Or accept the engine fallback as the shipping default until the next translation pass.

### Cluster D — Dead `[DataSourceProperty]` declarations (LOW/MEDIUM, dead code)

**Files:** `Main/Features/CareerSystem/UI/CareerChoiceObjectVM.cs:67`, `Main/Features/CareerSystem/UI/CareerScreenVM.cs:540`, `Main/Features/CareerSystem/UI/CareerSwitchTargetVM.cs:27`
**Findings consolidated:**
- "EffectScopeTooltip is a dead [DataSourceProperty] — never bound in the prefab"
- "EffectScopeBadge allocates TextObject + ToString on every property get" (the get-allocation concern is refuted since Gauntlet does not poll unbound props, but the dead-code aspect of EffectScopeTooltip is confirmed)
- "Passive choices lack visual effect-scope badge; asymmetric UX vs Keystones" (verifiers split: one says asymmetry is intentional design per inline VM comment; the other says it is a UX gap)
- "SwitchModeTitle [DataSourceProperty] is authored but has no prefab binding" — redundant with `ScreenTitle` which is already set to "Choose Your Path" in switch mode at ctor line 86-88
- "AbilitySpriteName [DataSourceProperty] populated but not bound in switch-mode card template" — VM computes `CareerSystem\Abilities\<id>`, prefab card has no widget to render it

**Fix:** Decide per property:
- `EffectScopeTooltip`: bind to a `HintWidget` on the badge container OR delete the property + its 2 loc keys (`_passive_tooltip`, `_keystone_tooltip`). Simplicity criterion favors delete; binding is also fine.
- Passive badge: keystone-only badge is intentional (passive default is "always active"); the explicit passive label would be redundant. Keep as-is.
- `SwitchModeTitle`: delete. `ScreenTitle` already drives the title widget in both modes.
- `AbilitySpriteName` on `CareerSwitchTargetVM`: either add a sprite widget to the card OR delete the property. Adding the icon is the better UX (cards have name + description but no ability visual).

### Cluster E — Discarded `switchService` ctor param (LOW, smell)

**File:** `Main/Features/CareerSystem/CareerSwitchDialogueBehavior.cs:22-32`
**Evidence:** Ctor accepts `ICareerSwitchService switchService` but discards it via `_ = switchService` with a comment about avoiding IoC churn.
**Verifiers split:** One says it is a real code-clarity smell (1/2 confirmed). The other says runtime impact is zero (refuted under reachability lens).
**Decision:** Could either remove the param + update `SubModule.cs` resolve, or leave with the comment. **Going with: remove**, since `GauntletCareerScreen` already resolves `ICareerSwitchService` via IoC directly — the ctor param is genuinely dead.

### Cluster F — Allocations in cold paths (LOW, acknowledged)

**Findings:**
- `CareerRegistry.GetEligibleSwitchTargets` allocates a new `List<CareerDefinition>` per call, called twice per switch-flow (dialogue gate + screen open).
- `CareerSwitchTargetVM` ctor allocates 4 `TextObject` instances per target (Name, Description, AbilityName, ChooseLabel).
- `CareerChoiceObjectVM.EffectScopeBadge` allocates per get (but Gauntlet does not poll unbound — actually only fires on construction).

**Decision:** All cold-path. No fix. Could hoist `ChooseLabel` to a static field but the gain is marginal.

### Cluster G — Test gaps (LOW, completeness)

**Findings:**
- `GetEligibleSwitchTargets` not tested with null `currentCareerId`
- `CanSwitch` not tested with empty `hero.StringId`
- `CareerSwitchTargetVM` has no dedicated tests
- `CareerSwitchDialogueBehavior` has no dialogue tests (in-game-only per the plan; acceptable)

**Decision:** Add the two missing service-level tests (null `currentCareerId`, empty `StringId`). Skip `CareerSwitchTargetVM` (trivial property-passthrough VM).

### Cluster H — Layout: 760px scroll panel with 1-target shows 560px empty canvas (LOW, cosmetic)

**File:** `Main/_Module/GUI/Prefabs/CareerSystem/CareerScreen.xml:329`
**Evidence:** ScrollablePanel `SuggestedHeight="760"` fixed. One card is `180+10+10=200px`. With one target: 560px of blank canvas below.
**Fix:** Either change the ScrollablePanel to `HeightSizePolicy="CoverChildren"` with a `MaxSuggestedHeight`, or keep fixed and accept the empty space. Decision: cosmetic edge case, keep fixed for now (most cultures have 2+ careers).

---

## Disputed (not real)

1. **"Duplicate TextObject allocation: `_switchModeTitle` allocated twice identically"** — verifiers correctly refuted: lines 86-88 assign `_screenTitle`, line 94 assigns `_switchModeTitle`. Different fields. Reviewer misread.
2. **"Redundant `_screenTitle` assignment when isSwitchMode=true"** — verifiers correctly refuted: `@ScreenTitle` IS bound in the prefab top panel (line 25); `_screenTitle` is the live field, not the dead one. The actual dead field is `_switchModeTitle` (already in Cluster D).
3. **"IsBrowsingTargets never fires OnPropertyChanged — latent notification gap"** — refuted as latent-only since no prefab binds `@IsBrowsingTargets`. Becomes real if we apply the Cluster B fix; will need to add `OnPropertyChanged(nameof(IsBrowsingTargets))` after `RebuildEligibleSwitchTargets` mutates the list. **This dispute becomes a confirmed fix-requirement once Cluster B is implemented.**

---

## Action plan

Pending Codex's parallel review (background task `bo71dfzxs`). Once Codex lands:
1. Synthesize union of confirmed findings between deep-review and Codex.
2. Fix Cluster A (brush rename) + Cluster B (empty-state binding + new VM property + OnPropertyChanged for IsBrowsingTargets) + Cluster D (delete dead properties, add ability icon to switch card) + Cluster E (remove discarded ctor param) + Cluster G (2 boundary tests).
3. Defer Cluster C (loc propagation) and Cluster H (layout) — flag as follow-ups.
4. Re-verify: `dotnet test TAOM.Tests` + `./build.ps1 -RunTests`.
5. REVIEW-LOG.md + AGENTS.md update + meta-RCA on the skipped-review-gates process violation.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
