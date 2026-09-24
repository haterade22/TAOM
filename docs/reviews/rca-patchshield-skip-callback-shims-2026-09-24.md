# RCA: plan 007, PatchShield skips the callback shims (2026-09-24)

**Scope:** branch `improve/007-patchshield-skip-callback-shims`, diff `7f02fc8d..0ad253d5` (three
commits: `2a3f71b1`, `73792944`, `0ad253d5`). Reviewed by six `/deep-review` lenses (standards,
engine compatibility, efficiency, completeness, data flow, design) and one Codex adversarial pass
(gpt-6-astra, ultra, 177,288 tokens). Report:
[deep-review-007-patchshield-skip-callback-shims-2026-09-24.md](deep-review-007-patchshield-skip-callback-shims-2026-09-24.md).

## Summary

The code does what the plan asked: the `ManagedCallbacks` prefix is checked before `harmony.Patch`,
the three #331 entries are byte-identical, the log formatter is culture-safe, and no runtime path
regressed beyond the trade the plan accepted. Every confirmed finding is about **what the change
claims**, not what it does. The comment, the CHANGELOG and the maintenance doc described the new
entry by the three types the plan was about, while the prefix actually reaches 88 classes; they
described the Native2Managed finalizer by its normal path, while it hands the exception back on four
fallback paths; and they carried three engine or measurement claims that the installed DLLs and
`diag.log` contradict. The only engine fact the change depends on (the shims live in namespace
`ManagedCallbacks`) was checked once by hand and pinned by nothing.

Thirteen findings were confirmed (1 MEDIUM process, 1 MEDIUM design disclosure, 11 LOW). Findings
4 to 13 are fixed in the follow-up commit. Findings 2 and 3 are now disclosed in the code comment,
the maintenance doc and the CHANGELOG, and their code-level choices wait on Mike, as does the
GitHub issue (finding 1).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | No GitHub issue for the change; CHANGELOG heading has no `#N` | Process | The plan assigned the issue to the orchestrator ("create before implementation lands"); the unattended builder cannot file public issues | NEEDS MIKE: file it, add `#N`, close with `triage-needs-ingame` |
| 2 | MED | `"ManagedCallbacks"` excludes the whole namespace (88 classes in v1.5.3: 3 shims, 79 `ScriptingInterfaceOf*` managed-to-native wrappers, 3 `CallbackManager`, 3 `ScriptingInterfaceObjects`); every text described only the 3 shims, and Native2Managed wraps only the shims | Logic / disclosure | The entry was justified from the types the plan targeted; nobody listed the namespace in the installed DLLs. The plan's "Coverage given up" note inherited the same blind spot, so the maintainer's decision was made on 3 of 88 classes | Docs now state the reach (policy comment, dr3, CHANGELOG "Known limitation"); narrowing to `*CallbacksGenerated` left to Mike. Lesson 1 |
| 3 | LOW | On the shims, when capture is off, re-entered, or the crash service is unresolved or throws, `HandleAndSwallow` returns the exception; the bridge is non-void, so Harmony rethrows with `throw`, the stack is reset and the trinity is no longer swallowed. Before, PatchShield's finalizer on the same shim did both | Stale coverage claim | "PatchShield adds nothing" was reasoned from the Native2Managed finalizer's normal path (`return null`), not from all of its return statements. The 2026-09-22 RCA had named these fallback paths | Docs disclose it; the one-line code fix (`RethrowStackPreserver.PreserveForRethrow` on the fallback returns) sits in `CrashReportPatchHelper.cs`, owned by plan 006: NEEDS MIKE. Lesson 2 |
| 4 | LOW | "those shims carry only finalizers" is false: the vendored ButterLib `BEWPatch.Enable` puts a `BlankTranspiler` on `Managed_ApplicationTick`, `EngineScreenManager_Tick` and `ManagedScriptHolder_TickComponents` | Unverified premise | The patch inventory on the shims was assumed from TAOM's own code; the vendored DLLs were not searched for `ManagedCallbacks.` | Comment reworded (the owner is protected, so the conclusion held). Lesson 1 covers the search |
| 5 | LOW | "varies about 30x between machines (5 ms to 186 ms observed)": both figures are one desktop's `diag.log` over time (about 6 to 9 ms before 2026-06-12 and 2026-09-01 to 09-03, about 187 ms otherwise) | Evidence misdescribed | The plan's before/after on one machine was paraphrased as a cross-machine spread | Comment reworded. Existing rule, `evidence-over-claims.md` §C: no new lesson |
| 6 | LOW | `v1.5.2-impact.md` row said pass 2 "ran later"; `diag.log` shows it began at 20:11:39.875, two ms after the row's timestamp, which was itself game initialisation mislabelled "main menu reached" | Evidence misdescribed | The plan called the log "cannot re-check" because the cited debug log was gone; `diag.log` holds the same lines | Row rewritten from `diag.log`. Existing rule, `evidence-over-claims.md` §C: no new lesson |
| 7 | LOW | Nothing pins the shims to namespace `ManagedCallbacks`; an engine bump that moves them silently brings back the ~46 s with every test green | Missing test | The plan's STOP condition checked the namespace once, by hand; the plan called the runtime namespace "structurally untestable", which is true only of the Harmony half | New `BindingVerification` test selects the shims as Native2ManagedPatcher does from the installed DLLs; RED with the entry misspelt. Lesson 3 |
| 8 | LOW | `dr3-maintenance.md` log sample: the new heading says "after one game start" but the block showed only pass 1, put `MarkSessionLaunchSuccessful` before `OnSubModuleLoad complete`, and showed `total` equal to `+N new` | Doc accuracy | The heading was rewritten without re-reading the block under it against `diag.log` | Sample rewritten in write order with both passes |
| 9 | LOW | CHANGELOG said pass 2 runs at "the first game start"; it runs at every game start (`diag.log` shows `+141 new` at a second start) | Doc accuracy | Wrote the saving's scope (first start) as the hook's scope | Reworded |
| 10 | LOW | Loop comment at `PatchShield.cs:168` still said "Never shield hot UI-layer targets" over a list with a non-UI entry | Stale comment | The edit changed the reference in the comment, not its claim | Reworded |
| 11 | LOW | Stale summaries: test class "two decisions", policy summary missing the formatter, test section header style | Stale comment | Same as 10 | Reworded, section headers added |
| 12 | LOW | `FormatShieldPassSummary_NoAttaches_DoesNotDivideByZero` never asserted `(no new attaches)` and used an input the only caller never logs | Weak test | Test asserted the absence of a failure, not the presence of the output | Asserts the suffix with a reachable input |
| 13 | LOW | `docs/features/arena.md:10` and `lessons/harmony-il.md:8` still named `PatchShield.ExcludedTargetNamespacePrefixes` | Stale reference | The move left a pointer comment in code but no doc sweep | Both now name the new home |

