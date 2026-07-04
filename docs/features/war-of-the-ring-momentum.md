# War of the Ring Momentum

## Overview

Tracks Evil-vs-Good war progress on a signed "momentum" meter fed by battles, sieges, raids, army-gatherings, and a daily strength differential. A persistent on-map slider shows who is winning; clicking it opens a detail popup (faction banners, leaders/allies, per-type breakdown, total stats). When one side's momentum lead reaches the victory threshold — or one side's kingdoms are all destroyed — the War of the Ring **ends**: an inquiry announces the winner, the peace-block is lifted, at-war kingdoms make peace, and the meter freezes.

Ported from LOTRAOM 1.2.12's "Momentum" system onto TAOM 1.4.6 (issue #327).

## Why This Exists

TAOM already had the *war-triggering* half — the `WarOfTheRingService` phase machine (`Peace → IsengardWar → FullWar`, see [war-of-the-ring.md](war-of-the-ring.md)) — but no way for the player to see who is winning and no campaign arc that resolves the war. Players wanted visible Evil-vs-Good progress. This feature is the *progress + victory* half.

## Architecture

### Layer stack (ADR-002/007)

```
CampaignEvents  →  WarOfTheRingMomentumBehavior (thin entry point)
                        │ snapshots engine args via
                   IWarEventSnapshotAdapter  (MapEvent/Siege/Raid/Army → flat DTOs)
                        │ delegates to
                   IMomentumEventService     (scoring + decay, pure)
                   IMomentumEnrollmentService (side membership from IAlignmentService)
                   IMomentumVictoryService   (victory decision + peace-out)
                        │ mutate
                   IMomentumStateStore → MomentumWarState (pure domain math)
                        │ read by
                   IMomentumQueryService  →  UI (MapView + popup VMs)
```

Every service is pure and unit-tested; the behavior holds no logic beyond routing. Sealed TaleWorlds types never cross into a service — the snapshot adapter flattens `MapEvent`/`Army`/etc. to plain DTOs at the boundary.

### Momentum model

- Per-side momentum is stored as an **int on a ×100 internal scale** (`MomentumWarState.MomentumScale`) for battle-scaling precision. `InternalMomentum = (Free − Evil) / 100`, positive = Free ahead.
- Each side keeps `Dictionary<MomentumActionType, Queue<MomentumEvent>>`, capped 100 per type. Events **decay**: their value is subtracted back out when their `EndTimeHours` passes (battles/sieges/raids 504h, army 168h, strength 12h).
- Config event values are **internal-scale units** and are ×100'd into the store on the way in (`MomentumEventService`). This is a deliberate fix of a donor bug — LOTRAOM added config values raw while comparing `raw/100` against the threshold, so its own tuning comments ("~2 sieges for victory") were 100× off. Here `siege 250 + siege 250 ≥ threshold 500` as intended.

### Enrollment (deviation from LOTRAOM, justified)

LOTRAOM enrolled kingdoms from its own war-event scripting. TAOM's phase machine already owns war declaration, so momentum **enrolls dynamically**: on session launch and each daily tick while `CurrentPhase == FullWar`, every kingdom whose `IAlignmentService.GetKingdomSide` is Free or Evil is swept into its side (Neutral excluded). Enrollment is bookkeeping only — it never declares wars or alliances (the donor's `StanceType.Alliance` reflection is impossible on 1.4.6 anyway; the enum has no `Alliance` value). Dynamic enrollment automatically covers player-founded and revolt kingdoms that scripted enrollment could not.

### Victory ↔ phase-machine integration

`WarPhase` gained a terminal `WarEnded` value and `IWarOfTheRingService` gained `EndWar(WarOutcome)` / `Outcome`. All three peace-block layers (`TaomDiplomacyModel.IsAtConstantWar`, `TaomKingdomDecisionPermissionModel`, the `MakePeaceAction` prefix) route through `IsWarOfTheRingActive` / `ShouldBlockPeace`, which key off `CurrentPhase == FullWar` — so flipping the phase to `WarEnded` lifts all three at once with **zero patch changes**.

