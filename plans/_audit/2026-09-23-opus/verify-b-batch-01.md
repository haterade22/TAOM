# Verify batch B-01: adversarial re-check of TEST-L5-02, TEST-L5-03, TEST-L5-04

Checker, run `2026-09-23-opus`, baseline `b2e387db` (every source read here is `git show b2e387db:<path>`
or `git grep ... b2e387db`, because the working tree HEAD is `4b5662b2` and carries another session's
edits). No build or test was run. Stored probe outputs under the session scratchpad `lane5-probe\` were
re-read (logs and trx), not re-executed.

## TEST-L5-02: the binding gate resolves the game only from the test process environment

- **Outcome**: CONFIRMED (mechanism); impact today LOW.
- **Re-read**:
  - `TAOM.Tests/Migration/GameAssemblies.cs:111-122`: `ResolveGameDir` reads only
    `Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR")` then `("BANNERLORD_GAME_DIR")`,
    else null. `EnsureLoaded` (`:36-48`) returns false on null, and callers go Inconclusive. As cited.
  - `Directory.Build.props:37-38`: `GameFolder` is built from `$(BANNERLORD_OVERRIDE_DIR)` /
    `$(BANNERLORD_GAME_DIR)`, MSBuild properties, which an env var or a `-p:` global property both
    satisfy. As cited.
  - `TAOM.Tests/TAOM.Tests.csproj:34-39`: the TaleWorlds references are `$(GameFolder)\bin\...`, so a
    test DLL that compiled had a resolved `GameFolder`. As cited.
  - The doc comment `GameAssemblies.cs:19-21` claims the resolution "mirrors Directory.Build.props";
    it mirrors the env-var half only.
- **Measurement re-read (not re-run)**: `lane5-probe\f1.log` `Failed: 14, Passed: 10222, Skipped: 3`;
  `f2.log` `Failed: 23, Passed: 9853, Skipped: 363`; `f3.log` `Passed!  - Failed: 0, Passed: 33,
  Skipped: 335`. A per-result diff of `f1_layout_envset.trx` vs `f2_layout_envunset.trx`
  (scratchpad `vb01_skipdiff.py`) finds exactly 360 results moved to NotExecuted, every one with a
  `Game assemblies not loaded/unavailable` or `Game dir unresolved` Inconclusive message (350 + 5 + 3
  + 1, plus 1 `SandBox.dll not available`), across 72 classes. The 360 reproduces. Unmentioned by the
  lane: the same unset variable also adds 9 hard failures (`ConsoleCommandBindingTests` x5,
  `LiveTableauRefTests` x3 in Setup, one `Patch86HideoutBossFightBindingTests`), so the full-suite F2
  run is red anyway; only a filtered run (F3) goes green.
- **Refutation attempts that failed**: no runsettings, `AssemblyInitialize` or props file sets the
  variable for the test host (`git grep BANNERLORD_GAME_DIR b2e387db` outside docs: only
  `GameAssemblies.cs`, 9 live-Armory test files, `build.ps1`, `setup-dev-env.ps1`, tools, CI).
  Not by-design: the Inconclusive-when-no-game behaviour is documented (`GameAssemblies.cs:20-21`,
  "e.g. CI without a game install"), but the case here is a build that had the game and a test host
  that cannot find it; nothing in `docs/adrs`, `.claude/rules` or the orientation trap index decides
  that.
- **Why impact is LOW today**: every concrete trigger in the finding is hypothetical at `b2e387db`.
  `setup-dev-env.ps1:20` writes the variable at User scope and `build.ps1:11` refuses to run without
  it, so desktop shells and IDEs inherit it. The one workflow sets it at job level
  (`.github/workflows/build.yml:260-261`), so its Test step (`:277-278`) sees it; the finding's
  "a workflow that sets it only on the build step" does not describe the current file. No doc or skill
  uses `-p:BANNERLORD_GAME_DIR` (`git grep -E "p:BANNERLORD|p:GameFolder" b2e387db`: no hit). Its
  value is as a cheap, additive hardening that also makes TEST-L5-01's red run rarer and gives CI a
  seam for a reference-assembly folder.
- **Corrected evidence**: as cited; add `.github/workflows/build.yml:260-261` (job-level env, so the
  CI trigger is prospective) and the 9 extra F2 failures above.
