# The Long Defeat

## Overview

A planned campaign-free survival mode on the siege maps: you defend Helm's Deep or Minas Tirith against wave after wave, and you spend gold between waves. **Today only the Phase 0 spike exists.** On 2026-11-03 an unattended run cleared three waves at Helm's Deep with every spike line present. The spike has no menu entry. Only a console command in cheat mode or an environment variable starts it, so a player session never meets it. The plan and the phases are in [#1079](https://github.com/haterade22/TAOM/issues/1079).

## Why This Exists

- **Vanilla behavior:** a siege is one battle, with one spawn logic that owns every troop from the start.
- **TAOM requirement:** waves without end, under vanilla's siege AI and engines, with no scripted AI. Fords of War scripts its own AI; a siege should not have to rewrite ladders, towers, rams and wall defence.
- **Without the spike:** Phase 1 would build a mode on engine assumptions that nobody has tested. The first review of the spike already disproved one of them (below).

## Architecture

### What the spike leaves out of vanilla's list

`LongDefeatSpikeLauncher` copies vanilla's custom siege list (`BannerlordMissions.OpenSiegeMissionWithDeployment`) and leaves these out: the spawn logic and handler, the reinforcement controller, `BattleEndLogic`, the two deployment behaviors, `BattlePowerCalculationLogic`, `GeneralsAndCaptainsAssignmentLogic`, and the observer, agent, banner, victory and highlight logics. `LongDefeatSpikeRunner` spawns every agent from `OnMissionTick`.

Two engine paths read what is left out. [The lifecycle doc](../reference/engine/mission-and-missionbehavior-lifecycle.md) has the mechanism and the line numbers.

| Engine path | What the spike does |
|---|---|
| `TeamQuerySystem` reads `IBattlePowerCalculationLogic` with no null check | `LongDefeatSpikeLogic` implements it and returns a large constant |
| `GeneralsAndCaptainsAssignmentLogic` asks a model that reads the spawn logic | It stays out, so the team has no general. `AssignPlayerRoleInTeamMissionController` still sets the order owner |

### How a soldier gets a place

Soldiers spawn with no position, and the deployment plan gives each formation its place. `Boot` gives `DefaultMissionDeploymentPlan` the troop counts and calls `MakeDefaultDeploymentPlans`. The garrison and each wave then call `Formation.BeginSpawn` and `Mission.SetFormationPositioningFromDeploymentPlan`, and each soldier asks `GetAgentBuildDataToSpawnTroop` with no position. Reinforcements skip those two calls, as vanilla does for a formation that already has a place (`MissionBattleSideSpawnContext.cs:369`). Two parts are not vanilla's path: each wave places its formations again, and a wave's batch stays open across several ticks. Vanilla's reader of `IsSpawning` is in the class this mission leaves out (`:403`).

**What keeps a soldier on the right floor:**

- The layout offers only slots that native `BatchFormationUnitPositions` accepts, spreading out from the frame (`Formation.cs:669-671`, `LineFormation.cs:495-497`). A slot needs a navmesh face, in a siege the frame's face group digit, and a height near its neighbours. That list comes from the native pseudo-C.
- A slot with no navmesh goes back toward the frame (`Mission.cs:3983-3985`).
- When the slots run out, every remaining soldier stands on the frame point (`Mission.cs:3992-3995`).
- `IMBAgent.SetInitialFrame` then takes the height. It casts a ray down from 1.5 m above the given height, falls back to the terrain height, and uses 0 if every try misses. The fixed path still goes through this step, but its input is now an accepted slot.

**Never compute a place by hand on a scene with floors.** The first version laid its own grid around the frame, in the manner of Fords of War's spawn block. On the first run at Helm's Deep, 64 of the 101 defenders stood below the wall line, with a median height of 0.0 against a frame at 26.5, and 3 died from the fall. Whether 0.0 came from a collider, from the terrain or from a miss was not traced.

**The read-back:** `[S1] spawn level` records each soldier's height in the tick the soldier spawns, and prints each class with its frame, `far`, `off the navmesh`, `on the frame point` and a `z` spread. It says `ok` when every soldier is within 10 m of the height the scene authored for that class and on a navmesh. The unattended judge fails on `spawn level WRONG`. **`ok` does not mean on the walls.** The tolerance cannot separate a terrace at 26.5 from a gate at 20.5.

**The pile:** the Helm's Deep terrace runs out of slots, so the engine stands the rest of the foot on one point: 41 of 68 in a garrison of 100, 64 of 115 in a garrison of 170 (the slots grow with the batch's planned shape, from 27 to 51). `[S1] spawn pile` names this when more than 6 of a class stand within 1 m of the frame point (a shield wall at spacing 0 can put 5 healthy soldiers there). A pile fails nothing, and they spread out by themselves: by t 30 s all 101 stood high. It looks wrong to a person, so Phase 1 needs a muster place with room. Reinforcements can pile on their own frame too (24 of 24 in wave 2, 2 of 16 in wave 3 of the same run), because `Reinforce` keeps the live formation's shape.

