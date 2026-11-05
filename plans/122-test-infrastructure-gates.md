# Plan 122: Make the engine-binding gates fail instead of skipping, bind like Harmony, and pin transpiler sites, patch numbers and repo quality

> **Executor instructions**: Follow this plan step by step, in your own worktree (see "Git workflow").
> Run every verification command and confirm the expected result before moving on. If anything in
> "STOP conditions" occurs, stop and report; do not improvise. The plan has four Parts. Each ends with
> its own done criteria and is a clean hand-off point: Part A (binding gate strictness), Part B (new
> binding and wiring gates), Part C (repo quality gates, partly tool- and maintainer-gated), Part D
> (optional: the nullable ratchet). Do not edit `plans/README.md`: the integrator owns it during this
> programme. Put every count you measured in your hand-off.
>
> **Drift check (run first, in the worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- TAOM.Tests/Migration TAOM.Tests/TAOM.Tests.csproj TAOM.Tests/Composition TAOM.Tests/Infrastructure TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs TAOM.Tests/Features/SkeletonBuffer TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/Siege Main/ManualPatchApplicator.cs Main/Features/Siege Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj Directory.Build.props .editorconfig docs/reference/taleworlds-api-snapshot docs/reference/harmony-patch-registry.md docs/ai-includes/agent-operating-manual.md docs/migration/s6-runtime-punchlist.md docs/features/skeleton-buffer-guard.md docs/features/xml-merge-fast-path.md docs/reference/development-machines.md docs/reference/provenance-register.md .ai/verification.md .ai/routing.json .claude/skills/verify-bindings .claude/skills/engine-bump .claude/skills/verify .claude/rules/harmony-patches.md tools/tests docs/reference/hooks-catalog.md .claude/skills/build-fix .claude/skills/security-scan docs/ai-includes/code-quality.md docs/features/siege.md docs/features/siege-defense.md tools/oneoff/generate_rivendell_materials.py tools/oneoff/translate_all_remaining.sh tools/blender/spider_cfg.py`
> Earlier WPs of this programme changed several of these (WP104 the binding tests and RepoPaths, WP105
> `Main/Composition`, WP107 two `internal` keywords in `HarmonyPatchBindingTests.cs`, WP109 the native-pin
> tests and docs, WP115 `Directory.Build.props`, WP118 `docs/features/xml-merge-fast-path.md`, WP103 the dotnet
> command lines in docs). Compare the live
> files with "Current state" and STOP only on a real mismatch.

## Status

- **Priority**: P2
- **Effort**: L (four Parts; plan on two or three executor sessions; Part D is optional)
- **Risk**: LOW for Parts A to C (tests, test helpers, docs and tool gates; no game code). MED for
  Part D (one line of the single-owner `Main/TAOM.csproj`, nullable warnings become errors in
  `Main/Features/Siege`, and two small Siege behaviour changes).
- **Depends on**: `plans/104-guarded-patch-categories.md` (RepoPaths, the `a0db0558b` reader conversion,
  `PatchCategoryApplier`), `plans/115-release-integrity.md` (its B1 `Directory.Build.props` target,
  which Part C edits after) and `plans/118-campaign-load-time.md` (its opt-in `XmlMergeLiveEquivalenceTests`
  harness, which A1's `a27c6b338` runsettings hunk exists for, and `docs/features/xml-merge-fast-path.md`, which A1
  corrects). Through WP115 and WP118 this also needs WP101 to WP107, WP109 and WP112 merged.
- **Category**: tests
- **Wave / lane**: wave 2, lane E
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D2 (the patch-number
  gate reads `FeatureModules.All`), D8 (every subject carries `v2.0.28`), D9 (no CI; plan 010 and the RefAsm
  no-game gate `8589a3384` are not ported; plan 019 is the optional last Part), D10, D19 (git through the
  Bash tool only). Orchestrator notes for this WP (binding): plan 008 is ported (its runsettings make an
  unlisted native build fail the native pin gate instead of reading Skipped, V03), and the stricter
  Harmony-faithful gate from verify report D4 (WP-C) is included.
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04. (The 1.5.x ref has since moved; every 1.5.x SHA below is a commit, so it is stable.)
- **Issue**: port of #652 (plan 008), #792 (`ea8f2ad52`), #938 (`6ddcc8f9b`), #1020 (`affcee2ea`), #660
  (plan 019); the ViewModelMixin gate rode in #857 and #931 (gate only). New work (the Harmony-faithful
  resolver, D4 WP-C, and the 1.4.x rewrite of the transpiler-site gate): the orchestrator creates the issue
  before implementation lands.

## Why this matters

The 1.4.x line has no CI (`ccef172ad` removed it), so the engine-binding gate on the laptop is the only
thing that notices when a Bannerlord update moves an engine member a TAOM patch depends on. Today that
gate can be green while checking nothing, and it is weaker than Harmony itself:

- **It passes by skipping.** Ten `Assert.Inconclusive` guards across the gate (verify report D4, G7);
  MSTest reports Inconclusive as Skipped with exit 0. WP109's native pin tables read Skipped, not Failed,
  on an unlisted `TaleWorlds.Native.dll` build until this plan's runsettings exist (V03, claim C8).
- **It resolves targets differently from Harmony 2.4.2** (D4, G2). `HarmonyPatchBindingTests.cs:104-160`
  uses `AccessTools.Method`, which walks base types and falls back to the parameterless overload, and
  merges every attribute of a class into one spec; Harmony resolves each patch method's target on the
  declared type only and throws on an ambiguous declared name. Patch parameter names are not checked at
  all (G3), and the hand-applied CompanionTactics patch is outside the gate (G4).
- **Transpilers fail silent.** The line has nine transpiler files; every one returns the instructions
  unchanged when its anchor is gone, so the feature stops working with every gate green. None has a test
  that feeds it the installed IL (`GetOriginalInstructions` appears nowhere in `TAOM.Tests` on the tip; the
  2026-06-12 audit asked for exactly this test, `plans/_audit/2026-06-12-harvest.md:289`).
- **Drift the 1.5.x line already pins goes unnoticed here**: stale reflection-site line labels (9 of the
  45 labels on the tip point at the wrong line, checked this session), two features sharing a patch
  number, a `[ViewModelMixin]` refresh name that UIExtenderEx 2.13.2 turns into a startup NRE, and drifting
  private `IDataStore` fakes.

When this plan lands, one command (the strict binding gate) is red whenever the engine moved under a
patch, a transpiler, a reflection site or a mixin, and green only when everything was actually checked.
Part C turns rules that lived only in prose (ADR-003/004/005, Python bug classes, skill frontmatter, shell
hygiene, NuGet advisories) into tests. D4 checked the stricter rules offline against v1.4.8: all 229 patch classes, the 7
`ManualPatchApplicator` `TargetMethod` classes and the one `AccessTools.Method` target bind, so the gate
should start green; a red result is a real finding to report, never a number to adjust.

## Current state

### Files and roles (`origin/bannerlord-1.4.5` unless marked)

- `TAOM.Tests/Migration/GameAssemblies.cs`: loads the module DLLs for the binding tests. Equals the 1.5.x
  plan 008 base `7f02fc8d3` byte for byte (checked). Resolution doc `:19-21`; private parameterless
  `ResolveGameDir()` `:111-122` reads only `BANNERLORD_OVERRIDE_DIR` and `BANNERLORD_GAME_DIR`.
- `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` (194 lines): `EveryHarmonyPatch_ResolvesTargetMethod_AgainstInstalledEngine`
  `:37-76`; floor `:46-50` is an `Assert.Inconclusive`; `DiscoverPatchTypes` `:80-97`; `ResolveTarget`
  `:104-160` (`AccessTools.Method` `:139`, `PropertyGetter` `:142`); `MergeSpec` `:162-190`. After WP107,
  `DiscoverPatchTypes` and `MergeSpec` are `internal static` (shared with WP107's `Patch37TargetShapeTests`):
  keep both, with their signatures.
- `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`: after WP104 it equals `a0db0558b`'s blob (WP104
  Step 7 cherry-picks it onto a file that equalled `a0db0558b^`). Two floors are `Assert.Inconclusive`
  (`a0db0558b` lines 64-65 and 113-114; 1.4.x tip `:52-53`, `:79-80`).
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: 45 `[DataRow]`s on the tip; labels such as
  `"SubModule.cs:503 (manual patch)"` (`:45`) and `"CustomAttacksUtils.cs:55"` (`:55`); the DataRow method
  is `:106`. WP109 adds rows above `:106`.
- `TAOM.Tests/Features/XmlMerge/XmlMergeLiveEquivalenceTests.cs` (WP118): an opt-in harness tagged
  `BindingVerification`. It reads Inconclusive without `TAOM_RUN_BENCHMARKS=1`. `docs/features/xml-merge-fast-path.md`
  (WP118, its Step B1.7): its "On the 1.4.x line" block says `binding-gate.runsettings` is absent, and A1.5 corrects that.
- `TAOM.Tests/TAOM.Tests.csproj` (38 lines): MSTest 3.1.1, Microsoft.NET.Test.Sdk 17.8.0, Lib.Harmony 2.4.2
  (`:19`, runtime assets); no `TaomGameFolder` metadata. WP115 adds two `<Reference>`s in another ItemGroup.
- `TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs:197`: a private `FakeDataStore` that skips null
  on save and overwrites duplicates (the engine throws on a duplicate). The only other `IDataStore` fake is
  `RacePersistenceServiceTests.cs:723` `RoundTripDataStore`, which stays by design (it keys by type).
- `Main/ManualPatchApplicator.cs:19-96`: eight `harmony.Patch` calls. `:24-28` patches
  `OrderOfBattleHeroItemVM.GetCaptainTooltip` with `Patch35_OOBHeroItem_GetCaptainTooltip.Postfix`, a class
  with no `[HarmonyPatch]` and no `TargetMethod`, so the gate never sees it. The other seven classes have a
  `TargetMethod()` and are discovered.
- The nine transpiler files (all verified this session): `BannerColorPersistence/Hooks/Banner_TryGetBannerDataFromCode_Transpiler.cs`
  (Patch15, off by default since v1.4.7, self-bails), `.../CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.cs`
  (Patch23, public `Transpiler`, no logger), `CastleRecruitment/Hooks/Patch42_AiHourlyTick_Transpiler.cs` and
  `Patch42_FillSettlements_Transpiler.cs` (public `Transpiler` over internal `CastleAiTranspiler.SwapIsCastleGate(instructions, anchor, label, logger)`),
  `CharacterSelection/Patches/RefreshCharacterEntityAuxPatch.cs` (Late_Transpiler, private `Transpiler(instructions, ILGenerator)`),
  `Enlistment/Hooks/BehaviorComponent_OnBehaviorActivated_Transpiler.cs` (Patch66, public), `MarriageAlignment/Hooks/Patch81_MarriageClanDraw.cs`
  (private), `PartyIconScale/Hooks/Patch53_PartyIconScale.cs` over internal `PartyIconScaleTranspiler.Rewrite(instructions, getScale, logger)`,
  `RaceAge/Hooks/DeliverOffSpring_RaceAssert_Patch.cs` (Patch13, private). WP109 adds the Patch103 transpiler,
  already pinned on installed IL by its own `MissionStartGuardBindingTests`; this plan does not duplicate it.
- `Main/TAOM.csproj:9` and `Dependencies/TAOM.Dependencies.csproj:9`: `<NoWarn>` carries the seven nullable ids
  (Part D). `Directory.Build.props`: after WP115 B1 it equals `9ca99437f`'s, with `<Nullable>enable</Nullable>`
  then a blank line and the build-identity comment at the top of the first PropertyGroup (Part C inserts there).
- On the tip there is no `#region`, no `[Obsolete]` and no `#if`/`#elif`/`#else`/`#endif` line in any `.cs`
  under `Main`, `Dependencies` or `TAOM.Tests` (`git grep` this session), so the banned-constructs gate starts green.

### Engine and library facts (verified this session; do not re-derive)

- Lib.Harmony 2.4.2 (`ilspycmd` on `~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll`):
  `public class PatchClassProcessor` with private fields `Dictionary<Type, MethodInfo> auxilaryMethods` and
  `List<AttributePatch> patchMethods`; its constructor only merges attributes (`containerAttributes.Merge` per
  patch method), nothing is patched. `internal class AttributePatch { internal HarmonyMethod info; internal HarmonyPatchType? type; }`.
  `internal static MethodBase PatchTools.GetOriginalMethod(this HarmonyMethod attr)` uses
  `AccessTools.DeclaredMethod` / `DeclaredPropertyGetter` / `DeclaredConstructor` and turns an
  `AmbiguousMatchException` into `HarmonyException("Ambiguous match for HarmonyMethod[...]")`.
  `PatchWithAttributes` throws `ArgumentException("Undefined target method for patch method ...")` on null.
  `GetBulkMethods`: `[HarmonyPatchAll]` first, then `TargetMethods` (a null element throws), then `TargetMethod`
  (null throws); `BulkPatch` refuses a patch method carrying a `methodName`, a non-Normal `methodType` or
  `argumentTypes`. `AccessTools.Method` walks base types and, on an ambiguous name, retries the parameterless
  overload; `AccessTools.DeclaredMethod` does neither. `MethodCreatorTools.EmitCallParameter`: injected names
  `__instance`, `__originalMethod`, `__args`, `__result`, `__resultRef`, `__state`, `__exception`,
  `__runOriginal`; `___name` binds `AccessTools.Field(original.DeclaringType, name)` (digits: `DeclaredField(type, index)`)
  else throws "No such field"; `__result` on a void original throws "Cannot get result from void method",
  on an unassignable type "Cannot assign method return type"; `__N` must parse and be in range; any other
  name must be an original parameter name or it throws `Parameter "x" not found in method ...`.
  `MethodCreator.AddPostfixes` passes `allowFirsParamPassthrough: true`, `AddPrefixes` and `AddFinalizers` pass
  false. `PatchProcessor.GetOriginalInstructions(MethodBase original, ILGenerator generator = null)` returns
  `List<CodeInstruction>`. `AccessTools.GetTypesFromAssembly(Assembly)` is public. TAOM uses no
  `[HarmonyArgument]`, `[HarmonyDelegate]`, `[HarmonyReversePatch]`, `[HarmonyPatchAll]`, `__args` or `__resultRef`
  (`git grep` on the tip).
- v1.4.8 (taom-src cache `C:\Users\mikew\.taom-src\v1.4.8`):
  - `CampaignBehaviorDataStore.BehaviorSaveData.SyncData<T>(string key, ref T data)` `:26-39`: save `_records.Add` `:28-32`
    (a duplicate throws), load `TryGetValue` `:33-38`; `LoadBehaviorData` `:86-106` calls `SyncData` only when a
    record exists. `IDataStore { bool IsSaving; bool IsLoading; bool SyncData<T>(string, ref T); }`.
  - `ClanPartyItemVM.UpdateProperties()` is `public void`, concrete (`ClanPartyItemVM.cs:1125`);
    `ClanPartyItemWithPartyVM` and `ClanPartyItemWithHeroVM` do not exist in the v1.4.8 dump.
  - `OrderOfBattleHeroItemVM`: `private List<TooltipProperty> GetCaptainTooltip()` `:305`, one overload.
  - Transpiler sites: `Banner.TryGetBannerDataFromCode(string, out List<BannerData>)` `:576`, and `RemoveRange`
    appears nowhere in `Banner.cs`; `CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero(...)` `:168`
    reads `hero.MapFaction` at `:172` and `:176`; `AiVisitSettlementBehavior.AiHourlyTick` `:137`, first `IsCastle`
    gate `:279` followed by `GetAvailableWageBudget`; `FillSettlementsToVisitWithDistancesAsDays` `:681`, gate `:706`
    followed by `IsSettlementSuitableForVisitingCondition`; `BodyGeneratorView.RefreshCharacterEntityAux()` `:650`,
    its one `new AgentVisualsData()` `:672`; `BehaviorComponent.OnBehaviorActivated()` `:103`, its one `ToString()`
    `:107`; `RomanceCampaignBehavior.CheckNpcMarriages(Clan)` `:109`, `Clan.All[MBRandom.RandomInt(Clan.All.Count)]`
    `:122`; `MobilePartyVisual.AddCharacterToPartyIcon(...)` `:1081`, `.Scale(0.3f)` `:1137`, `ScaleFactor * 0.3f`
    `:1197` (the `/ 0.3f` at `:1228` is a `div`, not matched); `HeroCreator.DeliverOffSpring(Hero, Hero, bool)` `:258`,
    its one `Debug.SilentAssert` `:260`.
  - Part D: `internal CampaignVec2 BesiegerCamp.GetSiegeCampPartyPosition(MobileParty, MatrixFrame[], MatrixFrame[])`
    `:305`, camp-1 index with no length check `:312-315`; callers `MobileParty.OnPartyJoinedSiegeInternal`
    (dump `MobileParty.cs:3209-3212`, dereferences `SiegeEvent.BesiegedSettlement.Town` at `:3211`) and
    `BesiegerCamp.SetPositionAfterMapChange` `:564` (`MapSceneWrapper.GetSiegeCampFrames(SiegeEvent.BesiegedSettlement, ...)`
    `:566`, the call `:595`).
- Bannerlord.UIExtenderEx 2.13.2 (`Main/TAOM.csproj:111`, a normal PackageReference in
  `Dependencies/TAOM.Dependencies.csproj:71`): `ViewModelMixinAttribute.RefreshMethodName` (`string?`). The five
  named-refresh mixins on the tip: `CharacterDeveloperCareerMixin` and `MessengerEncyclopediaMixin` (`RefreshValues`),
  `MissionAgentStatusCareerMixin` and `TimeAccelerationMixin` (`Tick`), `SpecialResourceMapBarMixin` (`Refresh`).
- On this laptop: the v1.4.8 `Modules/SandBoxCore/bin/Win64_Shipping_Client` folder is empty; PyYAML 6.0.3 is
  installed for `C:\Python314\python.exe`; Ruff, ShellCheck and Gitleaks are NOT installed (checked this session).

### Conventions that bind this work

- TDD: failing test first (RED, GREEN, REFACTOR). For a test-only gate port, prove it can fail (each step names
  how) before you rely on it.
- ADR-003 no `#region`; ADR-004 no `[Obsolete]`; ADR-005 no `#if DEBUG` outside the IoC registration; ADR-007
  adapters and ADR-002 thin entry points do not apply here (no production code in Parts A to C).
- `Main/IoC.cs` and `Main/SubModule.cs` are not touched by any Part. `Main/TAOM.csproj` is touched only in Part D
  (one line). `Directory.Build.props` and `.claude/settings*.json` are maintainer-only.
- `.claude/` and doc hunks: per the per-port procedure a code WP drops `.claude/` hunks except the lines that
  document the exact mechanism it ports. This WP keeps the `verify-bindings`, `engine-bump`, `verify` and
  `security-scan` skill lines and the two `harmony-patches.md` lines that describe the gates it adds; WP143
  reconciles the full rules later. `CHANGELOG.md`, `AGENTS.md`, `CLAUDE.md`, `docs/reviews/**`,
  `docs/reference/feature-map.md`, `docs/INDEX.md` and `tools/README.md` are never edited here.
- Never write an em or en dash in a line you write or edit (replace any on an edited line with a colon, comma
  or parentheses). Hyphens in flags and paths are fine.

## Commands you will need

Every command runs through the Bash tool as `cd /c/Users/mikew/source/repos/taom-port/122 && <command>`
(the shell does not keep `cd` between calls). Never `./build.ps1`.

| Purpose | Command | Expected |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId=` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>"` | `Failed: 0` |
| Plain binding gate | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification"` | `Failed: 0` |
| Strict binding gate (after A1) | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"` | `Failed: 0`, `Skipped: 0` |
| Tool tests | `python -m unittest discover -s tools/tests -t .` | no failure that Step 0 did not have |
| Docs | `python tools/lint_docs.py` | no new finding in a file you touched |
| Hooks | `bash tools/test_hooks.sh` | no new failure vs Step 0 (run after any `.claude/skills` edit) |

MSTest counts each `[DataRow]` as one result. "Total" below means the `Total tests` line.

## Scope

**In scope** (create or modify):
- Part A: `TAOM.Tests/Migration/{GameAssemblies,HarmonyPatchBindingTests,GameModelOverrideBindingTests}.cs`;
  new `TAOM.Tests/Migration/{GameAssembliesResolutionTests,BindingGateRunSettingsTests,HarmonyTargetResolver,HarmonyTargetResolverTests}.cs`;
  new `TAOM.Tests/binding-gate.runsettings`; `TAOM.Tests/TAOM.Tests.csproj` (one ItemGroup);
  `TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs` and
  `TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs` (`LoadInstalled` only);
  docs `docs/reference/taleworlds-api-snapshot/{README,reflection-sites}.md`, `docs/ai-includes/agent-operating-manual.md`,
  `docs/migration/s6-runtime-punchlist.md`, `docs/reference/hooks-catalog.md` (one row, conditional),
  `docs/features/skeleton-buffer-guard.md` (one sentence), `docs/features/xml-merge-fast-path.md` (one sentence);
  `.claude/skills/verify-bindings/SKILL.md`,
  `.claude/skills/engine-bump/SKILL.md` (one sentence).
- Part B: new `TAOM.Tests/Migration/{TranspilerSiteBindingTests,ViewModelMixinBindingTests}.cs`,
  `TAOM.Tests/Composition/PatchCategoryNumberTests.cs`, `TAOM.Tests/Infrastructure/EngineDataStore.cs` (unless
  WP110 brought it); `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`; `TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs`
  and any of the five `ea8f2ad52` session-reset files that exist; `docs/reference/harmony-patch-registry.md`
  (header line, plus missing `## <category>` sections only as B3 allows); the docs and skills listed in Part A;
  `.claude/rules/harmony-patches.md` (two lines).
- Part C: new `TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs`, `ruff.toml`,
  `tools/tests/{test_python_lint_gate,test_claude_config_lint,test_shell_lint_gate}.py`;
  `tools/oneoff/generate_rivendell_materials.py`, `tools/blender/spider_cfg.py`, `tools/oneoff/translate_all_remaining.sh`
  and other `tools/**` files only as C2/C3 allow; `.ai/verification.md`, `.ai/routing.json`,
  `docs/reference/development-machines.md`, `docs/reference/provenance-register.md`, `.claude/skills/verify/SKILL.md`;
  `Directory.Build.props`, `.gitleaksignore` and `.claude/skills/security-scan/SKILL.md` through the maintainer only.
- Part D: `.editorconfig`, new `Main/Features/Siege/.editorconfig`, `Main/Features/Siege/{SiegeDefenseService.cs,Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs,Models/ActiveSiegeDefenseEvent.cs,Models/KingdomSiegeMessages.cs,Models/SiegeDefenseConfig.cs}`,
  `Main/TAOM.csproj` (delete one line), `Dependencies/TAOM.Dependencies.csproj` (one line),
  `TAOM.Tests/Features/Siege/{SiegeCampGuardPatchTests,SiegeDefenseServiceTests}.cs`, new
  `TAOM.Tests/Infrastructure/NullableRatchetGateTests.cs`, `docs/ai-includes/code-quality.md`,
  `docs/features/{siege,siege-defense}.md`, `.claude/skills/build-fix/SKILL.md`, the `## Patch8_SiegeCampGuard`
  section of `docs/reference/harmony-patch-registry.md`.

**Out of scope** (do not touch):
- `Main/IoC.cs`, `Main/SubModule.cs`, every production patch, adapter and service outside Part D's Siege files.
- `Main/ManualPatchApplicator.cs` (wrapping its `harmony.Patch` calls is runtime work, deferred; see Maintenance notes).
- `tools/snapshot_api_surface.ps1`, `patch-targets.md`, `gamemodel-bases.md` and the README counts (WP137 owns
  verify report D4 WP-D); never use `snapshot_api_surface.ps1 -Check` as a gate here (it cannot exit 0 on 1.4.x, WP137's pack).
- CI workflows, `GameReferences.targets`, `RequiresGame`/`RequiresGameIL`/`LiveInstall` tags beyond what WP109 added (D9).
- `.claude/hooks/**`, `tools/test_hooks.sh`, `.claude/settings*.json`, `.claude/rules/{harness-facts,tests,hook-authoring}.md`
  (harness lane, WP143). `CHANGELOG.md` (D1), `AGENTS.md`, `CLAUDE.md`, `docs/reviews/**`, `tools/README.md`,
  `docs/reference/feature-map.md`, `docs/INDEX.md`.

## Git workflow

- Worktree (from the main checkout, Bash tool):
  `cd /c/Users/mikew/source/repos/TAOM && git worktree add /c/Users/mikew/source/repos/taom-port/122 -b port148/122-test-infrastructure-gates bannerlord-1.4.5`.
  Work only there. Never push, open a PR, tag, or merge into `bannerlord-1.4.5`.
- Per-port procedure (binding): apply a 1.5.x commit only with `git cherry-pick -n <sha>` (never let cherry-pick,
  `--continue` or `--amend --no-edit` create a commit: `GIT_EDITOR=true` on this laptop commits the 1.5.x message
  unedited); a series this plan names as an end-state transplant is copied from the stated commit instead. Drop
  bookkeeping hunks (`CHANGELOG.md` always; `docs/reviews/**`, `docs/reviews/lessons/*`, `tools/README.md`,
  `docs/reference/feature-map.md`): a conflicted one `git checkout --ours <path> && git add <path>`, a cleanly
  changed one `git checkout HEAD <path>`. Stage explicit paths (`git add <path> ...`, never `-A`), then run
  `git commit -F /c/Users/mikew/source/repos/taom-port/122-msg.txt` in the NEXT, separate Bash call. Build and
  test after each commit.
- Commit message template (write it to `/c/Users/mikew/source/repos/taom-port/122-msg.txt`, overwrite per commit):

```
<type>(<scope>): v2.0.28 - <description>      (at most 72 characters)

<Body for 1.4.x readers, wrapped at 72: what changed on the 1.4.8 line
and why. Never paste a 1.5.x body. No em or en dashes. No Co-Authored-By
or "Generated with" line.>

Research: <what was verified on v1.4.8 or in Harmony 2.4.2>
Not-tested: <what needs an in-game check, or "none (test-only change)">
Ported-from: <40-hex sha>     (one line per included 1.5.x commit; omit for new work)
```

  Where a 1.5.x commit's type and scope describe a feature this plan does not take (`8b9f0a233` chore(engine),
  `e7cdb2e96` feat(settlements)), the subject uses the type and scope given in the step instead.

## Steps

### Step 0: Base checks and baseline (no commit)

```bash
cd /c/Users/mikew/source/repos/taom-port/122 && git status --porcelain | wc -l && \
grep -q "public static string ReadSource" TAOM.Tests/Infrastructure/RepoPaths.cs && echo WP104 && \
test -f Main/Composition/FeatureModules.cs && echo WP105 && \
grep -q "internal static List<Type> DiscoverPatchTypes" TAOM.Tests/Migration/HarmonyPatchBindingTests.cs && echo WP107 && \
test -f TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs && echo WP109 && \
grep -q "TaomStampWorkingTreeState" Directory.Build.props && echo WP115 && \
test -f TAOM.Tests/Features/XmlMerge/XmlMergeLiveEquivalenceTests.cs && test -f docs/features/xml-merge-fast-path.md && echo WP118 && \
git diff --quiet 7f02fc8d3 HEAD -- TAOM.Tests/Migration/GameAssemblies.cs && echo GA-SAME
```

**Verify**: `0`, then `WP104`, `WP105`, `WP107`, `WP109`, `WP115`, `WP118`, `GA-SAME`. Any missing token: STOP.

Then run Restore, Build, Test, the Plain binding gate and Tool tests. Record T0 (Total), S0 (Skipped, expected 5:
`EveryAllowedRaceIsARealRegisteredRace`, the two ignored Warg tests and WP118's two harness tests, unless an earlier WP's hand-off says otherwise),
BV0 and BVS0 (the binding gate's Total and Skipped), and the Tool tests' failure list. Any `Failed:` above 0: STOP.
Also record `git grep -c "TestCategory(\"BindingVerification\")" -- TAOM.Tests | wc -l` (files, for your report).

---

## Part A: the binding gate fails instead of skipping, and binds like Harmony

### Step A1: Make the binding gate fail loudly on skips (plan 008 end state)

Ports `8c89e042f`, `549afffde`, `2ca0805bc`, `37306bca3`, `aa59f68ed`, `1f5047572`, and `a27c6b338`'s
runsettings hunk. Method: end-state
transplant. The series is linear on `7f02fc8d3` and its merge `0964b533b` resolved only bookkeeping
(`git diff-tree --cc 0964b533b` lists CHANGELOG, REVIEW-LOG and two lessons only), so `1f5047572` is the end
state, except the runsettings file and its test, which come from `a27c6b338`. Do not cherry-pick: the merge-tree simulation this session conflicts all six commits (in `CHANGELOG.md`,
review records, files the series itself creates, `tools/test_hooks.sh`, the notify hook or the CI file). The hook
and `test_hooks.sh` changes net to zero (`git diff 7f02fc8d3 1f5047572 -- .claude/hooks/notify-test-results.sh tools/test_hooks.sh`
is empty) and the CI step is out of scope (`.github/workflows/build.yml`, D9).

**A1.1 RED**:

```bash
cd /c/Users/mikew/source/repos/taom-port/122 && git show 1f5047572:TAOM.Tests/Migration/GameAssembliesResolutionTests.cs > TAOM.Tests/Migration/GameAssembliesResolutionTests.cs && git show a27c6b338:TAOM.Tests/Migration/BindingGateRunSettingsTests.cs > TAOM.Tests/Migration/BindingGateRunSettingsTests.cs
```

**Verify**: Build fails, and every error names `ResolveGameDir` or `BuiltGameFolder` (CS0117, CS0122 or CS1501).

**A1.2 GREEN**:

1. `git show 1f5047572:TAOM.Tests/Migration/GameAssemblies.cs > TAOM.Tests/Migration/GameAssemblies.cs` (exact:
   GA-SAME held at Step 0).
2. `git show a27c6b338:TAOM.Tests/binding-gate.runsettings > TAOM.Tests/binding-gate.runsettings`, then in its
   comment replace `verify-bindings skill and CI pass this file to dotnet test explicitly.` with
   `verify-bindings skill passes this file to dotnet test explicitly.` (the 1.4.x line has no CI).
   Both this file and A1.1's test come from `a27c6b338`, not `1f5047572`: that commit adds `TAOM_RUN_BENCHMARKS=1`
   under `<RunConfiguration><EnvironmentVariables>` and a third pin, and no later 1.5.x commit touched either file.
   WP118's opt-in `XmlMergeLiveEquivalenceTests` harness is tagged `BindingVerification` and reads Inconclusive
   without that variable, so the strict gate would fail it. The third pin is one more test, and every Total in this
   plan already counts it.
3. `TAOM.Tests/TAOM.Tests.csproj`: insert directly above the final `</Project>` (tabs, as the file uses):

```xml
	<!-- The game folder this test DLL was compiled against. GameAssemblies falls back to it when the
	     test process has neither BANNERLORD_OVERRIDE_DIR nor BANNERLORD_GAME_DIR, so the binding
	     gate loads the install the build referenced instead of skipping. -->
	<ItemGroup>
		<AssemblyAttribute Include="System.Reflection.AssemblyMetadataAttribute">
			<_Parameter1>TaomGameFolder</_Parameter1>
			<_Parameter2>$(GameFolder)</_Parameter2>
		</AssemblyAttribute>
	</ItemGroup>
```

**Verify**: Build exit 0. Filtered `FullyQualifiedName~GameAssembliesResolutionTests|FullyQualifiedName~BindingGateRunSettingsTests`:
`Passed: 11`, `Failed: 0` (9 methods plus 2 data rows).

**A1.3 The discovery floors fail instead of skipping**:

1. `cd /c/Users/mikew/source/repos/taom-port/122 && git diff --quiet a0db0558b HEAD -- TAOM.Tests/Migration/GameModelOverrideBindingTests.cs && echo SAME`.
   If `SAME`: `git show 1efdf2985:TAOM.Tests/Migration/GameModelOverrideBindingTests.cs > TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`
   (1efdf2985's file is exactly `a0db0558b` plus plan 008's three hunks, checked this session). If not `SAME`,
   apply the three hunks by hand: (a) summary line `/// Binding-verification gate for TAOM's GameModel overrides (the ~37 <c>Taom*Model</c> classes).`
   becomes the two lines `/// Binding-verification gate for TAOM's GameModel overrides (the <c>Taom*Model</c> classes;`
   and `/// taleworlds-api-snapshot/gamemodel-bases.md has the current count).`; (b) in each of the two
   `if (models.Count < 20)` blocks, put `        // The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.`
   above the `if`, and change `Assert.Inconclusive(` to `Assert.Fail(`; in the first block also replace
   `(expected ~37)` with `(taleworlds-api-snapshot/gamemodel-bases.md has the current count)`.
   Either way, the two `Assert.Fail` lines keep the em dash the old messages carry before `assembly-load`, and the WP's dash done
   criterion counts added lines, so make it a colon:
   `sed -i 's/ \xE2\x80\x94 assembly-load/: assembly-load/' TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`.
   **Verify**: `grep -c ": assembly-load" TAOM.Tests/Migration/GameModelOverrideBindingTests.cs` prints `2`.
2. `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`, the floor in `EveryHarmonyPatch_ResolvesTargetMethod_AgainstInstalledEngine`:

```text
old:
        if (patchTypes.Count < 30)
            Assert.Inconclusive(
                $"Only {patchTypes.Count} [HarmonyPatch] types discovered (expected ~70). " +
new:
        // The game loaded, so a short discovery is a TAOM type-load failure: fail, never skip.
        if (patchTypes.Count < 30)
            Assert.Fail(
                $"Only {patchTypes.Count} [HarmonyPatch] types discovered (taleworlds-api-snapshot/patch-targets.md has the current count). " +
```

**A1.4 WP109's interim wording and code**: WP109 wrote "until WP122" text and a 1.4.x-only `LoadInstalled`.

1. In `SkeletonBufferInstalledBinaryTests.cs` and `ClipBudgetSignatureInstalledBinaryTests.cs`, replace the five
   lines that start `        // 1.4.x: GameAssemblies has no three-argument ResolveGameDir` and end with the
   `Assert.Inconclusive("No Bannerlord install folder resolved (BANNERLORD_OVERRIDE_DIR, BANNERLORD_GAME_DIR).");`
   line with the 1.5.x tip form:

```csharp
        var dir = GameAssemblies.ResolveGameDir(
            Environment.GetEnvironmentVariable("BANNERLORD_OVERRIDE_DIR"),
            Environment.GetEnvironmentVariable("BANNERLORD_GAME_DIR"),
            GameAssemblies.BuiltGameFolder);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            Assert.Inconclusive("No Bannerlord install folder resolved (BANNERLORD_OVERRIDE_DIR, BANNERLORD_GAME_DIR, built GameFolder).");
```

   (add `using System;` at the top of a file only if the build asks for it).
2. `cd /c/Users/mikew/source/repos/taom-port/122 && git grep -n "WP122\|WP 122" -- .claude docs TAOM.Tests Main`. Rewrite
   each hit to the post-WP122 truth and drop the WP number. Expected hits: `.claude/skills/engine-bump/SKILL.md`
   Phase 4 item 7 (make its sentence say an unlisted build reads Inconclusive, `a skip in the default suite and a
   failure in the TestCategory=BindingVerification gate, whose TAOM.Tests/binding-gate.runsettings maps
   Inconclusive to Failed`, and drop `so read the skip count`) and `docs/features/skeleton-buffer-guard.md`
   "After an engine bump" (make it `That is a skip in the default suite and a failure in the binding gate (its
   runsettings map Inconclusive to Failed); NativePinGateTests keeps the class tagged BindingVerification.`).
   A hit anywhere else: rewrite it the same way and list it in your hand-off.

**A1.5 Command lines and the skill**:

1. In each binding-gate command below, make the command read exactly as given (WP103 may already have added the
   two `-p:` flags; add `--settings` before `--filter`):
   - `.claude/skills/verify-bindings/SKILL.md` Step 1 code block, `docs/ai-includes/agent-operating-manual.md`
     "Engine-binding gate" row, `docs/migration/s6-runtime-punchlist.md` ("Run before touching the items below"),
     `docs/reference/taleworlds-api-snapshot/README.md` (the gate code block and the Maintenance bullet):
     `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"`
   - `docs/reference/taleworlds-api-snapshot/reflection-sites.md` code block:
     `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true -p:ModuleId= --settings TAOM.Tests/binding-gate.runsettings --filter "FullyQualifiedName~ReflectionSiteBindingTests"`
2. `verify-bindings/SKILL.md`: replace the whole Pre-flight paragraph with the `1f5047572` paragraph
   (`MSYS_NO_PATHCONV=1 git show 1f5047572:.claude/skills/verify-bindings/SKILL.md`, the paragraph after `## Pre-flight`).
   Replace the first sentence of Step 2 (it starts `A red gate is a real finding` and ends `do not silence the test:`)
   with the `1f5047572` Step 2 paragraph, changing its sentence that starts `` `Main/SubModule.cs not found` `` to:
   `` `Source file not found` is the same kind of precondition: RepoPaths resolves files from the path the test DLL
   was compiled at, so the tree moved after the build; rebuild and rerun, never edit code for it. `` Add the
   `1f5047572` first table row (`` `Only N [HarmonyPatch] types discovered` / `Only N GameModel subclasses discovered` ``)
   above the `AmbiguousMatchException` row. In `## Output`, insert `, the run's `Skipped:` count (it must be 0)`
   right after `(pass/fail with the per-finding class for any failure)`.
3. `docs/reference/hooks-catalog.md`: only if the `notify-test-results.sh` row still reads exactly
   `Summarizes `dotnet test` results prominently to stderr (`TEST RESULTS: PASSED/FAILED` with counts)`, replace its
   third cell with the `1f5047572` cell (`git show 1f5047572:docs/reference/hooks-catalog.md | grep notify-test-results`).
   Otherwise leave it and say so.
4. `docs/features/xml-merge-fast-path.md`, the "On the 1.4.x line" block WP118 wrote (find it with
   `grep -n "binding-gate.runsettings" docs/features/xml-merge-fast-path.md`): replace the clause that says this line
   has no `binding-gate.runsettings` (or that the file arrives later), through the words that say the harness runs on
   this line only with `TAOM_RUN_BENCHMARKS=1` set, with: `` `TAOM.Tests/binding-gate.runsettings` sets
   `TAOM_RUN_BENCHMARKS=1` for the gate's test host, so `/verify-bindings` runs the harness on this line too ``. Leave
   the rest of the block as it is.

**A1.6 Verify**:
- Build exit 0. Strict binding gate: `Failed: 0`, `Skipped: 0`, Total BV0 + 0. If any failure reads
  `Assert.Inconclusive failed`, see STOP conditions (the environment-gap rule).
- Proof the settings bite: (a) the Strict binding gate command with `--filter "TestCategory=NoSuchCategory"`
  exits non-zero and prints `No test matches`; (b) with `--filter "FullyQualifiedName~EveryAllowedRaceIsARealRegisteredRace"`
  it reports `Failed: 1` (that test is Inconclusive on the laptop, hardcoded `E:\` path), and the same filter
  without `--settings` reports `Skipped: 1`.
- Test: Total T0 + 11, `Failed: 0`, Skipped S0. Docs: no new finding. Hooks: no new failure.
- `grep -c "no .binding-gate.runsettings\|holds only with\|arrives with" docs/features/xml-merge-fast-path.md` prints `0`, and
  `grep -c "runs the harness on this line too" docs/features/xml-merge-fast-path.md` prints `1`.
- `git grep -n "WP122\|WP 122" -- .claude docs TAOM.Tests Main` prints nothing.

**Commit A1** (stage: the two new tests, the runsettings, `GameAssemblies.cs`, `TAOM.Tests.csproj`,
`GameModelOverrideBindingTests.cs`, `HarmonyPatchBindingTests.cs`, the two InstalledBinaryTests,
`docs/features/xml-merge-fast-path.md`, and every other doc and skill file you edited): `test(bindings): v2.0.28 - make the binding gate fail loudly on skips`.
Body: the binding gate passed by skipping whenever the test process lacked the game variables; MSTest reports
`Assert.Inconclusive` as Skipped with exit 0, so a run that checked nothing read green. `GameAssemblies` now falls
back to the game folder the test DLL was built against; `TAOM.Tests/binding-gate.runsettings` maps Inconclusive
to Failed, fails a filter that matches nothing and opts the gate in to the live XML merge harness; the discovery floors fail. Every documented gate command passes
the settings, and the native pin tables now fail on an unlisted engine build instead of skipping. Trailers:
`Research: MSTest 3.1.1 and Microsoft.NET.Test.Sdk 17.8.0, the versions plan 008 was proven with on 1.5.x`,
`Not-tested: none (test-only change)`, and one `Ported-from:` per commit (`8c89e042fdecce1c117e8121e98d24fbaf4ec26d`,
`549afffde2646f3fce7a142e1ae4d4d3d7bd17ea`, `2ca0805bcb34a76d6bac159a68f6bde1e2d32fe5`,
`37306bca39c6d6955ac15fc83b6123f9374e4490`, `aa59f68ed63b7da7cc3006d1f93170f5ca1c99b1`,
`1f504757290493c0220897e913eb924f9ba8d88f`, `a27c6b33817f6375f793b37c3892f897450e4bfe`).

### Step A2: Resolve and bind patch targets the way Harmony 2.4.2 does (new work, D4 WP-C; HasBody from `197454e68`)

`197454e68` added a "has a body" check to the old resolver and a negative pin on `ClanPartyItemVM.UpdateProperties`,
which is abstract on v1.5.0 but concrete on v1.4.8 (`ClanPartyItemVM.cs:1125`), so that pin would fail here; its
TroopWeight retarget names types v1.4.8 lacks. This step takes the check, pins it with game-free fixtures, and puts
it inside a resolver that follows Harmony's own code path.

**A2.1 RED**: create `TAOM.Tests/Migration/HarmonyTargetResolverTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.Tests.Migration;

/// <summary>
/// The binding gate's own logic, with no game. Each fixture is a patch class Lib.Harmony 2.4.2 refuses (or
/// accepts) when it applies it. Nothing is patched: Harmony only reads the fixtures' attributes and metadata.
/// </summary>
[TestClass]
public class HarmonyTargetResolverTests
{
    private class FixtureBase
    {
        public void InheritedOnly() { }
    }

    private class FixtureTarget : FixtureBase
    {
        private int _count;

        public int Count(int amount) => amount + _count;

        public void Act(int amount) => _count += amount;

        public void Over() { }

        public void Over(int value) => _count = value;
    }

    private abstract class FixtureAbstract
    {
        public abstract void Run();
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureBase.InheritedOnly))]
    private static class InheritedOnlyPatch { public static void Prefix() { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Over))]
    private static class AmbiguousPatch { public static void Prefix() { } }

    [HarmonyPatch(typeof(FixtureTarget))]
    private static class PerMethodPatch
    {
        [HarmonyPatch(nameof(FixtureTarget.Act))]
        public static void Prefix() { }

        [HarmonyPatch("Missing")]
        public static void Postfix() { }
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class WrongNamePatch { public static void Prefix(int notThere) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class MissingFieldPatch { public static void Prefix(int ___nope) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class ResultOnVoidPatch { public static void Postfix(object __result) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class ResultTypePatch { public static void Postfix(string __result) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Act))]
    private static class BadIndexPatch { public static void Prefix(object __3) { } }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class GoodPatch
    {
        public static void Prefix(object __instance, int amount, ref bool __state, int ____count, object __0,
            bool __runOriginal, MethodBase __originalMethod) { }

        public static void Postfix(ref int __result, bool __state) { }

        public static Exception? Finalizer(Exception? __exception) => __exception;
    }

    [HarmonyPatch(typeof(FixtureTarget), nameof(FixtureTarget.Count))]
    private static class PassThroughPostfixPatch { public static int Postfix(int result) => result; }

    [HarmonyPatch(typeof(FixtureAbstract), nameof(FixtureAbstract.Run))]
    private static class AbstractTargetPatch { public static void Prefix() { } }

    private static class TargetMethodNullPatch
    {
        public static MethodBase? TargetMethod() => null;

        public static void Prefix() { }
    }

    private static class TargetMethodsWithNullPatch
    {
        public static IEnumerable<MethodBase?> TargetMethods()
        {
            yield return AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act));
            yield return null;
        }

        public static void Prefix() { }
    }

    private static class BulkWithAnnotationPatch
    {
        public static MethodBase TargetMethod() => AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act));

        [HarmonyPatch(nameof(FixtureTarget.Act))]
        public static void Prefix() { }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetTickCount();

    private static void AssertOneProblem(Type patchClass, string expected)
    {
        var problems = HarmonyTargetResolver.Check(patchClass);
        Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
        StringAssert.Contains(problems[0], expected);
    }

    [TestMethod]
    public void Check_TargetDeclaredOnlyOnABaseType_IsUndefined_ThoughAccessToolsFindsIt()
    {
        Assert.IsNotNull(AccessTools.Method(typeof(FixtureTarget), nameof(FixtureBase.InheritedOnly)), "the old gate's lookup");
        AssertOneProblem(typeof(InheritedOnlyPatch), "Undefined target method");
    }

    [TestMethod]
    public void Check_NameOnlyTargetWithTwoDeclaredOverloads_IsAmbiguous_ThoughAccessToolsPicksOne()
    {
        Assert.AreEqual(0, AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Over)).GetParameters().Length, "the old gate's lookup");
        AssertOneProblem(typeof(AmbiguousPatch), "Ambiguous match");
    }

    [TestMethod]
    public void Check_EachPatchMethodIsResolvedAgainstItsOwnTarget() => AssertOneProblem(typeof(PerMethodPatch), "Postfix");

    [TestMethod]
    public void Check_ParameterNamedLikeNoOriginalParameter_IsReported() => AssertOneProblem(typeof(WrongNamePatch), "\"notThere\" not found");

    [TestMethod]
    public void Check_FieldInjectionOfAMissingField_IsReported() => AssertOneProblem(typeof(MissingFieldPatch), "'nope'");

    [TestMethod]
    public void Check_ResultOnAVoidOriginal_IsReported() => AssertOneProblem(typeof(ResultOnVoidPatch), "Cannot get result from void method");

    [TestMethod]
    public void Check_ResultOfAnUnassignableType_IsReported() => AssertOneProblem(typeof(ResultTypePatch), "cannot assign");

    [TestMethod]
    public void Check_IndexPastTheOriginalsParameters_IsReported() => AssertOneProblem(typeof(BadIndexPatch), "no parameter at index 3");

    [TestMethod]
    public void Check_InjectedNamesMatchingNamesFieldsAndIndexes_Bind()
    {
        var problems = HarmonyTargetResolver.Check(typeof(GoodPatch));
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void Check_PassThroughPostfixFirstParameter_IsNotReadAsAName()
    {
        var problems = HarmonyTargetResolver.Check(typeof(PassThroughPostfixPatch));
        Assert.AreEqual(0, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void Check_AbstractTarget_HasNoBody() => AssertOneProblem(typeof(AbstractTargetPatch), "has no body");

    [TestMethod]
    public void HasBody_ConcreteYes_AbstractAndExternNo()
    {
        Assert.IsTrue(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(FixtureTarget), nameof(FixtureTarget.Act))));
        Assert.IsFalse(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(FixtureAbstract), nameof(FixtureAbstract.Run))));
        Assert.IsFalse(HarmonyTargetResolver.HasBody(AccessTools.Method(typeof(HarmonyTargetResolverTests), nameof(GetTickCount))));
    }

    [TestMethod]
    public void Check_TargetMethodReturningNull_IsReported() => AssertOneProblem(typeof(TargetMethodNullPatch), "TargetMethod() returned null");

    [TestMethod]
    public void Check_TargetMethodsWithANullEntry_IsReported() => AssertOneProblem(typeof(TargetMethodsWithNullPatch), "null entry");

    [TestMethod]
    public void Check_TargetMethodWithATargetAnnotation_IsRefused() => AssertOneProblem(typeof(BulkWithAnnotationPatch), "refuses");

    [TestMethod]
    public void ResolveOriginals_ReturnsOnlyTheTargetsThatResolve()
    {
        var problems = new List<string>();
        var originals = HarmonyTargetResolver.ResolveOriginals(typeof(PerMethodPatch), problems);
        Assert.AreEqual(1, originals.Count);
        Assert.AreEqual(nameof(FixtureTarget.Act), originals[0].Name);
        Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
    }

    [TestMethod]
    public void HarmonyInternals_StillWhereTheResolverReadsThem()
    {
        Assert.AreEqual(new Version(2, 4, 2, 0), typeof(Harmony).Assembly.GetName().Version,
            "Lib.Harmony moved off 2.4.2: re-read PatchClassProcessor, AttributePatch, PatchTools.GetOriginalMethod and "
            + "MethodCreatorTools.EmitCallParameter in the new version, update HarmonyTargetResolver, then this pin.");
        Assert.IsNotNull(HarmonyTargetResolver.PatchMethodsField, "PatchClassProcessor.patchMethods");
        Assert.IsNotNull(HarmonyTargetResolver.AuxilaryMethodsField, "PatchClassProcessor.auxilaryMethods");
        Assert.IsNotNull(HarmonyTargetResolver.AttributePatchType, "HarmonyLib.AttributePatch");
        Assert.AreEqual(typeof(HarmonyMethod), HarmonyTargetResolver.AttributePatchInfoField?.FieldType, "AttributePatch.info");
        Assert.AreEqual(typeof(HarmonyPatchType?), HarmonyTargetResolver.AttributePatchKindField?.FieldType, "AttributePatch.type");
        Assert.AreEqual(typeof(MethodBase), HarmonyTargetResolver.GetOriginalMethod?.ReturnType, "PatchTools.GetOriginalMethod");
    }
}
```

**Verify**: Build fails with CS0103 naming `HarmonyTargetResolver`, and no other error.

**A2.2 GREEN**: create `TAOM.Tests/Migration/HarmonyTargetResolver.cs`:

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace TAOM.Tests.Migration;

/// <summary>
/// Resolves a patch class's targets and binds its patch parameters the way Lib.Harmony 2.4.2 does when it applies
/// the class, without patching anything. PatchClassProcessor merges the attributes per patch method;
/// PatchTools.GetOriginalMethod resolves an attribute target on the DECLARED type only (an ambiguous declared name
/// throws); TargetMethod or TargetMethods replace the attributes; MethodCreatorTools.EmitCallParameter binds every
/// prefix, postfix and finalizer parameter by name. AccessTools.Method, which the old gate used, walks base types
/// and falls back to the parameterless overload, so it passed targets Harmony cannot patch (verify report D4, G2).
/// Three Harmony internals are read by reflection; HarmonyTargetResolverTests pins them and the Harmony version.
/// </summary>
internal static class HarmonyTargetResolver
{
    private static readonly Assembly HarmonyAssembly = typeof(PatchClassProcessor).Assembly;

    internal static readonly FieldInfo? PatchMethodsField = AccessTools.Field(typeof(PatchClassProcessor), "patchMethods");
    internal static readonly FieldInfo? AuxilaryMethodsField = AccessTools.Field(typeof(PatchClassProcessor), "auxilaryMethods");
    internal static readonly Type? AttributePatchType = HarmonyAssembly.GetType("HarmonyLib.AttributePatch");
    internal static readonly FieldInfo? AttributePatchInfoField =
        AttributePatchType == null ? null : AccessTools.Field(AttributePatchType, "info");
    internal static readonly FieldInfo? AttributePatchKindField =
        AttributePatchType == null ? null : AccessTools.Field(AttributePatchType, "type");
    internal static readonly MethodInfo? GetOriginalMethod = HarmonyAssembly.GetType("HarmonyLib.PatchTools") is { } tools
        ? AccessTools.Method(tools, "GetOriginalMethod", new[] { typeof(HarmonyMethod) })
        : null;

    private static readonly Harmony Owner = new Harmony("taom.tests.bindinggate");

    // Names Harmony injects whatever the original's parameters are (InjectedParameter, 2.4.2).
    private static readonly HashSet<string> Injected = new HashSet<string>(StringComparer.Ordinal)
    {
        "__instance", "__originalMethod", "__args", "__state", "__exception", "__runOriginal",
    };

    /// <summary>Every problem Harmony would hit applying this patch class; empty when it binds.</summary>
    internal static List<string> Check(Type patchClass)
    {
        var problems = new List<string>();
        foreach (var (patch, kind, original) in Bind(patchClass, problems))
            problems.AddRange(CheckBinding(original, patch, kind));
        return problems;
    }

    /// <summary>
    /// The body and parameter checks for one patch method against one original. Also used for patches applied by
    /// hand with harmony.Patch, as ManualPatchApplicator does.
    /// </summary>
    internal static List<string> CheckBinding(MethodBase original, MethodInfo patch, HarmonyPatchType? kind)
    {
        var problems = new List<string>();
        if (!HasBody(original))
            problems.Add($"{Describe(original)} has no body (abstract or extern); Harmony cannot patch it");
        if (kind == HarmonyPatchType.Prefix || kind == HarmonyPatchType.Postfix || kind == HarmonyPatchType.Finalizer)
            problems.AddRange(ParameterProblems(patch, kind.Value, original));
        return problems;
    }

    /// <summary>The methods Harmony would patch for this class, without duplicates; problems as in Check.</summary>
    internal static List<MethodBase> ResolveOriginals(Type patchClass, List<string> problems) =>
        Bind(patchClass, problems).Select(b => b.Original).Distinct().ToList();

    // Resolving is not patching: an abstract or extern target resolves by name and leaves Harmony nothing to rewrite.
    internal static bool HasBody(MethodBase method) => !method.IsAbstract && method.GetMethodBody() != null;

    internal static string Describe(MethodBase method) =>
        $"{method.DeclaringType?.FullName}.{method.Name}({string.Join(", ", method.GetParameters().Select(p => p.ParameterType.Name))})";

    private static List<(MethodInfo Patch, HarmonyPatchType? Kind, MethodBase Original)> Bind(Type patchClass, List<string> problems)
    {
        if (PatchMethodsField == null || AuxilaryMethodsField == null || AttributePatchInfoField == null
            || AttributePatchKindField == null || GetOriginalMethod == null)
            throw new InvalidOperationException("Lib.Harmony internals moved: see HarmonyTargetResolverTests.HarmonyInternals_StillWhereTheResolverReadsThem.");

        var bound = new List<(MethodInfo, HarmonyPatchType?, MethodBase)>();
        if (patchClass.GetCustomAttributes(true).Any(a => a.GetType().FullName == "HarmonyLib.HarmonyPatchAll"))
        {
            problems.Add("[HarmonyPatchAll] is not modelled by this gate");
            return bound;
        }

        PatchClassProcessor processor;
        try
        {
            processor = new PatchClassProcessor(Owner, patchClass);
        }
        catch (Exception ex)
        {
            problems.Add("Harmony cannot read the class: " + Unwrap(ex));
            return bound;
        }

        var patches = ((IEnumerable)PatchMethodsField.GetValue(processor)).Cast<object>()
            .Select(p => ((HarmonyMethod)AttributePatchInfoField.GetValue(p), (HarmonyPatchType?)AttributePatchKindField.GetValue(p)))
            .ToList();
        var auxiliary = (IDictionary)AuxilaryMethodsField.GetValue(processor);

        if (auxiliary.Contains(typeof(HarmonyTargetMethods)) || auxiliary.Contains(typeof(HarmonyTargetMethod)))
        {
            var originals = BulkTargets(auxiliary, problems);
            foreach (var (info, kind) in patches)
            {
                if (info.methodName != null || (info.methodType.HasValue && info.methodType.Value != MethodType.Normal) || info.argumentTypes != null)
                    problems.Add($"{info.method.Name}: Harmony refuses TargetMethod or TargetMethods combined with a target annotation");
                foreach (var original in originals)
                    bound.Add((info.method, kind, original));
            }
            return bound;
        }

        foreach (var (info, kind) in patches)
        {
            MethodBase? original;
            try
            {
                original = (MethodBase?)GetOriginalMethod.Invoke(null, new object[] { info });
            }
            catch (Exception ex)
            {
                problems.Add($"{info.method.Name}: {Unwrap(ex)}");
                continue;
            }

            if (original == null)
                problems.Add($"Undefined target method for patch method {info.method.Name}: {info.declaringType?.FullName}.{info.methodName} ({info.methodType})");
            else
                bound.Add((info.method, kind, original));
        }
        return bound;
    }

    private static List<MethodBase> BulkTargets(IDictionary auxiliary, List<string> problems)
    {
        var result = new List<MethodBase>();
        try
        {
            if (auxiliary[typeof(HarmonyTargetMethods)] is MethodInfo many)
            {
                if (many.GetParameters().Length > 0)
                {
                    problems.Add($"{many.Name} takes parameters; this gate models a parameterless TargetMethods only");
                    return result;
                }
                if (many.Invoke(null, null) is IEnumerable sequence)
                {
                    var items = sequence.Cast<object?>().ToList();
                    if (items.Count == 0) problems.Add("TargetMethods() returned an empty sequence");
                    if (items.Any(i => i == null)) problems.Add("TargetMethods() contained a null entry (unresolved target)");
                    result.AddRange(items.OfType<MethodBase>());
                    return result;
                }
                if (!(auxiliary[typeof(HarmonyTargetMethod)] is MethodInfo))
                {
                    problems.Add("TargetMethods() returned null");
                    return result;
                }
            }

            if (auxiliary[typeof(HarmonyTargetMethod)] is MethodInfo one)
            {
                if (one.GetParameters().Length > 0)
                {
                    problems.Add($"{one.Name} takes parameters; this gate models a parameterless TargetMethod only");
                    return result;
                }
                if (one.Invoke(null, null) is MethodBase target)
                    result.Add(target);
                else
                    problems.Add("TargetMethod() returned null: the target is not in the installed engine");
            }
        }
        catch (Exception ex)
        {
            problems.Add("TargetMethod or TargetMethods threw: " + Unwrap(ex));
        }
        return result;
    }

    private static IEnumerable<string> ParameterProblems(MethodInfo patch, HarmonyPatchType kind, MethodBase original)
    {
        if (UsesHarmonyArgument(patch))
        {
            yield return $"{patch.Name} uses [HarmonyArgument], which this gate does not model";
            yield break;
        }

        var parameters = patch.GetParameters().ToList();
        // A pass-through postfix returns the result type and takes it first (MethodCreator.AddPostfixes).
        if (kind == HarmonyPatchType.Postfix && patch.ReturnType != typeof(void) && parameters.Count > 0
            && parameters[0].ParameterType == patch.ReturnType)
            parameters.RemoveAt(0);

        var originalParameters = original.GetParameters();
        var originalNames = originalParameters.Select(p => p.Name).ToArray();
        var returnType = original is MethodInfo method ? method.ReturnType : typeof(void);

        foreach (var parameter in parameters)
        {
            var name = parameter.Name ?? string.Empty;
            if (Injected.Contains(name))
                continue;

            if (name.StartsWith("___", StringComparison.Ordinal))
            {
                var field = name.Substring(3);
                var found = field.Length > 0 && field.All(char.IsDigit)
                    ? AccessTools.DeclaredField(original.DeclaringType, int.Parse(field))
                    : AccessTools.Field(original.DeclaringType, field);
                if (found == null)
                    yield return $"{patch.Name}: parameter {name} injects field '{field}', which {original.DeclaringType?.FullName} does not have";
                continue;
            }

            if (name == "__result")
            {
                if (returnType == typeof(void))
                {
                    yield return $"{patch.Name}: Cannot get result from void method {Describe(original)}";
                    continue;
                }
                var type = parameter.ParameterType.IsByRef && !returnType.IsByRef
                    ? parameter.ParameterType.GetElementType()!
                    : parameter.ParameterType;
                if (!type.IsAssignableFrom(returnType))
                    yield return $"{patch.Name}: cannot assign {returnType.FullName} to __result of type {type.FullName}";
                continue;
            }

            if (name == "__resultRef")
            {
                yield return $"{patch.Name}: __resultRef is not modelled by this gate";
                continue;
            }

            if (name.StartsWith("__", StringComparison.Ordinal))
            {
                if (!int.TryParse(name.Substring(2), out var index))
                    yield return $"{patch.Name}: parameter {name} does not contain a valid index";
                else if (index < 0 || index >= originalParameters.Length)
                    yield return $"{patch.Name}: no parameter at index {index} of {Describe(original)}";
                continue;
            }

            if (Array.IndexOf(originalNames, name) < 0)
                yield return $"{patch.Name}: Parameter \"{name}\" not found in method {Describe(original)}";
        }
    }

    private static bool UsesHarmonyArgument(MethodInfo patch) =>
        IsHarmonyArgument(patch.GetCustomAttributes(true))
        || patch.GetParameters().Any(p => IsHarmonyArgument(p.GetCustomAttributes(true)))
        || (patch.DeclaringType != null && IsHarmonyArgument(patch.DeclaringType.GetCustomAttributes(true)));

    private static bool IsHarmonyArgument(object[] attributes) => attributes.Any(a => a.GetType().Name == "HarmonyArgument");

    private static string Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException && ex.InnerException != null)
            ex = ex.InnerException;
        return ex.GetType().Name + ": " + ex.Message;
    }
}
```

