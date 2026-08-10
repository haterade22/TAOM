# Doc-Health Linter (`tools/lint_docs.py`)

## Overview

One read-only pass over `docs/` (plus `CLAUDE.md`, `AGENTS.md`, and the shipped ModuleData configs)
that reports **doc rot** — the class of defect where the code moved and the prose did not. Seven
checks, pure stdlib, no game install required. Three of the seven **block a commit** through
`.claude/hooks/check-doc-config-drift.sh`; the other four are advisory.

Skill entry point: `/lint-docs`. Backs [ADR-010](../adrs/010-knowledge-base-architecture.md).
Sibling validators: [moduledata-validation.md](moduledata-validation.md) (game data),
[mesh-ref-validation.md](mesh-ref-validation.md) (assets), [doc-graph.md](doc-graph.md) (link topology).

## Why This Exists

A knowledge base that a future session is supposed to trust *instead of re-decompiling* is only worth
the trust if its claims are still true. Three failures motivated the checks, each caught after the
fact:

- **A config default flipped and its feature doc kept showing the old value.** Invisible to review —
  the doc read fine in isolation. Now `check_config_example_drift`, and it blocks commits.
- **The pin was bumped and `CLAUDE.md` / the API snapshot were left behind.** The repo sat at pin
  v1.4.6 with a v1.4.5 snapshot header during the v1.4.7 bump. Now `check_version_consistency`.
- **`AGENTS.md` grew to 112 KB with the review RULES starting at byte ~83.5 K**, past Codex's
  `project_doc_max_bytes` — so every Codex review ran without them. Now the eager-load budget check.

The fourth lesson is about the linter itself and is the reason the "what counts as a finding"
section below is so specific: **a check that is wrong every time is a deleted check.** The
stale-version check spent months at 29 findings and 0 true positives, which trains every reader to
skip the whole report — including the six checks that were correct.

## The seven checks

| # | Check | Blocks a commit? | What it means |
|---|---|---|---|
| 1 | **Dead links** | no | a `[text](path)` in `docs/` whose relative target does not resolve |
| 2 | **Stale version refs** | no | a version string *presented as the current target* when the pin says otherwise — see below |
| 3 | **Orphan feature docs** | no | a `docs/features/*.md` that **no other file under `docs/` links to**. Self-links don't count, and links from outside `docs/` (e.g. `tools/README.md`, a skill body) do **not** clear it |
| 3b | **Prose trapped in a backlinks region** | no | authored text sitting BETWEEN a file's `backlinks-start` / `backlinks-end` markers. `build_backlinks.py`'s `splice_footer` keeps only `content[:start] + regenerated footer + content[end:]`, so anything in there is deleted on its next run — no error, no conflict. The region may hold only blank lines, `## Referenced by`, and the generated link list. Uses the generator's own `rfind` semantics so it identifies the SAME region that will be rewritten, and skips `docs/reviews/raw/` (verbatim transcripts routinely QUOTE a footer). |
| 4 | **Missing feature docs** | no | a `Main/Features/<X>/` with no `docs/features/<x>.md`. PascalCase→kebab plus a fuzzy match (exact, `-system` suffix, prefix, substring either way) — the same algorithm as `.claude/hooks/detect-docs-gaps.sh` |
| 5 | **Config-example drift** | **yes** | a `docs/features/*.md` ```json block labelled with a config path whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json`, or a doc key the shipped config no longer has. Shared keys only, so a partial example is fine; an unparseable (annotated) block is skipped rather than guessed at |
| 6 | **Version mismatch** | **yes** | `CLAUDE.md`'s `Target: Bannerlord X`, `AGENTS.md`'s `mod for Bannerlord X`, or a `(vX snapshot)` header in `docs/reference/taleworlds-api-snapshot/{gamemodel-bases,patch-targets}.md` disagreeing with `.claude/pinned-game-version.txt` |
| 7 | **CLAUDE.md / AGENTS.md eager budget** | **yes** (except `size-warn`) | size caps plus per-line caps on CLAUDE.md. Both files load into every session and every agent spawn, so bytes here are a permanent per-turn tax |

