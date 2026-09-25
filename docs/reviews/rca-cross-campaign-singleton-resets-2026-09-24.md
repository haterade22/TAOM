# RCA: plan 001, the SpecialResources new-campaign reset (deep review + Codex, 2026-09-24)

## Top-line

Six `/deep-review` lenses and Codex (gpt-6-astra, ultra) reviewed `a39a9c86..4263535a` on
`improve/001-specialresources-reset`. The code change is correct: a new campaign wipes the
process-lifetime balance storage before the character-creation seed, and the SyncData load reads into
a null local. **No CRITICAL or HIGH.** Eight findings were confirmed: one MEDIUM, pre-existing and
behaviour-changing to fix (a save with no behavior record still inherits the previous campaign's
balances), and seven LOW or NIT, all in prose, test fixtures or the plan. Six are fixed on the branch;
the MEDIUM's fix and the missing GitHub issue wait for Mike; the plan's wrong RED command is recorded.
Full suite before and after: 10318 passed, 2 skipped, 0 failed.

## Findings

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|---|---|---|---|---|
| F1 | MEDIUM | A save older than SpecialResources (no behavior record) loaded after another campaign in the same process keeps that campaign's balances; the `Contains` gate in `OnGameLoaded` then skips the legacy seed | Stale state / lifecycle | The plan (June, base `141b749`) predates the MANDATORY session-reset rule (August) and deferred the load path by name; its premise that the null local covers "a save predating the feature" is wrong, because the engine never calls `SyncData` without a record | Recorded as a known limitation; fix waits for Mike. Lesson in `lessons/state-lifecycle-save.md` (a key-miss guard is not a no-record guard, and the reset goes before an `OnGameLoaded` seed) |
| F2 | LOW | CHANGELOG and feature doc claimed the null local fixes "a save without the balances key", a state no TAOM build wrote | Claim accuracy | The prose restated the plan's premise instead of the engine path the code actually reaches | Fixed; covered by the F1 lesson |
| F3 | LOW | CHANGELOG said balances leaked "for the player and every lord"; only the player's heroes hold balances | Claim accuracy | Written from the storage's shape (any `heroId`) rather than from its writers (all `Hero.MainHero`) | Fixed; one-off, covered by AGENTS.md "Evidence, never invention" |
| F4 | LOW | No GitHub issue before implementation | Process | The overnight sprint reserves public actions for Mike, so the executor could not file one | NEEDS MIKE; no new rule (the sprint protocol already records it) |
| F5 | LOW | A test comment said `Hero.MainHero` is null outside a campaign (it throws), and a test name, a doc line and a code comment described a "no main hero yet" state `OnNewGameCreated` never sees | Assumed API behaviour | The plan's own excerpt said "null in the test harness"; nobody read `Hero.cs:958` | Fixed. Lesson in `lessons/testing-qa.md` |
| F6 | LOW | Fixture used `castar` for Gondor's resource; the id is `caster` | Config ID mismatch | The display name "Castar" is what docs and the CHANGELOG say; storage keys are opaque, so no assertion could fail | Fixed. Lesson in `lessons/testing-qa.md` |
| F7 | NIT | `FakeDataStore` said it mirrors the engine but skipped null on save and overwrote a duplicate key | Test double fidelity | Only the load side (the key-miss contract) was compared with `BehaviorSaveData.SyncData` | Fixed (records null, throws on a duplicate); covered by the existing shared-fake follow-up (P5) |
| F8 | LOW | Plan 001's RED step builds `Main/TAOM.csproj`, which cannot compile the test that is meant to fail | Plan procedure error | The plan reasoned from "the method is missing" without asking which project holds the test | Lesson in `lessons/misc.md` |

## Root-cause pattern

F1, F2 and F5 share one cause: the plan's engine premises were carried into code comments, tests and
prose without re-reading the engine. The plan said a pre-feature save reaches the null local (it does
not reach `SyncData` at all) and that `Hero.MainHero` is null in the harness (it throws). The executor
checked the drift of TAOM code against the plan's excerpts, as the plan asked, but not the plan's
statements about the engine. The lenses and Codex re-read the engine and caught both.

## Why each agent missed these

- **Agent 1 Standards:** caught F1, F2, F3, F4. Missed F5 to F7: its checks are rule conformance, not
  engine behaviour in comments or fixture ids.
- **Agent 2 Engine compatibility:** caught F1, F2, F5, F7. Missed F3 and F6: the scope of "who holds
  balances" and the config id are data questions outside its API lens.
- **Agent 3 Efficiency:** caught F1 as a follow-up and routed F3. The rest is outside a cost lens.
- **Agent 4 Completeness:** caught F1 to F6. It judged F7 harmless because no test exercises the
  differing paths, which is right for the assertions and wrong for the fake's own doc comment.
- **Agent 5 Data flow:** caught F1, F2, F3, F5 and noted F7's difference (T10) as harmless. It traced
  ids through storage but did not cross-check the fixture literals against the config.
- **Agent 6 Design:** caught F1, F2, F3, F5 through P1 to P3. Fixture ids and the plan's RED command are
  outside a design lens.
- **Codex:** caught F1, F5, F6, F8 and F2 as a note. Missed F3 (it checked ids, not scope prose) and F7
  (it compared only the fake's load side).

## Feedback memories to codify

None beyond the three lessons entries; the rules they extend already exist
(`.claude/rules/csharp-architecture.md` "Session-Reset Story", AGENTS.md "Evidence, never invention").
One rule text is worth amending when the rules are next touched: the Session-Reset Story says to reset
from `OnSessionLaunched`, which runs after `OnGameLoaded` (`Campaign.cs:1685-1686`), so for a behavior
that seeds in `OnGameLoaded` the reset belongs at the top of `OnGameLoaded`. Listed for Mike, not edited
here (rule files are outside this branch's scope).
