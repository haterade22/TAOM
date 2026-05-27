# Karpathy autoresearch — full repo review + TAOM adoption map

> Source materials: `docs/raw/ai-research/karpathy-autoresearch/` — 1 file, last compiled 2026-05-27.
> Upstream: https://github.com/karpathy/autoresearch (MIT, March 2026).

## Summary

Karpathy's autoresearch is a 10-file repo (4 substantive) that gives an AI agent a single-GPU LLM training setup and lets it run autonomously overnight — edit `train.py`, run a 5-minute training experiment, measure `val_bpb`, keep or discard, iterate ~100 times while the human sleeps. The repo's real value isn't the training code; it's the **`program.md` pattern**: a single markdown file that fully specifies an autonomous loop. The human iterates on `program.md`; the agent iterates on `train.py`. They never overlap.

Karpathy's own framing: *"The `program.md` file is essentially a super lightweight 'skill'."* The repo is a demonstration that **agent-driven research is markdown-engineering**, not Python-engineering.

This node captures **everything** in the repo (README, program.md, train.py, prepare.py, pyproject.toml, .gitignore, .python-version, analysis.ipynb) plus what's worth importing into TAOM.

## Sources

- `docs/raw/ai-research/karpathy-autoresearch/SOURCES.md` — file inventory + provenance.
- Upstream README, program.md, train.py, prepare.py, pyproject.toml, .gitignore, .python-version, analysis.ipynb — read in full on 2026-05-27.

## Cross-references

- [features/career-system.md](../features/career-system.md) — TAOM's existing skill+phase pattern that the autonomous-loop skill would extend
- [adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md) — this research node itself is the first dogfood of the knowledge-compile pattern
- [reviews/REVIEW-GUIDE.md](../reviews/REVIEW-GUIDE.md), [reviews/REVIEW-LOG.md](../reviews/REVIEW-LOG.md) — the adversarial-review loop is the closest TAOM analogue to autoresearch's experiment loop
- [ai-includes/agent-teams.md](../ai-includes/agent-teams.md) — when to spawn parallel agents; autoresearch's `worktrees/` + `queue/` pattern is the more disciplined version
- CLAUDE.md "Autonomous-loop stewardship" — already cites `program.md` for the NEVER STOP rule
- CLAUDE.md "Working Discipline" — already cites `program.md` for crash judgment
- `.claude/rules/simplicity-criterion.md` — already imported the "0.001 improvement + 20 lines of hacky code? No." rule
- `.claude/rules/think-before-coding.md` — imported from karpathy-skills repo (sister project)
- `.claude/rules/harness-facts.md` — already cites autoresearch's `.gitignore` for worktree isolation

## Key claims (every pattern in the repo, with provenance)

### From README.md

1. **The whole repo is 3 files that matter** — `prepare.py` (read-only), `train.py` (agent edits), `program.md` (human edits). Plus `analysis.ipynb` for visualization. Everything else is metadata or build infra. Source: README.md "How it works".
2. **Single editable file** — agent only touches one .py. Keeps diffs reviewable, scope manageable, context window small. Source: README.md "Design choices".
3. **Fixed time budget = experiments are comparable** — 5 min/run regardless of platform. Two upsides: any change (architecture, batch size, model size) gives apples-to-apples results; the platform-best config is what you find. Downside: results aren't comparable to other people's compute. Source: README.md "Design choices".
4. **Self-contained, no distributed training, no complex configs** — one GPU, one file, one metric. Source: README.md "Design choices".
5. **README opens with manifesto prose** — a speculative "2200s AI research org" paragraph. The README is a *story*, not just docs. Sets the cultural frame for the project. Source: README.md opener.
6. **Platform-fork guidance is detailed and external** — 7 specific dials for shrinking the experiment to a Macbook (TinyStories dataset, lower vocab_size, lower MAX_SEQ_LEN, lower DEPTH, lower TOTAL_BATCH_SIZE, drop banded attention, etc.). The README lists 4 community forks with platform tags. Source: README.md "Platform support".

### From program.md

