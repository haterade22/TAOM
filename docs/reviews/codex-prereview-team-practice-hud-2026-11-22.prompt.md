# Codex independent pre-review: team practice HUD count (#1536), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
may build in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you cannot prove is UNVERIFIED.

## What to review

The uncommitted change in this worktree (branch `fix/team-practice-hud`, base 204aa2c96):
`git status --short` and `git diff` list it.

1. `Client/Features/TrainingGrounds/Teams/Hooks/MissionArenaPracticeFightVM_Tick_TeamPractice_Patch.cs`
   (new): a Harmony postfix on `SandBox.ViewModelCollection.Missions.MissionArenaPracticeFightVM.Tick`
   in the client assembly, with the core module's patch category.
2. `Main/Features/TrainingGrounds/Teams/Hooks/TeamPracticeFight.cs` (`TryOpponentsLeft`) and
   `Main/Features/TrainingGrounds/Teams/TeamPracticeRules.cs` (`OpponentsLeft`).
3. The tests under `TAOM.Tests/Features/TrainingGrounds/Teams/` and
   `TAOM.Tests/Migration/ReflectionSiteBindingTests.cs`.
4. `docs/features/team-practice.md`, the Patch174 section of `docs/reference/harmony-patch-registry.md`,
   `docs/reference/taleworlds-api-snapshot/patch-targets.md` and `reflection-sites.md`,
   `tools/ingame_checks/1528.toml`.

## Intent

In a team practice fight (a town of the Free Peoples: two of vanilla's six AI teams fight beside the
player) the HUD line "remaining opponents" counted the player's allies, because vanilla's
`RemainingOpponentCount` counts every fighter that is not the player. The postfix rewrites that one
line as vanilla's count minus the allies still standing, never below 0. A fighter who has not
entered has no side yet, so the number is an upper bound until all 30 have entered and exact after
that. The prize line stays vanilla's. In a fight that is not a team fight nothing may change.

## What to try to disprove

1. That the number is right in every state of one arena visit: the yard before the player talks to
   the master, each spawn of a practice fight, the team win (where `TryDeclareWin` writes 0 into
   vanilla's alive count while allies stand), the seconds after it, the player down while allies
   fight on, a second fight in the same visit, and a wave fight (#1529,
   `Main/Features/TrainingGrounds/Waves/`) in a team town, which runs in the same mission with the
   same HUD. Can the line show a number that a player can see is wrong?
2. That nothing changes outside a team fight: a town of Sauron's side, the Team Practice switch off,
   the reflection access missing, a tournament, a mission with no practice controller.
3. That the patch binds and applies: the target and its signature in the installed build, the
   injection of the private readonly field by the parameter name used, how a patch class in
   `TAOM.Client` gets applied under a core category (`Main/PatchCategoryIndex.cs`,
   `Main/SubModule.cs`, `Main/Composition/ClientComposition.cs`), what happens on a dedicated server
   and when the client assembly is absent, and whether a failure of this one class can take the
   three core Patch174 patches down with it.
4. That `Team.ActiveAgents.Count` is the right count of an allied team's living fighters at the
   moment the HUD ticks, and stays in step with vanilla's alive count.
5. That the docs, the registry rows and the tests say only what the code does, and that each new
   test can fail for the defect it names.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the input or state that fails, and the evidence you read. Then what you checked and
found correct, and what you could not check.
