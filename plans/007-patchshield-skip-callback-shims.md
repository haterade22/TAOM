# Plan 007: Stop PatchShield re-shielding the 247 callback shims at the first game start

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. Do NOT edit `plans/README.md`: the orchestrator
> maintains the index (this plan has no row there yet). When done, report
> back with the Done-criteria output.
>
> **Drift check (run first)**:
> `git diff --stat b2e387db..HEAD -- Dependencies/Foundation/PatchShield.cs Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/SubModule.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs docs/migration/dr3-maintenance.md docs/migration/v1.5.2-impact.md Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs`
> Expected: no output (verified empty at `4b5662b2` on 2026-09-23). If any
> file changed since this plan was written, compare the "Current state"
> excerpts against the live code before proceeding; on a mismatch, treat it
> as a STOP condition.
> `Main/SubModule.cs` is a read-only premise file that another session edits
> often, so check only the two lines this plan relies on:
> `git grep -n -e 'new Harmony("com.taom.mod")' -e 'Native2ManagedPatcher>().AttachAll' -- Main/SubModule.cs`
> Expected: exactly two hits (one per pattern). No hit for either is a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none. A sibling plan from the same audit, `plans/006-crash-capture-boot-cost.md`,
  may narrow the same 247-method sweep from the Main side; it did not exist on disk when this plan was
  revised. If that file is absent, ignore every mention of plan 006 below except the STOP check.
- **Category**: perf
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

TAOM.Dependencies' PatchShield attaches a Harmony finalizer to every Harmony-patched method in the
process. Its second pass runs synchronously on the main thread inside the loading screen of the
first game start of every process (new campaign, loaded campaign or custom battle). On the
maintainer's machine that pass attaches 372 finalizers in about 69 s (about 64% of a custom battle's
107 s loading screen), because every `Harmony.Patch` call currently costs about 186 ms there (an
environmental tax whose cause is unknown; it was 5 to 10 ms per call before 2026-06-12). About 247
of those 372 attaches land on the engine's native-to-managed callback shims
(`ManagedCallbacks.*CallbacksGenerated`), which TAOM's own crash reporter already wraps with a
finalizer that swallows exceptions in normal play; PatchShield adds nothing there and never swallowed
a single exception in 466 logged sessions. Excluding that namespace removes about two thirds of pass
2's attaches (about 46 s per first game start on this machine, about 1.5 s in the fast mode), and a
per-call `__originalMethod` wrapper from engine callback hot paths. The plan also makes the per-attach
cost visible in `diag.log` (which ships in every crash bundle) and fixes log and doc text that claims
pass 2 runs at the main menu.

## Current state

### Files and their roles

- `Dependencies/Foundation/PatchShield.cs` (452 lines): the shield. Holds the private
  namespace-exclusion list (lines 60-69, with its comment at 50-59), the private `IsExcludedTarget`
  (71-84), `Install()` (130-244) and the `shield pass` log line (237).
- `Dependencies/Foundation/PatchShieldPolicy.cs` (153 lines): pure, Harmony-free decisions extracted
  from PatchShield so they are unit-testable. This is where the exclusion list moves.
- `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs` (168 lines, 16 `[TestMethod]`s):
  existing MSTest class `PatchShieldPolicyTests`, namespace `TAOM.Tests.Infrastructure.Dependencies`.
  Its `using` block is `Microsoft.VisualStudio.TestTools.UnitTesting`, `System`, `System.Linq`,
  `TAOM.Dependencies.Foundation`. New tests go here.
- `Dependencies/SubModule.cs` (324 lines): TAOM.Dependencies' `MBSubModuleBase`. Pass 1 at line 234
  (`OnSubModuleLoad`), pass 2 at line 288 (`OnGameInitializationFinished`); wrong doc comment at
  267-272 and wrong label at 276. This is NOT the single-owner `Main/SubModule.cs`.
- `Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs`: READ ONLY. It patches the 247 callback
  shims that PatchShield then re-shields.
- `Main/Features/CrashReport/Hooks/CrashReportPatchHelper.cs`: READ ONLY. The finalizer body those
  shims already carry.
- `docs/migration/dr3-maintenance.md` (lines 255, 260, 269, 270, 286, 291) and
  `docs/migration/v1.5.2-impact.md` (line 141): prose that describes the exclusion list and the wrong
  "main menu" timing.

Project wiring: `Dependencies/` is its own project, `Dependencies/TAOM.Dependencies.csproj`.
`Main/TAOM.csproj:89` has `<ProjectReference Include="..\Dependencies\TAOM.Dependencies.csproj">`, and
`TAOM.Tests/TAOM.Tests.csproj:27` references `..\Main\TAOM.csproj`, so the test project already sees
`TAOM.Dependencies.Foundation.PatchShieldPolicy` (the existing tests call it). No csproj edit is needed.
Nullable is enabled repo-wide (`Directory.Build.props:6`), so `string?` compiles.

### Excerpts at `b2e387db`

`Dependencies/Foundation/PatchShield.cs:50-84` (the list and the check this plan moves):

```csharp
    // Issue #331 round 2 (2026-07-09, measured): NEVER shield the Gauntlet/2D UI layer.
    // A shield finalizer binds __originalMethod, so Harmony's generated wrapper pays a
    // MethodBase.GetMethodFromHandle + try/catch on EVERY CALL (~50µs). The Gauntlet
    // prefab system contains per-widget-recursion methods that UIExtenderEx patches
    // (WidgetFactory.IsCustomType prefix, WidgetTemplate.OnRelease blank-transpiler);
    // a tournament's accumulated template tree calls them ~2 MILLION times at release,
    // so the shield tax amplified a milliseconds-scale teardown into a measured 104-109s
    // frozen exit (+8,276 gen0 GCs, invariant across sessions — stack-sampled proof in
    // docs/reviews/rca-tournament-exit-hang-2026-07-06.md round 2). Shield value there
    // is nil anyway: the only patcher of that layer is BUTR's own UIExtenderEx.
    private static readonly string[] ExcludedTargetNamespacePrefixes =
    {
        "TaleWorlds.GauntletUI",
        "TaleWorlds.TwoDimension",
        // Round-2 compat review (2026-07-10): TAOM's own Patch38 target
        // (SettlementNameplateWidget.DetermineTargetAlphaValue, ~3000 calls/sec on the
        // campaign map) lives here and was silently paying the shield tax every frame.
        // Same rationale as above: hot widget/view layer, shield value nil.
        "TaleWorlds.MountAndBlade.GauntletUI",
    };

    private static bool IsExcludedTarget(MethodBase method)
    {
        try
        {
            var ns = method.DeclaringType?.Namespace ?? string.Empty;
            foreach (var prefix in ExcludedTargetNamespacePrefixes)
            {
                if (ns.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            }
        }
        catch { /* fail open — an unreadable type just gets shielded as before */ }
        return false;
    }
```

