---
name: taom-review
description: Review TAOM changes or a full snapshot for defects, architecture, engine compatibility, and test gaps without fixing code. Supports shared review packets and scoped findings.
---

# Review TAOM independently

Read the [policy](../../../.ai/policy.md),
[reviewer role](../../../.ai/roles/reviewer.md),
[Codex operating guide](../../../docs/ai-includes/codex-operating-guide.md), and
relevant [scope rules](../../../.ai/scopes.md). Use the detailed
[review reference](../../../.ai/review-reference.md) for severity and intentional
exceptions. Do not infer authority to repair defects or launch other agents.

Identify the requested review target: exact base/head, full snapshot, or working
changes. A packet review requires its clean head checkout. A user-requested
working-tree review may report provisional findings, but cannot approve a commit
or produce an evidence-complete packet. Record what was actually examined.

For a blind first pass, do not read other findings or the builder's explanation.
Attempt counterexamples from the acceptance criteria. Trace callers, lifecycle
raise sites, base methods, contested patches, configuration omissions and test
oracles. Start the caller trace from
`python tools/graphify_taom.py affected "<Type>" --depth 2` for each changed
public type: a lead list to read, not evidence. A stale graph needs
`refresh --if-stale`, which writes only outside the repo; record either in
coverage ([code graph](../../../docs/features/graphify-code-graph.md)). Read relevant historical lessons without accepting their conclusions
as current evidence. Verify engine claims against installed DLLs.

Keep implementation unchanged. Only run authorized checks after inspecting their
side effects. Return actionable findings with source locations, impact and
evidence, including UNVERIFIED obligations. For a packet, fill the actual
[report format](../../../.ai/report-format.md); do not claim paths you skimmed or
copy a template's inventory into coverage. For a scoped request without a packet,
use the human-readable TAOM finding format and explain coverage limits.

Review fixes under a fresh head when authorized. A native `codex review` result
or prose report is not automatically a validated packet or merge approval.
