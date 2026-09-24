# Plan 014: Reset Enlistment's per-session clocks and caches on load and on a new campaign

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report; do not improvise. The orchestrator maintains `plans/README.md`
> for this run: do NOT edit it; report your status in your final message.
>
> **Where you work (read this twice).** All work happens in ONE worktree:
> `E:\repos\wt-014-enlistment-session-scope` (Git Bash form `E:/repos/wt-014-enlistment-session-scope`,
> called **W** below). The main checkout `E:\repos\TAOM` holds another session's uncommitted edits
> (to `Main/SubModule.cs`, `Main/IoC.cs`, `CHANGELOG.md`, `docs/ai-includes/orientation.md`,
> several `docs/reviews/lessons/*.md` files and many creature files); touching it can commit their
> work. Your shell's working directory resets to `E:\repos\TAOM` on EVERY call, so a bare relative
> path lands in the wrong tree. Therefore, without exception:
> - Every shell call starts with `cd E:/repos/wt-014-enlistment-session-scope && `.
> - Every git call is `git -C E:/repos/wt-014-enlistment-session-scope ...` (the only exceptions are
>   the drift check below and the Step 0 `worktree add`, which name `E:/repos/TAOM` on purpose and
>   write nothing into its files).
> - Every Read, Edit or Write path is absolute and starts with `E:\repos\wt-014-enlistment-session-scope\`.
>   Every repo-relative path in this plan (for example `Main/Features/Enlistment/ServiceAttachmentService.cs`)
>   means that path under W. Never edit a path under `E:\repos\TAOM\`.
> - If the orchestrator gave you a different worktree cut from `b2e387db`, substitute its absolute
>   path for W everywhere.
>
> **Shell**: run every command in this plan in **Git Bash (the Bash tool)**, not PowerShell.
>
> **Drift check (run first)**:
> `git -C E:/repos/TAOM diff --stat b2e387db..bannerlord-1.5.x -- Main/Features/Enlistment/ServiceAttachmentService.cs Main/Features/Enlistment/IServiceAttachmentService.cs Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs Main/Features/Enlistment/ServiceMaintenanceService.cs Main/Features/Enlistment/IServiceMaintenanceService.cs Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs Main/Features/Enlistment/EnlistmentReconciler.cs TAOM.Tests/Features/Enlistment docs/features/enlistment.md`
> Expected: no output (verified empty on 2026-09-23 against `4b5662b2`, the branch tip then). It
> compares commits only and writes nothing. You work on a branch cut from `b2e387db`, so the
> excerpts below are exact for your worktree. If the command prints any file, the trunk moved under
> this plan: finish on your branch anyway, but list those files in your final report so the merge
> can be planned. If an excerpt below does not match YOUR worktree, that is a STOP condition.

## Status

- **Priority**: P2
- **Effort**: S (four one-to-five-line production edits, one constructor gains two parameters, one
  new test file, three test files adjusted, comment and doc edits)
- **Risk**: LOW (every reset added here only nulls in-memory fields; nothing persisted, no save
  format change, no engine call)
- **Depends on**: none
- **Category**: bug
- **Planned at**: commit `b2e387db`, 2026-09-23
- **Issue**: create before implementation lands (orchestrator)

## Why this matters

Every Enlistment service is a DryIoc `Reuse.Singleton`, so it lives for the whole Bannerlord process,
not for one campaign. Three of them hold values keyed on the absolute campaign clock that nothing
clears when the player loads an earlier save or starts a new campaign in the same process: the
settlement-dwell anchor, the shore-leave offer's settlement id and 24-hour cooldown stamp, and the
per-hour army-rhythm snapshot. After a load of an earlier save, the old stamp sits in the future, and
the code reads a negative elapsed time as "a moment ago": the exit sweep keeps the player inside a
town the commander has already left (until the new clock passes the old stamp plus 6 hours), and the
leave-on-arrival popup stays silent until the new clock passes the old stamp plus a day, which can be
the rest of the playthrough. Worse, the feature's one reset method, `ServiceMaintenanceService.ResetSessionCaches`,
runs only on game load: a brand-new campaign never reaches it, so the cached commander party handle,
the stale-battle-latch anchor and the army handle it already clears also leak into campaign two. After
this plan, both lifecycle edges (load and new campaign) run one reset that clears every one of these,
and tests pin both paths.

## Current state

### Files and their roles (all read at `b2e387db`)

- `Main/Features/Enlistment/ServiceAttachmentService.cs`: owns the dwell anchor `_settlementEntryHours`
  (declared `:34`, written `:41`, read `:43-44`, cleared only on a service exit at `:231`). Has no reset.
- `Main/Features/Enlistment/IServiceAttachmentService.cs`: its interface (`:34` `IsWithinSettlementDwell`,
  `:37` `StampSettlementEntry`, `:55` `InvalidateCommanderCache`).
- `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs`: interface
  `IEnlistmentWaitMenuPresenter` (`:17-44`) and class (`:46`). Holds `_lastOfferedSettlementId` (`:64`)
  and `_lastOfferedAtHours` (`:67`), tested in `OfferTownLeave` (`:118-158`). Has no reset.
- `Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs`: caches one snapshot per campaign
  hour; its `Invalidate()` (`:12`, `:67-71`) has zero callers anywhere in `Main` or `TAOM.Tests`.
- `Main/Features/Enlistment/ServiceMaintenanceService.cs`: `ResetSessionCaches()` (`:217-241`) is the
  documented single reset point; its constructor is `:58-84`.
- `Main/Features/Enlistment/IServiceMaintenanceService.cs`: the reset's interface doc (`:50-55`).
- `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`: the lifecycle entry point (147 lines).
  `OnGameLoaded` (`:115-122`) calls the reset; `OnNewGameCreated` (`:124-130`) does not.
- `Main/Features/Enlistment/EnlistmentReconciler.cs`: consumes the dwell (`:626`, `:641`); its comments
  at `:49-52` and `:450-455` say a new campaign never reaches `ResetSessionCaches` (true today, false
  after this plan).
- `docs/features/enlistment.md`: `:1692-1699` says the same.
- Tests: `TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs` (the existing reset tests,
  `:100-164`), `TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs` (second constructor
  call site, `:49-53`), `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs` (DryIoc
  graph validation; Step 4 must add two substitutes to its `BuildContainer()`, see the next excerpt),
  `TAOM.Tests/Features/Enlistment/EnlistmentWaitMenuPresenterTests.cs` (the presenter test pattern).

### Excerpt: `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs:28-44` (why Step 4 edits it)

```csharp
    private static IContainer BuildContainer()
    {
        var container = new Container();

        // Cross-feature dependencies owned by other registration modules.
        container.RegisterInstance(Substitute.For<IModLogger>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<ICoopPresenceProvider>());
        // IPathService is registered by Main/IoC.cs, not by RegisterEnlistmentFeature. It entered
        // this graph when the status board started reading the promotion ladder and the wage table:
        // EnlistmentBattleBehavior -> IServiceMaintenanceService -> IServiceStatusService ->
        // IPromotionService / IEnlistmentContentConfigProvider -> IPathService.
        container.RegisterInstance(Substitute.For<IPathService>());

        EnlistmentIoC.RegisterEnlistmentFeature(container);
        return container;
    }
```

Its five tests `Validate` `EnlistmentBattleBehavior`, `IEnlistmentReconciler`, `EnlistmentBehavior`,
`IServiceMaintenanceService` and `IEnlistmentDeploymentService`. Today no root reaches the wait-menu
presenter. Step 4 makes `ServiceMaintenanceService` take `IEnlistmentWaitMenuPresenter`, and the
presenter's graph needs two services that `RegisterEnlistmentFeature` does not register:

- `EnlistmentWaitMenuPresenter(…, IEnlistmentDialogGateService gate, …)` (`Presentation/EnlistmentWaitMenuPresenter.cs:72-83`)
  -> `EnlistmentDialogGateService(…, IPlayerContextAdapter playerContext, …)` (`EnlistmentDialogGateService.cs:13-18`).
  `IPlayerContextAdapter` (namespace `TAOM.Adapters`) is registered only by
  `Main/Features/Siege/SiegeDefenseIoC.cs:12`.
- `EnlistmentWaitMenuPresenter(…, IEnlistmentPlayerActionService actions, …)` ->
  `EnlistmentPlayerActionService(…, IDutyOrchestrationService duties, …)` (`EnlistmentPlayerActionService.cs:20-28`).
  `IDutyOrchestrationService` (namespace `TAOM.Features.Enlistment.Duties`) is registered only by
  `Main/Features/Enlistment/Duties/DutiesIoC.cs:21`.

Production is unaffected: `Main/IoC.cs` calls `SiegeDefenseIoC.RegisterSiegeDefenseFeature` (`:129`),
`RegisterEnlistmentFeature` (`:178`) and `DutiesIoC.RegisterEnlistmentDutiesFeature` (`:179`) on the
same container. Without the two substitutes, three of the five tests
(`MaintenanceService_Resolvable_*`, `LifecycleBehavior_Resolvable_*`, `BattleBehavior_Resolvable_*`)
would fail after Step 4. `ArmyRhythmSnapshotService`'s own dependencies (`IEnlistmentStore`,
`IEnlistmentContentStore`, `IArmyRhythmProbeAdapter`) are all registered by `EnlistmentIoC.cs`
(`:20`, `:74`, `:69`). The precedent for naming a cross-module type with `global::` inside this test
namespace is `DischargeConsequenceServiceTests.cs:347`.

### Excerpt: `Main/Features/Enlistment/ServiceAttachmentService.cs:29-44`

```csharp
    public const double SettlementDwellHours = 6.0;

    public event System.Action<string> ColumnEnteredSettlement;

    /// <summary>Campaign hour of the last successful placement. Session state; see IsWithinSettlementDwell.</summary>
    private double? _settlementEntryHours;

    /// <summary>
    /// Set by the follow path so the dwell can be measured. Taken as a parameter rather than read
    /// from CampaignTime here, because this service is pure of engine clocks and its tests depend
    /// on that.
    /// </summary>
    public void StampSettlementEntry(double nowHours) => _settlementEntryHours = nowHours;

    public bool IsWithinSettlementDwell(double nowHours) =>
        _settlementEntryHours.HasValue && nowHours - _settlementEntryHours.Value < SettlementDwellHours;
