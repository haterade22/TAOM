# RCA — BehaviorType fix (deep-review, 2026-05-14)

## Top-line

A 3-line bug fix (`MissionBehaviorType.Logic` → `MissionBehaviorType.Other` on three TAOM `MissionBehavior` subclasses that don't inherit `MissionLogic`) restored field-battle stability after vanilla v1.3.15 `Mission.CheckMissionEnded` was NRE'ing at t≈10s every battle. The 5-agent deep-review passed Standards / Compatibility / Efficiency / Data Flow cleanly; **Completeness** flagged one finding (LOW, process gap): **no GitHub issue was created for this work** despite the CLAUDE.md mandate "Every feature, bug fix, crash fix... MUST have a GitHub issue. No exceptions."

The fix itself, the CHANGELOG entry, the memory entry, and the build/test artifacts are all clean. The miss is procedural, not technical.

## Findings + Root Cause Table

| # | Sev | Bug | Category | Why Missed | Preventive Action |
|---|-----|-----|----------|-----------|-------------------|
| F1 | LOW | GitHub issue for this bug fix was never created. CLAUDE.md mandates an issue for every bug fix; the closing artifacts (CHANGELOG, memory, fix files) shipped without one. | **Process gap — recurring** | The session entry was "user reports a crash mid-battle" (debugging-style) rather than "open issue, implement fix, close issue." Once root cause was identified, the path went `apply 3 edits → CHANGELOG → memory → /deep-review` and skipped the `gh issue create` step entirely. The CLAUDE.md "GitHub Issues" section notes this exact failure mode: *"Codex review #28 caught us creating issue #92 retroactively for b7e7188. The pre-commit hook only enforces CHANGELOG, not issue creation — discipline is on the author."* The discipline failed here too. | **Behavioral** — when a user reports a crash + provides debugger state, the orchestrator should `gh issue create` as the first action after RCA confirms a real bug (not just a transient), so the issue number can be referenced in the fix commit. The existing memory entry `feedback_completion_workflow.md` already codifies the 4-phase workflow; this RCA is a reminder that *Phase 4 (closeout: issue + CHANGELOG + docs)* is a hard gate, not a soft suggestion. **No new memory entry** — the rule already exists; what's needed is application discipline. |
| F2 | COSMETIC | CHANGELOG entry references `MixedFormationsMissionBehavior.cs:20`, `SmartCavalryAIMissionBehavior.cs:32`, `SiegeDismountMissionBehavior.cs:15` — but after the 3-line comment block was added, the `BehaviorType` line moved to 24/36/19 respectively. | **Documentation rot during the same edit** | The plan was drafted with pre-edit line numbers (from the initial `grep -n` output) and the CHANGELOG was written from the plan before the post-edit line numbers were re-checked. | One-off cosmetic; not worth a rule. File paths and class names are correct, which is what the reader needs to find the change. If we ever build a CHANGELOG-cite linter, it could detect this — but that's overkill for one stale line reference per entry. |

## Root Cause Pattern

F1 is the recurring pattern: **process gaps at session-start (creating the GH issue before implementing) silently slip past every artifact the deep-review checks except the explicit completeness gate.** The pre-commit hook only enforces CHANGELOG. CLAUDE.md mandates the issue. The discipline is "open issue → fix → close issue" — but when a session starts as "debug a crash that just happened," the issue is naturally a closeout step, not an entry step, and gets forgotten.

F2 is documentation rot at the smallest possible scale — a stale line number in a CHANGELOG entry. Not worth a rule.

## Why Each Deep-Review Agent Did Not Catch F1 Earlier

- **Standards (Agent 1):** Doesn't check GH issues. Out of scope.
- **Compatibility (Agent 2):** Doesn't check GH issues. Out of scope.
- **Efficiency (Agent 3):** Doesn't check GH issues. Out of scope.
- **Completeness (Agent 4):** **DID catch it.** This is the agent's primary job and it did its primary job. The finding was reported correctly; the root cause is that the orchestrator (this session) didn't create the issue before invoking deep-review. The agent did its part.
- **Data Flow (Agent 5):** Doesn't check GH issues. Out of scope.

The deep-review skill itself behaved correctly: 4/5 agents legitimately couldn't catch this class of finding, the 1 agent whose scope covers it caught it, and the report flagged INCOMPLETE. The system is fine; the operator (me) skipped a step. F1 is the canonical case where the agent fired correctly and the orchestrator did not act on it during the session — instead the rule is: **never invoke `/deep-review` for a bug fix without first running `gh issue create`.**

## Feedback Memories to Codify

None. The relevant memory (`feedback_completion_workflow.md` per `MEMORY.md`) already exists. Adding another rule for the same recurring pattern would be redundant — what's needed is application, not codification.

The session-level lesson is captured in this RCA file and in the CHANGELOG; the next deep-review on a bug fix should check for the GH issue *before* the agent fires.

## Action items before commit

1. **`gh issue create`** — title `fix(battle): restore BehaviorType.Other on MixedFormations / SmartCavalryAI / SiegeDismount` with body sections per CLAUDE.md "GitHub Issues" mandate (Problem, Analysis, Solution, Files changed, Testing). Reference this RCA from the issue body.
2. Stage and commit the 4 working-tree changes (3 source + CHANGELOG) referencing the new issue in the commit message.
3. Close the issue with the commit (or shortly after).
4. F2 (stale line numbers) — fix-on-touch only; not worth its own commit.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