`Dependencies/Foundation/PatchShield.cs:162-238` (the pass loop and its log line; the exclusion
comment is at 194-196, the check at 197, the per-method `harmony.Patch` at 222-223):

```csharp
            List<MethodBase> patched;
            try
            {
                patched = Harmony.GetAllPatchedMethods().ToList();
            }
            ...
            int added = 0, skipped = 0, alreadyShielded = 0;
            lock (_lock)
            {
                foreach (var method in patched)
                {
                    if (method == null) { skipped++; continue; }
                    if (_shielded.Contains(method)) { alreadyShielded++; continue; }
                    // (lines 181-192: skip methods declared in a "TAOM*" assembly)
                    // Never shield hot UI-layer targets — a per-call __originalMethod
                    // finalizer on the Gauntlet prefab system froze tournament exits for
                    // ~107s (#331 round 2). See ExcludedTargetNamespacePrefixes.
                    if (IsExcludedTarget(method))
                    {
                        _shielded.Add(method);
                        skipped++;
                        continue;
                    }
                    // (lines 204-215: skip SaveShield targets)
                    try
                    {
                        bool isVoid = true;
                        if (method is MethodInfo mi) isVoid = mi.ReturnType == typeof(void);
                        var finalizer = isVoid ? voidFinalizer : resultFinalizer;
                        harmony.Patch(method, prefix: null, postfix: null, transpiler: null,
                            finalizer: new HarmonyMethod(finalizer));
                        _shielded.Add(method);
                        added++;
                    }
                    ...
                }
            }

            if (added > 0 || alreadyShielded == 0)
            {
                DiagLog.Log(Tag, $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {_shielded.Count})");
            }
```

The file's current `using` block (lines 1-7) is `System`, `System.Collections.Generic`, `System.IO`,
`System.Linq`, `System.Reflection`, `System.Threading`, `HarmonyLib`. There is no
`System.Diagnostics` import yet.

`Dependencies/Foundation/PatchShieldPolicy.cs:1-11` (header; the class is `public static`, file-scoped
namespace). `CompiledProtectedOwnerPrefixes` is declared at lines 23-63 and ends with `};` on line 63.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The two pure decisions behind <see cref="PatchShield"/>'s rescue path, extracted so they can be
/// tested without Harmony or a running game. PatchShield keeps the plumbing; this keeps the policy.
/// </summary>
public static class PatchShieldPolicy
```

`PatchShieldPolicy.cs:126-128` already warns against widening the exclusion list to campaign code:

```csharp
    /// Extending <c>ExcludedTargetNamespacePrefixes</c> instead would have been wrong: adding
    /// <c>TaleWorlds.CampaignSystem</c> there excludes nearly everything TAOM shields IN SOLO PLAY
    /// TOO, because that list is not co-op-scoped.
```

`Dependencies/SubModule.cs:267-292` (complete):

```csharp
    /// <summary>
    /// Called when the main menu has rendered — signals the crash-loop detector that
    /// this session reached menu (deletes the launch marker, snapshots modlist as
    /// last-good). Override of MBSubModuleBase.OnGameInitializationFinished, the
    /// closest TaleWorlds lifecycle hook to "we made it past load."
    /// </summary>
    public override void OnGameInitializationFinished(Game game)
    {
        base.OnGameInitializationFinished(game);
        DiagLog.Log("Dependencies", "OnGameInitializationFinished: entered (main menu reached)");

        try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → MarkSessionLaunchSuccessful"); IncompatibleModDetector.MarkSessionLaunchSuccessful(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "MarkSessionLaunchSuccessful", ex); EarlyLog.Error($"[TAOM.Dependencies] MarkSessionLaunchSuccessful failed: {ex.Message}"); }

        // Second PatchShield pass — captures patches registered by mods that hook this
        // lifecycle event (after our OnSubModuleLoad).
        // Re-probe before pass 2: the launcher's active-module list is reliably populated by now,
        // so this is the read that actually decides the unpatch/swallow policy for the session.
        try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → CoopPresence.Refresh"); CoopPresence.Refresh(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "CoopPresence.Refresh pass2", ex); }

        try { DiagLog.Log("Dependencies", "OnGameInitializationFinished: → PatchShield.Install (pass 2)"); PatchShield.Install(); }
        catch (Exception ex) { DiagLog.LogCaught("Dependencies", "PatchShield.Install pass2", ex); EarlyLog.Error($"[TAOM.Dependencies] PatchShield.Install (post-init) failed: {ex.Message}"); }

        DiagLog.Log("Dependencies", "OnGameInitializationFinished: complete");
    }