```

And the only other writer, `:223-231`:

```csharp
    public bool ExitSettlementForService(string commanderHeroId)
    {
        if (!_attachment.LeaveSettlement())
        {
            _logger?.LogError("[EnlistDiag] EXIT failed — the player is stuck inside a settlement the commander has left");
            return false;
        }

        _settlementEntryHours = null;
```

### Excerpt: `Main/Features/Enlistment/IServiceAttachmentService.cs:25-37`

```csharp
    /// <summary>
    /// True while the player is inside a settlement we placed them in less than
    /// <see cref="ServiceAttachmentService.SettlementDwellHours"/> campaign hours ago.
    ///
    /// Exists to stop the strobe. Measured 2026-08-25: an AI lord dips into a town for under an
    /// hour of campaign time, and following him in and straight back out produced ten transitions
    /// across three towns in three real minutes, median 2.5 seconds inside, twice entering and
    /// leaving within the SAME second. Nothing is usable in that window.
    /// </summary>
    bool IsWithinSettlementDwell(double nowHours);

    /// <summary>Record when a placement happened, so the dwell above can be measured from it.</summary>
    void StampSettlementEntry(double nowHours);
```

### Excerpt: `Main/Features/Enlistment/EnlistmentReconciler.cs:624-645` (the consumer)

```csharp
            case AttachmentStatus.SettlementFollowRequired:
                if (_attachment.FollowCommanderIntoSettlement(record.CommanderHeroId, snapshot.SettlementId))
                    _attachment.StampSettlementEntry(nowDays * 24.0);
                return;

            case AttachmentStatus.SettlementExitRequired:
                ...
                if (!snapshot.PartyIsInMapEvent && _attachment.IsWithinSettlementDwell(nowDays * 24.0))
                {
                    if (_diag?.IsEnabled == true)
                        _logger?.LogInfo($"[EnlistDiag] EXIT deferred — holding '{presence.SettlementId}' for the minimum dwell (commander is in '{snapshot.SettlementId ?? "the field"}')");
                    return;
```

`nowDays` is `CampaignTime.Now.ToDays` (`Hooks/EnlistmentBehavior.cs:109`).

### Excerpt: `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs`

`:39-44` (last member of the interface):

```csharp
    /// <summary>
    /// The column has stopped somewhere: offer the pass while it is actually stopped. Idempotent
    /// per settlement stop, so a re-follow does not re-ask.
    /// </summary>
    void OfferTownLeave(string settlementId, double nowHours);
}
```

`:60-70` (the latch fields):

```csharp
    /// <summary>
    /// Which settlement we last offered a pass for. Session state, never persisted: see
    /// <see cref="OfferTownLeave"/>.
    /// </summary>
    private string _lastOfferedSettlementId;

    /// <summary>Campaign hour of the last offer, so a cycling commander cannot spam the modal.</summary>
    private double? _lastOfferedAtHours;

    /// <summary>Minimum campaign hours between offers. One in-game day.</summary>
    private const double OfferCooldownHours = 24.0;
```

`:118-136` (the test that suppresses the offer; note the author's own comment at `:123-124` expects a
reload to re-arm it, which the singleton lifetime defeats):

```csharp
    public void OfferTownLeave(string settlementId, double nowHours)
    {
        if (!_coopSession.IsAuthority)
            return;

        // Once per stop. In-memory ON PURPOSE: a save/reload mid-stop re-asking is harmless and
        // self-healing, whereas persisting it would buy save-compat surface for nothing.
        if (string.IsNullOrEmpty(settlementId) || settlementId == _lastOfferedSettlementId)
            return;

        // AND a cooldown, ...
        if (_lastOfferedAtHours.HasValue && nowHours - _lastOfferedAtHours.Value < OfferCooldownHours)
            return;

        _lastOfferedSettlementId = settlementId;
        _lastOfferedAtHours = nowHours;
```

The method ends at `:158` with `_logger?.LogInfo($"[Enlistment] offered shore leave on arrival at '{settlementId}'");`
then `}`; `public void TakeTownLeave()` starts at `:160`. The caller is
`Hooks/EnlistmentMenuBehavior.cs:74-75`: `_presenter.OfferTownLeave(settlementId, CampaignTime.Now.ToHours);`.

### Excerpt: `Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs:7-13` and `:67-71`

```csharp
public interface IArmyRhythmSnapshotService
{
    /// <summary>Snapshot for the given campaign time; cached per game hour (one probe per hour, 11 donor call sites shared it).</summary>
    ArmyRhythmSnapshot GetSnapshot(double nowDays, double hourOfDay);

    void Invalidate();
}
```

```csharp
    public void Invalidate()
    {
        _cached = null;
        _cachedHourStamp = double.MinValue;
    }
```

`GetSnapshot` (`:34-65`) returns `_cached` when `Math.Floor(nowDays * 24.0) == _cachedHourStamp`, else
calls `_probe.Probe(_store.Record.CommanderHeroId)` and caches. Constructor (`:24-32`):
`ArmyRhythmSnapshotService(IEnlistmentStore store, IEnlistmentContentStore contentStore, IArmyRhythmProbeAdapter probe)`.
Proof `Invalidate` is dead: `git grep -n "Invalidate()" b2e387db -- Main/Features/Enlistment/Content TAOM.Tests`
prints 5 lines: the two definitions (`ArmyRhythmSnapshotService.cs:12` and `:67`) and three hits that
belong to other types (`ServiceMaintenanceServiceTests.cs:122` is `_status.Received(1).Invalidate();`,
`ServiceStatusServiceTests.cs:320` is `_sut.Invalidate();`, `MarriageClanPoolStampTests.cs:40` is a
test name containing `DoesNotInvalidate()`). None calls the rhythm service; the three consumers (`EnlistmentDailyService.cs:211`,
`DutyOrchestrationService.cs:75,133`) call `GetSnapshot` only. Consequence today: a quick reload
inside the same campaign hour serves the pre-load snapshot (commander army, siege, trust-derived
`HighScrutiny`) to duty offers and the daily loop for up to one campaign hour.

### Excerpt: `Main/Features/Enlistment/ServiceMaintenanceService.cs:39-84` (fields and constructor)

```csharp
    private readonly IEnlistmentStore _store;
    private readonly IEnlistmentStateMachine _machine;
    private readonly IServiceAttachmentService _attachment;
    private readonly ICommanderLordAdapter _commander;
    private readonly IGameMenuAdapter _gameMenu;
    private readonly IEnlistmentMenuService _menuService;
    private readonly IServiceStatusService _status;
    private readonly IArmyMembershipAdapter _army;
    private readonly IEncounterAdapter _encounter;
    private readonly IEncounterOwnershipPolicy _ownership;
    private readonly IEnlistmentReconciler _reconciler;
    private readonly IModLogger _logger;
    ...
    public ServiceMaintenanceService(
        IEnlistmentStore store,
        IEnlistmentStateMachine machine,
        IServiceAttachmentService attachment,
        ICommanderLordAdapter commander,
        IGameMenuAdapter gameMenu,
        IEnlistmentMenuService menuService,
        IServiceStatusService status,
        IArmyMembershipAdapter army,
        IEncounterAdapter encounter,
        IEncounterOwnershipPolicy ownership,
        IEnlistmentReconciler reconciler,
        IModLogger logger)
    {
        ...
        _reconciler = reconciler;
        _logger = logger;
    }
```

The file's namespace is `TAOM.Features.Enlistment` (file-scoped, `:6`); usings are `System`,
`TAOM.Adapters`, `TAOM.Core.Logging`, `TAOM.Features.Enlistment.Domain`.

### Excerpt: `Main/Features/Enlistment/ServiceMaintenanceService.cs:206-241` (the reset)

```csharp
    /// <summary>
    /// Drop per-session caches. MUST be called on game load and session launch: the cached party
    /// id is matched by StringId, and lord-party ids are identical across a reload of the same
    /// campaign — so a stale handle from a destroyed campaign HITS the cache test and the cheap
    /// position sync then drives the player from a dead party's position at frame rate.
    ///
    /// This method is the ONE place that knows the lifetime of the feature's per-session state, so
    /// collaborators' caches are dropped from here too rather than each being wired separately into
    /// the load hook — the same reason <c>_attachment.InvalidateCommanderCache()</c> is called here
    /// and not from <c>EnlistmentBehavior</c>.
    /// </summary>
    public void ResetSessionCaches()
    {
        _cachedCommanderPartyId = null;
        _lastJoinRequestedForMapEventToken = 0;
        _menuFailures = 0;
        _budget = 0f;
        _statusBudget = 0f;
        _attachment.InvalidateCommanderCache();
        _status?.Invalidate();

        // The army adapter holds a live Army REFERENCE ...
        _army?.ResetSessionCaches();

        // The reconciler is a singleton too, and its stale-battle-latch anchor is an absolute
        // campaign day (#551). ...
        _reconciler?.ResetForNewSession();
    }
```

(Lines `:227-232` and `:235-239` are comment blocks, elided above.) Every callee is a pure field
clear, verified at `b2e387db`: `MobilePartyAttachmentAdapter.InvalidateCommanderCache` is
`=> _cachedCommanderParty = null;` (`Main/Adapters/MobilePartyAttachmentAdapter.cs:208`);
`ServiceStatusService.Invalidate` is `=> _last = null;` (`ServiceStatusService.cs:148`);
`ArmyMembershipAdapter.ResetSessionCaches` sets `_createdArmy = null;` (`Main/Adapters/ArmyMembershipAdapter.cs:147-153`);
`EnlistmentReconciler.ResetForNewSession` is `=> _staleBattleLatchSinceDays = double.NaN;`
(`EnlistmentReconciler.cs:101`). So the reset touches no engine object and is safe on any peer and
at any lifecycle point.

### Excerpt: `Main/Features/Enlistment/IServiceMaintenanceService.cs:50-55`

```csharp
    /// <summary>
    /// Drop every per-session cache. Call on game load and session launch — a commander-party
    /// handle cached from a previous campaign matches by StringId and would drive the position
    /// sync from a destroyed party.
    /// </summary>
    void ResetSessionCaches();
```

### Excerpt: `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs:112-130` (the hole)

```csharp
    // CO-OP: host-only. Normalization discharges/parks — world mutations a client must
    // not apply; the client loads the host's already-normalized record.
    // internal for TAOM.Tests (InternalsVisibleTo).
    internal void OnGameLoaded(CampaignGameStarter starter)
    {
        if (!_coopSession.IsAuthority) return;

        // BEFORE normalizing — it owns every per-session cache (stale commander id, stale Army handle).
        _maintenance.ResetSessionCaches();
        _normalizer.Normalize(_playerParty.GetMainHeroId(), CampaignTime.Now.ToDays);
    }

    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A brand-new campaign starts with no service record. SyncData(IsLoading) has NOT
        // run here, so _justLoadedFromSave is false and clearing is correct.
        if (!_justLoadedFromSave)
            _store.Clear();
    }
