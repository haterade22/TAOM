# RCA: plan 006, crash capture boot cost (2026-09-24)

## Top-line

Branch `improve/006-crash-capture-boot-cost`, diff `7f02fc8d..6fe83bca`, reviewed by six
`/deep-review` lenses (standards, engine, efficiency, completeness, data flow, design) and one Codex
adversarial pass (gpt-6-astra, ultra). No runtime defect of HIGH severity was found: the allowlist,
the deleted finalizers and the live toggles do what the plan says. **22 findings were confirmed, all
in tests, comments and docs, and all fixed on the branch** (report:
`docs/reviews/deep-review-006-crash-capture-boot-cost-2026-09-24.md`). Eight more are decisions for
Mike. The confirmed findings share one root: **text and tests were written from the plan and the
toggle's intent, not from the code each sentence or assertion depends on.**

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MED | `Native2ManagedBridgeTests` passed an unthrown exception, so it could not see whether the throw site was preserved; a bare `return exception;` passed it | Test cannot fail | Fixture built to reach the branch, not past the callee's early return (`RethrowStackPreserver.cs:71`) | Throw and catch first, assert `TAOM.ThrowSite`; mutation checked red. Lesson in `testing-qa.md` |
| F2 | MED | CHANGELOG, `Native2ManagedTargets` comment, test message and feature doc said the sweep cost "about 30 s of every boot"; players measured 0 to 1 s | Overclaim | Desktop log gaps quoted as "every launch" and copied four times | Scoped to the desktop with the player figure. Lesson in `misc.md` |
| F3 | MED | Master hint and how-to said BUTR takes over with no restart; after any capture with Suspend BUTR on, BUTR stays disabled | Stale state across a toggle | Rewrite traced the toggle's reads, not what earlier captures changed | Hint and how-to corrected. Lesson in `misc.md` |
| F4 | MED | Doc claimed dev-trigger QA covers the Native2Managed attach; owed smoke (c) passes vacuously because both triggers read the master toggle | Verification gap | Smoke step written from the hint, not the trigger code | Doc names what each trigger reaches; redesigned check listed for Mike. Lesson in `testing-qa.md` |
| F5 | MED | `hero-race.md` named `Managed.ApplicationTick` as a catch point on the conversation UI tick; `ScreenManager.Tick` is reached only from the native `EngineScreenManager_Tick` | Wrong engine claim | Catch point inferred from names, not the call graph | Rewritten from the v1.5.3 call graph, with the render path the allowlist dropped |
| F6 | MED | Two engine-binding tests lacked `BindingVerification`; the reflection catalogue filed six engine names under category D ("not engine drift") | Gate gap | Category is a convention, not a gate | Category added; catalogue moved to category A. Lesson in `testing-qa.md` |
| F7 | LOW | Two docs said every crash finalizer runs at priority 800; the bridge runs at 400 and skips `HandleAndSwallow` when its toggle is off | Wrong doc claim | Sentence carried over from the category text | Docs corrected. Lesson in `harmony-il.md` |
| F8 | LOW | `Native2ManagedTargets.Resolve` had four skip paths and two tests; the missing-assembly test could not tell assembly from method | Skip-guard exhaustion (`tests.md`) | Tests covered the obvious two | Type-missing and lookup-throws tests; the assembly test uses a real shim |
| F9 | LOW | The allowlist tests passed with an empty list | Self-referential test | The plan prescribed it | Six names pinned literally. Lesson in `testing-qa.md` |
| F10 | LOW | Crash-report MCM table said "pass exceptions through untouched"; the master-off path returns the raw exception and loses its throw site without PatchShield | Wrong doc claim | Word chosen for the intent | Doc corrected; code fix is a follow-up in a read-only file |
| F11 | LOW | Catch-point row 5 described `Mission.Tick` as the mission tick; its body is one native call | Wrong doc claim | Name read as behaviour | Row rewritten |
| F12 | LOW | Registry said the tick finalizers are live "for the rest of TAOM's load"; the rest of `OnSubModuleLoad` runs on no patched frame | Wrong doc claim | Same as F11 | Reworded |
| F13 | LOW | Registry no longer contained the shim type names, so the crash-triage grep found nothing | Discoverability | Rewrite dropped the old wildcard without naming the six | Six shims named |
| F14 | LOW | Six places dated the change 2026-09-23; the commits are 2026-09-24 | Date drift | Plan date reused | All set to 2026-09-24 |
| F15 | LOW | Known limitations omitted the 241 dropped callbacks, the possibly dead entry 6 and the uncovered tableau render callback | Missing limitation | The plan listed it as a risk, the doc did not | Limitation added |
| F16 | LOW | Test list headed "by class:" left out four classes | Overclaim | Header changed, list not | Header now "among them:" |
| F17 | LOW | Cost notes counted one attach per target; PatchShield pass 2 attaches each shim again | Cost accounting | PatchShield not read | Comment, test message and doc include it |
| F18 | LOW | `CrashReportApplicationTickTrigger.cs:10-11` and `submodule-lifecycle-and-harmony.md:30,74` still said Patch37 is applied conditionally | Stale text made false by this change | Grep for the old gate's readers missed comments | Corrected |
| F19 | NIT | CHANGELOG said "empty base virtuals"; `ScriptComponentBehavior.OnTick` is assert-only | Precision | | Fixed |
| F20 | NIT | Two dead `using` lines in `Patch37_CrashReport.cs` | Dead code | | Removed |
| F21 | NIT | `GlobalSettings<T>` inside a `///` summary broke the XML doc | Doc markup | | Escaped in `<c>` |
| F22 | NIT | Bridge `Finalizer(Exception __exception)` under nullable context | Nullability | | `Exception?` |