7. **Setup → Confirm → Loop** — explicit phase ordering. Setup picks a tag, creates a fresh branch, reads in-scope files, verifies prereqs, initializes results.tsv. Then confirm-with-human. Then go autonomous. Source: program.md "Setup".
8. **Fresh branch per campaign** — `autoresearch/<tag>` (e.g. `autoresearch/mar5`). Must not already exist. Each campaign gets isolated git history. Source: program.md step 2.
9. **One single metric to optimize: `val_bpb`** — validation bits per byte, lower is better, vocab-size-independent so architectural changes compare fairly. Source: program.md "Experimentation".
10. **TSV result log, 5 columns** — `commit | val_bpb | memory_gb | status | description`. Status ∈ {keep, discard, crash}. **Untracked by git** (in .gitignore). Source: program.md "Logging results".
11. **The 9-step experiment loop**: look at git state → tune train.py → commit → run → grep results → record TSV → advance (if improved) or reset (if not) → next. Source: program.md "The experiment loop".
12. **`git reset` is the "discard" verb** — losing experiments are erased from history. Only kept experiments advance the branch. Source: program.md step 9.
13. **Rewind is allowed but should be "very very sparingly"** — the agent CAN go back to an earlier commit if stuck, but shouldn't make it a habit. Source: program.md "The experiment loop".
14. **10-minute hard timeout per experiment** — if a run exceeds 10 min (expected 5), kill it, treat as failure. Source: program.md "Timeout".
15. **Crash judgment**: trivial bug (typo, missing import) → fix and retry. Fundamentally broken idea → log "crash" and move on. Source: program.md "Crashes".
16. **NEVER STOP rule**: don't ask "should I keep going?". The human is asleep. The loop runs until manually interrupted. If you run out of ideas: think harder, re-read papers, combine prior near-misses, try more radical changes. Source: program.md "NEVER STOP". *We already adopted this in CLAUDE.md "Autonomous-loop stewardship".*
17. **Simplicity criterion**: a 0.001 bpb improvement that adds 20 lines of hacky code? No. A 0.001 bpb improvement from deleting code? Always. ~0 improvement but simpler code? Keep. Source: program.md "Simplicity criterion". *We already adopted this as `.claude/rules/simplicity-criterion.md`.*
18. **VRAM is a soft constraint, not hard** — some increase is acceptable for meaningful gains; should not blow up dramatically. Source: program.md "Experimentation".
19. **Read-only file boundary is enforced by social contract**, not code — "You CANNOT modify `prepare.py`. It is read-only." There's no chmod, no hook — just a rule in the markdown. Source: program.md "Experimentation".
20. **First run = baseline** — always start with the unmodified train.py to establish the comparison point. Source: program.md "The first run".
21. **Output capture without flooding context**: `uv run train.py > run.log 2>&1` — explicit "do NOT use tee or let output flood your context". Then `grep "^val_bpb:" run.log` to extract. Source: program.md step 4, step 5.
22. **Failure path is bounded**: `tail -n 50 run.log` to read stack trace; "if you can't get things to work after more than a few attempts, give up". Source: program.md step 6.
23. **Don't commit results.tsv** — explicit. The append-only log lives in the working tree but is git-ignored. Source: program.md step 7.

### From train.py (the file the agent edits)

24. **Module-level constants are the API surface** — `TOTAL_BATCH_SIZE = 2**19`, `EMBEDDING_LR = 0.6`, `DEPTH = 8` etc. live at the top of the file with one-line comments. No argparse, no .yaml, no .json. The git diff IS the experiment description. Source: train.py "Hyperparameters" section.
25. **Bleeding-edge defaults shipped as the baseline** — MuonAdamW optimizer (Muon for 2D matrix params + AdamW for everything else) with hardcoded Polar Express coefficients. ResFormer-style value embeddings with input-dependent gating. ReLU² activation. Token softcap at 15. The agent inherits frontier-research practice without having to research it. Source: train.py MuonAdamW class, GPT.forward.
26. **Fast-fail NaN guard**: `if math.isnan(train_loss_f) or train_loss_f > 100: print("FAIL"); exit(1)`. Aborts in seconds, not after 5 wasted minutes. Source: train.py training loop.
27. **Time-budget enforcement is in-loop with warmup exclusion**: `if step > 10 and total_training_time >= TIME_BUDGET: break`. The first 10 steps don't count against the budget (compilation time). Source: train.py training loop.
28. **Structured ASCII summary at the end**, designed for grep:
    ```
    ---
    val_bpb:          0.997900
    training_seconds: 300.1
    total_seconds:    325.9
    peak_vram_mb:     45060.2
    mfu_percent:      39.80
    total_tokens_M:   499.6
    num_steps:        953
    num_params_M:     50.3
    depth:            8
    ```
    The `---` separator + `key:           value` format is agent-extractable: `grep "^val_bpb:" run.log`. Source: program.md "Output format" + train.py final print block.
29. **Print-in-place training progress** with `\r` — single line gets overwritten step-by-step. The grep at end-of-run picks only the final structured block, not the carriage-returned progress line. Source: train.py training loop.
30. **`@torch.compile(dynamic=False, fullgraph=True)` on fused step functions** — the agent inherits a working compile pipeline and doesn't need to debug shape mismatches or graph breaks. Source: train.py adamw_step_fused, muon_step_fused.
31. **GC management to avoid 500ms stalls** — `gc.disable()` after step 0, periodic `gc.collect()` every 5000 steps. Detail-level optimization the agent doesn't need to think about. Source: train.py training loop.

