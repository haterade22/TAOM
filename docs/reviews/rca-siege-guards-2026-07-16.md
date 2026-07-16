# RCA — Siege guards (open-field-only gate for SmartCavalryAI + MixedFormations)

**Date:** 2026-07-16
**Scope:** `/deep-review` of the uncommitted changeset adding a `Mission.IsFieldBattle` gate to SmartCavalryAI and MixedFormations.
**Trigger:** a playtester (engine v1.4.6.115628, TAOM v2.0.12) hard-crashed to desktop during his first siege — native CTD, no managed exception, nothing captured by the crash pipeline — during OrderOfBattle formation distribution ~1s after `BattlePlayable`. The guards are **defensive**; root cause is still unconfirmed pending the player's Event Log fault offset.

## Top-line

5 core agents ran. **1 HIGH, 1 MED, 2 LOW confirmed; 1 false positive refuted.** The HIGH is the important one: **the changeset did not actually do what it claimed.** It gated the *service*, but the feature had a second, service-bypassing path that kept manipulating agents in a siege every frame. All confirmed findings fixed in-session; suite green (4220 passed, 0 failed); ADR-002 restored (149/72/143/112, ceiling 150).

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | **HIGH** | `SmartCavalryAIMissionBehavior.OnMissionTick` → `ApplyCollisionAvoidance` writes `agent.SetMovementDirection` per mounted unit per frame ([:86-89, :122](../../Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs)), bypassing `ICavalryChargeService` where the gate was placed. With `AvoidFriendlies` default `true`, SmartCavalryAI **still manipulated cavalry every frame in a siege** — the exact thing the changeset claimed to stop. | Incomplete suppression / multi-path feature gating | I gated the path the crash hypothesis ran through (Patch31 postfix → `HandleChargeOrder` → native `SetPositioning`) and assumed "gate the service = gate the feature." The MissionBehavior is a **second, independent entry point** that reaches engine writes without the service. Worse: an earlier research agent *had* surfaced `ApplyCollisionAvoidance` — but only to refute a null-mount NRE theory, so I filed it "not a crash risk" and never re-examined it as a *suppression* path. | **When gating a feature OFF, enumerate every path from every entry point to an engine write — never gate one layer and infer coverage.** A feature's MissionBehavior is an entry point *alongside* its Harmony patch. Codified below. |
| 2 | MED | `MixedFormationsMissionBehavior.cs` 147 → **151 lines**, breaching the ADR-002 150-line entry-point ceiling. Caused by my own 4-line comment. | ADR-002 | The file was already at 147/150. I added lines without checking the count; nothing mechanically enforces ADR-002. | Check `wc -l` after adding lines to any entry point. Fixed by condensing my comment to 1 line (149) — **not** by refactoring `TryGetTeamAdapters`, which would be scope creep into pre-existing code. |
| 3 | LOW | `HandleChargeOrder_NoPlayerTeam_DoesNothing` builds `Substitute.For<IBattlefieldQueryAdapter>()` off-helper, so the new `IsFieldBattle` defaulted to `false` (NSubstitute bool default). Passed for the right reason only because `HasPlayerTeam` is checked first. | Testing / mock drift | I updated the `MakeBattlefield` helper and assumed all mocks flowed through it. Didn't grep for off-helper construction. | When adding a member to a widely-mocked interface, grep **every** `Substitute.For<TheInterface>()` site, not just the helper. Fixed with an explicit `IsFieldBattle.Returns(true)`. |
| 4 | LOW | `Patch31_FormationSetMovementOrder.Postfix` builds adapters + runs an O(teams×formations) `NearestEnemyFormation` scan before the service gate short-circuits — wasted CPU per siege charge order. | Efficiency | n/a — correctly identified as perf-only. | **Deliberately NOT fixed.** No correctness impact (all pre-gate work is read-only; no native write occurs), and the gain is ~24 iterations + 2 allocs per charge order. Per `simplicity-criterion.md`, a tiny gain does not justify a fourth redundant gate. Recorded here so the decision is explicit, not forgotten. |
| 5 | — | Agent 4 reported "CHANGELOG MISSING — blocking." | **False positive** | The agent scanned only the top `## 2026-07-16` section; my entry is at line 121 under `## 2026-07-15`. | Refuted by direct grep before acting. Reinforces `evidence-over-claims.md` §A: a review finding is a hypothesis. ~95%, not 100%, accurate. |

