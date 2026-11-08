# Fords of War

## Overview

Fords of War is a campaign-free, single-player mission mode started from the main menu. Two sides,
West (the player) and East (an AI), face each other across 3 lanes. The player starts with no agent: they are a
commander with a free top-down camera and a command panel, spend gold on packs of troops, on heroes and on army upgrades, can take over one soldier with the P key,
give each lane a Push, Hold, Fall back or Seize order, and win by destroying the enemy keep. Each side owns a
chain of towers per lane that shoots back, and the target length of a match is 30 to 40 minutes, with 30 best (Mike, 2026-11-04).

The original build contract, written before the build, with its stage-by-stage Deviations up to 2026-10-31, is
[fords-of-war/build-spec.md](fords-of-war/build-spec.md). It is history: where it differs from this page, the code and this page win. This page is the maintainer's map: what exists,
what the numbers mean, how to check it in game and where to change it. What each match showed, and what changed because of it, is in
[fords-of-war/balance-log.md](fords-of-war/balance-log.md).

Mike, 2026-11-03: balance values, roster picks and test plans are the agent's to decide and report; merge and push still need his word.

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

- **No main agent, until the commander takes one.** A mission has a main agent only when an agent gets
  `AgentControllerType.Player`. The mode never spawns the player troop, so `Agent.Main` stays null until the
  commander takes control of a soldier (below). Everything that needs it all mission (the vanilla order bar,
  `OrderController.GetOrderFormCustomWidth`, the boundary crossing handler) is left out of the mission or never used.
- **Fighting as a soldier** (`FordsControlView`, `FordsPanelStandAside`, `FordsPointerPick`, `FordsAgentControl`, `FordsControlPick`): P takes
  control of the soldier nearest the pointer, within 8% of the screen's height, and a hero in reach wins over a closer soldier. The
  engine's own `Mission.TakeControlOfAgent` does the take, so the engine's controller, crosshair and camera do the
  rest, and the agent status HUD comes into view. While a soldier is controlled the panel hides, reads no keys and
  gives back the mouse, the commander camera stands aside, the lanes keep their last orders, and the match runs at 1x.
  P hands the soldier back to its AI, and its fall hands control back by itself: the engine clears the main agent, and
  `FordsControlView` polls for that because the removal can run off the main thread. The chosen speed comes back with
  the panel. A match that ends hands the soldier back too. The key is read on the screen tick, because a fast forward
  frame ticks the mission many times. The take and the hand-back run on the mission tick, because a view tick overlaps
  the agent tick and the Controller setter changes the agent's component list.
  - **Who can be taken** (`FordsAgentControl.Takeable`): a person of the commander's own side who stands in a formation
    and is on foot or on a horse, camel or mule. Left out: tower gunners (no formation), seated howdah and mumak crew (the
    seat teleports them each frame), trolls (their release and blocked clips have never run in a logged battle, see
    [troll-race.md](troll-race.md)), riders of any other creature (a behavior tree drives the creature), and a soldier
    in flight or fading out. Mike asked for "a person", so this first version keeps creatures out. A controlled soldier
    can still walk to a loose warg and mount it. A warg rage could turn vanilla's controller off, so `FordsAgentControl.Take` turns it back on; no warg rages in a match since 2026-11-07, so this is now a guard only.
  - **The hand-back** (`FordsAgentControl.Return`): no vanilla code clears the main agent of a living agent, so this is
    the part to watch. The engine's AI logics add new components that start on the Default behavior values, with morale at
    a flat 50 and no recovery, so `Return` sets both from the lane's order. It also stands up a crouching soldier and ends a walk.
  - **The evidence:** `[FordsOfWar][Control]` lines: `pick` for each press of P with the pointer, the reach and what it
    named; `took` with `mode=`; `returned`; `fell`; and `rejoin`, 10 seconds after a return, which says whether the AI moves
    the soldier again. A fault writes a `failed` error line and hands the soldier back.
  - **Not proven yet:** nobody has played this. An unattended run with `--control-check` (see "Unattended runs") is the run
    that proves the take, the hand-back and the fall, and a player owes the feel of the fight and the pointer pick.
    Target lock needs `MissionMode.Battle` or `Stealth`, and `MissionCombatantsLogic.AfterStart` sets Battle, so target
    lock is likely available if the player's lock option is on. The main agent equipment controller is not listed.
    No boundary crossing handler stops a soldier at the map's edge, and where
    `MissionHardBorderPlacer` stands relative to the camera's boundary is not read.
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
- **Director and four adapters.** `FordsMissionDirector` builds the layout, towers and match, then each
  tick samples the field, runs `match.Tick` and applies the effects: spawns within the per-tick budget,
  a lane order only when it differs from the last one applied, tower volleys, regeneration, the winner.
  It reaches the engine through four mission-scoped adapters (they hold the `Mission`, so they are not
  IoC singletons; the mission logic builds them): `FordsBattlefieldAdapter` (spawn, orders, samples),
  `FordsTowerAdapter` (tower entities, gunners, volleys), `FordsSceneAdapter` (bases, ground and
  navmesh snapping, tagged layouts) and `FordsCampAdapter` (neutral camps; the battlefield adapter makes it through `NewCampAdapter`).
- **Patch131.** `Patch131_FordsOfWar` (`FordsOfWarModule.PatchCategory`, `ApplyPhase.FirstMission`) is
  one prefix, `FordsTowerHitPatch`, on `DestructableComponent.OnHit`. It hands the blow to
  `IFordsTowerHitHook` (the director) and writes back the damage the match allows. A component that is
  not a registered Fords tower passes through untouched, and the prefix never throws. A blow with no
  weapon (a kick) is only counted. The engine builds a melee blow's base magnitude from swing or thrust speed and
  item weight, then momentum and skill (not the weapon's damage stat), multiplies it by the entity factor (Cut 0.8,
  Pierce 0.1, a passive attack 0.2) and, for a thrust that hits with the shaft, by 0.15, then truncates to an int, so
  most spear and lance thrusts reach a tower as 0. `OnHit` carries no strike type, so the patch floors any non-ranged
  blow that arrives as 0 from a weapon with thrust damage (`MissionWeapon.GetModifiedThrustDamageForCurrentUsage`,
  which includes the item modifier, not skill) to `towers.thrustFloorFactor` of that damage. It never touches a
  ranged blow, an empty weapon (a kick) or a weapon with no thrust damage (most axes and maces). The floored blow
  then runs the normal table, so a locked tower and a tower's own side still take nothing. Run `20261101-103348`
  (900 s, AI on both sides) floored 129 blows for 1,020 hit points on five towers, labelled none of its 264 melee blows
  RawZero (228 applied, 8 on locked towers, 28 from the tower's own side; run `20261031-225939`, under the older
  labels, had 124 RawZero of 142), and no tower fell. Own-side hits give 0 damage.
- **Launcher and mission logic.** `FordsMissionLauncher` opens the mission with eight behaviors, in
  order: `MissionCombatantsLogic` (`NoTeamAI`, two `CustomBattleCombatant`s, no player agent),
  `AgentHumanAILogic`, `AmmoSupplyLogic` (both sides), `MissionHardBorderPlacer`,
  `MissionBoundaryPlacer`, `MissionOptionsComponent`, `FordsOfWarMissionLogic` and `FordsTelemetryLogic` (last: it feeds the self-check report). Left out on purpose:
  the default spawn logic, `BattleEndLogic`, deployment controllers, the morale and panic logics,
  `AgentVictoryLogic`, `BattleMissionStarterLogic` and `MissionBoundaryCrossingHandler`.
  `FordsOfWarMissionLogic` is a `MissionLogic` under 150 lines; its tick also drives `FordsMissionExit`,
  the automatic return to the main menu, counted in mission time (a paused engine adds nothing).
- **Commander camera.** `FordsCommanderCameraView` (a `MissionView`) overrides `UpdateOverridenCamera`
  and returns true while `FordsSession.IsActive` and no soldier is controlled, so it owns the frame: WASD or arrows pan, Q and E
  rotate, the wheel zooms between 15 m and 120 m, the right mouse button rotates and tilts, Shift doubles
  the pan speed. It keeps the camera inside the boundary polygon and suppresses spectator cycling every
  frame. `IsPhotoModeAllowed()` returns false. While the commander controls a soldier, `UpdateOverridenCamera` answers false and the engine's own camera follows the soldier.
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
unit, hero, free-wave tier, upgrade or scene is skipped with one warning, and a missing file gives the built-in
default (`FordsMatchConfig.Default`), no factions or no maps. The main-menu option hides itself while
factions or maps are empty.

### Config File: `match.json`

Top level: `countdownSeconds` (number, shipped 10: seconds before the match goes live), `timeLimitMinutes` (number, shipped 40; 0 turns it off), `pierceVsMountedFactor` (number, shipped 2; 1 turns it off), then one object
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
| `towers.regenPerSecond` | float | Hit points regained per second | 1 |
| `towers.regenDelaySeconds` | float | Seconds without damage before regeneration starts | 45 |
| `towers.rockEveryVolleys` | int, 0 to 100 | Every this many volleys of a tower, a rock leads the volley; 0 for none | 3 |
| `towers.volleyIntervalSeconds` | float | Seconds between scripted volleys | 4 |
| `towers.volleyMissiles` | int | Missiles per volley: every second one is an arrow, the rest javelins | 3 |
| `towers.fireRange` | float | Metres: the nearest enemy inside it is a target (heroes first). Also the siege radius: the unit count, the entry distance, and the stay distance plus 15 m | 45 |
| `towers.attackRange` | float | Metres, formation center to tower, at which a lane attacks it; under Push an unguarded tower is attacked from the fire range | 20 |
| `towers.thrustFloorFactor` | float | Share (0 to 1) of the weapon's thrust damage that a melee blow the engine zeroed takes; 0 turns the floor off | 0.2 |
| `towers.assaultDamagePerSecond` | float | Damage ordinary blows may do to one tower each second; heroes and siege units pass it; 0 means no limit | 50 |
| `towers.wearStartMinutes` | float | The match minute after which the walls start to weaken | 15 |
| `towers.wearPerMinute` | float | Added each minute after that to a factor on tower damage and on the assault limit; 0 turns it off | 0.25 |
| `heroes.maxAlivePerSide` | int | Heroes alive at once per side | 2 |
| `heroes.respawnBaseSeconds` | float | Respawn delay at minute 0 | 60 |
| `heroes.respawnPerMinuteSeconds` | float | Added to the delay per match minute | 6 |
| `heroes.buybackBaseFraction` | float | Buyback price as a fraction of the hero price at minute 0 | 0.5 |
| `heroes.buybackPerMinuteFraction` | float | Added to that fraction per match minute | 0.05 |
| `heroes.freeArrivalSeconds` | float | How often each side's next hero comes for nothing; 0 turns it off | 300 |
| `heroes.damageFactor` | float | Multiplies the blows of a hero on the field, before armour | 3 |
| `heroes.damageTakenFactor` | float | Multiplies the blows a hero takes, after armour | 0.4 |
| `lanes.orderCooldownSeconds` | float | Cooldown between lane order changes | 20 |
| `lanes.engageRange` | float | Metres: enemies inside it make the lane charge, unless it is sieging (an enemy inside it no longer always makes a sieging lane charge) | 25 |
| `lanes.waypointReachRadius` | float | Metres at which a waypoint counts as reached | 6 |
| `lanes.holdDistanceBeforeTower` | float | Metres of lane before the next enemy tower where a Hold order stands; on a lane over about 428 m the lane midpoint is nearer its own base and is used instead (see Lane rules) | 60 |
| `lanes.leadBand` | float | Metres (1 to 300): the lead group is the units within this distance, on either side, of the lane's front line (its k-th most advanced unit, see Lane rules); a bad value reverts to 25 with one warning | 25 |
| `lanes.assaultThreshold` | int | Live units the AI gathers in its assault lane before it pushes; 70 since 2026-11-01, when a 45-minute AI match at 40 never got enough units to a tower and one at 70 did (runs 20261101-143718 and -152949) | 70 |
| `capture.radius` | float | Metres around a capture point that count units | 12 |
| `capture.secondsToCapture` | float | Seconds for one side alone to take a point | 20 |
| `upgrades[]` | list | The army upgrade tracks. Each has `id`, `name`, `effect` (`MeleeDamage`, `MissileDamage`, `MeleeArmour`, `MissileArmour`, `SiegeDamage` or `Fortify`), `step` (the change per level) and `prices` (gold per level, one entry per level). A missing or empty list gives the shipped defaults | six tracks of three levels at 300, 600 and 1000; step 0.1 for melee damage, missile damage and fortify, 0.08 for the two armours, 0.15 for siegecraft |
| `aiProfiles.<id>.decisionIntervalSeconds` | float | Seconds between AI decisions | easy 12, normal 8, hard 5 |
| `aiProfiles.<id>.incomeMultiplier` | float | Applied to the AI slot's passive income only, not to bounties | easy 0.8, normal 1.0, hard 1.0 |
| `aiProfiles.<id>.mistakeChance` | float | Chance the AI skips a decision | easy 0.25, normal 0.1, hard 0.0 |
| `aiProfiles.<id>.heroReserveGold` | int | Gold the AI keeps beyond the price before it buys a hero, a buyback or a spare pack | easy 600, normal 800, hard 1000 |

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
| `scenes[].towerHalfWidth` | number, 0 to 50 | Default 0. Set only for a tags scene whose towers are real entities with a body: the half width of a tower in metres. Melee contact with a tower then counts within that plus 2.5 m (4 m without it), the tower entity gets a damage component at match start instead of a barricade, the fitter keeps it where it stands, and the map has no ground garrison. Out of range or not a number reverts to 0 with one warning |
| `scenes[].laneFaceIds` | list of ints | Optional, for a tags scene. One navmesh face group id per lane (distinct positive integers): `LaneFaceBars` bars each lane's units and mounts from the other lanes' groups. A bad list, or one that needs more exclusion sets than the engine holds (6), gives the map no lane bars and logs one warning |

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
| `heroGuard` | object | `troop`, `count` (1 to 20) and `role`: the elite troops that take the field with each of the faction's heroes, at no extra price. A shipped guard is one of the faction's own offers, 3 to 6 strong (`EveryFaction_GuardsItsHeroesWithThreeToSixOfItsOwnEliteTroops`) |
| `freeWaves[].fromMinute`, `units[]` | float, list | Tier that applies from that match minute; each unit is `troop` and `count` |
| `feats`, `traits`, `upgradeCost`, `aiBuyCycle` | object, list, object, list | The faction's feats (see Factions), the strength and weakness lines the panel shows, price factors per upgrade track, and the AI's repeating buy order |
| `rotation` | object | Call of the Regions (Gondor): `seconds`, `callFee` and `regions[]`, each with its own `units[]` |
| `creatures` | object | The creature call: `cooldownSeconds` and `units[]`, with the same fields as the offers |
| `tactics` | object | How the faction's lanes meet the enemy (see Lane rules, "Culture tactics"): `march` and `close` name an arrangement (`Line`, `Loose`, `ShieldWall`, `Square`, `Column`, `Skein`), `closeRange` and `chargeRange` are metres from 1 to 100. Every key is optional; a block with a bad value is dropped with one warning |

### Current Values

**Match numbers and the decisions they encode** (from the build spec, section 5 and the Deviations):

