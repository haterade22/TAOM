# RCA — NamedCompanions State Matrix (#127 + #184, Codex Review 2026-05-13)

## Top-line

Phase 9b commit fixing NamedCompanions Entity State Matrix completion (Prisoner / Fugitive skip, `_spawned` singleton reset) shipped TWO commits' worth of work:

1. **First commit (would have been wrong)** — subscribed `_service.ResetSession()` to `OnSessionLaunchedEvent`.
2. **Second commit (actual ship)** — Codex independent review caught that `OnSessionLaunchedEvent` fires AFTER `OnNewGameCreatedPartialFollowUpEvent` in vanilla `Campaign.OnNewGameCreated` (Campaign.cs:2078–2084), so the reset would have cleared `_spawned` *after* `SpawnCompanions` ran in the same session, defeating the idempotence latch within a single campaign session. Re-bound to `OnNewGameCreatedEvent` (line 2080, dispatched before partial-follow-up at line 2083).

Net: bug never shipped because the codex-verify gate held. But the gate was the only thing between "passes 1944 tests + builds clean" and "wrong production wiring."

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | MEDIUM | `OnSessionLaunchedEvent` fires AFTER `OnNewGameCreatedPartialFollowUpEvent` for new-game flow. Reset would land after spawn, clearing the latch within the same session. | Lifecycle-ordering misread | Followed the audit issue's literal fix sketch ("subscribe `OnSessionLaunchedEvent`") without verifying the event dispatch order against decompiled `CampaignEvents.cs`. The audit author had the right intent (reset for next campaign) but specified the wrong event. | `feedback_taleworlds_event_dispatch_order_verify.md` — when a fix wires a new event subscription, decompile the event's call site in `CampaignEvents.cs` to confirm the firing order against other events the feature already subscribes to. The audit issue specifies WHAT to subscribe; the developer must verify WHEN. |

## Root-cause pattern

Audit-spec-vs-codebase mismatch. The audit fix-sketch in issue #127 said "subscribe `OnSessionLaunchedEvent` in `NamedCompanionBehavior` to call [ResetSession]." The developer (this session's agent) trusted the audit. Codex independently re-read the decompiled `Campaign.cs` and `CampaignEvents.cs` and found the dispatch order doesn't support the audit's spec.

**The class of bug:** "the audit was sometimes wrong" — exactly the bias Phase 9a verification was supposed to catch. But Phase 9a focused on whether the BUG still existed; it did NOT cross-check whether the audit's PROPOSED FIX would actually work. The fix sketch is unverified.

## Why deep-review missed this

`/deep-review` wasn't invoked for this fix (single feature module + thin scope, the "skip for one-line wiring fixes" rule). But even if `/deep-review` had run, its standards-compliance / efficiency / completeness / data-flow agents work in-codebase — none of them decompile vanilla source to check event dispatch order. This finding required Codex's adversarial-vs-vanilla-source mindset.

## Feedback memories to codify

1. **For Phase 9b agents:** when an audit fix-sketch specifies a TaleWorlds event subscription or a Harmony patch target, the audit's spec is a starting hypothesis, not an authoritative answer. Verify the event dispatch order (for events) or method signature (for patches) against decompiled source before committing. This is the same discipline as `feedback_taleworlds_vm_setter_decompile.md` but extended to event subscriptions.

2. **For codex-verify scope:** continue running it on cross-feature / state-machine / lifecycle fixes, even when "the change looks mechanical." The 1944-test green baseline didn't catch this; ONLY the cross-checking of decompiled source did.

## Commit references

- (would-have-been-wrong) — never landed because Codex review preceded commit
- Commit (TBD this session) — final fix using `OnNewGameCreatedEvent` per Codex finding
