# Staged Siege

## Overview

In a castle assault on a scene with recorded capture points, in the campaign or in a Custom Battle, the siege is fought in four stages, A to D. The attackers take each point in order, and a captured point brings fresh troops from both armies. In the campaign, an attacker who takes D makes the defenders fall back to the lord's hall (vanilla's hall fight follows, and a point E in the hall ends it as a win once taken). In a Custom Battle there is no hall, so taking D wins the battle. As the defender, losing D loses the castle either way. A scene with no recorded points, and every other kind of battle, stays vanilla. The MCM switch `Staged Sieges (Capture Points)` under `Battle Tactics/Staged Siege` ships off.

## Why This Exists

- **Vanilla behavior:** a wall assault is one phase. The attackers breach the gate or the walls and then kill every defender. Reinforcements come on one global timer, and the Reinforcement Wave Count option cuts overflow troops out of the battle.
- **TAOM requirement:** a layered siege in the manner of the multiplayer flag modes: outside the walls (A), the gatehouse (B), the courtyard (C), an inner point (D), then the lord's hall (E, vanilla's own hall mission). Each point must fall to unlock the next, and the army not yet committed arrives in stages.
- **Without this feature:** the defenders have no inner defence. Vanilla's `TacticDefendCastle.MustRetreatToCastle()` returns false, so once the gate is down the fight is a brawl in the courtyard that only ends when one side is dead.

## Architecture

### Design Challenge

- The engine has no "spawn a wave now" call. The deployment plan object is chosen at `Mission.Initialize`, but the spawn frames are made later, in `CheckDeployment` and, for every reinforcement batch, by `UpdateReinforcementPlan`, so reinforcements have to be steered through the settings the spawn logic is given and the frame each reinforcement spawns on.
- Team AI runs on its own thread. Anything the AI reads must be immutable, and anything that moves a formation must run on the main thread.
- Multiplayer's capture-point code (game modes, bot AI, spawn zones, marker HUD) does not work in singleplayer. Only the `FlagCapturePoint` visual is reusable.
- A defending player never reaches the hall in vanilla, so a defence that loses D has to end in a defeat instead.

### Solution Approach

One `MissionLogic`, `StagedSiegeMissionBehavior`, builds a `StagedSiegeService` per mission. It arms in `EarlyStart`, not `AfterStart`, because vanilla's siege spawn handler calls `InitWithSinglePhase` from its own `AfterStart` and the Harmony prefix there needs the service armed already. The service reconciles every 0.5 s of mission time on the main thread and publishes an immutable `StageSnapshot` through `StagedSiegeSlot`. The AI tactic, the spawn-frame patch and the HUD only read that snapshot.

