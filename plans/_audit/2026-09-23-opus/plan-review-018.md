# Cold review: plan 018 (composition root, first steps)

Reviewed file: `plans/018-composition-root-first-steps.md` (1,884 lines) against
`.claude/skills/improve/references/plan-template.md` "Quality bar", with the code at `b2e387db`
(`git show b2e387db:<path>` for `Main/SubModule.cs` and `Main/IoC.cs`) and engine signatures from
`pwsh tools/taom-src.ps1 path <Type>` (v1.5.3 cache). The plan was not edited.

**Verdict:** not executable by a weak model as written. Two blocking defects, both in the Step 0.3
read checker's expected numbers. Each one either forces a STOP that the plan itself declares or
leaves a verification that cannot pass. Everything else checked out or is minor.

## Blocking

**B1. The checker's first-run count is 41, not 42 (plan lines 674 and 1859).**
Step 0.3 says to run `check_reads_018.py` "once at the start of this step, on the untouched files" and
expect `old-style reads: 42`, and to STOP otherwise. But `FILES` (line 653) includes
`TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`, and Step 0.2 (lines 497-507) has already
replaced its only old-style read (`ReadRepoFile("Main", "SubModule.cs")`, line 55 at `b2e387db`)
with `RepoPaths.ReadSource(...)`, which the checker skips (line 663). I exported all 26 files at
`b2e387db` and ran the same regex: 42 hits, and one of them is `GameModelOverrideBindingTests.cs:55`.
So at the start of Step 0.3 the checker prints 41 and a faithful executor stops.
Fix: expect 41 (31 SubModule, 10 IoC) at the start of Step 0.3, or tell the executor to run the
checker before Step 0.2.

**B2. The expected "new reads" count for SubModule is one short everywhere (lines 675, 1785, 1844).**
After Step 0.2, `GameModelOverrideBindingTests.cs` holds **two** occurrences of
`RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true)`: the converted read (line 506) and
the new `ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister` test (line 548). The other 25 files
hold 31 SubModule reads at `b2e387db` (table lines 575-599; I confirmed every row). So:

| Point | Plan expects | Actual |
|---|---|---|
| Step 0.3 end (line 675) | `Main/SubModule.cs: 32` | 33 |
| Step 2.4 (line 1785), after `SubModule_AddsTheDialogBehavior` is deleted | 31 | 32 |
| Done criteria (line 1844) | 31 | 32 |

The IoC count (10) is right at every point: Step 2.2 deletes one IoC read and adds one.
The executor gets a failed verification with nothing to fix. The likely outcomes are a STOP
("fails twice", line 1869) or harm: deleting or rewriting a test read to hit the number.
Fix: 33, 32, 32.

## Non-blocking

