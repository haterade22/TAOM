# Deep review: plan 009 maintainer decisions (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 009, maintainer decisions (class-by-class category index, localized failure
         notice, #653 in the CHANGELOG heading)
Date: 2026-09-24
Branch: improve/009-guarded-patch-category-apply (worktree E:\repos\taom-improve\wt-009)
Range reviewed: 4c728dac..b6cb6ff5 (7912fdd8, b6cb6ff5); 7eae4704 landed during the review

Scope:   C# (Main/PatchCategoryIndex.cs, Main/PatchCategoryApplier.cs, Main/SubModule.cs, two
         test files), XML (taom_module_strings.xml and 12 std_taom_module_strings_*.xml),
         12 translation caches, docs and CHANGELOG
Waves:   wave 1 Agents 1, 2, 5, 7; wave 2 Agents 3, 4, 6; Codex adversarial on the same range

STANDARDS:     FAIL, 1 HIGH (translations outside <strings>), 3 LOW, 1 NIT
COMPATIBILITY: FAIL, 1 incompatible (LocalizedTextManager.LoadLanguage), 2 unverified
EFFICIENCY:    PASS, 0 issues
COMPLETENESS:  INCOMPLETE, C-1 (translations never load) plus 4 LOW and 2 NIT
DATA FLOW:     FAIL, 1 gap (HIGH, trace 4), 3 inconsistencies (LOW, traces 3, 5, 14)
DESIGN:        3 KEEP proposals (1 apply, 2 follow-up), 1 HIGH defect routed out of lens
XML:           FAIL, 4 gates (2 failed: the engine-read scan from this diff, the external
               loc-coverage check from unrelated live-install drift; 2 not run), 3 findings