```

Constructor (`:32-50`): `EnlistmentBehavior(IEnlistmentStore store, IEnlistmentStateMachine machine,
IEnlistmentReconciler reconciler, IEnlistmentLoadNormalizer normalizer, IPlayerPartyAdapter playerParty,
ICoopSessionProvider coopSession, IServiceMaintenanceService maintenance, IModLogger logger)`.
`IPlayerPartyAdapter` is in `TAOM.Adapters`; `ICoopSessionProvider` in `TAOM.Features.CoopInterop`.
The file is exactly 147 lines at `b2e387db` (`git show b2e387db:Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs | wc -l`).

### Excerpt: `Main/Features/Enlistment/EnlistmentReconciler.cs:49-52` and `:450-455` (comments that become false)

```csharp
    /// the code meant to be the safety net. Two independent guards, because they cover different
    /// paths: <see cref="ResetForNewSession"/> handles the load path, and the backwards-clock
    /// re-anchor in <see cref="BreakStaleBattleLatch"/> handles a brand-new campaign, which never
    /// reaches <c>ResetSessionCaches</c> at all.
```

```csharp
        // Re-anchor on no anchor, and equally on an anchor in the FUTURE. A clock that ran backwards
        // cannot be a continuous episode; it means a different campaign or an earlier save, and the
        // anchor belongs to a world this one has nothing to do with. This is the guard for the path
        // ResetForNewSession does not reach: ResetSessionCaches is wired to OnGameLoaded only, so a
        // brand-new campaign in the same process never calls it, and a new campaign's low day count
        // puts the leftover anchor ahead of it.
```

(Lines `:450-455`, verbatim. Step 7 replaces only `:452-455`.)

### Excerpt: `docs/features/enlistment.md:1692-1699`

```text
by the safety net written to prevent it. Two guards, because they cover different paths.
`IEnlistmentReconciler.ResetForNewSession` covers the load path, dropped from
`ServiceMaintenanceService.ResetSessionCaches` (the feature's one place that knows this lifetime,
which is also why the army handle is dropped there rather than from the load hook). A backwards-clock
re-anchor inside `BreakStaleBattleLatch` covers a brand-new campaign, which never reaches
`ResetSessionCaches` at all because it is wired to `OnGameLoaded` only: a new campaign starts at a low
day count, so the leftover anchor sits in its future, and a clock that ran backwards cannot be one
continuous episode. Found by the `/deep-review` data-flow agent, not by the tests, which all passed.
```

Line `:1700` is blank and `:1701` is `### The engine backstop, and the bundle that was suppressed`.

### Engine facts (v1.5.3, from `pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.Campaign`, cache file `C:\Users\mikew\.taom-src\v1.5.3\TaleWorlds.CampaignSystem.Campaign.cs`)

`Campaign.DoLoadingForGameType`, state `PostInitializeFourthState` (`:1675-1712`):

```csharp
			if (_gameLoadingType == GameLoadingType.SavedCampaign)
			{
				...
				OnGameLoaded(gameStarter);
				OnSessionStart(gameStarter);
				...
			}
			else if (_gameLoadingType == GameLoadingType.NewCampaign)
			{
				...
				OnNewGameCreated(gameStarter);
				OnSessionStart(gameStarter);
```

So a load fires `OnGameLoadedEvent` then `OnSessionLaunchedEvent`; a new campaign fires
`OnNewGameCreatedEvent` then `OnSessionLaunchedEvent`; never both `OnGameLoaded` and `OnNewGameCreated`.
Both fire before the first campaign tick. `CampaignEvents.cs:861` declares
`public static IMbEvent<CampaignGameStarter> OnNewGameCreatedEvent`, dispatched at `:2098-2100`
(`Instance._onNewGameCreatedEvent.Invoke(campaignGameStarter);`). `CampaignBehaviorBase` has a public
parameterless constructor (`TaleWorlds.CampaignSystem.CampaignBehaviorBase.cs:12-15`, it only sets
`StringId = GetType().Name`), so `EnlistmentBehavior` can be constructed in a unit test; the
precedent is `TAOM.Tests/Features/CoopInterop/CoopAuthorityGateTests.cs:223-231`, which calls
`sut.OnNewGameCreated(null)` on `CastleRecruitmentBehavior`.

### Conventions that bind this change

- **ADR-002 (thin entry points)**: CampaignBehaviors only route events to services; the house limit
  is under 150 lines. `EnlistmentBehavior.cs` is 147 lines now, so this plan adds at most 2 lines to
  it (Done criteria checks `<= 149`).
- **ADR-007 (adapters)**: services never touch TaleWorlds types. Nothing here adds an engine call;
  `ServiceAttachmentService` stays "pure of engine clocks" (its `:37-39` comment): the reset takes no
  clock at all.
- **ADR-008 (testability)**: services are 100% unit-testable through constructor injection, with no
  static TaleWorlds calls (`CampaignTime.Now`, `Campaign.Current`, `Hero.MainHero`). Every new member
  here is tested directly.
- **`.claude/rules/csharp-architecture.md:96-114`, "Singleton Services Holding Per-Campaign State MUST
  Have a Session-Reset Story"**: every singleton holding per-campaign state (explicitly "any absolute
  clock, latch or shown-flag") exposes `ResetForNewSession()`; the behavior calls it "from
  `OnSessionLaunched` when [SyncData] did not [load]", `LoadFrom` clears transients, and both paths
  get tests named `*SessionResetTests`. This plan applies that rule to Enlistment with one deliberate
  departure: it wires the new-campaign reset from `OnNewGameCreated`, not from `OnSessionLaunched`
  with a did-not-load flag. For a new campaign the engine fires `OnNewGameCreated` and then
  `OnSessionStart`, both before the first tick (Engine facts above), so the effect is the same.
  `OnNewGameCreated` never fires on a load, so it needs no flag, and it is where
  `EnlistmentBehavior` already clears its store. The load path keeps its existing call in `OnGameLoaded`.
- **`ServiceMaintenanceService.ResetSessionCaches` is the feature's one reset point** (its own doc,
  `:212-215`): collaborator resets are called from it, never wired separately into a hook.
- **TDD**: RED (a failing test, here a compile failure naming the missing member) before GREEN.
- **No `#region`, no `[Obsolete]`, no `#if DEBUG`** (ADR-003/004/005). The rhythm rename below
  migrates every use in the same change (there are none), as ADR-004 requires.
- Test style: MSTest plus NSubstitute, file-scoped namespace `TAOM.Tests.Features.Enlistment`,
  pattern `TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs:115-164`. Inside that
  test namespace, a bare `Content.X` resolves to `TAOM.Tests.Features.Enlistment.Content` (a real test
  namespace), so tests reach production Content types through `using TAOM.Features.Enlistment.Content;`,
  never a `Content.` prefix (precedent: `EnlistmentDailyServiceTests.cs:1-8`).

### Decisions already taken (do NOT change them)

- **Scope is Enlistment only.** A repo-wide `ISessionScoped` interface with a dispatcher and a
  reflection ratchet test, and flipping the 22 singleton CampaignBehaviors to `Reuse.Transient`
  (audit finding COMP-02), are separate follow-ups (Maintenance notes). Do not start either.
- **The presenter and the rhythm service are reset from `ResetSessionCaches`**, by adding them as
  constructor dependencies of `ServiceMaintenanceService`. Not from `EnlistmentMenuBehavior` or
  `EnlistmentBehavior` directly (that would split the one reset point, and `EnlistmentBehavior` has no
  line budget left).
- **`ArmyRhythmSnapshotService.Invalidate()` is renamed to `ResetForNewSession()` and wired**, not
  deleted. It has zero callers, but the snapshot is per-campaign derived state on a singleton, which
  the rule above says must have a reset; the wiring costs one constructor parameter and one line.
- **`OnNewGameCreated` calls the reset unconditionally**: not gated on `_justLoadedFromSave` and not on
  `_coopSession.IsAuthority`. Every callee is a field clear (Current state), and the existing
  `_store.Clear()` in the same method is not authority-gated either. `OnGameLoaded` keeps its gate
  and its order unchanged.
- **No backwards-clock guards** are added to `IsWithinSettlementDwell` or `OfferTownLeave`. With the
  reset on both lifecycle edges no reachable path leaves a future stamp behind, and the simplicity
  rule rejects code for unreachable paths. The reconciler's existing backwards-clock re-anchor
  (`EnlistmentReconciler.cs:456`) stays exactly as is; only its comments change.
- **`OnGameLoaded` is not edited.** It already calls the reset before the normalizer.
- **`docs/features/enlistment.md:469-474`** (the historical "wait-menu guard" fix record that says
  "`ResetSessionCaches()` on game load") is history; leave it.

### Test baseline at `b2e387db` (so you do not chase known failures)

The orchestrator's run at `b2e387db`: 10,239 tests, 10,235 passed, 2 failed, 2 not executed. The 2
failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` (Elk tests) and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist` (`AnimaliaMountWiringTests`). Both read the
live, unversioned Armory install that another session is editing right now; neither is caused by or
related to this plan, so they may pass, fail, or change while you work. Do not investigate or fix
them. The 2 not executed are deliberate `[Ignore]`s (`WargAttackServiceTests.cs:349,372`). The rule
for every later full run: the failing test names must be a subset of those 2 Armory tests plus
whatever Step 0 recorded. The Subset check below makes that comparison mechanical.

