# Provider-neutral building and adversarial review

Any AI can build TAOM. Different AIs independently review it using the same
project rules, scope and evidence contract. This directory contains the shared
workflow, not a second copy of every client configuration.

The current implementation exports commit-bound review packets, routes every
file, and validates returned evidence. It does not start AI clients, call paid
APIs, execute checks, schedule audits, change GitHub protection or merge code.

## Files

- [Policy](policy.md) and [role instructions](roles/builder.md): authority,
  independence and builder/reviewer/adjudicator responsibilities.
- [Review reference](review-reference.md): TAOM's detailed architecture, engine
  lookup instructions, historical review lessons and intentional exceptions.
- [Scope map](scopes.md) and [routing.json](routing.json): C#, data, UI, tools,
  harnesses, native dependencies, assets, localization, docs and fallback coverage.
- [Verification](verification.md): non-deploying commands and evidence limits.
- [Report format](report-format.md): strict records and maintainer dispositions.
- [Provider setup](providers.md): Codex, Claude Code, Kimi Code and universal
  explicit prompting for other clients or models.
- [Codex operating guide](../docs/ai-includes/codex-operating-guide.md): onboarding,
  native workflow skills, tool discovery and configuration boundaries.
- [reviewctl.py](../tools/reviewctl.py) and [tests](../tools/tests/test_reviewctl.py):
  the executable packet/validation contract. No third-party Python dependencies.

## A change-review cycle

1. An authorized builder works from the actual task and writes/tests the change.
2. With an authorized candidate commit available, prepare its exact review
   packet from a clean checkout. Do not clean or stash other people's work.
3. Use separate clean checkouts and fresh sessions for two providers other than
   the builder's model provider. Give each the same packet, without other
   reports or the builder's theories. Each may cover bounded slices.
4. Collect independent reports and real check evidence. An adjudicator attempts
   to refute findings after the blind pass. The maintainer decides dispositions.
5. Fix confirmed defects as the builder, then create a new packet for the new
   SHA. Review the fixes too. Run the validator and inspect its supporting
   evidence before deciding whether to merge.

From the repository root (replace the example model/session/task with reality):

```powershell
python tools/reviewctl.py lint
python tools/reviewctl.py prepare --base HEAD~1 --head HEAD --builder-provider anthropic --builder-model ACTUAL_MODEL_ID --builder-session build-123 --task "Actual requested outcome and acceptance criteria" --out .ai/runs/change-123
```

The output contains `manifest.json`, `prompt.md`, `review.template.json` and
`checks.template.json`. Templates deliberately claim no completed work. Save
each actual report under a distinct filename, complete the check evidence, then:

```powershell
python tools/reviewctl.py validate --packet .ai/runs/change-123 --report .ai/runs/change-123/review-openai.json --report .ai/runs/change-123/review-moonshot.json --checks .ai/runs/change-123/checks.json
```

Add `--dispositions PATH` for actual maintainer decisions on disputed findings.
Use a third provider when the builder changes: Claude can review Codex-built
work, Kimi can build, and so on. Two models from one provider count as one.
The minimum is encoded in routing policy, not silently reduced when a client is
unavailable. Current local configuration does not prove any client is installed
or authenticated.

Exit 0 from `validate` means EVIDENCE COMPLETE, advisory only. Exit 1 means HOLD
(missing coverage/checks, stale evidence, unresolved findings, etc.). Exit 2 means
invalid input, unsafe snapshot or environment failure. A dirty checkout, changed
HEAD or modified manifest invalidates the packet. Nonempty evidence fields are
attestations that still need inspection, not proof of execution or authentication.

The packet records full commits, the user-approved task, builder identity,
instruction hashes, all changed paths and statuses, lanes, and required checks.
It intentionally contains no source archive, proprietary engine files or model
credentials. Run artifact directories are ignored; copy only redacted final
summaries into `docs/reviews/` when authorized. Ignored local files and installed
engine assets are not secured or pinned by a Git SHA; use a fresh isolated
checkout and separately recorded environment/asset fingerprints.

## Reviewing everything

```powershell
python tools/reviewctl.py inventory --ref HEAD
python tools/reviewctl.py prepare --base HEAD --head HEAD --full --builder-provider anthropic --builder-model ACTUAL_MODEL_ID --builder-session baseline-123 --task "Audit the full tracked TAOM snapshot and record scope limits" --out .ai/runs/baseline-123
```

`inventory` is read-only planning output using current working-tree routing; it
is not approval. `--full` includes unchanged tracked files. Partition the
manifest into bounded slices, including binary assets and tests. Combine reports
until all lane/path obligations have two independent providers. Anything missed
remains visible as incomplete coverage. External installed modules need their own
inventory and evidence; they are not silently included in a repository audit.

## Adoption and enforcement boundary

The small root `AGENTS.md` is now the common entry point. The original detailed
review guidance lives in `review-reference.md`; existing Claude skills, settings
and technical rules remain in place. Claude imports the common entry point.
Other clients use native AGENTS support or the explicit bootstrap prompt.

Codex has five instruction-only skills in `.agents/skills/` for building,
reviewing, adjudicating, researching and verifying. They link to this shared
contract; they do not install runtime subagents or invoke paid providers.

The repository's existing Python CI discovery includes the review-tool and
documentation tests. This is regression testing, not an authenticated
adversarial-review service. Before
making it a merge gate, separately authorize and configure trusted orchestration,
provider accounts/budgets, isolated execution, authenticated report ingestion,
protected required checks, base-branch policy review and human approval. Do not
run a candidate branch's self-modified validator as the only merge authority.

Known follow-on work: optional paid-provider dispatch adapters, an immutable
cross-run audit ledger, historical defect/false-positive evaluations, scheduling,
and dependency-aware carry-forward of full-audit slices. None is claimed as
implemented here. First validate the shared process with one real small change.
