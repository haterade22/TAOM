# RCA — Overnight Refactor Stack Deep Review (2026-07-01)

**Scope reviewed:** the 4-branch refactor stack `bannerlord-1.4.5..refactor/recruitment-pool-split` — ElephantLike unification (#305, a405ea15), SubModule slim (#306, 572d0430 + 5a5824dd), PolygonWidget math extraction (#307, f1ca8f1e), recruitment pool split (#308, 0f738f88 + 355a4fdf).

**Review method:** `/deep-review` run as a 6-dimension workflow (standards, Bannerlord API compat via installed-DLL decompilation, efficiency, completeness, cross-system data flow, plus a refactor-specific behavior-preservation auditor that diffed every moved block against `git show bannerlord-1.4.5:<path>`), followed by adversarial verification of every raw finding and a second independent skeptic for HIGH ratings.

**Top line:** all five code dimensions returned **zero findings** — registration parity (59 AddModel/AddBehavior sites), Harmony wiring, ctor argument order at every profile/service binding site, engine API signatures (verified against installed v1.4.6 DLLs), hot-path caching, and partial-class static-init semantics all held. The completeness dimension returned **2 confirmed findings**, both stale documentation. Both were fixed in the same session. (An earlier in-session `/code-review low` pass separately caught the stranded pool data fields, fixed in 355a4fdf before this review ran.)

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | `docs/features/elephant.md:191-209,262-277` — dead link to deleted `Main/Features/Elephant/BehaviorTreeElements/` + 6 dead type names (`ElephantAttackTaskBase`, `EnemyInTrampleRangeDecorator`, `AttackOffCooldownDecorator`, `ElephantAttackActions`, `IBTElephantBlackboard`, `IsElephantMonster`) | Docs / refactor hygiene | The post-refactor leftover-reference sweep used `Grep ... glob:*.cs` — deliberately filtered to code, so doc hits never surfaced. The refactor discipline had no docs-sweep step. | Refactoring-specialist agent Method step 6 "Documentation sweep" (repo-wide grep, no file-type filter, living-vs-historical classification); LESSONS-LEARNED entry under Build, Tooling & Workflow. |
| 2 | LOW | `docs/features/mumakil.md:60-96` — same class: `MumakilAttackActions`, `IsMumakilMonster`, `BehaviorTreeElements/` references | Docs / refactor hygiene | Same as #1. | Same as #1. |

## Root-cause pattern

One pattern, two findings: **a refactor's definition of "leftover reference" was scoped to compilable code.** The compiler + test suite enforce code references for free, which trains the habit of treating a green build as a complete sweep — but docs have no compiler. The sweep command itself (`Grep` with `glob: *.cs`) encoded the blind spot.

Also worth recording: `CLAUDE.md` line 372-373 carries the same stale names (`ElephantAttackActions`, `+ BehaviorTreeElements/`). The fix was drafted but **blocked by `config-protection.sh`** (by design — CLAUDE.md edits need explicit user approval). The correction is surfaced in the session report for the user to approve rather than auto-applied.

## Why each review dimension missed it / caught it

- **Standards, API-compat, efficiency, data-flow, behavior-preservation:** correctly silent — their scopes are code; there was nothing wrong in code.
- **Completeness:** CAUGHT both — its prompt for this run included an explicit stale-doc-reference check (grep feature docs for the deleted/renamed names). This check should stay in the `/deep-review` completeness agent's standing prompt; this run proved its value.
- **The in-session author (orchestrator):** missed it at authoring time for the reason above (code-filtered grep). This is where the fix belongs — prevention at authoring time is cheaper than detection at review time.

## Feedback memories to codify

None beyond the LESSONS-LEARNED entry — this is a workflow-discipline lesson, not an engine/API fact. The durable artifacts are:
1. `docs/reviews/LESSONS-LEARNED.md` → "A structural refactor's leftover-reference sweep must cover living docs, not just code" (Build, Tooling & Workflow).
2. `.claude/agents/refactoring-specialist.md` → Method step 6 (Documentation sweep), so agent-run refactors carry the rule even without this RCA in context.
