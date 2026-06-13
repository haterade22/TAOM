# Plan 001: Reset CareerSystem + SpecialResources singletons on the new-campaign boundary

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**:
> `git diff --stat 141b749..HEAD -- Main/Features/CareerSystem Main/Features/SpecialResources TAOM.Tests/Features/CareerSystem TAOM.Tests/Features/SpecialResources`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1
- **Effort**: S
- **Risk**: LOW
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `141b749`, 2026-06-13
- **Issue**: create before implementation lands — orchestrator (TAOM issue-first mandate)

## Why this matters

Two feature services hold per-hero state in a `Reuse.Singleton` dictionary that lives for the **entire Bannerlord process**, not for one campaign. When a player finishes campaign A and starts campaign B without restarting the game, B inherits A's data:

- **CareerSystem** (`CareerDataService._heroData`): B's player inherits A's career choices, tier unlocks, and quest flags. Worse — at character creation, `CareerCreationHandler` calls `TryAddChoice(root, maxChoices)`; with leaked choices already counted, `GetChoiceCount() >= maxChoicesAllowed` **silently rejects B's root choice**. AI/lord career entries leak wholesale. On B's first save the leaked data is serialized into B's save file — permanent contamination.
- **SpecialResources** (`SpecialResourceStorageService._data`): B starts with A's resource balances for every `(hero, resource)` pair except the single key `InitializeHero` overwrites. A stale entry also makes the `_storage.Contains(...)` seeding gate suppress B's legitimate `StartingAmount` seeding, so the player inherits A's balance for any resource A ever touched.

