# Plan review: 042 (XML merge fast path), round 1

Reviewer: cold plan reviewer, read-only. Plan read at `plans/042-xml-merge-load-time.md` (1,320 lines,
untracked in the run worktree). Code read at `0912e1b7`; worktree HEAD is `80176e2a`, and
`git diff --stat 0912e1b7..HEAD` over the plan's drift-check paths prints nothing (docs and run
records only since). No earlier `plan-review-042*.md` existed.

## What was checked, and how

- **Engine excerpts**: `pwsh tools/taom-src.ps1 path` for `MBObjectManager`, `XmlResource`,
  `ModuleHelper`, `MBObjectManagerExtensions`, `CustomGame`, `Campaign`, `GameType`, `Debug`,
  `GameTextManager`, `BannerManager` (cache `v1.5.3`). Every quoted body and every line range in
  plan:75-289 matches the decompile: `CreateMergedXmlFile` :962-978, `ApplyXslt` :980-991,
  `MergeTwoXmls` :993-1006, `ToXDocument` :1008-1021, `ToXmlDocument` :1023-1031, `MergeElements`
  :820-871, `MergeElementAttributes` :799-818, `GetMergedXmlForManaged` :873-914, `HandleXsltList`
  :945-960, `LoadXmlWithValidation` :1057-1125, `InjectOptionalAttrToAllComplexTypes` :1239-1270,
  `ValidationEventHandler` :1320-1337, `CreateDocumentFromXmlFile` :1339-1355, `LoadXML` :786-797,
  `XmlResource` :78, :116, :171, :258; `Debug.Print` :163 (no-op when `DebugManager` is null);
  `GameType.GameTypeStringId => GetType().Name` (so `Campaign` and `CustomGame`); `CustomGame.cs`
  :50 `LoadCustomGameXmls()`, :56 `OnGameInitializationFinished`; `Campaign.cs` :1385 and :1471;
  `GameTextManager.cs:117` and `BannerManager.cs:198` pass `skipValidation: false`.
- **DLL gate**: `pinned-game-version.txt` prints `v1.5.3`; installed `TaleWorlds.ObjectSystem.dll`
  is 61792 bytes.
