# Plan 109: Survive mission-start throws (no endless reload) and guard the engine's per-frame skeleton buffers

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Do not edit `plans/README.md`: every WP branch shares it, so the
> integrator records this plan's status and test count when merging; put the hand-off numbers in your
> final message. Three Parts (A, B, C) in a fixed order; each ends at a green full suite and can be
> handed off on its own.
>
> **Drift check (run first, Step 0.2)**:
> `git diff --stat 1b00d1acd..HEAD -- Main/SubModule.cs Main/Composition Main/Features/CrashReport Main/Features/CoopInterop/CoopSettingsRelevance.cs Main/_Module/ModuleData/taom_module_strings.xml Main/_Module/THIRD-PARTY-LICENSES.txt TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs TAOM.Tests/Migration Dependencies/Foundation .claude/rules/harmony-patches.md .claude/skills/engine-bump/SKILL.md docs/reference docs/features`
> Earlier WPs are expected to have changed several of these (WP104 and WP105: `Main/SubModule.cs`,
> `Main/Composition`; WP106: `CoopSettingsRelevance.cs`, `SettingsFingerprintTests.cs`,
> `harmony-patches.md`; WP107: `Dependencies/Foundation`, maybe `CrashReportSettings.cs`). Compare the
> live files with "Current state" and STOP only on a real mismatch: an anchor this plan names is
> missing or appears twice.

## Status

- **Priority**: P0
- **Effort**: L (three Parts, seven commits, 374 ported test results)
- **Risk**: MED. The skeleton buffer guard is TAOM's first write to engine code; it writes only after
  an exact byte match and stays off on any mismatch. The start-guard transpiler soft-fails to vanilla.
- **Depends on**: WP106 and WP107 merged, on top of wave 0 (WP101 to WP105) in `bannerlord-1.4.5`
- **Category**: bug (load hang and battle freeze guards)
- **Wave / lane**: wave 1, lane A
- **Executor**: laptop
- **Decisions applied**: D1 (never stage `CHANGELOG.md`; the commit body is the entry), D2 (both
  guards arrive as their 1.5.x `*Module.cs` plus a `FeatureModules.All` line), D5 (guard and watch on
  by default, v1.4.8 KnownBuilds row, self-disable on a mismatch), D7 (no co-op claims carried), D8
  (subjects carry `v2.0.28`), D9 (gates run locally), D10 (an ordinary session executes this), D17
  (this is its MissionStartGuard half; WP106 owns the tick handshake), D19 (git only through the Bash
  tool), D20 (a ModuleData commit needs the refreshed laptop Armory)
- **Planned at**: 1.4.x `origin/bannerlord-1.4.5` `1b00d1acd` and 1.5.x `origin/bannerlord-1.5.x`
  `e45f6df2c`, 2026-11-04
- **Issue**: port of #775 (mission-start guard), #776 (skeleton buffer guard and watch), #806 (native
  pin gate), and the #699 AfterStart dev trigger of `649cc30ff`

## Why this matters

