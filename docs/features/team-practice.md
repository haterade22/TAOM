# Team Practice Fights

## Overview

In a town of the Free Peoples, the practice fight at the training grounds is a team fight: the player and two allied fighters, outlined in blue, face four others, and nobody can hurt a fighter of his own side. In a town of the Shadow, a neutral town, a co-op session, on a dedicated server, or with the MCM switch off, nothing is written and the fight is vanilla's free-for-all. Vanilla's prizes stay (5, 10, 25 or 60 denars by the player's own knockdowns, 250 for winning while standing); only the sides, the win condition's trigger and the Training Master's words change.

## Why This Exists

- **Vanilla behavior:** `ArenaPracticeFightMissionController` (SandBox) builds six AI teams and makes each one the enemy of the other five (`InitializeTeams`, `ArenaPracticeFightMissionController.cs:356-374`), then makes all six the player's enemies while he practises (`ArrangePlayerTeamEnmity`, :470-476). The fight ends for the player when all 30 fighters have entered and none is left (`CheckPracticeEndedForPlayer`, :316-323). The Training Master says "There are no rules, no teams."
- **TAOM requirement:** player feedback (#1528): in the cities of Sauron's forces a fight where every fighter is alone fits; in the cities of the Free Peoples soldiers would train in teams.
- **Without this feature:** every town runs the same free-for-all.

## Architecture

### Design Challenge

The team setup in vanilla is private and not virtual, so the team fight is a Harmony patch (Patch174) behind a service that decides the mode from the town's side. A TAOM mission controller in place of the vanilla one was rejected: it would copy about 550 lines of vanilla code, and the Seat of Seeing recognises an arena mission by the vanilla controller type.

Two vanilla rules assume that every other fighter is an opponent. The end rule (`RemainingOpponentCount == 0`) never fires while an ally lives, so a prefix on `OnMissionTick` writes 0 into the alive count once no fighter of the other side is left, and vanilla's own victory path does the rest. The prize rule is unchanged: `OnAgentRemoved` counts each fighter the player himself downs (:256-258), and with friendly fire off he cannot down an ally, so the count stays his knockdowns of the other side.

### Solution Approach

- **Side of a town:** the owner's, by `IAlignmentService.ResolveSide(FactionSides.Of(settlement.MapFaction))`, the rule the tournament alignment filter uses (#744). A captured town follows its conqueror. A town held by a neutral realm, or with no owner, is not a team town.
- **Teams:** the six AI teams split by index. The first `TeamPracticeRules.AllyTeamCount` (2) are the player's side, the rest the other side. Teams of one side are not enemies of each other. The constant is the one place to change the odds: half the teams (three against three) would make the 250 prize easier than vanilla.
- **Friendly fire:** `Mission.ForceNoFriendlyFire = true` for the mission. Without it a player's missile that meets a friendly agent is not cancelled in single-player (`Mission.cs:5868-5880`).
- **Allies:** outlined blue (`TeamPracticeRules.AllyOutline`, `0xFF4F8FD9`) with `MBAgentVisuals.SetContourColor` while the player practises.
- **Mode per visit:** decided once for each controller instance in `TeamPracticeState` (a `ConditionalWeakTable`), so an MCM change never leaves a fight half team and half free-for-all. It takes effect the next time the player enters the training grounds.
- **Training Master:** two dialogue lines at priority 110 (vanilla's are 100) with vanilla's input and output tokens and no consequence: the rules speech and the prize speech. A line is true only in a team fight. The prize line calls vanilla's own reward condition for `OPPONENT_COUNT_1..4` and `PRIZE_1..5`, so its figures cannot drift from the payout. A postfix on `conversation_arena_master_post_fight_on_condition` replaces `FIGHT_DEBRIEF` with the team win text when vanilla paid the top prize and the player stands (`TeamPracticeRules.ShowsTeamWin`).

### Component Diagram

```
TaomSettings.EnableTeamPracticeFights
        |
TeamPracticeSettingsProvider        IAlignmentService  ICoopSessionProvider  IDedicatedServerProvider
        \                                 |                  |                      |
         +---------------- TeamPracticeService.IsTeamTown(SideIds) ------------------+
                                          |
              TeamPracticeState (once per controller)  <-- ArrangePlayerTeamEnmity postfix
                                          |
   TeamPracticeRules (pure)  <-- TeamPracticeFight (engine glue) <-- SpawnArenaAgent postfix
                                                                  <-- OnMissionTick prefix
   TeamPracticeDialogBehavior (two lines)  /  PostFightCondition postfix (win text)
```

## Configuration

### MCM: Tournaments group, `EnableTeamPracticeFights`

| Setting | Default | Effect |
|---|---|---|
| Team Practice Fights | on | Free towns run the team fight. Off: every town is vanilla. Single-player only. |

The default is on and the setting is new, so no persisted value is flipped (`check_mcm_default_flips.py`).

## Key Files

| File | Purpose |
|------|---------|
| `Main/Features/TrainingGrounds/Teams/TeamPracticeRules.cs` | Pure rules: ally split, enemy matrix, win, outline |
| `Main/Features/TrainingGrounds/Teams/TeamPracticeService.cs` | Is this town a team town |
| `Main/Features/TrainingGrounds/Teams/TeamPracticeSettingsProvider.cs` | The MCM switch, default on |
| `Main/Features/TrainingGrounds/Teams/TeamPracticeDialogBehavior.cs` | The two Training Master lines |
| `Main/Features/TrainingGrounds/Teams/TeamPracticeModule.cs` | Feature module: services, Patch174 at GameInit, the behavior |
| `Main/Features/TrainingGrounds/Teams/Hooks/ArenaPracticeFightMissionController_TeamPractice_Patch.cs` | Three patches on the practice controller |
| `Main/Features/TrainingGrounds/Teams/Hooks/ArenaMasterCampaignBehavior_PostFightDebrief_Patch.cs` | The win text |
| `Main/Features/TrainingGrounds/Teams/Hooks/TeamPracticeFight.cs` | Engine glue for the three patches |
| `Main/Features/TrainingGrounds/Teams/Hooks/TeamPracticeState.cs` | The mode of one visit |
| `Main/Features/TrainingGrounds/Teams/Hooks/TeamPracticeAccess.cs` | The two private fields |
| `Main/Features/TrainingGrounds/Teams/Hooks/TeamPracticeTexts.cs` | The `{=KEY}` literals and the coin icon |

## Dependencies

- `IAlignmentService` (Execution): the Free, Evil or Neutral reading of the town's owner.
- `ICoopSessionProvider`, `IDedicatedServerProvider` (CoopInterop): `CoopSessionPolicy.IsSinglePlayerAuthority`.
- `FactionSides.Of` (Adapters): the owner's id and that faction's own culture, as one pair.
- `MissionThreadGuard` (AdvancedCombat): notes a native write that arrives off the main thread.

## Tests

- `TeamPracticeServiceTests`: Free, Evil, Neutral and no owner; an unlisted or Neutral-listed faction with a Free culture follows the culture; a kingdomless clan uses its own pair; switch off, session active, defer to host, not the authority, dedicated server.
- `TeamPracticeRulesTests`: the ally split for six teams, a short list has no ally, all 15 enemy pairs, the win rule per input, the debrief rule per input, the mode rule (no reflection access means not teams), "any opponent standing" over the has-bots flags (an empty list means no data, so true), the outline rule.
- `TeamPracticeStateTests`: the decision runs once per controller, controllers are independent, a throwing decision remembers nothing.
- `TeamPracticeSettingsProviderTests`: no MCM instance reads as on.
- `TeamPracticeWiringTests`: the module is listed once, Patch174 at GameInit, the dialogue tokens equal the ones vanilla's IL registers, the priority is above 100, the patch decides the mode through `IsTeamMode` with `TeamPracticeAccess.IsAvailable` and the debrief postfix calls `ShowsTeamWin` (source-text pins).
- `Patch174TeamPracticeBindingTests`: the four targets resolve with the parameters the patches bind, the first is not inlinable, the fields have their types.
- Not unit-tested: `TeamPracticeFight` (it reads and writes native `Team` and `Agent` objects); an in-game run proves it.

## How to verify in game

**Preconditions:** a single-player campaign, no co-op, Team Practice Fights on (Tournaments). One town each of the Free Peoples, the Shadow and a neutral realm. Manifest: `tools/ingame_checks/1528.toml`.

1. In a Free town, enter the training grounds and talk to the Training Master: "Tell me how the practice fights work" says "we train the way soldiers fight: in teams". Prize question: the figures equal vanilla's (3, 6, 10, 20 opponents; 5, 10, 25, 60, 250). `[Check] id=taom.team-practice.mode result=pass mode=yard` appears when the yard fills.
2. Start a practice fight. `[Check] id=taom.team-practice.mode result=pass mode=practice` is written. Two fighters outlined blue stand beside you; four others are not outlined. Shoot an ally with a bow: no damage, no kill, and the engine shows "you hit a friendly troop" (`Mission.cs:5897-5903`, single-player, with `ForceNoFriendlyFire` the missile is cancelled).
3. Down every fighter of the other side while you stand: the fight ends with "Victory!", the Training Master pays 250 and says "Your side held the yard". `[Check] id=taom.team-practice.win result=pass` is written. `OutcomeLog` writes a result and key pair once for each game process (`OutcomeLog.cs`, `Record`), so the line proves only that a win happened at least once in this session, and a second win in the same session writes no new line.
4. Fight a second practice in the same visit and see the Victory debrief and the 250 prize again (no new log line, see step 3). The reason: the start of a practice fades out the fighters of the one before (`StartPractice`, `ArenaPracticeFightMissionController.cs:292`).
5. Repeat in a Shadow town: no blue outlines, every fighter attacks every other, the original speech. No `taom.team-practice` line.
6. Turn the switch off, re-enter a Free town: vanilla's fight and speech.

An unreachable step is reported with the command tried and the unmet precondition.

## How to change the odds

1. Change `TeamPracticeRules.AllyTeamCount` (2 of 6 today) and the expected values in `TeamPracticeRulesTests`.
2. No other code changes. The spoken text says "the fighters outlined in blue" without a number.

## Known limits

- The HUD line "remaining opponents" (`MissionArenaPracticeFightVM`) counts allies too in a team fight (#1536).
- The ally outline needs `AgentVisuals` valid at spawn; not run in game yet.
- Vanilla's `SelectRandomAiTeam` (`ArenaPracticeFightMissionController.cs:230-246`) fills the first team with no fighter, by index, so a fallen ally (teams 0 and 1) is replaced before any opponent team. Each team holds at most one fighter, so the yard holds at most two allies and four opponents.

## Changelog

- 2026-11-22: team practice fights in Free towns (#1528).
- 2026-11-22: the win count is held at zero until the player is back at the master (#1528, Mike's decision). After the team win is declared the allies stand for about five seconds; an ally who falls in that time (an arrow already in flight might do it; the source does not prove it) took vanilla's count below zero, and vanilla pays the 250 only at exactly zero. The tick patch now puts the count back for a player who won and still stood (`TeamPracticeRules.WinCountSlipped`), and writes one `[TeamPractice]` log line when it does. Not seen in play: three reviews raised the case and none could prove it.

## GitHub Issue

- **Issue:** #1528: [Team practice fights in the towns of the Free Peoples](https://github.com/haterade22/TAOM/issues/1528)
- **Status:** Open