Both feature areas already have the *pattern* for this fix — `SpecialResourceService.ResetSessionState()` (Phase 9b #133 P2 R1) clears the service's session state on `OnNewGameCreated`, but it **never clears the storage dict**, and `CareerPersistenceBehavior` has no new-game reset at all. This plan closes both gaps. After it lands, starting a second campaign in the same process produces a clean slate for both features. (Reference memory: `feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md` — singleton-scoped state leaking across lifetime boundaries.)

## Current state

The relevant files (all paths absolute from repo root `c:/Users/mikew/source/repos/TAOM/`):

### CareerSystem (CORRECTNESS-01)

- `Main/Features/CareerSystem/CareerPersistenceBehavior.cs` — the `CampaignBehaviorBase` for career data. **`RegisterEvents()` is empty** — no new-game reset. The only bulk replace of career data is the SyncData *load* branch (line 118), which a new game never invokes.
- `Main/Features/CareerSystem/CareerDataService.cs` — owns the leaking dict; registered `Reuse.Singleton`.
- `Main/Features/CareerSystem/ICareerDataService.cs` — the interface; currently exposes a bulk replace only via `RestoreData(Dictionary<string, HeroCareerData>)`.
- `Main/Features/CareerSystem/CareerSystemIoC.cs:16` — `container.Register<ICareerDataService, CareerDataService>(Reuse.Singleton);` (process lifetime).
- `Main/SubModule.cs:420` — `campaignStarter.AddBehavior(new CareerPersistenceBehavior(careerDataService, careerLogger));` (single-owner file — do NOT edit; the behavior is already wired, this plan changes only its body).

`CareerPersistenceBehavior.cs:20-22` as it exists today:

```csharp
    public override void RegisterEvents()
    {
    }
```

`CareerPersistenceBehavior.cs:11-18` (the injected fields the new handler will use):

```csharp
    private readonly ICareerDataService _dataService;
    private readonly IModLogger _logger;

    public CareerPersistenceBehavior(ICareerDataService dataService, IModLogger logger)
    {
        _dataService = dataService;
        _logger = logger;
    }
```

`CareerDataService.cs:103-106` — the existing bulk-replace entry point (the reset will reuse this; `RestoreData(null)` already coalesces to an empty dict):

```csharp
    public void RestoreData(Dictionary<string, HeroCareerData> data)
    {
        _heroData = data ?? new Dictionary<string, HeroCareerData>();
    }
```

`CareerDataService.cs:11-19` + `27-32` — confirm that a leaked dict both rejects the new root choice AND survives because `GetOrCreateData` only *adds*:

```csharp
    public HeroCareerData GetOrCreateData(string heroStringId)
    {
        if (!_heroData.TryGetValue(heroStringId, out var data))
        {
            data = new HeroCareerData(heroStringId);
            _heroData[heroStringId] = data;
        }
        return data;
    }
    ...
    public bool TryAddChoice(string heroStringId, string choiceStringId, int maxChoicesAllowed)
    {
        var data = GetOrCreateData(heroStringId);
        if (data.GetChoiceCount() >= maxChoicesAllowed) return false;   // ← leaked choices trip this
        return data.AddChoice(choiceStringId);
    }
```

### SpecialResources (CORRECTNESS-02 / harvest also tags this finding-cluster ref)

- `Main/Features/SpecialResources/SpecialResourceStorageService.cs` — owns the leaking `_data` dict.
- `Main/Features/SpecialResources/SpecialResourcesBehavior.cs` — the `CampaignBehaviorBase`. `OnNewGameCreated` (lines 93-108) already calls `_service.ResetSessionState()` then `_service.InitializeHero(...)`, but **never clears `_storage`**. `SyncData` (lines 79-91) passes the **live** storage dict as the `ref` on load, so an absent save key leaves the prior campaign's dict in place.
- `Main/Features/SpecialResources/SpecialResourceService.cs:278-293` — `ResetSessionState()` clears `_pendingSpend` / `_inSession` / `_loggedResolveKeys` but **does not touch storage** (storage is a separate `ISpecialResourceStorageService` the service does not own the dict of).
- `Main/Features/SpecialResources/ISpecialResourceStorageService.cs` — the storage interface. `RestoreData(Dictionary<string, float>)` is the bulk replace; `RestoreData(null)` already coalesces to empty (see below).

`SpecialResourceStorageService.cs:6-8` + `34-37`:

```csharp
public class SpecialResourceStorageService : ISpecialResourceStorageService
{
    private Dictionary<string, float> _data = new();
    ...
    public void RestoreData(Dictionary<string, float> data)
    {
        _data = data ?? new Dictionary<string, float>();
    }
```

`SpecialResourcesBehavior.cs:79-91` — the load path that re-installs the prior campaign's dict when the save lacks the key:

```csharp
    public override void SyncData(IDataStore dataStore)
    {
        _logger.LogInfo("[SpecRes] SyncData called (save/load)");
        var data = _storage.GetAllData();              // ← LIVE dict, not a null local
        dataStore.SyncData("_taom_specialResources", ref data);
        _storage.RestoreData(data);
        _logger.LogInfo($"[SpecRes] SyncData restored {data?.Count ?? 0} entries");
        ...
    }
```

`SpecialResourcesBehavior.cs:93-108` — where the storage reset must be added (BEFORE `InitializeHero`):

```csharp
    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        var hero = Hero.MainHero;
        if (hero == null) return;

        // ... existing comment ...
        _service.ResetSessionState();

        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _service.InitializeHero(hero.StringId, kingdomId, cultureId);
        _isFirstTickAfterLoad = true;
        _logger.LogInfo($"SpecialResources: Initialized resource for {hero.Name}");
    }
```

### Repo conventions that bind this change

- **ADR-002 thin entry points**: `CampaignBehaviorBase` subclasses delegate to services; they hold no business logic. The new-game handler here is a one-line delegation (`_dataService.RestoreData(null)` / `_storage.RestoreData(null)`) — that is the correct shape, not a logic block.
- **`.claude/rules/tests.md` — TDD mandatory** (RED→GREEN→REFACTOR, a Critical Rule). Naming: `MethodName_StateUnderTest_ExpectedBehavior`. Framework: MSTest (`[TestClass]`/`[TestMethod]`/`[TestInitialize]`) + NSubstitute (`Substitute.For<T>()`, `.Returns()`, `.Received()`). **No Moq.**
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` — do NOT edit. The IoC registrations and the `AddBehavior` wiring are already in place; this plan only changes feature-folder service/behavior bodies + tests.

### Engine facts the executor must NOT re-derive

- **`CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, handler)`** with signature `void handler(CampaignGameStarter starter)` is the established new-game hook in this codebase — `SpecialResourcesBehavior.cs:51` already uses it. Use the SAME registration call and the SAME handler signature in `CareerPersistenceBehavior`. Do not invent a different event.
- **Order safety (CORRECTNESS-01 risk note)**: `OnNewGameCreatedEvent` fires before the CC character-creation stages initialize career data, so a reset there cannot wipe the CC-selected career. This is the documented dispatch order; do NOT add the reset to a later event (e.g. `OnSessionLaunched`) as that could clobber a freshly-created career.
- **Save-compat**: NONE. Both fixes only clear *in-memory* state at the campaign boundary and add NO new SyncData fields / SaveableTypeDefiner. Do not add a save field.
- `RestoreData(null)` is already null-safe in BOTH services (coalesces to a new empty dict — see excerpts above), so the reset call is simply `RestoreData(null)`. You do NOT need to add a new interface method.

## Commands you will need

| Purpose | Command | Expected on success |
|---------|---------|---------------------|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | exit 0, 0 errors |
| Tests (all) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true` | all pass |
| Tests (narrow, faster iteration) | `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~CareerPersistence|FullyQualifiedName~SpecialResource"` | the targeted tests pass |

