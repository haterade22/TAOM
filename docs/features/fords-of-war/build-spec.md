# Fords of War: build specification

This is the contract every build stage follows. It pins names, signatures, folders and default
numbers so that stages written by different agents fit together. When code and this file disagree,
fix the code, or record the change in the section "Deviations" at the end with the reason.

## 1. What the mode is

A campaign-free single-player mission, started from the main menu. Two sides, **West** and
**East**, face each other across 3 lanes (4 later). Each side owns an ordered chain of towers in
every lane and a keep behind its lanes. Each side has one or more **commander slots**; a slot is a
human or an AI and has its own gold. In single player the West side is one human slot and the East
side is one AI slot.

- The player has **no agent**. The player is a commander: a free camera plus a command panel.
- Every lane gets a small **free wave** per side on a timer. Commanders spend gold on **packs** of
  troops for a chosen lane, and on **heroes** (Sauron, the Witch-King, Saruman...).
- Troops march their lane automatically. A commander has three **lane orders** per lane: Push
  (default), Hold, Fall back. Orders have a cooldown. The AI uses the same orders.
- Towers have high hit points and **shoot back** (garrison gunners plus scripted volleys). A tower
  takes damage only when every tower in front of it in that lane has fallen. A keep takes damage only
  when its side has lost every tower in at least one lane.
- Kills pay gold to the owner of the killing unit. Towers and heroes pay a larger bounty.
- **Capture points** between lanes pay income to the side that holds them.
- **Neutral camps** are in the domain model now; their engine spawn is Phase 3 (see 9).
- A side wins when it destroys the enemy keep. Target length: 30 to 45 minutes.
- Heroes: at most 2 alive per side (config). A dead hero respawns after a timer, or the owner pays
  a buyback price to bring it back at once.

Multiplayer is a later, separate project. The one thing this build does for it: **all match rules
live in a pure domain layer with no TaleWorlds reference**, behind one command API that a network
server could own later.

## 2. Layers and folders

| Layer | Folder | May reference |
|---|---|---|
| Domain (pure rules) | `Main/Features/FordsOfWar/Domain/` | BCL only. No `using TaleWorlds.*`, no `TAOM.Adapters`. Gate test enforces it. |
| Engine services | `Main/Features/FordsOfWar/` (+ `Config/`) | Domain, adapter interfaces, `TAOM.Core.Logging` |
| Entry points | `Main/Features/FordsOfWar/Hooks/` | Services, TaleWorlds types (thin, under 150 lines, ADR-002) |
| Adapters | `Main/Adapters/IFords*Adapter.cs`, `Fords*Adapter.cs` | TaleWorlds core types (server-safe assemblies only) |
| Client UI | `Client/Features/FordsOfWar/` | Anything client-only: Gauntlet, MissionView, CustomBattle DLL (ADR-013) |
| Data | `Main/_Module/ModuleData/fords_of_war/*.json` | |
| Tests | `TAOM.Tests/Features/FordsOfWar/` (Domain tests under `Domain/`) | MSTest + NSubstitute |

Namespaces: `TAOM.Features.FordsOfWar.Domain`, `TAOM.Features.FordsOfWar`,
`TAOM.Features.FordsOfWar.Hooks`, `TAOM.Adapters`, `TAOM.Client.Features.FordsOfWar`. File-scoped
namespaces, `internal sealed` by default, C# 10, nullable enabled. Records are fine (the repo uses
them). Test names: `Method_State_Expected`.

Project rules that apply (read the files, do not restate them): `AGENTS.md` (TDD, architecture,
banned constructs), `.claude/rules/csharp-architecture.md` (agent slots, threads, `RunOrDefer`),
`.claude/rules/harmony-patches.md` (thread table, mission behavior lifecycle), ADR-002, ADR-007,
ADR-013. Builds and tests never deploy: `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=`
and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`. Never `./build.ps1`.

## 3. Domain contract (`Main/Features/FordsOfWar/Domain/`)

One public concept per file. Every rule class is engine-free and has its own test class.

### 3.1 Basic types

```csharp
internal enum MatchSide { West, East }            // static class MatchSides { Opponent(side) }
internal enum LaneOrder { Push, Hold, FallBack }
internal enum UnitRole { Infantry, Spear, Ranged, Cavalry, HorseArcher, Siege, Hero }
internal enum TowerKind { Tower, Keep }
internal enum MatchPhase { Countdown, Live, Ended }
internal readonly record struct Vec3f(float X, float Y, float Z)   // Distance, Distance2D, Lerp, +, -, *
internal readonly record struct AgentKey(int Value)                 // opaque id from the engine layer, NEVER Agent.Index
internal interface IFordsRandom { int Next(int maxExclusive); float NextFloat(); }  // + SeededFordsRandom
```

### 3.2 Configuration records (plain data, built by the engine layer from JSON)

```csharp
internal sealed record FordsEconomyConfig(int StartingGold, int IncomePerMinute, float KillBountyFraction,
    int MinKillBounty, int TowerBounty, int HeroKillBounty, int CaptureIncomePerMinute, int MaxGold);
internal sealed record FordsWaveConfig(float FreeWaveIntervalSeconds, int MaxLivePerLanePerSide,
    int MaxLivePerSide, int MaxSpawnsPerTick, int MaxQueuedPacksPerLane);
internal sealed record FordsTowerConfig(int TowerHitPoints, int InnerTowerHitPoints, int KeepHitPoints,
    float RangedDamageFactor, float HeroDamageFactor, float SiegeDamageFactor, float RegenPerSecond,
    float RegenDelaySeconds, int GunnersPerTower, float VolleyIntervalSeconds, int VolleyMissiles,
    float FireRange, float AttackRange);
internal sealed record FordsHeroConfig(int MaxAlivePerSide, float RespawnBaseSeconds,
    float RespawnPerMinuteSeconds, float BuybackBaseFraction, float BuybackPerMinuteFraction);
internal sealed record FordsLaneConfig(float OrderCooldownSeconds, float EngageRange, float WaypointReachRadius,
    float HoldDistanceBeforeTower, int AssaultThreshold);
internal sealed record FordsCaptureConfig(float Radius, float SecondsToCapture);
internal sealed record FordsAiProfile(string Id, float DecisionIntervalSeconds, float IncomeMultiplier,
    float MistakeChance, int HeroReserveGold);
internal sealed record FordsMatchConfig(FordsEconomyConfig Economy, FordsWaveConfig Waves,
    FordsTowerConfig Towers, FordsHeroConfig Heroes, FordsLaneConfig Lanes, FordsCaptureConfig Capture,
    IReadOnlyDictionary<string, FordsAiProfile> AiProfiles, float CountdownSeconds);

internal sealed record UnitCount(string TroopId, int Count);
internal sealed record FreeWaveTier(float FromMinute, IReadOnlyList<UnitCount> Units);
internal sealed record UnitOffer(string OfferId, string TroopId, UnitRole Role, int PackSize, int PackPrice);
internal sealed record HeroOffer(string HeroId, int Price);     // HeroId is an NPCCharacter id
internal sealed record FactionCatalog(string FactionId, string CultureId,
    IReadOnlyList<UnitOffer> Units, IReadOnlyList<HeroOffer> Heroes, IReadOnlyList<FreeWaveTier> FreeWaves);
```

`TroopPricing.UnitPrice(int level, bool mounted, float eliteWeight)` gives the default unit price
when an offer has no explicit price. Level grid and base price per unit: 1:4, 6:6, 11:9, 16:13,
21:18, 26:25, 31:34, 36:45, 41:60, 46:80, 51:105 (between grid points, use the lower one). Mounted
x1.5. Multiply by `eliteWeight` (troop_weights.xml: troll 4, mumak 20). Pack price = unit price x
pack size, rounded to the nearest 5.

### 3.3 Map layout

```csharp
internal sealed record LaneLayout(int Index, IReadOnlyList<Vec3f> Path);   // Path[0] = West base end, last = East base end
internal sealed record TowerSlot(string Id, MatchSide Owner, int Lane, int Order, TowerKind Kind, Vec3f Position, float Yaw);
    // Order 0 = the outermost tower (nearest the middle). Keeps: Lane = -1, Order = 99.
internal sealed record CapturePointSlot(string Id, Vec3f Position);
internal sealed record CampSlot(string Id, Vec3f Position, string TroopId, int Count, int Reward);
internal sealed record MapLayout(string SceneName, IReadOnlyList<LaneLayout> Lanes, IReadOnlyList<TowerSlot> Towers,
    IReadOnlyList<CapturePointSlot> CapturePoints, IReadOnlyList<CampSlot> Camps);
```

`LaneLayoutGenerator.Generate(string scene, Vec3f westBase, Vec3f eastBase, LayoutOptions o)`
builds a layout on any open scene from two base points (the engine layer finds them from the scene
boundary). `LayoutOptions(int LaneCount = 3, float LaneSpread = 45f, float WaypointStep = 12f,
IReadOnlyList<float> TowerFractions = {0.36, 0.20}, float KeepInset = 0.04)`. The middle lane is the
straight line between the bases. Side lanes leave each base together, bow out to `LaneSpread`
metres from the middle line by 25 percent of the length, run parallel, and come back together
at the other base (a smooth curve). Towers per side per lane at the given fractions of lane length
measured from that side's base, so `0.36` is the outer tower. Keeps at `KeepInset` from each base.
Capture points: one between each pair of neighbour lanes, at the middle of the length. Camps: one
per side between each pair of neighbour lanes at 0.35 of the length (rewards and troops come from
`maps.json` defaults). Heights stay as given; the adapter snaps them to the ground.

`LaneGeometry` helpers: progress of a point along a path (0 at West end, 1 at East end), nearest
path index, next waypoint toward the enemy for a side, point at a distance before a tower.

### 3.4 Rules (one class each, all pure)

| Class | Responsibility |
|---|---|
| `GoldLedger` | Gold per slot. `Balance(slot)`, `Earn(slot, amount)` (clamped at MaxGold), `TrySpend(slot, amount)`. |
| `BountyRules` | `KillBounty(int unitValue)` = max(MinKillBounty, round(value x KillBountyFraction)); tower bounty split equally across the destroying side's slots; hero kill bounty. |
| `WaveScheduler` | Free-wave timer per lane per side; tier by match minute; purchased pack queue per lane per side (max `MaxQueuedPacksPerLane`); emits `SpawnRequest`s within caps (`MaxLivePerLanePerSide`, `MaxLivePerSide`, `MaxSpawnsPerTick`). Free-wave units belong to the slot that owns the lane. |
| `TowerChain` | Tower state from the layout: hit points, destroyed, `IsLocked(towerId)`; keep lock rule; `OnDestroyed`. |
| `TowerDamageRules` | `int Effective(TowerHit hit)`: locked gives 0; a hit from the tower owner's own side (when `AttackerSide` is known) gives 0; ranged x RangedDamageFactor; hero x HeroDamageFactor; siege role x SiegeDamageFactor; at least 1 when the input is positive and unlocked. |
| `TowerRegen` | Regen `RegenPerSecond` after `RegenDelaySeconds` with no damage; never above max; never for a destroyed tower. |
| `TowerFireRules` | Per tower: cooldown `VolleyIntervalSeconds`; target = nearest enemy in `FireRange`, heroes first when a hero is in range; returns `TowerVolley(towerId, AgentKey target, int missiles)`. |
| `HeroRoster` | Per side: alive count limit; states Alive, Dead(respawnAt), with owner slot and lane; respawn time = RespawnBase + RespawnPerMinute x minute; buyback price = price x (BuybackBase + BuybackPerMinute x minute). Emits respawn spawn requests. |
| `LaneOrderBook` | Order per lane per side; cooldown per lane per side; `TrySet`. |
| `LaneMarchRules` | `LaneCommand Decide(LaneSituation s)` (below). |
| `CapturePointRules` | Progress per point from counts of each side in radius; contested when both present; owner changes at full progress; income per owned point. |
| `CampRules` | Camp alive count, cleared event, reward to the clearing side's slot that owns the nearest lane. |
| `MatchClock` | Phase (Countdown, Live, Ended), elapsed, minute. |
| `AiCommander` | Below. |

`LaneSituation(MatchSide Side, int Lane, LaneOrder Order, int LiveUnits, Vec3f? Center,
float NearestEnemyDistance, string? NextEnemyTowerId, float NextEnemyTowerDistance, bool NextEnemyTowerLocked)`.
`LaneCommand` kinds: `MoveTo(Vec3f)`, `Charge`, `AttackTower(string towerId)`, `HoldAt(Vec3f)`,
`None`. Decision order:
1. No units: `None`.
2. `FallBack`: `MoveTo` the waypoint nearest the side's own outermost living tower.
3. Enemy units within `EngageRange` of the center: `Charge`.
4. Next enemy tower within `AttackRange` and not locked: `AttackTower` (unless order is Hold).
5. `Hold`: `HoldAt` the one stable point `HoldDistanceBeforeTower` before the next enemy tower on
   the lane path, wherever the formation stands (a lane short of it advances to it, a lane past it
   falls back to it). With no next enemy tower or no path for the lane: `None`.
6. Otherwise `MoveTo` the next waypoint toward the enemy.
The engine layer applies a command only when it differs from the last one applied to that formation.

### 3.5 The match aggregate

```csharp
internal sealed record CommanderSlot(int Id, MatchSide Side, bool IsHuman, string? AiProfileId, IReadOnlyList<int> Lanes);
internal enum CommandResult { Ok, NotEnoughGold, UnknownOffer, UnknownLane, LaneNotOwned, QueueFull,
    HeroLimit, HeroAlreadyFielded, HeroNotDead, OnCooldown, NotLive }
internal sealed record SpawnRequest(int RequestId, MatchSide Side, int Lane, string TroopId, int Count,
    int OwnerSlot, UnitRole Role, bool IsHero);
internal sealed record UnitRecord(AgentKey Key, MatchSide Side, int Lane, int OwnerSlot, int Value, UnitRole Role, string? HeroId);
internal sealed record TowerHit(string TowerId, bool Ranged, UnitRole AttackerRole, int RawDamage, MatchSide? AttackerSide = null);
internal sealed class MatchSnapshot { float Dt; IReadOnlyList<LaneSnapshot> Lanes;
    IReadOnlyList<TowerTargets> TowerTargets; IReadOnlyList<CaptureCounts> Capture; IReadOnlyDictionary<MatchSide,int> LiveBySide; }
internal sealed record LaneSnapshot(MatchSide Side, int Lane, int LiveUnits, Vec3f? Center, float NearestEnemyDistance);
internal sealed record TargetCandidate(AgentKey Key, float Distance, bool IsHero);
internal sealed record TowerTargets(string TowerId, IReadOnlyList<TargetCandidate> EnemiesInRange);
internal sealed record CaptureCounts(string PointId, int West, int East);
internal sealed class MatchEffects { List<SpawnRequest> Spawns; List<FormationCommand> Commands;
    List<TowerVolley> Volleys; List<MatchEvent> Events; MatchSide? Winner; }
