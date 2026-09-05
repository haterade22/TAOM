# RCA — SettlementEconomy (#317) deep review, 2026-07-02

**Top-line:** 6-agent deep review (5 core + Step 2c tooling agent) on the town-gold regen feature +
prosperity tools. C# feature: clean (Standards/Efficiency/Completeness/Data-Flow all PASS; API
compat 12 verified / 0 incompatible, 1 advisory). Tooling agent: **2 HIGH** (idempotency broken
under the optional flags) + 1 MED (BOM idiom vs documented convention). All findings fixed
in-session and proven (in-memory apply→recompute harness: 0 changes on run 2 for all 6 flag
combinations). Nothing shipped — every finding was caught in-review — but the authoring-time
lesson is systemic.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|-----------|-------------------|
| 1 | HIGH | `--preserve` / `--pin-zero-village` broke the docstring's idempotency claim: frozen fiefs kept participating in the quantile-map **ranking population**, so after `--apply` their unchanged values shifted every free fief's rank on the next run — unbounded per-run drift (agent repro: every non-preserved castle climbed one quantile step per run until the 5600 cap) | Tooling | The idempotency claim was written from the **default-path** monotonicity argument (rank order preserved under the map ⇒ fixed point). The optional flags were added in the same authoring pass as post-hoc `targets` overwrites, and the claim was never re-checked against them — the proof covered a subset of the behavior the docstring asserted for the whole tool | Frozen fiefs now **excluded** from the ranking population (`compute_targets` ranks free fiefs only, then assigns pin-floor/preserve values). Invariant proven per flag combination. Lesson appended to LESSONS-LEARNED (test invariants per flag set) |
| 2 | HIGH | `--town-uplift N` stacked cumulatively: the post-hoc `+N` landed on top of the lift-only clamp, and after `--apply` the clamp floor already contained the previous `+N` — every identical re-run added another `+N` (agent repro: +50 per run, unbounded to cap) | Tooling | Same root cause as #1 — flag written after the invariant proof, never tested against it | Uplift moved **inside** `_quantile_map` (added to the quantile target *before* the lift-only `max(current, …)` clamp): after apply, `current ≥ quantile+N`, so re-runs are no-ops. Proven in the harness |
| 3 | MED | Both new scripts use the byte-round-trip BOM idiom (`decode('utf-8')` keeping U+FEFF as a char) while `tools/README.md`'s "XML I/O convention" mandates the `utf-8-sig` + explicit re-prepend idiom. Functionally byte-identical (agent verified the round-trip), but a silent house-style fork | Tooling / convention | The scripts copied `Assign-SettlementOwners.py` verbatim per the design brief — that sanctioned sibling itself predates the README convention. Same drift class as `rca-career-ui-revamp-2026-05-30.md` | `tools/README.md` now names the symmetric byte-round-trip as a **sanctioned alternative** (both idioms valid; don't mix within one script). Also added a defensive comment on `apply_to_file`'s latent DOTALL cross-boundary leak (unreachable today; documented so a future reuse doesn't trip it) |
| 4 | ADVISORY | `TaomSettlementEconomyModel.GetTownGoldChange`'s `town == null` arm routed to `base.GetTownGoldChange(town)`, which itself NREs on `town.Prosperity` — a guard that read as protective while deferring the crash. Dead branch in practice (the daily ticker never passes null) | C# | Guard mirrored the sibling's defensive style without asking what the base call does with the guarded value | Null now returns `0` (no gold change); Enabled-off keeps the true base passthrough. Caught by the API-compat agent, fixed same session |

## Root-cause pattern (findings 1+2)

**An invariant proved for the default path was asserted for the whole tool.** The quantile map's
idempotency proof (monotonic rank ⇒ fixed point) is genuine — and the optional flags each broke
its premise by mutating `targets` *outside* the ranked map while leaving the mutated fiefs *inside*
the next run's ranking input. The docstring asserted "a second run must report 0 changes"
unconditionally. The verification gate I ran before review (dry-run + planned post-apply re-run)
would only ever have exercised the default path.

## Why each agent missed / caught these

- **Agents 1–5 (core):** C#-scoped by design — none reads Python tooling. Correct behavior, not a
  gap: the Step 2c expansion rule ("changeset includes `tools/**/*.py` that WRITE outside the repo
  → launch a tooling agent") existed precisely for this (post `rca-scene-tooling-2026-05-28`) and
  **fired as designed**. This RCA is the second confirmation that the tooling-agent trigger earns
  its place.
- **Agent 2 (API compat)** caught the advisory null-guard (#4) by tracing what `base` does with the
  guarded argument — the exact "verify the fallback path actually survives the input" discipline
  from `csharp-architecture.md`'s lookup-fallback rule, applied to a guard clause.
- **The tooling agent** caught 1–3, including constructing minimal reproductions and proving the
  default path clean via randomized trials — findings were CONFIRMED, not plausible-and-wrong.

## Feedback memories to codify

One durable lesson (appended to `docs/reviews/LESSONS-LEARNED.md` → Build, Tooling & Workflow):
**test an advertised tool invariant per flag combination, not just the default path** — an
in-memory apply→recompute harness costs ~20 lines and turns the docstring claim into a checked
property. No new always-on rule warranted beyond that (the tooling-agent trigger already exists
and worked).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