## Root cause pattern: text written from intent

F2, F3, F4, F5, F7, F10, F11, F12 and F16 are sentences that describe what the change meant to do
rather than what the code does: a desktop number read as a universal one, a toggle described without
the state it leaves behind, catch points and priorities named from the category rather than the
patch method, "untouched" for a raw rethrow. F1 and F9 are the same failure in tests: an assertion
that holds whatever the code does. The plan itself carried F4, F7 and F9 (Codex noted each).

## Why each agent missed these

- **Builder (plan executor):** followed the plan's prescribed text and tests, which carried F4, F7
  and F9, and did not re-derive engine claims (F5, F11, F12) from the decompile.
- **Lens 1 (standards):** caught F1, F4, F6, F7, F8, F13, F14, F18 (as a follow-up) and the nits; not F2 or F3,
  which need the audit report and ButterLib's decompile, outside its rule set.
- **Lens 2 (engine):** caught F5, F6, F7, F12 and the allowlist's entry 6 question; not F3 or F4,
  which are about TAOM state and QA tooling rather than engine calls.
- **Lens 3 (efficiency):** caught F17; its scope is cost, so it did not audit prose claims.
- **Lens 4 (completeness):** caught F1, F2, F4, F7, F10, F14, F15, F16, F18; not F3 or F5.
- **Lens 5 (data flow):** caught F3, F4, F5, F6, F7, F10, F11, F18; not F2 (a CHANGELOG number,
  not a flow) or F9.
- **Lens 6 (design):** caught F1, F3, F7 and F18 as proposals.
- **Codex:** caught F4, F7, F9 and noted F1's limit; it missed F2, F3, F5 (it judged the hero-race
  text "supported"), F6 and F15, because it reviewed the prescribed plan's claims rather than the
  audit report, ButterLib's decompile and the binding-test conventions.

## Feedback memories to codify

Eight lessons appended (house shape): `lessons/testing-qa.md` (thrown-exception fixtures, literal
allowlist pins, `BindingVerification` for engine names, off-switch smoke inputs),
`lessons/harmony-il.md` (base-virtual finalizers, hand-attached priority) and `lessons/misc.md`
(performance claims name the machine; "takes effect immediately" names what it cannot undo). No new
rule file: each lesson extends an existing rule's scope.
