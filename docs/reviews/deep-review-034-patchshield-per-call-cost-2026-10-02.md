# Deep review: plan 034, PatchShield's per-call cost (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: PatchShield's finalizers stop binding __originalMethod; the original method is resolved
         from the stack only when an exception arrives (plan 034)
Date: 2026-10-02

Scope:   C# (TAOM.Dependencies PatchShield, PatchShieldPolicy comments), tests, docs.
         Commit 31f59582 over 64dbcac8 on perf/034-patchshield-per-call-cost.
Blast radius: 1 type checked with graphify affected (PatchShield, depth 2): production callers are
         Dependencies/SubModule.cs OnSubModuleLoad (:234) and OnGameInitializationFinished (:293);
         ResolveShieldedOriginal is internal and reached only from the two finalizers and the tests.
Waves:   Agents 1 to 6 in one wave (Standards, Engine compatibility, Efficiency, Completeness,
         Data flow, Design). Agent 7 (XML) and Tooling: NOT IN SCOPE (no XML, no gate changed).

STANDARDS:     PASS after fixes (2 dashes in new prose, now colons; 3 nits on comment wording and wraps, fixed)
COMPATIBILITY: PASS (0 incompatible, 5 unverified; 3 LOW, all fixed)
EFFICIENCY:    PASS (0 issues in the changed hunks; 3 FOLLOW-UP, 2 nits)
COMPLETENESS:  INCOMPLETE until this review: 1 MED test gap and 7 LOW, all fixed; the GitHub
               issue for plan 034 is not filed (maintainer's call)
DATA FLOW:     PASS after fixes (16 flows; gaps F1 documented, F4 tested; F3 fixed; F2 INFO)
DESIGN:        5 KEEP proposals (3 APPLY: P1 applied as a defect fix, P5 applied, P2 not applied;
               2 FOLLOW-UP)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

### Agent 1, Standards

All checks passed: no ADR-007 exposure (the new tests use only System, HarmonyLib, MSTest and
TAOM.Dependencies.Foundation), no banned construct, no new interface or IoC entry, test names follow
`MethodName_State_Expected`, no engine code in the new tests. The 17 dash characters on the branch's
added lines are retained words on long lines it rewrapped, except two: the dash after
`(ExcludedTargetNamespacePrefixes)` on dr3-maintenance.md:260 and the "named a three-factor
interaction" dash on arena.md:10 each introduce a clause the branch rewrote, so both were new prose
(both now colons, convergence round 1, below). Findings: LOW,
another mod patching PatchShield's own finalizer makes the lookup name the finalizer (row 1 below);
LOW, no constructor case (row 5); NITs on the try/catch phrasing, the 63 ns figure paired with the #331
stall, and uneven wraps (rows 12 to 14).

### Agent 2, Engine compatibility

0 incompatible. Verified against the shipped 0Harmony 2.4.2 (net472) decompile:
`GetOriginalMethodFromStackframe` is public and returns the frame's own method on a miss; every wrapper
rebuild registers the replacement; finalizers are called directly from the replacement inside protected
regions, so the judged frame is the replacement; `PatchProcessor.Patch` only accepts declared members,
so the resolved object is the one the old `GetMethodFromHandle` produced. Findings: L1, the 1.3 ns
figure was measured in a Release build while TAOM.Dependencies ships Debug (row 3); L2, a frame with no
method stops the lookup before Harmony's Mono address path can run (row 2); L3, no constructor target
(row 5). UNVERIFIED: a second Harmony copy with a private `originals` map, Mono itself, the MonoMod
`DMDDebug` route; each fails safe.

### Agent 3, Efficiency

No issue in the changed hunks. The benchmark's raw result matches the commit body (64.22 against 1.29
ns per call single-threaded, 240.7 bytes per call against 0, 1146.60 against 1.58 ns contended). The new
cost is a full stack walk on the exception path only, and every desktop session summary reads
"swallowed 0 ... rethrew 0". FOLLOW-UPs: F1, the SaveLoadDiagnostics finalizers bind
`__originalMethod` on per-object and per-container load methods run on TWParallel workers; F2, a
swallow that repeats writes diag.log on every call, and an unresolved original now repeats; F3, attach
time and codegen still count against re-including excluded targets.

### Agent 4, Completeness

