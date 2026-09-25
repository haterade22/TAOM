# Deep review: plan 010, C# on hosted Windows runners (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 010, compile and test C# on GitHub-hosted Windows runners against BUTR reference
         assemblies (branch improve/010-ci-on-hosted-windows, 2ca0805b..b8c00045)
Date: 2026-09-24

Scope:   build configuration (GameReferences.targets, three csproj), CI workflows, one new test
         file, category attributes on 122 test files, rule and doc text
Waves:   one wave: Agent 1 Standards, Agent 2 Engine compatibility, Agent 3 Efficiency,
         Agent 4 Completeness, Agent 5 Data flow, Agent 6 Design; Codex adversarial beside it

STANDARDS:     PASS: checks 1 to 10 and H1 to H6 clean; 4 LOW outside the checklist (3 confirmed)
COMPATIBILITY: PASS: 0 incompatible, 4 unverified; 1 MED and 2 LOW findings (all confirmed)
EFFICIENCY:    PASS: 0 issues in the changed hunks (2 FOLLOW-UP)
COMPLETENESS:  INCOMPLETE at review, COMPLETE after fixes: #421 link, feature-map rows,
               HintPath gap, tests.md failure signatures
DATA FLOW:     PASS after fixes: 16 flows, 3 gaps, 7 inconsistencies (all LOW)
DESIGN:        8 KEEP proposals (6 apply, 2 follow-up)
XML:           NOT IN SCOPE (no ModuleData or XSLT)
TOOLING:       NOT IN SCOPE (no hook or tools/ script changed)
```

## Verdict on each finding

A finding is a hypothesis until re-read against the worktree. Every row below was checked against
the files at `b8c00045` or by a run named in the evidence column.

| # | Source | Sev | Finding | Verdict | Evidence |
|---|---|---|---|---|---|
| F1 | Agent 2 F1, Agent 5, Codex P3 #1 | MED | The pin test checks only the `1.5.3.` prefix while its name, `csharp.yml:6` and the CHANGELOG claim the Steam build | CONFIRMED, fixed | `GameReferencesTargetsTests.cs:40` at `b8c00045`; `taom-src` shows `public const int DefaultChangeSet = 122374;` in the installed `TaleWorlds.Library` |
| F2 | Agent 1 L1, Agent 4, Agent 6, Codex P3 #1 | LOW | The reference guard misses `<HintPath>$(GameFolder)...` and other install properties; a conditional import still counts | CONFIRMED, fixed | `GameReferencesTargetsTests.cs:60-65` read only `Include`/`Exclude`; the new fixture test failed before the fix |
| F3 | Agent 1 L2, Codex P3 #2, Agents 2 to 5 | LOW | `csharp.yml:3-6` overclaims projects, tests and triggers | CONFIRMED, fixed | `on:` block at `csharp.yml:25-30`; `tools/BannerlordCraftingTool` not built; 24 unit skips in `7-unit.log` |
| F4 | Agent 2 F2, Agent 4, Agent 5 traces 5 and 6 | LOW | `tests.md` lists one CI failure signature; `RequiresGame` has no effect on the gate | CONFIRMED, fixed | `scratch/010/6a.log`: 6 `TypeInitializationException`, 6 `FileNotFoundException` (`TaleWorlds.MountAndBlade.View`), 1 (`SandBox`), 5 NRE |
| F5 | Agent 5 traces 8 and 9, Agent 6 | LOW | The `.ai/verification.md` no-game recipe fails as written; the RefAsm error names the failing step | CONFIRMED, fixed | `PackageDownload` is conditioned on RefAsm (`GameReferences.targets:53`); the recipe was replayed with `-c Release` added by hand (`r6-unit.log:1` tested `bin\Release`) and no gate run, not as written; convergence D1 rewrote it and replayed it as written |
| F6 | Agent 2 F3 | LOW | The missing-install error names only the Win64 layout | CONFIRMED, fixed | `Directory.Build.props:41` also accepts `Gaming.Desktop.x64_Shipping_Client` |
| F7 | Agent 5 trace 11 | LOW | "a skip fails" holds only for Inconclusive; an `[Ignore]`d check passes the gate | CONFIRMED, fixed | `binding-gate.runsettings` maps Inconclusive only; the `[Ignore]` path itself not run (UNVERIFIED) |
| F8 | Agent 1 L4, Agent 4, Agent 5 trace 13 | LOW | CHANGELOG: "again", runs on `bannerlord-1.4.5`, no issue link | CONFIRMED, fixed | `gh issue view 421`: OPEN, "No CI verifies any PR: Build & Test is skipped, and nothing runs the Python suite" |
| F9 | Agent 4 | LOW | `feature-map.md:115,117` stale | CONFIRMED, fixed | rows read at `b8c00045` |
| P1 | Agent 2 process note | n/a | The executor bypassed the plan's Step 6 STOP for the `System.Numerics.Vectors` load failure | FALSE POSITIVE | `E:\repos\TAOM\plans\010-ci-on-hosted-windows.md:54`: "Amendment 2 (orchestrator, 2026-09-24, after the second execution stopped at Step 6)" authorizes it |
| P2 | Agent 5 trace 5 | LOW | RefAsm test output lacks `System.Management`, `Steamworks.NET`, `GalaxyCSharp`, `StbSharp` | No defect today (no test loads them); the signature now appears in `tests.md` (F4) | grep in the agent report; no test names `GpuDisplayCollector` or `System.Management` |

The changes for F1 to F9 are summarized in the CHANGELOG entry
`fix(ci): v2.0.30 - review follow-ups for plan 010`. RCA: `docs/reviews/rca-ci-on-hosted-windows-2026-09-24.md`.

## DETAILS

The six lens reports and the Codex output were passed to the review lead verbatim. Their
verifiable claims are summarized here; everything not listed below matched the worktree.

- **Agent 1 (Standards).** No banned construct, service locator or naming problem. The 122 tagged
  files only gained `[TestCategory]` lines (142). Commit subject, CHANGELOG heading and counts
  (103, 29, 10; 8,183 and 338 executed; floors 7,700 and 320) match the logs. Findings L1 to L4 are
  F2, F3, the `_TaomNuGetRoot` proposal and F8.
- **Agent 2 (Engine compatibility).** The BUTR packages carry `buildId:25302170`, the installed
  `appmanifest_261550.acf` build. File sets, assembly identities and member metadata match the
  install for all 68 DLLs except a `NoInlining` implementation flag. Every stub body is
  `ldnull; throw`. Findings F1, F2 (here F4) and F3 (here F6).
- **Agent 3 (Efficiency).** No runtime C# changed. Local build 7.38 s, unit 15 s, gate 1 s.
  NuGet caching, `paths-ignore` and `concurrency` were measured and rejected.
- **Agent 4 (Completeness).** Tests pass the rules; no feature doc needed for CI infrastructure.
  Missing at review: the #421 link, the feature-map rows, the HintPath gap, the failure signatures.
- **Agent 5 (Data flow).** Install mode is byte-identical in references. Gaps: trace 4 (unused bin
  copies), trace 8 (recipe), trace 12 (a stale comment outside the diff). Inconsistencies: traces
  5, 6, 7, 9, 10, 11, 13.
- **Agent 6 (Design).** Eight KEEP proposals; see IMPROVEMENTS.

## ACTION ITEMS

1. Before the first push of this branch onto current trunk: trunk commit `709649c3` added six test
   files (`Animalia{AttackService,Config,Wiring}Tests`, `ElephantLikeReachTests`,
   `MonsterSize{Service,Wiring}Tests`) that were never run against RefAsm. Replay plan Step 6 (a)
   and (b) on the merged tree with the game variables unset and tag any that fail (Agent 4
   follow-up 2; UNVERIFIED whether any fail). The executor's `scratch/010/manifest.txt:142` still
   holds the class row for `Patch86HideoutBossFightBindingTests`; re-running the tagger from it
   would restore the class tag that D45 moved. Replace that row with
   `method|TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs|PatchClasses_AreRegisteredInAllThreePlaces|RequiresGame`
   first.
2. After the first hosted run: record its totals and checkout and restore times (hosted run
   UNVERIFIED). Keep #421 open for its Python half (`build.yml`'s python-tests job still triggers on
   `bannerlord-1.4.5` only).
3. Port `csharp.yml` and `GameReferences.targets` to `bannerlord-1.4.5` with the 1.4.8 BUTR build
   (the plan's Port note). Decision 44 attaches a check: confirm a 1.4.8 install's
   `Modules\SandBoxCore\bin` holds no DLL before the port drops the SandBoxCore reference there
   (UNVERIFIED on this desktop, which has no 1.4.x install; a missing type would fail the port build
   with CS0246, not silently).

## IMPROVEMENTS (Step 4)

APPLIED:
- `GameReferences.targets:36-49`: one `_TaomNuGetRoot` property replaces seven
  `EnsureTrailingSlash` calls, and the install group builds the two BCL paths from `TaomGameBin`
  (Agent 1 L3, Agent 6). Proof: normalized `-getItem:Reference` snapshots of all three projects are
  IDENTICAL before and after in install mode, RefAsm mode with a trailing-slash package root and
  RefAsm mode without one (9 of 9 compared).
- `GameReferences.targets:93-104`: the RefAsm game folder no longer copies the TaleWorlds stubs and
  `System.Numerics.Vectors` into its `bin`; a `MakeDir` keeps the folder for the `Bannerlord.exe`
  marker (Agent 5 trace 4, Agent 6). Proof: after a clean RefAsm build the folder holds only
  `Bannerlord.exe` in `bin` and the four module folders, and the CI gate step replays
  `gate: total=338 executed=338 passed=338 failed=0`, as before.
- The HintPath test change, the recipe text and the error text (Agent 6 proposals 3 to 5) were
  applied as defect fixes F2 and F5.

NOT APPLIED:
- `Main/TAOM.csproj:52-55`, `GameReferences.targets:30,40-41`: deleting the SandBoxCore reference
  (Agent 6). Parity holds on v1.5.3, but the 1.4.8 port's SandBoxCore bin was not checked (the
  laptop holds that install); needs Mike or a port-side check.
- `TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs:29`: moving `RequiresGame`
  from the class to `PatchClasses_AreRegisteredInAllThreePlaces` would return two methods to the
  unit step (Agent 5 trace 7). Behaviour-changing (CI selection) and not run on stubs: needs Mike.
- `csharp.yml` unit step: setting `BANNERLORD_GAME_DIR` to `refasm-game` could recover some of the
  24 skipped tests (Agent 5 trace 10, Agent 4 follow-up 7). Behaviour-changing and unmeasured:
  needs Mike.
- Mike decided all three on 2026-09-24 (D44 to D46); outcomes are in "Maintainer decisions
  applied" below.

FOLLOW-UP (pre-existing code or text outside the diff; no issue filed, because these are one-line
edits for whoever lands the branch, or belong to another plan):
- `TAOM.Tests/Features/Enlistment/EnlistmentRosterSlotInvariantsTests.cs:18-21` still says "CI
  compiles no C# at all" (Agents 1, 2, 4, 5, 6).
- `.claude/skills/engine-bump/SKILL.md:150-153` should name `BannerlordRefAsmVersion` and
  `GameReferencesTargetsTests` (Agents 1, 4, 5; CLAUDE.md "write the gate, then one line naming it").
- `.github/workflows/build.yml:1,10-12`: the name "Build & Test" and the `DOTNET_*` env block no
  longer fit a workflow that runs no dotnet (Agents 1, 4, 5, 6).
- `.ai/verification.md:87-90` and `.claude/rules/provenance.md:74-75` still describe the old CI
  (Agents 1, 4, 5).
- `Directory.Build.props:34-35` and `Main/TAOM.csproj:19` comments are true only in install mode
  (single-owner files; Agents 1, 5).
- `TAOM.Tests/Features/HeroRace/LiveTableauRefTests.cs`: fails when run alone in install mode
  (`TypeInitializationException` wrapping `FileNotFoundException` for
  `TaleWorlds.MountAndBlade.View`, 3 of 3 failed in a filtered run here); it passes in the full
  suite only because an earlier class hooks the resolver. Pre-existing order dependence (Agents 2
  and 5 flagged it UNVERIFIED; confirmed here).
- `Directory.Build.props:23-24`: `TaomBuildStamp` defeats incremental builds (Agent 3 F1; plan 017).
- `csharp.yml:45`: sparse checkout without `Main/_Module/AssetSources` if the first hosted
  checkout is slow (Agent 3 F2).
- `docs/reference/rules-catalog.md:44` could mention the CI categories; the main-session memory
  line "CI never compiles C#" goes stale after the first green hosted run (Agent 1 F7, F8).

VERDICT: READY FOR COMMIT, set after the convergence pass and its fixes (see CONVERGENCE below).
When this section was first written the convergence pass had not run, so the verdict was premature.

Suite at the review-fix commit `a4b90e4d` (install mode, clean `bin` and `obj`): `Failed: 2, Passed: 10246, Skipped: 2,
Total: 10250`; the two failures are the known live-Armory tests
(`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`). CI replay with the game variables
unset: unit `total=8208 executed=8184 passed=8184 failed=0`, gate
`total=338 executed=338 passed=338 failed=0`. `reviewctl.py lint`: "Shared contract OK: 10 lanes";
`test_reviewctl` and `test_ai_documentation`: 47 tests OK; `lint_docs.py`: `dead_links: 0`,
`ai_dashes: 0`, `--fail-on-drift` exit 0. The CI replay above ran the `csharp.yml` steps in the
worktree (`r4-*.log`, Debug); the separate replay of the `.ai/verification.md` recipe (`r6-*.log`)
did not follow the recipe as written (D1 below). The final numbers are in CONVERGENCE.

## CODEX REVIEW

Codex (gpt-6-astra, reasoning ultra, 129,699 tokens) reviewed `2ca0805b..b8c00045` through git
objects: **0 P1, 0 P2, 2 P3.** Raw output:
`docs/reviews/raw/codex-adversarial-010-ci-on-hosted-windows-2026-09-24.md` (git-ignored). It
quoted vanilla code (`Vec2.cs:45-49`, `TeamAIComponent.cs:276-291`) and cross-referenced every
configuration value in a table. It independently caught the plan's wrong `net46` package recipe
and confirmed the executor's correction.

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 (test oracle narrower than its name) | MED | Yes, raised | The prefix check lets a same-label BUTR build through, and the pin file cannot tell builds apart (F1). The HintPath and conditional-import parts are F2. Fixed with the changeset assertion and the whole-element scan. |
| 2 | P3 (header overclaims) | LOW | Yes | `csharp.yml:3` "every test that can run without Bannerlord" (F3). Fixed. |

Known Suspects: Codex disputed 6 (1, 2, 6, 7, 8, 10), partly confirmed 1 (suspect 9, the same as
finding 1) and left 3 unverified (3 snapshots, 4 hosted restore, 5 how the compiler error was
handled). Checked here: suspect 3's executor snapshots `0-norm-*` and `11-norm-*` are identical for
all three projects, and this session's snapshot at `b8c00045` matches them; suspect 5's handling is
recorded as Amendment 2 (P1 above); suspect 4 stays UNVERIFIED until the first hosted run.

- **Confirmed bugs:** 1 and 2 above.
- **False positives:** none.
- **Design questions:** none from Codex.
- **Things Codex missed:** F4 to F9. It reviewed through git objects only and did not read the
  executor's logs (`6a.log`), the reviewer recipe in `.ai/verification.md` or the issue tracker.

| # | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|
| 1 | Pin test proves the version, not the build | Logic error | Assumed one BUTR build per game version; the plan prescribed the prefix assertion | Changeset assertion; lesson in testing-qa |
| 2 | Workflow header overclaims | Other: doc claim | Written from the goal, not read back against `on:` and the filters | Repeat of the #647 `on:`-block lesson; fixed |

## AGENTS.md lessons (pending)

For the consolidated Phase 3h pass (not edited here, to avoid conflicts between branches):
- **What Codex does well:** caught a wrong package asset in the plan by reading PE metadata
  (the `net46` `System.Numerics.Vectors` forwarder), and tabulated every configuration value
  against its source of truth.
- **Bugs Codex typically misses:** claims in rules and reviewer docs that only a run log or an
  executed recipe can disprove (a failure-signature list, a no-game restore recipe), when it
  reviews through git objects without the executor's logs.

## CONVERGENCE

One `deep-reviewer` pass on the review-fix diff `b8c00045..a4b90e4d`: **5 LOW, 0 HIGH, 0 MED.**
Both applied improvements (the `_TaomNuGetRoot` property and the stub copies removed from
`refasm-game\bin`) were confirmed behaviour-preserving. Each defect was re-read against the
worktree before fixing; all five were confirmed, none was a false positive.

| # | Defect | Verdict | Evidence | Fix |
|---|---|---|---|---|
| D1 | The `.ai/verification.md` no-game recipe still could not be run as written: it built Release with the `managed-build` row, then sent the reader to the `csharp.yml` test commands, which run Debug and read `bin/Debug/net472/refasm-game` | CONFIRMED, fixed | `scratch/010r/r6-build.log:10` Release build; `r6-unit.log:1` tested `bin\Release\net472\TAOM.Tests.dll`, so `-c Release` was added by hand; no r6 gate log | The recipe now runs the `run:` blocks of all three `csharp.yml` steps (build, unit, gate) as written, all Debug, and says the gate path exists only for Debug. F5 above and the REVIEW-LOG line corrected |
| D2 | "Unset first" gave only a test-time reason, but the build writes `$(GameFolder)` into the test DLL's `TaomGameFolder` metadata, which `GameAssemblies.ResolveGameDir` falls back to | CONFIRMED, fixed | `Directory.Build.props:37-38`, `TAOM.Tests.csproj:41-48`, `GameAssemblies.cs:141-142` | The recipe says to unset both variables before the build and gives the reason |
| D3 | `tests.md` missed the `ReflectionTypeLoadException` from `Assembly.GetTypes()` | CONFIRMED, fixed | `scratch/010/6b.log:6,77` (`HarmonyFieldInjectionNamingTests.cs:49`); `6a.log` has none | Added to the signature list, noting that the test output does not print `LoaderExceptions` |
| D4 | Most of the guard's checks had no failing test; an `Import` under a conditional `ImportGroup` or `When` counted, and `$(gameFolder)` slipped past a case-sensitive match | CONFIRMED, fixed | `GameReferencesTargetsTests.cs:61,93,97` at `a4b90e4d` | One fixture row per rejected spelling: five install-property rows and five import rows. The import check now rejects a `Condition` on the Import or any ancestor and any `When` or `Otherwise` ancestor; property names match without case |
| D5 | The verdict was written before the convergence pass | CONFIRMED, fixed | line "No convergence `deep-reviewer` pass ran" under the verdict at `a4b90e4d` | Verdict now set from this pass; REVIEW-LOG Review 133 records it |

**RED first (D4).** With the new rows added, the property list cut to `$(GameFolder)` and the old
import check, 7 of 13 filtered tests failed: the three other install properties, `$(gameFolder)`,
and the `ImportGroup`, `When` and `Otherwise` rows (`scratch/010r/c1-red.log`). After the fix
13 of 13 passed (`c2-green.log`). Mutating the ancestor check to ignore `Condition` failed the
Import's own `Condition` row and the `ImportGroup` row (2 of 13, `c3-importmut.log`), so the
existing unconditional-import rule is now pinned by a test.

**Recipe replay, as written (D1, D2).** `scratch/010r/c-replay.py` copied every tracked and
unignored file of the worktree into a fresh folder with no `bin`, `obj` or `TestResults`, ran
`git init` (a checkout has a `.git`, and four source-reading tests locate the root by it), removed
`BANNERLORD_GAME_DIR` and `BANNERLORD_OVERRIDE_DIR` before anything ran, and executed the three
`run:` blocks read from that copy's `csharp.yml` under `pwsh`, in order (`scratch/010c/c-*.log`):
build exit 0 (Debug, the implicit restore reported `BANNERLORD_GAME_DIR` not set), unit
`total=8218 executed=8194 passed=8194 failed=0` against `bin\Debug\net472\TAOM.Tests.dll`, gate
`total=338 executed=338 passed=338 failed=0`. The first attempt without `git init` failed three
source-reading tests with "repo root (.git) not found", an artifact of the copy, not the recipe.

**Final checks.** Full suite (install mode): `Failed: 2, Passed: 10256, Skipped: 2, Total: 10260`;
the two failures are the known live-Armory tests (`TheElkItem_DeclaresTheScaleTheReachIsTunedFor`,
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`). `test_reviewctl` and
`test_ai_documentation`: 47 tests OK. `reviewctl.py lint`: "Shared contract OK: 10 lanes".
`lint_docs.py`: 0 dead links, 0 new dashes; its size warnings are on other rules.