### From prepare.py (the file the agent CANNOT edit)

32. **Constants block at file top, labeled "fixed, do not modify"** — MAX_SEQ_LEN, TIME_BUDGET, EVAL_TOKENS. These are the campaign-level invariants. Source: prepare.py top.
33. **Cache directory convention**: `~/.cache/autoresearch/` for data + tokenizer. Not in repo, not gitignored ad-hoc — just a known location outside the workspace. Source: prepare.py CACHE_DIR.
34. **Atomic file writes for download integrity**: download to `filepath + ".tmp"`, then `os.rename(temp_path, filepath)`. Prevents partial-write corruption. Source: prepare.py download_single_shard.
35. **Exponential backoff retry**: `for attempt in range(1, max_attempts + 1): time.sleep(2 ** attempt)`. Robust to network flakiness. Source: prepare.py download_single_shard.
36. **Tokenizer roundtrip sanity check**: `assert decoded == test`. Lightweight but always run. Source: prepare.py train_tokenizer end.
37. **Actionable assert messages**: `assert len(parquet_paths) > 0, "No parquet files found. Run prepare.py first."` — the user knows exactly what to do. Source: prepare.py make_dataloader.
38. **Pinned memory + non-blocking transfers** for CPU→GPU. Source: prepare.py make_dataloader.
39. **`evaluate_bpb` is the ground truth** — explicitly labeled "DO NOT CHANGE — this is the fixed metric". The whole comparability of the experiment depends on this function not moving. Source: prepare.py end.

### From analysis.ipynb (THE pattern I missed in the first review)

40. **The analysis notebook is the experimenter's debrief tool** — pandas reads `results.tsv`, matplotlib produces `progress.png`, headers say "Val BPB Over Time", "Summary Statistics", "Top Hits". The notebook turns the raw log into a story. Source: analysis.ipynb structure.
41. **Cumulative-minimum-of-kept = the "frontier" line** — `kept_bpb.cummin()` plotted as a step line. Shows the running best metric. Source: analysis.ipynb plot cell.
42. **Three categories color-coded**: KEEP (green dots with black edge), DISCARD (light grey faint), CRASH (excluded from plot). The kept-running-min step line connects the green dots. Source: analysis.ipynb plot cell.
43. **Each kept point is labeled with its description** — `ax.annotate(desc, ...)` at 30° rotation. So the chart isn't just "metric went down"; it shows what changes drove each drop. Source: analysis.ipynb plot cell.
44. **`progress.png` is committed** — even though `results.tsv` is gitignored, the **summary image** lives in the repo as a teaser and is referenced from the README. The CODE that produces it is in the repo; the RAW DATA is not; the STORY OUTPUT is. Source: analysis.ipynb savefig + .gitignore.
45. **Top Hits by Delta**: sort kept experiments by their improvement-over-previous-kept (not vs baseline). Shows which specific ideas had the biggest impact. Source: analysis.ipynb final cell.
46. **Cumulative effort per improvement**: list each kept experiment with its index. Reveals the search trajectory — how many discards happened between kept hits. Source: analysis.ipynb Summary Statistics cell.

### From pyproject.toml + .python-version

47. **9 dependencies total** — torch (pinned exact `==2.9.1`), numpy, pandas, matplotlib, pyarrow, requests, rustbpe, tiktoken, kernels. The exact pin on torch is reproducibility; the others are `>=`. Source: pyproject.toml.
48. **Custom torch index for CUDA wheels**: `pytorch-cu128` explicit index. Source: pyproject.toml `[tool.uv.sources]`.
49. **Python 3.10+ floor** — recent enough for modern features, old enough for compatibility. Source: .python-version.
50. **`uv run`** is the single-command entry point. No `pip install`, no `python -m venv`, no activation. Source: README "Quick start".

### From .gitignore

51. **The gitignore tells the operating-model story**:
    - `worktrees/` — agent fan-out into git worktrees (we already cite this)
    - `queue/` — experiment-queue directory (implied launcher pattern)
    - `results/` — output directory (results.tsv is the canonical, but bigger artifacts also live here)
    - `dev/` — experimental scratch space
    - `CLAUDE.md` + `AGENTS.md` — agent prompt files **gitignored** because they're per-launcher artifacts, not source code
    - `results.tsv` — the append-only log; lives in working tree but not in repo
    Source: .gitignore.
