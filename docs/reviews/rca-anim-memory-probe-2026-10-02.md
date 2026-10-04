# RCA: animation clip memory probe review findings (plan 036, 2026-10-02)

## Summary

The six-lens review of plan 036 (`141a20f1..2f560c6c`) found no HIGH or MEDIUM defect. The native read
path held up: every lens re-derived the signature, both targets and the 12 MiB budget on the installed
v1.5.3 binary, and no read happens before the section and alignment checks. Ten LOW findings were
confirmed. Nine are fixed in the review commit, and one (unparsable trailers on `2f560c6c`) needs a
history rewrite at merge. They fall into three themes: the end of a mission was modelled on one engine
path and one log line; two guards (the no-write test, the unguarded parser branches) proved less than
they claimed; and two doc statements described the budget and the loading query too simply.
Report: `docs/reviews/deep-review-036-anim-memory-probe-2026-10-02.md`.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| C1 | LOW | The lifecycle overrides quoted no caller line | Convention inconsistency | The plan quoted the callers in its own text, and the executor treated that as covering the rule; the rule asks for the line in the override's comment | Fixed; the existing rule already covers it |
| C2 | LOW | `End` dropped the partial window's min, max and window counts | Stale state / lifecycle | The plan specified "a summary at mission end" and the session was built to that; nobody compared the summary's fields with the periodic line's to see which ones only the periodic line carries | Fixed with a tail line and two tests; lesson in `lessons/misc.md` |
| C3 | LOW | The summary was written only from `OnEndMission`, which a teardown that never calls `Mission.EndMission` does not reach (it skips `EndMissionInternal`); `OnMissionStateFinalize` still calls `OnRemoveBehavior` on every behaviour (`Mission.cs:2228`, `:4716`) | Missing vanilla gate | The caller of `OnEndMission` was opened (`EndMissionInternal`), but not every path that tears a mission down. The review first gave a false reason (a network client never reaches `EndMissionInternal`); a client that calls `EndMission` reaches it through `CheckMissionEnd`'s else branch (`:4885-4888`), corrected in convergence round 1 | Fixed with `OnRemoveBehavior`; lesson in `lessons/state-lifecycle-save.md` |
| C4 | LOW | The feature doc said eviction starts at 12 MiB; the loader schedules it past 15 MiB, and the pass frees the excess over 12 MiB it measured at its start, evicting idle clips only (so it can end above 12 MiB; corrected in convergence round 1) | Logic error (doc) | The engine note recorded the `0xF00000` compare, but the feature doc was written from the budget float alone | Fixed in the feature doc; lesson in `lessons/native-cpp-port.md` |
| C5 | LOW | The cost paragraph did not say the loading query walks every clip record | Other: incomplete cost statement | The query was costed by its managed signature, not by its native body | Fixed in the feature doc |
| C6 | LOW | `Behavior_MakesNoNativeWrite` could not see the write overload of the adapter's own `Marshal.Copy` call, or an `unsafe` store | Other: guard weaker than its name | The guard listed write-named APIs; `Marshal.Copy` writes or reads depending on argument order | Fixed, proven by mutation; lesson in `lessons/testing-qa.md` |
| C7 | LOW | Five guard branches had no test | Missing null guard (untested) | The tests followed the plan's test list, which named the reasons a real module fails, not the parser's defensive branches | Fixed, each proven by mutation; one-off |
| C8 | LOW | The feature doc's Configuration section named only the heartbeat toggle | Convention inconsistency | The new toggle was documented in its own section and the shared section was not revisited | Fixed; one-off |
| C9 | LOW | The disabled line said no samples would be logged, yet a summary follows a mid-mission disable | Logic error (message) | The line was written for the arming failure, the common case, and reused for the mid-mission one | Fixed ("No further ... samples will be taken"); one-off |
| C10 | LOW | The `Refs:` and `Not-tested:` trailers of `2f560c6c` do not parse | Convention inconsistency | Wrapped trailer lines were not indented, and nothing checks trailer parsing at commit time | Orchestrator rewords at merge; this review's commit indents its continuation lines; one-off |

## Root-cause pattern

C2 and C3 share one theme: **the end of a mission was designed as one event with one output.** The plan
named `OnEndMission` and "a summary", and both were implemented exactly. The engine has two teardown
paths (end, then finalize; or finalize alone, when `Mission.EndMission` never ran), and the session has two kinds of
aggregate (per window and per mission). A diagnostic's end-of-life output has to cover every path that
ends it and every field its periodic output carries.

C6 and C7 share a second: **a guard named for a property it does not fully check.** "Makes no native
write" checked three API names; the parser's defensive branches existed but no test exercised them. Both
were proven by mutation in the fix, which is the check that would have exposed them first.

## Why each agent missed these

The lenses found all ten; this section records which lens could not have seen which.

- **Agent 1 (Standards)** found C1, C10 and the C2 gap. It did not flag C6, because a guard's coverage
  is outside the standards checklist.
- **Agent 2 (Engine compatibility)** found C3 to C5. It left the quit-to-menu path UNVERIFIED; the lead
  resolved it (`MBGameManager.EndGame` ends the mission and waits) and found the finalize-only path,
  which it misattributed to a network client (corrected in convergence round 1).