```

`Main/Features/CrashReport/Hooks/Native2ManagedPatcher.cs:21-26,72-80` (why the shims are patched at
all; `AttachAll` runs from `Main/SubModule.cs:203` in TAOM's `OnSubModuleLoad`, AFTER PatchShield
pass 1 and BEFORE pass 2, when the MCM toggles `EnableCrashCapture` and
`EnableNativeToManagedCapture` are on, both default `true`):

```csharp
    private static readonly string[] AutogenAssemblies =
    {
        "TaleWorlds.MountAndBlade.AutoGenerated",
        "TaleWorlds.Engine.AutoGenerated",
        "TaleWorlds.DotNet.AutoGenerated",
    };
    ...
                var types = SafeGetTypes(asm).Where(t => t != null && t.Name.EndsWith("CallbacksGenerated", StringComparison.Ordinal));
                foreach (var t in types)
                {
                    var methods = t.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                    foreach (var m in methods)
                    {
                        try
                        {
                            harmony.Patch(m, finalizer: new HarmonyMethod(bridge));
```

Its bridge (`Native2ManagedPatcher.cs:119-123`) routes every shim exception to
`CrashReportPatchHelper.HandleAndSwallow(__exception, "TaleWorlds.AutoGenerated.<callback>")`.
`CrashReportPatchHelper.cs:29-53`: in the normal path (crash capture on, the crash service resolves
and handles the exception) it returns `null`, which swallows every exception type. It returns the
exception unchanged when `EnableCrashCapture` is off (line 38-39), when the service does not resolve
(47), on re-entry while already handling one (`_onPatchStack`, 32), or when handling itself throws (51).

TAOM's own Harmony id is `com.taom.mod` (`Main/SubModule.cs:194`).

### Engine facts (verified during planning, do not re-derive)

- The three shim types live in namespace `ManagedCallbacks` in the installed v1.5.3 DLLs
  (`pwsh tools/taom-src.ps1 path ManagedCallbacks.CoreCallbacksGenerated`, and the same for
  `EngineCallbacksGenerated` and `LibraryCallbacksGenerated`; each decompiled file opens with
  `namespace ManagedCallbacks;` and `internal static class <Name>CallbacksGenerated`).
- `MBSubModuleBase.OnGameInitializationFinished(Game game)` is a virtual no-op
  (`TaleWorlds.MountAndBlade.MBSubModuleBase.cs:84`). `MBGameManager.OnGameInitializationFinished(Game game)`
  (`TaleWorlds.MountAndBlade.MBGameManager.cs:110-115`) loops
  `Module.CurrentModule.CollectSubModules()` and calls it on each. It is reached from
  `Campaign.OnInitialize` (`TaleWorlds.CampaignSystem.Campaign.cs:1471`,
  `base.GameManager.OnGameInitializationFinished(base.CurrentGame);`, after `CurrentGame.OnGameStart()`)
  and from `TaleWorlds.MountAndBlade.CustomBattle.CustomGame.cs:56`. So pass 2 runs at every game
  start (campaign or custom battle), synchronously inside the loading screen, never at the main menu.
- The Harmony finalizer signature PatchShield uses is unchanged by this plan:
  `private static Exception? ShieldFinalizerVoid(MethodBase __originalMethod, Exception __exception)`.

### Runtime evidence (from the game's logs; read during planning)

- `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM.Dependencies\diag.log`:
  line 35488 `13:50:31.642 ... → PatchShield.Install (pass 2)`, line 35489
  `13:51:40.999 [PatchShield] shield pass: +372 new, 46 already-shielded, 19 skipped (total: 437)`
  (69.4 s); a second game start in the same process, lines 35500-35501, `+141 new` in 26.5 s.
  `grep -c "\[PatchShield\] swallowed" diag.log` is 0 across 466 `session start` lines.
- `...\bin\Win64_Shipping_Client\Logs\taom_debug_2026-09-23_13-43-40.log` line 5:
  `[CrashReport] Native2Managed: attached 247 Finalizer(s)`.
- The attribution "about 247 of pass 2's 372 attaches are the shims" is by code path plus count
  identity; no log lists pass 2's methods. That is why the test plan asks for an in-game check.

### Conventions that bind this change

- **ADR-002 (thin entry points under 150 lines):** `Dependencies/SubModule.cs` is already 324 lines;
  do not add logic to it. This plan changes only its doc comment and one log string.
- **ADR-007 (adapters):** not triggered; no service touches a TaleWorlds type. PatchShield stays the
  Harmony-bound plumbing, PatchShieldPolicy stays pure (string in, bool or string out).
- **ADR-008 (testability):** decisions live in pure, directly tested functions. That is exactly the
  existing PatchShieldPolicy pattern (its doc: "PatchShield keeps the plumbing; this keeps the
  policy"). Model new tests on `PatchShieldPolicyTests`.
- **ADR-003 / ADR-004 / ADR-005:** no `#region`, no `[Obsolete]`, no `#if DEBUG`.
- **Test naming (`.claude/rules/tests.md`):** `MethodName_StateUnderTest_ExpectedBehavior`, MSTest
  `[TestMethod]`, Arrange/Act/Assert.
- **Human prose (AGENTS.md):** comments, docs and commit bodies use no em dash (U+2014) or en dash
  (U+2013); use commas, colons, semicolons or parentheses. Every line this plan adds, moves or
  rewrites must be dash-free, and the plan gives dash-free text for each one (including the one
  moved comment line and the one rewritten doc line that carry a dash today). Lines you do not touch
  keep their dashes. The "Dash scan" command below proves it.

## Commands you will need

Run every command from the worktree root (see "Git workflow"), in Git Bash.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Filtered test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchShieldPolicyTests"` | the pass count each step names, 0 failed |
| Full test | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | see the baseline note below |
| Data | `python tools/validate_moduledata.py` | 0 errors (no data changes here; run once as a sanity check) |
| Docs | `python tools/lint_docs.py` | the summary lines `- Dead links: **0**` and `- Em/en dashes in newly written prose: **0**` |
| Dash scan | `git diff <base> -- Dependencies TAOM.Tests docs \| python -c "import sys; t=sys.stdin.buffer.read().decode('utf-8','replace'); bad=[l for l in t.splitlines() if l.startswith('+') and not l.startswith('+++') and any(c in l for c in '\u2013\u2014')]; [print(ascii(l)) for l in bad]; sys.exit(1 if bad else 0)"` | no output, exit 0 |

(In the table the pipe is escaped as `\|`; type a plain `|`.) Never run `./build.ps1` (it deploys
into the game install). Both `-p:DisableModuleCopy=true` and `-p:ModuleId=` are required: without
them the build copies into the game while the maintainer may be playing. Build outputs under
`Dependencies/_Module/bin/` are gitignored, so they do not show in `git status`.

**Baseline at `b2e387db`:** 10,239 tests; 10,235 pass, 2 fail, 2 skipped (deliberate `[Ignore]`s in
`WargAttackServiceTests`). The 2 failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`. Both read the live, unversioned
LOTRLOME_Armory install, which another session is editing, so they fail regardless of this plan.
They are not yours to fix or investigate, and the set may drift while you work: Step 1 records the
set you actually see, and later steps compare against that.

## Scope

**In scope** (the only files you should modify):

- `Dependencies/Foundation/PatchShieldPolicy.cs`: add the exclusion list, `IsExcludedTargetNamespace`,
  and `FormatShieldPassSummary`; reword the class summary.
- `Dependencies/Foundation/PatchShield.cs`: delete the private list, delegate `IsExcludedTarget` to the
  policy, time the pass and log through the formatter.
- `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`: 7 new tests, 2 new `using`s.
- `Dependencies/SubModule.cs`: lines 267-272 (doc comment) and 276 (label string) ONLY.
- `docs/migration/dr3-maintenance.md`: lines 255, 269, 270, 286 and 291, plus one new line after 260
  (exact text in Step 9).
- `docs/migration/v1.5.2-impact.md`: line 141 only.

**Out of scope** (do NOT touch, even though they look related):

- `plans/README.md`: the orchestrator maintains it.
- `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`, `Directory.Build.props`: single-owner files.
  Nothing here needs them. If you believe a change there is needed, STOP and report the exact line.
- `Main/Features/CrashReport/**` (including `Native2ManagedPatcher.cs` and `CrashReportSettings.cs`):
  plan 006 owns the Native2Managed sweep and its MCM defaults.
- `CHANGELOG.md`: another session has uncommitted edits to it in the main tree. The orchestrator
  writes the entry at merge; a suggested entry is in "Maintenance notes".
- `Dependencies/Foundation/IncompatibleModDetector.cs`, including its doc comments at lines 11 and
  87 that also say "main-menu reach", and the crash-loop marker behaviour itself: the marker is only
  deleted at a game start, so quitting from the main menu looks like a failed launch. This plan
  documents that (Steps 8 and 9) but does not change it (deferred follow-up 3).
- Adding `com.taom.mod` to `CompiledProtectedOwnerPrefixes`: an UNVERIFIED side lead that needs its
  own investigation.
- Skipping targets whose every patch owner is protected: a shield-posture change the maintainer must
  decide.
- Deferring pass 2 off the loading path or batching patches: rejected (a background-thread
  `Harmony.Patch` rewrites code the main thread may be running; Harmony 2.4.2 has no batch API).
- The machine's slow per-`Patch` mode: an environment item for the maintainer, not a repo defect.
- Every path another session has modified in the main tree's `git status` (creature, Elk,
  ElephantLike, Animalia, CareerSystem files, `CHANGELOG.md`, and so on).

## Git workflow

- Work in a git worktree so the main tree's uncommitted work (another session) is never touched. If
  you were dispatched into a worktree, use it. Otherwise:
  `git worktree add E:/repos/wt-plan-007 -b improve/007-patchshield-skip-callback-shims HEAD`
  and run every command from `E:/repos/wt-plan-007`.
- Commit subject format: `<type>(<scope>): <version> - <description>`, at most 72 characters.
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` copied exactly, and that
  value already starts with `v` (`v2.0.30` at planning; do not add a second `v`). Re-read it before
  committing: `grep -m1 "<Version" Main/_Module/SubModule.xml`. Body wrapped at 72. No AI attribution
  trailer (no `Co-Authored-By`). No em or en dash in the body.
- The two commits (subjects are 69 and 68 characters with `v2.0.30`):
  1. `perf(patchshield): v2.0.30 - skip the ManagedCallbacks callback shims` (policy, PatchShield,
     tests). Trailer: `Not-tested: live pass-2 attach count and timing (needs a game start)`.
  2. `docs(patchshield): v2.0.30 - pass 2 runs at game start, not the menu` (SubModule doc and label,
     the two migration docs).
- Stage explicit paths only (each step names them). Never `git add -A`, `git add .` or `git commit -a`.
- Never push, never open a PR, never merge.

## Steps

### Step 1: Confirm the baseline

Run the drift check from the header. Record the commit you branched from (`git rev-parse HEAD`
before your first commit); the rest of the plan calls it `<branch start>`. Run the filtered test,
then the full test, and write down the full test's `Total`, and the name of every test on a line
starting with `Failed ` (the "Step 1 failure set").

**Verify**:
- Drift check prints nothing, and the `Main/SubModule.cs` grep prints exactly two lines.
- Filtered test → `Passed: 16`, `Failed: 0`.
- Full test → the Step 1 failure set is the two tests named in the baseline note (or a subset of
  them, if the other session has fixed the Armory). If any OTHER test fails, record its name and
  continue, unless it is in `PatchShieldPolicyTests` or under `TAOM.Tests/Infrastructure/`: then STOP.
- `pwsh tools/taom-src.ps1 path ManagedCallbacks.CoreCallbacksGenerated` prints a path, and
  `grep -n "^namespace" <that path>` prints `namespace ManagedCallbacks;`.

### Step 2: Write the tests that pin the existing exclusion behaviour (RED: does not compile)

Append to `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`, inside the class, a new
section headed by the comment `// IsExcludedTargetNamespace: the hot-layer exclusion list`:

```csharp
    [TestMethod]
    public void IsExcludedTargetNamespace_GauntletAndTwoDimensionLayers_ReturnsTrue()
    {
        foreach (var ns in new[]
                 {
                     "TaleWorlds.GauntletUI",
                     "TaleWorlds.GauntletUI.PrefabSystem",
                     "TaleWorlds.TwoDimension",
                     "TaleWorlds.MountAndBlade.GauntletUI.Widgets",
                 })
        {
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetNamespace(ns), $"'{ns}' must be excluded (#331)");
        }
    }

    [TestMethod]
    public void IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse()
    {
        // The list is not co-op-scoped: widening it to gameplay code would drop the shield in solo play.
        foreach (var ns in new[] { "TaleWorlds.CampaignSystem", "TaleWorlds.MountAndBlade", "SandBox", "TaleWorlds.Core" })
        {
            Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(ns), $"'{ns}' must stay shielded");
        }
    }

    [TestMethod]
    public void IsExcludedTargetNamespace_NullOrEmpty_ReturnsFalse()
    {
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(null));
        Assert.IsFalse(PatchShieldPolicy.IsExcludedTargetNamespace(string.Empty));
    }
```

**Verify**: build command → exit 0 (Main does not contain the tests). Filtered test → the build
fails with `CS0117` (`'PatchShieldPolicy' does not contain a definition for 'IsExcludedTargetNamespace'`).
That is the RED.

### Step 3: Move the list into PatchShieldPolicy, unchanged (GREEN, pure refactor)

In `Dependencies/Foundation/PatchShieldPolicy.cs`, add inside the class, directly after the `};` that
closes `CompiledProtectedOwnerPrefixes` (line 63):

- `public static readonly IReadOnlyList<string> ExcludedTargetNamespacePrefixes = new[] { ... };`
  containing exactly the three current string entries, in the same order. Move the #331 comment
  block (`PatchShield.cs:50-59`) above it and the Patch38 comment (`PatchShield.cs:64-67`) inside it,
  both verbatim with ONE exception: `PatchShield.cs:57` contains an em dash, so the moved line reads
  `// frozen exit (+8,276 gen0 GCs, invariant across sessions; stack-sampled proof in`
  (indented like its neighbours). Keep the `µ` in `~50µs`; it is not a dash.
- ```csharp
  /// <summary>Whether a patch target's declaring namespace is on the hot-layer exclusion list (ordinal prefix match).</summary>
  public static bool IsExcludedTargetNamespace(string? targetNamespace)
  {
      if (string.IsNullOrEmpty(targetNamespace)) return false;
      foreach (var prefix in ExcludedTargetNamespacePrefixes)
      {
          if (targetNamespace!.StartsWith(prefix, StringComparison.Ordinal)) return true;
      }
      return false;
  }
  ```
- Replace the class `<summary>` (lines 7-10) with:
  ```csharp
  /// <summary>
  /// The pure decisions behind <see cref="PatchShield"/> (which targets to skip, which owners never
  /// to unpatch, when to install), extracted so they can be tested without Harmony or a running
  /// game. PatchShield keeps the plumbing; this keeps the policy.
  /// </summary>
  ```

In `Dependencies/Foundation/PatchShield.cs`:

- Delete the private `ExcludedTargetNamespacePrefixes` field and its comment (lines 50-69). Leave this
  one-line pointer comment in its place:
  `// The hot-layer target exclusion list lives in PatchShieldPolicy.ExcludedTargetNamespacePrefixes (#331).`
- Replace the whole body of `IsExcludedTarget` (the lines between its braces) with:
  ```csharp
      try { return PatchShieldPolicy.IsExcludedTargetNamespace(method.DeclaringType?.Namespace); }
      catch { return false; /* fail open: an unreadable type just gets shielded as before */ }
  ```
- In the comment at line 196, change `See ExcludedTargetNamespacePrefixes.` to
  `See PatchShieldPolicy.ExcludedTargetNamespacePrefixes.` Leave lines 194-195 as they are.

**Verify**: build command → exit 0. Filtered test → `Passed: 19`, `Failed: 0`.
`git grep -n "ExcludedTargetNamespacePrefixes" -- Dependencies` → hits in `PatchShieldPolicy.cs`
(the field, the loop, and the existing doc line near "Extending") and exactly two in `PatchShield.cs`
(the pointer comment and the line-196 comment).

### Step 4: Failing test for the ManagedCallbacks exclusion (RED)

Add to `PatchShieldPolicyTests`:

```csharp
    [TestMethod]
    public void IsExcludedTargetNamespace_ManagedCallbacksShims_ReturnsTrue()
    {
        // Native2ManagedPatcher (Main/Features/CrashReport/Hooks) puts a swallow-everything finalizer
        // on every static method of ManagedCallbacks.{Library,Core,Engine}CallbacksGenerated (247 in
        // v1.5.3). Re-shielding them cost about 46 s of the first game start's loading screen and
        // adds nothing: PatchShield's rescue only strips prefixes, postfixes and transpilers, and
        // those shims carry only finalizers.
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetNamespace("ManagedCallbacks"));
    }
```

**Verify**: filtered test → exactly 1 failure,
`IsExcludedTargetNamespace_ManagedCallbacksShims_ReturnsTrue` (`Assert.IsTrue failed`), `Passed: 19`.

### Step 5: Add the ManagedCallbacks entry (GREEN)

Append `"ManagedCallbacks",` as the fourth entry of `PatchShieldPolicy.ExcludedTargetNamespacePrefixes`,
preceded by this comment:

```csharp
        // Plan 007 (2026-09-23, measured from diag.log): the engine's native-to-managed callback
        // shims, ManagedCallbacks.{Library,Core,Engine}CallbacksGenerated. TAOM's own
        // Native2ManagedPatcher already wraps every one (247 in v1.5.3) with a finalizer that
        // swallows every exception while crash capture is on (CrashReportPatchHelper.HandleAndSwallow).
        // Shielding them again cost one Harmony.Patch each at the first game start (about 46 s of a
        // 69 s pass 2 on a machine paying 186 ms per Patch) and stacked an __originalMethod wrapper
        // on engine callback hot paths: the #331 hot-layer rationale. Rescue value is nil: those
        // shims carry only finalizers, and the rescue strips prefixes, postfixes and transpilers.
        "ManagedCallbacks",
```

**Verify**: filtered test → `Passed: 20`, `Failed: 0`.

### Step 6: Time the pass and log it through a tested formatter (RED then GREEN)

6a. RED. Add `using System.Globalization;` and `using System.Threading;` to the test file's `using`
block, then add three tests to `PatchShieldPolicyTests`:

```csharp
    [TestMethod]
    public void FormatShieldPassSummary_WithAttaches_AppendsElapsedAndPerAttach()
    {
        var line = PatchShieldPolicy.FormatShieldPassSummary(added: 372, alreadyShielded: 46, skipped: 19, total: 437, elapsedMs: 69300);

        // The existing prefix stays byte-identical: docs/migration/dr3-maintenance.md and triagers grep it.
        StringAssert.StartsWith(line, "shield pass: +372 new, 46 already-shielded, 19 skipped (total: 437)");
        StringAssert.Contains(line, "in 69300 ms");
        StringAssert.Contains(line, "186.3 ms/attach");
    }

    [TestMethod]
    public void FormatShieldPassSummary_NoAttaches_DoesNotDivideByZero()
    {
        var line = PatchShieldPolicy.FormatShieldPassSummary(added: 0, alreadyShielded: 437, skipped: 0, total: 437, elapsedMs: 3);

        StringAssert.Contains(line, "in 3 ms");
        Assert.IsFalse(line.Contains("ms/attach"), line);
        Assert.IsFalse(line.Contains("NaN") || line.Contains("Infinity") || line.Contains("∞"), line);
    }

    [TestMethod]
    public void FormatShieldPassSummary_CommaDecimalCulture_UsesInvariantDecimalPoint()
    {
        var saved = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            var line = PatchShieldPolicy.FormatShieldPassSummary(added: 372, alreadyShielded: 46, skipped: 19, total: 437, elapsedMs: 69300);
            StringAssert.Contains(line, "186.3 ms/attach");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = saved;
        }
    }
