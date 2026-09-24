# Cold review: plan 018 (re-cut), composition root first steps

**Reviewed:** `plans/018-composition-root-first-steps.md` as it stands in the working tree on
2026-09-24 (the re-cut; it differs from the copy committed at `4c728dac`). All code was read through
`git show 4c728dac:<path>` and `git grep 4c728dac`, never the working tree. Engine claims were
checked against the local v1.5.3 decompile cache (`~/.taom-src/v1.5.3`) and the installed DLLs.

**Verdict:** one blocking defect. The plan cannot be executed as written: the test project stops
compiling at Step 1.2 because of an `Arg` name clash. The fix is one line. Everything else checked
out, apart from the non-blocking items below.

## Blocking

### B1. `ModuleRunnerTests.cs` does not compile: `Arg` is ambiguous (CS0104)

Step 1.1 writes `TAOM.Tests/Composition/ModuleRunnerTests.cs` with both `using DryIoc;` and
`using NSubstitute;`, then calls `Arg.Is<string>(...)` (three times) and `Arg.Any<string>()` (once).
DryIoc 4.8.8 exports a public `DryIoc.Arg`, and NSubstitute exports `NSubstitute.Arg`, so each call
is CS0104.

Evidence:
- Reflection over `~/.nuget/packages/dryioc.dll/4.8.8/lib/net45/DryIoc.dll` lists exported type
  `DryIoc.Arg`.
- The repo has already hit this. `TAOM.Tests/Features/Enlistment/DischargeConsequenceServiceTests.cs:7-9`
  at `4c728dac` says: "DryIoc and NSubstitute both export an `Arg` type ... `using Arg = NSubstitute.Arg;`".
  No other test file uses both imports and calls `Arg.` without that alias.

What happens: TAOM.Tests is a single project, so the error breaks the whole test build. Step 1.2
GREEN ("13 passed") fails, and the executor reaches the STOP condition "The build reports ... any
error in the new files that the code in this plan does not explain". The Step 1.1 RED output will
also show CS0104 next to the CS0234/CS0246 errors the plan tells the executor to expect.

Fix: add `using Arg = NSubstitute.Arg;` to the Step 1.1 file (as the Discharge test does), and name
CS0104 in the Step 1.1 note. `WandererAllegianceWiringTests` (Step 2.2) also imports both
namespaces, but it never uses `Arg`, so it compiles.

## Non-blocking

1. **Step 2.3 item 1: the fragment to replace spans two lines.** The fragment ``and resolved by
   <c>SubModule.OnGameStart</c> for <c>AddBehavior</c> (the FieldCommission precedent)`` is written
   as one line. In `WandererAllegianceIoC.cs` at `4c728dac` it is split across lines 7-8:
   `... (the FieldCommission` / `/// precedent). Depends on ...`. An Edit using the one-line text
   will not match, and no verify command covers the summary edit. Quote both lines exactly, or give
   the whole new summary block.
2. **User count is out of date.** "`TAOM.Tests/Infrastructure/RepoPaths.cs` ... (4 users today)":
   at `4c728dac` five test files use `RepoPaths`. Those five are the three `using static` users,
   `ShippedFertilityConfigTests` and plan 009's `PatchCategoryApplierTests`. Cosmetic.
3. **TDD order for `NoticeFor`.** `FaultNotice_HoldsProcessLoad_InquiresAtMainMenu_AndUsesAChatLineInGame`
   is added after `FeatureModuleHooks.cs` already exists, so it never runs RED. To fix, move the
   test into Step 1.3 (it then fails to compile, which counts as RED), or accept this and say so.
   Steps 0.3 and 2.1 also have no RED. That is acceptable: 0.3 is a mechanical refactor, and 2.1's
   guards go RED in Step 2.3.
4. **A fail-closed rethrow during `IoC.Configure` gives the player no notice.** A future module
   with `OwnsSaveData = true` that throws in `RegisterServices` or `InitializeStatics` rethrows out
   of `IoC.Configure()`. That is `SubModule.cs:116`, inside `OnSubModuleLoad`, and per the
   `SubModule.cs:192-194` comment the game then does not start. The held-fault inquiry never runs,
   so the player sees nothing and only the `[Module]` log line records the cause. This does not
   break the startup inquiry rule, because nothing calls `DisplayMessage`. No module owns save data
   in this plan. It belongs in Maintenance notes for the first save-owning module.
5. **The runner's logger factory resolves during registration.** `() => container.Resolve<IModLogger>()`
   is called while registration is still running when a module faults. That is an eager resolve
   during registration. It is harmless today, because `FileLogger` has no contributor collection,
   but it is an exception to the "`IRegistrator` has no Resolve" story. Say so in the IoC comment.
6. **`FeatureModuleHooks.ReportFaults` swallows a notice failure silently.** 009's
   `ReportPatchFailures` logs `[PatchApply] failure notice not shown`. The runner version has a bare
   `catch` that logs nothing. The `[Module]` line already names the fault, so this is minor.
7. **The `ClearAllMessages` caller list is only partly verified.** Every cited line was confirmed in
   the local v1.5.3 cache: `GauntletInitialScreen.cs:77`, `GauntletUISubModule.cs:261`,
   `Module.cs:1785`, `MBGameManager.cs:48` and `GauntletQueryManager.cs:164-174`. Plan 009's startup
   report and this plan's module inquiry have different `Text`, so `InquiryData.HasSameContentWith`
   does not drop the second one as a duplicate. The claim that no other caller exists was not
   confirmed, because the cache holds only 408 decompiled types, not the whole engine. It is
   plausible but UNVERIFIED here.