`-p:DisableModuleCopy=true` is REQUIRED on build AND test — the tests project builds Main, whose post-build target otherwise deploys to the game install. **NEVER run `./build.ps1`** from an executor (same deploy, and it must not run concurrently).

## Scope

**In scope** (the only files you should modify):
- `Main/Features/CareerSystem/CareerPersistenceBehavior.cs`
- `Main/Features/SpecialResources/SpecialResourcesBehavior.cs`
- `Main/Features/SpecialResources/SpecialResourceService.cs` (only IF you choose to fold the storage clear into `ResetSessionState` — see Step 5 note; the default approach does NOT touch this file)
- `TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs`
- `TAOM.Tests/Features/SpecialResources/SpecialResourceServiceTests.cs` (or a new `SpecialResourcesBehaviorTests.cs` — see Test plan)

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`, `Main/SubModule.cs`, `Main/Features/CareerSystem/CareerSystemIoC.cs`, `Main/Features/SpecialResources/` IoC file — single-owner / registration files. If a registration change seems needed, STOP and report the exact line.
- `Main/Features/CareerSystem/CareerDataService.cs` and `ICareerDataService.cs` — `RestoreData(null)` already does what's needed; do NOT add a new interface method.
- `Main/Features/SpecialResources/SpecialResourceStorageService.cs` and `ISpecialResourceStorageService.cs` — `RestoreData(null)` already coalesces to empty; do NOT add a new method.
- `Main/Features/CareerSystem/CareerCreationHandler.cs` — the harvest "belt-and-braces" `ClearCareer-before-SetCareer` suggestion is explicitly DEFERRED (see Maintenance notes); do not add it.
- Any save-format change (new SyncData fields / SaveableTypeDefiner).

## Git workflow

- Branch: work in the dispatched worktree's branch; do NOT push or open a PR.
- Commit (50/72, imperative, no AI attribution), e.g.:
  `fix(career,specres): reset singleton state on new-campaign boundary`
  Suggested trailers:
  `Save-compat: in-memory only — no new save fields`
  `Not-tested: live OnNewGameCreatedEvent dispatch (requires running game)`

## Steps

### Step 1: Failing test — CareerSystem resets on new campaign (RED)

In `TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs`, add a test that proves a fresh campaign does NOT inherit prior career data. The existing test class already constructs the behavior with a real `CareerDataService` and a substitute logger (`Setup()` at lines 24-29 — `_dataService` + `_behavior`).

The behavior currently has no public reset entry point, so the test drives the new-game handler directly. Add a `public` method (Step 3) named `OnNewGameCreated(CampaignGameStarter starter)` and have the test call it with `null` (the handler must not dereference `starter` — it only clears the dict). Model the assertion style on `CareerDataServiceTests.RestoreData_ReplacesExistingState` (`CareerDataServiceTests.cs:207-220`).

```csharp
    [TestMethod]
    public void OnNewGameCreated_AfterPriorCampaignData_ClearsCareerData()
    {
        // Arrange: simulate campaign A leaving career data in the singleton.
        _dataService.SetCareer("main_hero", "captain_of_osgiliath");
        _dataService.TryAddChoice("main_hero", "co_root", 10);
        _dataService.UnlockTier("main_hero", 2);
        _dataService.SetFlag("main_hero", "captain_proven");
        _dataService.SetCareer("lord_1_1", "black_uruk_captain");

        // Act: a new campaign (B) starts in the same process.
        _behavior.OnNewGameCreated(null);

        // Assert: campaign A's data is gone — clean slate for B.
        Assert.IsFalse(_dataService.HasCareer("main_hero"));
        Assert.IsFalse(_dataService.HasCareer("lord_1_1"));
        Assert.AreEqual(0, _dataService.GetChoiceCount("main_hero"));
        Assert.IsFalse(_dataService.IsTierUnlocked("main_hero", 2));
        Assert.IsFalse(_dataService.HasFlag("main_hero", "captain_proven"));
    }