```

**Verify**: filtered test → the build fails with `CS0117` for `FormatShieldPassSummary`.

6b. GREEN. Add `using System.Globalization;` to `PatchShieldPolicy.cs` and this method to the class:

```csharp
    /// <summary>
    /// The diag.log line for one shield pass. The prefix up to "(total: N)" is unchanged; the timing
    /// suffix exists because diag.log ships in every crash bundle, and the per-attach cost of
    /// Harmony.Patch varies about 30x between machines (5 ms to 186 ms observed), which decides
    /// whether pass 2 costs 2 s or 70 s of a player's first loading screen.
    /// </summary>
    public static string FormatShieldPassSummary(int added, int alreadyShielded, int skipped, int total, long elapsedMs)
    {
        var line = $"shield pass: +{added} new, {alreadyShielded} already-shielded, {skipped} skipped (total: {total}) in {elapsedMs} ms";
        return added > 0
            ? line + " (" + ((double)elapsedMs / added).ToString("F1", CultureInfo.InvariantCulture) + " ms/attach)"
            : line + " (no new attaches)";
    }
```

Then in `PatchShield.Install()`:

- Add `using System.Diagnostics;` to the file's `using` block.
- Insert `var stopwatch = Stopwatch.StartNew();` on its own line immediately before
  `List<MethodBase> patched;` (line 162 at `b2e387db`), so the timing covers enumeration plus the
  attach loop.
- Replace the log call at line 237 with
  `DiagLog.Log(Tag, PatchShieldPolicy.FormatShieldPassSummary(added, alreadyShielded, skipped, _shielded.Count, stopwatch.ElapsedMilliseconds));`
  keeping the surrounding `if (added > 0 || alreadyShielded == 0)` condition unchanged. `_shielded.Count`
  is read outside `lock (_lock)` today; keep that exactly as it is (behaviour unchanged, not in scope).

**Verify**: build → exit 0; filtered test → `Passed: 23`, `Failed: 0`.
`git grep -n "shield pass:" -- Dependencies` → one hit, in `PatchShieldPolicy.cs`.

### Step 7: Full suite, dash scan, then commit 1

**Verify**:
- Full test → `Total` is the Step 1 total plus 7 (10,246 if Step 1 saw 10,239); every failed test is
  in the Step 1 failure set; `Skipped: 2`.
- Dash scan with `<base>` = `HEAD` → no output, exit 0.

Then `git add Dependencies/Foundation/PatchShieldPolicy.cs Dependencies/Foundation/PatchShield.cs TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`
and make commit 1 (Git workflow section).
`git show --stat --format= HEAD` → exactly those three files.

### Step 8: Correct the lifecycle doc and label in `Dependencies/SubModule.cs`

Replace the doc comment at lines 267-272 (six lines, `/// <summary>` through `/// </summary>`) with:

