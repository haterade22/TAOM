# Plan 048: Delete eight members nothing calls and correct the comments that still describe them

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> Start every shell command with `cd "<your worktree>" && ` (the Bash tool's working directory can
> reset to the main checkout, which holds other sessions' uncommitted work). Prefix every `dotnet`
> command with the `TEMP="..." TMP="..."` your dispatch rules give, and pass the Bash tool a timeout
> of at least 600000 ms for every `dotnet test`.
>
> **Drift check (run first)**:
> `git diff --stat 7087fa633..HEAD -- Dependencies/Foundation/ReflectionUtils.cs Main/Features/CareerSystem/Mutations/MutationParams.cs Main/Features/CultureDoctrine/Domain/FormationRouting.cs Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs Main/Features/DevConsole/PatchReportFormatter.cs TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs docs/features/gui-sprite-system.md Main/SubModule.cs Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml`
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition. (`Main/SubModule.cs`, the shader patch and the
> career prefab are listed because claims in this plan depend on them; you do not edit them.)

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: build exit 0, `11 Warning(s)`, `0 Error(s)`; dotnet
  `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259` (game-backed desktop run),
  failing: none. Python suite `Ran 8578 tests`, `FAILED (failures=7, errors=2, skipped=13)`; failing
  names (heraldry and kingdom-generator data checks that fail on the trunk; this plan touches no
  `tools/` file, so you do not run that suite):
  `test_make_faction_emblems.ShippedDataTests.test_check_is_clean_after_apply`,
  `test_make_faction_emblems.ShippedDataTests.test_every_listed_faction_resolves_to_a_256_emblem`,
  `test_clan_heraldry_specs.ShippedSpecsMirrorTheLiveFiles.test_applying_every_spec_is_a_no_op`,
  `test_generator_regions_in_sync.GeneratorRegionsInSyncTests.test_every_owned_region_matches_a_fresh_run`
  (three subtests), and three `test_lindon_clan_expansion.LindonClanExpansionDataTests` tests. Doc
  lint `python tools/lint_docs.py --fail-on-drift` exit 0.
- **Issue**: filed by the orchestrator before execution

## Why this matters

Eight members in TAOM have no caller anywhere: not in code, tests, prefabs, the two live data modules,
or any installed module's DLL. Each one makes a reader search for callers that do not exist, and two of
them carry doc comments naming callers that never called them (`ReflectionUtils` says `VersionProbe`
and `SaveShield` use its helpers). Two more comments say the `Patch0_BattleScenes` category is
disabled, while `Main/SubModule.cs` applies it at every module load; a reader who trusts them would
believe a live Harmony prefix is inert. This plan deletes the members, the state that only they
used, and corrects every comment and doc line that describes them. No behaviour changes: the full
suite and the build are the proof.

## Current state

### Files and their roles

- `Dependencies/Foundation/ReflectionUtils.cs`: three static reflection helpers in the separately
  shipped `TAOM.Dependencies.dll`; two are dead (lines 51 and 70) and the class summary (lines 7 to 10)
  names wrong users.
- `Main/Features/CareerSystem/Mutations/MutationParams.cs`: typed reads of a career mutation's
  parameters; `GetBool` (29) and `GetInt` (35) are dead, `GetFloat` and `GetString` stay.
- `Main/Features/CultureDoctrine/Domain/FormationRouting.cs`: troop to formation routing;
  `IsRoutable` (54) is dead.
- `Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs`: crash report stack frames;
  `FromCurrent` (21) is dead, `FromException` and the private `Build` stay.
- `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs`: the career screen's choice group view
  model; `ExecuteToggleExpand` (89) is dead, and with it `IsExpanded` (48) and `_isExpanded` (10)
  become write-only.
- `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`: TAOM's player controller
  (a `MissionView`, line 17); `OnWeaponUsageToggleRequested` (649) is dead, and so is the flag only it
  sets (field 680, the clause on 556, the reset on 559).
- `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt`: the shrink-only line baseline; its row
  for the controller must be lowered (see "The gate trap").
- `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs` (lines 18 to 20),
  `Main/Features/DevConsole/PatchReportFormatter.cs` (line 15),
  `TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs` (line 15): comments that call
  `Patch0_BattleScenes` disabled.
- `docs/features/gui-sprite-system.md` (lines 241, 304, 371): name the deleted career members.

### The zero-caller search, run by the planner at `7087fa633`

Command (the worktree root, all tracked files, whole words, binaries skipped):

```bash
for n in TryInvokeStatic GetAssemblySimpleName GetBool GetInt IsRoutable FromCurrent ExecuteToggleExpand OnWeaponUsageToggleRequested _weaponUsageToggleRequested IsExpanded _isExpanded; do echo "=== $n"; git grep -n -w -I "$n"; done
```

Result, line text cut to the file:line and its start:

```
=== TryInvokeStatic
Dependencies/Foundation/ReflectionUtils.cs:51:    public static object? TryInvokeStatic(Type type, string methodName, params object[] args)
docs/changelog-archive/CHANGELOG-2026-H1.md:4622:- `ReflectionUtils.cs` — small helpers: ...
=== GetAssemblySimpleName
Dependencies/Foundation/ReflectionUtils.cs:70:    public static string GetAssemblySimpleName(Type? type)
docs/changelog-archive/CHANGELOG-2026-H1.md:4622:- `ReflectionUtils.cs` — small helpers: ...
=== GetBool
Main/Features/CareerSystem/Mutations/MutationParams.cs:29:    public bool GetBool(string key, bool defaultValue = false)
plans/002-nan-infinity-config-guards.md:284: ...
plans/002-nan-infinity-config-guards.md:329: ...
=== GetInt
CHANGELOG.md:26839:- career-system.md: the calculator step says float parameters (GetInt
Main/Features/CareerSystem/Mutations/MutationParams.cs:35:    public int GetInt(string key, int defaultValue = 0)
docs/reviews/deep-review-002-nan-infinity-config-guards-2026-09-24.md:183: ...
plans/002-nan-infinity-config-guards.md:284: ...
plans/002-nan-infinity-config-guards.md:329: ...
=== IsRoutable
Main/Features/CultureDoctrine/Domain/FormationRouting.cs:54:    public static bool IsRoutable(FormationClass formationClass) =>
=== FromCurrent
Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs:21:    public static IReadOnlyList<StackFrameSnapshot> FromCurrent(int skipFrames = 1)
=== ExecuteToggleExpand
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:89:    public void ExecuteToggleExpand()
docs/features/gui-sprite-system.md:241: ...
docs/reviews/rca-career-e4e7-2026-10-14.md:78: ...
=== OnWeaponUsageToggleRequested
Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:649:    public void OnWeaponUsageToggleRequested() { _weaponUsageToggleRequested = true; }
=== _weaponUsageToggleRequested
Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:556:        if (Input.IsGameKeyPressed(17) || _weaponUsageToggleRequested)
Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:559:            _weaponUsageToggleRequested = false;
Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:649:    public void OnWeaponUsageToggleRequested() { _weaponUsageToggleRequested = true; }
Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:680:    private bool _weaponUsageToggleRequested;
=== IsExpanded
Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs:301: ...
Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs:303: ...
Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs:42: ...
Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs:48: ...
Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs:49: ...
Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs:50: ...
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:48:    public bool IsExpanded
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:56:                OnPropertyChangedWithValue(value, nameof(IsExpanded));
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:92:            IsExpanded = !IsExpanded;
Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaItemList.xml:31: ...
Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaItemList.xml:35: ...
docs/features/gui-sprite-system.md:241: ...
docs/features/gui-sprite-system.md:304: ...
docs/reviews/rca-career-e4e7-2026-10-14.md:78: ...
docs/reviews/rca-career-ui-revamp-2026-05-30.md:11: ...
=== _isExpanded
Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs:300: ...
Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs:301: ...
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:10:    private bool _isExpanded;
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:50:        get => _isExpanded;
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:53:            if (_isExpanded != value)
Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs:55:                _isExpanded = value;
```

Reading it: every hit outside the declaring file is history (`CHANGELOG.md`,
`docs/changelog-archive/`, `docs/reviews/`, `plans/`), one of the three doc lines this plan corrects,
or a different type. The other `IsExpanded` and `_isExpanded` hits belong to the encyclopedia list
(`EncyclopediaListScreenVM`, bound by `TAOMEncyclopediaItemList.xml:31,35`) and to an encyclopedia
widget; neither touches the career group. A whole-word search over all tracked files also covers
`nameof(...)`, `AccessTools.Method(..., "X")`, `GetMethod("X")`, Harmony `TargetMethod` strings and
every GUI prefab under `Main/_Module/GUI` (they are tracked). Without `-I`, `IsExpanded` and
`_isExpanded` also match the vendored MCM binaries `Dependencies/_Module/bin/Win64_Shipping_Client/Bannerlord.MBOptionScreen.v1.4.*.dll`
and `v1.5.1.dll`: those are MCM's own members and do not reference TAOM types.

**Outside the repo (the planner's searches on the desktop install, 2026-11-12):**

- Installed DLLs: `grep -r -l -a -E "TryInvokeStatic|GetAssemblySimpleName" --include=*.dll` over the
  game's `bin` and `Modules` folders hits only the declaring `TAOM.Dependencies.dll` copies (Client,
  Server, wEditor), AWOIAF's `AWOIAF.Dependencies.dll` (its own copy of the helpers: the file holds the
  string `AWOIAF.Dependencies.Foundation` and not the string `TAOM.Dependencies`, so it does not
  reference TAOM's assembly) and Roslyn's `Microsoft.CodeAnalysis.Scripting.dll` (an unrelated Roslyn
  internal). No consumer assembly references the two TAOM statics. This is why removing two public
  members from the separately shipped `TAOM.Dependencies.dll` is safe: it is a binary break only for a
  consumer compiled against them, and none exists in this repo or in any module installed on the
  desktop. A third-party mod not installed there is outside what was searched.
- The same byte search for `OnWeaponUsageToggleRequested` hits only the engine's
  `TaleWorlds.MountAndBlade.View.dll` and `TaleWorlds.MountAndBlade.GauntletUI.dll`, and TAOM's own
  `TAOM.Client.dll`.
- Prefabs: no `*.xml` in any installed module binds `ExecuteToggleExpand`; the installed
  `Modules/TAOM/GUI/Prefabs/CareerSystem/CareerScreen.xml` contains neither `ExecuteToggleExpand` nor
  `IsExpanded` (`grep -c` printed 0), and the live `TAOM_Map` and `LOTRLOME_Armory` modules have no
  `*.xml` or `*.xslt` naming `ChoiceGroupsTier`, `ExecuteToggleExpand` or `CareerScreen`. The repo's
  `Main/_Module/GUI/PreFabs/CareerSystem/CareerScreen.xml` is the only prefab that lists the career
  groups (`DataSource="{ChoiceGroupsTier1}"` and the 2 and 3 forms at lines 251, 186, 120), and it
  binds no `IsExpanded` (the repo grep above).

### Engine facts (why vanilla cannot call the controller's method)

`pwsh tools/taom-src.ps1 path MissionMainAgentController` gives
`TaleWorlds.MountAndBlade.View.MissionViews.MissionMainAgentController.cs` (v1.5.7). The TAOM
controller copied vanilla's pattern:

```csharp
public class MissionMainAgentController : MissionView            // decompile :13
	private bool _weaponUsageToggleRequested;                       // :74
			if (base.Input.IsGameKeyPressed(17) || _weaponUsageToggleRequested)   // :714
	public void OnWeaponUsageToggleRequested()                      // :841
```

The only engine caller, from `pwsh tools/taom-src.ps1 path MissionGauntletMainAgentEquipDropView`
(`TaleWorlds.MountAndBlade.GauntletUI.Mission.MissionGauntletMainAgentEquipDropView.cs`):

```csharp
public class MissionGauntletMainAgentEquipDropView : MissionView                              // :16
	private MissionMainAgentController _missionMainAgentController;                            // :28
		_missionMainAgentController = base.Mission.GetMissionBehavior<MissionMainAgentController>();  // :84
	private void HandleQuickRelease()                                                          // :250
		_missionMainAgentController.OnWeaponUsageToggleRequested();                            // :252
```

The field is typed `MissionMainAgentController` and filled by `GetMissionBehavior<MissionMainAgentController>()`.
TAOM's class is `public class AutonomousMovementPlayerController : MissionView, IAutonomousMovementController`
(`AutonomousMovementPlayerController.cs:17`), not a subclass of `MissionMainAgentController`, and
`IAutonomousMovementController` does not declare `OnWeaponUsageToggleRequested` (the grep found the
one declaration only). So no engine path reaches TAOM's copy. You do not need to re-decompile; if you
do and either quote differs, STOP.

### Excerpts at `7087fa633` (CRLF files; edit with the Edit tool, never `sed -i`)

Line numbers in this plan are those at `7087fa633`. They locate the text; the Edit tool matches the
quoted text, so an earlier deletion in the same file does not move what you match.

`Dependencies/Foundation/ReflectionUtils.cs:5-16`, the summary to correct:

```csharp
namespace TAOM.Dependencies.Foundation;

/// <summary>
/// Small reflection helpers used by the defensive shields (PatchShield, SaveShield,
/// VersionProbe). All methods are exception-safe — they
/// return null/empty/false on any failure rather than throwing.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Re-implements
/// BetaDeps.Foundation.ReflectionUtils.
/// </summary>
public static class ReflectionUtils
{
```

Lines 47 to 80 are the two dead methods (from the `/// <summary>` above `TryInvokeStatic` at 47
through the closing `}` of `GetAssemblySimpleName` at 80); line 81 is the class's closing `}`:

```csharp
    /// <summary>
    /// Tries to invoke a static method by name on a type, returning the result or null.
    /// Catches all exceptions. Used by VersionProbe for safe API-discovery calls.
    /// </summary>
    public static object? TryInvokeStatic(Type type, string methodName, params object[] args)
    {
        ...
    }

    /// <summary>
    /// Gets the simple assembly name (no version/culture/pubkey) that defines the
    /// given type. Returns empty string on failure. Used by SaveShield for culprit
    /// attribution.
    /// </summary>
    public static string GetAssemblySimpleName(Type? type)
    {
        ...
    }
}
```

The real callers of the surviving `FindTypeAcrossLoadedAssemblies` (`git grep -n "ReflectionUtils\."`):
`Dependencies/Foundation/IncompatibleModDetector.cs:222`, `Dependencies/Foundation/SaveShield.cs:195`,
`Dependencies/Foundation/VersionProbe.cs:68`, and in `TAOM.dll` the CoopInterop feature:
`Main/Features/CoopInterop/CoopPlayerControlQuery.cs:34`, `CoopSessionProvider.cs:65`,
`Hooks/CoopRecruitAttributionPatch.cs:55`, `Hooks/CoopUpgradeChargePatch.cs:61`,
`Missions/CoopMissionAuthority.cs:48`. `PatchShield` does not call it.

`Main/Features/CareerSystem/Mutations/MutationParams.cs:24-41`:

```csharp
    public string GetString(string key, string defaultValue = "")
    {
        return _params.TryGetValue(key, out var val) ? val : defaultValue;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        if (!_params.TryGetValue(key, out var val)) return defaultValue;
        return bool.TryParse(val, out var result) ? result : defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        if (!_params.TryGetValue(key, out var val)) return defaultValue;
        return int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : defaultValue;
    }
}
```

`using System.Globalization;` stays: `GetFloat` (line 19) uses `NumberStyles` and `CultureInfo`.

`Main/Features/CultureDoctrine/Domain/FormationRouting.cs:51-57`:

```csharp
    }

    /// <summary>The routable classes: the engine's regular formations, indices 0 to 7.</summary>
    public static bool IsRoutable(FormationClass formationClass) =>
        formationClass >= FormationClass.Infantry && formationClass < FormationClass.NumberOfRegularFormations;

    private static readonly Dictionary<string, FormationClass> RoutableByName = new Dictionary<string, FormationClass>(StringComparer.OrdinalIgnoreCase)
```

`Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs:19-31`:

```csharp
    }

    public static IReadOnlyList<StackFrameSnapshot> FromCurrent(int skipFrames = 1)
    {
        try
        {
            var st = new StackTrace(skipFrames, fNeedFileInfo: true);
            return Build(st);
        }
        catch { return Array.Empty<StackFrameSnapshot>(); }
    }

    private static IReadOnlyList<StackFrameSnapshot> Build(StackTrace st)
```

`Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs`: line 10 `    private bool _isExpanded;`;
lines 44 to 61:

```csharp
    [DataSourceProperty]
    public int Tier => _group.Tier;

    [DataSourceProperty]
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                OnPropertyChangedWithValue(value, nameof(IsExpanded));
            }
        }
    }

    [DataSourceProperty]
    public bool IsLocked
```

and lines 86 to 94:

```csharp
        }
    }

    public void ExecuteToggleExpand()
    {
        if (!_isLocked)
            IsExpanded = !IsExpanded;
    }
}
```

`_isLocked` stays: the `IsLocked` property (61 to 73) still reads and writes it, so no unused-field
warning appears.

`Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs:554-561`:

```csharp
    private void ProcessWeaponToggleInput(Agent mainAgent)
    {
        if (Input.IsGameKeyPressed(17) || _weaponUsageToggleRequested)
        {
            mainAgent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
            _weaponUsageToggleRequested = false;
        }
    }
```

lines 644 to 651:

```csharp
    private void OnPlayerToggleOrder(MissionPlayerToggledOrderViewEvent obj)
    {
        _isPlayerOrderOpen = obj.IsOrderEnabled;
    }

    public void OnWeaponUsageToggleRequested() { _weaponUsageToggleRequested = true; }

    private void OnManagedOptionChanged(ManagedOptions.ManagedOptionsType optionType)
```

and lines 678 to 681 (681 is the last line of the file):

```csharp
    private float _lastLockedAgentHeightDifference;
    public bool IsChatOpen;
    private bool _weaponUsageToggleRequested;
}
```

### The gate trap: the controller is on the shrink-only baseline

`TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` (CRLF, 23 lines, 18 entries) line 7:

```
TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController 665
```

`EntryPointLineCountGateTests` counts a class "from its declaration line to its closing brace"
(`EntryPointLineCountGateTests.cs:17-18`, `:304`
`Lines = LineOf(masked, close) - LineOf(masked, match.Index) + 1`). Today: declaration line 17,
closing brace line 681, so 681 - 17 + 1 = 665. A listed class that ends below its number fails
(`:77-78`):

```csharp
            else if (lines < allowed)
                failures.Add($"{key}: {lines} lines is below its baseline {allowed}; lower it (set {WriteEnvVar}=1 and run Baseline_Regenerate_WhenAsked)");
```

The regenerator (`:100-125`) runs only with `TAOM_WRITE_ENTRY_POINT_BASELINE=1`, otherwise it calls
`Assert.Inconclusive`. With the variable set it keeps every existing row, lowers each to
`Math.Min(live, old)`, never adds a class, rewrites the five-line header, and writes the file with
`File.WriteAllLines` (CRLF on Windows, the file's current form). This plan removes four lines from
the class (559, 649, the blank 650, 680), so the row becomes `661`. `MaxBaselineEntries = 18`
(`:34`) does not change: no row is removed. Lowering a row records a shrink, which the gate asks
for; it does not loosen the gate. The same regeneration was used by earlier commits
(`git log --oneline -3 -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` shows
`efcf3f693`, `f2ee9a16f`, `8caac7da5`).

Other source-reading tests that read this controller and must stay green:
`TAOM.Tests/Features/Warg/WargControllerSwapTests.cs` (lines 60, 85, 95 read the file; they pin
`SwitchMainAgentController`, `HoldsHero` and the absence of `OnLockedAgentChanged`, none of which this
plan touches), `TAOM.Tests/Composition/SubModuleMissionViewWiringTests.cs`,
`TAOM.Tests/Features/SeatOfSeeing/SeatOfSeeingWiringTests.cs`,
`TAOM.Tests/Infrastructure/ClientMissionTypesTests.cs`. No other checked-in baseline
(`ServerJitBaseline.txt`, `ServiceSignatureBaseline.txt`, `SilentCatchBaseline.txt`) names any changed
type (`git grep` over `*.txt` and `*.json` found only the line-7 row above).

### The stale `Patch0_BattleScenes` comments, and what the code says

`Main/SubModule.cs:490-495`, inside `protected override void OnSubModuleLoad()` (line 114). Read only:
`SubModule.cs` is single-owner and is NOT edited.

```csharp
        // Patch0_BattleScenes: loads TAOM's sp_battle_scenes.xml (full 0-255 map_indices coverage) so the
        // TAOM_Map Main_map grid's extended indices (158-255) resolve to real battle terrains instead of
        // FailedAsserting against vanilla's 1-157 table. Re-enabled 2026-06-01 (TAOM_Map ships Main_map +
        // the extended XML exists; 3 patch targets verified against installed 1.4.5). In-game grid validation
        // pending the worldmap_battle_scene_grid re-author. See docs/reference/worldmap-battle-scene-grid.md.
        TryPatchCategory("Patch0_BattleScenes");
```

A category that really is never registered today, for the formatter's example:
`Main/SubModule.cs:530-535`:

```csharp
        // Patch21_ShaderPrecompilation: PARKED 2026-09-25 with the main-menu option (see
        ...
        // TryPatchCategory("Patch21_ShaderPrecompilation");
```

and its one declaration, `Main/Features/ShaderPrecompilation/Hooks/LoadingScreen_ShaderProgress_Patch.cs:17`
`[HarmonyPatchCategory("Patch21_ShaderPrecompilation")]` (`git grep -n "Patch21_ShaderPrecompilation" -- Main Client`
prints only those two files).

`Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs:16-21`:

```csharp
    // Phase 9b #156 — `volatile` prevents the JIT from caching this re-entry guard in a register
    // and missing writes from another thread. The patch fires from rendering threads (`GetBattleSceneIndexMap`
    // is called during map load and battle init), so multi-thread visibility matters. Dormant
    // today (category Patch0_BattleScenes is commented out in SubModule.cs), but if the feature is
    // ever re-enabled this guard must be cross-thread-correct. Also marked this type `static` per
    // the Harmony 2 patch-type naming rule (same fix as #151).
```

The file's own wording note (lines 23 to 30) warns: a source scanner in
`CoopVetoClassificationTests` once read a comment's "class" next to a line break as a declaration.
That test now strips comments (`CoopVetoClassificationTests.cs:473-474`), but keep the word "class"
out of the new comment anyway.

`Main/Features/DevConsole/PatchReportFormatter.cs:12-17`:

```csharp
/// Two behaviours are deliberately encoded rather than flagged as faults, because both are correct
/// by design and a report that cried wolf on them would be ignored within a week:
/// manual patches carry no <c>[HarmonyPatchCategory]</c> and get their own bucket, and some
/// categories (e.g. <c>Patch0_BattleScenes</c>) are deliberately never registered, so
/// declared-but-not-applied is sometimes the right answer. The report states the facts and lets the
/// reader apply the registry.
```

`TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs:14-17` (the same claim; the plan's
stale-claim sweep found it):

```csharp
/// `taom_debug`. Two behaviours are deliberately encoded rather than treated as bugs: manual patches
/// carry no category by design, and `Patch0_BattleScenes` is deliberately disabled — so
/// declared-but-not-applied is sometimes the correct output, and the report has to say which is which
/// rather than crying wolf.
```

No test uses `Patch0_BattleScenes` as data (`git grep -n Patch0_BattleScenes -- TAOM.Tests` prints only
that comment line).

### The doc lines (`docs/features/gui-sprite-system.md`, CRLF, very long lines)

Line 241:

```
The diamond grid of #388, not the earlier expandable pip panels. The prefab declares two visual definitions, `CareerHeaderSlide` and `CareerFooterSlide` (the title bar and the Done button slide in); there is no `ExtendablePanel`, and no prefab binds `IsExpanded`, `IsLocked` or `ExecuteToggleExpand` on the group VM.
```

Line 304 (end of it):

```
... Exposed by the VMs but bound by no prefab: `AbilityLabel` and the int `FreeCareerPoints` on the screen VM, `Name`, `KeystoneIconSprite` and `HasKeystoneIcon` on the choice, and `IsExpanded`, `IsLocked`, `Tier` on the group.
```

Line 371:

```
| `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs` | Choice group VM (group name and its choices; its expand state is bound by no prefab) |
```

History files that also name these members stay as they are (they record past state):
`CHANGELOG.md`, `docs/changelog-archive/`, `docs/reviews/`, `plans/`, `docs/audits/`, `docs/archive/`.

### Blast radius (`python tools/graphify_taom.py affected "<Type>" --depth 2` at `7087fa633`)

- `ReflectionUtils`: member callers are all `FindTypeAcrossLoadedAssemblies()`:
  `IncompatibleModDetector.cs:L222`, `SaveShield.cs:L195`, `VersionProbe.cs:L68`,
  `CoopRecruitAttributionPatch.cs:L55`, `CoopUpgradeChargePatch.cs:L61`. No caller of the two deleted members.
- `MutationParams`: `IMutationCalculatorRegistry.cs:L8,L9`, `MutationCalculatorRegistry.cs:L19,L24`,
  `BuiltInCalculators.cs:L7` (`RegisterAll`), `MutationService.cs:L91`, and the tests in
  `MutationCalculatorRegistryTests.cs` and `MutationParamsTests.cs` (all `GetFloat_*`). No `GetBool` or `GetInt` caller.
- `FormationRouting`: member callers are `TryParseClass()` (`CultureDoctrineConfigProvider.cs:L266`,
  `CultureMoraleAndAggressionTests.cs:L103`) and `TryRoute()` (`FormationRoutingRule.cs:L16`,
  `CultureMoraleAndAggressionTests.cs:L117`; both tests are in its class `FormationRoutingTests`, :98), plus type references from `Doctrine`, `DoctrineCatalog`,
  `TacticRoster`, `RoutedFormationGuard`, `FormationRoutingSubscriber`, `TeamDoctrineInstaller`. No `IsRoutable` caller.
- `StackFrameSnapshotBuilder`: member callers are all `FromException()`: `CrashReportService.cs:L108,L209`,
  `StackFrameSnapshotBuilderTests.cs:L13,L24`.
- `CareerChoiceGroupObjectVM`: referenced by `CareerScreenVM.cs:L648`, `GauntletCareerScreen.cs:L25`,
  `CareerScreenVMTests.cs` (L567, L585, L770, L1103, L1172, L1177). No member caller of the deleted members.
- `AutonomousMovementPlayerController`: "No affected nodes found." Via `IAutonomousMovementController`:
  `SeatScreenAdapter.cs:L62` and many importers of the namespace; none calls the deleted method (the
  interface does not declare it).
- `MBMapScene_GetBattleSceneIndexMap_Patch`: "No affected nodes found." (comment edit only)
- `PatchReportFormatter`: `DiagnosticCheats.cs:L39` (`PrintPatches`) and the eight
  `PatchReportFormatterTests` methods. (comment edit only)

### Conventions that bind this change

- **ADR-002** (thin entry points): entry points stay under 150 lines; the older ones over it sit on a
  shrink-only baseline, which a deletion must lower in the same change (`EntryPointLineCountGateTests`).
- **ADR-007** (adapters): services take adapters, never sealed TaleWorlds types. Not affected: nothing is
  added.
- **ADR-008** (testability): every service decision path has a unit test. Not affected: no decision
  path is added or removed; the deleted members had no tests.
- **ADR-013** (server-safe core, client UI submodule): the controller lives in `TAOM.Client`; the
  `Main/TAOM.csproj` build does not compile it, but `TAOM.Tests/TAOM.Tests.csproj:28` references
  `Client/TAOM.Client.csproj`, so the test command compiles it.
- **`.claude/rules/csharp-architecture.md`**: "Entry points <150 lines" row (the gate above). No other
  row applies to a deletion.
- **`.claude/rules/gui-ui.md`**: a VM property bound by a prefab must exist; the deleted `IsExpanded`
  is bound by none (the searches above).
- **`.claude/rules/harmony-patches.md`**: before editing an existing patch file, read its registry
  entry. `docs/reference/harmony-patch-registry.md` "## Patch0_BattleScenes" says "Battle scenes:
  **ACTIVE**. ... Re-enabled 2026-06-01; applied unconditionally from `OnSubModuleLoad`". This plan
  changes only comment lines in the patch file.
- **Lesson** `docs/reviews/lessons/testing-qa.md` "A deletion's RED step lives in the log, not the
  suite": no permanent test that something is absent. The RED proof is the zero-caller grep and the
  baseline gate's failure, both in your report.
- **Prose** (AGENTS.md "Human prose"): new comment and doc text has no em or en dash. Existing
  lines you do not rewrite keep theirs.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)`, at most `11 Warning(s)` |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259` |
| One test class | the same, plus `--filter "FullyQualifiedName~<ClassName>"` | the named tests run; a filter matching nothing proves nothing |
| Regenerate the line baseline | `TAOM_WRITE_ENTRY_POINT_BASELINE=1 dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"` | `Passed!  - Failed:     0, Passed:     1` |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 |
| Data | `python tools/validate_moduledata.py` | not needed: no ModuleData change |
| Python tools, hooks | not needed: no `tools/` or `.claude/` change | |

Prefix every `dotnet` command with the TEMP and TMP your dispatch rules give, for example
`cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet build ...`. Never `./build.ps1`: it deploys
into the game install. Put `TAOM_WRITE_ENTRY_POINT_BASELINE=1` only inline on the one regenerate
command; never export it.

## Scope

**In scope** (the only files you modify):
- `Dependencies/Foundation/ReflectionUtils.cs`
- `Main/Features/CareerSystem/Mutations/MutationParams.cs`
- `Main/Features/CultureDoctrine/Domain/FormationRouting.cs`
- `Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs`
- `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs`
- `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`
- `TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` (only through the regenerate command)
- `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs` (comment lines only)
- `Main/Features/DevConsole/PatchReportFormatter.cs` (comment line only)
- `TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs` (comment line only)
- `docs/features/gui-sprite-system.md` (lines 241, 304, 371)

**Out of scope** (do NOT touch, even though they look related):
- `Main/SubModule.cs` and `Main/IoC.cs`: single-owner; nothing here needs an edit. Read
  `SubModule.cs` only.
- The five empty `IMissionListener` members in `Main/Features/Messengers/MessengerCampaignBehavior.cs`,
  the vendored BehaviorTrees code (`Main/BehaviorTrees/`, `Main/BehaviorTreeWrapper/`), and the three
  `TcbVM` commands in `Client/Features/CustomBattles/Muster/TcbVM.cs` (a bug lead with work in flight).
- Any member not listed in this plan, including `IsLocked`, `Tier` and `_isLocked` on the career group
  VM, and `FindTypeAcrossLoadedAssemblies`.
- `using` directives: leave them as they are (an unused `using` raises no warning in this build).
- The `BetaDeps parity` lines of the ReflectionUtils summary (provenance text; see
  `docs/reference/provenance-register.md:75`).
- History files: `CHANGELOG.md`, `docs/changelog-archive/`, `docs/reviews/`, `docs/audits/`,
  `docs/archive/`, `plans/`. `docs/migration/dr3-maintenance.md:238` ("`ReflectionUtils` (small
  reflection helpers)") is in a folder `lint_docs.py` treats as historical; leave it.
- `Dependencies/_Module/SubModule.xml` or any version number.
- The gates themselves. Never turn a gate green by editing it: never edit `EntryPointLineCountGateTests.cs`,
  never change `MaxBaselineEntries`, never hand-edit the baseline file, never `[Ignore]` a test.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (`v2.0.38` at planning). Use
  `refactor(dead-code): <version> - delete eight members nothing calls`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Never edit `CHANGELOG.md`. No AI attribution trailer. Add a `Not-tested:` trailer.
- Stage explicit paths only; never `-A`, `-u`, `.` or `commit -a`. Write the message to a file in your
  scratch folder with the Write tool and run `git commit -F "<file>"`. Never `--no-verify`.
- One commit.

## Steps

### Step 1: record the base

Run the drift check (top of this plan). Then run the build and the full suite once, before any edit:

```bash
cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
```

Write both totals lines into your report.

**Verify**: the build shows `0 Error(s)` and at most `11 Warning(s)` (a lower count is fine; record
it and use it as the ceiling from here on); the test totals read
`Failed: 0, Passed: 39250, Skipped: 9, Total: 39259`. A different base is a STOP unless the drift
check explains it.

### Step 2: RED for the deletion: prove nothing calls the members (in the log)

Run the zero-caller search exactly as in "Current state" and paste its full output into your report:

```bash
cd "<your worktree>" && for n in TryInvokeStatic GetAssemblySimpleName GetBool GetInt IsRoutable FromCurrent ExecuteToggleExpand OnWeaponUsageToggleRequested _weaponUsageToggleRequested IsExpanded _isExpanded; do echo "=== $n"; git grep -n -w -I "$n"; done
```

**Verify**: the hits are exactly the ones listed under "The zero-caller search" (same files, same
lines). Any new hit in a `.cs`, `.xml`, `.xslt`, `.json`, `.py` or `.ps1` file outside the declaring
file and the history folders is a caller the planner missed: STOP for that member (see "STOP
conditions"), skip it and every edit that depends on it, and carry on with the rest.

### Step 3: delete the six members in four core and TAOM.Dependencies files, and correct the ReflectionUtils summary

1. `Dependencies/Foundation/ReflectionUtils.cs`: delete lines 46 to 80 (the blank line after
   `FindTypeAcrossLoadedAssemblies`'s closing brace at 45, through the closing `}` of
   `GetAssemblySimpleName`), so the file ends:

   ```csharp
           return null;
       }
   }
   ```

   Then replace summary lines 8 to 10 (from `/// Small reflection helpers used by` through
   `/// return null/empty/false on any failure rather than throwing.`) with:

   ```csharp
   /// Finds a type when its home assembly is unknown. Used by IncompatibleModDetector, SaveShield
   /// and VersionProbe in this assembly, and by the CoopInterop feature in TAOM.dll. Exception-safe:
   /// it returns null on any failure rather than throwing.
   ```

   Keep lines 11 to 14 (`///`, the two `BetaDeps parity` lines, `/// </summary>`) unchanged. Before
   you write the summary, re-run `git grep -n "ReflectionUtils\."` and confirm its callers are still
   the eight listed under "Current state"; if a caller outside those files appears, name it in the
   summary as well.
2. `Main/Features/CareerSystem/Mutations/MutationParams.cs`: delete lines 28 to 40 (the blank line
   after `GetString`, `GetBool`, the blank line, `GetInt`), so `GetString`'s closing `    }` is
   followed directly by the class's closing `}`.
3. `Main/Features/CultureDoctrine/Domain/FormationRouting.cs`: delete lines 53 to 56 (the
   `/// <summary>The routable classes ...` line, the two `IsRoutable` lines, and the blank line after
   them), leaving one blank line between `TryRoute`'s closing brace and `RoutableByName`.
4. `Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs`: delete lines 21 to 30
   (`FromCurrent` and the blank line after it), leaving one blank line between `FromException` and
   `Build`.

**Verify**:
- The build command gives exit 0, `0 Error(s)`, at most `11 Warning(s)` (this build compiles
  `TAOM.Dependencies` through the project reference at `Main/TAOM.csproj:84`).
- `cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~MutationParamsTests|FullyQualifiedName~MutationCalculatorRegistryTests|FullyQualifiedName~StackFrameSnapshotBuilderTests|FullyQualifiedName~FormationRoutingTests"`
  gives `Failed: 0` with a non-zero `Passed:` count.

### Step 4: delete the career group's toggle and its expand state, and correct the doc lines

1. `Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs`:
   - delete line 10 `    private bool _isExpanded;`;
   - delete lines 47 to 60 (the `[DataSourceProperty]` above `public bool IsExpanded`, the whole
     property, and the blank line after its closing brace), so `public int Tier => _group.Tier;` is
     followed by one blank line and the `[DataSourceProperty]` of `IsLocked`;
   - delete lines 88 to 93 (the blank line after the `Choices` property and the whole
     `ExecuteToggleExpand` method), so the `Choices` property's closing `    }` is followed directly by
     the class's closing `}`.
2. `docs/features/gui-sprite-system.md`, three edits with the Edit tool (the lines are long; match on
   the fragment, which is unique in the file). Each block below is old text, then new text.

   Line 241, old then new:

   ```
   and no prefab binds `IsExpanded`, `IsLocked` or `ExecuteToggleExpand` on the group VM.
   ```
   ```
   and no prefab binds `IsLocked` on the group VM.
   ```

   Line 304, old then new:

   ```
   and `IsExpanded`, `IsLocked`, `Tier` on the group.
   ```
   ```
   and `IsLocked` and `Tier` on the group.
   ```

   Line 371, old then new:

   ```
   Choice group VM (group name and its choices; its expand state is bound by no prefab)
   ```
   ```
   Choice group VM (group name, tier, lock state and its choices)
   ```

**Verify**:
- The build command gives exit 0, `0 Error(s)`, at most `11 Warning(s)`.
- `cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~CareerScreenPrefabBindingTests|FullyQualifiedName~CareerScreenVMTests"`
  gives `Failed: 0` with a non-zero `Passed:` count (the binding test checks every prefab binding on
  the career screen resolves to a VM member).
- `cd "<your worktree>" && git grep -n -w -e IsExpanded -e ExecuteToggleExpand -- Main/Features/CareerSystem Main/_Module/GUI/PreFabs/CareerSystem docs/features`
  prints nothing.

### Step 5: delete the controller's dead toggle request, then lower its baseline row

1. `Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`, from the bottom up so the
   line numbers hold:
   - delete line 680 `    private bool _weaponUsageToggleRequested;`;
   - delete lines 649 and 650 (`    public void OnWeaponUsageToggleRequested() { _weaponUsageToggleRequested = true; }`
     and the blank line after it), leaving one blank line between `OnPlayerToggleOrder` and
     `OnManagedOptionChanged`;
   - delete line 559 `            _weaponUsageToggleRequested = false;`;
   - on line 556 replace `        if (Input.IsGameKeyPressed(17) || _weaponUsageToggleRequested)` with
     `        if (Input.IsGameKeyPressed(17))`. Keep the braces. The method must read:

     ```csharp
         private void ProcessWeaponToggleInput(Agent mainAgent)
         {
             if (Input.IsGameKeyPressed(17))
             {
                 mainAgent.EventControlFlags |= Agent.EventControlFlag.ToggleAlternativeWeapon;
             }
         }
     ```

   The file now has 677 lines and the class spans lines 17 to 677: 661 lines.
2. RED for the baseline row: run the gate class.

   ```bash
   cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests"
   ```

   It must fail exactly one test, `EntryPoints_StayUnder150Lines_ExceptTheShrinkOnlyBaseline`, whose
   message names `TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController`, `661 lines` and
   `below its baseline 665`, and no other class. Quote the totals line and that message in your report.
3. Regenerate the baseline:

   ```bash
   cd "<your worktree>" && TAOM_WRITE_ENTRY_POINT_BASELINE=1 TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~EntryPointLineCountGateTests.Baseline_Regenerate_WhenAsked"
   ```

   Expected: `Passed!  - Failed:     0, Passed:     1`.
4. Check that only that row moved:

   ```bash
   cd "<your worktree>" && git diff --stat -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt && git diff -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt
   ```

   Expected: `1 file changed, 1 insertion(+), 1 deletion(-)`; the one removed line is
   `TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController 665` and the one added line is
   `TAOM.Features.AdvancedCombat.AutonomousMovementPlayerController 661`. The header still ends
   `# Entries: 18.` and `file TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` still says
   `with CRLF line terminators`.

**Verify**:
- The gate class filter from item 2 now gives `Failed: 0` (the regenerate test counts as skipped or
  inconclusive without the variable).
- `cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~WargControllerSwapTests|FullyQualifiedName~ClientMissionTypesTests|FullyQualifiedName~SeatOfSeeingWiringTests|FullyQualifiedName~SubModuleMissionViewWiringTests"`
  gives `Failed: 0` with a non-zero `Passed:` count (this run also compiles `TAOM.Client`).
- `cd "<your worktree>" && grep -c "IsGameKeyPressed(17)" Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`
  prints `1`, and `grep -c "ToggleAlternativeWeapon" Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs`
  prints `1`.

### Step 6: correct the three `Patch0_BattleScenes` comments

First re-read the code the new text describes, and STOP if it differs: `Main/SubModule.cs:490-495`
still calls `TryPatchCategory("Patch0_BattleScenes");` uncommented inside `OnSubModuleLoad`, and
`git grep -n "Patch21_ShaderPrecompilation" -- Main Client` still prints only `Main/SubModule.cs` (the
commented-out lines 530 and 535) and `LoadingScreen_ShaderProgress_Patch.cs:17`.

1. `Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs`: replace lines 18 to
   20, which read:

   ```csharp
       // is called during map load and battle init), so multi-thread visibility matters. Dormant
       // today (category Patch0_BattleScenes is commented out in SubModule.cs), but if the feature is
       // ever re-enabled this guard must be cross-thread-correct. Also marked this type `static` per
   ```

   with:

   ```csharp
       // is called during map load and battle init), so multi-thread visibility matters. The category
       // Patch0_BattleScenes is live: SubModule.OnSubModuleLoad applies it at every module load
       // (re-enabled 2026-06-01), so this guard runs in play. Also marked this type `static` per
   ```

   Line 21 (`    // the Harmony 2 patch-type naming rule (same fix as #151).`) stays. The comment keeps its
   six lines, and the word "class" does not appear in it.
2. `Main/Features/DevConsole/PatchReportFormatter.cs` line 15, old then new:

   ```csharp
   /// categories (e.g. <c>Patch0_BattleScenes</c>) are deliberately never registered, so
   ```
   ```csharp
   /// categories (e.g. <c>Patch21_ShaderPrecompilation</c>, parked) are deliberately never registered, so
   ```
3. `TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs` line 15, old (one line) then new
   (two lines, wrapped to the width of their neighbours; the new text drops the old line's dash):

   ```csharp
   /// carry no category by design, and `Patch0_BattleScenes` is deliberately disabled — so
   ```
   ```csharp
   /// carry no category by design, and a parked category such as `Patch21_ShaderPrecompilation` is
   /// never registered, so
   ```

   Line 16 (`/// declared-but-not-applied is sometimes the correct output, ...`) stays as it is.

**Verify**:
- This search for the four stale fragments prints nothing:

  ```bash
  cd "<your worktree>" && git grep -n -F -e 'visibility matters. Dormant' -e 'Patch0_BattleScenes is commented out' -e '<c>Patch0_BattleScenes</c>) are deliberately never registered' -e '`Patch0_BattleScenes` is deliberately disabled' -- Main Client TAOM.Tests
  ```

  At `7087fa633` it prints exactly four lines: `MBMapScene_GetBattleSceneIndexMap_Patch.cs:18` and `:19`,
  `PatchReportFormatter.cs:15` and `PatchReportFormatterTests.cs:15`. The fragments are fixed strings
  on purpose. A looser pattern also hits true lines that are out of scope:
  `TAOM.Tests/Features/CoopInterop/CoopVetoClassificationTests.cs:438` says the NavalTravel category
  "is commented out in SubModule.cs", which is still correct, and the word "dormant" appears in five
  unrelated files. Do not edit either.
- `cd "<your worktree>" && git diff -U0 -- Main/Features/BattleScenes Main/Features/DevConsole TAOM.Tests/Features/DevConsole | grep -E "^[+-][^+-]" | grep -v -E "^[+-][[:space:]]*//"`
  prints nothing (every changed line is a comment).
- `cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchReportFormatterTests|FullyQualifiedName~CoopVetoClassificationTests"`
  gives `Failed: 0` with a non-zero `Passed:` count.

### Step 7: full verification

```bash
cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=
cd "<your worktree>" && TEMP="<tmp>" TMP="<tmp>" dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
cd "<your worktree>" && python -B tools/lint_docs.py --fail-on-drift; echo "exit=$?"
cd "<your worktree>" && for n in TryInvokeStatic GetAssemblySimpleName GetBool GetInt IsRoutable FromCurrent ExecuteToggleExpand OnWeaponUsageToggleRequested _weaponUsageToggleRequested IsExpanded _isExpanded; do echo "=== $n"; git grep -n -w -I "$n" -- . ':!CHANGELOG.md' ':!docs/changelog-archive' ':!docs/reviews' ':!plans' ':!docs/audits' ':!docs/archive'; done
cd "<your worktree>" && git status --porcelain
cd "<your worktree>" && git diff -- . | LC_ALL=C.UTF-8 grep -n -P "^\+.*[\x{2013}\x{2014}]"
```

**Verify**:
- Build: exit 0, `0 Error(s)`, at most `11 Warning(s)`.
- Tests: `Passed!  - Failed:     0, Passed: 39250, Skipped:     9, Total: 39259` (no test is added
  or removed, so the totals equal the base from Step 1). Quote the line.
- Lint: `exit=0`.
- The grep prints only `===` headers, except `IsExpanded` and `_isExpanded`, whose only hits are
  `Client/Features/FactionUI/Campaign/EncyclopediaListScreenVM.cs`,
  `Client/Features/FactionUI/Campaign/EncyclopediaListWidgets.cs` and
  `Main/_Module/GUI/PreFabs/FactionUI/TAOMEncyclopediaItemList.xml`.
- `git status --porcelain` lists exactly the eleven in-scope files, each as ` M`.
- The dash grep prints nothing (no added line carries an em or en dash).

### Step 8: commit

Write this message to a file in your scratch folder with the Write tool (replace `<version>` with the
`<Version>` from `Main/_Module/SubModule.xml`; keep lines at most 72 characters), then stage and commit:

```
refactor(dead-code): <version> - delete eight members nothing calls

Delete members that no code, test, prefab or installed module calls:
ReflectionUtils.TryInvokeStatic and GetAssemblySimpleName in
TAOM.Dependencies, MutationParams.GetBool and GetInt,
FormationRouting.IsRoutable, StackFrameSnapshotBuilder.FromCurrent,
the career group's ExecuteToggleExpand with the IsExpanded state only
it read, and the player controller's OnWeaponUsageToggleRequested with
the flag only it set. The controller still toggles the alternative
weapon usage on its key, as before; vanilla's quick-release caller
holds a MissionMainAgentController and never reached TAOM's copy.

Correct the comments that described what is gone or wrong: the
ReflectionUtils summary now names its real callers, and two comments
that called Patch0_BattleScenes disabled now match SubModule, which
applies it at every module load. gui-sprite-system.md drops the
deleted career members.

The controller is on the shrink-only entry point baseline, so its row
drops from 665 to 661 lines.

Not-tested: in game. A pure deletion: the build and the full suite,
equal to the base, are the proof.
```

```bash
cd "<your worktree>" && git add -- Dependencies/Foundation/ReflectionUtils.cs Main/Features/CareerSystem/Mutations/MutationParams.cs Main/Features/CultureDoctrine/Domain/FormationRouting.cs Main/Features/CrashReport/Collectors/StackFrameSnapshotBuilder.cs Main/Features/CareerSystem/UI/CareerChoiceGroupObjectVM.cs Client/Features/AdvancedCombat/AutonomousMovementPlayerController.cs TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt Main/Features/BattleScenes/Hooks/MBMapScene_GetBattleSceneIndexMap_Patch.cs Main/Features/DevConsole/PatchReportFormatter.cs TAOM.Tests/Features/DevConsole/PatchReportFormatterTests.cs docs/features/gui-sprite-system.md
cd "<your worktree>" && git commit -F "<your message file>"
cd "<your worktree>" && git show --stat HEAD && git status --porcelain
```

If you skipped a member under a STOP in Step 2, leave its file out of the `git add`, and drop it from
the message (and fix "eight" in the subject).

**Verify**: `git show --stat HEAD` lists the eleven files (or fewer, if a member was skipped) and
nothing else; `git status --porcelain` prints nothing.

## Test plan

- No new tests. A pure deletion proves its RED in the log, not with a permanent absence test
  (`docs/reviews/lessons/testing-qa.md`, "A deletion's RED step lives in the log"). The RED evidence
  is the Step 2 grep (no caller) and the Step 5 gate failure (`661 lines is below its baseline 665`),
  both quoted in your report.
- Behaviour that must be preserved, and what pins it: `GetFloat` and `GetString`
  (`MutationParamsTests`, `MutationCalculatorRegistryTests`); `FromException`
  (`StackFrameSnapshotBuilderTests`); `TryParseClass` and `TryRoute` (`FormationRoutingTests`, in
  `CultureMoraleAndAggressionTests.cs`);
  the career screen's prefab bindings (`CareerScreenPrefabBindingTests`) and VM (`CareerScreenVMTests`);
  the controller's shape (`WargControllerSwapTests`, `ClientMissionTypesTests`); the controller's key
  toggle (Step 5 grep counts; the method is engine-bound and has no unit test).
- What cannot be tested: the controller's `ProcessWeaponToggleInput` reads the engine's input
  (`Input.IsGameKeyPressed(17)`) inside a live mission; only that line needs the engine. It keeps its
  read and its flag write; only the always-false `|| _weaponUsageToggleRequested` clause and its reset
  go.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The build command exits 0 with `0 Error(s)` and at most `11 Warning(s)`.
- [ ] The full test command prints `Failed: 0, Passed: 39250, Skipped: 9, Total: 39259` (equal to
      the base).
- [ ] `python tools/lint_docs.py --fail-on-drift` exits 0.
- [ ] The Step 7 grep over the whole repo, tests included and history folders excluded, prints no hit
      for `TryInvokeStatic`, `GetAssemblySimpleName`, `GetBool`, `GetInt`, `IsRoutable`, `FromCurrent`,
      `ExecuteToggleExpand`, `OnWeaponUsageToggleRequested` or `_weaponUsageToggleRequested`, and hits
      for `IsExpanded` and `_isExpanded` only in the three encyclopedia files.
- [ ] `git diff 7087fa633 -- TAOM.Tests/Infrastructure/EntryPointLineCountBaseline.txt` changes exactly one
      row, `... AutonomousMovementPlayerController 665` to `... 661`.
- [ ] The Step 6 fixed-string search for the four stale `Patch0_BattleScenes` fragments prints nothing.
- [ ] `git status --porcelain` is empty after the commit, and the commit holds only in-scope files.
- [ ] Every comment and doc line this plan supplied was re-checked against the code it describes
      (Step 3 item 1 and the Step 6 pre-check), and the result is in your report.

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- Step 1's base differs from the baseline above and the drift check does not explain it.
- The Step 2 grep shows a caller of a member that the planner did not list (code, test, prefab, XML,
  XSLT, JSON or script). Skip that member, the state that depends on it and its doc line, continue
  with the rest, and report the hit. If the caller is of `IsExpanded` from a career prefab, skip the
  whole career VM edit.
- The Step 5 gate run fails any class other than `AutonomousMovementPlayerController`, or reports a
  number other than 661; or the regenerated file's diff touches any line other than that one row
  (another row, the header, the entry count, the line endings).
- `WargControllerSwapTests`, `CareerScreenPrefabBindingTests` or `CoopVetoClassificationTests` fails.
- The Step 6 pre-check fails: `Main/SubModule.cs` no longer applies `Patch0_BattleScenes`, or
  `Patch21_ShaderPrecompilation` is applied anywhere. The drafted comments would then be false.
- The build shows more than 11 warnings, or a warning naming a file this plan changed.
- A step's verification fails twice after a reasonable fix.
- A hook denies or asks; or the fix seems to need an out-of-scope file, especially `Main/SubModule.cs`,
  `Main/IoC.cs`, a csproj, `EntryPointLineCountGateTests.cs` or a protected file.
- A TaleWorlds signature you re-check differs from "Engine facts"; report the mismatch.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (none known for this finding).
- `/deep-review` before merge (the commit touches C#), per CLAUDE.md.
- No `/localize`: no player-facing text changes. No feature doc or feature-map row: no feature is
  added; `docs/features/gui-sprite-system.md` is the only doc edit and the plan makes it.

## After merge: the maintainer's actions

None. No hook, setting or deployed file changes; the next `./build.ps1` deploys the smaller
assemblies. `TAOM.Dependencies.dll` loses two public members: a third-party mod compiled against
them (none is installed on the desktop) would fail to bind them.

## Maintenance notes

- `AutonomousMovementPlayerController` stays on the shrink-only baseline at 661; any later edit that
  adds lines fails the gate, and any later deletion must lower the row again with the same command.
- A lead, UNVERIFIED and out of scope: vanilla's equip-drop quick release
  (`MissionGauntletMainAgentEquipDropView.HandleQuickRelease`) toggles weapon usage on vanilla's
  `MissionMainAgentController` only. While TAOM's controller holds the player (vanilla's controller
  disabled by `SwitchMainAgentController`), that quick-release toggle may do nothing. This plan does not
  change that path; deleting TAOM's never-called copy neither causes nor fixes it. If the maintainer
  wants quick release to work under TAOM's controller, that is a feature with an in-game check.
- What the review should probe: that the controller's toggle still reads key 17 and sets
  `ToggleAlternativeWeapon` (Step 5); that the regenerated baseline changed one row only; that the new
  ReflectionUtils summary names the real callers; that the three comment edits changed no code line.
- Deferred: `docs/migration/dr3-maintenance.md:238` calls `ReflectionUtils` "small reflection helpers"
  (plural); it is in a historical folder and left alone. `IsLocked` and `Tier` on the career group VM are
  still bound by no prefab (gui-sprite-system.md says so); deleting them was not in this finding.
- This plan widens the brief's comment list by two lines its own stale-claim sweep found:
  `PatchReportFormatterTests.cs:15` (the same Patch0 claim as the formatter) and
  `gui-sprite-system.md:371` (the "expand state" table cell). Both are comment or doc text only.
