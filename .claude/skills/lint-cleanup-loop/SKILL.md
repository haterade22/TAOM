---
name: lint-cleanup-loop
description: Autonomous doc-rot loop — fix one lint finding at a time, re-lint, commit on improvement; runs until clean or N stuck iterations.
argument-hint: "[--category stale_versions|dead_links|orphan_features|missing_features|all] [--max-iters N]"
disable-model-invocation: true
disallowed-tools: AskUserQuestion
---

# Lint Cleanup Loop

**This skill is autonomous.** Once kicked off, it runs the experiment loop indefinitely. Do not pause to ask the user "should I keep going?". The user may be asleep or away. Stop only when the metric hits 0, when N consecutive iterations make no progress, or when the user manually interrupts.

Modeled on Karpathy's [autoresearch program.md pattern](../../../docs/research/karpathy-autoresearch.md). The full mapping:

| autoresearch | this skill |
|---|---|
| `train.py` (agent edits) | TAOM docs touched per iteration |
| `prepare.py` (read-only) | `tools/lint_docs.py` — the ground-truth metric |
| `program.md` (human edits) | This file |
| `val_bpb` (metric, lower is better) | `total_findings` from `lint_docs.py --summary` |
| `results.tsv` (untracked) | `.claude/state/lint-cleanup-loop/results.tsv` (gitignored) |
| `autoresearch/<tag>` branch | `taom-lint/<tag>` branch |
| 5-min time budget | per-iteration soft cap: 10 min wall clock |
| "keep" / "discard" / "crash" | same |
| "NEVER STOP" | same |

## Setup

1. **Agree on a run tag**: propose one based on today's date (e.g. `may27a`). The branch `taom-lint/<tag>` must not already exist.
2. **Create the branch**: `git checkout -b taom-lint/<tag>` from current HEAD.
3. **Pick the category** from `$ARGUMENTS` (default `all`). One of:
   - `stale_versions` — the largest category (184 today), mostly `1.3.15`-era references that should be `1.4.5`
   - `dead_links` — markdown link targets that don't resolve
   - `orphan_features` — feature docs that no other doc references
   - `missing_features` — `Main/Features/<X>/` without `docs/features/<x>.md`
   - `all` — run the linter on the full set; pick whichever has the highest count
4. **Capture baseline**: run `python tools/lint_docs.py --summary` and record the metric. Karpathy's "first run = baseline" rule (autoresearch program.md "The first run").
5. **Initialize results.tsv**: at `.claude/state/lint-cleanup-loop/results.tsv` with header `commit\tcategory\tcount_before\tcount_after\tstatus\tdescription`. Untracked by git — same convention as `autoresearch/results.tsv` (in `.claude/state/` which is gitignored).
6. **Announce baseline and start — do NOT prompt for confirmation**: print "Baseline is N findings in category X. Starting the loop; I will NOT stop for permission once running." Then kick off immediately. Invoking `/lint-cleanup-loop` **is** the authorization — this skill sets `disable-model-invocation: true` (user-only) and `disallowed-tools: AskUserQuestion` (the "NEVER STOP to ask" discipline is enforced by the harness, not just prose), so there is no confirmation step to wait on.

Kick off the loop. Do not seek confirmation until the metric reaches 0 or N consecutive iterations make no progress.

## The experiment loop

LOOP FOREVER (until stop condition):

1. **Look at git state** — current branch + most recent commit.
2. **Run lint + pick one finding** to fix:
   - `python tools/lint_docs.py > .claude/state/lint-cleanup-loop/last-lint.md 2>&1`
   - Read the report; pick a finding in the chosen category. Prefer findings clustered in one file (one Edit fixes many).
