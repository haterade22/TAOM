# RCA — CultureConversion Notable Replacement (2026-07-03)

**Scope:** `/deep-review` of the notable-replacement feature (12 changed files). 5 agents: standards PASS, API compatibility PASS (24/24 verified on installed 1.4.6), efficiency PASS (0 HIGH), data flow PASS (10 flows, 0 gaps). Completeness returned the 2 confirmed findings below. Per the mandatory Phase 3e rule, both get RCA regardless of severity.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | LOW | CHANGELOG claimed "+10" new tests; the actual count is 9 (8 service + 1 config provider) | evidence-over-claims §C (fabricated count) | The CHANGELOG entry was authored from working memory of "about ten tests" instead of counting the test methods in the diff. **Repeat offender** — same class as the 2026-05-30 hotfix-review fabrication (`feedback_no_write_before_reading_tool_output.md`); §C explicitly lists counts as never-invent facts. | Fixed to "+9". Rule strengthened in LESSONS-LEARNED (Build/Tooling/Workflow): every numeric claim in a CHANGELOG/doc/commit body must be produced by a command in the same session (`grep -c "\[TestMethod\]"` diff count), not recalled. |
| 2 | MED (process) | GitHub issue did not exist at review time | workflow ordering | CLAUDE.md's own guidance says open the issue when STARTING the work; this session deferred it to close-out, so the completeness agent correctly flagged it missing. | Issue created before the closing commit (this RCA and the issue reference each other). No rule change — the rule exists and is correct; the miss was sequencing within one session, caught by the gate designed to catch it. |

## Root-cause pattern

Both findings are process findings; zero code findings survived the 5-agent pass. The common thread is **artifact-vs-evidence ordering**: the CHANGELOG number and the issue should each have been produced from (or before) the work, not reconstructed after it. The code itself was TDD'd and signature-verified up front, which is why the code side came back clean.

## Why each agent missed these

Not applicable in the usual sense — the completeness agent is the one that CAUGHT both. Standards/API/efficiency/data-flow agents don't audit CHANGELOG numerics or issue existence, by design.

## Feedback memories to codify

None new. Finding 1 maps to the existing `feedback_no_write_before_reading_tool_output.md` / evidence-over-claims §C; the repeat is recorded in LESSONS-LEARNED rather than a new memory file.
