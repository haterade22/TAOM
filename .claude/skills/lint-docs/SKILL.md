---
name: lint-docs
description: Run the doc-health linter (tools/lint_docs.py) over docs/ and summarize dead links, stale version refs, orphan/missing feature docs. Use when auditing knowledge-base health or after large doc moves.
argument-hint: [--quick] [--write-report]
---

# Doc Lint

Runs `tools/lint_docs.py` and presents the findings inline. Backs [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md) (Knowledge-Base Architecture).

## What it checks

1. **Dead markdown links** — every `[text](path)` in `docs/` whose target doesn't resolve relative to the source file. Excludes external URLs, file:// links, code-fence and inline-code spans, codex transcripts (codex-adversarial-*, codex-prompt-*, codex-result-*), TEMPLATE.md, and docs/archive/.
2. **Stale version refs** — `1.3.15`, `1.3.x`, `Bannerlord 1.3` mentions outside `docs/migration/` and `docs/archive/`. Current target is **v1.4.5**; new feature docs and reviews should reference the current version. Historical RCA / codex-adversarial files are exempted (they intentionally cite the version under review).
3. **Orphan feature docs** — files in `docs/features/` that no other doc references. Either link them into `docs/INDEX.md` / a feature doc / an RCA, or delete them.
4. **Missing feature docs** — `Main/Features/<X>/` directories with no matching `docs/features/<x>.md` (PascalCase→kebab-case + fuzzy match, same algorithm as `.claude/hooks/detect-docs-gaps.sh`).

## Modes

- `$ARGUMENTS` empty or `--full` → run all four checks.
- `$ARGUMENTS` contains `--quick` → only check dead links (fastest; suitable for tight loops).
- `$ARGUMENTS` contains `--write-report` → write to `docs/reviews/doc-lint-<YYYY-MM-DD>.md` instead of streaming inline.

## Steps

### Step 1: Run the linter

If `--write-report` is requested:

```bash
python tools/lint_docs.py --report "docs/reviews/doc-lint-$(date +%Y-%m-%d).md"
```

Otherwise stream to stdout (capture full output via Bash):

```bash
python tools/lint_docs.py
```

For the `--quick` variant, add `--quick` to the command.

### Step 2: Summarize the top categories

After the script runs, **read the report** (either the written file or the captured stdout). Then write a one-screen summary back to the user with these sections:

- **Totals** — one line per category with counts.
- **Quick fixes (≤5 items)** — pick the top 5 issues most worth fixing right now. Prefer:
  - Dead links where the target file was clearly renamed (closest-match candidate visible)
  - Missing feature docs (one entry — author from TEMPLATE.md)
  - Stale-version refs clustered in a single recent doc (one-file batch fix)
- **Deferred / informational** — categories the user should know about but probably won't fix today (e.g., 184 stale-version refs spread across 30 files).

Do **not** auto-fix anything. The user decides what to fix. This skill is diagnostic.

### Step 3: Offer next actions

- If dead links cluster in one file → offer to read the file and propose fixes.
- If a memory-link path uses `../../C:/Users/mikew/.claude/projects/...` → mention that these paths bake in a per-user home dir; suggest referencing the memory filename in prose instead of linking.
- If a feature doc is missing → offer to scaffold via `docs/features/TEMPLATE.md`.
- If the user wants the full per-file list → point at the written report path.

## Out of scope (don't do these here)

- Don't run `/build-fix`, `/verify`, or any code-touching skill — this is a doc-only diagnostic.
- Don't rewrite stale-version refs en masse without confirming with the user — the linter is a generous regex; some `1.3.15` mentions are intentional history.
- Don't generate `## Referenced by` footers — that's `/build-backlinks` (Phase 3), not this skill.

## See also

- [ADR-010 Knowledge-Base Architecture](../../../docs/adrs/010-knowledge-base-architecture.md)
- [docs/INDEX.md](../../../docs/INDEX.md)
- `.claude/hooks/detect-docs-gaps.sh` — the SessionStart hook this skill's "missing feature docs" check builds on
