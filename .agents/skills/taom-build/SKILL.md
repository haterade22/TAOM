---
name: taom-build
description: Implement authorized TAOM gameplay, data, or tooling changes with engine research, TDD, and review handoff. Not for review-only or diagnosis-only requests.
---

# Build a TAOM change

Read the repository [policy](../../../.ai/policy.md),
[builder role](../../../.ai/roles/builder.md), and
[Codex operating guide](../../../docs/ai-includes/codex-operating-guide.md).
Use the [scope map](../../../.ai/scopes.md) to select the technical references
needed for this task. Keep business rules in their canonical files.

Inspect the current branch and worktree before editing. Confirm the requested
behavior and acceptance criteria, and preserve other sessions' changes. A
diagnosis does not become an implementation task merely because this skill was
selected. Work only on authorized files and avoid shared-file edit collisions.

For engine-dependent changes, establish behavior and signatures from the actual
installed build before designing adapters or patches. Write and run the failing
test, implement, then verify and refactor. Use the shared non-deploying commands;
do not invoke a Claude slash command as though it were a Codex capability.

Inspect unchanged consumers and missing configuration rows as well as the diff.
Update feature documentation and actual test evidence when the task warrants
them; the commit body is the changelog entry (`/release` writes CHANGELOG.md).
Report unavailable engine/runtime verification explicitly.

For a review handoff, use the [packet workflow](../../../.ai/README.md) when an
authorized committed snapshot exists. Do not commit other work to create it.
Keep self-review distinct from independent approval. Paid reviewers, external
issue changes, deployment and Git publication require their own task authority.
