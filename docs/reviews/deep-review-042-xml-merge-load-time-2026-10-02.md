# Deep review: plan 042, XML merge fast path (2026-10-02)

```
DEEP REVIEW REPORT
===================
Feature: plan 042, Patch99 runs the engine's module-XML merge with one document per type and each
         XSLT compiled once, byte-identical on the live install
         (branch perf/042-xml-merge-load-time, efb70763..19be93be)
Date:    2026-10-02 (review lead run 2026-10-03)

Scope:   C# (Main/Features/XmlMerge/, Main/Adapters/XmlMergeEngineAdapter.cs, one SubModule line,
         one FeatureModules line), tests, docs. No XML, no harness files.
Blast radius: 2 types changed by the lead (XmlMergeService, XmlMergeEngineAdapter), both new on
         this branch; git grep finds every reference inside the feature folder, its module and its
         tests. graphify not run (no caller outside the diff exists).
Waves:   one wave: Standards (1), Engine compatibility (2), Efficiency (3), Completeness (4),
         Data flow (5), Design (6). Codex not run.

STANDARDS:     FAIL: 0 rule violations; 1 MEDIUM test gap, 4 LOW, 1 INFO (doc)
COMPATIBILITY: PASS: 9 verified, 0 incompatible, 2 unverified; 5 doc/guard findings (3 LOW, 2 INFO)
EFFICIENCY:    PASS for players: 3 issues (0 high, 1 medium developer-loop, 2 low doc), 2 follow-up
COMPLETENESS:  INCOMPLETE: no GitHub issue; 2 MEDIUM untested behaviours; 5 LOW
DATA FLOW:     FAIL: 20 flows, 5 gaps (1 MEDIUM), 2 inconsistencies, 2 INFO residuals
DESIGN:        4 KEEP proposals (4 apply: P3 and P4 applied, P1 and P2 behaviour-changing, not applied)
XML:           NOT IN SCOPE (no ModuleData change)
TOOLING:       NOT IN SCOPE (no hook, validator or CI change)
```

## Details

Every finding below was re-checked by the review lead in the plan 042 worktree before any change: the
service, adapter, patch and cache at `19be93be`, the installed v1.5.3 decompile (`taom-src`,
`MBObjectManager.cs:962-1031`), `PatchShield.cs:120-300` and `PatchShieldPolicy.cs:60-160`, and the lens probe outputs
cited. Sixteen of the 19 new tests were proven red by a temporary mutation of the code it guards (four batches, files
restored byte for byte, sha256 checked; no git command); the other three were proven red in convergence round 1
(below). Scratch evidence, in the run's `scratch/lead-042/` folder (`mutate.py`,
`mutation-B1..B4.txt`, `nsscan.py`, `harness-othermods.txt`, `final-full.txt`, `final-binding-gate.txt`,
`refasm-*.txt`).

Numbering below: C1 to C12 are the lead's confirmed findings (the RCA's rows 1 to 12).

### Agent 1: Standards

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| S1 | MEDIUM | `OnOriginalFinished`'s `call.Handled` return untested; every fast merge reaches it | CONFIRMED (C1): mutation `if (call == null)` left all 27 original tests green | Fixed: `OnOriginalFinished_AfterAFastMerge_LogsNothingAndCountsOnce` |
| S2 | LOW | Outer catch, `fast_path=off/disabled`, `TypeOf`'s `ArgumentException`, `Describe(null)` untested | CONFIRMED (C8) | Fixed: logger-throws, summary off and disabled, invalid path, null entry, null list tests |
| S3 | LOW | Stand-aside rule has no test that can fail | CONFIRMED (C2) | Fixed: `CollectForeign` extracted; `XmlMergeForeignPatchFilterTests` |
| S4 | LOW | `WatchedMethods_AllResolve` checks names, adapter binds exact signatures | CONFIRMED (C4) | Fixed with P4 |
| S5 | LOW | Feature doc lacks `## Dependencies` | CONFIRMED (C10) | Fixed |
| S6 | INFO | Feature-map row's in-game claim; caller lists omit `CustomGame.cs:116`, `NavalCustomGame.cs:131` | CONFIRMED (C12, C7); `CustomGame.cs:116` and `NavalCustomGame.cs:131` read `CustomBattleScenes` with skipValidation true | Fixed: "in the test harness ... not yet measured in game"; `CustomBattleScenes` added |