- **Arming** (`StagedSiegeActivation`, guards in this order): a siege battle, no sally-out controller, not a client, siege state `OnTheWalls`, the MCM switch on, a player side known, the scene recorded. Co-op never reaches it. A Custom Battle siege passes that guard too (see "Custom battle sieges"). A battle that does not arm logs `[StagedSiege] inert: <verdict>` with the siege state and the mission kind (`campaign walls assault`, `custom battle siege` or `other`).
- **Capture rule** (`CaptureRule`): every 0.5 s the service counts humans within the point's radius (default 8 m) and within 3 m of its height, which keeps wall troops out of B and C. Progress rises over 20 s while at least 3 attackers are present and they outnumber the defenders 3 to 1. It falls over 30 s while the defenders dominate by the same ratio, and holds while contested.
- **Stages** (`StageMachine`, `StagePlan`): capturing B opens both gates (`CastleGate.OpenDoor`) so the AI can path inward. A: the attackers take the point and the attacking side's siege engines wait, unless the player commands that side: stopping engines clears the player's engine order on every engine of the side, so no team of a side the player commands stops them (an AI ally team included); the defenders keep vanilla's tactics plus a detachment (below). B: both sides on vanilla tactics. C and D: the attackers take the point, the defenders hold it with their ranged troops behind it. Capturing D ends the mission through `MissionEnded`: `CreateDefenderPushedBack()` when the player attacks a campaign siege (the hall follows), `CreateSuccessful` when the player attacks a Custom Battle siege, `CreateDefeated` when the player defends. Vanilla's `BattleEndLogic` is earlier in the list, so a side wiped out still ends the battle its own way first.
- **Detachment** (`DetachmentRule`): at the first tick after deployment ends, a quarter of the active defender infantry (capped at 60) is moved into an empty formation, teleported to the recorded stage A frame and driven with move and charge orders from the main thread. Only formations the AI may split count (`Formation.IsSplittableByAI`), and never one holding the player: `TransferUnits` returns at once for a formation a player general owns. In a defence the player leads, no detachment forms and the player holds A with his own troops (the HUD says "Hold point A"). It is released when A falls.
- **Reinforcements** (`ReserveBudget`, `ReinforcementCadence`, `StagedSiegeSpawnSettingsPatch`): while armed, the spawn settings change from Wave to Balanced with a custom timer. Each side's reserve is read once when deployment ends and may be reserved by stage as a share of that reserve (table below). A reservation is not a batch: each approved check adds reserved troops, and the engine spawns a batch once the reserved count reaches the batch size or the remaining reserve is at most one batch. A stage change makes both sides due at once (a burst of 20 s at one reservation per second), then a trickle every 10 s. A side with nobody standing (no active agent that is not running away) and a reserve left is always approved, so no battle softlocks and none ends as a rout while its reserve is held back. The settings' global interval is the larger of the incoming one and the trickle, because vanilla's rout check reads it plus 3 s.
- **Spawn frames** (`StagedSiegeSpawnFramePatch`): a reinforcement spawns at the recorded frame for its side and stage. Attackers appear at the previous point facing the current one; defenders at the recorded hold frame. Stage A keeps vanilla's frame.
- **AI** (`TacticStagedObjective`): a `TacticComponent` with weight 1000 in the stages that use it and 0 otherwise. The vanilla tactics stay registered (weight 10), so stage B hands back to them. It drives `BehaviorDefend` toward a position, which the service adds to each formation on the main thread first. It reads only the snapshot and has a failure latch. A sergeant player sees the line "Our plan is to fight for the objective." when the tactic changes (the row `str_team_ai_tactic_text.TacticStagedObjective`; `DoctrinePopupStringsTests` fails for any TAOM tactic without a row).
- **HUD** (`StagedSiegeMissionView`, `StagedSiegeHudVM`, `StagedSiegeHud.xml`): a strip such as `A+ B+ [C] D E` (`E` only when the player attacks), the objective ("Take point C" or "Hold point C"), the capture percentage and status, and a letter over the current point. The layer takes no input and every widget ignores events.

Reserve shares, as fractions of each side's reserve when deployment ends (unspent reserve carries forward):

| Side | A | B | C | D |
|---|---|---|---|---|
| Attackers | 0.20 | 0.30 | 0.25 | 0.25 |
| Defenders, player attacks | 0.15 | 0.25 | 0.20 | 0.20 |
| Defenders, player defends | 0.15 | 0.30 | 0.25 | 0.30 |

With the player attacking, the defenders keep the rest (about 20 percent) for the hall fight.

### Component Diagram

```
staged_siege.json  (tunables + recorded scenes)
        |
 StagedSiegeSceneCatalog  (validates, never throws)
        |
 StagedSiegeService  (one per mission, main thread)
   |        |                 |
   |   StagedSiegeSlot   IStagedSiegeMissionAdapter
   |   (StageSnapshot,   (TaleWorlds calls)
   |    volatile)
   |      /      |        \
 Tactic     SpawnFrame     HUD view
 (AI thread) patch        (Client)
```

## Sharing the field with Creature Siege Role

[Creature Siege Role](creature-siege-role.md) scripts trolls per agent (strike the gate, stand off, hold the gate, hold the courtyard). Those scripts would override the positions `TacticStagedObjective` gives a formation, so the role yields. `IStagedSiegeFieldReader.OwnsField(mission, side)` (implemented by `StagedSiegeFieldReader` over the published snapshot, so any thread may call it) is true while a staged siege is armed for the mission, not finished, and `StagePlan.ModeFor(stage, side)` is not `Vanilla`: the attackers in A, C and D, the defenders in C and D. The role asks once per side per pass; an AI-controlled troll formation on an owned side is released with the reason `StagedSiege` (a formation the player commands is not: the player-order rules decide, because the tactic steers only AI-controlled formations), and scripted again when the plan gives the side back. In stage B, for the defenders in stage A, after the end, and with no staged siege armed, the role works exactly as before.

