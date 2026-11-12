---
paths:
  - "Main/**/*.cs"
  - "Client/**/*.cs"
  - "TAOM.Tests/**/*.cs"
---

# TAOM Architecture Quick Reference

Full guide: `docs/ai-includes/architecture.md`

## Non-Negotiable Rules

| Rule | Detail |
|------|--------|
| Entry points <150 lines | ADR-002: delegate immediately to service. Gate: `EntryPointLineCountGateTests` |
| No sealed types in services | ADR-007: `ICareerHeroAdapter` not `Hero`; the one exception is a protected-virtual boundary seam (ADR-007 "Exceptions"). Gate: `ServiceSignatureGateTests` |
| Interfaces that earn their file | Every adapter has one (ADR-007); a service gets one only when a test fakes it or a second class implements it (ADR-002) |
| Constructor injection only | No service locator in services. Gate: `ServiceLocatorGateTests` |
| Convert at boundary | Adapt sealed types in the entry point, not deep in services |
| `?.` for computed properties | TaleWorlds getters crash before your null check — see `adapters.md` |
| Core names no client-only assembly | ADR-013: code that needs SandBox.View, Gauntlet, a ViewModelCollection or WinForms goes to TAOM.Client behind a core interface with a null; `ServerJitGateTests` fails a new core method that names one (unless its key is already in the baseline) |
| Writers before gates | An engine flag, stat or total a plan or gate relies on lists every writer, vanilla first, with citations, and cites the line that builds it, not only its getter (`lessons/adapters-taleworlds-api.md`, Hammerhand; `ItemRoster.TotalFood` counts the herd's meat and `SellItemsAction` re-prices every unit: `docs/reviews/rca-provender-2026-10-23.md`) |
| Readers before writers | State a feature creates for the first time (a hero in an AI party roster, a role on an AI party) lists every vanilla reader and cleanup of that state, each with a quoted line, before the first test; vanilla code assumes it is empty (`lessons/campaign-mechanics.md`, LordRetinue 2026-11-12) |
| Server-safe startup (ADR-013) | A call in a `SubModule` hook or `ManualPatchApplicator.ApplyAll` whose callee names a client-only assembly (`ClientAssemblies.cs`) goes behind the server check in a `[MethodImpl(MethodImplOptions.NoInlining)]` helper, with a row in `SubModuleServerGuardTests`: its IL walk sees only `SubModule`'s own body, not the callee's |
| Outcome lines | A feature proves its in-game check with `IOutcomeLog` (constructor) or `Outcomes` (static patches, pre-IoC): `Pass`/`Fail`/`Skip` with a `const string` id, plus a manifest in `tools/ingame_checks/`; an empty catch calls `Swallowed`, or carries `// swallow-ok: <reason>` when it is silent on purpose ([ingame-verdicts.md](../../docs/features/ingame-verdicts.md)) |
| Outcome cost | Per hit, agent, tick, model query or frame, and off the main thread: an `OutcomeCounter` held in a field, never `Pass`/`Fail`/`Skip`, which write a line flushed on the calling thread; build `detail` only where the line is written |

## IoC Lifetimes

| Lifetime | Use For |
|----------|---------|
| `Reuse.Singleton` | Services, engines, caches |
| `Reuse.Transient` | Hooks, stateless helpers |

## Test Coverage

Every decision path in a service or engine has a test, judged by the two-edit test. No coverage percentage is measured or claimed.

## Entity State Matrix (load-path mutations)

A `CampaignBehaviorBase` that changes Hero, Settlement or Clan state on load lists every state first (idle in settlement, in the player party on the map, in the party in a settlement, dead or disabled, prisoner, fugitive) and says mutate or skip, with a reason. A destructive load-path action needs stricter guards than its new-game twin; check idempotence before copying a sibling.

**Config Providers MUST Validate:** config files, MCM settings and editor-visible fields validate at load, not only parse ([config-providers.md](config-providers.md)).

## Singleton Services Holding Per-Campaign State MUST Have a Session-Reset Story (MANDATORY)

TAOM services are `Reuse.Singleton`: they live for the PROCESS, not the campaign. A singleton that
owns campaign data (an order book, a camp dictionary, a party-tracker cache) has two lifecycle
holes that SyncData does not cover:

1. **A brand-new campaign never calls `LoadFrom`.** The engine calls a behavior's `SyncData` in
   loading mode only when a save record exists, so campaign B inherits campaign A's dictionaries, and
   B's first save then PERSISTS A's state into B. (Shipped as 2 CRITICALs in the camps port, found
   independently by two reviews.)
2. **A loaded save has NEW engine objects under the old ids.** Any transient cache keyed by id but
   holding object references (MobileParty trackers, widget maps), and any absolute clock, latch or
   shown-flag, silently goes stale on load and drives ghosts or disables its mechanism.

**Rule:** every singleton holding per-campaign state exposes `ResetForNewSession()`, and the reset
runs before anything can read the state: in the constructor of a behavior that `SubModule.OnGameStart`
builds with `new` in every campaign. `OnGameStart` precedes `LoadBehaviorData` and every campaign
event, so a load's `SyncData` then restores its record over the reset, and a save without one stays
reset. Pin the `new` with a source-text test (`WarOfTheRingWiringTests`): a container-singleton
behavior is constructed once per process. Never reset persisted state from `OnSessionLaunched` or
`OnNewGameCreated` (listeners run newest-first, so a behavior added later reads it first) or from
`RegisterEvents` (on a load it runs after `LoadBehaviorData`). A transient cache that nothing reads
before `OnSessionLaunched` may still reset there. `LoadFrom` additionally clears every transient
cache/clock/latch. Both paths get tests (`*SessionResetTests`). When reviewing, walk a second
campaign in one process and a load-with-same-ids explicitly.

**Why:** `docs/reviews/rca-yotthani-camps-2026-08-23.md` Class 1. The singleton-service precedent
(config stores) made process lifetime look safe; campaign data is a different animal. The
`OnSessionLaunched` reset this rule first prescribed ran after the momentum behavior had already
read the stale War of the Ring phase (#764, `docs/reviews/rca-wotr-session-reset-2026-10-08.md`).

## Engine-Float Decision Gates: NaN Must FAIL the Gate (MANDATORY, the runtime sibling of the config rule in config-providers.md)

Every NaN comparison is false, so a gate's polarity decides.

1. **Config float:** reject non-finite at load with `FiniteFloatValidator`.
2. **Engine float at a decision gate:** write a positive requirement, `if (!(x > 0f)) return`, and return null from a `bool?` service so vanilla decides.
3. **Float-to-int cast:** check finiteness and range at the cast, because NaN and Infinity cast to `int.MinValue`, then gate the value, not arithmetic on it.
4. **A new input into an old calculation:** re-audit every downstream gate and guard the service's exit. A moved gate is a new gate.

One NaN test per gate. When you touch a method, gate every float decision path in it, not only the lines you added. `float.IsFinite` does not exist on net472. Reviewed by lens 5 rule 4b. RCAs: `rca-combat-mechanics-2026-07-02.md`, `rca-troopweight-result-frame-2026-07-17.md`, `rca-settlement-food-2026-09-06.md`.

## Lookup Functions With Fallbacks: Validate Before Lookup (MANDATORY)

A lookup that returns a default for unknown input (`RaceManager.GetRaceNameFromId` returns human) resolves names for logs; it does not validate. When a decision depends on the result, check validity first (`IsValidRaceId`) and treat invalid as not allowed. Test: the lookup returns its fallback and the caller rejects the input. `MBObjectManager.GetObject<T>` returns null for a missing id, so null-check it instead.

One engine type for many config variants: enumerate every `GetType`-keyed engine path first ([issue-and-quest-system.md](../../docs/reference/engine/issue-and-quest-system.md)).

## File Layout

```
Main/Features/MyFeature/
├── IMyFeatureService.cs     ← only if a test fakes it or a second class implements it
├── MyFeatureService.cs
├── MyFeatureIoC.cs          ← Reuse.Singleton registrations
├── Models/
│   └── TaomMyModel.cs       ← GameModel override (if needed)
└── Hooks/
    └── MyPatch.cs           ← Harmony patch (if needed)
Main/Adapters/
├── IMyTypeAdapter.cs
└── MyTypeAdapter.cs
TAOM.Tests/Features/MyFeature/
└── MyFeatureServiceTests.cs
```

## Mission-scope agent handles and the engine's threads (MANDATORY)

Two facts about `TaleWorlds.MountAndBlade.Agent` shape every mission-time feature (#592, #595; RCA
`docs/reviews/rca-warg-clip-on-horse-2026-09-13.md`):

1. **A deleted `Agent` is a live handle to whoever inherits its index.** The engine hands a deleted
   agent's index to the next agent it builds, and the managed object keeps native pointers captured at
   creation: `State`, `IsActive()`, `IsFadingOut()`, `Position`, velocity and `SetActionChannel` answer for
   the slot's NEW tenant, while `Monster`, `Name`, `Character`, `Health` and `Team` stay the dead agent's.
   So: never key mission state by `Agent.Index` without evicting in `OnAgentDeleted`; never hold an
   `Agent` or `IAgentAdapter` across frames and trust `IsActive()` alone; gate every held handle with
   `AgentSlotIdentity.IsCurrentOccupant(agent)` before it touches native state. The adapter cache is
   keyed by object and evicted for you; a restore closure, a shadow list, a machine's target agent are not.
2. **No engine callback is main-thread by contract.** Agent ticks, `Team.Tick`, `TeamAI`, AI `Formation.SetMovementOrder`, `Formation.GetOrderPositionOfUnit` (worker pool) and `AfterAsyncTickTick` run off the main thread and overlap views and UI. Blows, spawns and fades belong in `OnMissionTick`. A store reachable from a patch on any of those takes a lock. A collection an engine callback writes goes through `DeferredCallbackQueue.RunOrDefer`, or a lock when order does not matter. Wire `MissionThreadGuard.NoteCall` into a new native write. Table: [mission-frame-threads-and-native-costs.md](../../docs/reference/engine/mission-frame-threads-and-native-costs.md) "Which thread runs your target".
3. **One behaviour tree per agent.** `BehaviorTreeMissionLogic.trees` holds one tree per agent, and every soldier whose race or culture has an ability profile already carries the race-ability tree, so a second `BehaviorTreeAgentComponent` takes its slot. A new per-agent tree for such soldiers runs from its feature's own mission tick, as `DwarfTreeHost` does ([dwarf-attacks.md](../../docs/features/dwarf-attacks.md)).
