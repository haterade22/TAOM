# RCA: mission tick profiler review findings (plan 028, 2026-10-02)

## Top line

Plan 028's tick profiler (`b3bac1a9`) shipped no API incompatibility: every call site, signature and
lifecycle hook held on the installed v1.5.3. The six-lens review found four MEDIUM findings and a LOW
tail, all about what the profiler and PatchShield say, and when: an INFO `[Hitch]` stream with no
per-mission bound, a PatchShield skip that was never named in any log, a trade-off paragraph that was
wrong in both directions, and the profiler's install and per-mission halves left untested on a plan's
claim that they needed a live mission. Every finding was fixed before commit, test first where it was
testable; the follow-ups in the review report predate the change.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R1 | MED | `[Hitch]` wrote one synchronous INFO line per slow frame with no per-mission bound; at the 50 ms floor in a battle under 20 fps it became a per-frame INFO line (D6 rule 5) | Log volume | The plan bounded the rate by the default threshold only ("a 3 fps battle writes about 4 lines a second") and deferred a cap before D6 existed; nobody re-read the deferral against D6 or against the MCM floor | Cap of 100 per mission, overflow counted in `[TickSummary]`, one cap line; lesson in `misc.md` |
| R2 | MED | Three unconditional `ExcludedTargetMethods` entries were skipped silently; diag.log only counted them | Silent skip | The skip lives in `PatchShield.cs`, outside the diff, and the executor read D6 as applying to the profiler's own code | `PatchShield.LogHotMethodSkip` names each skipped method once with its owners; lesson in `harmony-il.md` |
| R3 | MED | The docs, the registry and the commit claimed no shield finalizer runs inside a frame and that any throwing patch lost its rescue; both were wrong | Unverified cost claim | The claim was written from the three methods the plan touched, without listing every per-frame shielded target or re-reading which exceptions PatchShield swallows | Corrected text cites the engine; lesson in `harmony-il.md` |
| R4 | MED | The installer, the mission behaviour, the timed wait and the fault paths had no test | Test coverage | The plan said the behaviour "needs a live Mission" and the executor took it as fact; the installer's static once-latch made it untestable after the first call | Tests with a null mission and a fake category apply; latch removed (the caller's guard is pinned); lesson in `testing-qa.md` |
| R5 | LOW | Three not-measuring cases logged no reason or a wrong pointer | Reason-line coverage | Reason lines were written for the failure cases the plan listed, not for every branch of the status decision | One status line per branch, each pinned |
| R6 | LOW | Three reason lines named the wrong consequence | Reason-line accuracy | One generic fault builder was reused for a different failure | `LeftVanilla(method)` consequence per method, pinned |
| R7 | LOW | The mission header omitted the live site counts | Header completeness | `Installed` was treated as a once-per-process fact, though any re-patch reruns the transpilers | Sites in the header |
| R8 | LOW | The header could follow a mission's first `[Hitch]` lines | Ordering | The header was put beside `[PerfContext]` at the first tick, while measuring starts at creation | Header at `OnCreated` |
| R9 | LOW | `[PerfContext] diag=` reported three diagnostics as on when the master toggle stopped them | Data meaning | The tokens were mapped from toggles, not from the consumers' gates | `DiagTokens` folds the gates, tested |
| R10 | LOW | Status lines were not pinned literally | D6 rule 6 | Only the data lines were treated as a contract | Every status line pinned |
| R11 | LOW | Five texts said the toggle is read once per process | Stale description | The per-mission re-read was added after the texts were written | Both directions stated |
| R12 | LOW | Category name repeated as a literal | Duplication | Copied from the older literal style | Constant |
| R13 | LOW | A by-name reflection lookup was not catalogued | Gate coverage | The binding test for the rewrite was taken to cover the lookup | Reflection-site row |
| R14 | LOW | Feature doc and feature map gaps | Doc coverage | Written for the mechanism, not for a reader of the logs | Doc additions |
| R15 | LOW | `Behaviour` type names against the house `Behavior` | Naming | Prose spelling carried into identifiers | Renamed |

## Root-cause pattern

R1, R2, R5, R6 and R10 share one cause: the D6 logging note was applied as a list of lines to write,
not as a test to run against every branch. Each branch that decides to measure, skip, cap or fall back
is a place where D6 asks for a line; enumerating the branches (the status decision, the skip in a file
outside the diff, the cap the plan deferred) finds them all. R3 and R4 share another: a plan's text
("outside the measured path", "needs a live Mission") was copied into code comments and test choices
without being checked, which dispatch rule 12 forbids.

## Why each agent missed these

The six lenses reviewed the finished change, so "missed" here means a lens that saw the code and did
not report the finding.

- **Standards (L1):** caught R1 to R4, R6, R7, R10 to R12 and R15. Missed R9: its rules cover
  structure and D6 shape, not what a token means to its consumer. Missed R8: it checked that a header
  exists, not when it is written relative to the first data line.
- **Engine (L2):** caught R1 to R3, R7, R13 and R14 (its strongest finding was R3, from Harmony's
  emitted IL and the `tickCompleted` writes). Did not test-audit (R4) or token-audit (R9): outside its
  lens.
- **Efficiency (L3):** caught R1, R3 and R6 (the site-count consequence). Judged the per-call cost
  bounded and so did not look at what a not-measuring mission logs (R5).
- **Completeness (L4):** caught R2, R4, R5 and R14. Judged `[Hitch]` bounded (R1) because nothing is
  dropped; it weighed D6 rule 4 (no information lost) and not rule 5 (per-frame lines are never INFO).
- **Data flow (L5):** caught R1 to R3, R6, R8, R9, R11, R12 and R14. Its only miss was R4: it traced
  values, not test coverage.
