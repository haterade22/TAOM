# Fords of War

## Overview

Fords of War is a campaign-free, single-player mission mode started from the main menu. Two sides,
West (the player) and East (an AI), face each other across 3 lanes. The player has no agent: they are a
commander with a free top-down camera and a command panel, spend gold on packs of troops and on heroes,
give each lane a Push, Hold or Fall back order, and win by destroying the enemy keep. Each side owns a
chain of towers per lane that shoots back, and the target length of a match is 30 to 45 minutes.

The full build contract, with every signature and the stage-by-stage Deviations, is
[fords-of-war/build-spec.md](fords-of-war/build-spec.md). This page is the maintainer's map: what exists,
what the numbers mean, how to check it in game and where to change it.

## Why This Exists

- **Vanilla behavior:** Bannerlord battles are one field and one player agent. The commander screens
  (order bar, formations) all assume a main agent; the vanilla order bar refuses to open without one.
  Nothing in vanilla gives a lane-based, tower-and-economy match, and nothing runs a mission with no
  campaign.
- **TAOM requirement:** a Lord of the Rings siege-style mode with heroes (Sauron, the Witch-King,
  Saruman, Imrahil) and troll siege units, played as a commander. It had to be safe to extend to
  multiplayer later, so every match rule lives behind one command API.
- **Without this feature:** there is no commander-style mode at all, and the multiplayer groundwork
  (rules behind one command API) has nothing to prove itself on.

## Architecture

### Design Challenge

- **No main agent.** A mission has a main agent only when an agent gets `AgentControllerType.Player`.
  The mode never spawns the player troop, so `Agent.Main` stays null. Everything that reads it (the
  vanilla order bar, `OrderController.GetOrderFormCustomWidth`, the boundary crossing handler) is either
  left out of the mission or never used.
- **No formation AI.** Under `MissionTeamAITypeEnum.NoTeamAI` no formation AI ticks on either side
  (`Formation.cs:2458`), so nothing moves unless the director orders it. The director issues every
  movement order for both sides and never hands a lane formation to AI control.
- **Towers are scripted entities, not siege engines.** `DestructableComponent.OnHit` does not filter by
  side in single player, and the barricade prefab ships `BattleSide None`. Lane locking (a tower takes
  damage only when every tower in front of it has fallen), the ranged, hero and siege factors and
  own-side immunity all have to be decided in a damage table.
- **Threads.** The tower hit callback and agent removals can arrive off the main thread, so the match
  is guarded by one gate that is never held across an adapter call, and a tower's fall is always parked
  for the next tick.
- **No battle-end logic.** `BattleEndLogic` is left out on purpose, so nothing in the engine ends or
  closes the mission. The mission ends through `MissionLogic.MissionEnded` (polled every 0.1 s from
  10 s mission time), and the way back to the main menu is the mode's own timers.

### Solution Approach

- **Pure domain.** `Main/Features/FordsOfWar/Domain/` holds every rule (gold, bounties, waves, towers,
  heroes, lane orders and marching, capture points, camps, the AI commander) with no TaleWorlds
  reference. `FordsMatch` is the aggregate: `Tick(MatchSnapshot)` returns `MatchEffects` (spawns,
  formation commands, volleys, heals, events, winner), and the commands go through
  `TryBuyPack`, `TryBuyHero`, `TryBuyback` and `TrySetLaneOrder`. The AI calls the same methods as the
  player. `FordsDomainPurityTests` fails when any domain file contains `using TaleWorlds` or
  `TAOM.Adapters`.
- **One UI command surface.** `FordsSession` is the only thing the client touches: the human slot's
  `MatchView`, the four commands, `AddGold` (console), a cached name lookup, the event feed and the
  camera numbers. The director fills it at mission start and clears it at mission end; every member
  takes the director's gate.
- **Director and three adapters.** `FordsMissionDirector` builds the layout, towers and match, then each
  tick samples the field, runs `match.Tick` and applies the effects: spawns within the per-tick budget,
  a lane order only when it differs from the last one applied, tower volleys, regeneration, the winner.
  It reaches the engine through three mission-scoped adapters (they hold the `Mission`, so they are not
  IoC singletons; the mission logic builds them): `FordsBattlefieldAdapter` (spawn, orders, samples),
  `FordsTowerAdapter` (tower entities, gunners, volleys) and `FordsSceneAdapter` (bases, ground and
  navmesh snapping, tagged layouts).