**Verify**: Build exit 0 (nullable warnings in `TAOM.Tests` are allowed; errors are not). Filtered
`FullyQualifiedName~HarmonyTargetResolverTests`: `Passed: 17`, `Failed: 0`. A fixture that fails: fix the resolver
code against the Harmony facts in "Current state", never the fixture's expectation (each mirrors a Harmony throw).

**A2.3 Switch the gate**: in `TAOM.Tests/Migration/HarmonyPatchBindingTests.cs`:

1. In `EveryHarmonyPatch_ResolvesTargetMethod_AgainstInstalledEngine`, replace the body of the `try` block
   (`var (ok, detail) = ResolveTarget(t);` and the `if (!ok) failures.Add(...)` line) with:
   `foreach (var problem in HarmonyTargetResolver.Check(t)) failures.Add($"  {t.FullName}\n      -> {problem}");`
   (two lines, as the file formats). Change the header line to
   `sb.AppendLine($"{failures.Count} problem(s) across {patchTypes.Count} Harmony patch classes against the installed engine.");`
   and the second line to `sb.AppendLine("Each is a patch Harmony would refuse (or apply to nothing) when its category applies:");`.
2. Delete `ResolveTarget` and `Describe` (`Describe` has no other caller). Keep `DiscoverPatchTypes`,
   `HasStaticTargetMember`, `StaticParameterless` and `MergeSpec`, and put this comment above `MergeSpec`:
   `    // One merged spec per class, for Patch37TargetShapeTests. The gate resolves per patch method through HarmonyTargetResolver.`