| Value | Why |
|---|---|
| Towers 8000, inner 11000, keep 18000 hit points | Set from a human-led assault, the maintainer's target (2026-11-01): a group of 15 units in contact should need about 2 minutes on an outer tower, and units in contact deal about 5 hit points per second each (runs 20261101-103348 and -112404). AI-versus-AI runs do not set it: at 1200 hit points no tower fell either (run 20261101-123312). The inner tower and the keep are tougher than the outer tower |
| Ranged factor 0.15 | Ranged hits are scaled down so archers do not strip a tower (spec section 3.4: ranged x `RangedDamageFactor`) |
| Thrust floor 0.2 | Vanilla multiplies a thrust's base magnitude by 0.1 (Pierce on an object) and, for a shaft hit, by 0.15, then truncates to an int, so most spear and lance thrusts reached the tower as 0. The floor gives a blow that arrives as 0 from a weapon with thrust damage a fifth of that thrust damage (about 8 hit points per blow in run `20261101-103348`), which the maintainer chose on 2026-11-01 |
| Hero factor 4, siege factor 3 | Heroes and the Siege role (the trolls) are the units that hurt towers; the factors multiply, so a hero archer gets both |
| Assault limit 50 a second per tower | Without it a tower's fall time shrank with every body at its walls, so the cheapest pack was the best tower killer: an elven tower fell 32 s after its first blow (balance-log.md, "The tower assault limit"). The limit sets the least time a tower holds against soldiers: 160 s outer, 220 s inner, 360 s keep, until the walls wear |
| Wall wear from minute 15, 0.25 a minute (0.15 until 2026-11-04, when wins still came at about 40 minutes) | Even sides fight mid-lane and rarely reach a tower, so a match between them did not end. Mike set the target at 30 to 40 minutes, 30 best, on 2026-11-04. Rivendell beat Mordor at 39.6 minutes in the first wear match (`20261104-082014`). Balance runs of 15 minutes do not see the wear |
| Hero damage 3 times, damage taken 0.4 times | Heroes made 0 to 2 kills per minute and died fast (balance-log.md, "Heroes and the AI's spare gold"). With the factors Elladan made 20 kills with no death and Mordor's heroes died 4 and 3 times against 11. A first guess: one hero must not decide a lane by itself |
| Tower regen 1/s after 45 s without damage | Regeneration starts only after a quiet period and never above the maximum. At 15 per second after 20 s every lull healed all the damage an assault did (run 20261101-123312 and earlier), so damage now carries over from one assault to the next |
| No visible bowmen, volley of 3 missiles (javelin, arrow, javelin) every 4 s, a rock in place of the first javelin every third volley, fire range 45 m | Scripted volleys at the nearest enemy in range, heroes first. Mike, 2026-11-04: archers aimed at the invulnerable bowmen, which wasted their time, so the tower itself throws |
| Hold distance 60 m, fire range 45 m | A lane gathering at its hold line must stand out of tower fire: 45 m plus 15 m of formation depth. It was 35 m, inside the tower's reach. `ShippedMatch_LaneHoldLineIsOutsideTowerFireRange` pins the relation. The hold point is never closer than 60 m to the tower: on a lane under about 428 m it is exactly 60 m short of the tower, on a longer lane it is the midpoint, which is farther |
| Attack range 20 m | Measured from the formation center to the tower; 6 m was too small for a center |
| 2 heroes alive per side, respawn 60 s + 6 s per minute | Heroes decide fights, so the count is capped and a death costs time that grows with the match; buyback is 0.5 + 0.05 per minute of the price |
| Starting gold 600, income 120 per minute, bounty fraction 0.35 | Starting values; kills pay the owner of the killing unit, towers and heroes pay a flat larger bounty |
| Free wave every 40 s, caps 90 per lane, 300 per side, 6 spawns per tick | Measured 2026-10-31 in `20261031-222418` (1800 s, both sides AI): peak live West 241 and East 159 against the per-side cap of 302, peak agents 454 of 2040, frame average 11.2 ms, p95 17.7 ms (checks 7 and 16 PASS) |
| Order cooldown 20 s | Setting the standing order again returns Ok and costs no cooldown |
| Capture 12 m radius, 20 s, 60 gold per minute per point | Starting values |
| Countdown 10 s | Seconds before the match goes live |
| AI: easy 12 s, x0.8, 0.25, 600; normal 8 s, x1.0, 0.1, 800; hard 5 s, x1.0, 0.0, 1000 | Difficulty comes from decision speed, passive income, skipped decisions and hero reserve; the multiplier never touches bounties |

**Layout options** (shipped default): `laneCount` 3, `laneSpread` 45, `waypointStep` 12, `towerFractions`
0.36 and 0.2, `keepInset` 0.04.

**Maps:** only `taom_fords_01_forceatmo` (`"layout": "tags"`, `towerHalfWidth` 5.5, `laneFaceIds` 1, 2 and 3, named by the key
`TAOM_FOW_Map_Fords01`, "[Fords] Fords of War"). Mike, 2026-11-05: remove all maps but Fords of War. With one map the menu asks no
map question. The four generated maps (Rohan Plains II, Pelennor, Mordor I, Rohan Plains I) are out of `maps.json`; the generated
layout code stays, with its tests, for a map added later (see "Add a map"). On `taom_fords_01_forceatmo` the director also logs two info lines every 30 mission seconds, fed by a 1 Hz look and read by
no verdict: `[Ground] units=<n> aboveTerrain=min/med/max belowNav=min/med/max offNavMoves=<n>` (how high units stand over the
terrain and under the navmesh) and one `[Lane] faces <stream> own= shared= foreign= top= off= foot= mounted=` line per stream
(unit-seconds by navigation face group: 0 shared, lane n is group n + 1, 4 tower tops). The German to Turkish names of the
new map are English seeds until the Fords translations owed to #763 are run. Tower and keep prefab `siege_barricade_a`, scale 1.0 and 1.6 (the 1.6
is a placeholder, not measured). Neutral camp default: 2 `fow_hill_troll` for a reward of 250.

**Factions** (offers / heroes with price):

| Faction | Culture | Offers | Heroes |
|---|---|---|---|
| `gondor` | `gondor` | 2 always on (militia spears and archers), plus 12 regional bands of 4 | Imrahil `lord_1_9` 1000, Boromir `lord_1_75` 900, Faramir `lord_1_34` 800 |
| `rohan` | `vlandia` | 8 | Theoden `lord_4_1` 1000, Eomer `lord_4_3_1` 900, Erkenbrand `lord_4_16` 800 |
| `mordor` | `mordor` | 8, plus 4 creatures (warg raiders, goblin spider riders, hill troll, cave troll) | Sauron `lord_1_17` 2400, the Witch-King `lord_1_15` 1600, Nazgul `lord_1_48_1` 1000 |
| `isengard` | `isengard` | 8, with `hill_troll` as the Siege unit | Saruman `lord_I1_0` 2000, Lurtz `lord_I4_1` 1000, Ugluk `lord_I1_1` 900 |
| `rivendell` | `rivendell` | 5 | `lord_R1_1` 2400, `lord_R2_1` 1800, `lord_R1_3` 1000 |
| `erebor` | `erebor` | 8 | `lord_E1_1` 2000, `lord_E1_2` 1200, `lord_E1_5` 900 |
| `harad` | `aserai` | 8, plus 2 beasts (war elephant, mumak) | `lord_A9_u` 1400, `lord_3_3_1` 1000, `lord_A9_l` 800 |
| `gundabad` | `gundabad` | 8 | `lord_G1_1` 1400, `lord_G1_2` 1000, `lord_G1_3` 800 |
| `dale` | `sturgia` | 8 | `lord_2_7_1` 1400, `lord_S9_u` 1000, `lord_S9_l` 800 |
| `dolguldur` | `dolguldur` | 12 in three rows (7 uruk packs, Khamul's shadow guard and veiled knights, warg raiders, spider riders, one goblin swarm), plus 1 creature (fell wargs) | `lord_D1_2` 1400, `lord_D1_4` 1000, `lord_D1_5` 800 |
| `easterlings` | `khuzait` | 8 | `lord_K8_u` 1400, `lord_K9_l` 1000, `lord_6_25` 800 |
| `umbar` | `umbar` | 8 | `lord_U1_1` 1400, `lord_U2_1` 1000, `lord_U3_1` 800 |
| `mirkwood` | `mirkwood` | 5 | `lord_M1_1` 2200, `lord_M1_11` 1600, `lord_M1_3` 900 |

Only Isengard sells a Siege unit; Mordor's trolls come through its creature call. Most factions have free-wave tiers at minutes 0, 10 and 20;
Isengard's step up every 5 minutes (0, 5, 10, 15 and 20) and Gundabad has one tier. Hero
prices and pack sizes are first guesses for the balance pass. A faction with more than eight offers gets a third row of four (F9 to F12); Dol Guldur is the first (Mike, 2026-11-05).

## Lane rules

`LaneMarchRules` decides each lane's command every tick, for both sides, from the lane's **lead group** rather than the mean
of every unit. Fresh waves spawn at the base into the same formation, so the mean falls behind the front after every spawn.

- **Lead group.** A lane's front line is the advance (arc position along the lane path, counted from the unit's own base) of its
  k-th most advanced unit, where n is the number of readable positions and k = min(n, max(3, ceil(n / 10))), so one or two runaways
  cannot drag the lane past a standing tower. The lead group is every unit whose advance lies within `lanes.leadBand` (25 m) of that
  front line, on either side. `LaneSituation.Lead` is their mean position, `LeadArc` their mean arc and `LeadCount` their number.
  `LaneMarchRules.LeadOf` computes it for `FordsMatch.SituationOf` (`FordsMatch.Lanes.cs`); the snapshot builder only passes each
  unit's position. A snapshot without positions falls back to the centre. `LanePath` turns each lane's polyline once, when the match
  is built, into per-segment float arrays (start, direction, length, squared length, arc at start) and an arc per vertex; the arc
  position, the lead group, the waypoint index and the hold point read them and give the same numbers as `LaneGeometry.ArcPosition`.
