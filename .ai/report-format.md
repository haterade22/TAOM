# Review records, version 1

`tools/reviewctl.py` is the executable contract. JSON uses UTF-8, unique keys and
exact fields. `prepare` exports deliberately incomplete templates. All records
must match both `packet_id` and `head_sha`; editing the manifest invalidates it.
Keep original reports immutable. Store reports/checks in the ignored packet
directory or an external artifact directory, not among tracked source files.

## Reviewer report

Fields are exactly: `version` (1), `review_id`, `packet_id`, `head_sha`,
`reviewer`, `status`, `coverage`, `summary`, `findings`, `unverified`.

- `review_id`: unique letters/digits/dots/underscores/hyphens, such as `r-openai-1`.
- `reviewer`: exactly `provider`, `model`, `session`. Record actual model identity
  supplied by the client/runtime and a unique session label. Do not guess. Use
  stable provider keys such as `openai`, `anthropic`, `moonshot`; the model origin,
  not a reseller or client brand, determines independence. Other providers work
  without a registry. These are attestations, not authenticated identities.
- `status`: `complete` or `incomplete`. Partial work must remain incomplete.
- `coverage`: lane id to an array of actually examined paths. Use exact manifest
  paths. Do not prefill the full inventory as a convenience. Partial slices may
  be complete for their assigned scope, but all manifest obligations still need
  two independent providers before the aggregate evidence is complete.
- `summary`: what you actually examined, including adjacent consumers, test
  oracles and cross-feature concerns. It must not just claim "looks good".
- `findings`: array of findings below; `[]` means no findings from the work done,
  not an assertion that unread paths are clean.
- `unverified`: array of unresolved obligations. Empty only when the assigned
  review has no unverified obligations. Missing engine evidence belongs here.

A finding has exactly these fields:

```json
{
  "id": "F1",
  "severity": "HIGH",
  "path": "Main/Features/Example/ExampleService.cs",
  "line": 42,
  "rule": "Acceptance criterion: empty input must be supported",
  "claim": "Describe the specific failing behavior, not a suspicion.",
  "impact": "Describe the user-visible consequence and triggering conditions.",
  "recommendation": "Describe the smallest remedy; do not implement it.",
  "evidence": "Exact reproduction/test, source excerpt and location, or calculation.",
  "engine_dependent": false,
  "engine_evidence": ""
}
```

Severity is CRITICAL, HIGH, MEDIUM or LOW, calibrated per the review reference.
Finding IDs must be unique within a report. Findings may refer to unchanged
consumers outside the diff. Lines are positive source line numbers; for removed
code cite the base revision explicitly in evidence. For opaque artifacts cite
the precise manifest/config reference and record the artifact hash in evidence.

When `engine_dependent` is true, `engine_evidence` must identify the installed
version/build, assembly/type/method, source location and relevant quoted
decompiled code. A nonempty field passes only structural validation, not truth
checking. Unsupported engine claims must remain UNVERIFIED, even if downgraded.

The human-readable companion may use the established TAOM format:
`[SEVERITY] path:line - Rule - Issue - Fix`, grouped by severity, then totals and
`VERDICT: CLEAN / ISSUES FOUND`. Use `UNVERIFIED` instead of CLEAN for incomplete
work. Never publish this prose verdict as stronger than the structured evidence.

## Check evidence

Use `checks.template.json`. Each required check appears exactly once with
`id`, `status`, `command`, `exit_code`, `evidence`. Record actual execution and
the exact command. Status must be `pass` and exit code an integer zero to pass
validation. Failed, skipped or not-run checks cannot satisfy the packet.
Evidence is a real log/artifact location or CI run link with the environment,
counts and relevant skip information. The checker does not fetch or authenticate
that evidence. See [verification.md](verification.md).

## Maintainer dispositions

Any unresolved finding, including LOW, holds the advisory result. The original
finding stays in its report. After adjudication, a maintainer can supply:

```json
{
  "version": 1,
  "packet_id": "COPY_FROM_MANIFEST",
  "head_sha": "COPY_FROM_MANIFEST",
  "decisions": [
    {
      "finding": "r-openai-1/F1",
      "decision": "refuted",
      "actor": {"kind": "human", "name": "ACTUAL_MAINTAINER"},
      "rationale": "Explain why the independently checked claim is false.",
      "evidence": "Location of the counterexample or authoritative source."
    }
  ]
}
```

Allowed final decisions are `refuted` and `accepted_risk`. Both require a real
human decision with rationale and evidence. AI adjudication does not grant risk
acceptance and must not fill in a human signature. No `fixed` decision is allowed
on the same SHA: commit an authorized fix, then prepare a new packet. Local JSON
cannot authenticate the claimed maintainer; protected approval remains external.