- **Agent 3 (Efficiency)** noted C9 outside its lens. Its per-frame proposal is a design question, not a
  defect.
- **Agent 4 (Completeness)** found C6 to C8 and the missing issue.
- **Agent 5 (Data flow)** found C2 and C6. It traced `OnEndMission` as CONNECTED because it followed
  the single-player path only.
- **Agent 6 (Design)** found the duplicated negative-counter rule, which the review applied (behaviour
  preserving). Its scope is simplification, not defects.

## Feedback memories to codify

None beyond the four lessons appended to `docs/reviews/lessons/` (`state-lifecycle-save.md`,
`testing-qa.md`, `native-cpp-port.md` and `misc.md`). The lifecycle lesson extends the existing
"An engine lifecycle virtual's firing set is read from its caller" (`lessons/state-lifecycle-save.md`):
this is the same category for the second time, so the extension names the condition on the caller, not
only the caller.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `2f560c6c..78f654d9` | R1-1: C3's stated reason, "a network client never reaches `EndMissionInternal`", is false; the fallback itself is right | `89f1efea` (wording); the `78f654d9` body is owed before merge | The lead read the `!GameNetwork.IsClient` branch of `CheckMissionEnd` and not the else branch (`Mission.cs:4885-4888`) | A claim that a path never reaches a method reads every branch of the gate, the else included (the corrected lesson in `lessons/state-lifecycle-save.md`) |
| 1 | same | R1-2: `Behavior_EndsTheSessionOnRemoveBehaviorToo` passed with `EndSession();` deleted | `89f1efea` | The test pinned the base call's text, not the call it is named for: the C6 and C7 pattern inside the review's own fix | Run the deletion mutation for the line a test is named for (the `lessons/testing-qa.md` mutation rule) |
| 1 | same | R1-3: "each pass trims it to 12 MiB or less" is not what the binary does | `89f1efea` (docs); the `78f654d9` body is owed before merge | C4's fix was written from the budget float and the 15 MiB compare, not from the loop's exit test | A native claim about a loop states its exit condition from the decompiled line that tests it |
| 1 | same | R1-4: default on for every player, and `FOR-MIKE.md` said to turn the probe on and restart | not a defect: NEEDS MIKE 1 | Raised already as N1; round 1 added the contradiction in the run folder, which the lead may not edit | The orchestrator aligned `FOR-MIKE.md` and the issue draft with the shipped default after round 2 |
| 2 | `78f654d9..89f1efea` | R2-1: the owed commit-message corrections were recorded only in the R1-1 and R1-3 rows; the lists the merge step reads named only C10 | the orchestrator's records commit after `89f1efea` | A fix pass edits the rows it fixes; nothing asks it to update NEEDS MIKE, the VERDICT and the REVIEW-LOG entry in the same edit | A correction that needs a history rewrite goes into the report's NEEDS MIKE list in the edit that finds it, and the merge step reads that list |
| 2 | same | R2-2: the engine notes' Eviction row still described a live-counter stop | the same records commit, with the Probe row and the residency bullet | R1-3's sweep covered the feature doc, the lesson, the RCA and the record, not the engine reference the feature doc links to | A corrected engine fact is swept through `docs/reference/engine/` as well, where AGENTS.md sends engine readers first |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2:
the review workflow's fix pass records a round only in the report.

## Codex round (2026-10-03)

One Codex adversarial review of `141a20f1..50df8cbf` raised three items, all confirmed against the
installed binary and the decompile and fixed in the pass that follows it (report: "Codex review
(2026-10-03)", issue #718).

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| X1 | P2 | The docs and the plan said a total far below 12 MiB rules out both levers; a clip's first load blocks a worker whatever the total, and a one-second sample taken between agent ticks cannot see it | Logic error (doc) | Every native claim was checked for truth, and none for what the measurement can reject; the feature's stated purpose ("decides between the levers") went into the reading guide unchallenged | For a diagnostic meant to decide, list per lever the observation that would reject it and the one the probe lacks, and write the gap beside the reading guide |
| X2 | P3 | The teardown test pinned source text, so deleting the line that writes the summary, or the line that clears the session, left it green | Other: test proves less than its name (third time, after C6 and R1-2) | R1-2 tightened the source match and stopped, because the plan called the callbacks "not testable offline"; nobody tried constructing the behavior, which `MountDespawnOffThreadTests` does against the game assemblies | Before pinning a callback by source text, check whether a `RequiresGame` test can drive it; where one can, drive it and run the deletion mutation for each line the test is named for |
| X3 | P3 | A comment said every address is proven before it is read; the header page, the code copy and the module's lifetime are trusted, and the engine's loading query is outside every check | Other: comment broader than the code | The comment was written for the arming path, which the reviews read closely, and it covered a class that also forwards an engine call nobody traced; the rollout cost sat in the review record and not in the doc that ships the default | Write a safety comment as three lists (checked, trusted, outside the checks); a doc that ships a default lists what was not measured |

Three lessons were appended (`lessons/testing-qa.md`, `lessons/native-cpp-port.md` twice).