- **What reads the lead.** The `towers.attackRange` test, the level-or-past test below, the `lanes.engageRange` test (nearest
  enemy, measured from the lead; a faction's `tactics` replace it with their charge and close ranges, see "Culture tactics") and
  the march waypoint. A lane whose lead is within 20 m of its next open enemy tower attacks it
  even when stragglers trail.
- **Tower in reach.** The next enemy tower counts as in reach when the lead is within `towers.attackRange` of it, or when the lead's
  arc is level with or past the tower's arc (West: lead arc at or above the tower's; East: at or below). A lead carried past a
  standing tower, by a Charge for example, therefore attacks it once it stops charging (a charging lane of a faction with tactics
  keeps charging until no enemy is within its close range; the close step stands aside there), instead of marching on. There
  is no march cap. A locked tower, a Hold order and a lane without a path never attack. Check 9's `bypass` part does not read the
  rules' lead: at each 1 Hz look the field probe counts, for a stream whose applied command is a MoveTo and that has a next standing
  enemy tower (the outermost standing tower of the other side in its lane, else that side's keep), the units whose arc along the
  lane is more than 10 m past that tower (West: unit arc at or above the tower's plus 10; East: at or below its minus 10). It FAILs
  when more than half of a marching stream's units are past the tower on two looks in a row.
- **Fall back, then fight at the tower.** A lane on Fall back walks to the waypoint nearest its outermost standing tower (the
  keep once none stands). When the nearest enemy is within charge range and stands within that tower's fire range, the lane
  charges instead; the decision trace shows `branch=Charge`. The enemy's own position is the test, not the lead's, and when the enemy's
  position is unknown the lead's stands in. A lane already charging keeps fighting until the enemy is `SiegeStayMargin` (15 m) beyond
  the fire range, so an enemy stepping across the line does not flip the command. Anywhere else a Fall back lane keeps walking back,
  even with an enemy next to it. A keep may have no gunner, so the keep case is the same test, not a claim that it shoots.
- **Forward-only march.** A waypoint counts as reached when `LeadArc >= arc(waypoint) - lanes.waypointReachRadius` (East
  mirrors it). Per side and lane the rules keep the waypoint index and never step it backward while the lane marches. The
  index resets on every branch other than March (Fall back, Siege, Charge, Close, Seize, an `AttackTower` command, Hold) and on an
  empty lane.
- **Siege.** Entry: under Push, the lead is within `towers.fireRange` of the lane's next enemy tower, and the units of the side's lanes that target that tower and stand within fire range of it number at least 1.5 times the tower owner's units there (defenders count from every lane, so a neighbouring lane's wave walking past does not start a siege). Exit: the defenders there outnumber those units, or the lead is more than 15 m beyond the fire range; the lane then needs the full 1.5 to 1 again, and entry is tighter than the exit line so a lead hovering at it does not flip the order. A siege outranks Charge and, for an unguarded tower, also March between 20 and 45 m. It clears when the tower falls or the target changes, and the player's lanes follow the same rule as the AI's. What units do: units already fighting keep fighting unless they are more than about 30 m from the tower (the engine favours a unit's current behaviour); the others strike the tower, and a unit walking to it does not stop for an enemy until it is within about 8 m. The lane's archers stop shooting at units; those carrying a melee weapon walk in and strike the tower. These follow from the engine's AI scoring and still need a game run to confirm. The lane state log line shows `near=<attackers>/<defenders> siege=<true|false>`.
- **Bounded Hold.** A held lane forms up `lanes.holdDistanceBeforeTower` (60 m) of lane before the enemy tower, or at the lane
  midpoint when that is nearer its own base, and at the midpoint when the next target is a keep. The point is snapped to the path
  vertex on the holder's own side, so a hold target is always a vertex of the navmesh-fitted path. With towers at 0.36 and 0.64 of
  the lane, the tower stands 0.14 of the lane beyond the midpoint, so the midpoint applies only to lanes over about 428 m
  (0.14 x 428 = 60); a shorter lane holds 60 m before the tower. Either way the hold stays 60 m or more from the tower, outside its
  45 m fire range on any realistic lane.
- **Decision trace.** `FordsMatch.LastDecisions` holds one `LaneDecision` per lane and side each tick (empty on a countdown or
  ended tick). Its `ToLogBody()` renders `order= live= branch= center=(x,y) lead=(x,y) leadArc= leadCount= enemy= tower=
  towerDist= centerTowerDist= locked= wp= target=(x,y) near=<attackers>/<defenders> siege=<true|false>` in invariant culture with one decimal and `-` for an absent or non-finite
  value (`towerDist` is from the lead, `centerTowerDist` from the centre); on an `AttackTower` decision `target=` is the tower id.
  A faction with tactics adds `arr=` (the arrangement the decision asks for). The director appends `cmd=` (the kind of the command it last sent the engine) and `sent=` (that command's target: a point, an
  AttackTower's tower id, or `-`), in one format for every line. The rules' own request is already in the body (`branch=`,
  `target=`), so a refused move that fell back to the nearest waypoint, or an AttackTower that became a Move to where the tower
  stood, reads as `branch=AttackTower ... cmd=MoveTo sent=(x,y)`. The line is a `[FordsOfWar][LaneState]` line, at most once per
  5 mission seconds per side and lane; a stream with no decision yet writes none.

### Culture tactics

Mike, 2026-11-07: Isengard and the dwarves should "advance as a formation, get close and then attack". A faction's `tactics` block
(`LaneTactic`, read by `FordsTacticsReader`) changes the engage step of the lane rules into two steps, for both the player's lanes
and the AI's:

- **Charge** when the nearest enemy is within `chargeRange` of the lead (the match's `lanes.engageRange`, 25 m, when the block
  names none). A lane that charges keeps charging until the enemy is beyond its close range, so a melee does not drop back into the
  close arrangement each time the nearest enemy falls.
- **Close** (`LaneBranch.Close`) when the nearest enemy is within `closeRange` but outside `chargeRange`. The lane takes its `close`
  arrangement and moves to where that enemy stands, so it meets the enemy as one formation. It keeps that target until the enemy is
  more than 5 m from it (`LaneMarchRules.CloseRetargetDistance`, or the charge range when that is smaller), so the order changes only
  when the enemy has moved, and a lead that reaches the target is within charge range. The target is ground the enemy walked on, so
  the navmesh check accepts it. A lane under Hold keeps its hold point and a lane under Seize its objective; both only form up. The
  lane does not close when its lead is within `towers.attackRange` of the next enemy tower, or when the lead or the target is level
  with or past that tower along the lane: the tower comes first, and a Move past it is what check 9 fails. The siege and fall back steps come
  before the close step; Seize and the tower attack come after it.
- **Arrangement.** The `march` arrangement applies on every other branch, a charge included; a block that names only `close` uses
  Line there, so a wall never outlives its close phase. A faction with no block, or no arrangement at all, sends no arrangement
  order and keeps the engine's default, Line (`Formation.ResetAux`, `Formation.cs` line 2885, run from `Reset`).
- **Engine call.** `FordsMissionDirector.ApplyArrangement` sends `Formation.SetArrangementOrder` through
  `IFordsBattlefieldAdapter.SetArrangement`, before the movement order and only when the arrangement changed.
- **What a shield wall does in the engine.** While the formation keeps its shape it moves at about 0.3 of its units' average top
  speed (Line 0.8, Loose and Skein 0.9; `ArrangementOrder` constructor, applied only while the units hold their places in the formation, `Formation.cs`
  line 1484), so a charge is not slowed. The front rank holds shields to the front, the flank files to the side and the inner ranks
  up (`ArrangementOrder.GetShieldDirectionOfUnit`). While a formation has space between its units, about every 0.5 s the engine
  moves shield bearers into the front rank, or units that brace when cavalry charges from the front
  (`LineFormation.SwitchFrontUnitTypesToFrontRows`). A shield wall has no space between its units, so that sort stops once the wall
  forms: the wall keeps the order the march left, and a unit that joins it is not moved forward. A wall also switches its units to
  the engine's defensive move behaviour, which sets ranged attack to a weight of 0 (`HumanAIComponent.RefreshBehaviorValues`). Under
  any Move order the normal move behaviour already gives ranged attack only 0.02 to 0.04 (Default and Charge: 2 to 5), so whether
  archers shoot less in a wall is native and unmeasured. Entering or leaving a wall also sets each unit's defensiveness, whose effect
  is native and unread. Because a wall walks at about 0.3 of top speed, the shipped walls form only when the lead is within 20 m.

| Faction | March | Close | Close range | Charge range | Intent |
|---|---|---|---|---|---|
| Isengard | Line | ShieldWall | 20 | 6 | Uruk-hai close in a wall and strike at contact |
| Erebor | Line | ShieldWall | 20 | 5 | The dwarf phalanx |
| Gondor | Line | ShieldWall | 20 | 8 | The tower guard's wall |
| Easterlings | Line | ShieldWall | 20 | 8 | Iron legions |
| Rivendell | Loose | Loose | 50 | 10 | Advance open, strike late |
| Mirkwood | Loose | Loose | 55 | 8 | Archers advance open, strike late |
| Dale | Line | Line | 45 | 10 | Bowmen keep the line |
| Rohan | Skein | none | none | 40 | Riders march in a wedge and charge from 40 m |
| Mordor, Gundabad | Loose | none | none | 35 | The swarm charges early |
| Harad, Dol Guldur, Umbar | none | none | none | 25 | No tactic yet |

## Factions

Each faction plays differently, in four ways: which troops it offers, a skill factor on its copies, its feats, and sometimes a mechanic. Everything
is data in `factions.json` and `fords_troops.xml`. The numbers below are first values, not measured balance.

| Faction | Strength | Weakness | Feats | Mechanic |
|---|---|---|---|---|
| Rohan | The best and cheapest cavalry: six of eight offers are riders at 0.65 of the full price for their level (the horse archers 0.5, `eliteWeight`; the foot 0.7), the two lowest-level rider packs hold 8, every free wave brings 4 riders, and 7 of the AI's 10 pack buys are riders | Weak foot and archers (skills x0.7); riders hurt towers little | `mountedDamageToTowers` 0.35, `towerFireRate` 1.3 (Sentries) | None yet |
| Gondor | Every regional specialist; strong towers | Only two offers are always there | `towerHitPoints` 1.4 (Stone) | Call of the Regions |
| Mordor | Most bodies: huge cheap orc packs and a larger army | Orcs very weak (skills x0.75); elites cost a lot | `unitCapBonus` 100, `waveInterval` 0.75, the largest free waves (25 or more a wave) | Creature call |
| Isengard | Heavy Uruk-hai; tower killers; waves that grow every 5 minutes | Fewer troops; only warg riders for cavalry | `damageToTowers` 1.15, `unitCapBonus` -50 | None yet |
| Rivendell | Holds at range, then strikes with a few elites and all three heroes | 150 troops and only five offers: a horde floods it | `towerRange` 1.5 (Far Sight), `heroSlotBonus` 1, `unitCapBonus` -150, `waveInterval` 2 | None yet |
| Erebor | Survives ten minutes, then outspends everyone; the heaviest armour | Slow free waves, few bows, war rams for its only cavalry | `income` 1.4 (Mountain Gold), `towerRegen` 4 (Stonewrights), `waveInterval` 1.25 | None yet |
| Harad | Saves gold for one great beast at a time, then guards it; poisoned arrows | Weaker towers; a fallen beast is a heavy loss of gold | `towerHitPoints` 0.85, `poisonPerSecond` 3, `poisonSeconds` 8 | Creature call on a 120 s timer: a war elephant (315 gold) or a mumak (1000 gold). `beast:<offer>` entries in its `aiBuyCycle` make the AI save for them |
| Gundabad | Ends the match early with cheap packs and constant waves | One tier of free waves that never improves; the weakest towers | `waveInterval` 0.5, `towerHitPoints` 0.7 | None yet |
| Dale | Breaks towers from range with four kinds of archers | Weak melee (skills x0.75), few riders | `rangedDamageToTowers` 4 (Black Arrows) | None yet |
| Dol Guldur | Walks under slowed towers; an uruk army in three rows, with Khamul's cavalry and spider riders to buy and a goblin swarm pack; free waves of 12, uruks with two goblins; calls fell wargs; venomed arrows; its three cheapest uruk packs and the swarm cost 0.65 of the full price, its other packs 0.75 to 1.2 | Few strong soldiers: its best come in packs of three | `enemyTowerFireRate` 0.75 (The Shadow), no income penalty, `poisonPerSecond` 2, `poisonSeconds` 6 | Creature call on a 45 s timer |
| Easterlings | Answers the enemy fastest with a mixed army of horse archers and iron legions | Weaker tower fire | `orderCooldown` 0.5 (Discipline), `towerFireRate` 0.85 | None yet |
| Umbar | Gets rich from the map: raids camps and holds capture points | Less income; no troop above level 31 | `campReward` 3, `captureIncome` 2 (Plunder), `bounty` 1.25, `income` 0.8 | None yet |
| Mirkwood | Holds at range with the finest archers (skills x1.2) | 150 troops and only five offers; elk riders are its only cavalry | `towerRange` 1.5 (Far Sight), `unitCapBonus` -150, `waveInterval` 1.5 | None yet |

- **Feats** (`FactionFeats`): `towerFireRate` (0 keeps towers silent), `towerRange` (every rule that reads a tower's fire range uses it, and a lane on Hold stands back by the extra reach), `towerHitPoints`, `damageToTowers`, `mountedDamageToTowers` (0 means no damage at all, reason
  `FactionFeat`), `waveInterval`, `unitCapBonus` (the lane cap rises by this over the lane count), `income`, `bounty`. Each factor is relative to 1. A new feat is one
  property, one line in the reader and one use in a rule. `towerRegen` scales how fast towers mend after the quiet delay, and `heroSlotBonus` adds to the side's limit of heroes alive. `rangedDamageToTowers` scales the side's missiles, on top of the shared `rangedDamageFactor`. `enemyTowerFireRate` scales the volleys of the other side's towers. `orderCooldown` scales the time before a lane takes a new order. `campReward` and `captureIncome` scale the gold from camps and capture points.
- **Poison** (`poisonPerSecond`, `poisonSeconds`, `PoisonRules`): a faction with these feats poisons a soldier struck by its arrows or bolts. Poison takes hit points through any armour, so it answers heavy elites. Another hit refreshes the time and a stronger poison replaces a weaker one, so doses never stack. A shield block, a tower gunner's arrow and a mount carry no poison. A lethal dose kills with the archer as the killer, while the archer lives. Harad has 3 a second for 8 s and Dol Guldur 2 a second for 6 s. The `[FordsOfWar][Balance] poison` line gives the doses and the damage per side.
- **Upgrades** (`match.json` `upgrades`, `UpgradeBook`, `FordsUpgradeHooks`, key U hides the panel, Shift+F1 to F6 or a click buys): gold buys three levels on each of six tracks for the whole army: melee damage, missile
  damage, melee armour, missile armour, siegecraft and fortify. The damage and armour tracks work per blow in `TaomCustomBattleDamageModel`, so a level reaches the units already on the field with no stat refresh. Tower gunners gain nothing from the damage tracks.
  A faction's `upgradeCost` scales its prices. An `aiBuyCycle` entry `up:<track>` makes the AI save for that upgrade. Proof in a run: `[FordsOfWar][Upgrade]` lines, one per purchase and one with the blow totals at the end.
- **Match speed** (`FordsGameSpeed`, `FordsMissionSpeed`, key L or the button in the top left corner): 1x, 2x, 4x and 10x, for a human and for an unattended run (`--speed`). 2x and 4x set the mission's `FixedDeltaTimeMode` each frame to the frame's own time times the speed, capped at 0.1 s, so a slow frame slows the match. 10x is the engine's fast forward, `Mission.SetFastForwardingFromUI`, which runs the AI on the main thread in steps of up to 0.1 s. `Scene.TimeSpeed` cannot do this: `Mission.UpdateSceneTimeSpeed` caps it at 1 every frame. A `[FordsOfWar][Speed]` line records each change, and `tools/fords_match_review.py` names a fast match first. Balance runs use 4x, and 10x is for smoke and crash runs only, because six matches showed that 10x changes the result: see [balance-log.md](fords-of-war/balance-log.md).
- **Hero guards** (`heroGuard`, `FordsMatch.AddGuard`, Mike 2026-11-05): every time a hero takes the field (bought, free, respawned or bought back) its faction's guard is queued in the hero's lane for the hero's owner (`WaveScheduler.QueueGuard`), and marches with that lane. The guard goes out before the lane's packs but only within the lane and side caps, so it waits when they are full and takes no pack queue place. It costs nothing and refunds nothing when its spawn fails. It does not follow the hero away from the lane: the engine gives each lane one formation. Proof in a run: one `[FordsOfWar][Hero] guard side=... lane=... troop=... count=...` line per guard request, beside the `fielded` and `respawned` hero marks (the spawn counts by troop cannot show it, because a guard is also one of the faction's offers).
- **Free heroes** (`heroes.freeArrivalSeconds`, `FordsMatch.HeroArrivals.cs`): every 5 minutes of live time each side's next hero takes the field for nothing, cheapest first, in the lane where the side is most outnumbered. A side with no slot free or no hero left waits for the next arrival, and a missed one does not carry over. A commander can still pay to field a hero early. The hero panel's title counts down. Proof in a run: `[FordsOfWar][Hero] kind=fielded` lines at about 300 and 600 s for both sides.
- **Hero strength** (`heroes.damageFactor`, `heroes.damageTakenFactor`, `HeroCombatFactors`, `FordsUpgradeHooks`): a hero of the match deals 3 times the damage and takes 0.4 times the damage, per blow in `TaomCustomBattleDamageModel`. Blows on towers keep their own `towers.heroDamageFactor`. Proof in a run: `heroRaised` and `heroLowered` on the `[FordsOfWar][Upgrade]` totals line, and the hero table of `tools/fords_match_review.py`.
- **Tower assault limit** (`towers.assaultDamagePerSecond`, `TowerAssaultLimit`): a tower takes at most 50 points a second from ordinary blows, and a blow past that is cut or counted as `Crowded`. Each tower holds at most one second's worth. Heroes and siege units pass the limit and spend none of it. So until the walls wear (see Wall wear), an outer tower lasts at least 160 s, an inner one 220 s and a keep 360 s against soldiers alone, and more bodies past a small assault add nothing. After minute 15 the limit rises with the wear: at minute 30 it is 162.5 points a second, and an outer tower of 8000 hit points can fall in about 49 s. Without it the cheapest pack was the best tower killer in the game: see [balance-log.md](fords-of-war/balance-log.md).
- **Lost lanes** (`TowerChain.LaneLost`, `WaveScheduler.Tick`): a lane whose towers have all fallen starts no new free wave for its owner, though a bought pack still comes. A free wave that already started still spawns its remaining units, and the lane's timer keeps running. The reason: in a long match attackers stopped to fight each free wave as it spawned at the base, 170 to 226 m short of an open keep, and a siege needs the lead within 45 m. Erebor's keep took 80 damage in 18 minutes before this rule and fell 320 s after its last lane cleared with it (see "Lost lanes" in [balance-log.md](fords-of-war/balance-log.md)).
- **Pierce against cavalry** (`pierceVsMountedFactor`, `MountedPierceRule`, `FordsPierceHooks`, Mike 2026-11-07): a horse, or a soldier on a mount, takes twice the damage of a pierce blow: a spear or pike thrust, an arrow, a crossbow bolt, a javelin, a tower's arrow, a couched lance. A blow a shield blocks keeps its shield damage, because the engine runs the damage model on a block too and the rule is for the soldier and the horse. A shield bash, a kick, a hit with the arm instead of the weapon, fall damage and a horse charge do not count: the engine makes them blunt whatever the blow's damage type says (`TaomAgentApplyDamageModel.BluntByVanillaRule`). The doubled damage also feeds the engine's own reactions to a blow: shrugging it off, a dismount and a horse rearing. It counts in Fords of War only: every other Custom Battle returns before the rule, the campaign's damage model never calls it, and the mission's teardown (`FordsMissionDirector.Shutdown`) sets the factor back to 1. Between the result and the teardown, about 25 seconds, every Fords factor stays set. Proof in a run: `mountedPierce=` on the `[FordsOfWar][Upgrade]` line at the match's end.
- **Scripted blows** (`FordsScriptedBlows`, Mike 2026-11-07): a warg or spider bite, an elephant's or a mumak's attack, a dwarf's head-butt or drop-kick, a troll's brute force and a signature strike's splash write their damage into the blow (`CustomAttacksUtils.TakeDamage`), which the engine registers without its damage model. In a Fords match that damage goes through the model first, so the army upgrades, the hero factors (Sauron's splash deals his 3 times) and, for a pierce blow, the pierce rule reach it; armour still does not. A shield that only reduces a scripted blow does not stop the pierce rule, because the rest lands on the soldier. TAOM's damage rules read the blow's empty weapon as blunt, so the pierce rule takes the blow's own type there. Outside a match the damage is written unchanged.
- **Time limit** (`timeLimitMinutes`, `TimeVictory`): even sides lose only outer towers and clear no lane, so a match between them did not end. At minute 40 the side with more towers standing wins, then the side with the larger share of its hit points left, keep included. A share, because a faction's keep can start stronger. An exact tie goes to West. The feed warns 5 minutes before, and the result panel says time ran out. A `[FordsOfWar][Time]` line records the score.
- **Wall wear** (`towers.wearStartMinutes`, `towers.wearPerMinute`, `TowerWear`): after minute 15 every blow on a tower or keep does more, and the assault limit rises with it: 3.5 times at minute 25, 4.75 times at minute 30 and 8.5 times at minute 45. Locks still hold. Even sides fight mid-lane and rarely reach a tower, so without this a match between them would not end. The feed says so once, and a `[FordsOfWar][Wear]` line records it. Balance runs read the first 15 minutes, so they do not see it. Each measure is in [balance-log.md](fords-of-war/balance-log.md).
- **AI under threat** (`AiCommander.Defend`, `BuyNextInCycle`): a threatened lane gets the buy cycle's next entry, and the AI saves for it, so a side that falls behind keeps its elites and upgrades. A faction with no cycle buys the cheapest spear or infantry pack.
- **AI spare gold** (`AiCommander.BuySparePack`): while its assault lane is full, gold above `heroReserveGold` buys the cycle's next pack for the lane with the most room, and the cycle moves on, so the faction's upgrades and elites keep their share. With every lane full the gold waits for a hero. Spending it on upgrades alone favoured a horde, whose army gains the most from a flat price: see [balance-log.md](fords-of-war/balance-log.md).
- **AI buying** (`aiBuyCycle`, `AiCommander`): a repeating list the AI buys in, so a faction plays to its identity. An entry is an offer id, or a role name for the dearest offer of
  that role the gold reaches, which suits Gondor's rotating bands. `up:<track>` names an upgrade and `beast:<offer>` a creature. The AI saves up when the next entry costs too much. It calls a creature only from this list, and a gate makes every creature faction name one, because an automatic call on the timer once took most of Mordor's gold. An upgrade or beast still comes while the assault lane is full, so a small faction's gold does not sit unspent. Mordor buys orc packs most of the time; Rohan keeps some foot to take towers.
- **AI regroup** (`AiCommander.FallBackRatio` 1.6, `ReturnRatio` 1.1, `RegroupMinEnemy` 10): the AI compares the value of the units within 40 m (`FordsMatch.PressureRadius`) of a lane's lead, not the lane's headcount, because a lane's count includes units still at their base and ignores quality (a 40-unit elven lane is not weak against 100 units). Units of any lane count on both sides, since the units physically there are what matter. A hero counts as `HeroPressureValue` (250, about one strong elite) instead of its price of 800 to 2400, so one hero neither forces a regroup nor blocks one; the unit record's own value is unchanged. A lane outside the assault, and not on Seize, goes to Fall back when it has value near its lead, at least 10 enemy units are there, and their value is more than 1.6 times its own. The AI sends the lane back to Push in three cases: it is in contact (at least 10 enemy units near, as for the Fall back) and their value is at most 1.1 times its own; its lead is within fire range of the lane's own anchor tower and no enemy is within 40 m; or nobody is left in the lane, so its next wave does not spawn into a retreat. A lane that outran the enemy but is not home yet stays on Fall back, and so does a lane between the two ratios. A few pursuers are not contact: on a retreat the lead is the rear guard, so the first riders to catch up would otherwise send the lane back to Push away from its tower. The AI releases only a Fall back it ordered itself: it forgets a lane on any visit where the order is not Fall back, so a person's later Fall back is never released. Every order waits for the lane's cooldown, a lane on Fall back is never picked as the assault lane, and when no lane can be the assault and no lane is under threat, the AI still buys the next entry of its buy cycle, in cycle order, for the lane with the most room; with every lane full it buys only a due upgrade or beast. Each order logs `[FordsOfWar][Ai] regroup side=<West|East> lane=<n> own=<value> enemy=<value> n=<enemy count>`, and `regroup end` with the same fields when it releases.
- **Traits** (`factions.json` `traits`): the lines that tell the player a faction's strengths and weaknesses. The panel shows them during the countdown.
- **Call of the Regions** (`rotation`, `RegionDeck`): a band of four offers from one region fills buy slots F3 to F6 for `seconds` (30), then the next region's band
  takes the same slots. Slots never move. The deck is shuffled: each region comes once per cycle, and a new cycle never starts with the region just shown. The panel
  names the next region. `callFee` (150 gold) shows a chosen region at once and restarts the timer (key R opens the list). The region already showing costs nothing.
  The AI buys from whatever is showing and does not pay the fee.
- **Creature call** (`creatures`, `FordsMatch.TryCallCreature`): the commander picks a creature (keys G, B, N, M) and pays for it, which queues it in the selected lane like
  a bought pack and starts one shared timer of `cooldownSeconds` (Mordor 30, Dol Guldur 45, Harad 120). Mordor's list is warg riders, goblin spider riders, a hill troll and an armored cave troll, Dol Guldur's fell wargs, Harad's a war elephant and a mumak. The spider
  is the ridden one: a free-roaming spider only works as a campaign bandit today (`creature-bandits.md`). The AI calls a creature only from a `beast:` entry in its `aiBuyCycle`.

## The roster

`fords_of_war/fords_troops.xml` holds the mode's own troops: one `fow_<campaign id>` copy of each troop `factions.json` and `maps.json` name. A balance edit there can never
reach a campaign, because `SubModule.xml` loads the file for CustomGame only. A script made the copies on 2026-11-02, so the file is now the source and nothing regenerates it.
That script took each faction's offers from low or high campaign troops, multiplied skills by faction (Rohan riders up, Rohan foot and Mordor orcs down, Uruks and Numenoreans
up), and took the spear off the King's Riders so they fight with sword and shield.

- **Tune here:** `<skills>` and `<Equipments>` in `fords_troops.xml`; `packSize`, `price`, `level` and `eliteWeight` in `factions.json`. A test
  holds an offer's `level` equal to the copy's `level`. `eliteWeight` no longer follows the campaign's `troop_weights.xml`.
- **What a copy keeps:** troll, warg and race mechanics read the agent's race and monster, not its troop id (checked across `Main/` on
  2026-11-02), so a copy keeps its abilities. `TroopWeightService` only reads party rosters, so the mode never used `troop_weights.xml`.
- **What differs:** `is_obsolete="true"` keeps a copy out of the Custom Battle picker (`ArmyCompositionGroupVM` lists `IsSoldier && !IsObsolete`,
  and Custom Battle loads CustomGame data too). No copy has upgrade targets, because nothing levels up in a match.
- **Names:** a copy keeps its campaign troop's `{=key}`, so the translation follows. A new name needs a new key.
- **Heroes** are not copied: `factions.json` still names the campaign's lords.
- **Gates:** `ShippedFordsRosterTests`. `validate_moduledata.py` checks the copies' item references, because it scans every ModuleData XML file.

## The command panel

`FordsCommand.xml` follows the research in `docs/features/fords-of-war/hud-research.md`. The middle of the screen stays clear and state sits at the edges.

| Zone | Holds | Why |
|---|---|---|
| Top strip, centre | Faction name, both keeps' bars with hit points, the clock | Slim and always visible; a bar reads faster than a number |
| Battle board, bottom left | One row per lane that also selects it: tower bars from West's inner to East's inner, order, unit counts. Then capture points and camps | One place for the state of the battle; the row scopes the buy grid and the orders |
| Command deck, bottom centre | The faction's mechanic row, gold and income, a fixed 4 by 2 buy grid (F1 to F8), 4 by 3 when a faction has more than eight offers (F9 to F12), 2 by 2 orders (Z X C V) | Slots never move, so key position equals button position |
| Heroes, bottom right | One button per hero: state and what it costs | High value and rarely pressed |
| Upgrades, right, above the heroes | Six upgrade tracks with level and price (Shift+F1 to F6 buys); key U shows or hides it | A purchase for the whole army, hidden when it is not wanted |
| Speed button, top left | The match speed (1x, 2x, 4x or 10x); a click or key L steps to the next | Kept off the top strip, whose height the clock and keep bars set |
| Feed, top right | At most 4 lines, each gone after 12 s, each with its own backing | News must read over the battle and leave |
| Intro, centre | During the countdown: the faction, its traits and the keys | A player learns the faction before the first decision |

Every button has a key letter on its face and a dimmed disabled state; a price turns red when the gold does not reach it; an order on cooldown shows the seconds and a shade that
drains. A faction changes only the accent colour and name, never the layout, so muscle memory carries over (`FordsFactionStyle`).

**Soldier control (key P).** The panel is for a commander with no agent, so while the commander controls a soldier (see "Fighting as a soldier") `FordsPanelStandAside` hides it, stops its key table and gives back the mouse, and the match runs at 1x. The chosen speed and the panel come back with the hand-back. The key hint in the intro lists P (`TAOM_FOW_Hud_KeysHintF12`). Three messages go to the engine's message log, not the feed: which soldier was taken (`TAOM_FOW_Hud_ControlTaken`), that it fell (`TAOM_FOW_Hud_ControlFell`), and that no soldier stood near the pointer (`TAOM_FOW_Hud_ControlNone`).

**A Gauntlet trap:** a child with `StretchToParent` inside a `CoverChildren` parent makes that parent fill the screen. A visibility binding on a widget with its own `DataSource` reads that
data source, so a list's visibility goes on a wrapper.

## Camps and the Seize order

**Camps.** Every `CampSlot` that survives the layout fit becomes a neutral camp when the match starts (`FordsMissionDirector.SpawnCamps`,
after the gunners). `FordsCampAdapter` puts the creatures on a third team with `BattleSideEnum.None`, hostile to West and East by
`Team.SetIsEnemyOf` in both directions, so West and East stay the mission's only two battle sides. Each camp is one formation of that
team (at most eight camps). The default camp is 2 `fow_hill_troll` for a reward of 250 (`maps.json` `defaults.camp`).

- **Staying home.** The creatures stand at the camp's centre under a Move order. When any lane unit of either side comes within
  `CampRules.AlertRange` (20 m) of the centre, `CampRules.UpdateAlerts` issues a `CampOrder` and the formation charges. When no unit is
  left in that range, the formation walks home. The order is sent only when it changes, and each change writes a `[FordsOfWar][Camp]` line.
- **The ring.** `FordsCampAdapter.AddRing` draws `map_circle_decal`, the decal the campaign map draws round a party, at
  `CampRules.Radius` (15 m). A camp whose creatures could not be made gets no ring.
- **The reward.** `FordsMatch.OnCampUnitKilled` counts each creature's death. The last one clears the camp, pays the reward to the slot of
  the killer's side that owns the lane nearest the camp, and raises `MatchEventKind.CampCleared`. A killer the match does not know (a
  creature that fell for another reason) clears the camp and pays nobody. A cleared camp does not come back.
