# RCA: spare a riderless mount on the biter's side (plan 062, #594, deep review, 2026-11-19)

## Top line

Plan 062 (#594) records a rider's team against his mount (`MountRiderSides`, written from
`AdvancedCombatBehavior.OnAgentBuild` and a new `OnAgentMount` override) and gives the spider and warg
bites one side rule (`CreatureBiteSides.Spares`): a riderless mount on the biter's side is spared, and
one whose side is unknown is spared too. The tier 1 review (Lens 2 Engine compatibility, Lens 5 Data
flow) confirmed the engine order the record depends on and found no defect in what a bite strikes. It
found one LOW inconsistency: the spider's `IsSameSide` now answered "does the bite spare this agent",
and the creature diagnostics, which count its hits as allies, began to count a loose mount of unknown
side as an ally while the comments still said "allies". The review renamed the predicate, corrected the
comments and pinned the count with a test. Codex did not run.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | LOW | `SpiderAttackService.IsSameSide` (SpiderAttackService.cs:197 at `d0abf3f49`) delegated to `CreatureBiteSides.Spares`, which also returns true for a loose mount of unknown side. `StrikeArc` counts its hits as `allies` when `countAllies` is set (:131), and `CreatureBanditDiag.NoteAttack` logs them as `alliesInArc` and takes `inArc - alliesInArc` as the enemies in the arc (CreatureBanditDiag.cs:201-204). So such a mount moved from the enemy count to the ally count, and the comments at :110-111 and :119 still said "allies". Diagnostics only: the capped strike skips every loose mount at :139 either way, and no damage changed. | Convention inconsistency: a widened predicate kept its old name | The plan changed the predicate's body and listed its two strike callers; the diagnostics caller sat behind `countAllies` and was read as unchanged. The name still read as a team test. | Renamed to `IsSpared`; the `StrikeArc` comments name what is counted; test `CappedStrike_ALooseMountOfUnknownSide_IsCountedAmongTheSpared_NotAsAMiss` (red on a mutant with the spare rule disabled: "Expected:<1>. Actual:<0>", green on the branch). Lesson added to `lessons/gamemodels-services.md`. |

## Why each agent missed these

- **Agent 5, Data flow:** found F1 (trace 6) by following `countAllies` into `CreatureBanditDiag` and
  rated it LOW, diagnostics only. It proposed the rename the review applied.
- **Agent 2, Engine compatibility:** noted the same count change in its probe of the capped spider
  path and rated it LOW. It is a TAOM naming question, not an engine fact, so it sat at the edge of its
  scope.
- **Agents 1, 3, 4 and 6:** skipped by tier. Agent 1 (Standards) or Agent 6 (Design) would have been
  the natural lens for a name that no longer matches its body.
- **The plan and the executor:** the plan replaced `IsSameSide`'s body in place and kept the name, so
  the third caller, the ally count, read the same as before.

## Root-cause pattern

One finding. It is close to the `lessons/build-tooling-workflow.md` entry "A rule reused by a second
consumer answers the second consumer's question, or it gets its own predicate" (#581, recurred #696).
Here the consumer's question still fits the wider rule: a spared agent is skipped by design, so it is
not a miss. What drifted was the name and the comments, so the fix is a rename, not a second predicate.

## Left for later

- FOLLOW-UP, no issue: an /improve lead files none, and the orchestrator decides at wrap-up. A loose
  mount of KNOWN enemy side still counts as an enemy in a capped strike's arc, though the capped strike
  never bites a loose mount (:139), so such an arc can still log `attack-whiff`. This predates plan 062.
- FOLLOW-UP, no issue: the in-game check that `rider.MountAgent` is set when `OnAgentBuild` fires for
  spawned cavalry, and that native updates the mount cache before `OnMount`. Both are UNVERIFIED in
  native code; the failure is safe (an unrecorded mount is spared, never wrongly bitten).

## Feedback memories to codify

None. The lesson entry carries the rule.