```

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0 will FAIL at this point because `OnNewGameCreated` does not exist yet. That compile failure IS the RED state. (Do not run tests yet — the test won't compile until Step 3.) Proceed to Step 3 to make it compile + pass; Step 2 is the SpecialResources RED.

### Step 2: Failing test — SpecialResources storage clears on new campaign (RED)

The cleanest unit under test for storage clearing is `SpecialResourcesBehavior.OnNewGameCreated`. There is currently **no** `SpecialResourcesBehaviorTests.cs`. Create one (mirror the substitute setup from `SpecialResourceServiceTests.cs:35-42` — it already wires `ISpecialResourceConfigProvider`, `ISpecialResourceStorageService`, `IModLogger`, and `ICareerPassiveService` as substitutes; the behavior needs `ISpecialResourceService`, `ISpecialResourceStorageService`, `ISpecialResourceConfigProvider`, `IModLogger`).

The behavior's `OnNewGameCreated` is currently `private` (line 93) — make it `public` in Step 4 so the test can call it. Assert that the storage's bulk-replace was invoked with an empty/null dict.

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.SpecialResources;

namespace TAOM.Tests.Features.SpecialResources;

[TestClass]
public class SpecialResourcesBehaviorTests
{
    private ISpecialResourceService _service;
    private ISpecialResourceStorageService _storage;
    private ISpecialResourceConfigProvider _config;
    private IModLogger _logger;
    private SpecialResourcesBehavior _behavior;

    [TestInitialize]
    public void Setup()
    {
        _service = Substitute.For<ISpecialResourceService>();
        _storage = Substitute.For<ISpecialResourceStorageService>();
        _config = Substitute.For<ISpecialResourceConfigProvider>();
        _logger = Substitute.For<IModLogger>();
        _behavior = new SpecialResourcesBehavior(_service, _storage, _config, _logger);
    }

    [TestMethod]
    public void OnNewGameCreated_NoMainHero_DoesNothing()
    {
        // Hero.MainHero is null outside a campaign — handler must early-return without touching storage.
        _behavior.OnNewGameCreated(null);
        _storage.DidNotReceive().RestoreData(Arg.Any<System.Collections.Generic.Dictionary<string, float>>());
    }
}
```

NOTE: `OnNewGameCreated` reads `Hero.MainHero` (a static engine property, null in the test harness). The early `if (hero == null) return;` at line 96 means the **storage clear must be placed BEFORE that guard** if you want it to run regardless — OR the assertion is on the no-hero early-return path as written above. **Decision (load-bearing): place `_storage.RestoreData(null)` at the very TOP of `OnNewGameCreated`, before the `Hero.MainHero` read**, so the clear is unconditional (a new campaign must always wipe prior storage even in edge cases where `Hero.MainHero` is briefly null). Then change the test to assert `_storage.Received(1).RestoreData(Arg.Is<...>(d => d == null || d.Count == 0));` and rename it `OnNewGameCreated_ClearsStorage`. Pick the unconditional placement; write the test to match.

**Verify**: build will FAIL (method is `private` / test references it) — that is RED. Make it pass in Step 4.

### Step 3: Implement CareerSystem reset (GREEN)

In `Main/Features/CareerSystem/CareerPersistenceBehavior.cs`:

1. Add the `using TaleWorlds.CampaignSystem;` (already present — line 3) covers `CampaignEvents` / `CampaignGameStarter` / `IDataStore`.
2. Wire the event in `RegisterEvents()` and add a `public` handler:

```csharp
    public override void RegisterEvents()
    {
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, OnNewGameCreated);
    }

    // A second campaign in the same process must not inherit the prior campaign's career
    // data. ICareerDataService is Reuse.Singleton (CareerSystemIoC.cs:16) so its dict survives
    // campaign teardown; a new game never invokes the SyncData load branch that would replace
    // it. OnNewGameCreatedEvent fires before CC initializes the new career, so this reset is
    // safe (does not wipe the CC-selected career). Public for unit-test reach.
    public void OnNewGameCreated(CampaignGameStarter starter)
    {
        _dataService.RestoreData(null); // null → fresh empty dict (CareerDataService.cs:105)
        _logger.LogInfo("CareerSystem: new campaign — cleared singleton career data");
    }
```

`OnNewGameCreated` must NOT dereference `starter` (the test passes `null`). It must NOT read `Hero.MainHero`.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0. Then `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~CareerPersistence"` → the Step 1 test passes (GREEN), existing `CareerPersistenceTests` still pass.

### Step 4: Implement SpecialResources storage reset (GREEN)

