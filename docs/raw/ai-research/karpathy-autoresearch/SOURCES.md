Source: https://github.com/karpathy/autoresearch (read 2026-05-27)

Andrej Karpathy's autoresearch repo. Public, MIT, March 2026.

Full repo file inventory (10 files):

| File | Size | Read? | Why |
|---|---|---|---|
| `README.md` | 8KB | Full | Manifesto + project structure + design choices + platform-fork guidance |
| `program.md` | 7KB | Full | The autonomous-loop spec the agent reads at session start |
| `train.py` | 26KB | Full | What the agent edits. GPT model + MuonAdamW + training loop + module-level hyperparameter constants |
| `prepare.py` | 15KB | Full | What the agent CANNOT edit. Constants + data prep + tokenizer + dataloader + `evaluate_bpb` (the metric) |
| `analysis.ipynb` | 8KB | Full | Pandas + matplotlib notebook that plots experiment trajectory from `results.tsv` |
| `pyproject.toml` | 543B | Full | Dependencies. 9 deps total. torch pinned exact, others `>=`. Custom PyTorch CUDA index. |
| `.gitignore` | 279B | Full | Tells the operating-model story: worktrees/, queue/, results/, dev/, CLAUDE.md, AGENTS.md, results.tsv all gitignored. |
| `.python-version` | 5B | Full | `3.10` |
| `progress.png` | 253KB | Not read | Output of analysis.ipynb. Committed as a teaser image in README. |
| `uv.lock` | 443KB | Skipped | Machine-generated dependency lockfile. Pure noise. |

The original repo is durable (public GitHub, MIT-licensed), so we don't mirror the source code here. Excerpts and structural notes are in the compiled research node at `docs/research/karpathy-autoresearch.md`.

Provenance: the patterns observed here come from reading the actual files, not from third-party summaries.

Karpathy tweet context (referenced by README):
- https://x.com/karpathy/status/2029701092347630069
- https://x.com/karpathy/status/2031135152349524125

Notable forks (per README):
- miolini/autoresearch-macos (MacOS)
- trevin-creator/autoresearch-mlx (MacOS, MLX)
- jsegov/autoresearch-win-rtx (Windows + RTX)
- andyluo7/autoresearch (AMD)