3. **Fix the finding** — Edit the doc(s). Examples:
   - **Stale `1.3.15` ref**: if the context describes current v1.4.5 behavior, update. If it describes historical v1.3.15 research, leave it (and re-evaluate whether the file's name should match `rca-` or `codex-adversarial-` prefix to get auto-exempted).
   - **Dead link**: find the renamed/moved target, or convert to inline-code prose if the target lives outside the repo (e.g., memory files).
   - **Orphan feature doc**: add a link from `docs/INDEX.md` to that doc, or delete the doc if it's vestigial.
   - **Missing feature doc**: scaffold from `docs/features/TEMPLATE.md`. Read the corresponding `Main/Features/<X>/` source first.
4. **`git commit` the fix** with a 50-char-or-fewer title:
   ```
   docs(lint): fix <N> <category> in <file-or-area>
   ```
5. **Re-run lint** to measure delta:
   ```
   python tools/lint_docs.py --summary > .claude/state/lint-cleanup-loop/post.txt 2>&1
   ```
6. **Extract the metric**: `grep "^total_findings:" .claude/state/lint-cleanup-loop/post.txt` (or the category-specific line).
7. **Record in results.tsv** (tab-separated): `commit\tcategory\tbefore\tafter\tstatus\tdescription`.
8. **Advance or reset**:
   - **improved (count decreased)** → keep the commit. Loop continues.
   - **equal or worse** → `git reset --hard HEAD~1`. Status = `discard`. Loop continues.
   - **lint crashed** (no output) → `git reset --hard HEAD~1`. Status = `crash`. Loop continues.

## Stop conditions

Stop ONLY when one of:

- **Metric hits 0** in the chosen category. Print: "Done. <category> reduced to 0."
- **5 consecutive iterations** with no improvement (5 discards in a row). Print: "Stuck — 5 consecutive non-improvements. Stopping."
- **User manually interrupts** (Ctrl-C, etc.).
- **Iteration budget exceeded** (`--max-iters N`, default unlimited).

Do NOT stop to ask permission. Do NOT stop because the changes feel "too repetitive". Do NOT stop because you want a code review — the commits ARE the review surface.

## Crash judgment (per autoresearch program.md)

- **Trivial bug** (typo in Edit, file not found because user moved it mid-loop) → fix and retry the same finding once. If retry fails, treat as discard and move on.
- **Fundamentally broken approach** (the fix you tried makes the metric worse OR introduces other errors) → discard, log `crash`, move on to a different finding.
- **3 consecutive crashes** on different findings → stop and report. Something is wrong with the linter or the docs format.

## Output capture discipline (per autoresearch program.md)

- `> file 2>&1` — never `tee`, never let lint output flood the conversation context.
- After each lint run, only `grep` the specific lines you need from the captured file.
- `tail -n 50` if you need to debug a crash.

## Simplicity criterion (per autoresearch program.md)

When choosing what to fix:

- Deleting a stale doc that's not referenced anywhere AND no longer accurate → always keep.
- Adding a TEMPLATE-scaffolded feature doc with minimal content just to satisfy the linter → reject; the doc must be genuinely useful or the gap stays.
- A 1-line fix that resolves 30 stale-version refs (e.g., updating a single migration table header) → big win.
- A 50-line refactor that resolves 2 dead links → bad ratio; skip and find a different finding.

## Branch + merge discipline

The loop runs on `taom-lint/<tag>` — isolated branch, no risk to `bannerlord-1.4.5`. When the loop stops (metric=0 or stuck):

1. Print a final summary block:
   ```
   ---
   tag:           <tag>
   category:      <category>
   start_count:   <baseline>
   end_count:     <final>
   iterations:    <N>
   commits:       <kept>
   discards:      <reset>
   crashes:       <crash>
   ---
   ```
2. **Tell the user the branch is ready for review.** Do not auto-merge to `bannerlord-1.4.5`. The user decides.

## What NOT to do

- Don't run `/lint-docs` and stop — that's diagnostic, not action. This skill DOES the fixes.
- Don't fix C# build errors, test failures, or feature bugs — this is a docs-only loop. If a fix would require touching Main/, skip the finding.
- Don't update CHANGELOG mid-loop — wait until the branch is ready and let the user write one batch entry.
- Don't pause to ask "is this fix correct?" — the metric is the judge. If it improved, keep. If not, reset.
- Don't go off-script — if you want to ALSO regenerate backlinks, ALSO run `/verify`, ALSO update docs/INDEX.md, that's scope creep. One loop, one metric, one category.

## State files (gitignored)

```
.claude/state/lint-cleanup-loop/
├── results.tsv          ← append-only TSV per the autoresearch convention
├── last-lint.md         ← most recent full report (overwritten each iter)
└── post.txt             ← most recent --summary output (overwritten each iter)
```

These live under `.claude/state/` which is gitignored.

## See also

- [docs/research/karpathy-autoresearch.md](../../../docs/research/karpathy-autoresearch.md) — the source pattern
- [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md) — knowledge-base architecture (this skill is the first autonomous-loop dogfood)
- `tools/lint_docs.py` — the ground-truth metric
- `tools/build_backlinks.py` — runs implicitly via the post-commit hook (or call directly if needed)