On v1.4.8 `Mission.AfterStart` sets `CurrentState = Continuing` only as its last statement, after six
loops of start calls into every submodule, behaviour and mission object, and its caller
`MissionState.FinishMissionLoading` clears `_missionInitializing` first. A throw from any start call
(TAOM's own mission wiring, or a vanilla or mod behaviour reading bad TAOM data) leaves the mission
`Initializing`, so `TickLoading` loads the same mission again on a later frame, for ever. On this line
nothing reaches `taom_debug.log`: TAOM.Dependencies' SaveShield swallows the throw at
`FinishMissionLoading` and writes only to `Modules/TAOM.Dependencies/diag.log` (report V04). That is
an infinite loading screen with no evidence, the shape of the maintainer's many 1.4.8 infinite
loading screens; #699 caught it on 1.5.x (731 reload passes). The same engine has unchecked per-frame
skeleton reservations: one too many freezes a crowded battle for good, audio still playing, nothing
logged, which is the shape of the players' "freezes and stays that way" reports; #701 records 1.4.8
freezes that stopped when players lowered corpses and ragdolls. This session's read-only scan of the
installed `TaleWorlds.Native.dll` found every guard pattern exactly once. Neither mechanism is proven
in the E3/E4 logs, so the watch's per-mission peak line is also the instrument that tells whether
1.4.8 battles reach the limit. Last, TAOM's BattleLoad window closer sits after all of its mission
wiring with no `try`, so a wiring throw also leaves the window open and the 300 s stall watchdog
writes a spurious bundle.

## Current state

### Executor environment

Laptop; repo `C:\Users\mikew\source\repos\TAOM`; Bannerlord v1.4.8 at
`C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord` (env
`BANNERLORD_GAME_DIR`); never write into the game install. Every Bash call sets its own variables;
long runs need a tool timeout of 600000 ms. After Step 0.1 every call starts with:

```
WT=C:/Users/mikew/source/repos/taom-port/109; AUX=C:/Users/mikew/source/repos/taom-port; BASE=$(cat $AUX/109-base.txt)
```

### Prerequisites from earlier WPs (Step 0.3 checks each)

| From | What | Used by |
|---|---|---|
| WP104 | `TryPatchCategory` in `Main/SubModule.cs`; `TAOM.Tests/Infrastructure/RepoPaths.cs` with `RepoPath(params string[])` and `ReadSource(string relativePath, bool stripComments = false)` | runner phases; three ported wiring tests |
| WP105 | `Main/Composition/*` (`TaomFeatureModule`, `PatchCategoryDecl`, `MissionBehaviorDecl`, `ApplyPhase`, `FeatureModuleHooks`, `FeatureModules.All`); in `SubModule.cs` `FeatureModuleHooks.RunPhase(ApplyPhase.MainMenu\|GameInit\|FirstMission, TryPatchCategory);` and `FeatureModuleHooks.AddMissionBehaviors(mission, AddTaomBehavior);`; porting guide in `docs/features/composition-root.md` | both modules |
| WP107 | `PatchShieldPolicy.IsExcludedTargetMethod(string?, string?)` and `IsExcludedTargetNamespace(string?)` | `MissionStartGuardBindingTests.PatchShield_StillWrapsMissionAfterStart_ByNameAndByNamespace` |
| WP106 | the `- **Finalizer**:` bullet in `.claude/rules/harmony-patches.md` (`f75288ebf`) | placement of the Step B4 bullet only |

The composition API did not change between WP105's source (`1efdf2985`) and `ecaa67d26^`:
`git diff 1efdf2985 ecaa67d26^ -- Main/Composition/` touches only `FeatureModules.cs`.

### 1.4.x files and roles (`origin/bannerlord-1.4.5`; WP104 and WP105 move line numbers)

- `Main/SubModule.cs` (single-owner; edited in Step B2 only): `OnMissionBehaviorInitialize` `:1757`;
  `if (!_missionTimePatchesApplied)` `:1765`; local `void AddTaomBehavior(MissionBehavior behavior)`
  `:1782`; `ShaderPrecompileRunner.TryClaimMission(mission)` `:1795`; the closer `:1888-1890`
  (`if (battleLoadDiagSvc != null) AddTaomBehavior(new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(battleLoadDiagSvc, IoC.Resolve<Features.BattleLoadDiagnostics.IBattleLoadStallMarker>()));`
  under a comment starting `// BattleLoadDiagnostics phase-6`); `LogTaomBehaviorsDone` `:1910`; next
  member `protected override void OnApplicationTick(float dt)` `:1913`. No `try` around the body.
- `Main/Features/CrashReport/CrashReportSettings.cs` (`TAOM.CrashReport`, `json2`): Master group
  `EnableCrashCapture` (Order 0), `SuspendButterLibHandler` (1), `EnableNativeToManagedCapture` (2,
  `public bool EnableNativeToManagedCapture { get; set; } = true;` `:32`); QA group
  `ThrowOnNextMissionTick` (0), `ThrowOnNextApplicationTick` (1, `:51`, the last property). Six settings.
- `Main/Features/CrashReport/DevTriggers/CrashReportDevTrigger.cs`: `CrashReportDevTriggerMissionBehavior : MissionLogic`,
  `public override void OnMissionTick(float dt)` `:21`; no `AfterStart`.
- `Main/Features/CoopInterop/CoopSettingsRelevance.cs`: `Instrumentation` set `:52-69` (has
  `"EnableNativeToManagedCapture", "SuspendButterLibHandler", "WriteCrashBundle",` and
  `"ThrowOnNextMissionTick",`); `Presentation` set from `:101` (has `"EnableNativeSkinFixes",`).
- `TAOM.Tests/Features/CoopInterop/SettingsFingerprintTests.cs:213` `AssertSplit(typeof(CrashReportSettings), reflected: 6, covered: 0);`;
  `EveryDocQuotingTheSettingsCounts_AgreesWithReflection` (`:216-254`) needs `**<total>**` in
  `docs/features/coop-interop.md` and ` <total> MCM settings` in `docs/features/bannerlord-together-compat.md`
  (origin total 243, `**180 are simulation-relevant**`).
- `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs:37-42`: `RestartAllowlist` keyed `"Class.Property"`.
- `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`: 5-column `[DataRow]`s; last row `:105`, method `:106`.
- `TAOM.Tests/Migration/GameAssemblies.cs`: public `EnsureLoaded()` `:36` and `GameDir` `:28`;
  `ResolveGameDir()` is private and parameterless `:111-122`. The 1.5.x three-argument overload and
  `BuiltGameFolder` are plan 008 (WP122), absent.
- `TAOM.Tests/Infrastructure/Localization/UnregisteredLocalizationKeyBaselineTests.cs`: a `{=taom_*}`
  literal in C# with no row in a non-`Languages` ModuleData XML fails the build.
- `TAOM.Tests/Infrastructure/Localization/LanguageFileCoverageTests.cs`
  (`EveryLanguage_DeclaresARowForEveryEnglishKey`, no baseline or exemption on 1.4.x): every key in a
  non-`Languages` ModuleData XML needs a row with the same id in each of the 12
  `Languages/<LANG>/std_taom_module_strings_<code>.xml` files. `ecaa67d26` added none (1.5.x filled them in
  `a9437a492`), so this plan seeds English rows with the helper in "Commands you will need". Those files
  use raw CR LF rows, four-space indent and a closing `  </strings>` line; git treats them as `-text`.
- `Main/_Module/THIRD-PARTY-LICENSES.txt` (117 lines): no MIT text; ends with
  `TAOM.dll and the remaining binaries built by this project are original TAOM work,` (`:114`).
- `docs/reference/provenance-register.md`: table up to `<!-- provenance-register-end -->`, then
  `## Detail`, then `## Adding a row`; no VanillaTuning row. No checker reads it.
- `.claude/rules/harmony-patches.md` (118 lines): `## Patch Types` bullets Prefix, Postfix,
  Transpiler `:42-44`. `.claude/skills/engine-bump/SKILL.md` (143 lines): Phase 4 numbered items 1 to 6,
  then `## Phase 5`; it names neither KnownBuilds class.
- Absent (`git grep` on origin, 0 hits each): `MissionStartGuard`, `SkeletonBuffer`,
  `TickProfilerTranspiler`, `class CallSwap`, `ClipBudgetSignature`, `PeSectionTable`,
  `NativePinGateTests`, `SurviveMissionStartFailures`, `ThrowOnNextMissionAfterStart`, `Patch103`,
  `VanillaTuning`, `AddTaomMissionBehaviors`; highest patch category number in `Main` is 88;
  `TAOM.Tests/Features/LoadTimeStamps/RecordingLogger.cs` absent; `tools/native_decompile.py` absent,
  `tools/native_crash_triage.py --rva` present.

### Who swallows a mission-start throw on 1.4.x (V04; re-read on origin this session)

- `Dependencies/Foundation/SaveShield.cs:84` finalizes `("TaleWorlds.MountAndBlade.MissionState", ..., "FinishMissionLoading", "MISSION-INIT")`;
  `SaveShieldPolicy.ShouldSwallow` swallows every category without co-op unless
  `saveshield-swallow-disabled.flag` exists, and logs one `SaveShield` line
  `swallowed <Type> in TaleWorlds.MountAndBlade.MissionState.FinishMissionLoading [MISSION-INIT] culprit=...` to `diag.log`.
- `Dependencies/Foundation/PatchShield.cs:376-378`: after a missing-API swallow it unpatches the
  Prefix, Postfix and Transpiler of each owner that is not protected, never finalizers. TAOM's
  `com.taom.mod` is not protected (`PatchShieldPolicy.IsProtectedOwner` is a case-insensitive prefix
  match against entries such as `TAOM`), so Patch103's prefix can be stripped: that is the canary's
  premise. `ShieldFinalizerVoid` (`:244`) has no priority attribute.
- The one TAOM patch on `Mission.AfterStart` today is Patch43 (`Main/Features/BattleLoadDiagnostics/Hooks/Mission_AfterStart_BattleLoad_Patch.cs:14`).

### Engine facts (installed v1.4.8 via taom-src, this session)

- `TaleWorlds.MountAndBlade.Mission.cs:3799` `public void AfterStart()`: `:3807`
  `OnBeforeMissionBehaviorInitialize(this)`, `:3811` `OnBehaviorInitialize()`, `:3815`
  `OnMissionBehaviorInitialize(this)`, `:3819` `EarlyStart()`, `:3825` `AfterStart()` (loop
  `:3823-3826`), `:3829` `AfterMissionStart()`, `:3835` `CurrentState = State.Continuing;`.
  `private void WaitTickCompletion()` `:3585`; `public List<MissionBehavior> MissionBehaviors { get; }` `:1329`;
  `public AgentReadOnlyList AllAgents` `:1383` (`AgentReadOnlyList : MBReadOnlyList<Agent>`,
  `MBReadOnlyList<T> : List<T>`); `public void AddMissionBehavior(MissionBehavior)` `:4615` (calls
  only `OnCreated()`); `public T GetMissionBehavior<T>() where T : class, IMissionBehavior` `:4631`.
- `MissionState.cs`: `TickLoading` `:221-233` (calls `LoadMission()` while `!_missionInitializing`),
  `LoadMission` `:235` (`_missionInitializing = true;` `:242`), `FinishMissionLoading` `:333`
  (`_missionInitializing = false;` `:335`, `CurrentMission.AfterStart();` `:345`).
- `MBSubModuleBase.cs:101` `public virtual void OnMissionBehaviorInitialize(Mission mission)`, `:105`
  `public virtual void OnBeforeMissionBehaviorInitialize(Mission mission)`.
- `MissionBehavior.cs`: public virtual `OnBehaviorInitialize()` `:21`, `EarlyStart()` `:29`,
  `AfterStart()` `:33`, `OnMissionScreenPreLoad()` `:49`, `OnRemoveBehavior()` `:130`,
  `OnPreDisplayMissionTick(float)` `:142`, `OnMissionTick(float)` `:146`; `protected virtual void OnEndMission()` `:126`.
  `MissionObject.cs:170` `public virtual void AfterMissionStart()`. `MissionLogic : MissionBehavior`.
- `TWParallel.cs:13` `delegate void ParallelForWithDtAuxPredicate(int, int, float)`, `:72`
  `public static void For(int, int, float deltaTime, ParallelForWithDtAuxPredicate body, int grainSize = 16)`;
  `ScriptComponentBehavior.cs:215` `protected internal virtual void OnTick(float dt)`.
- `InformationManager.cs:61` `DisplayMessage(InformationMessage)`; `InformationMessage.cs:21`
  `(string information, Color color)`; `Color.cs:118` `FromUint(uint)`; `TextObject.cs:71`
  `TextObject(string value, Dictionary<string, object> attributes = null)`, `:334` `SetTextVariable(string, string)`.

### The native scan (installed `TaleWorlds.Native.dll`, read-only Python, this session)

14,185,944 bytes; SHA-256 `2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`.
Matches over all of `.text`: watch 1 (`0x88681`), pool-2 load 1 (`0x88475`), pool-1 site 1 (`0x886B4`,
bytes `41 8b f4 4c 89 7c 24 20 f0 0f c1 75 00`), foreign pool-1 shape 0, pool-2 site 1 (`0x898F0`,
bytes `4c 89 7c 24 20 44 8b fa f0 44 0f c1 39`), foreign pool-2 shape 0, ClipBudget 1 (`0x23AADF`).
Both loads name the pointer at `0xD96D18` (inside `.data`, 8-aligned). The `E8` at `0x8849E` (load
plus `0x29`) targets `0x898D0`, `0x20` before the pool-2 site; exactly four `E8` sites target
`0x898D0` (`0x8849E`, `0x35CB08`, `0x35CB97`, `0x4B4AF8`). Unwind ranges run `0x88440` to `0x88814`
(980 bytes). Spin loops (`movzx; test; jnz`, 7 bytes) at `0x88730`, `0x88744`, `0x89977`, `0x89990`.
`cmp qword [rdi+0x10], 0` at `0x884B6`; `movsx rbx, byte [rdi+0x44]` at `0x884C1`. ClipBudget: budget
site `0x23AB04`, counter `0xDA59B0` (`.data`), budget `0xB2BC9C` (`.rdata`, reads 12582912.0). Rows:

```
SkeletonBuffer: [14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0)
ClipBudget:     [14185944] = ("v1.4.8", 0x23AADF, 0x23AB04, 0xDA59B0, 0xB2BC9C)
```

### Conventions that bind this change

- ADR-007 adapters: services never touch sealed TaleWorlds types; engine and native calls stay in
  `Main/Adapters/*MissionStartGuard*` and `*SkeletonBuffer*` (`SkeletonBufferWiringTests.Service_TakesAdaptersOnly`).
- ADR-002 thin entry points (under 150 lines); ADR-003 no `#region`; ADR-004 no `[Obsolete]`;
  ADR-005 no `#if DEBUG` outside IoC registration.
- TDD: each step lands its tests first and shows the RED state before the code.
- MCM `json2`: a persisted value survives, so rename a key to change a default, never flip it. The
  five keys here are new and keep their 1.5.x names and defaults.
- `.claude/rules/harmony-patches.md`: `MovementOrder` postfixes go through the shared deferred
  category (none here); Step B4 adds the `__state` pairing rule.
- D2 and `docs/features/composition-root.md`: copy each `*Module.cs` as is, append it at the END of
  `FeatureModules.All`, never apply its category or add its behaviour by hand in `SubModule.cs` or
  `IoC.cs`. Neither module owns save data, declares game models or overrides `RegisterCampaignObjects`.
- Bookkeeping stays out (`CHANGELOG.md`, `CLAUDE.md`, `docs/reviews/**`, `docs/INDEX.md`,
  `docs/reference/feature-map.md`, `tools/README.md`). Two `.claude/` edits are binding exceptions: the
  B4 rule line (it documents the mechanism this plan ports) and the C2 engine-bump item
  (`NativePinGateTests` reads that file).

## Commands you will need

```
# Restore (once):                exit 0
cd $WT && dotnet restore TAOM.sln
# Build: expect exit=0 and "0 Error(s)" (the BHA0001 Patch88 warning is known)
cd $WT && dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId= > $AUX/109-build.log 2>&1; echo "exit=$?"; grep -E "[0-9]+ Error\(s\)" $AUX/109-build.log | tail -1
# Full tests: expect "Failed: 0" and the total each step names
cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= > $AUX/109-test.log 2>&1; grep -E "Passed!|Failed!" $AUX/109-test.log | tail -1
# Filtered: set FILTER="<the step's filter>" in the same Bash call first
cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "$FILTER" 2>&1 | grep -E "Passed!|Failed!"
# Binding gate: expect "Failed: 0"; record Skipped
cd $WT && dotnet test TAOM.Tests --no-build -p:DisableModuleCopy=true -p:ModuleId= --filter "TestCategory=BindingVerification" 2>&1 | grep -E "Passed!|Failed!"
# Data: no ERROR beyond the Step 0.5 file
cd $WT && python tools/validate_moduledata.py > $AUX/109-data-now.txt 2>&1; tail -15 $AUX/109-data-now.txt
# Docs: no counter above the Step 0.6 file, except missing_features +1 (Main/Features/MissionPerf)
cd $WT && python tools/lint_docs.py --summary --dash-base $BASE
```

Both `-p:` flags are mandatory (on 1.4.x CopyModule ignores `DisableModuleCopy`, so a build without
`-p:ModuleId=` deploys into the game). Never `./build.ps1`; never two builds at once in one worktree.

Message check before every commit (set `p`):

```
python - <<'PY'
import re
p = r"C:/Users/mikew/source/repos/taom-port/109-msg-XX.txt"
t = open(p, encoding="utf-8").read()
subj = t.splitlines()[0]
ok = bool(re.match(r"^[a-z][a-z0-9-]*(?:\([^)]+\))?!?: v2\.0\.28 - \S", subj)) and len(subj) <= 72
bad = any(c in t for c in "\u2013\u2014") or "Co-Authored-By" in t or "Generated with" in t
print("subject ok" if ok else "SUBJECT BAD", "| dash or attribution:", bad, "| longest line:", max(len(l) for l in t.splitlines()))
PY
```

Expected: `subject ok | dash or attribution: False | longest line:` and a number of at most 72.

Language rows helper (B1.2 and C1.2 run it from `$WT` with their `ROWS`; no provider call, no cost). It
stops on an assertion if a key is already present or a file lacks exactly one closing `  </strings>`;
that is a STOP. Expected output `ok 12`:

```
python -X utf8 - <<'EOF'
import glob, pathlib, xml.etree.ElementTree as ET
ROWS = [("<key>", "<English text>")]
files = sorted(glob.glob("Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml"))
assert len(files) == 12, files
for f in files:
    p = pathlib.Path(f)
    data = p.read_bytes()
    assert data.count(b"\n  </strings>") == 1, f
    for key, _ in ROWS:
        assert ('id="%s"' % key).encode() not in data, (f, key)
    rows = b"".join(('    <string id="%s" text="%s" />' % (k, t)).encode("utf-8") + b"\r\n" for k, t in ROWS)
    new = data.replace(b"\n  </strings>", b"\n" + rows + b"  </strings>", 1)
    ET.fromstring(new)
    p.write_bytes(new)
print("ok", len(files))
EOF
```

## Scope

**Create** (each step names its files): `Main/Features/MissionPerf/**` (4 files),
`Main/Features/MissionStartGuard/**` (10), `Main/Features/SkeletonBuffer/**` (12), six
`Main/Adapters/*MissionStartGuard*`/`*SkeletonBuffer*` files; tests under `TAOM.Tests/Features/MissionPerf/`
(6, one of them in C2), `LoadTimeStamps/RecordingLogger.cs`, `MissionStartGuard/` (6), `SkeletonBuffer/` (10),
`BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs`, and `TAOM.Tests/Migration/NativePinGateTests.cs`;
`docs/features/{mission-start-guard,skeleton-buffer-guard}.md`.

**Edit**: `Main/SubModule.cs` (single-owner: Step B2 only), `Main/Composition/FeatureModules.cs`,
`Main/Features/CrashReport/CrashReportSettings.cs`, `.../DevTriggers/CrashReportDevTrigger.cs`,
`Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/_Module/ModuleData/taom_module_strings.xml`,
`Main/_Module/THIRD-PARTY-LICENSES.txt`, the 12 `Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`
(English rows only); `SettingsFingerprintTests.cs`, `SettingRequireRestartPostureTests.cs`,
`ReflectionSiteBindingTests.cs`; `docs/features/{coop-interop,bannerlord-together-compat,crash-report,battle-load-diagnostics,mcm,composition-root}.md`,
`docs/reference/{harmony-patch-registry,provenance-register}.md`,
`docs/reference/taleworlds-api-snapshot/{patch-targets,reflection-sites}.md`; `.claude/rules/harmony-patches.md`,
`.claude/skills/engine-bump/SKILL.md`.

**Out of scope (do NOT touch)**: `Main/IoC.cs`, `Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`
(SDK globbing picks up new files), `Directory.Build.props`, `.claude/settings*.json`, `docs/adrs/**`,
`Dependencies/**` (WP107 owns it), `TAOM.Tests/Migration/GameAssemblies.cs` (WP122); NameplateCull,
MapViewRelease and what only they need (WP120: `BattleLoadDiagnosticsSettings.cs`, the two hot-path
provider tests, `CoopVetoClassificationTests.cs`); the 649cc30ff tracing in `CrashReportService.cs` and
`CrashReportPatchHelper.cs`; `CHANGELOG.md`, `CLAUDE.md`, `docs/reviews/**`, `docs/INDEX.md`,
`docs/reference/feature-map.md`, `docs/reference/engine/**`, `tools/README.md`,
`.claude/skills/native-crash-triage/SKILL.md`, `.ai/**`, `plans/**`.

## Git workflow

- Work only in `$WT` on `port148/109-mission-start-and-skeleton-guards`. Never push, open a PR, tag or
  merge into `bannerlord-1.4.5`. Git through the Bash tool only. Never `cherry-pick`, never `--no-verify`.
- Copy files from a commit with `git checkout <ref> <path>...` WITHOUT a `--` separator (the `--` form
  trips the dangerous-git hook). Name files exactly; use a directory path only for a directory that does
  not exist yet (Step 0.3 proves that). This also stages them.
- Stage explicit paths (never `git add -A`, `-u` or `.`), then `git commit -F <msgfile>` in the NEXT
  sequential Bash call. A hook that blocks is a STOP.
- Subject `<type>(<scope>): v2.0.28 - <description>` (at most 72 characters); body wrapped at 72 for
  1.4.x readers; no em or en dash; no `Co-Authored-By` or `Generated with` line; trailers `Research:`,
  `Not-tested:`, `Save-compat:` where noted, `Ported-from: <40-hex>` per included 1.5.x commit. Each step
  gives a message you may use as is (write it to `$AUX/109-msg-<step>.txt`).

## Steps

### Step 0: worktree, prerequisites, baseline

0.1 Create the worktree and store the base SHA:

```
cd /c/Users/mikew/source/repos/TAOM && mkdir -p /c/Users/mikew/source/repos/taom-port && git worktree add C:/Users/mikew/source/repos/taom-port/109 -b port148/109-mission-start-and-skeleton-guards bannerlord-1.4.5 && git -C C:/Users/mikew/source/repos/taom-port/109 rev-parse HEAD > C:/Users/mikew/source/repos/taom-port/109-base.txt && cat C:/Users/mikew/source/repos/taom-port/109-base.txt
```

Check in the main checkout's `plans/README.md` that WP106 and WP107 are merged; if not, STOP.

0.2 Run the header's drift check and read each changed file against "Current state".

0.3 Prerequisites and collisions (one call):

```
WT=C:/Users/mikew/source/repos/taom-port/109; cd $WT
grep -c "internal static readonly TaomFeatureModule\[\] All" Main/Composition/FeatureModules.cs
grep -cE "FeatureModuleHooks\.RunPhase\(ApplyPhase\.(MainMenu|GameInit|FirstMission), TryPatchCategory\);|FeatureModuleHooks\.AddMissionBehaviors\(mission, AddTaomBehavior\);" Main/SubModule.cs
grep -cE "public static string RepoPath\(|public static string ReadSource\(string relativePath, bool stripComments = false\)" TAOM.Tests/Infrastructure/RepoPaths.cs
grep -cE "public static bool IsExcludedTarget(Method|Namespace)\(" Dependencies/Foundation/PatchShieldPolicy.cs
grep -c '"FinishMissionLoading", "MISSION-INIT"' Dependencies/Foundation/SaveShield.cs
grep -nE "HarmonyPatchType\.(Prefix|Postfix|Transpiler|Finalizer)" Dependencies/Foundation/PatchShield.cs
for d in Main/Features/MissionStartGuard Main/Features/SkeletonBuffer Main/Features/MissionPerf TAOM.Tests/Features/MissionStartGuard TAOM.Tests/Features/SkeletonBuffer TAOM.Tests/Features/MissionPerf TAOM.Tests/Features/LoadTimeStamps TAOM.Tests/Migration/NativePinGateTests.cs; do test -e $d && echo "EXISTS $d"; done
git grep -n '"Patch103_' -- Main; echo "patch103 grep done"
f="/c/Program Files (x86)/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.Native.dll"; ls -l "$f" | awk '{print $5}'; sha256sum "$f" | cut -c1-64
```

Expected, in order: `1`; `4`; `2`; `2`; `1`; lines with `Prefix`, `Postfix`, `Transpiler` and none with
`Finalizer`; no `EXISTS`; nothing before `patch103 grep done`; `14185944`;
`2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`. Anything else: STOP.

0.4 Restore, Build, Full tests. Write T0 (Total, Passed, Skipped) to `$AUX/109-t0.txt`. `Failed: 0`
and the count the integrator recorded in `plans/README.md`; otherwise STOP.

0.5 `cd $WT && python tools/validate_moduledata.py > $AUX/109-data-before.txt 2>&1`.

0.6 `cd $WT && python tools/lint_docs.py --summary --dash-base $BASE > $AUX/109-lint-before.txt`.

## Part A: shared helpers

### Step A1: call-site swap and PE pattern helpers (no behaviour change)

Sources, all unchanged on 1.5.x since `ecaa67d26` compiled against them: `TickProfilerTranspiler.cs`
(end state of `6eb2e1b90` and `daba6f0a0`), `ClipBudgetSignature.cs`, `PeSectionTable.cs`,
`SyntheticNativeImage.cs` and two tests (`a9f10688f`), `RecordingLogger.cs` (`346f3e093`, at its 1.5.x
path, so WP118's later pick meets an identical file). Method: path-limited checkout plus one new file;
pure additions, nothing to merge. Tip-file checkouts name the planned 1.5.x commit `e45f6df2c`, not the
moving `origin/bannerlord-1.5.x` (it has advanced since; these paths are unchanged).

RED: `cd $WT && git checkout e45f6df2c TAOM.Tests/Features/MissionPerf/TickProfilerTranspilerTests.cs TAOM.Tests/Features/MissionPerf/TickProfilerTranspilerOccurrenceTests.cs TAOM.Tests/Features/MissionPerf/AnimMemory/SyntheticNativeImage.cs TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureTests.cs TAOM.Tests/Features/MissionPerf/AnimMemory/PeSectionTableTests.cs TAOM.Tests/Features/LoadTimeStamps/RecordingLogger.cs`.
Build must FAIL with `CS0246`/`CS0234` naming `TickProfilerTranspiler`, `CallSwap`, `TickProfileLines`,
`ClipBudgetSignature` or `PeSectionTable`.

GREEN: `cd $WT && git checkout e45f6df2c Main/Features/MissionPerf/TickProfilerTranspiler.cs Main/Features/MissionPerf/AnimMemory/ClipBudgetSignature.cs Main/Features/MissionPerf/AnimMemory/PeSectionTable.cs`,
then create `Main/Features/MissionPerf/TickProfileLines.cs` with exactly this content (the members are
byte copies of `ecaa67d26:Main/Features/MissionPerf/TickProfileLines.cs` `:24`, `:121-127`, `:187-190`,
`:203-208`, `:258-259`; the full file needs WP117's profiler types):

```csharp
using System;
using System.Globalization;

namespace TAOM.Features.MissionPerf;

/// <summary>
/// The status lines of the soft-failing call-site swap (<see cref="TickProfilerTranspiler"/>), trimmed from the
/// 1.5.x line's TickProfileLines to what that swap writes. WP117 (the tick profiler port) replaces this file with
/// the full one. Status lines carry <see cref="StatusTag"/>, and an exception message is quoted with its square
/// brackets turned into parentheses.
/// </summary>
public static class TickProfileLines
{
    public const string StatusTag = "[TickProfiler]";

    public static string BuildSiteCountWarning(string method, string target, int count, int expected = 1) =>
        string.Format(CultureInfo.InvariantCulture,
            "{0} {1}: {2} matched {3} times, expected {4}; {5}",
            StatusTag, method, target, count, expected, LeftVanilla(method));

    public static string BuildHelperMismatchWarning(string method, string helper, string target) =>
        StatusTag + " " + method + ": helper " + helper + " does not fit " + target + "; " + LeftVanilla(method);

    /// <summary>A transpiler that threw: its method stays vanilla, with the consequence for the profiler.</summary>
    public static string BuildRewriteFault(string method, Exception ex) =>
        StatusTag + " " + method + " rewrite failed: " + ex.GetType().Name + ": " + Quote(ex.Message)
        + "; " + LeftVanilla(method);

    private static string LeftVanilla(string method) => method + " left vanilla, so " + method switch
    {
        "Mission.OnTick" => "no mission times behaviours by type (with 'Enable Hitch Probe' on, missions still measure in probe mode)",
        "Mission.OnPreTick" => "preTickMs reads 0 and that time lands in otherMs (the hitch probe still times the wait)",
        _ => "per-type attribution records nothing for it (the hitch probe's totals stay)",
    };

    internal static string Quote(string? message) =>
        (message ?? string.Empty).Replace('[', '(').Replace(']', ')').Replace('\r', ' ').Replace('\n', ' ');
}
```

**Verify**: Build `0 Error(s)`; Filtered with
`FILTER="FullyQualifiedName~TickProfilerTranspiler|FullyQualifiedName~ClipBudgetSignatureTests|FullyQualifiedName~PeSectionTableTests"`:
`Failed: 0`, `Passed: 39`, `Skipped: 0`; Full tests: T0 + 39, Skipped as T0.

Commit A1: `git add Main/Features/MissionPerf/TickProfileLines.cs` (the checkouts staged the other nine);
`git status --porcelain` shows exactly ten `A` lines. Message:

```
feat(mission-perf): v2.0.28 - call-site swap and PE scan helpers

Adds the helpers the mission-start guard and the skeleton buffer
guard build on, copied from the 1.5.x line: the soft-failing
call-site swap (TickProfilerTranspiler, CallSwap), a trimmed
TickProfileLines with only the warnings that swap writes, and the PE
section and byte-pattern helpers (PeSectionTable,
ClipBudgetSignature). RecordingLogger joins the test helpers at its
1.5.x path. Nothing calls them yet; players see no change.

Research: taom-src v1.4.8 MissionBehavior.OnMissionTick and
OnPreDisplayMissionTick, Mission.WaitTickCompletion,
ScriptComponentBehavior.OnTick, TWParallel.For with a dt delegate
Not-tested: nothing runtime; the helpers have no caller yet
Ported-from: 6eb2e1b90c2fe42d6cda851434c0ff6ec1fdcd1c
Ported-from: daba6f0a065bb53424c312119c1063aa78f9f2db
Ported-from: a9f10688fbbb2e540b36c5fb593dda71811a881a
Ported-from: 346f3e093ccc2c146107a3b7e005c0c06963cd85
```

**Part A done**: commit A1 exists; Full tests T0 + 39, `Failed: 0`.

## Part B: mission-start guard

### Step B1: MissionStartGuard (Patch103)

Source `ecaa67d26` (feat(stability): v2.0.34 - mission start, freeze and map guards), MissionStartGuard
half only. Simulation this session (`git merge-tree --write-tree --name-only --no-messages
--merge-base=ecaa67d26^ origin/bannerlord-1.4.5 ecaa67d26`, exit 1): 38 conflicted paths, of which 12
code, data and test files (`FeatureModules.cs` (absent at origin), `BattleLoadDiagnosticsSettings.cs`,
`CoopSettingsRelevance.cs`, `CrashReportSettings.cs`, `SubModule.cs`, `taom_module_strings.xml`,
`THIRD-PARTY-LICENSES.txt`, `CampaignHotPathSettingsProvidersTests.cs`, `SettingsFingerprintTests.cs`,
`SettingRequireRestartPostureTests.cs`, `HotPathSettingsProvidersTests.cs`, `ReflectionSiteBindingTests.cs`),
three `.claude/` files and 23 docs, plans and tools paths. Method: path-limited checkout of the new
files plus the hand edits below; a `cherry-pick -n` would also bring WP120's NameplateCull and
MapViewRelease. Resolution per conflicted file: this plan hand-edits `FeatureModules.cs`,
`CoopSettingsRelevance.cs`, `CrashReportSettings.cs`, `taom_module_strings.xml`,
`THIRD-PARTY-LICENSES.txt`, `SettingsFingerprintTests.cs` and `ReflectionSiteBindingTests.cs` (B1, C1),
`SubModule.cs` (B2) and `SettingRequireRestartPostureTests.cs` (C1), keeping every 1.4.x line and adding
only the guard's; `BattleLoadDiagnosticsSettings.cs` and the two hot-path provider tests carry only
WP120 hunks (untouched); of the `.claude/` files the engine-bump hunk is adapted in C2 and the other two
are deferred; the doc paths follow the Disposition.

B1.0 Record R0, the total of Filtered `FILTER="FullyQualifiedName~ReflectionSiteBindingTests"`.

B1.1 RED: `cd $WT && git checkout ecaa67d26 TAOM.Tests/Features/MissionStartGuard`. Build must FAIL
with `CS0246`/`CS0234` naming `TAOM.Features.MissionStartGuard` types.

B1.2 GREEN: `cd $WT && git checkout ecaa67d26 Main/Features/MissionStartGuard Main/Adapters/IMissionStartGuardAdapter.cs Main/Adapters/MissionStartGuardAdapter.cs`, then:

1. `Main/Features/MissionStartGuard/Hooks/MissionStartGuardSwaps.cs` line 12: `installed v1.5.4 body`
   becomes `installed v1.4.8 body` (keep the line count: the reflection rows cite lines 23 to 28 and 32).
   `Hooks/Mission_AfterStart_MissionStartGuard_Patch.cs` line 24: `// Priority.First, as Patch98 does: ahead of`
   becomes `// Priority.First: ahead of` (Patch98 is not on 1.4.x).
2. `Main/Composition/FeatureModules.cs`: append as the LAST entry of `All`:
   `        new Features.MissionStartGuard.MissionStartGuardModule(),`
3. `CrashReportSettings.cs`: after `public bool EnableNativeToManagedCapture { get; set; } = true;` insert
   a blank line and:

```csharp
    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Survive Mission Start Failures", Order = 3, RequireRestart = false,
        HintText = "When a mission behavior, a module's mission-start callback or a mission object throws in one of the six start calls of Mission.AfterStart while a mission is loading, log it, show a short message, cut that call short and let the mission start, instead of the game loading the mission again every frame for ever. A throw elsewhere in the load, such as a behavior's OnMissionScreenPreLoad, is not covered. Enable Crash Capture does not affect this setting. When off, the guard lets the exception pass through untouched. Takes effect immediately. Default ON.")]
    public bool SurviveMissionStartFailures { get; set; } = true;
```

4. `CoopSettingsRelevance.cs`, `Instrumentation`: after the line with
   `"EnableNativeToManagedCapture", "SuspendButterLibHandler", "WriteCrashBundle",` insert:

```csharp
        // Whether a throw out of a mission start call is survived (Patch103): crash containment, filed here with
        // EnableCrashCapture and EnableNativeToManagedCapture, which also change what runs after an exception.
        "SurviveMissionStartFailures",
```

5. `SettingsFingerprintTests.cs`: `AssertSplit(typeof(CrashReportSettings), reflected: ...)` +1 (`covered: 0` stays).
   `docs/features/coop-interop.md`, paragraph `**No MCM settings parity: reported, not yet exchanged.**`:
   `**<total>**` +1, the `CrashReportSettings` split number +1, and the "excluded" figure set to total
   minus the simulation-relevant count. `docs/features/bannerlord-together-compat.md`: `TAOM's <total> MCM settings` +1.
6. `taom_module_strings.xml`: above the closing `</strings>`, after the last `<string .../>`, add (tab indent):

```xml
	<!-- MissionStartGuardAdapter -->
	<string id="taom_mission_start_guard_notice" text="{=taom_mission_start_guard_notice}{MODULE} failed in {CALL} ({EXCEPTION}). The mission goes on, with that step cut short." />
```

   Then run the Language rows helper with
   `ROWS = [("taom_mission_start_guard_notice", "{MODULE} failed in {CALL} ({EXCEPTION}). The mission goes on, with that step cut short.")]`
   and `git add Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`.

7. `ReflectionSiteBindingTests.cs`: directly above `public void ReflectionSite_ResolvesAgainstInstalledEngine(` insert:

```csharp
    // --- MissionStartGuard (Patch103): the six start calls the transpiler swaps, found by AccessTools.Method at install.
    // A miss makes AccessTools.Method return null; Rewrite's fit check rejects the null target, so Mission.AfterStart stays vanilla.
    [DataRow("TaleWorlds.MountAndBlade.MBSubModuleBase", "MBSubModuleBase", "OnBeforeMissionBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:23,32")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "OnBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:24,32")]
    [DataRow("TaleWorlds.MountAndBlade.MBSubModuleBase", "MBSubModuleBase", "OnMissionBehaviorInitialize", "Method", "MissionStartGuardSwaps.cs:25,32")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "EarlyStart", "Method", "MissionStartGuardSwaps.cs:26,32")]
    [DataRow("TaleWorlds.MountAndBlade.MissionBehavior", "MissionBehavior", "AfterStart", "Method", "MissionStartGuardSwaps.cs:27,32")]
    [DataRow("TaleWorlds.MountAndBlade.MissionObject", "MissionObject", "AfterMissionStart", "Method", "MissionStartGuardSwaps.cs:28,32")]
```

**Verify B1.2**: Build `0 Error(s)`. `FILTER="FullyQualifiedName~TAOM.Tests.Features.MissionStartGuard"`:
`Failed: 0`, `Total: 84`, `Skipped: 0` (a skip means the game assemblies did not load: report it).
ReflectionSiteBindingTests: `Failed: 0`, total R0 + 6. `FILTER="FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~UnregisteredLocalizationKey|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~TAOM.Tests.Composition"`:
`Failed: 0`. `Rewrite_OnTheInstalledMissionAfterStart_SwapsExactlySixSitesWithNoWarning` must pass: it
proves the six swaps on the installed IL.

B1.3 Docs, notices, registry. First `cd $WT && git checkout ecaa67d26 docs/features/mission-start-guard.md`,
then apply these replacements in it (each FIND text occurs once):

```
FIND:    [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)     (twice: Overview, GitHub Issue)
REPLACE: `docs/reviews/adopt-yotthani-2026-10-08.md` on the 1.5.x line (not ported)

FIND:    (v1.5.4, decompile `Mission.cs:3815-3852`)
REPLACE: (v1.4.8, taom-src `Mission.cs:3799-3836`)

FIND:    the sentence from "It is the only caller:" to "no other call to `Mission.AfterStart`."
REPLACE: On the 1.5.x line (v1.5.4) a review of ten module assemblies found no other caller; that census was not repeated on v1.4.8.

FIND:    TAOM's own behaviours guard themselves (#699), but any vanilla or other-mod behaviour, submodule
         or mission object can still loop the game, and TAOM cannot guard what it does not own.
REPLACE: On 1.4.x several of TAOM's own `AfterStart` overrides have no `try` yet (`EnlistmentMeritMissionBehavior`,
         `FieldCommissionMissionLogic`; the #699 hardening is WP111's), and any vanilla or other-mod behaviour,
         submodule or mission object can loop the game too.

FIND:    isolating each registration is issue
         draft 11
REPLACE: isolating each registration is still open on both lines

FIND:    the whole "- **Without this feature:**" bullet
REPLACE: - **Without this feature:** the loading screen never ends, and the player can only kill the game. On 1.4.x
         nothing reaches `taom_debug.log`: TAOM.Dependencies' SaveShield catches the throw first (its MISSION-INIT
         finalizer on `MissionState.FinishMissionLoading`) and writes one line per pass to
         `Modules/TAOM.Dependencies/diag.log`, so the log shows the BattleLoad phases repeating with no
         `MissionAfterStartDone` and no `[CrashReport]` block. TAOM's crash capture (Patch37) sees the throw only
         when `saveshield-swallow-disabled.flag` exists.

FIND:    each present exactly once in the installed v1.5.4 body
REPLACE: each present exactly once in the installed v1.4.8 body

FIND:    the body of "## PatchShield and co-op"
REPLACE: `Mission.AfterStart` is also patched by Patch43 (BattleLoadDiagnostics), so PatchShield's second pass
         attaches its finalizer to it from a process's second game start; in the first game it is unshielded (TAOM's
         game-init patches apply after that pass). The guard's void finalizer runs at `Priority.First`, ahead of
         PatchShield's, and only observes, so the shield still decides alone: it swallows a missing-method,
         missing-field or type-load exception and hands any other back. A wrapped call catches a throw before it
         reaches the shield. After a swallow PatchShield unpatches an owner's prefixes, postfixes and transpilers,
         never its finalizers; that is what the canary watches for (above). No bool prefix exists, so
         `CoopVetoClassificationTests` has nothing to classify. Co-op is not supported on the 1.4.x line, and the
         1.5.x line's census of installed co-op assemblies was not repeated.

FIND:    in "How to check it in game" step 2, from "TAOM's crash" to the end of the step
REPLACE: On 1.4.x TAOM.Dependencies' SaveShield then swallows it (one `SaveShield` line `swallowed
         TaomDevTriggerException in TaleWorlds.MountAndBlade.MissionState.FinishMissionLoading [MISSION-INIT]` in
         `Modules/TAOM.Dependencies/diag.log`; no `[CrashReport]` block, no dialog), and the engine loads the
         mission again, which runs every submodule's `OnMissionBehaviorInitialize` on the same mission a second
         time. The trigger resets itself before it throws. Record, rather than assume, what the second load does:
         whether the battle starts, whether it loads again, and how many `not survived` lines appear.

ADD to "## Changelog":     - <today>: ported to the 1.4.x line (Bannerlord v1.4.8) by plan 109; applied at GameInit through the feature-module runner.
ADD to "## GitHub Issue":  - **1.4.x port:** plans/109-mission-start-and-skeleton-guards.md.
```

Before writing the PatchShield paragraph, confirm both PatchShield facts in the live
`Dependencies/Foundation/PatchShield.cs` (WP107 edited it); if one changed, write what the file shows.
Then `grep -n "v1\.5\|adopt-yotthani-2026-10-08.md\](" docs/features/mission-start-guard.md`: every hit
left must attribute something to the 1.5.x line, never state the current engine.

Then:

1. `docs/features/crash-report.md`, settings table, after the `Enable Native-to-Managed Capture` row:
   `| Master | Survive Mission Start Failures | true | A behaviour, submodule or mission object that throws in one of the six start calls of `Mission.AfterStart` is logged, its call is cut short, and the mission starts, instead of the engine loading the mission again for ever ([mission-start-guard.md](mission-start-guard.md), Patch103). Independent of Enable Crash Capture. Live: read when an exception arrives. |`
2. `docs/reference/harmony-patch-registry.md`, directly above `## Patch_MissionTime_SetMovementOrder`:

```markdown
## Patch103_MissionStartGuard

**Target:** `public void Mission.AfterStart()` (v1.4.8, taom-src `Mission.cs:3799-3836`; non-virtual, no parameters), one class `Mission_AfterStart_MissionStartGuard_Patch` in `Main/Features/MissionStartGuard/Hooks/`, category `Patch103_MissionStartGuard`, declared by `MissionStartGuardModule` and applied at `ApplyPhase.GameInit` by the feature-module runner: a transpiler, a void prefix (`object __instance`, `out bool __state`, `Priority.First`), a void finalizer (`Exception __exception`, `bool __state`, `Priority.First`) and a void `[HarmonyCleanup]` method (`Exception ex`).

Mission-start guard (ported from the 1.5.x line, #775; adapted from yotthani's VanillaTuning `mission-start-guard`, MIT). `Mission.AfterStart` sets `CurrentState = Continuing` last (`:3835`); `MissionState.FinishMissionLoading` clears `_missionInitializing` (`MissionState.cs:335`) before calling it (`:345`), so a throw from any start call leaves the mission `Initializing` and `TickLoading` (`:221`) runs `LoadMission` and `AfterStart` again, for ever. On 1.4.x TAOM.Dependencies' SaveShield swallows that throw at `FinishMissionLoading` (MISSION-INIT) and logs only to its `diag.log`. The transpiler (`TickProfilerTranspiler.Rewrite`, soft-failing) swaps the six `callvirt` sites (`:3807`, `:3811`, `:3815`, `:3819`, `:3825`, `:3829`) for same-named static helpers on `MissionStartGuardCalls` (a `try` with an exception filter); any other site count leaves the method vanilla and the install line says `OFF: wrapped N of 6`. Not wrapped: the spawn-path selector and deployment plan (`:3821-3822`), the weather model (`:3831-3834`) and a behaviour's `OnMissionScreenPreLoad` (`MissionState.LoadMission`).

**PatchShield and Patch43.** Patch43 (`Mission_AfterStart_BattleLoad_Patch`, prefix and postfix) also patches the method, so PatchShield shields it from a process's second game start. The guard's finalizer runs first and only observes. PatchShield unpatches an owner's prefix, postfix and transpiler after a swallow, never its finalizers, so the prefix is the canary: it hands the finalizer `__state = true`, and the finalizer writes one WARNING per process when that flag is false or the live swap count is not 6.

**Co-op.** A void prefix only, so `CoopVetoClassificationTests` needs no entry; the toggle is instrumentation in `CoopSettingsRelevance`.

Threads: main thread only. Tests: `MissionStartGuardBindingTests` (real IL: each call once, six swaps, the class applied to the real method), `MissionStartGuardServiceTests`, `MissionStartGuardCallsTests`, `MissionStartGuardHookTests`, `MissionStartGuardCanaryTests`, `MissionStartGuardModuleTests`. Feature: [mission-start-guard.md](../features/mission-start-guard.md). **ACTIVE**, not yet checked in game (CrashReport's "Throw On Next Mission AfterStart" is the check).
```

3. `docs/reference/taleworlds-api-snapshot/patch-targets.md`: `Patches: N.` +1, and directly above the
   `TAOM.Features.MixedFormations.Hooks.Patch30_FormationGetOrderPositionOfUnit` row insert
   ``| `TAOM.Features.MissionStartGuard.Hooks.Mission_AfterStart_MissionStartGuard_Patch` | `TaleWorlds.MountAndBlade.Mission.Void AfterStart()` |``
4. `docs/reference/taleworlds-api-snapshot/reflection-sites.md`, Category B table, after its last row,
   six rows in the table's format; the first is
   ``| `TaleWorlds.MountAndBlade.MBSubModuleBase` | `OnBeforeMissionBehaviorInitialize` | method (public virtual) | `MissionStartGuardSwaps.cs:23,32` | MissionStartGuard (Patch103). One of six start calls the transpiler swaps for a same-named helper, found by `AccessTools.Method`. Missing: the fit check rejects the null target and `Mission.AfterStart` stays vanilla (`OFF: wrapped 0 of 6`) |``
   and the other five are `MissionBehavior`/`OnBehaviorInitialize` (`:24,32`), `MBSubModuleBase`/`OnMissionBehaviorInitialize`
   (`:25,32`), `MissionBehavior`/`EarlyStart` (`:26,32`), `MissionBehavior`/`AfterStart` (`:27,32`) and
   `MissionObject`/`AfterMissionStart` (`:28,32`), purpose `MissionStartGuard. As above`. Below the table,
   above the existing `Status (...)` lines, add `Status (<today>): MissionStartGuard (Patch103) adds six
   method rows; they resolve against installed v1.4.8 (ReflectionSiteBindingTests <N>/<N>).` with your B1.2 numbers.
5. `Main/_Module/THIRD-PARTY-LICENSES.txt`: after the line `_Module/GUI/Fonts/. Each is listed below.` add
   the line `It also covers the parts of TAOM.dll adapted from yotthani's VanillaTuning (MIT).` Directly
   above the paragraph `TAOM.dll and the remaining binaries built by this project ...` insert the block
   below. The placeholder line is the full MIT text: the 19 lines (from `The MIT License (MIT)` to
   `THE SOFTWARE.`) that `git -C $WT show e45f6df2c:Main/_Module/THIRD-PARTY-LICENSES.txt | sed -n '44,62p'`
   prints, pasted verbatim (the provenance rule needs the whole notice, not a pointer):

```
VANILLATUNING PORTIONS  --  inside TAOM.dll

TAOM.dll contains code adapted from VanillaTuning by yotthani, with TAOM's changes:
the mission-start guard (source: Main/Features/MissionStartGuard/ and its adapter
in Main/Adapters/, in the TAOM repository).

Copyright (c) 2026 yotthani
Licensed under the MIT License:

<the 19 MIT lines printed by the command above>

----

```

   Then change the two lines `TAOM.dll and the remaining binaries built by this project are original TAOM work,`
   / `licensed under the MIT License.` to `TAOM.dll, apart from the VanillaTuning portions named above,` /
   `and the remaining binaries built by this project are original TAOM work, licensed under the MIT License.`
   (`.claude/rules/provenance.md`: never assert ownership you do not have).
6. `docs/reference/provenance-register.md`: as the last row before `<!-- provenance-register-end -->`:
   ``| Yotthani VanillaTuning | `VanillaTuning` `HoN/VanillaTuning` | MIT, (c) 2026 yotthani (`HoN/VanillaTuning/LICENSE`, since `1b9ce3ba`, 2026-10-07) | behavioural-port | `Main/Features/MissionStartGuard/**`; its adapter `Main/Adapters/{I,}MissionStartGuardAdapter.cs`; its tests `TAOM.Tests/Features/MissionStartGuard/**` | cleared |``
   (licence cell copied from the 1.5.x register row at `ecaa67d26`). Directly above `## Adding a row`:

```markdown
### Yotthani VanillaTuning

yotthani's `HoN/VanillaTuning` carries an MIT licence (recorded on the 1.5.x line: added in its `1b9ce3ba`, 2026-10-07). The 1.5.x line adapted four of its patches on 2026-10-08 (adoption record `docs/reviews/adopt-yotthani-2026-10-08.md` on `origin/bannerlord-1.5.x`); the 1.4.x line ports them through plans 109 and 120.

`Main/Features/MissionStartGuard/**` adapts its `mission-start-guard`: the idea (a start method that throws no longer leaves the mission loading for ever) and the finalizer's report are his; TAOM wraps the six start calls of `Mission.AfterStart` with one transpiler instead of patching each behaviour type, so the code is TAOM's own in structure. The MIT notice is in `Main/_Module/THIRD-PARTY-LICENSES.txt`.
```

**Verify B1.3**: `grep -c "Permission is hereby granted"` and `grep -c "THE SOFTWARE IS PROVIDED"` on
`Main/_Module/THIRD-PARTY-LICENSES.txt` are each one more than before this step; `grep -c "Yotthani VanillaTuning" docs/reference/provenance-register.md` is
2; `grep -c "Patch103_MissionStartGuard" docs/reference/harmony-patch-registry.md` is at least 2; Docs
and Data commands clean; Full tests T(A) + 90 (84 + 6), `Failed: 0`.

Commit B1: `git add` every path of B1.1 to B1.3; `git status --porcelain` shows nothing else. Message:

```
feat(stability): v2.0.28 - survive mission start throws (Patch103)

A mission whose start calls threw loaded again every frame for ever,
and TAOM.Dependencies' SaveShield swallowed the throw silently, so
players saw an endless loading screen with no report. A transpiler on
Mission.AfterStart now wraps its six start calls: a call that throws
is logged with its type and assembly, shown once on screen and cut
short, and the mission starts. MCM: TAOM Crash Report, "Survive
Mission Start Failures", on. Adapted from yotthani's VanillaTuning
(MIT); the notice is in THIRD-PARTY-LICENSES.txt.

Research: taom-src v1.4.8 Mission.AfterStart (:3799-3836, six calls
once each), MissionState.TickLoading, LoadMission and
FinishMissionLoading; SaveShield MISSION-INIT row
Not-tested: Patch103 in a live mission; the on-screen notice
Save-compat: no saved state; one new MCM key, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
```

### Step B2: the BattleLoad closer survives a wiring throw

Source: `ecaa67d26`'s `Main/SubModule.cs` hunk and `BattleLoadCloserWiringTests.cs` (unchanged on
1.5.x since). Method: hand re-implementation; the 1.5.x hunk's context lines differ.

B2.1 RED: `cd $WT && git checkout ecaa67d26 TAOM.Tests/Features/BattleLoadDiagnostics/BattleLoadCloserWiringTests.cs`;
Build; `FILTER="FullyQualifiedName~BattleLoadCloserWiringTests"` must show at least 1 failed of 4.

B2.2 GREEN, in `Main/SubModule.cs`:

1. Check `OnMissionBehaviorInitialize`: it starts with `base.OnMissionBehaviorInitialize(mission);` then
   the comment and `if (!_missionTimePatchesApplied)` block, holds exactly one
   `new Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior(` (the closer under
   `// BattleLoadDiagnostics phase-6`) and no `try` around the body. Otherwise STOP.
2. Move every statement after `base.OnMissionBehaviorInitialize(mission);` to the end of the method into
   a new `private void AddTaomMissionBehaviors(Mission mission)` placed directly after it, same order,
   unchanged, EXCEPT the closer: delete the `// BattleLoadDiagnostics phase-6` comment block and the
   `if (battleLoadDiagSvc != null)` statement with its two continuation lines.
3. Make the new method's header and the remaining `OnMissionBehaviorInitialize` equal to
   `git show ecaa67d26:Main/SubModule.cs | sed -n '2044,2091p'` (print it): lines 2044 to 2086 are the
   whole new `OnMissionBehaviorInitialize` (`base` call, the moved closer comment, `try {
   AddTaomMissionBehaviors(mission); } finally { ... }` adding the closer only when
   `mission.GetMissionBehavior<Features.BattleLoadDiagnostics.Hooks.BattleLoadPhaseBehavior>() == null`,
   no `catch`); lines 2088 to 2091 are the three-line comment and the signature of `AddTaomMissionBehaviors`.
   Copy those lines verbatim (they bind only members 1.4.x has).
4. Replace `MissionDiagnostic, BattleLoadPhase, the CrashReport dev trigger` with
   `MissionDiagnostic, the CrashReport dev trigger` wherever it occurs in `Main/SubModule.cs`,
   `Main/Composition/FeatureModules.cs` and `docs/features/composition-root.md` (WP105's wording; if
   that doc does not exist, leave it out here and in the verify grep).
5. `docs/features/battle-load-diagnostics.md`:

```
FIND:    | TAOM's own behaviors, each stamped by name |            (end of phase table row 4c)
REPLACE: | TAOM's own behaviors, each stamped by name, except `BattleLoadPhaseBehavior`: it is registered after `TaomBehaviorsDone`, in a `finally`, and is not in the count |

FIND:    `BattleLoadPhaseBehavior` is registered **unconditionally** (no `IsEnabled` check at the
         `AddTaomBehavior` call). It is the loading window's only closer while the opener runs in
REPLACE: `BattleLoadPhaseBehavior` is registered **unconditionally** (no `IsEnabled` check), in the `finally` of
         `SubModule.OnMissionBehaviorInitialize` around all of TAOM's mission wiring: it exists whatever that wiring
         threw (the mission-start guard, Patch103, survives such a throw and starts the mission), only when the
         mission has none yet (with the guard off, a throw makes the engine load the same Mission again), it is the
         last TAOM behaviour registered, so it ticks first, and it is not stamped as a `TaomBehaviorAdded` line. It
         is the loading window's only closer while the opener runs in
```

**Verify B2**: Build `0 Error(s)`; BattleLoadCloserWiringTests `Failed: 0`, `Total: 4`;
`FILTER="FullyQualifiedName~TAOM.Tests.Composition|FullyQualifiedName~SharedMovementOrderPostfixTests|FullyQualifiedName~WiringTests"`
`Failed: 0` (`Kernel_SubModule_CallsEachRunnerHookOnce_AfterItsFeatureBlock` stays green);
`grep -c "BattleLoadPhase, the CrashReport" Main/SubModule.cs Main/Composition/FeatureModules.cs docs/features/composition-root.md`
gives 0 for each; Full tests previous + 4.

Commit B2 (`Main/SubModule.cs`, `Main/Composition/FeatureModules.cs`, the test,
`docs/features/battle-load-diagnostics.md`, `docs/features/composition-root.md`):

```
feat(stability): v2.0.28 - BattleLoad closer outlives a wiring throw

A throw anywhere in TAOM's mission wiring skipped the BattleLoad
window closer, so the window never closed and the 300 s stall
watchdog wrote a spurious bundle. The wiring now runs in
AddTaomMissionBehaviors inside a try, and the closer is added in the
finally, once per mission. There is no catch: the throw still
reaches the mission-start guard.

Research: taom-src v1.4.8 Mission.AddMissionBehavior (:4615, calls
only OnCreated) and Mission.GetMissionBehavior (:4631)
Not-tested: a real wiring throw in game
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
```

### Step B3: the AfterStart dev trigger

Source `649cc30ff` (feat(crash-report): add diagnostic triggers for exception handling and mission start
throws), the trigger only, with the hint rewritten for 1.4.x. Simulation (exit 1): conflicts in
`CoopSettingsRelevance.cs`, `CrashReportService.cs`, `CrashReportPatchHelper.cs`,
`bannerlord-together-compat.md`, `coop-interop.md`. Method: hand re-implementation (Disposition says
why the tracing hunks stay out).

B3.1 RED: `SettingsFingerprintTests.cs`, the `CrashReportSettings` `reflected:` +1; then Build and
`FILTER="FullyQualifiedName~SettingsFingerprintTests"` shows 1 failed (`EverySettingsClass_HasItsSplitPinned`).

B3.2 GREEN:

1. `CrashReportSettings.cs`: after `public bool ThrowOnNextApplicationTick { get; set; }` add a blank
   line, a copy of the `[SettingPropertyGroup(...)]` line above `ThrowOnNextApplicationTick` (same group
   string), and:

```csharp
    [SettingPropertyBool("Throw On Next Mission AfterStart", Order = 2, RequireRestart = false,
        HintText = "QA only. Throws a tagged TaomDevTriggerException from a TAOM mission behavior's AfterStart on the next mission start, the #699 shape. Needs Enable Crash Capture on. With Survive Mission Start Failures on, the mission starts with that behavior's AfterStart cut short, and one message shows. With it off, the throw escapes, TAOM.Dependencies swallows it with one line in its diag.log, and the engine loads the mission again. Auto-resets to OFF after firing.")]
    public bool ThrowOnNextMissionAfterStart { get; set; }
```

2. `CoopSettingsRelevance.cs`, `Instrumentation`: add `"ThrowOnNextMissionAfterStart",` directly after `"ThrowOnNextMissionTick",`.
3. `CrashReportDevTrigger.cs`: directly above `public override void OnMissionTick(float dt)` insert:

```csharp
    // #699 follow-up repro: a throw out of a TAOM behavior's AfterStart, which runs inside
    // Mission.AfterStart's behavior loop (v1.4.8 Mission.cs:3823-3826) before the mission is
    // Continuing (:3835). Reset before the throw so one toggle fires once, even if the load reruns.
    public override void AfterStart()
    {
        CrashReportSettings? settings;
        try { settings = CrashReportSettings.Instance; }
        catch { return; /* MCM read errors must not break mission start */ }
        if (settings == null || !settings.EnableCrashCapture || !settings.ThrowOnNextMissionAfterStart) return;

        settings.ThrowOnNextMissionAfterStart = false;
        throw new TaomDevTriggerException("TAOM CrashReport dev trigger: mission AfterStart throw fired by MCM toggle.");
    }

```

4. The two co-op docs: totals, `CrashReportSettings` split and excluded figure +1, as in B1.2 item 5.
5. `docs/features/crash-report.md`: after the `Throw On Next Application Tick` row add a row whose group
   cell is that row's group text wrapped in backticks, followed by
   `| Throw On Next Mission AfterStart | false | Throws `TaomDevTriggerException` from a TAOM behaviour's `AfterStart` on the next mission start (#699's shape). Auto-resets before it throws. With "Survive Mission Start Failures" on, the mission starts and one message shows; with it off, the throw escapes and the engine loads the mission again (TAOM.Dependencies' SaveShield swallows it, with one line in its `diag.log`). Needs Enable Crash Capture on. |`

**Verify B3**: Build; SettingsFingerprintTests `Failed: 0`; Full tests same total as after B2, `Failed: 0`.

Commit B3 (`CrashReportSettings.cs`, `CoopSettingsRelevance.cs`, `CrashReportDevTrigger.cs`,
`SettingsFingerprintTests.cs`, `coop-interop.md`, `bannerlord-together-compat.md`, `crash-report.md`):

```
feat(crash-report): v2.0.28 - QA trigger throws from mission AfterStart

Adds a QA-only MCM toggle, "Throw On Next Mission AfterStart", that
throws from a TAOM behaviour's AfterStart on the next mission start:
the built-in repro for the mission-start guard. It resets itself
before it throws and needs Enable Crash Capture on. Off by default.

Research: taom-src v1.4.8 Mission.AfterStart behaviour loop
(:3823-3826) and Continuing (:3835)
Not-tested: the trigger itself (in-game only)
Save-compat: one new MCM key, default off
Ported-from: 649cc30ff2ddcfbc01228e9c8a1ee1e03241ac4a
```

### Step B4: the `__state` rule

Source `83ad1eab5` (docs(ai): v2.0.34 - pair Harmony prefix and finalizer via __state); simulation:
conflict in `.claude/rules/harmony-patches.md` (1.4.x lacks the bullet it sits under). Method: hand
re-implementation. In `## Patch Types` insert this bullet directly after the `- **Finalizer**:` bullet
if WP106 added it, otherwise after the `- **Transpiler**` bullet:

```markdown
- **Prefix and finalizer pair**: the link between them is Harmony's `__state` (one zeroed local per call, set by the prefix before anything can throw or return early), never a static, a count or a thread-keyed flag. Harmony reruns every finalizer when a later one throws, and PatchShield strips prefixes but never finalizers (`TryUnpatchOffendingPatches` unpatches Prefix, Postfix and Transpiler only), so a finalizer can run without its prefix. Test the pair through real Harmony (`MissionStartGuardCanaryTests` is the shape). Why: `docs/features/mission-start-guard.md` ("The canary") and `lessons/harmony-il.md` "If a prefix writes shared state conditionally, the finalizer must clear it conditionally too".
```

**Verify B4**: `grep -c "Prefix and finalizer pair" .claude/rules/harmony-patches.md` is 1;
`grep -c "If a prefix writes shared state conditionally" docs/reviews/lessons/harmony-il.md` is 1.

Commit B4 (`.claude/rules/harmony-patches.md` only):

```
docs(ai): v2.0.28 - pair Harmony prefix and finalizer via __state

A prefix and finalizer pair must link through Harmony's __state,
never a static flag: Harmony reruns every finalizer when a later one
throws, and PatchShield strips prefixes but never finalizers. The
mission-start guard's canary relies on it. One line in the Harmony
rule file, which loads with every patch file.

Research: Dependencies/Foundation/PatchShield.cs unpatch scope
Not-tested: rule text only
Ported-from: 83ad1eab56dc147c1d6259f69c3480a3e3a368c8
```

**Part B done**: commits B1 to B4 exist; Full tests T0 + 133 (39 + 84 + 6 + 4), `Failed: 0`; Binding
gate `Failed: 0`; Docs and Data clean against Step 0.

## Part C: skeleton buffer guard, watch and native pin gate

### Step C1: SkeletonBuffer guard and watch

Source `ecaa67d26`, SkeletonBuffer half; method as B1.

C1.1 RED: `cd $WT && git checkout ecaa67d26 TAOM.Tests/Features/SkeletonBuffer`; Build must FAIL with
`CS0246`/`CS0234` naming `TAOM.Features.SkeletonBuffer` types.

C1.2 GREEN: `cd $WT && git checkout ecaa67d26 Main/Features/SkeletonBuffer Main/Adapters/ISkeletonBufferEngineAdapter.cs Main/Adapters/ISkeletonBufferMemoryAdapter.cs Main/Adapters/SkeletonBufferEngineAdapter.cs Main/Adapters/SkeletonBufferMemoryAdapter.cs`, then:

1. Comment re-cites (code unchanged):

```
ISkeletonBufferMemoryAdapter.cs:20        <see cref="INativeModuleMemoryAdapter"/>  ->  <c>INativeModuleMemoryAdapter</c>   (WP117's type)
SkeletonBufferGuardService.cs:33          (they are 0x33 apart in v1.5.4)  ->  (0x33 apart on v1.4.8, as on v1.5.4)
SkeletonBufferSignature.cs:29             <c>FUN_180069aa0</c> in v1.5.4  ->  <c>FUN_180088440</c> in v1.4.8
SkeletonBufferSignature.cs:57             the load in v1.5.4)  ->  the load in v1.4.8);  (0x20 in v1.5.4)  ->  (0x20 in v1.4.8)
SkeletonBufferCave.cs:95                  at RVA 0x6AF60 ... at 0x6AF63  ->  at RVA 0x89900 (v1.4.8) ... at 0x89903
SkeletonBufferWatchState.cs:15            (v1.5.4)  ->  (v1.5.4; the same block shift on v1.4.8)
Hooks/SkeletonBufferWatchMissionBehavior.cs:24   // v1.5.4 caller: Mission.AfterStart runs  ->  // v1.4.8 caller: Mission.AfterStart (Mission.cs:3823-3826) runs
```

2. `Main/Composition/FeatureModules.cs`: append as the LAST entry: `        new Features.SkeletonBuffer.SkeletonBufferModule(),`
3. `CrashReportSettings.cs`: after `public bool SurviveMissionStartFailures { get; set; } = true;` insert
   (blank line before each):

```csharp
    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Skeleton Buffer Guard", Order = 4, RequireRestart = true,
        HintText = "The engine reserves room for the skeletons drawn in each frame in two per-frame buffers (65,536 and 262,144 entries) and never checks that the next one fits; one skeleton too many (about 2,340 in view for the first buffer) freezes the battle for good. When on, TAOM bounds-checks both buffers by rewriting 13 bytes of the engine at launch at each one, so a skeleton that would not fit is drawn from that buffer's first slots instead (its bones may look wrong for that frame). TAOM installs each check only where the exact engine bytes are found exactly once: a build that only moves the function is still patched, and a changed build gets nothing written. It also writes nothing for a buffer another module already guards, and guards the other one regardless. Takes effect at the next launch. Default ON.")]
    public bool SkeletonBufferGuard { get; set; } = true;

    [SettingPropertyGroup("Master")]
    [SettingPropertyBool("Skeleton Buffer Watch", Order = 5, RequireRestart = false,
        HintText = "Reads how full those two buffers get during every mission, not only battles, and writes the peaks to the log when the mission ends, with how many times each guard had to step in. If nothing guards the first buffer, also shows one warning at 90 percent full. Takes effect at the start of the next mission. Default ON.")]
    public bool SkeletonBufferWatch { get; set; } = true;
```

4. `CoopSettingsRelevance.cs`: `Instrumentation`, after `"SurviveMissionStartFailures",` add the comment
   line `// The skeleton buffer watch (docs/features/skeleton-buffer-guard.md): a log line and one on-screen warning.`
   and `"SkeletonBufferWatch",`; `Presentation`, after `"EnableNativeSkinFixes",` add
   `// The skeleton buffer guard: where the engine writes a frame's skeleton data when a pool is full; never what is simulated.`
   and `"SkeletonBufferGuard",`.
5. `SettingsFingerprintTests.cs`: `CrashReportSettings` `reflected:` +2; the co-op docs +2 as in B1.2 item 5.
6. `SettingRequireRestartPostureTests.cs`: add to `RestartAllowlist`

```csharp
        [$"{nameof(CrashReportSettings)}.{nameof(CrashReportSettings.SkeletonBufferGuard)}"] = "Read once, at the first main menu, where the guard rewrites 13 bytes at each of up to two sites in engine code or does not: the write is never undone, so turning it on or off needs a restart (docs/features/skeleton-buffer-guard.md)",
```

   and append to the class summary paragraph that describes the allowlist:
   `<c>CrashReportSettings.SkeletonBufferGuard</c> is allowlisted too: it is read once, at the first main menu, because the guard writes engine code once.`
   `docs/features/mcm.md`, paragraph starting `` `TAOM.Tests/Features/Mcm/SettingRequireRestartPostureTests.cs` reflects ``:
   raise its allowlist count word by one and add before its last sentence
   `` `CrashReportSettings.SkeletonBufferGuard` is allowlisted too: it is read once, at the first main menu, because the guard writes engine code once ([skeleton-buffer-guard.md](skeleton-buffer-guard.md)). ``
7. `taom_module_strings.xml`, after the B1 rows, above `</strings>`:

```xml
	<!-- SkeletonBufferEngineAdapter -->
	<string id="taom_skeleton_buffer_warning" text="{=taom_skeleton_buffer_warning}This mission uses {PERCENT} percent of the engine's skeleton buffer, and the game freezes for good when it is full. Lower the battle size." />
```

   Then run the Language rows helper with
   `ROWS = [("taom_skeleton_buffer_warning", "This mission uses {PERCENT} percent of the engine's skeleton buffer, and the game freezes for good when it is full. Lower the battle size.")]`
   and `git add Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml`.

8. `TAOM.Tests/Features/SkeletonBuffer/SkeletonBufferInstalledBinaryTests.cs`: replace its one `KnownBuilds`
   row with `[14185944] = ("v1.4.8", 0x88681, 0xD96D18, 0x886B4, 0x88475, 0x898F0),` and replace the
   opening of `LoadInstalled` (the `GameAssemblies.ResolveGameDir(...)` statement and its `if` with the
   Inconclusive message) with:

```csharp
        // 1.4.x: GameAssemblies has no three-argument ResolveGameDir (plan 008, WP122). EnsureLoaded resolves
        // BANNERLORD_OVERRIDE_DIR, then BANNERLORD_GAME_DIR, the same order.
        var dir = GameAssemblies.EnsureLoaded() ? GameAssemblies.GameDir : null;
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            Assert.Inconclusive("No Bannerlord install folder resolved (BANNERLORD_OVERRIDE_DIR, BANNERLORD_GAME_DIR).");
```

**Verify C1.2**: Build `0 Error(s)`; `FILTER="FullyQualifiedName~TAOM.Tests.Features.SkeletonBuffer"`:
`Failed: 0`, `Total: 235`, `Skipped: 0` (`InstalledNativeDll_KnownBuild_TargetsAreTheVerifiedRvas` must
pass, not skip); `FILTER="FullyQualifiedName~SettingsFingerprintTests|FullyQualifiedName~SettingRequireRestartPostureTests|FullyQualifiedName~UnregisteredLocalizationKey|FullyQualifiedName~LanguageFileCoverageTests|FullyQualifiedName~LanguageDataXmlTests|FullyQualifiedName~TAOM.Tests.Composition"`:
`Failed: 0`.

C1.3 Docs and notices. `cd $WT && git checkout ecaa67d26 docs/features/skeleton-buffer-guard.md`, then:

```
FIND:    both on by default (Mike, 2026-10-08)
REPLACE: both on by default (Mike on the 1.5.x line, 2026-10-08; decision D5 of the 1.4.x port programme)

FIND:    [adopt-yotthani-2026-10-08.md](../reviews/adopt-yotthani-2026-10-08.md)    (every occurrence left)
REPLACE: `docs/reviews/adopt-yotthani-2026-10-08.md` on the 1.5.x line (not ported)

FIND:    (pool 1 at RVA `0x69D1C`, pool 2 at `0x6AF58` in v1.5.4; the
REPLACE: (pool 1 at RVA `0x886BC`, pool 2 at `0x898F8` in v1.4.8, eight bytes into each 13-byte site; the

FIND:    28 for a vanilla human by yotthani's measurement
REPLACE: 28 for a vanilla human by yotthani's v1.5.4 measurement

FIND:    the sentence "The layout and the evidence are in [mission-frame-threads-and-native-costs.md](...) section 10."
REPLACE: The layout and the evidence are in "Native layout (v1.4.8)" below.

FIND:    (a spider mount is one more, a mumak brings eight crew, and every horse, warg and creature mount fills the second pool)
REPLACE: (a spider mount is one more, and every horse, warg and creature mount fills the second pool; the 1.4.x mumak carries no howdah crew)

FIND:    from "Section 10 of the engine doc lists them" to the end of that bullet
REPLACE: The 1.5.x line's engine doc (`docs/reference/engine/mission-frame-threads-and-native-costs.md` section 10,
         v1.5.4; WP117 brings it here) lists them; a guard for another pool waits for a measurement.

FIND:    (pool 1 in the 980-byte `FUN_180069aa0`, pool 2 in `FUN_18006af30`, which the first calls at `0x69AFE` and three other functions call for the same pool)
REPLACE: (on v1.4.8 pool 1 is in the 980-byte `FUN_180088440`, pool 2 in `FUN_1800898D0`, which the first calls at `0x8849E` and three other functions call for the same pool, at `0x35CB08`, `0x35CB97` and `0x4B4AF8`)

FIND:    so `FUN_18006af30` returns
REPLACE: so `FUN_1800898D0` returns

FIND:    (pool 1's site at RVA `0x69D14`, pool 2's at `0x6AF50`, the pool-2 load at `0x69AD5`)  ...  the global at RVA `0xD9D160`
REPLACE: (v1.4.8: pool 1's site at RVA `0x886B4`, pool 2's at `0x898F0`, the pool-2 load at `0x88475`)  ...  the global at RVA `0xD96D18`

DELETE:  (the same pass's two nameplate-cull mutations are in [nameplate-cull.md](nameplate-cull.md))

FIND:    in "After an engine bump", from "Re-derive with" to the end of the section
REPLACE: Re-derive by running `SkeletonBufferInstalledBinaryTests` (each pattern must still match once), read the code
         at each reservation with `python tools/native_crash_triage.py --rva 0x886BC` and `--rva 0x898F8` (the v1.4.8
         addresses), check that "Native layout (v1.4.8)" still holds, then add the build's file length and RVAs to
         `KnownBuilds`. `tools/native_decompile.py` is not on the 1.4.x line. Do not use
         `tools/native_sig_author.py xref` to count references: on v1.5.4 its single capstone sweep stopped at the
         first byte it could not decode (2.8 % of `.text`).

FIND:    site=0x69D14 resume=0x69D21      REPLACE: site=0x886B4 resume=0x886C1
FIND:    site=0x6AF50 resume=0x6AF5D      REPLACE: site=0x898F0 resume=0x898FD

ADD after "(2026-10-08 review" and after "issue draft 10 of the adoption record" in "## Performance":  , measured on the 1.5.x line
ADD " (1.5.x line)" to the 2026-10-08 Changelog entry, and the entry:
         - <today>: ported to the 1.4.x line by plan 109 with the v1.4.8 KnownBuilds row; installed from the feature-module runner's MainMenu phase.
ADD to "## GitHub Issue":  - **1.4.x port:** plans/109-mission-start-and-skeleton-guards.md.
```

Insert this section directly above `## The price`:

```markdown
## Native layout (v1.4.8)

Verified 2026-11-04 with a read-only scan of the installed v1.4.8 `TaleWorlds.Native.dll` (14,185,944 bytes, SHA-256 `2a5e0e0b15513ebb7052d747a621b82c50ee834553eba283b33bbbc33766e12a`). The 1.5.x line describes the same design on v1.5.4 in its engine doc section 10.

- **Where.** Each `SkeletonBufferSignature` pattern matches exactly once in `.text` and neither foreign shape matches: watch load at RVA `0x88681`, pool-2 load at `0x88475`, pool-1 site at `0x886B4` (resume `0x886C1`), pool-2 site at `0x898F0` (resume `0x898FD`). Both loads read the pointer at `0xD96D18`, an 8-aligned slot in `.data`.
- **The functions.** Pool 1 reserves in `FUN_180088440` (unwind ranges `0x88440` to `0x88814`, 980 bytes); its site is `0x33` after the watch load, as on every 1.5.x build. Pool 2 reserves in `FUN_1800898D0`, called by the host at `0x8849E` (the load plus `0x29`, entering `0x20` before the site) and by three other functions at `0x35CB08`, `0x35CB97` and `0x4B4AF8`.
- **Layout** (from the pattern bytes): pool 1's two buffers start at `[0xD96D18] + 0x9D0`, pool 2's at `+ 0xC28`, each `0x128` bytes (a fill counter, 32 block pointers at `+0x8`, 32 ready bytes at `+0x108`), the current one picked by the int `0x250` bytes after the pool's base. Block shift 11 for pool 1 (2,048 entries per block) and 13 for pool 2 (8,192), so 65,536 and 262,144 entries.
- **Who reserves.** The draw record's skeleton count is the byte at `[rdi+0x44]` (`movsx` at `0x884C1`); a record whose `[rdi+0x10]` is null (`cmp` at `0x884B6`) skips pool 1 and reserves only from pool 2.
- **The freeze.** Threads waiting on a block's ready byte spin in a seven-byte `movzx; test; jnz` loop at `0x88730` and `0x88744` (pool 1) and at `0x89977` and `0x89990` (pool 2). A frozen-battle dump with a thread in one of them is a full pool: run `/native-crash-triage`.
```

Then `grep -n "0x69\|0x6A\|0x6B\|0xD9D\|FUN_18006\|adopt-yotthani-2026-10-08.md\](\|v1\.5" docs/features/skeleton-buffer-guard.md`:
every hit left must attribute something to the 1.5.x line. Then:

1. `docs/features/crash-report.md`: after the `Survive Mission Start Failures` row add
   `| Master | Skeleton Buffer Guard | true | Bounds-checks both of the engine's per-frame skeleton pools, so a crowded battle no longer freezes when either fills ([skeleton-buffer-guard.md](skeleton-buffer-guard.md)). Restart: installed once at the first main menu. |`
   and `| Master | Skeleton Buffer Watch | true | Logs each mission's peak fill of both skeleton pools ([skeleton-buffer-guard.md](skeleton-buffer-guard.md)). Live: read at each mission start. |`
2. `THIRD-PARTY-LICENSES.txt`: the two lines `the mission-start guard (source: Main/Features/MissionStartGuard/ and its adapter`
   / `in Main/Adapters/, in the TAOM repository).` become `the mission-start guard and the skeleton-buffer guard and watch, whose` /
   `first-pool machine code block is his (sources: Main/Features/MissionStartGuard/ and` /
   `SkeletonBuffer/, and their adapters in Main/Adapters/, in the TAOM repository).`
3. `provenance-register.md`: in the VanillaTuning row `behavioural-port` becomes `verbatim-port`, and the
   Covers cell gains ``; `Main/Features/SkeletonBuffer/**`, its adapters `Main/Adapters/{I,}SkeletonBufferMemoryAdapter.cs` `Main/Adapters/{I,}SkeletonBufferEngineAdapter.cs`, its tests `TAOM.Tests/Features/SkeletonBuffer/**` ``.
   Append to `### Yotthani VanillaTuning`: ``Main/Features/SkeletonBuffer/**` adapts its `frame-buffer-guard` and `frame-buffer-watch`. The 43-byte machine code block in `SkeletonBufferCave.cs` is his, byte for byte except the counter's displacement (TAOM keeps the counter on a second page so the code page can be read-execute), which makes the row `verbatim-port`; the signature scan, the install order, the adapter and the watch are TAOM's. The second pool's 42-byte block, its site pattern and the link check are TAOM's own, written on his block's shape. The engine facts were re-derived on v1.4.8 for this port ([skeleton-buffer-guard.md](../features/skeleton-buffer-guard.md), "Native layout (v1.4.8)").``

**Verify C1.3**: Docs command clean except `missing_features` +1; Data command no new ERROR; Full tests
T(B) + 235, `Failed: 0`.

Commit C1 (every path of C1.1 to C1.3):

```
feat(stability): v2.0.28 - skeleton buffer guard and watch

The engine reserves per-frame room for the skeletons it draws in two
pools and never checks the limit; one reservation too many freezes
the battle for good, with no log. The guard rewrites 13 bytes at each
reservation at the first main menu, only after an exact byte match,
so an overflow reuses the pool's first slots (wrong bones for a
frame). The watch logs each mission's peak fill. MCM: TAOM Crash
Report, "Skeleton Buffer Guard" (restart) and "Skeleton Buffer
Watch", both on. Adapted from yotthani's VanillaTuning (MIT).

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll
(14,185,944 bytes): six patterns 1/1/0/1/1/0, global 0xD96D18,
pool-2 call link 0x8849E to 0x898D0
Not-tested: the engine-code write in the game process; the look of a
refused skeleton; MCM readable at the main-menu phase
Save-compat: no saved state; two new MCM keys, default on
Ported-from: ecaa67d26463f7652c8235ebff54a7fe78e030a7
```

### Step C2: native pin gate

Sources `15e52d7af` (test(skeleton-buffer): v2.0.34 - pin v1.5.5 targets, gate native pins) and
`c286e2393` (test(native-pins): v2.0.34 - pin gate reads compiled attributes), plus `fdee18f02`'s
summary sentence in the tip file. Simulation: `15e52d7af` conflicts in the engine-bump skill, both
InstalledBinaryTests, `skeleton-buffer-guard.md`, `docs/migration/v1.5.5-impact.md`; `c286e2393` in
`NativePinGateTests.cs`, `skeleton-buffer-guard.md`. Method: transplant two tip test files, hand-edit the rest.

C2.1 RED: `cd $WT && git checkout e45f6df2c TAOM.Tests/Migration/NativePinGateTests.cs TAOM.Tests/Features/MissionPerf/AnimMemory/ClipBudgetSignatureInstalledBinaryTests.cs`.
In the ClipBudget file replace all `KnownBuilds` rows with
`[14185944] = ("v1.4.8", 0x23AADF, 0x23AB04, 0xDA59B0, 0xB2BC9C),` and the opening of its `LoadInstalled`
with the five lines of C1.2 item 8. Build; `FILTER="FullyQualifiedName~NativePinGateTests"` shows
`Failed: 2` (`KnownBuildsTables_EachClass_IsTaggedBindingVerificationAndLiveInstall`,
`KnownBuildsTables_EachClass_IsNamedInTheEngineBumpSkill`).

C2.2 GREEN:

1. `SkeletonBufferInstalledBinaryTests.cs`: under `[TestCategory("LiveInstall")]` add `[TestCategory("BindingVerification")]`.
2. `.claude/skills/engine-bump/SKILL.md`, at the end of Phase 4's list (after item 6, before `## Phase 5`):

```markdown
7. **The per-build native address pins.** Two C# `KnownBuilds` tables, in
   `ClipBudgetSignatureInstalledBinaryTests` and `SkeletonBufferInstalledBinaryTests`, pin the
   `TaleWorlds.Native.dll` addresses of the clip-budget pattern and of the skeleton buffer guard's
   patterns (the game finds them by pattern; only the tests pin). On a build with no row both read
   Inconclusive: a skip in the default suite and, on 1.4.x, in the `TestCategory=BindingVerification`
   gate too until plan 008's `TAOM.Tests/binding-gate.runsettings` (WP122) maps Inconclusive to
   Failed, so read the skip count. `NativePinGateTests` keeps both classes tagged
   `BindingVerification` and named here. Add a row per build from a read-only scan of the installed
   DLL (its file length, then the RVAs the tests resolve), and check the first start's guard lines
   (Phase 5).
```

   In `## Phase 5`, put this before the first word of its first paragraph and change that word
   `Vanilla-only` to `Then: vanilla-only`: `First read the new build's first start in `taom_debug_*.log`
   for the native guards (`docs/features/skeleton-buffer-guard.md`, "After an engine bump"): both
   `[SkeletonBuffer] guard ON pool 1` and `guard ON pool 2` must appear; a `guard OFF pool N` (or one
   `guard OFF:`) with a pattern reason means players lost that pool's freeze protection until the
   patterns are re-derived.`
3. `docs/features/skeleton-buffer-guard.md`: in "After an engine bump", after its first sentence add
   `That is a skip in the default suite and, on 1.4.x, in the binding gate too until WP122's runsettings
   map Inconclusive to Failed; `NativePinGateTests` keeps the class tagged `BindingVerification` for that
   day.` Changelog: `- 2026-10-15 (1.5.x line): both native pin classes sit in the engine-bump binding
   gate, checked by `NativePinGateTests` (#806); on 1.4.x since plan 109.` GitHub Issue:
   `- **Issue:** #806: the native pin gate for both KnownBuilds classes.`

**Verify C2**: Build; `FILTER="FullyQualifiedName~NativePinGateTests|FullyQualifiedName~InstalledBinaryTests"`:
`Failed: 0`, `Total: 8`, `Skipped: 0`; Binding gate `Failed: 0` (record Skipped); Full tests previous + 6.

Commit C2 (`NativePinGateTests.cs`, `ClipBudgetSignatureInstalledBinaryTests.cs`,
`SkeletonBufferInstalledBinaryTests.cs`, `.claude/skills/engine-bump/SKILL.md`, `docs/features/skeleton-buffer-guard.md`):

```
test(skeleton-buffer): v2.0.28 - native pin gate with v1.4.8 rows

Both per-build native address tables (the skeleton buffer guard and
the clip-budget scan) now carry the v1.4.8 row and the
BindingVerification tag, and NativePinGateTests keeps every such
table tagged and named in the engine-bump skill. On this line an
unlisted build still reads Skipped in the binding gate until plan
008's runsettings land (WP122); the skill says to read the skip
count. Players see no change.

Research: read-only scan of the installed v1.4.8 TaleWorlds.Native.dll:
ClipBudget load 0x23AADF, budget 0xB2BC9C reads 12582912.0
Not-tested: nothing runtime
Ported-from: 15e52d7af67fbb72b6fc9bee8047f107c0d52b78
Ported-from: c286e2393100e84e2b1d802cc124a46bd312bc74
Ported-from: fdee18f02132a259c6ea52c6c43be5e82f5ca93f
```

**Part C done**: commits C1 and C2 exist; Full tests T0 + 374, `Failed: 0`.

### Step D: review gate and hand-off

1. Run every Done criterion.
2. Run `/deep-review` on `git diff $BASE..HEAD` and ask it to probe the "Maintenance notes" review
   spots. Act only on findings that change runtime behaviour or break this plan; land each fix as one
   extra commit `fix(stability): v2.0.28 - review follow-ups` (tests first, same procedure). Never
   rewrite earlier commits. If your session cannot invoke skills, say so. Never run `/review-codex` or
   `/codex-verify` (paid; maintainer only).
3. Final message: branch; each commit's short SHA and subject; T0 and final (Total, Passed, Skipped);
   binding-gate Skipped; anything unusual in Steps 0.3 to 0.6; the `/deep-review` outcome; the in-game list.
   This plan closes no older plan's row, so the integrator only records plan 109's own status and counts.

## Disposition

Simulation re-run this session (`git merge-tree --write-tree --name-only --no-messages --merge-base=<sha>^ origin/bannerlord-1.4.5 <sha>`): every commit exits 1.

| Commit | Subject | Disposition |
|---|---|---|
| `649cc30ff` | feat(crash-report): add diagnostic triggers for exception handling and mission start throws | **Adapted** (B3): trigger, setting, relevance entry, counts. **Skipped**: the temporary #699 tracing (`Diag699Reentry`, `Diag699Checkpoint`, `Diag699Entry`): on 1.4.x SaveShield catches the AfterStart throw before Patch37, so it would not see the loop (V04); the guard closes the loop; it needs `fb52e619d` (WP107) |
| `ecaa67d26` | feat(stability): v2.0.34 - mission start, freeze and map guards | **Transplanted in part**: MissionStartGuard (B1), the closer `finally` (B2), SkeletonBuffer (C1), settings, strings, notices, registry, snapshot rows, feature docs, shared test edits; adds English rows in the 12 language files, which the commit lacks (1.4.x `LanguageFileCoverageTests` requires them). **To WP120**: NameplateCull, MapViewRelease, `BattleLoadDiagnosticsSettings.cs`, two hot-path provider tests, the CoopVeto entry, three ReflectionSite rows, `nameplate-cull.md`, `map-view-release.md`, `map-perf-profiler.md`, Patch104, the license and provenance extension. **Deferred**: engine docs (WP117 creates `mission-frame-threads-and-native-costs.md`; WP144 the lifecycle doc), `native-crash-triage/SKILL.md` and `.ai/review-reference.md` (WP143; the v1.4.8 spin RVAs are in `skeleton-buffer-guard.md`), the `harmony-patches.md` AddTaomBehavior paragraph (absent here; WP111/WP143 take ecaa67d26's wording). **Skipped** (bookkeeping, 1.5.x records): INDEX.md, feature-map.md, tools/README.md, REVIEW-LOG.md, lessons/*, adoption record, two Codex prompts, the RCA, codex-track-record.md, agents-md-review-lessons-archive.md, external-repo-adoption.md, orientation.md (absent here), `plans/_audit/2026-10-02-perf/REPORT.md`. **Adapted**: the engine-bump skill lines (C2) |
| `83ad1eab5` | docs(ai): v2.0.34 - pair Harmony prefix and finalizer via __state | **Adapted** (B4): cites the 1.4.x lesson, not `daba6f0a0`'s (WP117) |
| `15e52d7af` | test(skeleton-buffer): v2.0.34 - pin v1.5.5 targets, gate native pins | **Adapted** (C2): class tags, gate test, a 1.4.x skill item without the Python `PINS` lines. **Skipped**: v1.5.5 row, `docs/migration/v1.5.5-impact.md` |
| `c286e2393` | test(native-pins): v2.0.34 - pin gate reads compiled attributes | **Ported** (C2): tip shape; doc lines adapted |
| `6eb2e1b90`, `daba6f0a0` (WP117) | TickProfilerTranspiler end state and two tests | **Taken verbatim** (A1); WP117 keeps them |
| `6eb2e1b90` (WP117) | TickProfileLines.cs | **Trimmed copy** (A1); WP117 replaces it |
| `a9f10688f` (WP117) | ClipBudgetSignature, PeSectionTable, SyntheticNativeImage, two unit tests, the installed-binary test | **Taken** (A1, C2) with the v1.4.8 row; the AnimMemory probe stays WP117's |
| `346f3e093` (WP118) | RecordingLogger.cs | **Taken verbatim** at its 1.5.x path (A1) |
| `fdee18f02` (WP117) | NativePinGateTests summary sentence | **Taken** with the tip file (C2) |
| plan 008 (WP122) | three-argument `GameAssemblies.ResolveGameDir`, binding-gate runsettings | **Not taken**: both installed-binary tests use `EnsureLoaded()`/`GameDir` |

## Test plan

- New results (counted from `[TestMethod]` attributes in the ported files): A1 39; B1 84 plus 6
  ReflectionSite rows; B2 4; C1 235; C2 6. Total 374. They cover the swap's soft-fail rules, the six
  real call sites in the installed IL, survive and rethrow paths, budgets and canary through real
  Harmony, the closer's `try`/`finally`, the guard's per-pool decisions, both code blocks executed
  in-process, the watch, the installed-DLL patterns and the pin gate.
- Untestable (`Not-tested:`): Patch103 in a live mission; the write into the game's own native DLL; MCM
  readability at the main-menu phase; the AfterStart trigger.

## Done criteria

At the end of each Part (later Parts include the earlier criteria):

- [ ] Build exits 0 with `0 Error(s)`.
- [ ] Full tests `Failed: 0`, Skipped as T0, Total T0 + 39 (A), + 133 (B), + 374 (C).
- [ ] Binding gate `Failed: 0` (B, C).
- [ ] `git -C $WT log --format=%s $BASE..HEAD` lists exactly the finished Parts' commits, each with
      `v2.0.28 - ` and at most 72 characters; `git -C $WT diff --stat $BASE..HEAD -- CHANGELOG.md Main/IoC.cs Dependencies plans` is empty.
- [ ] `git -C $WT status --porcelain` is empty.
- [ ] B: `grep -c "MissionStartGuardModule()" Main/Composition/FeatureModules.cs` is 1;
      `grep -c "Patch103_MissionStartGuard" Main/SubModule.cs` is 0;
      `grep -c "private void AddTaomMissionBehaviors(Mission mission)" Main/SubModule.cs` is 1;
      `grep -l "id=\"taom_mission_start_guard_notice\"" Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml | wc -l` is 12.
- [ ] C: `grep -c "SkeletonBufferModule()" Main/Composition/FeatureModules.cs` is 1; `grep -c 14185944`
      is 1 in each installed-binary test; `grep -c SkeletonBufferInstalledBinaryTests .claude/skills/engine-bump/SKILL.md` is at least 1;
      `grep -l "id=\"taom_skeleton_buffer_warning\"" Main/_Module/ModuleData/Languages/*/std_taom_module_strings_*.xml | wc -l` is 12.
- [ ] Data: no ERROR beyond `$AUX/109-data-before.txt`. Docs: no counter above `$AUX/109-lint-before.txt`
      except `missing_features` +1; `ai_dashes` does not rise.

## STOP conditions

- Any Step 0.3 result differs: a WP105 runner call or `FeatureModules.All` missing, `RepoPaths.ReadSource`
  with `stripComments` missing (WP104), a `PatchShieldPolicy` exclusion method missing (WP107), the
  SaveShield MISSION-INIT row gone, PatchShield unpatching finalizers, a target folder or `"Patch103_"`
  already present, or the native DLL not 14,185,944 bytes with the quoted SHA-256 (run `/engine-bump`;
  never hand-edit RVAs). Or `plans/README.md` does not show WP106 and WP107 merged.
- `MissionStartGuardBindingTests` reports a site count other than 1 or a swap count other than 6.
- `SkeletonBufferInstalledBinaryTests` fails or skips; any pattern count differs from 1/1/0/1/1/0.
- A ported test fails other than as the RED states predict, including a runtime `FileNotFoundException`
  for `MCMv5` or a compile error in a copied file.
- `OnMissionBehaviorInitialize` lacks the shape B2.2 item 1 describes, or `FeatureModulesTests` fails
  after B1, B2 or C1 (double wiring or a kernel anchor).
- A file this plan creates already exists (identical to its named source: skip and note; else STOP).
- A commit gate blocks (subject, ModuleData validator under D20, files-tracked); never bypass it.
- A review fix needs an out-of-scope file (`Main/IoC.cs`, a csproj, `Dependencies/`).
- A step's verification fails twice after a reasonable fix attempt.

## In-game checks owed (maintainer; `taom_debug_*.log` unless named)

1. **First start, main menu**: `[SkeletonBuffer] guard ON pool 1: site=0x886B4 resume=0x886C1 cave=0x...
   counter=0x... scanMs=...` and `[SkeletonBuffer] guard ON pool 2: site=0x898F0 resume=0x898FD ...`.
2. **Guard toggle**: untick "Skeleton Buffer Guard", restart: expect `[SkeletonBuffer] guard OFF: setting
   off (MCM Skeleton Buffer Guard)`. ON lines instead mean MCM was not readable at the main-menu phase
   (the provider falls back to on): report it.
3. **Game init**: `[MissionStartGuard] ON: 6 call sites wrapped in Mission.AfterStart (toggle on)`.
4. **Trigger, guard on**: tick "Throw On Next Mission AfterStart" (Enable Crash Capture on) and start a
   battle: it starts; one on-screen message; ERROR `[MissionStartGuard] AfterStart threw in
   TAOM.Features.CrashReport.DevTriggers.CrashReportDevTriggerMissionBehavior (assembly TAOM)`; WARNING
   `[MissionStartGuard] mission start survived 1 failed call(s); 1 shown on screen`; BattleLoad reaches
   `BattlePlayable`; no 300 s watchdog bundle.
5. **Trigger, guard off**: untick "Survive Mission Start Failures", repeat: one `Mission.AfterStart threw
   and was not survived` ERROR; `Modules/TAOM.Dependencies/diag.log` gains a `SaveShield` `swallowed
   TaomDevTriggerException ... FinishMissionLoading [MISSION-INIT]` line; record whether the second load
   starts the battle. Tick the setting back on.
6. **Large battle and siege** (corpses unlimited): one `[SkeletonBuffer] mission peak:` line per mission
   with a `pool 2 peak:` clause; record both percentages (do 1.4.8 battles approach the limit?).
   Overflows above 0 mean wrong bones for a frame, never a freeze.
7. **Custom Battle**: one start (wrapped calls outside the campaign).
8. **Desktop**: run `FILTER="FullyQualifiedName~InstalledBinaryTests"` there; a skip means its native
   DLL is another build and needs its own rows.

## Maintenance notes

- **Shared files**: `FeatureModules.All` (WP111, WP117, WP120 append after these lines);
  `AddTaomMissionBehaviors` is now the home of TAOM's mission wiring, so later mission ports (WP111,
  WP117) land inside it; `CrashReportSettings.cs`, `CoopSettingsRelevance.cs` and the fingerprint
  counts (the integrator recounts per merge); `THIRD-PARTY-LICENSES.txt` and the provenance register
  (WP115 adds RUNTIME LIBRARIES and HoN sections; WP120 extends the VanillaTuning section and row; once
  WP115's MIT text exists, the VanillaTuning block may point at it).
- **Pinned helpers**: WP117 reuses `TickProfilerTranspiler.cs`, `ClipBudgetSignature.cs`,
  `PeSectionTable.cs` and `NativePinGateTests.cs`, never re-adds them; its pick of `6eb2e1b90` meets two
  add/add files (keep the tip `TickProfilerTranspiler.cs`, take the full `TickProfileLines.cs`). WP118's
  pick of `346f3e093` meets an identical `RecordingLogger.cs`.
- **Engine bumps**: both `KnownBuilds` tables need a row per native DLL build (`/engine-bump` item 7).
  Until WP122 an unlisted build reads Skipped, not Failed, in the binding gate.
- **Expected finding**: `lint_docs.py` and `detect-docs-gaps.sh` report `Main/Features/MissionPerf`
  without a feature doc until WP117 brings the mission-perf docs. Report-only; add no stub.
- **Review spots for `/deep-review`**: the `try`/`finally` (no catch, one closer, moved body unchanged);
  module order and the MainMenu install with its MCM fallback; the two `LoadInstalled` adaptations; the
  SaveShield claims in the docs and the trigger hint; the license block (full MIT text, scoped ownership
  sentence); no `Patch103` string or watch behaviour hand-wired in `SubModule.cs`.
- **Deferred on purpose**: per-registration isolation in `AddTaomBehavior` (open on both lines); guards
  for the engine's other unbounded per-frame pools (unmeasured); translations of the two new strings
  (WP135, `/localize`, paid; the 12 language files carry English rows until then); the native-crash-triage skill paragraph (WP143).
- **Release**: TAOM.dll only; no `Dependencies/` change, so no #371 pairing duty from this plan.
