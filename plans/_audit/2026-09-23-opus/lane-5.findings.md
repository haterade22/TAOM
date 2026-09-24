# Lane 5: test effectiveness and CI (run `2026-09-23-opus`, baseline `b2e387db`)

Read-only auditor. Tests were run only with `--no-build` against the orchestrator's existing build in
the detached worktree `scratchpad\wt-opus-head` (at `b2e387db`), or from a copy of its test output in
`scratchpad\lane5-probe\`. Nothing was built; nothing inside the repo was written except this file.
Measurements use `git grep ... b2e387db -- TAOM.Tests` (committed tree, so another session's dirty
test files do not enter the counts) unless stated otherwise.

## Probe setup (all reproducible from the scratchpad)

- `scratchpad\lane5\TAOM.Tests\` is `git archive b2e387db TAOM.Tests`; `lane5\scrape.py` classifies it.
- `scratchpad\lane5-probe\out\` is a byte copy of the worktree's `TAOM.Tests\bin\Debug\net472` (90 files).
- `scratchpad\lane5-probe\fake\` is `git archive b2e387db` of `TAOM.sln CLAUDE.md AGENTS.md Main
  Dependencies docs TAOM.Tests .claude/pinned-game-version.txt` minus binary media (92 MB), with the
  same test bin placed at `fake\TAOM.Tests\bin\Debug\net472`. It reproduces the baseline: 10,222
  passed, 14 failed, 3 not executed. The failures beyond baseline's two live-Armory ones are export
  artifacts (no `.git`, no `.ogg`, no stub `SubModule.xml`, no `Dependencies\bin`), each checked by
  its message.
- Runs use `dotnet vstest <dll> --logger:trx --ResultsDirectory:results` (SDK 10.0.401); `trx.py`
  and `diff.py` parse the trx files. Raw trx and logs stay in `lane5-probe\results\`.

## Measurement 1: source-scraping tests

**Method.** `scrape.py` flags a test file when it holds a string literal matching `\.cs"`, `"*.cs"`,
`.csproj"`, `.props"` or `EndsWith(".cs")`; each hit was then read. 48 files match; one
(`Migration/ReflectionSiteBindingTests.cs`) only names a `.cs` file as a DataRow label and never reads
it. **47 test files read C# (or csproj) source as text** (outside lead: 43). They hold **254**
text-match calls (`StringAssert.Contains|DoesNotMatch|Matches`, `.Contains("`, `IndexOf("`, `Regex.*`).