- **A creature that cannot be made** counts as already dead, so a camp missing all of them is cleared at the start and no lane seizes it.

**Seize** is the fourth lane order (key V, `LaneOrder.Seize`), with the same 20 s cooldown as the others. `SeizeRules` holds the logic.

- **What is open.** A camp with creatures left, or a capture point the side does not own. An objective is open to the two lanes nearest
  to it (`LanesPerObjective`), because lane bars keep a lane off its neighbour's ground, so it cannot walk across a lane to a far
  objective. A Seize with nothing open is refused (`CommandResult.NothingToSeize`) and costs no cooldown.
- **What the lane does.** The objective is the open one nearest the lane's centre when the order is given, and it stays until it is held,
  so a lane does not turn round on the way. The lane holds at the objective (`LaneBranch.Seize`, a `HoldAt` command). At a camp it charges
  once one of its units is inside the alert range. Under Seize an enemy of any lane within `lanes.engageRange` (a faction's charge range,
  see "Culture tactics") draws a Charge, because the objective lies between lanes; within a faction's close range the lane takes its close
  arrangement and keeps its objective. It does not break off to attack a tower and never sieges, because a siege needs Push.
- **Back to Push.** When the side owns the point, or the camp is cleared by anyone, `IssueLaneCommands` returns the lane to Push by
  `LaneOrderBook.Release`. The cooldown of the Seize order keeps running. Any other order given by hand drops the objective.
- **The AI uses the same order** from the same view (`MatchView.LaneView.CanSeize`). One lane at a time seizes: a lane outside the assault,
  with no enemy at its tower (`Threat` 0) and at least `AiCommander.SeizeMinUnits` (12) units. It leaves a seizing lane alone unless the
  enemy outnumbers that lane at its tower; then it orders Push.
- **Decision trace.** A seizing lane's `[LaneState]` line ends with `seize=<objective id>`.

## The hand-built map

`taom_fords_01_forceatmo` in the live `TAOM_Map` is the first map built for this mode (`"layout": "tags"`). It is unversioned: a fix there
reverts on reinstall. The `forceatmo` part of the name makes Patch16 keep the scene's own atmosphere; on any other name the mission uses
the mode's noon fallback.

- **Real towers.** With `towerHalfWidth` above 0 the mode builds no prefab: `TowerSlot.Authored` marks the tower as the scene's own, and
  `FordsTowerComponent : DestructableComponent` is attached to the tagged entity at runtime. A unit counts as in contact at half width plus
  2.5 m. On a fall the engine hides the root, which hides the children, and turns physics off with the children; the fall line in the log
  reads `IsVisibleIncludeParents`, because each child's own flag stays true.
- **The missile owner.** `fow_gunner_<side>_<lane>_<order>_<n>` tags are spots on a tower top (`TowerSlot.GunnerSpots`), and a volley leaves from a
  point outside the walls above the first one (`VolleyOriginFromSpot`). No bowman shows there. `AddCustomMissile` reads `shooterAgent.Index`, so each
  tower has one hidden owner: the faction's first ranged troop, on that first spot, invulnerable, with `SetVisible(false)`, no formation and
  `AgentControllerType.None`, on a team with `BattleSideEnum.None` that is hostile to nobody. Native target scans reject an agent at the enemy test,
  so no AI aims at it (native 0x68EBC0, 0x68E650). With no AI controller its missiles still do damage, because `Mission.MissileHitCallback` cancels a
  friend's hit only for an AI attacker. That also makes it a friend of its own side, so `FordsUpgradeHooks.Reduce` zeroes those hits (`IsOwnTowerShot`),
  and `FordsMissionDirector.AttackerOf` takes its side from its tower. Its capsule stays, so a stray arrow can still vanish on it.
  Tower shots no longer wear shields, because a friend's missile gives a shield no damage. Do not park the owner far away: a missile's first tick
  sweeps scene objects from its start to its owner. On a generated map it stands 8 m behind the barricade, outside the attackers' swing.
- **The volley.** Every second missile is the arrow `fow_tower_arrow` (`fords_items.xml`, registered for CustomGame only), the others are the
  owner's volley item, so a tower with no owner fires nothing.
- **The rock.** Every `towers.rockEveryVolleys` volleys of a tower, `fow_tower_rock` takes the first javelin's place, with no spread. It is a hurled rock,
  not a lobbed catapult shot: from about 27 m up and within 45 m a true lob takes 5 s or more and misses. The item copies `boulder_projectile` with
  thrust 30 for 2500 and speed 25 for 8. Its `AffectsArea` ring is 1.2 m: full damage inside 1 m, a ninth at its edge; when it lands below 22 m/s the
  full-damage radius shrinks to 0.8 m and the damage halves. A rock that lands on the ground hits a human in the ring unarmoured; after a direct hit the ring uses the struck part's armour. As a Boulder
  in Custom Battle it can knock down a soldier it does not kill. The report counts rocks apart: `Volley.RocksLaunched`, and their hits under
  `Filtered.rock`, so check 11's javelin and arrow hit rate stays like for like. **Not proven in a match:** that the rock shows and sounds right from a
  bare tower, and no native fault on custom skeletons (the fire-pot crash in `blow-diagnostics.md` is unresolved).
