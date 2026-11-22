# Codex independent pre-review: team practice fights (#1528), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no git command that changes
anything. Report defects with evidence (file, line, the input or state that fails). Engine facts
come from the installed game (`pwsh tools/taom-src.ps1 path <Type>`), never from a name. A claim you
cannot prove from the engine source or the data is UNVERIFIED, not a finding.

## What to review

The uncommitted change in this worktree (branch `feat/training-grounds-teams`, base af5ddb617):
`git status --short` and `git diff` list it, and the new files are untracked.

- `Main/Features/TrainingGrounds/Teams/` (new): the rules, the service, the module, the dialogue
  behavior, and under `Hooks/` four Harmony patches (category `Patch174_TeamPractice`) with their
  state, reflection access, engine glue and texts.
- `Main/Features/TaomSettings.cs` (one MCM switch), `Main/Composition/FeatureModules.cs` (one line),
  `Main/Features/CoopInterop/CoopSettingsRelevance.cs`, `Main/_Module/ModuleData/taom_module_strings.xml`
  (five rows).
- `TAOM.Tests/Features/TrainingGrounds/Teams/` (new) and three existing test files.
- Docs: `docs/features/team-practice.md` (new; read it first), the patch registry, the API snapshot
  files, `coop-interop.md`, `feature-map.md`, `arena.md`, `tools/ingame_checks/1528.toml`.

## Intent (the maintainer's decisions; do not relitigate them)

- In a town whose owner is of the Free Peoples the practice fight at the training grounds is a team
  fight. In a town of the Shadow, a neutral town, a co-op session, on a dedicated server, or with the
  MCM switch off, the fight is vanilla and the feature writes nothing.
- Vanilla's controller (`ArenaPracticeFightMissionController`, SandBox) stays in charge. The feature
  only patches it.
- Vanilla's prize amounts stay.

## Evidence the builder has

- `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` on this tree: 44,055 passed,
  1 failed, 9 skipped. The failure is `LanguageFileCoverageTests.EveryLanguage_DeclaresARowForEveryEnglishKey`
  for the five new keys, which wait for the maintainer's language pass.
- No game run. Nothing below has been seen in play.

## What to try to disprove

1. That each Harmony patch binds to the method it names in installed v1.5.7 and can run where it
   runs: the argument lists, the injected parameters, the thread, and whether a target can be inlined
   by the JIT so the patch never fires.
2. That a team fight always ends. Walk `ArenaPracticeFightMissionController.OnMissionTick`,
   `SpawnArenaAgent`, `SelectRandomAiTeam`, `OnAgentRemoved`, `StartPractice` and
   `CheckPracticeEndedForPlayer` with the patches applied, for: the player falls; the other side is
   wiped out with fighters still to enter; every fighter has entered and only allies stand; the
   player leaves mid-fight; a second practice in the same visit; the counters after each. Name any
   state where the fight cannot end, ends twice, or the alive count goes negative.
3. That the prize and the debrief are right: the player is paid once, the amount is vanilla's, a
   knocked-down ally never counts as a beaten opponent, and the win text shows only for a team win.
4. That the idle state (the player is in the yard but not practising) is sound in a team town, and
   that friendly fire being off for the whole mission harms nothing else in that mission (the
   training master, the crowd, the conversation, a later vanilla-mode practice in the same mission).
5. That the mode decision is right and stable: which settlement it reads and when, a town captured
   since the last visit, a player with no kingdom, a town owned by a neutral or an unlisted faction,
   a null encounter, and a change of the MCM switch during a visit.
6. That the two dialogue lines attach to the right tokens, win over vanilla's lines only in a team
   town, break no other branch of the training master's conversation, and set every text variable
   they use.
7. That nothing leaks between missions or campaigns: static fields, the per-controller state, the
   friendly fire flag, the outline.
8. That the co-op classification of the new setting and the co-op gate are right.
9. That the tests can fail for the defects they name, and that no gate was weakened to pass (look at
   every count, baseline and allowlist the change raised).
10. That the docs state nothing false.

Also look for what nobody listed: other TAOM features and other mods that patch or read the same
vanilla controller or the same dialogue tokens, and missions other than the town practice fight
where the patched methods run.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the evidence you read, and the smallest input or state that shows the defect. Then a
list of what you checked and found correct, and a list of what you could not verify and why.
