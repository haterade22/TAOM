# Codex pre-review: shared shield cover rule and troll act_none guard (2026-11-06)

You are reviewing uncommitted changes in TAOM (a Bannerlord v1.5.6 mod, .NET Framework 4.7.2). Read-only: report
findings, do not edit. Read AGENTS.md first. Engine source: `pwsh tools/taom-src.ps1 path <Type>` prints the
decompiled file for an installed TaleWorlds type.

## What changed (git diff HEAD on these files)

- New `Main/Core/Combat/ShieldCover.cs`: `Blocks(shieldRaised, lookX, lookY, toAttackerX, toAttackerY)`, true only
  when the victim's ground-plane look direction is within 90 degrees of the direction to the attacker; NaN or a
  degenerate vector is no block. Tests: `TAOM.Tests/Core/Combat/ShieldCoverTests.cs`.
- `Main/Features/AdvancedCombat/CustomAttacksUtils.cs`: new `ShieldBlocks(Agent victim, Vec3 attackerPosition)` reads
  `GetCurrentActionType(1) == DefendShield`, `LookDirection`, `Position`, and calls `ShieldCover.Blocks`.
- Callers now use it instead of the bare `DefendShield` test:
  `Main/Features/TrollBruteForce/Hooks/BruteForceRing.cs` (attacker = the troll's position, not the ring centre),
  `Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeAttackTasks.cs` `Hit` (attacker = the creature; shared
  by war ram, elephant, mumakil, elk, moose), `Main/Features/SignatureStrikes/Hooks/SignatureStrikeRunner.cs` (ring
  victims; attacker = the striker), `Main/Features/DwarfAttacks/Hooks/DwarfAttackHit.cs` (its private copy removed
  from `DwarfAttackService` and `DwarfAttacksConfig`).
- Troll Brute Force: `ITrollBruteForceService.IsReady(actionResolved, lastFired, now)`, used by
  `BehaviorTreeElements/BruteForceReadyDecorator.cs` with `TrollBruteForceCombat.Action != ActionIndexCache.act_none`.
  Before, with the action missing, `BruteForceTask` played `act_none` with `ignorePriority` (clears channel 0) and
  the decorator's busy test (`GetCurrentAction(0) == Action`) read the empty channel as a smash.

## Known suspects (verify or refute with file:line evidence)

1. Is `Agent.LookDirection` the right facing for a shield bearer? Could the shield face differ from the look (for
   example a rider, a mount, or a defend action whose block direction follows the attack direction, not the look)?
   Find how vanilla decides a shield block in melee (`Mission` / `MissionCombatMechanicsHelper`, `CombatLogData`,
   `AttackCollisionData`), and whether TAOM's rule now disagrees with what the engine would do for a real blow.
2. For a ridden victim (`HasMount`), are `LookDirection` and `Position` the rider's? Does a rider ever hold
   `DefendShield` on channel 1?
3. Signature strikes: the trigger victim's own shield handling (the `PrimaryVictim` path and the shield-damage basis)
   must be unchanged; only ring victims use the new rule. Check.
4. Elephant-like `Hit`: is `creature.Position` right when the rider owns the blow? Is any caller relying on the old
   `blocking` meaning (logs, damage rolls in `ComputeInflictedDamage`)?
5. The troll guard: any other path that starts the smash without the decorator (console command, a test hook)?
6. Off-main-thread reads: are all four callers on the main thread (mission tick / behaviour tree run)?
7. Float gates: NaN polarity in `ShieldCover.Blocks` (csharp-architecture.md "Engine-Float Decision Gates").

Report each finding as: severity (CRITICAL/HIGH/MEDIUM/LOW), file:line, evidence quoted from the code you read, a
concrete failure scenario, and a proposed fix. Mark anything you could not verify UNVERIFIED.