**N1. The drift check's expected result is a judgment (lines 22-26).** `git diff --stat` shows
file names and line counts. It cannot show that each of the nine test files changed "only in one
expected string", and it cannot tell plan 009's `SubModule.cs` edits apart from another commit's.
The other session's pending edits to both single-owner files (line 351) make that a real risk once
they are committed. Suggest adding two checks with fixed expected output:
`git log --oneline b2e387db..HEAD -- <same paths>` (expect only plan 009's commits) and
`git diff b2e387db..HEAD -- <the nine test files> | grep "^[-+] "` (expect only the
`_harmony.PatchCategory(` / `.PatchCategory(` to `TryPatchCategory(` swaps).

**N2. Rule A (line 567) does not name the text variable for the two inline reads.**
`Patch80KingdomVoteDeadlockBindingTests.cs:289-294` and
`Patch82MapEventObserverInvariantBindingTests.cs:135-138` pass `File.ReadAllText(subModule)` inline,
where `subModule` is the *path*. "Keeps the name the assertions use" does not help, because no
assertion names the text. Say it outright: `var subModule = RepoPaths.ReadSource("Main/SubModule.cs",
stripComments: true);` replaces the path line, and `File.ReadAllText(subModule)` becomes `subModule`.
For Patch80, also say to delete `var repoRoot = FindRepoRoot();` at line 289 (the table's
"289-291" implies it but does not say so).

**N3. The CHANGELOG-hook claim (line 350) is not accurate for a worktree.**
`.claude/hooks/check-changelog-changed.sh` runs `cd "${CLAUDE_PROJECT_DIR:-$(pwd)}"` and then
`git diff --cached`. In a session rooted at `E:\repos\TAOM`, that reads the *main tree's* index, not
the worktree's. If the other session has staged `.claude/` files there, the executor's commits get
denied even though they stage no `.claude/` path. The STOP at line 1868 handles this safely. The
orchestrator should expect it rather than read it as an executor error.

**N4. Step 2.1 is not RED.** The five generic tests pass trivially on the empty list (line 1674 says
so). That is acceptable, because Step 2.3 then shows the double-wiring guard going red on the pilot
(line 1772). Noted so nobody reads it as a TDD gap.

**N5. The `SyncData` IL-length heuristic (lines 1639-1647) depends on the build configuration.** An
empty body is `nop; ret` (2 bytes) in Debug and `ret` in Release, and `dotnet test` defaults to
Debug, so it holds here. A reviewer should know that a one-statement `SyncData` that persists
nothing (for example a guard plus `return`) would be misread as persisting data. This is a false
positive, which is the safe direction.

## Current-state excerpts vs `b2e387db` (item 3)

**Matched** (checked by reading the code): `SubModule.cs` is 2,148 lines; `IoC.cs` is 251 lines;
`IoC.cs:109` is the pilot registration, `:80` is `private static IContainer _container;`, 199-213 is
the tail of `Configure`, and 216 is `RegisterCoreServices`. `SubModule.cs`: 624 (Patch42), 626, 641-652
(Patch55 block), 801-839 (`OnGameStart` tail verbatim), 849 (`OnGameLoaded`, the kernel test's
"before" anchor, comes after `OnGameStart`), 1044 (the parked model comment), 1260-1268, 1384-1387
(pilot lines), 1443 (`SuppressAll`), 1464-1465, 1861-1869, 1911-1941, 2020-2033 (verbatim). DryIoc
4.8.8 is at `Main/TAOM.csproj:100` and `PathMap` at `:6`. `TAOM.Tests.csproj` and
`Directory.Build.props` contain no `PathMap` or `ContinuousIntegrationBuild`. RepoPaths has 4 users.
`WandererAllegianceIoC.cs`, the service constructor, `WandererAllegianceConfigProvider` (reads
`IPathService.ModuleDataPath`, falls back with `LogWarning`), the settings provider (no MCM read in
its constructor) and the dialog behavior (priority 110, empty `SyncData`) all match. So do the six
`WandererAllegianceWiringTests` and the doc lines 179, 180 and 199. `RegisterWandererAllegianceFeature`
is called only from `IoC.cs:109`. `companion_hire` appears only in the pilot's two lines.
`LotrIssueSuppression.cs:170-197` and `FieldCommissionIoC.cs:31` also match.
All 25 rows of the Step 0.3 table match: read lines, guard lines, and helper keep/delete verdicts
(no deleted helper has another caller). Of all `new X(` in SubModule, `TaomPartyNavigationModel` is
the only one present in raw text but not in stripped text. Neither kernel file has a string literal
containing `//` or `/*`. I simulated the stripped view: every literal the 26 files assert on
SubModule/IoC still occurs at least once after stripping. `ResetForUnloadSweepTests` (which builds
its strings dynamically) gives the same result raw and stripped (8 declaring classes, none missing).
Engine facts verified: `IGameStarter.AddModel<T>(MBGameModel<T>)`, `BasicGameStarter` and
`CampaignGameStarter` implement both overloads, `CampaignGameStarter.AddBehavior`, and
`CampaignBehaviorBase()` sets only `StringId`. No TaleWorlds DLL contains the names `ApplyPhase`,
`FeatureState`, `ModelTarget`, `FeatureModules`, `ModuleRunner` or `PatchCategoryDecl`, and no
`Composition` namespace or type exists in the repo, so the new `using` lines cannot create an
ambiguity.

**Mismatches** (all minor; none would mislead the executor):

1. Line 55 "whole file": the `RepoPaths.cs` excerpt leaves out the 4-line `/// <summary>` block
   (lines 7-10 at `b2e387db`). The Step 0.1 replacement keeps it, so this does no harm.
2. Line 79 `CoopVetoClassificationTests.cs:304-312`: the excerpt leaves out the 4-line doc comment
   at 307-310 between the field and `StripComments`.
3. Line 91 `GameModelOverrideBindingTests.cs:47-70`: the method signature is at line 46
   (`[TestMethod]` at 44; the body closes at 69).
4. Line 113 `DiscoverGameModels (153-172)`: the method spans 153-164. `ReadRepoFile` at 190-198 is
   correct.
5. Line 138 `Main/IoC.cs:176-214`: the excerpt starts at 178 (176-177 are StaleCharacterRepair and
   FiefGranting).
6. Line 162: `git grep -n "IoC.Configure()" -- TAOM.Tests` finds two comments, not one
   (`EconomyDiagnosticsWiringTests.cs:12`, `SiegeDismountWiringTests.cs:71`).
7. Line 136 `InternalsVisibleTo at Main/TAOM.csproj:117-122`: the ItemGroup spans 117-125; the
   `TAOM.Tests` attribute is at 119-121.
8. Line 190 `1380-1392`: the quoted excerpt spans 1380-1389.

The "after plan 009" anchors (lines 164-179) cannot be checked against code, because plan 009 has
not landed. They agree with plan 009's own text (`plans/009-guarded-patch-category-apply.md` lines
601, 605, 658, 662, 711-716 and 809: `private bool TryPatchCategory(string category)`, four
`ReportPatchFailures` calls plus the definition, which makes 5). The precondition (line 18) guards
this correctly.

