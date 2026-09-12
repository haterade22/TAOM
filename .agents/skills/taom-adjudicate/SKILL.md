---
name: taom-adjudicate
description: Evaluate disputed TAOM review findings or an RCA by reproducing and attempting to refute claims. Does not implement fixes or sign human risk acceptance.
---

# Adjudicate TAOM findings

Read the [policy](../../../.ai/policy.md),
[adjudicator role](../../../.ai/roles/adjudicator.md), and
[Codex operating guide](../../../docs/ai-includes/codex-operating-guide.md).
Get the exact reviewed revision, original reports and any relevant RCA. A missing
report or unavailable engine dependency is an evidence gap, not a finding to
accept by confidence.

Preserve the original claims. Independently inspect each cited location and try
both a reproduction and a counterexample. Verify installed-engine quotations and
calculated values. For remediation claims, confirm the implementation exists
and inspect the fix for new defects. Agreement among models is not proof.

Return each finding's disposition as confirmed, refuted or unverified, with the
reason and reproducible evidence. Do not change production code. Confirmed issues
go back to the authorized builder. A fix changes the SHA and requires a new packet.

Your analysis is advisory. Only the maintainer can supply final `refuted` or
`accepted_risk` records under the [report contract](../../../.ai/report-format.md).
Never create a human signature yourself, silently discard a LOW finding, or
expand a one-pass review request into an unbounded paid retry loop.
