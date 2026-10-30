# Doc-Health Linter (`tools/lint_docs.py`)

## Overview

One read-only pass over `docs/` (plus the files CLAUDE.md loads, `AGENTS.md`, `.claude/rules/`, and
the shipped ModuleData configs) that reports **doc rot**: the class of defect where the code moved
and the prose did not. Pure stdlib, no game install required. Checks 5 to 8 below **block** a
commit through `.claude/hooks/check-doc-config-drift.sh` (the GitHub workflow that also ran them on
every push was removed on 2026-10-05); the others are advisory.

Run `python tools/lint_docs.py` directly. Backs [ADR-010](../adrs/010-knowledge-base-architecture.md).
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
- **CLAUDE.md regrew from 28 KB to 49 KB in six weeks, past its own cap,** because the only gate was
  a Claude PreToolUse hook and commits made from the IDE never run it. Now the ADR-011 context
  budget (check 7), enforced by the commit hook (#647).

The fourth lesson is about the linter itself and is the reason the "what counts as a finding"
section below is so specific: **a check that is wrong every time is a deleted check.** The
stale-version check spent months at 29 findings and 0 true positives, which trains every reader to
skip the whole report — including the six checks that were correct.

## The checks

| # | Check | Blocks a commit? | What it means |
|---|---|---|---|
| 1 | **Dead links** | no | a `[text](path)` in `docs/` whose relative target does not resolve |
| 2 | **Stale version refs** | no | a version string *presented as the current target* when the pin says otherwise — see below |
| 3 | **Orphan feature docs** | no | a `docs/features/*.md` that **no other file under `docs/` links to**. Self-links don't count, and links from outside `docs/` (e.g. `tools/README.md`, a skill body) do **not** clear it |
| 3b | **Prose trapped in a backlinks region** | no | authored text sitting BETWEEN a file's `backlinks-start` / `backlinks-end` markers. `build_backlinks.py`'s `splice_footer` keeps only `content[:start] + regenerated footer + content[end:]`, so anything in there is deleted on its next run — no error, no conflict. The region may hold only blank lines, `## Referenced by`, and the generated link list. Uses the generator's own `rfind` semantics so it identifies the SAME region that will be rewritten, and skips `docs/reviews/raw/` (verbatim transcripts routinely QUOTE a footer). |
| 4 | **Missing feature docs** | no | a `Main/Features/<X>/` with no `docs/features/<x>.md`. PascalCase→kebab plus a fuzzy match (exact, `-system` suffix, prefix, substring either way) — the same algorithm as `.claude/hooks/detect-docs-gaps.sh` |
| 5 | **Config-example drift** | **yes** | a `docs/features/*.md` ```json block labelled with a config path whose values disagree with the shipped `Main/_Module/ModuleData/**/*.json`, or a doc key the shipped config no longer has. Shared keys only, so a partial example is fine; an unparseable (annotated) block is skipped rather than guessed at |
| 6 | **Version mismatch** | **yes** | the `Target: Bannerlord X` line (AGENTS.md, or CLAUDE.md) or a `(vX snapshot)` header in `docs/reference/taleworlds-api-snapshot/{gamemodel-bases,patch-targets}.md` disagreeing with `.claude/pinned-game-version.txt`. No `Target:` line in either file is itself a finding, so moving the line cannot switch the check off |
| 7 | **Context budget** ([ADR-011](../adrs/011-knowledge-delivery-tiers.md)) | **yes** (except `size-warn`) | everything CLAUDE.md loads at launch (its `@`-imports, read from CLAUDE.md itself), the orientation.md trap index, and the rules without `paths:`. These load into every session and every custom agent spawn, so bytes here are a per-turn tax. An oversized path-scoped rule warns until the path-rule diet |
| 8 | **RCA FOLLOW-UP owner** ([#984](https://github.com/haterade22/TAOM/issues/984)) | **yes** | a newly written line of `docs/reviews/rca-*.md` with an uppercase `FOLLOW-UP` (or `FOLLOW-UPS`) and neither an issue reference (`#<number>` or an `/issues/<number>` URL) nor a stated reason (`no issue: <reason>`, `no issue filed, <reason>`, `none is filed because <reason>`). See "Check 8" below |
| 9 | **Git diff failed** ([#987](https://github.com/haterade22/TAOM/issues/987)) | **yes** | the new-lines checks (dashes, check 8) could not diff because the diff failed on a resolvable base, which means repository damage (a broken index or a missing object; a mistyped `--dash-base` exits 2 instead). Reported once under its own heading and `git_diff_failed:` counter; it used to read as zero findings |

**Why 3b exists (2026-08-09).** `docs/features/enlistment.md` had **51 lines** of a live-session
record sitting below its `backlinks-start` marker, and the regeneration was already armed — today's
handoff doc had become the file's 4th inbound reference while the footer still listed 3. It was
found by a doc-drift sweep, moved out, and the guard written. Running the generator during that same
pass then destroyed **50 lines of `REVIEW-LOG.md`** (Review 84's entire record) for exactly the same
reason, which had to be restored from `HEAD` — the bug demonstrating itself mid-fix is the reason
this is a check and not a note.

`--fail-on-drift` gates on **5 to 9**; checks 1-4 never block. Within 7, only hard violations
gate; a `size-warn` finding is report-only by design. `--drift-only` runs just the gating checks
(about 0.2 s against the full run's 9 s) and is what the commit hook calls, with `--staged`.

### Check 8: the RCA FOLLOW-UP owner

Three times a deferred finding sat in an RCA as a bare `FOLLOW-UP` until someone filed an issue for
it (#984), so a new one needs an owner on its own line. A table row is one line, so an issue in
another cell counts.

- **Scope is new lines only**, found the way the dash check finds them: `git diff -U0` against
  `--dash-base` (default `HEAD`) restricted to `docs/reviews/rca-*.md`, plus untracked RCAs read in
  full. `--staged` swaps both for the index (`git diff --cached`, file text from `git show :<path>`),
  which is what a commit records and what the hook runs, so an unstaged edit can neither hide a
  staged bare line nor trip the gate. A file renamed into an RCA from a non-RCA path reads as new
  and is checked in full.
- **What the hook passes (#987).** `--staged` judges the index against `HEAD`. A `git commit --amend`
  records the index against `HEAD~1` (the empty tree for a root commit), so the hook adds
  `--dash-base`. A path commit (`git commit -m x -- <paths>`, or paths after the message without
  `-a` or `-i`) records those paths' working copies, so the hook adds `--only-paths <paths>` (joined
  onto the directory the call runs in and passed absolute) and the check reads them from the working
  tree. A redirection (`2>&1`, `>| file`) is no path, and paths that change nothing leave the index
  as the base. A directory path is relevant only through the files changed in it
  (`git diff --name-only HEAD -- <paths>`). The hook reads the command with `_shellwords.py
  commit-plan`; a command it cannot read (`eval`, `xargs git commit`, over the scan limit) falls back
  to the raw `--amend` text.
- **Not checked (known limits).** `git commit -i`/`--include` and `--pathspec-from-file` are judged as
  index commits. More than about 450 named paths exceed the Windows argument limit and fall back to
  the index. A path amend (`--amend -- <paths>`) is judged on its paths alone, so HEAD's lines in
  other files are not re-checked.
- **A base git cannot resolve** (a mistyped `--dash-base`) exits 2 before any check runs, with a
  note on stderr and no summary block. A `git-diff-failed` finding remains for a diff that fails on
  a resolvable base (repository damage such as a broken index or a missing object): the dash and
  FOLLOW-UP checks report it once under its own heading and `git_diff_failed:` summary counter (not
  as a FOLLOW-UP line), and it gates `--fail-on-drift` and `--drift-only`. A repository with no commit yet reads `HEAD` as the empty tree. A
  git that is missing, or a directory that is no work tree, still fails open and prints a note on
  stderr.
- **Fences.** The dash and FOLLOW-UP scanners walk fences with the shared
  `iter_lines_outside_code_fences`, as the link and version checks do: a fence of any indent opens,
  and closes only on its own character with a run at least as long as the opener's, so a
  three-backtick line inside a four-backtick block stays inside it.
- **Matching** runs on the line with inline code spans and link targets removed. A `#<number>`
  counts unless it follows a label that numbers something else (finding, crash, case, step, item,
  row, line), is a six-digit hex colour, or sits inside `C#7` or an HTML entity. A Markdown link
  whose target is `/issues/<number>` is kept as a citation; any other link target, anchors such as
  `#3-details` included, is dropped. The reason must follow the phrase (`no issue:` with nothing
  after it is bare).
- **To exempt a passing mention**, put the word in backticks (`` `FOLLOW-UP` ``). A citation in
  backticks does not count: write the issue number in plain text.
- **Blind spot:** an edited legacy line becomes a new line. Rewording an old bare `FOLLOW-UP` in an
  existing RCA trips the gate until it cites an issue or a reason. Run on the 2026-10-23 tree, the
  whole-file scan flags 26 legacy lines across the existing RCAs, none of them reachable until the
  line is touched.

### Budget constants (check 7)

The caps are ADR-011's. Live numbers: `python tools/lint_docs.py --context-budget-json`, which is
also what `bash tools/context_budget_scan.sh` reads, so the report and the gate cannot disagree.

| Constant | Value | Covers |
|---|---|---|
| `ENTRY_DOCS_MAX_BYTES` / `ENTRY_DOCS_WARN_BYTES` | 25,600 / 23,000 B | CLAUDE.md plus every file it imports, together |
| `ENTRY_DOC_MAX_LINES` | 200 | each entry doc (the Claude Code docs' adherence ceiling) |
| `ENTRY_DOC_MAX_TABLE_ROW` / `ENTRY_DOC_MAX_PROSE_LINE` | 400 / 600 chars | each entry doc line; fenced code exempt |
| `TRAP_INDEX_MAX_ROWS` / `TRAP_INDEX_MAX_ROW_CHARS` | 47 / 180 | the `## Trap index` table in orientation.md |
| `UNSCOPED_RULES_MAX_BYTES` / `UNSCOPED_RULES_WARN_BYTES` | 16,384 / 14,500 B | the rules without `paths:`, together |
| `SCOPED_RULE_MAX_BYTES` | 12,288 B | each path-scoped rule; warn-only while `SCOPED_RULE_BUDGET_ENFORCE` is false |

The entry docs are CLAUDE.md and its `@`-imports, followed four hops the way Claude Code follows
them, with imports inside code ignored. Bytes are counted with CRLF folded to LF, so a Windows
checkout and CI agree. Two findings exist so a check cannot be switched off in silence: an import
that resolves to no file (`import-missing`), and a trap index that is no longer imported or has
lost its heading (`trap-index-missing`). Both gate. The import scan reads Markdown as Claude Code
does (a fence closes only with a run of its own character at least as long; a code span may
cross lines within a paragraph), and rules are found recursively. A rule whose frontmatter does
not parse loads in every session, so it counts as unscoped and is a gating
`rule-frontmatter-invalid` finding.

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
python tools/lint_docs.py --fail-on-drift  # exit 1 on checks 5 to 9 (gating checks, with the full report)
python tools/lint_docs.py --drift-only     # only the gating checks, and gate on them
python tools/lint_docs.py --drift-only --staged   # the commit hook's mode: check 8 reads the index
python tools/lint_docs.py --drift-only --staged --dash-base HEAD~1   # what the hook runs for an amend
python tools/lint_docs.py --drift-only --staged --only-paths docs/reviews/rca-x.md   # a path commit
python tools/lint_docs.py --context-budget-json   # entry docs, rules and caps, for tools
```

`--summary` emits one line per check plus `total_findings`, which is the form to assert against in a
script.

Interpreting a **noisy** run: the first move is not "edit the docs". #397's 29 findings were all
accurate prose and a wrong checker. Ask which side is wrong before changing either.

Interpreting a **clean** run: a checker reporting zero and a checker exempted into silence produce
byte-identical output. Say the tests are green alongside the zero, and read the blind-spot list above
before treating it as an all-clear.

**A zero is not self-validating.** A checker reporting no findings looks identical to a checker that has been exempted into silence; that is how the stale-version check reached 29/29 false positives before anyone re-read it. `tools/tests/test_lint_docs.py::test_naming_an_old_version_as_the_current_target_is_still_reported` is the fixture separating the two states. If a run is clean, say the tests are green too, and never resolve a noisy check by widening an exemption without a fixture proving the check still fires.

## Key Files

| File | Role |
|---|---|
| `tools/lint_docs.py` | the linter: every check above, stdlib only |
| `tools/tests/test_lint_docs.py` | unit tests over synthetic repo trees, including the exit codes the gates act on |
| `.claude/hooks/check-doc-config-drift.sh` | pre-commit gate; runs `--drift-only --staged` (plus `--dash-base HEAD~1` for an amend and `--only-paths` for a path commit) when a commit stages or names a file the gating checks read, an RCA included, and quotes every `## ` section of the report except `size-warn` lines (`tools/test_hooks.sh` 5d pins that list to the entry docs and to the RCA glob) |
| `.claude/pinned-game-version.txt` | the pin checks 2 and 6 read |
| `.claude/hooks/detect-docs-gaps.sh` | SessionStart sibling of check 4; shares the slug algorithm |

## Dependencies

Python 3.9+ stdlib (`argparse`, `json`, `re`, `pathlib`, `urllib.parse`, `dataclasses`), plus PyYAML
when installed, to parse rule frontmatter for check 7; without it a `frontmatter-unchecked` note
says the parse was skipped.
`Path.is_relative_to` sets the 3.9 floor. No game install, no network, no third-party packages —
which is why this is one of the few TAOM gates that can run in CI unchanged.

## Tests

`python -m unittest discover -s tools/tests -p "test_lint_docs.py"`: all synthetic trees
in a tempdir with `lint_docs`'s module-level path constants repointed (`_TempRepo` /
`_PathConstantRepo`). Repointing `DOCS_DIR` alone is not enough: `ADRS_DIR`, `REVIEWS_RAW_DIR` and the
exempt-prefix tuples are computed at import time, so a test that misses them lints its synthetic tree
against the **real** repo's exemptions.

**`test_naming_an_old_version_as_the_current_target_is_still_reported` is load-bearing.** It is the
only thing distinguishing "the check is quiet because the docs are clean" from "the check is quiet
because it is dead." Every future narrowing ships with a fixture of this shape, and no exemption pass
may delete this one.

## Changelog

- **2026-10-26**: an unresolvable `--dash-base` now exits 2 before any check runs. `git-diff-failed`
  stays for a diff that fails on a resolvable base (repository damage).
- **2026-10-26**: #987 review fixes. `>|` is no path; a directory path commit runs the gate only for
  files changed in it; `--only-paths` diffs only the RCA docs it names; the base probe runs only after
  a failed diff.
- **2026-10-24**: #987. The dash scanner uses the shared fence walker (indented fences, nested fence
  lengths); an unresolvable `--dash-base` is a gating `git-diff-failed` finding; the hook judges an
  amend against `HEAD~1` and a path commit by the named paths' working copies (`--only-paths`).
- **2026-09-23**: #647 (ADR-011). Check 7 became the context budget: the entry docs are read from
  CLAUDE.md's imports, the trap index and the unscoped rules are capped, and a missing import or
  trap-index heading is a finding. Check 6 reads the `Target:` line from AGENTS.md or CLAUDE.md.
  Dead links are checked in CLAUDE.md, AGENTS.md and the rules. `--drift-only` and
  `--context-budget-json` added; CI runs the gate on every branch.
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
- [#647](https://github.com/haterade22/TAOM/issues/647): ADR-011 knowledge delivery tiers; the context
  budget and the CI workflow
- [#984](https://github.com/haterade22/TAOM/issues/984): the RCA FOLLOW-UP owner check (check 8)
- [#987](https://github.com/haterade22/TAOM/issues/987): the commit hook reads what a commit records
  (amend, path commit), and an unresolvable base is a gating finding (check 9)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)

<!-- backlinks-end -->
