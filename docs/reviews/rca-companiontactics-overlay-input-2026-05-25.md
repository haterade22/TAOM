# RCA — CompanionTactics OOB / BattleActionBar overlay input wiring (2026-05-25)

## Top-line

User reported that the "Assign Heroes" and "Presets" buttons on the pre-battle Order-of-Battle deployment screen do nothing when clicked, while the adjacent vanilla "Reset Deployment" / "Ready" buttons work normally. Investigation found the bug is **identical class** to issue #202 / commit `d141304` from earlier in the same session week (EquipPresets "Presets" inventory button silent no-op): the custom `GauntletLayer` is added to the host screen without `_layer.InputRestrictions.SetInputRestrictions()`, so the layer paints but never registers with the input dispatcher and mouse events pass through.

Two files broken:
- [Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs:114](../../Main/Features/CompanionTactics/FormationPresets/OOBOverlayService.cs#L114) — user-reported, both buttons dead.
- [Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs:54](../../Main/Features/CompanionTactics/BattleActionBar/Hooks/BattleActionBarMissionView.cs#L54) — **latent**. The `BattleActionBar.xml` prefab has `Command.Click="ExecuteAction"` bindings, but mouse clicks were silently dropped. The bar remained functional only because `HandleHotkeyInput` polls `Mission.InputManager` directly, bypassing Gauntlet entirely — which masked the broken mouse path.

Both fixed in commit `28c8d1e`. Closes #225.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 (SHIP-TIME, user-reported) | HIGH (user impact) | `OOBOverlayService.Attach()` created the overlay layer without `SetInputRestrictions()`. Layer renders, "Assign Heroes" + "Presets" buttons paint, click events never invoked. | **Layer-lifecycle wiring gap** — MissionScreen overlay variant of the EquipPresets bug. | The rule we wrote 6 days earlier for #202 (commit `0b951c7`) explicitly **excluded** MissionScreen overlays from the trigger condition. The exclusion was based on the wrong inference that `BattleActionBar` (a sibling MissionScreen overlay) was working without `SetInputRestrictions()` — when in fact only its hotkey path was working. | Fixed in commit `28c8d1e`. Followup commit broadens the rule in `.claude/rules/gui-ui.md`, `.claude/skills/deep-review/SKILL.md` check #10, and `feedback_gauntlet_overlay_input_wiring.md` to cover MissionScreen overlays explicitly. |
| F2 (LATENT, found via codebase sweep during F1 fix) | MEDIUM | `BattleActionBarMissionView.OnMissionScreenInitializeFirstTime` created the overlay layer without `SetInputRestrictions()`. The bar has `Command.Click="ExecuteAction"` bindings but mouse clicks were silently dropped. User never reported because numeric hotkey path (`Mission.InputManager` polling) works regardless. | **Latent variant of F1** — same defect, masked by parallel hotkey path. | Sibling-overlay sweep performed during F1 investigation found this. **Until now, "BattleActionBar works" had been cited as evidence that MissionScreen overlays don't need `SetInputRestrictions()`** — that was the load-bearing wrong claim behind the #202 rule's scope error. | Fixed in same commit `28c8d1e`. |
| F3 (PROCESS) | HIGH (recurrence-multiplier) | The rule written for #202 was scoped too narrowly and shipped without a codebase sweep to find existing instances it should have caught. The OOB and BattleActionBar instances were sitting in the same grep output read while writing the rule. | **Rule-port discipline gap** — codified a rule from one instance, never sweep-validated. | Committed the rule (commit `0b951c7`) without running it against the rest of the codebase. The exclusion language ("NOT a MissionScreen overlay") was a guess that should have been a tested claim. | **New process rule (this RCA's preventive action):** when codifying a new rule from a single instance, immediately run the rule's grep against the rest of the codebase and verify each sibling instance against the rule's exception conditions. Sweep results should appear in the rule-codification commit message OR a follow-up commit before the rule is treated as load-bearing. See "Process change" section below. |

## Root Cause Pattern: "Rule scope inferred from a working sibling, without verifying which path made the sibling work"

F1 and F3 trace to the same epistemological error: the original rule for #202 observed that `BattleActionBar` was working ("user hasn't complained") and inferred MissionScreen overlays don't need `SetInputRestrictions()`. Two compounding mistakes:

1. **"Not complained-about" ≠ "working."** Latent UI bugs can survive indefinitely if there's a parallel input path the user uses by default.
2. **"Works" without qualifying the input path is meaningless for a rule about input path correctness.** The BattleActionBar works via hotkeys; the mouse path is broken. A rule about mouse input compatibility cannot be derived from observed hotkey functionality.

The unifying lesson:

> **When you classify a sibling as a working precedent to scope a rule, you must verify the sibling works via the SAME input path the rule governs. A working alternative input path is not evidence the broken path also works.**

This is broader than the GauntletLayer rule: it applies any time you derive scope boundaries for a rule from observed behavior in adjacent code. The observation must match the rule's governance.

## Why each `/deep-review` agent missed F1 (the user-reported bug) when it last ran on CompanionTactics

The CompanionTactics feature was reviewed earlier this session (see `docs/reviews/rca-companiontactics-2026-05-06.md`). At that time:

- **Standards (Agent 1):** Check #10 (the GauntletLayer rule) did not exist yet — it was added in commit `0b951c7` AFTER that review.
- **Compatibility (Agent 2):** Verified `new GauntletLayer(string, int, bool)` signature exists. Did not flag that the constructed layer needs further configuration.
- **Efficiency (Agent 3):** Once-per-mission overlay attach. Cannot surface a wiring bug.
- **Completeness (Agent 4):** Found feature doc / tests / IoC items. Input wiring isn't in its rubric.
- **Data Flow (Agent 5):** Traced the click handler → service → outcome chain. Did NOT trace the inverse chain ("what makes the click handler fire in the first place?"). Input wiring is upstream of the data flow.

After commit `0b951c7` added Check #10, the rule's MissionScreen exclusion meant the CompanionTactics overlays were still outside the agent's trigger. Check #10 has now been broadened (commit B in this RCA's pair) so the next deep-review on a feature with `new GauntletLayer(...)` on a MissionScreen will flag it.

## Feedback Memories Codified / Updated

- `feedback_gauntlet_overlay_input_wiring.md` — updated to cover BOTH ScreenBase AND MissionScreen overlays; added the "working precedent must share the input path" lesson; added the OOB/BattleActionBar examples alongside EquipPresets.
- (Implicit) the process lesson — "when codifying a new rule from a single instance, sweep the codebase for other instances before treating the rule as load-bearing" — could grow into its own feedback memory if it recurs. Not codifying yet because one instance is not enough datapoints; tracking via this RCA in the meantime.

## Process Change

Add to the standard rule-codification workflow (informally; consider promoting to a skill if it recurs):

1. Write the rule + the trigger condition.
2. Run the trigger condition (typically a grep) against the entire codebase.
3. For each matching instance, classify it as: (a) compliant — already follows the rule, (b) violating — needs fix, (c) exempt — meets the exception conditions, document why.
4. Fix all (b) instances in the same commit as the rule or a follow-up commit before declaring the rule load-bearing.
5. Record the sweep result in the rule-codification commit message.

The rule for #202 (commit `0b951c7`) skipped steps 2-5 and only sweep-validated against the EquipPresets file it was derived from. Steps 2-5 would have caught the OOB + BattleActionBar instances immediately and either (a) prompted fixes in the same week, or (b) prompted the rule scope to be widened before publication.

## Tests Added

None — F1 and F2 are both overlay-attach entry points. Per ADR-008, these are tested live in-game. The fix was verified by the user in-game (OOB "Assign Heroes" + "Presets" buttons now open their respective inquiries; vanilla "Reset Deployment" / "Ready" + Esc still work; BattleActionBar mouse path now functional in addition to the always-working hotkey path).

## Patch History

| Pre-fix (commit `28c8d1e`) | Post-fix |
|---|---|
| `_layer = new GauntletLayer("GauntletLayer", 200, false); _layer.LoadMovie(...)` | `_layer = new GauntletLayer(...); _layer.InputRestrictions.SetInputRestrictions(); _layer.LoadMovie(...)` |
| `_attachedScreen.RemoveLayer(_layer);` | `_layer.InputRestrictions.ResetInputRestrictions(); _attachedScreen.RemoveLayer(_layer);` |
| (same shape in BattleActionBarMissionView Init/Finalize) | (same fix shape applied) |
| `.claude/rules/gui-ui.md` rule scoped to "ScreenBase overlay via Harmony postfix (NOT a MissionScreen overlay)" | Scoped to "ScreenBase overlay (via Harmony postfix) OR MissionScreen overlay (via MissionView/MissionLogic attach)" with an explicit display-only exception |
| `.claude/skills/deep-review/SKILL.md` check #10 trigger limited to ScreenBase | Broadened to any feature-overlay attach path |