In `Main/Features/SpecialResources/SpecialResourcesBehavior.cs`:

1. Change `private void OnNewGameCreated(...)` (line 93) to `public void OnNewGameCreated(...)`.
2. Add `_storage.RestoreData(null);` as the FIRST statement (before `var hero = Hero.MainHero;`), with a comment:

```csharp
    public void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A second campaign in the same process must not inherit the prior campaign's per-(hero,
        // resource) balances. ISpecialResourceStorageService is Reuse.Singleton; ResetSessionState
        // (below) only clears the service's session state, NOT the storage dict. Clear it first,
        // unconditionally, so even a transient null Hero.MainHero can't skip the wipe.
        _storage.RestoreData(null); // null → fresh empty dict (SpecialResourceStorageService.cs:36)

        var hero = Hero.MainHero;
        if (hero == null) return;

        _service.ResetSessionState();

        GetHeroIds(hero, out var kingdomId, out var cultureId);
        _service.InitializeHero(hero.StringId, kingdomId, cultureId);
        _isFirstTickAfterLoad = true;
        _logger.LogInfo($"SpecialResources: Initialized resource for {hero.Name}");
    }
```

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0. Then `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~SpecialResource"` → the Step 2 test passes (GREEN), existing `SpecialResourceServiceTests` still pass.

### Step 5: Harden the SyncData load path so an absent save key yields empty storage (GREEN)

CORRECTNESS-02's secondary failure: `SpecialResourcesBehavior.SyncData` (lines 79-91) passes the **live** `_storage.GetAllData()` dict as the `ref` on load. If the save lacks the `_taom_specialResources` key (a save predating the feature, or a corrupt entry), the engine leaves the `ref` untouched and `RestoreData(data)` re-installs the live (prior-campaign) dict. Fix by using a **null local on the load branch only** — the established pattern in `Main/Features/Messengers/MessengerCampaignBehavior.cs:76-79`:

```csharp
    public override void SyncData(IDataStore dataStore)
    {
        _logger.LogInfo("[SpecRes] SyncData called (save/load)");
        if (dataStore.IsSaving)
        {
            var data = _storage.GetAllData();
            dataStore.SyncData("_taom_specialResources", ref data);
        }
        else
        {
            System.Collections.Generic.Dictionary<string, float> data = null;
            dataStore.SyncData("_taom_specialResources", ref data);
            _storage.RestoreData(data); // null/absent key → fresh empty dict, not prior campaign's
        }
        _logger.LogInfo("[SpecRes] SyncData complete");
        // Phase 9b #133 P1 — per-resource cap belongs inside RestoreData/Set, not here (see prior comment).
    }
```

Preserve the existing trailing `// Phase 9b #133 P1` comment intent (lines 86-90) — it documents why no `ClampAll` runs here. Do NOT re-add a `ClampAll` call.

Add a test (in `SpecialResourcesBehaviorTests.cs`) covering the absent-key load: configure the `IDataStore` substitute so `IsSaving` is false and the `ref` is left unset, then assert `_storage.Received().RestoreData(Arg.Is<...>(d => d == null || d.Count == 0))`. If mocking the `ref`-leaving-untouched behavior proves too awkward with NSubstitute, STOP and report — do NOT contort the production code to be testable; the load-branch null-local change is the load-bearing fix and is covered structurally.

> **Alternative considered (do NOT take unless the orchestrator directs)**: folding `_storage.RestoreData(null)` into `SpecialResourceService.ResetSessionState()` instead of the behavior. Rejected because `ResetSessionState` lives in `SpecialResourceService`, which does not own the storage dict (it holds an `ISpecialResourceStorageService` reference); clearing storage from there couples the session-state reset to storage and is harder to unit-test in isolation. Keep the clear in the behavior.

**Verify**: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0; `dotnet test TAOM.Tests -p:DisableModuleCopy=true --filter "FullyQualifiedName~SpecialResource"` → all pass.

### Step 6: Full suite + done check

