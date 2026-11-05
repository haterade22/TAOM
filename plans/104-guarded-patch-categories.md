# Plan 104: Apply every Harmony patch category through one guard, and give the wiring tests one honest source reader

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving to the next step. If anything in "STOP conditions"
> occurs, stop and report; do not improvise. When done, give the hand-off in Step 8 (status, commit
> hashes, measured counts). `plans/README.md` belongs to the integrator: read it, never edit it.
>
> **Drift check (run first, from the WP worktree)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages tools/translation_cache tools/triage_battle_load.py TAOM.Tests/Infrastructure TAOM.Tests/Migration/GameModelOverrideBindingTests.cs TAOM.Tests/Features docs/features/crash-report.md docs/features/battle-load-diagnostics.md docs/reference/engine/submodule-lifecycle-and-harmony.md docs/ai-includes/architecture.md .claude/skills/deep-review/SKILL.md`
> Expected: empty, or only files changed by earlier WPs of this programme (WP 101 brings
> `TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs`; harness-lane WPs may
> touch the SKILL.md). For any listed file that did change, compare it with the excerpts in
> "Current state" and STOP only on a real mismatch.

## Status

- **Priority**: P1
- **Effort**: M (Part A: M, Part B: S)
- **Risk**: MED (edits the single-owner `Main/SubModule.cs` at all 80 live patch-category call
  sites; runtime behaviour changes only when an apply fails)
- **Depends on**: WP 101 (`plans/101-baseline-sync.md`) merged into local `bannerlord-1.4.5`
- **Category**: bug (crash and load-hang robustness), tests
- **Wave / lane**: wave 0, code lane (this WP, then WP 105)
- **Executor**: laptop
- **Decisions applied**: D1 (no `CHANGELOG.md` staging; the commit body is the entry), D2 (WP 105
  adopts the plan 018 runner next and relies on this WP's anchors, `RepoPaths` and `a0db0558b`),
  D7 (no co-op client split code), D8 (every subject carries `v2.0.28`), D9 (no CI, no
  `RequiresGame` tags), D10 (an ordinary session executes this file), D19 (git through the Bash
  tool only), D20 (a ModuleData commit that a hook blocks on the laptop is a STOP)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: port of #653 (plan 009), #792 (`06252ae2c`) and #662 (plan 018 step 0, `a0db0558b`).
  The BHA0001 pragma (Step 1) is new work: the orchestrator creates or names its issue before
  implementation lands.

## Why this matters

`Main/SubModule.cs` on the 1.4.x line applies its Harmony patches with 80 live
`_harmony.PatchCategory("...")` call sites, and 64 of them have no try/catch. Harmony 2.4.2 has no
catch around a category, so one patch class whose target no longer resolves throws out of the
SubModule hook. In `OnGameInitializationFinished` the once-per-process flag is set before the
batch (`SubModule.cs:1324-1325`), so a throw skips every later category for the whole process:
the Patch65 landless-culture guard, the Patch82 `AllocateTroops` guard, Patch84, Patch85, the spider
AV guards, Patch76, the battle-load watchdog, the exit-stall sampler, `ManualPatchApplicator` and
the Harmony census (verify report D4 counted 29 bare categories after Patch88 alone). The throw
then leaves TAOM: on the installed v1.4.8 `MBGameManager.OnGameInitializationFinished` loops the
submodules with no catch, so the players' BetterTime hook (it loads after TAOM) and the
SkeletonScale bone-index pass are skipped too, and in `OnSubModuleLoad` the throw leaves
`Module.InitializeSubModuleBases`, which also has no catch. Separately, Harmony builds its category
index for the whole assembly in one pass with no catch, so one class whose attribute names a type
the engine no longer has fails every `PatchCategory` call in the process. No player has hit this
yet (the E3 player log of 2026-11-01 shows all nine preview categories "applied OK", and verify
report D4 resolved all 229 patch classes against v1.4.8), so this is insurance against the next engine drift or a mod
that reshapes a target, on a line whose maintainer has already chased many infinite loading
screens. After this plan a failed binding costs one category, logs `[PatchApply]` with its cause,
and is named in one localized notice per phase (finding LOAD-TIME-04).

The test half matters as much for the programme: 28 later port commits need
`TAOM.Tests/Infrastructure/RepoPaths.cs` (verify report D1), which does not exist on 1.4.x, and
`GameModelOverrideBindingTests` passes falsely today because it reads `SubModule.cs` raw and counts
the commented-out `TaomPartyNavigationModel` registration (`SubModule.cs:938`) as registered. The
one analyzer warning in the build, BHA0001 on Patch88, is a false positive that trains readers to
ignore analyzer output.

## Current state

### Files and their roles (all on `origin/bannerlord-1.4.5` unless marked)

- `Main/SubModule.cs` (1,969 lines): single-owner engine entry point. 82 lines contain
  `_harmony.PatchCategory(`: 80 live plus 2 commented (Patch54 at `:1612`, Patch57 at `:1619`).
  Live sites by method: `OnSubModuleLoad` 21, `OnBeforeInitialModuleScreenSetAsRoot` 1,
  `OnGameInitializationFinished` 57, `OnMissionBehaviorInitialize` 1. Sixteen sit inside a try:
  `:166` Patch37, `:271` Patch83, `:313` Patch61, `:323` the Patch61 sub-category loop, `:348`
  Patch62, `:371` Patch79, `:455` Patch68, `:471` Patch63_BannerBearerSpawnGuard, `:542` Patch55,
  `:1353` the preview loop, `:1381` Patch77, `:1397` Patch78, `:1672` Patch43, `:1696` Patch60,
  `:1707` and `:1709` Patch69. The other 64 are bare.
- `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs:22`: the
  `[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs), nameof(...InitializeLordPartyProperties))]`
  attribute that BUTR.Harmony.Analyzer 1.0.1.50 (`Main/TAOM.csproj:112`) reports as BHA0001. No
  `BHA0001` suppression exists anywhere on the 1.4.x line (no pragma, `.editorconfig` or csproj entry).
- `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs:55-62`: reads `SubModule.cs` raw through a
  private `ReadRepoFile` (`:190-198`) and tests `subModule.Contains($"new {m.Name}(")`.
- `TAOM.Tests/Infrastructure/`: no `RepoPaths.cs`, no `PatchCategoryApplierTests.cs`.
  `Localization/LanguageDataXmlTests.cs` (24 test methods) has no check for a `<string>` row outside
  the root `<strings>`; `Localization/LanguageFileCoverageTests.cs` requires every English key in all
  12 languages but finds rows with `DescendantsAndSelf`, so a misplaced row passes it.
- `Main/_Module/ModuleData/taom_module_strings.xml`: root `<strings>`, LF in the blob (CRLF in a laptop
  worktree, `core.autocrlf=true`). The 12 `Languages/<LANG>/std_taom_module_strings_<code>.xml`
  files: 2,649 lines each, rows indented four spaces, newest rows end CR LF, the closing
  `  </strings>` and `</base>` lines end CR CR LF; git classifies them `-text` and never converts
  their line endings. None of the 13 has a `taom_patch_apply_*` key.
- The 12 `tools/translation_cache/<lang>.json` files: written by `save_cache`
  (`tools/translate_with_claude.py:382-386`) as `json.dump(..., ensure_ascii=False, indent=2,
  sort_keys=True)` with no trailing newline; all 12 round-trip byte for byte (checked this session).
- Text that names the bare apply or the warning this plan deletes: `tools/triage_battle_load.py:530`
  and `:1096` (`'Patch43 diagnostics failed to apply'`), `docs/features/crash-report.md:280`,
  `docs/features/battle-load-diagnostics.md:714`,
  `docs/reference/engine/submodule-lifecycle-and-harmony.md:20-21` and `:30`,
  `docs/ai-includes/architecture.md:293-295`, and `.claude/skills/deep-review/SKILL.md:362-377`
  (check 9 tells reviewers to grep for `_harmony.PatchCategory("<category-name>")`, which would flag
  every patch class after this plan).

### Excerpts (`origin/bannerlord-1.4.5`, find by text; line numbers move as you edit)

`Main/SubModule.cs:101` is `    private Harmony _harmony;`; `:106-108` declare the static one-shot
flags `_missionTimePatchesApplied`, `_gameInitPatchesApplied`, `_basicTableauGuardApplied`.

`Main/SubModule.cs:161-172` (the Patch37 site; its catch at `:173-176` logs `[CrashReport] init failed`):

```csharp
        _harmony = new Harmony("com.taom.mod");
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                _harmony.PatchCategory("Patch37_CrashReport");
                IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                }
            }
```

`Main/SubModule.cs:1324-1325` (then the batch; Patch43 at `:1672` is a typical guarded site,
`try { _harmony.PatchCategory("Patch43_BattleLoadDiagnostics"); }` with a catch that logs
`[BattleLoad] Patch43 diagnostics failed to apply`):

```csharp
        if (_gameInitPatchesApplied) return;
        _gameInitPatchesApplied = true;