3. Rewrite the class summary to say the gate resolves and binds every patch class exactly as Lib.Harmony 2.4.2
   applies it (through `HarmonyTargetResolver`) against the installed engine, plus the patches
   `ManualPatchApplicator` applies by hand; drop the "v1.4.5" wording there and in the failure header.
4. Add `using System.Text.RegularExpressions;` and, inside the class, the manual list and two tests:

```csharp
    // Patches ManualPatchApplicator applies with harmony.Patch from a class the discovery cannot find (no [HarmonyPatch],
    // no TargetMethod). Keep in step with Main/ManualPatchApplicator.cs; the source gate below fails when that file
    // names a patch class neither the discovery nor this list covers.
    private static readonly (string Target, string Method, Type PatchClass, string PatchMethod, HarmonyPatchType Kind)[] ManualPatches =
    {
        ("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleHeroItemVM", "GetCaptainTooltip",
            typeof(TAOM.Features.CompanionTactics.Roles.Hooks.Patch35_OOBHeroItem_GetCaptainTooltip), "Postfix", HarmonyPatchType.Postfix),
    };

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryManualPatch_BindsLikeHarmony()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var failures = new List<string>();
        foreach (var (target, method, patchClass, patchMethod, kind) in ManualPatches)
        {
            // Resolved exactly as ManualPatchApplicator resolves it.
            var original = AccessTools.Method(AccessTools.TypeByName(target), method);
            var patch = AccessTools.Method(patchClass, patchMethod);
            if (original == null) { failures.Add($"{target}.{method} did not resolve against the installed engine"); continue; }
            if (patch == null) { failures.Add($"{patchClass.FullName}.{patchMethod} not found"); continue; }
            failures.AddRange(HarmonyTargetResolver.CheckBinding(original, patch, kind).Select(p => $"{patchClass.Name}: {p}"));
        }

        Assert.AreEqual(0, failures.Count, string.Join("\n", failures));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ManualPatchApplicator_EveryPatchClass_IsCheckedByTheGate()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var source = TAOM.Tests.Infrastructure.RepoPaths.ReadSource("Main/ManualPatchApplicator.cs", stripComments: true);
        var named = Regex.Matches(source, @"new\s+HarmonyMethod\(\s*typeof\(([\w.]+)\)").Cast<Match>()
            .Select(m => m.Groups[1].Value.Split('.').Last()).Distinct().ToList();
        Assert.IsTrue(named.Count >= 8, $"Only {named.Count} patch classes found in ManualPatchApplicator.cs; the scan broke, it did not pass.");

        var covered = new HashSet<string>(DiscoverPatchTypes(out _).Select(t => t.Name));
        covered.UnionWith(ManualPatches.Select(m => m.PatchClass.Name));
        var missing = named.Where(n => !covered.Contains(n)).ToList();

        Assert.AreEqual(0, missing.Count,
            "ManualPatchApplicator applies patch classes this gate never checks: " + string.Join(", ", missing)
            + ". Give the class a TargetMethod() or add a ManualPatches row.");
    }
```

