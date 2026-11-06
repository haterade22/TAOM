# RCA: Enlistment dead surface, the commander-party check and the daily column check (#842, #843, #844, deep-review, 2026-10-26)

## Top-line

`/deep-review` on the three commits for #842, #843 and #844 ran lenses 1, 2, 5 and 7 in a first wave and
lenses 3, 4 and 6 in a second. It found **no engine incompatibility and no runtime defect**: the party-id
compare returns the same value as the snapshot it replaces, every deleted member had no reader at the parent
commit, old saves and settings files with the removed entries still load, and the JSON edits are exactly the
retired keys. It found **two MEDIUM findings**, both about claims and proof rather than behaviour, and a set
of LOW items; all were fixed before the push.

## Findings

| # | Sev | Finding | Category | Why missed | Fix and preventive action |
|---|---|---|---|---|---|
| F1 | MEDIUM (Standards, Engine compat, Data flow, Design) | The #844 commit first added a rollback of the oath petition and described it to players as a fix; no shipped path reaches it (`OnOathSworn` runs `SubmitPetition` and `CompleteOath` back to back with the same fitness check, and a pending petition never blocked asking again). | A speculative guard sold as a player fix | The brief kept `WithdrawPetition` on a verifier's note without tracing the only caller. | The rollback and `WithdrawPetition` are deleted, as the issue planned; the pinned test is restored; the body is a cleanup with no player claim. The `PetitionPending` collapse stays a separate future change. |
| F2 | MEDIUM (Completeness) | Two tests could not catch a regression in the changed code: one still asserted the snapshot call that #842 removed, and nothing read the probe-to-snapshot mapping that #843 edited. | Tests pinned the old call, not the behaviour | Tests were updated for compile errors, not for what they prove. | The test asserts the new call with a commander set; a data-driven mapping test covers all seven kept fields. |
| F3 | LOW (Engine compat, Data flow, Completeness) | The #843 subject and comments called the probe hourly; it runs on the daily tick and on a duty request (the hour is only the cache key). | A cache key read as a schedule | The comment "one probe per game hour" was taken as the call rate. | Subject and comments say when it runs. |
| F4 | LOW (all lenses) | Leftovers of the deletions: an unused hour parameter through five signatures, an unread probe field, an unread counter (`DutyFailures`), an unused adapter method, a discarded constructor input, a three-line clamp class with one caller, a test double that copied the production ledger, a dead local, stale comments and unused usings; the modding reference still listed the removed reward entries. | A deletion stops at the first layer | Each deletion was checked for readers, not for what lost its last reader as a result. | All removed or corrected in the same commits. |

Noted, not changed:

- The army-rhythm snapshot's hourly cache now saves at most two probes a day; removing it changes two edge
  cases (a same-hour commander change, a second listener on the same daily tick). A design follow-up.
- The shipped `maxOfferChance` (0.4) differs from its compiled default (0.45), which sits above the highest
  reachable chance; older than this change.
- A commander defending a besieged town never gets the siege context, "moving" is also true while besieging,
  and siege credit may miss an assault led by an army; older than this change, design calls.
- A refused oath still leaves `PetitionPending` on record; no shipped path reaches it, and it blocks nothing.
  The state's collapse is its own change and needs an issue on the maintainer's word.

## Root-cause pattern

F1 and F4 share one cause: a deletion was judged one member at a time. Each step asked "does this member have
a reader", not "what does this deletion leave without a reader" or "who calls the member I am keeping". Ask
both before the commit, and trace the only caller of anything kept for safety.

## Feedback memories to codify

None; the lesson covers it.
