# RCA: finalizer rethrow class and the War of the Ring daily repair (2026-11-13)

**Issues:** #1155 (finalizers that handed an exception back bare; PatchShield owner protection
texts) and #1163 (resumed wars undeclared after the War of the Ring is switched off and on).
**Review:** Codex first pass at `ultra` over three parts (this change, the Patch147 rework and the
six other pull requests merged that day): 0 P1, 1 P2, 1 P3
(`codex-adversarial-harmony-guards-batch-2026-11-13.prompt.md`, output in `raw/`). `/deep-review`
tier 1 on #1155 with Agent 5 Data flow and Agent 2 Engine compatibility: no CRITICAL or HIGH, four
MEDIUM, the rest LOW. Fix re-review: Codex at `high`
(`codex-fixreview-harmony-guards-batch-2026-11-13.prompt.md`): 0 P1, 0 P2, 0 P3; Agent 5 at `high`:
no CRITICAL, HIGH or MEDIUM, nine LOW text items.

## Top-line

Nine finalizer sites in seven files returned `__exception` bare, so Harmony's `throw` erased the
throw site of every exception they passed on. Three of those finalizers contain an exception class
or rewrite the result; they now hand the rest back through `RethrowStackPreserver`. Four only
observe; they are now `void`, which keeps Harmony on `rethrow`. A source gate fails on a bare
hand-back.