**Verify**: Build exit 0. Filtered `FullyQualifiedName~HarmonyPatchBindingTests|FullyQualifiedName~Patch37TargetShapeTests`:
`Failed: 0`. Strict binding gate: `Failed: 0`, `Skipped: 0`, Total BV0 + 2. If
`EveryHarmonyPatch_ResolvesTargetMethod_AgainstInstalledEngine` lists any problem: STOP (see STOP conditions).

**A2.4 Docs**:
1. `verify-bindings/SKILL.md` Step 1: change `Three test classes under `TAOM.Tests/Migration/`:` to
   `The test classes under `TAOM.Tests/Migration/`:` and the `HarmonyPatchBindingTests` bullet to:
   ``- `HarmonyPatchBindingTests`: every patch class resolved and bound the way Lib.Harmony 2.4.2 applies it (`HarmonyTargetResolver`: the attribute target on the declared type, or `TargetMethod`/`TargetMethods`; each prefix, postfix and finalizer parameter; a body to patch), plus the patches `ManualPatchApplicator` applies by hand.``
   Step 2 table: change the `AmbiguousMatchException` row's cause to
   `a name-only [HarmonyPatch] on a method with two overloads declared on the named type`, and add four rows:
   ``| `Undefined target method` | the target is declared only on a base type, or the engine removed it (Harmony looks up the declared type only) | name the declaring type in `[HarmonyPatch]`, or `/research` the member and retarget |``;
   ``| `has no body` | the target became abstract or extern; the work moved into overrides | patch the concrete overrides (`TargetMethods`) |``;
   ``| `Parameter "x" not found` / `injects field` / `Cannot get result from void method` / `no parameter at index` / `cannot assign` | a patch parameter no longer binds: the engine renamed a parameter or field, or changed the return type | read the original with `pwsh tools/taom-src.ps1 path <Type>` and fix the patch parameter |``;
   ``| `ManualPatchApplicator applies patch classes this gate never checks` | a new hand-applied patch | give the class a `TargetMethod()` or add a `ManualPatches` row in `HarmonyPatchBindingTests` |``.