**Why 3b exists (2026-08-09).** `docs/features/enlistment.md` had **51 lines** of a live-session
record sitting below its `backlinks-start` marker, and the regeneration was already armed — today's
handoff doc had become the file's 4th inbound reference while the footer still listed 3. It was
found by a doc-drift sweep, moved out, and the guard written. Running the generator during that same
pass then destroyed **50 lines of `REVIEW-LOG.md`** (Review 84's entire record) for exactly the same
reason, which had to be restored from `HEAD` — the bug demonstrating itself mid-fix is the reason
this is a check and not a note.

`--fail-on-drift` gates on **5, 6, and 7** — checks 1-4 never block. Within 7, only hard violations
gate; a `size-warn` finding is report-only by design.

### Budget constants (check 7)

| File | Warn | Hard cap | Current |
|---|---|---|---|
| `CLAUDE.md` | 44,000 B | 46,000 B | 28,070 B (2026-08-07) |
| `AGENTS.md` | 40,000 B | 44,000 B | 38,879 B (2026-08-07) — inside the warn band |

Plus, on `CLAUDE.md` only: table rows ≤ 400 chars, non-table prose lines ≤ 600 chars, fenced code
exempt. A row is an index entry; prose belongs in the doc it links to.

## What "stale" means (check 2, the model that took two attempts)

**A version string is not rot. A version string presented as the current target is.** "Ported for
TAOM v1.3.15" and "v1.3.15 reference RVA, informational only" are true statements about history and
are correct as written — rewriting them to satisfy a linter falsifies the record.

Getting to that meaning took three narrowings on top of the raw version-pattern match (#399):

1. **A present-tense marker must appear on the line** — `current(ly)`, `target(s|ed|ing)`, `now`,
   `active`, `supported`, `builds/building against`, `pinned to`.
2. **That marker test runs with inline code stripped.** A present-tense *word* inside backticks is an
   identifier, not a claim: `` `CampaignTime.Now` `` in an API-break note is the case that forced
   this. The version match itself still runs on the raw line, so a `` `1.3.15` `` in backticks counts.
3. **A line that also names the pin is contrasting two versions, not calling the old one current** —
   "that mod ships a v1.3.15-only DLL. TAOM tracks the current engine (v1.4.7)".

The failed first attempt is worth knowing, because it is the tempting one: the 2026-08-05 sweep tried
to fix the noise with **whole-file** exemptions and got stuck at 29, because the remaining sites lived
in docs that must stay linted (`native-skin-fixes.md` alone held 10). The distinction lives per
**line**, so the fix had to. Measured on the same 29: the line-granular model cleared 26, the
wording-marker approach cleared 16.

## Exemption surface

Every exemption, and the property it keys on:

| Scope | Applies to | Rationale |
|---|---|---|
| `docs/{migration,archive,audits,changelog-archive,reviews/raw}/` | checks 2 + 5 | point-in-time records |
| `docs/adrs/` | checks 2 + 5 | an ADR names the versions that were current when the decision was taken. Added in #399 — the exemption comment had claimed ADRs were covered since the beginning, but the tuple never included them, so `adrs/010` reported itself as rot |
| filename contains `rca-`, `codex-adversarial-`, `codex-prompt-`, `codex-result-`, `doc-lint-`, `codex-track-record`, `review-lessons-archive`, `REVIEW-LOG`, `audit-` | checks 2 + 5 | review transcripts cite the version under review on purpose |
| path contains `docs/reviews/lessons` | check 2 | a lesson quotes the old version as its context |
| `docs/{archive,changelog-archive,reviews/raw}/` + codex/`doc-lint-` transcripts | check 1 | their links captured past state |
| **link target** resolves under `docs/reviews/raw/` | check 1 | see below |
| `TEMPLATE.md` | check 1 | placeholder links |

**The `raw/` exemption keys on the target, not the linking file, and that is deliberate.**
`docs/reviews/raw/` is gitignored (`.gitignore:157` — the Codex transcripts are 2-4 MB each and stay
on the reviewing machine), so 14 links resolved for whoever ran the review and were dead on every
other clone. Exempting the *linking* files instead — `rca-*` and `REVIEW-LOG` are already exempt from
check 2, so it looks consistent — would have switched off dead-link coverage for `REVIEW-LOG.md`'s
several hundred other links.

Generalise that: **whenever a check reads untracked or gitignored paths, its output differs per
machine.** Verify against a fresh clone, or simulate the absent directory, before believing a clean
local run.

## Known blind spots — do not read a clean run as "no rot"

Tracked as [#405](https://github.com/haterade22/TAOM/issues/405). All in check 2:

- **Only marker-word phrasing fires.** Measured against the merged linter, these are all missed:
  `TAOM is built for Bannerlord 1.3.15.` · `This feature requires Bannerlord 1.3.15.` ·
  `TAOM runs on Bannerlord 1.3.15.` · `Compatible with Bannerlord 1.3.15.` ·
  `We are on Bannerlord v1.3.15.` · `Decompile against Bannerlord 1.3.15 before editing.` ·
  `Engine: 1.3.15`.

  Widening the marker set to cover them costs **zero** new findings on the tree as it stands —
  measured, so it is a regex line plus fixtures, not an exemption pass.

  *(Writing this section tripped the checker twice — an old version string and a present-tense
  marker landing on one source line, where the marker belonged to a different noun. Both were
  cleared the way the rules prescribe: reword, or name the pin v1.4.7 on the line so gate 3 reads it
  as a contrast. The marker test reads the LINE, not the sentence — worth remembering before
  reflowing a paragraph that quotes old versions.)*
- **`STALE_VERSION_PATTERNS` covers `1.3.15` / `Bannerlord 1.3` / `v1.3.x` only.** v1.4.5 and v1.4.6
  are not matched at all, even though `docs/ai-includes/agent-operating-manual.md` names them stale.
  Adding them yields **21 findings** that need triage — a genuine mix, including real present-tense
  claims (`native-skin-fixes.md:16` "targets v1.4.6", `REVIEW-PLAN.md:97` "targets v1.4.5").
- **A line naming the pin *and* wrongly calling an older version current is suppressed** — the
  documented cost of resolving the contrast lines in the checker rather than by editing docs.

## How to run + interpret

```bash
python tools/lint_docs.py                  # full markdown report to stdout
python tools/lint_docs.py --summary        # --- delimited, grep-friendly counts
python tools/lint_docs.py --quick          # dead links only (fastest; tight loops)
python tools/lint_docs.py --report docs/reviews/doc-lint-$(date +%F).md   # atomic .tmp+rename
python tools/lint_docs.py --fail-on-dead   # exit 1 on any dead link
python tools/lint_docs.py --fail-on-drift  # exit 1 on checks 5/6/7 — the pre-commit gate
```

`--summary` emits one line per check plus `total_findings`, which is the form to assert against in a
script.

Interpreting a **noisy** run: the first move is not "edit the docs". #397's 29 findings were all
accurate prose and a wrong checker. Ask which side is wrong before changing either.

Interpreting a **clean** run: a checker reporting zero and a checker exempted into silence produce
byte-identical output. Say the tests are green alongside the zero, and read the blind-spot list above
before treating it as an all-clear.

## Key Files

| File | Role |
|---|---|
| `tools/lint_docs.py` | the linter — all seven checks, ~757 lines, stdlib only |
| `tools/tests/test_lint_docs.py` | 23 unit tests over synthetic repo trees |
| `.claude/skills/lint-docs/SKILL.md` | `/lint-docs` — run + summarize; diagnostic, never auto-fixes |
| `.claude/hooks/check-doc-config-drift.sh` | pre-commit gate; runs `--fail-on-drift` |
| `.claude/pinned-game-version.txt` | the pin checks 2 and 6 read |
| `.claude/hooks/detect-docs-gaps.sh` | SessionStart sibling of check 4; shares the slug algorithm |

## Dependencies

Python 3.9+ stdlib only (`argparse`, `json`, `re`, `pathlib`, `urllib.parse`, `dataclasses`).
`Path.is_relative_to` sets the 3.9 floor. No game install, no network, no third-party packages —
which is why this is one of the few TAOM gates that can run in CI unchanged.

## Tests

`python -m unittest discover -s tools/tests -p "test_lint_docs.py"` — 23 tests, all synthetic trees
in a tempdir with `lint_docs`'s module-level path constants repointed (`_TempRepo` /
`_PathConstantRepo`). Repointing `DOCS_DIR` alone is not enough: `ADRS_DIR`, `REVIEWS_RAW_DIR` and the
exempt-prefix tuples are computed at import time, so a test that misses them lints its synthetic tree
against the **real** repo's exemptions.

**`test_naming_an_old_version_as_the_current_target_is_still_reported` is load-bearing.** It is the
only thing distinguishing "the check is quiet because the docs are clean" from "the check is quiet
because it is dead." Every future narrowing ships with a fixture of this shape, and no exemption pass
may delete this one.

## Changelog

- **2026-08-07** — #399 (`fa7ba39b`, external contribution): stale-version model rewritten from "older
  than the pin" to "presented as the current target"; dead links skipped by target under
  `docs/reviews/raw/`; `docs/adrs/` added to the exempt prefixes; `_read_pin()` extracted. 43 findings
  → 0 with no doc content edited, 9 tests added (23 total). Blind spots filed as #405. Closes #397.
- **2026-07-18** — AGENTS.md size budget added to check 7 (the Codex `project_doc_max_bytes`
  truncation guard).
- **2026-07-12** — CLAUDE.md budget enforcement flipped on after the KB decomposition.
- **v1.4.7 bump** — checks 5 (config-example drift) and 6 (version consistency) added, plus the
  `check-doc-config-drift.sh` pre-commit gate.
- **ADR-010 Phase 2** — initial four checks (dead links, stale versions, orphan docs, missing docs).

## GitHub Issue

- [#397](https://github.com/haterade22/TAOM/issues/397) — stale-version check was ~100% false
  positives (closed by #399)
- [#399](https://github.com/haterade22/TAOM/pull/399) — the fix
- [#405](https://github.com/haterade22/TAOM/issues/405) — closed: marker-word narrowing + v1.4.5/v1.4.6
  never matched

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
