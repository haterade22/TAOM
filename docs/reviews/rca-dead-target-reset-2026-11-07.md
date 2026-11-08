# RCA: dead-target reset in every mission (#1096, /deep-review, 2026-11-07)

## Top-line

The change: a feature module, `DeadTargetReset`, adds one mission behavior to every mission. Once a second of mission time it calls
`Agent.InvalidateTargetAgent` on each active, AI-controlled agent whose `GetTargetAgent()` is killed or unconscious. That prevents
the native crash at `TaleWorlds.Native.dll` RVA `0x6B716E` (Mike, 2026-11-07: "every battle please"). Worktree
`E:/repos/taom-dead-target`, branch `fix/dead-target-reset`, base `6791dbc75`.

- **Tier 1** (a mission tick that makes native engine calls). First wave: Lens 5 Data flow and Lens 2 Engine compatibility at `max`,
  Codex at `ultra`. No CRITICAL or HIGH, so no second wave: Lens 1 Standards, 3 Efficiency, 4 Completeness and 6 Design are SKIPPED
  BY TIER, Lens 7 XML and Tooling are NOT IN SCOPE, and Step 4 is N/A.
- **Result:** 0 CRITICAL, 0 HIGH, 0 MEDIUM, 6 LOW, all in text or test names. No code change came out of the review. Lens 2: 12
  members verified in the installed v1.5.6 DLLs, 0 incompatible, 0 unverified.
- **The mechanism held under three independent reads.** Lens 2 and Codex each traced the native reset (`0x6E3240` to `0x5D6640`)
  and found that it clears the target and the flag at `+0x19c` together, while the corpse delete (`0x5D6720`) writes the index and
  leaves the flag. Both found the sweep and the 30 s delete on the same mission clock at 4x and in fast forward.
- **Proof:** dead-target and co-op role tests: 26 passed, 0 failed. `tools.tests.test_ingame_verdict`: 249 passed.
  `lint_docs.py --fail-on-drift` exit 0. Nothing ran in game: the round in `tools/ingame_checks/1096.toml` is owed.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | LOW | The code comment, a test comment and the feature doc gave "a soldier may chase a fleeing enemy" as a reason to leave a routed target alone. Routed is a removal state: `FadeOut` sets state 2, `Mission.OnAgentRemoved` receives it, and every target picker skips an agent that is not active. A fleeing soldier stays Active until it is removed. (Lens 5, Lens 2) | Engine semantics | The reason was written from the word "routed", not from the state machine. The rule it justified was right, so no test could catch it. | All three now say that a routed agent has left the battle, that the rout already reset its targeters, and that selection never picks an agent that is not active. Lesson in `native-cpp-port.md`. |
| 2 | LOW | The doc said the native reset acts "for an agent with an AI record". Every live agent has an AI record; the real gate is the controller (`+0x568` == 1). (Lens 5, Lens 2) | Doc accuracy | The decompile was summarized from the record pointer it dereferences, not from the condition above it. | Reworded to the controller gate. |
| 3 | LOW | The doc's overview said "nothing else in a battle changes". The reset clears the engagement state, so a soldier that kept a dead target picks a new one within a second, where before it could hold it until the corpse was deleted. (Lens 5, Codex) | Overclaim | The claim described intent, not the engine's behaviour after a reset. | The overview now states what changes for a soldier that held a dead target, and that a soldier with a live target is not touched. |
| 4 | LOW | The doc named only the delete `FUN_1805d6720` as a place that clears a target without its flag. The change to the Deleted state (`IMBAgent.SetStateFlags`, the state 5 branch of `FUN_1805f02b0`) does the same. (Lens 2) | Doc completeness | Only the path in the dump was traced. | The doc names both, and says the sweep prevents both because it clears the target before either runs. |
| 5 | LOW | The doc did not say that a forced target can be dead: `SetTargetAgent` (`0x6E19D0`) stores an index without a state check, and `TaskForceDetachment` uses it. (Codex) | Doc completeness | The automatic selectors were the only ones traced at first. | The doc says only a forced target can be dead and the next sweep resets it again. |
| 6 | LOW | The co-op test was named `InACoopSession_ItActsOnlyOnAgentsThisPeerOwns`, but it pins only the role row. Separately, nothing tied the mission-end line to the patterns in `1096.toml`, so a format change could leave the check unable to pass. (Lens 5) | Test oracle | The name described the outcome the role is meant to give. | Renamed to `CoopRole_IsAgentOwner_ByTheAiControllerInvariant`. New test `TheInGameCheck_PassesOnASweepThatReset_AndOnlyThen` reads the `pass` and `reached` patterns from the manifest and matches them against `DeadTargetSweep.Summary()`. |

## Residual risk (not fixed)

- **A late forced target.** If a forced target is set to an agent whose fade began more than 29 s earlier, the delete can run
  before the next sweep. Codex raised the margin; nobody showed a path that does it. `TaskForceDetachment` sets its target once
  and disbands when the target is not active, and TAOM's own forced target (FellFoes) requires an active agent. Closing this would
  need a per-frame sweep or a hook on the forced path, which `simplicity-criterion.md` sends to Mike first. FOLLOW-UP: #1096.
- **Every scripted mission mode.** Lens 2 found no harm in arenas, tournaments, town fights, stealth, sieges or hideouts; Codex
  could not close every mode. The sweep touches only agents whose target is already down, and vanilla calls the same reset at the
  end of a town fight.

## What the in-game round must show

`[DeadTarget] mission end: sweeps=N reset=M` with `M` above 0 in an ordinary match, no `[DeadTarget] sweep failed`, and no crash at
`0x6B716E` over an unattended Fords of War round (before the fix: five in about 110 matches).