- **Fix-sketch check**: sound. SDK projects generate `AssemblyInfo` by default and neither
  `TAOM.Tests.csproj` nor `Directory.Build.props` disables it, so an `AssemblyAttribute` item carrying
  `$(GameFolder)` compiles in. As sketched (metadata read after the two variables) it only helps when
  both are unset; it does not catch the reverse mismatch (built against `BANNERLORD_OVERRIDE_DIR`,
  tested with only `BANNERLORD_GAME_DIR` set, so module DLLs load from a different engine than the
  copied `TaleWorlds.*.dll`). Reading the metadata first, or warning when it differs from the
  environment, would cover that too.

## TEST-L5-03: collapse the source-scraper locators onto `RepoPaths`

- **Outcome**: CONFIRMED, with three count corrections; impact today LOW (maintenance and
  gate-integrity hygiene, no shipped-code effect).
- **Re-read, holds as cited**:
  - Scraper set: `git grep -l -E '\.cs"|"\*\.cs"|\.csproj"|\.props"|EndsWith\("\.cs"\)' b2e387db --
    TAOM.Tests` returns 49 paths = `TAOM.Tests.csproj` + 48 test files; drop
    `Migration/ReflectionSiteBindingTests.cs` (label only) and 47 remain. The same grep at `141b749`
    returns 10 test files, all still present, so 38 of 48 are new. Delta "introduced" holds.
  - 28 `FindRepoRoot` definitions in 28 files (`git grep -E "(static|private|internal|public).*\bFindRepoRoot\s*\("`,
    29 lines, one of them a call on `GarrisonCultureCoverageTests.cs:68`).
  - Nine `ReadProjectSource*` copies at exactly the cited lines (`FiefHubCampaignBehaviorTests.cs:143`,
    `RacePersistenceBehaviorTests.cs:119`, `MessengerCampaignBehaviorTests.cs:125`,
    `MountDespawnWiringTests.cs:73`, `SettlementGuardsWiringTests.cs:133`, `SiegeDismountWiringTests.cs:81`,
    `SiegePropDiagnosticsWiringTests.cs:51`, `EnlistmentDiagnosticsGateTests.cs:489`,
    `AutoResolveDiagnosticsWiringTests.cs:120`). Eight callers go `Assert.Inconclusive` on null (e.g.
    `MountDespawnWiringTests.cs:24-25,36-37`); only `EnlistmentDiagnosticsGateTests.cs:385-386` fails.
    Eight of the nine key off `Directory.GetCurrentDirectory()`, `AutoResolveDiagnosticsWiringTests`
    off `BaseDirectory`. As cited.
  - `.git`-keyed trio at `ExitStallDisarmTests.cs:120`, `FieldDutyRuntimeTests.cs:393`,
    `TaomCulturalFeatsDefinitionTests.cs:361`: all three walk `BaseDirectory` up to `.git` and throw.
    Their own comments record one earlier break of this locator in worktrees (`f1bc6b39`), which is
    the drift-by-copy the finding describes. In the stored F1 trx (no `.git` in `fake\`) all three
    fail, plus a fourth `.git`-keyed file the lane did not list
    (`Features/Enlistment/EnlistmentEquipmentCultureTests.cs`, a data reader, not a scraper).
  - `CoopVetoClassificationTests.cs:304-312` holds the length-preserving `StripComments` helper.
  - Other cited locators re-read: `AiPartySizeOrderingTests.cs:21-27` (cwd to `TAOM.sln`, throws),
    `FieldCampWiringTests.cs:24-25` and `SignatureStrikesBindingTests.cs:32-33` (fixed four-level),
    `SignatureStrikesBindingTests.cs:207-208` and `GameModelOverrideBindingTests.cs:56-57,190-198`
    (null then Inconclusive), `SettlementFollowingTests.cs:471` (relative string against cwd).
  - Run A re-read from `probeA_envset.trx`: 485 failed, 63 skipped. Diffed per result against F1: 471
    new failures, dominated by `file/dir not found` (224) and `TAOM.sln not found` (119); the rest are
    missing ModuleData, prefab and language files located by the same bin-relative walks, plus
    config-missing fallbacks (`Expected:<Free>. Actual:<Neutral>`). 60 new skips (36
    `VolunteerRecruitmentServiceTests` rows, the rest the Inconclusive-on-missing-source files).
    "All locator-driven" holds, though a large share is data-file locators, not the 47 scrapers.
- **Corrections**:
  - `RepoPaths` has **4** users at `b2e387db`, not 3: `EnlistmentDiagnosticsSettingsProviderTests.cs:4`,
    `FieldCommissionConfigProviderTests.cs:11`, `FieldCommissionSettingsProviderTests.cs:7` (all
    `using static`) and `RaceAge/ShippedFertilityConfigTests.cs:40`. All four passed in run A (5, 18,
    25 and 7 results).
  - Four-level hard-coded depth: **51** files, not 37 (a Python scan over all 753 files at
    `b2e387db`, scratchpad `vb01_locators.py`, matching `"..", "..", "..", ".."` or any
    `..\..\..\..` / `../../../..` form). cwd-keyed: 55, not 56. `BaseDirectory` or
    `Assembly.Location`: 62 (holds). `Assert.Inconclusive` files: 96 (holds).
  - `RepoPaths.cs:14-21` cited for the helper: `RepoPath` is `:14-19`, the `[CallerFilePath]`
    `ThisFile` is `:21`. Same file, fine.
- **Refutation attempts that failed**: nothing in `docs/adrs`, `.claude/rules`, `.ai` or the
  orientation trap index decides on per-file locators. The opposite: two RCAs already flag the copies
  as a defect and name `RepoPaths` as the fix (`docs/reviews/rca-field-commission-races-2026-09-17.md`
  row 3; `docs/reviews/rca-race-fertility-2026-09-19.md` row 2, which leaves the
  `CultureRaceConsistencyTests` swap as FOLLOW-UP). No test already pins "no private repo locator".
- **Fix-sketch caveat**: `RepoPaths` is layout-proof only because `TAOM.Tests.csproj` has no
  `PathMap`. `Main/TAOM.csproj:6` and `Dependencies/TAOM.Dependencies.csproj:6` already set
  `<PathMap>$(MSBuildProjectDirectory)=/_/</PathMap>`; if that is ever hoisted into
  `Directory.Build.props` (or `ContinuousIntegrationBuild` is turned on), `[CallerFilePath]` becomes
  `/_/...` and every `RepoPaths` user breaks. With the sketch's `Assert.Fail` that break is loud, not
  silent, so it is a note for the plan, not a blocker. Impact today is LOW: the desktop, worktrees and
  a GitHub-hosted checkout all resolve the current locators (lane Measurement 2); the silent-skip path
  needs a filtered run from a bin outside the repo (run C1).

## TEST-L5-04: replace the source scrapes with IL tests and a Roslyn analyzer, family by family

- **Outcome**: CONFIRMED as a diagnosis and direction item; the family table over-assigns examples to
  the IL route, so the "each IL conversion is S" estimate is optimistic. Impact today LOW (test
  quality; no shipped-code effect beyond the comment-shaped hole COMP-03 already owns).
- **Re-read, holds**:
  - Comment blindness: 8 of the 47 scrapers carry any comment handling (`StartsWith("//")`,
    `StripComments`, block-comment regex: `CoopVetoClassificationTests`, `FieldDutyRuntimeTests`,
    `EnlistmentAttachedToPolicyTests`, `EnlistmentDiagnosticsGateTests`, `SettlementLookupBindingTests`,
    `FieldCampWiringTests`, `MessengerCampaignBehaviorTests`, `RefugeWiringTests`); the other 39 match
    raw text. The false pass is real: `GameModelOverrideBindingTests.cs:59-61` accepts
    `subModule.Contains($"new {m.Name}(")`, and the only `new TaomPartyNavigationModel(` in
    `git show b2e387db:Main/SubModule.cs` is the comment at line 1044. (NavalTravel parking is
    by-design; here it only proves the mechanism.)
  - Indentation pin: `SettlementFollowingTests.cs:471-473` reads `"../../../../Main/Adapters/CommanderLordAdapter.cs"`
    against cwd and cuts at `IndexOf("        }")` (eight spaces). As cited.
  - `IlCallScanner` (`TAOM.Tests/Migration/IlCallScanner.cs:18-166`) walks raw IL of every method and
    constructor of every type, nested compiler-generated types included, and yields InlineMethod
    operands only (call, callvirt, newobj, ldftn, ldvirtftn, jmp). `git grep -l "IlCallScanner\."
    b2e387db -- TAOM.Tests`: 19 files. As cited.
  - Text-match volume: my count over the 47 files (`StringAssert.(Contains|DoesNotMatch|Matches)`,
    `.Contains("`, `IndexOf("`, `Regex.*`) is 263 against the lane's 254; same order, regex detail.
  - Analyzer feasibility: `Main/TAOM.csproj:112` already consumes a netstandard Roslyn analyzer
    (`BUTR.Harmony.Analyzer`), so "net472 SDK-style plus a netstandard2.0 analyzer" is proven in this
    repo, not only in principle. No `TreatWarningsAsErrors` anywhere, so a warning-level start does not
    block builds.
  - Not by-design: no ADR or rule endorses source scrapes; `docs/reviews/lessons/build-tooling-workflow.md:1549`
    ("a source-text scan only pins the idiom") and `.claude/rules/tests.md:73-75` (assert on output,
    not source markup) point the same way as the finding. One rule codifies a scrape's spelling:
    `.claude/rules/gamemodels.md:40` (rule 7) tells authors to write `new TaomXxxModel(` unqualified
    because `GameModelOverrideBindingTests` greps for it; any replacement must update that rule too.
- **Corrections to the family table** ("Type or method X must not reference Y" via `IlCallScanner`):
  several cited examples are not call-shaped, so a call ban cannot replace them as written:
  - `FieldDutyRuntimeTests.cs:375-380` bans `EnlistedDetachedOnDuty`, which is an enum member
    (`Main/Features/Enlistment/Domain/EnlistmentState.cs:30`, `= 4`): it compiles to `ldc.i4.4` and is
    invisible to a scanner that reads InlineMethod operands only. `IServiceAttachmentService` and
    `IDutyWorldAdapter` are only seen if a member of them is called.
  - `EnlistmentAttachedToPolicyTests.cs:44-47` permits `AttachedTo = null` and bans any other
    assignment; a ban on `set_AttachedTo` would flag the legal clear unless the scanner also checks
    for a preceding `ldnull` (not in the helper today). `:69-74` pins a guard condition
    (`main.Army.LeaderParty != main`), which no call ban expresses.
  - `ArmyMembershipBindingTests.cs:131-138` is a positive pin (must set `AiBehaviorObject`, must call
    `SeedBehaviorObject`), and `:189` bans the identifier text `others`, a heuristic no IL test can
    reproduce.
  - `HoNFormationPresetSerializationTests.cs:149-160` distinguishes calls to the same method
    `ConstructContainerDefinition` by their `typeof(...)` argument (an `ldtoken`), which the scanner
    skips; a behavioural test of the definer fits better.
  - Clean fits: `SettlementLookupBindingTests.cs:37-43` (ban `CampaignObjectManager.Find<Settlement>`
    across the Adapters namespace; note `SameMethod` matches by name and declaring type, so it would
    also catch every other `Find<T>` unless the generic argument is checked) and the
    `hero.CurrentSettlement` half of `SettlementFollowingTests.cs:480`.
  - A per-method IL test misses calls inside that method's lambdas and local functions (compiled into
    closure types); the assembly-wide bans avoid this, a "named method's body" test must follow them.
  - 13 of the 19 current `IlCallScanner` users gate on `GameAssemblies.EnsureLoaded()` and go
    Inconclusive without the game (e.g. `PartyOwnerGetterBanTests.cs:34,39-40`), while the scrapes they
    would replace need no game. Converting in that style moves tests into the TEST-L5-01/02 skip
    family; the plan should either not gate IL tests that touch only TAOM and bin-copied TaleWorlds
    types, or land after TEST-L5-01.
- **Minor line corrections**: `PartyOwnerGetterBanTests.cs:44-48` is the `FindCallers` call at
  `:45-49`; the discovery floor is `:51-52` and the unreadable-bodies report `:63-67`. "They only see
  the file they name" has exceptions (`SettlementLookupBindingTests` enumerates all of `Main/Adapters`;
  `CoopVetoClassificationTests` and `ResetForUnloadSweepTests` walk `Main`). "All 50" GameModels:
  `git grep -E "class\s+Taom\w*Model\s*:\s*\w+" b2e387db -- Main` finds 50 classes, 42 deriving
  directly from `Default*Model`; close enough.

## What I did not cover

- No test was re-run: every run number (F1 to F4, C1, A) comes from the stored `lane5-probe` logs and
  trx files, re-parsed here, not re-executed. The trx files could in principle predate a later change
  to the probe bin; I did not re-hash the bin against the worktree build.
- I did not verify the Roslyn-analyzer specifics beyond what the repo shows (RS0030 per-project
  scoping, `.editorconfig` per-folder severity as an alternative, analyzer-vs-compiler version
  pinning under SDK 10.0.401): UNVERIFIED general tooling claims, not load-bearing for the verdicts.
- The "call order" and "data kept in code" rows of the TEST-L5-04 table were spot-checked only for
  `AiPartySizeOrderingTests.cs:37-52` against `TaomPartySizeModel.cs:45-74` and
  `DisplayFrameSourceTests.cs:46-63`; the other named files in those rows were not opened.
- Lane 1's COMP-03 was read only as far as its false-pass mechanism (one grep of `SubModule.cs`
  line 1044 and `GameModelOverrideBindingTests.cs:59-61`); its 26-file table was not re-derived
  (my grep for `SubModule.cs"`/`IoC.cs"` literals finds 27 files, one of which only names it).