- **Repo excerpts**: `SubModule.cs` :656 and :1541-1566 (at `0912e1b7`), `FeatureModules.cs` list,
  `CoopVetoClassificationTests.cs` :307-312, `ReflectionSiteBindingTests.cs` :120,
  `harmony-patch-registry.md` :1045 and :1101 (backlinks block :1107), `feature-map.md` :103,
  `object-system-mbobjectmanager.md` :37, `FileLogger.cs` :9-11, `ObjectManagerAdapter.cs` :108-109,
  `MonsterSizeCatalogAdapter.cs` :48-49, `Patch83_StaleCharacterRepair.cs` :33,
  `Patch88_SpawnLordPartyScope.cs` :36, `CreatureImpactSoundTests.cs` :113, `IlCallScanner` :111,
  `LordFamilyTransformTests` (LiveInstall, Inconclusive without the game), `tests.md` categories,
  `csharp.yml` filters, `binding-gate.runsettings`, Harmony ids (`com.taom.mod` at
  `SubModule.cs:204`, PatchShield's id at `PatchShield.cs:37`), every harmony-il lesson title the
  plan names. All match.
- **Baseline**: `baseline.md:7` holds the quoted totals line. `git grep -n "Patch99_" 0912e1b7 --
  Main Dependencies TAOM.Tests docs` prints nothing.
- **Fixture RED/GREEN simulation** (cold-review checklist): `fixture_probe.ps1` in my scratch folder,
  Windows PowerShell 5.1 loading the installed engine as Step 10 does, wrote the plan's fixtures
  (XSD without the leading comment, see N4), called `XmlResource.ReadXsdFileAndExtractInformation`
  and ran the Design's loop with the engine's own `CreateDocumentFromXmlFile`, `ToXDocument`,
  `ToXmlDocument` and `MergeElements` plus a line-for-line `ApplyXslt` mirror. Output: XSD keys
  `/Things unique=[]`, `/Things/Thing unique=[id]`, `/Things/Thing/Part unique=[]`; all nine
  document cases `identical=True` (NoXslt_ThreeFiles 231 chars, XsltBesideSecondFile 213,
  XsltOnlyEntryInTheMiddle 213, XsltIsTheLastStep 271, FirstEntryXslt 229, SingleEntry 97,
  EmptyXsdPath 168, ReplaceWhileMerging 118 keeping `_replaceWhileMerging="true"`, TwoXsltsInARow
  227); MissingFile: `engine=FileNotFoundException fast=FileNotFoundException`. The Design's loop
  and the Step 6 oracles hold on the fixtures.
- **RefAsm private members**: the BUTR reference `TaleWorlds.ObjectSystem.dll`
  (`1.5.3.122374-beta`) contains the names `CreateDocumentFromXmlFile`, `LoadXmlWithValidation`,
  `InjectOptionalAttrToAllComplexTypes`, `ValidationEventHandler`, so the new
  `BindingVerification` rows can resolve in the hosted gate.
- **Docs lint at base**: `python -B tools/lint_docs.py --fail-on-drift` exit 0, writes nothing.

## Blocking

**B1. The dash done criterion cannot pass (plan:1227-1229).** It requires that "no new or changed
file holds U+2013 or U+2014" and a script over "each in-scope file" with empty output. Files this
plan changes already hold em dashes at `0912e1b7` (line counts):
`docs/reference/engine/object-system-mbobjectmanager.md` 18 (its title line and the `LoadXML`
heading at :37 itself), `docs/reference/harmony-patch-registry.md` 103, `docs/reference/feature-map.md`
40, `docs/reference/taleworlds-api-snapshot/reflection-sites.md` 12,
`TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` 5,
`TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs` 30, `Main/SubModule.cs` 135. The
script's output is never empty, so a literal executor fails the criterion or "fixes" prose it must
not touch (out of scope). Fix: scan only what this plan adds: the `+` lines of
`git diff -U0 0912e1b7 -- <in-scope paths>` plus whole new files (or, for markdown, the dash report
of `python -B tools/lint_docs.py --dash-base 0912e1b7`), and say "existing lines are exempt".

**B2. Step 7b's verify uses `git grep` on untracked files (plan:1052-1053).** Nothing is staged or
committed before Step 9, so `Main/Features/XmlMerge/XmlMergeModule.cs` is untracked and
`git grep -n "Patch99_XmlMergeFastPath" -- Main` prints nothing; the expected "prints the module
constant" fails every time and the executor hits "a step's verification fails twice" and STOPs.
Fix: `git grep --untracked -n "Patch99_XmlMergeFastPath" -- Main` (or `grep -rn ... Main`). The
Done-criteria `git grep` lines (plan:1217-1225) run after the commit and are fine; say so, or add
`--untracked` there too for a run that stops before Step 9.

## Non-blocking

**N1. Type visibility is unspecified and the literal shapes conflict (plan:729, 767, 947-956,
994-999).** The patch class is `public static` with `public static void Initialize(XmlMergeService)`
and `public static bool Prefix(..., out XmlMergeCall __state)`, so the service and `XmlMergeCall`
must be public; the repo's module services are `public sealed` with public constructors
(`TournamentJoinService.cs:13,20`). But `XsltTransformCache` is specified `internal sealed` and is
a constructor parameter of the service: a public constructor then fails with CS0051. The tempting
fix (an `internal` constructor) compiles and passes every planned test (they construct the service
directly via InternalsVisibleTo), but DryIoc's default container (`IoC.cs:92`, no custom rules)
needs a public constructor, so `InitializeStatics` faults at startup and the fast path never runs;
no planned test would catch that. Fix: state `public sealed class` for `XmlMergeService`,
`XsltTransformCache`, `XmlMergeCall`, `XmlMergeCounters` (public constructors), and add one test that
registers `XmlMergeModule` into a fresh `Container` with a substitute `IModLogger` and resolves
`XmlMergeService` (or `RequiresGame` if the real adapter constructor must run).

**N2. Step 7b has no RED (plan:937-1053).** The template wants a failing test before each C#
implementation step. Cheap order: write the patch class, run `CoopVetoClassificationTests` and quote
its failure naming `MBObjectManager_CreateMergedXmlFile_Patch`, then add the registry entry; likewise
`FeatureModulesTests` or the N1 module test before the `FeatureModules.All` line.

**N3. Step 6's RED expectation is wrong for one test (plan:892-893).**
`MissingFile_ThrowsTheSameExceptionTypeAsTheEngine` compares exception types, so against the stub
it fails by assertion (`FileNotFoundException` vs `NotImplementedException`), not with a
`NotImplementedException`. Say "every test fails; MissingFile by its type assertion".

**N4. The fixture XSD block cannot be copied literally (plan:841-842).** It opens with
`<!-- Things.xsd -->` before `<?xml version="1.0" encoding="utf-8"?>`; an XML declaration that is not
the first node is an `XmlException`, so `ReadXsdFileAndExtractInformation` throws "Malformed XSD
schema" in `[ClassInitialize]`. Move the label out of the block (my probe ran without it and
passed).

**N5. `LiveMergeListBuilder` omits two engine branches (plan:899-911).** (a) The module-level XSD:
`GetMergedXmlForManaged` (:884-888) uses `<module>/ModuleData/XmlSchemas/<id>.xsd` when it exists;
true today that none exists (checked: no `Modules/*/ModuleData/XmlSchemas`), but the harness should
mirror it or assert its absence, since a mod that ships one makes the gate compare a list the game
never builds. (b) `IncludedGameTypes`: the engine takes the `value` of every element child
(`XmlResource.cs:291-303`), not only `GameType`. Also say "enumerate with
`new DirectoryInfo(folder).GetFiles("*.xml")` in its own order, unsorted".

