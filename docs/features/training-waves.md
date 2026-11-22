# Training Waves (a wave fight with bets at the Training Master)

## Overview

The Training Master of a town offers a second fight beside vanilla's practice fight: waves of the town's own fighters, one after
another, all against the player, in practice weapons. Before each wave the player wagers gold on himself. A cleared wave pays the wager
back with odds (1.5 times on the first wave, 2 times from the sixth), and the limit on the next wager rises. A lost wave, or leaving the
yard in the middle of one, gives that wager to the town. Between waves the player may stop and keep his winnings. The risk is gold only: the
hero is not killed in the yard (the reasons are under "Readers of the controller's state" below). Single-player only; the place is the Training Grounds (#1527).

## Why This Exists

- **Vanilla behavior:** the Training Master offers one practice fight: thirty fighters in turn, no stake, a prize of up to 250 denars
  for beating them all (`ArenaMasterCampaignBehavior.cs` :309-366). A tournament bet exists, but only inside a tournament
  (`TournamentBehavior`).
- **TAOM requirement:** a repeatable gold sink and a gold prize for a rich hero, against fighters of his own town, without a new scene
  and without risking the hero.
- **Without this feature:** the Training Grounds offer nothing to a hero past the first fights; the town's gold never moves.

## Architecture

### Design Challenge

The fight has to happen inside vanilla's "ArenaPracticeFight" mission, whose `ArenaPracticeFightMissionController` already keeps six
practice fighters in the yard and respawns them on its own tick. The controller's members are private, and its flow assumes the player
fights only through its own `StartPlayerPractice`. A second fight in the same mission must stop that controller from spawning without
breaking the state its own dialogue, HUD and teleport read.

### Solution Approach

No Harmony patch. A TAOM `MissionLogic` (`TrainingWavesMissionLogic`) joins the mission and, where the practice controller exists, hands
the yard to a `WaveFightDirector`. The campaign side (`WaveBetService`, the dialogue lines) owns the run, the gold and the day ledger.

The wave start, read from vanilla's source (`ArenaPracticeFightMissionController.cs`, v1.5.7):

1. **Stand vanilla down.** Fade a copy of `_participantAgents`, then write `_spawnedOpponentAgentCount = 30`. Vanilla's tick spawns
   only while that count is under 30 (:197), and it resets the count to 0 only inside that same spawn branch (:203), so at 30 it
   spawns nothing and never resets. Its end check needs `IsPlayerPracticing` (:208), which a wave never sets.
2. **Swap the player's body.** Fade `Agent.Main`, then spawn the player on `Mission.PlayerTeam` with the practice kit (weapons of
   `weapon_practice_stage_<n>_<culture>`, armour from `TournamentModel.GetParticipantArmor`, copied as :325-354 does). The set is
   the town's culture, then the troop's culture, then `empire`, the last fallback vanilla itself takes (`ArenaPracticeFightMissionController.cs` :328).
3. **Spawn the fighters** one for each tick on a team of the adapter's own, with a `SimpleAgentOrigin` (its `SetWounded` is empty) and
   no horse, up to the wave's at-once limit; one more after the refill seconds whenever one falls.

The way back reverses it: fade the survivors, invoke vanilla's `StartPractice()` (:289-314) and its `SpawnPlayerNearTournamentMaster()`
(:115-123), find the master as :225 does and start his conversation through `MissionConversationLogic`. After a clear, a loss or the
last wave the master greets the player with a debrief line at priority 120 on the `start` token; vanilla's own post-fight line
(`arena_master_meet_start_2`, which pays the practice prize inside its condition) needs `AfterPractice`, which only a practising player
sets, so it is never asked.

What the engine source says about the four things the design leans on (each read from the v1.5.7 install):