The Codex pass over the day's merged work found one real defect outside this change: after #1149,
switching the War of the Ring off and on in Full War left a resumed war undeclared until the next
session launch, and a co-op client then disagreed with the host. The host's daily tick now runs the
existing repair (#1163).

Two of the main session's own conclusions were wrong and were caught by review. It had recorded the
undeclared state as a limit instead of closing it, and it had called PatchShield's treatment of
TAOM's own Harmony id "by design" although decision D13 already plans to change it.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| 1 | P2 (Codex; Agent 5 had rated it MEDIUM in the PR review that morning) | After "off, peace, on" in Full War the host's model held the pair at war with no declaration, war start date or rebuilt at-war list until the next launch, and a co-op client still read peace. | Stale state across a toggle | When Agent 5 raised it on #1149, the main session framed it as a product question (does the war resume?) and wrote the undeclared state into the doc as a limit. It did not ask whether a call that already exists closes it, and it read the #772 test "daily tick without the repair" as a decision; that test only described the behaviour of its time. | `OnDailyTick` runs `ReconcileDeclaredWars` after the phase check, gated like `EndForbiddenWars`; three behaviour tests and two service tests; lesson in `lessons/campaign-mechanics.md`. |
| 2 | MEDIUM (Agent 5) | Comments, a pin test, an RCA line and issue #1155 said `com.taom.mod` is unprotected "by design". D13 (`plans/_audit/2026-10-02-perf/DECISIONS.md`) plans a strip of only the owner whose patch threw, then protection of that id. | Decision record not searched | The main session searched the feature docs, the lessons and the git history, found four docs that describe the strip as the rescue, and stopped. `plans/` was not searched. Codex confirmed the pin too, because its prompt named only those four docs. | All texts now say "not protected yet" and name D13; the pin test says the D13 plan inverts it; lesson in `lessons/build-tooling-workflow.md`; one line for Codex in `.ai/review-reference.md`. |
| 3 | MEDIUM (Agents 5 and 2, Codex) | Four finalizers that never change the outcome (both Patch88 scopes, Patch93, Patch77) were converted to the preserver. `harmony-patches.md` says an observe-only finalizer is `void`. | Rule not applied | The nine sites were fixed by one script that replaced a statement. Nobody classified each finalizer's role first, although the rule line that was being enforced contains the sentence about observers. | The four are `void`; a pin test names them; a real-Harmony test pins that an all-void method keeps its trace. |
| 4 | MEDIUM (Agent 5) | The gate matched only `return __exception;` on one line and cut lines at `//`. A conditional arm, an expression body, a parenthesised or split return passed. | Source-scan gate too narrow (repeat, `lessons/testing-qa.md`) | The gate was written against the nine sites it had to catch, and its self-test listed the spellings it handled, not the ones it missed. | Five alternations over comment-stripped text, sixteen data rows, and a docstring that names what the gate cannot see. |
| 5 | MEDIUM (Agent 5), LOW (both re-reviews) | Texts stayed stale after each fix: the registry's Uncapturable Heroes entry, three feature docs, an engine doc, two class docs, a binding-test comment ("refuses to unpatch"), and, for #1163, two feature docs, three code comments and a lesson that still said the repair runs only at session launch. | Scope sweep by phrase, not by claim (repeat) | Each sweep grepped for the sentence that had just been changed. The same claim in other words ("propagates untouched", "session launch only", "refuses to unpatch us") was not searched. | All corrected. No new rule: `lessons/harmony-il.md` "Widening a guard: grep its category name across the whole tree" already says it. |
| 6 | LOW (Agent 2) | With a `null` site, a TAOM finalizer wrote the marker "an unknown method" before PatchShield could name the method. | Diagnostics | The rule line gives `null` as the default; the lesson allows `__originalMethod` on a cold target. | Patch65 passes `__originalMethod`; the four observers are void, so PatchShield's marker is back where it is present. The type-load shim keeps `null`: its target is hot. |
| 7 | LOW (Codex, Agent 5) | The daily repair retries a declaration that has no effect, and `TryDeclareWar` warned each time: once a day for a pair that another mod or an edited config refuses. | Log noise from a new call rate | The main session had looked for a producer in the shipped data, found none, and left the warning alone. An edited config is a producer. | The warning is written once per pair and process; the declaration is still retried; one test. |
| 8 | P3 (Codex) | #1146: if the second or third `Unpatch` call throws after the first removed a prefix, the target is not counted as unpatched and is never retried. | Log accuracy | Not missed: Agent 5 had listed it as hypothetical in the PR review. | Not applied. The failed strip is logged on that path (`LogCaught`), exact counting needs per-kind owner sets, and the effect is one wrong number in a session summary. |
| 9 | LOW | The shim's hand-back inside its own catch has no behavioural test; sentences that mixed sites and finalizers, or compressed D13's dates; a test count in a doc. | Test gap, wording | - | The gate covers the shim site and the test says so; wording corrected. |

## Root-cause pattern

Rows 1 and 2 are the same slip: a conclusion written down after a search that was too narrow. In
row 1 the question "can this be closed?" was replaced by "how do I describe it?". In row 2 the
question "who decided this?" was answered from docs that describe behaviour, not from the records
that hold decisions. Both conclusions were then given to the maintainer as settled.

Rows 3 and 4 come from fixing a class by its spelling. The script that fixed nine statements could
not see that four of them belonged to finalizers that should not return a value at all, and the
gate written from those nine statements saw only that spelling.

## Why each reviewer missed these

- **The main session** produced rows 1 to 5 and 7, as described above.
- **Codex, first pass:** found row 1 and proposed the fix. It confirmed the "by design" pin
  (row 2) against the code and the four docs its prompt named; nothing in the prompt pointed it to
  `plans/`. It noted row 3 as an observation, not a finding.
- **Agent 2:** found rows 3 and 6 from Harmony's emit. It checked the owner-protection texts
  against the code, which they matched, not against decision records.
- **Agent 5:** found rows 2, 4 and 5. It had found row 1's mechanism that morning; its severity was
  overruled by the main session.

## Lessons recorded

- `lessons/campaign-mechanics.md`: "Before you document a limit, ask whether a repair that already
  exists closes it (#1163, 2026-11-13)".
- `lessons/build-tooling-workflow.md`: "Before you write 'by design', search the decision records,
  not only the docs (#1155, 2026-11-13)".
- `.ai/review-reference.md`, Codex lessons: confirm a claimed decision against the decision
  records.
- `.claude/rules/harmony-patches.md`: the finalizer line names the gate.

## Deferred

- FOLLOW-UP (no issue: the plan is recorded as owed under D13 in
  `plans/_audit/2026-10-02-perf/DECISIONS.md` and `FOR-MIKE.md`): strip only the owner whose patch
  threw, then protect `com.taom.mod`. The five feature patches that create their own `TAOM.*`
  Harmony id are protected today and would stay swallowed on a shielded target; that belongs to the
  same plan.
- FOLLOW-UP (no issue: log accuracy only, row 8): count a partly failed strip in PatchShield's
  summary.
- FOLLOW-UP (no issue: an existing limit, recorded in `docs/features/coop-interop.md`): under a
  co-op mod TAOM cannot probe, the launch-time repair runs on every peer and the daily one on none.
- Owed: an in-game check of #1163 (past day 44, switch the war off, let a Hostile pair make peace,
  switch it on, wait one day for the war notice). On that day the repair can raise one notice per
  resumed pair, and call-to-war proposals with them.
