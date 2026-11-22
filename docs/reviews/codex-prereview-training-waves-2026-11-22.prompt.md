# Codex independent pre-review: wave fights at the training grounds (#1529), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
is building in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove from the engine
source or the data is UNVERIFIED, not a finding.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds`, HEAD af5ddb617): `git status --short` and `git diff` list it, and the new
files are untracked. It holds two features. Review both, and how they meet.

1. **Wave fights (#1529), the main subject.** `Main/Features/TrainingGrounds/Waves/` (the run rules
   under `Domain/`, the config reader, `WaveBetService`, `WaveFightDirector`, the dialogue and the
   mission logic under `Hooks/`), the adapters `Main/Adapters/ArenaWaveArena.cs`,
   `IArenaWaveArena.cs`, `ArenaPracticeControllerAccess.cs`, `TrainingWavesAdapter.cs`,
   `ITrainingWavesAdapter.cs`, the data file `Main/_Module/ModuleData/training_grounds/training_waves.json`,
   the tests under `TAOM.Tests/Features/TrainingGrounds/Waves/`, `docs/features/training-waves.md`
   (read it first) and `tools/ingame_checks/1529.toml`.
2. **Team practice fights (#1528).** `Main/Features/TrainingGrounds/Teams/` and its tests. An earlier
   review of it led to fixes in `TeamPracticeRules.cs`, `Hooks/TeamPracticeFight.cs`, both patch
   files, the tests and `tools/ingame_checks/1528.toml`: review that code as it stands.
3. **Shared files both features edit:** `Main/Features/TaomSettings.cs`, `Main/Composition/FeatureModules.cs`,
   `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/Features/CoopInterop/Missions/CoopMissionRoles.cs`,
   `Main/_Module/ModuleData/taom_module_strings.xml`, the reflection site list and its test, the
   setting count tests, `docs/features/coop-interop.md`.

## Intent (the maintainer's decisions; do not relitigate them)

- At the training master of a town the player can fight waves of the town's own fighters with
  practice weapons, with a wager on each wave. A cleared wave pays the wager back at the wave's
  odds. Between waves the player bets again or withdraws with the winnings. A lost wave, or leaving
  mid-wave, loses that wave's wager to the town. The largest wager grows with each cleared wave.
- The wave fight runs inside vanilla's "ArenaPracticeFight" mission, beside vanilla's
  `ArenaPracticeFightMissionController`, with no Harmony patch of its own. Single-player only.
- The hero must not be killed or wounded by a wave.
- The bet table and its rules are data (`training_waves.json`).

## Evidence the builder has

- A full `dotnet test` on the two features before they were joined: one failure each, the language
  coverage test for the new keys, which waits for the maintainer's language pass. After the join the
  Training Grounds tests pass (428) and a full run is in progress.
- No game run. Nothing below has been seen in play.

## What to try to disprove

1. **Money.** That no sequence of offers, stakes, clears, losses, withdrawals, refunds, faults,
   conversation ends and mission ends can pay the player twice, take a stake twice, lose a stake
   without a fight, keep a stake after a wave that never started, overflow, or leave the run's net
   different from the purse's change. Include: leaving the conversation at each step, leaving the
   mission at each state, a tournament starting, a wound, a second run in one day, two towns in one
   day, a save and load between visits, a new campaign in the same process.
2. **The mission.** That the vanilla controller is stood down and brought back correctly: walk
   vanilla's `OnMissionTick`, `StartPractice`, `SpawnArenaAgent`, `OnAgentRemoved`,
   `SpawnPlayerNearTournamentMaster`, `CheckPracticeEndedForPlayer` and `OnEndMissionRequest` against
   `ArenaWaveArena` and `WaveFightDirector`, for a won wave, a lost wave, a wave that cannot start, a
   fault mid-wave, and a second run in the same visit. Name any state where the player has no body,
   two bodies, cannot leave, or vanilla's practice prize is paid for a wave.
3. **The hero's safety.** That a wave cannot kill or wound the hero or change his campaign state,
   with the lines that prove it.
4. **Agents and teams.** The new team in a running mission, its relations to every other team, the
   spawn capacity check, agent handles held across frames, the spawn frames, a scene with too few
   frames, and every TAOM mission behavior that attaches to this mission and now sees agents on a
   team it did not expect.
5. **The dialogue.** Every token, priority and condition against vanilla's `ArenaMasterCampaignBehavior`;
   that vanilla's own post-fight line cannot fire for a wave; that the offer never shows where the
   mission logic is absent; that each line sets every text variable it uses; that a flag on a
   `start` line is always cleared.
6. **Saved state and lifetime.** The day ledger in `SyncData`, the singleton service across
   campaigns and loads, static fields, the per-mission objects.
7. **The two features together.** In a town of the Free Peoples both run in the same mission: the
   team patches on `ArrangePlayerTeamEnmity`, `SpawnArenaAgent` and `OnMissionTick`, friendly fire
   forced off, and the wave fight calling vanilla's `StartPractice`. Find any state where one
   breaks the other.
8. **Co-op.** The mission role, the settings class, and every gate.
9. **Config.** That a malformed or hostile `training_waves.json` cannot crash, overflow or pay out
   unbounded gold, and that the shipped file matches the code's default.
10. **Tests and gates.** That the tests can fail for the defects they name; that no count, baseline
    or allowlist was raised without cause; that the in-game check manifests prove what they claim.
11. **Docs.** That neither feature doc states something false.

Also look for what nobody listed.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