The victory sequence order is load-bearing (pinned by a `Received.InOrder` test): **freeze the state → `EndWar` → peace-out cross-side pairs**. `MakePeaceAction` is blocked until the phase leaves `FullWar`, so peace must come after `EndWar`. Both victories are gated on the player-participation requirement (LOTRAOM parity — the donor gates the Evil branch too). A load-reconcile in `OnSessionLaunched` calls `EndWar` if a save says the momentum war ended but the phase machine hasn't caught up (idempotent).

## Configuration

`Main/_Module/ModuleData/momentum/momentum.json` (validated; invalid values revert to default + warn; `Reuse.Singleton` — edits need a full game restart):

| Field | Default | Meaning |
|-------|---------|---------|
| `enabled` | true | JSON fallback for the master toggle |
| `victoryThreshold` | 500 | internal momentum lead to win |
| `events.maxBattleMomentum` | 300 | battle cap (scaled by casualties ÷ loser strength) |
| `events.siegeMomentum` | 250 | fixed per captured settlement |
| `events.raidMomentum` | 200 | fixed per raided village |
| `events.armyMomentum` | 200 | fixed per army gathered |
| `events.maxStrengthMomentum` | 300 | daily strength-differential cap |
| `durationsHours.*` | 504/504/504/168/12 | decay windows |
| `player.requireParticipationForVictory` | true | war can't end until the player has fought enough |
| `player.participationMultiplier` | 1.5 | momentum ×1.5 when the player takes part |
| `player.minimumPlayerEventsForVictory` | 5 | player events needed before either side can win |
| `strengthRatioForMaxMomentum` | 4.0 | how many × stronger a side must be for the max daily award |

**MCM** ("War of the Ring/Momentum" group, `TaomSettings`): `MomentumEnabled`, `ShowWarOfTheRingMapMeter`, `MomentumVictoryThreshold` (100–2000), `MomentumParticipationMultiplier` (1.0–3.0), `MomentumRequirePlayerForVictory`, `MomentumMinPlayerEvents` (0–20). MCM values win over JSON; both surfaces clamp to the same ranges.

Defaults equal LOTRAOM's shipped `momentum_config.xml`, so the out-of-box balance matches the donor (minus the 100× scale bug).

## Persistence

Primitive-dict SyncData (Messengers pattern — no `SaveableTypeDefiner`), key `_taom_wotr_momentum`, serialized by `MomentumStateStore`: war flags, victor, per-side momentum/kingdoms/stats, every event queue, and the player-event list. **Event descriptions are stored resolved** (in the write-time language — a documented limitation; a save made in English shows English event tooltips after switching to French). The Diplomacy behavior persists the phase + outcome as ints. This fixes a real LOTRAOM bug: the donor never persisted the player victory-gate events, so victory progress silently reset on reload.

## Key Files

| File | Purpose |
|------|---------|
| `Domain/MomentumWarState.cs` + `MomentumSideData.cs` | pure momentum math (scale, decay, queue caps) |
| `Domain/MomentumEvent.cs`, `MomentumTotalStats.cs`, `MomentumActionType.cs`, `MomentumSide.cs` | domain POCOs |
| `Snapshots/*.cs` | flat DTOs (battle/siege/raid/army) built at the adapter boundary |
| `MomentumEventService.cs` | scoring + decay |
| `MomentumEnrollmentService.cs` | side membership sweep |
| `MomentumVictoryService.cs` | victory decision + ordered peace-out |
| `PlayerMomentumService.cs` | participation multiplier + victory gate |
| `MomentumStateStore.cs` | flat-dict SyncData + `MomentumChanged` event |
| `MomentumQueryService.cs` | UI read facade (pins the `positive = Evil` slider sign) |
| `MomentumConfigProvider.cs` + `MomentumSettingsProvider.cs` | validated JSON + MCM-over-JSON |
| `MomentumTextService.cs` | localized event-description composer |
| `WarOfTheRingMomentumBehavior.cs` | thin campaign-event entry point + SyncData |
| `WarOfTheRingMomentumIoC.cs` | registrations |
| `UI/MomentumIndicatorMapView.cs` | on-map slider MapView (adds/removes via `IMomentumUIService`) |
| `UI/MomentumPopupController.cs` + `MomentumPopupVM.cs` | detail popup |
| `UI/MomentumIndicatorItemVM.cs`, `MomentumIndicatorVM.cs`, `FactionRelationshipVM.cs`, `BreakdownVM.cs` | VMs |
| `Main/Adapters/WarEventSnapshotAdapter.cs` + `KingdomStrengthAdapter.cs` | engine boundary |
| `Main/_Module/GUI/PreFabs/MomentumView/*.xml` | prefabs (fork residue, 1.4.x-migrated) |
| `Main/_Module/ModuleData/taom_wotr_strings.xml` | 24 localization keys |