**A healthy Helm's Deep:** the defender archers' frame stands at height 1.5, so `[S1] after initial spawn` reads `high` of 68 of 101, with a `z` minimum near 1.3. That is the scene's frame and not the fault repeating. `[S1] frames` prints all six frames.

### How a wave ends

No wave retreats, so a wave ends when no attacker stands, and that does not happen by itself. Only `BattleEndLogic` ends a vanilla siege, the player's exit aside: on a depleted side, or a side wholly running away (`BattleEndLogic.cs:198-222, 279-361`). Kills, a morale rout, the power retreat and the sally lead there. This mode leaves that logic out. On the first unattended run the last 7 attackers, then 6, and the last 31 defenders stood apart from t 420 s to the limit at t 1,500 s.

**The hidden gate at Helm's Deep:** the scene holds a hidden inner gate at height 32.5 (`aserai_castle_gate_inner_l1 visible="false"`, `scene.xscene:5971`). The engine takes it as `InnerGate` with no visibility check (`TeamAISiegeComponent.cs:54-57`), and a hidden gate never ticks, so it never opens (`CastleGate.cs:619-622`). The Middle lane then stays closed (`SiegeLane.cs:355-364`), and attackers on the gate lane go to that gate and stay in `AttackEntity` (`BehaviorAssaultWalls.cs:161-166`). In all three runs the first attacker at height 32 or more appeared 20 s after the outer gate fell. Defenders hold their lane place on plain Move orders, and they hunt only with over 30 attackers inside (`TacticDefendCastle.cs:975`). Constant team power turns off the retreat and the sally, which are the other ways out. So `Boot` breaks any hidden gate and logs `[S1] hidden gate`. It is not removed, because a null `InnerGate` throws in `BehaviorAssaultWalls`. **A campaign siege on this scene meets the same hidden gate**, and nobody has checked what it does there. **Breaking it works** (run `20261103-235325`, build `985b0d54e`, defaults): `[S1] hidden gate at (133.4, 180.7, 32.5): broken`, and `Middle open True` once the outer gate falls. Waves 1 and 2 of 110 cleared at 349 s and 669 s with no `stale` line, against 465 s and 840 s before. Wave 3 went quiet with 44 attackers left, and the charge ended it at 1,254 s, with no soldier felled by rule.

`WatchStalemate` is the rule for every other stand-off:

1. With half the wave down and no loss on either side for 60 s, or no loss for 180 s at any count, `[S2] wave N stale` prints the attackers' places and each formation's behavior, side, order and count inside, and both teams change to vanilla's `TacticCharge`. That tactic only weights `BehaviorCharge`, which returns 0 until the walls test passes (`BehaviorCharge.cs:169-201`), so it moves soldiers on some lanes and not on others, and a formation under a player's orders does not move at all.
2. After 90 s more with no loss, `[S2] wave N: nobody reached the last M attackers` prints their places, and they fall (`Mission.KillAgentCheat`). One soldier can stand for good: in wave 2 of the passing run the last attacker never left the spawn area, at (260.2, 305.9, -0.8). Only stragglers fall: at most a tenth of the wave, and fewer than the defenders. Otherwise the run ends as `stalemate`: a clear must be earned, and at Minas Tirith the first form of this rule felled 35 attackers with 2 defenders left. The judge's result counts the waves that ended by this rule.
3. At the wave's clear, the defenders get a new `TacticDefendCastle`, and they start the next wave out of place: `high` read 46 at the start of wave 3, and 163 of 170 about 15 s later. The next wave's new `TacticBreachWalls` comes when its queue empties, so the `tactics` text still says `TacticCharge` for the attackers until then.

The rule fired in waves 2 and 3 of the passing run. A wave of 110 lost its last soldier at about 840 s and 1,380 s of mission time, 90 s and 128 s after the `stale` line. Soldiers can still flee on low morale, and Phase 1 closes that. The state lines of the four runs count none, and one `OnAgentPanicked` warning fired at Minas Tirith.

### The terrain shader dialog at Helm's Deep

