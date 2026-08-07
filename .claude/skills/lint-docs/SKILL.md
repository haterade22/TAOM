---
name: lint-docs
description: Run the doc-health linter (tools/lint_docs.py) over docs/ and summarize dead links, stale version refs, orphan/missing feature docs. Use when auditing knowledge-base health or after large doc moves.
argument-hint: [--quick] [--write-report]
---

# Doc Lint

Runs `tools/lint_docs.py` and presents the findings inline. Backs [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md) (Knowledge-Base Architecture).

## What it checks

1. **Dead markdown links** — every `[text](path)` in `docs/` whose target doesn't resolve relative to the source file. Excludes external URLs, file:// links, code-fence and inline-code spans, codex transcripts (codex-adversarial-*, codex-prompt-*, codex-result-*), TEMPLATE.md, and docs/archive/. Also skips any link whose **target** lands under `docs/reviews/raw/` — that dir is gitignored (`.gitignore:157`; the transcripts are 2-4 MB each and stay on the reviewing machine), so those links resolve for whoever ran the review and are dead on every other clone. Exempting by target, not by linking file, keeps `REVIEW-LOG.md`'s several hundred other links covered.
2. **Stale version refs** — a version string **presented as the current target** when the pin says otherwise. Not a mention: "Ported for TAOM v1.3.15" and "v1.3.15 reference RVA, informational only" are true statements about history and are correct as written. Current target is **v1.4.7** (the pin at `.claude/pinned-game-version.txt`).

   Three things narrow it to that meaning (#399): the line must carry a present-tense marker (`current`, `target`, `now`, `active`, `supported`, `builds against`, `pinned to`), tested with **inline code stripped** so `` `CampaignTime.Now` `` is an identifier rather than a claim; a line that **also names the pin** is contrasting two versions, not calling the old one current; and `docs/adrs/` joins `docs/migration/` + `docs/archive/` + `docs/audits/` + `docs/changelog-archive/` as exempt, since an ADR names the versions that were current when the decision was taken. Historical RCA / codex-adversarial / REVIEW-LOG / audit-* files are exempt by filename.

   **Known gaps — do not read a clean run as "no rot" ([#405](https://github.com/haterade22/TAOM/issues/405)).** Only marker-word phrasing fires, so `built for` / `requires` / `runs on` / a bare `Engine:` label are missed. Version patterns cover only `1.3.15` / `Bannerlord 1.3` / `v1.3.x` — **v1.4.5 and v1.4.6 are not matched at all**, even though `agent-operating-manual.md` names them stale. And a line naming the pin *and* wrongly calling an older version current is suppressed by design.
3. **Orphan feature docs** — files in `docs/features/` that no other doc references. Either link them into `docs/INDEX.md` / a feature doc / an RCA, or delete them.
4. **Missing feature docs** — `Main/Features/<X>/` directories with no matching `docs/features/<x>.md` (PascalCase→kebab-case + fuzzy match, same algorithm as `.claude/hooks/detect-docs-gaps.sh`).
5. **Config-example drift** — a `docs/features/*.md` `json` example whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json` config it mirrors (shared keys only — a partial example showing a subset is fine), or a doc key the shipped config no longer has. Historical docs (migration/archive/reviews) exempt. The v1.4.7 case: flipping a config default left the feature doc's example showing the old value, invisibly. **Blocks commits** via `.claude/hooks/check-doc-config-drift.sh`.
6. **Version mismatch** — CLAUDE.md's "Target: Bannerlord X" line(s) or an API-snapshot header that disagrees with `.claude/pinned-game-version.txt`. Catches "pin bumped but a doc/snapshot left stale" (run `/engine-bump` if the pin itself is behind the installed game). Also gated by the same hook.

## Modes

- `$ARGUMENTS` empty or `--full` → run all six checks.
- `$ARGUMENTS` contains `--quick` → only check dead links (fastest; suitable for tight loops).
- `$ARGUMENTS` contains `--write-report` → write to `docs/reviews/doc-lint-<YYYY-MM-DD>.md` instead of streaming inline.
- `--fail-on-drift` → exit 1 if any config-example drift OR version mismatch is found. This is the mode the `check-doc-config-drift.sh` pre-commit hook runs; the other four checks never block a commit.

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
- **Deferred / informational** — categories the user should know about but probably won't fix today (e.g., orphan feature docs spread across 30 files).

Do **not** auto-fix anything. The user decides what to fix. This skill is diagnostic.

**A zero is not self-validating.** A checker reporting no findings looks identical to a checker that has been exempted into silence — that is exactly how the stale-version check reached 29/29 false positives before anyone re-read it. `tools/tests/test_lint_docs.py::test_naming_an_old_version_as_the_current_target_is_still_reported` is the fixture separating the two states. If a run is clean, say the tests are green too, and never resolve a noisy check by widening an exemption without a fixture proving the check still fires.

### Step 3: Offer next actions

- If dead links cluster in one file → offer to read the file and propose fixes.
- If a memory-link path uses `../../C:/Users/mikew/.claude/projects/...` → mention that these paths bake in a per-user home dir; suggest referencing the memory filename in prose instead of linking.
- If a feature doc is missing → offer to scaffold via `docs/features/TEMPLATE.md`.
- If the user wants the full per-file list → point at the written report path.

## Out of scope (don't do these here)

- Don't run `/build-fix`, `/verify`, or any code-touching skill — this is a doc-only diagnostic.
- Don't rewrite stale-version refs en masse without confirming with the user. Since #399 the check aims only at present-tense claims, so a hit is more likely to be real — but "fix the docs" is still the wrong first move when a run is noisy. #397's 29 findings were all accurate prose and a wrong checker; editing them would have falsified the record.
- Don't generate `## Referenced by` footers — that's `/build-backlinks` (Phase 3), not this skill.

## See also

- [ADR-010 Knowledge-Base Architecture](../../../docs/adrs/010-knowledge-base-architecture.md)
- [docs/INDEX.md](../../../docs/INDEX.md)
- `.claude/hooks/detect-docs-gaps.sh` — the SessionStart hook this skill's "missing feature docs" check builds on