## Dependencies

`IWarOfTheRingService` (Diplomacy — the phase machine), `IAlignmentService` (Execution — Free/Evil/Neutral classification, `execution/alignment.json`), `IAllianceAdapter` + `IPlayerContextAdapter` (shared adapters), MCM (`TaomSettings`).

## Tests

`TAOM.Tests/Features/WarOfTheRingMomentum/` — 142 tests across 9 files: domain math (scale, tanh removed, queue-cap trim), config validation (one per rule), state-store round-trip (NaN reject, malformed skip, pipe-in-description, re-cap), player service (gate, multiplier, tie), event service (scoring, decay, filters, donor-parity quirks), enrollment (never-Neutral, dedup, removal), victory (threshold/elimination, player gate, `EndWar`-before-`MakePeace` ordering, idempotence), query facade (slider sign, clamp). Plus the extended `WarOfTheRingServiceTests` for the `WarEnded`/`EndWar` phase transition.

## How-To

**Retune balance:** edit `momentum/momentum.json`, restart the game (singleton cache). For live in-game tuning use the MCM group.

**Reach FullWar fast for testing:** the existing WotR `testMode` (`diplomacy/war_of_the_ring.json`, `phase2Day: 3`) triggers FullWar on day 3 — the meter appears there.

**Add a new momentum source:** add a `MomentumActionType`, a snapshot DTO + adapter method, a `Process*` in `MomentumEventService`, a duration in `MomentumDurationsConfig.GetHours`, a label in `MomentumPopupVM.ActionTypeLabel`, and a `{=taom_wotr_*}` string. The store's per-type serialization and the breakdown UI pick it up automatically.

## Deliberate deviations from LOTRAOM

- **Scale fix**: config values are internal-scale units ×100'd into the store (donor was 100× off its own tuning).
- **Raids require an enrolled kingdom**: the donor sided raids by culture with no enrollment check, so every looter raid fed Evil +200.
- **No alliance-stance setting**: `StanceType.Alliance` doesn't exist on 1.4.6; TAOM Diplomacy owns stances anyway.
- **Raid/army don't apply the participation multiplier or victory-gate credit**: donor parity — LOTRAOM applied both only to battles and sieges. Documented on the snapshot DTOs.
- **Dropped the dead tanh soft-cap** (`softCapDivisor`/`DisplayMomentum`): never wired into the donor's UI either; removed per the simplicity criterion.

## Known limitations

- Event descriptions freeze in the write-time language (stored resolved in the save).
- A battle resolving earlier in the campaign day than the daily enrollment sweep on the exact day FullWar first triggers is dropped (war "begins" that tick).
- Popup "Total:" reads positive = Free ahead while the slider reads positive = Evil ahead — each self-consistent within its own layout; no on-screen number is shared.

## Status

Built + 142 tests green + deep-reviewed (5 agents, 6 findings fixed — RCA `docs/reviews/rca-wotr-momentum-2026-07-03.md`) + Codex-reviewed. **In-game verification (meter render, popup, victory flow, save/reload) owed** — rendering ≠ live per `gui-ui.md`.
