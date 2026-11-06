# RCA: Dwarf Attacks deep review (#1087, 2026-11-06)

## Top-line

`/deep-review` at tier 1 (Codex at `ultra`; lenses 5 Data flow, 2 Engine compatibility, 7 XML and Tooling) on the
uncommitted head butt and drop kick. One HIGH, found by Lens 5 and Lens 2 independently: the dwarf attack tree took
the agent's single behaviour-tree slot, so every dwarf soldier lost Stand Fast. Fourteen MEDIUM and LOW findings
followed, most of them found by two reviewers. All were fixed before the first commit. A Custom Battle run earlier the
same day (`taom_debug_2026-11-06_15-11-30.log`, a dirty build) showed both attacks landing 77 blows before the fixes.

The orchestrator verified each finding against the code before fixing it. The C# fixes came with RED tests for the
three new service rules; the tool fixes were proven against the Tooling lens's mutant scripts, every mutant killed.

## Findings

| # | Sev | Finding | Found by | Category | Why missed | Fix and proof |
|---|---|---|---|---|---|---|
| F1 | HIGH | `DwarfAttacksMissionBehavior` attached its tree as a `BehaviorTreeAgentComponent`. `BehaviorTreeMissionLogic` keeps one tree per agent; on a late spawn the dwarf tree came first and `RaceAbilityRuntime.CarriesTree` skipped the dwarf, so Stand Fast never fired. At the first tick the second component overwrote the slot and showed "Error adding a tree" for each dwarf. | Lens 5, Lens 2 | Cross-feature shared state | The troll and war ram pattern was copied; their creatures have no ability profile. The one-tree rule lived only in a comment in `RaceAbilityRuntime.cs`. | `Hooks/DwarfTreeHost` holds and runs the trees from the behavior's tick with the component's gates (occupant, active, co-op owner). `DwarfAttacksWiringTests.Feature_NeverTakesTheAgentsTreeSlot`. Rule: `csharp-architecture.md` item 3. |
| F2 | MED | The player's own dwarf attacked after an AI hand-over (Battle Command seat, Fords of War, Long Defeat): `IsAiAgentDecorator` reads only the controller. | Codex, Lens 2 | Identity vs controller | The decorator's name reads as "not the player". | The ready decorator refuses `IsMainAgent \|\| IsPlayerTroop`. `ReadyDecorator_ExcludesThePlayersOwnDwarf`. |
| F3 | MED | Height ignored: the engine's nearby query is flat, so a wall-top dwarf could butt a man on a ladder below. | Codex, Lens 2 | Engine query shape | The war ram and troll ring, the precedents, fight on open ground. | `DwarfAttackService.IsWithinHeight` (1 m) in the decorator and the hit; NaN fails. Service tests. |
| F4 | MED | A raised shield halved the blow and stopped the knockdown whichever way it faced. | Codex (MED), Lens 2 (LOW) | Pose test standing in for a hit test | Copied from the war ram and troll ring, which have the same test. | `ShieldBlocks` needs the bearer to face the dwarf. Service tests, front and rear. |
| F5 | MED | The drift guard only logged: with an action missing, the task still played `act_none` with `ignorePriority`, which clears channel 0, and `IsOurs` read an empty channel as "attacking". | Lens 5, Lens 2, Lens 7 | A guard that reports but does not stop | The troll's guard has the same gap. | `IsReady` takes `actionResolved`; `IsOurs` ignores `act_none`. Service tests. |
| F6 | MED | A hit reaction that replaced the attack before a tick saw it wrote the "never played" `[Check]` FAIL, which beats every pass in the session. | Lens 5, Tooling | Evidence line claims more than it knows | The fail line's meaning was not checked against the interruption path the task's own comment names. | The fail is written only when `CheckActionAnimationClipExists` is false for the dwarf's set; otherwise cut short. |
| F7 | MED | The attacks ran in tournaments and arena practice with a blow armour does not reduce. | Lens 5 (Lens 2: design note) | Mission scope | The troll precedent has no gate; the sibling soldier features do (`SignatureMissionGate`). | `Mission.MissionCombatType.Combat` only; the gate is logged once per mission. |
| F8 | LOW | At 0 % damage the pass line still recorded a hit. | Lens 5, Codex | Evidence | | No `RecordHit` at 0 damage; the manifest needs `damage=[1-9]`. |
| F9 | LOW | The decorator and the hit used different victim filters, so a fading or invulnerable enemy could spend a cooldown. | Lens 5 | Two copies of one rule | | One filter, `DwarfAttacksCombat.IsCandidate`. |
| F10 | LOW | `IsActive()`, a native read, came before `IsCurrentOccupant` on held handles. | Codex | Handle order | | Identity check first in the decorator, task and hit. |
| F11 | MED | `bind_dwarf_attack_actions.py` validated by regex only and parsed only on the write path: commented-out rows, reordered duplicates and a merge-conflicted snapshot passed `--check`, and `--apply` wrote three of four files before it threw. | Codex, Tooling | Check mode weaker than write mode | `tools/README.md` "parse before writing" was applied to the write path only. | Parse and validate every planned output in every mode. `ParsedValidationTests`, the unparsable-snapshot test, `bind_partial.py` exit 1 with no write. |
| F12 | MED | Neither live writer refused while the game or the Kit ran. | Tooling | Missing sibling guard | The sibling copied, `gen_troll_anim_clips.ps1`, has no guard either. | `game_or_kit_running` and `Assert-GameAndKitClosed`, exit 2; tests for both. |
| F13 | MED | Many guards in both tools had no test (17 and 10 tests passed every guard-deleting mutant). | Tooling | Test strength | No mutant run before calling the tests done. | 56 tool tests; every mutant in the reviewer's scripts killed. |
| F14 | LOW | Tool fault exited 1 like a bad clip; a re-run of `-Apply` skipped the re-read; LF files decided by presence; README row credited the wrong script with previews; no live `--check` test. | Tooling | Tool polish | | Exit 2, re-read on every apply, majority line ending, README corrected, a LiveInstall test. |
| F15 | MED | `release-process.md` did not say that #1087 needs the Armory package; without it no dwarf attacks. Also the gate runners, the snapshot README and the bind docstring missed the feature. | Lens 7 | Release coupling | The feature doc's Armory note did not reach the release checklist. | A #1087 paragraph with the channel check; `data_gates.py` and `audit_armory_refs.py` run `--check`; snapshot README and docstring corrected. |