Per the skill, this was the one convergence pass; its fixes do not open another design round.

VERDICT (after convergence): READY FOR COMMIT

## Maintainer decisions applied (2026-09-24)

Mike answered the three NOT APPLIED items above. All three outcomes are in one commit,
`fix(ci): v2.0.30 - apply maintainer decisions for plan 010`, on base `2897fcca`. Every number
below was measured in this worktree for this section (logs in `scratch/010/md/`), except the
convergence figures named as recorded. The CI replay
(`replay.py`) deleted the three projects' `bin` and `obj` folders and `TestResults`, removed
`BANNERLORD_GAME_DIR` and `BANNERLORD_OVERRIDE_DIR` from the environment before anything ran,
pointed `NUGET_PACKAGES`, `TEMP` and `TMP` at scratch folders on E: (C: is nearly full; the RefAsm
snapshots passed the same root as `-p:NuGetPackageRoot`, which `_TaomNuGetRoot` reads), and
executed the `run:` blocks of the `build`, `unit` and `gate` steps read from the worktree's
`csharp.yml` under `pwsh`, in order, from the worktree root.

| # | Decision | Outcome | Evidence |
|---|---|---|---|
| D44 | Delete the unused SandBoxCore reference | APPLIED | `Main/TAOM.csproj` Reference and both `TaomSandBoxCoreModuleBin` properties removed; the targets header now says the install paths leave out the empty `Modules\SandBoxCore\bin`. Normalized `-getItem:Reference` snapshots before and after: IDENTICAL for all three projects in install and RefAsm mode (6 of 6; Main 75, Dependencies 58, Tests 60 references). Install: `dotnet build Main/TAOM.csproj --no-incremental -p:DisableModuleCopy=true -p:ModuleId=` 0 errors; full suite `Failed: 2, Passed: 10256, Skipped: 2, Total: 10260`, the recorded totals, with the two known live-Armory failures. RefAsm replay build: `0 Error(s)`, no SandBoxCore type named. The 1.4.8 check the decision attached moves to action item 3 |
| D45 | Move `RequiresGame` from the Patch86 binding class to `PatchClasses_AreRegisteredInAllThreePlaces` | APPLIED | Replay unit step: `unit: total=8220 executed=8196 passed=8196 failed=0` (the convergence replay recorded 8,218 and 8,194); `AssaultPrefix_StillCallsPlanAssaultAndRemoveIf` and `AmbushPrefix_StillCallsPlanAmbush` both Passed on the reference assemblies. Gate `total=338 executed=338 passed=338 failed=0`. `.claude/rules/tests.md` now allows a method tag when the rest of the class runs on the stubs |
| D46 | Point the unit step's `BANNERLORD_GAME_DIR` at `refasm-game` | MEASURED, REVERTED | Without it: total 8,220, 8,196 passed, 24 skipped (NotExecuted), 0 failed. With `$env:BANNERLORD_GAME_DIR = Join-Path $PWD 'TAOM.Tests/bin/Debug/net472/refasm-game'` (the gate's path) as the first line of the unit `run:` block: total 8,220, 8,210 passed, 0 skipped, **10 failed**, step exit 1. Skips dropped by 24 but 10 new failures appeared, so the edit was reverted and `csharp.yml` is unchanged |