Separately confirmed and **not** a defect: `OpenSiegeMissionNoDeployment` hardcodes `(MissionTeamAITypeEnum)1` = `FieldBattle` unconditionally (`SandBoxMissions.cs:1582`, identical v1.4.6/v1.4.7), so relief-force / no-deployment siege assaults still run both features. The playtester's crash was `SiegeMissionWithDeployment` → `Siege(2)`, which the gate **does** suppress. Documented as an accepted constraint.

## Root-cause pattern: "I changed one site and assumed the change propagated"

Findings 1 and 3 are the same mistake at different scales:

- **#1:** gated the service → assumed the *feature* was gated. It wasn't; a sibling path bypassed it.
- **#3:** updated the mock helper → assumed all *mocks* used it. One didn't.

Both are **single-site edit, unverified fan-out**. In each case the correct move was mechanical and cheap: grep for all call sites / all paths, and read the result — rather than reasoning outward from the one site I happened to touch. This is the same family as `evidence-over-claims.md` §C ("never state a fact you haven't read this turn"), applied to *coverage* rather than to facts.

**The sharpest lesson: the changed-file list was the wrong review scope.** The HIGH lived in `SmartCavalryAIMissionBehavior.cs` — a file that was **not in the diff**. A diff-scoped review structurally cannot find it. Agent 5 found it only because its brief said "read the whole SmartCavalryAI feature folder and enumerate every path to a native write," explicitly including files outside the changeset.

## Why each agent missed these

| Agent | Caught? | Why |
|---|---|---|
| 1 — Standards | Found #2 | Scoped to ADR compliance **on changed files**. `SmartCavalryAIMissionBehavior.cs` wasn't in the changeset, so it never opened it. Correct within its remit. |
| 2 — API Compat | No | Remit is signature verification against the installed engine. Suppression completeness is out of scope. (Did excellent work: diffed v1.4.6 vs v1.4.7 byte-for-byte and independently proved the live-read design correct — caching at `OnBehaviorInitialize` would read `NoTeamAI` 100% of the time because every `OnBehaviorInitialize` runs before any `EarlyStart`.) |
| 3 — Efficiency | No | Reviews perf **of the changed lines**. An ungated path in an untouched file is invisible to it. |
| 4 — Completeness | Found the missing issue; produced #5 | Checks tests/docs/issue **existence**, not call-graph coverage. Its false positive came from reading only the newest CHANGELOG section. |
| 5 — Data Flow | **Caught #1**, #3, #4 | Its brief mandates tracing every path from source → transform → consumer **across the whole feature**, not the diff. This is the third consecutive review where Agent 5 is the only agent to find the real bug. |

## Lesson to codify

**Rule (new): Gating a feature off requires path enumeration, not layer gating.**
When adding a kill-switch / mission-type gate / master toggle that must make a feature inert, enumerate **every** entry point the feature owns (Harmony patches, `MissionBehavior`s, `CampaignBehavior`s, GameModel overrides, UI handlers) and **every** path from each to an engine write. Gate at a point that dominates all of them, or gate each. Do not gate the service and infer the feature is covered — a boundary class that does "per-agent boundary work" without a service abstraction (a documented, legitimate TAOM pattern) is exactly the path that will be missed.

**Detection:** grep the feature folder for direct engine writes (`Set*`, `Add*`, `Remove*` on `Agent`/`Formation`/`Team`) and confirm each is downstream of the gate.

This generalizes the existing deep-review Agent 5 "MCM toggle coverage / master-toggle fold check" rule, which asks the same question for GameModel overrides. This changeset proves the rule's scope was one category too narrow: it covered *model overrides*, not *behavior-class boundary work*. Appended to `docs/reviews/lessons/gamemodels-services.md`.

## Status

- #1 HIGH — **fixed** ([SmartCavalryAIMissionBehavior.cs:62-68](../../Main/Features/SmartCavalryAI/Hooks/SmartCavalryAIMissionBehavior.cs)) — gate hoisted to the top of `OnMissionTick`, covering the state machine **and** collision avoidance, and skipping the per-formation adapter build. The service-side gates stay: they are the unit-tested contract and also cover the Patch31 postfix path.
- #2 MED — **fixed** (comment condensed; 149 lines).
- #3 LOW — **fixed** (explicit `IsFieldBattle.Returns(true)`).
- #4 LOW — **rejected with reason** (see table).
- #5 — **refuted**.
- Verification: `dotnet test TAOM.Tests` → **4220 passed, 0 failed, 2 skipped**.
- Owed: GitHub issue (Agent 4, confirmed genuine gap), and the in-game siege smoke test once Beau supplies the Event Log offset.
