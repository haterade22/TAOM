# RCA: #1430 port of Silas He's troop weight party screen fixes (2026-11-17)

**Scope:** the party screen header guard (verdict recorded at `PartyScreenLogic.Initialize`) and Transfer All's
weighted member budget (transpiler on `PartyScreenLogic.TransferAllTroops`). Branch `fix/silas-port`, on top of
af9aa589f, reviewed uncommitted. Tier 1: Lens 5 Data flow and Lens 2 Engine compatibility, then a re-review of the
runtime fixes by each lens. No XML changed, so Lens 7 was not in scope. Codex skipped (out of usage since
2026-11-15); its pass is owed before merge.

**Top line:** two MED and five LOW findings. Both MED findings come from one gap: the port changed what a value means
in one place (a budget handed back into vanilla's loop, a header that now stays in the weighted frame) without
listing every state that produces the value and every reader of the old meaning. Lens 2 confirmed every engine API
and the transpiler anchors on the installed v1.5.7 IL.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `WeightedFillBudget` returned 0 both for "no room" and for "some room, but the first eligible troop is heavier than it". Vanilla reads `num <= 0` as "move everything" (v1.5.7 PartyScreenLogic.cs:1015-1018), so a near-full party pressing Transfer All moved the whole roster. Silas's source had the same 0 case. Fixed: with some room and nothing moved yet the budget is at least 1, as vanilla's own clamp moves; tests `SomeRoom_ButTheFirstEligibleTroopDoesNotFit_MovesOne`, `SomeRoom_ButNoEligibleTroop_ReturnsZero`. | Transpiler, value fed back into vanilla | The builder checked that 0 matches vanilla's full-party press, and the test fixed 0 as intended, but did not list every input that produces 0. | Lesson added: lessons/harmony-il.md, "A value a transpiler hands back into vanilla's control flow can hit vanilla's sentinel". |
| 2 | MED | The header now stays weighted while heavy troops are dragged in, so it could read over capacity while vanilla's red tint and Over Limit prompt (raw count against the frozen limit) stayed off. Before the change the weighted header showed only while the two frames agreed. Fixed: the main side sets `IsMainTroopsLimitWarningEnabled` both ways under vanilla's gate `AreMembersRelevantOnCurrentMode` (PartyVM.cs:3069); test `HeaderTint_MainSide_FollowsTheWeightedFrame_UnderVanillasModeGate`. | UI state, frame change | The earlier "downgrade only" rule was correct for the old precondition; the builder removed the precondition without re-reading the rule that depended on it. | Lesson added: lessons/localization-ui.md, "A display that stays in a new frame longer needs the flags that agreed with the old frame". |
| 3 | LOW | The transpiler threw `ArgumentNullException` from `CodeInstruction.Calls` when the limit getter does not resolve, against the soft-fail contract. Fixed: a null getter returns the method unchanged. | Transpiler soft-fail | The anchor checks covered missing instructions, not a missing member. | No new lesson. |
| 4 | LOW | The missing-anchor warning was not logged once: Harmony runs a transpiler again on every re-patch, and PatchShield re-patches. Fixed: a per-process latch reset in `Initialize`; test `Transpiler_WithoutTheAnchors_WarnsOnceAcrossRepatches`. | State lifetime | The text said "one warning" from the transpiler's point of view, not Harmony's. | Existing state-lifetime lesson; the texts now say "per process". |
| 5 | LOW | The anchor search scanned to the end of the method, so after an engine change it could match the loop's own `num <= 0` test and run `Budget` on every pass. Fixed: the test must be the instruction right after the store (v1.5.7 IL_00af, IL_00b0). | Transpiler anchor | A wide search looked more robust; it was less strict. | No new lesson; the real-IL test pins the position. |
| 6 | LOW | Three wrong citations: the normal screen builds its data in `PartyScreenHelper.OpenPartyScreen` (:53), not `CreateBasicInitDataWithMainParty` (:81, the loot, manage and donate screens); the lock filter is :1019 and :1027, not :1021. Fixed in the code comments and the feature doc. | Engine claims | Line numbers were carried from one read without checking the method that owns them. | Existing rule (AGENTS.md "Research first"). |
| 7 | LOW | After the finding 2 fix, five texts still said the warning flags "stay correct without being rewritten" or "downgrade only" (troop-weight-system.md usage-frame and tint paragraphs, TroopWeightDisplayHook.cs class comment, TroopWeightDisplay.cs). Fixed: each says the identity holds against the live limit and the party screen restates the main side's flag. Found by the Lens 5 fix re-review. | Doc drift | The fix edited the code and its own comment, not the older texts that stated the rule. | Covered by the finding 2 lesson (read every reader of the old rule). |

A weighted count of NaN would have reached the planner, which returns 0 (move everything), against the doc's "any
failure keeps vanilla's budget". No caller produces a NaN (weights are checked at load), so it is not counted as a
finding; the hook now sends a NaN to vanilla's budget.

Not fixed, recorded:

- Accepted (Lens 5 #3): on the Loot screen, after a weighted fill that leaves heavy troops behind, Done asks "You're
  leaving troops and/or prisoners behind!" because `CanRightPartyTakeMoreTroops` counts heads against the frozen
  limit (PartyVM.cs:275, :3211). It is a Yes/No confirmation, not a block; a fix would need a patch on a vanilla
  property for a prompt.
- Accepted (Lens 5 #5): a screen with no recorded verdict (only if the `Initialize` postfix failed) gets the live
  comparison in the header but vanilla's budget in Transfer All. Each reader takes its safer choice in a failure
  mode.
- Accepted: the other side's tint only clears; its vanilla gate reads the private `_currentMode` (PartyVM.cs:3070).
- `FOLLOW-UP` (no issue: found outside this change's diff, Mike to decide with the batch): the LordCourtesy gift
  screen's Done check `GiftDonePossible` compares the raw count with the frozen limit (LordCourtesyAdapter.cs:219-223),
  so dragging heavy troops can still pass the lord's weighted cap.

## Root-cause pattern

Findings 1 and 2 share one cause: a port changed the meaning of a value at one site and checked the site, not the
set. For finding 1 the set is every input that makes the planner return 0; for finding 2 it is every flag that was
correct only because the header used to fall back to the raw frame. The original package had finding 1 too, so a
port inherits the original's unchecked cases unless the porter lists them again.

## Why each agent missed these

- The builder (this session) ran the new tests green; the test for 0 encoded the defect as intended behaviour.
- Lens 5 found findings 1 and 2; Lens 2 found finding 1 independently and findings 3 to 6. The Lens 5 fix re-review found finding 7; the Lens 2 fix re-review found nothing.
- No gate checks a transpiler's return value against the special values of the code it feeds.

## Lessons

- lessons/harmony-il.md: "A value a transpiler hands back into vanilla's control flow can hit vanilla's sentinel"
  (finding 1).
- lessons/localization-ui.md: "A display that stays in a new frame longer needs the flags that agreed with the old
  frame" (finding 2).