**N6. The ready header claims more than Stage 1 resolves (plan:572 vs 820-829).** The header names
`MergeElements, ToXDocument, ToXmlDocument` as resolved, but Step 6 binds only
`CreateDocumentFromXmlFile` by reflection; the three public helpers are direct calls that fail only
when first JIT-compiled. Either resolve them with `AccessTools.Method` in the constructor too (so
`BindingProblem` covers them) or reword the header literal.

**N7. Summary semantics are open (plan:579, 808).** Whether `files`, `ms`, `max_ms`, `max_type`
include engine-path merges is not stated, nor whether `xslt_compiles` and `schema_builds` count
vanilla merges. The executor writes both the code and the test, so it will be self-consistent, but
the feature doc then documents whatever was guessed. State it (suggest: `merges`, `files`, `ms`, the
maxima over all merges; cache counters over fast merges only).

**N8. Unpinned signatures and names (plan:698, 706-721).** `Fast(...)`, `Vanilla(...)`,
`Summary(...)` parameter lists, the `FastFailed_...`/`Disabled_...` test names and the
`SameDocument` message text for the different-lengths case (and which index a prefix difference
reports) are left to the executor. Not fatal; pin them for a weaker model.

**N9. Step 9's "every new non-engine test executed there" has no command (plan:1088-1089).** Give one:
the RefAsm unit step with `--logger "trx;LogFileName=unit.trx"` and a count of `XmlMerge` results, or
`--filter "FullyQualifiedName~XmlMerge&TestCategory!=RequiresGame&TestCategory!=LiveInstall"` with an
expected count.

**N10. Stale orchestrator step (plan:1268-1271).** "Plan 040 (unwritten)": `plans/040-load-time-stamps.md`
now exists in the run worktree; its :496 already records that 028, 041 and 042 reserve
`Patch97`-`Patch99` and its :22 says 042 is judged by its stamp (1). Reword to "plan 040 already
accounts for this".

**N11. Drift exemptions are narrower than the shared files (plan:12-15).** Plan 040 also appends to
`ReflectionSiteBindingTests.cs`, `reflection-sites.md`, `harmony-patch-registry.md` and
`feature-map.md`. If 042 is ever rebased onto 040's tip, appended rows there are "a change in an
in-scope file" and read as a STOP. Extend the exemption to appended rows or sections by other plans,
re-anchored by text.

**N12. `reflection-sites.md` convention (plan:1071).** Each addition there carries a dated
`Status (...)` line naming the rows and the version they resolve against (:89-97). Ask for one, and
name the table (Category B) the row goes in.

**N13. Absolute path (plan:316).** `C:\ProgramData\Mount and Blade II Bannerlord\logs` is a generic
install path, but the quality bar says no local absolute paths in a plan; say "the engine's rgl log
folder under ProgramData".

**N14. Minor.** MSTest does not guarantee method order, so "the custom battle run sees XSLT cache
hits" (plan:916-918) holds only if `Campaign` runs first; say "whichever runs second". The harness
times the engine first and the fast path second, so the fast run gets a warm file cache; acceptable
for a 50% gate, worth one sentence. Blast radius (plan:432-440) omits `FeatureModules` and
`ReflectionSiteBindingTests`, which this plan edits. The collapsed catch comment at plan:183-184 says
"two Debug.Print lines naming xmlPath"; only the second line names `xmlPath` (the first prints line,
position and source object, or the parameter name).

## Checklist answers

1. Executable with only the plan and the repo: yes after B1 and B2; the knowledge gaps are N1
   (visibility), N5 (two list branches), N7 and N8 (unpinned shapes).
2. Every step ends in a command with an exact result, except Step 7a's count explanation (bounded
   by a STOP) and Step 9's "executed there" (N9).
3. Excerpts vs code at `0912e1b7`: all engine and repo excerpts match; mismatches are only the
   collapsed-comment wording (N14) and the draft fixture XSD (N4).
4. TDD order holds for Steps 2 to 6; Step 7b lacks a RED (N2). Issue line present (filed by the
   orchestrator; `#TBD` rule in Step 8). ADR-002, 003, 004, 005, 007, 008 named. No protected file;
   Step 0 says none. Single-owner `SubModule.cs` edit is exact and checked by the Done criteria;
   `IoC.cs` and csprojs out of scope. STOP conditions are specific (DLL size, byte difference, IL
   shape, 50% bar, Patch99 taken). Planned-at `0912e1b7` and drift paths cover Scope. Commands are
   non-deploying with `-p:ModuleId=`. No worktree path or branch name. No CHANGELOG step.
5. No em or en dash in the plan; no secret values.
