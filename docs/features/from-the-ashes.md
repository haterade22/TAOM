# From the Ashes

## Overview

From the Ashes lets the player's company work on the map's buildings. In a looted village of the player's own realm, the company spends a day clearing the ruin and raises the village back to its normal state, which earns the goodwill of the notables and the village's owner. In the keep menu of a town or castle the player's clan owns, the company lends its hands to the head of the fief's build queue and adds construction points every hour. An evil player's rebuild is thrall labour: captives do the work and the notables give nothing. Since #856.

## Why This Exists

- **Vanilla behavior:** a looted village heals by itself, a little each day (`VillageHealCampaignBehavior` calls `IncreaseSettlementHealthAction`), and a build queue advances by the fief's daily construction number. The player's party has no way to speed up either.
- **TAOM requirement:** a company standing idle in a ruined village should be able to help, and the help should cost something real (hours, labour, materials) and pay in relations and Athletics.
- **Without this feature:** the player waits for the daily heal, or rides away.

## Architecture

### Design Challenge

- Time must run while the company works, but a standard game menu stops campaign time. The work is a wait menu, so the traps of the field camp apply (see [field-camp.md](field-camp.md): "Menus stop time", and #567, where a false wait-menu condition hid every option).
- The job must survive a save inside the wait menu without a throw in a load handler (a throw loops the loading screen, `docs/reviews/lessons/campaign-mechanics.md`).
- Several engine facts are easy to get wrong: a rebuild finishes with `IncreaseSettlementHealthAction.Apply(settlement, 1f)` and never with `ChangeVillageStateAction.ApplyBySettingToNormal` alone (the health would stay low); `Town.AllTowns` holds no castles; `Settlement.Notables` holds only the notables physically present.
- Leaving the wait menu needs a destination. `DefaultEncounterGameMenuModel.GetGenericStateMenu` has no village case in its settlement branch, so a bare `GameMenu.ExitToLast` from a village leaves no menu to land on and soft-locks the player.

### Solution Approach

`FromTheAshesService` holds the rules and the job. The rule methods (`CanStart`, `Begin`, `Progress`, `HoursDue`, `Validate`, `RewardsFor`, `CostFor`) write nothing: they read their arguments, the config and the live gates (the MCM switch, the co-op session, enlistment, the stored job). The job methods (`TryStart`, `Advance`, `Complete`, `Stop`, `Interrupt`, `Abandon`) move the one job in `AshesJobStore` through `IAshesWorldAdapter`. Time is always passed in as campaign hours. The menu hooks are guarded calls into the service.

- **Progress is never stored.** `AshesJob` keeps the start time (a double of campaign hours) and the length; the wait menu's progress is `(now - start) / length`, so a save in mid-job resumes exactly. A project's construction points are paid by `HoursDue` (whole hours elapsed less `HoursCredited`), so a fast-forward, a load or a second look at the same minute pays each hour once.
- **Rebuild.** Labour (20 healthy troops, or 20 healthy regular captives for the thrall variant) is a requirement and is never consumed. The thralls' count is the prison roster's regulars less its wounded regulars: captive heroes and wounded prisoners never count. The materials are checked at the start and again, and taken, at completion. The village is restored first and the materials taken after, so a village the engine will not raise costs nothing. If the materials are gone by then, the job ends with its own message ("The work on {SETTLEMENT} stops short") and nothing is taken.
- **Project help.** The head of `Town.BuildingsInProgress` gets 15 points an hour (300 over the 20 hours), mirroring `BuildingsCampaignBehavior.TickCurrentBuildingForTown`: only below level 3, never while the fief is under siege, then `BuildingHelper.CheckIfBuildingIsComplete`. When the head completes, the next queued project is picked up on the next credit. If the queue empties mid-job, the job completes early and its message names the last building worked on (kept in memory on the job, never saved). Materials are paid at the start.
- **Refund.** The materials of a project come back to the party when the job ends by a forced cancel: any cancel verdict (the fief changed hands, a siege, a map event, a co-op session, enlistment, the player left) or a fault. Credited hours stay credited. Stopping by choice keeps the materials spent. A rebuild paid nothing at its start, so it has nothing to refund. A refund is a world write, so it needs `IsAuthority`, and it never throws.
- **Side.** The adapter reports the player's side as one `SideIds` pair, `FactionSides.OfHero(hero)`: the player clan's map faction (a kingdom, or the clan itself when independent) with that faction's culture. The service resolves it with `IAlignmentService.ResolveSide(SideIds)`, so the id and the culture are always those of the same entity (#744, #957). Evil is the thrall variant. `AreEnemyAlignments` is not used, because it treats Neutral as everyone's enemy. A Neutral player gets the Free version.
- **Ownership.** A rebuild needs the village's map faction to be the player's map faction (an independent player's is his own clan). Project help needs the player's clan to own the fief and `!Town.IsOwnerUnassigned`, the gate vanilla's keep stash uses. The materials' stash obeys the same gate.
- **Materials** come from the party first, then from the stash of the fortification that owns the settlement (the village's bound fortress, or the fief itself), only when the player's clan owns that fortification and its owner is assigned. Counting and taking walk the same sources in the same order.
- **One site rule.** `CanStart` and `Validate` call the same private `SiteBlock(kind, site)`, so a start refusal and a running job's cancel cannot drift apart. Its last check is the empty queue, so an enlisted player is told about enlistment before an empty queue. `CanStart` shows the hide-class blocks (not a looted village, not the owner, nothing to do here, busy) before it asks the single-player question, so a co-op peer sees no option exactly where single-player sees none.
- **Single-player only.** The start gate is `CoopSessionPolicy.IsSinglePlayerAuthority` (no live session, no deferral to a host, no dedicated server, and `IsAuthority`). `Validate` applies the same predicate to a running job, so a job carried into a co-op session cancels with no penalty. Every world write also checks `ICoopSessionProvider.IsAuthority`.
- **The MCM switch** (`Fief Management / From the Ashes`, default on) hides only the start options. A running job always finishes or stops normally, and the hourly reconcile never reads the switch.
- **Pre-emption.** A `MapEventStarted` listener cancels the job (no penalty, refund) when the main party is among the involved parties. The hourly reconcile and the wait menu's own tick catch the rest (a map event the listener missed, a siege, a changed owner).

### The landing

Every way a job ends (finish, Stop, a cancel, a fault, an init that finds no running job) leaves the wait menu through `FromTheAshesLanding.Land`, in this order:

1. The menu the engine itself would show, `EncounterGameMenuModel.GetGenericStateMenu()` (through `IGameMenuAdapter.GenericStateMenuId`), when it returns an id that is not our wait menu. This is what vanilla's `SwitchToMenuIfThereIsAnInterrupt` does, and it lands a besieged fief on `menu_siege_strategies` or `encounter_interrupted_siege_preparations`.
2. Else the settlement's own menu (`IGameMenuAdapter.CurrentSettlementMenuId`: `town`, `castle` or `village`).
3. Else, while the party is still in a settlement, `IEncounterAdapter.Finish(forcePlayerOutFromSettlement: true)`, which walks the player out.
4. `GameMenu.ExitToLast` only when the party is in no settlement.

Landing on a settlement menu, which stops campaign time, is safe here: the party was already on such a menu when the job started, and once the job ends nothing timed is waiting (the store is cleared before any world write). The trap in [field-camp.md](field-camp.md) is about timed work left behind a time-stopping menu. The landing never checks "the menu is now X" with `EnsureMenuOpen`, because the village, `town_outside` and `castle_outside` initialisers redirect, so an exact-id test would fail on a successful landing.

### Component Diagram

```
from_the_ashes.json ── FromTheAshesConfigProvider ──┐
TaomSettings.EnableFromTheAshes ─ SettingsProvider ─┤
                                                    ├─ FromTheAshesService ── IAshesWorldAdapter ── engine
village_looted / town_keep / castle ─ StartOptions ─┤        │
wait menu (taom_ashes_wait) ─ WaitMenu ─┐           │        └─ AshesJobStore ── FromTheAshesBehavior (SyncData)
HourlyTickEvent, MapEventStarted ─ Reconcile ─┤     │
                                        Landing ────┘── IGameMenuAdapter, IEncounterAdapter
```

### The numbers

| | Rebuild (Free) | Rebuild (Evil, thrall labour) | Project help |
|---|---|---|---|
| Hours | 24 | 24 | 20 |
| Labour | 20 healthy troops | 20 healthy regular captives | 40 healthy troops |
| Materials | 40 `hardwood`, at completion | 40 `hardwood`, at completion | 30 `hardwood`, 10 `clay`, 5 `tools`, 5 `iron` (Iron Ore), at the start |
| Notable relation each | +3 | 0 | +1 |
| Owner relation | +5 (none when the owner is the player) | +5 | none |
| Athletics XP per hero in the party | 200 | 200 | 150 |
| Stop early | -1 with each notable | 0 | no penalty; credited hours stay, materials stay spent |
| Forced cancel | nothing written | nothing written | materials return to the party; credited hours stay |
| Construction | none | none | 15 points an hour |

### State matrix (a job in progress)

| State mid-job | Detected by | Action |
|---|---|---|
| Load with the menu persisted | menu init, then tick | resume; credit only `HoursDue` |
| After a load inside the job | vanilla | time stays paused until the player presses play; the wait menu resumes from the stored start |
| Load, record bad or settlement unresolvable | `AshesJobStore.LoadFrom` (including a length beyond 168 hours), `Validate` | drop with one warning; no world write |
| Player picks Stop | the option | Rebuild: the penalty only, materials never taken. Project: no penalty, credited hours stay, materials stay spent |
| Village recovers by itself | tick or hourly (`!IsLooted`) | cancel, no penalty, message |
| Fief besieged or raided | hourly or the tick, through `Validate` | cancel, no penalty, project materials refunded; lands on the siege menu |
| Map event on the main party | the `MapEventStarted` listener, else the hourly look or the tick | cancel, no penalty, project materials refunded; the engine shows the encounter |
| Owner changes or the ownership rule fails | `Validate` | cancel, no penalty, project materials refunded |
| Player captured, or the party leaves the settlement | `CurrentSite` is null or another settlement | cancel, project materials refunded |
| Enlisted, or in another lord's army | `Validate` | cancel, project materials refunded |
| Switch off mid-job | n/a | the job continues; only the start options hide |
| Co-op session live, or the peer loses authority | `Validate` (`CoopSessionPolicy.IsSinglePlayerAuthority`) | cancel, no penalty; the refund and every other write need `IsAuthority` |
| Queue empties mid-job (project) | `AddConstruction` returns false, or `Validate` | complete early, rewards paid, the message names the last building |
| The next queued project becomes the head | the next credit | the points go to the new head; the status line names it |
| Materials gone at completion (rebuild) | `End` | message, nothing taken, nothing rebuilt |
| An exception in a callback | the class's catch | log once (naming the callback, and whether a job was dropped), drop the job, refund a project; a fault in the wait menu then lands, while a fault in the hourly reconcile or the map-event listener only drops the job (`FromTheAshesReconcile.Fault`) |

## Configuration

### Config File: `Main/_Module/ModuleData/settlement_buildings/from_the_ashes.json`

Three objects, `rebuild`, `thralls` and `project`. Eight fields are shared; two belong to the rebuild objects and one to the project, and a field outside its variant is neither read nor required. An invalid or missing field reverts to its default on its own; a missing object reverts that whole variant; one summary warning names every field that reverted. A missing file or one that is not a JSON object reverts everything. Whole-number fields refuse a fractional value. The file is read once per process; an edit needs a restart.

| Field | Read by | Type | Valid | Meaning |
|---|---|---|---|---|
| `hours` | all three | number | finite, 1 to 168 | length of the job in campaign hours |
| `labour` | all three | integer | 0 to 500 | healthy troops (regular captives for `thralls`) that must be in the party; never consumed |
| `hardwood`, `clay`, `tools`, `iron` | all three | integer | 0 to 1000 each | materials by item id (`iron` is Iron Ore) |
| `notableRelation` | all three | integer | 0 to 20 | relation gained with each notable present at the end |
| `athleticsXp` | all three | integer | 0 to 5000 | Athletics XP for each hero in the party at the end |
| `ownerRelation` | `rebuild`, `thralls` | integer | 0 to 20 | relation gained with the village's owner |
| `stopPenalty` | `rebuild`, `thralls` | integer | -20 to 0 | relation change with each notable when a rebuild is stopped early |
| `constructionPerHour` | `project` | number | finite, 0 to 1000 | construction points added each hour |

### Current Values

The shipped file carries the table above. `ShippedFromTheAshesConfigTests` loads it with no warning and pins the defaults to it.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/FromTheAshes/FromTheAshesModule.cs` | the feature module: services and the behavior |
| `Main/Features/FromTheAshes/FromTheAshesService.cs` | the rules and the job |
| `Main/Features/FromTheAshes/AshesJob.cs` | the saved job, its in-memory last project name, `AshesJobKind` |
| `Main/Features/FromTheAshes/AshesSiteSnapshot.cs` | the engine-free facts the rules read |
| `Main/Features/FromTheAshes/AshesTypes.cs` | the enums and result records of the service |
| `Main/Features/FromTheAshes/AshesJobStore.cs` | the one job, its save halves and the bad-record drop |
| `Main/Features/FromTheAshes/FromTheAshesConfigProvider.cs` | validating loader for the JSON |
| `Main/Features/FromTheAshes/FromTheAshesSettingsProvider.cs` | the MCM switch, cached lazily |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesBehavior.cs` | campaign entry point: events and SyncData |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesStartOptions.cs` | the three start options |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesWaitMenu.cs` | the one wait menu and its Stop option |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesReconcile.cs` | the hourly reconcile and the map-event listener |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesLanding.cs` | the one exit from the wait menu |
| `Main/Features/FromTheAshes/Hooks/FromTheAshesTexts.cs` | status line, tooltips and end messages |
| `Main/Adapters/IAshesWorldAdapter.cs` | the world the service reads and changes |
| `Main/Adapters/AshesWorldAdapter.cs` | the TaleWorlds side of it |
| `Main/Adapters/IGameMenuAdapter.cs`, `GameMenuAdapter.cs` | gained `GenericStateMenuId` (additive) |
| `Main/_Module/ModuleData/settlement_buildings/from_the_ashes.json` | the numbers |
| `Main/Features/TaomSettings.cs` | `EnableFromTheAshes` |
| `Main/Composition/FeatureModules.cs` | the module line |

## Dependencies

- `IAlignmentService` (Execution): the service resolves the player's side from the snapshot's kingdom and culture ids.
- `IEnlistmentStateQuery` (Enlistment): no work while enlisted.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): single-player gate and `IsAuthority`, see [coop-interop.md](coop-interop.md).
- `IHeroSkillXpAdapter`, `IGameMenuAdapter`, `IEncounterAdapter` (Adapters): Athletics XP, menu switching and the engine's own landing choice, and the encounter that walks the player out of a settlement.

## Tests

- `TAOM.Tests/Features/FromTheAshes/FromTheAshesServiceTests.cs`: the rules, the job, its rewards, the refund and the side resolution, against a fake world.
- `TAOM.Tests/Features/FromTheAshes/AshesJobStoreTests.cs`: the save halves and the bad-record drop.
- `TAOM.Tests/Features/FromTheAshes/FromTheAshesConfigProviderTests.cs`, `ShippedFromTheAshesConfigTests.cs`: one test per validation rule, the per-variant schema, and the shipped file.
- `TAOM.Tests/Features/FromTheAshes/FromTheAshesTextsTests.cs`: which text key each block and outcome maps to.
- `TAOM.Tests/Features/FromTheAshes/AshesWorldAdapterPinTests.cs`: source pins for the engine calls (`IncreaseSettlementHealthAction.Apply(s, 1f)`, level below 3, the stash gate, captive labour, the name cache).
- `TAOM.Tests/Features/FromTheAshes/FromTheAshesSettingsProviderTests.cs`: the MCM switch, its group and its hint.
- `TAOM.Tests/Features/FromTheAshes/FromTheAshesWiringTests.cs`: the module line, the per-campaign behavior, and the source pins (the wait-menu condition is the literal `true`, one isLeave option, one landing, one exit, every Hooks file under 150 lines).
- `TAOM.Tests/Features/FromTheAshes/FromTheAshesBehaviorTests.cs` (`RequiresGame`): the per-campaign reset and the exact SyncData round trip.
- `CampaignHotPathSettingsProvidersTests`: the lazy settings accessor; `SettingsFingerprintTests` and [coop-interop.md](coop-interop.md): the setting counts.

## Known Limits

- The thrall variant's notable relation is configurable but defaults to 0, by design.
- A rebuild ends short, with a message and nothing taken, if the player sells the materials before it completes.
- A forced cancel refunds a project's materials to the party, never to the stash they may have come from.
- After a load inside a job, campaign time stays paused until the player presses play. This is vanilla behaviour for any load into a menu; the wait menu and its progress resume from the stored start.
- Only the main party works; there is no AI use of the feature.
- Hours of a project are credited as whole hours, so a fractional length credits its last partial hour at the end.
- A solo player with a co-op mod enabled sees the options greyed out with "Not available in a shared campaign." (the start gate stands down for any live or unprobeable co-op layer, and `FromTheAshesTexts` does not hide `NotSinglePlayer`).
- If the player encounter is missing at landing, `EncounterAdapter.Finish` does nothing. The source shows the encounter survives a load, so this is not expected to occur.

### In-game checklist (owed)

All of this is unseen in game. Items marked verified in source were read in the engine source; their in-game confirmation is owed like the rest.

- A looted village of your realm shows "Help raise the village from its ashes" on its menu; a thrall player sees "Set your thralls to rebuild the village" instead; a village of another realm shows neither.
- A rebuild runs 24 hours with a progress bar, ends on the village's own menu, the village is Normal with its health restored and 20 more militia, hardwood is gone, notables +3, owner +5, Athletics XP on each hero.
- An owner rebuild: with the village in your own clan's hands, the owner relation is not changed.
- Stop in the middle of a rebuild: -1 with each notable, nothing taken, the village stays looted.
- A save and load in the middle of a rebuild: the wait menu and the bar resume, the rebuild still completes, and time is paused until you press play.
- A thrall rebuild end to end: with an evil kingdom (or an evil culture and no kingdom), 20 healthy captives, no notable gain, the message "stands again, raised by your thralls". A captive lord or wounded prisoners do not count toward the 20.
- A player whom `IAlignmentService.ResolveSide` reports as Neutral gets the free version.
- Project help appears in the keep menu of an owned town or castle with a project in the queue and not otherwise; it takes the materials at once, adds 15 points an hour, and a save and load in the middle resume.
- A castle works as well as a town (`town_keep` and `castle` are separate menus).
- The queue's next project is picked up mid-job, the status line names it, and an early completion names the building in its message.
- A siege that begins mid-project cancels the job and lands on the siege menu (verified in source: `menu_siege_strategies` and `encounter_interrupted_siege_preparations` come from `GetGenericStateMenu`), and the project's materials are back in the party.
- Cancel inside a village (the village recovers by itself) lands on the village menu, not on a blank map. A raid is a defensive path the source rules out inside a settlement; a cheap spot check only.
- A hostile party engaging the main party mid-job cancels it with no penalty and the encounter shows. The source rules this out inside a settlement; a cheap spot check only.
- The party-first-then-stash materials: with the hardwood in the stash only, the option is enabled; with it nowhere, the tooltip names it; an unassigned fief owner disables the stash.
- Switching the MCM option off hides both options and lets a running job finish.
- `taom.time_status` after a load inside a job, and after each exit (finish, Stop, cancel), shows no stuck menu context or time lock.
- Verified in source; in-game confirmation owed: the `PlayerEncounter` survives a load inside the persisted wait menu. The save holds the player encounter (`Campaign.cs:458-459`, SaveableProperty 54) and the party's settlement (`MobileParty.cs:82-83`, SaveableField 1001); menu init checks the job against `CurrentSite()` and lands if they disagree.
- Verified in source; in-game confirmation owed: no party engages the main party inside a settlement (`EncounterManager.cs:50`, `:147-151`), so a hostile party cannot engage it inside a looted village.
- Verified in source, in-game confirmation owed: a forced exit (`EndWait`, then the landing order above) leaves time control and the village menu as expected, and the `village` menu after the rebuild shows the normal options without reopening the settlement.

## How to Change a Value

1. Edit `from_the_ashes.json` (a restart applies it).
2. A new material needs a field in `AshesVariantConfig`, the field table in `FromTheAshesConfigProvider` and a line in `CostFor`.

## Changelog

- 2026-10-20 (#957): the player's side is one `SideIds` pair from `FactionSides.OfHero(hero)`; the kingdom-less branch no longer pairs an empty id with the hero's own culture.
- 2026-10-19: first build on feat/settlements-buildings (#856): village rebuild with a thrall variant, project help, MCM switch, JSON config. Not yet seen in game.
- 2026-10-19: review fixes. Every exit lands through one helper (the engine's own choice, then the settlement menu, then a finished encounter), which fixes a soft-lock in villages. The menus split into start options, wait menu, reconcile and landing (ADR-002), with one wait menu id. A forced cancel refunds a project's materials; the gold cost is gone; thrall labour counts regular healthy captives; a map event cancels the job. The config schema is per variant and the saved start is a double.

## GitHub Issue

- **Issue:** [#856](https://github.com/haterade22/TAOM/issues/856) From the Ashes: help rebuild raided villages and finish fief projects
- **Status:** Shipped on `bannerlord-1.5.x` in `c4e4b4d43` (v2.0.35; its text tests gained the game tag in `79c5d3101`).
  Issue open: the in-game check is owed; its strings are translated in all 12 languages (2026-11-12), tracked in [#931](https://github.com/haterade22/TAOM/issues/931).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/features/coop-interop.md](./coop-interop.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/feature-map.md](../reference/feature-map.md)

<!-- backlinks-end -->
