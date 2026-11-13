# Plan 049: RefugeService's save-load repair lives in one unit with lazy clock seams

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat 7087fa633..HEAD -- Main/Features/Refuge/RefugeService.cs Main/Features/Refuge/Domain TAOM.Tests/Features/Refuge`.
> If an in-scope file changed since this plan was written, compare the "Current state" excerpts with
> the live code; a mismatch is a STOP condition.

## Status

- **Priority**: P3
- **Effort**: S
- **Risk**: LOW (with the lazy clock delegates this plan prescribes; MED without them)
- **Depends on**: none
- **Category**: tech-debt
- **Planned at**: commit `7087fa633`, 2026-11-12
- **Baseline at that commit**: dotnet `Passed! - Failed: 0, Passed: 39250, Skipped: 9, Total: 39259`
  (game-backed desktop run), failing: none. Build: 11 warnings, 0 errors. Python suite (not touched
  by this plan): `Ran 8578 tests, FAILED (failures=7, errors=2, skipped=13)`, nine heraldry and
  kingdom-generator data checks that fail on trunk.
- **Issue**: filed by the orchestrator before execution

## Why this matters

`Main/Features/Refuge/RefugeService.cs` is 1,448 lines. The repair it runs on every save load (drop
null rows, then canonicalize and validate each row) is the most self-contained part of the logic half,
and the part most likely to grow: every new `RefugeData` field gets a repair line. Moving it, line for
line, into one internal static unit beside `RefugeData` lets a reader see the whole load repair in one
small file and leaves the service with founding, raids, the frame tick and party operations.

The move is behaviour-preserving only if the campaign clock stays lazy. Today the repair reads the
clock only for a building row (`DaysSince`) and only for a future start (`NowTime`). Two existing tests
load idle rows through a test subclass that overrides neither clock seam; an extraction that read
`NowTime()` once up front would reach `CampaignTime.Now` with no campaign and fail them, and in game
would add a clock read to every load. So the unit takes the clock as two delegates bound to the
service's own virtual seams and calls each in exactly the place the code calls it today.

## Current state

### Files

- `Main/Features/Refuge/RefugeService.cs`: the refuge book and lifecycle service (namespace
  `TAOM.Features.Refuge`, class `public class RefugeService : IRefugeService, IRefugeBook`, line 109).
  Holds `LoadFrom` (643 to 666), `RepairLoadedRow` (748 to 790, summary comment included), the
  `MinBuildTargetHours` constant (113 to 114) and its second user `SaneBuildHours` (873 to 881).
  It already has `using TAOM.Features.Refuge.Domain;` (line 11).
- `Main/Features/Refuge/Domain/`: holds `RefugeData.cs` (the persisted row, namespace
  `TAOM.Features.Refuge.Domain`) and `RefugeSaveDefiner.cs`. The new unit goes here, as
  `Main/Features/Refuge/Domain/RefugeBookRepair.cs`, in the same namespace. Domain folders across the
  repo hold static rule classes: for example `Main/Features/FordsOfWar/Domain/TimeVictory.cs:8`
  `internal static class TimeVictory` and `Main/Features/Enlistment/Domain/EnlistmentTransitionTable.cs:15`.
- `Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs:88`: `_refuges.LoadFrom(refuges, counter);`,
  the only production caller of `LoadFrom`. Not touched.
- `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs`: the service's tests. Class-level
  `[TestCategory("RequiresGame")]` (line 27). Not edited by this plan.
- `TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs`: new, the unit's direct tests.
- The project files are SDK style with implicit compile items (`Main/TAOM.csproj` has no
  `<Compile Include>`), so a new `.cs` file needs no csproj edit. `Main/TAOM.csproj:114-116` grants
  `InternalsVisibleTo` to `TAOM.Tests`, so an `internal static` class is reachable from the tests.
- Nullable is enabled in `Directory.Build.props:6`, and `.editorconfig:40-44` sets CS8600 to CS8604 to
  `none`; `List<string> nullKeys = null;` compiles without a warning today and will in the new file.

### `LoadFrom` and `SaveInto` at 7087fa633 (`RefugeService.cs:643-672`)

```csharp
    public void LoadFrom(Dictionary<string, RefugeData> refuges, int counter)
    {
        _refuges = refuges ?? new Dictionary<string, RefugeData>();
        // A partially recovered or externally modified save can carry null rows; every tick path
        // dereferences the value, so scrub here rather than crash on the first FrameTick.
        List<string> nullKeys = null;
        foreach (var pair in _refuges)
        {
            if (pair.Value == null)
                (nullKeys ?? (nullKeys = new List<string>())).Add(pair.Key);
        }
        if (nullKeys != null)
        {
            foreach (var key in nullKeys)
                _refuges.Remove(key);
            _logger.LogWarning($"[Refuge] dropped {nullKeys.Count} null book row(s) from the loaded save.");
        }
        foreach (var pair in _refuges)
            RepairLoadedRow(pair.Key, pair.Value);
        _counter = counter < 0 ? 0 : counter;
        _visualShown.Clear();
        _holdNoteShown.Clear();
        _nextFrameWorkHours = double.MinValue;
    }

    public void SaveInto(out Dictionary<string, RefugeData> refuges, out int counter)
    {
        refuges = _refuges;
        counter = _counter;
    }