52. **The substrate (program.md) is in the repo; the session context (CLAUDE.md) is ephemeral** — this is the opposite of TAOM's model, where CLAUDE.md is canonical project doc. Karpathy's choice makes sense for a launcher-fed swarm; TAOM's makes sense for a single-developer mod project.

## Open questions

- **Could TAOM define a per-campaign analogue of `val_bpb`?** Single numeric metric to minimize, vocab-size-independent of the campaign details. Candidates:
  - For doc cleanup: `lint_total = dead_links + stale_versions + orphans + missing_docs`
  - For feature-port loops: `findings_open` (P1+P2 only)
  - For culture-polish loops: composite `quality_score` (validators pass count)
  - For test-coverage push: `untested_public_methods`
  Each campaign would define its own metric — that's the autoresearch pattern, transplanted.
- **Is `tools/analyze_reviews.py` worth building?** Pandas + matplotlib over `docs/reviews/REVIEW-LOG.md`. Would produce a TAOM-equivalent of `progress.png`: bugs-found-over-time, prompt-version vs accuracy, time-to-fix per feature. Low-cost, high-narrative-value.
- **Should TAOM gitignore CLAUDE.md / AGENTS.md?** No — we use them as canonical project docs and they're load-bearing for fresh sessions. But the *concept* — that per-session prompt context is ephemeral — could apply to a `.claude/state/context/` directory of per-session snapshots (which we already gitignore).
- **Does TAOM have an equivalent of `prepare.py`'s "DO NOT MODIFY" label?** Sort of: ADRs are policy-level, `.claude/rules/` are scoped-load constraints. But no single file declares itself off-limits the way `prepare.py` does. Worth considering for files like `Main/IoC.cs` that are single-owner and frequently target of accidental cross-feature edits.

## Adoption priorities for TAOM

**Tier 1 (high leverage, low cost, build now):**

- **`/lint-cleanup-loop` skill** — first proof-of-concept autonomous campaign. Metric: `dead_links + stale_versions + orphans + missing_docs` from `tools/lint_docs.py`. Branch: `taom-lint/<date>`. TSV: `commit | metric | status | description`. 9-step loop modeled on program.md. Already 184 stale-version refs waiting to be picked off.
- **`tools/analyze_reviews.py`** — pandas + matplotlib over `docs/reviews/REVIEW-LOG.md` (parse the existing markdown tables). Output: `docs/reviews/progress.png` showing bug counts over time + Codex accuracy trend. Mirrors `analysis.ipynb` directly.
- **Add `---`-delimited structured summary to `/verify`** — final output should end with:
    ```
    ---
    build: pass
    tests: 2254/2254
    lint_dead_links: 0
    lint_stale: 184
    git_clean: yes
    ---
    ```
    Grep-friendly for any future autonomous loop that wraps `/verify`.

**Tier 2 (adopt patterns selectively):**

- **Fast-fail guards in long loops** — codify "3 consecutive failures → exit" for any autonomous-campaign skill. Don't burn an hour on a doomed approach.
- **Atomic-write + retry-with-backoff for Python tools** — `tools/lint_docs.py --report` already writes a report; should write to `.tmp` then rename, like Karpathy does.
- **Time-budget enforcement with warmup exclusion** — each iteration of an autonomous loop has a wall-clock cap (e.g. 30 min), with the first iteration's setup time excluded from the budget.

**Tier 3 (don't adopt — different operating model):**

- **Don't gitignore CLAUDE.md / AGENTS.md.** We treat them as canonical; Karpathy's launcher generates them per-session. Different scale.
- **Don't reduce TAOM to "no tests".** Karpathy's eval function IS the test; TAOM has cross-feature interactions that need unit tests. But: legitimize "no unit tests, just a metric" for autonomous-campaign features where the integration result is the ground truth (MissionDiagnostic-style).
- **Don't move TAOM data to `~/.cache/taom/`.** We have a Modules/ deployment model that doesn't map. But: `~/.taom-src/` already follows the pattern for decompilation cache; can extend to `~/.cache/taom/lint-history/` for trend data.

**The big idea, restated:**

Karpathy's autoresearch is markdown-engineering masquerading as ML research. The valuable artifact is `program.md` — a single file that fully specifies an autonomous loop. TAOM has 23 skills, 5 agents, 15 rules, and an active Codex review loop. All of those are **reactive** (user invokes). None are **proactive** (point at me, walk away, come back to results). Importing the program.md pattern fills exactly that gap, and we already have most of the substrate (branch-per-campaign discipline, TSV-shaped REVIEW-LOG, simplicity-criterion rule, NEVER STOP rule) — what's missing is one canonical skill that wraps it all up and is pointable at a topic.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
