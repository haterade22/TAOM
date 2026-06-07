# Bannerlord CampaignEvents + CampaignBehavior (Phase 9)

> **One process, traced from the decompile** (`TaleWorlds.CampaignSystem`, v1.4.5): the campaign-side hook system —
> how a `CampaignBehaviorBase` registers, subscribes to campaign events, persists, and is torn down. The campaign
> parallel to phase 4's mission backbone, used by **every** TAOM `CampaignBehavior` (CultureConversion,
> SpecialResources, Messengers, Siege, CastleRecruitment, NamedCompanions, …). Part of the phased engine study.

## WHAT it is

Campaign logic is packaged as **`CampaignBehaviorBase`** units. Each one **subscribes** to global **`CampaignEvents`**
(daily/hourly ticks, settlement-owner-changed, game-loaded, hero-killed, …) in `RegisterEvents()` and **persists**
its state in `SyncData()`. The campaign fires the events; behaviors react. This is the sanctioned, no-Harmony way to
add campaign-map behavior.

## HOW it works

### `CampaignBehaviorBase` (CampaignBehaviorBase.cs:3 — `abstract : ICampaignBehavior`)
The entire contract:
```
public readonly string StringId;            // defaults to GetType().Name
public abstract void RegisterEvents();      // subscribe to CampaignEvents here
public abstract void SyncData(IDataStore);  // persist state (Phase 6)
public static T GetCampaignBehavior<T>();   // => Campaign.Current.GetCampaignBehavior<T>()  (cross-behavior lookup)
```
**That's it — `RegisterEvents` + `SyncData` are the only two members you must implement.** A behavior is registered
via `campaignStarter.AddBehavior(new XxxBehavior())` in `SubModule.OnGameStart`; the campaign then calls its
`RegisterEvents()` (subscriptions go live) and `SyncData()` on save/load.

### `CampaignEvents` + `IMbEvent` (CampaignEvents.cs, IMbEvent.cs, MbEvent.cs)
`CampaignEvents` is the static registry of campaign events (each a `MbEvent`/`IMbEvent` instance:
`DailyTickEvent`, `HourlyTickEvent`, `DailyTickSettlementEvent`, `OnSettlementOwnerChangedEvent`,
`OnGameLoadedEvent`, `OnNewGameCreatedEvent`, `OnGameOverEvent`, `HeroKilledEvent`, `OnMissionStartedEvent`, …).
A behavior subscribes in `RegisterEvents`:
```
CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
```
The **`IMbEvent` interface (IMbEvent.cs:5-30) exposes only:**
- `AddNonSerializedListener(object owner, Action[<T…>] action)` — subscribe (owner = the behavior, for grouping).
- `ClearListeners(object o)` — remove **all** of an owner's listeners.

**There is NO `RemoveNonSerializedListener`** (confirmed v1.4.5). So you can add + clear-all-for-owner, but not
remove a single listener. The campaign's `CampaignEventDispatcher` holds the listener lists and fires each event
(e.g. `DailyTickEvent` fired once per campaign day).

### Lifecycle
```
SubModule.OnGameStart → campaignStarter.AddBehavior(new XxxBehavior())
  → RegisterEvents()                         (subscriptions go live)
  → events fire during play (DailyTick, OnSettlementOwnerChanged, …)
  → save:  SyncData(dataStore)  [IsSaving]
  → load:  SyncData(dataStore)  [!IsSaving] → then OnGameLoadedEvent fires (re-apply transient/derived state)
  → game over / new game: OnGameOverEvent / OnNewGameCreatedEvent   (no per-behavior teardown virtual — see gotcha)
```

## WHY it's shaped this way

A minimal base (`RegisterEvents` + `SyncData`) + a global typed-event bus keeps behaviors decoupled: a behavior
declares only what it listens to + what it persists, and the campaign owns the firing. `AddNonSerializedListener`'s
"non-serialized" means the subscription itself isn't saved (re-established each load via `RegisterEvents`) — which
is why `RegisterEvents` runs every campaign start/load, not once.

## TAOM relevance + gotchas
- **Every TAOM `CampaignBehavior`** subscribes here. Common events: `DailyTickEvent`/`HourlyTickEvent`/`TickEvent`,
  `DailyTickSettlementEvent` (CultureMarketplace, CastleRecruitment), `OnSettlementOwnerChangedEvent`
  (CultureConversion), `OnGameLoadedEvent` (re-apply non-engine-saved state), `CanHaveCampaignIssuesEvent`
  (CastleRecruitment issue suppression).
- **No single-listener removal** (`IMbEvent` has no `RemoveNonSerializedListener`). To stop listening, use
  `ClearListeners(owner)` (drops *all* of that owner's) or an owner-proxy. (`feedback_imbevent_remove_one_unavailable`
  — confirmed identical in 1.4.5; audit/Codex suggestions to call `RemoveNonSerializedListener` are invalid.)
- **No `OnGameEnd`/`OnFinalize` virtual** on `CampaignBehaviorBase` (the class has only `RegisterEvents`/`SyncData`).
  For singleton cleanup at campaign teardown, subscribe to `CampaignEvents.OnGameOverEvent` (best-effort) or
  `OnNewGameCreatedEvent` (`feedback_campaignbehavior_no_ongameend`).
- **`OnGameLoadedEvent` re-applies derived/non-saved state** — e.g. CultureConversion re-applies its
  `Settlement.Culture` overrides on load (Settlement.Culture isn't engine-saved). Load-path mutations must follow the
  **entity-state matrix** (`.claude/rules/csharp-architecture.md`) — destructive ops need state guards.
- **`SyncData` is the persistence half** (Phase 6) — most TAOM features `SyncData` primitives/composite strings; no
  `SaveableTypeDefiner` unless a custom class is saved.
- **`GetCampaignBehavior<T>()`** is the cross-behavior lookup (and `Campaign.Current.GetCampaignBehavior<T>()`).
- Subscribe in `RegisterEvents`, **not the ctor** (the ctor runs before the campaign event bus is ready; RegisterEvents
  is the engine-called hook).

## The native boundary
The campaign event system is **entirely managed** (`CampaignEvents`, `MbEvent`, `CampaignEventDispatcher`,
`CampaignBehaviorBase` are all C#). It operates on campaign objects (`Settlement`, `Hero`, `Clan` — `MBObjectBase`s
from Phase 5). No native boundary here — it's pure managed campaign simulation.

## Evidence (file:line, v1.4.5)
- `CampaignBehaviorBase.cs`:3-25 (`StringId`, `RegisterEvents` abstract @17, `GetCampaignBehavior<T>` @19, `SyncData` abstract @24 — and NO teardown virtual).
- `IMbEvent.cs`:5-30 (`AddNonSerializedListener` + `ClearListeners` only; no `RemoveNonSerializedListener`; arities `IMbEvent<T>`…`<T1..T5>`).
- `CampaignEvents.cs` (the static event registry), `MbEvent.cs` (`MbEvent`/dispatcher), `Campaign.cs` (`GetCampaignBehavior<T>`).
- TAOM precedent + gotchas: memories `feedback_imbevent_remove_one_unavailable`, `feedback_campaignbehavior_no_ongameend`; behaviors registered in `Main/SubModule.cs OnGameStart` via `AddBehavior`.
