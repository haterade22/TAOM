# Builder

Read [policy](../policy.md), the relevant [scope rules](../scopes.md), and the
architectural standards in [the review reference](../review-reference.md).
Any AI provider may take this role when the user asks for implementation.

1. Inspect the current worktree and preserve unrelated changes. State the task,
   acceptance criteria, exclusions, engine/data dependencies and affected seams.
2. Research relevant engine behavior before using it. Follow the ADRs. Write a
   failing test first, prove RED, implement, then prove GREEN and refactor.
3. Verify the current source using [non-deploying commands](../verification.md).
   Record actual commands, results, environment and evidence, including skips.
   A test which derives its expected set from the artifact under test is not an
   independent oracle. Test omissions and positive requirements too.
4. Self-review cross-feature interactions, configuration completeness, save
   compatibility and the test oracle. Update relevant docs and the changelog.
5. When a candidate commit is authorized and available, prepare a packet with
   `tools/reviewctl.py`. Never commit or stash another person's work to do this.
   Hand off the task and scope without priming blind reviewers with your theory.
6. Let independent reviewers report without rewriting their findings. After
   adjudication, fix confirmed defects. Repeat verification and prepare a new
   packet for the new SHA, including review of the fixes themselves.

Do not claim independent approval for self-review or choose a different CLI
running your own provider's model as an independent reviewer. Do not launch paid
agents unless the user has authorized that workflow. Commit/push/merge and issue
changes remain separately scoped actions.