Also fixed in a changed doc line: `dr3-maintenance.md` said the marker is "Created at SubModule
construction"; `Dependencies/SubModule.cs:218` writes it in `OnSubModuleLoad` via `RunEarlyPhase`.

## Root-cause pattern

Findings 2, 3, 4, 5 and 6 share one shape: **a claim written from the plan's model of the system,
not from the artifact.** The plan named three types, so the prefix "excluded three types"; the plan
cited the normal path, so the finalizer "swallows every exception"; the plan recorded a before/after,
so it became "between machines". Each claim had a cheap artifact check the change did not run:
`ilspycmd -l c` on the three DLLs, reading every `return` in `HandleAndSwallow`, grepping the
vendored DLLs for the namespace, grouping `diag.log` by date. Finding 7 is the same shape at the
test level: the premise was checked once and then trusted.

## Why each agent missed these

Plan 007 was implemented by an unattended builder from a written plan; the lenses are the first
reviewers.

- **The plan author (Opus review sprint):** verified the three shim types and the 247 count, the
  points the plan depended on, and stopped there. It did not list the namespace or the shims' other
  patch owners, and it wrote the timing as a cross-machine spread.
- **The builder:** followed the plan's text into the comment, the CHANGELOG and the docs, so the
  plan's blind spots became the code's claims. Its STOP-condition checks passed because they asked
  the plan's questions.
- **Lens 1 (standards):** caught the stale comment, sample and CHANGELOG wording (8 to 11) and the
  missing issue; the reach question is outside its rule set.
- **Lenses 2, 4, 5 and 6** caught finding 2 independently by listing the DLLs, which is the check
  the plan skipped. Lens 5 alone traced finding 3 (the fallback returns) to the stack-trace loss;
  Codex reached the same coverage point from the swallow side.
- **Lens 3 (efficiency):** out of scope for correctness; it supplied the per-date timing table that
  settled finding 5.
- **Codex:** confirmed the namespace and the Harmony finalizer contract but did not list the
  namespace's other classes, and did not search the vendored ButterLib for shim patches (findings 2
  and 4). It found finding 3 at P3.

## Lessons appended

1. `lessons/harmony-il.md`: "A namespace-prefix exclusion excludes every type in the namespace: list
   the namespace in the installed DLLs, and every other patcher of it, before justifying the entry."
2. `lessons/harmony-il.md`: "Removing one of two finalizers on a method: the one that stays must cover
   every guarantee the removed one gave, on every return path, not only its normal one."
3. `lessons/testing-qa.md`: "An engine fact a hardcoded name depends on gets a `BindingVerification`
   test against the installed DLLs, not a one-time STOP check."

## Feedback memories to codify

None: each lesson is a review-time check, and the per-category lessons files are where the next
review of PatchShield or any blanket-patching component reads first.
