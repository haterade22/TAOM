---
name: taom-verify
description: Verify TAOM changes using scope-appropriate checks, fresh build/test evidence, and optional review-packet validation without deploying or repairing code.
---

# Verify a TAOM changeset

Read the [policy](../../../.ai/policy.md),
[verification contract](../../../.ai/verification.md), and
[Codex operating guide](../../../docs/ai-includes/codex-operating-guide.md).
Identify the requested scope and actual working revision before choosing checks.
Use [routing](../../../.ai/routing.json) for packet obligations; for a smaller
diagnostic request, report precisely which checks you ran and did not run.

Inspect check side effects and prerequisites. Do not deploy into the game,
repair code, install tools, change environment configuration or restore another
session's files merely to obtain a pass. Missing access is a reported limitation.

For managed tests, build the solution including tests before `--no-build`, using
the same configuration and both module-copy suppression properties. Stop the
test step when its prerequisite build fails. For Python, verify actual discovery
and skips. Do not run tests that mutate shared fixtures against another session's
working files; follow the isolation warning in the verification contract.

Record exact commands, exit status, environment, totals and relevant evidence.
Do not present inherited logs as your own run or an auto-skipped check as covered.
For packet validation, require the exact head, real reports and completed check
records. `reviewctl` validates attestations; it does not run tests or authenticate
reviewers. No result from this skill authorizes a commit, deployment or merge.