```

No `.PatchCategory(` call exists in `Main/` or `Dependencies/` outside `Main/SubModule.cs`
(`git grep -P "\.PatchCategory\s*\("`, checked this session). No string literal in
`Main/SubModule.cs` or `Main/IoC.cs` contains `//` or `/*`, so a comment stripper that is not
literal-aware is safe on both (checked this session).

### Engine and library facts (verified 2026-11-04; the executor must not re-derive them)

From `pwsh -NoProfile -File tools/taom-src.ps1 path <Type>` on the installed v1.4.8 DLLs (cache
`C:\Users\mikew\.taom-src\v1.4.8\`):

- `TaleWorlds.MountAndBlade.Module`: `private void InitializeSubModuleBases()` at `Module.cs:201-208`
  calls `value.OnSubModuleLoad()` for every submodule with **no try/catch**; `LoadSubModules` calls
  it at `:1102`; `Module.Initialize` calls `LoadSubModules` at `:267`. (The 1.5.x plan 009 text
  quotes a catch-and-rethrow; that is v1.5.3 code and is wrong for this line.)
- `TaleWorlds.MountAndBlade.MBGameManager.OnGameInitializationFinished(Game game)` at `:110-125`:
  loops `Module.CurrentModule.CollectSubModules()` calling each override with no catch, then sets
  the `SkeletonScale` bone indices.
- `TaleWorlds.MountAndBlade.MBSubModuleBase`: `protected internal virtual void OnSubModuleLoad()`
  `:8`; `protected internal virtual void OnBeforeInitialModuleScreenSetAsRoot()` `:16`;
  `public virtual void OnGameInitializationFinished(Game game)` `:84`;
  `public virtual void OnMissionBehaviorInitialize(Mission mission)` `:101`.
- `TaleWorlds.Library.InformationManager`: `public static void DisplayMessage(InformationMessage message)`
  `:61` (invokes the `DisplayMessageInternal` event, no queue, `:63`);
  `public static void ShowInquiry(InquiryData data, bool pauseGameActiveState = false, bool prioritize = false)` `:91`.
- `TaleWorlds.Library.InquiryData(string titleText, string text, bool isAffirmativeOptionShown,
  bool isNegativeOptionShown, string affirmativeText, string negativeText, Action affirmativeAction,
  Action negativeAction, string soundEventPath = "", float expireTime = 0f, Action timeoutAction = null,
  Func<(bool, string)> isAffirmativeOptionEnabled = null, Func<(bool, string)> isNegativeOptionEnabled = null)` `:33`.
- `TaleWorlds.Library.InformationMessage(string information, Color color)` `:21`;
  `TaleWorlds.Library.Colors.Red` `:7`.
- `TaleWorlds.Localization.TextObject`: `public string Value` `:14`;
  `TextObject(string value, Dictionary<string, object> attributes = null)` `:71`; `ToString()` `:197`;
  `SetTextVariable(string tag, TextObject variable)` `:329`; `SetTextVariable(string tag, string variable)` `:334`;
  `public bool GetVariableValue(string tag, out TextObject variable)` `:361`.
- `TaleWorlds.Localization.LocalizedTextManager.LoadLanguage(LanguageData)` `:231-262`: walks the
  root's children and reads `<string>` only under a child named `strings` (`:243-255`); any other
  `<string>` is skipped silently.
- `TaleWorlds.CampaignSystem.Party.PartyComponents.LordPartyComponent`: nested
  `public class InitializationArgs` `:14`, with
  `public void InitializeLordPartyProperties(MobileParty mobileParty, Hero owner)` `:29`. The Patch88
  binding is real; `Patch88LordPartyTemplateTests.InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner`
  (`TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs:83`) pins it.

From the v1.4.8 dump `C:\Decompiled_Bannerlord\_modules_build\Native__TaleWorlds.MountAndBlade.GauntletUI.cs`:
Native's `GauntletUISubModule.OnBeforeInitialModuleScreenSetAsRoot` (`:4283`) creates
`GauntletChatLogView` (`:4292`) and the `GauntletQueryManager` (`:4299-4300`, which subscribes
`InformationManager.OnShowInquiry` at `:3824`); `GauntletInitialScreen.OnInitialize` (`:2516`)
calls `InformationManager.ClearAllMessages()` (`:2552`); `CreateQuery` refuses a duplicate of the
active or a queued inquiry (`:3925`) and otherwise queues it (`:3935`). So a startup notice must be
an inquiry raised from TAOM's `OnBeforeInitialModuleScreenSetAsRoot`, never a chat line from
`OnSubModuleLoad`. The installed `Modules/Native/ModuleData/global_strings.xml:66-68` defines
`str_ok` as `{=oHaWR73d}Ok`, which the notice button reuses.

From `ilspycmd` on `~/.nuget/packages/lib.harmony/2.4.2/lib/net472/0Harmony.dll` (the version both
`Main/TAOM.csproj:110` and `TAOM.Tests/TAOM.Tests.csproj:19` pin, and the one the E3 player log
reports as `0Harmony v2.4.2.0`):

```csharp
public void PatchCategory(string category)          // picks the CALLER's assembly from new StackTrace().GetFrame(1)
public void PatchCategory(Assembly assembly, string category)
{
    Dictionary<string, List<Type>> value = AssemblyCachedCategories.GetValue(assembly, BuildCategoryCache);
    if (value.TryGetValue(category, out var value2))
        value2.Do(delegate(Type type) { CreateClassProcessor(type).Patch(); });
}
```

`BuildCategoryCache` loops `AccessTools.GetTypesFromAssembly`, `HarmonyMethodExtensions.GetFromType(type)`
and `HarmonyMethod.Merge(...).category` with no catch. Public members the ported code uses, all
present in 2.4.2: `AccessTools.GetTypesFromAssembly(Assembly)`, `HarmonyMethodExtensions.GetFromType(Type)`,
`HarmonyMethod.Merge(List<HarmonyMethod>)`, field `HarmonyMethod.category`,
`Harmony.CreateClassProcessor(Type)`, `PatchClassProcessor.Patch()`, `Harmony.GetPatchInfo`,
`Patches.Postfixes`, `Patch.owner`, `Harmony.UnpatchAll`.

### What the 1.5.x end state is, and what this plan takes from where

Plan 009 merged on 1.5.x in `998ae7e95` (parents `4046c07de`, `4da415e21`). `git diff 4da415e21 998ae7e95`
touches 326 files overall (the other parent's history), but over the files this plan takes it changes
only `Main/SubModule.cs`, and only with 1.5.x-only content (MonsterSize, Animalia, TrollBruteForce
lines), so the end state of every plan 009 file is the branch tip. Later 1.5.x
edits to these files are not taken: `346f3e093` (load-stamps timing, WP 118), `e9b28e2ad`
(`RequiresGame` tags, D9), `03d9db8dc`, `d021426b0`, `6d7e67f3f` (co-op client split, D7).

| 1.4.x file | Source | Treatment |
|---|---|---|
| `Main/PatchCategoryApplier.cs` | `998ae7e95` | verbatim copy (84 lines) |
| `Main/PatchCategoryIndex.cs` | `998ae7e95` | verbatim copy |
| `TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs` | `998ae7e95` | verbatim copy (14 tests) |
| `TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs` | `998ae7e95` | verbatim copy (6 tests) |
| `TAOM.Tests/Infrastructure/RepoPaths.cs` | `03d9db8dc` (equals the 1.5.x tip) | copy, then 3 lines adapted (`CodeRoots = { "Main" }`) |
| `TAOM.Tests/Infrastructure/RepoPathsTests.cs` | `06252ae2c` | copy, plus 2 adapted `CSharpFiles` tests |
| 10 test files pinned to `TryPatchCategory(` | `45bcf80bd` | verbatim copies (the 1.4.x files equal `45bcf80bd^`) |
| `LanguageDataXmlTests` gate | `27fd23bbb` | hand insert, comment re-cited to v1.4.8 |
| English, 12 language and 12 cache rows | `998ae7e95` | inserted by the Step 4 script |
| `Main/SubModule.cs` | `git diff 7f02fc8d3 4da415e21 -- Main/SubModule.cs` | hand re-implementation (Step 4e) |
| 4 docs, `tools/triage_battle_load.py` | `9da9b5b92`, `bdf7d515c`, `4c728dac0`, `7912fdd8f`, `27fd23bbb` | hand edits with v1.4.8 corrections |
| deep-review SKILL.md check 9 | `bdf7d515c`'s `5-data-flow.md` hunk | hand edit (1.4.x has no `lenses/` folder) |
| 25 wiring tests, `ParkedModels` | `a0db0558b` | `git cherry-pick -n` |

### Cherry-pick simulation, re-run this session

`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`:
clean (exit 0) only for `9da9b5b92` and `4da415e21`. `45bcf80bd` conflicts in `CHANGELOG.md` and
`Main/SubModule.cs`; `bdf7d515c`, `7912fdd8f` and `b6cb6ff5d` conflict in `Main/SubModule.cs` and in
`Main/PatchCategoryApplier.cs` and its tests (files the first commit creates), plus docs and
bookkeeping (`b6cb6ff5d` also in `PatchCategoryIndexTests.cs` and `taom_module_strings.xml`);
`4c728dac0` in `Main/SubModule.cs`, docs and bookkeeping; `27fd23bbb` in `PatchCategoryApplier.cs`,
`PatchCategoryIndex.cs`, `PatchCategoryIndexTests.cs`, the DE, FR and JP language files and caches,
plus docs and bookkeeping (not `Main/SubModule.cs`); `7eae47044` conflicts in all 12 language files; `aa7a55181` in 3 bookkeeping files; `06252ae2c` in `RepoPaths.cs`, `RepoPathsTests.cs`
and 6 wiring tests that do not exist here; `a0db0558b` in 6 pinned test files (the plan 009 pin
lines), `SignatureStrikesBindingTests.cs` (absent) and `RepoPaths.cs` (absent).

Replaying the series cannot work (every later commit edits files the first one creates), hence
the end-state transplant. `a0db0558b` conflicts on today's base only because of the pin lines and
`RepoPaths.cs`; after Steps 2 and 4 its expected conflicts are listed in Step 7.

### Conventions that bind this change

- **TDD**: a failing test (or, for a gate, a proven-red state) before the implementation. Each
  step below names its RED and GREEN.
- **ADR-002 thin entry points**: `SubModule` stays the single-owner entry point; the two new members
  are delegation and reporting only. Do not move logic into it beyond what Step 4e lists.
- **ADR-007 adapters**: services never touch sealed TaleWorlds types. `PatchCategoryApplier` is not
  a service; its only engine type is the `TextObject` it builds, which keeps it unit-testable.
- **ADR-003, ADR-004, ADR-005**: no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **MCM json2 rule** (rename a persisted setting, never flip its default): not triggered; this plan
  adds no setting.
- **`.claude/rules/harmony-patches.md`**: every `MovementOrder` postfix goes through the shared
  deferred category `Patch_MissionTime_SetMovementOrder`; it stays applied in
  `OnMissionBehaviorInitialize`, now through `TryPatchCategory`.
- **Localization**: new player text is `{=KEY}default`, registered, and present in all 12 languages.
  The five keys reuse the 1.5.x AI first drafts; never run `tools/translate_with_claude.py` (it
  spends money; maintainer only).

## Commands you will need

Each Bash call is a fresh shell, so start every call with
`cd /c/Users/mikew/source/repos/taom-port/104 && export MSYS_NO_PATHCONV=1 && PY=/c/Python314/python.exe && LOG="$(git rev-parse --git-dir)" && BASE=<the SHA from Step 0> &&`.
`PY` is the laptop's Python; `LOG` is the worktree's private git folder (write logs and the commit
message file there, never in the tree); `BASE` is the commit the WP branch started from.

| Purpose | Command | Expected on success |
|---|---|---|
| Restore | `dotnet restore TAOM.sln` | exit 0 |
| Build | `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > "$LOG/wp104-build.log" 2>&1; echo "exit=$?"` | `exit=0`, `grep -c " error " "$LOG/wp104-build.log"` prints 0 |
| BHA count | `grep -c "warning BHA" "$LOG/wp104-build.log"` | 0 after Step 1 (at least 1 before it) |
| Test | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 \| tail -5` | `Failed: 0` |
| Filtered | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<Class>" 2>&1 \| tail -5` | `Failed: 0` |
| Binding gate | `dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" 2>&1 \| tail -5` | `Failed: 0`, skipped not above Step 0 |
| Data | `"$PY" tools/validate_moduledata.py > "$LOG/wp104-data.log" 2>&1; grep -c "ERROR" "$LOG/wp104-data.log"` | same count as Step 0 |
| Tool test | `"$PY" -m unittest discover -s tools/tests -t . -p "test_triage_battle_load.py"` | `OK` |
| Docs | `"$PY" tools/lint_docs.py --summary --dash-base "$BASE"` | no finding that names a file this plan touched |

Both `-p:` flags are mandatory on every build and test: `Main/TAOM.csproj:133-136` does not gate
the CopyModule target on `DisableModuleCopy`, so a build without `-p:ModuleId=` deploys into the
game install. Never `./build.ps1`. Never two builds at once in one worktree. The totals in this plan
count each `[DataRow]` as its own test, which is how MSTest 3.1.1 (`TAOM.Tests/TAOM.Tests.csproj:10-11`)
normally reports them; if your run reports the six `MentionsOutsideComments` rows as one test, every
expected total drops by 5 and that alone is not a failure.

## Scope

**In scope** (the only files you modify):

- `Main/SubModule.cs` (single-owner; this WP is its only editor while it runs)
- `Main/PatchCategoryApplier.cs`, `Main/PatchCategoryIndex.cs` (new)
- `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs`
- `Main/_Module/ModuleData/taom_module_strings.xml`, the 12 `Languages/<LANG>/std_taom_module_strings_<code>.xml`
  files and the 12 `tools/translation_cache/<lang>.json` files (the Step 4d script names them all)
- `tools/triage_battle_load.py` (two hint strings)
- `TAOM.Tests/Infrastructure/RepoPaths.cs`, `RepoPathsTests.cs`, `PatchCategoryApplierTests.cs`,
  `PatchCategoryIndexTests.cs` (new); `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs`
- the 10 pinned tests (Step 4c lists them) and the 25 `a0db0558b` test files (Step 7 lists them,
  `TAOM.Tests/Migration/GameModelOverrideBindingTests.cs` among them)
- `docs/features/crash-report.md`, `docs/features/battle-load-diagnostics.md`,
  `docs/reference/engine/submodule-lifecycle-and-harmony.md`, `docs/ai-includes/architecture.md`
- `.claude/skills/deep-review/SKILL.md` (check 9 only)

**Out of scope** (do NOT touch, even though they look related):

- `Main/IoC.cs`, `Main/TAOM.csproj`, `Main/ManualPatchApplicator.cs` (its unguarded `harmony.Patch`
  calls are a recorded follow-up, not this plan), `Dependencies/**` (no TAOM.Dependencies change, so
  no #371 pairing duty), `Directory.Build.props`, `.claude/settings*.json`.
- `Main/Composition/**`, `TAOM.Tests/Composition/**`, `FeatureModules`, `RunPhase` calls: WP 105.
- `CHANGELOG.md` (D1), `docs/reviews/**` (review records, RCAs, lessons, REVIEW-LOG), `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md`, `CLAUDE.md`, `AGENTS.md`: WPs 142 to 144 sync them.
- `TAOM.Tests/Features/SignatureStrikes/**` (feature absent on 1.4.x), `TAOM.Tests/binding-gate.runsettings`
  and every plan 008 change (WP 122), `RequiresGame` tags (D9).
- The 1.5.x `.claude/skills/deep-review/lenses/` files, `tools/translate_with_claude.py`,
  `Main/_Module/SubModule.xml` (its version moves only in a `/release`, D8), and `plans/README.md`
  (the integrator's; read it with `git show bannerlord-1.4.5:plans/README.md`, never edit it).
- Other feature docs that still show `_harmony.PatchCategory("...")` examples: docs-sync WPs.

## Git workflow

- Machine: the maintainer's laptop. Repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 at
  `BANNERLORD_GAME_DIR`. Never write into the game install.
- One worktree: `git worktree add C:/Users/mikew/source/repos/taom-port/104 -b port148/104-guarded-patch-categories bannerlord-1.4.5`,
  run from the main checkout. Work only in the worktree. Leave the main checkout's uncommitted files
  and any stash alone.
- Git only through the Bash tool (D19). Stage explicit paths (a shell glob that expands to named
  files is fine); never `git add -A`, `-u` or `.`; never `commit -a`.
- Commit: write the message to `"$(git rev-parse --git-dir)/WP104_MSG.txt"` with a quoted heredoc,
  stage, then run `git commit -F "$(git rev-parse --git-dir)/WP104_MSG.txt"` in the NEXT sequential
  Bash call, never in the same command and never through PowerShell. Never let `cherry-pick`,
  `--continue`, `-C`, `-c` or `--amend --no-edit` create a commit: `GIT_EDITOR=true` on this laptop
  would commit a 1.5.x message unedited.
- Message format (this plan gives each one in full):
  `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters), blank line, a body for 1.4.x
  readers wrapped at 72 with no em or en dashes and no `Co-Authored-By` or "Generated with" line,
  then the trailers (`Research:`, `Not-tested:`, `Save-compat:`, one `Ported-from: <40-hex sha>` per
  1.5.x commit whose content is included).
- Never push, open a PR, tag, merge into `bannerlord-1.4.5`, or stage `CHANGELOG.md`. If a hook
  blocks a commit, read its message, then follow the STOP conditions; never bypass a hook. The
  integrator (the maintainer, or a session the maintainer names) merges this branch in the README
  order and records your hand-off in `plans/README.md`.
- No paid review is authorized by this plan.

---

## Part A: the guard (Steps 0 to 4)

### Step 0: Prepare the worktree, record the base and the baseline

From the main checkout:

```bash
cd /c/Users/mikew/source/repos/TAOM && export MSYS_NO_PATHCONV=1 && git merge-base --is-ancestor 1b00d1acd bannerlord-1.4.5 && git merge-base --is-ancestor 8df90444c bannerlord-1.4.5 && echo WP101-OK
cd /c/Users/mikew/source/repos/TAOM && export MSYS_NO_PATHCONV=1 && git worktree add C:/Users/mikew/source/repos/taom-port/104 -b port148/104-guarded-patch-categories bannerlord-1.4.5
cd /c/Users/mikew/source/repos/taom-port/104 && git rev-parse HEAD
```

Write down the SHA the last command prints: it is `$BASE` for the rest of this plan (recompute it
any time with `git merge-base HEAD bannerlord-1.4.5`). Then run the drift check from the header,
and these baselines (each in its own Bash call, prefixed as "Commands" says):

```bash
grep -c "_harmony.PatchCategory(" Main/SubModule.cs
grep -c "BHA0001" Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs
ls TAOM.Tests/Infrastructure/RepoPaths.cs Main/PatchCategoryApplier.cs 2>&1 | grep -c "No such file"
dotnet restore TAOM.sln
dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > "$(git rev-parse --git-dir)/wp104-build0.log" 2>&1; echo "exit=$?"; grep -c "warning BHA" "$(git rev-parse --git-dir)/wp104-build0.log"
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= 2>&1 | tail -5
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" 2>&1 | tail -5
/c/Python314/python.exe tools/validate_moduledata.py > "$(git rev-parse --git-dir)/wp104-data0.log" 2>&1; grep -c "ERROR" "$(git rev-parse --git-dir)/wp104-data0.log"
```

**Verify**: `WP101-OK` printed; `82`; `0` (anything else means WP 101 already silenced the warning:
skip Step 1); `2` (both files absent); build `exit=0` with a BHA count of at least 1; the full suite
prints `Failed: 0` and the counts in the `Baseline after WP101` section of
`git show bannerlord-1.4.5:plans/README.md` (or the
latest count a WP merged since then recorded; the orchestrator measured 9154 total, 9151 passed,
3 skipped on the merged tree). Record all numbers: Steps 4 and 8 compare against them. That same
section must name the installed `LOTRLOME_Armory` as the public 2026-09-29 build (D20, WP 101 Part A).
If it does not, Steps 1 to 3 may proceed, but STOP before Step 4, whose commit touches ModuleData,
and report.

### Step 1: Silence the Patch88 BHA0001 false positive (new work, verify report D4)

RED: the Step 0 build log shows the BHA0001 warning on `Patch88_InitializeLordPartyPropertiesScope.cs(22,2)`.

Edit `Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope.cs`. Directly
after the line `/// </summary>` (`:21`), insert the comment and pragma, and add the restore line
after the `[HarmonyPatch(...)]` line, so the block reads:

```csharp
/// </summary>
// BUTR.Harmony.Analyzer 1.0.1.50 does not resolve a nested target type: it reports this attribute as
// BHA0001 against "TaleWorlds.CampaignSystem.Party.PartyComponents.InitializationArgs", a name with the
// containing LordPartyComponent dropped. The target exists on v1.4.8 (LordPartyComponent.cs:14, :29)
// and Patch88LordPartyTemplateTests.InitializeLordPartyProperties_StillResolves_OnTheNestedArgs_MobilePartyAndOwner pins it.
#pragma warning disable BHA0001
[HarmonyPatch(typeof(LordPartyComponent.InitializationArgs), nameof(LordPartyComponent.InitializationArgs.InitializeLordPartyProperties))]
#pragma warning restore BHA0001
[HarmonyPatchCategory(Patch88_LordPartyTemplate.Category)]
public static class Patch88_InitializeLordPartyPropertiesScope
```

Keep `nameof`; do not switch to `TargetMethod()`.

**Verify** (GREEN):
`dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= --no-incremental > "$(git rev-parse --git-dir)/wp104-build.log" 2>&1; echo "exit=$?"; grep -c "warning BHA" "$(git rev-parse --git-dir)/wp104-build.log"`
prints `exit=0` and `0`. Then the Filtered command with `Patch88LordPartyTemplateTests`: `Failed: 0`.

Commit (stage the one file, then commit in the next call):

```text
chore(harmony): v2.0.28 - silence the Patch88 BHA0001 false positive

The build printed one analyzer warning: BHA0001 on the Patch88 scope
class, naming LordPartyComponent's nested InitializationArgs without
its containing type. BUTR.Harmony.Analyzer 1.0.1.50 does not resolve
nested target types; the target exists on v1.4.8 and Harmony binds it
at runtime. A scoped pragma on that one attribute silences the false
positive, so the next real BHA warning is not lost in the noise.

Research: taom-src LordPartyComponent (:14 nested class, :29 method)
Not-tested: nothing in game; the change is build output only
```

### Step 2: Add RepoPaths and its tests (`06252ae2c` adapted, with the RepoPaths parts of `35007dbb8`, `a0db0558b`, `fa0b24e13`, `03d9db8dc`)

RED first: create the test file and watch the build fail.

```bash
git show 06252ae2c:TAOM.Tests/Infrastructure/RepoPathsTests.cs > TAOM.Tests/Infrastructure/RepoPathsTests.cs
```

In that file, directly after the line `public class RepoPathsTests` and its `{`, insert:

```csharp
    [TestMethod]
    public void CSharpFiles_Main_ContainsTheSubModuleAndNothingUnderObjOrBin()
    {
        var files = RepoPaths.CSharpFiles("Main").ToList();

        Assert.IsTrue(files.Any(f => Path.GetFileName(f) == "SubModule.cs"), "SubModule.cs is missing.");
        var repoDir = RepoPaths.RepoPath();
        var built = files.Where(f => f.Substring(repoDir.Length).Split('\\', '/').Any(s => s == "obj" || s == "bin")).ToList();
        CollectionAssert.AreEqual(new string[0], built, "CSharpFiles must skip obj and bin.");
    }

    // This line has one compiled code root. The 1.5.x line adds "Client" (its TAOM.Client
    // submodule), which does not exist here.
    [TestMethod]
    public void CSharpFiles_WithNoRoots_ReadsEveryCodeRoot()
    {
        CollectionAssert.AreEqual(new[] { "Main" }, RepoPaths.CodeRoots);
        Assert.IsTrue(RepoPaths.CSharpFiles().Any(f => Path.GetFileName(f) == "SubModule.cs"));
    }
```

**Verify** (RED): the Build command exits non-zero and the log names `RepoPaths` as unknown (CS0103).

GREEN: create the reader from the 1.5.x tip shape and adapt three lines.

```bash
git show 03d9db8dc:TAOM.Tests/Infrastructure/RepoPaths.cs > TAOM.Tests/Infrastructure/RepoPaths.cs
```

Then edit it:

- Replace
  `    /// <summary>The compiled code roots a source-scanning gate reads: core and the client submodule (ADR-013).</summary>`
  with
  `    /// <summary>The compiled code roots a source-scanning gate reads. This line has one; the 1.5.x line also scans its Client submodule, which does not exist here.</summary>`
- Replace `    public static readonly string[] CodeRoots = { "Main", "Client" };` with
  `    public static readonly string[] CodeRoots = { "Main" };`
- Replace `Main/SubModule.cs and Main/IoC.cs had no such literal at 4c728dac.` with
  `Main/SubModule.cs and Main/IoC.cs had no such literal at 1b00d1acd.`

**Verify** (GREEN):
- `diff --strip-trailing-cr <(git show 03d9db8dc:TAOM.Tests/Infrastructure/RepoPaths.cs) TAOM.Tests/Infrastructure/RepoPaths.cs | grep -c "^>"` prints `3`.
- Build: `exit=0`. Filtered with `RepoPathsTests`: `Failed: 0, Passed: 14` (8 test methods plus 6 data rows).

Commit (stage both files):

```text
test(infra): v2.0.28 - add RepoPaths, the shared repo source reader

Wiring tests on this line each locate and read Main/SubModule.cs and
Main/IoC.cs their own way, as raw text. RepoPaths is the one reader
the later ports rely on: RepoPath finds a repo file from its own
compile-time path, ReadSource normalises CRLF and fails on a missing
file, StripComments blanks comments while keeping offsets,
MentionsOutsideComments prefilters raw text, and CSharpFiles walks
the code roots. CodeRoots holds Main only, because the 1.5.x Client
project does not exist here. Only the RepoPaths parts of the commits
below are taken.

Not-tested: none (test infrastructure only)
Ported-from: 35007dbb8d0324efd71a49ce356636262225d9ab
Ported-from: a0db0558b74b7e19594fbb8072650feb2fc132bf
Ported-from: fa0b24e139ae81d606630e78513462dc4b626b75
Ported-from: 06252ae2c7efd68842d59ced471e8848c37d090a
Ported-from: 03d9db8dcf13432bf5b4d60e3bc63d0d60d60831
```

### Step 3: Gate translation rows outside root `<strings>` (`27fd23bbb`, test only)

In `TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs`, insert this test directly before
the `[TestMethod]` line of `AllTranslationFiles_StringEntries_HaveIdAndTextAttributes` (`:336-337`).
The file already has every `using` it needs.

```csharp
    // LocalizedTextManager.LoadLanguage (installed v1.4.8, LocalizedTextManager.cs:243-255) reads
    // only <string> children of a <strings> child of the root and skips every other <string>
    // without a log line, so the player sees the English default. The 1.5.x port of the patch
    // failure notice once seeded 60 rows after </strings> that every presence check (which walks
    // descendants) counted as translated.
    [TestMethod]
    public void AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent()
    {
        foreach (var (file, lang) in GetAllTranslationFiles())
        {
            var doc = XDocument.Load(file);
            var loaded = new HashSet<XElement>(doc.Root!.Elements("strings").Elements("string"));
            var stray = doc.Descendants("string").Where(s => !loaded.Contains(s))
                .Select(s => (string?)s.Attribute("id")).ToList();
            Assert.AreEqual(0, stray.Count,
                $"Languages/{lang}/{Path.GetFileName(file)}: rows outside <base>/<strings> never load: "
                + string.Join(", ", stray));
        }
    }
```

The gate is green on arrival (156 language files on `1b00d1acd`, zero stray rows, checked this
session); on 1.5.x it was red on `b6cb6ff5d`'s misplaced rows, so it can fail. Optional proof:
append `<string id="wp104_probe" text="x" />` after `</strings>` in the DE file, run the Filtered
command (expect `Failed: 1`, naming `wp104_probe`), then restore that one file with
`git checkout Main/_Module/ModuleData/Languages/DE/std_taom_module_strings_deu-DE.xml`.

**Verify**: Build `exit=0`; Filtered with `LanguageDataXmlTests`: `Failed: 0, Passed: 25`.

Commit (stage the test file):

```text
fix(harmony): v2.0.28 - fail translation rows outside root <strings>

LocalizedTextManager.LoadLanguage on v1.4.8 reads only the <string>
children of the root's <strings> element and skips every other row
without a log line, so a misplaced row renders in English while a
check that walks all descendants counts it as present. A new
LanguageDataXmlTests gate fails on any such row. Every language file
on this line is clean, so the gate lands green and guards the rows
the next commit adds.

Research: taom-src LocalizedTextManager.LoadLanguage (:243-255)
Not-tested: none (test only)
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71
```

### Step 4: Port the plan 009 guard from its end state (`45bcf80bd`, `bdf7d515c`, `4c728dac0`, `7912fdd8f`, `b6cb6ff5d`, `7eae47044`, `27fd23bbb`)

One commit at the end of 4h; never commit a red state in between.

**4a. Tests first (RED: does not compile).**

```bash
git show 998ae7e95:TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs > TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs
git show 998ae7e95:TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs > TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs
```

**Verify**: Build exits non-zero with CS0246 or CS0103 errors naming `PatchCategoryApplier` and
`PatchCategoryIndex`, and no other error.

**4b. The two classes, verbatim.**

```bash
git show 998ae7e95:Main/PatchCategoryApplier.cs > Main/PatchCategoryApplier.cs
git show 998ae7e95:Main/PatchCategoryIndex.cs > Main/PatchCategoryIndex.cs
```

`Main/TAOM.csproj` is SDK-style and compiles every `.cs` under `Main/` (no csproj edit), and its
`InternalsVisibleTo` already names `TAOM.Tests`.

**Verify**: Build `exit=0`. Run
`dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~PatchCategoryIndexTests" 2>&1 | tail -15`:
`Failed: 4, Passed: 16`. The four failures are exactly
`MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper`,
`SubModuleSource_MainMenuSetup_ReportsStartupFailuresInAnInquiry`,
`SubModuleSource_PatchFailureNotice_IsLocalized` and
`SubModuleSource_KeepsTheSideEffectsOfAFailedApply` (they read `SubModule.cs`, which 4e converts).

**4c. Pin the ten tests to the new spelling.** Precondition, then copy:

```bash
git diff --quiet 45bcf80bd^ HEAD -- TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs && echo IDENTICAL
for f in BanditManagement/Patch86HideoutBossFightBindingTests.cs Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs FieldCamp/FieldCampWiringTests.cs LordPartyTemplates/Patch88LordPartyTemplateTests.cs LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs Refuge/RefugeWiringTests.cs ReturnToArmy/Patch87ReturnToArmyTests.cs; do git show "45bcf80bd:TAOM.Tests/Features/$f" > "TAOM.Tests/Features/$f"; done
```

The first command must print `IDENTICAL` (STOP otherwise). Nine files change one assertion string
from `_harmony.PatchCategory("X")` (or `.PatchCategory("X")`) to `TryPatchCategory("X")`; the Patch65
file only corrects a doc comment that called the batch unguarded.

**Verify** (RED evidence): Build `exit=0`; then

```bash
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Patch86HideoutBossFightBindingTests|FullyQualifiedName~Patch80KingdomVoteDeadlockBindingTests|FullyQualifiedName~Patch85EnlistedDetachDeferralBindingTests|FullyQualifiedName~FieldCampWiringTests|FullyQualifiedName~Patch88LordPartyTemplateTests|FullyQualifiedName~Patch82MapEventObserverInvariantBindingTests|FullyQualifiedName~Patch84SiegeAftermathMenuGuardTests|FullyQualifiedName~RefugeWiringTests|FullyQualifiedName~Patch87ReturnToArmyTests" 2>&1 | tail -15
```

shows exactly 9 failures, one per class, each a "SubModule.cs never applies" or "no longer applies"
message. (Do not run the Localization tests yet: until 4d registers the keys,
`UnregisteredLocalizationKeyBaselineTests` fails on the `{=taom_patch_apply_failed}` literal that 4b
added. That is expected.)

**4d. Register the five strings in all 13 files and the 12 caches.** Run this script from the
worktree root. It copies the rows byte for byte from the 1.5.x end state (already placed inside
`<strings>`, with 27fd23bbb's DE, FR and JP fixes), so nothing is retyped.

```bash
cd /c/Users/mikew/source/repos/taom-port/104 && export MSYS_NO_PATHCONV=1 && /c/Python314/python.exe -I - <<'EOF'
import json, pathlib, subprocess

SRC = "998ae7e952ce636994290ebaed883abdf5d30862"
KEYS = ["taom_patch_apply_failed", "taom_patch_apply_phase_startup", "taom_patch_apply_notice_title",
        "taom_patch_apply_phase_game_init", "taom_patch_apply_phase_mission_start"]
LANGS = {"BR": "por-BR", "CNs": "zho-CN", "CNt": "zho-HK", "DE": "deu-DE", "FR": "fre-FR",
         "IT": "ita-IT", "JP": "jpn-JP", "KO": "kor-KO", "PL": "pol-PL", "RU": "rus-RU",
         "SP": "spa-LA", "TR": "tur-TR"}

def show(path):
    return subprocess.run(["git", "show", SRC + ":" + path], check=True, capture_output=True).stdout

# 1) English: the two comments and five rows, before the last </strings>.
rel = "Main/_Module/ModuleData/taom_module_strings.xml"
src = show(rel).split(b"\n")
first = [n for n, l in enumerate(src) if b"<!-- PatchCategoryApplier -->" in l]
assert len(first) == 1
start = first[0] - 1
assert b"harvested by tools/harvest_literal_loc_keys.py" in src[start]
end = [n for n, l in enumerate(src) if b'id="taom_patch_apply_phase_mission_start"' in l][0]
assert end - start == 7
block = [l.rstrip(b"\r") for l in src[start:end + 1]]
p = pathlib.Path(rel)
data = p.read_bytes()
assert b"taom_patch_apply" not in data, "English rows already present"
nl = b"\r\n" if b"\r\n" in data else b"\n"
pos = data.rfind(b"</strings>")
assert pos > 0 and data[pos - len(nl):pos] == nl
p.write_bytes(data[:pos] + nl.join(block) + nl + data[pos:])

# 2) The 12 language files: the five rows, inside <strings>, CR LF like the rows above them.
for d, code in LANGS.items():
    rel = "Main/_Module/ModuleData/Languages/%s/std_taom_module_strings_%s.xml" % (d, code)
    src = show(rel).split(b"\n")
    rows = []
    for key in KEYS:
        hit = [l.rstrip(b"\r") for l in src if ('id="%s"' % key).encode() in l]
        assert len(hit) == 1, (rel, key)
        rows.append(hit[0])
    p = pathlib.Path(rel)
    data = p.read_bytes()
    assert b"taom_patch_apply" not in data, rel + " already has the rows"
    marker = b"\n  </strings>"
    assert data.count(marker) == 1, rel
    p.write_bytes(data.replace(marker, b"\n" + b"".join(r + b"\r\n" for r in rows) + b"  </strings>", 1))

# 3) The 12 caches, written exactly as tools/translate_with_claude.py save_cache writes them.
for lang in ["br", "cns", "cnt", "de", "fr", "it", "jp", "ko", "pl", "ru", "sp", "tr"]:
    rel = "tools/translation_cache/%s.json" % lang
    upstream = json.loads(show(rel).decode("utf-8"))
    p = pathlib.Path(rel)
    cache = json.loads(p.read_text(encoding="utf-8"))
    for key in KEYS:
        assert key not in cache, (rel, key)
        cache[key] = upstream[key]
    with open(p, "w", encoding="utf-8", newline="\n") as fh:
        json.dump(cache, fh, ensure_ascii=False, indent=2, sort_keys=True)
print("ok")
EOF
```

**Verify**: the script prints `ok`; `git diff --numstat -- Main/_Module/ModuleData tools/translation_cache`
prints 25 lines: `8 0` for `taom_module_strings.xml` and `5 0` for each of the 12 language files and
12 caches. (The script was dry-run this session in memory against the 1.4.x blobs: 5 rows land inside
`<strings>` in every language file, 8 lines in the English file for both LF and CRLF.)

**4e. Convert `Main/SubModule.cs`.** First the mechanical rename of all 82 call lines:

```bash
sed -i 's/_harmony\.PatchCategory(/TryPatchCategory(/g' Main/SubModule.cs
grep -c "_harmony.PatchCategory(" Main/SubModule.cs
grep -c "TryPatchCategory(" Main/SubModule.cs
```

Expected `0` then `82`. That converts the 64 bare sites completely. Now make edits E1 to E15 with the
Edit tool (the "find" text is the post-sed text; it is unique in the file). Sites not listed here
(Patch62, Patch79, Patch63_BannerBearerSpawnGuard, Patch78) keep their try/catch unchanged: those
catches now guard only the `Initialize` calls, as on 1.5.x.

- **E1, field.** Find `    private Harmony _harmony;` and add a line after it:
  `    private PatchCategoryApplier _patches;` (`Main/TAOM.csproj` NoWarn already silences CS8618).

- **E2, Patch37 site.** Replace the seven comment lines from
  `// Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport IMMEDIATELY` through
  `// a manual DI container; accept and document the residual.` (`:154-160`), plus the code down to
  the end of the `try { ... }` body (keep the catch), with:

```csharp
        // Codex review #46 (2026-05-25) MED-01: attach Patch37_CrashReport first so its tick
        // finalizers (Module.OnApplicationTick, ScreenManager.Tick, Mission.Tick and the rest) are
        // live as early as possible. It does NOT cover this method's own body: its
        // MBSubModuleBase.OnSubModuleLoad finalizer patches the base method, and this override is
        // already on the stack when it attaches. A throw from here leaves the engine's
        // Module.InitializeSubModuleBases, which has no catch on v1.4.8 (Module.cs:201-208), so it
        // also skips every later module's OnSubModuleLoad. That is why every category goes through
        // TryPatchCategory: one binding that no longer resolves costs its category (Harmony keeps
        // the classes it applied before the failing one) instead of the module load or the rest of
        // a batch. Categories are indexed by PatchCategoryIndex, class by class, not by Harmony's
        // own assembly-wide index: a class whose [HarmonyPatch] names a type the engine no longer
        // has would fail Harmony's index and so every category, where here only that class is
        // skipped and reported (#653).
        _harmony = new Harmony("com.taom.mod");
        var categoryIndex = PatchCategoryIndex.Build(typeof(SubModule).Assembly);
        _patches = new PatchCategoryApplier(
            category => categoryIndex.Apply(_harmony, category),
            IoC.Resolve<IModLogger>());
        _patches.RecordSkippedClasses(categoryIndex.SkippedClasses);
        if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableCrashCapture) ?? true)
        {
            try
            {
                if (TryPatchCategory("Patch37_CrashReport"))
                {
                    IoC.Resolve<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>().Subscribe();
                    if ((TAOM.Features.CrashReport.CrashReportSettings.Instance?.EnableNativeToManagedCapture) ?? true)
                    {
                        IoC.Resolve<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>().AttachAll(_harmony);
                    }
                }
            }
```

- **E3, Patch83 comment.** Replace the four comment lines from
  `// GUARDED, unlike Patch58 above, and the guard is load-bearing: this category binds an` through
  `// a crash guard into a worse crash than the one it prevents (the Patch61/Patch62 shape).` with:

```csharp
        // GUARDED, and the guard is load-bearing: the adapter behind Initialize reflects four
        // engine members. A rename would throw out of OnSubModuleLoad and take the rest of module
        // init with it, turning a crash guard into a worse crash than the one it prevents (the
        // Patch61/Patch62 shape). A category that fails to apply is contained by TryPatchCategory.
```

- **E4, Patch61.** Replace the two comment lines
  `// type from killing its siblings. Diagnostics must never break startup: every category in` and
  `// its own try/catch, fail = vanilla.` with
  `// type from killing its siblings. Diagnostics must never break startup: TryPatchCategory` and
  `// contains each category's failure, the try/catch covers the Initialize calls, fail = vanilla.`
  Then replace the sub-category loop body (the inner try/catch whose catch logs
  `$"[SaveLoad] {category} not applied (engine drift?): ..."`) so the loop reads
  `{ TryPatchCategory(category); }`, one statement on its own line inside the braces. Keep the outer
  try/catch.

- **E5, Patch68.** Replace the try/catch around `TryPatchCategory("Patch68_EconomyDiagnostics");`
  (its catch logs `[EconomyDiagnostics] Patch68 failed to apply`) with the single line
  `        TryPatchCategory("Patch68_EconomyDiagnostics");`. Keep the comment above it.

- **E6, end of OnSubModuleLoad.** After `        TryPatchCategory("Patch42_CastleRecruitment");` add:

```csharp
        // No ReportPatchFailures here: nothing receives a message yet (see the startup report in
        // OnBeforeInitialModuleScreenSetAsRoot), so this phase's failures wait for it.
```

- **E7, Patch55 and the startup report.** Inside `if (!_basicTableauGuardApplied)`, replace the
  try/catch around `TryPatchCategory("Patch55_BasicTableauRaceGuard");` (its catch logs
  `[HeroRace] Patch55_BasicTableauRaceGuard apply failed`) with:

```csharp
            TryPatchCategory("Patch55_BasicTableauRaceGuard");
            // Reports OnSubModuleLoad's failures and Patch55's together. The earliest a notice can
            // be shown: Native's GauntletUISubModule, which runs before TAOM, creates the chat log
            // and the inquiry manager in this hook, and InformationManager queues nothing sent
            // before them. An inquiry, not a chat line: the initial screen clears the chat log
            // after the splash video (GauntletInitialScreen.OnInitialize, ClearAllMessages).
            ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_startup}startup"), persistent: true);
```

- **E8, the two helpers.** Find the end of `OnGameLoaded`:
  `        StampSaveLoadPhase(Features.SaveLoadDiagnostics.Domain.SaveLoadPhase.GameLoaded);` followed by
  `    }`. Insert after that `    }` and before the comment
  `    // The GameInitializationFinished stamp lives in the existing OnGameInitializationFinished`:

```csharp

    private bool TryPatchCategory(string category) => _patches.TryApply(category);

    // One notice per phase naming every category that failed, so a dead crash guard is never
    // silent: a red chat line, or an inquiry the player dismisses when a screen change would clear
    // the chat log first. The notice itself must never break the phase, hence the catch. The
    // button reuses vanilla's own "Ok" row (Native global_strings.xml str_ok), already translated.
    private void ReportPatchFailures(TextObject phase, bool persistent = false)
    {
        var failures = _patches.TakeFailureSummary(phase);
        if (failures == null) return;
        try
        {
            var summary = failures.ToString();
            if (persistent)
                InformationManager.ShowInquiry(new InquiryData(
                    new TextObject("{=taom_patch_apply_notice_title}TAOM").ToString(), summary, true, false,
                    new TextObject("{=oHaWR73d}Ok").ToString(), string.Empty, null, null));
            else
                InformationManager.DisplayMessage(new InformationMessage(summary, Colors.Red));
        }
        catch (System.Exception ex)
        {
            IoC.Resolve<IModLogger>().LogError($"[PatchApply] failure notice not shown: {ex.Message}");
        }
    }
```

  No new `using` is needed: the file already imports `TaleWorlds.Library`, `TaleWorlds.Localization`
  and `TAOM.Core.Logging`, and both new classes are in namespace `TAOM`.

- **E9, preview loop.** Inside `foreach (var previewCategory in new[] { ... })` (`:1338`), replace
  the whole try/catch body (`:1351-1360`, whose catch logs `PatchCategory '{previewCategory}' FAILED`)
  with:

```csharp
            if (TryPatchCategory(previewCategory))
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogAlways($"PatchCategory '{previewCategory}' applied OK.");
            else
                Features.HeroRace.Diagnostics.TableauDiagnostics.LogError(
                    $"PatchCategory '{previewCategory}' FAILED, the character preview will fall back to vanilla resolution; the [PatchApply] line in the TAOM log has the cause.");
```

  Keep the `foreach (var previewCategory in new[]` line and the array exactly as they are
  (`HeroRaceWiringTests` anchors on them).

- **E10, Patch77.** Replace the three comment lines
  `// generator and tears it down again. Wrapped because the constructor postfix binds by`,
  `// arity: if a future engine build declares a second BodyGeneratorView constructor, Prepare`,
  `// returns false and nothing binds, but a PatchCategory throw here would brick startup.` with:

```csharp
        // generator and tears it down again. The constructor postfix binds by arity: if a future
        // engine build declares a second BodyGeneratorView constructor, Prepare returns false and
        // nothing binds. A category that fails to apply disables the switcher through the if below;
        // the try/catch does the same when an Initialize call throws.
```

  Then replace `            TryPatchCategory("Patch77_PlayerSwitcher");` with:

```csharp
            if (!TryPatchCategory("Patch77_PlayerSwitcher"))
            {
                IoC.Resolve<TAOM.Features.PlayerSwitcher.IPlayerSwitchPolicyProvider>()
                    .DisableForSession("Patch77 could not be applied; the [PatchApply] line in the TAOM log has the cause");
            }
```

  Keep the existing catch (it still calls `DisableForSession` when an `Initialize` throws).

- **E11, Patch43.** Replace
  `        try { TryPatchCategory("Patch43_BattleLoadDiagnostics"); }` and its four-line catch (which logs
  `[BattleLoad] Patch43 diagnostics failed to apply`) with
  `        TryPatchCategory("Patch43_BattleLoadDiagnostics");`. The watchdog `Start()` line after it stays.

- **E12, Patch60.** Replace the two lines
  `        try { TryPatchCategory("Patch60_TournamentExitMovieRelease"); }` and its one-line catch with
  `        TryPatchCategory("Patch60_TournamentExitMovieRelease");`.

- **E13, Patch69 and the game-init report.** Replace the four lines (two `try { TryPatchCategory("Patch69_...") }`
  lines, each followed by a one-line catch) with:

```csharp
        TryPatchCategory("Patch69_TournamentRosterGuard");
        TryPatchCategory("Patch69_TournamentEndGuard");
        ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));
```

  This must sit before the `ManualPatchApplicator` comment and `ManualPatchApplicator.ApplyAll(_harmony);`.

- **E14, the mission-start report.** Inside `if (!_missionTimePatchesApplied)`, after
  `            TryPatchCategory("Patch_MissionTime_SetMovementOrder");` add
  `            ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_mission_start}mission start"));`

- **E15, the parked Patch57 comment.** Replace the two commented lines
  `        // try { TryPatchCategory("Patch57_NavalAtSeaLandRescueGuard"); }` and the commented catch
  after it with the single line `        // TryPatchCategory("Patch57_NavalAtSeaLandRescueGuard");`.
  Leave the commented `navalRescueLogger` lines above it.

**Verify** (each must hold):

```bash
grep -c "_harmony.PatchCategory(" Main/SubModule.cs
grep -c "TryPatchCategory(" Main/SubModule.cs
grep -c "ReportPatchFailures(" Main/SubModule.cs
grep -c "try { TryPatchCategory" Main/SubModule.cs
diff <(git show "$(git merge-base HEAD bannerlord-1.4.5)":Main/SubModule.cs | sed -n 's/.*PatchCategory("\([^"]*\)".*/\1/p' | sort) <(sed -n 's/.*PatchCategory("\([^"]*\)".*/\1/p' Main/SubModule.cs | sort) && echo SAME-CATEGORIES
```

Expected: `0`, `83` (80 live calls, 2 commented, 1 declaration), `4` (3 calls, 1 declaration), `0`,
`SAME-CATEGORIES`. The category comparison is the authoritative check that no category was dropped
or renamed; if only the `83` differs, re-read your comment text against E2 to E10.

**4f. GREEN.** Build `exit=0`. Then:

```bash
dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchCategoryApplierTests|FullyQualifiedName~PatchCategoryIndexTests|FullyQualifiedName~Patch86HideoutBossFightBindingTests|FullyQualifiedName~Patch80KingdomVoteDeadlockBindingTests|FullyQualifiedName~Patch85EnlistedDetachDeferralBindingTests|FullyQualifiedName~FieldCampWiringTests|FullyQualifiedName~Patch88LordPartyTemplateTests|FullyQualifiedName~Patch65LandlessCultureSpawnGuardBindingTests|FullyQualifiedName~Patch82MapEventObserverInvariantBindingTests|FullyQualifiedName~Patch84SiegeAftermathMenuGuardTests|FullyQualifiedName~RefugeWiringTests|FullyQualifiedName~Patch87ReturnToArmyTests|FullyQualifiedName~HeroRaceWiringTests|FullyQualifiedName~Localization" 2>&1 | tail -5
```

**Verify**: `Failed: 0`.

**4g. Full suite and data.** Run the Test command and the Data command.

**Verify**: `Failed: 0`; total equals the Step 0 total plus 35 (14 RepoPaths, 1 gate, 14 applier,
6 index); skipped equals the Step 0 skipped count; the Data `ERROR` count equals Step 0's.

**4h. Commit.** Stage exactly these paths and check the staged list:

```bash
git add Main/PatchCategoryApplier.cs Main/PatchCategoryIndex.cs Main/SubModule.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/LordSpawnGuard/Patch65LandlessCultureSpawnGuardBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml tools/translation_cache/*.json
git diff --cached --name-only | wc -l
git status --porcelain | grep -v "^M  \|^A  " | wc -l
```

Expected `40` and `0`. Commit message:

```text
fix(harmony): v2.0.28 - apply every patch category through one guard

SubModule applied its patches through 80 PatchCategory call sites,
64 of them with no try/catch. Harmony 2.4.2 has no catch around a
category, so one class whose target no longer resolves threw out of
the hook. In OnGameInitializationFinished the once-per-process flag
is set before the batch, so every later category (the Patch65,
Patch82, Patch84 and Patch85 guards among them), the battle-load
watchdog, ManualPatchApplicator and the census were skipped for the
process, and the throw then left TAOM through MBGameManager's
submodule loop, which has no catch on v1.4.8. In OnSubModuleLoad the
throw leaves Module.InitializeSubModuleBases, also without a catch.

Every category now goes through TryPatchCategory, backed by
PatchCategoryApplier: a failure logs [PatchApply] at Error with its
cause, costs only that category, and is named in one localized
notice per phase (an inquiry at the first main menu, a red chat line
at game initialization and at the first mission). PatchCategoryIndex
builds the category index class by class, so a [HarmonyPatch] that
names a type the engine no longer has costs that class, not every
category. The Patch37, Patch77 and preview-batch sites keep their
side effects. The five taom_patch_apply_* strings ship in all 12
languages, inside <strings>, as the 1.5.x AI first drafts. Ported
from the post-merge end state of 998ae7e95 (plan 009).

Research: taom-src Module.cs:201-208, MBGameManager.cs:110-125
Research: ilspycmd Lib.Harmony 2.4.2 PatchCategory, BuildCategoryCache
Not-tested: live apply path and the notices (needs the game)
Save-compat: no save data touched
Ported-from: 45bcf80bd34bf6759210c3ce01b7485447cd31ed
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
Ported-from: 4c728dac0f2a4ec2b410692ecd08931abaaced4f
Ported-from: 7912fdd8fa31d323d5b31db0b9c3a2014768ab50
Ported-from: b6cb6ff5df940d2f265428fd10fba40cc332dc5c
Ported-from: 7eae4704434dcbe73b951628e4968023cd0c60cb
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71
```

### Part A done criteria

- [ ] `git log --format=%s "$BASE"..HEAD` lists 4 subjects (3 if Step 1 was skipped), each matching
      `^[a-z]+\([a-z-]+\): v2\.0\.28 - ` and at most 72 characters.
- [ ] `grep -c "_harmony.PatchCategory(" Main/SubModule.cs` is 0 and the 4e category comparison prints `SAME-CATEGORIES`.
- [ ] `git diff --quiet 998ae7e95 HEAD -- Main/PatchCategoryApplier.cs Main/PatchCategoryIndex.cs TAOM.Tests/Infrastructure/PatchCategoryApplierTests.cs TAOM.Tests/Infrastructure/PatchCategoryIndexTests.cs && echo VERBATIM` prints `VERBATIM`.
- [ ] Build `exit=0`, BHA count 0; full suite `Failed: 0`, total = Step 0 total + 35.
- [ ] `git diff --name-only "$BASE"..HEAD | wc -l` prints 44 (43 if Step 1 was skipped) and
      `git diff --name-only "$BASE"..HEAD | grep -c CHANGELOG.md` prints 0.
- [ ] `git status --porcelain` is empty.

---

## Part B: docs, the review check and the honest reader (Steps 5 to 8)

Start Part B only on a branch where Part A's done criteria hold.

### Step 5: Correct the docs and the triage hint (`9da9b5b92`, `bdf7d515c`, `4c728dac0`, `7912fdd8f`, `27fd23bbb`)

Before editing, record `"$PY" tools/lint_docs.py --summary --dash-base "$BASE" > "$(git rev-parse --git-dir)/wp104-lint0.txt" 2>&1`.

1. `docs/features/crash-report.md:280`: replace the whole bullet whose bold label is the
   "MBSubModuleBase.OnSubModuleLoad chicken-and-egg" (it ends "to maximise coverage of our own init.") with:

   ```markdown
   - **`MBSubModuleBase.OnSubModuleLoad` chicken-and-egg.** TAOM's own `OnSubModuleLoad` is what registers Patch37, and Patch37 cannot catch a throw from that method: its `MBSubModuleBase.OnSubModuleLoad` finalizer patches the base method's body, and TAOM's override is already running when it attaches. The same holds for every other module's `OnSubModuleLoad`, loaded before or after TAOM: each override is a separate method the base-method finalizer never wraps. On v1.4.8 the throw then leaves `Module.InitializeSubModuleBases`, which has no catch (`TaleWorlds.MountAndBlade.Module.cs:201-208`), so every module after the thrower skips its `OnSubModuleLoad` as well. TAOM applies its own categories through `TryPatchCategory`, so a binding that no longer resolves logs a `[PatchApply]` error and stops that one category at its failing class instead of failing the load (a class whose attribute names a type the engine no longer has is skipped and reported on its own: `PatchCategoryIndex` indexes the categories class by class, where Harmony's own index would fail every category, #653). Patch37 is still registered first so its tick finalizers are live as early as possible.
   ```

2. `docs/features/battle-load-diagnostics.md:714`: in that line, change the backticked text
   `Patch43 diagnostics failed to apply` to `[PatchApply] Patch43_BattleLoadDiagnostics FAILED` and
   the word "warning" right after it to "error". Nothing else on the line changes.

3. `tools/triage_battle_load.py:530` and `:1096`: replace `'Patch43 diagnostics failed to apply'`
   with `'[PatchApply] Patch43_BattleLoadDiagnostics FAILED'` in both strings.

4. `docs/ai-includes/architecture.md`: after the tree line `├── ManualPatchApplicator.cs` (`:294`) add:

   ```text
   ├── PatchCategoryApplier.cs      # guarded apply of one Harmony patch category
   ├── PatchCategoryIndex.cs        # category index built class by class (#653)
   ```

5. `docs/reference/engine/submodule-lifecycle-and-harmony.md`: first read the post-Part-A line
   numbers: `grep -n 'protected override void OnSubModuleLoad()\|_harmony = new Harmony("com.taom.mod");\|TryPatchCategory("Patch37_CrashReport")\|TryPatchCategory("Patch42_CastleRecruitment")\|protected override void OnBeforeInitialModuleScreenSetAsRoot()' Main/SubModule.cs`.
   Call them L1 (`OnSubModuleLoad`), L2 (`new Harmony`), L3 (Patch37), L4 (Patch42), L5
   (`OnBeforeInitialModuleScreenSetAsRoot`). Replace the `OnSubModuleLoad()` table row (`:20`) with:

   ```markdown
   | **`OnSubModuleLoad()`** (:L1) | Earliest: module DLL loaded, before menu | `_harmony = new Harmony("com.taom.mod")` (:L2); apply the module-load patch categories (21 call sites; the larger batch is in `OnGameInitializationFinished`) via **`TryPatchCategory("PatchNN_X")`** (:L3-L4), the guarded helper over `PatchCategoryIndex.Apply(_harmony, category)`: a failure logs `[PatchApply]`, stops that category at its failing class (classes applied before it stay patched), and every other category still applies (a class whose attribute names a type the engine no longer has is skipped and reported on its own, see Categories below); wire static patch fields via `.Initialize(service, …)`; IoC bootstrap. Nothing receives `InformationManager.DisplayMessage` yet, so this phase's failures are shown later, in the startup inquiry of `OnBeforeInitialModuleScreenSetAsRoot`. |
   ```

   Replace the `OnBeforeInitialModuleScreenSetAsRoot()` row (`:21`) with:

   ```markdown
   | **`OnBeforeInitialModuleScreenSetAsRoot()`** (:L5) | After all modules load, before main menu | Pre-menu setup; applies `Patch55_BasicTableauRaceGuard`, then shows every startup patch failure (this phase's and `OnSubModuleLoad`'s) in one persistent inquiry, since Native's `GauntletUISubModule` has built the inquiry manager by now; NativeSkinFixes **install** (the native MinHook layer; managed Harmony can't touch native; **parked 2026-07-08**: the install call is commented out, so it does no MinHook work until re-enabled). |
   ```

   Replace the `- **Categories:**` bullet (`:30`) with:

   ```markdown
   - **Categories:** a patch class carries `[HarmonyPatch(typeof(Target), "Method")]` + `[HarmonyPatchCategory("PatchNN_X")]`; **`TryPatchCategory("PatchNN_X")`** (in `SubModule`, through `PatchCategoryApplier`) applies all patches in that group. Harmony stops a category at its first class whose target does not resolve and throws, with no rollback of the classes it already applied; the helper contains that to the one category. Harmony's own `PatchCategory` builds its category index once per assembly by reading every type's attributes, so one attribute naming a type the engine no longer has would fail every category; TAOM builds that index class by class in `PatchCategoryIndex` (Harmony's public API only), skips the unreadable class, logs it as `[PatchApply] ... SKIPPED` and names it in the startup inquiry (#653). TAOM applies categories *selectively* (some conditionally, e.g. `Patch37_CrashReport` :L3), so a category can be skipped without disabling everything.
   ```

   Write the numbers you read in place of L1 to L5.

**Verify**:
- `grep -c "Patch43 diagnostics failed to apply" tools/triage_battle_load.py docs/features/battle-load-diagnostics.md` prints `0` for both.
- `grep -c "\[PatchApply\] Patch43_BattleLoadDiagnostics FAILED" tools/triage_battle_load.py` prints `2`.
- `grep -c "_harmony.PatchCategory" docs/reference/engine/submodule-lifecycle-and-harmony.md` prints `0`.
- `grep -c ":L[1-5]" docs/reference/engine/submodule-lifecycle-and-harmony.md` prints `0`.
- Tool test: `OK`.
- After committing, rerun lint into `wp104-lint1.txt`; `diff` of the two files adds no line that names
  any of the four docs.

Commit (stage the five files):

```text
docs(harmony): v2.0.28 - correct Patch37 coverage, name the guard

The crash-report doc said Patch37 is registered first to maximise
coverage of TAOM's own init. Its OnSubModuleLoad finalizer patches
the base method, so it never covers TAOM's own override; the doc now
says so, states that v1.4.8's Module.InitializeSubModuleBases has no
catch, and names TryPatchCategory and PatchCategoryIndex. The
lifecycle reference, the architecture tree, the battle-load reading
trap and the triage tool now point at the [PatchApply] line instead
of the Patch43 warning the guard removed.

Not-tested: none (docs and a tool hint)
Ported-from: 9da9b5b92dd12a86572335e98684b3689da44744
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
Ported-from: 4c728dac0f2a4ec2b410692ecd08931abaaced4f
Ported-from: 7912fdd8fa31d323d5b31db0b9c3a2014768ab50
Ported-from: 27fd23bbb7c4ad8b79947d3621f3b6989f778c71
```

### Step 6: Point deep-review check 9 at `TryPatchCategory` (the 1.4.x home of `bdf7d515c`'s lens hunk)

`.claude/skills/deep-review/SKILL.md` check 9 (`:362-377` on `1b00d1acd`) is word for word the
1.5.x lens text that `bdf7d515c` rewrote. Precondition:
`grep -n "_harmony.PatchCategory" .claude/skills/deep-review/SKILL.md` lists exactly four lines, the
check's `:365`, `:366`, `:367` and `:377` (numbers may shift if a harness WP merged first). If it
does not, skip this step, go to Step 7, and report the skip.

Replace lines `:362`, `:363`, `:365`, `:366` and `:367` (in that order) with the five lines in
this block, keep `:364` and `:368-375` as they are, and change `:376-377` (inside the second code
fence of the reference template) to the last two lines of the block:

```markdown
9. **Harmony Patch Category Registration (MANDATORY: fires for every `[HarmonyPatch]` class in scope).** TAOM uses category-based Harmony patching exclusively. `Harmony.PatchAll()` is NEVER called. A patch class with only `[HarmonyPatch(...)]` and no `[HarmonyPatchCategory(...)]`, OR with both attributes but no matching `TryPatchCategory("CategoryName")` call in `Main/SubModule.cs`, is silently dead code (no error, no warning, the patch simply never engages at runtime). The failure mode is invisible: the feature ships, tests pass, but the Harmony patch does nothing.
   - **Check (apply for every changed file under `Main/` containing `[HarmonyPatch]`; a test-assembly probe in `TAOM.Tests` is applied by its own test, never by SubModule):**
     - Grep `Main/SubModule.cs` for `TryPatchCategory("<category-name>")` (a category applied in a loop, such as the Patch61 sub-categories or the character-preview batch, appears as a string literal in that loop's array). If absent, flag as HIGH. A direct `_harmony.PatchCategory(` call is itself a finding: `PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails on it.
     - The category name in `[HarmonyPatchCategory]` and the string in `TryPatchCategory(...)` must match exactly (case-sensitive).
     - **APPLY-TIMING sub-check (registered ≠ applied in time):** find WHICH `SubModule` lifecycle method the `TryPatchCategory(...)` call lives in, and confirm it runs BEFORE the patched target can first render. TAOM's late batch (`OnGameInitializationFinished`, gated `_gameInitPatchesApplied`) fires on CAMPAIGN INIT, correct only for in-game / character-creation screens. If the patched type is rendered on a **main-menu / pre-campaign screen** (Save/Load, main menu, launcher; decompile to find the instantiator, e.g. `BasicCharacterTableau` is built only by `SaveLoadHeroTableauTextureProvider` on the cold Load Game screen), the category MUST be applied in `OnSubModuleLoad` or `OnBeforeInitialModuleScreenSetAsRoot` (process-static one-shot), NOT the late batch, else the prefix attaches too late and the guarded crash stays live. Flag as HIGH/CRITICAL. Do NOT accept "the category is registered" as sufficient.
     // In Main/SubModule.cs, alongside the other TryPatchCategory calls:
     TryPatchCategory("Patch38_SettlementNameplateFade");   // ← REQUIRED
```

**Verify**: `grep -c "_harmony.PatchCategory" .claude/skills/deep-review/SKILL.md` prints `1` (the
"is itself a finding" sentence) and `grep -c "TryPatchCategory" .claude/skills/deep-review/SKILL.md`
prints `6`.

Commit (stage the one file):

```text
fix(harmony): v2.0.28 - deep-review check 9 greps TryPatchCategory

Check 9 of /deep-review told reviewers to grep SubModule.cs for a
bare _harmony.PatchCategory call and flag HIGH when absent. Every
category now goes through TryPatchCategory, so the old grep would
flag every patch class as dead code. The check now greps
TryPatchCategory, names the loop arrays, and treats a direct
_harmony.PatchCategory call as a finding in itself, which the
PatchCategoryApplierTests source gate also fails.

Not-tested: none (review instructions only)
Ported-from: bdf7d515c49740c60d0204770c503bdb2b5436dc
```

### Step 7: Read SubModule and IoC through one reader (`a0db0558b`, `git cherry-pick -n`)

Do not take the plan 018 merge `1efdf2985`'s versions of these files: that merge also carries plan
008 (Inconclusive becomes Fail), which belongs to WP 122.

Precondition (the 24 files that must equal `a0db0558b^` now; the 25th, `HeroRaceWiringTests.cs`,
carries an extra #700 test on this line and merges cleanly):

```bash
git status --porcelain | wc -l
git diff --quiet a0db0558b^ HEAD -- TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/BannerColorPersistence/BannerTripletOrderingTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs TAOM.Tests/Migration/GameModelOverrideBindingTests.cs && echo IDENTICAL
```

Expected `0` and `IDENTICAL`. Then:

```bash
git cherry-pick -n a0db0558b
```

Expected: git reports conflicts in exactly these three paths, and no others:
`TAOM.Tests/Infrastructure/RepoPaths.cs` (content), `TAOM.Tests/Infrastructure/RepoPathsTests.cs`
(add/add) and `TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs` (modify/delete;
the feature is absent here). Resolve, whether or not git flagged the first two:

```bash
git checkout HEAD TAOM.Tests/Infrastructure/RepoPaths.cs TAOM.Tests/Infrastructure/RepoPathsTests.cs
git rm -q -f TAOM.Tests/Features/SignatureStrikes/SignatureStrikesBindingTests.cs
git add TAOM.Tests/Features/AutoResolveDiagnostics/AutoResolveDiagnosticsWiringTests.cs TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs TAOM.Tests/Features/BannerColorPersistence/BannerTripletOrderingTests.cs TAOM.Tests/Features/BattleLoadDiagnostics/ExitStallDisarmTests.cs TAOM.Tests/Features/CompanionTactics/SharedMovementOrderPostfixTests.cs TAOM.Tests/Features/CoopInterop/ResetForUnloadSweepTests.cs TAOM.Tests/Features/Diplomacy/Patch80KingdomVoteDeadlockBindingTests.cs TAOM.Tests/Features/Enlistment/Patch85EnlistedDetachDeferralBindingTests.cs TAOM.Tests/Features/FiefManagement/FiefHubCampaignBehaviorTests.cs TAOM.Tests/Features/FieldCamp/FieldCampWiringTests.cs TAOM.Tests/Features/HeroRace/HeroRaceWiringTests.cs TAOM.Tests/Features/HeroRace/RacePersistenceBehaviorTests.cs TAOM.Tests/Features/LordPartyTemplates/Patch88LordPartyTemplateTests.cs TAOM.Tests/Features/MapEventGuard/Patch82MapEventObserverInvariantBindingTests.cs TAOM.Tests/Features/MapEventGuard/Patch84SiegeAftermathMenuGuardTests.cs TAOM.Tests/Features/Messengers/MessengerCampaignBehaviorTests.cs TAOM.Tests/Features/MountDespawn/MountDespawnWiringTests.cs TAOM.Tests/Features/Refuge/RefugeWiringTests.cs TAOM.Tests/Features/ReturnToArmy/Patch87ReturnToArmyTests.cs TAOM.Tests/Features/SettlementGuards/SettlementGuardsWiringTests.cs TAOM.Tests/Features/SiegeDismount/SiegeDismountWiringTests.cs TAOM.Tests/Features/SiegePropDiagnostics/SiegePropDiagnosticsWiringTests.cs TAOM.Tests/Features/UncapturableHeroes/UncapturableHeroesWiringTests.cs TAOM.Tests/Features/WandererAllegiance/WandererAllegianceWiringTests.cs TAOM.Tests/Migration/GameModelOverrideBindingTests.cs
git diff --cached --name-only | wc -l
git status --porcelain | grep -c "^UU\|^AA\|^DU\|^UD"
git ls-files TAOM.Tests/Features/SignatureStrikes | wc -l
```

Expected `25`, `0`, `0`. If the `block-dangerous-git` hook asks about the `checkout` or `rm` line,
approve it: those commands discard only the cherry-pick's own conflict output in this worktree.
Never run `git cherry-pick --continue`; if `git status` still reports a cherry-pick in progress
after the commit below, run `git cherry-pick --quit`.

What the change does: every one of the 25 files reads `Main/SubModule.cs` or `Main/IoC.cs` through
`RepoPaths.ReadSource(path, stripComments: true)`, its private locator helper goes, and no assertion
changes; `GameModelOverrideBindingTests` gains the `ParkedModels` dictionary (one entry,
`TaomPartyNavigationModel`, parked 2026-06-26, #296/#120) and the test
`ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister`.

**Verify**: Build `exit=0`; Filtered over the 25 classes plus `RepoPathsTests` (or simply the Test
command): `Failed: 0`; `git grep -c "ReadRepoFile" -- TAOM.Tests/Migration/GameModelOverrideBindingTests.cs`
prints nothing. A converted test that fails is a STOP (do not edit its assertion).

Commit message:

```text
test(composition): v2.0.28 - read SubModule and IoC through one reader

The 25 test files that guard wiring in Main/SubModule.cs and
Main/IoC.cs now read both files through RepoPaths.ReadSource with
comments stripped, and their own locator helpers are gone; no
assertion changed. The switch exposed one false pass: the GameModel
binding gate counted TaomPartyNavigationModel as registered because
a commented-out AddModel line contains its constructor call. The
parked model is now an explicit ParkedModels entry with its reason
(NavalTravel, #120 and #296), and a new test keeps that list honest
in both directions.

Not-tested: none (test-only change)
Ported-from: a0db0558b74b7e19594fbb8072650feb2fc132bf
```

### Step 8: Wrap up

Run the Build, the Test command and the Binding gate.

**Verify**: `exit=0`; `Failed: 0` with total = Step 0 total + 36 and skipped = Step 0 skipped; Binding
gate `Failed: 0` with skipped not above Step 0's.

Do not edit `plans/README.md`. End with a hand-off report the integrator can record:

- Status: `DONE`, or `IN PROGRESS (Part A done)` if the session must end after Part A.
- Branch `port148/104-guarded-patch-categories` and the output of
  `git log --format="%h %s" "$(git merge-base HEAD bannerlord-1.4.5)"..HEAD`.
- Measured counts: test total, passed and skipped, Step 0's values beside them, the BHA warning
  count, and the Data `ERROR` count.
- Any step you skipped (Step 1 or Step 6) and why.
- README rows: only the WP 104 row. There is no old-plan row to close (plan 009 has no row on this line).

### Part B done criteria

- [ ] `git log --format=%s "$BASE"..HEAD` lists the Part A commits plus Steps 5, 6 (unless skipped)
      and 7, all with the `v2.0.28 - ` label and at most 72 characters.
- [ ] The whole-WP changed-file check (under "Done criteria (whole WP)") prints `66`, `25` and nothing.
- [ ] Full suite `Failed: 0`, total = Step 0 total + 36; Binding gate `Failed: 0`.
- [ ] `git status --porcelain` is empty; `git ls-files TAOM.Tests/Features/SignatureStrikes` prints nothing.

---

## Disposition table (every commit in the pack, plus the shared hunks)

| Commit | Group | Disposition | Where |
|---|---|---|---|
| `45bcf80bd` fix(harmony): apply every patch category through one guard | plan-009 | Transplanted from the `998ae7e95` end state: applier, tests, SubModule conversion, 10 test pins. CHANGELOG dropped (D1) | Step 4 |
| `9da9b5b92` docs(harmony): correct Patch37 coverage, name the guard | plan-009 | Transplanted: crash-report and lifecycle docs; the SubModule comment rides in E2 | Steps 4, 5 |
| `bdf7d515c` fix(harmony): review follow-ups for plan 009 | plan-009 | Transplanted: inquiry at the first main menu, wording, tests, docs, triage hint; its `5-data-flow.md` hunk re-homed in the 1.4.x SKILL.md check 9. Lessons, review records, REVIEW-LOG, CHANGELOG dropped | Steps 4, 5, 6 |
| `4c728dac0` fix(harmony): convergence fixes for plan 009 | plan-009 | Transplanted: comment and doc text (its SubModule comment is superseded by `7912fdd8f`'s, which E2 carries). Review records, RCA, lessons dropped | Steps 4, 5 |
| `7912fdd8f` fix(harmony): apply maintainer decisions for plan 009 | plan-009 | Transplanted: `PatchCategoryIndex`, its tests, the applier and SubModule wiring, doc text. Review record, lesson, CHANGELOG dropped | Steps 4, 5 |
| `b6cb6ff5d` fix(loc): localize the patch failure notice for plan 009 | plan-009 | Transplanted: TextObject notice, 5 English rows, 12 language rows, 12 cache entries. CHANGELOG and review record dropped | Step 4 |
| `7eae47044` fix(loc): move the notice translations inside `<strings>` | plan-009 | Folded: Step 4d places the rows inside `<strings>` once, never replaying the misplacement | Step 4 |
| `27fd23bbb` fix(harmony): review follow-ups for plan 009 | plan-009 | Transplanted: the LanguageDataXmlTests gate (re-cited to v1.4.8), DE/FR/JP text and caches, applier and index doc comments, architecture tree, lifecycle text. Review records, REVIEW-LOG, lessons, CHANGELOG dropped | Steps 3, 4, 5 |
| `aa7a55181` fix(docs): convergence fixes for plan 009 | plan-009 | Skipped: bookkeeping only (LESSONS-LEARNED, review record, RCA) | none |
| `4da415e21` docs(reviews): Codex prompt for plan 009 | plan-009 | Skipped: bookkeeping only (Codex prompt) | none |
| `998ae7e95` merge (plan 009) | plan-009 | Source of the end state; its own merge-side SubModule lines (MonsterSize, Animalia, TrollBruteForce) are 1.5.x content, not ported | Steps 4, 5 |
| `06252ae2c` test(infra): faster wiring scans, pin the comment scan | tests-repopaths-infra | Adapted: `MentionsOutsideComments` and its RepoPathsTests rows; the five wiring-test edits skipped (files absent on 1.4.x); WP105 Step 1 applies the `FeatureModulesTests` line when it creates that file | Step 2 |
| `a0db0558b` test(composition): read SubModule and IoC through one reader | plan-018 (shared, owned by WP 105) | Ported by cherry-pick: 25 conversions, `ParkedModels`; SignatureStrikes test dropped (feature absent); its RepoPaths parts land in Step 2 | Steps 2, 7 |
| `fa0b24e13` (hunk) RepoPathsTests CRLF probe | plan-018 (WP 105) | Taken inside the `06252ae2c` RepoPathsTests end state; WP 105 keeps ours for that path and ports the rest of `fa0b24e13` | Step 2 |
| `35007dbb8` (hunk) RepoPaths base | field-commission group (WP 129) | Only the RepoPaths file base is taken; its field-commission fix stays with WP 129 | Step 2 |
| `03d9db8dc` (hunk) CodeRoots, CSharpFiles | co-op (D7) | Only the two RepoPaths members, adapted to `Main`; everything else stays out (D7) | Step 2 |
| `346f3e093`, `e9b28e2ad`, `d021426b0`, `6d7e67f3f` | later edits to the guard files | Not here: WP 118 (load-stamps timing), D9 (RequiresGame tags), D7 (co-op client split) | none |
| `9ed3ee308`, `44045b348`, rest of `fa0b24e13`, `ecbc2199e`, `0f3af9577`, and the group's bookkeeping skips `667002d8d`, `71fd21698` | plan-018 | WP 105 owns them all | none |
| LOAD-TIME-04 (finding) | deep-dive | Closed by Step 4 | Step 4 |
| BHA0001 on Patch88 (verify report D4, WP-A) | new work | Scoped pragma | Step 1 |

## Test plan

- New tests: `TAOM.Tests/Infrastructure/RepoPathsTests.cs` (14 cases: ReadSource LF, an explicit
  CRLF file, either separator, missing file fails, StripComments keeps offsets, 6 MentionsOutsideComments
  rows, missing file throws, 2 CSharpFiles); `LanguageDataXmlTests.AllTranslationFiles_StringRowOutsideRootStrings_IsNeverPresent`;
  `PatchCategoryApplierTests` (14: constructor null checks, success, failure logged with cause, the
  next category still applies, summary null, summary names phase and groups in order, summary clears
  per phase, a real Harmony 2.4.2 unresolvable target, and five source gates over `SubModule.cs`: no
  direct `PatchCategory` call plus the index wiring, no report in `OnSubModuleLoad`, the startup
  inquiry, the localized notice, the three side effects); `PatchCategoryIndexTests` (6, over an
  assembly emitted at run time: Harmony's own apply throws `TypeLoadException` for a healthy category,
  Build skips only the broken class, a healthy category applies through the index, an unknown
  category is a silent no-op, an uncategorised class is neither skipped nor applied, a mixed category
  throws and keeps its first class patched); `GameModelOverrideBindingTests.ParkedModels_AreRealModels_ThatSubModuleDoesNotRegister`.
  Total 36 new test cases.
- Changed tests: the ten pinned spellings (Step 4c) and the 25 reader conversions (Step 7); no
  assertion logic changes.
- Pattern files: none needed, every test file is ported verbatim or from the named commit.
- Structurally untestable here (goes in `Not-tested:`): the live Harmony apply inside the game
  process, the inquiry and the red line on screen, and what the engine does with an exception that
  escapes TAOM.
- Verification: the Test command, `Failed: 0`, total = Step 0 total + 36.

## Done criteria (whole WP)

- [ ] Part A and Part B done criteria all hold.
- [ ] The changed-file set is exactly this plan's scope. Run (each in its own call):

```bash
git diff --name-only "$(git merge-base HEAD bannerlord-1.4.5)"..HEAD | wc -l
git diff --name-only "$(git merge-base HEAD bannerlord-1.4.5)"..HEAD | grep -c "^TAOM.Tests/Features/"
git diff --name-only "$(git merge-base HEAD bannerlord-1.4.5)"..HEAD | grep -v -E '^(\.claude/skills/deep-review/SKILL\.md|Main/(SubModule|PatchCategoryApplier|PatchCategoryIndex)\.cs|Main/Features/LordPartyTemplates/Hooks/Patch88_InitializeLordPartyPropertiesScope\.cs|Main/_Module/ModuleData/(taom_module_strings\.xml|Languages/[A-Za-z]+/std_taom_module_strings_[A-Za-z-]+\.xml)|TAOM\.Tests/(Features/[A-Za-z]+/[A-Za-z0-9]+Tests\.cs|Infrastructure/(RepoPaths|RepoPathsTests|PatchCategoryApplierTests|PatchCategoryIndexTests|Localization/LanguageDataXmlTests)\.cs|Migration/GameModelOverrideBindingTests\.cs)|docs/(ai-includes/architecture|features/battle-load-diagnostics|features/crash-report|reference/engine/submodule-lifecycle-and-harmony)\.md|tools/(translation_cache/[a-z]+\.json|triage_battle_load\.py))$'
```

  Expected: `66`, `25`, and no output. The 66 are 4 Main code files, 13 strings files, 12 caches,
  the triage tool, 4 docs, the SKILL.md, and 31 test files (25 under
  `TAOM.Tests/Features/`, `GameModelOverrideBindingTests.cs`, and 5 under `TAOM.Tests/Infrastructure/`).
  Subtract 1 for each of Step 1 or Step 6 you skipped, and say so in your report.
- [ ] `git diff --name-only "$(git merge-base HEAD bannerlord-1.4.5)"..HEAD | grep -c "^plans/"` prints 0.
- [ ] The Step 8 hand-off report is given.

## STOP conditions

Stop and report (do not improvise) if:

- Step 0 does not print `WP101-OK`, or `git show bannerlord-1.4.5:plans/README.md` shows WP 105 in
  progress or merged.
- You reach Step 4 and the `Baseline after WP101` section of that same `plans/README.md` does not
  name the public 2026-09-29 `LOTRLOME_Armory` as installed (D20).
- Any "Current state" excerpt does not match the live file, or `grep -c "_harmony.PatchCategory(" Main/SubModule.cs`
  is not 82 before Step 4e (another session or an earlier WP changed `SubModule.cs`).
- A precondition prints anything other than `IDENTICAL` (Steps 4c and 7).
- The Step 4d script raises an assertion, or the numstat is not exactly `8 0` plus 24 times `5 0`.
- After Step 4e the category comparison does not print `SAME-CATEGORIES`.
- `git cherry-pick -n a0db0558b` conflicts in any path beyond the three Step 7 names.
- Any test that passed at Step 0 fails, or a converted or pinned test still fails after its step;
  never edit an assertion to make it pass. A step's verification fails twice after a reasonable fix.
- A step seems to need `Main/IoC.cs`, `Main/TAOM.csproj`, `Dependencies/**`, `Directory.Build.props`,
  `.claude/settings*.json` or `CHANGELOG.md`.
- A hook blocks a commit: the CHANGELOG gate (never stage `CHANGELOG.md`, D1), the subject gate (fix
  only if your subject lacks `v2.0.28 - `), or the ModuleData validation gate on the Step 4 commit
  (D20: data-touching commits wait for the maintainer's laptop Armory update; never bypass).
- The build shows any BHA warning other than the Patch88 one, BHA0001 moves to another line, or
  `Main/TAOM.csproj` no longer pins BUTR.Harmony.Analyzer 1.0.1.50 or Lib.Harmony 2.4.2.
- A TaleWorlds or Harmony signature differs from "Engine and library facts" (for example taom-src
  shows a catch in `Module.InitializeSubModuleBases`); report the mismatch, do not re-derive.
- The Binding gate fails, or its skipped count rises above Step 0's.
- `git status` shows a modified file outside the in-scope list.

## In-game checks owed (maintainer)

Executors cannot run the game. After the integrator merges WP 104 (a build from the merged
`bannerlord-1.4.5`, deployed the normal way), check the newest `taom_debug_<timestamp>.log` (written
under `Logs/` relative to the game's working directory, `Main/Core/Logging/FileLogger.cs:26`, `:40`):

1. Boot to the main menu: no inquiry titled "TAOM" appears, and the log has no line containing `[PatchApply]`.
2. Start or load a campaign: the log has nine `[TableauDiag] PatchCategory '...' applied OK.` lines
   (Patch1, Patch2, Patch3, Patch4, Patch5, Late_Transpiler, Late_ActionSetOverride, Patch67,
   Patch72), still no `[PatchApply]`, and no red "TAOM: patch groups failed to apply" chat line.
3. Start the first battle: a `[BattleLoad] seq=1 ... phase=EncounterStart` line appears (Patch43
   applied, so the game-init batch ran through it), and no red failure line at mission start.
4. New campaign, character creation: the Player Switcher panel appears on the face generator (Patch77
   applied), and the log has no `Player Switcher disabled for this session` line
   (`PlayerSwitchPolicyProvider.cs:26-32` writes it on any disable).
5. Load a second save in the same process: it loads normally (the once-per-process guards are unchanged).
6. Not checked in game, by design: the failure notice itself. The unit tests drive real Harmony
   2.4.2 for both failure kinds; forcing a failure in game would need a deliberately broken build.

## Maintenance notes

- **Every new patch category goes through `TryPatchCategory("...")`.** The source gate
  `PatchCategoryApplierTests.MainSource_AppliesEveryPatchCategoryThroughTheGuardedHelper` fails on any
  direct `.PatchCategory(` call in `Main/`. Pick the lifecycle phase by the apply-timing rule in
  deep-review check 9.
- **WP 105 anchors** (copy them exactly; the plan 018 kernel tests read them):
  `TryPatchCategory("Patch42_CastleRecruitment");` ends the ProcessLoad batch;
  `ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_startup}startup"), persistent: true);`
  sits inside the Patch55 one-shot; `ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_game_init}game initialization"));`
  follows `TryPatchCategory("Patch69_TournamentEndGuard");`;
  `ReportPatchFailures(new TextObject("{=taom_patch_apply_phase_mission_start}mission start"));` sits
  in the mission-time block; `private bool TryPatchCategory(string category)` is the delegate the
  runner takes. `RepoPaths` and `RepoPathsTests` (with `fa0b24e13`'s CRLF test) already exist: WP 105
  keeps ours for both paths.
- **Later WPs**: WP 118 ports `346f3e093`'s per-group timing onto this applier without the co-op
  parts; WP 129 drops `35007dbb8`'s `RepoPaths.cs` hunk; WP 122 (plan 008) converts Inconclusive to
  Fail in files Step 7 already rewired, keeping the `RepoPaths.ReadSource` reads; WPs that call
  `RepoPaths.CSharpFiles()` get `Main` only.
- **Residuals plan 009 did not close**: a non-category statement in the game-init batch (an
  `Initialize` call, an `IoC.Resolve`) can still throw and abort the batch, and the failures recorded
  before it would then show at "mission start" (the deferral `bdf7d515c` recorded).
  `ManualPatchApplicator.cs` still makes its `harmony.Patch` calls without a guard (verify report D4,
  G4); a later robustness WP should decide whether to wrap them.
- **Strings**: the five keys are the 1.5.x AI first drafts; the localization WP (135) may revise them.
  Rows live inside `<strings>`; the Step 3 gate keeps it that way.
- **Docs still showing the old spelling**: `docs/features/alignment-aware-execution.md`, `arena.md`,
  `banner-color-persistence.md`, `battle-scenes.md`, `localization-override.md`,
  `settlement-nameplate-fade.md`, `skip-campaign-intro.md`, `weather-bounds-guard.md` and
  `docs/reference/worldmap-battle-scene-grid.md` show `_harmony.PatchCategory("...")` examples; the
  docs-sync WPs reconcile them.
- **What a reviewer should probe** (the orchestrator runs `/deep-review` on the branch before the
  merge; `/verify-bindings` afterwards): every former catch's side effect survives (Patch37 hooks
  subscribe only on success, a Patch77 failure still disables the switcher, the preview log still
  says FAILED); the Patch61 sub-categories now apply even when the main category fails (intended,
  per `45bcf80bd`); category order is unchanged (every edit is in place) and none was dropped (the 4e
  comparison); the startup inquiry cannot be shown twice (`CreateQuery` refuses a duplicate, and the
  report runs inside the Patch55 one-shot); no new string literal in `SubModule.cs` contains `//` or
  `/*` (the test readers strip comments without being literal-aware).
- **API snapshot**: `tools/snapshot_api_surface.ps1` is unaffected (no new patch class in TAOM.dll);
  its known staleness belongs to another WP.