| Question | Answer |
|---|---|
| `Mission.Teams.Add` for the ninth team | `TeamCollection.Add` (`Mission.cs` :739-785) assigns `Attacker` and `AttackerAlly` only when they are empty, and vanilla has filled both, so the new team is a plain list entry. The managed `SetRelations` (:829-840) makes a new Attacker team the enemy of the Defender side only, which here is the player's team. No fixed team limit exists (the native add-team routine grows its table). Its relation to the other Attacker teams and to the neutral team is the native default, which was not verified, so the adapter sets the neutral teams friendly itself; the vanilla teams are empty while a wave runs. Fords of War also adds a team in the middle of a mission (`FordsTowerAdapter.cs`). |
| `FadeOut` and `OnAgentRemoved` | `Agent.FadeOut` is one native call (`Agent.cs` :4304). Its native body sets the agent's state to 2 (Routed) and then calls a managed callback with the mission, the agent index, 0, the state and a blow before it returns. That has the shape of `Mission.OnAgentRemoved` (:3000), and Long Defeat's notes read the same slot as the removal callback; the slot-to-name mapping was not re-derived here. |
| `StartPractice()` and agents it did not spawn | It fades only `_participantAgents`, mounts and player-controlled agents (:292). A wave fighter is none of these and survives it, so the adapter fades the fighters itself first. The player's wave body is player-controlled and is faded. It then zeroes both counts, clears corpses and spawns six practice fighters again (:306-312). |
| `SpawnPlayerNearTournamentMaster()` with a main agent | It does not look: it always spawns a new player-controlled agent. A new body with the Player controller becomes `MainAgent` as it is built (`Agent.cs` :1255), so the order is fixed: `StartPractice` fades the wave body first, then this spawns the new one. |

Readers of the controller's state that a wave touches, each checked:

- The hero's safety. `SimpleAgentOrigin.SetWounded` and `OnAgentRemoved` are empty, but `SetKilled` is not: it calls
  `KillCharacterAction.ApplyByBattle` on a hero (`SimpleAgentOrigin.cs` :144-162). The safety rests on two facts. The yard's
  `ArenaAgentStateDeciderLogic` always answers Unconscious, and it is the first `IAgentStateDecider` of this mission
  (`Mission.GetAgentState` takes the first one, :5040-5055; `OpenArenaStartMission` lists it and no TAOM type implements the
  interface). And no behavior in this mission's list calls `SetKilled`: its only vanilla callers are `BattleAgentLogic` (on a Killed
  state) and `CustomBattleAgentLogic`, and `OpenArenaStartMission` lists neither.
- The practice HUD reads `RemainingOpponentCount`, which is not meaningful during a wave, but its panel is visible only while
  `IsPlayerPracticing` (`SandBox/GUI/Prefabs/ArenaPracticeFight.xml`), so it stays hidden.
- `OnAgentRemoved` (:248-265) decrements `_aliveOpponentCount` for every removed human that is not the main agent, so it goes negative
  during a wave. `StartPractice` resets it to 0 on the way back, after the adapter has faded the survivors.
- `OnScoreHit` (:160-180) pays practice-fight combat XP for the player's hits on the wave's fighters. That is intended.
- `Mission.OnEndMissionRequest` asks every logic in turn; vanilla's controller asks only while practising, so TAOM's logic asks during
  a fight, and `BasicLeaveMissionLogic` still refuses to let the player leave with an enemy within 5 m.

### Fault policy

