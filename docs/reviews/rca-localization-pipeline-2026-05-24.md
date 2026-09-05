# RCA — Localization Pipeline Deep-Review Findings

**Date:** 2026-05-24
**Scope:** Commits 831ac36 (gap-fill), 20713a1 (XSLT keys), 3e43c88 (docs) — full localization-AI-pipeline session.
**Reviewer:** /deep-review (5 core agents)
**Findings:** 1 HIGH + 1 LOW. Both fixed in same session.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | HIGH | `write_back()` in `translate_with_claude.py` compiled N regex patterns + ran N `subn()` calls on the full file (~10-30s per file × 12 langs ≈ 5min wasted per full-suite run) | Perf — algorithmic | Pattern was correct for small N (the early pilot batches of 50 entries). Scaled badly when XSLT batch hit 1,431 entries × 12 langs. Never benchmarked at full scale because cache made re-runs fast — masked the perf issue. | When designing a function that processes a translation set, prefer "compile once, apply once" — single regex with id alternation + dictionary lookup in the replacement callback. This is now in [`tools/translate_with_claude.py:362-380`](../../tools/translate_with_claude.py). General rule: any function that takes a `dict[str, str]` and substitutes per-entry into a single file should batch-compile. |
| 2 | LOW | SP `lang_name` differed between `translate_with_claude.py` ("Latin American Spanish") and `rebuild_translation_files.py` ("Spanish (LA)") — latent: only activated if translate-output is consumed directly without going through rebuild | DRY violation — duplicated config | The two scripts hand-maintain duplicate `LANGUAGES` dicts. When the second script was added, the SP entry was hand-typed differently from the original. No automated check caught the mismatch because the rebuild script is currently the canonical write path, masking the divergence in the unused translate-direct-write path. | One-off fix is sufficient (alignment commit). General preventive: when a config dict is duplicated across two tools in the same `tools/` directory, the second tool's dict should `import` from the first. **Not refactoring now** — both tools are stable, and the failure mode is contained to one config row. Logging this as a deferred follow-up rather than refactoring mid-session. |

## Root-cause pattern (shared theme)

Both findings stem from a **duplicate-logic-between-two-Python-tools** pattern. `translate_with_claude.py` and `rebuild_translation_files.py` independently:
- Define the LANGUAGES dict (12 entries each)
- Define source-XML lists (the SOURCES table)
- Define XML escape logic
- Parse `{=KEY}default` syntax

When the rebuild script was added (commit 831ac36) to fix the write-back-into-empty-stubs bug, it copied logic from the translate script rather than extracting shared helpers. The duplication created two failure modes simultaneously:

1. The translate script's `write_back()` perf bug was never benchmarked at scale because the rebuild script became the canonical write path → write_back's slow code never noticed.
2. The duplicate LANGUAGES dict diverged at the SP row.

**Future preventive action (deferred):** extract `tools/_loc_common.py` with `LANGUAGES`, `SOURCES`, `escape_xml_attr()`, `parse_source_with_keys()`. Both scripts import from it. Not refactoring now to keep this RCA-driven fix surgical, but flagged for the next localization-tool change.

## Why each agent missed (or caught) these

| Agent | Caught? | Why |
|-------|---------|-----|
| **1 (Standards)** | No | Standards is for ADRs / formatting / naming. Algorithmic perf and DRY violations are outside its remit. Correctly out of scope. |
| **2 (Bannerlord API)** | No | Tools are Python — no TaleWorlds API surface. Out of scope. |
| **3 (Efficiency)** | **YES** for finding #1 (HIGH). | Identified the N-regex-compile loop on the first read. This is exactly what the Efficiency agent is for. Working as intended. |
| **4 (Completeness)** | No | Completeness checks docs/tests/issues — not internal code quality. Out of scope. |
| **5 (Data Flow)** | **YES** for finding #2 (LOW). | Cross-system trace caught the duplicate LANGUAGES dict divergence. This validates the data-flow agent's design — it's the only agent that compares parallel code paths for consistency. |

Both findings caught by the agent designed for that finding class. **No prompt updates needed**, no scope gaps in the existing agent suite.

## Feedback memories to codify

No new feedback memory needed. Both findings are one-off bugs in newly-written tooling that the existing agent rules already catch correctly. The "compile-once, apply-once" pattern is a generic Python perf idiom not specific enough to TAOM to warrant a new memory.

If a third localization tool is added in the future and the duplicate-dict pattern bites a third time, **then** create a `feedback_extract_loc_common_module.md` memory and do the shared-module refactor. One occurrence = bug, two = pattern, three = rule.

## Fix commits

- write_back O(N×size) → O(file_size) single-regex pattern: see this commit
- SP lang_name alignment: see this commit
- RCA file: this file

No code-architecture changes. No breaking API changes. No re-translation needed (the bug was perf-only — output was always correct).

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/reviews/REVIEW-LOG.md](./REVIEW-LOG.md)

<!-- backlinks-end -->