- **After the match ends** a blow on a tower or keep does nothing (`TowerDamageReason.MatchEnded`), so no keep falls during the 25 second result
  window and a win on time stays a win on time in the run report.
- **What a tower shot on its own side does.** `FordsUpgradeHooks.Reduce` zeroes it (counted as `ownTowerShots` in the `[Upgrade]` totals line), so
  there is no damage, no knockdown and no shield wear. The hit sound, the hit particle and a stuck arrow remain, because the missile's owner is a
  friend of both sides and the engine cancels a friend's hit only for an AI owner. For the same reason the combat log calls a tower hit on the
  soldier the player controls "friendly fire" when damage reports are on.
- **Lane IDs.** `laneFaceIds` lists one navmesh face group per lane. `LaneFaceBars` bars each lane's units and mounts from the other lanes'
  groups and the tower-top group by `Agent.SetAgentExcludeStateForFaceGroupId`, which leaves room for five sets (the engine's limit is six).
  Cross paths and rings use group 0, which nobody is barred from. Not sealed yet: a run still logs up to 8% of unit time on a foreign group.
- **Housekeeping.** Every 3 s `IFordsBattlefieldAdapter.Housekeep` refills thrown weapons and fades every riderless mount, whatever its kind.
  The engine's `AmmoSupplyLogic` refills arrows and bolts. Neither is a map feature, so both run on every map. A mount fades only after 20 s with no rider and
  only when no soldier holds it or has chosen it (`RiderlessMountRule`), because the engine's fade does not unlink a rider from its mount. The `[Housekeep]` line's
  `mountsSpared` counts, per Monster id, how often that guard held a mount back, and `mountsRemoved` the mounts faded (for example `horse:22,taom_war_elephant:1`). Every kind goes, wargs, spiders, war elephants and mumakil included (Mike, 2026-11-07): soldiers never choose a riderless mount as a target, because the engine takes a mount's side from its rider and a riderless one is nobody's enemy, so only stray blows reached it and a riderless beast stood on the field for the rest of the match. A howdah or mumakil crew stands on a platform that releases its seats when the beast is gone, as on a death: each archer is set down at the beast's position and rejoins the side's Ranged formation, which is lane 1's (outside the unit registry, so no bounty or tally). Whether the crew of a fading mumakil reach the ground needs a game run.
  At the end of a match the `[FordsOfWar][Balance]` lines give each troop's kills, deaths and the gold value of both, and each commander's gold earned, spent and left.

## Layout fitting

A generated layout is fitted to the scene before it is played (`FordsLayoutFitter`, `FordsLayoutPlanner`, run by
`FordsMissionDirector.PlanLayout`).

- **Ring search.** A lane waypoint, lane end, keep, capture point or camp that stands off the navmesh is searched for in rings
  of 2, 4, 8 and 12 m at eight bearings, the two directions across the lane first. A lane waypoint may not cross a
  neighbouring lane's original line, nor come closer to it than half its original gap.
- **Tower slide.** A tower on bad ground slides along the fitted lane up to 30 m, toward its own base first, and keeps its id.
  The logged and judged distance is the arc offset along the lane (10, 20 or 30 m), not the straight line, so a bend in the lane
  cannot shorten it. A tower that finds no spot is dropped.
- **Rejection rule.** One rule, `FordsLayoutFitter.EmptiedGroups`, refuses a layout in two places. If a lane loses every tower of
  one side in the fit, the fit is rejected and the director refuses to start, with one error line naming the tower ids (`the layout
  for <scene> was refused: ...`), because that keep would stand open. If the same happens while the towers are built (every tower
  of one side in a lane failed to spawn), the director refuses after `BuildTowers` with one error line (`the layout for <scene> was
  refused after the towers were built: lane 0 has no tower left for East (tower_east_0_0, tower_east_0_1 could not be built)`) and
  starts nothing. Losing only some towers of a lane is a warning. These are the only cases that stop a match.
- **Bases.** The scene adapter has one `TryGetBasePoints(insetFraction, shortSide, ...)`; the director passes `axis == FordsBaseAxis.Short`. A base off the navmesh is moved along the base-to-base axis (a scan, then a bisection to the edge of the ground,
  then 4 m back in), never toward the polygon centre. The log shows `base side=West raw=(x, y) settled=(x, y) moved=N m`.
- **Segment check.** Each pair of consecutive kept lane points counts as blocked when the engine finds no clear navmesh line
  and no walking path of at most 1.4 times the straight distance plus 10 m.
- **Planner ladder.** The planner tries lane spread x1, x0.7 and x0.5 on the long axis, then the same three on the short axis
  when the boundary's long side is under 1.25 times its short side. An attempt passes with at most 10% of items dropped (lane
  waypoints, towers and capture points; camps are fitted but not counted, because a map plays without them), no tower moved over 30 m
  and at most 5% blocked segments (`FordsLayoutLimits`). Dropped camps show on the attempt line as `camps dropped n/m (not
  counted)`. The score is 1000 when rejected, plus 100 per whole dropped fraction, 10 per far-moved tower and 100 per whole
  bad-segment fraction (named constants in `FordsLayoutPlanner`). The first passing attempt wins.
- **Play the best attempt.** When no attempt passes, the director plays the lowest-scoring one and logs one warning
  (`... playing the best attempt: ...`). Only a rejected fit stops the match. The limits are a judgement call, not measured in
  game.
- **Log lines.** `[FordsOfWar][Layout]` lines: one `attempt` line per planner attempt (spread, axis, dropped, far moves, bad
  segments, pass or fail), one `lane=` line per lane (`waypoints kept moved dropped maxGap badSegments`), one `tower <id> slid
  <m> m` line per slid tower, `dropped towers:` when any fell, the `base` lines, and at match start the report's own
  `[Layout]` line (scene, both bases, waypoints per lane, built and missing tower ids, `keepOpenAtStart` per side). Right after the
  towers are built (also when the start is then refused) one `[FordsOfWar][Layout] timing plan=<ms> ms towers=<ms> ms` line gives
  the cost of planning (generate, fit and retries) and of spawning the tower entities.

**2026-10-31 result** on `taom_rohan_battle_002_forceatmo`: the planner's first attempt passed. 0 of 168 items dropped, 0 bad
segments of 145, 1 point moved, all 14 towers and both keeps built, bases settled without moving. Rohan Plains I, which failed
before the fitter was rewritten (34 path points dropped, 3 of 14 towers lost), has no in-game run since; an offline replay on
its decoded navmesh passed only on the short axis at spread x0.7.

## Unattended runs (autorun)

One match can play itself with nobody at the keyboard, so a build can be judged from its self-check report. It never runs for
players: only the environment variable `TAOM_FORDS_AUTORUN` arms it, there is no file fallback, and an unarmed session costs one
bool and one null check per application tick. The standing runbook (the idle gate, the launch recipe, how to judge a run) is
[unattended-game-runs.md](../reference/unattended-game-runs.md).

- **What arms it.** `TAOM_FORDS_AUTORUN` holds the absolute path of a UTF-8 `key=value` arm file. A set variable pointing at a
  missing file or a file without a usable `out=` logs one `[FordsOfWar][Autorun] not armed: ...` warning and stays off.
- **Arm file keys** (case-insensitive, `#` comments, last duplicate wins; an unknown key or bad number logs a warning and uses
  the default): `west`, `east`, `ai`, `west_ai` (`1` puts an AI commander on the West side too, so both sides play; default 0), `speed` (1, 2, 4 or 10 from the first frame, default 1), `control_check` (a positive mission second at which the scripted soldier control starts, see Control check below; default 0, off), `scene` (optional, resolved like `taom.fords_start`), `live_seconds` (mission seconds
  before the match is ended, default 600), `live_wait_seconds` (wall seconds to go live, default 600), `wall_limit_seconds`
  (wall seconds after the start at which the match is ended whatever the mission clock says, default 2400), `out` (required,
  absolute folder for the status and done files), `quit` (`1` quits the game at the end) and `screenshots` (comma-separated live seconds at which the game saves a picture of the screen, UI included; one more is taken during the countdown). A quoted `out=` value, or one holding
  `<`, `>` or `|`, leaves the plan unarmed with the "needs an absolute out=<folder>" problem. A `live_seconds` at or above
  `wall_limit_seconds` is accepted with a warning (the wall limit then ends the match first). The `armed plan=` line ends with
  `arm=<path of the arm file>`, and the run's setup text and JSON carry `west_ai=0|1`.
- **Control check** (`control_check`, `FordsControlCheck`, launcher `--control-check <seconds>`): no keyboard reaches an unattended run, so from that mission second `FordsControlView` takes the soldier nearest an enemy (`FordsAgentControl.Front`) in three rounds. It holds the first for 30 s and hands it back, rests 15 s, holds the second for up to 180 s (the round ends early when the soldier falls), rests 15 s, then takes a third and holds it for 15 s. All three need about 255 mission seconds after the start. The `[FordsOfWar][Control]` lines (`took`, `returned`, `fell`, `rejoin`) and the `[FordsOfWar][Speed]` lines are the evidence; no verdict row reads them, and the `armed plan=` line does not echo the key.
- **Wiring.** `FordsOfWarModule` registers `FordsAutorunPlan` and `FordsAutorunDriver`, with core's `NullFordsAutorunHost` as the
  host (it never starts a match). `TAOM.Client` replaces the host in `TaomClientComposition` with `FordsAutorunHost` (plan, setup
  prompt, logger), the way it replaces `IFordsOfWarGameManagerFactory`. `ClientSubModule` resolves the plan on the first
  application tick and calls `FordsAutorunStart.Driver`, which resolves the driver only when the plan is armed, so an unarmed
  session never builds `FordsConfigCatalog`. A set `TAOM_FORDS_AUTORUN` that cannot arm the run logs one `[FordsOfWar][Autorun]
  not armed: <why>` warning from there.
- **Phases.** `FordsAutorunDriver` is ticked from `ClientSubModule.OnApplicationTick` with wall time. It waits until the main menu
  has been up for 4 s (hiding any inquiry meanwhile), starts the match through `FordsSetupPrompt.Start` with trigger `autorun`,
  waits for it to go live, and after `live_seconds` of mission time (or the wall limit) with no winner and no end requested it
  ends the match through the result panel's exit path (`FordsOfWarGameManager.ReturnToMainMenu`). The report writes its final=0
  pass at mission end and the final=1 pass on the menu return. The driver then writes `fords_autorun_done.txt` once and quits
  when `quit=1`. It rewrites `fords_autorun_status.txt` every 5 s and logs `[FordsOfWar][Autorun]` phase lines plus a
  `clock wall= mt= live= ratio=` line every 30 s (ratio is mission seconds per wall second).
- **Done file.** Keys `reason`, `run`, `report`, `log`, `live_seconds`, `wall_seconds`, `final_passes`, `final_total`, `started`,
  `ended`. Reasons: `final` (the only success), `start_refused`, `start_failed`, `never_live`, `wall_limit` (the report is still
  collected but the launcher scores it a failure), `final_timeout` (no final=1 pass within 120 s) and `exit_stalled` (an EndGame
  that someone else requested never closed the mission, 120 wall seconds after the driver saw it; no final=1 pass is waited for). A
  reason that follows an earlier outcome keeps both, for example `never_live+final_timeout`. The launcher fails every reason but
  `final`.
- **Launcher.** `python tools/run_fords_autorun.py --live-seconds 120` (also `--scene`, `--west`, `--east`, `--ai`, `--west-ai`,
  `--wall-limit`, `--speed`, `--control-check`, `--screenshots`, `--keep-on-crash`, `--require-idle-minutes`, `--runs`, `--runs-root`, `--dry-run`) writes the arm file, starts `Bannerlord.exe` directly with the module list of
  the F5 profile and the variable set, polls every 5 s, scans the engine logs for a crash and copies the results into
  `<runs-root>/<yyyyMMdd-HHmmss>/` (default `E:/Temp/fords-runs`; `--runs-root` is resolved to an absolute path): `arm.txt`,
  `runner_log.txt`, `runner_status.txt`, `fords_autorun_done.txt`, the newest `taom_debug_*.log`, the `fords_report_*_run1.json` and
  the engine logs (with `--screenshots`, also the pictures as PNG files). It prints the verdict rows. `--wall-limit` and `--live-seconds` must be positive; the arm file always carries the
  runner's wall limit (2400 s by default) and is written with LF. The deployed `TAOM.dll` must contain the autorun: look for
  `[FordsOfWar][Autorun] armed plan=` and `[BuildStamp]` in the new log.
- **Pass rule.** The launcher exits 0 only when the done reason is `final`, the JSON says `final=true`, no verdict row is FAIL, the
  copied TAOM log holds no `[ERROR] [CrashReport]` line stamped between the done file's `started` and `ended` times (a swallowed
  engine exception prints a full ERROR block even when every in-game check passes; the INFO and WARNING startup lines are
  ignored), the game then exited by itself within 120 s and no rgl or watchdog crash marker appeared after the done file. It logs
  the game's exit code. A runner error inside the watch loop becomes a failure reason, and the game is still stopped and the
  evidence still collected.
- **Safety.** The module list is read from `Main/Properties/launchSettings.json` (profile `Bannerlord`, `$(ModuleId)` read as
  `TAOM`). The preflight fails closed: it refuses to start while any `Bannerlord.exe` or the game folder's `Watchdog.exe` runs, and
  also when it cannot list the processes, and while the PC has had keyboard or mouse input within `--require-idle-minutes` (default 10; 0 turns the gate off; an age it cannot read refuses too). The game is killed through its `Popen` handle (`proc.kill()`, then wait 30 s); the only
  other process stopped is a `Watchdog.exe` whose command line holds `-p <pid>`.