**D46 failures.** Five `Patch71FillTests` methods (`Fill_FallsBackToBattleGear_LeavesTheTargetsOwnEquipmentTypeAlone`,
`Fill_NoSlotEquipmentAndNoBattleGear_LeavesTheTargetUntouched`, `Fill_NullTarget_DoesNotThrow`,
`Fill_TargetIsTheCampaignWideSingleton_IsLeftUnwritten`,
`Fill_TemplateSuppliesTheSlot_CopiesTheSourceEquipmentType`) and five `TeamCombatantSelectorTests`
methods (`Select_EnemySide_TakesEveryCombatantOnThatSide`,
`Select_NoAllyTeam_PlayerTeamGetsTheWholeSide`, `Select_NullCombatant_IsSkipped`,
`Select_PlayerSideSplit_AllyTeamGetsTheUncommandedPartiesOutsideThePlayersArmy`,
`Select_PlayerSideSplit_BothTeamsRegisterWithTheSidesBestSkill`). Each threw
`System.NullReferenceException: Object reference not set to an instance of an object.` from a stub
constructor: `at TaleWorlds.Core.Equipment..ctor(EquipmentType equipmentType)` via
`Patch71FillTests.New` (line 30), and `at TaleWorlds.Core.BasicCultureObject..ctor()` via
`TeamCombatantSelectorTests.Combatant` (line 36). The other 14 of the 24 skips (in
`EconomyDiagnosticsPatchDiscoveryTests`, `HowdahCrewBehaviourCurvesTests`,
`HowdahCrewLookupBanTests`, `HowdahSeatReleaseTests`, `InformationManagerClearBanTests`,
`LotrIssueSuppressionTests`, `PartyOwnerGetterBanTests` and `SettlementEncounterInvariantTests`)
passed.

**Follow-up (not applied).** The ten failing methods carry no `RequiresGame` tag and pass the unit
step today only because `GameAssemblies` resolves no game there and they go Inconclusive. Tagging
`Patch71FillTests` and `TeamCombatantSelectorTests` `RequiresGame` would let a later change point
the unit step at `refasm-game` and recover the other 14; that is a new decision for Mike, not part
of D46.

**Other checks.** CHANGELOG: new entry `fix(ci): v2.0.30 - apply maintainer decisions for plan 010
(#421)`, and the base entry's heading now cites #421 (kept open for its Python half).
