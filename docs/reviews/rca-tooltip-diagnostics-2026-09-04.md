# RCA: Patch79 tooltip diagnostics (deep-review, 2026-09-04)

## Top-line

`/deep-review` (5 parallel agents) on the Patch79 changeset found **1 HIGH + 1 MED + 2 LOW** code findings and 2 documentation gaps. All were fixed in the same session, before any commit. A sixth item (F5, a stale rule) had been found during planning and is what shaped the changeset: the planned migration of `SpecialResourceMapBarMixin` off `SecondaryInfoItems` was dropped once its justification was shown false on v1.4.8, and the rule was corrected instead.

Verification: **8035 passed, 0 failed, 2 skipped** in an isolated worktree at HEAD `b54f845c` carrying only this changeset. The shared working tree did not compile at review time because of another session's in-flight FieldCommission edit (interface members added ahead of their implementations); that work is not part of this changeset and was not touched.

## Findings

| # | Sev | Bug | Category | Why it reached review | Preventive action |
|---|---|---|---|---|---|
| F1 | HIGH (efficiency, per-hover) | `TooltipProbeLog.TryRecordBuild` built an interpolated string key before the `HashSet.Add` check, allocating on every tooltip request including the steady-state path where nothing is logged. | Hot-path allocation | The limiter exists to make the fast path cheap and I optimised the slow path: wrote the key the readable way and never asked what runs when nothing is logged. | Fixed: value-tuple key `(string, TooltipBuildOutcome)`, zero allocation on the fast path. No new rule; deep-review Agent 3 rule 11 (allocation inside a patch body) caught it as designed. |
| F2 | MED (data flow) | Probe B read `_dataSource != null` as "tooltip built". `OnShowTooltip` assigns `_dataSource` at line 113 then calls `LoadMovie` at 114 inside the same try; a `LoadMovie` throw leaves `_dataSource` set and was reported as success. Only `_movie`, assigned after `LoadMovie` returns, means built. | Success signal read from the wrong field; exits counted per catch, not per statement | I read the try block as one exit ("the catch") when it guards two statements with different state left behind, and wrote "both silent exits leave it null" into two doc comments, the registry and the CHANGELOG before review. | Fixed: the probe reads `_dataSource` and `_movie`, classifies three outcomes (`Built`, `ConstructedButMovieFailed`, `NotConstructed`) and names the failing stage. Lesson in `lessons/harmony-il.md`. |
| F3 | LOW (efficiency) | `TooltipProbeLog.Reset()` is tested but never called; both probes accumulate keys for the process lifetime (bounded, about 10 KB). | Unwired lifecycle | Wrote `Reset` for the test's lifecycle case without deciding who calls it. | Documented as per-process in both probes (restart between diagnostic runs). Wiring a campaign hook for scaffolding fails the simplicity criterion. |
| F4 | LOW (API compatibility) | The `SubModule.cs` comment justified applying Patch79 in `OnSubModuleLoad` as "the Patch62 reasoning". Patch62's target is in the root `bin`; `GauntletInformationView` is in Native's module `bin`. The analogy does not carry. The timing is safe for a different reason: Native's `SubModule.xml` declares `GauntletUISubModule` from that DLL and TAOM depends on Native `LoadBeforeThis`. | Justification by precedent name instead of mechanism | Both targets are "GauntletUI" by name; I never checked which bin folder each assembly lives in. | Fixed: the comment states the mechanism and the verification date. Folded into the `lessons/adapters-taleworlds-api.md` entry with F5. |
| F5 | (planning, pre-review) | `.claude/rules/gui-ui.md` asserted `SecondaryInfoItems.Add` causes `IndexOutOfRangeException` in `HandlePanelSwitchingInput` via positional indexing. False on v1.4.8: the method has no collection access, and `SecondaryInfoItems` is referenced by `MapInfoVM` only. It nearly drove a full rewrite of a working feature. | Stale crash-derived rule, never version-stamped | The rule recorded a crash and a mechanism but not the engine version, so no bump procedure could flag it: `/verify-bindings` checks code, not prose. | Fixed: the rule now carries the verified facts, dated, and asks for a version stamp on crash-derived claims. Lesson in `lessons/adapters-taleworlds-api.md`. Follow-up recommended: add "grep rules and the patch registry for engine-behaviour claims" to the `/engine-bump` checklist. |
| F6 | docs (completeness) | No GitHub issue exists for the tooltip investigation; no lessons entry existed for F5. | Process | Issue creation is a public artifact and waits for the user; the lesson was owed to this RCA step. | Lessons written (this RCA). The issue is an action item for the user. |

## Root-cause pattern: a stated count or precedent stood in for the mechanism

F2, F4 and F5 are one shape. In each, prose asserted something about engine behaviour ("both exits", "the Patch62 reasoning", "positional indexing") that was checkable against the decompile in a single read, and the prose was written first and trusted afterwards. F2 is the same failure inside one session: the doc comment said "both" before anyone counted the statements in the try. The fix in every case was the same act: open the method, read the statements in order, write down the state at each one.

## Why each agent did or did not catch these

- **Standards (Agent 1):** nothing in scope; all findings were performance, data flow or documentation. Passed correctly.
- **API compatibility (Agent 2):** caught F4 by checking which bin folder the target assembly lives in. Rated the timing [Likely] on the Patch58 precedent; upgraded to [Certain] by reading Native's `SubModule.xml`. Its two UNVERIFIED items (base-class method-name collisions on `ViewModel` and `GlobalLayer`) were closed by grep: neither declares either method.
- **Efficiency (Agent 3):** caught F1 and F3. Correctly declined to recommend a log-level downgrade after reading `FileLogger`.
- **Completeness (Agent 4):** caught F6, confirmed removal notes in all five artifacts, found no weak assertions.
- **Data flow (Agent 5):** caught F2 by reading the try block statement by statement, which is exactly the read I skipped. Its trace 1 confirmed Probe A patches the method the item template actually dispatches to (the `HintWidget` carries no `DataSource` override, so the command binds to the `MapInfoItemVM` itself). Its trace 9 independently re-verified F5's correction.

No agent missed anything another caught; every miss was mine, upstream of review.

## Feedback memories to codify

Two lessons, both appended to their category files:

- `docs/reviews/lessons/adapters-taleworlds-api.md`: a rule written from a crash must say which engine version it was verified against, and name the mechanism rather than a precedent.
- `docs/reviews/lessons/harmony-il.md`: a private field read as a success signal must be the last thing the success path assigns; enumerate exits per statement inside a try.

No new always-load rule: `gui-ui.md` itself now carries the version-stamp requirement at the point where the failure occurred.
