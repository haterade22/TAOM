# Codex pre-review: dead-target reset in every mission (#1096, 2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`. A Bannerlord match runs on this machine. The orchestrator ran the new
  tests with the co-op role, feature module, gate and architecture tests: 470 passed, 0 failed, 3 skipped.
- Read files, `git diff`, `git status`, the installed engine's decompiled sources (`pwsh tools/taom-src.ps1 path <Type>`, one type at
  a time) and native code (`python tools/native_decompile.py --rva 0x<rva>` or `--engine-method <name>`, one at a time).

## The request (Mike, 2026-11-07)

Fix the native crash at `TaleWorlds.Native.dll` RVA `0x6B716E`, in every battle, not only Fords of War ("every battle please").

## The change (worktree `E:/repos/taom-dead-target`, branch `fix/dead-target-reset`, base `6791dbc75`, uncommitted)

- `Main/Features/DeadTargetReset/`: `DeadTargetResetModule` (feature module, adds the behavior to every mission),
  `Hooks/DeadTargetResetMissionBehavior.cs` (once a second in `OnMissionTick`: for each active, AI-controlled agent in
  `Mission.Agents` whose `GetTargetAgent()` is Killed or Unconscious, `InvalidateTargetAgent()`; one log line at `OnEndMission`),
  `DeadTargetSweep.cs` (clock, rule, counts).
- Registration: `Main/Composition/FeatureModules.cs`, `CoopMissionRoles.cs` (AgentOwner), `CoopMissionRolePolicyTests` count.
- Tests: `TAOM.Tests/Features/DeadTargetReset/`. Docs: `docs/features/dead-target-reset.md`, feature-map row, coop-interop row,
  `tools/ingame_checks/1096.toml`. Issue #1096 holds the dump evidence.

## The orchestrator's reading (claims to check, not facts)

From a full dump and Ghidra: a death resets no soldier's target; the corpse handler `FUN_1805b3b80` fades a dead agent through
`FUN_1805eb010` without a rout; 30 s later the delete `FUN_1805bced0` (via `FUN_1805d6720`) sets the targeting soldiers' index to -1
but leaves the flag at AI record `+0x19c`; the next AI tick's `FUN_1806b4550` and `FUN_1806b6600` read slot -1. `InvalidateTargetAgent`
reaches `IMBAgent_InvalidateTargetAgent` (`0x6E3240`), which calls `FUN_1805d6640` (clears `+0x1304` and the flag).

## Questions (file:line evidence; missing engine or native evidence is UNVERIFIED)

1. Does `InvalidateTargetAgent` on a soldier whose target is dead really leave `+0x19c` at 0 and the target index at -1, so the
   later delete cannot reproduce the fault? Read `FUN_1805d6640` and `FUN_1806a88e0`. Can the AI re-acquire the dead agent as its target
   before the delete (any target selection that accepts a Killed or Unconscious agent)?
2. Is it safe to call `GetTargetAgent` and `InvalidateTargetAgent` from `MissionBehavior.OnMissionTick`: main thread, and not while the
   asynchronous agent tick runs (`Mission.Tick`, `MissionState`)? Is `Mission.Agents` safe to enumerate there?
3. Does `GetTargetAgent` return the dead agent while its slot is dead and fading (state 4), and null once deleted? Any case where it
   throws or returns an agent from another slot?
4. Is resetting a dead target harmful to vanilla AI anywhere: sieges, ladders, mounted AI, archers, a soldier in a scripted mission
   (arena, hideout, tournament, stealth, conversation scenes), the player's own agent (excluded by `IsAIControlled`?), mounts?
5. Co-op: is the AgentOwner role with no ownership check right (a puppet is `Controller None`)?
6. Is once a second enough at 10x fast forward and 4x speed? Is mission time the right clock against a delete timed by
   another clock?
7. Anything else wrong in this diff, including a simpler fix.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
