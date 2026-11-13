# lane-tests findings (2026-11-12, baseline 7087fa633, v2.0.38)

Scope: `TAOM.Tests/` (2,403 tracked `.cs` files, 30,725 `[TestMethod]` lines plus data rows; the baseline run reports
39,259 tests), its csproj, `binding-gate.runsettings`, the shared helpers and the source-reading gates. Skip list
honoured: no finding edits `HeroDefence`, `FieldBallista`, `CoopInterop`, `FordsOfWar`, `LongDefeat` test folders (a few are
named below only as evidence of a pattern).

**Suite wall time (not mine to measure).** The orchestrator's baseline log
(`scratch/baseline/dotnet-test.log`, `times.txt`) holds: `Passed! - Failed: 0, Passed: 39250, Skipped: 9, Total: 39259,
Duration: 1 m 52 s`, and `dotnet-test exit=0 seconds=129` including the test build. The log carries no per-test timings (only
skipped tests print a duration), and no `.trx` is in the repo. So every per-finding time below is an estimate from counts and
is marked UNVERIFIED; each finding's fix sketch starts with a one-line Stopwatch measurement so the executor can confirm
before refactoring. The 9 skips on this desktop are all opt-in or `[Ignore]` (3 `Baseline_Regenerate_WhenAsked`, 2 FastMerge,
2 Warg, 1 HitchProbe benchmark, 1 `CargoSprite_...` with `[Ignore("#876 ...")]`), so the 450 `Assert.Inconclusive` sites hide
nothing on a machine with the game.

Ranked by leverage. Count commands are run from `TAOM.Tests/` in the baseline worktree.

---

### [TST-01] Read the Main and Client C# corpus once per test run, not once per gate

- **Evidence**: `Infrastructure/RepoPaths.cs:27-33` (`CSharpFiles` enumerates the tree on every call) and `:49-58`, `:71-80`
  (`ReadSource` and `MentionsOutsideComments` call `File.ReadAllText` with no cache). Callers that scan the whole of Main and
  Client: 56 call sites in 50 files (`git grep -nE 'CSharpFiles\((\)|RepoPaths\.CodeRoots|"Main", "Client"|ScannedRoots|CodeRoots)' -- . | grep -v 'static IEnumerable' | wc -l`
  prints 56; the `-l` form prints 50), plus 7 more `CSharpFiles("Main")`. Samples:
  `Adapters/AffectRelativesGateTests.cs:23`, `Infrastructure/BannedConstructsGateTests.cs:166`,
  `Infrastructure/SilentCatchRatchetTests.cs:39` (cached with a private `Lazy`, so it reads once for its own class only),
  `Infrastructure/ServiceLocatorGateTests.cs:34`, `Infrastructure/EntryPointLineCountGateTests.cs:237`,
  `Features/WarPledge/WarPledgeWiringTests.cs:79-81` (a `MentionsOutsideComments` loop reading every file), and 31
  `MentionsOutsideComments(` call sites. `Infrastructure/Localization/RegisteredDefaultRoundTripTests.cs:45` is a 57th
  full-tree reader that does its own `EnumerateFiles(..., "*.*", AllDirectories)` over Main and Client, including `bin` and
  `obj`, and filters afterwards.
  The tree is 4,610 `.cs` files, 17.2 million characters (`git ls-files 'Main/*.cs' 'Client/*.cs' | wc -l`, then `xargs cat | wc -c`
  gives 16.16 MB plus 1.46 MB).
- **Impact**: every one of those sites re-opens 4,610 files and decodes 17 MB. A Python read of the same files takes 0.26 s warm
  (`scratch/lane-tests/readtime.py`), and stripping comments takes 0.33 s more. Times 56 gives a floor of about 15 s of the
  112 s suite in file I/O alone, before the per-gate regexes. .NET `File.ReadAllText` timing on this tree: UNVERIFIED. The gates
  also run again in every `/verify`, `/ship` and executor pass of this run, so the cost repeats many times tonight.