```csharp
    /// <summary>
    /// Runs at the end of every game initialisation, NOT at the main menu: the engine's
    /// MBGameManager.OnGameInitializationFinished fans out to every submodule from
    /// Campaign.OnInitialize (new or loaded campaign) and from CustomGame (custom battle), inside
    /// that game's loading screen. Marks the launch successful (deletes the crash-loop marker and
    /// snapshots last-good-modlist.txt; a process that quits from the main menu without starting a
    /// game therefore leaves the marker behind, a known gap not changed here), then runs PatchShield
    /// pass 2, which the player waits through on that loading screen.
    /// </summary>
```

Replace the label string at line 276, `"OnGameInitializationFinished: entered (main menu reached)"`, with
`"OnGameInitializationFinished: entered (game start: campaign or custom battle)"`.
Change nothing else in the file.

**Verify**: build → exit 0. `git grep -n "main menu reached" -- Dependencies` → no output.
`git diff --stat -- Dependencies/SubModule.cs` → one file, 10 insertions, 7 deletions.

### Step 9: Correct the two migration docs, then commit 2

Edit `docs/migration/dr3-maintenance.md`. Line numbers are at `b2e387db`; do the edits bottom-up
(291 first) so the numbers stay valid, and change nothing else on each line.

- Line 291 (inside a code block): replace the whole line with
  `[INFO  ] [PatchShield]                shield pass: +N new, 0 already-shielded, M skipped (total: N) in T ms (X.X ms/attach)`