- **Design (L6):** caught R3 to R5 and proposed the refactors applied in Step 4. Rejected the cap (R1)
  on simplicity; D6 is binding and overrides that judgement.

## Feedback memories to codify

None beyond the three lessons appended to `docs/reviews/lessons/`: `misc.md` (cap a per-event
diagnostic per run), `harmony-il.md` (name every skip, list every per-frame target before claiming a
frame is free of a cost) and `testing-qa.md` (an unattached `MissionBehavior` is testable).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `b3bac1a9..e64529b3` | C1: R3's corrected trade-off said a missing-API throw from `Mission.OnTick` is still swallowed one frame up by the shield on `MissionState.TickMissionAux`; that shield exists only from a process's second game start | `7ca09cc2` | R3 was rewritten from the PatchShield path at a second start; when pass 2 attaches TAOM's late batch was not re-read | The `harmony-il.md` lesson's Prevent line on shield timing |
| 1 | same | C2: three `BehaviorTickTableTests` methods kept the `TakeTop_` prefix after P6 renamed the method `WindowTop` | `7ca09cc2` | The rename changed the test bodies, not the test names | After a rename, grep the old name across tests and docs |
| 1 | same | C3: two fallbacks in the plan's own code wrote no reason line (the memory read's false return, the provider's two new validators), against the D6 note | `7ca09cc2` | D6 rule 3 was checked on the paths the lenses traced; a failure reported by a return value, and a validator's silent default, were not traced | A reason line for every disable, skip or fallback, including one reported by a return value |
| 2 | `e64529b3..7ca09cc2` | C4: C1's text still understated a first game: the throw unwinds the whole application tick, and a postfix that throws after `Mission.OnTick` has ended the mission stops `MissionState.OnTick` from popping it, so the battle never closes | `9d52bb86` (the orchestrator, docs) | C1's fix followed the throw to its catch and stopped there; it did not read what each unwound caller runs after the call | The `harmony-il.md` lesson's Prevent line: follow an exception to its next catch and list what every unwound frame skips |
| 3 | `81811ea2..31c0fd15` | R3-1 to R3-8, all LOW: the `Mission.OnPreTick` shield claim (R18 above), a strip described as culprit-only and a throw before the clear called safe, a narrowed rule that read as excluding `Mission.OnTick`, "without running any transpiler" (each Harmony `Unpatch` reruns the remaining transpilers), a Key Files row, a dropped exception message, a maintenance note that still named three exclusions, and a stop warning whose `t` survived a one-token mutation | This commit (the one that adds this row) | Fix pass 3 wrote each claim from the code it had just changed, not from the code on the other side of it: PatchShield's attach rule, Harmony's `Unpatch`, the order of the `Mission.OnTick` loops. The one test pinned a time that the mutation also produced (0 against about 0) | Before a text says what a shield or an unpatch does, read both ends (the attach rule and the Harmony call). A test that pins a printed time or count starts from a value the mutation cannot also produce: back-date the start |

## Fix pass 3 (2026-10-03): the Codex whole-plan review and the Claude review of the D13 change

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| R16 | MED | The profiler kept measuring after its hooks stopped working. `Installed` was set once at game start, so another mod's transpiler that took an anchor (Patch97's rewrite then found none and lowered `OnTickSites`) or a PatchShield strip of TAOM's patch left every later mission measuring with a hook gone, and the transpiler warning that says no mission is measured was false | Stale readiness | R7 had already met `Installed` treated as a once-per-process fact and answered it with a header that prints the counts; nothing gated on them, and no test followed an install with a change | `HookHealth` and `MissionTickProfilerHealth`: the hooks are read at each mission start (the site count on every tick), one aggregated warning, tests that install and then change; lesson in `harmony-il.md` |
| R17 | MED | The texts around the D13 exclusion said the `TickAgentsAndTeamsImp` exclusion fixes the `WaitTickCompletion` hang and that re-shielding `Mission.OnTick` rescues a soft-lock. `tickCompleted` is cleared before the `OnMissionTick` loop and set only by the agent tick, so an exception swallowed above `Mission.OnTick` between the two hangs the next frame, shielded or not, and on the inline call (fast-forward) the exclusion does not keep the exception out of `Mission.OnTick` | Unverified safety claim | The claim came from the exclusion's cost argument and the swallowed-body case, not from every write to the completion flag and every throw position against it | Texts corrected with the engine lines; the hazard named for the PatchShield follow-up plan; the standing Prevent rules narrowed; lesson in `harmony-il.md` |
| R18 | LOW | `Mission.OnPreTick` was described as shielded or "back under the shield", though TAOM patches it only through the default-off Patch97. The correction then said it is shielded only while the profiler is installed, which is false: PatchShield attaches to every patched method outside its skip sets, so a foreign mod's patch on it attaches the finalizer with the profiler off | Overstated scope, then understated | The D13 wording treated the two Mission methods as a pair; the correction read what TAOM patches, not PatchShield's attach rule (`PatchShield.Install`: every patched method that is not TAOM-declared, excluded or a SaveShield target) | The texts say TAOM patches it only through Patch97; any patch on it attaches the shield (convergence round 3, R3-1) |
| R19 | LOW | The once-per-process wiring test pinned the order of the latch and the install but not the early return that makes the block once-per-process | Test strength | The assertion anchored on the nearest text, which a deleted guard leaves in place | The test pins the early return, the latch and the install in one method, with three mutation tests |

R16 is the second time the same lesson came up in this plan (R7, then R16): a once-per-process flag is
true at one moment, and a report of the live counts does not stop a mission from using a hook that is
gone. The fix reads the hooks where they are used. A strip mid-mission is still read only at the next
mission start, for a measured reason (patch info is deserialized on every read and each patch method is
resolved by scanning the loaded assemblies), recorded under "Known limits" in the feature doc.