**How `dotnet test` prints results** (SDK 10.0.401 on this machine): one summary line such as
`Failed!  - Failed:     2, Passed: 10235, Skipped:     2, Total: 10239, Duration: ...` or
`Passed!  - Failed:     0, Passed:     5, Skipped:     0, Total:     5, Duration: ...`. The counts
are space-padded, so wherever this plan says "`Failed: 0`" or "`Passed: 5`" it means the summary
line matches `grep -E "Failed:\s+0,"` or `grep -E "Passed:\s+5,"`; never grep the literal
`Failed: 0`. Each failing test also prints an indented line `  Failed <MethodName> [<time>]`.

## Commands you will need

Every command runs in Git Bash and starts with `cd E:/repos/wt-014-enlistment-session-scope && `.
Never run `./build.ps1`, never launch the game, never write under `E:\Steam\` or `E:\repos\TAOM\`.
Full-run logs go in **L** = `E:/repos/wt-014-logs` (created in Step 0), a plain folder outside every
repository, so they never show in `git status`.

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `cd E:/repos/wt-014-enlistment-session-scope && dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, `0 Error(s)` |
| Test (full) | `cd E:/repos/wt-014-enlistment-session-scope && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= > E:/repos/wt-014-logs/full.log 2>&1; grep -E "Total:\s+[0-9]+" E:/repos/wt-014-logs/full.log` | one summary line with `Total:` about 10,239 (plus the new tests). Call the Bash tool with `timeout: 600000` (or `run_in_background: true` and wait); the default 120 s is too short |
| Subset check (after a full run) | `grep -E "^\s+Failed [A-Za-z0-9_]+" E:/repos/wt-014-logs/full.log \| awk '{print $2}' \| sort -u > E:/repos/wt-014-logs/now.txt; cat E:/repos/wt-014-logs/now.txt; grep -vxF -f E:/repos/wt-014-logs/allowed.txt E:/repos/wt-014-logs/now.txt` | the last command prints nothing (every failure is allowed). If the summary's `Failed:` count is not 0 but `now.txt` is empty, the line format differs from the above: STOP |
| Test (filtered) | `cd E:/repos/wt-014-enlistment-session-scope && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~<ClassName>"` | `Failed: 0` (padded, see above); use `timeout: 600000` for the first run (it compiles) |
| RED compile check | the Test (filtered) command piped to `2>&1 \| grep -o "error CS[0-9]*: [^[]*" \| sort -u` | the error lines the step names, and no others |
| Data | `cd E:/repos/wt-014-enlistment-session-scope && python tools/validate_moduledata.py \| grep "error(s)"` | one summary line; error count no higher than the Step 0 value (this plan changes no data) |
| Docs | `cd E:/repos/wt-014-enlistment-session-scope && python tools/lint_docs.py \| grep "Dead links:"` | `- Dead links: **0**` |
| Tree guard | `git -C E:/repos/wt-014-enlistment-session-scope rev-parse --abbrev-ref HEAD` | `plan-014-enlistment-session-scope` |

## Scope