```

`SaveInto` hands `_refuges` back by reference, so the repair must mutate the same dictionary instance
and the rows in place. Tests rely on it: `Seed` at `RefugeServiceTests.cs:363-364` says "LoadFrom keeps
the reference, so repeated seeding into _book stays visible."

### `RepairLoadedRow` at 7087fa633 (`RefugeService.cs:746-791`)

```csharp
    // --- internals ---

    /// <summary>Canonicalizes and validates one persisted row (hostile or damaged saves). The
    /// DICTIONARY KEY is the identity every lookup resolves and every distance check measures, so
    /// a divergent inner PartyId would make Dismantle/visuals act on a DIFFERENT party than the
    /// one the player stands at -- cross-party destruction. Numeric repair kills the absorbing
    /// states: a non-finite BuildTargetHours on a Building row could otherwise never reach
    /// BuildProgress >= 1 (NaN fails every comparison; Infinity divides to 0) and the row eats a
    /// cap slot forever.</summary>
    private void RepairLoadedRow(string key, RefugeData data)
    {
        if (!string.Equals(data.PartyId, key, StringComparison.Ordinal))
        {
            _logger.LogWarning(
                $"[Refuge] loaded row keyed '{key}' carried inner PartyId '{data.PartyId}'; canonicalized to the key.");
            data.PartyId = key;
        }
        if (data.Tier != (int)RefugeTier.Refuge && data.Tier != (int)RefugeTier.Stronghold)
        {
            _logger.LogWarning($"[Refuge] row '{key}' had unknown tier {data.Tier}; reset to Refuge.");
            data.TierEnum = RefugeTier.Refuge;
        }
        // Only NON-FINITE targets are absorbing (Infinity never divides to >= 1; NaN fails every
        // comparison). Zero and negative targets already resolve as "done" in BuildProgress's
        // positive-requirement gate, so they are left alone.
        if (data.Building && !FiniteFloatValidator.IsFinite(data.BuildTargetHours))
        {
            _logger.LogWarning(
                $"[Refuge] row '{key}' had non-finite BuildTargetHours {data.BuildTargetHours}; floored so the build can finish.");
            data.BuildTargetHours = MinBuildTargetHours;
        }
        // A FUTURE BuildStartTime is the same absorbing-state class from the other side: the
        // elapsed numerator stays negative for the whole offset, the build never finishes, and
        // the row is neither ready nor dismantlable while it eats a cap slot.
        if (data.Building && DaysSince(data.BuildStartTime) < 0.0)
        {
            _logger.LogWarning(
                $"[Refuge] row '{key}' had a future BuildStartTime; reset to now so the build can finish.");
            data.BuildStartTime = NowTime();
        }
        if (data.MilitiaAdded < 0)
            data.MilitiaAdded = 0;
        if (data.MilitiaPreRallyCount < 0)
            data.MilitiaPreRallyCount = 0;
    }

    private void FinishBuild(string partyId, RefugeData data)
```

`git grep -n "RepairLoadedRow"` at 7087fa633 finds the definition (755), the one call (661), and two
historical records that are not edited: `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md:12823`
and `docs/reviews/camps-review-log-2026-08-22.md:213`. `docs/features/refuge.md` does not name it; its
"Load repairs hostile rows" paragraph (lines 112 to 116) describes what `LoadFrom` does, which stays
true after the move.

### The shared floor (`RefugeService.cs:113-114` and `:873-881`)

```csharp
    /// <summary>Floor on a build's target hours so BuildProgress never divides by ~zero.</summary>
    private const float MinBuildTargetHours = 0.1f;
```

```csharp
    private float SaneBuildHours()
    {
        float hours = _settings.BuildHours;
        // Belt over the provider's validation: a degenerate target would make BuildProgress
        // divide by ~zero or never finish; the floor makes it finish almost immediately instead.
        if (!FiniteFloatValidator.IsFinite(hours) || hours < MinBuildTargetHours)
            return MinBuildTargetHours;
        return hours;
    }
```

`git grep -n "0\.1f" -- Main/Features/Refuge` at 7087fa633 finds only line 114. The constant must stay
one constant used by both the repair and `SaneBuildHours`, never two copies.

### The clock seams (`RefugeService.cs:988` and `:992`)

```csharp
    protected virtual CampaignTime NowTime() => CampaignTime.Now;
    ...
    protected virtual double DaysSince(CampaignTime time) => (CampaignTime.Now - time).ToDays;
```

Both signatures match the delegate types the unit takes: `NowTime` converts to `Func<CampaignTime>`
and `DaysSince` to `Func<CampaignTime, double>`. A delegate made from a method group of a virtual
method dispatches to the override (the C# compiler emits a virtual function load), so the test
subclass's overrides still apply when the service passes `DaysSince` and `NowTime`.

### The two test subclasses the move must keep working

- `TestableRefugeService` (`RefugeServiceTests.cs:30`) overrides
  `protected override CampaignTime NowTime() => default;` (99) and
  `protected override double DaysSince(CampaignTime time) => Days;` (101).
  `LoadFrom_FutureBuildStart_ResetsSoTheBuildCanFinish` (1262 to 1291) sets `_sut.Days = -41.0`, loads a
  building row whose `BuildStartTime = CampaignTime.Never`, and asserts
  `row.BuildStartTime.Equals(default(CampaignTime))`: it depends on both overrides.
- `HeroMergeProbeService` (`RefugeServiceTests.cs:2312`) overrides **neither** clock seam. Its two tests,
  `Dismantle_MovesEveryHeroAndHeroPrisonerByActionBeforeTheBulkCopy` (2355, `probe.LoadFrom(book, 0)` at
  2365) and `Dismantle_NoHeroesInRefuge_StillBulkMerges` (2384, call at 2392), load rows with
  `Established = true` and `Building` false. Today they never reach `CampaignTime.Now`, because
  `DaysSince` sits behind `data.Building &&` and `NowTime()` sits behind the future-start test. If the
  unit evaluated either seam eagerly, these two tests would call the base `CampaignTime.Now`
  (`Campaign.Current.MapTimeTracker.Now`, engine `CampaignTime.cs:70`) with no campaign and fail.

### Every existing `LoadFrom` call in the tests (all must pass unchanged)

`RefugeServiceTests.cs` calls `LoadFrom` at lines 364 (`Seed`), 1176 (save round trip), 1284, 2052,
2069, 2150, 2163, 2177, 2191, 2365 and 2392: eleven calls. The named load tests are
`LoadFrom_FutureBuildStart_ResetsSoTheBuildCanFinish` (1262), `LoadFrom_Null_ResetsToEmptyBookAndCounter`
(2048), `LoadFrom_NullRows_ScrubbedWithOneWarning` (2060, asserts
`_logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("2")))` at 2074),
`LoadFrom_DivergentInnerPartyId_CanonicalizedToTheDictionaryKey` (2143),
`LoadFrom_NaNBuildTarget_FlooredSoTheBuildCanFinish` (2156, asserts `0.1f`),
`LoadFrom_InfiniteBuildTarget_FlooredSoTheBuildCanFinish` (2169, asserts `0.1f`) and
`LoadFrom_UnknownTierAndNegativeMilitia_Repaired` (2183). `RefugeCampaignBehaviorTests.cs:138` and
`:149` call `LoadFrom` on an NSubstitute fake of `IRefugeService`; the signature does not change.

Because the whole `RefugeServiceTests` class is `RequiresGame`, the no-game RefAsm replay skips all of
it. The proof that the move holds is the game-backed `dotnet test` on the desktop.

### Engine facts (from `pwsh tools/taom-src.ps1 path CampaignTime`, v1.5.7 decompile)

```csharp
	public static CampaignTime Now => Campaign.Current.MapTimeTracker.Now;      // line 70
	public static CampaignTime Never => new CampaignTime(long.MaxValue);        // line 72
	public bool Equals(CampaignTime other)                                      // line 205
	{
		return _numTicks == other._numTicks;
	}