**Inconclusive.** 96 test files contain `Assert.Inconclusive` at all (seed F2 counted 42 "when a path
is missing"; the difference is the reason). Classified by the guard around each call:

| Reason for Inconclusive | Files | Sites |
|---|---|---|
| Game assemblies not loaded (`GameAssemblies.EnsureLoaded()` false, i.e. `BANNERLORD_GAME_DIR` unset in the test process) | 73 | 91 |
| A repo source file not found (all among the 47 scrapers) | 10 | 21 |
| A repo data or build-output file not found: `ConfigIdValidationTests`, `CustomBattleCommandersShippedDataTests`, `WarOfTheRingShippedConfigTests`, `VolunteerRecruitmentServiceTests` (13 sites), `BundledDependencyManifestTests:207`, `DependenciesPairingTests:74` | 6 | 24 |
| Live install missing (Armory, game dir) | 8 | 12 |
| Discovery floor ("only N method bodies scanned") | 5 | 6 |

So **16 files go Inconclusive when a repo file is missing** (10 source plus 6 data). The 10 source
ones: `AutoResolveDiagnosticsWiringTests.cs:96,111`, `FiefHubCampaignBehaviorTests.cs:97,115,134`,
`RacePersistenceBehaviorTests.cs:56,105`, `MessengerCampaignBehaviorTests.cs:36,49`,
`MountDespawnWiringTests.cs:25,37`, `SettlementGuardsWiringTests.cs:38,51,66,89`,
`SiegeDismountWiringTests.cs:37,50`, `SiegePropDiagnosticsWiringTests.cs:22,35`,
`SignatureStrikesBindingTests.cs:208`, `GameModelOverrideBindingTests.cs:57`.

**Helper copies.** Every scraper carries its own repo locator. By locator style (47 files):

| Style | Files | When not found |
|---|---|---|
| `FindRepoRoot()`: walk up from **cwd** to `TAOM.sln` | 16 (e.g. `AiPartySizeOrderingTests.cs:21`, `BannerTripletOrderingTests.cs:16`, `DisplayFrameSourceTests.cs:20`, `Patch80KingdomVoteDeadlockBindingTests.cs:299`, `Patch82...:143`, `Patch84...:214`, `Patch85...:141`, `Patch86...:267`, `Patch87...:295`, `Patch88...:231`, `GameModelOverrideBindingTests.cs:190`) | throws (fails); `GameModelOverrideBindingTests` returns null, then Inconclusive |
| `ReadProjectSource(...)`: probe the relative path upward, return null | 9 | caller calls Inconclusive (8) or `Assert.Fail` (1) |
| walk up from cwd to `Main/Adapters`, or probe a file | 3 (`EnlistmentAttachedToPolicyTests.cs:29`, `SettlementLookupBindingTests.cs:25`, `ArmyMembershipBindingTests.cs:195`) | fails |
| relative string `"../../../../Main/..."` against cwd | 1 (`SettlementFollowingTests.cs:471`) | throws |
| walk up from `BaseDirectory` to `.git` | 3 (`ExitStallDisarmTests.cs:120`, `FieldDutyRuntimeTests.cs:393`, `TaomCulturalFeatsDefinitionTests.cs:361`) | throws; also fails in any tree without `.git` |
| walk up from `Assembly.Location` to `Main/Features` | 2 (`CoopVetoClassificationTests.cs:314`, `ResetForUnloadSweepTests.cs:29`) | fails |
| fixed `BaseDirectory\..\..\..\..` | 11 (e.g. `FieldCampWiringTests.cs:24`, `HeroRaceWiringTests.cs:30`, `RefugeWiringTests.cs:20`, `WandererAllegianceWiringTests.cs:20`, `UncapturableHeroesWiringTests.cs:19`, `SignatureStrikesBindingTests.cs:32`, `IoCRegistrationDisciplineTests.cs:38`) | fails; `SignatureStrikesBindingTests` Inconclusive |
| shared `RepoPaths.RepoPath` (`[CallerFilePath]`) | 2 (`EnlistmentDiagnosticsSettingsProviderTests`, `FieldCommissionSettingsProviderTests`) | cannot miss while the source tree exists |

The **`ReadProjectSource`-style copies are 9, 8 keyed off the current working directory** (outside
lead: 8 and 7): `FiefHubCampaignBehaviorTests.cs:143`, `RacePersistenceBehaviorTests.cs:119`,
`MessengerCampaignBehaviorTests.cs:125`, `MountDespawnWiringTests.cs:73`,
`SettlementGuardsWiringTests.cs:133`, `SiegeDismountWiringTests.cs:81`,
`SiegePropDiagnosticsWiringTests.cs:51`, `EnlistmentDiagnosticsGateTests.cs:489`
(`ReadProjectSourceLines`, the one that fails instead: "the scan must never pass by not finding its
inputs"), plus `AutoResolveDiagnosticsWiringTests.cs:120` (keyed off `AppDomain.BaseDirectory`).
Repo-wide (all 753 test files, not only scrapers): 56 files use the cwd, 62 use `BaseDirectory` or
`Assembly.Location`, 37 hard-code the four-level depth, 28 define their own `FindRepoRoot`, and only
`TAOM.Tests/Infrastructure/RepoPaths.cs:12` (added `35007dbb`, 2026-09-17, 3 users) is layout-proof.
Lane 1 section 4 and COMP-03 already cover the 26 of these that read `SubModule.cs`/`IoC.cs`.

## Measurement 2: can they go silently green? PROVED, two ways

| Run | Where | `BANNERLORD_GAME_DIR` | Settings | Result | Exit |
|---|---|---|---|---|---|
| A | `out\` (outside any repo) | set | default | 9,691 passed, **485 failed**, 63 skipped | 1 |
| C1 | `out\`, filter `MountDespawnWiringTests` | set | default | **"Passed!"** 3 passed, 2 skipped, 0 failed | **0** |
| C2 | same | set | `MapInconclusiveToFailed` | 3 passed, 2 failed | 1 |
| F1 | `fake\` (repo layout) | set | default | 10,222 passed, 14 failed (export artifacts), 3 skipped | 1 |
| F2 | `fake\` | **unset** | default | 9,853 passed, 23 failed, **363 skipped** | 1 |
| F3 | `fake\`, filter `TestCategory=BindingVerification` | **unset** | default | **"Passed!"** 33 passed, **335 skipped**, 0 failed | **0** |
| F4 | same | unset | `MapInconclusiveToFailed` | 33 passed, **335 failed** | 1 |

- **Outside the repo** (run A), most scrapes fail loudly (485 failures, mostly `TAOM.sln not found`
  or `... not found at <path>`), so a whole-suite run cannot go green that way. But the 10
  Inconclusive-on-missing-source files plus 36 `VolunteerRecruitmentServiceTests` data rows go to
  Skipped (61 results), and any run that selects only them is green: C1 printed `Passed!` and exited 0
  while both wiring assertions checked nothing.
- **The larger hole is the game directory, not the layout.** `TAOM.Tests/Migration/GameAssemblies.cs:111-122`
  reads `BANNERLORD_GAME_DIR` from the test process environment, while the build takes the same name as
  an MSBuild property (`Directory.Build.props:37-38`). So `-p:BANNERLORD_GAME_DIR=...`, an IDE test
  runner started without the variable, or a CI job that sets it only for the build step compiles fine
  and then skips **360 results** (F2 minus F1). Run F3 is the proof: the engine-drift gate (the
  `BindingVerification` category, 368 results) reports `Passed!`, exit 0, with 335 never executed.
- **What the trx records.** MSTest 3.1.1 maps Inconclusive to vstest `Skipped`; the trx writes the
  per-result `outcome="NotExecuted"`, and the `Counters` element does not count it anywhere: C1's trx
  has `total="5" executed="3" passed="3" failed="0" inconclusive="0" notExecuted="0"` and
  `ResultSummary outcome="Completed"`. Only `total - executed` reveals it. baseline.md's "0
  inconclusive" holds only because it also read the per-result outcomes (its 2 NotExecuted are the
  two `[Ignore]` warg tests); the Counters alone can never show an Inconclusive.
- **A GitHub-hosted runner's layout** does not trigger the cwd problem: `actions/checkout` puts the
  repo at `D:\a\<repo>\<repo>` and the testhost runs with the output folder
  (`TAOM.Tests\bin\<cfg>\net472`) as its working directory, so the upward walks find `TAOM.sln`, the
  fixed four-level depth holds for `bin\Release\net472` too, and checkout keeps `.git`. What a hosted
  runner does trigger is the game-directory skip: no game, so every `GameAssemblies`-gated test goes
  Skipped and the run stays green (F3). That is why the Inconclusive mapping must land before CI does.
- **`MapInconclusiveToFailed` in MSTest 3.1.1.** The adapter's settings parser holds the key
  `MAPINCONCLUSIVETOFAILED` (UTF-16 literal found in
  `~/.nuget/packages/mstest.testadapter/3.1.1/build/net462/Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.dll`),
  and runs C2 and F4 show it is honoured: the element is
  `<RunSettings><MSTest><MapInconclusiveToFailed>true</MapInconclusiveToFailed></MSTest></RunSettings>`
  (file `lane5-probe\map.runsettings`).

## Measurement 3: which tests need the real game at runtime (RequiresGame)

**Method (empirical, not by imports).** Two copies of the test bin under `fake\TAOM.Tests\bin\`, both
run with `BANNERLORD_GAME_DIR` unset, diffed per result against run F2 (same layout, real DLLs,
variable unset) by `diff.py`:

| Bin | What it models | Passed | Failed | Skipped | New failures vs F2 |
|---|---|---|---|---|---|
| `RefAsm\net472`: the 50 `TaleWorlds.*.dll` replaced by BUTR `Bannerlord.ReferenceAssemblies.Core 1.5.3.122374-beta` `ref/net472` copies | CI that copies the stripped assemblies into the test output | 8,546 | 1,330 | 363 | **1,307 results in 92 files** |
| `NoTw\net472`: no `TaleWorlds.*.dll` at all | CI with a plain `PackageReference` (NuGet `ref/` assets are compile-only, so nothing is copied) | 7,174 | 2,565 | 316 | 2,540 results in 203 files, plus 184 results never discovered |

- BUTR's stripped assemblies **load** (no `ReferenceAssemblyAttribute`) and every method body is
  `ldnull; throw` (measured: `TaleWorlds.Library.MathF.Clamp` has 2 IL bytes and throws
  `NullReferenceException` when invoked from PowerShell). So on CI a test either never executes engine
  code, or it fails **loudly** with an NRE. 1,301 of the 1,307 new failures are that NRE.
- The engine members that trip them, by first TaleWorlds frame: `Vec2.Equals` 193, `TextObject..ctor`
  171, `CampaignBehaviorBase..ctor` 150, `Vec2..ctor` 139, `FeatObject..ctor` 113,
  `ExplainedNumber..ctor` 100, `ViewModel..ctor` 60, `MBBindingList..ctor` 60, `Vec3..ctor` 41. These
  are pure managed engine types; they run fine locally and can never run against metadata-only
  assemblies.
- **"Imports TaleWorlds" is a poor proxy.** 102 of 753 test files have a `using TaleWorlds...` or
  `using SandBox...` (outside lead: 100 of 752). Only 57 of those are among the 92 that need the game;
  35 of the 92 import nothing from TaleWorlds (they reach the engine through TAOM production code, for
  example `TroopWeightServiceTests`, `SupplyOrderScreenVMTests`, `SiegeDefenseServiceTests`), and 45
  files that do import TaleWorlds run fine on stubs (NSubstitute proxies of engine-typed interfaces
  only need the types to load).
- The 92 files, with failing results per file (the measured `RequiresGame` list; files on the brief's
  skip list are included because the measurement ran against the committed tree):
  CulturalFeats/CulturalFeatsServiceTests (113), Refuge/RefugeServiceTests (112),
  FieldCamp/CampServiceTests (81), SmartCavalryAI/CavalryChargeServiceTests (77),
  SupplyLines/SupplyOrderScreenVMTests (60), AiPartySize/AiPartySizeServiceTests (49),
  TroopProgression/WageModifierServiceTests (37), MixedFormations/FormationLayoutServiceTests (36),
  FieldCamp/FieldCampOverlayVMTests (31), SupplyLines/SupplyGoodsSearchTests (29),
  TroopWeight/TroopWeightServiceTests (27), SettlementFood/SettlementFoodServiceTests (25),
  CareerSystem/CareerScreenVMTests (25), CareerSystem/CareerAgentStatServiceTests (24),
  SupplyLines/SupplyOrderServiceTests (23), TroopWeight/SizePenaltyTests (21),
  FiefGranting/FiefGrantingBehaviorCaptureGateTests (20), Enlistment/Duties/FieldDutyRuntimeTests (20),
  SettlementNameplateRelation/NameplateRelationPaletteTests (18),
  FiefGranting/FiefGrantingBehaviorSessionResetTests (18), Enlistment/ServiceVocabularyTests (16),
  Spider/SpiderAttackServiceTests (15), SceneScripts/Roads/RoadGeometryBuilderTests (14),
  CareerSystem/TaomCareerHotKeyCategoryTests (14), AutoResolveDiagnostics/AutoResolveDiagnosticsBehaviorTests (14),
  Warg/WargAttackServiceTests (13), StartupResources/StartupResourcesBehaviorTests (13),
  SmartCavalryAI/CavalryPathPlannerTests (13), Siege/SiegeDefenseServiceTests (12),
  PlayerSwitcher/KingdomJoinOfferBehaviorTests (12), TimeAcceleration/TaomTimeControlHotKeyCategoryTests (11),
  CoopInterop/CoopAuthorityGateTests (11), CareerSystem/CareerPassiveServiceTests (11),
  Refuge/RefugeCampaignBehaviorTests (10), HeroRace/TableauPositionServiceTests (10),
  CultureDoctrine/ArcherFlankGeometryTests (10), AdvancedCombat/CustomAttacksUtilsTests (10), and 55
  more with 1 to 9 each (full list: `lane5-probe\requiresgame_refasm.txt`; reproduce with
  `diff.py results/f2_layout_envunset.trx results/ci_RefAsm.trx`).

**Tests coupled to the live, unversioned install** (read the Armory or the game install at runtime,
committed tree): `Core/CultureRaceConsistencyTests.cs:123-128` (Armory `skins.xml`),
`Features/BannerBearers/BannerBearerReplacementWeaponDataTests.cs:106` (Armory items),
`Features/Animalia/AnimaliaMountWiringTests.cs:37,44`, `Features/Elephant/HowdahCrewLoadoutTests.cs:19,82`,
`Features/Elephant/HowdahHarnessItemTests.cs:21,37`, `Features/Elephant/HowdahPrefabTests.cs:62,144,170,257`,
`Features/Elephant/LegacyHowdahPrefabTests.cs:26,50`, `Features/Elk/ElkConfigTests.cs:101,104`,
`Features/Mumakil/MumakilPlatformTests.cs:50,187,328`: **10 files read the unversioned Armory**, and
9 of them fall back to a hard-coded `E:\Steam\...` path. `Core/LordFamilyTransformTests.cs:32,54`
reads the vanilla install. The two baseline failures are in this set
(`ElkConfigTests.TheElkItem_DeclaresTheScaleTheReachIsTunedFor`, and
`AnimaliaMountWiringTests.AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`; both files are on
the brief's skip list, read here at `b2e387db` only). Their verdict is a function of whatever the
Armory holds today: missing means Skipped, present-and-edited means red. CI can never run them.

**Binding tests against metadata only.** With a fake game directory built from the BUTR packages
(`lane5-probe\fakegame\`: an empty `Bannerlord.exe`, Core in `bin\Win64_Shipping_Client`, Native,
SandBox, CustomBattle and StoryMode under `Modules\<m>\bin\...`), the `BindingVerification` category
ran **338 passed, 30 failed, 0 skipped** of 368 (`results\ci_bind_refasm.trx`). All 30 fail loudly
and need something metadata cannot give: vanilla method IL (the Patch80/86/87/88,
UncapturableHeroes, WandererAllegiance, MarriageAlignment and CultureDoctrine call-site scans,
`TranspilerSiteBindingTests`), vanilla ModuleData or prefab files (`XsltTemplateCoverageTests`, the
three `Prefab*BindingTests`, `LordInlineSkillParityTests`, `BannerBearerReplacementWeaponDataTests`),
or engine execution (`GlobalStringsOverridesTests`, `TaomStartOptionsProviderTests`).

## Measurement 4: tests pinned to implementation

**Method.** Counted over all 753 files with `scrape.py`-style regexes, then read a sample.

| Pattern | Count | Verdict from the sample |
|---|---|---|
| Text-match calls on source inside the 47 scrapers | 254 | the pinning problem; worst files below |
| `.Received(n)` exact call counts | 811 in 144 files; `n >= 2` only 48 in 24 files | sampled `InitialChildGenerationServiceTests.cs:88,107,143,165,238` (`Received(3).CreateChild`): these count children created, which is behavior. Not a finding. |
| `BindingFlags.NonPublic` | 65 in 39 files | mostly engine private fields that Harmony injects (a legitimate pin); on TAOM's own types only 5 sites (`FileLoggerTests.cs:278,402,409`, `MissionTickStallWatchdogTests.cs:194`, `BehaviorTreeAgentComponentThreadingTests.cs:40`). Minor. |

Worst implementation pins (all read this run):

- `Features/Enlistment/SettlementFollowingTests.cs:471-473` reads
  `"../../../../Main/Adapters/CommanderLordAdapter.cs"` relative to the cwd, then cuts the method body
  with `IndexOf("        }")`: the assertion depends on **eight spaces of indentation**. A reformat
  or an extracted local changes what is checked without failing.
- `Features/AiPartySize/AiPartySizeOrderingTests.cs:36-103` asserts call order by `IndexOf` in
  `TaomPartySizeModel.cs`; its own comment says "nothing but a source-order assertion catches it". The
  two calls go to injected interfaces (`TaomPartySizeModel.cs:19-20,55,63,74`), so the order is
  runtime-testable once it sits in a service method (`Received.InOrder`), which is also what
  `gamemodels.md` rule 4 asks of an override.
- `Features/TroopWeight/DisplayFrameSourceTests.cs:36-75` pins argument spelling
  (`"ApplyPartySizeWeightPenalty(party, ref result, includeDescriptions)"`) and a
  `if|switch|foreach` regex over an override body; a rename of the local `result` fails it.
- `Features/FieldCamp/FieldCampWiringTests.cs` and `Features/Refuge/RefugeWiringTests.cs` (20
  text-matches each), `UncapturableHeroesWiringTests.cs` (14), `WandererAllegianceWiringTests.cs`
  (12): composition-root spelling (Lane 1 COMP-03 owns these).
- `Features/Enlistment/EnlistmentDiagnosticsGateTests.cs:300-330` (16): the best of the family (it
  strips comments, tracks aliases of the toggle and fails on a missing input), which shows how much
  machinery a text scan needs to approach what an analyzer gets from the syntax tree for free.

# Findings

Ranked by leverage (impact over effort, discounted by confidence and risk). Delta is against the June
audit commit `141b749`: test files calling `Assert.Inconclusive` grew from 11 to 96, and files naming a
`.cs` literal from 10 to 48 (`git grep -l ... 141b749` versus `b2e387db`).

### [TEST-L5-01] Map Inconclusive to Failed: today the whole engine-drift gate can report "Passed!" having run nothing

- **Evidence**: `TAOM.Tests/TAOM.Tests.csproj:1-43` has no `RunSettingsFilePath`, and `git grep -l
  -E "runsettings|MapInconclusiveToFailed" b2e387db` returns nothing, so MSTest 3.1.1's default
  (Inconclusive becomes Skipped) applies everywhere.
- **Evidence**: run F3 (repo layout, `BANNERLORD_GAME_DIR` unset, `--filter
  TestCategory=BindingVerification`): `Passed!  - Failed: 0, Passed: 33, Skipped: 335`, exit 0.
  Run F4, the same with `map.runsettings`: 335 failed, exit 1. Run C1 shows the same for a source
  scrape (`MountDespawnWiringTests`, 2 of 5 skipped, exit 0).
- **Evidence**: `.claude/skills/verify-bindings/SKILL.md:27` tells every agent the gate "self-reports
  `Assert.Inconclusive` (it does not falsely pass)", and its Step 1 command is exactly F3's filter.
  The run above refutes that sentence: the skill's own command exits 0 with `Passed!`.
- **Evidence**: the trx cannot rescue a reader either: MSTest Inconclusive lands as per-result
  `outcome="NotExecuted"` with `inconclusive="0" notExecuted="0"` in `Counters` and
  `ResultSummary outcome="Completed"` (C1's trx). `build.ps1:37-40` checks only the exit code.
- **Impact**: 360 results (the 73-file `GameAssemblies` family, F2 minus F1) plus 61 source and data
  results (run A) can silently skip. The binding suite is the gate every engine bump relies on
  (`/engine-bump`, `/verify-bindings`), and the moment CI arrives on a hosted runner (no game) all of it
  goes quiet by default.
- **Effort**: S. One runsettings file, one csproj property, and a decision for the few tests that
  legitimately cannot run somewhere (they move to a category, TEST-L5-05 and TEST-L5-06, instead of
  skipping).
- **Risk**: LOW to MED. Anything that goes Inconclusive today turns red: the laptop's partial install
  (memory says desktop-only for now) and a missing Armory. `[Ignore]` stays Skipped (it is not an
  Inconclusive), so the two warg tests are unaffected.
- **Confidence**: HIGH (measured, both directions).
- **Fix sketch**: add `TAOM.Tests/TAOM.runsettings` holding
  `<RunSettings><MSTest><MapInconclusiveToFailed>true</MapInconclusiveToFailed></MSTest></RunSettings>`
  and `<RunSettingsFilePath>$(MSBuildThisFileDirectory)TAOM.runsettings</RunSettingsFilePath>` in
  `TAOM.Tests.csproj`, so `dotnet test`, `build.ps1` and IDE runners all pick it up. Correct the
  verify-bindings sentence. Longer term, the Inconclusive calls themselves can become `Assert.Fail` (the
  house already argues for it at `TroopUpgradeSkillMonotonicityTests.cs:101-105`).
- **Delta**: pre-existing mechanism, grown (11 to 96 Inconclusive files). **P1**: no (test-gate
  integrity, not crash, save or security). **Plan candidate**: yes, the smallest change with the
  largest effect in this lane, and the precondition for CI.

### [TEST-L5-02] Let the binding gate find the game the build used, instead of only the test process environment

- **Evidence**: `TAOM.Tests/Migration/GameAssemblies.cs:111-122` resolves the game only from the
  environment variables `BANNERLORD_OVERRIDE_DIR` and `BANNERLORD_GAME_DIR`. The build resolves
  `GameFolder` from MSBuild properties of the same names (`Directory.Build.props:37-38`), which an
  environment variable OR a `-p:` global property satisfies.
- **Evidence**: F2 versus F1: the same bin with and without the variable in the test process moves
  360 results from executed to skipped; nothing about the build differs.
- **Impact**: `dotnet test -p:BANNERLORD_GAME_DIR=...`, a Rider or VS test run launched without the
  variable, or a workflow that sets it only on the build step all compile against the game and then
  skip the binding suite. TEST-L5-01 turns that into a red run; this finding makes the red run
  unnecessary.
- **Effort**: S. **Risk**: LOW (additive fallback). **Confidence**: HIGH.
- **Fix sketch**: in `TAOM.Tests.csproj` (game refs at `:34-39`), emit
  `[assembly: AssemblyMetadata("TaomGameFolder", "$(GameFolder)")]` through an `AssemblyAttribute`
  item; `GameAssemblies.ResolveGameDir` reads it after the two variables. The same seam lets CI point
  the gate at a BUTR reference-assembly folder (design below).
- **Delta**: pre-existing (`GameAssemblies.cs` exists at `141b749`). **P1**: no. **Plan candidate**:
  yes, fold into the TEST-L5-01 plan.

### [TEST-L5-03] Collapse the 47 source-scraper locators (and the 28 `FindRepoRoot` copies) onto `RepoPaths`, and delete their Inconclusive branches

- **Evidence**: the locator table in Measurement 1: seven locator styles across 47 scrapers; repo-wide
  28 `FindRepoRoot` definitions, 56 cwd-keyed files, 62 `BaseDirectory`-keyed, 37 with a hard-coded
  four-level depth. The layout-proof helper already exists (`TAOM.Tests/Infrastructure/RepoPaths.cs:14-21`,
  `[CallerFilePath]`) and has 3 users.
- **Evidence**: run A (bin copied outside the repo): 485 failures and 61 skips, all locator-driven;
  the three `RepoPaths` users kept passing. The `.git`-keyed trio (`ExitStallDisarmTests.cs:120`,
  `FieldDutyRuntimeTests.cs:393`, `TaomCulturalFeatsDefinitionTests.cs:361`) fails in any tree without
  `.git` (the source export `fake\`, run F1).
- **Impact**: each copy is a place where "file not found" can mean skip, fail or throw, chosen by
  whoever pasted it; eight of nine `ReadProjectSource` copies choose skip. The copies are also why a
  comment-stripping fix (Lane 1 COMP-03) would have to be applied 26 times.
- **Effort**: M (mechanical, about 100 files, each a two-line change; can ride along whenever a file is
  touched). **Risk**: LOW, test-only. **Confidence**: HIGH.
- **Fix sketch**: extend `RepoPaths` with `ReadSource(params string[] parts)` that asserts existence
  (`Assert.Fail`, never Inconclusive), normalises CRLF, and offers a `code-only` view with comments
  stripped (the COMP-03 interim helper; `CoopVetoClassificationTests.cs:304-312` has one to lift).
  Replace the private locators file by file; the verification is run A again (0 locator failures from
  a bin copied anywhere on the machine).
- **Delta**: introduced (38 of the 48 scrapers are new since `141b749`). **P1**: no. **Plan
  candidate**: yes, together with COMP-03's interim step (one helper, one plan).

### [TEST-L5-04] Replace the source scrapes with analyzers and IL tests, family by family

- **Evidence**: 47 scrapers, 254 text matches (Measurement 1). They cannot tell code from comments
  (Lane 1 COMP-03 proves a false pass on a commented-out registration), they pin spelling and even
  indentation (`SettlementFollowingTests.cs:473`), and they only see the file they name.
- **Evidence**: the repo already owns the right tool for half of them: `TAOM.Tests/Migration/IlCallScanner.cs`
  walks raw IL of every TAOM method and is called from 19 test files (`git grep -l "IlCallScanner." b2e387db -- TAOM.Tests`), for example
  `Features/BattleBalance/PartyOwnerGetterBanTests.cs:44-48`, which bans a call assembly-wide with a
  discovery floor and an "unreadable bodies" report.

| Family | Files (examples) | Replacement | Why that one |
|---|---|---|---|
| Composition-root wiring (SubModule.cs / IoC.cs text) | about 26 (Lane 1 section 4) | reflection over the feature-module list (Lane 1 design) | the question is "is it registered at runtime", which only the object graph answers |
| "Type or method X must not reference Y" | `FieldDutyRuntimeTests.cs:372-392`, `SettlementLookupBindingTests.cs:37`, `ArmyMembershipBindingTests.cs:131,185`, `EnlistmentAttachedToPolicyTests.cs:41,69`, `SettlementFollowingTests.cs:471`, `HoNFormationPresetSerializationTests.cs:147` | IL test through `IlCallScanner` (a named method's body must not call member Y) | compiled truth: comments, spelling and formatting cannot fool it, and it runs with the rest of the suite |
| Folder-wide or shape rules (no branching in a GameModel override, gamemodels.md rule 4; no eager `Resolve` inside `Register*Feature`; bool-prefix classification) | `DisplayFrameSourceTests.cs:48-63`, `IoCRegistrationDisciplineTests.cs`, `CoopVetoClassificationTests.cs`, `EnlistmentDiagnosticsGateTests.cs:300-330` | a Roslyn `DiagnosticAnalyzer` in a small netstandard2.0 analyzer project referenced by `Main` as an analyzer | syntax-level rules over every file in a folder, reported at build time with file:line in the IDE; the scrape covers one named model while the rule is meant for all 50 |
| Assembly-wide API bans | `PartyOwnerGetterBanTests`, `InformationManagerClearBanTests` (already IL) | keep the IL tests, or `Microsoft.CodeAnalysis.BannedApiAnalyzers` with a `BannedSymbols.txt` | the off-the-shelf analyzer (RS0030) applies per project, not per folder, so folder-scoped bans still need the custom analyzer |
| Call order inside an entry point | `AiPartySizeOrderingTests.cs:36-103`, `BannerTripletOrderingTests.cs`, `SharedMovementOrderPostfixTests.cs` | move the sequence into a service method and assert `Received.InOrder` on the injected fakes | order is behavior; the override then holds one call, which is what gamemodels.md rule 4 wants anyway |
| Data kept in code (string keys, pool tables, feat metadata) | `PlayerSwitcherLocalizationTests.cs`, `UnregisteredLocalizationKeyBaselineTests.cs`, `RegisteredDefaultRoundTripTests.cs`, `TavernMercenaryDataTests.cs:251`, `TaomCulturalFeatsDefinitionTests.cs:327` | keep as a text scan through the shared helper, or parse with `Microsoft.CodeAnalysis.CSharp` (literals only, comments ignored) | the property really is textual (which keys the source names); an analyzer adds nothing |
| csproj / props checks | `BundledDependencyManifestTests.cs:30-31` | keep (XML parse of XML is correct) | not a C# scrape |

- **Impact**: removes the comment-shaped hole and the refactor tax, and moves the cheapest rules
  (shape rules) to compile time where they cover the whole codebase instead of one file.
- **Effort**: L overall; the analyzer project plus two rules is M, each IL conversion is S.
- **Risk**: LOW for IL conversions. MED for the analyzer project: net472 plus SDK-style works with a
  netstandard2.0 analyzer, but the rule set must start at warning level, or it blocks unrelated builds.
- **Confidence**: HIGH for the diagnosis, MED for the effort.
- **Fix sketch**: first convert the "must not reference" family to `IlCallScanner` (lowest risk, the
  helper exists); then an `Analyzers/TAOM.Analyzers.csproj` with TAOM001 (no control flow in a
  `GameModel` override body) and TAOM002 (no `IoC.Resolve` inside a `Register*Feature` method); delete
  each scrape in the commit that lands its replacement.
- **Delta**: introduced (38 new scrapers since June). **P1**: no. **Plan candidate**: yes, as two
  plans: IL conversions (S), analyzer spike (M).

### [TEST-L5-05] Take the live-Armory tests out of the unit gate: they make HEAD's verdict depend on another session's disk

- **Evidence**: 10 test files read the unversioned `LOTRLOME_Armory` at runtime (Measurement 3 list),
  9 with a hard-coded `E:\Steam\...` fallback (for example `HowdahPrefabTests.cs:62`,
  `MumakilPlatformTests.cs:50`, `ElkConfigTests.cs:101`, `CultureRaceConsistencyTests.cs:123`). All 10
  are new since `141b749`.
- **Evidence**: baseline.md: the only 2 failures of the HEAD suite are two of these, caused by
  another session editing the live Armory. In run A (bin outside the repo) `AnimaliaMountWiringTests`
  failed with a different message than in baseline, because the Armory changed between the two runs.
- **Impact**: a green or red `dotnet test` stops meaning "this commit": the same commit flips with
  install state, which trains everyone to discount red. On a machine without the Armory the same tests
  skip silently (TEST-L5-01).
- **Effort**: S. **Risk**: LOW, if `/verify` and `/armory-audit` run the category explicitly so the
  signal is kept. **Confidence**: HIGH.
- **Fix sketch**: `[TestCategory("LiveInstall")]` on the 10 files; the default local and CI filter
  excludes it; `/verify` runs it as a separate, labelled step whose failure names the install, not the
  commit. Replace the `E:\Steam` literals with `GameAssemblies.GameDir` (one resolution rule).
- **Delta**: introduced. **P1**: no. **Plan candidate**: yes (small, mechanical). Note: 5 of the 10
  files are on this run's skip list (another session's live work); the plan must wait for that work to
  land.

### [TEST-L5-06] Tag `RequiresGame` from the measurement, not from `using` lines

- **Evidence**: Measurement 3: 92 files (1,307 results) execute engine method bodies; only 57 of them
  import a TaleWorlds namespace, and 45 importing files run fine on metadata. A `using`-based tag would
  miss 35 files and over-exclude 45.
- **Impact**: without a correct tag, hosted CI either runs them (1,307 red NREs) or excludes by a bad
  proxy (35 files red, 45 files needlessly dropped from CI).
- **Effort**: S (attribute on 92 classes, list in `lane5-probe\requiresgame_refasm.txt`).
- **Risk**: LOW. The tag set is self-policing on CI: BUTR's stubs throw on execution, so a test that
  starts touching engine code without the tag goes red on CI, never green.
- **Confidence**: HIGH for this commit's set (measured); the set drifts with every change, which the
  self-policing covers.
- **Fix sketch**: `[TestCategory("RequiresGame")]` at class level; CI filter
  `TestCategory!=RequiresGame&TestCategory!=LiveInstall`. The top of the list (`CulturalFeatsServiceTests`
  113, `RefugeServiceTests` 112, `CampServiceTests` 81) is where a value-type seam (for example keeping
  `Vec2` math out of services) would win back the most CI coverage later; not worth doing up front.
- **Delta**: n/a (new tagging). **P1**: no. **Plan candidate**: yes, inside the CI plan.

### [TEST-L5-07] Build and test on GitHub-hosted Windows against BUTR reference assemblies (extends seed F1)

- **Evidence**: seed F1 (`.github/workflows/build.yml:3-8,253-259`); this lane measured that every
  game DLL `Main/TAOM.csproj:25-59` references is published by BUTR for the installed build (design
  below), that 8,546 results pass against the stripped assemblies, and that the missing pieces are two
  BCL assemblies available on NuGet.
- **Impact**: today no job compiles C# on `bannerlord-1.5.x`; a broken build or a red suite reaches
  the branch whenever the local gate is skipped. With the design below every push and PR compiles
  both branches and runs about 8,500 tests plus 338 binding checks, with no game and no workstation.
- **Effort**: M. **Risk**: MED (first-run surprises in `Bannerlord.BuildResources` without a game
  folder are UNVERIFIED; the design keeps local builds byte-identical by default). **Confidence**: HIGH
  on package coverage and test counts (measured); MED on the build step (not built, per the brief).
- **Fix sketch**: see "Design: CI on GitHub-hosted Windows".
- **Delta**: pre-existing (F1). **P1**: no. **Plan candidate**: yes, after TEST-L5-01 (which must land
  first, or the hosted run is green by skipping).

### [TEST-L5-08] Direction: run the binding gate against the next engine build before Steam installs it

- **Evidence**: 338 of 368 `BindingVerification` results pass against BUTR metadata alone
  (`results\ci_bind_refasm.trx`); the 30 that need IL or vanilla data fail loudly. BUTR tags each
  package with the Steam build (`buildId:25302170` in the Core nuspec equals the local
  `appmanifest_261550.acf` `buildid`), and publishes betas (`1.5.3.122374-beta`).
- **Impact**: `/engine-bump` today starts after the game has already updated under a shipped build
  (the GAME VERSION DRIFT banner). A workflow_dispatch job taking a BUTR version as input would give
  the binding verdict for a new beta the day BUTR publishes it, before any player or the desktop
  installs it. How soon BUTR publishes after a TaleWorlds release is UNVERIFIED.
- **Effort**: S once TEST-L5-07 exists (one input, one filter). **Risk**: LOW. **Confidence**: MED.
- **Fix sketch**: `workflow_dispatch` input `bannerlord_refasm_version`; restore with it; run
  `TestCategory=BindingVerification&TestCategory!=RequiresGameIL`, tagging the 30 IL/data results.
- **Delta**: n/a. **P1**: no. **Plan candidate**: yes (design/spike plan).

### [TEST-L5-09] Mutation-testing spike on AlignmentRecruitment (design)

See "Design: mutation-testing spike". **Effort**: S (half a day). **Risk**: LOW (disposable worktree).
**Confidence**: MED (tool support read from Stryker's docs, not run). **Delta**: n/a. **P1**: no.
**Plan candidate**: yes, as a time-boxed spike.

## Design: CI on GitHub-hosted Windows

Goal: every push and pull request on `bannerlord-1.5.x` and `bannerlord-1.4.5` compiles all C# and runs
every test that can run without a game, on `windows-latest`, with local builds unchanged by default.

### What TAOM needs, and who ships it (measured)

| `Main/TAOM.csproj` reference (lines at `b2e387db`) | Installed files | Source on CI |
|---|---|---|
| `bin\Win64_Shipping_Client\TaleWorlds.*.dll` minus Native (`:40-43`) | 50 DLLs | `Bannerlord.ReferenceAssemblies.Core`: its `ref/net472` holds all 50 (set difference empty; it adds 4 `.exe`) |
| `Modules\Native\bin\...\*.dll` (`:44-47`) | 5 | `Bannerlord.ReferenceAssemblies.Native`: the same 5 |
| `Modules\SandBox\bin\...\*.dll` (`:48-51`) | 6 | `Bannerlord.ReferenceAssemblies.SandBox`: the same 6 |
| `Modules\SandBoxCore\bin\...\*.dll` (`:52-55`) | **none** (the folder holds no DLL) | nothing needed; no SandBoxCore package exists (nuget.org 404) |
| `Modules\CustomBattle\bin\...\*.dll` (`:56-59`) | 2 | `Bannerlord.ReferenceAssemblies.CustomBattle`: the same 2 |
| `System.Management.dll` from the game bin (`:25-28`), assembly 4.0.1.0 | 1 | not BUTR. NuGet `System.Management 4.7.0`, `lib/netstandard2.0` is 4.0.1.0 (its `lib/net45` is `_._`, so reference the file through `GeneratePathProperty`) |
| `System.Numerics.Vectors.dll` from the game bin (`:35-39`, alias `SNV`), 4.1.3.0 | 1 | not BUTR. NuGet `System.Numerics.Vectors 4.4.0`, `lib/net46` is 4.1.3.0; keep the alias |
| TAOM.Dependencies | n/a | a **ProjectReference** (`TAOM.csproj:89` at `b2e387db`, not a DLL at `:86`), built from source; its own game refs (`Dependencies/TAOM.Dependencies.csproj:16-17`, TaleWorlds.* minus Native and CampaignSystem) come from the Core package, keeping the CampaignSystem exclusion |
| Test-time module loads (`GameAssemblies.cs:33-34` adds StoryMode) | 5 | `Bannerlord.ReferenceAssemblies.StoryMode` (test job only) |

All versions on nuget.org include `1.5.3.122374-beta` and `1.4.8.119303` (flat-container index, read
this run). **Build match**: the installed `bin\Win64_Shipping_Client\Version.xml` holds only
`v1.5.3`; the change set is in the `Version.xml` resource embedded in `TaleWorlds.Library.dll`, which
reads `v1.5.3.122374`, and the Core nuspec's tag `buildId:25302170` equals `buildid` in
`E:\Steam\steamapps\appmanifest_261550.acf`. The package is the installed build, exactly. The
`1.4.8.119303` line for `bannerlord-1.4.5` was not checked against a 1.4.8 install (none here).

The assemblies are "stripped metadata-only" (nuspec description): they load at runtime (no
`ReferenceAssemblyAttribute`) and every body throws `NullReferenceException` (measured). That shapes
the test plan below.

### Property switch (local builds unchanged)

- `Directory.Build.props`: add `TaomGameRefs` (default `Install`) and `BannerlordRefAsmVersion`
  (`1.5.3.122374-beta` on 1.5.x, `1.4.8.119303` on 1.4.5), pinned next to
  `.claude/pinned-game-version.txt`; `lint_docs.py --fail-on-drift` already checks config drift and can
  check the two agree.
- One imported file (for example `build/GameReferences.targets`) owns both shapes, imported by
  `Main/TAOM.csproj`, `Dependencies/TAOM.Dependencies.csproj` and `TAOM.Tests/TAOM.Tests.csproj`:
  `Install` keeps today's HintPath items verbatim; `RefAsm` adds the four BUTR PackageReferences plus
  the two BCL packages. In `RefAsm` the test project also copies the package's `ref/net472` DLLs into
  its output: measured, that is 8,546 passing results versus 7,174 plus 184 undiscoverable when
  nothing is copied (`NoTw` run).
- CI passes `-p:TaomGameRefs=RefAsm`. No automatic switch on an empty `GameFolder`: locally that
  would hide a missing install behind stubs. Instead, `Install` with an empty `GameFolder` should fail
  with one clear error (today it yields hundreds of CS0246).
- Add `Microsoft.NETFramework.ReferenceAssemblies` (PrivateAssets all) so the net472 targeting pack no
  longer depends on what Visual Studio installed (`build.yml:248-252` records its absence).
- `GameAssemblies` gets the TEST-L5-02 metadata seam; in `RefAsm` the build writes the folder holding
  the BUTR module DLLs laid out as `Modules\<m>\bin\Win64_Shipping_Client` (as `lane5-probe\fakegame\`
  did), so 338 binding results run on CI.

### Test categories and the CI filter

| Category | Files | Where it runs |
|---|---|---|
| (none) | about 650 files | local and CI |
| `RequiresGame` | 92 files, 1,307 results (Measurement 3, TEST-L5-06) | local only |
| `RequiresGameIL` | the 30 binding results needing vanilla IL or vanilla data | local; CI never |
| `LiveInstall` | 10 Armory readers (TEST-L5-05) | local, labelled step in `/verify` |

CI: `dotnet test ... --no-build --settings TAOM.Tests/TAOM.runsettings --filter
"TestCategory!=RequiresGame&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall" --logger trx`,
then a floor check on the trx (`executed` at least N, the pattern `build.yml:222-235` already uses for
`tools/tests`), so a filter or discovery accident cannot pass with few tests. With
`MapInconclusiveToFailed` on (TEST-L5-01), an untagged test that needs the game fails red, never green.

### Triggers, runner, permissions

- `on: push` and `pull_request` for `[bannerlord-1.5.x, bannerlord-1.4.5]`, plus `workflow_dispatch`
  with an optional `bannerlord_refasm_version` input (TEST-L5-08). Today `build.yml:3-8` names only
  `bannerlord-1.4.5`.
- `runs-on: windows-latest`, `permissions: contents: read`, no secrets (all packages are on
  nuget.org). Delete the self-hosted job (`build.yml:238-278`) and the warning job (`:15-25`).
- **No self-hosted runner.** The repo is public and contributor branches live in it
  (`build.yml:255-258`). The guard `if: ... github.event_name != 'pull_request'` (`:259`) is not a
  security boundary because it lives in the file it is meant to protect: for a `push` or a
  `workflow_dispatch`, GitHub runs the workflow file from the pushed or selected ref, so anyone with
  write access can push a branch whose `build.yml` adds that branch to `on.push.branches` and drops
  the `if`, and the job then runs their code on the workstation with the runner account's rights
  (reasoned from GitHub's documented event semantics, not exercised). GitHub's own guidance is to keep
  self-hosted runners off public repositories. A hosted runner is a throwaway VM, so the same abuse
  costs one wasted run.
- Build with `-c Release -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=`: `GameFolder`
  is empty on the runner so `CopyModule` cannot deploy anyway, but the flags keep one command form
  everywhere.

### Caching

- NuGet: `actions/cache` on `~\.nuget\packages`, keyed on `hashFiles('**/*.csproj',
  'Directory.Build.props', 'build/*.targets', '**/packages.lock.json')`. Better, adopt
  `RestorePackagesWithLockFile` (triage-B L480 DEPS-06, still valid) so the key is exact and restore
  runs `--locked-mode`. The BUTR payload is small (Core 5.2 MB, the others under 2 MB together).
- Do not cache `obj/` or `bin/`: the local build is 8 s and the suite about 30 s (baseline.md), so
  runner start-up and restore dominate, and a stale incremental cache is a correctness risk for no win.

### Fallback when BUTR does not ship something

Nothing is missing today. The fallback matters on an engine-bump day before BUTR publishes, or if BUTR
drops a DLL: generate metadata-only assemblies from the installed DLLs with a refasm tool (for example
JetBrains Refasmer; exact tool id and flags UNVERIFIED), and publish them to a **private** GitHub
Packages feed read with `GITHUB_TOKEN` (`packages: read`). Do not commit them to this public repo:
they reproduce TaleWorlds' proprietary API surface, and whether TaleWorlds permits that is UNVERIFIED
(BUTR's packages carry BUTR's MIT expression for the packaging; TaleWorlds' position is not stated in
them). That licensing call is Mike's; waiting for BUTR is the zero-risk default.

### Order of work

1. TEST-L5-01 and TEST-L5-02 (without them a hosted run is green by skipping, run F3).
2. Tag `LiveInstall` and `RequiresGame`, `RequiresGameIL` (TEST-L5-05, TEST-L5-06).
3. Property switch plus the workflow; first run on a branch, compare its trx with this lane's `RefAsm`
   numbers (8,546 passed expected before tagging, about 8,500 executed after).
4. Optional: TEST-L5-08 dispatch input.

## Design: mutation-testing spike

**Tool support (web, this one question).** Stryker.NET 4.16.0 is current (`dotnet-stryker` on
nuget.org, updated 2026-09-11). Its README states .NET Framework support from 4.5, tested against 4.8.
Its docs add, for .NET Framework projects: `--solution` is required ("The solution file is required for
dotnet framework projects"), `nuget.exe` must be on PATH, and the NuGet MSBuild targets must be
installed; the tool itself needs a current .NET runtime (this desktop has SDK 10.0.401), while the
project may target net472. The default runner is VSTest (the MTP runner does not combine mutants).
Not run here, so "works on TAOM" is UNVERIFIED; that is what the spike answers.

**Feature: `Main/Features/AlignmentRecruitment`.** 8 files, 244 lines, zero TaleWorlds imports;
`RecruitmentAlignmentService.IsRecruitmentBlocked` (`RecruitmentAlignmentService.cs:23-43`) is a pure
boolean decision with six exits; 36 test rows (`RecruitmentAlignmentServiceTests` 28, config provider
8); stable since 2026-06-17 (2 commits), so the score measures the tests, not churn; consumed by
`TaomVolunteerModel`, `WandererAllegianceService` and `MarriageAlignmentService`, so a surviving mutant
is a real recruitment bug; and it is not in the `RequiresGame` list, so the same tests run on CI.
Runner-up: `Main/Core/Validation` (`FiniteFloatValidator`, `SettingClamp`, 43 test rows).

**Setup.** A disposable worktree only. A `Directory.Build.rsp` at the worktree root holding
`-p:DisableModuleCopy=true -p:ModuleId=`, because Stryker drives MSBuild itself and `Main`'s
`CopyModule` otherwise deploys into the game (`TAOM.csproj:7` sets `ModuleId` unconditionally).
`stryker-config.json`: `solution TAOM.sln`, `project TAOM.csproj`, `test-projects [TAOM.Tests]`,
`mutate ["**/Features/AlignmentRecruitment/**/*.cs", "!**/*IoC.cs", "!**/I*.cs"]`,
`coverage-analysis perTest`, reporters `html`, `json`, `progress`, thresholds high 80 low 60 break 0.

**Measure.** (1) Whether net472 plus MSTest 3.1.1 plus VSTest runs at all, and the first error if
not. (2) Mutation score overall and per file, surviving mutants by mutator. (3) Wall time: initial
test run, compile, per-mutant. (4) The same with `test-case-filter
FullyQualifiedName~AlignmentRecruitment` versus the full suite, to learn whether tests elsewhere kill
mutants (the filter is fast but can understate the score).

**Stop conditions.** Stop and record the error if the solution does not build or the initial test run
fails after 2 hours of setup (verdict: not viable on net472 today). Stop if the initial test run exceeds
10 minutes or the whole run exceeds 60 minutes for this scope (verdict: too slow for this suite). Abort
immediately if `Modules\TAOM` in the game folder changes (compare its mtime before and after). Otherwise
finish with the report and one go/no-go line: score at least 80% with no surviving mutant that is a real
gap means per-PR mutation testing adds little at this tier; surviving real gaps become test issues, and
the next step is `--since` on changed services only.

## Considered and rejected

- **Rewriting the 96 Inconclusive calls as `Assert.Fail` now.** Rejected as the first move: the
  runsettings mapping gives the same verdict for one file instead of 96; convert call sites as files
  are touched.
- **A coverage-percentage gate** (coverlet is already referenced, `TAOM.Tests.csproj:20-23`).
  Rejected: the playbook asks which untested code is dangerous, not a percentage, and 12% of results
  cannot run on CI, which would skew any CI number.
- **Seams to make the 92 RequiresGame files engine-free** (wrap `Vec2`, `TextObject`,
  `ExplainedNumber`). Not worth doing up front: large churn in production services for CI coverage of
  code the desktop already tests; revisit per file when one is refactored anyway.
- **ArchUnitNET or NetArchTest** for the architecture rules. Rejected: `IlCallScanner` already covers
  call bans with a floor check; a new dependency buys little.
- **Generating executable (not stripped) assemblies from the install for CI.** Rejected outright:
  that is redistributing TaleWorlds code.
- **Hardening the self-hosted runner instead** (environments with required reviewers, ephemeral
  runner). Rejected: more moving parts than a hosted runner, and it still puts a public repo's code on
  the workstation that holds the live install.
- **`.Received(n)` and private-member reflection as implementation pins.** Measured and sampled
  (Measurement 4): mostly behavior or engine pins, 5 sites on TAOM privates. Not a finding.
- **Stryker in CI on every PR.** Rejected until the spike reports.

## What I did not cover

- No build (brief). Whether `Bannerlord.BuildResources 1.1.0.129` and the rest of the build work with
  an empty `GameFolder` and BUTR packages is UNVERIFIED; my probes swapped DLLs in an existing test bin,
  which proves runtime behavior and package coverage, not compilation.
- The net472 targeting pack on `windows-latest` and the hosted runner's testhost working directory
  were reasoned, not observed on a runner; no workflow was run and no GitHub settings were read.
- The `1.4.8.119303` packages were not compared with a 1.4.8 install.
- Tests that pass against stubs could still pass vacuously (production code catching the stub's NRE
  and a test asserting the fallback); not audited.
- Stryker was not installed or run (a spike design only), and only its README and docs pages were read.
- Files on the brief's skip list (Animalia, Elephant howdah, Elk, Mumakil, AdvancedCombat blow flags)
  entered the counts through the committed tree only; their working-tree state was not read.
- `tools/tests` (Python, already in CI per `build.yml:192-237`) and the hook harness were not audited.
- The 30 `RequiresGameIL` results were grouped by class from their failure messages, not tagged one by
  one.
