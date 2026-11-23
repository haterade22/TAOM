# Codex independent pre-review: abilities stay out of the yard and the tournament (#1537), 2026-11-22

You are the reviewer (see `.ai/roles/reviewer.md`). Review only: change no file, run no tool with
`--apply`, write nothing to the game install, start no game, run no build or test (another process
may build in this worktree), run no git command that changes anything. Report defects with
evidence (file, line, the input or state that fails). Engine facts come from the installed game
(`pwsh tools/taom-src.ps1 path <Type>`; the whole v1.5.7 decompile is under
`E:\Decompiled_Bannerlord\_categories_v1.5.7\`, search only that folder), never from a name. A claim
you cannot prove is UNVERIFIED.

## What to review

The uncommitted change in this worktree (branch `fix/arena-ability-gates`, base b923515f5):
`git status --short` and `git diff` list it.

1. `Main/Features/SignatureStrikes/Hooks/SignatureMissionGate.cs` (rewritten): `IsEligible`,
   `IsCombatOutsideArena`, `AllowsKind`, `IsArenaOrTournament`.
2. `Main/Features/DwarfAttacks/DwarfAttacksMissionBehavior.cs`: its gate calls
   `SignatureMissionGate.IsCombatOutsideArena`.
3. `Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs`: log text only.
4. One comment each in `Main/Features/DreadAura/Hooks/DreadMissionGate.cs` and
   `Main/Features/MountDespawn/Hooks/MountDespawnMissionGate.cs`.
5. `TAOM.Tests/Features/SignatureStrikes/SignatureMissionGateTests.cs`, four feature docs, one
   lesson, `tools/comment-ratchet-baseline.txt`.

## Background and intent (the maintainer's decision; do not relitigate it)

Several gates kept features out of arenas and tournaments with
`mission.CombatType == Mission.MissionCombatType.Combat`. The research on issue #1537
(`gh issue view 1537 --comments`) found that no installed managed assembly calls the setter and that
the native reset writes 0, so the value is `Combat` in every mission and the test excluded nothing.
The maintainer decided on 2026-11-22 that Signature Strikes, Race Abilities and Dwarf Attacks stay
out of the town arena's practice mission (practice, team and wave fights) and out of tournament
matches. The change detects those missions by the mission behaviours
`ArenaPracticeFightMissionController` and `TournamentBehavior`.

## What to try to disprove

1. The research itself: find any writer of the mission combat type in the installed game that it
   missed. If the value can be `ArenaCombat` anywhere, say where.
2. That the two behaviours identify exactly the missions meant. List every SandBox mission opener
   that adds one of them, and every arena or tournament mission that adds neither (an arena duel,
   a quest duel, any tournament game type). Is a mission closed that should stay open, or open that
   should be closed?
3. That the gates read the answer at a time when it is true: when each consumer first calls the
   gate (Signature Strikes, Race Abilities, `HeroMissionGate`, `HeroAurasMissionLogic`, Dwarf
   Attacks), whether vanilla's behaviours are attached by then, and whether a cached answer can go
   stale.
4. That nothing changes in a field battle, a siege, a hideout, a custom battle, Fords of War, The
   Long Defeat and a Story Mission, and that no TAOM code, test or tool relied on these features in
   an arena.
5. That `SignatureMissionGate`, a core type, may name two SandBox types in method bodies without a
   load failure on a dedicated server (ADR-013, `docs/adrs/013-*.md`).
6. That the changed comments, docs and lesson say only what the source supports, including the two
   statements that Dread Aura and Mount Despawn are kept out of arenas by a team-AI check, and that
   the new tests can fail for the defect they name.

## Report format

Findings by severity (P1 blocks the commit, P2 should be fixed before merge, P3 note), each with the
file and line, the input or state that fails, and the evidence you read. Then what you checked and
found correct, and what you could not check.