TOOLING:       NOT IN SCOPE
```

## Verification of every finding

Each finding was re-checked against the worktree before any action. Duplicates across lenses are
merged; the lens ids are listed.

| # | Sev | Finding | Lenses | Verdict | Evidence read this pass | Action |
|---|---|---|---|---|---|---|
| 1 | HIGH | The 60 translated rows (5 keys, 12 languages) sat after `</strings>`, as children of `<base>`; `LocalizedTextManager.LoadLanguage` reads only `base/strings/string`, so every language showed English | A1 F1, A2 F1, A3, A4 C-1, A5 T4, A6 D1, A7 F1, Codex P2 | CONFIRMED | ElementTree scan of every `Languages/*/*.xml` at HEAD: 0 stray rows, 5 `taom_patch_apply_*` rows inside `<strings>` in each of the 12 files. The same test run against the `b6cb6ff5` blobs found all 5 ids outside `<strings>` | Rows moved by `7eae4704` (landed while the lenses ran). This pass adds the gate `LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent`: RED with the `b6cb6ff5` blobs written back into the worktree ("Expected:<0>. Actual:<5>", BR first), GREEN at HEAD. CHANGELOG and decision row 2 of the first report corrected |
| 2 | LOW | The seeded rows ended `\r\n` in files whose lines end `\r\r\n` | A7 F2 | CONFIRMED at `b6cb6ff5` | Bytes at HEAD: DE lines 2665 to 2678 end in LF, like the #608 rows before them; `</strings>` and `</base>` end `\r\r\n` | Resolved by `7eae4704`, which wrote the neighbours' LF. The file-wide mix predates plan 009 (follow-up) |
| 3 | LOW | The phase noun is translated alone and dropped into the sentence: DE "während Start", FR "lors de le démarrage", JP "起動時中に" | A4 C-2, A5 T5, A6 O1, A7 F3 | CONFIRMED | Read all 60 rows. PL and RU store genitive phases; BR, CNs, CNt, IT, KO, SP, TR read correctly | Fixed in the XML and the cache together (the translator only re-translates a row that still equals the English, so the cache keeps the fix): DE phases "des Starts", "der Spielinitialisierung", "des Missionsbeginns"; FR "pendant {PHASE}"; JP "{PHASE}に" with the stored "起動時" phases |
| 4 | LOW | A class the index skips belongs to no category, so its category applies the rest and `TryApply` returns true; the preview loop then logs "applied OK" beside the SKIPPED line | A1 F3, A2 F2, A5 T3, A6 P1, Codex P3 | CONFIRMED as behaviour; the fix is a design call | `PatchCategoryIndex.cs:44-48` and `:65`; `PatchCategoryIndexTests` asserts `TryApply(BrokenCategory)` is true; `SubModule.cs:1513-1514`. Patch77 is not bypassed: `Patch77_BodyGeneratorView_Constructor` has a bare `[HarmonyPatch]` and its `Prepare` names `typeof(BodyGeneratorView)`, so a removed type still fails the category | NEEDS MIKE (proposal 1 below is behaviour-changing). The semantic is now documented on `PatchCategoryIndex.Apply`, `PatchCategoryApplier.TryApply` and in the CHANGELOG |
| 5 | LOW | The null-category guard in `Build` is untested; without it `TryGetValue(null)` throws out of `OnSubModuleLoad` | A1 F4 | CONFIRMED | No uncategorised patch class in TAOM.dll or the probe assembly | Probe class with `[HarmonyPatch]` and no category added; `Build_AClassWithNoCategory_IsNeitherSkippedNorApplied`. Mutation check: with the guard deleted, 5 index tests fail with `ArgumentNullException`; restored, all pass |
| 6 | NIT | Two test names break `MethodName_StateUnderTest_ExpectedBehavior` | A1 F5 | CONFIRMED | Names read; no other file references them | Renamed to `HarmonyPatchCategory_AnUnreadableAttributeInTheAssembly_ThrowsForAHealthyCategory` and `Build_OneClassWithUnreadableAttributes_SkipsOnlyThatClass` |
| 7 | LOW | The CHANGELOG paired the HEAD test count with the `7912fdd8` suite | A1 F2, A4 N-1, A5 T14 | CONFIRMED | CHANGELOG read | Now quotes this pass's suite and counts |
| 8 | NIT | Stale line references: lifecycle doc `:199`, `:207-616`, `:207`, `:623`; first report `Game.cs:299` | A2 F3, A5 T14 | CONFIRMED | `SubModule.cs:200` (`new Harmony`), `:210` (Patch37), `:619` (last module-load call), `:626` (`OnBeforeInitialModuleScreenSetAsRoot`); taom-src `Game.cs:300` `GameTexts.Initialize` | Fixed |
| 9 | LOW | `Apply` claims Harmony parity for a multi-class category (stop at the first failure, keep earlier classes) with no test | A4 C-3 | CONFIRMED gap | Every probe category had one class | `Apply_ACategoryWhoseSecondClassCannotResolve_ThrowsAndKeepsTheFirstClassPatched` on an emitted two-class category; green first run (characterisation of existing behaviour) |
| 10 | LOW | `docs/ai-includes/architecture.md` tree lists neither new root file | A4 C-4 | CONFIRMED | `:293-295` read | Both added |
| 11 | LOW | #653's body still describes `4c728dac` | A4 C-5 | CONFIRMED (title, state and label read with `gh issue view 653`) | Body edit is a public action this assignment does not authorize | Owed at `/ship` |
| 12 | NIT | Vanilla's `{=oHaWR73d}Ok` is untranslated in CNs and CNt | A4 N-2 | FALSE POSITIVE | A scan decoding UTF-8 and UTF-16 finds `id="oHaWR73d"` in all 12 Native language folders, including `CNs/std_common_strings_xml-zho-CN.xml` and `CNt/std_common_strings_xml-zho-HK.xml` (UTF-16, which a byte grep misses) | None |
| 13 | NIT | `taom_patch_apply_notice_title` could be `{=!}TAOM` | A7 NIT, A6 O2 | Not a defect | The maintainer chose the registered title | NOT APPLIED |
| 14 | UNVERIFIED | `PatchCategoryIndex.Build` runs outside any guard; a pre-2.3 runtime Harmony would now stop the module load where the old code failed every category | A2, A5 T9, A6 O4 | NEEDS MIKE (risk acceptance) | Every installed `0Harmony.dll` is 2.4.2.0 (lens reads); players' runtime Harmony unverified | None; recorded |

Lenses with nothing further: Agent 3 found no performance issue (the index costs Harmony's own
single pass; the failure path is cheaper because a throwing build is no longer repeated per
category).

## Details by lens

**Agent 1, Standards.** Checks 1 to 10 pass on the C#: `TextObject` is not sealed and ADR-007
names it; no `#region`, `#if` or `[Obsolete]`; `SubModule` changes are wiring. Findings 1, 4, 5,
6, 7 above.

**Agent 2, Compatibility.** 27 API usages verified against the installed v1.5.3 DLLs and Harmony
2.4.2 (the index mirrors `BuildCategoryCache` and `PatchCategory(Assembly, string)`; language data
loads in `BannerlordConfig.Initialize` before the startup inquiry; `GameTexts` is unusable at that
hook). One incompatible (finding 1), two unverified (finding 14; the non-English inquiry in game).

**Agent 3, Efficiency.** No issues. Outside its lens it reported finding 1 and proposed the gate
that was applied.

**Agent 4, Completeness.** Tests, IoC and SubModule.xml pass; #653 open and cited. Findings 1, 3,
7, 9, 10, 11, 12.

**Agent 5, Data flow.** 14 flows; the gap is finding 1, the inconsistencies are findings 3, 4 and
7/8. Confirmed that the emitted probe assembly cannot leak into the six `GetAssemblies()` scanners
(none reads attributes) and that `_failed` is drained after its last writer.

**Agent 6, Design.** Proposals 1 to 3 below; finding 1 routed to the data lenses; `PatchCategoryIndex`
and the probe-assembly technique judged already optimal.

**Agent 7, XML.** `validate_xml_schemas.py` PASS, `validate_moduledata.py` PASS (0 errors, 1591
older warnings), `check_external_loc_coverage.py` FAIL on live TAOM_Map and Armory drift that no
file in this diff touches, and an engine-read scan FAIL (finding 1). Findings 1, 2, 3, 13.
**OWED in game:** set the game to German, force one module-load and one game-init failure locally
(never committed), and check that the inquiry body, phase and title render in German with
vanilla's translated `str_ok` button and that the game-init chat line is German.

## Action items

1. Mike: decide finding 4 (should a category that lost a class at index time count as failed?).
   Proposal 1 is the ready fix.
2. Mike: accept or guard finding 14 (index build outside any guard on a pre-2.3 Harmony).
3. `/ship`: refresh the #653 body (finding 11) and land the `.claude/rules/harmony-patches.md:61`
   gate line owed at merge.
4. In-game smokes: the German inquiry above, plus the earlier U1 and U2 smokes from the first report.

```
IMPROVEMENTS (Step 4)
```

**APPLIED:**
- `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs`: Agent 6 proposal 2 and Agent 3's
  gate, `AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent` (test only, PRESERVING).
  RED on the `b6cb6ff5` language blobs, GREEN at HEAD.
- `TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs`: findings 5, 6 and 9 (tests only).
- `Main/PatchCategoryIndex.cs`, `Main/PatchCategoryApplier.cs`: doc comments stating finding 4's
  semantic (no behaviour change).

**NOT APPLIED:**
- `Main/PatchCategoryIndex.cs:44-48,63-68`, Agent 6 proposal 1 (recover the skipped class's
  category with a filtered `GetCustomAttributes(typeof(HarmonyPatchCategory), true)` read and make
  `Apply` throw for it): behaviour-CHANGING (the preview log, Patch37's hook subscription and
  Patch77's switcher disable all follow the new result), so it needs Mike. Agents 2 and 5 each
  proved the premise on CLR 4.0.30319.42000 in scratch probes; not re-run here.
- Agent 5 option (b), the same change in five lines: needs Mike, as above.
- Agent 7 NIT `{=!}TAOM` title: the maintainer chose the registered title.
- Agent 3 deferring the three `new TextObject(phase)` allocations behind a `Func`: rejected by its
  own lens under the simplicity criterion.

**FOLLOW-UP** (pre-existing code, not applied; no issue filed from a review-lead pass, since
filing a public issue needs the maintainer's word):
- `tools/translate_with_claude.py:748-777` `sync_missing_ids` (Agent 6 proposal 3): it detects
  `\r\n` because `"\r\n" in raw` is also true of `\r\r\n`, so in these files the bare-LF tail rows
  and `</strings>` split as one line and `insert_at = anchor + 1` lands after `</strings>`. Anchor on
  the `</strings>` line with `splitlines(keepends=True)` and add a mixed-ending case to
  `tools/tests/test_translate_batching.py` `SyncMissingIdsTests`. The new placement gate now fails
  any misplaced row, so the tool can no longer ship one silently.
- The 12 `std_taom_module_strings_*` files mix `\r\r\n` with bare LF (the `taom_aso_*` rows and the
  #608 behaviour rows, `75f1880a`); normalise in its own change.
- `LanguageFileCoverageTests.cs:74` and `LanguageTextIntegrityTests.cs:74` walk descendants; the new
  gate makes that harmless, but they could read `Root.Elements("strings").Elements("string")`.
- `AccessTools.GetTypesFromAssembly` drops a class that fails to load without logging it, in Harmony
  and in the index alike; recording `LoaderExceptions` would report it.
- `Patch37_CrashReport.cs:15` suggests `_harmony.UnpatchCategory`, which rebuilds Harmony's own
  index and would throw in the #653 case; any unpatch path needs a matching method on the index.
- Plan 018 (`improve/018-composition-root-first-steps`) anchors on the string phase API this diff
  replaced; re-cut after 009 merges.
- `RegisteredDefaultRoundTripTests.Prefixes` could add `taom_patch_apply_`.

**Convergence pass:** not run. The review lead cannot spawn agents; the applied improvements are
tests, two doc comments and data corrections. The orchestrator's second pass covers it.

## CODEX REVIEW

Codex adversarial review of `4c728dac..b6cb6ff5`, raw output
`docs/reviews/raw/codex-adversarial-009-guarded-patch-category-apply-decisions-2026-09-24.md`
(complete: ends with `END OF CODEX REVIEW`). Quality: it quoted the installed loader and Harmony
2.4.2 code, cross-referenced every localization id against its registration and the 12 cache files,
and dispositioned all 10 known suspects.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | HIGH | Yes | Finding 1. Every non-English player loses the decided change; rated as the lenses did. Moved by `7eae4704`, gated here |
| 2 | P3 | LOW | Yes, as behaviour | Finding 4. The success result is real and contradicts the SKIPPED line; the fix changes three call sites' behaviour, so it goes to Mike. Codex correctly found no Patch77 bypass |

Known suspects: 9 and 10 CONFIRMED (both are finding 1); 1 to 3 and 7 to 8 DISPUTED as defects of
this range, 4 to 6 UNVERIFIED historical events. I agree with each disposition: suspects 1 to 3 test
the original conversion, which this range does not change.

- **Confirmed bugs:** finding 1 (fixed and gated); finding 4 (documented, decision owed).
- **False positives:** none.
- **Design questions:** finding 4.
- **Things Codex missed:** the root cause in `sync_missing_ids` (Codex said only that the XML is
  wrong); the DE, FR and JP grammar (finding 3); the untested null-category guard (finding 5); the
  stale line references (finding 8); the CHANGELOG test snapshot (finding 7).

Root cause table (review-codex 3e):

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | Translated rows outside `<strings>` | Other: data written where the engine never reads | Assumed the translator's seeding placed rows correctly; every check counted rows at any depth | `AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent`; lesson in `lessons/localization-ui.md` |
| 2 | A category that lost a class reports success | Logic error (result semantics) | Parity with Harmony was the design goal, and Harmony has no notion of a skipped class | Semantic documented; decision to Mike |

## AGENTS.md lessons (pending)

For the consolidated Phase 3h pass across all branches:
- Bugs Codex typically misses: the tool-side root cause of a data defect (it reported the misplaced
  XML but not the seeding code that misplaced it), and grammar in machine-translated fragments that
  are substituted into a sentence.
- What Codex does well: proving a localization defect from the installed loader's own loop, and
  cross-referencing every new id against registration, XML and cache in one table.

VERDICT: READY FOR COMMIT (every confirmed defect fixed or, for finding 4, documented with the
decision owed to Mike; full suite: Failed 2, Passed 10256, Skipped 2, Total 10260, the two known
live-Armory tests on a branch based before `a39a9c86`).

## Convergence

The convergence reviewer read `b6cb6ff5..HEAD` (`7eae4704`, `27fd23bb`) and found no C# or data
defect, and two LOW doc defects. Both were checked against the files and are fixed.

| # | Sev | Defect | Verification | Fix |
|---|---|---|---|---|
| C1 | LOW | The RCA summary said "4 LOW ... 2 NIT", which sums to 6, not the 10 it claims | The RCA table has 8 LOW rows (#2, #3, #4, #5, #7, #9, #10, #11) and 2 NIT rows (#6, #8) | Summary now reads "8 LOW data, test, doc and result-semantics findings, 2 NIT" |
| C2 | LOW | `LESSONS-LEARNED.md` said Localization & UI has 49 lessons | `grep -c '^### '` gives 52 (two lessons appended by `27fd23bb`) | Set to 52 |

In the same edit, the two counts that had drifted before this range were set to their derived
values: Harmony & IL 62 to 63, Build, Tooling & Workflow 167 to 173. All 13 category counts now
match `grep -c '^### '` on their files.

- **False positives:** none.
- **Not fixed here, outside this range:** the untracked
  `codex-adversarial-009-guarded-patch-category-apply-decisions-2026-09-24.prompt.md` is left for its
  owner to commit; the stale line references in
  `docs/reference/engine/submodule-lifecycle-and-harmony.md` (`:294`, `:512`, `:640`, and the one at
  `:74`) predate plan 009 and are a follow-up.
- **Suite:** Failed 2, Passed 10256, Skipped 2, Total 10260. The two failures are
  `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
  `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`, the known live-Armory tests on a branch
  based before `a39a9c86`.

CONVERGENCE VERDICT: READY FOR COMMIT.