## The lord's hall (point E)

Vanilla opens the hall only for a player attacker, so this is attacker-only. The hall arms from the saved siege state, not from anything the walls fight remembers in memory: vanilla keeps the settlement `InTheLordsHall` in the save, through a retreat, a reload and a restart (`CampaignSiegeStateHandler`, reset only by `FinalizeSiegeEvent`). The mission's `EarlyStart` decides hall-or-walls first: in a hall mission (`LordsHallFightMissionController` present) `StagedHallService` runs and the walls rules do not; in any other mission the walls path runs as before. The hall arms (`StagedHallActivation`, guards in this order) in a hall mission, in a campaign, not on a client, with the settlement's siege state `InTheLordsHall`, the MCM switch on, the settlement's walls scene in the catalog, and at least one `FightAreaMarker` in the hall scene. The walls scene is the one the assault loads: the settlement's `center` location at `Town.GetWallLevel()` (`IStagedHallAdapter.WallsSceneName`, the call the player's assault makes in `PlayerSiege.StartSiegeMission`, `PlayerSiege.cs:171-172`). So point E arms in any hall at a recorded settlement with the switch on, including after a retreat, a reload or a restart, and also when vanilla's own rule ended the walls fight. The hall does not touch the in-memory walls end record. It logs `[StagedSiege] hall inert: <verdict>` or `[StagedSiege] hall armed: settlement <id>, area <index>, markers n`, and `[StagedSiege] hall result ready: state <BattleState> victory <bool> defeated <bool>` when the result is set.

- **Point E** (`HallZone`): every marker with the highest `AreaIndex` (vanilla scenes use areas 1 to 3, a union of markers of 1 to 4 m). An agent is inside when its 3D distance to any E marker is within that marker's radius, the reading of vanilla's `IsAgentInArea`. The centroid of the E markers places the flag (`flag_pole_big_sergeant_E`, defender colours, attacker colours once taken) and the HUD marker, and decides nothing.
- **Capture:** every reconcile pass the service counts humans in E by team side and runs the Phase 1 `CaptureRule` with the Phase 1 tunables. A side with nobody standing anywhere is left to vanilla's `BattleEndLogic`, which ends the hall first.
- **Taken:** the service stops both spawners (`LordsHallFightMissionController` is an `IMissionAgentSpawnLogic`), shows `The lord's hall is taken.` once, and `MissionEnded` returns `MissionResult.CreateSuccessful`, the result of a vanilla hall win (the first `MissionLogic` returning true wins, and `BattleEndLogic` is earlier in the list). The player also gets vanilla's win cue (`MBInformationManager.AddQuickInformation`, then the `str_battle_won_press_tab_to_view_results` line with the leave key set as `BattleEndLogic` does), because `BattleEndLogic` shows its own only for a depleted enemy. The result is fixed once set, but the mission does not close by itself and the surviving defenders keep fighting (spawners are stopped, so their number only falls). Leaving from the results screen is refused within 5 m of an alarmed enemy ("Can't retreat, there are enemies nearby!", `BattleEndLogic.TryExit` and `Mission.IsPlayerCloseToAnEnemy`): step clear of the defenders or finish them, then leave. The owner change is the vanilla chain, traced in the decompile: `CampaignMissionComponent.OnMissionResultReady` calls `SetPlayerVictorious`, which calls `MapEvent.SetOverrideWinner(Attacker)` for an attacker victory; `CheckIfOneSideHasLost` keeps it; `SiegeAssaultEventComponent.CheckIfBattleShouldContinueAfterBattleMission` returns false; `TryHandlePlayerEncounterEnd` opens `menu_settlement_taken`; `OnBeforeMapEventFinalize` raises `SiegeCompleted(isWin: true)`; and `KingdomManager.SiegeCompleted` runs `ChangeOwnerOfSettlementAction.ApplyBySiege`. TAOM's result equals the result `BattleEndLogic` gives a hall win, so the chain is the same one. The in-game check stays owed.
- **HUD:** the same layer shows the strip `A+ B+ C+ D+ [E]` (`E+` once taken), the objective `Take the lord's hall`, the four whole-line progress sentences and a marker over the centroid; after the capture the objective reads `The lord's hall is taken.` and the progress line hides. `StagedSiegeHudModel.FromHall` is the pure reading; the stage snapshot reading is unchanged.
- **Console:** `taom.staged_siege_status` reports the hall state in a hall mission.
- **No Harmony patch:** the hall rides `StagedSiegeMissionBehavior` (a `MissionLogic`), which TAOM's submodule adds to every mission.

## Custom battle sieges (Phase 3a)

A Custom Battle siege on a recorded scene runs the same stages A to D for either player side. It also lets scenes be recorded without travelling on the campaign map.

- **Start one:** Custom Battle, game type Siege, map `Ustokol Castle` (`sturgia_castle_siege_001`, vanilla lists it as a siege map; it is the pilot scene, so it is recorded). Pick either side. Vanilla's own list is merged into the picker, so TAOM adds nothing to offer it [Likely, not yet seen in game].
- **Arming:** `IStagedSiegeMissionAdapter.IsCustomBattleSiege` is true when `Campaign.Current` is null, the mission is a siege battle and it carries a `CustomSiegeMissionSpawnHandler` (`TaleWorlds.MountAndBlade.MissionSpawnHandlers`, `TaleWorlds.MountAndBlade.dll`). The service hands `StagedSiegeActivation` `IsCampaignWallsAssault || IsCustomBattleSiege`, so the verdict names are unchanged. The log line gains `, custom battle`: `armed: scene sturgia_castle_siege_001, player Attacker, custom battle`.
- **The end:** there is no lord's hall. `StageMachine` takes `hallFollows` (a campaign siege with the player attacking). Capturing D ends `DefenderPushedBack` when the hall follows, `AttackerWon` when the player attacks without one, and `PlayerDefeated` when the player defends (both kinds). `StagedResult.For` maps those to `CreateDefenderPushedBack()`, `CreateSuccessful(mission)` and `CreateDefeated(mission)`. Custom Battle never gets the pushed-back result, because its scoreboard shows that as a defeat.
- **HUD:** `StageSnapshot.HallFollows` drives it. The strip shows E only when the hall follows, and a finished `AttackerWon` reads `The last point has fallen` (the existing `taom_staged_siege_fallen` key, reused: it is true of a win and needs no new text).
- **Unchanged:** the spawn settings, reinforcement frames, detachment, staged tactic, the Creature Siege Role yield and the console recorder work as in a campaign siege. The hall service stays campaign-only.
- **Recording there:** run `taom.staged_siege_record` in a Custom Battle siege on a new scene; the recorder reads the same gates and markers.

## Side Effects You Should Know About

- **The Reinforcement Wave Count option does not apply in a staged siege.** The prefix sets the wave count to 0. The refill is capped at `Min(result, InitialSpawnedNumber - NumberOfActiveTroops)`, so each side is topped up to its starting count and never past it, and under Balanced vanilla's wave-count trim never runs, so the whole reserve stays available. In an unarmed siege the option works as before.
- **A JSON edit needs a game restart.** `StagedSiegeSceneCatalog` loads `staged_siege.json` lazily, once per process.
- **The MCM switch is read once per battle,** so a change applies from the next battle, not mid-siege.
- **Siege Forces coexists.** Its prefix on `InitWithSinglePhase` writes only the four spawn-total ints; this one writes only `spawnSettings`.

## Configuration

### Config File: `Main/_Module/ModuleData/siege/staged_siege.json`

Tunables sit at the top level, scenes under `scenes`, keyed by `Mission.SceneName` (case-insensitive). A bad tunable reverts to its default with one warning naming the key; a bad scene is skipped with a warning and never drops a good one. Integer keys must be JSON integers; NaN and Infinity are rejected before the range check.

| Key | Default | Range | Meaning |
|---|---|---|---|
| `captureRadius` | 8 | 2 to 30 | Metres around a point, unless a scene point names its own `radius` (the shipped pilot names none) |
| `heightBand` | 3 | 0.5 to 10 | Metres of height either side of the point that still count |
| `minAttackers` | 3 | 1 to 50 (int) | Fewest attackers that can start a capture |
| `dominanceRatio` | 3 | 1 to 10 | Outnumbering needed to rise or fall |
| `captureSeconds` | 20 | 2 to 300 | Time to capture at full dominance |
| `decaySeconds` | 30 | 2 to 600 | Time to lose progress |
| `reconcileSeconds` | 0.5 | 0.1 to 2 | Service stride in mission time |
| `detachmentShare` | 0.25 | 0 to 0.5 | Share of defender infantry sent to A |
| `detachmentCap` | 60 | 0 to 200 (int) | Most units in the detachment |
| `trickleSeconds` | 10 | 1 to 120 | Interval between reservations. Also the floor of the reinforcement interval vanilla's rout check reads (its grace is that interval plus 3 s, `BattleEndLogic.cs:299`, `:335`), so the default grace is 13 s instead of vanilla's 6 s |
| `refillBurstSeconds` | 20 | 0 to 120 | Burst length after a stage change |
| `refillBurstIntervalSeconds` | 1 | 0.25 to 10 | Interval between reservations inside the burst |
| `reinforcementBatchPercentage` | 0.05 | 0.01 to 0.5 | Balanced batch size |
| `desiredReinforcementPercentage` | 0.166 | 0.01 to 1 | Balanced refill target |
| `reserveShares` | see table above | each 0 to 1, sum at most 1 | `attacker`, `defenderPlayerAttacks`, `defenderPlayerDefends`, four numbers each |

A scene needs `points` A to D (`x`, `y`, `z`, optional `radius` 2 to 30), a `detachment` frame (`x`, `y`, `z`, `fx`, `fy`) and `defenders` frames for B, C and D. A facing must be a unit-ish vector (length 0.5 to 1.5) and is normalized at load. Coordinates past 100000 in absolute value are rejected.

### Current Values

The shipped file carries the defaults and one scene, `sturgia_castle_siege_001` (the pilot, used by 17 TAOM castles), with eight records: points A to D, the A detachment frame and defender frames B to D. Positions are in the file; `ShippedStagedSiegeConfigTests` pins them.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/StagedSiege/Domain/` | Pure rules: `CaptureRule`, `StageMachine`, `StagePlan`, `ReserveBudget`, `ReinforcementCadence`, `DetachmentRule`, `StageSnapshot`, `StagedSiegeActivation`, `ScenePlan`, `StagedResult` (the end to result mapping) |
| `Main/Features/StagedSiege/StagedSiegeService.cs` | Per-mission rules runner, fault policy, S2 to S5 log lines |
| `Main/Features/StagedSiege/StagedSiegeSlot.cs` | The armed service and the published snapshot; the armed hall service and its snapshot; the last walls end, for the inert log |
| `Main/Features/StagedSiege/StagedHallService.cs`, `Domain/HallZone.cs`, `Domain/StagedHallActivation.cs`, `Domain/HallSnapshot.cs` | Point E in the lord's hall: the service, the zone, the arming decision and the HUD input |
| `Main/Features/StagedSiege/IStagedSiegeFieldReader.cs`, `StagedSiegeFieldReader.cs` | The seam Creature Siege Role asks whether the stage plan steers a side |
| `Main/Features/StagedSiege/StagedSiegeSceneCatalog.cs` | Loads and validates `staged_siege.json` |
| `Main/Features/StagedSiege/StagedSiegeRecorder.cs`, `StagedSiegeRecordFragment.cs` | The in-game recorder and its export |
| `Main/Features/StagedSiege/Hooks/StagedSiegeMissionBehavior.cs`, `StagedSiegeMissionEdge.cs` | Mission boundary; the edge holds the end mapping, spawner stops, agent filtering and the console drain |
| `Main/Features/StagedSiege/Hooks/StagedSiegeCommandQueue.cs` | The console's thread-safe queue, drained on the main thread at the start of the mission tick |
| `Main/Features/StagedSiege/Hooks/StagedSiegeSpawnSettingsPatch.cs`, `StagedSiegeSpawnFramePatch.cs`, `StagedReinforcementTimer.cs` | `Patch126_StagedSiege` and the custom timer |
| `Main/Features/StagedSiege/Hooks/TacticStagedObjective.cs` | The stage tactic |
| `Main/Features/StagedSiege/UI/StagedSiegeHudModel.cs`, `StagedSiegeHudVM.cs` | HUD reading (pure) and view model |
| `Main/_Module/GUI/PreFabs/StagedSiege/StagedSiegeHud.xml` | The HUD movie |
| `Client/Features/StagedSiege/Hooks/StagedSiegeMissionView.cs` | `[DefaultView]` that owns the layer (TAOM.Client) |
| `Main/Adapters/IStagedSiegeMissionAdapter.cs`, `StagedSiegeMissionAdapter.cs` | Every TaleWorlds call the service makes |
| `Main/Adapters/IStagedHallAdapter.cs`, `StagedHallMissionAdapter.cs` | Every TaleWorlds call the hall service makes: the markers, the agents in E, the flag, the spawners, the message |
| `Main/Features/DevConsole/Cheats/StagedSiegeCheats.cs` | Recorder and test commands |
| `Main/_Module/ModuleData/siege/staged_siege.json` | Tunables and recorded scenes |

## Dependencies

- `IStagedSiegeMissionAdapter` (Adapters): the mission, spawn logic, gates, formations and flags.
- `StagedSiegeSettingsProvider`: reads `TaomSettings.EnableStagedSiege` (ships off; turning the default on later needs a renamed property, since MCM keeps a saved value over a changed default).
- Co-op ([coop-interop.md](coop-interop.md)): `StagedSiegeMissionBehavior` is Unsafe and is not added; `StagedSiegeMissionView` is Local.
- Harmony: [Patch126_StagedSiege](../reference/harmony-patch-registry.md).

## Recording a scene

Do this in a siege mission on the scene (dev console, mission only). Each command appends to `Documents\Mount and Blade II Bannerlord\TAOM\staged_siege_records.jsonl`, never to the install.

1. Stand on the middle of point A and run `taom.staged_siege_record A point`. Add a radius (2 to 30) as a third argument only for a point that needs its own; a point recorded without one follows the `captureRadius` tunable. Point records from the Phase 0 spike recorder carry `radius: 8` on every point: record those points again (the newest record wins) or delete their lines before an export. A look too close to straight up or down is refused for a defender or detachment frame, so face level when recording those.
2. Stand where the A detachment should hold, facing the way it should face, and run `taom.staged_siege_record A detachment`.
3. Repeat `point` for B, C and D, and run `taom.staged_siege_record B defender` (also C and D) at each defender hold position, facing the way the hold should look.
4. `taom.staged_siege_show` spawns a flag at each recorded point so you can check heights and spacing. `taom.staged_siege_open_gates` opens the gates for walking the courtyard. Both only enqueue (see below) and also work in a scene with no armed siege.
5. `taom.staged_siege_export` folds the newest record per piece into `staged_siege_<scene>.json` next to the JSONL and lists any piece still unrecorded.
6. Merge that entry into `Main/_Module/ModuleData/siege/staged_siege.json` by hand and restart the game.

Other commands: `taom.staged_siege_status` says why the feature is inert or, when armed, the stage, progress, detachment, spawn counts and tactic state. `taom.staged_siege_stage <A-D|end>` forces an armed siege forward (never backward; `end` captures D). `staged_siege_stage`, `staged_siege_open_gates` and `staged_siege_show` write engine AI, gate or mission-object state, which must not happen while the async AI tick may be reading it, so each replies "Queued" and the mission behavior applies the request on the main thread at the start of its next tick and logs the result (`[StagedSiege] console: ...`). Record, status and export read or write files directly. See [dev-console.md](dev-console.md).

## Tests

- `TAOM.Tests/Features/StagedSiege/`: pure rules (`CaptureRuleTests`, `StageMachineTests`, `StagePlanTests`, `ReserveBudgetTests`, `ReinforcementCadenceTests`, `DetachmentRuleTests`, `ScenePlanTests`, `StageSnapshotTests`, `StagedSiegeActivationTests`), the catalog and the shipped file (`StagedSiegeSceneCatalogTests`, `ShippedStagedSiegeConfigTests`), the service against a substitute adapter (`StagedSiegeServiceTests`), the slot, recorder and fragment tests, the console queue (`StagedSiegeCommandQueueTests`), the settings provider, the prefix body (`StagedSiegeSpawnSettingsPatchTests`), wiring (`StagedSiegeWiringTests`), the HUD reading and wording (`StagedSiegeHudModelTests`, `StagedSiegeHudStringsTests`), the HUD movie against its view model (`StagedSiegeHudPrefabTests`) and the engine bindings (`StagedSiegeBindingTests`, `[RequiresGame]`). The hall: `HallZoneTests`, `StagedHallActivationTests`, `StagedHallServiceTests` (a substitute adapter), the slot's hall tests, `StagedSiegeHudModelTests.FromHall_*`; the shared field: `StagedSiegeFieldReaderTests`, `CreatureSiegeRulesTests.StagedSiege`, `CreatureSiegeRoleServiceStagedSiegeTests`.
- `TAOM.Tests/Features/CoopInterop/Missions/CoopMissionRolePolicyTests.cs` pins both co-op rows, including the view's type name in TAOM.Client.
- Every prefab attribute and brush the HUD uses is covered by the GUI prefab gates (`EveryPrefabAttribute_NamesASettableWidgetProperty`, `EveryGuiPrefabBrush_IsDeclaredByAModule`).

## How to verify in game

**Preconditions:** `./build.ps1` deploy, the MCM switch `Battle Tactics/Staged Siege` on, a campaign save next to a castle that uses `sturgia_castle_siege_001`. The game log is the main evidence: every line is tagged `[StagedSiege]`.

1. Start an assault as the attacker. The log shows `armed: scene sturgia_castle_siege_001, player Attacker`, and a flag stands on A.
2. Take A, then B, then C, then D. Check `stage X captured` and the HUD after each, that the gates open at B (`S4 gates opened`), and that reinforcements arrive at the recorded frames.
3. After D, check the hall picker and the hall fight, and that casualties carry over.
4. Lose as attacker at B and at D.
5. Defend the same castle. Hold all four points to win; lose D to confirm the settlement falls.
6. Load a save made mid-campaign and start a siege.
7. Run once with the Siege Forces picker on.
8. Attack a castle with no recorded scene. The log shows `inert: UnrecordedScene` and the siege stays vanilla.
9. Check the HUD at every stage for both sides.
10. Attack with trolls in the army (Mordor): in stages A, C and D their `[CreatureSiege]` role lines stop (reason `StagedSiege`) and they follow their formation; in stage B the gate-striking returns.
11. After a captured D, enter the lord's hall: the log shows `hall armed: settlement ..., area n, markers m`, a flag stands in the last area, and the HUD reads `Take the lord's hall`.
12. Hold at least three attackers (and three times the defenders' number) in the last area for 20 s: `hall E captured`, the message, the flag recolours, the mission ends as a win and the settlement changes owner.
13. Enter a hall at a settlement with no recorded scene: `hall inert: WallsSceneNotRecorded` and the vanilla hall fight is unchanged.
14. Push the defenders back, retreat from the hall, re-attack (or reload the save, or restart the game and load it): the hall still arms. Also check the "The lord's hall is taken" cue and the Tab hint at the capture, and try to leave once beside a defender (refused) and once 5 m clear of every defender (allowed).

First-run log lines that settle the open questions: `S2 deployment over: settings live True` (the IL already shows the ref write on the `in` parameter reaches `Init`; this confirms it at runtime), `S2 reinforcement ... m from the staged frame` (small distances mean the zero-navmesh spawn frame resolved), `S3 detachment units` (falling while `ai False` holds means vanilla merged it back), `S4 tactics` plus the zone counts (the AI is pathing inward after B), `stage X zone` (defenders counted with nobody in the courtyard means wall troops are leaking in).

An unreachable step is reported with the command tried and the unmet precondition; it is not marked verified through another path.

## Checklist: custom battle (owed in game)

1. Start Custom Battle, Siege, Ustokol Castle, as the attacker, and note the wall level you picked (the catalog keys only on the scene name and Custom Battle lets the player choose the level, `BannerlordMissions.cs:162-167`). Note the wall level the points were recorded at, and repeat the run at one other level to see whether the points still fit. The log shows `armed: scene sturgia_castle_siege_001, player Attacker, custom battle`, a flag on A, and a strip `[A] B C D` with no E.
2. Take A to D (or `taom.staged_siege_stage end`). The screen shows vanilla's "battle won" line and the "press Tab to view results" hint (`StagedSiegeService.AnnounceEnd`, since vanilla shows them only for a depleted enemy), the scoreboard reads Victory, and the log shows `S5 MissionEnded returned AttackerWon`.
3. Repeat as the defender. Lose D: the scoreboard reads Defeat (`PlayerDefeated`).
4. Run `taom.staged_siege_record` on a scene that is not recorded yet.
5. Start the same battle with the MCM switch off: `inert: Disabled`.

## Findings from the first in-game run (2026-10-29)

Sirannon, scene `sturgia_castle_siege_001`, player attacking.

- The spawned flag works. `CreateMissionObjectFromPrefab` returns a `FlagCapturePoint`, it lowers from 1.0 to 0.0 in exactly 10 s and recolours.
- `CastleGate.OpenDoor` opens both gates.
- The recorder produced the eight shipped records.
- The forced-finish end chain (S5) was not tested. Vanilla won first: the defenders died rather than routed, so vanilla's pull-back rule (at least 20 routed defenders) never applied. 240 attackers killed the 257-strong garrison in about two minutes once the gates opened.

## Open items

- **S5 is owed.** Nothing has yet run the path from a captured D to the hall picker (player attacking) or to a defeat (player defending). `taom.staged_siege_stage end` is the way to reach it without playing four stages.
- **S2 to S4 are checked by the log lines above** on the first full run. The risks they expose: a Balanced batch that never fills, a zero-navmesh spawn frame landing off the mesh, `TacticDefendCastle` merging the detachment back, teleported units stuck off the navmesh, the AI not pathing inward after B.
- **Creature Siege Role yields to the plan (Phase 2, built).** See "Sharing the field" above. Owed: an in-game look at a troll army in stages A to D.
- **Reserve left over at S5 (to check).** A player-attacked siege ends with about 20 percent of the defender reserve unspent, a state vanilla never creates. Check the hall picker, the hall troop counts and the casualties.
- **Reinforcements may join the detachment (to watch).** Defender reinforcements can be assigned into the detachment formation (`MissionReinforcementsHelper` prefers small formations near the spawn). Watch the `S3 detachment units` line rising above its start.
- **The defender-side hall needs Mike's decision, and is high risk.** A defending player never reaches the hall in vanilla (the pull-back and every consumer are attacker-only), so a defender who loses D is defeated. Giving the defence a hall would need a TAOM-owned hall mission: see the Phase 2 and 3 research.
- **Phase 2:** record the ten most-used scenes and sergeant-mode checks are still open; the E capture zone in the hall is built (see above). **Owed in game for point E:** the hall arms after a staged D capture (`hall armed` log line), the E flag and HUD look right, the capture ends the hall as a win, the settlement changes owner (the vanilla chain, traced; still to see in game), and the hall's defenders count as the losing side's survivors [Likely]. **Phase 3:** a fallback derived from the scene's gates and the defender-side hall (custom battle sieges are built, see above).
- Not covered so far: the laptop, a Custom Battle run (built in Phase 3a, never played), any run as the defender.

## Changelog

- 2026-10-29: Phase 3a built (#1054): custom battle sieges arm on a recorded scene for either side; capturing D wins for an attacker and loses for a defender, with no lord's hall. Not yet seen in game.
- 2026-10-29: Phase 2 deep-review fixes: point E arms from the saved siege state (a retreat, a reload and a restart no longer lose it), a player-commanded troll formation is not released for the staged plan, the win cue and a hall result log line, the MCM hint names point E. The Harmony category was renumbered from `Patch125` to `Patch126` because trunk took 125 for `Patch125_HideoutClanGuard`.
- 2026-10-29: Phase 2 built (#1054): Creature Siege Role yields to the stage plan, and point E captures the lord's hall for a player attacker. Not yet seen in game.
- 2026-10-29: Phase 1 deep-review fixes: a player general keeps his siege engines, the rout grace follows the staged trickle and a fleeing side counts as softlocked, a player-led defence forms no detachment, the sergeant popup has a row, console commands queue to the main thread, recorded points follow `captureRadius`, the HUD progress line is one sentence per status.
- 2026-10-29: Phase 1 built (#1054): domain rules, service, spawn patches, stage tactic, HUD, recorder, co-op rows, pilot scene `sturgia_castle_siege_001`. First attacker run on Sirannon confirmed the flag, the gates and the recorder.

## GitHub Issue

- **Issue:** #1054, Staged Siege: capture points A to E in a siege assault
- **Status:** Open
