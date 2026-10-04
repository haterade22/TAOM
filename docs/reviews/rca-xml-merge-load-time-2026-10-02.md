# RCA: XML merge fast path (plan 042), deep review 2026-10-02

## Top-line

Six deep-review lenses (Standards, Engine compatibility, Efficiency, Completeness, Data flow, Design) reviewed
`efb70763..19be93be` on `perf/042-xml-merge-load-time`. Codex was not run. The review lead re-checked every finding
in the worktree: **12 confirmed (0 HIGH, 3 MEDIUM, 9 LOW), 0 false positives.** No finding was a runtime defect in
the merge itself: the loop matched the installed v1.5.3 engine call for call, and every type stayed byte-identical,
including a lead re-run of the live harness with NavalDLC and Bannerlord.Diplomacy added (34 Campaign and 29
CustomGame types). The confirmed findings were test gaps on code every fast merge runs, drift guards weaker than the
engine dependency they guard, and documentation that claimed more than the evidence (identity on every input, a
`rethrow` that PatchShield removes, an incomplete caller list). All 12 are fixed in the review commit; 16 of its 19 new
tests were proven red against a deliberate mutation of the code they guard, and the other three in the convergence
round.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `OnOriginalFinished`'s `call.Handled` early return (`XmlMergeService.cs:185`) had no test, yet Harmony runs the finalizer after every fast merge; removing it doubled the summary counts and logged `reason=not-initialized`, all tests green | Test coverage of the live path | Every finalizer test fed an engine-path call; the executor tested the finalizer as "the engine-path logger", not as "runs after every call" | Test `OnOriginalFinished_AfterAFastMerge_LogsNothingAndCountsOnce`; lesson in testing-qa ("drive a guard with the state the game gives it") |
| 2 | MED | The stand-aside owner filter (`XmlMergeEngineAdapter.cs` `Collect`/`Add`) had no test that could fail: the only real-adapter test asserted zero patches with nothing patched | Test coverage of the live path | The rule sat in an adapter (thin by convention), so it was treated as wiring; the in-game state (TAOM's own prefix on the target) never appeared in a test | Filter extracted to the pure `CollectForeign`; `XmlMergeForeignPatchFilterTests` (5 tests, hosted CI) on Harmony's own `Patches`/`Patch`; same testing-qa lesson |
| 3 | MED | The engine drift guard was weaker than the dependency: the IL tests compared an unordered, de-duplicated set of `MBObjectManager` callees, and nothing pinned `ApplyXslt` (which `XsltTransformCache` copies), `ToXDocument` or `ToXmlDocument` (whose round trip the fast path drops). An engine update could change the merged document with no exception and no fallback while `/verify-bindings` stayed green | Drift guard | The executor pinned "which helpers" because that is what the existing binding tests do; the 2026-10-01 lesson "pin that order from its IL" was not consulted for a re-implemented engine body. Repeat of that lesson | `MirroredEngineBodies_CallExactlyThePinnedSequence` (ordered calls of five bodies, `BindingVerification` + `RequiresGameIL`); lesson in adapters-taleworlds-api |
| 4 | LOW | `WatchedMethods_AllResolve` checked names only while the adapter resolves exact parameter lists, so a signature change would turn the fast path off for every player with the gate green | Drift guard | Two parallel lists (names, signatures) in the adapter; the test was written against the name list | One `WatchedMethods` table; `WatchedMethods_ResolveWithTheAdaptersSignatures` resolves the same lists; same adapters lesson |
| 5 | LOW | Patch comment, feature doc and registry said the void finalizer keeps Harmony on `rethrow`; PatchShield's pass 2 gives every patched non-TAOM engine method a value-returning finalizer, so from the first game init on Harmony uses `throw` | Doc claim vs Harmony mechanics | The 2026-09-22 lesson's Prevent line ("a finalizer that only observes should be void, which leaves Harmony on rethrow") reads as unconditional; the executor did not ask which other finalizers the new patch would bring onto the target | Wording fixed in three places; lesson in harmony-il ("a new patch on an engine method also gets PatchShield's finalizer") |
| 6 | LOW | "Identical output" stated without its domain; the dropped round trip differs on two inputs (a namespace prefix rebound by a later file; adjacent text left by `_replaceWhileMerging` in mixed content), proven on fixtures by lenses 1, 2 and 3, neither of which throws | Doc claim vs proof domain | The harness proved the installed module set, and the claim was copied from the harness's scope into general statements | "Known limits" section; claims scoped in the doc, registry, engine reference, patch, service and CoopVeto entry; lesson in testing-qa |
| 7 | LOW | The unvalidated-caller lists omitted `CustomBattleScenes` (`CustomGame.cs:116`, `NavalCustomGame.cs:131`, skipValidation true) | Incomplete enumeration | The caller list was built from the campaign path and the adapters, not from a grep of every `GetMergedXmlForManaged` call | Added in the patch comment, feature doc and registry |
| 8 | LOW | Service branches untested: the outer catch, the summary's `off` and `disabled` states, the decision order (skip-validation before foreign patch), `TypeOf` edge cases, a null list, a second patch set or exception type logging again | Test coverage (ADR-008) | Tests were written per planned behaviour, not per branch | Nine tests in `XmlMergeServiceTests`, each red under a mutation |
| 9 | LOW | `XsltTransformCache`'s length check and "nothing broken is cached" had no test of their own | Test coverage | The rewrite test changed only the time (`DropC` over `DropB`, the same length), so nothing tested the length check alone | `Apply_FileRewrittenWithTheSameTimeButAnotherLength_Recompiles`, `Apply_MalformedStylesheet_ThrowsEveryTimeAndIsNeverCached` |
| 10 | LOW | Feature doc lacked the template's Dependencies section and the Stage 2 (schema cache) decision behind `schema_cache_hits` always 0 | Doc completeness | The doc followed the plan's outline, not the template | Both added |
| 11 | LOW | `lords.xslt`'s remaining cost was attributed to "the transform rather than the compile"; lens 3 measured about 0.1 s compile, about 1.0 s first transform of a fresh instance, 0.05 to 0.1 s after, and about 11 MB held by the cached instance | Cost attribution | One temporary timing print measured a first run only | Performance section, Decision 1 and the cache comment corrected; `FOR-MIKE.md` (outside the branch) left to the orchestrator |
| 12 | LOW | Feature-map row said the patch cut "most of the 15 to 28 s each load spent merging", an in-game claim from harness data | Doc claim vs evidence | The row summarised the goal, not the measurement | Reworded to the harness figures, "not yet measured in game" |

## Root-cause pattern: a proof stated wider than it was run

Findings 3, 4, 5, 6 and 12 share one shape: a check or a measurement was run on a narrower domain than the sentence or
test name built on it. A name check stood for a signature check, a set of callees for a call order, one Harmony
finalizer for the method's whole finalizer list, one module set for every player's, a harness run for an in-game
load. Findings 1 and 2 are the test-side version: the tests covered the inputs the author thought of first (an
engine-path call, an unpatched method), not the inputs the game supplies on every call.

## Why each agent missed these

The lenses are reviewers, and between them they found all 12; this section is about the build.

- **Executor (builder):** wrote tests per planned behaviour and verified the harness thoroughly, but did not mutate
  its own guards (the `Handled` return, the owner filter) or read the 2026-10-01 IL-order lesson and the 2026-09-22
  rethrow lesson for the patch it was writing.
- **Plan 042:** its drift-guard step asked for "which helpers the engine calls", which the executor implemented
  faithfully; the plan, not the executor, set the guard's strength.
- **Lens overlap:** findings 1 to 4 were each found by two to four lenses independently, which is the review working
  as intended. Finding 5 was found only by lenses 2 and 5, both of which decompiled Harmony and read PatchShield.

## Feedback memories to codify

None beyond the four lessons appended under `docs/reviews/lessons/` (harmony-il, adapters-taleworlds-api, two in
testing-qa).

## Convergence rounds

| Round | Diff read | Finding | Fixed in | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | `19be93be..4a67f13f` | R1: the IL drift guard projected calls to `DeclaringType.Name::Name`, blind to constants, overloads and generic element types | `98a69d4f` | The guard was built from the existing "which helpers" shape and judged by the changes it caught, not by the ones it could not see | List what a guard cannot see before claiming what it catches |
| 1 | same | R2: which method is the target and which are watched had no test that could fail | `98a69d4f` | The choice sat in a thin adapter method that ran only with nothing patched | Give such a choice a seam and feed it data that differs by method |
| 1 | same | R3: the record claimed every new test was proven red; the batches covered 16 of 19 | `98a69d4f` | The count was written from memory of the batches, not from their logs | A count comes from the file that holds it |
| 2 | `4a67f13f..98a69d4f` | F1: the narrowed "not pinned" lists still omitted branch structure (a dropped `else`, an added `continue`, a nested `if`) | the orchestrator's records commit | The narrowing listed what the fix's own examples exposed, not everything `FingerprintToken` drops | Derive a "not pinned" list from the code that filters, here every opcode `FingerprintToken` returns null for |
| 2 | same | F2: "each new test red first" was false again; `Fingerprint_OfTheSameBody_IsStable` pins determinism and no drift mutation can turn it red | the same commit (claim corrected) | R3's fix repeated R3's claim for its own new tests | A red-first claim names the tests it covers, and a test no mutation can fail is named as such |
| 2 | same | F3: the seam test's fake returned the same patches for every method, so a seam reading the target's patch info for a watched method passed | the same commit (test fixed, proven red against that mutant) | The fake was built to exercise the flags, not to tell methods apart | A fake behind a per-item seam returns per-item data |

This section and the REVIEW-LOG entry's convergence clause were written by the orchestrator after round 2:
the review workflow was interrupted by a session restart before its round-2 pass, which the orchestrator then ran
as one `deep-reviewer`.
