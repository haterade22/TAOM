# RCA — superpowers enforcement changeset review findings (2026-05-29)

## Context

While baking four adopted obra/superpowers disciplines into TAOM's workflow (new rule `evidence-over-claims.md`, two verification hooks, skill edits), an adversarial-review workflow (`enforcement-changeset-review`, 3 dimensions × find→verify) surfaced **17 findings: 10 confirmed, 7 rejected**. Of the 10 confirmed, 8 were "confirmed correct — no action" (the verify stage validating that the code was right). **Two were real defects worth fixing** (1 MED behavioral + skill-prose MED) plus minor style LOWs. None shipped — they were caught pre-commit. This RCA extracts the systemic lessons so the *classes* of defect can't recur, per the deep-review §3e "make the same category of bug impossible" mandate.

## Findings → root cause

| # | Sev | Finding | Root cause |
|---|-----|---------|------------|
| 1 | MED | `check-verification-evidence.sh` re-fired the reminder on **every** Stop while `.cs` stayed dirty — no muting, unlike sibling `check-deep-review.sh` | **RC1** |
| 2 | LOW | `mark-verification-run.sh` used `cat 2>/dev/null` + `printf` — diverged from the 13 sibling PostToolUse hooks' I/O preamble | **RC1** |
| 3 | LOW | jq-fallback precision tradeoff under-documented | RC1 (minor) |
| 9 | MED | Skill prose (`review-codex`/`deep-review`/`ship`) **amplified** `evidence-over-claims.md` rather than pointing to it — "enforcement theater" / context tax, most redundant in `ship` (which only delegates to `deep-review`) | **RC2** |
| 4,5,6,7,8,10 | LOW | confirmed-correct (deleted-file guard, missing-marker `-nt`, non-build filtering, concurrency, 2-hook justification, simplicity) — no action | — |

## Root causes

### RC1 — A new hook mirrored its sibling's *detection* convention but not its *full* convention set

`check-verification-evidence.sh` correctly copied `check-deep-review.sh`'s detection style (git state, no stdin, stderr reminder, `exit 0`) — but **only the parts in focus**. It missed the sibling's **muting** mechanism (early-exit when already-handled) and `mark-verification-run.sh` wrote a fresh stdin preamble (`cat 2>/dev/null` / `printf`) instead of copying a sibling's verbatim. The failure mode: *treating the sibling as a detection template instead of a full behavioral template.* Detection got audited; the surrounding conventions (muting, idempotency, I/O preamble, exit semantics) flew through.

This is the same shape as the documented C++-port failure (`feedback_native_port_hot_path_audit.md`): architectural intent consumes the audit budget; behavioral-preservation details ride along unaudited.

### RC2 — Discipline text was *amplified* into skills instead of *pointed to*

When folding `evidence-over-claims.md` into the review skills, some inserts restated the rule's rationale (the ~95% framing, the "hypothesis not verdict" prose) rather than pointing to the rule and adding only the **skill-specific delta**. The clearest case: `ship` Phase 1 restated the triage ordering that `deep-review` (which `ship` invokes) already carries. Result: soft guidance that reads like enforcement but isn't gated, plus per-invocation context tax — exactly what `simplicity-criterion.md` rejects (tiny marginal gain + recurring cost).

## Preventive actions (so the class can't recur)

1. **`harness-facts.md` → new "Authoring a new hook" checklist** (always-load, fires whenever hooks are touched): when adding a hook to an existing category, enumerate and consciously match-or-deviate on the sibling's FULL convention set — detection, **muting/idempotency**, I/O preamble (copy verbatim), exit semantics. Don't audit only the part you're focused on.
2. **`external-skill-ports.md` → authoring note**: when adding discipline text to a skill, point to the centralized rule and add ONLY the skill-specific delta; never restate the rule's rationale (that's amplification / context tax). If skill B just invokes skill A, don't duplicate A's guidance in B.
3. **Memory** `feedback_hook_authoring_mirror_siblings.md` for cross-session recall.

## What worked

The adversarial-review-then-verify workflow caught all of this **before commit** — and the verify stage correctly *rejected 7* not-real findings (e.g. a reviewer's wrong claim that a `dotnet` command had touched the marker; the redundant-with-deep-review claim). That is the `evidence-over-claims.md` discipline operating on itself: a finding is a hypothesis, verified against the source before action. The lesson is not "review harder" (the review worked) but "apply the RC1/RC2 checks during *authoring* so the review has less to find."

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