A throw in the director's tick is caught by the mission logic, logged once and handed to `WaveFightDirector.Fault()`. A fault must
never leave the player in a fight with his stake held, so it settles the run as a refund, not as a loss: a fight the fault ended gives
the stake back (`WaveRun.Abort`, `WaveBetService.AbortFight`; the town gets nothing), a due wave is refunded, a pause between waves
ends with the winnings. The offer is hidden for the rest of the mission, and when vanilla's yard had been stood down the player is taken
back to the master. Every refund is announced at the top of the screen with the notice `taom_tgw_an_refunded` ("The wave was called
off. Your wager of N denars is returned."), through one method (`WaveFightDirector.AnnounceRefund`): the three refunds of a wave that
cannot start (no troop in the town, vanilla's yard cannot be stood down, the player's body cannot be built), a wave in which no fighter
could be spawned, a mission that ends while a wave is due, and a fault that gave a stake back. The notice is shown before the player is
taken back, so a return that throws cannot hide it. The stand-down flag goes up before the stand-down
call, so a throw after the fighters were faded still takes the player back. The flag falls only when the return to the master has come back, so if that return throws part way the fault path tries it once more; if the second try throws too, the player still has the leave key.

### Component Diagram

```
training_waves.json            MCM: Wave Fights at the Training Grounds
        |                                  |
TrainingWavesConfigProvider     TrainingWavesSettingsProvider
        \                                  /
         WaveBetService (run, gold, day ledger, offer, debrief)
          |                                  |
 TrainingWavesDialogBehavior          WaveFightDirector (waves, spawns, return)
 (dialogue lines, SyncData)                   |
                                       IArenaWaveArena
                                              |
                              ArenaWaveArena + ArenaPracticeControllerAccess
                              (the only engine touch: spawns, fades, controller)
                                              |
                              TrainingWavesMissionLogic (MissionLogic, thin)
```

## Configuration

### Config File: `Main/_Module/ModuleData/training_grounds/training_waves.json`

`rules` and `waves`. The reader never throws: a row that breaks a rule is skipped with one warning, a row that can be adjusted is
adjusted, a rule outside its range keeps its default, and the reader accepts at most 20 rows (it warns and ignores the rest). A top-level
key other than `rules` and `waves` gets one warning each (a misspelt key is the usual cause), and a file with no `waves` array warns once
and takes the default waves. No valid row means the compiled default table. The
file is read once per process, so an edit needs a restart. A test pins that the shipped file reads with no warning and equals the
compiled default.

| Field | Type | Meaning |
|-------|------|---------|
| `rules.minStake` | int, 1 to 1,000 | The smallest wager the table allows. The smallest wager on offer is the first of the three stakes (a quarter of the wave's limit, rounded down to tens, so 120 on wave 1), and the purse must hold that much. |
| `rules.hardCeiling` | int, 100 to 1,000,000 | The largest wager on any wave, before the perk factor; lowered to the MCM tournament bet cap when that is above 0. |
| `rules.oneRunPerTownPerDay` | bool | One run for each town for each campaign day. |
| `rules.returnSeconds` | int, 1 to 30 | The wait after a wave ends before the player is taken back to the master. |
| `rules.refillSeconds` | int, 1 to 30 | Once the first `atOnce` fighters are in, at least this many seconds pass between two spawns, counted from the last spawn, not from a fall: a fighter that falls late is replaced on the next tick. |
| `waves[].count` | int, 1 to 60 | Fighters in the wave. |
| `waves[].atOnce` | int | At most this many stand at once; above `count` it is lowered to `count`. |
| `waves[].tiers` | object of tier 3, 4, 5 to weight | The weight of each troop tier in the draw; a tier the town lacks falls back to the nearest lower one it has, then the nearest higher. |
| `waves[].weaponStage` | int, 1 to 3 | The practice weapon set (`weapon_practice_stage_<n>_<culture>`, falling back to `empire`). |
| `waves[].cap` | int, 10 to 1,000,000 | The largest wager on this wave; a cap below the previous wave's is raised to it. |
| `waves[].oddsTenths` | int, 11 to 40 | 15 pays the wager back 1.5 times. |

The MCM switch `EnableTrainingWaves` ("Wave Fights at the Training Grounds", group Tournaments, default on) turns the offer off. It is
`PlayerLocal` in `CoopSettingsRelevance`. The MCM tournament bet cap also limits a wave wager, and a cap that leaves no wager of at least the minimum hides the offer; the setting's hint says a very low cap does.

### Current Values

| Wave | Fighters | At once | Tiers 3:4:5 | Weapon stage | Cap | Odds |
|---|---|---|---|---|---|---|
| 1 | 3 | 1 | 1:0:0 | 1 | 500 | 1.5 |
| 2 | 5 | 2 | 1:0:0 | 1 | 1000 | 1.6 |
| 3 | 6 | 2 | 2:1:0 | 2 | 2000 | 1.7 |
| 4 | 8 | 3 | 1:2:0 | 2 | 4000 | 1.8 |
| 5 | 10 | 3 | 0:2:1 | 2 | 7500 | 1.9 |
| 6 | 12 | 4 | 0:1:1 | 3 | 12000 | 2 |
| 7 | 14 | 4 | 0:1:2 | 3 | 15000 | 2 |
| 8 | 16 | 5 | 0:0:1 | 3 | 15000 | 2 |

Rules: table minimum 10 (the smallest wager on offer is higher: 120 on wave 1), ceiling 15,000, one run for each town for each day (a run that ends in a refund before a wave is cleared gives the town its day back), 5 s before the return, 3 s to refill. The wager limit
is the smaller of the wave's cap and the ceiling, times the Deep Pockets factor of the player (1 to 10). A full run at every cap pays
111,200 against 57,000 staked, a net of 54,200, and 108,400 with a factor of 2. A test pins both numbers.

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TrainingGrounds/Waves/Domain/` | `WaveRun` (the state machine), `WaveTable`, `WaveRules`, `WaveRow`, `StakeOptions`, `WaveRoster`, `WaveDayLedger`, `SpawnPicker`: whole numbers, no engine type |
| `Main/Features/TrainingGrounds/Waves/Config/WaveTableReader.cs` | Validating reader of the JSON |
| `Main/Features/TrainingGrounds/Waves/WaveBetService.cs` | The offer gates, the stake, every move of gold, the run, the debrief |
| `Main/Features/TrainingGrounds/Waves/WaveFightDirector.cs` | The mission side: wave start, spawn rhythm, wave end, return, fault |
| `Main/Features/TrainingGrounds/Waves/Hooks/TrainingWavesMissionLogic.cs` | The `MissionLogic`: tick, leave prompt, mission end |
| `Main/Features/TrainingGrounds/Waves/Hooks/TrainingWavesDialogBehavior.cs`, `TrainingWavesDialogLines.cs`, `TrainingWavesTexts.cs`, `TrainingWavesAnnouncementTexts.cs` | The master's lines, their English, the four quick notices |
| `Main/Features/TrainingGrounds/Waves/TrainingWavesModule.cs`, `TrainingWavesIoC.cs` | Module: services, the campaign behavior (built with `new` for each campaign), the mission logic |
| `Main/Adapters/IArenaWaveArena.cs`, `ArenaWaveArena.cs` | The yard of one mission: spawns, fades, the way back |
| `Main/Adapters/ArenaPracticeControllerAccess.cs` | The four private members of vanilla's controller, bound once |
| `Main/Adapters/ITrainingWavesAdapter.cs`, `TrainingWavesAdapter.cs` | Day, town, player state, partner, vanilla flags, the gift to the town |
| `Main/_Module/ModuleData/training_grounds/training_waves.json` | The bet table |
| `tools/ingame_checks/1529.toml` | The in-game check manifest |

## Dependencies

- `ICareerPurseAdapter` (CareerSystem): `ChargeGold` takes the wager, `GrantGold` pays.
- `ITournamentRewardsSettingsProvider`: the tournament bet cap (`MaxBetPerRound`).
- `ICoopSessionProvider`, `IDedicatedServerProvider`: the single-player gate, `CoopSessionPolicy.IsSinglePlayerAuthority`.
- `IOutcomeLog`: the nine `[Check]` lines.
- `CrewAgentBudget` (ElephantLike): the capacity check a direct `Mission.SpawnAgent` needs (#1107).
- `AgentSlotIdentity` (AdvancedCombat): the check on a held fighter handle before it is read or faded.

### Co-op

Single-player only. The mission logic is `CoopMissionRole.Unsafe` (not added in a session), and the offer and
`PlaceStake` read `IsSinglePlayerAuthority` live (`PlaceStake` through the offer's refusal). The payout, the refund, the abort, the
forfeit and the town credit do not re-read it: a run that began as single-player settles as one. See [coop-interop.md](coop-interop.md), "Mission behaviors".

## Tests

- `TAOM.Tests/Features/TrainingGrounds/Waves/WaveRunTests.cs`, `WaveDomainTests.cs`, `WaveTableReaderTests.cs`: the state machine, the
  table, the reader's rules and the shipped file.
- `WaveBetServiceTests.cs`, `WaveBetDialogueStateTests.cs`, `TrainingWavesBehaviorTests.cs`, `TrainingWavesDialogLinesTests.cs`,
  `TrainingWavesTextsTests.cs`, `TrainingWavesAnnouncementTextsTests.cs`: the offer gates, the stake, the money moves, the lifecycle,
  the lines and their priorities, the texts.
- `WaveFightDirectorTests.cs` with `FakeArena.cs`: when a wave starts and waits, the spawn rhythm (the refill clock runs from the last spawn) and the at-once limit, the refund notice, a clear, a
  loss and a loss in the same tick as a clear, the return and the debrief flag, the mission end, the fault path, the exact lines the
  manifest reads.
- `TrainingWavesMissionWiringTests.cs`, `TrainingWavesWiringTests.cs`: the module's declarations, `: MissionLogic`, the guards around
  the director, the four reflection members and their catalogue rows.
- Shared gates: `CoopMissionRolePolicyTests`, `ReflectionSiteBindingTests`, `CrewAgentBudgetTests`, `EntryPointLineCountGateTests`,
  `ServiceSignatureGateTests`, `BannedConstructsGateTests`, `FeatureModulesTests`, `TrainingGroundsTextTests`.
- The fake yard proves the director's decisions. It cannot prove what the engine does with a spawn, a fade or a team: that is the list
  below.

## How to verify in game

**Preconditions:** a build with this change deployed (the version on the branch), a campaign in a town with no tournament, a healthy
and undisguised hero with at least 120 denars (the smallest wager on wave 1), "Wave Fights at the Training Grounds" on (the default), no co-op session.

1. Walk to the Training Master and talk. The wave option shows; with a tournament, a wound, a disguise, a run already today or under
   the smallest wager (120 on wave 1) it is greyed with its reason. Proof: `taom.training-waves.offer` (`result=pass town=...`, or `result=skip reason=...`).
2. Take the offer, read the rules, pay a wager, end the conversation. Proof: `taom.training-waves.stake`, then the vanilla practice
   fighters leave the yard and `taom.training-waves.stand-down` (`faded=N vanilla_spawned=30`). A person checks that none of them
   comes back during the fight.
3. Fight wave 1: three fighters, one at a time. Proof: `taom.training-waves.wave` (`spawned=3 count=3 peak=1`) and
   `taom.training-waves.payout` on a clear. A person checks the practice kit and the notices at the top of the screen.
4. After about 5 seconds the master is in front of the player and speaks the debrief. Proof: `taom.training-waves.return` and
   `taom.training-waves.debrief` (`after_practice=false`).
5. Place a second wager on wave 2 (5 fighters, 2 at once), or withdraw. Proof: `taom.training-waves.run-end` (the gold change since the
   first wager equals the run's net).
6. Go down in a wave: the hero is not wounded, the master names the lost wager. Leave the yard in the middle of a wave: the prompt
   appears, and on confirming the wager is gone. Proof: `taom.training-waves.forfeit`, `taom.training-waves.run-end`.
7. Use vanilla's own practice fight afterwards (the town menu, or the master's old option): it must still work and pay its own prize.

What only a game run can show, because it rests on engine behaviour the source read could not settle:

- That `FadeOut` raises the removal callback at once (the fade of the wave body before the new body spawns depends on it; if it does
  not, the old body is removed later and the new one is already `MainAgent`).
- That `Teams.Add` for a ninth team mid-mission does not fault, and that the wave's fighters attack only the player. The adapter logs
  nothing for the team; a fault shows as `[TrainingWaves] mission tick threw` in the TAOM log.
- The spawn frame count of the town's yard scene (the offer hides below two) and that no fighter spawns within 12 m of the player.
- That the player's body appears in the practice kit and the hero cannot die (the decider returns Unconscious).
- That no fighter or corpse of a wave is left in the yard after the return, and that vanilla's six fighters are back and idle.

An unreachable step is reported with the command tried and the unmet precondition. Never mark it verified through another path.

## How to change the table

1. Edit `training_waves.json`: a row for each wave, in order, with `cap` not falling from one row to the next.
2. Restart the game (the file is read once per process). A bad value shows as one warning in the TAOM log and the default applies.
3. No code change. A change of the shipped values also changes `WaveTable.Default` and its tests (`TrainingWavesConfigProviderTests` pins the shipped
   file against the default).

## Performance

The director runs once each tick and does work only while a run is live; a tick with no run returns after a few reads. A fight
polls the fighters it spawned (at most five at once in the shipped table), and no `[Check]` line is written in the per-tick path.
`OutcomeLog` (`Main/Core/Logging/OutcomeLog.cs`, `Record`) writes a line the first time an (id, result, key) is seen in a game
process, so a line in the log shows that a result was seen at least once in this session, never how often.

## Changelog

- 2026-11-22: first build (#1529): the bet table and reader, the offer, stake and debrief lines, the mission side with the director and
  the yard adapter, and the nine check lines.
- 2026-11-22: review fixes (#1529): the "too poor" rule reads the smallest wager on offer, a refund shows a notice, the stand-down flag
  goes up before its call, the player's body is no longer faded when it cannot be built, the weapon set falls back to `empire`, the MCM
  hint names the tournament bet cap, and the reader warns about an unknown top-level key or a missing `waves`.

## GitHub Issue

- **Issue:** #1529: a wave fight with bets at the training master
- **Status:** Open