2. `docs/reference/taleworlds-api-snapshot/README.md` item 1 under "Why this exists" (`**Harmony resolution ambiguity**`):
   rewrite the item, with no dash, as: `1. **Harmony resolution failures**: Harmony resolves an attribute target on the declared type only, so a target declared only on a base type does not resolve, and a name-only `[HarmonyPatch]` on a method with two overloads declared on the named type throws `AmbiguousMatchException` when the category applies. Either way the patch never applies. (The compiler and the BUTR analyzer both pass it: the member exists.)`
   Leave the counts and the HarmonyPatchBindingTests row's count to WP137.

**Verify**: Docs: no new finding. Hooks: no new failure. Test: Total T0 + 30, `Failed: 0`.

**Commit A2** (stage `HarmonyTargetResolver.cs`, `HarmonyTargetResolverTests.cs`, `HarmonyPatchBindingTests.cs`, the
skill and README): `test(bindings): v2.0.28 - resolve patch targets the way Harmony does`. Body: the gate resolved
targets with `AccessTools.Method`, which walks base types and falls back to the parameterless overload, and merged
a class's attributes into one spec, so it could pass a patch Harmony refuses at apply time; it never checked patch
parameter names, and the hand-applied CompanionTactics tooltip patch was outside it. It now follows Harmony 2.4.2's
own path per patch method (PatchClassProcessor, GetOriginalMethod, TargetMethod rules, parameter binding, a body to
patch), covers the manual patches, and fails when ManualPatchApplicator gains a class it cannot see. Game-free
fixtures pin each rule and the Harmony internals it reads. Trailers: `Research: ilspycmd of 0Harmony 2.4.2
PatchClassProcessor, AttributePatch, PatchTools.GetOriginalMethod, MethodCreatorTools.EmitCallParameter; v1.4.8
ClanPartyItemVM.UpdateProperties is concrete (ClanPartyItemVM.cs:1125)`, `Not-tested: none (test-only change)`,
`Ported-from: 197454e68e40946cc322cd1ec58ee4e21185f5fa`.

### Part A done criteria

- [ ] `git log --format=%s bannerlord-1.4.5..HEAD` lists the A1 and A2 subjects, each with `v2.0.28 - ` and at most 72 characters.
- [ ] Strict binding gate: `Failed: 0`, `Skipped: 0` (or only the environment gaps you listed).
- [ ] Test: Total T0 + 30, `Failed: 0`, Skipped S0.
- [ ] `git grep -n "ResolveTarget(" -- TAOM.Tests/Migration/HarmonyPatchBindingTests.cs` and
      `git grep -n "WP122\|WP 122" -- TAOM.Tests .claude docs Main` both print nothing.
- [ ] `git status --porcelain` is empty.

---

## Part B: transpiler sites, reflection labels, patch numbers, mixins, one data-store fake

### Step B1: Feed the installed IL through the nine transpilers (`8b9f0a233`, the test pattern only, rewritten)

`8b9f0a233` is the v1.5.2 compile adaptation; it binds 1.5.x-only types (`MobilePartyVisualHelper_GetHumanAgentPartyVisual_Patch`,
`Patch53_PartyIconScaleHumanVisual`, `PartyIconScaleTranspiler.RewriteIconSites`). Take only the idea.
Create `TAOM.Tests/Migration/TranspilerSiteBindingTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BannerColorPersistence.Hooks;
using TAOM.Features.CastleRecruitment.Hooks;
using TAOM.Features.CharacterSelection.Patches;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.MarriageAlignment.Hooks;
using TAOM.Features.PartyIconScale;
using TAOM.Features.PartyIconScale.Hooks;
using TAOM.Features.RaceAge.Hooks;

namespace TAOM.Tests.Migration;

/// <summary>
/// Proves TAOM's transpilers still find their IL sites in the installed engine (the 1.5.x pattern of 8b9f0a233,
/// rewritten for the 1.4.x transpilers). HarmonyPatchBindingTests proves a transpiler's target resolves; it cannot
/// see whether the rewrite still matches anything inside it. Every TAOM transpiler is fail-safe: with its anchor gone
/// it returns the instructions unchanged, so the feature stops while every other gate stays green. Each test reads
/// the real installed IL with PatchProcessor.GetOriginalInstructions, runs the production transpiler, and counts what
/// it changed. The expected counts are the v1.4.8 sites (taom-src, cited per test); after an engine bump a moved count
/// is a finding: read the new IL before touching a number. Patch103 (MissionStartGuard) is pinned the same way by its
/// own MissionStartGuardBindingTests. A new transpiler gets a test here, or a feature binding test that runs it over the
/// installed IL.
/// </summary>
[TestClass]
public class TranspilerSiteBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch15_BannerLayerCap_IsStillAbsent_SoTheDisabledTranspilerStaysCorrect()
    {
        RequireGame();
        var il = InstalledIl(Banner_TryGetBannerDataFromCode_Transpiler.TargetMethod(), "Banner.TryGetBannerDataFromCode");

        // v1.4.7 made banner layers unlimited; v1.4.8 Banner.TryGetBannerDataFromCode (Banner.cs:576) has no RemoveRange.
        Assert.AreEqual(0, il.Count(ci => ci.operand is MethodInfo mi && mi.Name == "RemoveRange"),
            "The engine trims banner layers again. Patch15_BannerLayerLimit is off by default and self-bails: enable "
            + "BannerColorConfig.EnableLayerLimitTranspiler only after reading the new IL, and give it a site test.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch23_NotificationCharacter_SwapsBothMapFactionReads()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler)),
            "CampaignSceneNotificationHelper.CreateNotificationCharacterFromHero");

        var output = CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.Transpiler(il).ToList();

        // v1.4.8 CampaignSceneNotificationHelper.cs:172 and :176 read hero.MapFaction for the two colours.
        Assert.AreEqual(2, Calls(output, nameof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler),
            nameof(CampaignSceneNotificationHelper_CreateNotificationCharacter_Transpiler.GetFactionOrClanFaction)));
        Assert.AreEqual(0, Calls(output, "Hero", "get_MapFaction"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch42_AiHourlyTick_SwapsTheRecruitmentIsCastleGate()
    {
        RequireGame();
        var il = InstalledIl(Patch42_AiHourlyTick_Transpiler.TargetMethod(), "AiVisitSettlementBehavior.AiHourlyTick");

        var output = Patch42_AiHourlyTick_Transpiler.Transpiler(il).ToList();

        // v1.4.8 AiVisitSettlementBehavior.cs:279: !settlement.IsCastle && item < 1f && GetAvailableWageBudget() > 0.
        Assert.AreEqual(1, Calls(output, nameof(CastleAiToggle), nameof(CastleAiToggle.IsCastleAndAiDisabled)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch42_FillSettlements_SwapsTheVisitIsCastleGate()
    {
        RequireGame();
        var il = InstalledIl(Patch42_FillSettlements_Transpiler.TargetMethod(), "AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays");

        var output = Patch42_FillSettlements_Transpiler.Transpiler(il).ToList();

        // v1.4.8 AiVisitSettlementBehavior.cs:706: !settlement.IsCastle && ... IsSettlementSuitableForVisitingCondition(...).
        Assert.AreEqual(1, Calls(output, nameof(CastleAiToggle), nameof(CastleAiToggle.IsCastleAndAiDisabled)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LateTranspiler_CharacterCustomization_InjectsTheRaceActionSet()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(RefreshCharacterEntityAuxPatch)), "BodyGeneratorView.RefreshCharacterEntityAux");
        var before = il.Count;

        var output = RunPrivateTranspiler(typeof(RefreshCharacterEntityAuxPatch), il, (object?)null);

        // v1.4.8 BodyGeneratorView.cs:672: the method's one new AgentVisualsData(); three instructions go in after it.
        Assert.AreEqual(before + 3, output.Count, "the face-generator action-set injection degraded to a no-op");
        Assert.AreEqual(1, Calls(output, nameof(RefreshCharacterEntityAuxPatch), nameof(RefreshCharacterEntityAuxPatch.GetActionSet)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch66_OrderBanner_SwapsTheOneToStringCall()
    {
        RequireGame();
        var il = InstalledIl(BehaviorComponent_OnBehaviorActivated_Transpiler.TargetMethod(), "BehaviorComponent.OnBehaviorActivated");

        var output = BehaviorComponent_OnBehaviorActivated_Transpiler.Transpiler(il).ToList();

        // v1.4.8 BehaviorComponent.cs:107 builds the banner from ToString(), the method's only ToString call.
        Assert.AreEqual(1, Calls(output, nameof(BehaviorComponent_OnBehaviorActivated_Transpiler),
            nameof(BehaviorComponent_OnBehaviorActivated_Transpiler.DescribeBehavior)));
        Assert.AreEqual(0, output.Count(ci => ci.operand is MethodInfo mi && mi.Name == "ToString" && mi.GetParameters().Length == 0));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch81_PartnerClanDraw_SwapsBothClanAllCalls()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(Patch81_MarriageClanDraw)), "RomanceCampaignBehavior.CheckNpcMarriages");

        var output = RunPrivateTranspiler(typeof(Patch81_MarriageClanDraw), il);

        // v1.4.8 RomanceCampaignBehavior.cs:122: Clan.All[MBRandom.RandomInt(Clan.All.Count)], two get_All calls.
        Assert.AreEqual(2, Calls(output, nameof(Patch81_MarriageClanDraw), nameof(Patch81_MarriageClanDraw.CandidateClansFor)));
        Assert.AreEqual(0, Calls(output, "Clan", "get_All"));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch53_PartyIcon_SwapsBothScaleLiterals()
    {
        RequireGame();
        var il = InstalledIl(Patch53_PartyIconScale.TargetMethod(), "MobilePartyVisual.AddCharacterToPartyIcon");
        var logger = Substitute.For<IModLogger>();
        var getScale = AccessTools.Method(typeof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale));

        var output = PartyIconScaleTranspiler.Rewrite(il, getScale, logger);

        // v1.4.8 MobilePartyVisual.cs:1137 .Scale(0.3f) (people) and :1197 ScaleFactor * 0.3f (mount).
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        Assert.AreEqual(2, Calls(output, nameof(PartyIconScaleConfig), nameof(PartyIconScaleConfig.GetScale)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch13_DeliverOffSpring_NopsTheRaceAssert()
    {
        RequireGame();
        var il = InstalledIl(SingleOriginal(typeof(DeliverOffSpring_RaceAssert_Patch)), "HeroCreator.DeliverOffSpring");
        var before = il.Count;

        var output = RunPrivateTranspiler(typeof(DeliverOffSpring_RaceAssert_Patch), il);

        // v1.4.8 HeroCreator.cs:260: the method's one Debug.SilentAssert (mother.Race == father.Race), NOPped in place.
        Assert.AreEqual(before, output.Count);
        Assert.AreEqual(0, Calls(output, "Debug", "SilentAssert"));
    }

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static List<CodeInstruction> InstalledIl(MethodBase? target, string what)
    {
        Assert.IsNotNull(target, what + " did not resolve against the installed engine.");
        return PatchProcessor.GetOriginalInstructions(target!);
    }

    // The one method Harmony would patch for an attribute-targeted class, resolved as Harmony resolves it.
    private static MethodBase SingleOriginal(Type patchClass)
    {
        var problems = new List<string>();
        var originals = HarmonyTargetResolver.ResolveOriginals(patchClass, problems);
        Assert.AreEqual(0, problems.Count, patchClass.Name + ": " + string.Join("; ", problems));
        Assert.AreEqual(1, originals.Count, patchClass.Name + " should patch exactly one method.");
        return originals[0];
    }

    private static List<CodeInstruction> RunPrivateTranspiler(Type patchClass, List<CodeInstruction> il, params object?[] extra)
    {
        var transpiler = AccessTools.Method(patchClass, "Transpiler");
        Assert.IsNotNull(transpiler, patchClass.Name + ".Transpiler not found.");
        var arguments = new object?[] { il }.Concat(extra).ToArray();
        return ((IEnumerable<CodeInstruction>)transpiler!.Invoke(null, arguments)!).ToList();
    }

    private static int Calls(IEnumerable<CodeInstruction> il, string declaringType, string method) =>
        il.Count(ci => (ci.opcode == OpCodes.Call || ci.opcode == OpCodes.Callvirt)
            && ci.operand is MethodInfo mi && mi.Name == method && mi.DeclaringType?.Name == declaringType);
}
```