- Line 286: replace `After a normal launch (made it to main menu),` with
  `After a normal launch and one game start (campaign or custom battle),`.
- Line 270: replace `Snapshot of enabled modules at last main-menu reach.` with
  `Snapshot of enabled modules at the last game start (written with the marker delete).`
- Line 269: replace `deleted on \`OnGameInitializationFinished\` (main menu reached). Survival to next launch = previous session crashed pre-menu.`
  with `deleted on \`OnGameInitializationFinished\`, which fires at the first game start (campaign or custom battle), not at the main menu. Survival to next launch means the previous session crashed before a game started, or quit from the main menu without starting one.`
- After line 260 (the long `` - `PatchShield` `` bullet, which ends with
  `RCA: [rca-shield-rethrow-stack-2026-09-22.md](../reviews/rca-shield-rethrow-stack-2026-09-22.md).`),
  insert ONE new line, indented two spaces so it stays inside that bullet. Do NOT edit line 260
  itself: it carries em dashes that must stay untouched.
  ```
    **Callback shims excluded (2026-09-23, plan 007):** the exclusion list also carries `ManagedCallbacks`, the engine's native-to-managed callback shims, which TAOM's Native2Managed crash capture already wraps with a swallow-everything finalizer; re-shielding them cost one `Harmony.Patch` per shim (247) at the first game start. The list now lives in `PatchShieldPolicy.ExcludedTargetNamespacePrefixes`.
  ```
