# RCA: Lost Footing rescue, the tier 1 review (2026-11-10)

**Top line:** the tier 1 review of the Lost Footing rescue (#894 phase 2) found one HIGH and six MEDIUMs. The HIGH was mechanical: a new MCM setting moved the pinned co-op settings counts, and the diff updated none of them. The MEDIUMs changed what the rescue does: one rescue cause could never move anyone, formation moves stacked soldiers on one point, scripted holds were not excluded, and an off-mesh soldier in water lost the dry-land test. The first wave ran lenses 5 and 2; the HIGH started a second wave of lenses 1, 3, 4 and 6 at `high`. Codex did not run: it was out of usage, so its pass is owed before merge. Two unattended Helm's Deep runs bracket the fixes: run 1 (before) `rescued=200 freed=167 refused=25`, run 2 (after) `rescued=218 freed=211 refused=87`, with no Lost Footing error in either.

## Findings

Every row was read in the source or the log before it was fixed. Severities are the reviewers' own.

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | HIGH | `SettingsFingerprintTests` pinned 505/347 settings and the docs quoted 540/347; the new `LostFootingRescue` property made them 506/348 and 541/348 | Pinned count | The filtered runs named the feature's tests, not the co-op gates that count every setting. Lens 5 | Pins and both docs updated. Run the full suite before a review gate, as `evidence-over-claims.md` B says |
| 2 | MED | The unreachable cause could never move a soldier: `GetOrderPositionOfUnit` returns `WorldPosition.Invalid` for a unit flagged `UnreachableViaNavMesh` (Formation.cs:1382-1385), and the cause had only the formation move | Engine behaviour assumed | The flag was read from its name; the order-spot code that reads it was not. No run had produced an unreachable case. Lens 2 | Cause, its clock and its tunable deleted; a test pins that such a soldier is never moved |
| 3 | MED | Formation moves stacked soldiers on one point: 29 of 31 moves in run 1 landed on (241.3, 134.4). A unit without its own slot gets the formation's single point (Formation.cs:1344, :1360-1362) | Engine behaviour assumed | The spot was taken as "his place in the formation" from the method's name. Lens 2, from the run log | The move spreads by up to sqrt(unplaced units) metres, as vanilla's deployment spread does (:1345-1353). Run 2: 12 moves on 11 points |
| 4 | MED | A soldier in a ladder queue or on a Long Defeat or Fords scripted hold could be moved, and would walk back to his point. `IsUsingGameObject` covers standing points only (Agent.cs:670) | Guard weaker than its claim | The doc's "a ladder, a machine" was written from the property's name. Lens 2 | Refuse `GetScriptedFlags() != None` (`scripted`). Run 2: 45 such refusals at 26 spots |
| 5 | MED | An off-mesh soldier who was also in water got the land step and no dry test, so the near move could put him on a submerged face | Rule order | The cause order made off-mesh win, and `needLand` followed the cause. Lens 5 found the overlap in an Osgiliath log | `needLand` comes from the water flags; test added |
| 6 | MED | `freed` was described as proof that a moved soldier walks; it counts episodes that end by recovery, which a teleport onto a face causes by itself. `rescued` counts moves, not soldiers | Measure not read | The summary field was named for the hope, not the fact. Lenses 2 and 5 | Doc, toml and comments say what it counts. The toml fail needs five moves with none freed |
| 7 | MED | Stale docs after the change: INDEX row, Key Files, Tests, the PlanRescue summary, the settings provider comment, the Seat of Seeing reader table | Doc drift | No sweep of every page that names the feature. Lens 4 | All updated; a "How to verify in game" section added |
| 8 | MED | Wiring pins missed five guards (scripted flags, boundary, ridden, water level, thread tripwire): deleting any left every test green | Test gap | The pin list was written before the review added guards. Lens 4 | Five pins added |
| 9 | LOW | `AwaitFree` could be set outside an episode (by the unreachable move) and leak into a later recovery | State leak | Lens 5 | Gone with row 2: every remaining cause needs an open episode |
| 10 | LOW | After a move, the off-mesh second look still described the old spot | Stale state | Lens 5 | `Detail` and `ConfirmTried` cleared on a move; test added |
| 11 | LOW | Two float gates let NaN through (`<` and `>` comparisons) | Rule 4b | Lens 5 | Positive forms |
| 12 | LOW | The teleport had no `MissionThreadGuard.NoteCall`; the adapter comment said the teleport "tells each component" | Rule and text | Lens 2 | Tripwire added; comment corrected (the base `OnAgentTeleported` is empty and nothing overrides it) |
| 13 | LOW | The near move did not test `IsPositionInsideBoundaries` | Missing check | Lens 2 | Added |
| 14 | LOW | A soldier refused for `player-near` was never tried again: run 1 left 10 soldiers stuck to the end beside the player | Rule gap | Found from run 1's log before the review | Retried every 10 s, logged once; `FarWaitLogged` then deleted as always equal to `FarRetryAt` being set (lens 6) |
| 15 | LOW | In-method comments outside ADR-009's keep-list, stale class summaries, and a decision dated 2026-11-11 for a 2026-11-10 word | Comment and date | Lens 1 | Comments moved into named methods or deleted; summaries and dates corrected |

## Root-cause pattern

Rows 2, 3, 4 and 6 share one cause: a behaviour was written down from a member's name (`GetOrderPositionOfUnit` as "his place", `IsUsingGameObject` as "on a ladder", `freed` as "walks again"), where the effect lives in code the name does not show. The project rule already says to quote the line that produces an effect (AGENTS.md "Research first"). The run log found row 3 and row 14 before any reader did, which is why the rescue log names both ends of every move.

## Why each reviewer missed or found these

- **The author** read the snap pair and the teleport, not the order-spot code or what `IsUsingGameObject` covers.
- **Lens 5 (data flow)** found rows 1, 5, 6, 9, 10 and 11 and the census-off text.
- **Lens 2 (engine compatibility)** found rows 2, 3, 4, 12 and 13 and verified 22 engine members.
- **Lens 4 (completeness)** found rows 7 and 8; **lens 1** row 15; **lens 6** the `FarWaitLogged` deletion; **lens 3** no cost issue (worst tick 2.73 ms before the fixes, 2.90 ms after).

## Deferred, each with an owner

- FOLLOW-UP #894: Codex pre-review before merge (it was out of usage).
- FOLLOW-UP #894: the 25 m player test measures from the hero's body, not the camera, so with Seat of Seeing's free view or after the player falls a formation move can happen in view. The camera API is UNVERIFIED; the maintainer decides.
- FOLLOW-UP #894: a move can run up to about 1 s after the gate closes (the gate is read once a second). A guard for a speculative case: the maintainer decides first (`simplicity-criterion.md`).
- FOLLOW-UP #894: the native effects of the teleport (re-path, an action in progress) and which face the lookup picks where levels overlap stay UNVERIFIED until `python tools/native_decompile.py` runs on `IMBAgent.SetPosition` and `IScene.WorldPositionValidateZ`.
- FOLLOW-UP #1079: run 2 refused 45 off-mesh soldiers on a scripted hold at 26 spots on Helm's Deep's walls and gates; whether they are Long Defeat posts or vanilla ladder queues is for the Long Defeat owner. Both runs also failed the runner's shop spawn check (one defender off the navmesh at spawn), which is Long Defeat's.

## Feedback memories to codify

None beyond the lesson entry.
