# The Long Defeat

## Overview

A planned campaign-free survival mode on the siege maps: you defend Helm's Deep or Minas Tirith against wave after wave, and you spend gold between waves. **Today only the Phase 0 spike exists, and it has never run in game.** The spike has no menu entry. Only a console command in cheat mode or an environment variable starts it, so a player session never meets it. The plan and the phases are in [#1079](https://github.com/haterade22/TAOM/issues/1079).

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

### Decisions the spike makes for now

- **Power never orders a retreat.** The constant keeps both power ratios near 1, so `TacticBreachWalls` never takes its one-way retreat on power, and `TacticDefendCastle` never sallies on it. The spike therefore cannot show that retreat. Phase 1 decides whether a wave may retreat.
- **Waves are larger than 100.** At 100 attackers or fewer, `TacticBreachWalls` stops using its machines, so the default wave is 120 and the garrison is 100.
- **Each wave gets a new tactic.** The first spawn batch latches a shock assault and clears `ForcedUse` on the engines. The runner sets `ForcedUse` back and adds a new `TacticBreachWalls` when the wave's queue empties.
- **The garrison refills between waves,** so a later wave's evidence does not depend on the first wave's losses.
- **All in-mission tests run after wave 1:** S5, S7, then S4. Every later wave then meets the repaired gate.

### The spikes

The run answers these through tagged lines in the TAOM log. `[S3]` lines also list the ladders, towers, rams, gates, siege lanes and defender engines.

| Spike | Log lines | What it proves | Status |
|---|---|---|---|
| S1 Boot | `[LongDefeat][S1]`, `[S1 boot]` | The mission opens without those behaviors, deployment finishes, and you have an agent with an order bar | Built, not run |
| S2 Waves | `[S2] wave N starts`, `all N spawned`, `wave N CLEARED`, `reinforcements`, the 5 s state line | Several waves in a row, attackers climb, defenders man the walls | Built, not run |
| S3 Engines | `[S3]`, `[S3 wave cleared]` | What ladders, towers and rams do across waves | Built, not run |
| S4 Gate repair | `[S4] before`, `broken`, `repaired` or `NOT repaired` | Vanilla's `CastleGate.OnMissionReset` gives a closed gate that the AI attacks again | Built, not run |
| S5 Engine buy | `[S5] deployed` or `NOT deployed` | `DeploymentPoint.Deploy` works in mid-mission and a crew can use the engine | Built, not run |
| S6 High tower | | Posted archers hold on recorded frames and fire | **Not built** |
| S7 Armoury | `[S7] target`, `before`, `after the swap`, `after the armour write` | The swap and a driven armour write. **The proximity prompt, the panel and a TAOM race body are not covered** | Partly built, not run |
| S8 Cost | The `frame mean` and `frame max` values on the 5 s state line | Frame time. **Load time on Minas Tirith at 300 agents is not measured** | Partly built, not run |

Expect `defender detachment False` on the `[S4] repaired` line: `UsableMachine.Disable` destroys the gate's detachment and the reset does not remake it. That is an open question for Phase 2, not a failure of the repair.

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
2. Look for `[LongDefeat][S1] after initial spawn` in the newest `taom_debug_*.log`. It must say `deployment finished True`, and the order owner must be your lord.
3. Wait for each `[LongDefeat][S2] wave N CLEARED` line, or order your troops to fight. A `fault in phase` line, or a game that stops, is a failure: read the line and the crash report.
4. After wave 1, read the `[S5]`, `[S7]` and `[S4]` lines. They must say `deployed`, `after the armour write` and `repaired`. `NOT deployed` or `NOT repaired` is a failed spike.
5. In wave 2, watch the `[S3]` lines: the first gate's hit points must fall again if a ram reaches it.

**Unattended:** `python tools/run_long_defeat_spike.py --fortress helms`, under the gate in [unattended-game-runs.md](../reference/unattended-game-runs.md). Exit 0 needs every wave cleared and the S1, S2, S4, S5 and S7 lines present. It judges no value on those lines, so read them.

An unreachable step is reported with the command tried and the unmet precondition. Never mark it verified through another path.

## Notes for Phase 1

- **Leaving the mission:** holding Tab for 0.6 s ends the battle and pops to the Custom Battle screen, because no view clears `IsFriendlyMission`. The real manager needs Fords' `ReturnToMainMenu` and an answer to Tab. The Escape menu's own exit already goes to the main menu.
- **Quit in a live mission:** the unattended quit calls `Utilities.QuitGame` with the mission still running. Fords ends the game first. The first unattended run shows whether this exits cleanly.
- **Trolls and repaired gates:** `CreatureSiegeRules` latches a gate as destroyed for good, so a troll would treat a repaired gate as open. Clear the track on a repair, or stand the role down in this mission.
- **The Nine:** `DreadMissionGate` admits only Fords by name, so the Nine project no dread here.
- **The buy point:** `DeploymentHandler` is absent, so the placeholder of the live deployment point stays visible until something deploys on it.

## Changelog

- 2026-11-03: the Phase 0 spike lands on trunk at Mike's word, ahead of its first in-game run, so that F5 in the main checkout carries it.

## GitHub Issue

- **Issue:** [#1079](https://github.com/haterade22/TAOM/issues/1079), The Long Defeat: wave survival mode on the siege maps
- **Status:** Open