- **The mod-change dialog.** The first launcher run stalled because the engine shows a blocking dialog, "Mod change detected" ("A
  change in your active mods was detected. Deleting the runtime shader cache to avoid rendering issues.", one OK button),
  whenever the shader cache was built for a different module list; it appears before the graphics device starts. The launcher
  posts `WM_COMMAND` with the OK button's control id (the button handle as lParam) to exactly that dialog: no activation, no wait,
  one log line per window, and the post is repeated on each poll while the window lingers. It fails the run on a dialog whose
  title names a crash, and logs any other dialog without touching it.
- **Crash dump lost (follow-up, no code).** When the game crashes, `Watchdog.exe` asks a human what to do with the dump and the
  launcher kills it, so the dump is lost (`--keep-on-crash <seconds>` makes it wait that long for a person to answer the dialog first, and copies the engine's dump, which the game's `watchdog_log_<pid>.txt` names, to `engine_dump.dmp` in the run folder once that log says the crash uploader started, before the uploader deletes it); the copied `rgl_log` and `watchdog_log` files are the only native evidence of an
  unattended crash. Starting the game under a watchdog configuration that saves the dump without a prompt is not done.
- **`StopGameOnFocusLost`.** `BannerlordConfig.txt` has `StopGameOnFocusLost=True` on the desktop, and the launcher only logs
  it. It did not pause the mission in the probe run below (mission time ran at ratio 1.00 to wall time); the `clock` line
  shows the ratio on every run, and `wall_limit_seconds` is the backstop if a run ever stalls. An armed run also declines the Escape menu that a lost focus opens (`FordsCommandView`), which would otherwise pause the mission until someone closed it.

**2026-10-31 result.** A 45-second probe run (`E:/Temp/fords-runs/probe-20261031-220442`) completed the whole chain: armed, main
menu, start, live, end through the autorun limit, a final=1 report, a done file with `reason=final`, and the game quit by itself.
Full unattended matches, 2026-10-31 (folders under `E:/Temp/fords-runs/`):

| Run | Setup | Result |
|---|---|---|
| `20261031-220826` | 600 s, East AI against a passive West | 5 PASS, 0 FAIL; check 9 PASS with 731 native tower blows |
| `20261031-222418` | 1800 s, both sides AI (`west_ai`) | 7 PASS, 0 FAIL; checks 7 and 16 PASS (peak live West 241 and East 159 against a cap of 302, peak agents 454 of 2040, frame average 11.2 ms, p95 17.7 ms); no tower fell, check 14 not seen |
| `20261031-225939` | 1800 s, both sides AI, deployed tower hit points lowered for a test | no tower fell; the run could not prove which numbers had loaded, which the `[Tower] config` line now settles |

Both of the first two runs logged one engine exception from tower regeneration (regeneration raised a tower's hit points across a
damage stage; see Known limitations for the fix), which the earlier launcher let pass.

## Key Files

| File | Purpose |
|---|---|
| `Main/Features/FordsOfWar/Domain/FordsMatch.cs` (+ `.View.cs`, `.SpawnFailures.cs`, `.Lanes.cs`, `.HeroArrivals.cs`, `.Upgrades.cs`) | The aggregate: `Tick`, the `Try*` command API, kill credit, spawn-failure refunds, `GetView` |
| `Main/Features/FordsOfWar/Domain/` (rule classes) | `GoldLedger`, `BountyRules`, `WaveScheduler`, `TowerChain`, `TowerDamageRules`, `TowerRegen`, `TowerFireRules`, `HeroRoster`, `LaneOrderBook`, `LaneMarchRules`, `LanePath`, `CapturePointRules`, `CampRules`, `MatchClock`, `AiCommander`, `LaneLayoutGenerator`, `LaneGeometry`, `TroopPricing`, `UpgradeBook`, `PoisonRules`, `SeizeRules`, `RegionDeck`, `HeroCombatFactors`, `FordsGameSpeed`, `TowerAssaultLimit`, `TowerWear`, `FordsControlPick` |
| `Main/Features/FordsOfWar/FordsOfWarModule.cs` | Feature module: registrations, `Patch131_FordsOfWar`, main-menu option on the MainMenu phase |
| `Main/Features/FordsOfWar/FordsMissionDirector.cs` | Per-mission orchestrator; applies effects through the adapters |
| `Main/Features/FordsOfWar/FordsSession.cs` | The one command and view surface for the client |
| `Main/Features/FordsOfWar/Hooks/FordsMissionLauncher.cs` | Opens the mission with the eight behaviors |
| `Main/Features/FordsOfWar/Hooks/FordsOfWarMissionLogic.cs` | `MissionLogic`: start, tick, removals, `MissionEnded` |
| `Main/Features/FordsOfWar/Hooks/FordsTowerHitPatch.cs` | Patch131, prefix on `DestructableComponent.OnHit` |
| `Main/Features/FordsOfWar/Hooks/FordsUpgradeHooks.cs` | The army upgrades and the hero damage factors, applied per blow from the Custom Battle damage model |
| `Main/Features/FordsOfWar/FordsMissionExit.cs`, `FordsExitClock.cs` | Automatic return to the main menu: 25 s after the result, 3 s after a failed start |
| `Main/Features/FordsOfWar/FordsMenuEntry.cs`, `FordsSetupPrompt.cs`, `FordsSetupChoices.cs` | Main-menu option `TaomFordsOfWar` and the start prompts |
| `Main/Features/FordsOfWar/FordsOfWarCommands.cs`, `Cheats/FordsOfWarCheats.cs` | Console commands `taom.fords_start` and `taom.fords_gold` |
| `Main/Features/FordsOfWar/Config/` | `FordsConfigCatalog` and the four readers (match, factions, maps, upgrades) |
| `Main/Features/FordsOfWar/Autorun/`, `Client/Features/FordsOfWar/FordsAutorunHost.cs`, `tools/run_fords_autorun.py` | The unattended run: plan, arm file, driver and its pure rules, status and done files; the launcher |
| `Main/Features/FordsOfWar/FordsMatchPlan.cs`, `FordsSnapshotBuilder.cs`, `FordsLayoutFitter.cs`, `FordsLayoutPlanner.cs`, `FordsLayoutFit.cs`, `FordsTagLayoutReader.cs`, `FordsBaseFinder.cs`, `FordsTowerGeometry.cs`, `FordsSpawnPlacement.cs` | Pure helpers around the director |
| `Main/Features/FordsOfWar/IFordsOfWarGameManagerFactory.cs`, `NullFordsOfWarGameManagerFactory.cs` | Core factory interface and null default (ADR-013) |
| `Main/Adapters/IFordsBattlefieldAdapter.cs`, `IFordsTowerAdapter.cs`, `IFordsSceneAdapter.cs`, `IFordsCampAdapter.cs`, `IFordsMenuAdapter.cs` (+ impls), `FordsMissionSpeed.cs` (match speed), `FordsTowerComponent.cs` (a real tower's destructible), `FordsAgentRegistry.cs`, `FordsEngine.cs`, `FordsTowerStage.cs` (the shown tower stage only moves forward), `FordsAgentControl.cs` (static: the commander's take and return of a soldier; the mission's main agent is its only state) | Adapters and engine helpers |
| `Client/Features/FordsOfWar/FordsOfWarGameManager.cs`, `FordsOfWarGameManagerFactory.cs` | Client game manager and factory |
| `Client/Features/FordsOfWar/FordsOfWarMissionViews.cs` | `[ViewMethod]` list for the mission |
| `Client/Features/FordsOfWar/FordsCommanderCameraView.cs`, `FordsCameraMath.cs` | Commander camera |
| `Client/Features/FordsOfWar/FordsCommandView.cs`, `FordsCommandVM.cs`, `FordsCommandHotkeys.cs`, `FordsHudText.cs` | Command panel, hotkeys and every player-facing line |
| `Client/Features/FordsOfWar/FordsControlView.cs`, `FordsPanelStandAside.cs`, `FordsPointerPick.cs` | Soldier control: key P on the screen tick, the take and hand-back on the mission tick, the `[FordsOfWar][Control]` lines; the panel, its speed and the mouse while a soldier is controlled; the pick on the screen |
| `Main/Features/FordsOfWar/Domain/FordsControlPick.cs` | The pure pick: the unit the pointer names, a hero in reach first, within 8% of the screen's height; which mounts can be ridden |
| `Main/Features/FordsOfWar/Autorun/FordsControlCheck.cs` | The scripted take, hand-back and fall for the arm key `control_check` |
| `Main/_Module/GUI/PreFabs/FordsOfWar/FordsCommand.xml` | Panel prefab (movie `FordsCommand`) |
| `Main/_Module/ModuleData/fords_of_war/match.json`, `factions.json`, `maps.json` | Configuration |
| `Main/_Module/ModuleData/fords_of_war/fords_troops.xml`, `fords_items.xml` | The mode's own troop copies and items, loaded for CustomGame only (see The roster) |
| `Main/Features/FordsOfWar/Diagnostics/` | The self-check report (`FordsRunReport`), the verdicts and the evidence classes |
| `Main/Features/FordsOfWar/Hooks/FordsTelemetryLogic.cs`, `Hooks/FordsMissionResults.cs` | Mission behavior that feeds the report; the result hand-off |
| `Main/Features/FordsOfWar/FordsFieldProbe.cs`, `Main/Adapters/FordsVolleyHits.cs` | Field measurements and volley hit records for the report |
| `Main/Features/Warg/WargRageLog.cs` | Warg rage start and restore records the report reads; since 2026-11-07 a tripwire, because no warg rages in a match |
| `Client/Features/FordsOfWar/FordsCameraProbe.cs` | Camera evidence from the client (the view resolves the report and passes it in) |
| `Client/ClientSubModule.OnBeforeInitialModuleScreenSetAsRoot` | The final report pass when the main menu is back |
| `docs/features/fords-of-war/build-spec.md`, `greybox-scene-spec.md`, `balance-log.md` | The original build contract (history); the tag spec for hand-built scenes; what each match showed |
| `tools/fords_match_review.py` | Reads finished runs and flags what needs a person's reading |

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
- Soldier control, lost lanes, the assault limit and wall wear: `Domain/FordsControlPickTests` (nearest in reach, a hero over a closer soldier, nothing out of reach, a NaN pixel out of reach, which mounts can be ridden), `Autorun/FordsControlCheckTests` (the three rounds, the rest between them, a fall ending its round), `FordsSessionTests` (`IsOver`), `Domain/FordsLostLaneTests`, `Domain/TowerAssaultLimitTests` and `Domain/TowerWearTests`. `Autorun/FordsAutorunPlanTests` reads the `control_check` key, and `Client/FordsClientWiringTests` pins the control view in the view list, the key on the screen tick and the agent change on the mission tick, the camera and the panel standing aside, and each rule of `FordsAgentControl`. Those are source reads: `FordsAgentControl` needs a live mission.
- `ShippedFordsConfigTests.cs`: the shipped JSON parses; every troop, hero and culture id exists; computed
  prices equal the stored ones; the hold line is outside tower fire range; the volley item is a crafted
  javelin that SandBoxCore loads for a Custom Game.
- `ShippedFordsRosterTests.cs` (the troop copies, each faction's feats, buy cycles and creature gate, the shipped upgrades, hero arrival, hero factors, AI reserves and poison),
  `Domain/FordsUpgradeTests`, `FordsHeroArrivalTests`, `FordsHeroCombatTests`, `FordsGameSpeedTests`, `FordsMatchSeizeTests`, `AiBuyCycleTests`, `PoisonRulesTests`,
  `Client/FordsCommandHotkeysTests` and `Client/FordsUpgradePanelTests`. The review tool has Python tests: `python -m unittest tools.tests.test_fords_match_review`.
- `FordsMissionDirectorTests.cs`, `FordsSessionTests.cs`, `FordsOfWarMissionLogicTests.cs`,
  `FordsMissionLauncherTests.cs` (pins the behavior list and the omissions), `FordsMissionExitTests.cs`,
  `FordsExitClockTests.cs`.
- `FordsOfWarBindingTests.cs`, `FordsTowerHitPatchTests.cs`: engine bindings and the Harmony target.
- `FordsOfWarModuleTests.cs`, `FordsEntryPointLineCountTests.cs` (ADR-002 line caps),
  `Client/FordsClientWiringTests.cs`, `Client/FordsCommandVMTests.cs`, `Client/FordsCommandPrefabTests.cs`,
  `Client/FordsCameraMathTests.cs`, `Client/FordsHudStringsTests.cs`.
- `Diagnostics/` (the report, verdicts, evidence, log lines and report file), `FordsVerifyDocTests` (each checklist item names its `check=NN` row), and beside it the tests for the telemetry logic (`FordsTelemetryLogicTests`: the mission clock, the untimed first frame, the 1 Hz warg watch and the warg counts pushed before the closing pass; the options component, the warg log's activation, the 30 s agent-count window and `OnAgentHit` need a live mission and are not driven), the field probe, the volley hits and the camera probe (`Client/FordsCameraProbeTests.cs`, with a real `FordsRunReport`). `Main/Features/Warg/WargRageLog.cs` is tested in `TAOM.Tests/Features/Warg/WargRageLogTests.cs`; a reflection test there fails if the class names a TaleWorlds type in a signature.
- Review-fix batch (2026-10-31): `Domain/LanePathTests` (the path table equals `LaneGeometry.ArcPosition` over vertices, ties, a
  zero-length segment and the ends; the k-th unit front), `Domain/FordsMatchLaneTests` and `LaneMarchRulesTests` (a lead past a
  standing tower attacks it, West and East; 43 units at the tower and one 60 m past still lead from the main body), `Domain/LaneDecisionTests`,
  `Diagnostics/FordsMarchWatchTests` (the `bypass` arithmetic: 10 m past, more than half), `FordsFieldProbeTests` (the units past the next tower, West and East), `FordsLaneStateLineTests` (`cmd=` and `sent=`),
  `FordsLayoutFitLinesTests` (the timing line), `FordsVerdictDiagnosticsTests` and `FordsRunReportDiagnosticsTests` (the check 9
  ladder, tower health, the check 3 `exceptions` part), `FordsTowerStageTests` (the stage only advances),
  `FordsMissionDirectorLayoutTests` (the post-build refusal and the timing line), `FordsMissionDirectorDiagnosticsTests`,
  `FordsLayoutPlannerTests`, and under `Autorun/` `FordsAutorunPlanTests`, `FordsAutorunDriverTests`, `FordsAutorunBeatTests` and
  `FordsAutorunStartTests`. The telemetry logic is now an entry point over `FordsTelemetryMeter`, and `FordsEntryPointLineCountTests`
  sums every file of a partial class. The launcher has Python tests: `python -m unittest tools.tests.test_run_fords_autorun`
  (arm text, judge and final verdict, preflight, stop-own-tree, watch, collect, dialog handling, input checks).
- `Scenarios/` (the scenario harness): `FordsScenario` drives the real report, director and match, lane generator, spawn placement, field probe, exit clock and camera maths with faithful fakes, in the engine's per-frame order, then judges. `FordsScenarioTests` runs five scenarios (a clean three-lane match with no FAIL, one injected fault per row, a pause and a hitch, the camera pushed into the edge, the session tail after a finished run). `FordsVerdictContractTests` holds one row per verdict part (55) with the scenario that passes it and the fault that fails it, or `PassOnly`, and fails when the table and the parts `Judge` emits differ. The telemetry and mission logics are mirrored in the harness (they need a live mission), and the volley-hit reader is fed through the report, not driven (it takes engine structs).
- Shared tests this mode touches: `MissionViewRegistrationGateTests.Allowlist` (the three views built by
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

**Evidence for the lane chain.** The JSON and the log answer each link of "units reach a tower and strike it" apart. Lane commands are counted uncapped per stream and kind (`CommandsGiven`, `CommandsRefused`, `FirstGivenMt`; the log still prints only the first 30 `[Lane]` lines), and the `[Summary]` line carries them as `cmds=W0:40/1/1/0!0 E1:...` (MoveTo/HoldAt/Charge/AttackTower given, then refusals) beside `alarmed=W0:1.00 ...` (the share of alarmed lane units; lane units now spawn alarmed as vanilla battle spawns do, gunners do not). A command whose target was off the navmesh is counted in `OffNavmesh` once per command, with the refused target, whether the nearest-waypoint fallback then took the order (the command is recorded as applied and never sent again) or nothing could be sent (it is retried each tick but counted only the first time); the first is logged once per stream as `[Lane] offNavmesh`. `[FordsOfWar][LaneState]` lines (see Lane rules) show the rules' decision beside what was applied. Per stream ordered at a tower, the whole ladder is measured at that order's own target: `AttackUnitSeconds` (live unit-seconds under the order), `AttackFlaggedUnitSeconds` (those whose combat flags carry `AttackEntity`) and `AttackContactUnitSeconds` (melee units within 4 m of that tower), then `TowerBlows`, `AppliedBlows` (the ones that took hit points) and `GunnerBlows`. The 1 Hz look sets each stream's current target from its applied command and clears it when the look sees no AttackTower order; a blow on any other tower, or on another tower's gunners, or while the stream has no such order, is counted on that tower's own totals and not on the stream's ladder (a one second lag after an order changes is accepted). A blow on an invulnerable gunner is taken from `OnMeleeHit` and `OnMissileHit`: an unblocked melee blow on a gunner arrives cancelled with zero damage and never reaches `OnAgentHit`, a blow the gunner blocks still does with zero damage, and a missile vanishes on the gunner (`MissileCollisionReaction.BecomeInvisible`) while `OnMissileHit` still fires. It is counted per tower (`GunnerBlowsMelee`, `GunnerBlowsRanged`) and per stream only when the attacker is a known enemy of the tower's owner and the stream is ordered at that tower. A weaponless blow (a kick, a mount's charge) is counted by its reason but stays out of the melee raw-damage summary. Per tower the JSON also keeps the lowest rules hit points seen at the 1 Hz looks (`MinRulesHp`), the damage applied per attacker class (`AppliedDamage`), the hit points regeneration gave back (`HealedTotal`), blows by attacker side (`BlowsBySide`) and a histogram of every melee blow's raw damage (`MeleeRaw`: count, minimum, median, maximum), so a `RawZero` outcome can be told from a small number. At match start one `[FordsOfWar][Tower] config` line states the effective tower, inner tower and keep hit points, regeneration per second and delay, and the ranged, hero and siege factors, so a run proves which numbers loaded. At match start the `[Layout]` line and check 4's `layout` part report the fit (see Layout fitting).

1. Look at the main menu. A "Fords of War" entry shows with its hint. Click it: your faction, enemy faction
   and difficulty prompts appear, and no map prompt (Fords of War is the only map). Cancel one prompt and
   confirm the flow ends with no match. Report: `check=01` (option installed, the engine's visibility poll, prompts shown and answered per step, a cancel or abandoned flow seen; any menu flow that ended in a fault FAILs, even when a later flow was fine). Drawn on screen is yours to judge.
2. Run `taom.fords_start gondor mordor hard`. Observable: the console prints "Starting Fords of War:
   gondor against mordor on taom_fords_01_forceatmo, AI profile hard, 3 lanes." and the match
   loads. An unknown argument prints the list of valid ids. Run `taom.fords_start` with no arguments for
   the defaults (West gondor, East rohan, `normal`, the only scene in `maps.json`, Fords of War). Report: `check=02` (a named start that reached the match, an unknown argument's reply, the no-argument start and the setup it resolved; `ShippedFordsConfigTests` pins the shipped defaults, so the row does not ask you to compare them).
3. In the log, find `[FordsOfWar] match started on <scene>: 3 lane(s), ... AI profile <id>`. Confirm there
   is no `could not spawn '<id>'` line once the first waves and any bought hero are out: that proves the
   hero ids (for example `lord_1_17`) resolve in a Custom Game. Report: `check=03` (every troop and hero id checked against this game, spawns and spawn failures by troop; a recorded start failure with no match started FAILs its `started` part, for the menu routes too; an `exceptions` part FAILs, naming the first type and site, when an exception was swallowed by TAOM's CrashReport finalizers since the mission started, because the mission telemetry listens to `CrashReportPatchHelper.Swallowed` (other Harmony finalizers, such as `PatchShield` and `Patch50`, are not heard), and it passes only when that listener was in place and none did).
4. Wait out the 10 s countdown. Towers and keeps appear on both sides in 3 lanes, free waves march and
   fight, and your gold rises on kills. The camera pans (WASD), rotates (Q, E, right mouse drag) and
   zooms (wheel) and stays inside the playable area. Report: `check=04` (countdown end, towers built against expected, first spawn per lane, march gain per lane, deaths, kill gold, camera pan, rotate and zoom, inside the engine's boundary, camera ownership, and a `layout` part that FAILs on a missing tower or a keep open at the start and names the ids, for example `missing=tower_west_1_0 keepOpen=West`). Towers built are counted against the layout before unplaceable ones are dropped, so a dropped tower FAILs. A camera resting on the boundary edge counts as inside; it reads outside only when the engine says so and the camera is over 0.5 m from the edge. The battlefield is lit and the panel's standing dark boxes (the top strip, the battle board at the bottom left, the command deck at the bottom centre, the heroes at the bottom right) each hug their content; a box that stretches to a screen edge it is not anchored to is the stretch-in-cover layout fault.
5. Open the escape menu. Photo Mode reads disabled ("Photo mode isn't allowed."), and the Options entry
   opens the options screen. Report: `check=05` (pause edges, Photo Mode vetoes, Options requests and wiring).
6. Run `taom.fords_gold 5000`. The console prints "Added 5000 gold. You now hold ..." and the panel's gold
   matches. Buy a pack with F1 to F12 or the buttons; the gold drops by the pack price and a feed line
   appears. Report: `check=06` (the gold command's reply, pack purchases, feed lines).
7. Press H (or click a hero card) to field the first hero in the list that you can afford, for Mordor Sauron at 2400. The hero
   spawns in the selected lane. Kill it, then check the respawn timer on its card and the buyback button. Report: `check=07` (a fielded hero has a spawned mark of the same hero and side within 5 mission seconds, with the requested lane in the `[Hero]` line; death; respawn spawned within 1.5 mission seconds; buyback results). The row no longer compares the lane the engine put the hero in with the one asked for, because the adapter read that lane back from the formation it had just placed the hero in, so the two could never differ. Leaving the match more than 5 mission seconds after a fielding with no spawn mark reads FAIL. A hero bought in the last 5 s before the winner is named is excused, because the clock ended before the spawn ran: it neither fails the part nor takes away the pass of the heroes that did spawn, and a part with only excused purchases reads NOT_SEEN.
8. Set a lane to Hold (X), then Push (Z) and Fall back (C). The order buttons show a cooldown; the lane
   forms up 60 m before the enemy tower, or at the lane midpoint on a lane over about 428 m, and does not stand inside tower fire
   (the hold is at least 60 m from the tower, outside the 45 m fire range, so check 8 does not fail on a short lane by design). Report: `check=08` (accepted Push, Hold and Fall back orders, held lanes against tower fire range, the keep-to-keep distance as `baseDistance`). A hold sample counts only once the lane's front unit (the one farthest from its keep) is within 10 m of the hold point measured along the lane's axis (the direction of the path segment at the hold point), so a wide line formation whose middle stands at the point is settled and a lane still walking back from a tower is not judged. A FAIL names the nearest standing enemy tower whose fire range covered the lane (`West1 in fire of <tower id>`, and `tower=<id>` in the per-lane numbers).
9. Send melee units at an outer tower. Check the log and the tower bar. The row's info reads `attackTowerGiven=N (W1:1 E1:4) nativeTowerBlows=Z gunnerBlows=A melee/B ranged | ladder W1>tower_east_1_0 orders= underS= flaggedS= contactS= towerBlows= melee= gunnerBlows= applied= | towerHealth <tower id>:minHp= healed= applied=Melee:..,Ranged:.. meleeRaw=nN/minA/medB/maxC floored=F/D probe=KEY:COUNT,... | melee blows ... | all blows ...`. `meleeRaw` is the engine's raw damage, so thrust zeros stay zeros there; `floored=F/D` is the blows the thrust floor turned into damage and the hit points they took; `probe=` names the three commonest melee probe keys of the tower (`T/Pierce/tip0/pas0/alt0/arm0/mnt0/b<1`: swing or thrust, damage type, tip hit, braced, alternative attack, arm bone, mounted, then `hit` or the base magnitude bucket `b>=1`, `b<1`, `b0`, `bNaN`). Read the ladder left to right per stream and stop at the first zero: `orders` 0 means the rules never ordered the strike; `underS` above 0 and `contactS` 0 means the units never stood within 4 m of the tower they were ordered at (a locked or another tower does not count); contact above 0 and `flaggedS` 0 means the `AttackEntity` flag never reached them; flagged above 0, `towerBlows` 0 and `gunnerBlows` above 0 means they swing at the gunners; `towerBlows` above 0 and `applied` 0 means the rules took nothing (read the outcome counts and `meleeRaw`); all zero after flagged means the engine does not swing at a bare `DestructableComponent`. Every number in the ladder counts only blows on the tower the stream was ordered at. The `bypass` part reads `samplesJudged=N failingSamples=M`: a look is judged when a stream's applied command is a MoveTo with a next standing enemy tower, and failing when more than half its units stand more than 10 m past that tower along the lane (West: unit arc at or above the tower's plus 10; East: at or below its minus 10). It FAILs when one stream has two failing looks in a row, naming the worst stream (most failing looks, then the largest share), the tower, the share, the run length and the mission time of the second look (`worst=E0 past tower_west_0_0: 83.3% of its units, 2 looks in a row, from mt=612.5`, with `also=` for other failing streams); it passes once a stream was judged and none failed, and is NOT_SEEN while none was judged. The pass rule of the other parts is unchanged. A tower behind another takes no damage until the outer one falls. Report: `check=09` (blows per tower by attacker class and reason, locked blows, melee near towers, engine against rules hit points, the per-stream ladder and the `bypass` part).
10. Stand a side's own units next to its own tower and watch its bar: towers take no damage from their own
    side (gunners, defenders shooting across it). Report: `check=10` (own-side blows that took nothing, which includes own-side blows the engine had already zeroed and own-side blows on locked or fallen towers, and hit-point drift). NOT_SEEN is expected when nothing strikes its own tower.
11. Check 11 judges the javelin and `fow_tower_arrow` each against its own N and S: tune the arrow from the row's `arrow` part and `arrowPerBodyPart`. At match start, find the log line `tower volley item 'western_javelin_2_t3': thrust damage N,
    missile speed S`. Check that an unarmoured attacker hit in the torso near launch speed loses about
    N x (1 + 0.0006 x the gunner's Throwing skill) hit points. Armour, a leg hit, a non-human body and
    race damage reductions lower it; a head or neck hit doubles it; the impact speed scales it by
    (relative impact speed / launch speed) squared, so an attacker walking into the volley takes more.
    Check also that no warning says the reach is below the fire range. Report: `check=11` (item N and S, reach against fire range, per-hit magnitude against N x multiplier x (impact speed / S) squared). The `hits` part also FAILs when the towers asked to fire at least 10 times and not one missile was added (`launched` counts missiles the adapter added, 0 when the aim skipped them all), and when 20 or more missiles flew and no hit was seen; the row prints the hits it filtered out, by reason. The hit reader takes engine structs, so only your run proves it.
12. Since 2026-11-07 no warg rages in a match (Mike: "it is not needed for this"; `WargTryToGoRage` starts none in Fords), so
    this row reads NOT SEEN on every run, and a `started=` count above 0 FAILs it: the gate broke. Before that: field a Mordor or
    Isengard warg rider (`warg_raiders`, `warg_enforcers`) and watch a raging warg: it returns to its lane formation instead of
    wandering off. Report: `check=12` (rage starts, formations restored, riders back in formation within 60 s of mission time, riders left on foot with no formation to return to, called `stranded`, which FAIL the restored part). A rider counts as back when it is within max(15 m, half the formation's width + 5 m) plus the formation's depth of the formation's front-middle, so a rider at the rear of a deep formation is not read as lost, or when it is within 15 m of a mate in the formation's arrangement (not a detached unit, not another rider whose rage episode is open), so a rider with the lead group of a lane formation strung out along its lane is not read as lost either. The `returned` and `not-returned` lines print both distances (`distance=` to the front-middle, `nearest=` to the closest mate, `none` when there is none). A rider whose rage restarted before it returned is counted as `superseded` (in the row's numbers and a `[Warg][Rage]` log line): its first reset already counted toward `restored`, and only its return goes unjudged. The `[Warg][Rage]` lines in the log show each episode (start, end, returned, not-returned, stranded, superseded).
13. Look at the capture-point row under the lane rows of the battle board (bottom left): it shows owner and capture
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

**Soldier control (no check row yet).** In a running match put the pointer next to one of your soldiers and press P: a message names the soldier, the panel hides, the pointer is free and the engine's agent status HUD shows. Fight, then press P to hand it back: the panel and the commander camera return at their usual view angle, your speed setting comes back, and the soldier goes back to its lane. With a hero near the pointer, P should take the hero over a closer soldier. Press P at a troll or a warg rider: it should name another soldier, or say none stands near the pointer. Let a controlled soldier die: control returns by itself with a message, and P takes another. Read the `[FordsOfWar][Control]` lines: `pick`, `took` (with `mode=`), `returned`, `fell` and, 10 s after a return, `rejoin` (`ai=1`, `brain=1` and `formation=1` with `moved` above 0 mean the soldier is back under its AI). Judge by eye what the log cannot: the feel of the fight, whether target lock works, and whether the pick follows the pointer on a slope. No verdict row and no review tool reads these lines yet.

## How to tune or extend

After every match, run `python tools/fords_match_review.py <run folder>`, read its findings, and add the result to [balance-log.md](fords-of-war/balance-log.md). The `side=` line of `[FordsOfWar][Balance]` says where each commander's gold came from and what it paid for.

**Change a number.** Edit `match.json`: no code change. Mind the pinned relations: `lanes.holdDistanceBeforeTower`
must stay above `towers.fireRange`, the readers bound each value (tower fractions 0.01 to 0.49, lane count 3 or 4).
Persisted saves hold nothing, because a match lives and ends inside one mission.

**Change what a faction sells.** Edit `factions.json`. Troop and hero ids must exist in the module data,
and hero ids must also resolve in a Custom Game (`characters/custom_battle_lords.xml`, or `characters/lords.xml`,
which is registered for `CustomGame`). Give an offer `level` and `eliteWeight` and let `TroopPricing` set the
price; add a `price` only to override it. A faction shows at most twelve offers, one per buy key (F1 to F12).
Culture ids are traps (`empire` is Dunland): read them from the troop files.

**Add a faction.** Add the block, a `TAOM_FOW_Faction_<Name>` key and its row in `taom_module_strings.xml` and the
12 language files, then run `ShippedFordsConfigTests`.

**Add a map.** Add a scene to `maps.json` with `"layout": "generated"`. Pick an open, non-siege scene
with a large boundary and a navmesh; the layout is built from two base points found from the boundary, and
`FordsLayoutFitter` snaps points to the ground and the navmesh.

**Use a hand-built scene.** Follow [fords-of-war/greybox-scene-spec.md](fords-of-war/greybox-scene-spec.md):
a scene in the Modding Kit (the shipped `taom_fords_01_forceatmo` is one; the spec proposed the id `taom_fords_greybox_01`), with waypoints tagged `fow_lane_<lane>_<n>`,
towers tagged `fow_tower_<west|east>_<lane>_<order>`, keeps `fow_keep_<west|east>`, capture points
`fow_capture_<n>` and camps `fow_camp_<n>`. Then add it to `maps.json` with `"layout": "tags"` (plus `towerHalfWidth` for real towers and `laneFaceIds` for lane bars). An
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

- **A shield wall is a mixed formation.** A lane is one formation of every class its waves bring. The engine moves shield bearers
  to the front rank while the lane marches with space between its units, and stops once the wall forms, so a lane with enough of
  them shows a shield front, but archers and riders stand in the same wall. Split
  formations per lane would need up to two more formations a lane, against the engine's 8 regular formations a team.
- **Archers in a shield wall: unverified in game.** The engine's source says a wall's units take the defensive move behaviour, with
  a ranged weight of 0, where the normal move behaviour gives 0.02 to 0.04. Whether archers shoot less while their lane closes in a
  wall, near the lead or trailing behind it, needs a game run.
- **The same native crash four times, engine side, unfixed; mechanism named, trigger not.** Fourth occurrence: Erebor v Rohan, 15:00 on 2026-11-05, mission time about 241 s, same RVA and registers; the crash uploader deleted its dump (the launcher now saves `engine_dump.dmp`). **Mechanism** (three dumps read, `E:/Temp/fords-crash-0x6B716E`; Codex reached the same reading): the faulting agent is an AI soldier on foot whose AI flag `+0x19c` still says its target is valid while the target index is -1, so `FUN_1806b6600` reads the agent slot before the array (`rsi - rdi = 0x20F0 = 0x2D50 - 0xC60`), which is zero. Deleting an agent (`FUN_1805bced0`) sets every AI's target on it to -1 (`FUN_1805d6720`) but leaves the flag, and the per-agent AI tick (`FUN_1806a75a0`, worker thread) can read the flag before its refresh (`FUN_18069ce50`) clears it: an engine bug. **Trigger, open:** something deletes an agent that a soldier still targets without routing it first. The riderless sweep's fade cannot: `Agent.FadeOut` routs the agent first (`FUN_1805eb120` calls `FUN_1805f02b0` with state 2), and the rout resets each soldier's target and flag together (`FUN_1805d6640`) before the fade starts. A death resets no soldier that targets the dead agent (`FUN_1805f02b0` acts only for Routed and Deleted), so the open candidates are the delete of a dead agent and 4x speed. Which path deletes a killed agent in single player is unread (the third branch of `FUN_1805fbb80`); the 4 s `CorpseFadeSeconds` setting's only reader removes corpse records without calling the agent delete. Third occurrence: run `20261105-143340` (Rohan v Rivendell, round robin build `92973f27a`, 4x, 14:37, mission time about 557 s) faulted at the same RVA `0x6B716E` (Event Log), with `r8` = 0 and a read of `0xC` again (`native_crash_triage.py --dump`). Its last log lines show Rohan riders dying on their horses and riderless chargers taking missiles. Two of the three crashes were Rohan matches, the faction with the most horses; that fits the riderless mount idea, and still names no cause. Dump: `E:/Temp/fords-runs/20261105-143340/crash.dmp`. Second occurrence: run `20261105-104403` (Rohan v Mordor, round robin build `92973f27a`, 4x, about 10:46, mission time 190 to 300 s) faulted at RVA `0x6B716E` on changeset 125270, `movss xmm13, [r8+0xC]` with `r8` = 0, agent index 150 whose last blows struck a riderless horse. `python tools/native_build_match.py` carries the first crash's `0x6B4C4E` (changeset 125053) to `0x6B716E` (MATCHED, hash): the same site. The agent records match field for field (`+0x55c` = -1, state 1, flags `0x18cf98`, a soldier on foot). A riderless mount fade fits the second crash but not the first (no fade in the 31 s before it), so the cause stays unnamed. Full dump: `E:/Temp/fords-runs/20261105-104403/crash.dmp`, analysis in `E:/Temp/fords-crash-74588`. The round robin retries a crashed pair once.
- **First occurrence.** Run `20261103-225059` (Dol Guldur v Erebor, 4x, about 760 s, 348 agents, no Kit open) died on an AI worker thread at `TaleWorlds.Native.dll` RVA `0x6B4C4E`, `FUN_1806b40e0`: it reads the target agent's record through index -1, slot -1 holds null, and the read of `[null + 0xC]` faults. Its caller `FUN_1806b2030` enters when its move state is 1, with no check on the index, and the agent's AI mode was 5. `IMBAgent_GetTargetAgent` reads the same two fields (`+0x1504`, `+0x225c`), so that index is the AI's target. The agent was an Erebor spearman (slot 68). No TAOM call on this path sets a target, so it looks like an engine fault in a large fight, not proven: one occurrence in about 25 matches. Compare the offset if a crash repeats, but only against a crash on the same engine changeset: this run was on changeset 125053, and the hotfix to 125270 on 2026-11-04 changed the native DLL (see "Hotfix: changeset 125270" in [v1.5.6-impact.md](../migration/v1.5.6-impact.md)). The launcher cannot get a dump from the Watchdog's dialog without a person, and answering Yes may upload to TaleWorlds, so take one with `procdump -ma <pid>` while the launcher waits; a full dump takes about 60 s.
- **The white ground in Fords runs is closed (Mike, 2026-11-03).** It comes from his local ReShade setup, not TAOM: 28 of 28 runs with his setup were white at the countdown, and an empty ReShade base path gave 0.2 percent. Run no white tests and do not raise it unless he asks. If he does, the unproven lead is the `DPX` effect active in his `ReShadePreset.ini`, plus `obs_capture.addon64`; dlss5-bridge, renodx and swapchain_override alone each gave 0.3 percent near-white, without the shaders folder.
- **Dread aura cannot rout troops here.** Nothing turns a panic into a rout: the launcher leaves out
  `MissionAgentPanicHandler` on purpose (the behavior whose `OnPreMissionTick` calls `Retreat`;
  `FordsMissionLauncherTests` pins the omission), because troops that rout would leave their lanes. The
  engine's default mission behaviors do not add it, and at the shipped `moraleFloor` of 10 the drain never
  reaches the engine's 0.01 panic threshold anyway. The aura still works through the fear voice
  (`fearVoiceChancePerPulse`) and race-ability frights. The no-rout claim was not seen in game.
- **A camp's ring is the map decal.** Its look on the ground is unconfirmed until someone sees it in game.
- **Translations.** The `TAOM_FOW_` texts are English in all 12 languages until the translation backlog,
  issue #763.
- **Generated maps are unchecked, and none ships since 2026-11-05.** The hand-built scene `taom_fords_01_forceatmo` is the only map (see The hand-built map). The generated layout fits Rohan Plains II cleanly
  (2026-10-31: 0 of 168 items dropped, all 14 towers built), but Rohan Plains I, Pelennor and Mordor have no in-game fit run since the
  fitter was rewritten, and the planner limits (10% dropped, 30 m tower move, 5% bad segments) are untested in game.
- **Check 9 is resolved.** The unattended 600 s match `20261031-220826` (East AI against a passive West) passed check 9 with 731
  native tower blows, which answers build-spec open question 3: melee AI does damage the barricade. The earlier FAIL (the 16:54
  match, 1,030 unit-seconds near a tower and no blow) came before the lane rules were rewritten.
- **The first AI matches saw no tower fall.** In `20261031-222418` and `20261031-225939` (1800 s, both
  sides AI) no tower fell and no keep fell. On that generated map, gunners on the ground absorbed a share of the melee blows
  (`GunnerBlowsMelee`); every report since they moved to the tower tops on 2026-11-02 records 0. Most melee blows on a tower do zero raw damage (`RawZero`; read `meleeRaw` in the check 9
  info); the thrust damage floor (`towers.thrustFloorFactor`) turns the thrust ones into damage from 2026-11-01. Tower balance (hit points, regeneration, the damage factors) is the maintainer's call. Run `20261031-225939` lowered the
  deployed tower hit points for a test but could not prove which numbers loaded; the `[Tower] config` line now does. Since then towers do fall: the matches in [balance-log.md](fords-of-war/balance-log.md) lost 0 to 6 towers a side, and the 45-minute match `20261103-201138` lost 6 towers on one side with no keep down. The first keep fell in `20261103-203523`: Erebor beat Dol Guldur at 889 s, and check 14 passed its keep, result, exit and menu parts. A person has still to see the result panel.
- **Regeneration against the damage stage.** Regeneration raises a tower's hit points, and the engine recomputes the stage from
  them, which once read `_destructionStates[-1]` and threw (seen in runs `20261031-220826` and `20261031-222418`, one exception
  each). `FordsTowerAdapter` now sets `DestructableComponent.OnCalculateDestructionStateIndex` so the shown stage only moves forward
  (`FordsTowerStage`). Owed in game: a heal between two hits on a staged tower with no exception, and the check 3 `exceptions` part
  on a real swallowed finalizer exception.
- **A crash dump is lost in unattended runs.** See the Crash dump bullet under Unattended runs.
- **Matches on 2026-10-31:** the first Rohan field scene matches (before the lane rules and layout fitter above were rewritten)
  passed checks 3, 11 (volley damage matches the engine formula, median ratio 1.000 over 14 hits) and 13, check 16's cap and
  agent-limit parts and the camera parts of check 4; the 1800 s match `20261031-222418` also passed checks 7 (heroes seen) and 16.
  Not yet seen: warg rage (12), keep fall and exits (14), a provoked failed start (15). The match first ran its start twice and
  rendered black; both are fixed (see the RCA, rows G1 and G2), and the `[FordsOfWar][Camera] pose` lines log the camera's
  position, ground and view direction for the first 30 s.

## Performance

- Spawning is budgeted: `waves.maxSpawnsPerTick` units per tick, `maxLivePerLanePerSide` and
  `maxLivePerSide` caps, and a corpse fade-out of 4 s.
- A formation order is issued only when it differs from the last applied one (`LaneCommand` has value
  equality). The old Hold rule, which used the drifting live center, produced a new command about 60 times
  a second; Hold now has one stable target.
- The tower hit prefix reads one volatile field first, so a mission with no Fords match costs a null check.
- Panel lists are updated in place, so a button under the cursor survives the refresh (about four a second).
- Each lane's path is turned into a float-array table once at match start (`LanePath`), because the lead group costs about 0.45 ms
  per tick at 240 units when it walks the polyline points each time (measured in a Debug build before the table).
- Agent count is judged by `check=16`, against TAOM's config cap and the engine's agent limit; frame times are reported per 30 s window with no budget set.

## Changelog

- 2026-11-07: the AI regroups under its own tower: a lane outside the assault that the units near its lead outvalue by more than 1.6 to 1 falls back, returns to Push at 1.1 to 1, and a Fall back lane fights when an enemy comes within its tower's fire range (`AiCommander.FallBackRatio`, `ReturnRatio`, `RegroupMinEnemy`).
- 2026-11-07: culture tactics: a faction's `tactics` block sets its lanes' march and close arrangements and the ranges at which they close in formation and charge; Isengard, Erebor, Gondor and the Easterlings close in a shield wall.
- 2026-11-07: cavalry and horses take twice the damage of a pierce blow (`pierceVsMountedFactor`), and scripted blows (bites, signature splash) go through the damage model; both in Fords of War only.
- 2026-11-05: Fords of War is the only map (Mike); the four generated maps left `maps.json`.
- 2026-11-04: a time limit: at minute 40 the side holding more of the field wins (`timeLimitMinutes`, `TimeVictory`).
- 2026-11-04: no bowmen on a tower: a hidden, neutral owner per tower, so no archer aims at it, and a rock every third volley (`towers.rockEveryVolleys`, `fow_tower_rock`); `towers.gunnersPerTower` is gone. Not yet run.
- 2026-11-04: soldier control: P takes the soldier nearest the pointer (a hero in reach first) and hands it back, and a fall hands it back by itself; the panel and the commander camera stand aside and the match runs at 1x meanwhile (`FordsControlView`, `FordsPanelStandAside`, `FordsPointerPick`, `FordsAgentControl`, `FordsControlPick`), the engine's agent status HUD joins the view list, and the arm key `control_check` with the launcher flag `--control-check` scripts a take, a hand-back and a fall. Trolls, creature riders and seated crew cannot be taken. Not yet played. A lane whose towers have all fallen starts no new free wave for its owner (`TowerChain.LaneLost`); not yet measured. A tower takes at most 50 damage a second from ordinary blows (`towers.assaultDamagePerSecond`), and after minute 15 every blow on a tower does more (`towers.wearStartMinutes`, `towers.wearPerMinute`). Rohan's riders cost 0.7 of the full price for their level and its horse archers 0.5; Dol Guldur's four cheapest packs cost 0.8, its spider riders 1.5 and its fell wargs 1.2. Mordor's `damageToTowers` 0.6 of 2026-11-03 is gone. A threatened lane follows the buy cycle (`AiCommander.Defend`, committed late on 2026-11-03). Mike's target for a match is 30 to 40 minutes, 30 best, in place of 30 to 45. The engine is v1.5.6 changeset 125270.
- 2026-11-03: nine more factions (Rivendell, Erebor, Harad, Gundabad, Dale, Dol Guldur, Easterlings, Umbar, Mirkwood), 13 in all, with more feats (tower range,
  regeneration, poison, order cooldown, camp reward); army upgrades bought with gold; the AI buys to each faction's identity (`aiBuyCycle` with `up:` and `beast:` entries);
  a free hero every 5 minutes; hero damage factors; the AI hero reserve of 600, 800 and 1000 gold and `AiCommander.BuySparePack`; Mordor's `damageToTowers` 0.6; match speed
  1x, 2x, 4x and 10x; the end-of-match balance lines, `tools/fords_match_review.py` and `balance-log.md`.
- 2026-11-02: fixed a native crash at `TaleWorlds.Native.dll+0x6E5BCD`, about once in seven long matches: housekeeping faded a horse a soldier still held.
- 2026-11-02: the mode has its own roster, `fords_troops.xml`, loaded for CustomGame only.
- 2026-11-02: four factions that play differently: feats, traits, Gondor's Call of the Regions with a fee, Mordor's creature call, rosters built for each. New command panel. The
  autorun can save pictures of the screen.
- 2026-11-02: neutral camps spawn, with a ring, and pay when cleared; the Seize order for lanes, used by person and AI; the hand-built map
  with real towers, bowmen, lane IDs and housekeeping; lanes charge only their own lane's foes; towers fire arrows and javelins.
- 2026-11-01: last review findings: check 9's ladder counts blows only on the stream's ordered tower; `bypass` is measured from the
  units at the 1 Hz probe (more than half past the next tower by more than 10 m on two looks in a row) instead of the rules' lead;
  an off-navmesh target is counted once per command even when the waypoint fallback took the order; a weaponless blow stays out of
  the melee raw summary; check 3 `exceptions` names the CrashReport finalizers it hears; gunner hit wording corrected; the telemetry
  meter no longer subscribes twice when a mission reruns `AfterStart`.
- 2026-10-31: review-fix batch: the lead group is a k-th unit front with a band on both sides, and a lead level with or past a
  standing tower attacks it (no march cap); `LanePath` table; one `[LaneState]` format with `cmd=` and `sent=`; check 3
  `exceptions`, check 9 per-stream ladder and `bypass`, per-tower hit point evidence and the `[Tower] config` line; tower stage
  hook against the regeneration exception; layout fit refusal after `BuildTowers`, camps out of the planner share, slide distance
  as arc offset, timing line; autorun `west_ai`, `exit_stalled`, host registered through the client composition; launcher fails on
  a FAIL row or a `[CrashReport]` error block, posts the dialog click, fails closed, kills through its process handle; docs: hold
  arithmetic corrected (the midpoint applies to lanes over about 428 m).
- 2026-10-31: unattended autorun and `tools/run_fords_autorun.py`; lane rules with a lead group, a forward-only march and a bounded Hold; a layout fitter with a planner ladder; uncapped lane command counts, `[LaneState]` and `[Layout]` lines, tower contact and gunner blow evidence; Rohan Plains II first in `maps.json`.
- 2026-10-31: self-check review fixes: the warg rage log, the run's own mission clock, the verdict rules
  of checks 7, 8, 11, 12, 15 and 16, and the scenario tests that drive the real report.
- 2026-10-31: self-check report: per-step verdict rows and a JSON report per run.
- 2026-10-31: first build. The mode starts from the main menu or `taom.fords_start`, runs a 3-lane
  tower-and-keep match for four factions (Gondor, Rohan, Mordor, Isengard) on a generated layout over three
  existing field scenes, with a commander camera, a command panel, heroes with respawn and buyback, capture
  points, a tower damage table behind Patch131, and an AI commander at three difficulties.

## GitHub Issue

Not created yet: the issue needs Mike's word.
