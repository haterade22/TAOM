# RCA — ECC-adoption changeset review findings (2026-05-29)

## Context

Adopting selected concepts from affaan-m/ECC into TAOM (the `tools/audit_claude_config.py` config-security scanner + `/security-scan` skill, the `/adopt-external` skill + doc, and token-opt/stocktake edits). The `enforcement-changeset-review` workflow (3 dimensions × find→verify) returned **32 findings: 20 confirmed, 12 rejected**. Of the 20 confirmed, **16 were "verified correct — no action"** (the verify stage validating the scanner: fail-open calibration, secret masking, placeholder detection, BOM handling, exit codes, DOC_HINT dampening). **Four were real and fixed pre-commit** (none shipped). This RCA extracts the systemic lessons.

## Findings → root cause

| # | Sev | Finding | Root cause |
|---|-----|---------|------------|
| 1 | MED | `/adopt-external` SKILL.md duplicated the 9-step cycle verbatim from its own doc | **RC-A (repeat)** |
| 2 | MED | Scanner + skill cited "TAOM *mandates* fail-open hooks per harness-facts.md" — but harness-facts.md only mentioned fail-open as a side-effect, never as a mandate | **RC-B** |
| 3 | MED | `_FETCH_EXEC` regex used `[|;]` — flagged semicolon-sequential commands (`curl x; bash y`) as fetch-and-execute, a false positive | one-off regex over-match |
| 4 | LOW | `_read()` size assumption + hook extension-match intent undocumented; token-opt values not `[HEURISTIC]`-tagged | doc polish |

## Root causes

### RC-A — Authored a new skill that amplifies its own doc (REPEAT of superpowers RC2)

`/adopt-external/SKILL.md` restated all nine procedure steps that already live in `docs/ai-includes/external-repo-adoption.md`. This is the **exact pattern** the superpowers RCA (`rca-superpowers-enforcement-2026-05-29.md`, RC2) named and that `external-skill-ports.md` § "Don't amplify a rule — point to it" forbids — authored **two commits after** shipping that rule. The prevention existed (the rule + the `/skill-stocktake` "Authoring discipline" checklist, which has a "thin entry point" bullet); the gap was purely **not applying it to my own fresh skill**. The adversarial review caught it (prevention works as a *detector*), but it should have been caught at authoring time.

**Lesson / reinforcement:** when you author a new skill, run the `external-skill-ports.md` authoring checklist (and `/security-scan`) on it **before** the review — don't rely on the review to catch self-amplification. The `/adopt-external` doc step 5 already says "author skills per external-skill-ports.md"; treat that as a hard self-check, not a footnote.

### RC-B — Cited a "mandate" the cited doc didn't state

The scanner's calibration comment claimed fail-open hooks are "mandated per harness-facts.md," but harness-facts.md only described fail-open as an incidental side-effect of malformed JSON. This is the **"Verify Before Reference"** rule applied to my *own* citations: asserting a source says X without confirming it. Resolved by making the citation true — added a formal "TAOM hooks MUST fail open" row to harness-facts.md's Hook-lifecycle table (the convention is real and universally followed; it just wasn't written down).

**Lesson:** when you cite a rule/doc as mandating something, open the doc and confirm it states it — or formalize it in the same change. A citation is a claim; verify it (`evidence-over-claims.md`).

### #3 — `_FETCH_EXEC` over-match (not systemic)

A regex bug: `;` (sequential) was treated like `|` (pipe). Fixed to match only a true pipe. The generalizable bit: **test ported security rules on positive AND negative cases** — the `_FETCH_EXEC` fix is now covered by a pipe-vs-semicolon test, and the scanner overall is validated against both a planted-attack fixture (must fire) and the real tree (must stay quiet).

## What worked

The find→verify review **rejected 12 findings** — reviewers' own "no fix needed" affirmations and misreads (e.g. a claimed duplicate `/security-scan` mention that didn't exist) were correctly killed at the verify stage rather than acted on. That is `evidence-over-claims.md` operating on the review itself: a finding is a hypothesis, verified against the file before it costs a change.

## Prevention status

Already institutionalized (no new rule needed) — `external-skill-ports.md` "don't amplify" + `/skill-stocktake` "Authoring discipline" + `evidence-over-claims.md`. This RCA's contribution is the **harness-facts.md fail-open formalization** (closing RC-B's citation gap) and recording RC-A as a *repeat* so the "apply the rule to your own fresh skill" discipline is on file.