- Line 255: replace the whole line (it starts `` - `IncompatibleModDetector` ``) with this text,
  which also drops the line's one em dash:
  ```
  - `IncompatibleModDetector`: writes `session-launching.marker` at startup, deletes it at the first game start (campaign or custom battle), not at the main menu. If the marker survives to the next launch, the previous session crashed before a game started or quit from the main menu without starting one; diffs modlist against `last-good-modlist.txt` to identify newly-added likely-culprit mods. **Detection only**, no XML mutation of LauncherData.xml.
  ```

`docs/migration/v1.5.2-impact.md` line 141: replace the parenthetical
`(PatchShield pass 2 alone took 70 s)` with
`(PatchShield pass 2, 70 s, ran later, inside the new campaign's loading screen: it fires at game start, not at the main menu)`.
Do not change the timestamp or the row label; they came from a log this plan cannot re-check.

**Verify**:
- `python tools/lint_docs.py` → prints `- Dead links: **0**` and `- Em/en dashes in newly written prose: **0**`.
- Dash scan with `<base>` = `<branch start>` → no output, exit 0 (this covers commit 1 and the
  uncommitted doc edits together).
- `git grep -n -e "main menu reached" -e "main-menu reach" -e "made it to main menu" -- docs/migration/dr3-maintenance.md` → no output.
- Build → exit 0.

Then `git add Dependencies/SubModule.cs docs/migration/dr3-maintenance.md docs/migration/v1.5.2-impact.md`
and make commit 2. `git show --stat --format= HEAD` → exactly those three files.

### Step 10: Done check

Run every command in "Done criteria" and report the output to whoever dispatched you. Do not edit
`plans/README.md`.

## Test plan

- **New tests (7), all in `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`:**
  - `IsExcludedTargetNamespace_GauntletAndTwoDimensionLayers_ReturnsTrue` (the three #331 prefixes
    and a child namespace of each family).
  - `IsExcludedTargetNamespace_GameplayNamespaces_ReturnsFalse` (guards against over-broad
    exclusion; `TaleWorlds.CampaignSystem`, `TaleWorlds.MountAndBlade`, `SandBox`, `TaleWorlds.Core`).
  - `IsExcludedTargetNamespace_NullOrEmpty_ReturnsFalse`.
  - `IsExcludedTargetNamespace_ManagedCallbacksShims_ReturnsTrue` (the regression this plan fixes).
  - `FormatShieldPassSummary_WithAttaches_AppendsElapsedAndPerAttach` (prefix byte-identical).
  - `FormatShieldPassSummary_NoAttaches_DoesNotDivideByZero`.
  - `FormatShieldPassSummary_CommaDecimalCulture_UsesInvariantDecimalPoint`.