## Second wave

F1 was a HIGH, so lenses 1 Standards, 3 Efficiency, 4 Completeness and 6 Design ran on the fixed code at `high`
effort. They found no HIGH. A convergence pass checked the applied changes: no defect, and two LOW test items
(an unreachable negative-ordinal branch with a loose assertion, removed; a source pin that failed on a rename,
loosened).

| # | Sev | Finding | Found by | Fix and proof |
|---|---|---|---|---|
| W1 | MED | The decorator and the task resolved their services from the container on first use, a service locator in a node. | Lens 1, Lens 6 | The behavior resolves the services once; `DwarfTreeHost` passes them to a typed `DwarfAttacksBehaviorTree.Build`, which hands them to the node constructors. `TreeNodes_TakeTheirServicesFromTheBuild`. The untyped `BuildTree(object[])`, its null branch and the "build failed" log are gone. |
| W2 | MED | No test pinned the co-op ownership gate in `DwarfTreeHost`, the one gate the removed agent component used to apply. | Lens 4 | `TreeHost_RunsOnlyTheTreesOwnedHere`. |
| W3 | MED | Every idle dwarf slept the same 300 ms, so a battle's scans tended to land in the same frames. | Lens 3 | `DwarfAttackService.IdleScanPeriod`: 250 to 349 ms by build order. Service test. A behaviour change, small; revert by setting the spread to 1. |
| W4 | LOW | The player, mount and machine rule lived in the decorator with only a source-text test. | Lens 1 | `DwarfAttackService.MayAttack`, with a test per excluded state. |
| W5 | LOW | No test for a slider low enough that the blow rounds to 0, nor for the cut-short counter. | Lens 4 | `DecideBlow_RoundsToZero_IsNull`, `CutShortAndNeverPlayed_CountOnTheirOwnCounter`. |
| W6 | LOW | The static victim buffer in `DwarfAttackHit` kept the last blow's agents until the next blow. | Lens 3 | Cleared after the loop. |
| W7 | LOW | Stale comments (`DwarfAttackChecks` named the wrong tick, `base(10)` kept for shape); the manifest did not fail on an `OnAgentBuild` error. | Lens 1, Lens 4 | Comments corrected, `base(10)` dropped (the delay only matters to the agent component), fail pattern added. |

Not applied: building the agent names only when the pass line is written (two string reads per hit, bounded by the
12 s and 20 s cooldowns); a test that pins the impact fractions to the clips' frame counts across files (the clips
are binary Kit output); reordering `IsCandidate` (which of its reads are native is not verified); removing the
repeated identity checks in the nodes (the host already checks each handle before it runs the tree, but the checks
are cheap, and a node that touches a recycled slot would act on another agent).

## Root-cause pattern

F1, F4, F5 and F7 share one cause: the feature copied the troll and war ram pattern, and those precedents target
creatures, not soldiers of a race. A soldier of a race already has a tree (F1), fights on walls and ladders (F3), holds a
shield up against other enemies (F4), and fights in tournaments (F7). A precedent's safety does not carry over when the
new feature's agents are a different population. The lesson entries make the shared-slot rule and the player identity
test explicit; the rest are fixed in this feature.

## Why each agent missed what it missed

- **Codex** saw F2 to F4 and F11 but not F1: its brief listed the tree runtime as a suspect, not the other features
  that attach trees to the same agents.
- **Lens 7** worked on the XML and found the release-doc gap; the C# findings were outside its lens.
- **Tooling** found the tool gaps and corrected the brief's false premise about `gen_troll_anim_clips.ps1`.
- **The orchestrator's brief** framed the tree as the troll's pattern without asking which other features attach to
  dwarves; Lens 5 and Lens 2 found F1 by reading the slot's other users.

## Deferred

- FOLLOW-UP (no issue: pre-existing code outside this change; Mike to decide whether to file): the troll Brute Force
  guard has the F5 gap (`TrollBruteForceMissionBehavior.cs` logs "Trolls will not smash." and `BruteForceTask` still
  plays `act_none`).
- FOLLOW-UP (no issue: pre-existing code outside this change; Mike to decide whether to file): the war ram, the troll
  ring and the signature strikes read a raised shield whichever way it faces (F4's test in
  `ElephantLikeAttackTasks.cs`, `BruteForceRing.cs`, `SignatureStrikeRunner.cs`).
- Known limitation: no line-of-sight test, so an enemy at the same height behind a thin wall within 1.6 m can be hit,
  as with the troll ring and the war ram. A raycast per scan costs a native query on every dwarf.
- UNVERIFIED, judged in game: whether the drop kick's displacement stops at walls and ledges; whether native code adds
  its own kick collision for an `actt_kick` with no `CombatParameterId` (the 15:11 run showed no second blow).