### Agent 2: Engine compatibility

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | LOW | "Identical output" unscoped in four places; the dropped round trip differs when a later file rebinds a prefix | CONFIRMED (C6): lens 3's `probe-out.txt` S1 shows the engine `x="1"` vs fast `d2p1:x="1"`. Lead scan of 4,614 installed ModuleData files: only `xsi`/`xsd`, two Native files with lower-case URIs, no mixed content (87 files ElementTree could not parse) | Fixed: "Known limits"; claims scoped in doc, registry, engine reference, patch, service, CoopVeto entry |
| F2 | LOW | Void finalizer does not keep `rethrow`: PatchShield pass 2 attaches `ShieldFinalizerWithResult` | CONFIRMED (C5): `PatchShield.cs:164-208` skips only TAOM-declared, excluded and SaveShield targets; `TaleWorlds.ObjectSystem` is on neither list | Fixed in patch comment, feature doc, registry |
| F3 | LOW | Engine members bound by name without catalogue rows; IL pins unordered; engine-bump body diff skips `ApplyXslt`/`MergeTwoXmls` | CONFIRMED (C3, C4) for the test strength; catalogue rows NOT APPLIED (see below) | Fixed: ordered IL fingerprint of five bodies, exact-signature test |
| F4 | INFO | XSLT cache staleness: same length and time; `xsl:include`/`xsl:import` | CONFIRMED as residual; lead grep: 0 of 23 installed stylesheets use include, import or `document()` | Documented in the cache comment and "Known limits" |
| F5 | INFO | `CustomBattleScenes` missing from unvalidated lists | CONFIRMED (C7) | Fixed |

Unverified items stay unverified: a foreign patch on `CreateDocumentFromXmlFile` applying through the cached delegate
(MonoMod detours the method entry, so it should; no probe), and mods not installed here.

### Agent 3: Efficiency

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | MEDIUM (dev loop) | Live harness adds about 40 s to every unfiltered local `dotnet test` | CONFIRMED: lead's full run at `19be93be` took 1 m 6 s; the two harness tests took 28 s and 17 s in the lead's run with two extra modules | NOT APPLIED: changes what the default suite runs (NEEDS MIKE) |
| F2 | LOW | `lords.xslt` remaining cost is first-run JIT, not the transform | CONFIRMED from `lords-jit-out.txt` (load 129 ms, first transform 1,029 ms, second 50 ms, fresh instance 1,095 ms) | APPLIED: Performance, Decision 1 and cache comment; `FOR-MIKE.md:279-280` is outside the branch (orchestrator) |
| F3 | LOW | Cache memory cost unstated | CONFIRMED from `lords-mem-out.txt` (GC heap 19.9 to 31.0 MB, 20.2 MB released) | APPLIED: cache comment and Performance |
| F4 | FOLLOW-UP | Pre-warm `lords.xslt` off the loading screen | Not verified in game | FOLLOW-UP |
| F5 | FOLLOW-UP | Stage 2 sat near its 300 ms bar | Recorded in the feature doc | FOLLOW-UP: re-decide from in-game `load_ms` |

Lens 3's recommendation of a document-tree digest in the harness: NOT APPLIED (below).

### Agent 4: Completeness

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| M1 | MEDIUM | Stand-aside owner filter untested | CONFIRMED (C2) | Fixed |
| M2 | MEDIUM | `call.Handled` guard untested | CONFIRMED (C1) | Fixed |
| L1 | LOW | `fast_path=off/disabled` never asserted | CONFIRMED (C8) | Fixed |
| L2 | LOW | Outer catch, decision order, `TypeOf` edges, second stand-aside set and exception type | CONFIRMED (C8) | Fixed: one test each |
| L3 | LOW | Cache length check and malformed stylesheet untested | CONFIRMED (C9) | Fixed: two tests |
| L4 | LOW | No Dependencies section; Stage 2 decision unrecorded | CONFIRMED (C10) | Fixed |
| L5 | LOW | No limitations note for the riskiest assumption | CONFIRMED (C6) | Fixed: "Known limits", with the `TAOM_XMLMERGE_MODULES` remedy |
| PROC | n/a | No GitHub issue (`#TBD`) | CONFIRMED | NEEDS MIKE (public; orchestrator files from the draft) |

Recommendation (a), run the harness with NavalDLC and Diplomacy: done by the lead. All 34 Campaign and 29 CustomGame
types identical (`harness-othermods.txt`: `Total tests: 2, Passed: 2`). Recommendation (b), a richer fixture: NOT
APPLIED (below).

