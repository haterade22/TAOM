# RCA: plan 012, the loading-window lower traced only on a real drop (deep review and Codex, 2026-09-24)

## Top-line

`/deep-review` of branch `improve/012-loading-window-trace-per-frame`, diff `7f02fc8d..6f7ddd39`
(four commits: the pure gate `LoadingWindowTraceGate`, the Prefix plus `__state` capture on
`LoadingWindow_Disable_Patch`, the doc updates, the CHANGELOG entry). Six lenses ran (Standards,
Engine, Efficiency, Completeness, Data flow, Design); XML and Tooling were not in scope. A Codex
gpt-6-astra ultra pass ran on the same diff.

**No CRITICAL, HIGH or MED code defect, and no engine incompatibility.** Engine verified 10 API and
engine claims against the installed v1.5.3 DLLs and the shipped Harmony 2.4.2, 0 unverified. The
production logic is correct in all four (before, after) cells. Codex found no P1 or P2.

Confirmed findings: **one MED process gap and four LOW** (below). The MED (no GitHub issue) needs
Mike: `/issue` is public and never auto-invoked. The four LOW were fixed in the review follow-up
commit, the two test gaps with a mutant run each.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The change narrowed "every raise and lower of the loading window" in the feature doc and the registry, but the same phrase stayed in `docs/reference/feature-map.md:34` and in the class summary at `LoadingWindow_Transitions_Patch.cs:7`, one paragraph above the paragraph it rewrote. Also a missing backtick pair on `IsLoadingWindowActive` in the registry | Stale doc / convention | The plan's file list scoped the doc edits (feature doc, registry, header's second `<para>` only), and the builder followed it; nobody grepped the repo for the old phrase | Fixed. Repeat bullet added to build-tooling-workflow "When a change moves a number a file quotes, grep the file for the OLD LITERAL": grep the whole repo for the old phrase, not only the files a plan names |
| 2 | LOW | "The engine calls `DisableGlobalLoadingWindow` on every frame of the main menu, the party screen and character creation" (patch comment, gate test docstring, feature doc, CHANGELOG). The installed DLLs also lower every frame from the inventory, clan, kingdom, quests, character and crafting screens, and from the banner editor once its scene is ready; the gate doc said the engine clears the flag "unconditionally", but the clear is inside the manager guard | Unverified rationale prose | The list came from the log's top callers (party screen 140,047 lines, main menu 122,119), carried from the plan into four files; no caller census. Repeat of the harmony-il rationale lesson (rca-stale-character-repair-2026-09-06) | Fixed with a longer list and the manager qualification; the convergence pass found the barber and face generator still missing and reworded every copy as non-exhaustive. Repeat bullet on harmony-il "A patch comment's RATIONALE is prose": a plan that quotes a call frequency carries its census, or says "several screens" |
| 3 | LOW | No test ran the real Prefix. `Prefix_CapturesTheFlagIntoAnOutBoolNamedState` asserted only its signature, and the Postfix tests hand-fed `__state`, so `__state = true` (the per-frame flood back) passed all eight tests | Test gap | The plan's test list named the Prefix test after its intent while prescribing only a shape check; no mutant was run | Fixed: `PrefixThenPostfix_WhenTheWindowIsAlreadyDown_CapturesFalseAndLogsNothing`; the `__state = true` mutant fails it (run). Signature tests renamed to `Prefix_Signature_IsOutBoolNamedState` and `Postfix_Signature_TakesStateByValue`. New testing-qa lesson |
| 4 | LOW | `Postfix_WhenTheWindowWasUpAndIsNowDown_LogsOneLoweredLineWithCallers` accepted any `callers:` text, so `callers: <none>`, `callers: <unavailable>` or a helper hop between the Postfix and `TraceWithCallers` (which skips exactly two frames, the constraint the comment at `:40-41` states) all passed; it also counted only matching calls | Test gap | Same test list; the comment's constraint had no test behind it | Fixed: the test now asserts exactly one `LogInfo`, no fallback text, and a first caller that is not `LoadingWindow_Disable_Patch`; a `NoInlining` helper-hop mutant fails it (run). Same testing-qa lesson |
| 5 | MED | No GitHub issue for the change; the CHANGELOG heading has no `(#N)` | Process (documentation duty) | The plan assigned the issue to the orchestrator ("create before implementation lands"); the executor cannot file public issues | Not fixed here: needs Mike. When filed, add `(#N)` to the CHANGELOG heading and, at close, `triage-needs-ingame` with the owed smoke |