A Helm's Deep load can stop at a native dialog: `Error compiling shader file: $BASE/Shaders/Sources/pbr_terrain.rs` (and `pbr_terrain_gbuffer.rs`), X4008 at `terrain_pixel_functions.rsh:818`, raised to an error by X3129. **Press Cancel.** The mission then loads in seconds. Retry compiles the same source (native `FUN_1800934e0` returns to `D3DCompileFromFile`), and Cancel returns a null blob. Two native flags skip the dialog, and nobody has read what sets them. The dialog holds the game, and nobody is there in an unattended run, so `tools/run_long_defeat_spike.py` presses Cancel by itself and logs that in `runner_log.txt`. Left open for 304 s, the dialog makes `BattleLoadStallWatchdog` write a `[CrashReport]` block, which fails the judge.

Open: Mike's by-hand launches at 17:26 and 17:28 loaded in under 10 s and show no sign of this dialog, and all three unattended launches at this fortress met it. The runner's `shader cache list differs` does not explain that, because it is on all four runs, and Minas Tirith had no dialog. Nobody has seen how the terrain looks after a Cancel.

### Decisions the spike makes for now

- **Power never orders a retreat.** The constant keeps both power ratios near 1, so `TacticBreachWalls` never takes its one-way retreat on power, and `TacticDefendCastle` never sallies on it. No wave retreats in this mode (Mike, 2026-11-03). The constant closes only the team retreat: a soldier can still flee on low morale, so Phase 1 adds attackers to `TaomCustomBattleMoraleModel.CanPanicDueToMorale` and checks `FellFoesAdapter.SendOff`, which calls `Retreat` directly.
- **Waves are larger than 100.** At 100 attackers or fewer, `TacticBreachWalls` stops using its machines. The by-hand default is a wave of 120 and a garrison of 100. The unattended default is 110 and 170, because 100 defenders held on one run and all died on the other, with about 45 attackers left, and the between-wave tests need a cleared wave. This is no balance measure.
- **Each wave gets a new tactic.** The first spawn batch latches a shock assault and clears `ForcedUse` on the engines. The runner sets `ForcedUse` back and adds a new `TacticBreachWalls` when the wave's queue empties.
- **The garrison refills between waves,** so a later wave's evidence does not depend on the first wave's losses.
- **All in-mission tests run after wave 1:** S5, S7, then S4. Every later wave then meets the repaired gate.

### The spikes

The run answers these through tagged lines in the TAOM log. `[S3]` lines also list the ladders, towers, rams, gates, siege lanes and defender engines.

| Spike | Log lines | What it proves | Status |
|---|---|---|---|
| S1 Boot | `[LongDefeat][S1]`, `[S1 boot]`, `[S1] spawn level` | The mission opens without those behaviors, deployment finishes, you have an agent with an order bar, and each batch stands near its frame's height and on a navmesh. A pile is named and fails nothing | **Passed** (runs `20261103-215943` and `20261103-235325`) |
| S2 Waves | `[S2] wave N starts`, `all N spawned`, `wave N CLEARED`, `reinforcements`, the 5 s state line | Several waves in a row, attackers climb, defenders man the walls | **Passed:** three waves of 110 cleared at 465 s, 840 s and 1,380 s, each refilling the garrison. Attackers climb, with at most 49, 57 and 33 high. No soldier fled |
| S3 Engines | `[S3]`, `[S3 wave cleared]` | What ladders, towers and rams do across waves | **Passed:** both towers reach the wall by 191 s and the ram breaks the gate by 261 s. The towers stay at the wall, and later waves climb them. Helm's Deep has no ladders |
| S4 Gate repair | `[S4] before`, `broken`, `repaired` or `NOT repaired` | Vanilla's `CastleGate.OnMissionReset` gives a closed gate that the AI attacks again | **Passed:** `repaired: hit points 15000.0/15000.0 destroyed False disabled False open False`, and wave 2's ram broke that gate again by 551 s |
| S5 Engine buy | `[S5] deployed` or `NOT deployed`, and `bought` on the `[S3]` line | `DeploymentPoint.Deploy` works in mid-mission and a crew can use the engine | **Partly.** Minas Tirith: deployed and visible at every level, 11 standing points, and one soldier used it for about 20 s of the wave after, then none. Helm's Deep read `visible False`: all four of its defender spawners are hidden in the scene (`scene.xscene:15335`, `:8948`), and `DeploymentPoint` sets only the engine or its direct parent. `SiegeWeapon.TickAux` assigns a crew, and the engine registers that tick only if the weapon is visible at `OnInit` (`SiegeWeapon.cs:147, 157-166`), so a Helm's Deep engine also needs its spawner made visible and `SetScriptComponentToTick`. `TacticDefendCastle` clears `ForcedUse` when it stops its engines (`:1062-1076`), so the runner now sets it again each wave. The `[S3]` line carries `forced`, `user formations` and `walking to it`, to settle why the crew left |
| S6 High tower | | Posted archers hold on recorded frames and fire | **Not built** |
| S7 Armoury | `[S7] target`, `before`, `after the swap`, `after the armour write` | The swap and a driven armour write. **The proximity prompt, the panel and a TAOM race body are not covered** | **Passed on a human body:** driven armour went from 50/80 to 41/38 after the write. The prompt, the panel and a race body are owed |
| S8 Cost | The `frame mean` and `frame max` values on the 5 s state line | Frame time and load time at 300 agents | **Passed.** Minas Tirith at 300 soldiers: the mission loaded in about 10 s, with no shader dialog, and wave frame means were 14.3 and 13.0 ms, with a worst frame of 66.7 ms. Helm's Deep at 280: means of 10.8 to 11.3 ms, the same worst |
| S9 Mounted class | | A mounted player outside the walls can ride, the attackers react, and the player returns to the keep. Owed before Phase 2. Fallback: the Cavalry class rides in the courtyard only | **Not built** |

