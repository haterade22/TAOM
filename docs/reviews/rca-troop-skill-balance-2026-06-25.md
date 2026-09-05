# RCA — Deep Review: Troop Skill Balance (2026-06-25)

**Scope reviewed:** `tools/rebalance_troops.py` (curve + `detect_culture` id-routing + `SKIP_TROOP_IDS` + militia override + Δ-crash fix), `tools/analyze_troop_balance.py` (read-only overview + new level-monotonicity check), the full-roster rebaseline of 11 `troops_*.xml`, the new/changed cultural modifiers (goblin, mistymountainorcs, dale, dolguldur elite bump, mordor_uruk Black Uruk elite), the goblin-archer Bow buff, and `docs/features/troop-skill-balance.md`. Python tooling + XML data + docs — **no C#**.

**Review shape:** 4 tailored agents (the C#-centric core 5 are N/A for this changeset) — Tooling Correctness, Balance/Data-Integrity, XML-Integrity/Data-Flow, Completeness/Docs.

## Top-line

**No real bugs in the shipped work.** Agents B (balance) and C (XML integrity) comprehensively verified the data is correct: all 7 balance claims hold against the actual XML, 11/11 files parse clean, `validate_moduledata` PASS, the diff is values-only, the creature/elephant-rider exclusions are intact, and the intended hierarchy (Uruks > Orcs > Goblins; elves elite; Dale best non-elf polearm) is correctly encoded. Two notable Agent-A findings were **refuted** on verification; the remainder are cosmetic LOWs and design recommendations.

## Findings

| # | Sev (claimed → resolved) | Finding | Category | Resolution |
|---|---|---|---|---|
| 1 | HIGH → **REFUTED** | "Partial-skill-block troops silently lose formula skills on `--apply`" | Tooling | Intended design. Partial blocks are role-appropriate (an archer has no Polearm/2H; a crossbowman has no Bow). The rebalance correctly updates *present* skills onto the curve and leaves role-irrelevant skills absent. Agent B verified every partial-block troop's present skills are on-curve; `mordor_orc_archer` confirmed (Ath/1H/Bow/Thr only — correct for an archer). Adding all 8 skills would be a *design change*, not a fix. |
| 2 | MED → **REFUTED as stated** | "BOM preserved by accident; use `utf-8-sig`" | Tooling | The repo is **mixed-BOM** (5 of 16 troop files have a BOM, 11 don't). The current `utf-8` read+write correctly preserves *per-file* BOM state (BOM→U+FEFF-as-data→BOM; no-BOM→no-BOM), verified across all 11 changed files (parse OK, validate PASS). Agent A's proposed `utf-8-sig` write would **add** BOMs to the 11 non-BOM files — a regression. NOT FIXED. (A bytes-mode `had_bom` round-trip per `tools/README.md` would be more robust and string-preprocessing-safe, but is non-urgent and carries a CRLF-in-empty-block insertion caveat; tracked as latent.) |
| 3 | LOW → **CONFIRMED, fixed** | Dead `or True` in `analyze_troop_balance.py` `render_report` `dq_count` listcomp | Tooling | `len([r for r in dq['remap_findings'] if not r['in_mods'] or True])` — the `or True` makes the filter a no-op. The count is coincidentally correct (matches the HTML renderer's `len(dq['remap_findings'])`). Simplified to `len(dq['remap_findings'])` — deletion that holds parity. |
| 4 | LOW (latent) → **CONFIRMED, documented** | `apply_skills_via_regex` outer pattern `id="X".*?<skills>` (DOTALL) could cross into the NEXT troop if a future troop has NO `<skills>` element | Tooling | No current trigger — all 802 troops have a `<skills>` element. Latent foot-gun for future creature/stub troops. Documented as a known risk; not hardened now (regex change risks regressing working code; the empty/self-closing-block paths already cover the realistic cases). |
| 5 | LOW → **CONFIRMED, left as-is** | Redundant `'iron_hills' in troop_id or troop_id.startswith('iron_hills')` in `detect_culture` (first clause supersets the second) | Tooling | Harmless. Per `simplicity-criterion.md`, a tiny cosmetic gain isn't worth the churn on working code. Left as-is. |
| 6 | INFO → **design recommendation** | `dragon_wrath_*` (L21–46, `troops_rhun_new.xml`) and `orthanc_*` (L26–41, `troops_isengard.xml`) are elite named sub-lines that currently inherit their file's base modifier — like the Black Uruks did before the `mordor_uruk` split | Data/Design | NOT a bug — they sit cleanly on the rhun/isengard curve. But if either is intended as a *distinct elite faction* (as Black Uruks are vs Mordor orcs), it's a candidate for the same `detect_culture` id-routing. Surfaced to the owner as a design decision. |
| 7 | INFO | `mordor_uruk` comment "between Gundabad & Dol Guldur" + `dolguldur` "just under Isengard" | Docs | Aggregate-correct. Dol Guldur's *Polearm* (+18) edges Isengard's (+15), but its *total* modifier (+65) is below Isengard's (+75). The comments describe the aggregate tier, which holds. No change. |

## Disagreement (valuable signal)

Agent A (Tooling, haiku-equivalent depth) flagged the partial-block handling **HIGH** and the BOM **MED**; Agent B (Balance, sonnet) and direct verification **refuted both**. The lesson: a tooling agent reasoning about *what the code does mechanically* ("missing skills are never added") flagged a data-loss bug, but the *domain meaning* ("an archer shouldn't have Polearm") makes it intended. Cross-checking the mechanical finding against the data-domain agent's verification resolved it. This is exactly why the review runs both a mechanical-tooling agent and a domain-balance agent.

## Why this passed clean

The work was incrementally verified during authoring (dry-run delta inspection, `validate_moduledata` after each apply, the monotonicity check, balanced-diff confirmation), so the deep review found no shipped defects — only an over-flagged HIGH/MED (refuted), a dead-code LOW (fixed), and a design recommendation. There is no systemic-bug pattern to extract here; the durable lessons are the two refuted-finding categories (so a future reviewer doesn't re-raise them) — appended to `LESSONS-LEARNED.md`.

## Preventive actions

1. **Codify the two refuted categories** in `LESSONS-LEARNED.md` (Data/Content + Build/Tooling) so the next deep review of troop tooling doesn't re-flag partial blocks or the mixed-BOM handling. (Done.)
2. **Dead-code fix** applied (`or True`).
3. **Latent DOTALL** documented; revisit if a creature/stub troop without a `<skills>` block is ever added to a `troops_*.xml`.
4. **Design follow-up** offered: `dragon_wrath_*` / `orthanc_*` id-routing if they're meant to be distinct elite factions.

**Verdict: READY (already shipped). One dead-code cleanup applied; one design recommendation surfaced.**

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