Not a defect, recorded for the reader: Data flow trace 5 (a healthy new-campaign load logs two raises
and one lower, because raises are still traced per call). The asymmetry is deliberate; tracing only
real raises would hide the second raiser. One sentence added to `map-load-diagnostics.md` so a reader
does not pair raises with lowers.

## Root-cause pattern

Findings 1 to 4 share one source: **the plan was treated as the specification for the prose and the
tests, and the plan's text was never itself verified.** Its screen list, its file list and its test
list all flowed straight into the diff. The production code was correct because the plan's engine
excerpt was verified (Codex and the Engine lens both re-derived it); the surrounding prose and tests
were not, because nobody re-derives a list they were handed.

## Why each agent missed these

The six lenses reviewed the diff; the question here is why the builder (the plan executor) and the
plan author did not catch them, and which lens caught what.

- **Agent 1 (Standards)** caught 1 and 5, and the test-name shape that 3 renamed. It could not have
  caught 2 (engine behaviour is outside its rule set) or the test-strength half of 3 and 4.
- **Agent 2 (Engine)** caught 2 by enumerating `DisableGlobalLoadingWindow` callers in the
  installed `SandBox.GauntletUI.dll` and `TaleWorlds.MountAndBlade.GauntletUI.dll`; the list it
  produced still left out the barber and face generator, which reach the lower through
  `BodyGeneratorView.OnTick`. Its scope is
  engine claims, so the doc sweep (1) and the tests (3, 4) were outside it.
- **Agent 3 (Efficiency)** found no performance issue, correctly; none of these is a cost finding.
- **Agent 4 (Completeness)** caught 1 (feature map), 3 and 5. It did not flag 4: it read the
  `callers:` assertion as sufficient.
- **Agent 5 (Data flow)** caught 1, 3 and the "unconditionally" half of 2. It did not flag 4 because
  it traced the Postfix's routing to `TraceWithCallers` in the source, not what the test would accept.
- **Agent 6 (Design)** caught 2 in full as a PRESERVING KEEP proposal. Tests were outside its scope.
- **Codex** caught 3 and 4 (P3-1, P3-2) and qualified "unconditionally"; it did not sweep other docs
  for the old phrase (1) or the screen list beyond the three it was given (2).

## Feedback memories to codify

None beyond the lessons entries. The two repeats (the rationale lesson in harmony-il and the
old-literal lesson in build-tooling-workflow) got Repeat bullets; the testing-qa entry is new. For
AGENTS.md "Lessons From Prior Reviews", see the pending list in
`docs/reviews/deep-review-012-loading-window-trace-per-frame-2026-09-24.md`.

## Codex adversarial review

| # | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|
| P3-1 | The Prefix test proves shape, not capture (finding 3) | Other: test asserts shape under a behaviour name | The plan prescribed the limited test; no mutant was run | Round-trip test added, mutant run; testing-qa lesson |
| P3-2 | The lowered-trace test accepts a fallback or helper-shifted caller chain (finding 4) | Other: assertion weaker than the constraint it guards | The comment's two-frame constraint had no test | Assertions tightened, mutant run; same lesson |

Codex's suggested integration test (raise the window, lower it, require one trace, then repeat
disables and require none) needs the engine flag raised in the test host: either reflection on the
private `IsLoadingWindowActive` setter or `EnableGlobalLoadingWindow`, which needs a manager and
native `Utilities`. The plan ruled both out; the in-game smoke covers it. Not applied.