Tests and docs present; CHANGELOG untouched. Findings: MED, no test runs the real finalizers' swallow
path when the lookup misses (row 4); LOW, a resolved lookup is never asserted to leave the miss counter
alone (row 6); LOW, constructors untested (row 5); LOW, the patched-finalizer case (row 1); LOW, the
no-miss summary is unpinned (row 7); LOW, dr3-maintenance.md lists two of the five miss reasons (row
8); LOW, harmony-il.md gives the #331 stall as both 104 to 109 s and 105 to 109 s (row 9); LOW, the
after-merge smoke never exercises the lookup (goes on the issue's checklist). The GitHub issue is a
draft only (`plans/_audit/2026-10-02-perf/issue-drafts.md:80`).

### Agent 5, Data flow

Traces 1, 2, 4 to 8, 11, 13 and 14 connected. Findings: F1 LOW, a miss is permanent for the process
and the docs did not say the swallow then repeats on every call (row 11); F2 INFO, a fresh replacement
is unmapped for a short window inside `UpdateWrapper` (not a defect, see below); F3 LOW, the patched
finalizer case (row 1); F4 LOW, the degraded swallow path untested (row 4); F5 NIT, two of five reasons
documented (row 8); F6 NITs, "names '?'" against the printed `?.?`, and the D6 citation without a path
(rows 10 and 15); F7 NIT, "close to 300" (false positive, below).

### Agent 6, Design and elegance

P1 (KEEP, APPLY): skip a frame whose original is the finalizer's own type; applied as the fix for row 1.
P2 (KEEP, APPLY): flatten the loop; not applied (below). P3 (merge the two finalizers) and P4 (reuse
`RethrowStackPreserver.Describe`): FOLLOW-UP, both need files plan 034 kept out of scope. P5 (KEEP,
APPLY): replace the hand-rolled occurrence counter with `Regex.Matches`; applied. Already optimal: the
live-stack walk over the exception's own trace, the never-throw try/catch, the log-once line.

## Findings, verified by the review lead

Every finding was re-read against the worktree before acting on it. The patched-finalizer case was
also run: the new test failed on the reviewed code with
`Expected:<...Targets>. Actual:<...PatchedProbe>. resolved ...PatchedProbe.Finalizer`.

| # | Sev | Finding (lenses) | Verdict | Action |
|---|---|---|---|---|
| 1 | LOW | Another mod's Harmony patch on PatchShield's finalizer runs its body in a replacement that Harmony maps back to the finalizer; the lookup returned the finalizer as the shielded method, so the swallow would strip that mod's patch from PatchShield's finalizer and leave the real offender (1, 2, 4, 5, 6) | CONFIRMED (RED test) | `PatchShield.cs:325` skips a frame whose original's declaring type is the finalizer's; test `ResolveShieldedOriginal_FinalizerPatchedByAnotherMod_ReturnsTheShieldedMethodNotTheFinalizer` |
| 2 | LOW | A frame whose `GetMethod()` is null stopped the lookup before `Harmony.GetOriginalMethodFromStackframe`, whose Mono path maps such frames by address (decompile `HarmonySharedState.GetStackFrameMethod`); the old binding worked there (2) | CONFIRMED (code read) | `PatchShield.cs:318-331` hands every frame to Harmony; identical on .NET Framework (Harmony returns null there, same reason text). Not testable on .NET Framework: commit trailer |
| 3 | LOW | Docs, comments and the lesson state about 1.3 ns per call as the new cost; that is a Release build, and TAOM.Dependencies ships Debug (2, 3 N2) | CONFIRMED (measured) | Re-ran the executor's benchmark as a Debug build: variant c 5.37 ns, a postfix-only patched method 1.85 ns, the b to c gap 64.38 ns (1,304.75 ns contended). Ten statements now give 5.4 ns as shipped, 1.3 ns optimized |
| 4 | MED | No test runs the real finalizer's swallow path when the lookup misses (4, 5) | CONFIRMED | `ShieldFinalizerVoid_LookupMisses_SwallowsNamesUnknownAndStripsNothing`, proven against a climbing mutation (below) |
| 5 | LOW | No constructor target (1, 2, 4, 5) | CONFIRMED | `ShieldFinalizerVoid_ShieldedConstructorThrows_MarkerNamesTheConstructor`, proven against a MethodInfo-only mutation |
| 6 | LOW | A resolved lookup never asserted to leave the miss counter alone (4) | CONFIRMED | Assertion added to `ResolveShieldedOriginal_CalledFromAFinalizer_ReturnsThePatchedOriginal`, proven against a count-every-lookup mutation |
| 7 | LOW | The no-miss session summary is unpinned (4, 6) | CONFIRMED | `WriteSessionSummary_NoMiss_ReadsAsBefore` (zeroes the counter by reflection and restores it), proven against an always-suffix mutation |
| 8 | LOW | dr3-maintenance.md lists two of the five miss reasons (4, 5) | CONFIRMED | All five listed |
| 9 | LOW | harmony-il.md gives the #331 stall two ranges in new text (4) | CONFIRMED | The new lesson now says 104 to 109 s, as `:6` and the RCA's 104,482 ms do |
| 10 | NIT | The miss line says the swallow line "names '?'", it prints `?.?` (2, 5, 6) | CONFIRMED | `FormatUnresolvedOriginal`, the XML doc, the pinned test line and the doc example say `?.?` |
| 11 | LOW | A miss is permanent: a foreign patch that keeps throwing is never stripped, so it is swallowed and logged on every call; the docs said only "nothing is unpatched" (5 F1, 3 F2) | CONFIRMED | dr3-maintenance.md says so; rate-limiting the line is behaviour-changing (NEEDS MIKE) |
| 12 | NIT | "paid a GetMethodFromHandle + try/catch" reads as if the try/catch went away (1) | CONFIRMED | Reworded: the call ran inside the try/catch any finalizer adds |
| 13 | NIT | PatchShieldPolicy pairs 63 ns with "that cost turned a teardown into a 104 to 109 s freeze", which the new lesson says the per-call time cannot explain (1) | CONFIRMED | Now names the binding, its 241 bytes per call, and points at the lesson |
| 14 | NIT | Uneven wraps: coop-interop.md:407 (145 characters), harmony-il.md:195 (153), PatchShieldPolicy.cs:73, bannerlord-together-compat.md:82 (1, 2) | CONFIRMED | Rewrapped while correcting the same lines |
| 15 | NIT | The test cites "maintainer decision D6" without its file (4, 5, 6) | CONFIRMED | Cites `plans/_audit/2026-10-02-perf/DECISIONS.md` |
| 16 | NIT | CreatureBanditsWiringTests credits the 1,145 ns figure to the #331 RCA (2) | CONFIRMED | The RCA is cited for the mechanism, plan 034 for the figure |
| 17 | MED | No GitHub issue for plan 034 (4) | NEEDS MIKE | Filing issues is outside this run's authority |
| FP1 | NIT | The commit body's "close to 300" (3 N1, 5 F7, 6) | FALSE POSITIVE | The desktop diag.log shows 295, 303 and 305 attached at later game starts (Agent 4); "close to 300" covers them. No amend |
| FP2 | INFO | A replacement is live before Harmony maps it, so a throw in that window misses once (5 F2) | FALSE POSITIVE as a defect | One transient, fail-safe miss that resolves on the next throw; the shape the design accepts |

**A test that proved nothing until this review.** The first version of row 4's test put only a foreign
prefix on the outer method. The climbing mutation left it green: the outer replacement's body then ends
`call; ret`, and the x64 JIT made it a tail call, so the outer frame was never on the stack. A postfix
keeps the frame, and the test then failed against the mutation (`Expected:<1>. Actual:<0>. the miss is
counted`: the climb resolved the outer method). Lesson in `lessons/testing-qa.md`.

**Mutation proof.** Mutations a (count every lookup), b (always print the summary suffix), c (climb past
an unresolved frame) and d (accept only a `MethodInfo` original) applied together failed 6 of 16
`PatchShieldFinalizerTests`, every new coverage test among them; c alone failed the bound test, the
lookup-miss test and the log-once test. The file was restored and compared byte for byte with the green
copy.

## Answers to the orchestrator's focus

1. **Which frame.** `new StackTrace(1, false)` drops the lookup's frame; frames of the finalizer's
   type are skipped, and now also a frame whose Harmony original has that type (row 1); exactly one
   frame is then judged. It cannot name an outer shielded method, a recursive caller's other frame, an
   unmapped dynamic method or another mod's prefix or postfix (callees that have returned). The cases
   the old binding caught that this misses are a replacement built by a Harmony copy with a private map
   and the MonoMod `DMDDebug` route (UNVERIFIED); Mono is now handled (row 2). On a miss: one counted
   miss, one diag.log reason line per session, the swallow decided by exception type alone, nothing
   stripped, the marker "an unknown method".
2. **ShouldSwallow and TryUnpatchOffendingPatches** differ from the base only by `MethodBase?`; the
   resolved object is the one Harmony keys its patch state by. Pinned by the swallow, strip and marker
   tests, and now by the miss test.
3. **RethrowStackPreserver:** no diff in the range or in this review; 19 of 19 pass in the filtered
   run (61 of 61 with PatchShieldPolicyTests and PatchShieldFinalizerTests).
4. **Threads:** `Interlocked` counter, `Interlocked.Exchange` once-flag, `DiagLog` under its lock;
   Harmony's `originals` lock runs no user code. TAOM.Tests sets no parallelization, so the two
   reflection resets in the tests cannot race.
5. **Deviations:** DiagLog is right (TAOM.Dependencies cannot reference Main); the 3 extra tests pin
   D6; two dashes in new prose, on dr3-maintenance.md:260 and arena.md:10 (each introduces a clause
   the branch rewrote; this review's own diff kept that dr3 line's six dashes and removed nine in
   all). `lint_docs.py --dash-base 64dbcac8` lists both lines, and its exit code is not evidence about dashes:
   `--fail-on-drift` never fails on them. Corrected in convergence round 1, below. "close to 300" is
   FP1.
6. **bannerlord-1.4.5:** its `PatchShield.cs:244` and `:255` still take `__originalMethod`, and it has
   no RethrowStackPreserver, so a port is an adaptation (the original is needed only in the swallow
   branch). NEEDS MIKE.
7. **Known failures:** only `EveryLanguage_DeclaresARowForEveryEnglishKey`, as at the base.

## Action items

1. File the plan 034 issue, with the in-game check that after a session with a swallow or a rethrow
   marker diag.log carries no "could not tell which shielded method" line and no "unknown N time(s)"
   suffix (Mike).
2. Decide the 1.4.5 port and the follow-ups listed below (Mike).

## IMPROVEMENTS (Step 4)

```
APPLIED:     PatchShieldFinalizerTests.cs: the occurrence counter is Regex.Matches (Agent 6 P5),
             behaviour-preserving; ResolveShieldedOriginal_CallerIsNotAReplacement_CountsEveryMiss
             AndLogsTheFirstOnce green before and after.
             PatchShield.cs:325 (Agent 6 P1) applied as the fix for row 1, RED first.
NOT APPLIED: PatchShield.cs:306-333, flatten the loop (Agent 6 P2): with row 1's guard the skip depends
             on Harmony's mapping as well as the declaring type, so "skip, then judge one" needs the
             Harmony lookup inside the predicate (a second lookup on the judged frame) or the loop
             back. Not simpler; the simplicity criterion rejects it.
FOLLOW-UP:   Agent 6 P3, merge ShieldFinalizerVoid and ShieldFinalizerWithResult (needs
             RethrowStackPreserverTests renames; changes names triagers know from crash bundles).
             Agent 6 P4, reuse RethrowStackPreserver.Describe (widens a private member plan 034 kept
             out of scope).
             Agent 3 F1, SaveLoadDiagnostics HeaderLoadData_Readers_Patch.cs:34 and
             ContainerLoadData_Fill_Patch.cs:65 bind __originalMethod on per-object and per-container
             TWParallel calls; measure the counts first.
             Agent 3 F2 and Agent 5 FU2, a repeating swallow logs on every call (behaviour-changing).
             Agent 3 F3, attach time and codegen when re-including an excluded target.
             Agent 5 FU1, EarlyLog.DrainTo has no caller, so TAOM.Dependencies' EarlyLog lines never
             reach a log ("Another 0Harmony.dll detected" among them).
             Agent 5 FU3, TryUnpatchOffendingPatches counts a finalizer-only owner as unpatched.
             docs/features/crash-report.md:283 stale twice over; RethrowStackPreserver.cs:60,
             harmony-il.md:582 and submodule-lifecycle-and-harmony.md:35 still show the
             (__exception, __originalMethod) shape, right for SaveShield only; PatchShield.cs:135's
             runtime line still says "finalizer tax".
             No issues filed: this run has no GitHub authority; each is in needs_mike.

VERDICT: READY FOR COMMIT
```

Final full suite after the fixes: `Failed! - Failed: 1, Passed: 12361, Skipped: 2, Total: 12364`
(the base of this review, 31f59582: `Failed: 1, Passed: 12357, Skipped: 2, Total: 12360`; the one
failure in both is `EveryLanguage_DeclaresARowForEveryEnglishKey`). Filtered PatchShield classes: 61 of
61. The RefAsm unit step was not run: the new tests reach no engine type.

## CODEX REVIEW

Codex not run: no adversarial review was dispatched for this item, so there is no Codex finding to
assess.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | n/a | n/a | n/a | Codex not run |

**AGENTS.md lessons (pending)**, for the orchestrator to consolidate:

- Look harder here: when a change replaces a value Harmony injects (`__originalMethod`, `__instance`)
  with one derived from the stack, probe the case where the patch method itself is patched by another
  owner; its body then runs inside a replacement that maps back to the patch method.
- Look harder here: a test asserting that a frame is (or is not) on the stack must keep code after the
  call in that frame; a `call; ret` body may be tail-called by the x64 JIT and the test passes vacuously.
- Look harder here: a benchmark quoted for TAOM.Dependencies or Main must say Release or Debug; the
  shipped build is Debug.

## Convergence round 1

The convergence reviewer read `31f59582..4f8f3018` and reported three LOW findings. Each was re-read
against the worktree before acting; all three are confirmed and fixed in the round 1 convergence commit
(subject "convergence fixes for plan 034"). No code behaviour changed, so no test applies; the only C#
edit joins a comment line.

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| C1 | LOW | harmony-il.md:6, a line 31f59582 edited, still credited the try/catch to binding `__originalMethod` (row 12's correction missed it) | FIXED | Now says the wrapper calls `MethodBase.GetMethodFromHandle` on every invocation, "for a finalizer, inside the try/catch every finalizer adds". The older 2026-09-26 lesson at harmony-il.md:674 repeats the phrase; it is outside this branch's diff and left as it is |
| C2 | LOW | dr3-maintenance.md:260, the dash after `(ExcludedTargetNamespacePrefixes)` introduces a clause the branch rewrote, and this record claimed no dash in new prose on the strength of a linter exit code | FIXED | The dash is a colon. The two statements in Agent 1 and focus answer 5 are corrected: the linter listed the line, and `tools/lint_docs.py:1518-1529` never fails `--fail-on-drift` on a dash. The line keeps five dashes in sentences neither commit rewrote, so `lint_docs.py --dash-base 64dbcac8` still lists it |
| C3 | LOW | Row 14's rewrap moved the uneven wraps instead of removing them in three of four places | FIXED | coop-interop.md:408-415 and bannerlord-together-compat.md:83-86 rewrapped (no line above 103 characters); the coop-interop rewrap replaced its one retained dash ("pass, so pass 2") so the rewrapped line carries none. PatchShieldPolicy.cs:75 now continues "UIExtenderEx patches" on one comment line (107 characters, the shortest join that leaves no fragment) |

**Dash check after the fixes.** `python -B tools/lint_docs.py --dash-base 64dbcac8` lists four lines:
arena.md:10, dr3-maintenance.md:260, submodule-lifecycle-and-harmony.md:35 and harmony-il.md:6. A word
diff against 64dbcac8 shows no dash token added by the branch, but a retained dash can still lead a
rewritten clause, as C2's did, so each listed dash was read in its sentence. One more had the same
shape as C2: arena.md:10's "named a three-factor interaction" dash leads a sentence whose third
factor 31f59582 rewrote (the reflection lookup and its 63 ns). It is now a colon. Every other dash on
the four lines sits in a sentence the branch did not rewrite, which output-style.md Part 2 exempts.

## Convergence round 2

The convergence reviewer read `4f8f3018..bbe7fe9f` and raised one LOW finding. The review workflow's
last round runs no fix pass, so the orchestrator checked it and closed it in the commit
`docs(patchshield): v2.0.32 - record plan 034's convergence rounds`.

| # | Severity | Finding | Verdict | Resolution |
|---|---|---|---|---|
| R2-1 | LOW | This record said one dash was in new prose (the Agent 1 paragraph and focus answer 5), after the round 1 fix pass had found a second with the same shape (`arena.md:10`) and turned both into colons; focus answer 5's parenthesis also put the review diff's nine removed dashes on one line | Confirmed: at `31f59582` both lines carry a dash that leads a rewritten clause, and at `bbe7fe9f` both are colons; `bbe7fe9f`'s body says "Two dashes ... are now colons" | The STANDARDS line, the Agent 1 paragraph and focus answer 5 now say two dashes and name both lines; the parenthesis now credits the nine removals to the whole review diff |

No code changed in round 2. The full suite at `bbe7fe9f`, run by the orchestrator:
`Failed! - Failed: 1, Passed: 12361, Skipped: 2, Total: 12364`, the known
`EveryLanguage_DeclaresARowForEveryEnglishKey` only. The RCA gains a "Convergence rounds" section
covering both rounds.