- **Effort**: S to M. One cache in one file, no test body changes for the 50 callers.
- **Risk**: LOW. Callers read raw text; some then apply `Replace("\r\n", "\n")` or `StripComments` themselves, so the cache
  must hold the raw decoded text and nothing else. Memory: about 35 MB resident for the run. A test that writes into the tree
  while running would be stale; `git grep -n 'File.WriteAllText' -- . ` should be checked for writes under `Main/` (none seen
  in the callers read).
- **Confidence**: HIGH on structure (code read, counts run); MED on the saved seconds (UNVERIFIED).
- **Fix sketch**: In `RepoPaths`, add a `Lazy<Dictionary<string,string>>` of path to raw text for `Main`, `Client`, `Dependencies`,
  `CoopCompat`, filled on first use; `CSharpFiles` keeps its signature and returns the cached keys; add `RepoPaths.Text(path)` and make
  `ReadSource`, `MentionsOutsideComments` and the `File.ReadAllText` / `Select(File.ReadAllText)` sites in the 50 files use it
  (a mechanical replace). Pin it with one test in `RepoPathsTests`: two calls return the same string instance and the file
  count stays above 4,000. Measure first with a `Stopwatch` around `CSharpFiles().Select(File.ReadAllText).Count()`.
- **Delta**: pre-existing (commit `06252ae2c`, 2026-10-14, "faster wiring scans" already added the raw-first filter inside
  `MentionsOutsideComments`; the read itself was never cached).
- **P1**: no.
- **Plan candidate**: yes. The best test-suite cost saver that keeps all coverage, and it speeds every later verify.
- **Issue**: none known (issue tracker not searched, no `gh` call made).

---

### [TST-02] Build the IL call index of each TAOM assembly once