- **Pattern:** the existing tests in the same class (pure static calls, no mocks).
- **Structurally untestable (commit `Not-tested:` trailer):** the live pass-2 attach count and
  timing, and that Harmony's `GetAllPatchedMethods()` returns the shims with
  `DeclaringType.Namespace == "ManagedCallbacks"` at runtime. PatchShield is static and Harmony-bound
  and cannot run in the test host (stated in the test class's own doc comment).
- **In-game check owed (for the maintainer, not the executor):** after a deploy, start one custom
  battle, then read the last pass-2 line in `Modules/TAOM.Dependencies/diag.log`. Expected: about
  `+125 new` instead of `+372 new`, skipped up by about 247, and the new
  `in T ms (X.X ms/attach)` suffix. Pass 2's wall time should drop by roughly 247 times the logged
  ms/attach.

## Done criteria

ALL must hold (run from the worktree root):

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~PatchShieldPolicyTests"` → `Passed: 23`, `Failed: 0`.
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` → `Total` is the Step 1 total plus 7, and every failed test is in the Step 1 failure set.
- [ ] `git grep -n '"ManagedCallbacks"' -- Dependencies/Foundation/PatchShieldPolicy.cs` → exactly one hit.
- [ ] `git grep -n "private static readonly string\[\] ExcludedTargetNamespacePrefixes" -- Dependencies` → no output.
- [ ] `git grep -n -e "main menu reached" -e "main-menu reach" -- Dependencies/SubModule.cs docs/migration/dr3-maintenance.md` → no output.
- [ ] `git grep -n "shield pass:" -- Dependencies` → one hit, in `PatchShieldPolicy.cs`.
- [ ] Dash scan with `<base>` = `<branch start>` → no output, exit 0.
- [ ] `git diff --name-only <branch start>..HEAD` lists exactly these six files:
      `Dependencies/Foundation/PatchShield.cs`, `Dependencies/Foundation/PatchShieldPolicy.cs`,
      `Dependencies/SubModule.cs`, `TAOM.Tests/Infrastructure/Dependencies/PatchShieldPolicyTests.cs`,
      `docs/migration/dr3-maintenance.md`, `docs/migration/v1.5.2-impact.md`.
- [ ] `git status --short` in the worktree prints nothing.
- [ ] `git log --format=%s <branch start>..HEAD | awk 'length > 72'` → no output, and
      `git log --format=%B <branch start>..HEAD | grep -c "Co-Authored-By"` → `0`.

## STOP conditions

Stop and report (do not improvise) if:

- The drift check prints anything and the excerpts in "Current state" no longer match the files, or
  the `Main/SubModule.cs` grep does not print exactly two lines.
- `pwsh tools/taom-src.ps1 path ManagedCallbacks.CoreCallbacksGenerated` fails, or any of the three
  shim types is not in namespace `ManagedCallbacks` (an engine bump moved them; the exclusion would
  then miss).
- `Native2ManagedPatcher.cs` no longer patches `*CallbacksGenerated` types, or
  `CrashReportPatchHelper.HandleAndSwallow` no longer returns `null` on its normal path (capture on,
  service resolved). The premise "PatchShield adds nothing on those shims" would then be false.
- `git grep -n "ExcludedTargetNamespacePrefixes" -- '*.cs'` shows a hit in any file other than
  `Dependencies/Foundation/PatchShield.cs` and `Dependencies/Foundation/PatchShieldPolicy.cs` (some
  code would be reading the private field by name or reflection). Hits in `.md` files are expected
  and do not count.
- Plan 006 or anything else has already changed `Dependencies/Foundation/PatchShield.cs` or
  `PatchShieldPolicy.cs` since `b2e387db` (`git log --oneline b2e387db..HEAD -- Dependencies/Foundation`
  prints anything): reconcile with the orchestrator first.
- Step 1 shows a failing test in `PatchShieldPolicyTests` or under `TAOM.Tests/Infrastructure/`, a
  later full run fails a test outside the Step 1 failure set, or the existing 16
  `PatchShieldPolicyTests` stop passing.
- Any change seems to require `Main/SubModule.cs`, `Main/IoC.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`, `plans/README.md` or anything under `Main/Features/CrashReport/`.
- You are tempted to widen the exclusion list beyond `ManagedCallbacks`, to add `com.taom.mod` to the
  protected owners, or to skip "all owners protected" targets: those are decisions for the maintainer.
- A build or test step fails twice after a reasonable fix attempt.

## Maintenance notes

- **Coverage given up:** a THIRD-PARTY mod's prefix, postfix or transpiler on a `ManagedCallbacks`
  shim no longer gets PatchShield's missing-API swallow and rescue. With TAOM's default MCM values the
  Native2Managed finalizer still swallows those exceptions on its normal path. The uncovered corners
  are `EnableCrashCapture` off (then `HandleAndSwallow` returns the exception), the crash service
  failing to resolve or to handle, and `EnableNativeToManagedCapture` off (then the shims are only
  patched if another mod patches them). Judged acceptable: 0 PatchShield swallows in 466 logged
  sessions, and #331 already excludes whole hot layers for the same per-call-wrapper reason.
- **Interaction with plan 006 (if it lands):** 006 narrows or defaults off the Native2Managed sweep
  from the Main side. With the sweep off, the shims are not patched and pass 2 never sees them. This
  plan still matters for installs whose persisted MCM json keeps the sweep on (the "Persisted MCM
  defaults" trap: json2 keeps an old value) and for players who opt back in. The two plans touch
  disjoint files.
- **Reviewer focus (`/deep-review`):** that the moved list is byte-identical for the three old
  entries (only the one comment dash changed); that `IsExcludedTarget` still fails open; that the
  stopwatch start covers enumeration and the loop; that the `shield pass:` prefix is unchanged; the
  culture handling in the formatter.
- **Suggested CHANGELOG entry (orchestrator writes it):** "PatchShield no longer re-shields the
  engine's 247 native-to-managed callback shims, which TAOM's crash capture already wraps. On a
  machine paying about 186 ms per Harmony patch this takes about 46 s off the first game start's
  loading screen. The `shield pass` line in diag.log now reports elapsed time and ms per attach."
- **Deferred follow-ups (file as separate issues):**
  1. `com.taom.mod` matches no `CompiledProtectedOwnerPrefixes` entry
     (`PatchShieldPolicy.cs:23-63`, `IsProtectedOwner` at 84-95), so after a missing-API exception
     on a TaleWorlds method TAOM patches, `TryUnpatchOffendingPatches` (declared at
     `PatchShield.cs:323`; its owner loop is 378-401) could strip TAOM's own prefixes, postfixes and
     transpilers from that method. UNVERIFIED; needs its own investigation. It is also a
     prerequisite for follow-up 2.
  2. Skip targets whose every owner is protected (removes most of the remaining ~125 pass-2 attaches
     and the `+141` second-game-start pass). A shield-posture change for the maintainer.
  3. The crash-loop marker is deleted only at a game start, so a menu-only session reads as a failed
     launch (the audit counted 40 `previousCrashLoop=True` against 453 early-phase entries in
     diag.log; not re-verified by this plan, not traced further). `IncompatibleModDetector.cs:11`
     and `:87` still describe it as a main-menu hook; fix that wording with the behaviour.
  4. The machine's slow per-`Patch` mode (about 186 ms since 2026-06-12, 5 to 10 ms before): an
     environment item for the maintainer; cause unknown (Defender behaviour monitoring not ruled out).
