# RCA: the #551 / #552 changeset review (2026-09-06)

Three findings survived verification out of the `/deep-review` pass on the fix for crash bundle
`31942985`. Two were mine and one was a real defect introduced by the fix itself. All three were
fixed in the same session.

The fix under review: an enlisted player was torn out of a map event he had just joined, because
`EnlistmentBattleBehavior.OnMapEventEnded` gated on `State == EnlistedBattle` rather than on the
identity of the event that ended. Full chain in [enlistment.md](../features/enlistment.md).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `EnlistmentReconciler._staleBattleLatchSinceDays` is per-campaign state on a `Reuse.Singleton`, never reset between campaigns. A campaign ending while latched leaves an absolute campaign-day anchor behind; loading a later save makes `elapsed` enormous, so the recovery fires on the first latched tick and finishes a live loot encounter with no real waiting. | Singleton session-reset | The field is cleared on every non-latched tick, which makes it look self-managing within a session. The cross-session question was never asked, and the doc comment I wrote asserted the opposite ("a save/load restarts the clock") without checking the lifetime. | `ResetForNewSession()` on `IEnlistmentReconciler`, dropped from `ServiceMaintenanceService.ResetSessionCaches`; plus a backwards-clock re-anchor for the new-campaign path that reset never reaches. Tests: `StaleBattleLatch_ResetForNewSession_DropsTheAnchor`, `StaleBattleLatch_ClockRanBackwards_ReAnchorsInsteadOfRecovering`, and two wiring tests on `ResetSessionCaches`. |
| 2 | MED | Doc comments in the patch, the feature doc and the harmony registry named `MapEventSide.CheckSimulationSkillUpgrades`. No such method exists; the two cited dereferences live in `ApplySimulatedHitRewardToSelectedTroop`. | Fabricated signature | I read the two dereference SITES (correct line numbers, correct behaviour) but never scrolled up to read the enclosing method's name, then wrote a plausible one. | Corrected in all three files. The durable lesson is below. |
| 3 | MED | `EnlistmentBattleBehavior.OnMapEventEnded` walked `InvolvedParties` twice unconditionally, on a path that runs for every map event ending in the world while enlisted. | Efficiency | `mainPartyInvolved` was computed eagerly on the line above the `&&`, so the short-circuit I thought I had never existed. | Reordered so the cheap reference-comparison walk gates the expensive id-resolving one, with a comment saying the operands must not be tidied back. |

## Root-cause pattern: I asserted engine and lifetime facts I had not read

Findings 1 and 2 are the same failure wearing different clothes. In both, I wrote a confident,
specific, checkable claim into a durable artifact without running the check:

- Finding 2 asserted a method NAME from having read its body.
- Finding 1 asserted a LIFETIME ("a save/load restarts the clock") from having read the field's
  in-session behaviour.

Both claims were the kind that a single command would have settled: `grep` for the method name in the
decompile, `grep` for the IoC registration lifetime. Neither was run, because in both cases I had
just read something adjacent and felt informed.

`.claude/rules/evidence-over-claims.md` §C already covers this exactly, and lists "function/field
signatures" among the things never to state unverified. The gap is not that the rule was missing. It
is that **the rule fires loudest when you know you are ignorant, and this failure mode happens when
you feel informed**. The fabricated name came immediately after correctly reading the code around
it. Proximity to the truth is what made the guess feel safe.

The practical form: when writing a durable artifact, every proper noun (type, method, field) and
every lifetime claim is a separate assertion needing its own evidence, even when the surrounding
paragraph was verified. Reading `MapEventSide.cs:1050` does not license naming the method at
`:1031`.

## Why each agent missed these

| Agent | Why its rule set did not fire |
|---|---|
| 1 Standards | Explicitly checked the singleton session-reset rule and concluded no reset was needed, reasoning that the field is "cleared when the shape is absent". That is true within a session and misses the cross-campaign case entirely. Its scope is per-file compliance, so it had no reason to open the IoC registration or the load hook. |
| 2 Compatibility | Caught finding 2, by decompiling `MapEventSide` in full rather than the cited lines. Did not look at finding 1, which is not an engine question. |
| 3 Efficiency | Caught finding 3, though it under-read the severity: it believed the second walk only happened when the commander was involved. Its analysis of the `&&` ignored that `mainPartyInvolved` was computed eagerly on the previous line. |
| 4 Completeness | Verified tests exist for every new guard, and they do. Finding 1 has no missing-test signature: the guard it needed did not exist, so there was nothing absent to notice. Also misattributed another session's uncommitted CLAUDE.md edits to this changeset. |
| 5 Data Flow | Caught finding 1, by tracing the field's lifetime out of the class into the IoC registration and the load hook, then finding `ResetSessionCaches` and asking why the new field was not in it. This is the third consecutive review where the data-flow agent found the one defect the other four could not. |

## The agents are claims, not verdicts

Every finding above was re-verified against the source before being acted on, per
`evidence-over-claims.md` §A. That mattered twice:

- Agent 4 credited this changeset with CLAUDE.md doc-count edits (skills 40→41, feature-map 74→98)
  that belong to another session's uncommitted work in the same tree. `git diff` settled it. Acting
  on that report would have meant claiming another session's work.
- Agent 3's severity reasoning for finding 3 was wrong in the safe direction. The defect was real
  and slightly worse than described.

## Finding 1 is a repeat, which raises the bar for its preventive action

`lessons/state-lifecycle-save.md` already carried "A process-singleton per-campaign cache must clear
on `OnSessionLaunchedEvent`", written after CaravanTrade's `CaravanVisitMemory` leaked across an
in-process campaign switch (#335). This is the second instance, and the consequence is a step worse.
A stale keyed cache mis-scores a few entities and heals as entries are overwritten; #335 self-healed
within four town visits. A stale ABSOLUTE campaign day does not heal at all, because it does not get
overwritten, it gets compared: the leftover value makes `elapsed` enormous and the gate passes on the
first tick.

So the preventive action is deliberately two guards rather than one reset. The reset alone would have
left the brand-new-campaign path open, since `ResetSessionCaches` is wired to `OnGameLoadedEvent` and
never fires for a new game. The appended lesson names the timestamp variant specifically, and names
that `OnGameLoaded`-only wiring gap, because the generic rule was already on file and did not stop
this.

## Feedback memories to codify

One, for the pattern behind findings 1 and 2:

**A verified paragraph does not verify the nouns inside it.** When writing a durable artifact (doc,
registry entry, code comment), each type/method/field name and each lifetime or ownership claim needs
its own evidence, even when you have just read the surrounding code. The fabrication risk peaks
immediately after correct research, not during ignorance.

Findings 3 needs no memory: it is an ordinary efficiency miss, caught by the agent whose job it is.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