Expect `defender detachment False` on the `[S4] repaired` line: `UsableMachine.Disable` destroys the gate's detachment and the reset does not remake it. That is no failure of the repair, because `Formation.cs:2517-2525` makes a detachment on demand.

## Configuration

No MCM and no ModuleData. An unattended run takes an arm file of `key=value` lines: `fortress`, `waves`, `wave_size`, `garrison`, `intermission_seconds`, `live_limit_seconds`, `defenders_ai`, `quit`, `out`.

## Key Files

- `Main/Features/LongDefeat/LongDefeatModule.cs`, `ILongDefeatGameManagerFactory.cs`, `NullLongDefeatGameManagerFactory.cs`, `LongDefeatMission.cs`: these stay after Phase 1.
- `Main/Features/LongDefeat/Spike/`: the launcher, runner, logic, options, fortress table and console command. Phase 1 replaces this folder with a pure domain and adapters, so its direct engine access and the runner's lack of unit tests are deliberate.
- `Client/Features/LongDefeat/`: the game manager and factory (ADR-013), the mission views, the autostart and the view that declines the focus-lost Escape menu in an armed run.
- `tools/run_long_defeat_spike.py`: the unattended wrapper on `tools/run_fords_autorun.py`.

## Dependencies

- **Troops:** the `fow_*` troops in `Main/_Module/ModuleData/fords_of_war/fords_troops.xml`, which load in a CustomGame, and the stub lords in `characters/custom_battle_lords.xml`. The launcher refuses a fortress with an unknown troop.
- **Scenes:** `taom_rohan_castle_helms_deep_forceatmo` and `taom_gondor_town_minas_tirith_forceatmo` in the unversioned `TAOM_Map`.
- **Co-op:** absent. The launcher adds the only behavior, so no other mission carries it. A dedicated server keeps the null factory.

## Tests

- `TAOM.Tests/Features/LongDefeat/`: the options parser, the module shape, the fix pins, and the fortress table against the shipped data.
- `tools/tests/test_run_long_defeat_spike.py`: the judge, the arguments, and the strings shared with the C# side.
- The runner has no unit test. The proof is a game run.

## How to verify in game

**Preconditions:** a build that holds the spike, the main menu with no game loaded, cheat mode on, and the console open.

1. Enter `taom.ld_start helms`. The reply starts with `Starting:`, and the Helm's Deep siege scene loads. The defenders wait for your orders in this path, except that a formation that empties passes to the AI by engine rule and stays there after the refill.
2. Look for `[LongDefeat][S1] after initial spawn` in the newest `taom_debug_*.log`. It must say `deployment finished True`, and the order owner must be your lord. Every `[S1] spawn level` line must say `ok`. Any `spawn level WRONG` line is a failure.
3. Wait for each `[LongDefeat][S2] wave N CLEARED` line, or order your troops to fight. `[S2] run over:` names why the run ended, and the limit is 1,800 s. A `fault in phase` line, or a game that stops, is a failure: read the line and the crash report.
4. After wave 1, read the `[S5]`, `[S7]` and `[S4]` lines. They must say `deployed`, `after the armour write` and `repaired`. `NOT deployed` or `NOT repaired` is a failed spike.
5. In wave 2, watch the `[S3]` lines: the first gate's hit points must fall again if a ram reaches it.

