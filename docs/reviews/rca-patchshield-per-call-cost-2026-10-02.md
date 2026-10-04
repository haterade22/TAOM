# RCA: PatchShield finds the shielded method from the stack (plan 034 review, 2026-10-02)

## Top line

Plan 034 removed `__originalMethod` from PatchShield's two finalizers, because the parameter made
Harmony's wrapper call `MethodBase.GetMethodFromHandle` on every call of every shielded method (about
63 ns and 241 bytes per call, 1,145 ns with 8 threads contending). After a throw, the finalizers now ask
`PatchShield.ResolveShieldedOriginal`, which judges the one frame that called the finalizer and maps it
back through Harmony. Six review lenses found no HIGH. The one behavioural defect was a case the old
binding could not get wrong: when another mod Harmony-patches PatchShield's finalizer, the lookup named
the finalizer as the shielded method, so the swallow path would strip that mod's patch from
PatchShield and leave the real offender in place. A new test reproduced it on the reviewed code. A
second gap (Mono frames that report no method) bypassed Harmony's own fallback. The figure the docs gave
for the new cost, 1.3 ns per call, came from a Release build; the shipped Debug build measures 5.4 ns.
The rest were test gaps and wording. Every confirmed finding is fixed in the review follow-up commit.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| 1 | LOW | Another owner's Harmony patch on `ShieldFinalizerVoid` or `ShieldFinalizerWithResult` runs the finalizer's body in a dynamic method that Harmony maps back to the finalizer; the lookup skipped frames by declaring type only, so it judged that frame and returned the finalizer | Logic error (identity derived from runtime state) | The plan enumerated what could sit above the finalizer (outer shielded methods, recursion, inlining, unknown dynamic methods) but not the finalizer itself being patched, the one way the finalizer's own frame stops carrying its declaring type | `PatchShield.cs:325` also skips a frame whose Harmony original belongs to the finalizer's type; RED-first test with a probe finalizer that a foreign owner patches; lesson in `lessons/harmony-il.md` |
| 2 | LOW | A frame whose `GetMethod()` is null ended the lookup before `Harmony.GetOriginalMethodFromStackframe`, which maps such frames by address on Mono | Missing vanilla gate (third-party library path) | The executor and four lenses read `GetOriginalMethodFromStackframe` as a map lookup on `frame.GetMethod()`; only the engine lens read `GetStackFrameMethod` through to its Mono branch | Every frame goes to Harmony; untestable on .NET Framework, so a Not-tested trailer; same lesson |
| 3 | LOW | Ten statements gave the new finalizer's cost as about 1.3 ns per call; the benchmark ran a Release build, TAOM.Dependencies ships Debug (`IsJITOptimizerDisabled` True on the deployed DLL); the same harness built Debug measures 5.37 ns (1.85 ns for a postfix-only patched method) | Measurement not matched to the shipped artefact | The benchmark was built in Release (its result header reads "JIT optimizer disabled: False") and nothing asked which build players run, although `build.ps1` defaults to Debug and the maintainer ships Debug on purpose | Ten statements now say 5.4 ns as shipped, 1.3 ns optimized; lesson in `lessons/harmony-il.md` |
| 4 | MED | No test ran the real finalizer's swallow path when the lookup misses (the `?.?` line, no unpatch, the counted miss) | Test coverage | The plan's tests pinned the resolver and the resolved paths; the degraded path was argued safe by reading the null checks | `ShieldFinalizerVoid_LookupMisses_SwallowsNamesUnknownAndStripsNothing` |
| 5 | LOW | The first version of that test put only a prefix on the outer method; its replacement then ends `call; ret`, the x64 JIT tail-called it, and a lookup that climbed past the miss still passed the test | Test meaningfulness (vacuous pass) | The executor had met the same tail call in its dynamic method and stored the result to defeat it, but nothing generalised the rule to the patched caller one frame up | The outer method carries a postfix; the test was proven against a climbing mutation; lesson in `lessons/testing-qa.md` |
| 6 | LOW | Three more unpinned contracts: a constructor target, a resolved lookup leaving the miss counter alone, the no-miss session summary | Test coverage | Tests were written per plan step, not per promise in the docs ("the summary reads exactly as before") | One test or assertion each, each proven against a mutation |
| 7 | LOW | Docs: two of the five miss reasons listed, the repeating swallow after a permanent miss unmentioned, the stall given as both 104 and 105 to 109 s, "names '?'" against the printed `?.?` | Stale or incomplete claim | Prose written once from the first reason and the first log string; not re-read against the final code | Rewritten against the code; the pinned test carries the `?.?` text |
| 8 | NIT | Comment and citation wording (try/catch phrasing, the 63 ns figure paired with the stall, a D6 citation without its file, the 1,145 ns figure credited to the #331 RCA, uneven wraps) | Wording | Edits made line by line across ten files | Reworded |

Two lens notes were not defects: "close to 300" in the commit body matches the desktop diag.log (295,
303 and 305 attached), and a replacement that Harmony has detoured but not yet mapped gives one
transient, fail-safe miss.

## Root-cause pattern

Rows 1 to 3 share one shape: the change swapped a value the runtime handed over (Harmony injected the
original method; the cost was a fact of the shipped build) for one derived from circumstances, and the
derivation was checked against the circumstances the author pictured. Harmony's injected
`__originalMethod` was correct whatever patched what; the stack is not, and the cases that break it are
exactly the ones the injection hid (the finalizer patched in turn, frames a runtime reports without a
method). In the same way, the benchmark was right about what it ran but not about what ships.

## Why each agent missed or caught these

- **Agent 1 (Standards)** caught row 1 from the orchestrator's focus list, not from its rubric, which
  has no check for derived identity. It did not check the build configuration (row 3); that is not a
  standards item.
- **Agent 2 (Engine compatibility)** caught rows 2 and 3 by reading Harmony's code past the public
  API, and the deployed DLL's `DebuggableAttribute`. It rated row 1 contrived and leaned to rejecting a
  guard under the simplicity criterion; the review lead's test showed the case is real whenever the
  trigger exists, and the guard is one line.
- **Agent 3 (Efficiency)** verified the benchmark's figures against its raw output but took its build
  configuration as given (its N2 called the gap immaterial).
- **Agent 4 (Completeness)** and **Agent 5 (Data flow)** found rows 4, 6 and 7, and row 1 independently.
  Neither could see row 5: it arose in the fix.
- **Agent 6 (Design)** found row 1 and proposed its fix, with the right test design (a dedicated probe,
  never the real finalizer, which Harmony would leave detoured for the rest of the run).
- **The executor** proved the bound against a climbing variant and defeated the tail call in its own
  dynamic method, which is why the existing bound test was sound; the same reasoning was not carried
  to new frames.

## Feedback memories to codify

None beyond the three lessons: rows 1 and 2 go to `lessons/harmony-il.md` as one rule, row 3 to the same
file, and row 5 to `lessons/testing-qa.md`. No rule file changes; the Harmony rule would only repeat the
lesson.

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `31f59582..4f8f3018` | C1: `lessons/harmony-il.md:6`, a line `31f59582` edited, still credited the try/catch to binding `__originalMethod`; row 12's correction missed it | `bbe7fe9f` | Row 12's sweep searched the docs that quote the cost, not the lesson line this branch had itself edited | A correction to a claim greps the claim's words across every file the branch touched, its own edits included |
| 1 | same | C2: the dash after `(ExcludedTargetNamespacePrefixes)` on `dr3-maintenance.md:260` leads a clause the branch rewrote, and the record cited a linter exit code as proof of no dash in new prose | `bbe7fe9f` | `lint_docs.py --fail-on-drift`'s exit code never reflects dashes; the lead read the exit code, not the dash list | Read the dash list `--dash-base` prints and judge each listed dash in its sentence; an exit code is evidence only of what it gates |
| 1 | same | C3: row 14's rewrap moved three uneven wraps instead of removing them | `bbe7fe9f` | The rewrap was checked on the line row 14 named, not across the paragraph | After a rewrap, measure every line of the paragraph |
| 2 | `4f8f3018..bbe7fe9f` | R2-1: the record said one dash in new prose in two places, after the fix pass had found and fixed a second (`arena.md:10`) | the orchestrator's records commit after `bbe7fe9f` | The fix pass recorded the second dash in its own section without updating the record's earlier counts | A count changed by a fix is corrected everywhere the record states it, in the same edit |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2:
the review workflow's fix pass records a round only in the report.
