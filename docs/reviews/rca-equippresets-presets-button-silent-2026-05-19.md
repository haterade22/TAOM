# RCA — EquipPresets Presets button silent no-op (2026-05-19)

## Top-line

User report: the "Presets" button in the inventory overlay rendered correctly but clicking it did nothing — no dialog, no error, no log line. Root cause was a single missing line: `_layer.InputRestrictions.SetInputRestrictions()` after `new GauntletLayer(...)` in `Patch33_GauntletInventoryScreen.OnInitialize_Postfix`. Without that call the layer paints but never registers with the screen's input dispatcher; mouse events pass through. Fix is one line of code + one line of teardown.

Crucially, **this bug shipped past two prior reviews** of EquipPresets (deep-review and Codex review #28). Both focused on service-layer correctness (`InventoryLogic.TransferCommand` migration, modifier preservation, slot-fit gating) and ignored the input-wiring stage of the GauntletLayer lifecycle, because there was no prior inventory-overlay precedent in the project to compare against. This RCA documents that gap.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 (SHIP-TIME, user-reported) | HIGH (from user impact — button is dead) | `OnInitialize_Postfix` created the overlay layer without calling `InputRestrictions.SetInputRestrictions()`. Layer renders, never receives mouse events, click handler never invoked. | **Layer-lifecycle wiring gap** (Gauntlet-overlay specific) | Original port from external suite. Original Codex review #28 + `/deep-review` were both scoped to service correctness (data persistence, modifier preservation, slot-fit gating). Neither agent's prompt asked "does the custom GauntletLayer enable input?" because TAOM had no other `ScreenBase` overlay to compare against — `FiefManagement` and `CareerSystem` use `SetInputRestrictions()` but both are full-screen replacements (different mental model), and the `CompanionTactics` battle overlays attach to a `MissionScreen` (different layer composition). The pattern was invisible until in-game testing surfaced it. | **New feedback memory:** `feedback_gauntlet_overlay_input_wiring.md` — when adding a Gauntlet overlay to a `ScreenBase` via Harmony postfix, the layer MUST call `InputRestrictions.SetInputRestrictions()` after construction or the button is a silent no-op. Rendering ≠ live. **`/deep-review` GUI checklist follow-up:** add a check for input wiring on any new `GauntletLayer` instantiation. |
| F2 (LOW, caught by follow-up `/deep-review` data-flow agent) | LOW | `OnFinalize_Prefix` removed the layer without first calling `InputRestrictions.ResetInputRestrictions()`. `ScreenBase.RemoveLayer` → `HandleFinalize` does not reset the input mask itself, so the dangling `InputUsageMask = All` flag persists on the orphaned C# object. | **Teardown asymmetry** (`SetInputRestrictions` was added without pairing `ResetInputRestrictions`) | The follow-up `/deep-review` data-flow agent caught this on its first pass by comparing against `GauntletCareerScreen.cs:84` which DOES call Reset. Low practical risk (the layer is gone from `_layers`, so `RefreshGlobalOrder` never reads the orphan), but diverges from project pattern. | Fixed in same commit. No new memory rule — this is "if you call Set, call Reset on teardown," which is generally good engineering hygiene already covered by symmetric-resource conventions. |

## Root Cause Pattern: "Layer-lifecycle wiring gap"

F1 is the only systemically-interesting finding here. The pattern is:

> **A custom Gauntlet overlay attached to a `ScreenBase` (NOT a MissionScreen, NOT a full-screen replacement) needs an explicit `InputRestrictions.SetInputRestrictions()` call on its `GauntletLayer` after construction, or the layer paints but is invisible to the input dispatcher. The button rendering is not proof the overlay is live.**

This wasn't documented anywhere in TAOM rules or in any feedback memory until this commit, because EquipPresets is the first feature in TAOM that uses this pattern. (`FiefManagement` / `CareerSystem` are full-screen replacements; the CompanionTactics overlays attach to `MissionScreen` which has different layer semantics.)

The original Codex review #28 covered the LIKELY failure modes for an inventory feature (data corruption, modifier loss, slot-fit bypass) and missed the UNLIKELY-but-fatal mode (the button doesn't do anything). This is the asymmetry of testing: it's much easier to ship a port where the button works but the data is wrong than the inverse — but the latter is what shipped here.

## Why Deep-Review Missed F1 (the original ship-time bug)

When the feature was ported and originally reviewed (`/deep-review` + Codex review #28 in early May 2026):

- **Standards (Agent 1):** ADR compliance is structural. Doesn't check API-call-sequence on TaleWorlds objects.
- **Compatibility (Agent 2):** Verified `new GauntletLayer(string, int, bool)` exists with the right signature. Did not flag that the constructed layer needs further configuration to be functional. Compatibility agents check "does this API call work?" not "is this API call sufficient?"
- **Efficiency (Agent 3):** Once-per-screen-open is trivially fine. Couldn't surface a wiring bug.
- **Completeness (Agent 4):** Found feature doc / tests / IoC — nothing about layer input wiring is in its rubric.
- **Data Flow (Agent 5):** Traced the click handler → service → adapter → InventoryLogic chain (and caught the `TransferCommand` issue, which became the Codex #28 critical finding). Did NOT trace the inverse chain: "what makes the click handler fire in the first place?" Input wiring is upstream of the data flow.

The lesson: **deep-review's Standards agent (or a new Phase 4 GUI agent) needs an explicit check for `InputRestrictions.SetInputRestrictions()` on every new `GauntletLayer` instantiation in a Harmony postfix on `OnInitialize`.** Adding that line to the agent prompt is the systemic fix.

## Why Deep-Review Caught F2 (the followup-pass low finding)

The follow-up `/deep-review` data-flow agent specifically traced layer lifecycle (Init → SetInputRestrictions → ... → RemoveLayer) and noticed the missing Reset by comparing against `GauntletCareerScreen.cs:84`. This is exactly the agent working as intended — it had a precedent (`GauntletCareerScreen`) to compare against and used it. F1 didn't have a precedent at original port time, which is why the second-pass review caught F2 but the first-pass review missed F1.

## Feedback Memories Codified

- `feedback_gauntlet_overlay_input_wiring.md` — when adding a Gauntlet overlay to a `ScreenBase` via Harmony postfix on `OnInitialize`, the layer MUST call `_layer.InputRestrictions.SetInputRestrictions()` after construction or the button is a silent no-op. Pair with `ResetInputRestrictions()` in `OnFinalize`. Rendering ≠ live. **First-precedent gap:** this rule didn't exist because EquipPresets is TAOM's first `ScreenBase` overlay (FiefManagement / CareerSystem are full-screen replacements; CompanionTactics overlays attach to MissionScreen). Future first-of-kind UI patterns should get a "no precedent" tag in review so reviewers know the pattern's correctness cannot be inferred from sibling code.

## `/deep-review` skill follow-up

Add to the Step 2 agent prompts (Standards OR a new Phase 4 GUI agent — TBD):

> If the changeset adds a new `GauntletLayer` instantiation in a Harmony postfix on a `ScreenBase`'s `OnInitialize` (or equivalent screen-init entry point), verify:
> - The layer calls `InputRestrictions.SetInputRestrictions()` after construction. Without this call, the layer paints but does not register with the screen's input dispatcher — buttons render but never receive clicks.
> - The corresponding `OnFinalize` (or layer-teardown) path calls `ResetInputRestrictions()` before `RemoveLayer`.
> - The decision to set or not set `IsFocusLayer = true` is documented in a comment (set it ONLY for full-screen replacements; do NOT set for parasitic overlays on top of a still-live vanilla screen).

This adds one rule for the specific class of bug; the broader principle (test custom UI by checking input pipeline, not just render) is already implicit in the "verify before reference" mandate but worth making explicit here.

## Tests Added

None — F1 and F2 are both Harmony postfixes on `OnInitialize_Postfix` / `OnFinalize_Prefix`. Per ADR-008, Harmony entry points are tested live in-game, not via unit tests. The fix was verified by the user in-game (button now opens the Save/Load/Update/Delete inquiry; vanilla Esc/hotkeys still function; inventory drag/drop unaffected).