### Agent 5: Data flow

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| F1 | MEDIUM | Drift guard unordered; `ApplyXslt` unpinned; harness outside the bump gate | CONFIRMED (C3) | Fixed: `MirroredEngineBodies_CallExactlyThePinnedSequence` (`BindingVerification`, `RequiresGameIL`) runs in `/verify-bindings` and fails on a reordered loop or another callee name (convergence round 1 widened it to overloads, constants and comparisons; it still does not pin every input, so the harness runs after every engine update). Tagging the harness `BindingVerification`: NOT APPLIED |
| F2 | LOW | Watched-method test checks names | CONFIRMED (C4) | Fixed with P4 |
| F3 | LOW | `Handled` guard untested | CONFIRMED (C1) | Fixed |
| F4 | LOW | Real `Collect`/`Add` tested only empty | CONFIRMED (C2) | Fixed (pure helper, hosted CI) |
| F5 | LOW | Failure lines carry no stack | CONFIRMED as stated | NOT APPLIED: adds a line per failure against D6 (NEEDS MIKE) |
| F6 | LOW | `schema_cache_hits` has no writer; `schema_builds` repeats `files` | CONFIRMED | Same as P2: NOT APPLIED (NEEDS MIKE) |
| F7 | LOW | "Void finalizer keeps rethrow" | CONFIRMED (C5) | Fixed. Its consequence (PatchShield now swallows a trinity exception from the engine's merge into a null document) is FOLLOW-UP |

Follow-ups noted: `_gate` held across a whole multi-second merge (no thread evidence either way; UNVERIFIED);
compiled `lords.xslt` held for the process (now measured by lens 3: about 11 MB).

### Agent 6: Design and elegance

| # | Proposal | Behaviour | Verdict | Action |
|---|---|---|---|---|
| P1 | Call the engine's `ApplyXslt`; delete `XsltTransformCache` | CHANGING | Re-verified; the lens's cost estimate (0.15 to 0.3 s per later load) is low: lens 3 measured the cached instance saving about 1.1 s per later load of `lords.xslt` (first-run JIT), against about 11 MB held | NOT APPLIED (NEEDS MIKE). The minimum it asked for if the cache stays, a pin on `ApplyXslt`'s body, is APPLIED in the IL fingerprint |
| P2 | Drop `schema_builds`/`schema_cache_hits` and "schema cache off" | CHANGING (log format) | Re-verified (`XmlMergeCounters.cs:22-25`, no writer of `SchemaCacheHits`) | NOT APPLIED (NEEDS MIKE) |
| P3 | Stand-aside dedupe key is the logged line | PRESERVING | Re-verified (`XmlMergeLines.StandAside` sorts) | APPLIED; `TryMergeFast_ForeignPatch_LogsTheStandAsideLineOnlyOnce` green before and after, `TryMergeFast_ADifferentPatchSet_LogsAnotherStandAsideLine` red under a constant key |
| P4 | One watched-method table; test resolves exact signatures | PRESERVING | Re-verified | APPLIED; `WatchedMethods_ResolveWithTheAdaptersSignatures` red under a wrong `ApplyXslt` parameter list |

## Orchestrator focus items

1. **Loop and wiring: verified.** Lead read `XmlMergeService.cs:71-108` against v1.5.3 `MBObjectManager.cs:962-1006`:
   load next, convert accumulated, convert next, merge; loop from 1; `!= ""` at `:78`, `:89`, `== ""` at `:100`. The
   finalizer is `void` (patch `:55`). Owners are exactly `com.taom.mod` and `TAOM.Dependencies.Foundation.PatchShield`,
   now pinned by `TaomsOwnOwners_AreNeverForeign_InAnyCollection`. Every once-only write is under `_gate`. The summary
   call is at `SubModule.cs:1561`, before the guard. The new ordered fingerprint pins the engine's side of all this.
2. **Dropping the round trip.** Not an identity in general: lenses 1, 2 and 3 each proved a namespace-prefix rebind
   case on fixtures, and lens 3 a mixed-content adjacent-text case; neither throws, so neither falls back. On this
   install: lead scan found no input that triggers either, and the lead's harness run with NavalDLC and Diplomacy added
   was identical on every type. No runtime guard added (lenses 1, 2, 5 and 6 agree: cost above a risk no installed
   module carries); the boundary is now written down with the re-proof command.
3. **Stale stylesheet:** correct for the edited or replaced main stylesheet (time and length re-checked per call,
   stamp read before compile, failures never cached; now tested for length alone and for a malformed file). Residuals
   (include/import, same length and time) documented.
4. **D6 logging:** met; P3 changes no line. P2 and lens 5 F5 would change lines and are left to the maintainer.
5. **Leftover `lords.xslt` print:** none. Lead `git grep` at `19be93be` over the feature folder, the adapter and the
   tests for `Console.Write`, `Debug.Print/Write`, `Trace.Write`, `TestContext.WriteLine` and `lords` finds only the
   planned harness table (`XmlMergeLiveEquivalenceTests.cs:77-78, 122-123`), a cache comment and a test fixture path;
   `git status` was clean at the start.
6. **Known failures:** matched exactly (full suite: `EveryLanguage_DeclaresARowForEveryEnglishKey`; reference
   assemblies: that one plus `Patch93_HasTheSevenPatchesInItsCategory` and `Patch94_HasTheMapIconNoParleyAndNoJoinPatches`).

## Action items

1. File the plan 042 issue from the draft, replace `#TBD`, label `triage-needs-ingame` (maintainer, public).
2. Convergence pass (Step 4.6): one `deep-reviewer` on the review commit's diff (orchestrator spawns).
3. Correct `FOR-MIKE.md:279-280` ("1.2 s of every load") to the measured split (outside this branch).
4. `/verify-bindings` to add `CreateMergedXmlFile` to `patch-targets.md` (the plan's own post-review step).
5. Maintainer decisions listed under NOT APPLIED.

## Codex review

Codex not run for this item (no paid dispatch was authorised). Phase 3d assessment:

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| none | n/a | n/a | n/a | No Codex review exists for plan 042 |

Things Codex would have been asked to check that the lenses covered: the loop order against the engine, the round
trip's neutrality, the stand-aside filter, PatchShield's interaction. Nothing for the track record.

**AGENTS.md lessons (pending):** none from Codex. For the orchestrator's wrap-up, one candidate for the "Look harder
here" list in `.ai/review-reference.md`: when a patch adds the first Harmony patch to an engine method, check what
PatchShield's pass 2 then attaches to it (rethrow becomes `throw`; the missing-API trinity is swallowed into a default
result).

## Improvements (Step 4)

**APPLIED**
- `Main/Adapters/XmlMergeEngineAdapter.cs`: P4, one `WatchedMethods` table (name to exact parameter list) replaces
  `WatchedMethodNames` plus `WatchedSignatures`; proof `WatchedMethods_ResolveWithTheAdaptersSignatures`.
- `Main/Adapters/XmlMergeEngineAdapter.cs`: the owner filter as the pure `CollectForeign` (C2); proof
  `XmlMergeForeignPatchFilterTests` (5 tests, hosted CI).
- `Main/Features/XmlMerge/XmlMergeService.cs:297-302`: P3, the logged line is the dedupe key; proof
  `TryMergeFast_ForeignPatch_LogsTheStandAsideLineOnlyOnce`, `TryMergeFast_ADifferentPatchSet_LogsAnotherStandAsideLine`.
- Lens 3 F2 and F3: `docs/features/xml-merge-fast-path.md` Performance and Decision 1, `XsltTransformCache.cs` class
  comment (documentation; evidence `lords-jit-out.txt`, `lords-mem-out.txt`).
- Defect fixes C1 to C12 as listed in the agent tables; tests: 10 in `XmlMergeServiceTests`, 2 in
  `XsltTransformCacheTests`, 5 in `XmlMergeForeignPatchFilterTests`, 2 in `XmlMergeBindingTests` (one replacing
  `WatchedMethods_AllResolve`).

**NOT APPLIED**
- P1 (`XsltTransformCache.cs`, `XmlMergeService.cs:86`): behaviour-changing; maintainer's call. Measured trade-off:
  the cache saves about 1.1 s per later load in a process for `lords.xslt` and holds about 11 MB.
- P2 and lens 5 F6 (`XmlMergeLines.cs:19,29,53`, `XmlMergeCounters.cs:22-25`): log format change; maintainer's call.
- Lens 3 F1 (`XmlMergeLiveEquivalenceTests.cs:27-63`): opt-in or cached engine side changes what the default suite
  runs; maintainer's call.
- Lens 5 F1, tagging the live harness `BindingVerification`: adds about 45 s to `/verify-bindings`; the new ordered IL
  fingerprint now fails that gate on any change to the mirrored bodies and names the harness. Maintainer's call.
- Lens 5 F5 (`XmlMergeService.cs:198,309`): a DEBUG line with `ex.ToString()` adds a second line per failure, against
  the D6 "one reason line per fallback" note; maintainer's call.
- Lens 2 F3, catalogue rows for `ApplyXslt`/`MergeTwoXmls`: the ordered IL fingerprint is the stronger pin and runs in
  the same gate; adding catalogue rows would duplicate it.
- Lens 3's document-tree digest and lens 4's richer fixture (test-only): the two known divergent inputs are now
  documented, and the lead's scan shows no installed input of either kind; a digest would turn a documented limit
  into a harness failure only for data no module ships. Left for the maintainer with P1/P2.

**FOLLOW-UP** (pre-existing code or outside the plan; no issue filed, issues are the maintainer's)
- `Dependencies/Foundation/PatchShieldPolicy.cs` `ExcludedTargetMethods`: whether to exclude
  `MBObjectManager.CreateMergedXmlFile`, so a missing-API exception from the engine's own merge crashes as in the
  unpatched game instead of loading that type empty (lens 5 F7).
- Lens 3 F4: pre-warm `lords.xslt` off the loading screen (about 1 s on each process's first load; unmeasured in game).
- Lens 3 F5: re-decide Stage 2 from the first in-game `load_ms` fields.
- Lens 5: a `thread=` field to settle which thread the native merge callbacks use.

VERDICT: READY FOR COMMIT (every confirmed defect fixed; final full suite matches the base's known failure; the Step
4.6 convergence pass is owed by the orchestrator)

## Convergence round 1

One `deep-reviewer` on `19be93be..4a67f13f` found 3 LOW. The fix pass re-checked each against the code before
changing anything: all three CONFIRMED, 0 false positives, all fixed in the convergence commit (the branch HEAD
after `4a67f13f`, subject "convergence fixes for plan 042"). Eight of the nine new tests, and the changed IL test,
were run red first; `Fingerprint_OfTheSameBody_IsStable` cannot be (it pins determinism, which no drift mutation
breaks; corrected after convergence round 2); mutation
evidence is in the run's `scratch/fix-042-r1/` folder (`mutate.py`, `mutation-M1..M4-*.txt`, `red-fingerprint.txt`,
`final-full.txt`), with each mutated file restored byte for byte and its sha256 checked (no git command).

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| R1 | LOW | The IL drift guard projected each call to `DeclaringType.Name::Name`, so a constant argument (`keepDuplicates: false` at `MBObjectManager.cs:974`, the loop start, list indices, `""`), a same-name overload and the element type of a generic list were invisible; the test comment, feature doc and row F1 above claimed more | CONFIRMED: `IlCallScanner.ExtractCalledMethods` yields only `InlineMethod` operands; under that projection the six pairs in `IlFingerprintTests` (false or true, loop from 1 or 0, `""` or `" "`, `Over(object)` or `Over(string)`, `List<string>` or `List<Tuple<string, string>>` `Count`, `<` or `<=`) fingerprinted the same (`red-fingerprint.txt`: `Failed: 7, Passed: 1`) | Fixed. New `IlCallScanner.Fingerprint`: in IL order, each call with its constructed declaring type and parameter list, each constant (`ldc.*`, `ldstr`, `ldnull`), each field load, each comparison and conditional branch. `MirroredEngineBodies_CallExactlyThePinnedSequence` re-pinned from the installed v1.5.3 bodies and checked line by line against the decompile (`MBObjectManager.cs:962-1031`): the `ldc.i4 0` before `MergeTwoXmls` is `keepDuplicates: false`, the first `ldc.i4 1` the loop start, the second the increment. Red under a pin of `keepDuplicates: true` (M4). Claims narrowed where the fingerprint still stops: it does not pin which local or argument feeds a call, which statements a branch skips, or `MergeElements` (the branch case added after convergence round 2), so the feature doc, the test comment and the engine-bump skill (Phase 4 step 1) now say to run `XmlMergeLiveEquivalenceTests` after every engine update whatever the gate says. Lesson in adapters-taleworlds-api amended |
| R2 | LOW | Which method's postfixes and finalizers count (the target's never, a watched method's always) was chosen only in `ForeignPatches()` (`XmlMergeEngineAdapter.cs:112, :114`), with no test that could fail on a swap | CONFIRMED: the five filter tests pass the flag themselves; the real `ForeignPatches()` ran only with nothing patched | Fixed. `ForeignPatches(target, watched, patchInfo)` is now an internal static seam, the instance method passes `Harmony.GetPatchInfo`; `ForeignPatches_CountsTheTargetsPatchesBeforeTheOriginal_AndEveryPatchOnAWatchedMethod` feeds every collection from a foreign owner on a dummy target and a dummy watched method. Red under the flag swap (M1: `Failed: 1, Passed: 5`) |
| R3 | LOW | The record, RCA and REVIEW-LOG said every new test was proven red, but the cited batches proved 16 of 19; RCA row 9's "Why missed" said the rewrite test changed time and length | CONFIRMED: `XsltTransformCacheTests` `DropB` and `DropC` differ only in `'b'` and `'c'`, and `Apply_FileRewrittenWithNewTime_Recompiles` writes one over the other | Fixed. The three missing proofs run: `WatchedMethod_EveryForeignPatch_IsForeign` red with the `Postfixes` line removed (M2), `NoPatchInfo_FindsNothing` red with the null-info guard removed (M3), the IL test red under M4. The three statements now say 16 of 19 plus this round; RCA row 9 corrected |

**Tests added:** `IlFingerprintTests` (8: six one-operand pairs, the exact tokens of a call and its constant, a stable
re-read) and one in `XmlMergeForeignPatchFilterTests`.

**Final suite (after the fixes):** `Failed: 1, Passed: 12440, Skipped: 2, Total: 12443`, against `Failed: 1,
Passed: 12431, Skipped: 2, Total: 12434` at `4a67f13f` (the 9 new tests); the one failure is the base's
`EveryLanguage_DeclaresARowForEveryEnglishKey`. Reference-assembly unit step: `Failed: 3, Passed: 10115,
Skipped: 28, Total: 10146`, the three known base failures only.

**Orchestrator focus items re-checked in this round:** nothing in the merge path changed (the service, patch and cache
are untouched); the adapter change is a pure extraction, so the stand-aside owners and the six collections are as
recorded above.

## Convergence round 2

Run by the orchestrator as one `deep-reviewer` over `4a67f13f..98a69d4f`, after a session restart interrupted
the review workflow before its own round-2 pass. Every pin matched installed v1.5.3 (the test bin's
`TaleWorlds.ObjectSystem.dll` sha256 equals the installed one; all five fingerprints re-derived by hand from
`ilspycmd -il`), and the merge path is unchanged. Three LOW findings, closed in the commit
`docs(xml-merge): v2.0.32 - record plan 042's convergence rounds`:

| # | Sev | Finding | Verdict | Resolution |
|---|---|---|---|---|
| F1 | LOW | The narrowed "not pinned" lists (test comment, feature doc, R1 row, `IlCallScanner` summary) omitted branch structure: `FingerprintToken` drops `br`, `leave`, `switch` and every branch target, so a dropped `else`, an added `continue` or a nested `if` fingerprints the same | Confirmed by a verbatim scanner replica | All four lists now name it |
| F2 | LOW | Round 1's "every new test was run red first" is false for `Fingerprint_OfTheSameBody_IsStable`, which pins determinism | Confirmed: it passed in the cited red run and no mutation touches it | Claim corrected here and in the REVIEW-LOG entry: eight of nine new tests, plus the changed IL test |
| F3 | LOW | `ForeignPatches_CountsTheTargetsPatchesBeforeTheOriginal_AndEveryPatchOnAWatchedMethod` fed every method the same patches, so a seam reading the target's patch info in the watched loop passed | Confirmed | Each method now gets its own owner. Proven: green on the real code (`Passed: 6`), red with the watched loop reading `patchInfo(target!)` (`Failed: 1, Passed: 5`), adapter restored byte for byte (sha256 identical) |

Nits left as they are: the engine-bump skill could say to confirm the harness tests passed rather than skipped;
one test name lacks the `Fingerprint_` prefix; no hosted fingerprint test covers the release-IL branch opcodes;
`harmony-patch-registry.md` still says "the ordered call sequence".

## Review pass of 2026-10-03: Claude re-review and Codex adversarial review

After the maintainer's follow-ups (`d797b895`), a Claude re-review (2 LOW) and a Codex adversarial review of
`efb70763..d797b895` (1 P2, 3 P3) ran. The fix pass re-read every finding against the code, the installed v1.5.3
decompile (`taom-src`, `MBObjectManager.cs`), `PatchShield.cs`, `PatchShieldPolicy.cs` and the perf run's decision
record before changing anything. All six were CONFIRMED; the PatchShield one is settled by merge order, not by code on
this branch. The issue the sections above call `#TBD` was filed as #724.

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| C1 | Claude | LOW | The feature doc lists the three levers decision D14 settled as open, and its Issue line still says #TBD | CONFIRMED: D14 reads "no lords.xml, build-step merge or off switch"; issue #724 is filed and open | "Decisions" section with each lever marked decided; Issue line and changelog updated. The 039 and 040 feature docs carry the same stale Issue line on their own branches and are not touched here |
| C2 | Claude | LOW | "Known limits" gives the harness remedy without `TAOM_RUN_BENCHMARKS=1`; the `FastFailedDetail` comment says the DEBUG line follows FastFailed on every fallback | CONFIRMED: without the variable the harness skips; in `NoteFastFailure` the WARNING sits inside the once-per-type `if` and the DEBUG line outside it | Doc and comment corrected |
| X1 | Codex | P2 | PatchShield turns a failed engine merge into a silently skipped type | CONFIRMED as a hazard of this branch alone: pass 2 attaches `ShieldFinalizerWithResult` (`PatchShield.cs:149-210`), which swallows the missing-API trinity (`:291-306`) into a null result, and `LoadXML` passes the null to `LoadXml` inside its own try/catch (`MBObjectManager.cs:786-797`, `:1357-1362`). SETTLED BY MERGE ORDER: plan 040 puts `CreateMergedXmlFile` on `ExcludedTargetMethods` and merges first | The dependency is documented (feature doc "Shipping order", registry, patch comment) and the text that said "the unpatched game's outcome" now carries the condition. No exclusion entry is added here: a second copy would duplicate 040's |
| X2 | Codex | P3 | The live gate can pass without heavy-type merges or the speed bar | CONFIRMED: two failures of one exception type counted as equivalence; heavy times were printed and never asserted; the inventory check was "at least one type", and an absent module was skipped silently, an explicitly requested one included | `LiveGateRules` and `LiveMergeListBuilder.Select` and `Missing`: an engine exception fails, every requested module must exist (`FastMode` may be absent from the default order only), the four heavy types must be present and merge, and their fast total must be at most half the engine total. The rules are pure, so `LiveGateRulesTests` and `LiveMergeListBuilderTests` pin them in the default suite |
| X3 | Codex | P3 | Runtime eligibility is broader than the proven equivalence domain | CONFIRMED from source: `DecideLocked` looks at no input, and the XSLT cache keys on path, last-write time and length | Documented precisely in "Known limits". No runtime guard: it would change behavior and no decision asks for one |
| X4 | Codex | P3 | The recorded engine time crosses the plan's STOP boundary | CONFIRMED: 11.2 to 11.7 s for NPCCharacters against a literal limit of 11.2 s | Recorded in "Performance", with the same-host ratio (now asserted) as the comparison policy. The records show no stop-and-resume authorization; none is claimed |

**Evidence.** The 26 new test cases (20 in `LiveGateRulesTests`, 6 in `LiveMergeListBuilderTests`) ran red first
against stubs that mirrored the harness's old rules: `Total tests: 26, Passed: 12, Failed: 14` (the failures are the new
rules; the 12 passes pin behavior the rules keep). Green after: `Passed: 26`. Twenty mutations of the new guards (each
rule's branch, the optional-module exemption, the explicit-order test, the bar's comparison, the totals-versus-per-type
choice, the heavy-types-only sum, trimming and splitting) were each killed by exactly the tests expected, every mutated
file restored byte for byte (sha256 checked, no git command). Live: the default order passes (Campaign 16.7%, CustomGame
9.4% of the engine time), Codex's misspelled-module example (`TAOM_Mapp`) now fails both tests, and a bar shrunk to 5%
fails both on the ratio, which proves the harness hands real data to the rules. Full suite `Failed: 1, Passed: 12470,
Skipped: 4, Total: 12475` against `Failed: 1, Passed: 12444, Skipped: 4, Total: 12449` before; the one failure is the
known `EveryLanguage_DeclaresARowForEveryEnglishKey`. The new classes also pass on reference assemblies (37 of 37 with
`XmlMergeLinesTests` and the opt-in pin, `-p:TaomGameRefs=RefAsm`).

## Convergence review of the follow-ups: second round, 2026-10-03

A convergence review of the previous fix (`d797b895..92cdbb0c`) raised four LOW findings. The fix pass re-read each one
against the code before changing anything: `PatchShield.cs`, plan 040's branch (`perf/040-load-time-stamps`), the merged
`perf/integration-trial` registry, the harness and its rules, and the plan's offline figures. All four were CONFIRMED.

| # | Sev | Finding | Verdict | Action |
|---|---|---|---|---|
| R1 | LOW | The three passages edited for X1 (feature doc "Harmony shape", the patch comment, the registry) still said as plain fact that PatchShield's second pass gives `CreateMergedXmlFile` its value-returning finalizer, beside the rule that 042 ships only after plan 040's exclusion; the merged registry would say both | CONFIRMED: `PatchShield.cs:184-189` skips an excluded method before `harmony.Patch` (`:209`); 040's `PatchShieldPolicy.cs:156` lists the method; Patch99's finalizer and 040's stamp finalizer are both `void`; in `perf/integration-trial` the registry's Patch99 text (`:1137`) says the value-returning finalizer attaches while its Patch100 text (`:1145`) says the method is excluded | All three passages state the shipped shape first (PatchShield never attaches, every finalizer is void, Harmony keeps `rethrow`) and the branch-alone shape as the hazard (`ShieldFinalizerWithResult`, `throw` plus `RethrowStackPreserver`, the swallow). "PatchShield's per-call finalizer is no concern here" is dropped |
| R2 | LOW | When the engine threw, neither the gate's message nor the table row said what the fast path did, and the message named one cause | CONFIRMED: the pre-fix harness printed the fast side; `LiveGateRules.CheckTypes` built the message from `EngineError` alone and the row from the engine's type alone. "Engine threw, fast merged" means the fast path accepts input the engine rejects | The message appends the fast side ("merged, so it accepts what the engine rejected" or "threw X: message") and names both causes (the harness's lists differ from the game's, or the fast path diverges); the row reads "engine threw X, fast merged" or "engine threw X, fast threw Y". Both halves are pinned in `CheckTypes_OnlyTheEngineThrew_Fails` and `CheckTypes_BothSidesThrowTheSameExceptionType_Fails` |
| R3 | LOW | The timing policy said a slow host "moves both sides" and called the margin threefold | CONFIRMED from the recorded figures: the four heavy types' engine total is 16,837 ms in the harness against the prototype's 8,806 and 9,003 (1.87 to 1.91 times), the fast total 2,772 ms against 3,078 and 2,438 (0.90 to 1.14 times), so the harness ratio (16.5%) reads about half the prototype's (27.1% and 35.0%). The margin is 3.04 times on the harness ratio and 1.43 to 1.85 times on the prototype's | The code comment and "Timing policy" say what the numbers show and give the margin both ways. Whether to assert a tighter harness bar (25%, say) is recorded as open for the maintainer; the bar stays at 50% |
| R4 | LOW | `Select` kept a white-space-only segment ("Native; ;TAOM") as an empty module name, which the new missing-module rule reported as a failure naming nothing | CONFIRMED: `Split(RemoveEmptyEntries)` drops only zero-length entries and .NET Framework 4.7.2 has no `TrimEntries`; the two new rows failed first with "Different number of elements" | `Split(';')`, then `Trim()`, then `Where(m => m.Length != 0)`; rows for a blank middle segment and a blank trailing segment added to `Select_AnEnvironmentValue_IsExplicitTrimmedAndSplitOnSemicolons` |

**Evidence.** Red first, against the unchanged rules: `Failed: 4, Passed: 24, Skipped: 0, Total: 28` (the two
gate-message tests and the two new rows). Green after: `Passed: 28`. Six mutations of the new guards were each killed
by exactly the tests expected (the fast side's "merged" text, the fast exception's message, the whole fast side, the
cause wording, the blank filter, and the filter placed before the trim), every mutated file restored byte for byte
(sha256 compared, no git command). The table row lives in the opt-in harness and cannot be unit-pinned, so it was run
live: a temporary edit of the harness that made the engine throw for `Monsters` printed `engine threw
InvalidOperationException, fast merged` and the new message; a second that made both sides throw printed `engine threw
ArgumentException, fast threw ArgumentException` and both messages. The harness file was restored byte for byte. Default full suite
`Failed: 1, Passed: 12472, Skipped: 4, Total: 12477` against `Failed: 1, Passed: 12470, Skipped: 4, Total: 12475`
before; the one failure is the known `EveryLanguage_DeclaresARowForEveryEnglishKey`. The live harness passes both
tests (`Campaign` 16.3%, `CustomGame` 9.3%, every type identical). On reference assemblies the CI filter passes 39 of 39.
`python tools/lint_docs.py --fail-on-drift` exits 0.
