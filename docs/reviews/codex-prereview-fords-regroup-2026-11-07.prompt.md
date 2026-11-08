# Codex pre-review: Fords of War AI regroup under its own tower (2026-11-07)

Your role is reviewer ([.ai/roles/reviewer.md](../../.ai/roles/reviewer.md)). This is a review only:

- Do not edit, create or delete any file. Do not stage, commit or stash.
- Do not run `dotnet build`, `dotnet test` or `build.ps1`: another process runs the full test suite on this machine. The
  orchestrator ran the FordsOfWar and gate tests after the change: 2,522 passed, 0 failed, 3 skipped.
- Read files, `git diff`, the installed engine's decompiled sources (`pwsh tools/taom-src.ps1 path <Type>`, one type at a time).

## Scope (read this carefully)

Worktree `E:/repos/taom-fords-trial` (detached). Its index holds six other changes that are reviewed already. **The change under
review is the unstaged diff only: `git diff` (not `git diff HEAD`, not `git diff --cached`).** Ten files:
`Main/Features/FordsOfWar/Domain/AiAction.cs`, `AiCommander.cs`, `FordsMatch.cs`, `LaneMarchRules.cs`, `MatchEffects.cs`,
`Main/Features/FordsOfWar/FordsMissionDirector.cs`, `TAOM.Tests/Features/FordsOfWar/Domain/AiCommanderTests.cs`,
`LaneMarchRulesTests.cs`, `docs/features/fords-of-war.md`, `docs/features/fords-of-war/balance-log.md`.

## The request (Mike, 2026-11-07)

"The current AI behavior is pretty dumb, just attacking the first thing it sees/closest enemy." Mike gave full authority to make
the Fords of War AI smarter overnight. The orchestrator picked this rule from a design study.

## The change

1. `AiCommander.SetOrders`: a lane of the AI's side that is not the assault lane (latched or picked) and has at least one unit
   goes to FallBack when its enemy count is at least `RegroupMinEnemy` (10) and more than `FallBackRatio` (1.6) times its own.
   The AI remembers the lanes it sent back (`_regrouping`) and releases only those, to Push, once the enemy count is at most
   `ReturnRatio` (1.1) times its own. Orders wait for the lane cooldown. `PickAssaultLane` skips a lane on FallBack. A FallBack
   the AI did not order (a person's, on the AI's side) is never released by the AI.
2. `LaneMarchRules.Evaluate`: a FallBack lane whose lead is within fire range of its own outermost standing tower (the keep when
   none stands) and whose nearest enemy is within the charge range skips the FallBack branch, so the existing Charge branch fights.
3. A log line `[FordsOfWar][Ai] regroup lane=<n> own=<..> enemy=<..>` (and `regroup end`) goes from `AiAction.Note` through
   `MatchEffects.Notes` to `FordsMissionDirector`, only for orders the match accepted.

## Questions (file:line evidence)

1. Can a lane oscillate (FallBack, fight, Push, FallBack) so fast that it never does anything useful? Consider the cooldown,
   the `_regrouping` set, the Seize and assault paths, and what `OwnLive` and `EnemyLive` count (`MatchView.LaneView`,
   `FordsMatch.View.cs`). `EnemyLive` counts the whole enemy lane, including units still at their base: is that the right input?
2. Is there any path where the AI's FallBack starves or blocks the assault, the Seize logic, hero buying or the buy cycle (anything
   in `AiCommander` that reads `lane.Order`)? Before this change the AI never ordered FallBack at all.
3. The FallBack fight in `LaneMarchRules`: does it interact correctly with `_charging` hysteresis, the siege branch, the culture
   tactics (Close branch, arrangements) and the decision trace? Can a lane flip between Charge and FallBack every tick at the
   edge of the tower's fire range?
4. Is `_regrouping` correct across a lane whose order changes for another reason (a person, the match, a lane emptied and
   refilled), across tower falls, and when a FallBack order is rejected by the match?
5. Do the new tests prove the behaviour (a test that passes without the change is a weak oracle)? Any missing case?
6. Match length: Mike's target is 30 to 40 minutes, 30 best. Is there a structural reason this rule would push matches to the
   40 minute time limit (for example both sides regrouping forever)?
7. Anything else wrong in the diff, including a simpler design.

## Output

Per finding: severity (CRITICAL, HIGH, MEDIUM, LOW), location, failure scenario, evidence. Then a one-line verdict per question.