## Item 4 checklist

| Check | Result |
|---|---|
| TDD order for C# | Yes: 0.1, 0.2, 1.1/1.2, 1.3/1.4, 2.2/2.3/2.4 each go RED before GREEN. 2.1 is test-after (N4). |
| Issue-first | Status line 36: the orchestrator creates the issue before implementation lands. Acceptable per the template. |
| Binding ADRs named | ADR-002, 003/004/005, 007, 008, csharp-architecture, simplicity-criterion, plan 009's source gate, localization (lines 281-288). |
| Single-owner files | Listed explicitly with exact permitted edits (lines 325-326, 48-49). The deletions are pinned by grep (lines 1781, 1846). |
| STOP conditions specific | Yes (lines 1856-1869). B1 turns line 1859 into a false STOP. |
| Done criteria machine-checkable | Yes, except that line 1844 has the wrong number (B2). |
| Planned-at SHA vs drift paths vs Scope | `b2e387db` is consistent. The drift list covers every in-scope path, including all 26 test files. `4b5662b2` (HEAD at planning) touches none of them (verified). |
| Non-deploying commands | Build and test carry `-p:DisableModuleCopy=true -p:ModuleId=` (lines 307-309, 1841-1843). `./build.ps1` is forbidden. |
| Commit subjects | 70, 69 and 70 characters; version `v2.0.30` matches `SubModule.xml`. |

## Item 5

No em or en dash appears in the plan's prose. The six hits (lines 101, 105, 144, 202, 501, 608) are
all inside code blocks that quote existing source verbatim, which is exempt. No secret values.

## Evidence notes

- The per-file read table and the 42 count come from a script run over `git show b2e387db:<file>`
  exports. The 41/33/32 figures follow from the plan's own Step 0.2 edits plus the checker's
  counting rules (lines 655-666). I did not run them against an executed worktree.
- The test-count arithmetic was rechecked: `T0` + 5, then + 17 (13 ModuleRunner, 4 FeatureModules),
  then + 6, for a total of +28; the Composition filter gives 22. The baseline of 10,239 was not
  re-measured.