Docs: `verify-bindings/SKILL.md` Step 1, add the bullet ``- `TranspilerSiteBindingTests`: the transpilers it lists, run over the installed engine's IL, still find their sites.``
and the Step 2 row ``| a `TranspilerSiteBindingTests` count is off | the engine IL moved under a fail-safe transpiler, so the feature silently stopped | read the new IL, re-anchor the transpiler, then set the count from the new site (cite its line) |``.
`README.md` gate table: add ``| `TranspilerSiteBindingTests` | The transpilers it lists, run over the installed IL, still find their sites (the disabled Patch15's site is still absent). | one test per listed transpiler |``.
`engine-bump/SKILL.md` Phase 4 item 1: change `every Harmony patch / GameModel / reflection site.` to
`every Harmony patch / GameModel / reflection site / transpiler site (`TranspilerSiteBindingTests`).` and replace
the dash after the bold `/verify-bindings` on that line with a colon. `.claude/rules/harmony-patches.md` `## Patch Types`:
append to the Transpiler bullet ` Every TAOM transpiler gets a test in TAOM.Tests/Migration/TranspilerSiteBindingTests.cs, or a feature binding test that runs it over the installed IL.`

**Verify**: Build exit 0. Filtered `FullyQualifiedName~TranspilerSiteBindingTests` with the strict settings:
`Passed: 9`, `Failed: 0`, `Skipped: 0`. Mutation check: change the Patch23 expected `2` to `3`, rebuild, the
filtered run shows `Failed: 1`; restore `2`, rebuild, `Passed: 9`. Docs and Hooks: no new finding. Test: Total
T0 + 39. A failing transpiler test (other than your mutation): STOP.

**Commit B1**: `test(migration): v2.0.28 - feed installed IL through nine transpilers`. Body: every TAOM transpiler
is fail-safe, so a missed anchor after an engine update silently disables its feature while the target-binding gate
stays green. Nine new binding tests read the real v1.4.8 IL, run each production transpiler, and assert the exact
number of sites it rewrote (or, for the disabled Patch15, that the banner-layer cap is still gone). Trailers:
`Research: taom-src v1.4.8 sites listed in each test`, `Not-tested: none (test-only change)`,
`Ported-from: 8b9f0a233043c66883bd0c2487eca3f98f8bcff8`.

### Step B2: Pin reflection-site labels to their lines (`ee19f1052`, adapted)

Method: hand port (the 1.5.x labels are 1.5.x lines; never copy them).

1. RED: in `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` add the usings `System.Collections.Generic`,
   `System.IO`, `System.Text.RegularExpressions` and `TAOM.Tests.Infrastructure`. Directly after the closing brace of
   `ReflectionSite_ResolvesAgainstInstalledEngine` (before `    // --- helpers ---`), insert lines 183 to 293 of
   `git show ee19f1052:TAOM.Tests/Migration/ReflectionSiteBindingTests.cs` (`sed -n '183,293p'`: the two label tests,
   `HasExactToken`, its test and `CatalogueRows`), then this helper:

```csharp
    private static List<string> SourceFilesNamed(string fileName)
    {
        // Main/ and Dependencies/: TAOM.Dependencies binds engine members by name too.
        var sep = Path.DirectorySeparatorChar;
        return new[] { "Main", "Dependencies" }
            .SelectMany(root => Directory.GetFiles(RepoPaths.RepoPath(root), fileName, SearchOption.AllDirectories))
            .Where(f => !f.Contains(sep + "obj" + sep) && !f.Contains(sep + "bin" + sep))
            .ToList();
    }

```

   In the inserted block change `exists under Main/.` to `exists under Main/ or Dependencies/.` and
   `no such file under Main/ (member` to `no such file under Main/ or Dependencies/ (member`.
   **Verify**: Build exit 0; Filtered `FullyQualifiedName~ReflectionSiteBindingTests.EveryLineLabel|FullyQualifiedName~ReflectionSiteBindingTests.EveryDataRow`
   shows both failing (on `1b00d1acd` this session: 9 stale labels, and no catalogue row for `_initialPlayerAgent`).
2. GREEN: for each stale label the message names the line(s) where the member now appears outside comments. Open the
   file there and choose the line that spells the member in the lookup (its string literal, `nameof`, or the name
   filter inside a `GetMethods` loop), never a declaration or a helper's name. Repoint the DataRow label and the
   matching `reflection-sites.md` Category B row together. On `1b00d1acd` the repoints were: `SubModule.cs:503 (manual patch)`
   to `ManualPatchApplicator.cs:24 (manual patch)`; `CustomAttacksUtils.cs:55` to `:60`; `BannerEditorView_OnTick_Patch.cs:21`
   to `:22`; `PartyScreenLogic_AddCommand_Patch.cs:71` to `:72`; `EncyclopediaUnitBadgeMixin.cs:31` to `:32`;
   `NavigationCacheAdapter.cs:94` and `:97` to `:404` and `:420`; `GuardsCampaignBehavior_TakeGuardAgentData_Patch.cs:30`
   to `:32`; `PlayerIdentityAdapter.cs:34` to `:35`. Also repoint `NavigationCacheAdapter.cs:88` and `:91` to `:374`
   and `:388`: the test passes them only because the helper names `FindCheckBeingNeighbor2Arg` and
   `FindGetCacheElementByRecord` contain the member name; the convention labels the `method.Name != "..."` filter.
   Earlier WPs may have moved lines: trust the test's message and your reading, not this list. Add the missing
   Category B row after the `RegisterBlow` row:
   ``| `TaleWorlds.MountAndBlade.Mission` | `_initialPlayerAgent` | field (private) | `ShaderPrecompilePlayerAgentGuard.cs:44` | ShaderPrecompilation deployment fallback, shader-precompile battles only: when no player agent set this field, the guard writes the first player-team agent into it, so the engine's deployment dereference of InitialPlayerAgent does not throw. Missing: the lookup returns null and the fallback cannot seed the field |``.
   In the reflection-sites.md **Maintenance** paragraph, insert after `When you change a site, update both.`:
   `Label the line that spells the member (its string literal, its `nameof`, or the name filter inside the lookup), not the first line of a multi-line call and not a declaration; `EveryLineLabel_PointsAtALineThatNamesItsMember` checks this with comments blanked, and `EveryDataRow_HasAMatchingRowInTheCatalogue` checks that every `[DataRow]` has its row here.`

**Verify**: Filtered `FullyQualifiedName~ReflectionSiteBindingTests`: `Failed: 0` (the strict settings too). Docs:
no new finding. Test: Total T0 + 42. A member that appears at no line of its file: STOP.

**Commit B2**: `test(migration): v2.0.28 - pin reflection-site labels to their lines`. Body: a reflection-site label
(File.cs:N) shows only in a failure message, so a wrong one stays wrong; 9 of the 45 had drifted and the
catalogue lacked one pinned row. Two game-free tests now check that each labelled line still spells its member and
that every DataRow has its catalogue row; the labels are repointed and the convention is written in the catalogue.
Trailers: `Not-tested: none (test-only change)`, `Ported-from: ee19f105269304b1fbdca1ca2cef088192f6a012`.

### Step B3: One patch number names one feature (`6ddcc8f9b`, adapted)

1. `git show 6ddcc8f9b:TAOM.Tests/Composition/PatchCategoryNumberTests.cs > TAOM.Tests/Composition/PatchCategoryNumberTests.cs`
   (D2: `FeatureModules.All` and `PatchCategoryDecl` exist after WP105). Edits: delete the
   `[TestCategory("RequiresGame")]` line (D9); `MinimumCategories = 100` to `90`, `MinimumRegistryHeadings = 100` to
   `90`, `MinimumFeatureMapMentions = 50` to `30` (a source-text scan of `1b00d1acd` this session found 92 declared
   categories, 95 `## ` registry headings and 34 feature-map mentions, every category with a heading, and only Patch63
   shared); empty the `NoRegistryHeading` initializer (delete its five 1.5.x
   entries, keep `{` and `};`). Keep `SharedNumbers` (`63`: BannerBearers, BlowDiagnostics, the only shared number on
   the tip) and both `RowCitesAnotherFeature` entries (both apply on the tip).
2. Build; Filtered `FullyQualifiedName~PatchCategoryNumberTests`. Expected `Passed: 4`. A failure is either a real
   duplicate (STOP) or a category an earlier WP added without a `## <category>` registry heading: if the 1.5.x
   registry (`git show origin/bannerlord-1.5.x:docs/reference/harmony-patch-registry.md`) has that section and its
   feature is the same, copy the section verbatim into the 1.4.x registry in number order and re-cite any engine line
   in it to v1.4.8 (or delete the citation); if it is a sub-category documented under its parent's section, add a
   `NoRegistryHeading` entry naming the parent; otherwise STOP.
3. `docs/reference/harmony-patch-registry.md`: after the paragraph that starts `> **Ground truth is the code:**` add
   a line `>` and then `> **Gate:** `PatchCategoryNumberTests` fails when one `PatchNNN` number names two features, when a category has no `## <category>` heading here, or when a feature-map row cites a patch number that is not its feature's.`
   `.claude/rules/harmony-patches.md` `## Patch Organization`: add the bullet
   `- A new patch number names one feature, and its category gets a `## <category>` heading in the registry: `PatchCategoryNumberTests` pins both.`
   Mutation check: rename the heading `## Patch13_RaceAge` to `## Patch13_RaceAgeX` in the registry (it must be a heading with a real category; the file is read at run time, so no rebuild), Filtered shows `Failed: 1`; restore.

**Verify**: Filtered `Passed: 4`; Docs: no new finding; Test: Total T0 + 46.

**Commit B3**: `test(patches): v2.0.28 - one patch number names one feature`. Body: a new test reads every patch
category (attributes and feature modules) and fails when a PatchNNN number names two features, when a category has
no registry heading, or when a feature-map row cites another feature's number; each scan has a floor so a broken
scan fails instead of passing. Patch63's two features stay listed by name. Trailers: `Not-tested: none (test-only change)`,
`Ported-from: 6ddcc8f9b3d097cfe8d31d203015c163a9346093`.

### Step B4: Gate every `[ViewModelMixin]` refresh name (`e7cdb2e96`, the gate only)

1. `git show e7cdb2e96:TAOM.Tests/Migration/ViewModelMixinBindingTests.cs > TAOM.Tests/Migration/ViewModelMixinBindingTests.cs`.
   Edits: delete `    [TestCategory("RequiresGameIL")]` (keep `BindingVerification`); `checkedCount >= 6` to `checkedCount >= 5`
   (the five tip mixins); in the summary delete the sentence from `<c>PrefabExtensionBindingTests</c>` through
   `the other half of a UI extension.` (that class does not exist on 1.4.x).
2. Docs: `README.md` gate table row ``| `ViewModelMixinBindingTests` | Every UIExtenderEx `[ViewModelMixin("Method")]` names one non-abstract method on its target view model. | one assertion accumulating all failures |``;
   `verify-bindings/SKILL.md` Step 1 bullet ``- `ViewModelMixinBindingTests`: every `[ViewModelMixin("Method")]` refresh name resolves to one non-abstract method on its target view model.``
   and Step 2 row ``| a `[ViewModelMixin]` refresh method `is not declared` | UIExtenderEx 2.13.2 throws a NullReferenceException at `UIExtender.Register` for a name that no longer resolves | look up the renamed method with `pwsh tools/taom-src.ps1 path <Type>` and update the attribute |``;
   `engine-bump/SKILL.md` Phase 4 item 1: append ` / `[ViewModelMixin]` refresh method (`ViewModelMixinBindingTests`)` before the final full stop of the list you edited in B1.

**Verify**: Build exit 0 (a CS0246 for `Bannerlord.UIExtenderEx` is a STOP; do not add packages). Filtered
`FullyQualifiedName~ViewModelMixinBindingTests` with the strict settings: `Passed: 2`. Mutation check: change `>= 5`
to `>= 6`, rebuild, `Failed: 1`; restore, rebuild, `Passed: 2`. Test: Total T0 + 48.

**Commit B4**: `test(migration): v2.0.28 - gate every ViewModelMixin refresh name`. Body: UIExtenderEx 2.13.2 throws a
NullReferenceException at UIExtender.Register for a refresh name the engine renamed, which takes TAOM's UI extensions
down at startup. A binding test resolves each of the five named refresh methods on its target view model the way
UIExtenderEx does and refuses a missing, overloaded or abstract one. Trailers: `Research: UIExtenderEx 2.13.2
ViewModelMixinAttribute.RefreshMethodName`, `Not-tested: none (test-only change)`,
`Ported-from: e7cdb2e96bae643e809c97df4edc9ce897da5a68`.

### Step B5: One engine-faithful data store fake (`ea8f2ad52`)

Method: `cd /c/Users/mikew/source/repos/taom-port/122 && git cherry-pick -n ea8f2ad52; git status --porcelain`.
Simulated this session onto `1b00d1acd`: conflicts only in the five session-reset and WarChronicle test files
(absent there). Resolve:
- A path absent in HEAD (status `DU`): `git rm -q -f -- <path>`.
- `TAOM.Tests/Infrastructure/EngineDataStore.cs` with status `AA` (WP110 created it): `git checkout --ours TAOM.Tests/Infrastructure/EngineDataStore.cs && git add TAOM.Tests/Infrastructure/EngineDataStore.cs`;
  `git diff ea8f2ad52 -- TAOM.Tests/Infrastructure/EngineDataStore.cs` must show only `v1.5.5` versus `v1.4.8`. If the file
  arrived cleanly as new (`A`), replace `(v1.5.5, decompile` with `(v1.4.8, decompile` (the cited lines 26 to 39, 28 to 32
  and 33 to 38 hold on v1.4.8) and `git add` it.
- One of the five files present in HEAD (another WP brought it) and conflicted (`UU`): `git checkout --ours <path>`,
  then convert its private fake by hand: add `using TAOM.Tests.Infrastructure;`; in every construction replace the
  type name `FakeDataStore` with `EngineDataStore`; `{ Mode = FakeDataStore.StoreMode.Saving }` becomes
  `{ Loading = false }` and `{ Mode = FakeDataStore.StoreMode.Loading }` becomes `{ Loading = true }`;
  `store.Mode = FakeDataStore.StoreMode.Loading;` becomes `store.Loading = true;`; `SetData(` becomes `Seed(`; delete
  the private `FakeDataStore` class with its doc comment; `git add` it. A private fake with any other member: STOP.
  If such a file merged cleanly (`M`), the pick already converted it: check `grep -c "class FakeDataStore" <path>` prints `0`.
- `CareerPersistenceTests.cs` applies cleanly (the tip differs from `ea8f2ad52^` only by a `RequiresGame` line outside the hunks).

**Verify**: `git status --porcelain | grep -c "^UU\|^AA\|^DU\|^UD"` prints `0`. Build exit 0. Filtered
`FullyQualifiedName~CareerPersistenceTests|FullyQualifiedName~SessionResetTests`: `Failed: 0`. Test: Total T0 + 48
(one test is renamed, none added). `git grep -n "class FakeDataStore" -- TAOM.Tests` lists no file you converted.
If `git status` still reports a cherry-pick in progress after the commit, run `git cherry-pick --quit`.

**Commit B5**: `test(infra): v2.0.28 - one engine-faithful data store fake`. Body: the career persistence tests used a
private IDataStore fake that skipped null on save and overwrote a duplicate key, so a test could pass where the
engine throws. EngineDataStore mirrors v1.4.8 BehaviorSaveData.SyncData (Add on save, TryGetValue on load) and is
now the one fake; RacePersistenceServiceTests keeps its type-keyed RoundTripDataStore on purpose. The career test
that loads an empty store is renamed for what it models. Trailers: `Research: v1.4.8 CampaignBehaviorDataStore.cs:26-39 and LoadBehaviorData :86-106`,
`Not-tested: none (test-only change)`, `Ported-from: ea8f2ad52d3a8bb0cfac3ddc9119b4b8e780bf5c`.

### Part B done criteria

- [ ] Five more commits (B1 to B5), each subject with `v2.0.28 - ` and at most 72 characters.
- [ ] Strict binding gate: `Failed: 0`, `Skipped: 0`, Total BV0 + 12 (2 from A2, 9 from B1, 1 from B4).
- [ ] Test: Total T0 + 48, `Failed: 0`, Skipped S0. `git status --porcelain` is empty.

---

## Part C: repo quality gates (`affcee2ea`, adapted; #1020)

`affcee2ea` carries five gates. ShellCheck, Gitleaks and Ruff are not installed on this laptop, and executors never
install tools (environment-failures rule): C2 and C3 need maintainer step M1, C4 needs M2, C5 is the maintainer's.
Do C1 always. The `affcee2ea` hunks for `deep-review/lenses/4-completeness.md` and `tools/context_budget_scan.sh`
(absent on 1.4.x), the hook and `test_hooks.sh` ShellCheck fixes, `hook-authoring.md`, `tests.md`, `AGENTS.md`,
`tools/README.md`, the lessons, RCA and review records, and the ralph-claude-code and impeccable provenance rows are
not taken (harness lane, WP142, WP143, or not this adoption).

### Step C1: Gate banned constructs repo-wide

1. `git show affcee2ea:TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs > TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs`.
   Edits (no `TAOM.Client` project on 1.4.x): delete `using TAOM.Client;`; `ScannedRoots = { "Main", "Client", "Dependencies", "TAOM.Tests" }`
   to `{ "Main", "Dependencies", "TAOM.Tests" }`; in the summary `TAOM, TAOM.Client, TAOM.Dependencies and TAOM.Tests` to
   `TAOM, TAOM.Dependencies and TAOM.Tests`; in `Scan_ExemptsIoCRegistrationFromConditionalsOnly` `"Client/IoC.cs"` to
   `"Dependencies/IoC.cs"`; rename `Scan_ReachesAllFourAssemblies` to `Scan_ReachesAllThreeAssemblies` and expect
   `{ "TAOM", "TAOM.Dependencies", "TAOM.Tests" }`; in `NoCompiledCodeUsesObsolete` `TAOM.Client's attributes cannot resolve`
   to `TAOM's attributes cannot resolve`; in `ScannedAssemblies` delete `typeof(ClientSubModule).Assembly,` and make its
   comment `// Core, Dependencies (reached through Main's reference) and this test assembly.`
2. **Verify**: `grep -c "Client" TAOM.Tests/Infrastructure/BannedConstructsGateTests.cs` prints `0`. Build exit 0. Filtered
   `FullyQualifiedName~BannedConstructsGateTests`: `Passed: 9`. Mutation check: add a line `#region X` and `#endregion`
   inside any test method of that file, `Failed: 1`; remove them. Test: Total T0 + 57.
3. **Commit C1**: `test(infra): v2.0.28 - gate banned constructs repo-wide`. Body: ADR-003 (no #region), ADR-004 (no
   Obsolete attribute) and ADR-005 (no conditional compilation outside Main/IoC.cs) were prose. A test now reads every
   .cs under Main, Dependencies and TAOM.Tests for the directives, and reflects over TAOM, TAOM.Dependencies and
   TAOM.Tests for the attribute; the tree is clean today. Trailers: `Not-tested: none (test-only change)`,
   `Ported-from: affcee2ead013f29a4b059af5ea999be440e0998`.

### Step C2: Ruff bug-class gate and the Claude config lint (needs M1 for Ruff)

Precondition: `python -m ruff --version` prints a version (M1); `grep -c "CLAUDE_PROJECT_DIR" .claude/settings.json`
prints 20 or more (WP103 anchored the hooks; `test_real_settings_hooks_exist` needs 20); `grep -c "Frontmatter fields documented as consumed" .claude/rules/harness-facts.md`
prints `1`. If any of the three fails: STOP this step, report which, and continue with C3 and C4 if their
preconditions hold.

1. `git show affcee2ea:ruff.toml > ruff.toml`; in it replace the comment sentence that starts `Style classes are deliberately not selected:`
   (two lines, with the 1.5.x counts) with `# Style classes are deliberately not selected: cleaning them is churn with no crash behind it.`
2. `git show affcee2ea:tools/tests/test_python_lint_gate.py > tools/tests/test_python_lint_gate.py` (as is).
3. `git show affcee2ea:tools/tests/test_claude_config_lint.py > tools/tests/test_claude_config_lint.py`, then
   `sed -i 's/Consumed frontmatter (the skill allowlist):/Frontmatter fields documented as consumed/g' tools/tests/test_claude_config_lint.py`
   (the 1.4.x `harness-facts.md` row label; it yields the 16 keys the tip's 43 skills and 5 agents all pass, checked this session).
4. RED: `python -m unittest tools.tests.test_python_lint_gate tools.tests.test_claude_config_lint -v`. Expect Ruff findings
   at least for `tools/oneoff/generate_rivendell_materials.py` (uses `json` without importing it, F821) and
   `tools/blender/spider_cfg.py` (Blender exec layer).
5. GREEN: add `import json` after `import csv` in `generate_rivendell_materials.py`; in `spider_cfg.py` insert after its
   second line the two lines `# ruff: noqa: F821` and `# Blender exec layer: bpy and the harness helpers come from the globals harness.py defines.`
   Fix any other finding only if it is in a `tools/` file and is a missing import or a plainly undefined name with an
   obvious source; more than 10 other findings, or any outside `tools/`: STOP.
6. Docs: `.ai/routing.json` tooling row, add `"ruff.toml"` after `".github/*"`. `.ai/verification.md`: replace
   `include the review-tool and documentation tests. That tests the tools;` with
   `include the review-tool and documentation tests and two lint gates (#1020): Ruff on bug classes and the Claude config lint. Those gates need Ruff and PyYAML (docs/reference/development-machines.md, "Lint gates"); a missing tool fails its gate, and a run with TAOM_ALLOW_NO_RUFF or TAOM_ALLOW_NO_YAML set reports that gate as skipped, never passed. That tests the tools;`.
   `.claude/skills/verify/SKILL.md`, end of `## Step 2b: Python tool tests`: add the paragraph
   `The suite includes two lint gates (#1020): Ruff on Python bug classes (python -m ruff) and the Claude config lint (PyYAML). A missing tool fails its gate; a run with TAOM_ALLOW_NO_RUFF or TAOM_ALLOW_NO_YAML set reports that gate as skipped, never as passed.`
   `docs/reference/development-machines.md`: `Three environment variables carry` to `These environment variables carry`;
   add the env table row ``| `TAOM_ALLOW_NO_RUFF`, `TAOM_ALLOW_NO_YAML` | set only on a machine that lacks the tool | turn that lint gate's failure into a reported skip |``;
   add a section before `## Writing docs and tools on either machine`: a `## Lint gates (#1020)` heading, the sentence
   `The Python suite (/verify Step 2b) runs gates that need these tools; Ruff and PyYAML are found by Python's import (user site, so python -I and python -s cannot see them).`,
   and a table with the header `| Tool | Install | Laptop |` and the rows
   `| Ruff | python -m pip install --user ruff (the desktop ran 0.17.2 per #1020) | <the version M1 installed> |`
   and `| PyYAML | python -m pip install --user pyyaml | 6.0.3 (2026-11-04) |`.
   `docs/reference/provenance-register.md`, last rows before `<!-- provenance-register-end -->`: the `dev-quality-gates`
   and `Ruff` rows from `affcee2ea` (`git show affcee2ea -- docs/reference/provenance-register.md`), and under the detail
   sections a `### Ruff` entry: Ruff (https://github.com/astral-sh/ruff, MIT) from PyPI into the user site of the system
   Python, run as `python -m ruff`; nothing ships in a TAOM release; setup in `development-machines.md`.

**Verify**: `python -m unittest tools.tests.test_python_lint_gate tools.tests.test_claude_config_lint -v`: `Ran 27 tests`, `OK`.
Tool tests: no failure beyond Step 0's. Docs: no new finding.

**Commit C2**: `feat(tools): v2.0.28 - Ruff bug-class gate and Claude config lint`. Body: two Python gates from the
#1020 review: Ruff lints the whole repo for bug classes only (syntax errors, undefined names; target Python 3.14), and
found a missing json import in a one-off tool, now fixed; the Claude config lint checks skill and agent frontmatter,
the 30-word description cap and that every registered hook script exists, with skill keys read from the
harness-facts allowlist row. A missing tool fails its gate unless the matching TAOM_ALLOW_NO_* variable is set.
Trailers: `Research: Ruff <version> on the laptop`, `Not-tested: none (tool gates)`, `Ported-from: affcee2ead013f29a4b059af5ea999be440e0998`.

### Step C3: ShellCheck gate (needs M1 for ShellCheck)

Precondition: `"${TAOM_SHELLCHECK:-shellcheck}" --version` prints 0.11.0 (or the path M1 gave). Otherwise STOP this step.

1. `git show affcee2ea:tools/tests/test_shell_lint_gate.py > tools/tests/test_shell_lint_gate.py` (as is).
2. RED: `python -m unittest tools.tests.test_shell_lint_gate -v`. Read the findings. Any finding in `.claude/**` or
   `tools/test_hooks.sh`: STOP this step (the harness lane owns those scripts) and hand the list over. Findings in other
   `tools/**/*.sh`: fix each with the smallest change ShellCheck asks for (for example
   `tools/oneoff/translate_all_remaining.sh`: split `export ANTHROPIC_API_KEY=$(cat ~/.taom_anthropic_key)` into
   `ANTHROPIC_API_KEY=$(cat ~/.taom_anthropic_key)` and `export ANTHROPIC_API_KEY`); more than 10 findings: STOP.
3. Docs: extend the two Step 2b and `.ai/verification.md` sentences from C2 to three gates (add `and ShellCheck on every .sh`
   and `TAOM_ALLOW_NO_SHELLCHECK`; if C2 did not land, add those two texts as C2 gives them but naming ShellCheck alone,
   and create the "Lint gates" section with only the ShellCheck row); add the development-machines env row ``| `TAOM_SHELLCHECK` | optional | tools/tests/test_shell_lint_gate.py: the binary, else shellcheck on PATH, else E:\Tools\shellcheck-0.11.0\shellcheck.exe |``,
   add `TAOM_ALLOW_NO_SHELLCHECK` to the allow row, and the "Lint gates" table row
   `| ShellCheck | shellcheck-v0.11.0.zip from https://github.com/koalaman/shellcheck/releases, SHA-256 checked, unzipped to a folder named for the version; set TAOM_SHELLCHECK unless it is on PATH | <the path M1 gave> |`;
   add the `ShellCheck` provenance row from `affcee2ea` and a `### ShellCheck` detail line (GPL-3.0; TAOM runs the binary
   and links nothing, so no obligation on TAOM's code; nothing ships in a release).

**Verify**: `python -m unittest tools.tests.test_shell_lint_gate -v`: `Ran 4 tests`, `OK`. Tool tests: no failure beyond Step 0's.

**Commit C3**: `feat(tools): v2.0.28 - ShellCheck gate for every shell script`. Body: ShellCheck at warning level, with
no rc file, now runs over every .sh git lists; the one allowed pattern is SC2206 on a set -f line, the split the
hook rules require. Name each script you fixed and why. Trailers: `Not-tested: none (tool gate)`,
`Ported-from: affcee2ead013f29a4b059af5ea999be440e0998`.

### Step C4: Fail restore on a high or critical NuGet advisory (maintainer applies; executor verifies and commits)

1. Executor: confirm `grep -c NuGetAuditMode Directory.Build.props` prints `0`, then STOP and ask the maintainer to apply
   M2 in the worktree.
2. After M2: `git diff Directory.Build.props` shows only the M2 block. Run `dotnet restore TAOM.sln --force-evaluate`.
   An `NU1903` or `NU1904` error: STOP (never add a `NuGetAuditSuppress` yourself; the maintainer reviews each advisory).
   `NU1900` (offline): report the audit as UNCHECKED and stop this step. Then Build and Test: `Failed: 0`.
3. `.claude/skills/verify/SKILL.md`: insert directly above `## Step 2: Test Suite` the `## Step 1b: NuGet audit` section
   from `affcee2ea` (`MSYS_NO_PATHCONV=1 git show affcee2ea:.claude/skills/verify/SKILL.md`, from `## Step 1b` to the line
   before `## Step 2`).

**Commit C4** (stage `Directory.Build.props` and the skill): `feat(build): v2.0.28 - fail restore on high NuGet advisories`.
Body: NuGet Audit now covers transitive packages and a high or critical advisory (NU1903, NU1904) fails restore and
so the build; offline it stays a warning. /verify Step 1b forces a full restore, because NuGet skips the audit on a
no-op restore. The maintainer applied Directory.Build.props. Trailers: `Research: dotnet restore --force-evaluate clean on <date>`,
`Not-tested: none (build gate)`, `Ported-from: affcee2ead013f29a4b059af5ea999be440e0998`.

### Step C5 (maintainer only): Gitleaks history scan

M3 below. The executor does nothing here: reviewing secret candidates is the maintainer's.

### Part C done criteria

- [ ] C1 committed; C2, C3 and C4 committed or reported blocked with the exact precondition that failed.
- [ ] Test: Total T0 + 57, `Failed: 0`. Tool tests: no failure beyond Step 0's, plus `Ran` up by 27 (C2) and 4 (C3) for the steps that landed.
- [ ] `git status --porcelain` is empty.

---

## Part D (optional, D9): the nullable ratchet (plan 019 end state)

Do this Part only with budget left after Parts A to C; otherwise report it as not started. Ports `4d2a6c9ac`,
`7ad7a3074`, `19ca72d3d`, `155e3ea57`, `de2881366` (D1) and `503b933ee`, `bdc80a5a8`, `272ac1f06` (D2). Method:
end-state transplant. The series is linear on `7f02fc8d3` and its merge `c16051e7c` resolved only bookkeeping; the
tip equals `7f02fc8d3` for every file it touches except the two csproj package versions and the registry (checked
this session), so `git show de2881366:<path>` and `git show 272ac1f06:<path>` are exact for those files.

### Step D0: Precondition

```bash
cd /c/Users/mikew/source/repos/taom-port/122 && git diff --quiet 7f02fc8d3 HEAD -- .editorconfig Main/Features/Siege/Hooks Main/Features/Siege/SiegeDefenseService.cs Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs Main/Features/Siege/Models/KingdomSiegeMessages.cs Main/Features/Siege/Models/SiegeDefenseConfig.cs TAOM.Tests/Features/Siege docs/ai-includes/code-quality.md docs/features/siege.md docs/features/siege-defense.md .claude/skills/build-fix/SKILL.md && echo IDENTICAL; git diff --name-status 7f02fc8d3 HEAD -- Main/Features/Siege; grep -c '<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>' Main/TAOM.csproj; grep -c '<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625;8374;8174</NoWarn>' Dependencies/TAOM.Dependencies.csproj
```

**Verify**: `IDENTICAL`, then exactly `M	Main/Features/Siege/Models/TaomSiegeEventModel.cs`, then `1` and `1`. Otherwise STOP.

### Step D1: Move the nullable suppression into `.editorconfig` and null-clean the Siege folder

1. RED: `git show de2881366:<path> > <path>` for `TAOM.Tests/Infrastructure/NullableRatchetGateTests.cs`,
   `TAOM.Tests/Features/Siege/SiegeCampGuardPatchTests.cs` and `TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs`.
   Build, then Filtered `FullyQualifiedName~NullableRatchetGateTests|FullyQualifiedName~SiegeCampGuardPatchTests|FullyQualifiedName~SiegeDefenseServiceTests`:
   at least the two `ProductionCsproj_NoWarn_OmitsEveryRatchetId` rows fail (a compile error naming a Siege member the
   new tests need is also an acceptable RED).
2. GREEN: `git show de2881366:<path> > <path>` for `.editorconfig`, `Main/Features/Siege/.editorconfig`,
   `Main/Features/Siege/Hooks/BesiegerCamp_GetSiegeCampPartyPosition_Patch.cs`, `Main/Features/Siege/Models/ActiveSiegeDefenseEvent.cs`,
   `Main/Features/Siege/Models/KingdomSiegeMessages.cs`, `Main/Features/Siege/SiegeDefenseService.cs`,
   `docs/ai-includes/code-quality.md`, `docs/features/siege.md`, `.claude/skills/build-fix/SKILL.md`. Then:
   - `Main/TAOM.csproj` (single-owner, this one line only): delete the line `		<NoWarn>$(NoWarn);8600;8601;8602;8603;8604;8618;8625</NoWarn>`.
   - `Dependencies/TAOM.Dependencies.csproj`: that line becomes `		<NoWarn>$(NoWarn);8374;8174</NoWarn>`.
   - Re-cite: in `docs/features/siege.md` and the patch file replace every `v1.5.3` with `v1.4.8` (the claims hold on
     v1.4.8: `BesiegerCamp.cs:312-315`, callers `MobileParty.OnPartyJoinedSiegeInternal` and `BesiegerCamp.SetPositionAfterMapChange`,
     "Current state"). In `code-quality.md` replace `the shipped build has two warnings in total, and a standing warning tier becomes noise.`
     with `a standing warning tier becomes noise.` (a 1.5.x count).
   - `docs/reference/harmony-patch-registry.md`: replace the `## Patch8_SiegeCampGuard` section body (`**Target:** Various`
     and `Siege camp guard`) with the `272ac1f06` section (`git diff 7f02fc8d3 272ac1f06 -- docs/reference/harmony-patch-registry.md`),
     changing `` `BesiegerCamp.cs:312-315` in v1.5.3 `` to `` `BesiegerCamp.cs:312-315` in v1.4.8 `` and the parenthesis on
     the callers to `(one engine caller, MobileParty.OnPartyJoinedSiegeInternal, dereferences SiegeEvent.BesiegedSettlement first; the other, BesiegerCamp.SetPositionAfterMapChange, passes it to MapSceneWrapper.GetSiegeCampFrames, which does; v1.4.8 MobileParty.cs:3209-3212 and BesiegerCamp.cs:564-595)`.

**Verify**: `grep -rn "v1\.5" docs/features/siege.md Main/Features/Siege` prints nothing. Build exit 0 with `0 Error(s)`
(a CS86xx error in `Main/Features/Siege`: STOP). Filtered as in RED: `Failed: 0`. Test: Total (Part C total) + 14
(5 `SiegeCampGuardPatchTests` methods and 9 `NullableRatchetGateTests` results; `SiegeDefenseServiceTests` stays at 26).

**Commit D1**: `fix(siege): v2.0.28 - null-clean the Siege folder, nullable as errors`. Body: the seven nullable
warning ids move from both production csproj NoWarn lists into the root .editorconfig, so a folder can raise them;
Main/Features/Siege is null-clean and sets them to errors; Patch8_SiegeCampGuard gets an explicit no-settlement branch
instead of an NRE swallowed by its catch-all, with the same defer-to-vanilla outcome; NullableRatchetGateTests fails
if an id returns to a NoWarn. Trailers: `Research: v1.4.8 BesiegerCamp.cs:305-315, :564-595; MobileParty.cs:3209-3212`,
`Not-tested: in game (siege camp placement)`, `Save-compat: none`, and one `Ported-from:` line each:
`4d2a6c9ac70b5ad2f2e4d3719ec006eef77a927f`, `7ad7a30747da1d56c558dbf4dd84cc9aaf6c4f56`,
`19ca72d3d112d1dbbbfe8e18cd72d7a182fe4ee0`, `155e3ea573634acde7fecda6569ff473f10044a7`,
`de288136648467c3509b6eabe6e0e1993eb0ae22`.

### Step D2: Fill a partial siege-defense message from the defaults

1. RED: `git show 272ac1f06:TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs > TAOM.Tests/Features/Siege/SiegeDefenseServiceTests.cs`;
   Build; Filtered `FullyQualifiedName~SiegeDefenseServiceTests` shows failures (or a compile error naming `GetMessages`).
2. GREEN: `git show 272ac1f06:<path> > <path>` for `Main/Features/Siege/Models/KingdomSiegeMessages.cs`,
   `Main/Features/Siege/Models/SiegeDefenseConfig.cs`, `Main/Features/Siege/SiegeDefenseService.cs`,
   `docs/features/siege-defense.md`, `docs/features/siege.md`; replace every `v1.5.3` in `siege.md` with `v1.4.8` again.

**Verify**: Build exit 0; Filtered `Failed: 0`, with `SiegeDefenseServiceTests` at 31 methods (26 before); Test:
Total (after D1) + 5. Docs: no new finding.

**Commit D2**: `fix(siege): v2.0.28 - siege defense messages fall back per field`. Body: a KingdomMessages entry with a
null or empty field, or a JSON null entry, now takes that field from the service defaults instead of passing null to
the popup and to GrantReward; the shipped config fills all five fields in all 18 entries, so players see no change
today. Trailers: `Not-tested: in game (siege defense popup)`, and one `Ported-from:` line each:
`503b933eec0dab050e633bc18de85cae434fe44e`, `bdc80a5a87d007270f654e306f9618955c4d121d`,
`272ac1f06dc71bf97e2ad6e231f249ff1d28e666`.

### Part D done criteria

- [ ] D1 and D2 committed; Build `0 Error(s)`; Test `Failed: 0`; `git status --porcelain` empty.
- [ ] `git grep -n "8600;8601" -- Main/TAOM.csproj Dependencies/TAOM.Dependencies.csproj` prints nothing.

---

## Disposition table (every commit in the pack)

| Commit | Group | Disposition | Where |
|---|---|---|---|
| `8b9f0a233` chore(engine): compile against v1.5.2 | transpiler-site-binding-gate | Adapted: the TranspilerSiteBindingTests pattern rewritten for the nine 1.4.x transpilers. The `harmony-patches.md` caller-actor bullet goes to WP143's rules sync (it is on the 1.5.x tip, `harmony-patches.md:30`). Everything else is the v1.5.2 compile adaptation (1.5.x-only) | B1 |
| `197454e68` fix(troopweight): patch the concrete clan rows | harmony-binding-hasbody-gate | Adapted: HasBody inside the new resolver, pinned by game-free fixtures; the ClanPartyItemVM negative pin is not portable (concrete on v1.4.8, `:1125`); TroopWeight retarget skipped (types absent on v1.4.8) | A2 |
| `8c89e042f` test(bindings): fail loudly on skips | plan-008 | Transplanted (end state `1f5047572`); CI step skipped (D9); hook and `test_hooks.sh` hunks net zero | A1 |
| `549afffde` review follow-ups for plan 008 | plan-008 | Transplanted (resolver guards, tests, skill, reflection-sites command); hook hunks net zero; review records dropped | A1 |
| `2ca0805bc` convergence fixes for plan 008 | plan-008 | Transplanted (skill precondition sentence, two test comments); hook hunks reverted upstream | A1 |
| `37306bca3` maintainer decisions for plan 008 | plan-008 | Transplanted (TreatNoTestsAsError, BindingGateRunSettingsTests, skill and hooks-catalog text) | A1 |
| `aa59f68ed` review follow-ups for plan 008 | plan-008 | Transplanted (skill Step 2 wording; `RepoPaths.RepoPath` is available from WP104); records dropped | A1 |
| `1f5047572` convergence fixes for plan 008 | plan-008 | Transplanted (one skill line; the end state) | A1 |
| `b8c000452` ci(tests): hosted Windows runners | plan-010-ci-on-hosted-windows | Skipped, D9 (CI and the RefAsm build mode; every 1.4.x build machine has the game) | none |
| `a4b90e4d2`, `2897fcca1`, `2628c66c5` plan 010 follow-ups | plan-010-ci-on-hosted-windows | Skipped, D9 | none |
| `c139bc50d` plan 010 maintainer decisions | plan-010-ci-on-hosted-windows | Skipped, D9. D44 (drop the SandBoxCore wildcard reference) holds on v1.4.8 (the laptop's SandBoxCore bin is empty) but is a no-op not worth a single-owner csproj edit | none |
| `1e9899443`, `2bc6695eb` plan 010 review records | plan-010-ci-on-hosted-windows | Skipped (bookkeeping) | none |
| `4d2a6c9ac`, `7ad7a3074`, `19ca72d3d`, `155e3ea57`, `de2881366` | plan-019-nullable-ratchet | Transplanted (end state `de2881366`, v1.4.8 re-citations), optional | D1 |
| `503b933ee`, `bdc80a5a8`, `272ac1f06` | plan-019-nullable-ratchet | Transplanted (end state `272ac1f06`); review records, lessons, RCA dropped; the "2,256 warnings" figure not carried | D2 |
| `e9b28e2ad` tag engine-bound tests RequiresGame | plan-010-ci-hosted-windows | Skipped, D9 (meaningful only with plan 010's no-game run) | none |
| `ee19f1052` pin reflection-site labels | tests-reflection-site-labels | Adapted: tests and convention ported, labels re-derived on 1.4.x, Dependencies root added, missing row added | B2 |
| `ea8f2ad52` one engine-faithful data store fake | tests-engine-data-store-fake | Ported by `cherry-pick -n`; absent session-reset files dropped; present ones converted | B5 |
| `6ddcc8f9b` one patch number names one feature | issue-938-patch-number-gate | Adapted: floors for 1.4.x, RequiresGame dropped (D9), 1.5.x-only exceptions removed, registry gate line and rule line kept; lessons and RCA to WP142 | B3 |
| `e7cdb2e96` feat(settlements): Reeve's Ledger | tests-viewmodel-mixin-binding-gate | Adapted: ViewModelMixinBindingTests only (floor 5), engine-bump and README rows; the feature, strings, settings, composition line and co-op rows skipped (new feature) | B4 |
| `affcee2ea` five quality gates | issue-1020-quality-gates | Adapted: banned constructs, Ruff, Claude config lint, ShellCheck, NuGet audit (maintainer), Gitleaks (maintainer); skipped hunks listed at the top of Part C | C1 to C5 |
| `8589a3384` no-game binding gate runs with no variable | plan-010-refasm-nogame-gate | Skipped, D9 | none |
| `7bac0c9d4` no-game gate step and v1.5.6 record | plan-010-refasm-nogame-gate | Skipped (superseded, 1.5.x docs) | none |
| `0740c632e` plan 044 | plan-010-refasm-nogame-gate | Skipped (1.5.x-only plan) | none |
| D4 WP-C (new work) | verify report D4 | Implemented: Harmony-faithful resolver, parameter binding, manual-patch coverage | A2 |
| `a27c6b338` (hunk: runsettings `TAOM_RUN_BENCHMARKS=1` and its pin) | WP118's xml-merge group (shared hunk) | Taken in A1 (WP118 is a dependency, so its harness is always present) | A1 |

## Test plan

- New C# tests: `GameAssembliesResolutionTests` (8), `BindingGateRunSettingsTests` (2 rows and 1 method), `HarmonyTargetResolverTests`
  (17 game-free: base-type target, declared-overload ambiguity, per-method targets, five parameter-binding failures, the
  bind-clean and pass-through cases, abstract target, HasBody, three TargetMethod rules, ResolveOriginals, the Harmony
  internals and version pin), `HarmonyPatchBindingTests` (+2: manual patches, ManualPatchApplicator source gate),
  `TranspilerSiteBindingTests` (9), `ReflectionSiteBindingTests` (+3), `PatchCategoryNumberTests` (4),
  `ViewModelMixinBindingTests` (2), `BannedConstructsGateTests` (9). Part D adds 5 `SiegeCampGuardPatchTests` and 9
  `NullableRatchetGateTests` results (D1) and 5 `SiegeDefenseServiceTests` (D2). Python: 5 + 22 (C2) and 4 (C3).
- Structural pattern: model fixtures on `HarmonyTargetResolverTests` above; binding tests follow `ReflectionSiteBindingTests`.
- Proof each gate can fail: A1 (b) and the no-match filter, the resolver fixtures, and the mutation checks in B1, B3,
  B4 and C1. Untestable here: a live Harmony apply inside the game, and the engine's behaviour when a category fails
  (WP104 covers the guard).
- Final: Build; Test `Failed: 0`, Total T0 + 57 (+ Part D's count if done); Strict binding gate `Failed: 0`, `Skipped: 0`.

## Done criteria (whole WP)

- [ ] Every Part's done criteria hold (Part D only if done; C2 to C4 only if their preconditions held).
- [ ] `git log --format=%s bannerlord-1.4.5..HEAD | grep -vc "v2.0.28 - "` prints `0`, and no subject exceeds 72 characters
      (`git log --format=%s bannerlord-1.4.5..HEAD | awk 'length > 72' | wc -l` prints `0`).
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD | grep -E "^(Main/(IoC|SubModule)\.cs|CHANGELOG\.md|AGENTS\.md|CLAUDE\.md|\.claude/settings|\.claude/hooks/|tools/test_hooks\.sh|docs/reviews/|tools/README\.md|docs/reference/feature-map\.md)"` prints nothing.
- [ ] `git diff --name-only bannerlord-1.4.5..HEAD -- Main` lists nothing outside `Main/Features/Siege` and `Main/TAOM.csproj` (and those only with Part D).
- [ ] No em or en dash in any line this WP added: `git diff bannerlord-1.4.5..HEAD | grep '^+' | grep -c $'\u2014\|\u2013'` prints `0`.
- [ ] Hand-off lists the plan's status, every commit hash with its subject, T0, S0, BV0, every Total you measured, each
      mutation check's result, any environment gap, and which of C2 to C5 and Part D are done, blocked (with the failed
      precondition) or not started. The integrator records them in `plans/README.md`; no old-plan row needs updating.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0 misses a token, or any "Current state" excerpt does not match the live file beyond the earlier-WP changes the
  drift check names.
- A1: the strict binding gate fails. Exception (environment gap): if every failure reads `Assert.Inconclusive failed`, its
  message names install data the laptop lacks (never `Game assemblies not loaded` or `Game dir unresolved`), and the
  same test reads Skipped without `--settings`, list each in the hand-off and continue. Never edit a test or the
  settings to get green.
- A2: `HarmonyTargetResolver.Check` reports any problem for a real TAOM patch class, or `EveryManualPatch_BindsLikeHarmony`
  fails. D4 found none, so this is either a defect in your resolver (compare it with "Current state") or a real latent
  binding failure: report the exact problem list; never change a production patch or loosen the resolver in this WP.
- A2: `HarmonyInternals_StillWhereTheResolverReadsThem` fails (Lib.Harmony is not 2.4.2, or an internal moved).
- B1: a transpiler test fails (other than your mutation): a real silent non-binding on v1.4.8. Report which; do not edit the transpiler.
- B2: a member appears at no line of its labelled file. B3: a patch number has two owners not in `SharedNumbers`, or a
  missing heading has no 1.5.x section to copy and is not a sub-category. B4: a mixin refresh method fails to resolve,
  or the build cannot find `Bannerlord.UIExtenderEx`. B5: a private fake has a member `EngineDataStore` lacks.
- C2/C3: a tool is missing (stop that step only); Ruff reports more than 10 findings beyond the two named, or any outside
  `tools/`; ShellCheck reports anything in `.claude/**` or `tools/test_hooks.sh`, or more than 10 findings. C4: an
  `NU1903`/`NU1904` error, or the maintainer has not applied M2 (never edit `Directory.Build.props` yourself).
- D0 is not exactly as expected, or D1's build reports a CS86xx error in `Main/Features/Siege`.
- A step seems to need `Main/IoC.cs`, `Main/SubModule.cs`, `.claude/settings*.json`, a hook script, `CHANGELOG.md`, or
  any production file outside Part D's list.
- A hook blocks a commit for any reason other than a subject typo you can fix, or asks to confirm a git command this
  plan does not give.
- A verification fails twice after a reasonable fix attempt.

## Maintainer steps

- **M1 (before C2 and C3, laptop)**: install Ruff (`python -m pip install --user ruff`; the desktop ran 0.17.2 per #1020)
  and ShellCheck 0.11.0 (`shellcheck-v0.11.0.zip` from https://github.com/koalaman/shellcheck/releases, SHA-256 checked
  against the asset digest), then put ShellCheck on PATH or set `TAOM_SHELLCHECK` to its path. Tell the executor the
  Ruff version and the ShellCheck path.
- **M2 (C4)**: in the worktree, insert this block into `Directory.Build.props` directly after the line
  `		<Nullable>enable</Nullable>` and its blank line, before the `Build identity` comment:

```xml
		<!--
		  NuGet Audit (#1020): a package with a known high or critical advisory fails restore, and so
		  the build. Below net10.0 the audit reads direct packages only by default, so `all` adds the
		  transitive ones. Offline, NU1900 stays a warning and the build goes on. An advisory that
		  does not apply goes into a NuGetAuditSuppress item with its reason and "(review YYYY-MM-DD)".
		-->
		<NuGetAuditMode>all</NuGetAuditMode>
		<WarningsAsErrors>$(WarningsAsErrors);NU1903;NU1904</WarningsAsErrors>

```

- **M3 (C5, optional)**: install Gitleaks 8.30.1 (`gitleaks_8.30.1_windows_x64.zip`, checked against `checksums.txt`),
  run `"$TAOM_GITLEAKS" git --redact --no-banner --exit-code 2 "$(git rev-parse --show-toplevel)"` over the 1.4.x history,
  rotate and purge any real secret, put each reviewed false positive's fingerprint in `.gitleaksignore` (never a value),
  add the `## Git history (Gitleaks)` section of `affcee2ea`'s `security-scan/SKILL.md` with the 1.4.x baseline numbers,
  the `TAOM_GITLEAKS` development-machines row and the `Gitleaks` provenance row, and commit them
  (`feat(tools): v2.0.28 - Gitleaks history step in /security-scan`). Do not copy the 1.5.x fingerprints.
- **M4**: the orchestrator creates the issue for the new work before this branch merges.
- **Integrator**: run `/deep-review` on the branch before the merge and `/verify-bindings` after it (the strict command);
  recount the test totals in `plans/README.md`.

## In-game checks owed (maintainer)

Parts A to C change no game code: nothing to check in game. Part D only:

| Check | Where | Pass |
|---|---|---|
| Siege camp placement | Besiege a town with a large army; watch parties settle around the camp | parties take camp positions; `Logs/taom_debug_*.log` has no `[Siege]` NRE or "No besieged settlement" line |
| Siege defense popup | Trigger a siege defense event for a kingdom with a full config entry | the popup shows the same title and texts as before |

## Maintenance notes

- **The strict command is the gate.** `dotnet test ... --settings TAOM.Tests/binding-gate.runsettings --filter "TestCategory=BindingVerification"`
  is green only at `Skipped: 0`. Without `--settings` it is the lenient default suite, where a skip still reads green.
- **New transpiler**: add a `TranspilerSiteBindingTests` test, or a feature binding test that runs it over the installed
  IL, in the same change (the rule line in `harmony-patches.md` says so). WP117 (MissionPerf), WP120, WP125 and WP131
  (`f83a63e57` changes the CastleRecruitment gate) will meet this rule;
  `f83a63e57`'s 1.5.x row uses the 1.5.x `SwapIsCastleGate(il, logger)` signature, so adapt it to the 1.4.x call.
- **New hand-applied patch**: give the class a `TargetMethod()` or add a `ManualPatches` row; the source gate fails otherwise.
  Wrapping `ManualPatchApplicator`'s `harmony.Patch` calls in a guard (D4 G4) is still open: a robustness WP should decide it.
- **New patch category**: give it a `## <category>` registry heading in the same change. WP142's feature-map sync must keep
  `PatchCategoryNumberTests` green (a 1.5.x row naming a 1.5.x-only patch fails it).
- **Lib.Harmony upgrade**: `HarmonyInternals_StillWhereTheResolverReadsThem` fails first; re-read the four Harmony members
  named in its message before changing the pin.
- **WP137** owns the API snapshot regeneration and the README counts (D4 WP-D); this plan already corrected the README's
  "Harmony resolution" claim. **WP127** deletes the `PathReuseCache` scaffold: delete its DataRow and its catalogue row
  together, or `EveryDataRow_HasAMatchingRowInTheCatalogue` fails. **WP143** must change `ALLOWLIST_ROW` in
  `tools/tests/test_claude_config_lint.py` back to `Consumed frontmatter (the skill allowlist):` in the same commit that
  syncs the 1.5.x `harness-facts.md` row, and should carry the caller-actor bullet of `8b9f0a233` and the gate paragraph
  of `tests.md`. **Integrator, WP119**: if WP119 merges after this WP, convert any private `FakeDataStore` in its session-reset tests to
  `EngineDataStore` as B5 describes, as one commit after both merge. **WP118** is a dependency: its plan skips `a27c6b338`'s `binding-gate.runsettings` and
  `BindingGateRunSettingsTests` hunks, and A1 takes them (`TAOM_RUN_BENCHMARKS=1` for the gate's test host and its pin),
  because the strict gate would otherwise fail WP118's opt-in harness. A1 also corrects the sentence WP118 wrote about
  this file in `docs/features/xml-merge-fast-path.md`.
- **Reviewer focus**: the resolver's parity with Harmony 2.4.2 (bulk versus attribute mode, the pass-through postfix, the
  `___` and `__N` rules); that no gate test was weakened to pass; that each expected transpiler count cites a v1.4.8 line;
  that the B2 repoints label the lookup line, not a declaration.
- **Release**: Parts A to C ship nothing to players. Part D changes `TAOM.Dependencies.csproj` warnings only (no code), but
  any release still ships TAOM and TAOM.Dependencies from one build (#371).
