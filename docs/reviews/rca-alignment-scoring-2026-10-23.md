# RCA: AI trip scoring honours alignment (#887, deep-review 2026-10-23)

> **1.4.x port note (plans/131, 2026-11-04).** Ported with `93684c32c` as `AlignmentRecruitmentModule`
> plus one `FeatureModules.All` line (decision D2), keeping the 1.5.x number Patch114. The v1.5.5 facts
> below hold on the installed v1.4.8: the estimate is the private `(int, float)
> GetApproximateVolunteersCanBeRecruitedDataFromSettlement(Hero, Settlement)`
> (`AiVisitSettlementBehavior.cs:406`), called from `AiHourlyTick` at `:304`. Finding 1 (a number clash
> with a parallel 1.5.x worktree) has no 1.4.x counterpart.

## Top-line

`/deep-review` ran six lenses in two waves (1 Standards, 2 Engine, 3 Efficiency, 5 Data flow; then 4 Completeness,
6 Design) on the uncommitted #887 change in the `feat/recruitment-garrisons` worktree, against `fdb683039`. XML and
Tooling were not in scope. Nothing was incompatible with the installed v1.5.5 engine or Lib.Harmony 2.4.2, and the data-flow
lens traced 16 flows with no gap: AI trip scoring and the recruit-time model now agree for every lord and settlement
pair. The review confirmed 4 MEDIUM and 10 LOW findings; all in the changed code were fixed before commit.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | MED | Patch114 is also claimed by the uncommitted B08 worktree (Messengers, #898). Each branch passes `PatchCategoryNumberTests` alone; the second to merge fails. | Patch number chosen from trunk only | The number was taken as "highest on trunk plus one"; parallel batch worktrees were not checked, although the batch notes already record the same clash for Patch105 and Patch106. | Renumber at merge if this branch lands second; test method names carry no number. New lesson in `lessons/harmony-il.md`. |
| 2 | MED | `RecruitmentAlignmentSettingsProvider` resolved MCM's `TaomSettings.Instance` on every getter, and the new postfix reaches it on the AI think path (it was already hot through `TaomVolunteerModel`). | Settings read on a hot path | csharp-architecture.md rule 9 exists; the plan reused the provider without opening it. | Lazy cached accessor, as `CastleRecruitmentSettingsProvider`; row added to `CampaignHotPathSettingsProvidersTests`. No new rule. |
| 3 | MED | `AlignmentRecruitmentModule` was the only feature module with no wiring test. Dropping its `FeatureModules.cs` line would leave the patch unapplied with every test green. | Wiring not pinned | The plan listed decision and binding tests, not the module test that every sibling has. | `AlignmentRecruitmentWiringTests`, modelled on `CrownGuardWiringTests`. No new rule: the completeness lens's module scan caught it. |
| 4 | MED | The #887 body described a skipping prefix, a hook, a `SubModule.cs` line and a `reflection-sites.md` row; none was built. | Issue drift | The spec text was copied into the issue before design. | Issue body rewritten to the built design. |
| 5 | LOW | The pure helper `AlignmentRecruitmentScoring` became one `&&` over a bool the postfix had already computed, and its arguments were all evaluated before the own-faction short-circuit. | Indirection without a win | The brief pinned a helper for testability before the design settled. | Helper and its tests deleted; the postfix checks the own map faction first, then asks the service, like `TaomVolunteerModel`. The rule itself stays tested in `RecruitmentAlignmentServiceTests`. |
| 6 | LOW | The key extraction is hand-copied in `TaomVolunteerModel` and the postfix, with no pointer between them. | Drift risk | New second copy. | Cross-reference comments in both. |
| 7 | LOW | The binding test applied the class, not the category through production's `PatchCategoryIndex`, asserted nothing after unpatching, and sat in `TAOM.Tests/Migration`. | Test proves less than its name | No sibling was read before writing it. | Rewritten in the `CrownGuardBindingTests` shape in `Features/AlignmentRecruitment`, plus an IL mirror test that `AiHourlyTick` still calls the estimate. |
| 8 | LOW | Docs: at-war villages pass vanilla's filter for a landless kingdom; Phase 1 wars start on day 30, not 44; the shield wraps a ProcessLoad patch from the first game start; a JIT-time missing-API failure also reaches the finalizer; the registry section sat after the backlinks footer; a stale `alignment-recruitment.md` line said AI map-recruit never crosses kingdoms; `configs-balance.md` anchors moved; two stale counts. | Doc claims from the plan, not the engine | The plan's model of the candidate list skipped vanilla's landless exception at `:801`. | Fixed. Finding 4 of `rca-castle-recruitment-seam-2026-10-18.md` already holds the lesson (write engine claims from the engine). |

| 9 | MED | Convergence pass: the new `TheModule_InitializesItsStaticsFromTheContainer` asserted nothing, so an empty `InitializeStatics` would leave the postfix inert with every test green, the failure class of finding 3. | Vacuous test | The fix brief named the check ("does not throw") instead of the property it protects. | The test now asserts the service and logger handoff and resets the statics. Covered by the lesson in `lessons/gamemodels-services.md` (#918): code and checks in a brief are held to the rules. |
| 10 | LOW | Convergence pass: a fault in vanilla's body returns `(0, 0f)`, not vanilla's estimate; the "same keys" rationale is wrong for a clan without a kingdom (the result holds: a null recruiter is Neutral); `configs-balance.md` still said 29 alignment keys and anchored `enabled` at the missing-file warning. | Doc claims | Wording written from the fix brief. | Fixed. |

## Why each lens caught what it did

- **Standards (1):** findings 1, 5 (the duplicated zero check) and 7. **Engine (2):** verified 20 API facts, finding 8.
- **Efficiency (3):** finding 2 and the evaluation order in 5. **Data flow (5):** 16 flows, finding 6 and part of 8.
- **Completeness (4):** findings 3 and 4. **Design (6):** findings 5 and 6.

## Feedback memories to codify

None. The lesson below carries the rule.