**Unattended:** `python tools/run_long_defeat_spike.py --fortress helms`, under the gate in [unattended-game-runs.md](../reference/unattended-game-runs.md). Exit 0 needs every wave cleared, the S1, S2, S4, S5 and S7 lines present, and no `spawn level WRONG` line. It judges no other value on those lines, so read them. The first pass: `python tools/run_long_defeat_spike.py --fortress helms --wave-size 110 --garrison 170`, in `E:\Temp\long-defeat-runs\20261103-215943`. That run set `--live-limit 1800`, now the default. Minas Tirith, `--fortress minas --waves 2 --wave-size 120 --garrison 180`, in `E:\Temp\long-defeat-runs\20261103-222355`, before the straggler limit: its wave 2 is not a real clear, and the same command should now end as `stalemate`.

An unreachable step is reported with the command tried and the unmet precondition. Never mark it verified through another path.

## Decisions for the later phases

Mike decided these on 2026-11-03. The full text is in [the #1079 design comment](https://github.com/haterade22/TAOM/issues/1079#issuecomment-6617849770).

- **No wave retreats.** A wave ends when every attacker is dead.
- **General:** the player leads the defenders, and a bought hero takes over while the player is down.
- **Commanders:** waves 4, 8 and 12 and on bring a captain, a lieutenant, then a general, with abilities on a cooldown. Every faction's waves, commanders and summons use its own troops.
- **Classes:** Sword and Board, Two Handed Infantry, Archer, Cavalry. Each has a start kit from the faction's own gear and its own boon pool. Cavalry is a sally force from a recorded point outside the walls, because a vanilla siege spawns everyone on foot (S9).
- **Boons:** pick 1 of 3 after every 5 cleared waves. A boon lasts until the run ends. This is an assumption Mike may correct. The design reference is a comparison only, and its provenance row lands with the first commit that builds boons.
- **Affixes:** named powers on the attackers, rolled for each wave: 1 from wave 5, 2 from wave 10, 3 from wave 15.
- **Endless scale:** a wave cannot exceed the battle size, so each wave also raises health and damage, and the troop tiers rise.

## Notes for Phase 1

- **Where archers start:** Helm's Deep authors the defender archers' frame on low ground. Phase 1 keeps that, or posts them on recorded frames (S6).
- **Leaving the mission:** holding Tab for 0.6 s ends the battle and pops to the Custom Battle screen, because no view clears `IsFriendlyMission`. The real manager needs Fords' `ReturnToMainMenu` and an answer to Tab. The Escape menu's own exit already goes to the main menu.
- **Quit in a live mission:** works. `Utilities.QuitGame` with the mission still running exited with code 4294967295 and a clean-unload line on all four unattended runs.
- **The shader dialog:** a player would meet it too (see above). Phase 1 needs an answer before a menu entry lands: a cached variant, or a guarded copy of line 818.
- **The stalemate rule:** carry `WatchStalemate` into the domain, with its thresholds in JSON. `KillAgentCheat` credits the kill to the main agent, or with none to the victim's last hitter (`Mission.cs:6220`, `Agent.cs:4689-4693`). Prefer `Agent.Die` (`Agent.cs:4678`), which credits no main agent. A charge can send a formation to a closed inner gate (`BehaviorCharge.cs:25-31`). With the hidden gate broken at `Boot` and repairs limited to visible gates, that needs a `[S1] hidden gate ... NOT broken` line, or a scene with a visible inner gate that a repair closes.
- **Muster place:** record one with room for the garrison, because the terrace frame piles most of it.
- **Trolls and repaired gates:** `CreatureSiegeRules` latches a gate as destroyed for good, so a troll would treat a repaired gate as open. Clear the track on a repair, or stand the role down in this mission.
- **The Nine:** `DreadMissionGate` admits only Fords by name, so the Nine project no dread here.
- **The buy point:** `DeploymentHandler` is absent, so the placeholder of the live deployment point stays visible until something deploys on it, except at Helm's Deep, whose scene hides it (`scene.xscene:1552`).

## Changelog

- 2026-11-03: the Phase 0 spike lands on trunk at Mike's word, ahead of its first in-game run, so that F5 in the main checkout carries it.
- 2026-11-03: first in-game runs. S1's boot and S3's engines work. The garrison spawned in the caves, so every spawn now takes vanilla's path, and `[S1] spawn level` fails a run that repeats the fault.
- 2026-11-03: first unattended runs. A quiet fight now ends by rule (`WatchStalemate`), the wrapper answers the shader dialog, and a pile has its own line. The run passes with S2, S3, S4 and S7 proved on Helm's Deep.

## GitHub Issue

- **Issue:** [#1079](https://github.com/haterade22/TAOM/issues/1079), The Long Defeat: wave survival mode on the siege maps
- **Status:** Open
