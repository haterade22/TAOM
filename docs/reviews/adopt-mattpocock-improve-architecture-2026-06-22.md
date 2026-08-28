# Adoption review — mattpocock/skills `improve-codebase-architecture`

- **Date:** 2026-06-22
- **Source:** https://github.com/mattpocock/skills/tree/main/skills/engineering/improve-codebase-architecture
- **License:** MIT
- **Procedure:** [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)
- **Outcome:** SELECTIVE (Tier 1 only) — folded the Ousterhout "deep vs shallow module" lens + the concentrate-vs-move deletion test into `/improve`'s architecture audit. The skill itself, its HTML report, and its 3-skill companion suite were NOT adopted (duplicative of `/improve` + the docs/knowledge-graph + ADRs).

## What it is

One skill from mattpocock's `skills` repo (MIT, 141k stars, actively maintained). Two markdown files (SKILL.md + HTML-REPORT.md), **no scripts/hooks/config**. A 3-phase architecture-improvement workflow grounded in John Ousterhout's *A Philosophy of Software Design*:

1. **Explore** the codebase for "deepening opportunities" (turn shallow modules into deep ones), using a design vocabulary (module / interface / depth / seam / adapter / leverage / locality) that lives in a companion `/codebase-design` skill, plus the "deletion test."
2. **Present** candidates as a self-contained HTML report (Tailwind + Mermaid via CDN, written to OS temp, opened in a browser) with before/after diagrams + recommendation-strength badges.
3. **Grilling loop** — once the user picks one, run the companion `/grilling` skill to walk the design tree; side effects update a `CONTEXT.md` domain glossary via the companion `/domain-modeling` skill, and offer ADRs.

It is **not standalone** — it depends on three companion skills (`/codebase-design`, `/grilling`, `/domain-modeling`) and a `CONTEXT.md` convention.

## Security vet (gating) — verdict two ways

- **Foreign-skill vet:** `python tools/audit_claude_config.py --root <clone> --external` over both files → **no findings** (the first production use of TAOM's new `--external` foreign-skill vet, shipped same day in `adopt-skillspector-2026-06-22.md`).
- **Manual read:** HTML-REPORT.md is pure HTML/CSS scaffolding guidance. The only external resources are Tailwind + Mermaid CDNs, which load in the **user's browser** when they open the generated report — not at skill-execution time. The skill's only shell touch is `$TMPDIR` resolution + `xdg-open`/`open`/`start` to open the report. `disable-model-invocation: true`. No install, no exec, no exfil.
- **Verdict:** safe to LEARN FROM. There is no package/plugin to install (just skill text + an HTML template), so the port-never-install concern is minimal; per convention we adopt reviewed text, not the skill as-is.

## Novel vs duplicative

TAOM already has `/improve` (itself ported from shadcn/improve, 2026-06-12) — a "senior-advisor, never-implementer" whole-repo audit that scans, presents a vetted findings table, and writes self-contained handoff plans. The two skills are ~90% the same workflow.

| mattpocock idea | TAOM today | Disposition |
|---|---|---|
| Scan → present opportunities → advisor never edits → user picks one | `/improve` (near-identical) | Duplicative |
| Parallel Explore-agent codebase walk; ADR-conflict awareness | `/improve` Phase 1–2 + recon | Duplicative |
| Plans/findings are the product; "not worth doing" beats padding | `/improve` Hard Rules + tone | Duplicative |
| **"Deep vs shallow module" lens** (interface ≈ implementation complexity → abstraction doesn't earn its keep) + **concentrate-vs-move deletion test** | **Absent** — `/improve`'s architecture playbook has *god modules* (over-concentration), duplication, dead code, and ADR-violation detectors, but not the *under-abstraction / shallow-module* failure; `simplicity-criterion`'s deletion test is the *redundant-code* sense, not Ousterhout's | **NOVEL — adopted (Tier 1)** |
| Design vocabulary (depth / seam / leverage / locality) | Partial — "adapter" means ADR-007 here; "leverage" = impact/effort in `/improve` | Skipped — risks colliding with established TAOM meanings |
| HTML report (Tailwind+Mermaid via CDN, browser-open, OS temp) | `/improve` writes markdown plans in `plans/` | Skipped — CDN dependency, doesn't render offline, not git-friendly; markdown plans fit TAOM's executor-handoff model better |
| Companion suite: `/codebase-design`, `/grilling`, `/domain-modeling`, `CONTEXT.md` | TAOM uses ADRs + the docs/ knowledge graph + memories + `think-before-coding`'s lightweight design pass | Skipped — a different knowledge-management philosophy that overlaps TAOM's; porting this skill alone would leave 3 dangling skill references |

## What shipped (Tier 1)

- `.claude/skills/improve/references/audit-playbook.md` § Tech Debt & Architecture — a new bullet for **shallow modules** + the **deepening deletion test**, framed as the under-abstraction counterpart to the existing "god modules" over-concentration bullet, with the tells (forwarding wrapper, single-impl/single-caller adapter, testability-only pure-function extraction) and the concentrate-vs-scatter decision. MIT attribution inline.
- `.claude/rules/simplicity-criterion.md` § Relationship to other rules — one line distinguishing this rule's "is this code redundant?" deletion test from `/improve`'s "is this abstraction shallow?" deepening deletion test, so the two aren't conflated.

No new skill, no companion suite, no HTML report, no `CONTEXT.md` convention.

## Why not more

The honest answer to "what can we do more effectively": almost nothing here that `/improve` doesn't already cover — the workflow, the advisor-not-implementer stance, the ADR-awareness, the parallel exploration are all present. The single genuine gap was an *analytical lens* (shallow-module detection), which costs two doc bullets to close. Adopting the HTML report or the companion suite would have been adopting machinery TAOM already has in a different (and, for a solo C#/Bannerlord dev, better-fitting) form — markdown plans over browser HTML, ADRs + knowledge-graph + memories over `CONTEXT.md` + `/grilling`.

## Verification

- `python tools/audit_claude_config.py` (self-audit after the edits) — exit 0, no new findings.
- Both edits are documentation-only (a playbook reference + an always-load rule line); no code, no tests affected.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
