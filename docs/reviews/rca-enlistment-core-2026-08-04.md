# RCA — Enlistment core checkpoint review (2026-08-04)

First `/deep-review` of the #375 Enlistment native rewrite (Phase 1 core + Phase 2.1/2.2
menus/battle). 5 agents; compatibility came back clean (37 verified / 0 incompatible / 0
unverified — the Phase 0.2 pre-verification sweep did its job). 7 findings confirmed, all
fixed in-session before the checkpoint commit; none reached HIGH-as-shipped severity
because the feature is not yet player-reachable (no enlist dialog until Phase 2.3).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | LOW | `EnlistmentMenuBehavior` hit 153 lines (ADR-002 ceiling 150) | standards | Two post-hoc additions (post-battle `EnsureParked` closure, `SaveKey` const) landed after the last line-count check | Presentation extracted to `EnlistmentWaitMenuPresenter`; count on every entry-point edit, not once at creation |
| 2 | HIGH (per-frame) | Wait-menu tick allocated a `TextObject` and ran `CampaignObjectManager.Find<Hero>` (linear scan) every frame | efficiency | Tick handler mirrored the donor's per-tick text refresh without asking *what actually changes per tick* (commander names are static mid-service) | Text now built once per menu init in the presenter; position sync throttled to every 5th tick |
| 3 | MED | `_reportedUnknownIds` diagnostic set unbounded | efficiency | "Menu-id universe is small" assumption left unstated | Capped at 100 with clear-on-overflow + regression test |
| 4 | MED (latent) | `Assess` commander-fitness omitted `IsPrisoner` while its two sibling predicates (`IsCommanderFit`, `ReconcileGrace`) checked it — masked only by the unverified engine correlation "captured hero ⇒ `PartyBelongedTo == null`" | parallel-method consistency | The same fitness criterion was hand-written three times in one session; no single named predicate, so drift was invisible per-file | `IsPrisoner` added + two pinning tests (Assess + reconciler, prisoner WITH live party). Follow-up idea for content phase: fold into one `CommanderSnapshot.IsFitForService` |
| 5 | LOW | `ReconcileBlocked` switch had no `default` arm — an unhandled `AttachmentBlockReason` would no-op silently | data flow | Defensive enum value (`NotInAttachableState`) produced but consumer written only for today's reachable set | Logged `default` arm added |
| 6 | MED | Player-captivity detection was hourly-tick-only — up to an hour where the wait menu could tick against a party vanilla considers captive | event coverage | The state machine gained a captive STATE without asking *which EVENT drives the edge*; the donor (whose event list seeded ours) never subscribed captivity events either | `HeroPrisonerTaken`/`HeroPrisonerReleased` listeners added (signatures verified on installed 1.4.7); lesson generalized below |
| 7 | LOW | Dead/undocumented data: `EnlistedAtDay` round-tripped but never read; `EnlistedDetachedOnDuty` + 4 `DischargeReason` values consumer-wired with zero producers, undocumented; store's parse-failure reset bypasses `SaveNormalization` with the constraint unstated | data flow | Content-phase seams built ahead of their producers without reservation notes | `EnlistedAtDay` now shown in the status inquiry; reservation notes on the enum values; the SyncData-timing constraint documented on `SaveNormalization` |

## Root-cause pattern

Two systemic threads:

1. **Copying the donor's cadence instead of deriving it.** Findings 2 and 6 are the same
   miss: the donor drove everything from polling (4 Hz tick, per-tick text), so the
   rewrite inherited poll-shaped handlers and a poll-only event list. The state machine
   was designed first-principles; its *drivers* were not.
2. **Three hand-written copies of one predicate.** Finding 4 is the classic parallel-
   method drift the data-flow agent exists to catch — writing the commander-fitness
   criterion three times in three files during a single session guaranteed eventual
   divergence.

## Why each agent missed these (for the ones caught late)

All seven were caught by this review — none shipped past it. Noted instead: the
compatibility agent's clean 37/0/0 result is downstream of running the Phase 0.2
signature sweep BEFORE implementation; the sweep also caught the `out TextObject hint`
parameter and `CampaignVec2` drift that would otherwise have been compile-time surprises.
Front-loading the decompile pass measurably de-risked the patch layer.

## Lessons to codify

- **For every state-machine edge, name the driving event — or explicitly accept polling
  latency.** Appended to `docs/reviews/lessons/state-lifecycle-save.md`. (The append is
  deliberately NOT staged with this checkpoint commit: the lessons file carries another
  session's uncommitted edits; it rides with the ship-phase docs commit.)
- Finding 4's centralization (single named fitness predicate) is queued for the content
  phase rather than done now — three call sites with pinning tests is acceptable; a
  fourth caller is the trigger.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