```

`CampaignTime` is a struct. The test comment at `RefugeServiceTests.cs:1268-1270` records that
`CampaignTime.Never` is the only nonzero `CampaignTime` a test can build without a campaign ("every
Hours/Days factory multiplies by internal statics a live campaign initializes"). In the RefAsm run the
TaleWorlds assemblies are metadata-only stubs, so calling any engine member (`Never`, `Equals`) fails
there; passing a `CampaignTime` by value or using `default(CampaignTime)` executes no engine code.
`TAOM.Tests/Features/Refuge/RefugeDataTests.cs` is untagged and constructs `RefugeData` (which has
`CampaignTime` fields), so building a `RefugeData` runs on the stubs.

### Blast radius

`python tools/graphify_taom.py affected "RefugeService" --depth 2`, run by the plan writer against the
main checkout's graph (which reported itself STALE, 79 code files changed since its build; a refresh
from the planning worktree did not finish inside 600 s), lists 399 nodes. Outside `TAOM.Tests` the
members that reach the service are all through `IRefugeService`: `RefugeIoC.cs:37,45`,
`RefugeMenuController.cs` (`CanFound`, `Found`, `CanUpgrade`, `Dismantle`, `NearestDismantlable`) and
`RefugeCampaignBehavior.cs` (`FrameTick` 141, `HourlyTick` 149, `LoadFrom` 88). The rest are the
`RefugeServiceTests` methods and its two subclasses. This plan changes no public, protected or internal
member of `RefugeService` (only the private `RepairLoadedRow` and the private constant go), so no
caller is affected. `RefugeBookRepair` is new and has no callers yet. Re-run the query in Step 1.

### Gates checked

- `ServiceSignatureGateTests` (`TAOM.Tests/Infrastructure/ServiceSignatureGateTests.cs:157-160`) scans
  only types whose name ends in `Service` (or `I*Adapter` interfaces). `RefugeBookRepair` is out of its
  scope by name; do not name the unit `*Service`. `TAOM.Tests/Infrastructure/*.txt` baselines do not
  mention `RefugeService`.
- `EntryPointLineCountGateTests` does not apply: `RefugeService` is a service, not an entry point, and
  it is not in `EntryPointLineCountBaseline.txt`.
- No tool or test reads `RefugeService.cs` as text (`git grep -l "RefugeService.cs"` over `tools`,
  `TAOM.Tests/Infrastructure` and `.claude` finds nothing).

### Conventions that bind this change

- **ADR-002** (thin entry points under 150 lines): not affected; `RefugeCampaignBehavior` is unchanged.
- **ADR-007** (adapters; protected-virtual boundary seams, amendment 2026-09-24): seam signatures use
  ids, value types and TAOM types, and engine structs such as `CampaignTime` are allowed. The unit takes
  no sealed TaleWorlds type: only `Dictionary<string, RefugeData>`, the `IModLogger` TAOM interface, and
  two delegates over the `CampaignTime` struct the seams already use. The seams themselves stay in the
  service, unchanged.
- **ADR-008** (service testability): no static TaleWorlds call outside a seam. The unit calls none: it
  reads the clock only through the delegates and uses `FiniteFloatValidator` (TAOM,
  `Main/Core/Validation/FiniteFloatValidator.cs:22`, `public static bool IsFinite(float value)`).
- **ADR-009** (self-documenting code): the moved comments go verbatim; the only new comments are the
  unit's summaries and the two-line note in `LoadFrom`.
- **`.claude/rules/csharp-architecture.md`**: "Engine-Float Decision Gates": `FiniteFloatValidator.IsFinite`
  stays the non-finite test, so zero and negative targets stay untouched, exactly as today. "Test
  Coverage": every decision path in the unit gets a test (see the test plan).
- **`.claude/rules/tests.md`**: test first; NSubstitute only; names `Method_State_Result`; tag a test
  `RequiresGame` when it executes engine code; "A class tag is the default; you may tag only the
  methods that need the game when that returns checks worth running on the stubs". This plan tags only
  the one method that calls `CampaignTime.Never` and `CampaignTime.Equals`.
- **`.claude/rules/simplicity-criterion.md`**: a move with equal behaviour and simpler structure. No
  new abstraction, no null guard the caller already makes, no interface.

## Commands you will need

Prefix every `dotnet` command with the `TEMP="..." TMP="..."` your dispatch rules give (quoted).

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors, warnings not above Step 1's count |
| Build the test project | `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests, `Failed: 0` |
| Unit tests | the same, plus `--filter "FullyQualifiedName~RefugeBookRepairTests"` | `Total: 16`, `Failed: 0` |
| Service tests | the same, plus `--filter "FullyQualifiedName~RefugeServiceTests"` | Step 1's total, `Failed: 0` |
| RefAsm build | `dotnet build TAOM.Tests -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId=` | exit 0 |
| RefAsm unit step | `dotnet test TAOM.Tests --no-build -p:TaomGameRefs=RefAsm -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~RefugeBookRepairTests&TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification"` | `Total: 15`, `Failed: 0` |
| Move check | `python -B "<your scratch folder>/check_refuge_repair_texts.py"` from the worktree root | `OK: 5 literals, ...`, exit 0 |
| Docs | `python tools/lint_docs.py --fail-on-drift` | exit 0 (no doc changes, run once at the end) |

Both MSBuild flags go on build AND test. Never `./build.ps1`: it deploys into the game install. A filter
that matches nothing proves nothing: read the `Total:` it prints. In this repo a `DataRow` is one test
result; if the filtered unit run prints a total other than 16, count the methods and rows in the file
you wrote and explain the difference before going on.

## Scope

**In scope** (the only files you modify):
- `Main/Features/Refuge/RefugeService.cs`
- `Main/Features/Refuge/Domain/RefugeBookRepair.cs` (new)
- `TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs` (new)

**Out of scope** (do NOT touch, even though they look related):
- `TAOM.Tests/Features/Refuge/RefugeServiceTests.cs`: its eleven `LoadFrom` calls are the proof and
  must pass unchanged. Editing it to make a test pass is a STOP.
- `Main/Features/Refuge/Domain/RefugeData.cs` and `RefugeSaveDefiner.cs`: no field, attribute or save
  change.
- `Main/Features/Refuge/Hooks/RefugeCampaignBehavior.cs`, `Main/Features/Refuge/RefugeIoC.cs`,
  `IRefugeService.cs`: no wiring or interface change.
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`: single-owner; nothing here needs them.
- `docs/features/refuge.md`: it does not name `RepairLoadedRow`; the key-files row is the
  orchestrator's (see below).
- The historical records `docs/changelog-archive/CHANGELOG-2026-H2-handwritten.md` and
  `docs/reviews/camps-review-log-2026-08-22.md`: they name `RepairLoadedRow` as history; leave them.
- The gates themselves. Never turn a gate green by editing it: deleting or `[Ignore]`-ing a test,
  loosening an assertion, or adding an allowlist entry. STOP and report instead.
- Any behaviour change, including "while I am here" fixes to the repair rules, the warning wording or
  the comments' wording.

## Git workflow

- Commit on the branch you were given; never push or open a PR. One commit.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` when you commit (it reads `v2.0.38` at 7087fa633; a hook
  refuses any other). Use:
  `refactor(refuge): <version> - Move the save-load repair into one unit`.
- The body is the changelog entry, wrapped at 72, no AI attribution trailer. Never edit `CHANGELOG.md`.
  Write it to a file in your scratch folder and run `git commit -F "<file>"`. Stage the three in-scope
  paths by name. A draft body (re-check every claim in it against your diff before you use it):

```text
RefugeService.LoadFrom no longer holds the repair it runs on every save
load. The null-row scrub and the per-row repair (key canonicalization,
unknown tier, non-finite build target, future build start, militia
floors) now live in RefugeBookRepair, an internal static unit beside
RefugeData, moved line for line with the same order and the same five
warning texts. The service keeps the null-book fallback, the counter
floor and the cache resets, and hands the repair the same dictionary.

The unit reads the campaign clock through two delegates bound to the
service's DaysSince and NowTime seams and calls each only where the
repair needs it, so loading rows that are not building still reads no
clock. The 0.1 hour build-target floor is one constant, shared by the
repair and the build-hours clamp.

No save change: RefugeData is untouched.

Not-tested: an in-game load of a damaged save; the RequiresGame
RefugeServiceTests ran in the game-backed suite.
```

## Steps

### Step 1: record the base

1. Run the drift check at the top of this plan. Expected: no output. Any changed in-scope file: compare
   its excerpts above with the live code, and STOP on a mismatch.
2. `python tools/graphify_taom.py refresh --if-stale` (prefix `timeout 900`), then
   `python tools/graphify_taom.py affected "RefugeService" --depth 2`. Expected: the production callers
   reach the service only through `IRefugeService` members, as "Blast radius" lists. If the refresh
   fails or times out, run `affected` anyway, note that it reported stale, and go on: the change touches
   only private members. If `affected` shows a production caller of `RepairLoadedRow` or
   `MinBuildTargetHours`, STOP.
3. Build `Main/TAOM.csproj` and record the warning and error counts.
4. Run the full test command and record its totals line and failing names.
5. Run the service-tests filter (`FullyQualifiedName~RefugeServiceTests`) and record its `Total:`.
   Call it R.

**Verify**: the build shows 0 errors (11 warnings at planning); the full run shows
`Failed: 0, Passed: 39250, Skipped: 9, Total: 39259`, or the difference is explained in your report
(a different baseline on your branch is not a STOP if it has no failures in `TAOM.Tests.Features.Refuge`);
R is 154 (counted from the source at 7087fa633: 150 `[TestMethod]` methods plus 2 `[DataTestMethod]`
methods with 2 `DataRow`s each, at lines 408 and 793). A different R with the drift check clean is a STOP:
the filter matches the wrong set.

### Step 2: write the failing unit tests (RED)

Create `TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs` with exactly this content:

```csharp
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Refuge.Domain;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.Refuge;

/// <summary>
/// Direct coverage for <see cref="RefugeBookRepair"/>, the save-load repair that
/// RefugeService.LoadFrom runs. The clock is two counting delegates, so these tests pin WHEN the
/// repair reads it: a book whose rows are not building reads neither. Only the future-start reset
/// calls engine members (CampaignTime.Never and CampaignTime.Equals, which fail on the RefAsm
/// stubs), so only that method carries RequiresGame; the rest run in the no-game replay.
/// </summary>
[TestClass]
public class RefugeBookRepairTests
{
    private IModLogger _logger;
    private List<string> _warnings;
    private double _days;
    private CampaignTime _now;
    private int _daysCalls;
    private int _nowCalls;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _warnings = new List<string>();
        _logger.When(l => l.LogWarning(Arg.Any<string>())).Do(call => _warnings.Add(call.Arg<string>()));
        _days = 0.0;
        _now = default;
        _daysCalls = 0;
        _nowCalls = 0;
    }

    private double DaysSince(CampaignTime start)
    {
        _daysCalls++;
        return _days;
    }

    private CampaignTime Now()
    {
        _nowCalls++;
        return _now;
    }

    private void Repair(Dictionary<string, RefugeData> book) =>
        RefugeBookRepair.Repair(book, DaysSince, Now, _logger);

    private static Dictionary<string, RefugeData> Book(string key, RefugeData row) =>
        new Dictionary<string, RefugeData> { [key] = row };

    [TestMethod]
    public void Repair_NullRows_RemovedWithOneWarningNamingTheCount()
    {
        var healthy = new RefugeData { PartyId = "r1", Established = true };
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = healthy,
            ["r_null"] = null,
            ["r_null2"] = null,
        };

        Repair(book);

        Assert.AreEqual(1, book.Count, "null rows are removed from the same dictionary");
        Assert.AreSame(healthy, book["r1"]);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "dropped 2 null book row(s)");
    }

    [TestMethod]
    public void Repair_DivergentInnerPartyId_CanonicalizedToTheKey()
    {
        var row = new RefugeData { PartyId = "r2", Established = true };

        Repair(Book("r1", row));

        Assert.AreEqual("r1", row.PartyId);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "canonicalized to the key");
    }

    [TestMethod]
    public void Repair_UnknownTier_ResetToRefuge()
    {
        var row = new RefugeData { PartyId = "r1", Tier = 99 };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeTier.Refuge, row.TierEnum);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "unknown tier 99");
    }

    [TestMethod]
    public void Repair_StrongholdTier_Kept()
    {
        var row = new RefugeData { PartyId = "r1", TierEnum = RefugeTier.Stronghold };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeTier.Stronghold, row.TierEnum);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Repair_NegativeMilitia_FlooredAtZeroWithoutAWarning()
    {
        var row = new RefugeData { PartyId = "r1", MilitiaAdded = -5, MilitiaPreRallyCount = -3 };

        Repair(Book("r1", row));

        Assert.AreEqual(0, row.MilitiaAdded);
        Assert.AreEqual(0, row.MilitiaPreRallyCount);
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(float.NaN)]
    [DataRow(float.PositiveInfinity)]
    [DataRow(float.NegativeInfinity)]
    public void Repair_NonFiniteTargetOnBuildingRow_FlooredToTheSharedMinimum(float target)
    {
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = target };

        Repair(Book("r1", row));

        Assert.AreEqual(RefugeBookRepair.MinBuildTargetHours, row.BuildTargetHours);
        Assert.AreEqual(0.1f, row.BuildTargetHours);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "non-finite BuildTargetHours");
    }

    [TestMethod]
    public void Repair_NonFiniteTargetOnIdleRow_LeftAlone()
    {
        var row = new RefugeData { PartyId = "r1", Building = false, BuildTargetHours = float.NaN };

        Repair(Book("r1", row));

        Assert.IsTrue(float.IsNaN(row.BuildTargetHours), "only a building row's target is repaired");
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(-3f)]
    public void Repair_ZeroOrNegativeTargetOnBuildingRow_LeftAlone(float target)
    {
        // BuildProgress already resolves these as done; only non-finite targets are absorbing.
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = target };

        Repair(Book("r1", row));

        Assert.AreEqual(target, row.BuildTargetHours);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    public void Repair_IdleRows_NeverReadTheClock()
    {
        // RefugeServiceTests' HeroMergeProbeService loads idle rows through a service that
        // overrides neither clock seam; an eager read would reach CampaignTime.Now there.
        _days = -41.0;
        var book = new Dictionary<string, RefugeData>
        {
            ["r1"] = new RefugeData { PartyId = "r1", Established = true },
            ["r2"] = new RefugeData { PartyId = "r2", BuildTargetHours = float.NaN },
        };

        Repair(book);

        Assert.AreEqual(0, _daysCalls, "daysSince is read only for a building row");
        Assert.AreEqual(0, _nowCalls, "now is read only for a future start");
        Assert.AreEqual(0, _warnings.Count);
    }

    [DataTestMethod]
    [DataRow(2.0)]
    [DataRow(0.0)]
    public void Repair_BuildingRowStartNotInTheFuture_ReadsDaysSinceButNotNow(double days)
    {
        // 0.0 is the boundary: the future test is a strict "< 0.0".
        _days = days;
        var row = new RefugeData { PartyId = "r1", Building = true, BuildTargetHours = 6f };

        Repair(Book("r1", row));

        Assert.AreEqual(1, _daysCalls);
        Assert.AreEqual(0, _nowCalls);
        Assert.AreEqual(0, _warnings.Count);
    }

    [TestMethod]
    [TestCategory("RequiresGame")]
    public void Repair_BuildingRowStartedInTheFuture_ResetsToNow()
    {
        // CampaignTime.Never is the one nonzero CampaignTime a test can build without a campaign
        // (see LoadFrom_FutureBuildStart_ResetsSoTheBuildCanFinish); it and Equals are engine
        // members, which is why this method needs the game.
        _days = -41.0;
        _now = CampaignTime.Never;
        var row = new RefugeData { PartyId = "r1", Established = true, Building = true, BuildTargetHours = 6f };

        Repair(Book("r1", row));

        Assert.IsTrue(row.BuildStartTime.Equals(CampaignTime.Never), "the reset lands on now()");
        Assert.AreEqual(1, _daysCalls);
        Assert.AreEqual(1, _nowCalls);
        Assert.AreEqual(1, _warnings.Count);
        StringAssert.Contains(_warnings[0], "future BuildStartTime");
    }

    [TestMethod]
    public void Repair_EveryDefectOnOneRow_WarnsInTheFixedOrder()
    {
        _days = -1.0;
        var row = new RefugeData
        {
            PartyId = "r2",
            Tier = 99,
            Building = true,
            BuildTargetHours = float.NaN,
            MilitiaAdded = -1,
            MilitiaPreRallyCount = -1,
        };

        Repair(Book("r1", row));

        Assert.AreEqual(4, _warnings.Count);
        StringAssert.Contains(_warnings[0], "canonicalized to the key");
        StringAssert.Contains(_warnings[1], "row 'r1' had unknown tier 99");
        StringAssert.Contains(_warnings[2], "row 'r1' had non-finite BuildTargetHours");
        StringAssert.Contains(_warnings[3], "row 'r1' had a future BuildStartTime");
        Assert.AreEqual("r1", row.PartyId);
        Assert.AreEqual(RefugeTier.Refuge, row.TierEnum);
        Assert.AreEqual(0.1f, row.BuildTargetHours);
        Assert.AreEqual(1, _nowCalls);
        Assert.AreEqual(0, row.MilitiaAdded);
        Assert.AreEqual(0, row.MilitiaPreRallyCount);
    }
}
```

Then build the project that holds the test: `dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.

**Verify**: the build fails, and every error is in `RefugeBookRepairTests.cs` and names
`RefugeBookRepair` (the type does not exist yet; CS0103 or CS0246). An error in any other file, or one
not about `RefugeBookRepair`, is a STOP: report it. Quote the error count and one error line in your
report as the RED evidence.

### Step 3: create the unit (GREEN)

Create `Main/Features/Refuge/Domain/RefugeBookRepair.cs` with exactly this content. Its two method
bodies are the base's lines 646 to 661 and 757 to 789, comments included, with only these renames:
`_refuges` to `book`, `_logger.` to `logger.`, `DaysSince(` to `daysSince(`, `NowTime()` to `now()`,
and the per-row call. Do not reword a comment or a string.

```csharp
using System;
using System.Collections.Generic;
using TAOM.Core.Logging;
using TAOM.Core.Validation;
using TaleWorlds.CampaignSystem;

namespace TAOM.Features.Refuge.Domain;

/// <summary>
/// The save-load repair of the refuge book, moved out of <c>RefugeService.LoadFrom</c>
/// unchanged. The clock arrives as two delegates, bound by the service to its own
/// <c>DaysSince</c> and <c>NowTime</c> seams, and each is invoked only where the repair needs it:
/// a load whose rows are not building reads no clock at all.
/// </summary>
internal static class RefugeBookRepair
{
    /// <summary>Floor on a build's target hours so BuildProgress never divides by ~zero.</summary>
    internal const float MinBuildTargetHours = 0.1f;

    /// <summary>Scrubs null rows, then repairs every remaining row in place. The dictionary
    /// instance is kept (the service hands it back to SyncData by reference). The caller passes
    /// a non-null book.</summary>
    internal static void Repair(
        Dictionary<string, RefugeData> book,
        Func<CampaignTime, double> daysSince,
        Func<CampaignTime> now,
        IModLogger logger)
    {
        // A partially recovered or externally modified save can carry null rows; every tick path
        // dereferences the value, so scrub here rather than crash on the first FrameTick.
        List<string> nullKeys = null;
        foreach (var pair in book)
        {
            if (pair.Value == null)
                (nullKeys ?? (nullKeys = new List<string>())).Add(pair.Key);
        }
        if (nullKeys != null)
        {
            foreach (var key in nullKeys)
                book.Remove(key);
            logger.LogWarning($"[Refuge] dropped {nullKeys.Count} null book row(s) from the loaded save.");
        }
        foreach (var pair in book)
            RepairRow(pair.Key, pair.Value, daysSince, now, logger);
    }

    /// <summary>Canonicalizes and validates one persisted row (hostile or damaged saves). The
    /// DICTIONARY KEY is the identity every lookup resolves and every distance check measures, so
    /// a divergent inner PartyId would make Dismantle/visuals act on a DIFFERENT party than the
    /// one the player stands at -- cross-party destruction. Numeric repair kills the absorbing
    /// states: a non-finite BuildTargetHours on a Building row could otherwise never reach
    /// BuildProgress >= 1 (NaN fails every comparison; Infinity divides to 0) and the row eats a
    /// cap slot forever.</summary>
    private static void RepairRow(
        string key,
        RefugeData data,
        Func<CampaignTime, double> daysSince,
        Func<CampaignTime> now,
        IModLogger logger)
    {
        if (!string.Equals(data.PartyId, key, StringComparison.Ordinal))
        {
            logger.LogWarning(
                $"[Refuge] loaded row keyed '{key}' carried inner PartyId '{data.PartyId}'; canonicalized to the key.");
            data.PartyId = key;
        }
        if (data.Tier != (int)RefugeTier.Refuge && data.Tier != (int)RefugeTier.Stronghold)
        {
            logger.LogWarning($"[Refuge] row '{key}' had unknown tier {data.Tier}; reset to Refuge.");
            data.TierEnum = RefugeTier.Refuge;
        }
        // Only NON-FINITE targets are absorbing (Infinity never divides to >= 1; NaN fails every
        // comparison). Zero and negative targets already resolve as "done" in BuildProgress's
        // positive-requirement gate, so they are left alone.
        if (data.Building && !FiniteFloatValidator.IsFinite(data.BuildTargetHours))
        {
            logger.LogWarning(
                $"[Refuge] row '{key}' had non-finite BuildTargetHours {data.BuildTargetHours}; floored so the build can finish.");
            data.BuildTargetHours = MinBuildTargetHours;
        }
        // A FUTURE BuildStartTime is the same absorbing-state class from the other side: the
        // elapsed numerator stays negative for the whole offset, the build never finishes, and
        // the row is neither ready nor dismantlable while it eats a cap slot.
        if (data.Building && daysSince(data.BuildStartTime) < 0.0)
        {
            logger.LogWarning(
                $"[Refuge] row '{key}' had a future BuildStartTime; reset to now so the build can finish.");
            data.BuildStartTime = now();
        }
        if (data.MilitiaAdded < 0)
            data.MilitiaAdded = 0;
        if (data.MilitiaPreRallyCount < 0)
            data.MilitiaPreRallyCount = 0;
    }
}
```

The service still has its own copy at this point; that is intended, so the tree builds between steps.
Line endings and BOM need no care: the Refuge `.cs` files are stored LF (`i/lf w/crlf`), git normalizes
a new file on add, and no gate checks either.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0
errors; the unit-tests filter (`FullyQualifiedName~RefugeBookRepairTests`) prints `Failed: 0` and
`Total: 16`.

### Step 4: prove the laziness tests can fail

This is the plan's main risk, so prove the tests that guard it bite. With the Edit tool, add this line as
the first statement inside `Repair` (above the null-row comment):

```csharp
        var eagerNow = now();
```

Run the unit-tests filter. Then remove that line with the Edit tool (restoring Step 3's exact text) and
run the filter again.

**Verify**: with the line in place, the filter prints `Failed: 5`, `Total: 16`, and the five failing
results are exactly `Repair_IdleRows_NeverReadTheClock` and both rows of
`Repair_BuildingRowStartNotInTheFuture_ReadsDaysSinceButNotNow` (they expect `_nowCalls` 0 and get 1),
plus `Repair_BuildingRowStartedInTheFuture_ResetsToNow` and `Repair_EveryDefectOnOneRow_WarnsInTheFixedOrder`
(they expect 1 and get 2). Quote the failing names. With the line removed, the filter prints
`Failed: 0`, `Total: 16`. Do not use git to undo the edit.

### Step 5: wire the service to the unit

Edit `Main/Features/Refuge/RefugeService.cs`. The line numbers below are the base's, and item 1 shifts
every later line up by 3: make each edit by matching its text with the Edit tool, not by line number.

1. Delete lines 113 to 115 (the `MinBuildTargetHours` summary comment, the constant, and the blank line
   after it), so the `FrameWorkIntervalHours` summary follows the `RefugeIdPrefix` line and its blank
   line.
2. Replace the body of `LoadFrom` (643 to 666) so it reads exactly:

```csharp
    public void LoadFrom(Dictionary<string, RefugeData> refuges, int counter)
    {
        _refuges = refuges ?? new Dictionary<string, RefugeData>();
        // The seams go in as method groups, so the test subclass's overrides apply; the repair
        // calls each only where it needs the clock, so a load of idle rows reads none.
        RefugeBookRepair.Repair(_refuges, DaysSince, NowTime, _logger);
        _counter = counter < 0 ? 0 : counter;
        _visualShown.Clear();
        _holdNoteShown.Clear();
        _nextFrameWorkHours = double.MinValue;
    }
```

3. Delete `RepairLoadedRow` with its summary comment (748 to 790) and the blank line after it, so
   `// --- internals ---`, one blank line, then `private void FinishBuild(string partyId, RefugeData data)`
   follow each other.
4. In `SaneBuildHours`, change both uses of `MinBuildTargetHours` to
   `RefugeBookRepair.MinBuildTargetHours`:

```csharp
        if (!FiniteFloatValidator.IsFinite(hours) || hours < RefugeBookRepair.MinBuildTargetHours)
            return RefugeBookRepair.MinBuildTargetHours;
```

Leave every other line of the file as it is. `using TAOM.Features.Refuge.Domain;` is already present
(line 11). Do not remove a `using`: `FiniteFloatValidator` (in `TAOM.Core.Validation`) is still used by
`SaneBuildHours` and the militia formula.

Then write the move check below to `<your scratch folder>/check_refuge_repair_texts.py` with the Write
tool and run it from the worktree root as `python -B "<your scratch folder>/check_refuge_repair_texts.py"`.
It reads the base service with `git show 7087fa633:...`, takes the five warning literals from base lines
658, 760, 765, 774 and 783, and checks that each appears exactly once in the unit, in that order, and no
longer in the service; then it checks that base lines 646 to 661 and 757 to 789, with only the five
renames, appear as contiguous runs in the unit. The plan writer ran it against Step 3's text (exit 0)
and against the unedited base service (exit 1, "still in the service") and a one-character mutant
(exit 1).

```python
"""Plan 049 check: the five load-repair warning literals moved byte-identical and in order.

Run from the repository root: python -B <this file> [unit-path] [service-path]
Exit 0 and "OK" when every literal of the base's five warning lines appears exactly once in the
new unit, in the base's order, and no longer appears in the service. Exit 1 otherwise.
"""
import re
import subprocess
import sys
from pathlib import Path

BASE = "7087fa633"
SERVICE = "Main/Features/Refuge/RefugeService.cs"
UNIT = "Main/Features/Refuge/Domain/RefugeBookRepair.cs"
# 1-based lines of the base file that hold the five interpolated warning literals.
LINES = [658, 760, 765, 774, 783]


def main() -> int:
    unit_path = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(UNIT)
    service_path = Path(sys.argv[2]) if len(sys.argv) > 2 else Path(SERVICE)
    base = subprocess.run(
        ["git", "show", f"{BASE}:{SERVICE}"],
        capture_output=True, check=True,
    ).stdout.decode("utf-8").splitlines()
    literals = []
    for number in LINES:
        found = re.findall(r'\$"[^"]*"', base[number - 1])
        if len(found) != 1 or "[Refuge]" not in found[0]:
            print(f"FAIL: base line {number} does not hold one [Refuge] literal: {base[number - 1]!r}")
            return 1
        literals.append(found[0])
    unit = unit_path.read_text(encoding="utf-8")
    service = service_path.read_text(encoding="utf-8")
    failed = False
    last = -1
    for literal in literals:
        count = unit.count(literal)
        if count != 1:
            print(f"FAIL: {count} copies in the unit of {literal}")
            failed = True
            continue
        position = unit.index(literal)
        if position < last:
            print(f"FAIL: out of order in the unit: {literal}")
            failed = True
        last = position
        if literal in service:
            print(f"FAIL: still in the service: {literal}")
            failed = True
    # The moved bodies, line for line: base 646-661 (the null-row scrub) and 757-789 (the row
    # repair), with only the renames the move makes, must appear as contiguous runs in the unit.
    renames = [
        ("_refuges", "book"),
        ("_logger.", "logger."),
        ("DaysSince(", "daysSince("),
        ("NowTime()", "now()"),
        ("RepairLoadedRow(pair.Key, pair.Value)", "RepairRow(pair.Key, pair.Value, daysSince, now, logger)"),
    ]
    unit_lines = [line.strip() for line in unit.splitlines() if line.strip()]
    for first, last_line in ((646, 661), (757, 789)):
        run = []
        for line in base[first - 1:last_line]:
            text = line.strip()
            if not text:
                continue
            for old, new in renames:
                text = text.replace(old, new)
            run.append(text)
        hits = [i for i in range(len(unit_lines) - len(run) + 1) if unit_lines[i:i + len(run)] == run]
        if len(hits) != 1:
            print(f"FAIL: base lines {first} to {last_line} appear {len(hits)} times as one run in the unit")
            failed = True
    if failed:
        return 1
    print(f"OK: {len(literals)} literals, byte-identical and in order; both moved bodies match line for line")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

**Verify**, all from the worktree root. The two new files are untracked until Step 9, and plain
`git grep` searches tracked files only, so these greps carry `--untracked` (it still skips ignored
paths such as `bin/` and `obj/`):
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0, 0 errors, warnings
  not above Step 1's count.
- The move check prints `OK: 5 literals, byte-identical and in order; both moved bodies match line for line`
  and exits 0.
- `git grep -n --untracked "RepairLoadedRow" -- Main TAOM.Tests docs/features` prints nothing.
- `git grep -n --untracked "MinBuildTargetHours" -- Main` prints exactly four lines: the constant and its
  use in `RefugeBookRepair.cs`, and the two `RefugeBookRepair.MinBuildTargetHours` uses in `RefugeService.cs`.
- `git grep -n --untracked "const float MinBuildTargetHours" -- Main TAOM.Tests` prints exactly one line,
  in `RefugeBookRepair.cs`; `git grep -n --untracked "0\.1f" -- Main/Features/Refuge` prints exactly that
  one line.
- `git grep -n "RefugeBookRepair.Repair(_refuges, DaysSince, NowTime, _logger);" -- Main/Features/Refuge/RefugeService.cs`
  prints one line.

### Step 6: the existing service tests pass unchanged

Run the service-tests filter, then this named filter:

`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~RefugeServiceTests.LoadFrom_|FullyQualifiedName~RefugeServiceTests.Dismantle_MovesEveryHeroAndHeroPrisonerByActionBeforeTheBulkCopy|FullyQualifiedName~RefugeServiceTests.Dismantle_NoHeroesInRefuge_StillBulkMerges"`

**Verify**: the service filter prints `Failed: 0` and `Total:` R from Step 1; the named filter prints
`Total: 9`, `Failed: 0` (the seven `LoadFrom_` tests and the two probe tests).
`git diff --stat -- TAOM.Tests/Features/Refuge/RefugeServiceTests.cs` prints nothing.

### Step 7: the full game-backed suite

Run the full test command.

**Verify**: `Failed: 0`, `Skipped:` the same as Step 1, and `Total:` Step 1's total plus 16 (the new
class's results).

### Step 8: the RefAsm unit step for the new untagged tests

The new class has untagged tests that construct `RefugeData` and pass `CampaignTime` values, so prove
they run on the stubs (`.ai/verification.md`). Run the RefAsm build, then the RefAsm unit step from the
commands table.

**Verify**: `Total: 15`, `Failed: 0` (the sixteen results minus the one `RequiresGame` method). A test
that fails here with a `NullReferenceException` or `TypeInitializationException` from a TaleWorlds frame
means it executes engine code: STOP and report it with its name; do not tag it to make it pass without
reporting. If the RefAsm restore itself fails for an environment reason (no network, the package feed),
record "RefAsm step NOT RUN" with the exact error and go on to Step 9. After this step the test bin is a
RefAsm build: if you must run a game-backed test again, first run
`dotnet build TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --no-incremental`.

### Step 9: docs lint and commit

Run `python tools/lint_docs.py --fail-on-drift`. Stage the three in-scope paths by name, write the
commit message (see "Git workflow") to a file in your scratch folder, and commit with
`git commit -F "<file>"`.

**Verify**: lint exits 0; `git show --stat HEAD` lists exactly
`Main/Features/Refuge/RefugeService.cs`, `Main/Features/Refuge/Domain/RefugeBookRepair.cs` and
`TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs`; `git status --porcelain` prints nothing.

## Test plan

- New: `TAOM.Tests/Features/Refuge/RefugeBookRepairTests.cs`, 12 methods and 16 results, one per
  decision path of the unit: null-row scrub with its one counted warning; key canonicalization; unknown
  tier reset and a valid Stronghold kept; militia floors with no warning; non-finite target on a
  building row (NaN, +Infinity, -Infinity) floored to the shared constant; non-finite target on an idle
  row left alone; zero and negative targets left alone; idle rows read no clock; a building row with a
  past or present start reads `daysSince` once and `now` never (2.0 and the 0.0 boundary); a future
  start resets to `now()` (`RequiresGame`); and every defect on one row warns in the fixed order.
- Pattern to model after: `TAOM.Tests/Features/Refuge/RefugeDataTests.cs` (untagged pure-data tests on
  the Refuge domain) and the warning-capture form `_logger.When(l => l.LogWarning(Arg.Any<string>())).Do(...)`
  used in `TAOM.Tests/Core/Logging/EarlyLogBridgeTests.cs:126`.
- Unchanged and binding: every test in `RefugeServiceTests`, in particular the seven `LoadFrom_` tests
  and the two `HeroMergeProbeService` tests, which fail if a clock seam were made eager.
- Not testable here: an in-game load of a damaged save (named in the commit's `Not-tested:` trailer).
  No other line is waived.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` exits 0 with 0 errors and
      no more warnings than Step 1 recorded
- [ ] The full `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` shows `Failed: 0` and a
      total of Step 1's total plus 16
- [ ] `--filter "FullyQualifiedName~RefugeServiceTests"` shows `Failed: 0` and the same total as Step 1
- [ ] The RefAsm unit step for `RefugeBookRepairTests` shows `Total: 15`, `Failed: 0` (or is recorded
      NOT RUN with an environment error)
- [ ] Step 4's mutation run showed `Failed: 5` with the five names Step 4 lists, quoted in the report
- [ ] The move check script exits 0 with its `OK:` line
- [ ] `git grep -n "RepairLoadedRow" -- Main TAOM.Tests docs/features` returns nothing
- [ ] `git grep -n "const float MinBuildTargetHours" -- Main TAOM.Tests` returns exactly one line, in
      `Main/Features/Refuge/Domain/RefugeBookRepair.cs`
- [ ] `git diff 7087fa633 --stat -- TAOM.Tests/Features/Refuge/RefugeServiceTests.cs Main/Features/Refuge/Domain/RefugeData.cs Main/Features/Refuge/Hooks`
      prints nothing
- [ ] `git show --stat HEAD` lists exactly the three in-scope files; `git status --porcelain` is empty
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes (the commit body's claims included)

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts (line numbers included: the
  move check reads base lines 646 to 661, 658, 757 to 789 and the literal lines by number).
- Step 2's build fails for anything other than the missing `RefugeBookRepair`.
- After Step 5, `Dismantle_MovesEveryHeroAndHeroPrisonerByActionBeforeTheBulkCopy` or
  `Dismantle_NoHeroesInRefuge_StillBulkMerges` fails: a clock seam was made eager. Do not work around it.
- After Step 5, any `RefugeServiceTests` test fails, or passing one seems to need an edit to
  `RefugeServiceTests.cs`.
- Step 4's mutation does not make `Repair_IdleRows_NeverReadTheClock` fail: the test does not guard
  what it claims.
- The change seems to need any out-of-scope file, especially `RefugeData.cs`, `RefugeCampaignBehavior.cs`,
  `IRefugeService.cs`, `Main/IoC.cs`, `Main/SubModule.cs` or `Main/TAOM.csproj`.
- A step's verification fails twice after a reasonable fix.
- A TaleWorlds signature differs from "Current state" (`CampaignTime.Now`, `Never`, `Equals`, or the
  seam signatures at `RefugeService.cs:988` and `:992`); report the mismatch.
- The assumption "a method-group delegate to `DaysSince` or `NowTime` dispatches to the test
  subclass's override" proves false (the future-start service test would then fail): STOP.

## Orchestrator steps (not the executor's)

- Issue: file it before dispatch (the brief names none).
- `/localize`: none; the five warning texts are log lines, not player-facing, and are unchanged.
- `docs/features/refuge.md`: optionally add a "Key files" row (the table at lines 162 to 168) for
  `Domain/RefugeBookRepair.cs`, "save-load repair of the book: null-row scrub, row canonicalization;
  clock through the service's seams". No feature-map row is needed: no new feature.
- `/deep-review` before merge (C# commit).

## After merge: the maintainer's actions

None. No save, settings, hook or player-visible change.

## Maintenance notes

- A new `RefugeData` field that needs load repair gets its line in `RefugeBookRepair.RepairRow`, with a
  direct test in `RefugeBookRepairTests`. If the repair needs the clock, it takes it from the existing
  delegates, in the conditional position where it is needed, never as a value computed up front.
- Review should probe: the two delegate arguments in `LoadFrom` are method groups (not lambdas calling
  `CampaignTime.Now`); the order of the five checks; that the comments moved verbatim; that
  `SaneBuildHours` uses the unit's constant; that the dictionary instance is the one `SaveInto` returns.
- Deferred: further extraction from `RefugeService` (raid selection is already a seam-fed pure loop
  with its own tests and needs no move). Not in this plan.