**Verify**:
- `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` → exit 0
- `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass (existing count + the 2-3 new tests)
- `git status` → only the in-scope files changed.

## Test plan

- **New tests:**
  - `TAOM.Tests/Features/CareerSystem/CareerPersistenceTests.cs` → `OnNewGameCreated_AfterPriorCampaignData_ClearsCareerData` (the regression: prior campaign's career/choices/tiers/flags + a second hero are all gone). Structural pattern: the existing `CareerPersistenceTests.Setup()` (real `CareerDataService` + substitute logger) and `CareerDataServiceTests.RestoreData_ReplacesExistingState`.
  - `TAOM.Tests/Features/SpecialResources/SpecialResourcesBehaviorTests.cs` (new file) → `OnNewGameCreated_ClearsStorage` (asserts `_storage.Received(1).RestoreData(empty-or-null)`), and the SyncData absent-key load test from Step 5 if it's cleanly mockable. Substitute setup pattern: `SpecialResourceServiceTests.cs:35-42`.
- **Structurally untestable** (name for the commit's `Not-tested:` trailer): the live `CampaignEvents.OnNewGameCreatedEvent` registration + dispatch (requires a running game) — the tests invoke the public handler directly instead.
- **Verification**: `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass, including the 2-3 new tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true` exits 0; the new CareerSystem + SpecialResources reset tests exist and pass
- [ ] `CareerPersistenceBehavior.RegisterEvents()` registers `OnNewGameCreatedEvent` (no longer an empty body)
- [ ] `SpecialResourcesBehavior.OnNewGameCreated` calls `_storage.RestoreData(null)` before reading `Hero.MainHero`
- [ ] `SpecialResourcesBehavior.SyncData` uses a null local on the load branch (grep: `Dictionary<string, float> data = null` appears in the file)
- [ ] No files outside the in-scope list are modified (`git status`)
- [ ] `plans/README.md` status row for plan 001 updated

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the locations in "Current state" doesn't match the excerpts (drift since `141b749`) — especially if `CareerPersistenceBehavior.RegisterEvents()` is no longer empty, or `SpecialResourcesBehavior.OnNewGameCreated` already clears storage.
- A step's verification fails twice after a reasonable fix attempt.
- The fix appears to require touching an out-of-scope file (`IoC.cs` / `SubModule.cs` / either feature's IoC registration / `CareerDataService.cs` / `SpecialResourceStorageService.cs` interface).
- `CampaignEvents.OnNewGameCreatedEvent` / `AddNonSerializedListener` does not have the signature documented in "Engine facts" (i.e. the sibling `SpecialResourcesBehavior.cs:51` registration no longer compiles the same way) — report the mismatch, do not decompile-and-improvise.
- The Step 5 absent-key SyncData test cannot be written cleanly with NSubstitute — report it; do NOT distort production code for testability (ship the load-branch null-local fix as structurally-covered).
- You discover the assumption "`RestoreData(null)` coalesces to an empty dict" is false in either service (it is true in both as of `141b749` — re-read if drift is suspected).

## Maintenance notes

For the human/agent who owns this code after the change lands:

- **What interacts with this**: any future per-hero singleton state added to either feature (e.g. a new cache in `CareerDataService` or `SpecialResourceStorageService`) MUST also be cleared in the same `OnNewGameCreated` handler — the singleton/lifetime-asymmetry trap (`feedback_singleton_controller_per_mission_behavior_lifetime_asymmetry.md`) recurs whenever new singleton state is added without a campaign-boundary reset. `SpecialResourceService.ResetSessionState()` already covers `_pendingSpend`/`_inSession`/`_loggedResolveKeys`; storage is now covered by the behavior.
- **What `/deep-review` should probe** (the orchestrator runs it before commit since this touches ≥2 C# files): (1) that the CareerSystem reset cannot wipe a freshly-CC-created career — confirm `OnNewGameCreatedEvent` ordering vs CC stages is preserved; (2) that `SpecialResources` storage clear is BEFORE the `Hero.MainHero` null guard (unconditional); (3) that the SyncData null-local change didn't drop the per-resource-cap comment or accidentally re-introduce a `ClampAll`.
- **Explicitly deferred (NOT in this plan)**: the harvest's belt-and-braces `ClearCareer-before-SetCareer` in `CareerCreationHandler.cs:39-46`. With the new-game reset in place, CC always runs against a clean dict, so the defensive clear is redundant. Defer until/unless a save-load-into-CC path is found that bypasses `OnNewGameCreated`. The harvest also flags a starter-change/`_justLoadedFromSave` reset (à la `CultureConversionBehavior.cs:93-107`) to cover loads where SyncData never fired — that is a SEPARATE, larger change (load-path reset, not new-game reset) and is OUT OF SCOPE here; raise it as a follow-up finding if cross-campaign *load* leakage is observed.
- **Related open findings** (do NOT fix here): CORRECTNESS-04/05/06 in `plans/_audit/2026-06-12-harvest.md` are separate TroopWeight + CareerSystem-mutation NaN/cache findings with their own plans.