## Checks that passed

- **Precondition at `4c728dac`:** `Main/PatchCategoryApplier.cs` exists. `ReportPatchFailures(`
  counts 4, `private bool TryPatchCategory(string category)` counts 1, and
  `ReportPatchFailures("startup", persistent: true);` counts 1.
- **Drift check:** `git diff --stat b2e387db..4c728dac -- <paths>` lists exactly ten files, summary
  `10 files changed, 168 insertions(+), 159 deletions(-)`. `git log` lists exactly `4c728dac`,
  `bdf7d515` and `45bcf80b`. The nine-test-file greps return `0` and `9`.
- **Current-state excerpts:** each of these matches `git show 4c728dac:<path>` at the stated lines,
  with `...` elisions marked:
  - `RepoPaths.cs` (whole file)
  - `CoopVetoClassificationTests.cs:304-312`
  - `GameModelOverrideBindingTests.cs:44-69, 153-164, 190-198`
  - `SubModule.cs:110, 203-205, 619-626, 638-648, 797-835, 810, 845, 851-872, 1063, 1279-1287, 1399-1408, 1483-1484, 1870-1877, 1919, 1927-1932, 1945-1950, 2029-2042`
  - `IoC.cs:80, 109, 178-214` and `RegisterCoreServices` after `Configure`
  - `WandererAllegianceIoC.cs` (body)
  - the two converted `WandererAllegianceWiringTests` methods
  - `wanderer-allegiance.md:179, 180, 199`
  - `TAOM.csproj:6, 100, 117-125`
  - `Directory.Build.props` (`net472`, `LangVersion 10.0`, `Nullable enable`)
  - the Patch80 (289-294), Patch82 (135-138) and SiegePropDiagnostics (18-23) before blocks
  - every Step 0.3 table row's read lines, guard lines and helper keep/delete decision (helper
    callers counted per file)
- **Counts:** at `4c728dac` the 26 files hold 42 old-style reads (32 SubModule, 10 IoC). The
  step-by-step arithmetic (41, then 33/10, then 32/10) is consistent. The only `new XxxModel(` that
  appears in raw text but not in the comment-stripped text is `TaomPartyNavigationModel`. Neither
  source file has a string literal containing `//` or `/*`. Stripping comments from both files
  left every string the 26 test files assert against SubModule or IoC text intact. 53 literals hit
  the raw text only, and I read the call site of each one that was not an obvious reflection name
  or path part: those assert on patch files, behavior files or reflection names, never on the
  SubModule or IoC text.
- **Kernel anchors:** each SubModule and IoC anchor that `AssertOnceBetween` uses occurs exactly once
  in the stripped text, in the order the test needs. `WandererAllegiance` appears in `SubModule.cs`
  only at 1403 and 1406, and in `IoC.cs` only at 109, so the `0`/`0` greps after Step 2.4 hold.
- **Compile surface:** checked everything except the `Arg` clash in B1, which did not pass:
  - No type named `ApplyPhase`, `FeatureState`, `ModelTarget`, `FaultNotice`, `FeatureModules`,
    `ModuleRunner` or `Composition` exists in Main, TAOM.Tests or the TaleWorlds Core, Library,
    CampaignSystem, MountAndBlade, Engine or Localization metadata.
  - `IResolver` and `IRegistrator` do not clash with any TaleWorlds type.
  - `IoCRegistrationDisciplineTests` matches the `Register\w+(...)` signature, not the parameter
    type, so the `IRegistrator` change is safe.
  - TAOM.Tests copies the TaleWorlds DLLs (`Private=True`), so building a `CampaignBehaviorBase`
    subclass in a test is safe.
  - The pilot's constructors need only the four faked services. The config provider falls back to
    defaults when the file is missing.
- **Engine facts:** `IGameStarter.AddModel<T>`, `CampaignGameStarter` and `BasicGameStarter.AddModel<T>`,
  `InformationManager.ShowInquiry` and `DisplayMessage`, and the `InquiryData` constructor match the
  v1.5.3 cache.
- **Merge note:** `709649c3` adds the Animalia and MonsterSize registrations after `IoC.cs:123`, the
  MonsterSize call before the game-init guard, and the Animalia mission behavior. None of these
  touches an anchor this plan uses.
- **Startup inquiry rule** (`lessons/localization-ui.md`, "Nothing receives a chat message before
  the initial screen"): the plan follows it.
  - Faults from `IoC.Configure` and `ApplyPhase.ProcessLoad` are held. `Hold` never calls
    `TakeFaultSummary`.
  - They are shown with `ShowInquiry` at TAOM's first `OnBeforeInitialModuleScreenSetAsRoot`,
    queued before 009's startup inquiry.
  - `DisplayMessage` is used only from `OnGameStart`, `GameInit` and mission start.
  - The runner's `ProcessLoad` call adds no `ReportPatchFailures(` to `OnSubModuleLoad`, so 009's
    gate stays green.
  - Two queued inquiries at startup are a documented deferral.
- **Commit gates:** the three subjects are 70, 69 and 70 characters with `v2.0.30`, which matches
  `SubModule.xml`. The CHANGELOG hook fires only on `.claude/`, `CLAUDE.md` or `AGENTS.md`, as the
  plan describes.

## Executability

A weak executor could follow the plan with only the plan and the repo. The one exception is B1:
the executor would stop at Step 1.2, correctly, under the plan's own STOP rule. Every step ends in
a command with an expected result, except the Step 2.3 summary edit (non-blocking item 1). The STOP
conditions are specific to this plan's risks. Once B1 is fixed, and ideally items 1 and 3, the plan
is ready to dispatch.