- **Patch131.** `Patch131_FordsOfWar` (`FordsOfWarModule.PatchCategory`, `ApplyPhase.FirstMission`) is
  one prefix, `FordsTowerHitPatch`, on `DestructableComponent.OnHit`. It hands the blow to
  `IFordsTowerHitHook` (the director) and writes back the damage the match allows. A component that is
  not a registered Fords tower passes through untouched, and the prefix never throws. A blow with no
  weapon (a kick, a mount's charge) is left alone. Own-side hits give 0 damage.
- **Launcher and mission logic.** `FordsMissionLauncher` opens the mission with eight behaviors, in
  order: `MissionCombatantsLogic` (`NoTeamAI`, two `CustomBattleCombatant`s, no player agent),
  `AgentHumanAILogic`, `AmmoSupplyLogic` (both sides), `MissionHardBorderPlacer`,
  `MissionBoundaryPlacer`, `MissionOptionsComponent`, `FordsOfWarMissionLogic` and `FordsTelemetryLogic` (last: it feeds the self-check report). Left out on purpose:
  the default spawn logic, `BattleEndLogic`, deployment controllers, the morale and panic logics,
  `AgentVictoryLogic`, `BattleMissionStarterLogic` and `MissionBoundaryCrossingHandler`.
  `FordsOfWarMissionLogic` is a `MissionLogic` under 150 lines; its tick also drives `FordsMissionExit`,
  the automatic return to the main menu, counted in mission time (a paused engine adds nothing).
- **Commander camera.** `FordsCommanderCameraView` (a `MissionView`) overrides `UpdateOverridenCamera`
  and returns true while `FordsSession.IsActive`, so it owns the frame: WASD or arrows pan, Q and E
  rotate, the wheel zooms between 15 m and 120 m, the right mouse button rotates and tilts, Shift doubles
  the pan speed. It keeps the camera inside the boundary polygon and suppresses spectator cycling every
  frame. `IsPhotoModeAllowed()` returns false.
- **ADR-013 split.** The client game manager (`FordsOfWarGameManager : CustomGameManager`), the views,
  the panel and its view models live in `TAOM.Client`. Core declares `IFordsOfWarGameManagerFactory` and
  registers `NullFordsOfWarGameManagerFactory` as the default (it logs and returns null); `TAOM.Client`
  registers the real factory in `TaomClientComposition` and replaces it. Without the client the option
  stays harmless: the prompt shows a notice.
- **Multiplayer: out of scope.** A networked mode needs its own module, data and server assembly, which
  is a separate project (build-spec phase 4). What this build keeps ready: gold is per commander slot
  (`GoldLedger`, `CommanderSlot`), the lane owner is a slot id, the domain has no engine reference, and
  every player action is one `Try*` call that a server could own.

### Component Diagram

```
match.json / factions.json / maps.json        Main menu "Fords of War"      taom.fords_start
                |                                     |                          |
        FordsConfigCatalog               FordsSetupPrompt (3-4 prompts) <--------+
        (readers, never throw)                        |
                |                         IFordsOfWarGameManagerFactory
          FordsMatchPlan                  (Null in core, real in TAOM.Client)
                |                                     |
                |                         FordsOfWarGameManager (client)
                |                                     |
                |                         FordsMissionLauncher.Launch -> MissionState.OpenNew
                |                                     |
                |                         FordsOfWarMissionLogic (MissionEnded, FordsMissionExit)
                |                                     |
                +----------------------> FordsMissionDirector <---- FordsTowerHitPatch (Patch131)
                                          |        |        \
                                          |        |         FordsSession <---- Client panel + camera
                                          |        |         (views, commands)   (TAOM.Client)
                      FordsMatch (pure domain, Try* API, Tick -> MatchEffects)
                                          |
              FordsBattlefieldAdapter   FordsTowerAdapter   FordsSceneAdapter
                    (Mission)             (Mission)            (Mission)
```

## Configuration

Three files in `Main/_Module/ModuleData/fords_of_war/`. `FordsConfigCatalog` reads each once per
process and never throws: a bad number reverts to its default with one warning naming the key, a bad
unit, hero, free-wave tier or scene is skipped with one warning, and a missing file gives the built-in
default (`FordsMatchConfig.Default`), no factions or no maps. The main-menu option hides itself while
factions or maps are empty.

### Config File: `match.json`

Top level: `countdownSeconds` (number, shipped 10: seconds before the match goes live), then one object
per record. Names are camelCase.

| Block and field | Type | Description | Shipped |
|---|---|---|---|
| `economy.startingGold` | int | Gold each slot starts with | 600 |
| `economy.incomePerMinute` | int | Passive income per minute | 120 |
| `economy.killBountyFraction` | float | Share of a killed unit's value paid to the killer's owner | 0.35 |
| `economy.minKillBounty` | int | Floor of a kill bounty | 2 |
| `economy.towerBounty` | int | Paid when a tower falls, split across the destroying side's slots | 300 |
| `economy.heroKillBounty` | int | Paid for killing a hero, instead of the per-value bounty | 250 |
| `economy.captureIncomePerMinute` | int | Income per owned capture point per minute | 60 |
| `economy.maxGold` | int | Purse cap | 99999 |
| `waves.freeWaveIntervalSeconds` | float | Free wave per lane per side (zero or less turns them off) | 40 |
| `waves.maxLivePerLanePerSide` | int | Live-unit cap per lane per side | 90 |
| `waves.maxLivePerSide` | int | Live-unit cap per side | 300 |
| `waves.maxSpawnsPerTick` | int | Units spawned per tick across all lanes and sides | 6 |
| `waves.maxQueuedPacksPerLane` | int | Bought packs waiting per lane | 3 |
| `towers.towerHitPoints` | int | Outer tower hit points | 8000 |
| `towers.innerTowerHitPoints` | int | Inner tower hit points | 11000 |
| `towers.keepHitPoints` | int | Keep hit points | 18000 |
| `towers.rangedDamageFactor` | float | Multiplier on ranged hits | 0.15 |
| `towers.heroDamageFactor` | float | Multiplier on hero hits | 4 |
| `towers.siegeDamageFactor` | float | Multiplier on hits from the Siege role | 3 |
| `towers.regenPerSecond` | float | Hit points regained per second | 15 |
| `towers.regenDelaySeconds` | float | Seconds without damage before regeneration starts | 20 |
| `towers.gunnersPerTower` | int | Invulnerable archers garrisoning each tower | 2 |
| `towers.volleyIntervalSeconds` | float | Seconds between scripted volleys | 4 |
| `towers.volleyMissiles` | int | Javelins per volley | 3 |
| `towers.fireRange` | float | Metres: the nearest enemy inside it is a target (heroes first) | 45 |
| `towers.attackRange` | float | Metres, formation center to tower, at which a lane attacks it | 20 |
| `heroes.maxAlivePerSide` | int | Heroes alive at once per side | 2 |
| `heroes.respawnBaseSeconds` | float | Respawn delay at minute 0 | 60 |
| `heroes.respawnPerMinuteSeconds` | float | Added to the delay per match minute | 6 |
| `heroes.buybackBaseFraction` | float | Buyback price as a fraction of the hero price at minute 0 | 0.5 |
| `heroes.buybackPerMinuteFraction` | float | Added to that fraction per match minute | 0.05 |
| `lanes.orderCooldownSeconds` | float | Cooldown between lane order changes | 20 |
| `lanes.engageRange` | float | Metres: enemies inside it make the lane charge | 25 |
| `lanes.waypointReachRadius` | float | Metres at which a waypoint counts as reached | 6 |
| `lanes.holdDistanceBeforeTower` | float | Metres before the next enemy tower where a Hold order stands | 60 |
| `lanes.assaultThreshold` | int | Live units the AI gathers in its assault lane before it pushes | 40 |
| `capture.radius` | float | Metres around a capture point that count units | 12 |
| `capture.secondsToCapture` | float | Seconds for one side alone to take a point | 20 |
| `aiProfiles.<id>.decisionIntervalSeconds` | float | Seconds between AI decisions | easy 12, normal 8, hard 5 |
| `aiProfiles.<id>.incomeMultiplier` | float | Applied to the AI slot's passive income only, not to bounties | easy 0.8, normal 1.0, hard 1.2 |
| `aiProfiles.<id>.mistakeChance` | float | Chance the AI skips a decision | easy 0.25, normal 0.1, hard 0.0 |
| `aiProfiles.<id>.heroReserveGold` | int | Gold the AI keeps beyond a hero's price before it buys one | easy 200, normal 300, hard 400 |

A file may override one profile or add a new name; a new profile starts from normal.

### Config File: `maps.json`

| Field | Type | Description |
|---|---|---|
| `defaults.options` | object | Layout options: `laneCount` (3 or 4), `laneSpread` (metres between neighbour lanes at full bow), `waypointStep` (metres between waypoints), `towerFractions` (tower positions as fractions of lane length from the owner's base; each 0.01 to 0.49), `keepInset` (keep distance from each base) |
| `defaults.towerPrefab`, `defaults.keepPrefab` | object | `prefab` (entity prefab name) and `scale` |
| `defaults.camp` | object | `troopId`, `count`, `reward` for neutral camps |
| `scenes[].scene` | string | Scene id |
| `scenes[].name` | string | Display name, a `{=KEY}default` string |
| `scenes[].layout` | string | `generated` (from the scene boundary) or `tags` (read the markers of a hand-built scene); an unknown value skips the scene |
| `scenes[].options`, `towerPrefab`, `keepPrefab`, `camp` | object | Optional per-scene overrides of the defaults |

### Config File: `factions.json`

`factions` is an object keyed by faction id.

| Field | Type | Description |
|---|---|---|
| `name` | string | `{=TAOM_FOW_Faction_<Name>}English` |
| `culture` | string | Culture id used for the banner and the combatant; a faction without one is skipped |
| `units[].id` | string | Offer id |
| `units[].troop` | string | Troop id; must exist in the module data (a shipped-config test checks it) |
| `units[].role` | string | `Infantry`, `Spear`, `Ranged`, `Cavalry`, `HorseArcher`, `Siege` or `Hero`; Cavalry and HorseArcher price as mounted |
| `units[].packSize` | int | Units per purchase |
| `units[].level`, `eliteWeight` | int, float | Feed `TroopPricing`; the shipped offers state no `price` |
| `units[].price` | int | Optional explicit pack price that overrides `level` |
| `heroes[].id`, `price` | string, int | NPCCharacter id and gold price |
| `freeWaves[].fromMinute`, `units[]` | float, list | Tier that applies from that match minute; each unit is `troop` and `count` |

### Current Values

**Match numbers and the decisions they encode** (from the build spec, section 5 and the Deviations):

| Value | Why |
|---|---|
| Towers 8000, inner 11000, keep 18000 hit points | Starting values for the balance pass; the inner tower and the keep are tougher than the outer tower |
| Ranged factor 0.15 | Ranged hits are scaled down so archers do not strip a tower (spec section 3.4: ranged x `RangedDamageFactor`) |
| Hero factor 4, siege factor 3 | Heroes and the Siege role (the trolls) are the units that hurt towers; the factors multiply, so a hero archer gets both |
| Tower regen 15/s after 20 s without damage | Regeneration starts only after a quiet period and never above the maximum |
| 2 gunners per tower, volley of 3 javelins every 4 s, fire range 45 m | The garrison: scripted volleys at the nearest enemy in range, heroes first |
| Hold distance 60 m, fire range 45 m | A lane gathering at its hold line must stand out of tower fire: 45 m plus 15 m of formation depth. It was 35 m, inside the tower's reach. `ShippedMatch_LaneHoldLineIsOutsideTowerFireRange` pins the relation |
| Attack range 20 m | Measured from the formation center to the tower; 6 m was too small for a center |
| 2 heroes alive per side, respawn 60 s + 6 s per minute | Heroes decide fights, so the count is capped and a death costs time that grows with the match; buyback is 0.5 + 0.05 per minute of the price |
| Starting gold 600, income 120 per minute, bounty fraction 0.35 | Starting values; kills pay the owner of the killing unit, towers and heroes pay a flat larger bounty |
| Free wave every 40 s, caps 90 per lane, 300 per side, 6 spawns per tick | The caps are meant to keep a 30-minute match under the agent cap (not yet measured; see the checks below) |
| Order cooldown 20 s | Setting the standing order again returns Ok and costs no cooldown |
| Capture 12 m radius, 20 s, 60 gold per minute per point | Starting values |
| Countdown 10 s | Seconds before the match goes live |
| AI: easy 12 s, x0.8, 0.25, 200; normal 8 s, x1.0, 0.1, 300; hard 5 s, x1.2, 0.0, 400 | Difficulty comes from decision speed, passive income, skipped decisions and hero reserve; the multiplier never touches bounties |

**Layout options** (shipped default): `laneCount` 3, `laneSpread` 45, `waypointStep` 12, `towerFractions`
0.36 and 0.2, `keepInset` 0.04.

**Maps** (all `"layout": "generated"`, three lanes): `taom_rohan_battle_001_forceatmo` (the default),
`taom_gondor_pelennor_fields_forceatmo` and `taom_mordor_battle_001_forceatmo`. Nobody has checked their
boundary size or navmesh in game. Tower and keep prefab `siege_barricade_a`, scale 1.0 and 1.6 (the 1.6
is a placeholder, not measured). Neutral camp default: 2 `hill_troll` for a reward of 250.

**Factions** (offers / heroes with price):

| Faction | Culture | Offers | Heroes |
|---|---|---|---|
| `gondor` | `gondor` | 9 | Imrahil `lord_1_9` 1000, Boromir `lord_1_75` 900, Faramir `lord_1_34` 800 |
| `rohan` | `vlandia` | 9 | Theoden `lord_4_1` 1000, Eomer `lord_4_3_1` 900, Erkenbrand `lord_4_16` 800 |
| `mordor` | `mordor` | 10, with `hill_troll` as the Siege unit | Sauron `lord_1_17` 2400, the Witch-King `lord_1_15` 1600, Nazgul `lord_1_48_1` 1000 |
| `isengard` | `isengard` | 10, with `hill_troll` as the Siege unit | Saruman `lord_I1_0` 2000, Lurtz `lord_I4_1` 1000, Ugluk `lord_I1_1` 900 |

Gondor and Rohan have no siege unit. Each faction has free-wave tiers at minutes 0, 10 and 20. Hero
prices and pack sizes are first guesses for the balance pass. `cave_troll` is not used: its weight row in
`troop_weights.xml` is commented out. Decks are ordered so that the two dearest offers fall within F1 to
F8.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FordsOfWar/Domain/FordsMatch.cs` (+ `.View.cs`, `.SpawnFailures.cs`) | The aggregate: `Tick`, the `Try*` command API, kill credit, spawn-failure refunds, `GetView` |
| `Main/Features/FordsOfWar/Domain/` (rule classes) | `GoldLedger`, `BountyRules`, `WaveScheduler`, `TowerChain`, `TowerDamageRules`, `TowerRegen`, `TowerFireRules`, `HeroRoster`, `LaneOrderBook`, `LaneMarchRules`, `CapturePointRules`, `CampRules`, `MatchClock`, `AiCommander`, `LaneLayoutGenerator`, `LaneGeometry`, `TroopPricing` |
| `Main/Features/FordsOfWar/FordsOfWarModule.cs` | Feature module: registrations, `Patch131_FordsOfWar`, main-menu option on the MainMenu phase |
| `Main/Features/FordsOfWar/FordsMissionDirector.cs` | Per-mission orchestrator; applies effects through the adapters |
| `Main/Features/FordsOfWar/FordsSession.cs` | The one command and view surface for the client |
| `Main/Features/FordsOfWar/Hooks/FordsMissionLauncher.cs` | Opens the mission with the eight behaviors |
| `Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs` | `MissionLogic`: start, tick, removals, `MissionEnded` |
| `Main/Features/FordsOfWar/Hooks/FordsTowerHitPatch.cs` | Patch131, prefix on `DestructableComponent.OnHit` |
| `Main/Features/FordsOfWar/FordsMissionExit.cs`, `FordsExitClock.cs` | Automatic return to the main menu: 25 s after the result, 3 s after a failed start |
| `Main/Features/FordsOfWar/FordsMenuEntry.cs`, `FordsSetupPrompt.cs`, `FordsSetupChoices.cs` | Main-menu option `TaomFordsOfWar` and the start prompts |
| `Main/Features/FordsOfWar/FordsOfWarCommands.cs`, `Cheats/FordsOfWarCheats.cs` | Console commands `taom.fords_start` and `taom.fords_gold` |
| `Main/Features/FordsOfWar/Config/` | `FordsConfigCatalog` and the three readers |
| `Main/Features/FordsOfWar/FordsMatchPlan.cs`, `FordsSnapshotBuilder.cs`, `FordsLayoutFitter.cs`, `FordsTagLayoutReader.cs`, `FordsBaseFinder.cs`, `FordsTowerGeometry.cs`, `FordsSpawnPlacement.cs` | Pure helpers around the director |
| `Main/Features/FordsOfWar/IFordsOfWarGameManagerFactory.cs`, `NullFordsOfWarGameManagerFactory.cs` | Core factory interface and null default (ADR-013) |
| `Main/Adapters/IFordsBattlefieldAdapter.cs`, `IFordsTowerAdapter.cs`, `IFordsSceneAdapter.cs`, `IFordsMenuAdapter.cs` (+ impls), `FordsAgentRegistry.cs`, `FordsEngine.cs` | Adapters and engine helpers |
| `Client/Features/FordsOfWar/FordsOfWarGameManager.cs`, `FordsOfWarGameManagerFactory.cs` | Client game manager and factory |
| `Client/Features/FordsOfWar/FordsOfWarMissionViews.cs` | `[ViewMethod]` list for the mission |
| `Client/Features/FordsOfWar/FordsCommanderCameraView.cs`, `FordsCameraMath.cs` | Commander camera |
| `Client/Features/FordsOfWar/FordsCommandView.cs`, `FordsCommandVM.cs`, `FordsCommandHotkeys.cs`, `FordsHudText.cs` | Command panel, hotkeys and every player-facing line |
| `Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml` | Panel prefab (movie `FordsCommand`) |
| `Main/_Module/ModuleData/fords_of_war/match.json`, `factions.json`, `maps.json` | Configuration |
| `Main/Features/FordsOfWar/Diagnostics/` | The self-check report (`FordsRunReport`), the verdicts and the evidence classes |
| `Main/Features/FordsOfWar/Hooks/FordsTelemetryLogic.cs`, `Hooks/FordsMissionResults.cs` | Mission behavior that feeds the report; the result hand-off |
| `Main/Features/FordsOfWar/FordsFieldProbe.cs`, `Main/Adapters/FordsVolleyHits.cs` | Field measurements and volley hit records for the report |
| `Main/Features/Warg/WargRageLog.cs` | Warg rage start and restore records the report reads |
| `Client/Features/FordsOfWar/FordsCameraProbe.cs` | Camera evidence from the client (the view resolves the report and passes it in) |
| `Client/ClientSubModule.OnBeforeInitialModuleScreenSetAsRoot` | The final report pass when the main menu is back |
| `docs/features/fords-of-war/build-spec.md`, `greybox-scene-spec.md` | Build contract; Kit scene spec |

## Dependencies

- `IFordsMenuAdapter` (Adapters): the only place the start flow touches `Module`, `InitialStateOption`,
  `MultiSelectionInquiryData`, `InformationManager`, `MBGameManager.StartNewGame` and `EndGame`.
- `IFordsBattlefieldAdapter`, `IFordsTowerAdapter`, `IFordsSceneAdapter` (Adapters): spawning and
  ordering agents, tower entities, gunners and volleys, scene boundary and navmesh.
- `IFordsOfWarGameManagerFactory` (Core, implemented by TAOM.Client): the game manager for a match.
- `DeferredCallbackQueue` (Core): parks agent removals and tower falls for the next tick.
- `DreadMissionGate` (Dread Aura): admits a Fords mission with no campaign (`FordsOfWarMission.IsActive`).
  Dread's hero axis reads a Custom Battle character's own id (`DreadSourceTracker.HeroAxisId`).
- `TaomConsole.RunAtMainMenu` and `DevConsoleGuard.CheckMainMenuCheatUsage` (Dev Console): the
  main-menu gate for `taom.fords_start`. See [dev-console.md](dev-console.md).
- `SubModule.RegisterCustomBattleModels`: TAOM's Custom Battle models apply in a game started by a
  `CustomGameManager` subclass, because the condition is "the starter is a `BasicGameStarter`", not the
  manager's type (read in the decompile, not checked in game).
- The volley item `western_javelin_2_t3`, a `CraftedItem` in SandBoxCore `weapons.xml` that a Custom
  Game loads.
- Co-op: `FordsOfWarModule` and the launcher contain no co-op handling, and the module declares no
  campaign behavior. Single player only.

## Tests

`TAOM.Tests/Features/FordsOfWar/` holds the mode's tests. Run them with `dotnet test TAOM.Tests
-p:DisableModuleCopy=true -p:ModuleId= --filter "FullyQualifiedName~Fords"`; the filter is by name, so it
also picks up a few tests outside the folder whose names contain "Fords" (the Dread Aura gate tests). The
counts change with every stage, so this page quotes none: run the filter and read its last line. The main
classes:

- `Domain/FordsDomainPurityTests.cs`: the purity gate (no `using TaleWorlds`, no `TAOM.Adapters` under `Domain/`).
- `Domain/FordsMatchTests.cs`, `FordsMatchScenarioTests.cs`, `FordsMatchSpawnFailureTests.cs`,
  `FordsMatchBuybackSlotTests.cs`, `FordsMatchKillCreditTests.cs`: the aggregate (a tower falls and the
  next unlocks, a keep unlocks, bounties, hero respawn and buyback, refunds for failed spawns).
- `Domain/WaveSchedulerTests.cs`, `TowerChainTests.cs`, `TowerDamageRulesTests.cs`, `HeroRosterTests.cs`,
  `LaneMarchRulesTests.cs`, `CapturePointRulesTests.cs`, `AiCommanderTests.cs`, `LaneLayoutGeneratorTests.cs`:
  one class per rule.
- `ShippedFordsConfigTests.cs`: the shipped JSON parses; every troop, hero and culture id exists; computed
  prices equal the stored ones; the hold line is outside tower fire range; the volley item is a crafted
  javelin that SandBoxCore loads for a Custom Game.
- `FordsMissionDirectorTests.cs`, `FordsSessionTests.cs`, `FordsOfWarMissionLogicTests.cs`,
  `FordsMissionLauncherTests.cs` (pins the behavior list and the omissions), `FordsMissionExitTests.cs`,
  `FordsExitClockTests.cs`.
- `FordsOfWarBindingTests.cs`, `FordsTowerHitPatchTests.cs`: engine bindings and the Harmony target.
- `FordsOfWarModuleTests.cs`, `FordsEntryPointLineCountTests.cs` (ADR-002 line caps),
  `Client/FordsClientWiringTests.cs`, `Client/FordsCommandVMTests.cs`, `Client/FordsCommandPrefabTests.cs`,
  `Client/FordsCameraMathTests.cs`, `Client/FordsHudStringsTests.cs`.
- `Diagnostics/` (the report, verdicts, evidence, log lines and report file), `FordsVerifyDocTests` (each checklist item names its `check=NN` row), and beside it the tests for the telemetry logic (`FordsTelemetryLogicTests`: the mission clock, the untimed first frame, the 1 Hz warg watch and the warg counts pushed before the closing pass; the options component, the warg log's activation, the 30 s agent-count window and `OnAgentHit` need a live mission and are not driven), the field probe, the volley hits and the camera probe (`Client/FordsCameraProbeTests.cs`, with a real `FordsRunReport`). `Main/Features/Warg/WargRageLog.cs` is tested in `TAOM.Tests/Features/Warg/WargRageLogTests.cs`; a reflection test there fails if the class names a TaleWorlds type in a signature.
- `Scenarios/` (the scenario harness): `FordsScenario` drives the real report, director and match, lane generator, spawn placement, field probe, exit clock and camera maths with faithful fakes, in the engine's per-frame order, then judges. `FordsScenarioTests` runs five scenarios (a clean three-lane match with no FAIL, one injected fault per row, a pause and a hitch, the camera pushed into the edge, the session tail after a finished run). `FordsVerdictContractTests` holds one row per verdict part (52) with the scenario that passes it and the fault that fails it, or `PassOnly`, and fails when the table and the parts `Judge` emits differ. The telemetry and mission logics are mirrored in the harness (they need a live mission), and the volley-hit reader is fed through the report, not driven (it takes engine structs).
- Shared tests this mode touches: `MissionViewRegistrationGateTests.Allowlist` (the two views built by
  the mission's `[ViewMethod]` list), `ClientSubModuleTests` (the exact client registrations) and
  `ConsoleCommandBindingTests.MainMenuCommands`.

The module declares no campaign behavior, so it needs no row in `docs/features/coop-interop.md`.

## How to verify in game

**Preconditions:** the desktop install with TAOM deployed and `TAOM.Client` loaded (a build from this
branch's worktree, launched from that worktree's `TAOM.sln`). Start the game to the main menu. The two
console commands need cheat mode: `taom.fords_start` is refused while a game is running and when cheat
mode is off, and `taom.fords_gold` is refused when no match is running. Keep the game log open: the
checks below name log lines.

**Self-check report.** Each match gets `Logs\fords_report_<log timestamp>_run<n>.json` beside `taom_debug_<same timestamp>.log`, written at its first verdict pass; the `[FordsOfWar][Run]` line at the start prints both absolute paths. A start that fails at the main menu writes `final=1` at once. The log gets one `[FordsOfWar][Verdict] check=NN result=PASS|FAIL|PARTIAL|NOT_SEEN` row per step below, with each part's own result, the numbers, the rule and what only you can judge. Rows marked `final=0` are written at the result, at mission end and at a start failure; the `final=1` block is written when the main menu is back (and again when you acknowledge the start-failed notice, and once more at the next match start when a run still owes a pass, for example a start-failed notice that was never acknowledged: that late block carries the notice FAIL). When one run has several `final=1` blocks, read its newest. Each run writes its own `final=1` block and its own JSON, and every row carries `run=<n>`. Do not read "the last `final=1` block": read the full match's block for items 3 to 14 and 16, the failed start's block for item 15, and take items 1 and 2 as session-wide (their rows count every menu flow and console start of the session). A `[FordsOfWar][Summary]` line every 30 s of mission time keeps the progress if the game crashes.

1. Look at the main menu. A "Fords of War" entry shows with its hint. Click it: your faction, enemy faction
   and difficulty prompts appear, then a map prompt (three maps are listed). Cancel one prompt and
   confirm the flow ends with no match. Report: `check=01` (option installed, the engine's visibility poll, prompts shown and answered per step, a cancel or abandoned flow seen; any menu flow that ended in a fault FAILs, even when a later flow was fine). Drawn on screen is yours to judge.
2. Run `taom.fords_start gondor mordor hard`. Observable: the console prints "Starting Fords of War:
   gondor against mordor on taom_rohan_battle_001_forceatmo, AI profile hard, 3 lanes." and the match
   loads. An unknown argument prints the list of valid ids. Run `taom.fords_start` with no arguments for
   the defaults (West gondor, East rohan, `normal`, the Rohan Plains map). Report: `check=02` (a named start that reached the match, an unknown argument's reply, the no-argument start and the setup it resolved; `ShippedFordsConfigTests` pins the shipped defaults, so the row does not ask you to compare them).
3. In the log, find `[FordsOfWar] match started on <scene>: 3 lane(s), ... AI profile <id>`. Confirm there
   is no `could not spawn '<id>'` line once the first waves and any bought hero are out: that proves the
   hero ids (for example `lord_1_17`) resolve in a Custom Game. Report: `check=03` (every troop and hero id checked against this game, spawns and spawn failures by troop; a recorded start failure with no match started FAILs its `started` part, for the menu routes too).
4. Wait out the 10 s countdown. Towers and keeps appear on both sides in 3 lanes, free waves march and
   fight, and your gold rises on kills. The camera pans (WASD), rotates (Q, E, right mouse drag) and
   zooms (wheel) and stays inside the playable area. Report: `check=04` (countdown end, towers built against expected, first spawn per lane, march gain per lane, deaths, kill gold, camera pan, rotate and zoom, inside the engine's boundary, camera ownership). Towers built are counted against the layout before unplaceable ones are dropped, so a dropped tower FAILs. A camera resting on the boundary edge counts as inside; it reads outside only when the engine says so and the camera is over 0.5 m from the edge.
5. Open the escape menu. Photo Mode reads disabled ("Photo mode isn't allowed."), and the Options entry
   opens the options screen. Report: `check=05` (pause edges, Photo Mode vetoes, Options requests and wiring).
6. Run `taom.fords_gold 5000`. The console prints "Added 5000 gold. You now hold ..." and the panel's gold
   matches. Buy a pack with F1 to F8 or the buttons; the gold drops by the pack price and a feed line
   appears. Report: `check=06` (the gold command's reply, pack purchases, feed lines).
7. Press H (or click a hero card) to field the most affordable hero, for Mordor Sauron at 2400. The hero
   spawns in the selected lane. Kill it, then check the respawn timer on its card and the buyback button. Report: `check=07` (a fielded hero has a spawned mark of the same hero and side within 5 mission seconds, with the requested lane in the `[Hero]` line; death; respawn spawned within 1.5 mission seconds; buyback results). The row no longer compares the lane the engine put the hero in with the one asked for, because the adapter read that lane back from the formation it had just placed the hero in, so the two could never differ. Leaving the match more than 5 mission seconds after a fielding with no spawn mark reads FAIL. A hero bought in the last 5 s before the winner is named is excused, because the clock ended before the spawn ran: it neither fails the part nor takes away the pass of the heroes that did spawn, and a part with only excused purchases reads NOT_SEEN.
8. Set a lane to Hold (X), then Push (Z) and Fall back (C). The order buttons show a cooldown; the lane
   forms up about 60 m before the enemy tower and does not stand inside tower fire. Report: `check=08` (accepted Push, Hold and Fall back orders, held lanes against tower fire range, the keep-to-keep distance as `baseDistance`). A hold sample counts only once the lane's front unit (the one farthest from its keep) is within 10 m of the hold point measured along the lane's axis (the direction of the path segment at the hold point), so a wide line formation whose middle stands at the point is settled and a lane still walking back from a tower is not judged. A FAIL names the nearest standing enemy tower whose fire range covered the lane (`West1 in fire of <tower id>`, and `tower=<id>` in the per-lane numbers).
9. Send melee units at an outer tower. Check the log and the tower bar: whether melee AI damages the barricade
   at all is still open (build-spec check 3). A tower behind another takes no damage until the outer one falls. Report: `check=09` (blows per tower by attacker class and reason, locked blows, melee near towers, engine against rules hit points).
10. Stand a side's own units next to its own tower and watch its bar: towers take no damage from their own
    side (gunners, defenders shooting across it). Report: `check=10` (own-side blows and hit-point drift). NOT_SEEN is expected when nothing strikes its own tower.
11. At match start, find the log line `tower volley item 'western_javelin_2_t3': thrust damage N,
    missile speed S`. Check that an unarmoured attacker hit in the torso near launch speed loses about
    N x (1 + 0.0006 x the gunner's Throwing skill) hit points. Armour, a leg hit, a non-human body and
    race damage reductions lower it; a head or neck hit doubles it; the impact speed scales it by
    (relative impact speed / launch speed) squared, so an attacker walking into the volley takes more.
    Check also that no warning says the reach is below the fire range. Report: `check=11` (item N and S, reach against fire range, per-hit magnitude against N x multiplier x (impact speed / S) squared). The `hits` part also FAILs when the towers asked to fire at least 10 times and not one missile was added (`launched` counts missiles the adapter added, 0 when the aim skipped them all), and when 20 or more missiles flew and no hit was seen; the row prints the hits it filtered out, by reason. The hit reader takes engine structs, so only your run proves it.
12. Field a Mordor or Isengard warg rider (`warg_raiders`, `warg_enforcers`) and watch a raging warg: it
    returns to its lane formation instead of wandering off. Report: `check=12` (rage starts, formations restored, riders back in formation within 60 s of mission time, riders left on foot with no formation to return to, called `stranded`, which FAIL the restored part). A rider counts as back when it is within max(15 m, half the formation's width + 5 m) plus the formation's depth of the formation's front-middle, so a rider at the rear of a deep formation is not read as lost. A rider whose rage restarted before it returned is counted as `superseded` (in the row's numbers and a `[Warg][Rage]` log line): its first reset already counted toward `restored`, and only its return goes unjudged. The `[Warg][Rage]` lines in the log show each episode (start, end, returned, not-returned, stranded, superseded).
13. Look at the capture-point row under the lane bars in the top-left panel: it shows owner and capture
    progress in percent, and a contested mark when both sides stand on the point. Report: `check=13` (captures, contested time, nearest approach per point).
14. Destroy a keep (use `taom.fords_gold` and a hero or siege unit to speed it up). The result panel
    appears; do nothing and the game returns to the main menu after 25 s, or click its button to leave at once. Report: `check=14` (keep fall, result, the way out and its timings, main menu back).
15. If a start can be made to fail (the log names the reason, such as a keep that cannot be built): the game
    returns to the main menu 3 s after the failure and shows the start-failed notice. Nobody has seen that
    notice appear over the main menu yet; if no failure can be provoked, report this step as unreachable. UNVERIFIED candidate for provoking route A: temporarily set a nonexistent keep prefab name in `maps.json` (nobody has tried it; restore the file afterwards). On the run where you provoke the failure on purpose, check 3 reads FAIL (its `started` part: a failure was recorded and the match never started), and that is the expected result there. Report: `check=15` (failure route, 3 s exit, notice requested and acknowledged, whether it was still up over the main menu). On route A the exit is timed in mission seconds, because a pause stops mission time and must not fail a working exit: it passes when the exit came due within 8 mission seconds of the failure and the mission closed within 15 s (wall time) of the end-game call. The `notice` part FAILs when the notice was requested and never acknowledged by the time the next run begins, or when the notice request itself reported failure. Check 3 reads FAIL on that run, as said above.
16. Let a 30-minute match run. The agent count stays under the cap and
    the frame time stays playable (build-spec check 6). Report: `check=16` (`cap`: peak live units per side against TAOM's config cap, `maxLivePerSide` plus the hero allowance; `agents`: the peak of all agents in the mission against the engine's agent limit, which the match records at its start through the battlefield adapter, PASS only with at least 60 s of live play; live seconds against 1800; frame times as numbers, no budget set).

Report any unreachable step with the command tried and the unmet precondition, and never mark it verified
through another path.

## How to tune or extend

**Change a number.** Edit `match.json`: no code change. Mind the pinned relations: `lanes.holdDistanceBeforeTower`
must stay above `towers.fireRange`, the readers bound each value (tower fractions 0.01 to 0.49, lane count 3 or 4).
Persisted saves hold nothing, because a match lives and ends inside one mission.

**Change what a faction sells.** Edit `factions.json`. Troop and hero ids must exist in the module data,
and hero ids must also resolve in a Custom Game (`characters/custom_battle_lords.xml`, or `characters/lords.xml`,
which is registered for `CustomGame`). Give an offer `level` and `eliteWeight` and let `TroopPricing` set the
price; add a `price` only to override it. Keep the two dearest offers within the first eight (F1 to F8).
Culture ids are traps (`empire` is Dunland): read them from the troop files.

**Add a faction.** Add the block, a `TAOM_FOW_Faction_<Name>` key and its row in `taom_module_strings.xml` and the
12 language files, then run `ShippedFordsConfigTests`.

**Add a map.** Add a scene to `maps.json` with `"layout": "generated"`. Pick an open, non-siege scene
with a large boundary and a navmesh; the layout is built from two base points found from the boundary, and
`FordsLayoutFitter` snaps points to the ground and the navmesh.

**Use a hand-built scene.** Follow [fords-of-war/greybox-scene-spec.md](fords-of-war/greybox-scene-spec.md):
the greybox scene `taom_fords_greybox_01` in the Modding Kit, with waypoints tagged `fow_lane_<lane>_<n>`,
towers tagged `fow_tower_<west|east>_<lane>_<order>`, keeps `fow_keep_<west|east>`, capture points
`fow_capture_<n>` and camps `fow_camp_<n>`. Then add it to `maps.json` with `"layout": "tags"`. An
incomplete set of markers (a keep or the outer tower of a lane missing) falls back to the generated
layout with one warning. A tagged entity that already has a `DestructableComponent` is adopted as the tower.

**Change AI behavior.** Edit `aiProfiles` for the numbers, or `AiCommander` for the rules. The AI reads
only `MatchView`, and the offers it sees exclude any offer a failed spawn marked unavailable.

**Add a rule.** Put it in `Domain/` with its own test class, call it from `FordsMatch.Tick` or a `Try*`
method, and surface it through `MatchView` and `FordsSession`. Do not reference TaleWorlds there: the
purity test fails the build.

**Add or change player text.** Every `TAOM_FOW_` line is a `{=KEY}default` literal in `FordsHudText.cs` (panel)
or the setup flow, registered in `taom_module_strings.xml` and seeded in English in the 12 language files.
`FordsHudStringsTests` pins source, English file and seeds against each other.

## Known limitations

- **Dread aura cannot rout troops here.** Nothing turns a panic into a rout: the launcher leaves out
  `MissionAgentPanicHandler` on purpose (the behavior whose `OnPreMissionTick` calls `Retreat`;
  `FordsMissionLauncherTests` pins the omission), because troops that rout would leave their lanes. The
  engine's default mission behaviors do not add it, and at the shipped `moraleFloor` of 10 the drain never
  reaches the engine's 0.01 panic threshold anyway. The aura still works through the fear voice
  (`fearVoiceChancePerPulse`) and race-ability frights. The no-rout claim was not seen in game.
- **Neutral camps are a later phase.** `CampRules` and the camp slots exist in the domain, but the aggregate
  does not call them and nothing spawns camps (a third team is needed; research the team-count limit first).
- **The AI does not steer to capture points.** It buys and orders by lane threat and assault state only.
- **Translations.** The `TAOM_FOW_` texts are English in all 12 languages until the translation backlog,
  issue #763.
- **Greybox scene owed.** The Kit scene from the greybox spec is not built; the three shipped maps use a
  generated layout and their boundary and navmesh have not been checked in game.
- **Not checked in game at all:** every item under "How to verify in game". The volley's damage and
  speed, the warg formation fix, the start-failed notice and melee damage to the barricade are the open
  engine questions.

## Performance

- Spawning is budgeted: `waves.maxSpawnsPerTick` units per tick, `maxLivePerLanePerSide` and
  `maxLivePerSide` caps, and a corpse fade-out of 4 s.
- A formation order is issued only when it differs from the last applied one (`LaneCommand` has value
  equality). The old Hold rule, which used the drifting live center, produced a new command about 60 times
  a second; Hold now has one stable target.
- The tower hit prefix reads one volatile field first, so a mission with no Fords match costs a null check.
- Panel lists are updated in place, so a button under the cursor survives the refresh (about four a second).
- Agent count is judged by `check=16`, against TAOM's config cap and the engine's agent limit; frame times are reported per 30 s window with no budget set.

## Changelog

- 2026-10-31: self-check review fixes: the warg rage log, the run's own mission clock, the verdict rules
  of checks 7, 8, 11, 12, 15 and 16, and the scenario tests that drive the real report.
- 2026-10-31: self-check report: per-step verdict rows and a JSON report per run.
- 2026-10-31: first build. The mode starts from the main menu or `taom.fords_start`, runs a 3-lane
  tower-and-keep match for four factions (Gondor, Rohan, Mordor, Isengard) on a generated layout over three
  existing field scenes, with a commander camera, a command panel, heroes with respawn and buyback, capture
  points, a tower damage table behind Patch131, and an AI commander at three difficulties.

## GitHub Issue

Not created yet: the issue needs Mike's word.
