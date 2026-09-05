# RCA — ShaderPrecompilation re-enable + scene-walk (2026-06-17)

Re-enabled the "Pre-compile Shaders" feature (disabled 2026-05-22) and extended it to walk each TAOM
battle scene so terrain/forced-atmosphere shaders compile — the class behind the intermittent
battle-load `d3dcompiler` CTD (issue #287). Reviewed by `/deep-review` (5 agents) + an adversarial
Codex pass. **0 HIGH that shipped; the pure core (planner/decider/scene-provider) was clean and
unit-tested — every confirmed defect was in the ENGINE-COUPLING SEAM** (clock source, async-callback
identity, teardown signal, scene loadability), i.e. the ADR-008 in-game-only surface the unit tests
can't reach. All fixed; 24 unit tests green.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | The decider's "nothing to compile, advance after grace" counted item-start (StartGame) time, not RENDER time. A heavy `_forceatmo` scene still LOADING (loading window up, shaders not queued yet) on an HDD would exceed the 20s grace and be SKIPPED before any shader compiled — defeating #287 on exactly the heavy scenes. | Observation state machine — clock source | The observation-matrix review checks the VALUES (0/positive/0) but I didn't check what the elapsed CLOCK measures relative to when the observed phenomenon (shader queue) actually begins. The grace clock started before the scene rendered. | FIXED: thread `isLoading` (LoadingWindow.IsLoadingWindowActive) into `Decide`; the grace counts from the first non-loading frame (`_renderStartedMs`). Regression test `Decide_StillLoading_NeverAdvancesOnZero_EvenPastGrace`. Caught by deep-review Agent 5. |
| 2 | MED→CONFIRMED | Stale static callback: `TaomShaderGameManager.NotifyItemRendering()` forwarded to the runner with only a `_state == Starting` guard, which could NOT distinguish item N from item N+1. If item N timed out and the runner advanced to N+1 (Starting), N's late `OnLoadFinished` would flip N+1 to Running on N's callback, corrupting N+1's clock. `NotifyItemFailed` had no guard at all. | Async-callback identity | I didn't enumerate the cross-item callback race for an engine-constructed object (the game manager is `new`'d by `MBGameManager`, fires its callback whenever the engine finishes loading — which can be after the runner moved on). | FIXED: per-item **generation** tag — `StartCurrentItem` does `++_generation`, the game manager captures it, the callbacks echo it, and the runner ignores any callback whose generation ≠ current. Also removed the now-dead `IsShaderBattleActive` static (a late manager could leave it true). Caught by Codex. |
| 3 | MED | I had REDUCED `EndTimeoutMs` 60s→30s based on the deep-review API agent's (disputed) claim that `Game.Current` never nulls after a custom-battle `EndGame` (so the timeout is the routine path). Codex's deeper decompile showed the clean teardown chain (`EndGame → Mission.EndMission → MissionState CleanStates → Game destroyed → Game.Current==null`) DOES fire — so a 30s force-advance risks `StartNewGame`-ing the next item onto a half-torn-down state stack. | Engine-lifecycle behavior + acting on a disputed agent claim | Two reviewers DISAGREED on the teardown behavior and I tuned the constant toward the more pessimistic one WITHOUT decompiling the chain myself. (`feedback_codex_caught_api_misread`: when reviewers disagree on a TaleWorlds API, decompile — don't pick the more confident one.) | FIXED: `EndTimeoutMs` is now a generous 90s LAST-RESORT backstop; the normal exit is the clean `Game.Current==null` path. `TickEnding` logs the live state at 1 Hz so the first walk confirms which path fires. Caught by Codex (resolving the Agent-2/Agent-5 split in Agent 5's favor). |
| 4 | LOW | `taom_dwarves_battle_001_forceatmo` was in the default scene list but is absent from `custom_battle_scenes.xml` (and any worldmap/battle data) — it can't load as a custom battle (would just `StartTimeout` after 120s) and is never used in a real battle, so it has zero coverage value. | Scene loadability / classify-by-grep | I built the scene list from a filesystem `find` of `SceneObj/taom_*_battle_*` folders — disk presence, not loadability. (`feedback_classify_by_grep_not_by_assumption`: verify, don't assume disk == usable.) | FIXED: excluded from `DefaultScenes` + the config (commented, with the reason). Caught by Codex's scene-id cross-check. |
| 5 | LOW (limitation, deferred) | The character battle adds up to 3000 troops/side, but the engine caps actual battle/render size — so not every troop's shaders compile in one pass. (Pre-existing; the original feature had it.) | Coverage limitation | Assumed roster size == render count; the engine caps deployed agents. | DOCUMENTED as a known limitation (CHANGELOG + feature doc); the #287 SCENE shaders are fully covered. Full character coverage (roster batching across multiple battles) = iteration 2. Recorded per the no-silent-deferral rule. Caught by Codex. |
| 6 | watchpoint | Render-grace relies on `IsLoadingWindowActive==false` as a proxy for "scene rendered, shaders queued". A gap between loading-window-down and the first shader queuing could let a scene advance early. | Engine-lifecycle proxy | The proxy is the best available signal without deeper mission-state threading. | MITIGATED: render-grace bumped 20s→30s for margin; flagged as the #1 thing to watch in the first walk's log. Codex DISPUTED it as a shipped bug but raised the watchpoint. |

## Root-cause pattern

**The pure core was solid; every real defect was in the engine-coupling seam.** The decider's value
logic (initial-zero latch, settle, no-progress) was correctly extracted and unit-tested — and the
review confirmed no regression of the 2026-05-04 latch RCA. But the four real bugs (1–4) all live where
the pure logic meets the engine: *when* the clock starts vs when rendering begins, *which* item an async
callback belongs to, *what* state teardown leaves behind, *whether* a scene is actually loadable. These
are exactly the ADR-008 "in-game-only, test via game" surfaces the unit tests cannot reach. The lesson:
for an engine-orchestration feature, the highest-value review is the **lifecycle-seam trace** (the
API-compat agent's semantic findings + the data-flow agent's clock/state-machine trace + Codex's
decompile), not the pure-core tests. The tests prove the decider is right; they say nothing about
whether the runner feeds it the right inputs at the right time.

## Why each deep-review agent caught / missed

- **Agent 5 (Data flow / observation matrix)** caught #1 (the clock-source premature-advance) — its mandate
  to enumerate observation-state transitions extended naturally to "when does elapsed start." This is the
  agent the observation-matrix rule was written for (the 2026-05-04 RCA), and it earned its keep again.
- **Agent 2 (API compat)** surfaced #3 (teardown) as a semantic concern but reached the WRONG conclusion
  (Game.Current never nulls) — only Codex's full `EndGame`→`CleanStates` decompile corrected it. Agent 2
  vs Agent 5 disagreeing was the signal that I should have decompiled the chain myself before tuning the
  constant.
- **Agents 1/3/4** (standards/efficiency/completeness) passed the orchestration cleanly — correctly,
  those dimensions don't model async-callback identity or scene loadability. #2 and #4 needed Codex's
  adversarial cross-item and config cross-check lenses.

## Feedback memories to codify

No new always-on rule — these are instances of existing principles:
- #1 extends the **observation-state-matrix** rule (already in `harmony-patches.md` + Agent 5) with: *also
  verify the elapsed-clock source vs when the observed phenomenon begins* — captured here, promote if it recurs.
- #2 is the async-callback-identity pattern: *an engine-constructed object that fires a callback to a reused
  orchestrator must tag each callback with a generation/id* — same shape as the stall-marker generation idea;
  captured here.
- #3 reaffirms `feedback_codex_caught_api_misread` (decompile on reviewer disagreement) — no new memory.
- #4 reaffirms `feedback_classify_by_grep_not_by_assumption` (verify loadability, not disk presence) — no new memory.

## Status

All code findings fixed; build clean; 24 shader unit tests green. The feature is in-game-only (ADR-008):
the real validator is a 1-2 hr precompile walk, with the `[ShaderPrecompilation]` log as the instrument
(watch the `Ending item N: Game.Current==null=.. resolved via clean-menu|timeout` lines to confirm the
teardown path, and any scene where loading goes false but the count stays 0 past the grace).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