- **Evidence**: `Migration/IlCallScanner.cs:28-58` (`FindCallers` walks every method body of the assembly, resolving each
  `call`-shaped token with `Module.ResolveMethod`, `:110-139`), called per test with no cache. Whole-TAOM scans through
  `Infrastructure/TaomAssemblies.cs:28-44` (core plus client, 2 assemblies per call): `Composition/ClientSubModuleTests.cs:75`,
  `Features/BattleBalance/PartyOwnerGetterBanTests.cs:46`, `Features/Elephant/HowdahCrewLookupBanTests.cs:39`,
  `Features/ElephantLike/CrewAgentBudgetTests.cs:38` and `:39` (two full scans in one test), `Features/Enlistment/SettlementEncounterInvariantTests.cs:93`,
  `Features/PlayerSwitcher/InformationManagerClearBanTests.cs:59`, `Features/ServerSettings/ServerSettingsSourceTests.cs:75`,
  `Infrastructure/TaomAssembliesTests.cs:28`. Direct whole-assembly scans of TAOM: `Infrastructure/MusicPauseGateTests.cs:47` (both
  assemblies), `BehaviorTrees/CreatureTreeClockTests.cs:51`, `Features/MissionPerf/MissionTickProfilerBindingTests.cs:160`.
  That is 13 full TAOM walks. Engine-assembly walks: `Features/Enlistment/Patch66EnlistmentBindingTests.cs:75,166,169,197`
  (the file's own comments say TaleWorlds.CampaignSystem each time), `Migration/FarWatchEngineFactsTests.cs:87`,
  `Features/MapViewRelease/MapViewReleaseBindingTests.cs:110`, `Features/NameplateCull/NameplateCullBindingTests.cs:372`.
  Related: `Infrastructure/ServiceSignatureGateTests.cs:50` and `:63` each run `Scan(TaomAssemblies.All)` (a full type and
  member reflection pass) in two tests of one class.
- **Impact**: each walk decodes the IL of every method and resolves every call token. TAOM is 4,342 Main files plus 268 Client files
  (`git ls-files` count), so a walk is likely seconds. The 13 repeats are redundant: the callee set of a method does not change
  between tests. Per-walk time: UNVERIFIED (no timings in the log).
- **Effort**: M.
- **Risk**: LOW to MED. The index must keep `scanned` and `unreadable` semantics, because the callers assert them
  (`scanned > 0`, `unreadable.Count == 0`); engine-assembly indexes are big, so cache only the TAOM assemblies and leave the
  engine scans alone.
- **Confidence**: HIGH on the repetition (code read); MED on the gain (UNVERIFIED).
- **Fix sketch**: In `TaomAssemblies`, hold a `Lazy<Dictionary<MethodBase, MethodBase[]>>` per TAOM assembly (the result of
  `ExtractCalledMethods` per method, plus the `unreadable` list and the `scanned` count) and make `TaomAssemblies.FindCallers` and a
  new `IlCallScanner.FindCallers(IndexedAssembly, ...)` filter that index with `isBanned`. Same for the `Scan` result in
  `ServiceSignatureGateTests` (a private `Lazy`). Measure first: time one `TaomAssemblies.FindCallers(m => false, ...)`.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, after TST-01 (same shape, own plan).
- **Issue**: none known.

---

### [TST-03] Stop parsing 31 MB of translation XML in tests that look for characters and items

- **Evidence**: `Features/EyeOfSauron/HuntTroopListsDataTests.cs:27-31`, `Features/FireAndTribute/FireAndTributeDataTests.cs:70`
  (both `XDocument.Load` every `*.xml` under `Main/_Module/ModuleData` recursively to find `NPCCharacter`), and
  `Features/NamedCompanions/NamedCompanionShippedDataTests.cs:83-85` (`File.ReadAllText` plus a regex over the same tree to find `<Item id=`).
  Tracked ModuleData XML is 51.9 MB in 457 files, of which the 289 files under `Languages/` are 31.4 MB
  (`git ls-files 'Main/_Module/ModuleData/*.xml' | xargs cat | wc -c` and the same for `Languages/*`). No file under `Languages/`
  holds `<NPCCharacter` or `<Item id=` (`git grep -l '<NPCCharacter' -- Languages | wc -l` prints 0, same for `<Item id=`),
  so excluding that folder returns the same set.
  Behind this: 66 test files carry their own `Descendants("NPCCharacter")` loader (`git grep -lE 'Descendants\("NPCCharacter"\)'`),
  and the two `LoadCharacters` bodies above are identical line for line.
- **Impact**: 60 percent of the bytes these three tests read are irrelevant. Two of them parse that into an `XDocument` DOM. The
  third (NamedCompanions) reads all of it only after an early `return` when `itemIds.Count == 0` (`:79`), which is the case
  today, so it is currently free (see TST-07).
- **Effort**: S for the exclusion; M for a shared `ShippedCharacters` lazy loader in `TAOM.Tests/Core/` that the 66 files could adopt over time.
- **Risk**: LOW. Same elements returned; add `Where(f => !f.Contains("Languages"))` or enumerate the non-Language folders.
- **Confidence**: HIGH.
- **Fix sketch**: Step 1 only: add `ModuleDataXml()` to `RepoPaths` (all `*.xml` under ModuleData except the `Languages` folder, cached with TST-01),
  use it in the two `LoadCharacters` and the NamedCompanions scan, and make the second `LoadCharacters` call the first. Defer the
  66-file adoption.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (small; bundle with TST-01 or TST-06).
- **Issue**: none known.

---

### [TST-04] Compile each shipped XSLT once, not once per test

- **Evidence**: `new XslCompiledTransform()` appears 15 times in 15 files; the stylesheets are large: `Main/_Module/ModuleData/lords.xslt` is
  1,156,868 bytes, `heroes.xslt` 177,579, `spclans.xslt` 107,584, `spcultures.xslt` 100,057
  (`wc -c`). `Core/CulturePartyTemplateTests.cs:337-345` compiles `spcultures.xslt` inside `TransformSentinelStub()`, called per test
  (5 matches of the name, 4 calls); `Features/NazgulFamily/NazgulRaceDataTests.cs:43-55` compiles the 1.1 MB `lords.xslt` inside its
  `TransformSentinelStub()`, called by 2 tests; `Core/CultureExecutionerTests.cs:97-98` compiles per test method;
  `Core/NotableTemplateGenderTests.cs:157`, `Core/CustomBattleTroopFilterTests.cs:26`, `Features/Diplomacy/ShippedKingdomDataTests.cs:122`,
  `Features/CultureConversion/GarrisonCultureCoverageTests.cs:247`, `Core/LordFamilyTransformTests.cs:70`. The file names are
  spread over 13 tests for `lords.xslt` (`git grep -n '"lords.xslt"'`), 12 for `spcultures.xslt`, 11 for `spkingdoms.xslt`.
- **Impact**: `XslCompiledTransform.Load` generates and JIT-compiles IL for the whole stylesheet; for a 1.1 MB stylesheet that is
  plausibly a second or more per compile. Number of compiles of `lords.xslt` per run: at least 6 (NazgulRaceData x2, LordFamily, VanillaWanderer,
  CustomBattleLordStubs, ShippedClanSide). Per-compile time: UNVERIFIED. Several of these classes are `LiveInstall` (they need the game's
  vanilla XML), so the saving is on the desktop and in `/verify`, not in a no-game run.
- **Effort**: S.
- **Risk**: LOW. `XslCompiledTransform` is thread-safe for `Transform` after `Load`; tests do not run in parallel.
- **Confidence**: MED (the count is read; the time is not measured).
- **Fix sketch**: Add `ShippedXslt.Compiled(string fileName)` to `Core/CultureDataFixture.cs` (already shared by 48 files): a
  `ConcurrentDictionary<string, Lazy<XslCompiledTransform>>` using the `XmlReaderSettings` that `LordFamilyTransformTests.Transform`
  already uses. Replace the per-test `new XslCompiledTransform()` and `Load` lines. Measure first with a Stopwatch around one `Load("lords.xslt")`.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes (small).
- **Issue**: none known.

---

### [TST-05] Source-text pins make every extraction or composition move fail dozens of tests

- **Evidence**: Count of exact-code pins: `git grep -cE 'StringAssert\.Contains\([^,]+, *"[^"]*[;(){][^"]*"\)' -- .` sums to 1,056 assertions in 205 files
  (heuristic: a quoted literal containing `;`, `(`, `{` or `)` as the needle). Worst files:
  `Features/MarshalsWord/MarshalHoldLineWiringTests.cs` (63; for example `:71` pins
  `"if (now < Volatile.Read(ref _nextHoldRead)) return; Volatile.Write(ref _nextHoldRead, now + MarshalTuning.HoldReadSeconds);"`, `:229` pins a
  whole `var company = ... ;` statement), `Features/MarshalsWord/MarshalMarkCaptureAndShotTableTests.cs` (47),
  `Features/CreatureBandits/CreatureBanditsWiringTests.cs` (32), `Features/MarshalsWord/MarshalAdapterPinTests.cs` (27),
  `Features/NotableServices/NotableServicesWiringTests.cs` (26), `Features/InnRounds/InnRoundsWiringTests.cs` (26),
  `Features/MarshalsWord/MarshalThrowerPatchTests.cs:455-461` (flattens whitespace, then pins one statement down to the argument list).
  Separately, `Main/SubModule.cs` and `Main/IoC.cs` text is read by 163 `ReadSource` sites in 91 test files
  (`git grep -nE 'ReadSource\("Main/(SubModule|IoC)\.cs"' -- . | wc -l` prints 163; unique files 91). 23 of them are `IsFalse(... .Contains("X"))`
  (a "not hand-wired" check that survives a move to `FeatureModules.cs`); the rest bind a positive shape of the hand wiring.
- **Impact**: the run's lane arch will plan extractions (RefugeService, Composition migration, Kimi leads 1 and 2). A rename of
  a field, a re-ordering of arguments or a move of a statement to a neighbour class fails the pins above with no behaviour change.
  The executors will spend a cycle each updating literals; the cost is paid per refactor, not once. The pins that read
  `SubModule.cs` and `IoC.cs` are the direct blocker for the composition migration (Kimi lead 1).
- **Effort**: M for the composition seam; L to rewrite the 1,056 pins (not recommended).
- **Risk**: MED. Loosening a wiring pin can let a real regression through; keep each pin's intent.
- **Confidence**: HIGH on counts and the examples; the heuristic over-counts a few `Assert.IsTrue` lines, which I did not include in 1,056.
- **Fix sketch**: Do not rewrite the 1,056. Do (1) a `RepoPaths.ReadComposition()` that returns SubModule, IoC and
  `Main/Composition/FeatureModules.cs` joined, and point the positive `SubModule`/`IoC` pins at it, in the same plan as the composition move; (2) put the list of
  the five worst files into the plan briefs of any refactor that touches `MarshalsWord`, `CreatureBandits`, `NotableServices` or `InnRounds`, so the executor
  expects to edit them. Leave the rest.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes for (1) only when the composition plan is chosen; (2) is a note for the orchestrator.
- **Issue**: none known.

---

### [TST-06] One repo-root locator, one string-row reader, one safe `GetTypes`

- **Evidence**:
  - Repo-root locators other than `RepoPaths`: 71 of the 81 test files that use `AppDomain.CurrentDomain.BaseDirectory` do not mention `RepoPath`
    (`git grep -lF BaseDirectory -- . | xargs grep -LF RepoPath | wc -l`); 60 of them point at `_Module`. They hard-code `..\..\..\..` from the bin folder, for example
    `Features/CareerSystem/CareerChoiceIntegrityTests.cs:30-32` and `Infrastructure/Localization/LanguageDataXmlTests.cs:13-15, 20-22`. `RepoPaths.cs:8-10` says
    its compile-time path exists so "a test ... does not depend on the test assembly's output layout". In addition 20 copies of
    `private static string FindRepoRoot()` (`git grep -n 'static string FindRepoRoot()'`, e.g. `Core/CharacterFaceCoverageTests.cs:35`) and 6 of `RepoRoot()`.
  - The same `Rows(path, key)` body (regex `<string\s+id="KEY"\s+text="..."` plus `WebUtility.HtmlDecode`) written five times:
    `Features/BondOfHire/BondOfHireWordingTests.cs:38`, `Features/LongMemory/LongMemoryWordingTests.cs:37`,
    `Features/ManyHammers/ManyHammersWordingTests.cs:38`, `Features/GoodDeeds/GoodDeedsWordingTests.cs:31` (key fixed),
    `Features/MarshalsWord/MarshalWordingTests.cs:79`; `Features/SmithsMeasure/SmithsMeasureWordingTests.cs:47` and
    `Features/TokensOfEsteem/TokensOfEsteemWiringTests.cs:274` are near copies.
  - Hand-rolled `try { GetTypes() } catch (ReflectionTypeLoadException ...)`: 24 copies (`git grep -cE 'catch \(ReflectionTypeLoadException' -- .` sums to 24),
    for example `Infrastructure/ServiceSignatureGateTests.cs:196`, `Infrastructure/PrefabInsertPositionTests.cs:59`, `Migration/GameModelOverrideBindingTests.cs:201`;
    `Migration/IlCallScanner.cs:72-81` has the canonical form.
  - Seven recording loggers implementing `IModLogger` by hand: `Features/AnimationVariety/AnimVarietyServiceTests.cs:25`,
    `Features/AnimationVariety/GaitVarietyServiceTests.cs:21`, `Features/LoadTimeStamps/RecordingLogger.cs:12`,
    `Features/LocalizationOverride/OverrideLanguageGateTests.cs:230`, `Features/SeatOfSeeing/ListLogger.cs:7`, and two in FordsOfWar (skip list).
- **Impact**: readability and maintenance, and the fixed-depth paths break if the test output folder moves (for example `-o` or a Release layout change).
  Not a runtime cost, so it ranks below TST-01 to 04.
- **Effort**: M (mechanical, many files; do it in slices by folder).
- **Risk**: LOW. Each replace keeps the file read; run the filtered tests per slice.
- **Confidence**: HIGH on counts.
- **Fix sketch**: Add to `RepoPaths`: `ModuleData(params string[] parts)` and `StringRows(path, key)`; add `TaomAssemblies.TypesOf(Assembly)` (the safe `GetTypes`,
  cached per assembly, which also helps TST-02); add one `RecordingModLogger` to `Infrastructure/`. Convert in slices, first the `Rows` copies (5 files) and the 20
  `FindRepoRoot` copies; leave the 71 `BaseDirectory` files for a later plan.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes for the first slice (Rows, FindRepoRoot, safe `GetTypes`); later slices optional.
- **Issue**: none known.

---

### [TST-07] Repo-data checks that skip instead of failing, and one early return that passes

- **Evidence**: 47 `Assert.Inconclusive` lines say a repo file or folder "not found" or "run from repo root" (filter:
  `git grep -n 'Assert.Inconclusive' -- . | grep -i 'not found\|could not locate\|run from' | grep -vi 'bannerlord\|install\|game\b\|live\|armory'`),
  in 19 files. Examples: `Core/ConfigIdValidationTests.cs:57,86,104,108,135,139,193` (7), `Features/TroopProgression/VolunteerRecruitmentServiceTests.cs:1961,2001,2054,2103,2147,2233,2277,2953`
  ("Could not locate ... gondor.json relative to test bin"), `Features/MonsterSize/MonsterSizeWiringTests.cs:25,48` ("Main/IoC.cs not found: run from the repo root"),
  `Features/SettlementGuards/SettlementGuardsWiringTests.cs:63,86`, `Features/Elk/ElkConfigTests.cs:103`. `RepoPaths.ReadSource` already fails on a missing file, and its
  comment (`RepoPaths.cs:41-45`) gives the reason: a gate that goes Inconclusive when it cannot find its file stops guarding. Also
  `Features/NamedCompanions/NamedCompanionShippedDataTests.cs:79`: `if (itemIds.Count == 0) return;` reports green when the shipped data has no item costs
  (the comment says it is intentional until one is added).
- **Impact**: if the checkout layout or the bin depth changes, these tests report skipped and the run still ends green. Not a problem on this desktop today (the baseline's
  9 skips are all opt-in), so the risk is in future layouts and tooling that moves `bin`.
- **Effort**: S.
- **Risk**: LOW. Replace the walkers with `RepoPaths.RepoPath(...)` and `Assert.IsTrue(File.Exists(...))`; a test that was silently skipped on a partial checkout (the laptop, orientation.md "Two machines") would fail there, so keep Inconclusive only for the game-install and Armory checks.
- **Confidence**: HIGH.
- **Fix sketch**: Do it together with TST-06 for the same files. Leave the `NamedCompanions` early return as it is (its comment states the intent); it is listed so nobody counts that test as live coverage.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: yes, folded into the TST-06 first slice (same files).
- **Issue**: none known.

---

### [TST-08] RefAsm no-game unit step is still not in `/verify`

- **Evidence**: `plans/_audit/2026-10-28-nogame-tests/DECISIONS.md` row 4 ("Plan 044 Step 0: add the RefAsm unit step to `/verify`?") shows Answer `open`, waiting on the maintainer.
  `.ai/verification.md` lists the three-command no-game sequence but `/verify` does not run it. The 450 `Assert.Inconclusive` sites and the `RequiresGame` tags only matter for that path.
- **Impact**: the no-game replay is how a contributor without the game sees the suite; without the step in `/verify` an untagged engine test reaches the trunk and is
  found by a contributor later.
- **Effort**: S.
- **Risk**: LOW (a temp worktree per the decision note).
- **Confidence**: MED (I did not read `/verify`; I only read the decisions file and `.ai/verification.md`).
- **Fix sketch**: Needs the maintainer's answer to row 4; not an executor item.
- **Delta**: pre-existing.
- **P1**: no.
- **Plan candidate**: no (decision owed; open plan 044 covers it).
- **Issue**: #1038 per the BRIEF (plans 043 and 044 are open with issues #739 and #1038); I did not confirm which number belongs to 044.

---

## Considered and rejected

- **Sleeps and spins in tests**: 35 `Thread.Sleep` / `Task.Delay` sites, literal sum 429 ms (`git grep -nE 'Thread\.Sleep\(|Task\.Delay\(' -- . | wc -l`), 15 of them in `Features/MissionPerf/HitchProbeHooksTests.cs`. Under half a second in total. Not worth doing.
- **Assembly-level `[assembly: Parallelize]`**: none set (`git grep -n 'Parallelize' -- .` prints nothing), but 32 test files assign process-wide statics (IoC, hook `Profiler` fields) and 294 classes have `[TestCleanup]` resets. Parallel classes would race on that state. High risk for an unmeasured gain on a 112 s suite. Rejected.
- **Project file and runsettings**: `TAOM.Tests.csproj` has no setting that slows the run (no coverlet collection unless `--collect`; `Private=True` copies 51 TaleWorlds DLLs, 30 MB, at build only). `binding-gate.runsettings` is deliberate and pinned by `BindingGateRunSettingsTests`. No finding.
- **Tests with no assertion token**: a brace-matching scan (`scratch/lane-tests/scan_nothing.py`) flagged 231 of 32,287 methods, but the scan mis-reads `[DataRow]` lines and helper-named asserts (`Prop(...)`, `Method(...)` in `CreatureSiegeRoleBindingTests.cs:74`), and the samples I read (`BehaviorTreeBuilderTests.cs:194`, a does-not-throw test) are intended. I found no confirmed assertion-free test. Not reported.
- **`Assert.IsTrue(x >= 0)` lines**: nine sites, all `IndexOf` found-checks before a `Substring`; fine.
- **`BannedConstructsGateTests`**: the directive scan has planted-violation tests (`:56-72`) and a four-assembly reach test, so it is not a gate of prohibitions alone. No finding.
- **Wording tests reading 12 language files per row** (`Features/GoodDeeds/GoodDeedsWordingTests.cs:55-70` etc.): language files average 108 KB (31.4 MB over 289 files), so the cost is small; the duplication is in TST-06.

## What I did not cover

- **Timing.** I did not run dotnet and found no `.trx` or per-test timings in the repo or the baseline log. Every saving above is an estimate from counts and one Python read timing; each fix sketch starts with the Stopwatch measurement that would confirm it. The suite wall time (1 m 52 s) is from the orchestrator's baseline log.
- **Files read in full:** `Infrastructure/RepoPaths.cs`, `TaomAssemblies.cs`, `Migration/IlCallScanner.cs` (lines 1-200), the head of `BannedConstructsGateTests.cs` and `ServiceSignatureGateTests.cs`, `binding-gate.runsettings`, the csproj, `Directory.Build.props` (first 80 lines), `GameReferences.targets` (first 80 lines). Other files named were read at the cited lines only.
- **Not reached:** the 39,000 individual test bodies. I sampled; `Migration/` binding tests, `Adapters/`, `BehaviorTreeWrapper/` and `SceneScripts/` were only counted, not read. I did not look for NSubstitute setups that test the mocks, nor for time or order dependence beyond the sleep count.
- **Expected sets derived from the artefact under test (checklist item 3).** I did not find a confirmed case. I read `ServiceSignatureGateTests` (baseline is a shrink-only file with a hard `MaxBaselineEntries` count, which is sound) and `EntryPointLineCountGateTests` (partly). `ServerLoadGateTests`, `ServerJitGateTests`, `SilentCatchRatchetTests` baselines and the Migration engine-mirror tests were not read.
- **Mirror tables and per-branch dispatch (playbook 4).** Not audited.
- **`TAOM.CoopCompat.Tests`** and the Python tool tests: out of scope or skip list. The skip-list test folders (HeroDefence, FieldBallista, CoopInterop, FordsOfWar, LongDefeat) were not audited except as pattern evidence.
- **Open issues.** I made no `gh` call, so each "Issue: none known" means none seen in the files I read.
- **Test-count claim.** "30,725" is the sum of `git grep -c '\[TestMethod'` hits (lines, counting `[DataTestMethod]`-free lines only); the baseline log's 39,259 includes data rows.
