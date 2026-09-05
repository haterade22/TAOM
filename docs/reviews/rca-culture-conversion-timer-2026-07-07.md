# RCA — CultureConversion hold-timer restart (#333, 2026-07-07)

**Surfaced by play-test, not review.** User captured Grymmclúd (`castle_E6`) as Rhûn; culture + notables stayed dwarven. Log forensics showed the fief queued toward `khuzait` **16 times over four play-days with zero completions**. The shipped conversion + notable-replacement code (#325) was healthy — the bug was in the pre-existing *queueing* lifecycle.

## Root cause

`CultureConversionService.OnSettlementConquered` (in the feature since 2026-06-02) called `record.StartPending(nowDays, ownerCulture)` on **every** `OnSettlementOwnerChanged` event, resetting the 45-day clock each time. The engine fires that event **twice per conquest** — `ApplyBySiege` at capture (owner = kingdom leader), then `ApplyByKingDecision` at the synchronously-resolved fief-grant election ~1 in-game day later (verified in installed 1.4.6: `ChangeOwnerOfSettlementAction.ApplyInternal` dispatches unconditionally; `KingdomManager.SiegeCompleted` → `SettlementClaimantCampaignBehavior` → `Kingdom.AddDecision` resolves the AI election inline → `ApplyByKingDecision`). Add kingdom re-grants, barters, and same-culture recaptures, and a contested frontier fief re-queued faster than it could ever hold 45 days.

## Fix

A same-target guard in `OnSettlementConquered` (after the recruitment-pool + player-owned gates, before `StartPending`): if a timer is already pending toward the new owner's culture, continue it instead of restarting. A *different* target still restarts; a recapture by the effective culture still cancels. Cancel + stale-drop paths now log at DEBUG (both were silent, which slowed triage).

## Findings table

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | MED | Hold-timer restarts on every ownership event → contested fiefs never convert | Campaign mechanics / event multiplicity | The 2026-06-02 feature + its reviews assumed `OnSettlementOwnerChanged` = one fire per conquest. Every test seeded a *single* `OnSettlementConquered` call; none fired the event twice for one logical takeover, so the restart-on-refire was invisible to the suite and to both the deep-review data-flow trace and the Codex pass (which reasoned about the completion path, not repeated queueing). | Fixed + 2 regression tests firing the event twice. LESSONS-LEARNED (Campaign Mechanics): a CampaignEvent hook must be tested under its real firing *multiplicity*, and any "start/reset on event" must be idempotent across the engine's repeat-fires for one logical action. |

## Why the #325 reviews didn't catch it

Out of scope, not a review failure: #325 added notable replacement at conversion *completion*; the timer-restart lived in the *queueing* path untouched by that changeset. The deep-review + Codex correctly bounded themselves to the diff. The lesson is about the *original* feature's test design (single-fire seeding), which this fix corrects.

## Deep-review of the fix

5 agents, 0 findings. Standards PASS; engine double-fire VERIFIED + guard proven more robust than a `ChangeOwnerOfSettlementDetail` whitelist (which would break mixed-culture grants + barter/gift/rebellion and is fragile to enum growth); efficiency 0 (log calls branch-guarded); completeness COMPLETE; data-flow full 5×7 state×event trace, 0 gaps, guard provably mutually exclusive with the effective-culture cancel-branch and correctly ordered after R4/R5.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