Every path below is relative to W (`E:\repos\wt-014-enlistment-session-scope\`).

**In scope** (the only files you may modify or create):

- `Main/Features/Enlistment/ServiceAttachmentService.cs` (add one member)
- `Main/Features/Enlistment/IServiceAttachmentService.cs` (add one member)
- `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs` (add one interface member and one method)
- `Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs` (rename `Invalidate` to `ResetForNewSession`, add a doc line)
- `Main/Features/Enlistment/ServiceMaintenanceService.cs` (two constructor parameters and fields, three calls in `ResetSessionCaches`, doc comment `:207`)
- `Main/Features/Enlistment/IServiceMaintenanceService.cs` (doc comment `:51` only)
- `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs` (`OnNewGameCreated` only)
- `Main/Features/Enlistment/EnlistmentReconciler.cs` (comments at `:49-52` and `:452-455` only; no code)
- `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs` (create)
- `TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs` (usings, two fields, setup, constructor call, two new tests)
- `TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs` (usings and the constructor call at `:49-53` only)
- `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs` (`BuildContainer()` only: two
  `RegisterInstance` substitutes and their comment, after `:40`)
- `docs/features/enlistment.md` (`:1692-1699` rewritten, one paragraph added after it)

That is 13 paths.

**Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`, `Directory.Build.props`:
**recommend, don't edit.** None needs a change: every type involved is already registered
`Reuse.Singleton` in `Main/Features/Enlistment/EnlistmentIoC.cs` (`:31` `IServiceAttachmentService`,
`:47` `IServiceMaintenanceService`, `:55` `IEnlistmentWaitMenuPresenter`, `:75` `IArmyRhythmSnapshotService`,
`:90` `EnlistmentBehavior`), and in the live container DryIoc injects the new constructor parameters
by type (the presenter's two cross-module dependencies are registered by `Main/IoC.cs:129` and `:179`).
Only the test container in `EnlistmentContainerWiringTests` lacks them, which Step 4 fixes inside
that test file. The SDK-style csproj globs the new test file. `EnlistmentIoC.cs` itself needs no
change; if you find it does, STOP.

**Out of scope** (do NOT touch, even though they look related):

- `Main/Features/Enlistment/EnlistmentIoC.cs`, `Main/Features/Enlistment/Hooks/EnlistmentMenuBehavior.cs`.
- `Main/Adapters/MobilePartyAttachmentAdapter.cs`, `Main/Adapters/ArmyMembershipAdapter.cs`,
  `Main/Adapters/CommanderLordAdapter.cs` (their cross-campaign retention is the follow-up below).
- The reconciler's CODE (`EnlistmentReconciler.cs:456` re-anchor and everything else); comments only.
- Any `SyncData` or persisted field (no save-format change of any kind).
- `CHANGELOG.md`, `docs/ai-includes/orientation.md`, `docs/reviews/lessons/*.md`: another session has
  uncommitted edits to them in the main checkout. Give the CHANGELOG and lesson text in your final
  report instead (Step 8).
- Every other feature's singletons, and the 22 `AddBehavior(IoC.Resolve<...>())` behaviors in `Main/SubModule.cs`.

## Git workflow

- **Worktree and branch** (Step 0):
  `git -C E:/repos/TAOM worktree add E:/repos/wt-014-enlistment-session-scope -b plan-014-enlistment-session-scope b2e387db`.
  This creates W and the branch; it does not modify the main checkout's files.
- **Before every commit**, run both and confirm the exact results:
  1. The Tree guard prints `plan-014-enlistment-session-scope`.
  2. `git -C E:/repos/wt-014-enlistment-session-scope diff --cached --name-only` lists exactly the files
     the step names, nothing else.
- **Stage and commit** with `git -C E:/repos/wt-014-enlistment-session-scope add <path> <path>` then
  `git -C E:/repos/wt-014-enlistment-session-scope commit -m "<subject>" -m "<body>"`, adding a third
  `-m "<trailer>"` only where a step gives a trailer. Explicit paths only; never `git add -A`,
  `git add .` or `git commit -a`.
- **Commit subject:** `<type>(enlistment): v<version> - <description>`, at most 72 characters, where
  `<version>` is the `<Version value="...">` in `Main/_Module/SubModule.xml` (it reads `v2.0.30` at
  `b2e387db`, line 6; re-read it in W). The four subjects below are already measured (70, 71, 65 and
  67 characters).
- **Commit bodies** are given pre-wrapped (every line under 72 characters, no dashes). Pass the body
  exactly as shown, line breaks included, as the second `-m` (a double-quoted Bash string may span
  lines). Do not re-wrap or re-word it.
- **No AI attribution trailer** (no `Co-Authored-By`). Optional trailers: `Not-tested:`, `Research:`.
- **Never push**, never open a PR, never merge. You commit C# without `/deep-review` because you
  cannot invoke skills; the orchestrator runs `/deep-review` on this branch before any merge.
- After each commit, check: `git -C E:/repos/wt-014-enlistment-session-scope log -1 --format=%s | python -X utf8 -c "import sys; s=sys.stdin.read().strip(); print(len(s), s)"`
  prints a number at most 72, and
  `git -C E:/repos/wt-014-enlistment-session-scope log -1 --format=%b | awk 'length($0)>72{n++} END{print n+0}'` prints `0`.

## Steps

### Step 0: Create the worktree and record the baseline

1. Run the drift check (top of this file) and note its output.
2. Create the worktree (command in Git workflow). If it fails because the directory or the branch
   already exists, STOP. Run the Tree guard: it prints `plan-014-enlistment-session-scope`.
3. Confirm the excerpts. `git -C E:/repos/wt-014-enlistment-session-scope log -1 --format=%h` prints
   `b2e387db`. Then each of these prints `1`:
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -c "private double? _settlementEntryHours;" Main/Features/Enlistment/ServiceAttachmentService.cs`
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -c "private double? _lastOfferedAtHours;" Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs`
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -c "    void Invalidate();" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs`
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -c "private void OnNewGameCreated(CampaignGameStarter starter)" Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -c "ResetSessionCaches" Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`
     (the one call in `OnGameLoaded`)
   - `cd E:/repos/wt-014-enlistment-session-scope && grep -rn "\.Invalidate()" Main/Features/Enlistment TAOM.Tests/Features/Enlistment | grep -c "_rhythm"`
     prints `0` (not `1`): nothing calls the rhythm cache's `Invalidate` today.
   And `cd E:/repos/wt-014-enlistment-session-scope && wc -l < Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs` prints `147`.
4. Record the data baseline: run the Data command and note the error count.
5. Create L and the allowed-failures list:
   `mkdir -p E:/repos/wt-014-logs && printf '%s\n' TheElkItem_DeclaresTheScaleTheReachIsTunedFor AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist > E:/repos/wt-014-logs/allowed.txt`
6. Run Test (full) with `timeout: 600000`, then
   `grep -E "^\s+Failed [A-Za-z0-9_]+" E:/repos/wt-014-logs/full.log | awk '{print $2}' | sort -u > E:/repos/wt-014-logs/now.txt; cat E:/repos/wt-014-logs/now.txt`.
   The printed names are the Step 0 list (expected: at most the two Armory tests). Add them to the
   allowed list:
   `cat E:/repos/wt-014-logs/now.txt >> E:/repos/wt-014-logs/allowed.txt && sort -u -o E:/repos/wt-014-logs/allowed.txt E:/repos/wt-014-logs/allowed.txt`.
   Do not fix any of them.
7. Confirm Enlistment starts green: Test (filtered) with `TAOM.Tests.Features.Enlistment` shows
   `Failed: 0`. If not, STOP (the plan's own area is already broken).

**Verify**: the Tree guard prints the branch name; item 3 prints `b2e387db`, five `1`s, one `0` and
`147`; item 4 printed an error count; item 6 printed a summary line with `Total:`; item 7 shows
`Failed: 0`.

### Step 1 (RED): Pin the three resets on the services themselves

Create `E:\repos\wt-014-enlistment-session-scope\TAOM.Tests\Features\Enlistment\EnlistmentSessionResetTests.cs`
with exactly this content (a Step 5 section is appended later):

```csharp
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.Enlistment.Domain;
using TAOM.Features.Enlistment.Hooks;
using TAOM.Features.Enlistment.Presentation;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Session scope for the Enlistment singletons (csharp-architecture.md, "Singleton Services Holding
/// Per-Campaign State MUST Have a Session-Reset Story"). Every service here is Reuse.Singleton and
/// outlives the campaign, while these fields hold absolute campaign hours. Loading an earlier save,
/// or starting a new campaign, runs the clock backwards, and a stamp left in the future reads as
/// "a moment ago" until the new clock catches up with it.
/// </summary>
[TestClass]
public class EnlistmentSessionResetTests
{
    // ---- settlement-dwell anchor (ServiceAttachmentService) ------------------------------------

    private static ServiceAttachmentService NewAttachment() =>
        new ServiceAttachmentService(
            Substitute.For<IMobilePartyAttachmentAdapter>(),
            Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IModLogger>());

    [TestMethod]
    public void AttachmentReset_DropsTheDwellAnchor_SoAnEarlierClockIsNotInsideTheDwell()
    {
        var sut = NewAttachment();
        sut.StampSettlementEntry(1000.0);
        Assert.IsTrue(sut.IsWithinSettlementDwell(10.0),
            "Precondition: an anchor in the future reads as inside the dwell; that is what the reset exists to clear.");

        sut.ResetForNewSession();

        Assert.IsFalse(sut.IsWithinSettlementDwell(10.0));
        Assert.IsFalse(sut.IsWithinSettlementDwell(1001.0));
    }

    [TestMethod]
    public void AttachmentReset_ThenANewPlacement_StartsAFreshDwell()
    {
        var sut = NewAttachment();
        sut.StampSettlementEntry(1000.0);
        sut.ResetForNewSession();

        sut.StampSettlementEntry(10.0);

        Assert.IsTrue(sut.IsWithinSettlementDwell(12.0));
        Assert.IsFalse(sut.IsWithinSettlementDwell(16.0));
    }

    // ---- arrival-offer latch (EnlistmentWaitMenuPresenter) --------------------------------------

    private static EnlistmentWaitMenuPresenter NewPresenter(IInquiryAdapter inquiry)
    {
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord());
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(true);
        var actions = Substitute.For<IEnlistmentPlayerActionService>();
        actions.CanTakeTownLeave().Returns(true);
        var settings = Substitute.For<IEnlistmentFeatureSettingsProvider>();
        settings.IsEnabled.Returns(true);
        settings.OfferLeaveOnArrival.Returns(true);

        return new EnlistmentWaitMenuPresenter(store, Substitute.For<ICommanderLordAdapter>(),
            Substitute.For<IEnlistmentDialogGateService>(), Substitute.For<IEnlistmentService>(),
            inquiry, coop, actions, Substitute.For<IGameMenuAdapter>(),
            Substitute.For<IServiceStatusService>(), settings, Substitute.For<IModLogger>());
    }

    private static void AssertOffersShown(IInquiryAdapter inquiry, int count) =>
        inquiry.Received(count).ShowTwoOptionInquiry(
            "taom_enlist_arrival_title", Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Action>(), Arg.Any<System.Action>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<System.Collections.Generic.IReadOnlyDictionary<string, string>>(), Arg.Any<bool>());

    [TestMethod]
    public void PresenterReset_ReArmsTheArrivalOffer_OnAnEarlierClock()
    {
        var inquiry = Substitute.For<IInquiryAdapter>();
        var sut = NewPresenter(inquiry);
        sut.OfferTownLeave("town_EW1", 1000.0);
        sut.OfferTownLeave("town_EW2", 10.0);   // an earlier save: the clock ran backwards
        AssertOffersShown(inquiry, 1);          // precondition: suppressed without a reset

        sut.ResetForNewSession();
        sut.OfferTownLeave("town_EW2", 10.0);

        AssertOffersShown(inquiry, 2);
    }

    [TestMethod]
    public void PresenterReset_ReArmsTheArrivalOffer_ForTheSameSettlement()
    {
        // The settlement-id latch is session state too: past the cooldown, only the reset re-arms
        // an offer for the town the previous session last offered.
        var inquiry = Substitute.For<IInquiryAdapter>();
        var sut = NewPresenter(inquiry);
        sut.OfferTownLeave("town_EW1", 1000.0);

        sut.ResetForNewSession();
        sut.OfferTownLeave("town_EW1", 1030.0);

        AssertOffersShown(inquiry, 2);
    }

    // ---- army-rhythm snapshot (ArmyRhythmSnapshotService) --------------------------------------

    [TestMethod]
    public void RhythmReset_ForcesAFreshProbe_InTheSameCampaignHour()
    {
        // A quick reload inside the same campaign hour would otherwise serve the pre-load world.
        var store = Substitute.For<IEnlistmentStore>();
        store.Record.Returns(new EnlistmentRecord());
        var content = Substitute.For<IEnlistmentContentStore>();
        content.Record.Returns(new ServiceContentRecord());
        var probe = Substitute.For<IArmyRhythmProbeAdapter>();
        probe.Probe(Arg.Any<string>()).Returns(new ArmyRhythmProbe());
        var sut = new ArmyRhythmSnapshotService(store, content, probe);

        sut.GetSnapshot(10.0, 12.0);
        sut.GetSnapshot(10.0, 12.0);
        probe.Received(1).Probe(Arg.Any<string>());

        sut.ResetForNewSession();
        sut.GetSnapshot(10.0, 12.0);

        probe.Received(2).Probe(Arg.Any<string>());
    }
}
```

Arithmetic the asserts rely on: `SettlementDwellHours` is 6.0, so a stamp at 10.0 is inside at 12.0
(2 < 6) and outside at 16.0 (6 < 6 is false); a stamp at 1000.0 read at 10.0 gives -990 < 6, true.
`OfferCooldownHours` is 24.0, so 1030.0 is past a 1000.0 stamp. Both hour stamps `floor(10.0 * 24)`
are equal, so the second `GetSnapshot` hits the cache.

**Verify** (RED): run the RED compile check with `EnlistmentSessionResetTests`. Expected: the build
fails, and every error line is `error CS1061` naming `ResetForNewSession` on
`ServiceAttachmentService`, `EnlistmentWaitMenuPresenter` or `ArmyRhythmSnapshotService` (three
distinct type names; the same message may repeat per call site). Any other error code, or an error
naming anything else: STOP.

### Step 2 (GREEN): Add `ResetForNewSession` to the three services

1. `Main/Features/Enlistment/IServiceAttachmentService.cs`: directly after the line
   `    void StampSettlementEntry(double nowHours);` (`:37`) insert:

   ```csharp

       /// <summary>
       /// Forget the dwell anchor. Session reset only (a load or a new campaign), never per tick:
       /// the anchor is an absolute campaign hour, and one left in the future reads as "inside the
       /// dwell" until the new clock passes it.
       /// </summary>
       void ResetForNewSession();
   ```

2. `Main/Features/Enlistment/ServiceAttachmentService.cs`: directly after the two-line
   `IsWithinSettlementDwell` member (`:43-44`) insert:

   ```csharp

       public void ResetForNewSession() => _settlementEntryHours = null;
   ```

3. `Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs`:
   - In the interface, directly after `    void OfferTownLeave(string settlementId, double nowHours);`
     (`:43`, before the interface's closing `}` at `:44`) insert:

     ```csharp

         /// <summary>
         /// Forget which stop was last offered and when. Session reset only (a load or a new
         /// campaign): the cooldown stamp is an absolute campaign hour, so an earlier save's clock
         /// would otherwise keep the offer silent until it caught up.
         /// </summary>
         void ResetForNewSession();
     ```

   - In the class, directly after the closing `    }` of `OfferTownLeave` (`:158`) and before
     `    public void TakeTownLeave()` insert:

     ```csharp

         public void ResetForNewSession()
         {
             _lastOfferedSettlementId = null;
             _lastOfferedAtHours = null;
         }
     ```

4. `Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs`:
   - Interface: replace `    void Invalidate();` (`:12`) with:

     ```csharp
         /// <summary>Drop the cached snapshot. Session reset only (a load or a new campaign).</summary>
         void ResetForNewSession();
     ```

   - Class: rename `    public void Invalidate()` (`:67`) to `    public void ResetForNewSession()`; the
     body (`_cached = null; _cachedHourStamp = double.MinValue;`) stays unchanged.

5. Run Build, then Test (filtered) with `EnlistmentSessionResetTests`, then Test (filtered) with
   `EnlistmentWaitMenuPresenterTests`, then with `SettlementFollowingTests`.

**Verify**: Build exits 0 with `0 Error(s)`; `EnlistmentSessionResetTests` shows `Failed: 0` and
`Passed: 5`; the other two filtered runs show `Failed: 0`;
`cd E:/repos/wt-014-enlistment-session-scope && grep -rn "Invalidate()" Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs` prints nothing.

**Commit**: stage exactly `Main/Features/Enlistment/IServiceAttachmentService.cs`,
`Main/Features/Enlistment/ServiceAttachmentService.cs`,
`Main/Features/Enlistment/Presentation/EnlistmentWaitMenuPresenter.cs`,
`Main/Features/Enlistment/Content/ArmyRhythmSnapshotService.cs`,
`TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`. Subject:
`fix(enlistment): v2.0.30 - reset the dwell anchor, offer latch, rhythm`. Body:

```text
The dwell anchor, the arrival-offer settlement id and cooldown stamp,
and the per-hour rhythm snapshot are absolute-clock state on
process-lifetime singletons. Each now exposes ResetForNewSession, and
the rhythm service's uncalled Invalidate is renamed to match. Wiring
follows in the next commit.
```

### Step 3 (RED): Pin the wiring in `ResetSessionCaches`

1. `TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs`:
   - Add `using TAOM.Features.Enlistment.Content;` and `using TAOM.Features.Enlistment.Presentation;`
     to the using block (`:1-7`).
   - After the field `    private IEnlistmentReconciler _reconciler = null!;` (`:26`) add:

     ```csharp
         private IEnlistmentWaitMenuPresenter _presenter = null!;
         private IArmyRhythmSnapshotService _rhythm = null!;
     ```

   - In `Setup()`, directly after `        _reconciler = Substitute.For<IEnlistmentReconciler>();` (`:44`) add:

     ```csharp
             _presenter = Substitute.For<IEnlistmentWaitMenuPresenter>();
             _rhythm = Substitute.For<IArmyRhythmSnapshotService>();
     ```

   - Change the constructor call's last line (`:52`) from `            _reconciler, _logger);` to
     `            _reconciler, _presenter, _rhythm, _logger);`.
   - Directly after the test `Pump_DoesNotResetTheArmyAdapterCache` (ends `:164`, before the
     `// ---- gating ---` comment) add:

     ```csharp

         [TestMethod]
         public void ResetSessionCaches_AlsoDropsTheDwellAnchorTheOfferLatchAndTheRhythmCache()
         {
             // All three hold absolute campaign-hour state on singletons. Asserted here, on the one
             // reset point, so a new collaborator reset cannot be wired into a hook instead.
             _pump.ResetSessionCaches();

             _attachment.Received(1).ResetForNewSession();
             _presenter.Received(1).ResetForNewSession();
             _rhythm.Received(1).ResetForNewSession();
         }

         [TestMethod]
         public void Pump_DoesNotResetTheDwellAnchorTheOfferLatchOrTheRhythmCache()
         {
             // Session resets only. Clearing the dwell anchor on an ordinary pump would bring back
             // the enter-and-leave strobe the dwell exists to stop.
             MakeEnlisted();

             PumpExpensive();

             _attachment.DidNotReceive().ResetForNewSession();
             _presenter.DidNotReceive().ResetForNewSession();
             _rhythm.DidNotReceive().ResetForNewSession();
         }
     ```

2. `TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs`: add the same two usings to
   `:1-6`, and change `:53` from `            Substitute.For<IEnlistmentReconciler>(), _logger);` to
   `            Substitute.For<IEnlistmentReconciler>(), Substitute.For<IEnlistmentWaitMenuPresenter>(),`
   followed by a new line `            Substitute.For<IArmyRhythmSnapshotService>(), _logger);`.

**Verify** (RED): run the RED compile check with `ServiceMaintenanceServiceTests`. Expected: the only
error is `error CS1729: 'ServiceMaintenanceService' does not contain a constructor that takes 14 arguments`
(two occurrences, one per test file). Any other error, including a `CS0104` ambiguity: STOP. (At
`b2e387db` no simple type name is declared twice across `Main/Features/Enlistment/**`, `Main/Adapters`,
`Main/Features/CoopInterop` and `Main/Core/Logging`, so the new usings cannot clash.)

### Step 4 (GREEN): Call the three resets from `ResetSessionCaches`

In `Main/Features/Enlistment/ServiceMaintenanceService.cs`:

1. After the field `    private readonly IEnlistmentReconciler _reconciler;` (`:49`) add:

   ```csharp
       private readonly Presentation.IEnlistmentWaitMenuPresenter _presenter;
       private readonly Content.IArmyRhythmSnapshotService _rhythm;
   ```

   (`Presentation.` and `Content.` resolve from namespace `TAOM.Features.Enlistment`, exactly as
   `EnlistmentIoC.cs:28` and `:75` write them; do not add usings to this file.)

2. In the constructor parameter list, between `        IEnlistmentReconciler reconciler,` (`:69`) and
   `        IModLogger logger)` (`:70`) add:

   ```csharp
           Presentation.IEnlistmentWaitMenuPresenter presenter,
           Content.IArmyRhythmSnapshotService rhythm,
   ```

   and in the body, after `        _reconciler = reconciler;` (`:82`) add
   `        _presenter = presenter;` and `        _rhythm = rhythm;` on two lines.

3. At the end of `ResetSessionCaches()`, after `        _reconciler?.ResetForNewSession();` (`:240`)
   and before the method's closing `    }`, add:

   ```csharp

           // Three more pieces of absolute campaign-hour state on singletons: the settlement-dwell
           // anchor, the arrival-offer latch and its 24-hour cooldown, and the per-hour rhythm
           // snapshot. Loading an earlier save or starting a new campaign runs the clock backwards,
           // and a stamp left in the future reads as "a moment ago": the exit sweep would hold the
           // player in a town the commander has left, and the shore-leave offer would stay silent.
           _attachment.ResetForNewSession();
           _presenter?.ResetForNewSession();
           _rhythm?.ResetForNewSession();
   ```

4. In the doc comment, change line `:207` from
   `    /// Drop per-session caches. MUST be called on game load and session launch: the cached party`
   to `    /// Drop per-session caches. MUST be called on game load and on a new campaign: the cached party`.

5. `Main/Features/Enlistment/IServiceMaintenanceService.cs:51`: change `Call on game load and session launch`
   to `Call on game load and on a new campaign` (keep the rest of the line as it is).

6. `TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs`: the test container must now
   supply the presenter's two cross-module dependencies (Current state, "why Step 4 edits it").
   Directly after `        container.RegisterInstance(Substitute.For<IPathService>());` (`:40`) and
   before the blank line and `        EnlistmentIoC.RegisterEnlistmentFeature(container);` (`:42`),
   insert exactly:

   ```csharp
           // IPlayerContextAdapter (SiegeDefenseIoC) and IDutyOrchestrationService (DutiesIoC) entered
           // this graph when ResetSessionCaches began resetting the wait-menu presenter:
           // IServiceMaintenanceService -> IEnlistmentWaitMenuPresenter -> IEnlistmentDialogGateService
           // -> IPlayerContextAdapter, and -> IEnlistmentPlayerActionService -> IDutyOrchestrationService.
           container.RegisterInstance(Substitute.For<global::TAOM.Adapters.IPlayerContextAdapter>());
           container.RegisterInstance(Substitute.For<global::TAOM.Features.Enlistment.Duties.IDutyOrchestrationService>());
   ```

   Change nothing else in the file (no usings, no test methods).

7. Run Build, then Test (filtered) with `ServiceMaintenanceServiceTests`, with
   `EnlistmentPumpAuthorityTests`, and with `EnlistmentContainerWiringTests`.

**Verify**: Build exits 0 with `0 Error(s)`; all three filtered runs show `Failed: 0`;
`EnlistmentContainerWiringTests` shows `Passed: 5` (its `MaintenanceService_Resolvable_...`,
`LifecycleBehavior_Resolvable_...` and `BattleBehavior_Resolvable_...` tests are the DryIoc proof that
the two new dependencies resolve with no cycle);
`cd E:/repos/wt-014-enlistment-session-scope && grep -c "RegisterInstance(Substitute.For<global::" TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs`
prints `2`; `git -C E:/repos/wt-014-enlistment-session-scope diff --numstat -- TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs`
prints `6	0` and the path (six lines added, none removed).

**Commit**: stage exactly `Main/Features/Enlistment/ServiceMaintenanceService.cs`,
`Main/Features/Enlistment/IServiceMaintenanceService.cs`,
`TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs`,
`TAOM.Tests/Features/Enlistment/EnlistmentPumpAuthorityTests.cs`,
`TAOM.Tests/Features/Enlistment/EnlistmentContainerWiringTests.cs`. Subject:
`fix(enlistment): v2.0.30 - ResetSessionCaches drops the session latches`. Body:

```text
ResetSessionCaches is the feature's one reset point, so the three new
resets are called from it. The maintenance service takes the wait-menu
presenter and the rhythm service to do so. The container wiring test
now substitutes IPlayerContextAdapter and IDutyOrchestrationService,
which the presenter's graph needs and which other feature modules
register in the live container.
```

### Step 5 (RED): Pin the new-campaign path

Append this section to `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`, inside the
class, directly before its final closing `}`:

```csharp

    // ---- the new-campaign path (EnlistmentBehavior) --------------------------------------------

    [TestMethod]
    public void NewCampaign_DropsTheSessionCaches_AndStillClearsTheStore()
    {
        // The engine fires OnNewGameCreated, never OnGameLoaded, for a new campaign
        // (Campaign.DoLoadingForGameType), so the load hook's reset alone left campaign two
        // running on campaign one's caches.
        var store = Substitute.For<IEnlistmentStore>();
        var maintenance = Substitute.For<IServiceMaintenanceService>();
        var sut = new EnlistmentBehavior(store, Substitute.For<IEnlistmentStateMachine>(),
            Substitute.For<IEnlistmentReconciler>(), Substitute.For<IEnlistmentLoadNormalizer>(),
            Substitute.For<IPlayerPartyAdapter>(), Substitute.For<ICoopSessionProvider>(),
            maintenance, Substitute.For<IModLogger>());

        sut.OnNewGameCreated(null);

        maintenance.Received(1).ResetSessionCaches();
        store.Received(1).Clear();
    }
```

**Verify** (RED): run the RED compile check with `EnlistmentSessionResetTests`. Expected: exactly one
error, `error CS0122: 'EnlistmentBehavior.OnNewGameCreated(CampaignGameStarter)' is inaccessible due to its protection level`.
Anything else: STOP.

### Step 6 (GREEN): Reset on a new campaign

In `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`, replace the method at `:124-130`:

```csharp
    private void OnNewGameCreated(CampaignGameStarter starter)
    {
        // A brand-new campaign starts with no service record. SyncData(IsLoading) has NOT
```

with (the rest of the method, the second comment line and the `if (!_justLoadedFromSave) _store.Clear();`,
stays exactly as it is):

```csharp
    // internal for TAOM.Tests. A new campaign never reaches OnGameLoaded: drop the session caches here too.
    internal void OnNewGameCreated(CampaignGameStarter starter)
    {
        _maintenance.ResetSessionCaches();
        // A brand-new campaign starts with no service record. SyncData(IsLoading) has NOT
```

That is exactly two added lines. Do not add a blank line; the file must stay under 150 lines.

**Verify**: Build exits 0; Test (filtered) with `EnlistmentSessionResetTests` shows `Failed: 0` and
`Passed: 6`; Test (filtered) with `EnlistmentContainerWiringTests` shows `Failed: 0`;
`cd E:/repos/wt-014-enlistment-session-scope && wc -l < Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`
prints `149`; `cd E:/repos/wt-014-enlistment-session-scope && grep -c "_maintenance.ResetSessionCaches();" Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs`
prints `2`.

**Commit**: stage exactly `Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs` and
`TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs`. Subject:
`fix(enlistment): v2.0.30 - reset session caches on a new campaign`. Body (second `-m`):

```text
The engine fires OnNewGameCreated, not OnGameLoaded, for a new
campaign, so the commander party handle, the army handle, the
stale-battle anchor and the three new latches all leaked into a second
campaign in the same process. The reset is field clears only, so it
is not authority-gated.
```

Trailer, as a third `-m` of its own: `Not-tested: a real second campaign in one process (needs the game)`.

### Step 7: Make the comments and the feature doc tell the truth

1. `Main/Features/Enlistment/EnlistmentReconciler.cs:49-52`: replace the four lines shown in Current
   state (from `/// the code meant to be the safety net. Two independent guards, because they cover different`
   to `/// reaches <c>ResetSessionCaches</c> at all.`) with:

   ```csharp
       /// the code meant to be the safety net. Two independent guards: <see cref="ResetForNewSession"/>
       /// runs from <c>ResetSessionCaches</c> on a load and on a new campaign, and the backwards-clock
       /// re-anchor in <see cref="BreakStaleBattleLatch"/> stays as the self-contained second guard for
       /// any path that skips the reset (a co-op client's load returns before it).
   ```

2. `Main/Features/Enlistment/EnlistmentReconciler.cs:452-455`: replace the four comment lines from
   `        // anchor belongs to a world this one has nothing to do with. This is the guard for the path`
   to `        // puts the leftover anchor ahead of it.` with:

   ```csharp
           // anchor belongs to a world this one has nothing to do with. ResetSessionCaches runs on a
           // load and on a new campaign, so this is the second guard, for any path that skips the
           // reset: a leftover anchor ahead of a new campaign's low day count is re-anchored here.
   ```

   The code line after it (`if (!FiniteFloatValidator.IsFinite(_staleBattleLatchSinceDays) || nowDays < _staleBattleLatchSinceDays)`)
   must be unchanged.

3. `docs/features/enlistment.md`: replace lines `:1692-1699`, which are exactly these eight lines
   (the paragraph's first five lines, `:1687-1691`, stay):

   ```text
   by the safety net written to prevent it. Two guards, because they cover different paths.
   `IEnlistmentReconciler.ResetForNewSession` covers the load path, dropped from
   `ServiceMaintenanceService.ResetSessionCaches` (the feature's one place that knows this lifetime,
   which is also why the army handle is dropped there rather than from the load hook). A backwards-clock
   re-anchor inside `BreakStaleBattleLatch` covers a brand-new campaign, which never reaches
   `ResetSessionCaches` at all because it is wired to `OnGameLoaded` only: a new campaign starts at a low
   day count, so the leftover anchor sits in its future, and a clock that ran backwards cannot be one
   continuous episode. Found by the `/deep-review` data-flow agent, not by the tests, which all passed.
   ```

   with:

   ```text
   by the safety net written to prevent it. Two guards. `IEnlistmentReconciler.ResetForNewSession` is
   dropped from `ServiceMaintenanceService.ResetSessionCaches` (the feature's one place that knows this
   lifetime, which is also why the army handle is dropped there rather than from the load hook), and
   that reset now runs on a load and on a new campaign. A backwards-clock re-anchor inside
   `BreakStaleBattleLatch` is the second, self-contained guard. When it was written,
   `ResetSessionCaches` ran from `OnGameLoaded` only, so a brand-new campaign never reached it; a new
   campaign starts at a low day count, so the leftover anchor sat in its future, and a clock that ran
   backwards cannot be one continuous episode. Found by the `/deep-review` data-flow agent, not by the
   tests, which all passed. `EnlistmentBehavior.OnNewGameCreated` now runs the reset too; the re-anchor
   stays for any path that skips it (a co-op client's load returns before the reset).

   **Every clock-keyed latch on an Enlistment singleton is reset on both lifecycle edges.**
   `ResetSessionCaches` also clears the settlement-dwell anchor
   (`IServiceAttachmentService.ResetForNewSession`), the arrival-offer settlement id and 24-hour
   cooldown (`IEnlistmentWaitMenuPresenter.ResetForNewSession`) and the per-hour army-rhythm snapshot
   (`IArmyRhythmSnapshotService.ResetForNewSession`). Each held an absolute campaign hour. Before this,
   loading an earlier save left the stamps in the future, which the code read as "a moment ago": the
   exit sweep held the player in a town the commander had left until the new clock passed the old
   stamp plus 6 hours, and the shore-leave offer stayed silent until it passed the old stamp plus a
   day. The tests are `EnlistmentSessionResetTests` and the `ResetSessionCaches_*` tests in
   `ServiceMaintenanceServiceTests`.
   ```

   Keep the blank line and the `### The engine backstop, and the bundle that was suppressed` heading
   that follow.

4. Run Build, Test (filtered) with `EnlistmentReconcilerTests`, and the Docs command. Then check the
   added lines of the whole branch for em and en dashes (a Python check, because this Git Bash
   passes `$'\u2014'` to grep as literal text, so a grep version can never fail):
   `cd E:/repos/wt-014-enlistment-session-scope && git diff -U0 b2e387db -- docs/features/enlistment.md Main/Features/Enlistment TAOM.Tests/Features/Enlistment | python -X utf8 -c "import sys; print(sum(1 for l in sys.stdin if l.startswith('+') and not l.startswith('+++') and (chr(0x2014) in l or chr(0x2013) in l)))"`
   prints `0`.

**Verify**: Build exits 0; `EnlistmentReconcilerTests` shows `Failed: 0`; Docs prints
`- Dead links: **0**`; the dash check prints `0`;
`cd E:/repos/wt-014-enlistment-session-scope && grep -c "wired to OnGameLoaded only" Main/Features/Enlistment/EnlistmentReconciler.cs` prints `0`;
`cd E:/repos/wt-014-enlistment-session-scope && grep -c "Two guards, because they cover different paths" docs/features/enlistment.md` prints `0`.

**Commit**: stage exactly `Main/Features/Enlistment/EnlistmentReconciler.cs` and
`docs/features/enlistment.md`. Subject: `docs(enlistment): v2.0.30 - session reset now covers a new campaign`.
Body:

```text
The reconciler comments and the feature doc said a new campaign never
reaches ResetSessionCaches. It now does, and the doc lists the three
latches it clears.
```

### Step 8: Final verification and report

Run every Done-criteria command. Then put these in your final report (do not write them to files):

- **CHANGELOG entry** for the orchestrator to add (no dashes): "Enlistment no longer carries clocks
  from one campaign into the next. Loading an earlier save could hold you inside a town your
  commander had already left, and silence the shore-leave offer for up to the rest of the
  playthrough, because both remembered a campaign hour from the session before. Starting a new
  campaign without restarting the game also skipped Enlistment's cache reset entirely. Both paths
  now clear every per-session value."
- **Lesson** to propose for `docs/reviews/lessons/state-lifecycle-save.md` (the orchestrator appends
  it): "A per-feature reset method that claims to be 'the one place that knows the lifetime' is only
  as good as its callers and its list: Enlistment's ran on load only, and three clock-keyed latches
  added later never joined it. Wire the reset to both `OnGameLoaded` and `OnNewGameCreated`, and add
  each new singleton clock to it in the same commit, with a `Received(1)` test on the reset point
  (plan 014, `EnlistmentSessionResetTests`)."
- The commit hashes and subjects, the drift-check output, the Step 0 failure list, the final test
  counts and failure names, and the owed in-game check (Maintenance notes).

## Test plan

- **New tests** (8):
  - `TAOM.Tests/Features/Enlistment/EnlistmentSessionResetTests.cs` (6):
    `AttachmentReset_DropsTheDwellAnchor_SoAnEarlierClockIsNotInsideTheDwell` (backwards clock, with
    the bug as a precondition), `AttachmentReset_ThenANewPlacement_StartsAFreshDwell` (the dwell still
    works after a reset), `PresenterReset_ReArmsTheArrivalOffer_OnAnEarlierClock` (cooldown latch,
    with the suppression as a precondition), `PresenterReset_ReArmsTheArrivalOffer_ForTheSameSettlement`
    (settlement-id latch), `RhythmReset_ForcesAFreshProbe_InTheSameCampaignHour` (hour cache),
    `NewCampaign_DropsTheSessionCaches_AndStillClearsTheStore` (the new-campaign hook).
  - `TAOM.Tests/Features/Enlistment/ServiceMaintenanceServiceTests.cs` (2):
    `ResetSessionCaches_AlsoDropsTheDwellAnchorTheOfferLatchAndTheRhythmCache` (wiring) and
    `Pump_DoesNotResetTheDwellAnchorTheOfferLatchOrTheRhythmCache` (the reset is not per tick).
- **Structural pattern**: `ServiceMaintenanceServiceTests.cs:115-164` for the wiring tests,
  `EnlistmentWaitMenuPresenterTests.cs:32-55` and `:166-182` for the presenter,
  `CoopAuthorityGateTests.cs:223-231` for calling a behavior hook directly.
- **Existing guards that must stay green**: `EnlistmentContainerWiringTests` (DryIoc resolves the new
  constructor graph; Step 4 adds two cross-module substitutes to its container, no new test),
  `EnlistmentPumpAuthorityTests`, `EnlistmentWaitMenuPresenterTests`, `SettlementFollowingTests`,
  `EnlistmentReconcilerTests`.
- **Untestable here** (commit trailer `Not-tested:`): `OnGameLoaded` itself (it reads
  `CampaignTime.Now` after the reset, which needs a live `Campaign`; the reset it triggers is pinned on
  the service), and a real load-earlier-save or second-campaign session in one process.
- **Verification**: Test (full) then the Subset check prints nothing, and the filtered runs show the
  8 new tests passing.

## Done criteria

ALL must hold (every command starts with `cd E:/repos/wt-014-enlistment-session-scope && `):

- [ ] Build exits 0 with `0 Error(s)`.
- [ ] Test (full) prints a summary line with `Total:`, and the Subset check's last command prints
      nothing (every failure is one of the 2 Armory tests or on the Step 0 list in `allowed.txt`).
- [ ] Test (filtered) with `EnlistmentSessionResetTests` prints `Failed: 0` and `Passed: 6`.
- [ ] Test (filtered) with `ServiceMaintenanceServiceTests` prints `Failed: 0`, and
      `cd E:/repos/wt-014-enlistment-session-scope && dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~ResetSessionCaches_AlsoDropsTheDwellAnchorTheOfferLatchAndTheRhythmCache|FullyQualifiedName~Pump_DoesNotResetTheDwellAnchorTheOfferLatchOrTheRhythmCache"`
      prints `Failed: 0` and `Passed: 2`.
- [ ] Test (filtered) with `EnlistmentContainerWiringTests` prints `Failed: 0` and `Passed: 5`.
- [ ] `grep -rn "ResetForNewSession" Main/Features/Enlistment/ServiceMaintenanceService.cs` prints
      exactly four lines (the reconciler's existing call and the three new ones).
- [ ] `grep -rn "void Invalidate()" Main/Features/Enlistment/Content/` prints nothing.
- [ ] `wc -l < Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs` prints `149` (at most 149).
- [ ] `grep -c "_maintenance.ResetSessionCaches();" Main/Features/Enlistment/Hooks/EnlistmentBehavior.cs` prints `2`.
- [ ] `git -C E:/repos/wt-014-enlistment-session-scope diff --name-only b2e387db` lists exactly the 13
      in-scope paths (Scope section), no more (`... | wc -l` prints `13`).
- [ ] `git -C E:/repos/wt-014-enlistment-session-scope status --porcelain` prints nothing (all committed).
- [ ] Every commit subject is at most 72 characters and no commit carries `Co-Authored-By`
      (`git -C E:/repos/wt-014-enlistment-session-scope log b2e387db..HEAD --format=%B | grep -c Co-Authored-By` prints `0`).
- [ ] `git -C E:/repos/wt-014-enlistment-session-scope log b2e387db..HEAD --format=%b | awk 'length($0)>72{n++} END{print n+0}'` prints `0`.
- [ ] The Data command's error count is no higher than the Step 0 value; the Docs command prints
      `- Dead links: **0**`.

## STOP conditions

Stop and report back (do not improvise) if:

- Any Step 0 item 3 check prints something other than the stated value (the code drifted from the
  excerpts), or `EnlistmentBehavior.cs` is not 147 lines.
- A RED step fails with anything other than the named compile errors (for example Step 1 already
  compiles, which would mean someone added `ResetForNewSession` since `b2e387db`).
- `EnlistmentContainerWiringTests` fails after Step 4 (with its two substitutes in place) or Step 6,
  especially with a DryIoc recursive dependency error, or an unresolved-service error naming anything
  other than `IPlayerContextAdapter` or `IDutyOrchestrationService`. Do not add a third substitute.
  The plan assumes nothing below `EnlistmentWaitMenuPresenter` or
  `ArmyRhythmSnapshotService` depends on `IServiceMaintenanceService` (at `b2e387db` only the hook
  behaviors `EnlistmentBehavior`, `EnlistmentMenuBehavior`, `EnlistmentBattleBehavior` and
  `EnlistmentMaintenanceBehavior` take it). Report the error text; do not move the reset into a hook
  and do not use `Lazy<>` or a service locator.
- Constructing `EnlistmentBehavior` in the Step 5 test throws (for example a TypeLoad or
  `TypeInitializationException` from the TaleWorlds assemblies). Report the exception; do not
  `[Ignore]` the test or turn it into `Assert.Inconclusive`.
- Step 6 cannot be done within two added lines (the ADR-002 ceiling of under 150 lines).
- Any other existing test newly fails after a GREEN step, in particular any test that expects
  `ResetSessionCaches` NOT to be called on a new campaign.
- The fix appears to need `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj`,
  `Directory.Build.props`, `Main/Features/Enlistment/EnlistmentIoC.cs`, any adapter, any
  `SyncData`, or any file outside Scope. If a registration really is missing, report the exact line
  to add to `EnlistmentIoC.cs` instead of adding it.
- A step's verification fails twice after a reasonable fix attempt.
- `git worktree add` fails because `E:/repos/wt-014-enlistment-session-scope` or the branch
  `plan-014-enlistment-session-scope` already exists. Report it; do not delete or reuse either.
- A Read, Edit, Write or `cd` into W, or a write into L, is denied by permissions (both are outside
  the project directory).
- A commit is denied by a hook. The PreToolUse commit hooks read the main checkout's index, not W's,
  so another session's staging there can deny your commit. Report the hook's message verbatim.
- In each of the last three cases, never fall back to editing, staging or committing under
  `E:\repos\TAOM`.

## Maintenance notes

- **Owed in-game check** (label `triage-needs-ingame` when the issue closes): (1) enlist, let the
  column enter a town, save, play on past a few days, then load that save and let the commander leave
  town: the player follows within a few campaign hours, and with `[EnlistDiag]` diagnostics on no
  long run of `EXIT deferred` lines appears. (2) After that load, the next town the column enters
  pops "The column halts". (3) Quit to menu while enlisted, start a new campaign without restarting,
  enlist under a lord: the player tracks the new lord's party, never a stale position.
- **What a reviewer should probe**: that `OnNewGameCreated` resets before the store clear and is not
  gated (every callee is a field clear; the Current state section lists each); that nothing calls
  the three new resets outside `ResetSessionCaches` (a per-tick call would re-open the strobe the
  dwell stops); that the new constructor dependencies do not create a DryIoc cycle
  (`EnlistmentContainerWiringTests`).
- **Accepted trade-off**: `ServiceMaintenanceService` now depends on `IEnlistmentWaitMenuPresenter`, a
  presentation-layer interface. Win: one reset point, as its own doc demands. Cost: one upward
  dependency, interface only, no TaleWorlds type crosses it, and the presenter's graph pulls two
  cross-module services into the feature's container test (Step 4 substitutes them). The follow-up
  below removes it.
- **Also fixed by Step 6, not separately tested in game**: audit finding CORRECTNESS-07's "position
  pin" (PLAUSIBLE, not reproduced). `MobilePartyAttachmentAdapter._cachedCommanderParty`
  (`Main/Adapters/MobilePartyAttachmentAdapter.cs:161-164`, a live `MobileParty` whose comment says it
  "MUST be invalidated on discharge, session launch and game load") and
  `ServiceMaintenanceService._cachedCommanderPartyId` were cleared on load only; lord party ids repeat
  in a fresh campaign (`LordPartyComponent.cs:134-137`, `stringId + "_party_1"`), so the cheap sync
  could match a dead campaign's party. The new-campaign reset now clears both.
- **Deferred follow-ups (separate plans, with their evidence)**:
  - **One session-scope mechanism** (CORRECTNESS-07's class fix): an `ISessionScoped { void ResetForNewSession(); }`
    registered with `RegisterMany`, one dispatcher on `OnNewGameCreated`, on `OnGameLoaded` before
    normalisers, and on game end, plus a reflection ratchet test over every `Reuse.Singleton` type
    holding an engine object, `CampaignTime`, or a `double`/`double?` named `*Hours`/`*Days`/`*Stamp`.
    Evidence: 496 singleton registrations, only 6 instance `ResetForNewSession` implementations at
    `b2e387db` (`git grep -n ResetForNewSession b2e387db -- Main`); `Main/SubModule.cs:788-798` hand-resets
    two ArmyTargeting singletons in `OnGameEnd`; adapters that keep a dead campaign's objects
    reachable across campaigns: `Main/Adapters/CommanderLordAdapter.cs:74` (`MapEvent`),
    `ArmyMembershipAdapter.cs:25` (`Army`), `MobilePartyAttachmentAdapter.cs:164` (`MobileParty`).
    Risk to design for: never clear a value `SyncData` just restored.
  - **COMP-02**: 22 CampaignBehaviors are added as singletons
    (`git show b2e387db:Main/SubModule.cs | grep -c "AddBehavior(IoC.Resolve"` = 22, against 34
    `AddBehavior(new`), 10 of them Enlistment's (`SubModule.cs:1412-1421`), each hand-rolling
    new-campaign detection (`EnlistmentBehavior.cs:132-146`, `MessengerCampaignBehavior.cs`,
    `CaravanVisitMemoryBehavior.cs`). Flipping them to `Reuse.Transient` touches `Main/SubModule.cs`
    and the feature IoC files; it needs its own plan.
  - `Main/Features/QuickActions/Hooks/InventorySearchCampaignBehavior.cs`: a singleton whose
    `_persistedVersion` survives into a legacy save that lacks the key, so its legacy reconcile never
    fires on a second load in one process (LOW).
- **Rejected here**: backwards-clock guards (`nowHours >= anchor`) in `IsWithinSettlementDwell` and
  `OfferTownLeave`. With both lifecycle edges reset they guard no reachable path; if a future path
  skips the reset (for example the ISessionScoped work changes the ordering), reconsider them.
