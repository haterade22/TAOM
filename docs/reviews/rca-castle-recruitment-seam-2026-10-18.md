# RCA: castle recruitment seam (#886, deep-review 2026-10-18)

> **1.4.x port note (plans/131, 2026-11-04).** Ported to the 1.4.x line with `f83a63e57`; the
> addendum's `0d3f2bc88` follows in the same plan without its #920 co-op stand-down (decision D7).
> Engine lines below are the 1.5.x review's v1.5.5 citations. On the installed v1.4.8 the same facts
> sit at `AiVisitSettlementBehavior.cs:279` (recruit gate), `:327` (x1.4 castle bonus), `:406-435`
> (the estimate's 4 and 2 slots) and `:706` (the targeting gate the removed transpiler patched), and
> `RecruitmentCampaignBehavior.cs:291-303` (`HourlyTickParty` skips castles), `:414`
> (`CheckRecruiting`), `:504` and `:522` (`RecruitVolunteersFromNotable` asks the volunteer model).
> On 1.4.x the transpiler-site check lives in `CastleRecruitmentEngineMirrorTests`.

## Top-line

`/deep-review` ran six lenses in two waves (1 Standards, 2 Engine, 3 Efficiency, 5 Data flow; then 4 Completeness,
6 Design) on the uncommitted #886 change in the `feat/recruitment-garrisons` worktree, against `6553b8c70`. XML and
Tooling were not in scope. No lens found a CRITICAL or HIGH defect, and every engine API the change uses resolved
against the installed v1.5.5 DLLs. The review confirmed 3 MEDIUM and 9 LOW findings. All findings in the changed code
were fixed in the same session; two pre-existing gaps are follow-ups for the maintainer.

The largest lesson is about the plan, not the code: the plan's co-op claim (a `[Likely]` that the old postfix ran on a
co-op client) was never checked, and the build added an authority gate on that claim to a listener that runs for every
party every in-game hour.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | `CastleRecruitmentBehavior.OnHourlyTickParty` checked `ICoopSessionProvider.IsAuthority` first. `HourlyTickPartyEvent` is raised only from `CampaignPeriodicEventManager.TickPeriodicEvents`, which BannerlordCoop's `PartyTickPatch` skips on a client, so the check was always true where it ran. Under an enabled co-op module it cost an array allocation and a reflective invoke per call, for about 2,000 to 3,200 parties per in-game hour. | Co-op gate on a per-entity tick | The plan reasoned from Harmony semantics ("a postfix runs even when a prefix skips the original") and never read where the event is raised or what the co-op mod skips. `docs/features/coop-interop.md` already says per-entity ticks never reach a client; nobody opened it. The existing lesson "enumerate EVERY handler and gate it" pushes toward adding gates, with no counterweight. | Gate removed (behaviour preserved), co-op note widened in the behavior. New lesson in `lessons/campaign-mechanics.md`: read the raise path before adding an authority gate; a per-entity periodic tick needs none. |
| 2 | MED | Engine-bump tracking of vanilla `RecruitmentCampaignBehavior.HourlyTickParty` was dropped. Its `patch-targets.md` row went with the deleted postfix, but `CastleAiRecruitDispatcher` still copies its guards, and the "no double recruit" claim depends on vanilla still skipping castles. The scan's slot count mirrors the private volunteer estimate, which was never tracked. | Mirrored engine logic left untracked | Deleting a patch was treated as pure deletion. Nobody asked what the deleted row was protecting in `engine_body_diff.py`. | New `TAOM.Tests/Migration/CastleRecruitmentEngineMirrorTests.cs`: `CheckRecruiting` still binds to the delegate shape, `HourlyTickParty` still never reads `IsCastle`, the estimate still loads 4 and 2. Each was shown to fail when its assertion is flipped. New lesson in `lessons/harmony-il.md`. |
| 3 | MED | Issue #886's Testing section asks for "no notables" and "toggle off" cases. Neither exists, and the plan did not account for them; `CastleAiToggle`'s new branches have no unit test (`Settlement` is sealed). | Issue test list not reconciled | The plan narrowed the tests to the pure scan and never compared its test list with the issue's. | `CastleAiToggle` is listed as game-tested in `docs/features/castle-recruitment.md`; the two cases are recorded on #886 as owed in-game checks. |
| 4 | LOW | New docs claimed a `CheckRecruiting` bind miss means "lords travel to castles and do not recruit", and that "an AI lord or caravan in a castle recruits". Vanilla's `OnBeforeSettlementEntered` recruits on entry anyway, and caravans never recruit in a castle (`CheckRecruiting`'s caravan branch is town-only). The toggle doc said an empty castle "keeps the x1.4 bonus"; every castle keeps it. | Doc claims written from the plan, not the engine | The plan's model of castle recruiting held only the hourly path. The implementer wrote prose from the plan. | Rows, test messages and the feature doc corrected against the engine source. Lesson below (finding 5's), since both come from one incomplete model. |
| 5 | LOW (pre-existing) | With "AI Recruits From Castles" off, AI lords still take castle volunteers on entry (vanilla `OnBeforeSettlementEntered`) and garrisons still take slot 0 daily (`GarrisonRecruitmentCampaignBehavior`). The MCM hint and config docs imply the toggle stops AI recruiting. | Toggle promise vs engine consumers | The 2026-05-31 design enumerated the vanilla gates that block castles, not every consumer of a castle notable's `VolunteerTypes`. This review repeated the model until the data-flow lens listed the consumers. | Known Limitation recorded in the feature doc. Follow-up for the maintainer (close it through `TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero`, or narrow the promise and `/localize` the hint). New lesson in `lessons/campaign-mechanics.md`. |
| 6 | LOW | `CastleAiRecruitDispatcher` caught the `Delegate.CreateDelegate` exception, returned null, and logged one warning that merged three causes. | Swallowed error | `docs/ai-includes/code-quality.md` "Don't Swallow Errors" exists; the implementer brief did not cite it. | Rebuilt as a closed delegate bound per campaign in `Bind()`, with a separate log per cause. No new rule. |
| 7 | LOW | `ReflectionSiteBindingTests` matches a Method row by name only, so a signature change to `CheckRecruiting` keeps the gate green while the game logs a warning. | Gate fidelity | Pre-existing helper design. | Covered for this site by the mirror test in finding 2. The helper itself is a follow-up (optional parameter-type column). |
| 8 | LOW | `IsCastleAndAiDisabled` stopped describing its result once castle stock decided it too. | Name drift | ADR-009 rule 3 applies; the change widened semantics without renaming. | Renamed `IsExcludedFromRecruitScoring`. No new rule. |
| 9 | LOW | The `patch-targets.md` header was set to 301 by arithmetic after the generator's output was discarded. The generator's "unrelated" extra row was a real omission from `2410aec51` (`AllianceCampaignBehavior_OnCallToWarAgreementProposedByPlayerKingdom_Patch`), which also left that target out of engine-bump diffs. | Generator diff discarded unexplained | The unexpected row was read as noise. | Regenerated table taken (302 rows, footer kept). New lesson in `lessons/build-tooling-workflow.md`. |
| 10 | LOW | Stale prose outside the plan's doc list: `CastleRecruitmentConfig.cs:14`, `docs/features/tavern-mercenaries.md`, `docs/features/caravan-bandit-parity.md` (its caravan claim was wrong before this change), the Patch42 class summary, the feature doc's plural "transpilers". | Doc sweep by identifier only | The plan grepped for the deleted class names, not for prose that names the mechanism ("postfix", "Patch42 postfixes"). | Fixed. No new rule: the completeness lens's stale-doc sweep caught it. |
| 11 | LOW | Test names in `CastleVolunteerScanTests` left out the method segment of `Method_State_Expected`; a doc line started with `+` and rendered as a nested bullet; "own own-faction" typo; a doc line said the binding tests need the gate runsettings to run (they run whenever a game install resolves). | Style and accuracy | The implementer brief named no test-naming rule. | Fixed. One-off. |
| 12 | LOW (follow-up) | `FieldCommissionMissionLogic.OnAgentRemoved` checks `IsAuthority` before its cheap guards on a per-agent battle callback. | Guard order | Outside this change. | Follow-up only; no issue filed (LOW, pre-existing, maintainer's call). |
| 13 | LOW | The convergence pass found three wording slips in the applied fixes: the mirror tests were described as pinning "the vanilla logic the dispatcher copies" (they pin three facts; the copied party guard and siege filter are not pinned), the co-op sentence spoke for every co-op mod (proven for BannerlordCoop only), and a broken verb plus a wrong test path in the feature doc. | Doc claims broader than the evidence | The fix brief described the new tests by intent, not by what each asserts. | Fixed. No new rule: finding 4's lesson covers writing claims from the evidence. |
| 14 | LOW (follow-up, pre-existing, `[Likely]`) | The dispatcher's guard excludes only `MobileParty.MainParty`, as the deleted postfix did. On a BannerlordCoop host a remote player's party in a castle would pass it and could recruit automatically; the co-op mod rewrites vanilla `== MainParty` checks with its own transpiler, which does not reach TAOM code. | Player-party guard under co-op | Copied verbatim from vanilla, which assumes one player. | Not changed here (parity). Needs triage and an in-game check; an issue only on the maintainer's word. |

## Disagreement resolved: the slot count

The orchestrator and the data-flow lens first argued for a 3-slot scan: a perkless own-faction lord at relation 0 to 4
can recruit only slots 0 to 2 (`DefaultVolunteerModel.MaximumIndexHeroCanRecruitFromHero` = 1 + 0 + 1 + 1), so slot 3
would rarely drain and the gate would nearly always open. The design lens refuted the premise. In a new game,
`NotablesCampaignBehavior.SetInitialRelationsBetweenNotablesAndLords` walks `Settlement.All` with no type filter, after
`OnNewGameCreated` where TAOM spawns the castle notables, so castle notables get the same random lord relations as town
notables; many lords reach slot 3. Vanilla's 0.25 floor also fires exactly when its own estimate (4 own-faction slots)
is zero, so 4 is the exact trigger. Verified in the v1.5.5 source; the approved 4 stays, with a comment recording why
and the one residual (a non-kingdom lord counts 2 slots at a foreign castle).

## Root-cause pattern: a model of the engine built from the path being changed

Findings 1, 2, 4 and 5 share one cause. Each design step modelled only the vanilla path TAOM touched (the hourly tick,
the patched method) and drew conclusions about the whole system from it: who raises the event, who else recruits from
castle notables, what the deleted row protected. Each was settled by reading one more engine consumer or raise site.
The same instinct is already in `csharp-architecture.md` "GameModel Cross-Entity Propagation", but it triggers only on
a GameModel override.

## Why each lens caught or missed what it did

- **Standards (1):** caught 6, 8 and 11. The co-op gate's cost and the engine claims are outside its rules.
- **Engine (2):** verified all 16 API uses, refuted the plan's co-op claim, caught 4 and 10. It did not weigh the
  gate's cost (Efficiency's domain).
- **Efficiency (3):** caught 1 with the raise-site proof and 12. Doc accuracy beyond performance is outside its lens.
- **Data flow (5):** caught 5, the caravan claim in 4, 2 and 10, and opened the slot-count question.
- **Completeness (4):** caught 3, 9 (found the true cause of the extra row) and confirmed 2.
- **Design (6):** proposed the rename, the closed delegate, the in-helper anchor and the slot-count comment, and settled
  the slot count.

## Feedback memories to codify

None. The lessons below carry the rules; no harness or memory change is needed.