internal sealed record FormationCommand(MatchSide Side, int Lane, LaneCommand Command);
internal sealed record MatchEvent(MatchEventKind Kind, MatchSide Side, string Subject, int Amount);
    // kinds: TowerDestroyed, KeepDestroyed, HeroFielded, HeroDied, HeroRespawned, PointCaptured, CampCleared, PackBought

internal sealed class FordsMatch
{
    FordsMatch(FordsMatchConfig config, MapLayout layout, IReadOnlyDictionary<MatchSide, FactionCatalog> factions,
               IReadOnlyList<CommanderSlot> slots, IFordsRandom random);
    MatchEffects Tick(MatchSnapshot snapshot);             // advances clock, income, waves, regen, fire, capture, AI, lane commands
    CommandResult TryBuyPack(int slot, int lane, string offerId);
    CommandResult TryBuyHero(int slot, int lane, string heroId);
    CommandResult TryBuyback(int slot, string heroId);
    CommandResult TrySetLaneOrder(int slot, int lane, LaneOrder order);
    void RegisterUnit(int requestId, UnitRecord unit);     // one call per spawned agent
    void OnSpawnFailed(int requestId, int unspawned);      // units of a request that never spawned: refund, free the hero slot
    void OnUnitKilled(AgentKey victim, AgentKey? killer);  // bounty, hero death; unknown keys are ignored
    void OnTowerDestroyed(string towerId, AgentKey? killer);
    int AdjustTowerDamage(TowerHit hit);                   // the tower hit hook calls this; never throws
    MatchView GetView(int slot);                           // read model for the UI and the AI
    bool IsTower(string towerId);
}
```

`MatchView`: phase, elapsed, the slot's gold, per lane (order, cooldown left, own and enemy live
counts, towers with id, owner, order, hp, max, locked, destroyed), keeps, heroes (id, side, state,
respawn left, buyback price), offers with price and affordability, capture points (owner, progress),
winner. The AI reads the same view.

`AiCommander(int slot, FordsAiProfile profile, IFordsRandom random)`, `IReadOnlyList<AiAction>
Decide(MatchView view, float now)`. Every `DecisionIntervalSeconds`:
1. Threat per own lane = enemy live count near its next own tower; defend the worst lane first with
   the cheapest Spear or Infantry pack when the threat exceeds own count.
2. Coordinated assault: in the lane with the best own-to-enemy ratio, buy packs and keep the lane on
   Hold until own live count reaches `AssaultThreshold`, then Push.
3. When gold exceeds hero price + `HeroReserveGold` and a hero slot is free, field the most
   expensive affordable hero in the assault lane. Buy back a dead hero when gold is plentiful.
4. With probability `MistakeChance`, skip the decision (easy difficulty feels softer).
`IncomeMultiplier` applies to the AI slot's passive income only (config, not cheating on bounties).

### 3.6 Gate tests in this layer

- `FordsDomainPurityTests`: no file under `Main/Features/FordsOfWar/Domain/` contains `using TaleWorlds`
  or `TAOM.Adapters`.
- Every rule class has its own test class; the aggregate has scenario tests (a tower falls, the next
  unlocks, a keep unlocks, a bounty is paid to the killer's owner, a hero respawns, the AI defends).

## 4. Engine layer (`Main`)

| File | Purpose |
|---|---|
| `FordsOfWarModule.cs` | `TaomFeatureModule`. Registers services; declares the patch category `Patch131_FordsOfWar` (130 was the highest in use on 2026-10-30; add its row to `docs/reference/harmony-patch-registry.md`) at `ApplyPhase.FirstMission`; `OnPhase(MainMenu)` adds the main-menu option once (guard by id, safe to repeat, see `MainMenuStep`). Add one line to `Main/Composition/FeatureModules.cs`. No `MissionBehaviorDecl`: the mission logic is added only by the launcher. |
| `FordsOfWarMission.cs` | `const string MissionName = "TaomFordsOfWar"`; `IsActive(Mission?)`. |
| `Config/FordsConfigCatalog.cs` + DTOs | Loads `match.json`, `factions.json`, `maps.json` once per process, never throws, logs one warning per bad entry, returns domain records. Pattern: `StagedSiegeSceneCatalog`. |
| `FordsMatchSetup.cs` | `record FordsMatchSetup(string SceneName, string WestFaction, string EastFaction, string AiProfile, int LaneCount)`. |
| `FordsSetupPrompt.cs` | Main-menu flow: three `MultiSelectionInquiryData` prompts (your faction, enemy faction, difficulty) and a map prompt when `maps.json` lists more than one scene; then `MBGameManager.StartNewGame(factory.Create(setup))`. |
| `IFordsOfWarGameManagerFactory.cs` | Core interface; `TAOM.Client` implements it (ADR-013), like `IShaderPrecompileGameManagerFactory`. Core registers a null default that logs and returns null when the client is absent. |
| `FordsMissionLauncher.cs` | Called by the client game manager in `OnLoadFinished`. `MissionState.OpenNew(MissionName, new MissionInitializerRecord(scene){ PlayingInCampaignMode = false, ... }, behaviors)`. Behavior list: `MissionCombatantsLogic` with `MissionTeamAITypeEnum.NoTeamAI` (two `CustomBattleCombatant`s; West is the player side, `isPlayerGeneral` true, but NO player agent is spawned), `AgentHumanAILogic`, `AmmoSupplyLogic` for both sides, `MissionHardBorderPlacer`, `MissionBoundaryPlacer`, `MissionOptionsComponent` (so the escape menu's Options entry works), `FordsOfWarMissionLogic`, then `FordsTelemetryLogic` last (the self-check report's feed). Leave out: `DefaultBattleMissionAgentSpawnLogic`, `BattleEndLogic`, deployment controllers, morale and panic logics, `AgentVictoryLogic`, `BattleMissionStarterLogic` (only sets battle mode, which `MissionCombatantsLogic.AfterStart` already does) and `MissionBoundaryCrossingHandler` (acts only on `Agent.Main` in single player, and the mission has none). Template: `HostReviewMissionLauncher` (behavior list) and the vanilla lord's-hall list (`BannerlordMissions.cs:273-291`). |
| `FordsMissionDirector.cs` | Per-mission orchestrator, owned by the mission logic. Setup: build or load the layout, spawn towers and gunners, set corpse fade-out (4 s), create the match. Each tick: build the snapshot through the adapters, `match.Tick`, apply effects (spawn within caps, apply formation commands only on change, fire volleys, end the match). Kill callbacks go through `DeferredCallbackQueue.RunOrDefer` or a lock, because they can arrive off the main thread; a tower's fall is always parked for the next tick. |
| `Hooks/FordsOfWarMissionLogic.cs` | `MissionLogic`, under 150 lines: `AfterStart` (throw-safe), `OnMissionTick`, `OnAgentRemoved`, `MissionEnded(ref MissionResult)` (true once the match has a winner; `Mission.CheckMissionEnd` polls it every 0.1 s from 10 s mission time, `Mission.cs:4819-4883`), `OnEndMission`, all delegating to the director. Its tick also drives `FordsMissionExit` (the automatic return to the main menu: nothing closes a mission ended through `MissionEnded`). Never spawn `Game.Current.PlayerTroop` and never set an agent's controller to `Player`: the mission must keep no main agent. |
| `IFordsTowerHitHook.cs` + `Hooks/FordsTowerHitPatch.cs` | Harmony prefix on `DestructableComponent.OnHit` with `ref int inflictedDamage`: when the component is a registered Fords tower, replace the damage with `AdjustTowerDamage`. Never throws; unknown components pass through untouched. Binding test pins the target signature. |
| `Main/Adapters/IFordsBattlefieldAdapter.cs` (+ impl) | Spawn agents (`Mission.SpawnAgent` with `AgentBuildData`: `Team`, `Formation`, `InitialPosition`, `InitialDirection`, `TroopOrigin(new BasicBattleAgentOrigin(character))`, `Banner(team.Banner)`), one formation per lane per side (lane index = formation index), formation center and live count, nearest enemy distance, enemies in range of a point, apply `MovementOrderMove`, `MovementOrderCharge`, `MovementOrderAttackEntity`, corpse fade, end mission. Issues orders on the main thread only. |
| `Main/Adapters/IFordsTowerAdapter.cs` (+ impl) | `GameEntity.Instantiate(scene, prefab, frame)` of `siege_barricade_a` (configurable per tower kind, scale configurable), set `MaxHitPoint` and `HitPoint`, subscribe `OnDestroyed`, map component to tower id, spawn gunners (invulnerable archers held at the tower base, ammo kept up by the mission's `AmmoSupplyLogic`, killed when the tower falls), fire volleys with `Mission.AddCustomMissile` of a crafted javelin (`western_javelin_2_t3`) owned by a gunner. |
| `Main/Adapters/IFordsSceneAdapter.cs` (+ impl) | Scene boundary to base points (inset along the longest axis), ground height and navmesh snapping for a layout, tagged-entity layout reading for a future authored scene (tags `fow_lane_<i>_<n>`, `fow_tower_<side>_<lane>_<order>`, `fow_keep_<side>`). |
| Dread Aura | `DreadMissionGate`: also eligible when `FordsOfWarMission.IsActive(mission)`, without the campaign requirement on that branch. Test it. |

### 4.1 Session API for the client

The client never touches `FordsMatch` or the director. Core exposes one singleton,
`FordsSession` (concrete class, registered by the module; an interface only if a client test needs
a fake), which the director fills at mission start and clears at mission end:

```csharp
internal sealed class FordsSession
{
    bool IsActive { get; }                       // a Fords match is running
    MatchView? GetView();                        // GetView of the local commander slot (West in single player) of the running match, or null
    CommandResult TryBuyPack(int lane, string offerId);
    CommandResult TryBuyHero(int lane, string heroId);
    CommandResult TryBuyback(string heroId);
    CommandResult TrySetLaneOrder(int lane, LaneOrder order);
    string DisplayName(string characterId);      // resolved once through an adapter, cached; the id when unknown
    IReadOnlyList<MatchEvent> DrainEvents();     // events since the last call, for the panel's feed
    Vec3f CameraStart { get; }                   // above the human side's keep
    Vec3f CameraLookAt { get; }                  // toward the enemy base
    IReadOnlyList<Vec3f> BoundaryPolygon { get; } // to keep the camera inside the scene
}
```

All members run on the main thread (views tick there). Commands go to the same `FordsMatch.Try*`
methods the AI uses, so a future multiplayer server can sit at this seam.

Model check owed in the engine stage: confirm whether TAOM's Custom Battle models
(`SubModule.RegisterCustomBattleModels`) apply in a game started by a `CustomGameManager`
subclass, and report the condition that registers them.

Signature checks: read the decompiled files in `C:/Users/mikew/.taom-src/v1.5.6/` directly (the file
for `TaleWorlds.MountAndBlade.Mission` is `TaleWorlds.MountAndBlade.Mission.cs`). Batch the reads.

## 5. Data (`Main/_Module/ModuleData/fords_of_war/`)

- `match.json`: every number in 3.2, with the defaults below, and `aiProfiles` easy, normal, hard.
- `factions.json`: start with `gondor`, `rohan`, `mordor`, `isengard`. Per faction: culture id,
  6 to 10 unit offers across roles (basic infantry, spears, archers, cavalry where the culture has
  it, one elite, one siege unit when it exists, for example `cave_troll` or `hill_troll` for Mordor
  and Isengard), 2 or 3 heroes, and free-wave tiers at minute 0, 10 and 20. Every troop and hero id
  must exist in `Main/_Module/ModuleData` (a shipped-config test checks it). Heroes must also
  resolve in a Custom Battle game: check `characters/custom_battle_lords.xml` and add a stub when one
  is missing. Culture ids are traps (`empire` is Dunland): read them from the troop files.
- `maps.json`: default scene list (open field battle scenes from `custom_battle_scenes.xml` with a
  large boundary), layout options, camp defaults, tower prefab names and scales. Each scene entry has
  `"layout": "generated"` (from the boundary, the default) or `"layout": "tags"` (read the markers in
  `greybox-scene-spec.md`: `fow_lane_<lane>_<n>`, `fow_tower_<west|east>_<lane>_<order>`,
  `fow_keep_<west|east>`, `fow_capture_<n>`, `fow_camp_<n>`). A tags scene whose markers are
  incomplete falls back to the generated layout and logs one warning.

Default numbers (all tunable): starting gold 600, income 120 per minute, bounty fraction 0.35,
minimum bounty 2, tower bounty 300, hero kill bounty 250, capture income 60 per minute per point,
max gold 99,999; free wave every 40 s; caps 90 live per lane per side, 300 per side, 6 spawns per
tick, 3 queued packs per lane; tower 8,000 hp, inner tower 11,000, keep 18,000, ranged factor 0.15,
hero factor 4, siege factor 3, regen 15 per s after 20 s, 2 gunners per tower, a volley of 3 javelins every
4 s, fire range 45 m, attack range 20 m (formation center to tower; was 6 m, too small for a center, changed 2026-10-30); heroes 2 alive per side, respawn 60 s + 6 s per minute,
buyback 0.5 + 0.05 per minute of price; lane order cooldown 20 s, engage range 25 m, waypoint radius
6 m, hold distance 60 m (fire range 45 m plus 15 m for formation depth), assault threshold 40; capture radius 12 m, 20 s to capture; AI easy (12 s,
x0.8, 0.25, 200), normal (8 s, x1.0, 0.1, 300), hard (5 s, x1.2, 0.0, 400); countdown 10 s.

## 6. Client (`Client/Features/FordsOfWar/`)

| File | Purpose |
|---|---|
| `FordsOfWarGameManager.cs` | `CustomGameManager` subclass (template `TaomShaderGameManager`); `OnLoadFinished` calls the core launcher with the setup. When the mission ends, return to the main menu. |
| `FordsOfWarGameManagerFactory.cs` | Implements the core factory; one line in `TaomClientComposition`. |
| `FordsOfWarMissionViews.cs` | `[ViewCreatorModule]` with `[ViewMethod(FordsOfWarMission.MissionName)]`: only views that work with no main agent (template `HostReviewMissionViews`, minus the main-agent equipment and cheer views). |
| Commander camera | See 6.1 (pending research). |
| `FordsCommandView.cs` + `FordsCommandVM.cs` + prefab XML | Gauntlet layer, cursor always visible: gold, clock, lane selector, unit offers, hero offers and buybacks, three lane-order buttons with cooldowns, tower bars per lane, a read-only row per capture point, event feed, victory or defeat panel. Hotkeys: 1 to 4 select lane, F1 to F8 buy offer, H fields the first affordable hero, Z Push, X Hold, C Fall back. All text as `{=TAOM_FOW_...}default` keys. Template: the Staged Siege HUD (`StagedSiegeMissionView`, `StagedSiegeHudVM`, its prefab and prefab tests). |

### 6.1 Commander camera and input (research `wf_25118a6d-08f`, static reading of v1.5.6)

Facts the design rests on:

- A mission has a main agent only when an agent gets `AgentControllerType.Player`
  (`Agent.cs:1252-1256`; `Mission.cs:4504-4507` sets it only for `Game.Current.PlayerTroop` on the
  player side). The mode never spawns the player troop, so `Agent.Main` stays null.
- With no main agent in single player, `MissionScreen` uses `SpectatorCameraTypes.Free`, but the
  spectator-cycle keys can lock the camera onto an agent (`MissionScreen.cs:3838-3929`).
- `MissionView.UpdateOverridenCamera(float dt)` returning true makes `MissionScreen` skip its own
  camera update (`MissionScreen.cs:858-905`). The view then owns the frame, as the tail of vanilla
  `UpdateCamera` does (`MissionScreen.cs:2357-2365`): set `CombatCamera.Frame` (or call
  `UpdateFreeCamera(MatrixFrame)`), the vertical FOV, `SceneView.SetCamera(CombatCamera)` and
  `Mission.SetCameraFrame` (audio and LOD).
- The vanilla order bar refuses to open with no main agent (`MissionOrderVM.cs:740-749`). Never use
  `OrderType.FormCustom`: `OrderController.GetOrderFormCustomWidth` reads `Agent.Main.Position`
  (`OrderController.cs:1621-1624`).
- `MissionScreen.GetProjectedMousePositionOnGround` (`:3543`) and `ScreenPointToWorldRay` (`:3502`)
  turn a cursor position into a ground point.

Design:

- `FordsCommanderCameraView : MissionView`, overriding `UpdateOverridenCamera` and returning true
  while the Fords mission is active. A top-down RTS camera: WASD or arrow keys pan on the ground
  plane relative to the camera yaw, Q and E rotate, the mouse wheel zooms (height 15 m to 120 m),
  holding the right mouse button rotates and tilts, Shift doubles the pan speed. The pitch follows the
  height (steeper when high). The start frame is above the West keep, looking toward the East base.
  Keep the camera inside the scene boundary. Call `MissionScreen.SuppressSpectatorCyclingThisFrame()`
  every frame as a guard.
- The command panel's Gauntlet layer keeps the cursor visible for the whole mission
  (`InputRestrictions.SetInputRestrictions` with mouse input; check how `StagedSiegeMissionView` and
  `BattleActionBarMissionView` set theirs, and remember #970: the BattleActionBar layer got no clicks
  because the cursor was hidden). Mouse clicks on the panel go to the panel; the camera reads the
  keys and the wheel and the right button.
- Optional, phase 2: a left click on the ground selects the nearest lane.
- Escape opens the vanilla escape menu (keep `CreateMissionSingleplayerEscapeMenu` in the view list).

Control model: With `NoTeamAI` no formation AI ticks on either side (`Formation.cs:2458`). Both sides
keep `IsAIControlled` true, which only makes a Move within 1 m of the standing order count as the
same order (`MovementOrder.cs:662-667`). So the director issues every movement order for both sides
through `Formation.SetMovementOrder`, and never hands a lane formation to AI control.

## 7. Test plan

- Domain: unit tests per rule, scenario tests for the aggregate, purity gate.
- Config: shipped JSON parses; every troop, hero and culture id exists; prices positive; a
  validation test for every default in 5.
- Binding: every reflection or Harmony target resolves on v1.5.6 (template `StagedSiegeBindingTests`).
- Composition: the module is listed in `FeatureModules`; the client factory is registered.
- Full suite green before each stage reports done.

## 8. In-game checks owed (Mike)

1. Main menu shows "Fords of War"; the three prompts appear; the match loads on the default scene.
2. Towers appear on both sides in 3 lanes; free waves march and fight; gold rises on kills.
3. Melee units ordered to attack a tower reduce its hit points (the open engine question).
4. Gunners and volleys hit attackers (the log's `tower volley item` line gives the javelin's thrust damage N and missile speed; an unarmoured torso hit near launch speed loses about N x (1 + 0.0006 x the gunner's Throwing skill); armour, a leg hit, a non-human body and race damage reductions lower it, a head or neck hit doubles it, and the impact speed scales it by (relative impact speed / launch speed) squared); a tower ahead takes no damage until the outer one falls.
5. Buy a pack, a hero (Sauron), use Hold then Push; the AI defends and attacks.
6. A 30-minute match stays under the agent cap; read `[MissionPerf]` lines.

## 9. Phases

1. Tonight: domain, engine layer, client, data, tests, on a generated layout over an existing scene.
2. After Mike's checks: tuning, a greybox lane scene in the Modding Kit (spec in a separate file).
3. Neutral camps on the field (needs a third team: research the team-count limit first), more
   factions, a themed map.
4. Multiplayer: a separate project (MP-category module, MP data, server assembly).

## Deviations

Stage 1 (domain foundations). No signature from 3.1 to 3.3 changed. Choices the spec left open,
for later stages to rely on:

- `FordsMatchConfig.Default` (static property) holds the section 5 numbers and the easy, normal, hard
  AI profiles. Tests and the config loader's fallback use it.
- `TroopPricing.UnitPrice` returns `float`; `PackPrice(float unitPrice, int packSize)` and
  `PackPrice(int level, bool mounted, float eliteWeight, int packSize)` return `int` (nearest 5, at
  least 5).
- `GoldLedger(FordsEconomyConfig, IEnumerable<int> slots)`: every slot starts at `StartingGold`;
  `Earn` returns the amount actually added. `BountyRules(FordsEconomyConfig)` has `KillBounty(int)`,
  `TowerBountyPerSlot(int slotCount)` (integer division) and the `HeroKillBounty` property.
- `MatchClock(float countdownSeconds)`: `Phase`, `Elapsed` and `Minute` count live time only;
  `Advance(dt)`, `End()`, `CountdownRemaining`. A zero countdown starts the clock Live.
- `LayoutOptions` is a sealed record with a hand-written constructor (the spec's list default needs
  it), same parameter names and defaults as 3.3. `LaneSpread` is the gap between neighbour lanes at
  full bow (three lanes: sides at +/- LaneSpread; four lanes: outer lanes at +/- 1.5 x LaneSpread).
  Tower fractions are sorted descending, so Order 0 is always the largest fraction.
- `LaneLayoutGenerator` ids: `tower_<west|east>_<lane>_<order>`, `keep_<west|east>`, `point_<i>`,
  `camp_<west|east>_<i>` (i = gap between lane i and i+1). Camps carry `TroopId ""`, `Count 0`,
  `Reward 0`; the engine layer fills them from `maps.json` with `with`. Yaw is `atan2(y, x)` radians
  of the owner's march direction. Lane 0 lies on the negative side of the base-to-base axis turned 90
  degrees counter-clockwise.
- `LaneGeometry` (all ground-plane, Z ignored): `PathLength`, `Progress`, `NearestIndex`,
  `NextWaypoint(path, side, position, reachRadius)`, `PointBeforeTower(path, towerPos, attackerSide,
  distance)`, `PointAt(path, arcMetres)`.

Stage 2 (towers, heroes, lanes). No signature from 3.1 to 3.5 changed. Types stage 2 created from 3.5 because
its rules need them (stage 4 must reuse them, not redefine): `TowerHit`, `TowerVolley`, `TargetCandidate`,
`TowerTargets`, `SpawnRequest`, `CommandResult`, `LaneSituation`. Added by stage 2: `TowerHeal(TowerId, Amount)`,
`HeroState {Alive, Dead}`, `HeroEntry`, `LaneCommandKind`. Choices the spec left open:

- `LaneCommand` is one record `LaneCommand(LaneCommandKind Kind, Vec3f Target, string? TowerId)` with value equality
  (so the engine layer can compare with the last applied command) and factories `LaneCommand.None`,
  `LaneCommand.Charge`, `MoveTo(p)`, `HoldAt(p)`, `AttackTower(id)`.
- `TowerChain(MapLayout, FordsTowerConfig)`: `Ids`, `IsTower`, `Slot`, `MaxHitPoints`, `HitPoints`, `IsDestroyed`,
  `IsLocked`, `AliveTowers(owner, lane)` (outermost first), `ApplyDamage(id, amount)` (returns hit points left; zero
  destroys), `Heal(id, amount)` (returns amount added), `OnDestroyed(id)` (true only the first time). An unknown id is
  locked. A keep is open when its owner has no towers at all, or when any lane of the layout has no standing tower of its owner (also a lane that never had one).
- `TowerDamageRules(config, chain).Effective(hit)`: ranged, hero and siege factors multiply (a hero archer gets both);
  rounds to nearest; unknown, destroyed or locked towers take 0.
- `TowerRegen(config, chain)`: `NotifyDamaged(id)`, `Tick(dt)` returns `TowerHeal`s and applies them to the chain.
  The aggregate calls `NotifyDamaged` after every applied hit and mirrors the returned heals onto the entities.
- `TowerFireRules(config, chain).Tick(dt, targets)`: also drops candidates beyond `FireRange`; locked towers still fire.
- `HeroRoster(FordsHeroConfig)`: keyed by (side, heroId). `TryField` (HeroAlreadyFielded, then HeroLimit),
  `OnDied(side, id, now, minute)`, `TryBuyback(side, id, now)` (makes the respawn due now; the aggregate charges gold
  with `BuybackPrice(side, id, minute)` first), `DueRespawns(now, nextRequestId)` (earliest first; a side at its alive
  limit waits for a free slot), `HeroEntry.ToSpawnRequest(requestId)` for the first spawn. Times are match-clock seconds.
- `LaneOrderBook(FordsLaneConfig)`: `Order`, `CooldownLeft(side, lane, now)`, `TrySet(side, lane, order, now)`. Setting
  the standing order again returns Ok and costs no cooldown. Lane validity is the aggregate's check.
- `LaneMarchRules(lanes, towers, layout, chain).Decide(s)`: no units or no `Center` gives `None`; fall back goes to the
  lane waypoint nearest the outermost standing own tower, else the standing keep, else the own base end; an unknown
  lane gives `None` (so does Hold on an unknown lane, with no next enemy tower, or with no path; Hold never uses the live center).

Stage 3 (waves, capture points, camps). No signature from 3.1 to 3.5 changed; `SpawnRequest` already existed from stage 2.
Types stage 3 created from 3.5 because its rules need them (stage 4 reuses them): `CaptureCounts`. Added by stage 3:
`CaptureChange(PointId, NewOwner)`, `CapturePointState(PointId, Owner, Capturer, Progress, Contested, Rising)` (`Rising` came with the re-review fix-up stage),
`CampCleared(CampId, Side?, Slot, Reward)`. Choices the spec left open:

- `WaveScheduler(FordsWaveConfig, IReadOnlyDictionary<MatchSide, FactionCatalog>, int laneCount, Func<MatchSide,int,int> laneOwner)`.
  `laneOwner(side, lane)` is the slot id that owns the lane; free-wave units get it as `OwnerSlot`. `NextRequestId()` is public
  so `HeroRoster.DueRespawns(now, scheduler.NextRequestId)` shares one counter (ids start at 1).
- `TryQueuePack(side, lane, ownerSlot, offer)` returns Ok, UnknownLane, UnknownOffer (pack size 0 or less) or QueueFull. The aggregate
  checks gold and `LaneNotOwned` first and charges only on Ok. `QueuedPacks(side, lane)`. A pack part-way out still holds its queue slot.
- `Tick(dt, minute, liveInLane(side, lane), liveOnSide(side))` returns the `SpawnRequest`s. The live callbacks read the start-of-tick
  counts; units emitted by the same call are added on top. `MaxSpawnsPerTick` counts units, shared by all lanes and sides; the
  starting lane rotates each tick so a tight budget does not starve the later lanes. A split request becomes several
  `SpawnRequest`s with fresh ids.
- The free-wave timer starts full, so the first wave comes on the first tick, then every interval. A non-positive interval turns
  free waves off. A free wave that cannot start while the caps are full is held as one pending wave; further due waves for that
  lane and side are skipped until it is out (no stacking). The tier is the one with the highest `FromMinute` not above the minute;
  none started yet means no wave. A free-wave troop with no matching `UnitOffer` gets role Infantry. Purchased packs go before the
  free wave of the same lane.
- `CapturePointRules(FordsCaptureConfig, FordsEconomyConfig, IReadOnlyList<CapturePointSlot>)`: `Points`, `Get(id)`, `OwnedBy(side)`,
  `Tick(dt, counts)` returns `CaptureChange`s, `CollectIncome(side, dt)` returns whole gold for the side's owned points and carries
  the fraction (the aggregate splits it among the side's slots). One side alone: progress rises by `dt / SecondsToCapture`, whatever
  the unit count. Both present: contested, no change. Owner alone, or nobody: an enemy's unfinished progress drains at the same rate.
  An owned point stays owned until an enemy finishes the capture. Radius is not used by the rules; the engine layer counts units with it.
- `CampRules(MapLayout, Func<MatchSide,int,int> laneOwner)`: `Alive`, `IsCleared`, `NearestLane` (nearest waypoint, ties to the lower
  lane, -1 without lanes), `OnUnitKilled(campId, killerSide)` returns `CampCleared` on the last unit. The clearing side is the side of the
  last kill; its slot is `laneOwner(side, nearestLane)` and the reward is the camp's `Reward`. Unknown killer: cleared, Slot -1, Reward 0.
  A camp with Count 0 never clears (generated camps stay inert until maps.json fills them). Alive starts at `Count`; there is no
  "actually spawned" correction yet.

Stage 4 (match aggregate and AI). No signature from 3.5 changed except the additions listed here. Types stage 4 created:
`CommanderSlot`, `UnitRecord`, `MatchSnapshot` + `LaneSnapshot`, `MatchEffects`, `FormationCommand`, `MatchEvent` + `MatchEventKind`,
`MatchView`, `FordsMatch` (two partial files: rules and view), `AiCommander`, `AiAction` + `AiActionKind`. Choices the spec left open, for the
engine layer and the client to rely on:

- `MatchEffects` has one extra list, `Heals` (`TowerHeal`): the regenerated hit points the engine layer mirrors onto the tower entities.
  `Winner` is set on every tick from the moment a keep falls.
- `MatchSnapshot` members are `init` properties with empty defaults. `LiveBySide` may be left empty: the match sums the lane counts.
  `Lanes` has one entry per (side, lane) that has units; a lane missing from it counts as empty.
- `MatchEvent.Side` is the side the subject belongs to: the owner of a fallen tower or keep, the hero's side, the buyer's side, the new
  owner of a point. `Amount` is the gold involved (tower bounty per slot, pack price, hero price), else 0. A tower bounty is paid when the
  tower falls, not for a keep (a keep ends the match: `KeepDestroyed`, `Winner` = the other side).
- Bought heroes are not in the tick that bought them: `TryBuyHero` queues the spawn and the next `Tick` returns it. Events raised between
  ticks (`PackBought`, `HeroFielded`, `TowerDestroyed`, `HeroDied`, `KeepDestroyed`) are delivered by the next `Tick`, also after the match ended.
- Tick order: clock, passive income, capture, tower regen, spawns (queued heroes, waves, hero respawns), volleys, AI, lane commands.
  The AI's purchases queue and spawn on the following tick. A countdown tick returns empty effects.
- Lane commands: one `FormationCommand` per (side, lane) that has units and whose decision is not `None`, every tick. The engine layer
  dedupes. The next enemy target is the enemy's outermost standing tower in that lane, else its standing keep (so a cleared lane attacks the keep).
- `AdjustTowerDamage` applies the effective damage to the rules mirror and restarts the regen delay. `OnTowerDestroyed` is idempotent by tower
  id and ignores the killer (the split is by side); it works even when the damage already took the mirror to zero. Both ignore unknown ids.
- `TryBuyHero` and `TryBuyPack` check gold before the rules' own refusals (so `HeroLimit` only shows for an affordable hero) and charge only on Ok.
  `TryBuyback` needs the slot that fielded the hero (else `LaneNotOwned`), a free hero slot (else `HeroLimit`), and the gold.
  An unknown slot gets `LaneNotOwned`; `NotLive` comes first. The unknown-slot `GetView` returns no gold, lanes unowned, no offers.
- `OnUnitKilled`: bounty goes to the killer's owner slot only when both units are registered and on different sides. A hero victim pays
  `HeroKillBounty` instead of the per-value bounty. `RegisterUnit`'s request id ties a unit to the charge its request made (the check
  fix stage's failed-spawn records); the kill rules do not use it.
- `MatchView` is one record with nested read records (`LaneView`, `TowerView`, `HeroView`, `UnitOfferView`, `HeroOfferView`, `CapturePointView`).
  Extra members the AI needs: `Slot`, `Side`, `OwnedLanes`, `HeroSlotsFree`, `AssaultThreshold`, per-lane `Owned`, `Threat`.
  `Threat` = enemies the last snapshot put in `TowerTargets` for the lane's outermost standing own tower; with no tower left in the lane, the
  enemy's live count there. `Heroes` and `Keeps` list both sides. Hero offers carry `Fielded`.
- `AiCommander`: first decision after one `DecisionIntervalSeconds`; random is drawn only for the mistake roll (only when `MistakeChance` is
  above 0). Per decision: defend the worst lane where `Threat > OwnLive` with the cheapest spear or infantry pack (every price sort breaks a tie by troop id, then offer id, ordinal, never by deck order); pick the assault lane
  (unthreatened, best (own+1)/(enemy+1), lowest index on a tie); buy back dead own heroes, then field the dearest hero where
  gold > price + `HeroReserveGold`; flip Push/Hold orders (Hold on the assault lane while it holds fewer than `AssaultThreshold` units, Push
  elsewhere and after the threshold; never touches FallBack, never while on cooldown); buy the dearest affordable non-hero pack in the
  assault lane while it is under the threshold. An AI slot with an unknown profile id uses `normal`.
- Camps: `CampRules` is not wired into the aggregate (the spec's 3.5 gives it no entry point and camp spawning is Phase 3).
  `MatchEventKind.CampCleared` exists for it.

Domain fix stage (five verified defects in stage 2 and 4 code). No signature from 3.1 to 3.5 changed except one addition:
`MatchView.HeroView` gained a last member, `RespawnTotal` (the full respawn delay the hero died with), which the AI needs for its
buyback rule. Choices the spec left open:

- Buyback slot: `HeroEntry` has `BuybackPending` and `RespawnTotal` (optional trailing members), `HoldsSlot` and `CanBuyback(now)`.
  `HeroRoster.AliveCount` counts alive heroes plus dead heroes with a paid buyback, so `TryField`, `FordsMatch.TryBuyback`,
  `DueRespawns` and `MatchView.HeroSlotsFree` all see the reserved slot from the moment of purchase. A paid buyback spawns on the next
  `DueRespawns` even when the side is full (its slot is already counted); a natural respawn still waits for a free slot.
- `TryBuyback` returns `HeroNotDead` (no new `CommandResult` value) for a hero already bought back or whose respawn timer has ended.
- Kill credit: `FordsMatch` keeps a `RecentDeathLog` (2048 entries, oldest evicted) of side and owner slot per dead unit, used only to
  find a killer that already died. The killer is looked up before the victim is recorded.
- AI buyback: only a hero with `RespawnLeft` above max(10 s, 25 percent of `RespawnTotal`).
- AI assault latch: per lane, set when the assault lane holds `AssaultThreshold` units (it remembers the enemy's outermost standing
  tower in that lane, empty when only the keep is left). The lane keeps Push until its own count is below 35 percent of
  `AssaultThreshold` (exactly 35 percent keeps pushing) or the remembered tower is destroyed; then the usual Hold-to-rebuild rule applies.
- `FordsMatchConfig.Default` attack range is 20 m, as section 5 now says. The shipped `match.json` (another stage) must carry 20 too.

Stage 5 (config catalog and shipped data). No signature from 3.1 to 3.5 changed. Choices the spec left open, for the engine layer and
the client to rely on:

- Types live in `Main/Features/FordsOfWar/Config/`, namespace `TAOM.Features.FordsOfWar`, all `internal`. There are no separate DTO
  classes: like `StagedSiegeSceneCatalog`, the readers walk a Newtonsoft `JObject` and validate as they go (a DTO bound by
  `ToObject` cannot give one warning per bad entry). Files: `FordsConfigCatalog`, `FordsConfigScope` (shared number rules),
  `FordsMatchConfigReader`, `FordsFactionsReader`, `FordsFactionSet`, `FordsMapsReader`, `MapEntry`, `MapLayoutMode`, `TowerPrefab`,
  `CampDefaults`.
- `FordsConfigCatalog(IPathService, IModLogger)` reads `<ModuleData>/fords_of_war/*.json`, each file once per process on first use of
  its part, and never throws. The module (engine stage) registers it as a singleton. Members: `Match` (a `FordsMatchConfig`),
  `Factions` (file order), `TryGetFaction(id)`, `FactionDisplayName(id)` (the raw `{=KEY}English`, the id when unknown), `Maps` (file
  order, the first is the default), `TryGetMap(sceneId)`. Ids match case-insensitively. A missing or unreadable file gives the default
  `FordsMatchConfig`, no factions, or no maps, with one warning; the menu entry should stay hidden when `Factions` or `Maps` is empty.
- `MapEntry(SceneId, DisplayName, Layout, Options, Tower, Keep, Camp)`: `Layout` is `MapLayoutMode.Generated` or `Tags`, `Options` is
  the spec's `LayoutOptions`, `Tower` and `Keep` are `TowerPrefab(Prefab, Scale)` per `TowerKind` (built-in default `siege_barricade_a`,
  scale 1.0 for a tower and 1.6 for a keep; the 1.6 is a placeholder, not measured), `Camp` is `CampDefaults(TroopId, Count, Reward)`.
- JSON shapes. `match.json`: `countdownSeconds` plus one object per record (`economy`, `waves`, `towers`, `heroes`, `lanes`, `capture`,
  camelCase member names) and `aiProfiles`, which starts from easy, normal and hard and lets a file override one or add a name on top of
  normal. `factions.json`: `factions` keyed by faction id, each with `name` (`{=TAOM_FOW_Faction_<Name>}English`), `culture`, `units`
  (`id`, `troop`, `role`, `packSize`, then `price`, or `level` plus optional `eliteWeight` for `TroopPricing`; cavalry and horse archer
  roles price as mounted), `heroes` (`id`, `price`) and `freeWaves` (`fromMinute`, `units` of `troop` and `count`; sorted by minute on
  load). `maps.json`: a `defaults` block (`options`, `towerPrefab`, `keepPrefab`, `camp`) and an ordered `scenes` array (`scene`, `name`,
  `layout`, and optional `options`, `towerPrefab`, `keepPrefab`, `camp` overrides). The shipped offers carry `level` and
  `eliteWeight` and no `price` (the reader computes it through `TroopPricing`; `price` stays an optional explicit override, and a test pins that
  an offer that states one equals `TroopPricing.PackPrice`), and a test pins that the level and weight equal the troop file and
  `troop_weights.xml` and that the computed prices equal the ones the offers had when they stored them.
- Bad input: a bad number reverts to its default with one warning naming the key; a bad unit, hero, free-wave tier or scene is skipped
  with one warning; a faction with no valid unit offer or no `culture` is skipped; a scene with an unknown `layout` is skipped. Lane
  count is limited to 3 or 4 and each tower fraction to 0.01 through 0.49 (a side's tower cannot pass the middle).
- Shipped data. Four factions: gondor (culture `gondor`), rohan (culture `vlandia`), mordor, isengard, with 9, 9, 10 and 10 unit offers
  and three heroes each (Gondor: Imrahil `lord_1_9`, Boromir `lord_1_75`, Faramir `lord_1_34`; Rohan: Theoden `lord_4_1`, Eomer
  `lord_4_3_1`, Erkenbrand `lord_4_16`; Mordor: Sauron `lord_1_17`, the Witch-King `lord_1_15`, Nazgul `lord_1_48_1`; Isengard: Saruman
  `lord_I1_0`, Lurtz `lord_I4_1`, Ugluk `lord_I1_1`). Mordor and Isengard offer `hill_troll` as the Siege unit (`cave_troll` is not used:
  its weight row in `troop_weights.xml` is commented out as work in progress). Gondor and Rohan have no siege unit. Hero prices (800 to
  2,400) and pack sizes are first guesses for the balance pass.
- No new Custom Battle stub was needed: the Mordor and Gondor heroes and Theoden and Erkenbrand already have stubs in
  `characters/custom_battle_lords.xml`, and Eomer and the three Isengard heroes are defined in `characters/lords.xml`, which is registered
  for `CustomGame`. A shipped-config test checks every hero against those two files.
- Maps: Rohan Plains (`taom_rohan_battle_001_forceatmo`, the default), Pelennor Fields and Mordor Plains I, all `"layout": "generated"`.
  They are open non-siege rows of `custom_battle_scenes.xml` with Plain terrain. Nobody has checked their boundary size or navmesh
  coverage in game, so the first in-game check decides whether the list stays. Map display names reuse the scene rows' existing
  `aom_taom_<scene>_name` keys instead of new `TAOM_FOW_` keys, so the translations already shipped apply.
- Neutral camp default: two `hill_troll` for a reward of 250 (not the `taom_troll_bandit_*` twins: `TrollBanditDataTests.Twins_JoinNoOtherTemplateOrCulture` allows them in three files only). The camp spawn itself stays Phase 3.
- Localization: the four faction names are `TAOM_FOW_Faction_Gondor`, `TAOM_FOW_Faction_Rohan`, `TAOM_FOW_Faction_Mordor`, `TAOM_FOW_Faction_Isengard` in
  `taom_module_strings.xml`, and each of the 12 `std_taom_module_strings_*.xml` files carries a row with the English text as a seed. No
  translation tool ran; the 12 translations are owed to #763. Later stages add their own `TAOM_FOW_` keys the same way.

Stage 6 (adapters, director, mission logic, tower hit patch, session). No signature from 3.1 to 3.5 changed. What this stage added or
decided, for the module, launcher and client stages to rely on:

- Files beyond the section 4 table. Pure helpers in `Main/Features/FordsOfWar/` (all tested without an engine): `FordsMatchPlan` (a
  `FordsMatchSetup` resolved against `FordsConfigCatalog`: config, map entry with the chosen lane count, the two faction catalogs, the AI
  profile id, with `normal` as the fallback profile), `FordsSnapshotBuilder`, `FordsTowerGeometry` (garrison spots, volley origin and the
  ballistic aim), `FordsSpawnPlacement`, `FordsBaseFinder`, `FordsLayoutFitter`, `FordsTagLayoutReader`. In `Main/Adapters/`:
  `FordsAgentRegistry` (shared by both unit adapters), `FordsEngine` (vector conversion, ground height, team and character lookups),
  `FordsUnitSample`. `FordsMatchSetup.cs` is a section 4 row owned by a later stage; it is created here, exactly as specified, because the
  director's plan needs it.
- Patch category. `FordsOfWarModule` does not exist yet, so `Patch131_FordsOfWar` is a const in `FordsOfWarPatchCategory`
  (`FordsOfWarPatchCategory.Name`); the module should declare `new(FordsOfWarPatchCategory.Name, ApplyPhase.FirstMission)`. The registry
  section `## Patch131_FordsOfWar` is already in `docs/reference/harmony-patch-registry.md`, because `PatchCategoryNumberTests` demands one
  for any `[HarmonyPatchCategory]` on a TAOM type. No feature-map row was added.
- Mission logic constructor (the launcher must follow it): `new FordsOfWarMissionLogic(FordsMatchSetup setup, FordsConfigCatalog catalog,
  FordsSession session, IModLogger logger)`, internal, public class. It builds the three adapters itself (they hold the `Mission`, so they are
  not IoC singletons), resolves the plan, starts the director, and installs `FordsTowerHitPatch` with it. The module needs to register
  `FordsSession` and `FordsConfigCatalog` as singletons. A match that cannot start (unknown scene or faction, no teams, no boundary, a keep that
  cannot be built) logs an error and ends the mission through `MissionEnded`.
- Adapter interfaces are `internal`: their signatures carry the internal domain types. NSubstitute cannot fake an internal interface, so
  `Main/TAOM.csproj` gained `InternalsVisibleTo("DynamicProxyGenAssembly2")`. Nothing else in the project file changed.
- Responsibilities that moved. Enemies in range of a tower or capture point and the lane centers are computed by the pure
  `FordsSnapshotBuilder` from `IFordsBattlefieldAdapter.SampleUnits()` instead of by adapter queries. The battlefield adapter has no
  `EndMission`: the winner ends the mission through `MissionLogic.MissionEnded` (the engine polls it), so nothing calls it. It has
  `CompleteDeployment()`, which calls `Mission.OnInitialSpawnCompleted()` once: with no deployment controller in the behavior list that is
  what sets `IsDeploymentFinished`, so agents spawned afterwards get their stats and ammo initialised (vanilla spawn logics do the same).
- Teams and formations. West is `Mission.PlayerTeam`, East is `Mission.PlayerEnemyTeam`. Lane `n` is the formation of `FormationClass` `n`
  on each team, so at most four lanes (`Prepare` refuses more). Spawns use `Mission.GetAgentBuildDataToSpawnTroop` (vanilla's own build-data
  path, with `BasicBattleAgentOrigin`) plus `.Formation(...)` and the team's banner, then `SpawnTroopWithAgentBuildData(data, false, true)`. No agent gets a
  controller type; a test greps the logic, director and both unit adapters for `PlayerTroop` and `AgentControllerType`.
- Director. `Start()`, `Tick(dt)`, `OnAgentRemoved(object, object?, bool killed)`, `AdjustTowerDamage`, `Shutdown()`, `Winner`. Human slot id 1
  (West, owns every lane), AI slot id 2 (East, owns every lane, chosen profile). Spawn requests join a backlog and the director spawns at
  most `MaxSpawnsPerTick` units a tick across it, so a bought hero is never lost to the cap. A request whose troop cannot be built is dropped
  with one warning per troop id. A unit's kill value is its pack price per unit when an offer names the troop, else the level price. The
  match gate (`_gate`) is never held across an adapter call; agent removals go through `DeferredCallbackQueue.RunOrDefer`, and a tower's fall is always enqueued for the next tick (the deep review fix stage).
  The camera starts 25 m behind the West keep and 45 m up, looking at the East keep.
- Layout. A `"layout": "tags"` scene is read through `FordsTagLayoutReader` (ids follow the generator's: `tower_<side>_<lane>_<order>`,
  `keep_<side>`, `point_<n>`, `camp_<n>`; at most four lanes; both keeps and an outer tower per side per lane required; a tower tagged on a lane the scene has no waypoints for is a problem too); an incomplete one
  logs one warning with the first problem and falls back to the generated layout. If a tagged scene already placed a marked entity with a
  `DestructableComponent`, the tower adapter adopts it instead of instantiating the prefab. `FordsLayoutFitter` snaps points to the ground and
  moves a point with no navmesh toward the base-to-base line in eight steps; keeps and lane ends are never dropped (counted as unfixed).
  Base points come from the `walk_area` boundary through the minimum-area bounding rectangle, 10 percent in from each end.
- Gunners and volleys. Gunners are invulnerable (`Agent.SetMortalityState(Invulnerable)`), formation-less, held with
  `SetScriptedPositionAndDirection(..., ConsiderRotation)`, topped up by `AmmoSupplyLogic` and faded out when their tower falls. They are not Fords units: no
  key, no bounty, and a tower's kills pay nobody. Volleys are `Mission.AddCustomMissile` of the crafted javelin `western_javelin_2_t3` (SandBoxCore
  `weapons.xml`, template `Javelin`) at the javelin's own missile speed with 1.5 m spread, aimed by `FordsTowerGeometry.AimDirection` at gravity 9.81 (null, and no missile, when an input is not a finite number).
- Killer credit. `FordsAgentRegistry` keeps the last 256 evicted agents, so a killer that left the mission in the same frame as its victim
  still resolves to a key for the match's own death record.
- No new player-facing text was added by this stage, so no `TAOM_FOW_` key and no translation row.

Stage 7 (module, start flow, launcher, dread gate, console, model check). No signature from 3.1 to 3.5 changed except two small
additions: `FordsMatch.GrantGold(slot, amount)` (the ledger's `Earn`, capped at MaxGold, returns what was added) and
`FordsSession.AddGold(amount)` (grants to the human slot, 0 while inactive), both for `taom.fords_gold`. What this stage decided, for
the client stage and later ones to rely on:

- Patch category. `FordsOfWarPatchCategory` is deleted; the const is now `FordsOfWarModule.PatchCategory`, and `FordsTowerHitPatch` names
  it. The registry section `## Patch131_FordsOfWar` already existed from stage 6; its text now says the module declares the category.
- `FordsOfWarModule` registers as singletons: `FordsConfigCatalog`, `FordsSession`, `IFordsMenuAdapter` (`FordsMenuAdapter`),
  `IFordsOfWarGameManagerFactory` (`NullFordsOfWarGameManagerFactory`), `FordsSetupPrompt`, `FordsMenuEntry`, `FordsMissionLauncher`. The
  three unit and tower adapters are not registered: they hold the `Mission` and the mission logic builds them (stage 6).
- Types beyond the section 4 table. Pure: `FordsChoice` (a prompt row: id plus raw `{=KEY}English` label) and `FordsSetupChoices` (the rows
  of each prompt, `Compose`, and `TryResolve`, which turns optional text into a `FordsMatchSetup` with the defaults West = first faction,
  East = first other faction, profile `normal`, scene = first map; ids match case-insensitively and an unknown one is an error that lists the
  valid ids). `FordsMenuEntry` (installs the option) and `FordsOfWarCommands` (the two console bodies, so they run in tests). Adapter:
  `IFordsMenuAdapter` + `FordsMenuAdapter` in `Main/Adapters/` (option lookup and add, single-choice prompt, notice, `StartNewGame`), the only
  place the start flow touches `Module`, `InitialStateOption`, `MultiSelectionInquiryData` or `InformationManager`.
- Main-menu option. Id `TaomFordsOfWar`, label `{=TAOM_FOW_MenuOption}Fords of War`, order index 100 (the slot the parked shader option used; vanilla's
  Multiplayer is 9997; the sandbox options' indices could not be read from the decompile, so the exact place in the list is for the first look).
  Added once by `OnPhase(MainMenu)` through `MainMenuStep.Run`, guarded by the id. It does not clash with `MainMenuCustomizer`, which touches only
  `StoryModeNewGame` and `SandBoxNewGame`. The option is hidden (an `isHidden` func asked at each draw) while `factions.json` or `maps.json`
  yields nothing, instead of not being added.
- Start flow. Order: your faction, enemy faction (all factions listed, the same one allowed), difficulty (easy, normal, hard first, then any other
  profile), then the map only when `maps.json` lists more than one. Cancelling any prompt ends the flow. `FordsSetupPrompt.Start(setup)` is the
  prompt-free path the console uses: factory, then `MBGameManager.StartNewGame`. Neither entry point throws. A null manager logs one warning
  (from the prompt) and shows `{=TAOM_FOW_NoClient}`. The null factory also logs one INFO line saying why, so a missing client reads in the log twice
  (info from the factory, warning from the prompt); the task asked for both.
- Factory. `IFordsOfWarGameManagerFactory.Create(FordsMatchSetup)` returns `MBGameManager?` and is `internal` (its parameter is an internal record;
  TAOM.Client sees core internals through `InternalsVisibleTo`). Client stage: register the real factory in `TaomClientComposition` with
  `registrator.Register<IFordsOfWarGameManagerFactory, ...>(Reuse.Singleton)` and no `ifAlreadyRegistered`, so it replaces the null default
  (`FordsOfWarModuleTests` pins that a later registration wins). `CustomGameManager.OnLoadFinished` (CustomGameManager.cs:53-57) pushes a
  `CustomBattleState`; `TaomShaderGameManager` calls the base and then opens its mission, and the Fords manager can do the same: call
  `base.OnLoadFinished()`, then `IoC.Resolve<FordsMissionLauncher>().Launch(setup)` (internal, reachable from the client through the friend access).
- `FordsMissionLauncher` lives in `Hooks/` (it names engine types, like `HostReviewMissionLauncher`). `Launch(setup)` returns false with an error
  logged if the scene, a faction or a culture is unknown, and never throws. Pure seams: `BehaviorTypes()` (the exact section 4 list, in order) and
  `CreateBehaviors(setup, west, east)` (one instance per type); a test pins that the two agree. West is `BattleSideEnum.Defender` and the player's
  combatant, East the Attacker; `isPlayerSergeant: false` is the "isPlayerGeneral true" of the section 4 row. Banners are the culture's own as in
  `CustomBattleHelper.GetCustomBattleParties`, with the second side's colors swapped when both sides share a culture. The record is
  `PlayingInCampaignMode = false`, atmosphere from `BannerlordMissions.CreateAtmosphereInfoForMission("spring", 12)` (this mode's own pick; the installed `OpenCustomBattleMission` defaults `timeOfDay` to 6f),
  `DecalAtlasGroup = 2`, `SceneLevels = ""`. Server safety: every type used (`CustomBattleCombatant`, `MissionCombatantsLogic`, `AmmoSupplyLogic`,
  the boundary and border placers, `BannerlordMissions`) compiles from the same core assemblies `HostReviewMissionLauncher` already uses, so nothing
  moved behind the client factory; `ServerLoadGateTests` passes in the full run.
- Dread Aura. `DreadMissionGate.IsEligible(mission)` now calls the new internal overload `IsEligible(mission, Func<Mission,bool> isFordsMission)`
  with `FordsOfWarMission.IsActive`. A Fords mission is eligible with no campaign (the branch comes before the campaign check) unless a network session
  is active. `FordsOfWarMission.IsActive(mission)` is true when `MissionState.Current.CurrentMission` is that mission and `MissionState.Current.MissionName`
  is `MissionName`; the tests build a real `MissionState` by reflection.
- Console. `taom.fords_start` could not use `RunAnywhere`: at the main menu there is no `Game.Current`, so every existing gate answers "Campaign was
  not started." and the command could never run. This stage added `TaomConsole.RunAtMainMenu` and `DevConsoleGuard.CheckMainMenuCheatUsage` (refuses
  while a game is running; reads cheat mode from `NativeConfig.CheatMode`, which is what `Game.CheatMode` reads). In the test host that gate answers
  "Cheat mode is disabled!", not "Campaign was not started.", so `ConsoleCommandBindingTests` (the whole-assembly check that every command returns the
  campaign message with no campaign) gained a one-entry list, `MainMenuCommands = { "fords_start" }`, expecting the cheat message for it. This edits a
  shared test; `docs/features/dev-console.md` now documents the fourth gate and both commands. `taom.fords_gold` uses `RunInMission`.
- Localization. 18 new keys `TAOM_FOW_MenuOption`, `TAOM_FOW_MenuHint`, nine `TAOM_FOW_Prompt_*`, `TAOM_FOW_Difficulty_Easy|Normal|Hard`, `TAOM_FOW_NoContent`, `TAOM_FOW_NoClient` and
  `TAOM_FOW_StartFailed`, registered in `taom_module_strings.xml` and as English seeds in the 12 language files. No translation tool ran; the 12 translations
  are owed to #763. The notice button reuses the engine's `{=oHaWR73d}Ok`.

Model check (section 4.1): TAOM's Custom Battle models DO apply in a game started by a `CustomGameManager` subclass. The path, each step read from the
v1.5.6 decompile or this repo: `CustomGameManager.DoLoadingForGameManager` creates the game with `Game.CreateGame(new CustomGame(), this)`
(CustomGameManager.cs:19); `CustomGame.OnInitialize` makes a `BasicGameStarter`, adds the vanilla Custom Battle models (`InitializeGameModels`,
CustomGame.cs:41 and 59-112), then calls `GameManager.OnGameStart(game, starter)` (CustomGame.cs:43); `MBGameManager.OnGameStart` hands the starter to
every submodule (MBGameManager.cs:159-165), which reaches `SubModule.OnGameStart` (Main/SubModule.cs:899); that calls
`RegisterCustomBattleModels(gameStarterObject)` (SubModule.cs:915). The condition (SubModule.cs:1327-1330) is that the starter is a `BasicGameStarter`
and not a `CampaignGameStarter`; it never looks at the game manager's type, so any manager whose game type is `CustomGame` gets the culture morale,
charge, banner-bearer and damage models. The module step (`FeatureModuleHooks.AddGameStartContent`, SubModule.cs:942, BasicGameStarter branch at
FeatureModuleHooks.cs:96) runs after it for the same starter. Nothing needs changing in `SubModule.cs`. Not checked in game.

Client stage (game manager, commander camera, command panel). No signature from 3.1 to 3.5 or 4.1 changed. Everything is in
`Client/Features/FordsOfWar/` (namespace `TAOM.Client.Features.FordsOfWar`) plus the prefab and the strings. What this stage added or
decided, for the next stages and the in-game check:

- Files beyond the section 6 table. Pure and tested without the game: `FordsCameraMath` with `FordsCameraState` and `FordsCameraInput`
  (readonly structs, because TAOM.Client declares no records), `FordsHudText` (every player-facing line, one method each), and the item
  view models `FordsLaneVM`, `FordsOfferVM`, `FordsHeroOfferVM`, `FordsOrderVM`, `FordsLaneBarVM`, `FordsTowerBarVM`, `FordsFeedLineVM`, `FordsPointVM`
  on a small base `FordsViewModel` (typed property-changed setters). The prefab is `Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml`
  (the module's GUI folder ships from `Main/_Module`, like the staged siege HUD), movie name `FordsCommand`. The panel's view model lives in
  TAOM.Client (ADR-013), not in core as the staged siege one does, because the movie is client-only; the tests reach it through the
  existing `InternalsVisibleTo`.
- Game manager. `FordsOfWarGameManager : CustomGameManager` calls `base.OnLoadFinished()` (which pushes the custom battle state, as
  `TaomShaderGameManager` relies on) and then `FordsMissionLauncher.Launch(setup)`. A launch that returns false or throws logs an error and
  returns to the main menu. The way back is `MBGameManager.EndGame()` (what the vanilla escape menu's "Exit to Main Menu" calls,
  GauntletUI `OnExitToMainMenu`): `MissionState.OnTick` cleans every state when the manager is ending, so the game is destroyed and the
  main menu shows; without it the mission's end pops to the custom battle screen underneath. Two callers use
  `FordsOfWarGameManager.ReturnToMainMenu`: the result panel's button (`FordsCommandView`) and the one tail of `OnLoadFinished` that serves a launch that returned false and one that threw. The two automatic
  exits (25 s after the result, 3 s after a failed start) are core's `FordsMissionExit`, driven by the mission logic's tick (see the
  deep review fix stage 3). A mission ended through `MissionEnded` stays open until `EndMission`; nothing closes it, so the timer and the
  button are the way out. The factory takes `FordsMissionLauncher` and `IModLogger` by constructor injection, so the
  manager needs no `IoC.Resolve`. A failed launch, after the return, shows the setup prompt's start-failed notice (the manager and the
  factory take `IFordsMenuAdapter` for it). Registered with one line in `TaomClientComposition`, replacing core's null default.
- View list (`FordsOfWarMissionViews`). Kept: the singleplayer escape menu (`CreateMissionSingleplayerEscapeMenu(false)`, not the
  campaign's ironman flag), `MissionBoundaryWallView`, `FordsCommanderCameraView`, `FordsCommandView`. The screen fade is a global layer
  (`GauntletCameraFadeView`, a `GlobalLayer`), not a mission view, so it has no entry. The options handler is listed only when the mission
  carries a `MissionOptionsComponent`: `MissionGauntletOptionsUIHandler.OnMissionScreenInitialize` dereferences
  `Mission.GetMissionBehavior<MissionOptionsComponent>()` unguarded, and `FordsMissionLauncher` (stage 7) adds none. So the escape menu's
  Options entry does nothing for now (the menu null-checks the component). To get options in the match, add `new MissionOptionsComponent()`
  to `FordsMissionLauncher.CreateBehaviors` and `BehaviorTypes()` and to the section 4 behavior list and its pinned test; this stage did
  not touch the launcher. Every other vanilla view in the host review list was left out (listed in the class comment), each because it reads
  the main agent or the campaign.
- Shared test edit. `MissionViewRegistrationGateTests.Allowlist` gained `FordsCommanderCameraView` and `FordsCommandView`: the gate
  demands `[DefaultView]` on every TAOM mission view, and these two are built by the mission's `[ViewMethod]` list (the case its own comment
  reserves the allowlist for). A `[DefaultView]` on them would build a second copy in the Fords mission and a copy in every other mission.
  `FordsClientWiringTests` pins that they carry no `[DefaultView]`.
  `ClientSubModuleTests.TaomClientComposition_RegisterServices_RegistersExactlyTheExpectedServices` pins the client's registrations as an exact list, so
  its expected array gained `IFordsOfWarGameManagerFactory` (the test's own comment says each batch adds its rows).
- Camera. `UpdateOverridenCamera` returns true while `FordsSession.IsActive`, and false (the vanilla camera) before the director fills the
  session or after a fault, which latches the view off. Frame: `MissionScreen.UpdateFreeCamera(frame)` (sets the combat camera frame and
  the bearing and elevation other views read), `SetFovVertical(CameraViewAngle, Screen.AspectRatio, 0.1, 12500)`,
  `SceneView.SetCamera`, `Mission.SetCameraFrame(ref frame, 65 / CameraViewAngle, ref listener)`. The frame axes are (side, image up,
  back) with the camera looking along minus the third axis, as `UpdateFreeCamera` reads it; `FordsCameraMathTests` pins the basis as
  orthonormal and right handed. Height is metres over the terrain (`FordsEngine.TerrainAt`, smoothed), 15 to 120; pitch runs 35 degrees
  at 15 m to 70 degrees at 120 m, plus a player tilt of at most 20 degrees, held between 20 and 85 degrees. Pan speed is 10 + 0.9 x height
  metres per second, Shift doubles it, Q and E turn 1.6 rad/s, the wheel scales the height by 0.88 per notch (wheel away zooms in), the
  right-button drag turns 0.005 rad and tilts 0.004 rad per pixel. The drag is read from the pointer position, because the cursor is
  visible all mission. The camera position (not the look point) is kept inside `BoundaryPolygon`: inside is left alone, outside is moved to
  the nearest edge point; fewer than three points means no clamp. All numbers are constants in `FordsCameraMath`, tunable after the first look.
- Panel input. `FordsCommandView` sets `InputRestrictions.SetInputRestrictions(true, InputUsageMask.Mouse)` on its layer before the movie
  loads: the cursor is visible, and mouse buttons and the wheel go to the layer only where a widget takes events (ScreenManager.cs:638-670
  hit-tests the layer). Every widget except a button sets `DoNotAcceptEvents`, so the camera's right drag and wheel work over the panel too
  (a test walks the prefab for it). The result backdrop takes events. Keys are not the layer's (it is not a focus layer): the view reads
  them from the scene layer's input context, like the camera. Hotkeys are off while the result is up.
- Panel behaviour. Lists are updated in place (same item objects, add or drop at the end only), so a button under the cursor survives the
  four-a-second refresh. The refusal line clears after 4 s. The enemy's `PackBought` events are not listed; every other event kind has an
  own-side and an enemy-side line. The orders' cooldown shows on the two blocked buttons (the standing order can be confirmed again at no
  cost). A hero card shows ready, in the field, or fallen with the respawn timer and a buyback button.
- Localization. 50 new keys `TAOM_FOW_Hud_*` (19), `TAOM_FOW_Order_*` (3), `TAOM_FOW_Notice_*` (8), `TAOM_FOW_Result_*` (5) and `TAOM_FOW_Feed_*` (15), all as
  literals in `FordsHudText.cs`, registered in `taom_module_strings.xml` and seeded in English in the 12 language files. No translation
  tool ran; the 12 translations are owed to #763. `FordsHudStringsTests` pins source, English file and seeds against each other, and that
  the prefab has no static text.

### Check fix stage

Fixes A, B and C of the check stage. No signature from 3.1 to 3.5 changed except one addition, `FordsMatch.OnSpawnFailed`. This section
supersedes the older text it names.

- Hold (fix A). `LaneMarchRules` gives Hold one target: the lane point `HoldDistanceBeforeTower` before the next enemy tower, whatever the
  formation's own position. Before, a lane past that point (or with no tower or path) was told to hold at its live center, which drifts with
  every agent, so the director's exact-equality dedupe saw a new command about 60 times a second. A lane short of the point advances to it, a
  lane past it falls back to it. With no next enemy tower, an unknown tower id, or no path for the lane, `Decide` returns `LaneCommand.None`
  for Hold. This supersedes the stage 2 line "hold on an unknown lane holds at the current position" and section 3.4 item 5 (both updated or
  noted here). The director leaves a lane with no command alone: `FordsMatch.IssueLaneCommands` never emits a `None` command, so
  `ApplyCommand` is not called, the formation keeps its last order and `_applied` keeps the last applied command. In play the case is
  unreachable except after the enemy keep has fallen, because a lane whose towers are gone targets the standing keep.
- Hold distance (fix B). `holdDistanceBeforeTower` is 60 m (tower fire range 45 m plus 15 m of formation depth) in
  `FordsMatchConfig.Default`, in the shipped `match.json` and in section 5; it was 35 m, inside the tower's reach. The reader's 0 to 300
  bound admits it. The pinned scenario test `FordsMatchTests.Tick_HoldOrder_HoldsBeforeTheTower` moved from 605 to 580 with it.
- Failed spawns (fix C). The director calls `FordsMatch.OnSpawnFailed(requestId, unspawned)` under `_gate` when `SpawnOne` fails, with the
  units of that request still to spawn (the failing one included). The match keeps a record per charged request (`SpawnCharge`: the
  request, what a hero request charged, the bought offer for a pack, the units not yet registered) and removes it when `RegisterUnit` has
  seen every unit or the request failed, so it does not grow. It is made when the request is created: `TryBuyHero` (purchase price),
  `DueRespawns` (the buyback price remembered by `TryBuyback`, else 0 for a natural respawn), and for a bought pack through the new
  `WaveScheduler.TakePackOffer(requestId)`, which returns the offer once per request id (a split pack has one id per part) and null for a
  free wave. A hero request refunds its charge to the owner slot, drops the entry through the new `HeroRoster.Remove` (the alive slot and
  the "in the field" state go with it) and marks the hero unavailable for that side. A pack request refunds `floor(PackPrice x unspawned /
  PackSize)` (the unspawned count is capped at the units not yet registered) and marks the offer unavailable for that side. A free wave, an
  unknown id and a second call for the same request change nothing. Unavailable offers are filtered out of `UnitOffersOf` and `HeroOffersOf`,
  so `TryBuyPack` and `TryBuyHero` answer `UnknownOffer` and `MatchView` lists neither; `AiCommander` reads only `MatchView.UnitOffers` and
  `MatchView.HeroOffers`, so it never buys one. The mark is keyed by offer id for a pack, so a free wave of the same troop does not trip
  it. Supersedes the stage 4 line "`RegisterUnit`'s request id is not used by the rules". No event is raised for a refund.

Fixes D to I of the check fix stage. No signature from 3.1 to 3.5 changed. This section supersedes the older text it names.

- Gunner ammo (fix D). The adapter's own refill is deleted: `IFordsTowerAdapter.RefillAmmo`, `FordsTowerAdapter.Refill`, the director's
  5 s clock (`AmmoRefillSeconds`, `_ammoClock`, `RefillAmmoEvery`) and its test. It wrote `MissionEquipment.SetAmountOfSlot`, which does
  not reach the native agent (the engine's change is `Agent.SetWeaponAmountInSlot`), and it also refilled thrown weapons. The mission's
  `AmmoSupplyLogic` (listing both sides in the launcher) covers the gunners: every 3 s it tops up each AI-controlled agent of a listed side
  to the slot's maximum minus one, for arrow and bolt slots only (`IsAnyAmmo`), through `SetWeaponAmountInSlot`. The gunners are spawned
  with the default `AgentControllerType.AI` and a team of a listed side. Supersedes the stage 6 line "refilled every 5 s" and the section 4
  tower adapter row.
- Dread aura in a Fords match (fix E). `DreadSourceTracker.HeroAxisId(isCampaignCharacter, campaignHeroId, characterId)` picks the hero-axis
  id: a `CharacterObject` answers with its hero's id (null for a regular troop, as before); any other character (a Custom Battle
  `BasicCharacterObject`, which has no hero object) answers with its own StringId. Fords heroes are NPCCharacter rows keyed by the hero id.
  Outside a campaign `DreadMissionGate` admits only Fords missions, so the fallback cannot reach another battle type. Every Fords Mordor hero
  is already in the config: Sauron `lord_1_17` (the `heroIds` list and the `sauron` race), the Witch-King `lord_1_15` and `lord_1_48_1` (both in
  the `nazgul_nine` set, `NazgulRegistry`). No set was edited.
- Photo Mode (fix F). `FordsCommanderCameraView.IsPhotoModeAllowed()` returns false. `MissionScreen.IsPhotoModeAllowed` asks every
  `MissionView`, and the escape menu item reads disabled ("Photo mode isn't allowed.") when one says no.
- Panel layer order (fix G). `FordsCommandView.LayerOrder` is 20, was 100. Evidence from the v1.5.6 decompile: the scene layer is order
  -100; the vanilla mission HUD layers use 1 to 17 (crosshair and objective 1, agent control mode 3, lock visualizer 10, order of battle 13,
  orders and spectator control 14, scoreboard 15, kill feed 17); boundary crossing and mission leave use 47, the options handler 49 and the
  escape menu 50 (a focus layer whose root widget stretches over the whole screen and takes events), and `ScreenManager.EarlyUpdate` gives the
  mouse to the topmost layer hit. So 20 draws over the HUD and under the menus, and the menu takes the clicks. `FordsCommandVM.Tick(dt,
  enginePaused)` returns at once while paused (the escape menu calls `MBCommon.PauseGameEngine`, `MBCommon.IsPaused` is the flag, and views
  keep ticking), so the notice timer counts no paused frame. The view passes `MBCommon.IsPaused`. (The automatic exits moved out of the
  panel in the deep review fix stage 3; the mission tick they now run on stops while the engine is paused.)
- ADR-002 (fix H). The key table moved to `Client/Features/FordsOfWar/FordsCommandHotkeys.cs` (`Read(IInputContext, FordsCommandVM)`, same keys,
  same Ctrl and Alt rule); `FordsCommandView.cs` is 139 lines, was 162. `FordsEntryPointLineCountTests` pins every file in `Hooks/`, the two
  mission views and the module under 150 lines.
- Options (fix I). `FordsMissionLauncher` lists and builds `MissionOptionsComponent` between `MissionBoundaryPlacer` and
  `FordsOfWarMissionLogic`; section 4's list and `FordsMissionLauncherTests` follow. The component is a `MissionLogic` in
  `TaleWorlds.MountAndBlade` with one event and one method and reads no `Agent.Main`. Supersedes the client stage line that the launcher adds
  none; the view list's guard stays.

### Check fix stage (verifier follow-ups)

Three items the verifiers raised against fixes C and D. No signature from 3.1 to 3.5 changed.

- Hero rows after a dropped offer (fix C follow-up). `FordsHeroOfferVM` no longer takes its hero id in the constructor; `Update` now
  takes it first and `HeroId` is rebound on every refresh. Before the failed-spawn fix the hero offer list never shrank, so an id fixed
  at creation stayed aligned with its row. Once `HeroOffersOf` drops an offer, `Sync` trims the last row and keeps the earlier ones, so a
  row showed one hero and its button acted on another. Pack rows were not affected (`FordsOfferVM` acts by index on the same view). The
  F-keys shift by one after a dropped pack offer; the panel labels follow the keys, so this stays as built.
- Stale spec text (fix C follow-up). The stage 4 `RegisterUnit` line and the stage 2 Hold line in this section were rewritten in place,
  so a reader of them alone no longer gets the old rule.
- Gunner refill leftovers (fix D follow-up). Deleted the private `FordsTowerAdapter.Snapshot()` (its only caller was the removed refill)
  and the engine-binding pins that only the refill used: `MissionWeapon.IsAnyConsumable`, `Amount`, `ModifiedMaxAmount`,
  `MissionEquipment.SetAmountOfSlot`, the `MissionEquipment[EquipmentIndex]` indexer, and `Agent.Equipment` (the last one beyond the
  verifier's list: nothing in the Fords code reads it now, same reason). The pins `FordsTowerHitPatch` and the volley still use stay.

### Deep review fix stage (fix stage 1: domain and director)

Six fixes after the `/deep-review`. No signature from 3.1 to 3.5 changed except the additions named here. This section supersedes the
older text it names (the spec sentences were edited in place too).

- Own-side tower hits (fix 1). The engine does not filter blows by side in single player (`DestructableComponent.OnHit` never reads its
  `BattleSide`, and the barricade prefab ships `BattleSide None`), so gunners shooting across their own tower, defenders shooting at
  attackers packed round it and an inner tower's volley crossing the own outer tower all reached the damage table. `TowerHit` gained a
  last member `MatchSide? AttackerSide = null`; `TowerDamageRules.Effective` returns 0 when it equals the tower's owner. A 0 never reaches
  `TowerChain.ApplyDamage` or `TowerRegen.NotifyDamaged`, which `FordsMatch.AdjustTowerDamage` already skipped for a zero result, so hit
  points and the regen quiet timer are untouched. Null keeps the old behaviour. The director resolves the side from the attacker's
  `UnitRecord.Side` when the agent is registered, else through the new `IFordsBattlefieldAdapter.SideOf(object? agent)` (a gunner has no
  record). `SideOf` maps the agent's `Team` with the new `FordsEngine.SideOf(mission, team)`, the inverse of `TeamOf` (reference equality
  with `Mission.PlayerTeam` and `PlayerEnemyTeam`); `FordsOfWarBindingTests` pins `Agent.Team`. Deviation from the brief: `SideOf`
  takes no lock. The battlefield adapter has none of its own (the lock `TryGetKey` takes is the agent registry's, which guards the
  agent maps), and `SideOf` reads only `Agent.Team` and the mission's two team fields. The lock-order rule the brief wanted holds anyway:
  the director calls `SideOf` outside its `_gate`, as it does `TryGetKey`. The director takes `_gate` twice on a hit (once to read the
  unit record, once for the match) so that the adapter query sits between them.
- Tower fall parked (fix 2). `FordsMissionDirector.OnTowerDestroyed` now calls `DeferredCallbackQueue.Enqueue`, not `RunOrDefer`, so
  `OnTowerDestroyed` on the match, the fallen set and `KillGunners` (which fades agents out) always run at the start of the next
  `Tick`, never inside the native hit callback. `TowerSite` is deleted. The kill site (`OnAgentRemoved`) is unchanged: it does only
  bookkeeping on the director's own collections under `_gate` and makes no engine call, so running it at once on the main thread is
  safe and keeps `RunOrDefer`'s off-thread parking. Consequence for callers: the chain unlocks the tower behind a fallen one on the tick
  after the engine reports the fall, not at the report (a tower brought to zero by damage is already destroyed in the mirror, so that
  path is unchanged). The harmony registry text was corrected to match.
- Lane with no towers (fix 3). `TowerChain.OwnerLostALane` counts every lane of the layout (its `Lanes` plus the lanes of its towers),
  and a lane where the owner has no standing tower, including one that never had any, is lost. The keep of an owner with a tower-less lane
  is therefore open from the start, matching `FordsMatch.NextTargetOf`, which already sends that lane's units at the keep.
- Buyback offer (fix 4). `MatchView.HeroView` gained a last member `CanBuyback`. It is true for a hero of the viewing slot's own side
  that passes every `TryBuyback` rule except gold: the match is live, the slot exists and owns the hero, `HeroEntry.CanBuyback(now)`, and
  the side is under its alive limit. `FordsMatch.CheckBuyback` holds that sequence once; `TryBuyback` calls it and then checks gold, and
  `GetView` calls it per hero, so the flag and the command cannot drift apart. An enemy hero, or any hero in an unknown slot's view, is
  false. `AiCommander` requires it for a buyback, besides its timer rule. The client panel's buyback button follows `CanBuyback` too (fix 19
  of the deep review fix stage 3).
- Unused API (fix 5). `FordsSession.HumanSlot` is deleted, with its two test assertions (the session test keeps checking the camera; the
  director test now checks that the session view is the West slot's). The spec's session sketch was edited. The private `_humanSlot`
  stays: every session command uses it.
- NaN polarity (fix 6). `FordsSnapshotBuilder.BuildCapture` skips a sample unless its distance is `<= radius`, so a position that is
  NaN counts at no point (it used to count at every one). `FordsTowerGeometry.AimDirection` now returns `Vec3f?`: null when the origin, the
  target or the speed is not finite, so no NaN direction can reach the engine. The one caller, `FordsTowerAdapter.Fire`, skips that
  missile with `continue`. The adapter-side finiteness filters (positions read from the engine) are the next stage's job.

### Deep review fix stage (fix stage 2: adapters, launcher, data, warg)

Twelve of the thirteen fixes in this stage landed; fix 7 (volley damage) did not. No signature from 3.1 to 3.5 changed. This section
supersedes the older text it names (the spec sentences were edited in place too).

- Volley damage (fix 7), NOT DONE in that stage; done by the "Deep review fix stage (volley item)" below. `Mission.AddCustomMissile` passes a damage bonus of 0, so a volley arrow hits with its own item's
  thrust damage (`bodkin_arrows_a`: 3). The pinned fix was an existing item with 30 to 60 thrust damage, loaded in a CustomGame and
  readable from XML. No such item exists. The only plain `<Item>` entries with a stated thrust damage that `SandBoxCore/ModuleData/items`
  loads for a CustomGame are: arrows and bolts 0 to 5, `throwing_stone` 10, `stealth_throwing_stone` 15, slings 25 (`sling_wool`), 45
  (`sling_braided`) and 57 (`sling_reinforced`), boulders and shot 100 to 250 (`boulder` 100, `grapeshot_stack` 120, `pot` 200,
  `grapeshot_projectile` 250), and the siege projectiles 2200 to 2500 (`ballista_projectile` 2200, `pot_projectile` 2300,
  `boulder_projectile` 2500). The slings in range are launchers, not ammunition (no flying mesh, the missile would be a flying sling), and the
  boulders are physics missiles that break on impact. Javelins, throwing axes and throwing knives are `CraftedItem` entries, whose damage
  `Crafting` computes from piece weights at load (Javelin: `CalculateThrustBaseDamage(isThrown: true) * 9`), so no XML number exists to
  scan. `Native/ModuleData/mpitems.xml` is loaded for `MultiplayerGame` only (`Native/SubModule.xml`), and the Armory has no thrown item.
  That stage left `FordsTowerAdapter.ArrowItemId` unchanged and listed options for the
  maintainer: (a) a TAOM-authored ammunition item (a bolt with about 45 thrust damage) in a new module XML registered for CustomGame, which is
  an XML change this stage was told not to make; (b) a crafted javelin, with its real damage read once in game and logged. The native damage
  path (a custom missile hitting an agent) stays owed in game either way.
- Engine positions (fix 8). `FordsEngine.FinitePosition(Vec3)` returns the domain point or null for a NaN or an infinity.
  `FordsBattlefieldAdapter.SampleUnits` drops such a sample and `PositionOf` returns null for one, so no rule sees a non-finite position. This
  is the "adapter-side finiteness filter" the fix 6 text left to this stage.
- Navmesh guard (fix 9). `FordsBattlefieldAdapter.Move` asks `Scene.GetNavigationMeshForPosition(in ground) == UIntPtr.Zero`, as
  `FordsSceneAdapter.IsWalkable` does, instead of `WorldPosition.IsValid` (which tests only the 2D position and the scene, and the
  `(Scene, Vec3)` constructor leaves the navmesh unset). The skip and the one-time log stay, and the log text is now true. A target that
  was silently accepted off the navmesh is now skipped, so the layout fitter's navmesh snapping is what keeps lanes moving.
- Attack fallback (fix 10). `FordsTowerAdapter.EntityOf` returns null for a destroyed tower (`DestructableComponent.IsDestroyed`), so
  `AttackTower` returns false for one and the director's `Move` to the tower's slot runs (and, if that Move is skipped, the nearest lane waypoint; see the re-review fix-up stage).
- Prefab flags (fix 11). `SpawnTower` sets `DestroyedByStoneOnly` and `DestroyOnAnyHit` to false on the component whether the entity
  was instantiated or adopted from a tagged scene (both go through the same lines), so `DestructableComponent.OnHit` neither zeroes nor
  forces the damage the hit patch decided.
- Banner (fix 12). `FordsBattlefieldAdapter.Spawn` adds `.Banner(team.Banner)` to the build data, like the gunners.
- Launcher (fix 13). Verified in the v1.5.6 decompile: `BattleMissionStarterLogic.AfterStart` is only `Mission.SetMissionMode(Battle,
  atStart: true)`, and `MissionCombatantsLogic.AfterStart` is the same call. `MissionBoundaryCrossingHandler` ticks only the server's peer
  agents or `Agent.Main` (`MissionBoundaryCrossingHandler.OnMissionTick`), its other work is network-session only, and no other class
  reads it. Both are removed from the order list, `CreateBehaviors`, section 4 and the pinned tests, and the exclusion test now lists them.
  The launcher then had seven behaviors; `FordsTelemetryLogic` made it eight.
- Dead code (fix 14). `MBObjectManager.GetObject<T>` for a non-sealed `T` walks every record whose class is assignable to `T` and returns
  null, never throws (MBObjectManager.cs, v1.5.6), and `CharacterObject` derives from `BasicCharacterObject`, so the second lookup and the
  try/catch never changed a result. `FordsEngine.Character` is now one lookup.
- Troop names (fix 15). `FordsEngine.DisplayName(name)` strips one leading `[...] ` tag; a name without one, with no space after the
  bracket, or with nothing after the tag is unchanged. `FordsBattlefieldAdapter.CharacterName` uses it.
- Data (fix 16). (a) The shipped offers no longer carry `price`; `level` and `eliteWeight` stay and the reader computes the same numbers.
  The reader is unchanged: `price` was already an optional override that wins over `level`. A new test pins the 38 computed prices to the
  values the offers had before. (b) `defaults.layout` is renamed `defaults.options` and the reader's special case is gone, so
  `layout` is only a scene's mode. (c) Decks are reordered so the two dearest offers of a faction fall within F1 to F8, and a test pins it.
  Deviation: the brief named Mordor and Isengard, but Gondor (9 offers) and Rohan (9 offers) had the same defect on their 9th offer
  (`fountain_guard`, `royal_riders`), so all four decks were reordered. The offers pushed to the end are the second tier of a role
  already present: Gondor `guardsmen`; Rohan `veteran_spears`; Mordor `morannon_warriors`, `warg_ravagers`; Isengard `warriors`,
  `warg_enforcers`. F-key positions of other offers shift, and the AI buys by role and price, not by position.
- Warg formation (fix 17). `PrepareOnFirstCall`'s AI branch stores the rider's live formation in the shared blackboard value before it clears
  it. The value is shared by reference: `BehaviorTreeBuilder.CopyInterfaceProperties` copies the tree's `BTBlackboardValue<Formation>` object
  into every node that declares `IBTInFormation`, so `CleanIfEnemyDied` and `OnWargDied` read the stored formation. Verified order:
  `Mission.SpawnAgent` builds the mount (`OnAgentBuild(agent2, null)`) before the rider, and `Agent.Build` sets `Formation` only in the
  rider's own build, so the constructor's `agent.RiderAgent?.Formation` was null for every warg spawned after the first scan. `Agent` is
  sealed: the only test is a source-order pin, and the behaviour is owed in game (see the notes of this stage). A warg that dies without
  raging never reaches `PrepareOnFirstCall`, so `ResetAiControlledAgent` now writes the rider's formation only when the stored one is not null
  (re-review fix-up stage).
- API snapshot (fix 18). See the stage report: `patch-targets.md` is regenerated by `pwsh tools/snapshot_api_surface.ps1`.

### Deep review fix stage (fix stage 3: client panel, automatic exits, text)

Six fixes (19 to 24). No signature from 3.1 to 3.5 changed. This section supersedes the older text it names (the spec sentences were
edited in place too).

- Hero buttons (fix 19). `FordsCommandVM.RefreshHeroes` lights a field button only when the match is Live and `offer.CanAfford && view.HeroSlotsFree > 0`, and a
  buyback button only when the match is Live and `hero.CanBuyback && view.Gold >= hero.BuybackPrice`, so a click the match would answer with `HeroLimit` or
  `HeroNotDead` no longer looks available. The H key (`FieldFirstAffordableHero`) is unchanged: it still picks the first hero the gold
  reaches and shows the match's refusal, because the brief named the buttons only.
- Capture points (fix 20). `MatchView.CapturePoints` now reaches the panel through `FordsPointVM` (one read-only row per point: a colour
  swatch, the holder line, the capture line, a contested mark) and `FordsCommandVM.Points` / `PointsTitle`. Deviation from the brief: the
  row sits in the top-left towers panel under the lane bars, not in the bottom command deck, because the deck is already four rows tall and
  the points are a status readout like the towers. The point number is the list position plus one (the layout's order), not the id suffix.
  The capture line shows when the point has a `Capturer`, in whole percent clamped to 0 to 100, as "taking it" while the progress rises or a
  contest holds it and as "slipping back" while it decays (re-review fix-up stage); the contested mark shows whenever both sides
  stand on the point, with or without a capture running. The title is empty when the layout has no points. The swatch uses the tower bars'
  blue and red (`FordsTowerBarVM.OwnColor` and `EnemyColor` went from private to internal) and a grey constant for neutral. The prefab adds no
  sprite or brush it did not use (`BlankWhiteSquare_9`, `Tooltip.Text`); a test pins that. Seven new keys, `TAOM_FOW_Hud_PointContested`,
  `PointEnemy`, `PointEnemyTaking`, `PointNeutral`, `PointOwn`, `PointYouTaking` and `PointsTitle`, are registered in
  `taom_module_strings.xml` and seeded in English in the 12 language files; the 12 translations are owed to #763. No AI steering was added.
- Load failures (fix 21). `FordsSetupPrompt.ShowStartFailed(menu, logger)` (static, internal, never throws) shows `{=TAOM_FOW_StartFailed}`, and
  `FordsSetupPrompt.StartFailedMessage` is now internal. The client game manager calls it after `ReturnToMainMenu` in `OnLoadFinished` (one
  tail serves a launcher that returned false and one that threw), and `FordsMissionExit` calls it after a failed-start exit. The manager and its
  factory take `IFordsMenuAdapter` for this. The prompt's own `Fault` is unchanged. Owed in game: the notice is a global inquiry layer
  (`GauntletQueryManager`, layer order 19300, read in the Native module build dump, not in the v1.5.6 cache), so it should outlive the game's
  teardown; nobody has seen it appear over the main menu.
- Automatic exits (fix 22). Both exits left the panel and run in core on the mission logic's tick, counted in mission time: `MissionState`
  passes dt 0 while `Paused || MBCommon.IsPaused` (MissionState.cs:150-154), and it keeps ticking behaviors after `MissionEnded` while the state
  is Continuing. New files: `FordsExitClock` (pure; `FordsExit` is None, AfterResult or AfterFailedStart; 25 s and 3 s; asks once; ignores a
  dt that is zero, negative or NaN), `FordsMissionExit` (clock plus `IFordsMenuAdapter.EndGame()` plus the failed-start notice; throw-safe,
  a failure is one warning). `IFordsMenuAdapter` gained `EndGame()` (`MBGameManager.EndGame`, a public static in TaleWorlds.MountAndBlade,
  the same assembly the adapter already uses for `StartNewGame`). The logic's constructor gained a trailing optional `FordsMissionExit? exit =
  null` (production passes none; the tests pass one over a fake menu); the file is 149 lines, one under the ADR-002 limit. The failed-start
  clock starts at the first tick after `AfterStart` failed, so the 3 s run from mission start, not from the engine's first `MissionEnded` poll
  (about 10 s). Removed from the panel: `FordsCommandVM.AutoExitSeconds` and its result timer, `FordsCommandView.WatchForMissionEndedWithoutMatch`
  and its three fields. Kept: the result button (`ExecuteExit`, guarded to fire once) and `FordsOfWarGameManager.ReturnToMainMenu`. The paused-frame
  skip in `FordsCommandVM.Tick` stays, because the refusal-notice timer still needs it (a refusal should be on screen when the escape menu
  closes). The engine claim that a finished mission closes itself after `MissionCloseTimeAfterFinish` was wrong: `Mission.CheckMissionEnd`
  returns at once while the state is Continuing and `MissionEnded` is set (Mission.cs:4844-4853), so nothing closes it. The two sentences that said so
  (this spec and `FordsCommandView`) are corrected. The logic's wiring is tested for the failed-start path through the optional constructor
  argument; the winner path (25 s) is tested at the clock and exit level, not through a live director.
- Lane row id (fix 23). `FordsLaneVM` no longer takes its lane in the constructor; `Update` sets `Lane` from the view, as the hero rows do.
- Text (fix 24). (a) The control model paragraph of section 6.1 now cites `Formation.cs:2458` and `MovementOrder.cs:662-667`, both read in
  the v1.5.6 cache; the old `Team.cs:459-464` claim is gone. (b) `FordsViewModel` says `GauntletView` subscribes to the plain and the typed
  events, and the typed ones skip its by-name reflection read (GauntletView.cs:145-149 and 370-374). (c) The launcher comment and the stage 7
  launcher bullet say spring at noon is this mode's own pick, and name the installed `timeOfDay = 6f` default (BannerlordMissions.cs:109); no claim is made
  about the vanilla season. (d) `DreadMissionGate.IsEligible`'s summary names the Fords match. (e) `DevConsoleGuard` says four gates and that
  the main-menu gate is the inverse of the other three. (f) `docs/features/dread-aura.md` names the Fords exception in the architecture
  tree and the mission-type list, says the hero axis reads the character id outside a campaign, and says the aura cannot rout troops in a Fords
  match because the launcher omits `MissionAgentPanicHandler` (`FordsMissionLauncherTests` pins the omission; the first text named
  `AgentMoraleInteractionLogic`, corrected in the re-review fix-up stage). Deviation: the brief also
  listed "the yell chance" among the ways the aura still works there; no yell setting exists in the aura code, so only the fear voice
  (`fearVoiceChancePerPulse`) and the race-ability frights are named, and the doc says the no-rout claim was not seen in game. (g) The Patch131
  section of `harmony-patch-registry.md` already described `Enqueue` (not `RunOrDefer`) after the domain stage's fix 2; the sentence now also says
  what the next tick's `Drain` does (marks the tower fallen, calls `OnTowerDestroyed`, runs `KillGunners`), which is what
  `FordsMissionDirector.OnTowerDestroyed` queues.

### Deep review fix stage (volley item)

Fix 7 of fix stage 2 (tower volleys carry almost no damage) is done with option (b) of its bullet, and the shipped-config gate the review's
RCA asked for is added. No signature from 3.1 to 3.5 changed. This section supersedes the older text it names (the spec sentences in
section 4, section 5, the volley paragraph of the stage 6 notes and section 8 were edited in place).

- Volley item. The volley fires `western_javelin_2_t3` (Lowland Javelin, tier 3, `culture="Culture.vlandia"`), a `CraftedItem` with
  `crafting_template="Javelin"` at SandBoxCore `ModuleData/items/weapons.xml:6729`. SandBoxCore's `Items` node (`SubModule.xml`) lists `CustomGame`
  and loads the whole items folder; Native's `CraftingPieces` (`crafting_pieces.xml`) and `CraftingTemplates` load for it too, and the
  template is `item_type="Thrown"`. Characters that Custom Game loads carry it: `SandBoxCore/ModuleData/spnpccharacters.xml:17691` (registered for
  `CustomGame`). Why a javelin: `Mission.OnAgentShootMissile` (Mission.cs, v1.5.6) fires a thrown weapon (`CurrentUsageItem.IsRangedWeapon` and
  `IsConsumable`) as the item itself with `damageBonus 0`, which is exactly what `AddCustomMissile` does, so a custom javelin takes the same
  path as a thrown one; the other usage of a javelin (`OneHandedPolearm_JavelinAlternative`) is index 1 and `MissionWeapon` starts at usage 0.
  `FordsTowerAdapter.ArrowItemId` is renamed `VolleyItemId`, and its missing-item warning now says javelin. Tier 3 was picked as a mid-tier
  item; no number for its damage was read from a file (it is computed at load from the piece weights), the adapter logs it instead.
- Speed and aim. The fixed 75 m/s is gone. `FordsTowerAdapter` reads `MissionWeapon.GetModifiedMissileSpeedForCurrentUsage()` of the javelin
  once and passes it as both `baseSpeed` and `speed` to `AddCustomMissile` (so the strike-speed factor is 1, as for a thrown javelin), and
  `FordsTowerGeometry.AimDirection` aims with it. `FordsTowerGeometry.Reach(speed)` (speed squared over gravity, the 45 degree range on level
  ground; 0 for a speed not above zero) is new, with three tests. The adapter constructor gained a third parameter, the configured
  `towers.fireRange` (`FordsOfWarMissionLogic` passes `plan.Config.Towers.FireRange`; the logic file is still 149 lines).
- Logging. At the first volley of a match (the lazy item lookup) the adapter logs one info line, `tower volley item '<id>': thrust damage N,
  missile speed S`, for the in-game check, and one warning naming the reach and the fire range when `Reach(speed)` is below `towers.fireRange`.
  A missing item is one warning and the tower never fires, as before. The gunner owner and `missionObjectToIgnore` (the tower's component)
  are unchanged.
- Tests. `ShippedFordsConfigTests.TowerVolleyItem_IsACraftedJavelinSandBoxCoreLoadsForACustomGame` reads the installed SandBoxCore and Native XML
  (inconclusive when they are absent): the Items node includes `CustomGame`, the id is exactly one `CraftedItem` with the `Javelin` template, and
  that template is `Thrown`. `FordsOfWarBindingTests` pins `MissionWeapon.GetModifiedMissileSpeedForCurrentUsage` and
  `GetModifiedThrustDamageForCurrentUsage` (both `int`).
- Hold line gate (fix 2 of this stage, from the review's RCA). `ShippedFordsConfigTests.ShippedMatch_LaneHoldLineIsOutsideTowerFireRange`
  asserts `lanes.holdDistanceBeforeTower > towers.fireRange` for the shipped match.json and for `FordsMatchConfig.Default`, with a message that a
  lane gathering at its hold line must stand out of tower fire. It passes today (60 against 45). Checked red: with match.json at 40 it fails, and
  with the built-in default at 40 it fails; both were restored.
- Owed in game. The native damage of a custom javelin hitting an agent or a tower component has never been seen; the log line gives the
  thrust damage N, and the check is that an unarmoured torso hit near launch speed costs an attacker about N x (1 + 0.0006 x the gunner's
  Throwing skill); armour, a leg hit, a non-human body and race damage reductions lower it, a head or neck hit doubles it, and the impact
  speed scales it by (relative impact speed / launch speed) squared, so an attacker walking into the volley takes more (a siege engine or a
  tower takes its `TowerHit` factors, not this number). Also owed: that the logged speed reaches the fire range (the warning says so if not), that the
  javelin mesh flies and sticks sensibly, and the arc drop (gravity 9.81 was never measured).

### Re-review fix-up stage

Seven items after the re-review. No signature from 3.1 to 3.5 changed. This section supersedes the older text it
names (the spec sentences were edited in place too).

- Warg rider formation (fix 1). `CleanIfEnemyDied.ResetAiControlledAgent` sets `rider.Formation` only when the formation passed is not null,
  and still calls `DisableScriptedMovement`. A warg that dies without raging never ran `PrepareOnFirstCall`, so the blackboard still held the
  null captured at build time, and `OnWargDied` (and `CleanIfEnemyDied`) wrote it over the rider's formation. A rider who never left his
  formation now keeps it. `WargRageFormationTests` gained a second source pin (the null check comes before the write); `Agent` is sealed, so
  this is a pin on the source and not a run of the behaviour. Owed in game: kill a warg that has not raged and see its rider stay in his lane.
- Capture row (fix 2). `CapturePointState` and `MatchView.CapturePointView` gained a trailing `bool Rising` (true when the last step raised
  the progress). `CapturePointRules.Advance` clears it at the top of every step and sets it where the progress grows, so a contest, a drain
  (nobody there, or the owner standing on its own point) and a finished capture leave it false. `FordsPointVM` shows the existing "taking it"
  line while `Rising` or `Contested` (a contest holds the progress), and a new line otherwise: `TAOM_FOW_Hud_PointYouSlipping` ("Your capture is
  slipping back: N%") or `TAOM_FOW_Hud_PointEnemySlipping` ("The enemy capture is slipping back: N%"). Both keys are registered in
  `taom_module_strings.xml` and seeded in English in the 12 language files; the translations are owed to #763.
- Tag layout (fix 3). `FordsTagLayoutReader.CheckTowers` first rejects a tower whose lane is not below the lane count, naming the tag
  (for example `fow_tower_west_3_0 is on lane 3, but the scene has 3 lane(s) numbered 0..2`). Deviation from the brief's wording: the
  reader has no logger, so "reject" is the reader's existing contract, a problem text that makes the caller log its one warning and fall back
  to the generated layout; the stray tower is not dropped on its own. A lane number too long for an int reads as `int.MaxValue`, so it is
  rejected too, and the message then shows that number instead of the typed digits.
- AI ties (fix 4). Every AI sort that ranks offers by price now has deck-order-free keys after it: the defend pick and the assault pick order
  by price, then `TroopId`, then `OfferId` (ordinal); the dead-hero buyback order and the hero-field pick end in `HeroId` (ordinal). The lane
  sorts already ended in the lane index and the tower sort in the tower order, which is unique per lane. Gondor's guardsmen and bowmen, both
  145, now resolve by troop id whatever the deck order.
- Buy buttons in the countdown (fix 5). `FordsCommandVM` passes `view.Phase == MatchPhase.Live` to the pack buttons (`FordsOfferVM.Update`),
  the hero buttons (field and buyback) and the lane order buttons (`FordsOrderVM.Update`), which share the problem: `CheckLane` answers
  `NotLive` to a pack, a hero and an order alike. The keys (F1 to F8, H, 1 to 4, Z, X, C) are unchanged and still show the match's refusal.
  After the match ends the buttons are dark too, which the match refuses as well.
- Skipped moves (fix 6). `IFordsBattlefieldAdapter.Move` returns bool: false when the adapter skipped the order (target off the navmesh;
  its log-once is unchanged), true when it was given and also when the lane has no formation (another target would not help).
  `FordsMissionDirector.ApplyCommand` records `_applied` only after an order was given. A `MoveTo` or `HoldAt` whose Move returned false goes
  again to the nearest waypoint of that lane's own path (2D distance, `LaneGeometry.NearestIndex`; the path points were checked walkable at
  setup), and the `AttackTower` fallback to a fallen tower's slot does the same. If that second Move is skipped too, nothing is recorded and
  the command is tried again on the next tick, with both Moves, so an unplaceable order costs two navmesh queries per tick until it clears;
  it can happen only for a lane end, which the fitter keeps even off the navmesh, and the lane then keeps its last order, as it did before
  this fix. The test fixture's battlefield fake now answers `Move` with true by default
  (an NSubstitute bool method answers false unset). Types whose shape changed: `CapturePointState` and `CapturePointView` (fix 2, stage 3 types) and
  `IFordsBattlefieldAdapter.Move` (this fix).
- Text (fix 7). (a) The no-rout cause is corrected in `dread-aura.md`, `fords-of-war.md` and the fix 24 (f) sentence above: the launcher leaves
  out `MissionAgentPanicHandler` (read in the v1.5.6 cache: its `OnPreMissionTick` calls `CommonAIComponent.Retreat` for a panicked agent);
  `MissionState.AddDefaultMissionBehaviorsTo` adds only `MissionNetworkComponent` (network sessions), `RecordMissionLogic` (recording),
  `BasicMissionHandler`, `CasualtyHandler` and `AgentCommonAILogic`, so the engine does not add it. `FordsMissionLauncherTests`
  `BehaviorTypes_LeaveOutEverythingTheSpecExcludes` names both `AgentMoraleInteractionLogic` and `MissionAgentPanicHandler` among the absent
  types, so the absence is pinned. `CommonAIComponent` panics a troop only below 0.01 morale (a riderless mount also panics when hit), and the shipped `moraleFloor` of 10 keeps the
  drain above it. The sentence in `dread-aura.md` about a tournament mission lacking `AgentMoraleInteractionLogic` is a separate claim and was
  left alone. (b) The javelin damage sentences (check 4 above, the volley item's owed-in-game bullet, `fords-of-war.md` check 11) now say an
  unarmoured torso hit near launch speed loses about N x (1 + 0.0006 x the gunner's Throwing skill); the convergence review corrected the
  caveat to: armour, a leg hit, a non-human body and race damage reductions lower it, a head or neck hit doubles it
  (`CustomAgentApplyDamageModel`, missile Pierce on a human head or neck), and the impact speed scales it by (relative impact speed / launch
  speed) squared. The 0.0006 Throwing factor was seen in `CustomBattleAgentStatCalculateModel.cs:92`, the rest of the formula was taken from
  the brief and not re-derived. (c) The `FordsMissionExit` summary says `EndGame` is `async void` (`MBGameManager.cs:182`), so only a
  synchronous failure of the call is caught and logged. (d) The "Three callers" sentence in the game manager bullet listed two and now says two:
  the result panel's button (`FordsCommandView.cs:82`) and the `OnLoadFinished` tail (`FordsOfWarGameManager.cs:51`); `FordsMissionExit` calls
  the menu adapter's `EndGame`, not `ReturnToMainMenu`.

## Self-check logging

This change adds, beyond the contract above:

- `Diagnostics/` in the Fords feature: `FordsRunReport`, the per-check verdicts, the evidence classes, the log line format and the report file writer (frame times come from the shared `MissionPerf.FrameStats`).
- Domain counters: `TowerDamageReason`, `KillCredit`, a `TowerDamageRules.Decide` overload, `FordsMatch.Phase` and a return value from `OnUnitKilled`.
- Engine seams in Main: `FordsTelemetryLogic`, `FordsMissionResults`, `FordsFieldProbe`, `FordsVolleyHits` and `WargRageLog`, wired through the director, the launcher, the exit and the menu adapter.
- TAOM.Client: `FordsCameraProbe` and the final pass in `ClientSubModule.OnBeforeInitialModuleScreenSetAsRoot`.
- Output: one `[FordsOfWar][Verdict] check=NN` row per in-game step, a `[FordsOfWar][Summary]` line every 30 s of mission time, and `Logs\fords_report_<log timestamp>_run<n>.json` per run.
- Docs: `FordsVerifyDocTests` requires each "How to verify in game" item to name its `check=NN` row.
