# RCA: the render-wait marker and the shader-compile watchdog deferral (2026-09-04)

**Feature:** `BattleLoadDiagnostics` phase 4f `WaitingForRender` + `ShouldDeferForShaderCompile`
**Issue:** [#539](https://github.com/haterade22/TAOM/issues/539)
**Motivating artifact:** player crash bundle `taom_crash_20260904_211527_b18f3441` (TAOM v2.0.26, Bannerlord v1.4.8.119303)
**Review:** `/deep-review` x2, 5 agents each. 9 confirmed findings (3 HIGH, 2 MEDIUM, 4 LOW), all fixed before commit. Pass 2 reviewed pass 1's fixes and found three more, two of them HIGH, all three introduced BY those fixes.

## Top line

The change itself was correct in its diagnosis and wrong in one of its safeguards. It correctly
identified that a 305-second "stall" was the engine serially compiling ~700 cold character shaders
behind `SceneView.ReadyToRender()`, and it correctly closed the 290-second instrumentation hole that
made that undiagnosable. But the watchdog deferral it added to stop the false positive had **no upper
bound**, which would have converted a false alarm on a healthy load into permanent silence on a
broken one.

The lesson that would have prevented it was already written down in this repository, in the same
subsystem, about the same engine counter. It was not consulted.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `ShouldDeferForShaderCompile` refreshed its progress clock on ANY change to the shader count, in either direction, with no absolute cap. A count thrashing among positive values (compiles completing while new requests arrive) defers the watchdog on every poll forever: no `STILL LOADING` line, no bundle, for a load that never finishes. | State / lifecycle | Wrote the predicate from the b18f3441 evidence, where the queue WAS draining, and never asked what a non-draining but changing queue does. `ShaderPrecompileDecider` in this repo already carries a named `ChurnTimeout` abort for exactly this ("count > 0 CONTINUOUSLY, churns without ever settling", added after the 1.4.7 precompile hang) and it was not read. | Added a 900 s absolute deferral cap plus a `churn-capped` token on the fired line. Lessons entry appended: a progress-based gate needs BOTH a no-progress timeout and an absolute cap. |
| 2 | MED | The triage tool's new `COMPLETED` scoping fell back to whole-log scanning whenever no `MissionInitialize` anchor existed, reproducing the same false `COMPLETED` the scoping fix was written to remove (verified: a synthetic no-anchor log with an early `BattlePlayable` and a later `FinishMissionLoadingDone` returned `COMPLETED`). | Tooling | Fixed the observed instance rather than the class. The fallback was written as "preserve old behaviour for truncated logs" without asking what old behaviour was, which was the bug. | Anchors widened to any mission-start phase; with no anchor at all the log is only called clean if it ENDS on `BattlePlayable`. Three tests, including the no-anchor multi-mission shape. |
| 3 | LOW | Both watchdog MCM hints still promised the watchdog "flags it as stalled" at the configured threshold, after the change made it hold off while shaders compile. | Docs / user-facing promise | Changed the behaviour behind a user-configurable number and did not re-read what that number's hint promises. | Both hints now state the deferral and its cap. |
| 4 | LOW | The feature doc's own changelog entry claimed "Patch43 went 17 to 18 hooks". The real transition is 18 to 19; 19 files carry the category attribute, and the prior entry called its own addition "the 18th hook". | Evidence / fabrication | A count written from memory of adjacent text instead of from a command. This is the `evidence-over-claims.md` §C trap verbatim, in the same session that quoted that rule. | Corrected, and the entry now states the count was verified by grep. |

## Root-cause pattern: a safeguard built from one observation

Findings 1 and 2 are the same mistake at two layers. Both were written to fix a specific observed
failure (a draining queue; an anchored multi-mission log) and both silently kept the failing behaviour
for the neighbouring case (a churning queue; an unanchored log). Neither had a test for the
neighbouring case, because the neighbouring case was never enumerated.

The tell is that both fixes were expressed as **conditions under which to keep going** rather than as
**bounds on how long to keep going**. A gate written as "proceed while X looks fine" needs a companion
"and never for longer than Y", or the gate's failure mode is unbounded. `ShaderPrecompileDecider` got
this right in 2026-07 with three independent stops: a no-progress timeout, a churn backstop, and an
absolute per-item cap. This change shipped into review with one of the three.

## Why each agent missed what it missed

- **Agent 1 (Standards)** passed correctly and even evaluated the sentinel-collision and
  latch-ordering rules by name. Neither rule covers "is the deferral bounded"; both are about
  correctness of state transitions, not about liveness. Its scope was right and its answer was right.
- **Agent 2 (API compatibility)** verified all seven engine claims and caught that my own prompt had
  misquoted the gate condition as `Handler.RenderIsReady()` when the source reads
  `!flag && (Handler == null || Handler.RenderIsReady())`. It got that by reading the body instead of
  trusting the prompt, which is the behaviour we want. Findings 1 to 4 are outside its remit.
- **Agent 3 (Efficiency)** passed. It measured the per-frame cost, correctly reported the native
  shader-count call as UNVERIFIED rather than inventing a figure, and then offered a microsecond
  estimate anyway, which is a softer version of the same failure. Liveness is not a performance
  question, so finding 1 was out of scope.
- **Agent 4 (Completeness)** reported COMPLETE and asserted the registry's hook count was correct.
  It was, but only in the registry; the agent did not cross-check the same count in the feature doc,
  where finding 4 lived. **This is the gap worth fixing**: "the count is right in file A" was reported
  as "the count is right", and its own per-file test counts were also wrong in the same report.
- **Agent 5 (Data flow)** found findings 1, 2 and 3. It reached finding 1 by enumerating the boundary
  states of the count and asking what each one does over repeated polls, rather than at a single
  instant. That temporal framing is what the other four lacked.

## Preventive actions taken

1. `docs/reviews/lessons/state-lifecycle-save.md` gains an entry on bounding progress-based gates.
2. The deep-review Agent 4 prompt should cross-check a stated count in **every** document that states
   it, not the first one found, and should never report a count it did not itself compute. Recorded
   here; not yet applied to the skill.
3. Reinforcement, not a new rule: finding 4 is `evidence-over-claims.md` §C. The rule needs no change.
   The failure was not consulting it while writing a number into a document.

## Review pass 2: reviewing the fixes

Pass 1's fixes were themselves re-reviewed by the same five agents, scoped at the new code. That
found three more defects, two of them HIGH, and **all three were introduced by the pass-1 fixes**.
That is the headline: the fixes for a review were more defective than the code the review examined.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 5 | HIGH | The `shaders=N ` token that pass 1 added to the `WATCHDOG STILL LOADING` line sits between the em-dash and the literal `last`, but `triage_battle_load.py`'s `_WATCHDOG_RE` required whitespace there. The regex therefore matched ONLY the token-free shape, so every bundle from a current build had its watchdog line silently dropped: no `tl.watchdog`, no "Stall watchdog fired" section, in a report whose entire subject is a fired watchdog. | Cross-language contract | I verified the tool against bundle b18f3441, whose log PREDATES the token and so structurally could not expose the break. The Python fixture `_watchdog()` had the same blind spot: it emitted the old shape, so the regex stayed pinned to a string the C# side had stopped producing. | Regex now captures an optional token block (non-greedy, so a status line containing "last" still parses) and the report SURFACES the tokens. Fixture takes a `tokens=` argument; six new tests cover both shapes plus the churn case. A C# test pins the composed line so both halves of the contract are asserted. |
| 6 | HIGH | `classify` (verdict) anchors on any mission-start marker; `classify_phase_timings` (bucket ledger) anchors on `MissionInitialize` alone. When the last mission stalls BEFORE `MissionInitialize`, the ledger silently falls back to an EARLIER mission, and the report prints a stall verdict above a healthy timing table describing a different load. | Two summarizers, one report | Pass 1 fixed the verdict's scoping and never asked whether the OTHER consumer of the same event list had been scoped too. The pass-1 test asserted only `assertNotEqual(kind, "COMPLETED")` on a log with no bucket markers, so `classify_phase_timings` returned `None` and the divergence could not surface. | The ledger now refuses to render when its anchor falls outside the verdict's mission. Five tests, including one asserting the rendered report does not contradict itself, and one proving a genuine two-mission log still renders the LATER ledger. |
| 7 | MED | The churn cap measured time since `BattleLoadLoadingWindow.OpenedAtUtc` (Mission.Initialize), but compilation only starts at `Scene.ResumeLoadingRenderings`. A siege scene spending 550 s in native setup had only 350 s of the advertised 15-minute allowance left. | Wrong clock | I wrote the cap by analogy to `ShaderPrecompileDecider`'s `ChurnTimeout` and used the clock that was already in the method. The decider's `ChurnTimeout` measures CONTINUOUS non-zero time and resets on every dip to zero; the clock I used is its separate `AbsoluteTimeout` concept. Citing a precedent is not the same as reading it. | `BattleLoadRenderWaitProbe.SecondsCompilingContinuously` mirrors the decider's semantics exactly (starts on the empty-to-non-empty edge, resets on a drain). Six probe tests plus a watchdog test for the long-pre-render case. |
| 8 | LOW | `FormatChurnToken` re-derived `ShouldDeferForShaderCompile`'s three terms instead of sharing them. Correct at every input today, verified exhaustively by the data-flow agent, but structurally free to drift. | Two seams, one decision | Flagged independently by the standards agent and the data-flow agent. I wrote the token as a formatter and only afterwards noticed it was a second copy of a decision. | Replaced the boolean predicate and the formatter with one `Decide` returning `Defer` / `FireWedge` / `FireChurnCapped`; the token derives from the verdict and cannot contradict it. |
| 9 | LOW | The hook comment and the feature doc cited `MissionState.cs:196-200` for the render gate. It is at line 110 in both the taom-src cache and the categories dump. | Evidence / fabrication | A line number written from memory rather than from a command. **Second instance in this session** after the hook count (finding 4). | Corrected to 110-113. The repeat is why the lessons entry below is phrased as a habit, not an incident. |

### Root-cause pattern for pass 2: a fix inherits none of the original's scrutiny

Findings 5, 6 and 7 share a shape. Each was written at the end of a session, to close a review
finding, under the implicit assumption that a small targeted change carries small risk. Each then
did the same thing the original code had been faulted for:

- Finding 5 added a token to a line without checking the parser that consumes it, exactly as the
  original change had added a marker without checking the window that consumes it.
- Finding 6 fixed one consumer of the event list and left its sibling, exactly as the original had
  fixed one bucket and left its neighbour.
- Finding 7 cited a precedent instead of reading it, exactly as the original had inherited none of
  that precedent's three stops.

**The operative lesson: verification performed against the artifact that motivated the change cannot
see a defect the change itself introduces.** I replayed bundle b18f3441 after every pass-1 fix and it
looked correct every time, because b18f3441's log predates the token whose handling I had just
broken. A fix needs a test written from the NEW shape, not a re-run against the OLD evidence.

### Why each agent missed what it missed, pass 2

- **Standards** passed and independently flagged finding 8, the same duplicated condition the
  data-flow agent found. Findings 5 to 7 are behavioural, outside its rule set.
- **API compatibility** verified all three engine signatures the doc comment claims, confirmed
  `FirstMissionTickAfterLoading` is public, correctly reported the native shader counter's
  monotonicity as UNVERIFIED rather than guessing, and caught finding 9. It also found a narrow
  `MissionEndTime` corner where the probe goes stale; that fails safe (the stale reading ages past
  the no-progress window and the watchdog fires) and was left alone deliberately.
- **Efficiency** returned two MEDIUMs and **both were wrong**. It estimated `Stopwatch.ElapsedMilliseconds`
  at 10-20 microseconds and derived a 270 ms cost; measured, it is 154.5 ns as an upper bound, so the
  real figure is 2.8 ms, off by roughly 100x. Its suggested fix (only sample when the count changed)
  would have silenced the 1 Hz heartbeat exactly when the queue freezes, which is the case the marker
  exists for. Its own prompt says an unverified cost claim is reported as UNVERIFIED, never as a
  severity; that instruction did not hold.
- **Completeness** was told explicitly not to repeat pass 1's two failures (reporting counts it had
  not computed, and confirming a number in one document while the same number was wrong in another).
  It computed every figure and cross-checked both documents. That correction worked.
- **Data flow** found 5, 6, 7 and 8. For the second pass running it was the only agent to find a
  behavioural defect, and the only one that reasoned about the system over TIME (repeated polls, a
  whole report read top to bottom) rather than at a single instant.

## What is still owed

The in-game smoke is unrun and this is a player's machine, so there is no local reproduction. Owed: a
cold-cache first-encounter battle showing `WaitingForRender` lines at 1 Hz with a falling `shaders=`
count and no bundle written, then `BattlePlayable`. Neither the deferral nor the churn cap has
executed against a real engine.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/battle-load-diagnostics.md](../features/battle-load-diagnostics.md)

<!-- backlinks-end -->
