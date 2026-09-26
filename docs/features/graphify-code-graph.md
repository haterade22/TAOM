# graphify Code Graph

## Overview

TAOM keeps a graphify code graph of its own C#, Python, PowerShell and shell code, outside the repo
at `E:\graphify\TAOM`, and reads it at fixed points in the workflow to answer one question before
a change is made or reviewed: **what depends on this type?** `tools/graphify_taom.py` is the only
way to build or query it, and a PreToolUse hook denies graphify's raw write verbs. Decision record:
[ADR-012](../adrs/012-graphify-code-graph-in-the-workflow.md). Issue
[#677](https://github.com/haterade22/TAOM/issues/677).

## Why This Exists

- **Before:** graphify was installed but wired into nothing
  ([adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md)). Five weeks later
  the only graph ever built had been lost with its session scratchpad, and nothing had used it.
- **The need:** Serena answers "who calls this method, right now"; nothing answered "what is the
  whole dependent set of this type, at depth 2, with file and line" or "which types are the hubs".
  A reviewer who never sees the callers outside the diff cannot find the bug that lives there.
- **Without the wrapper and gate:** each raw graphify verb has a silent trap (below), and the
  correct command line had to be re-derived from a review every time.

## Architecture

### Design challenge

graphify is a fast-moving upstream (200-plus releases) whose defaults are wrong for TAOM:

| Raw verb | Trap |
|---|---|
| `extract` without `--out` | writes `graphify-out/` into the scanned repo |
| `extract` WITH `--out`, incremental re-run | still writes `graphify-out/cache/stat-index.json` into the scanned repo. Reproduced 2026-09-26: graphify's `cache.py` fixes the stat-index location from its first caller, and one caller passes no cache root. The same file was committed in 8318e346 |
| `extract --mode deep` / `--backend` | the full semantic pass cost 18.2M input tokens for the same hub ranking as `--code-only` |
| `update` | keeps only file-backed nodes: about 5,100 external-type nodes and 12,000 edges vanish, without `--force` |
| `cluster-only`, `label` | name communities with an LLM unless `--no-label` |
| `claude install`, `codex install`, `hook install` | write their own sections into CLAUDE.md, AGENTS.md, `.claude/settings.json` or git hooks |

### Solution approach

```
python tools/graphify_taom.py refresh [--if-stale]
        |  lock (one refresh across sessions), out root outside the repo
        |  env GRAPHIFY_OUT=<absolute out>/graphify-out   <- pins every cache write
        v
graphify extract <repo> --code-only --out <out>  ->  graphify cluster-only <out> --no-label --no-viz
        |
        v
repo contamination check (git ls-files --others --ignored)  ->  taom-stamp.json (HEAD, build start, deletions)

python tools/graphify_taom.py affected|explain|path|god-nodes|query ...
        |  staleness check against the stamp, --graph pinned
        v
graphify <verb> ... --graph <out>/graphify-out/graph.json

.claude/hooks/check-graphify-usage.sh  (PreToolUse, Bash + PowerShell)
        -> python tools/graphify_taom.py gate   (deny raw write verbs, allow queries)
```

- **Output root.** `E:\graphify\TAOM` for the main tree. A linked git worktree gets
  `E:\graphify\TAOM-wt-<folder>`, so a builder in `isolation: "worktree"` never overwrites the shared
  graph with its own code. `TAOM_GRAPHIFY_OUT` overrides both. A root inside the repo is refused.
- **Why an absolute `GRAPHIFY_OUT`.** graphify computes its output as `out_root / GRAPHIFY_OUT`, and
  an absolute value replaces the base, so `graph.json` lands exactly where `--out` alone puts it
  while the stat-index cache can no longer fall back to the scanned tree.
- **Stale** means a code file (`.cs .py .ps1 .psm1 .sh .xaml .cpp .h .js .ts .csproj .sln`) changed
  after the build started, is new and untracked, or was deleted after the build. JSON is excluded:
  settings files churn and carry 31 of 42,770 nodes.
- **The gate** judges each command segment the way its shell would split it (quote-aware, with the
  shell's own escape), strips prefixes (`VAR=x`, `&`, `timeout N`, `uvx`, `uv tool run`), unwraps
  `bash -c` and `pwsh -Command`, and denies when the program is `graphify` with a verb outside the
  query set, or `graphify-mcp`. A commit message or grep pattern that mentions graphify is allowed.

### Non-goals

- **No XML or XSLT.** graphify collects neither (0 of 1,048 ModuleData files). Troop, item,
  culture and party-template questions go to the taom-moduledata MCP or
  `python tools/validate_moduledata.py`.
- **No docs graph.** Markdown topology is [doc-graph](doc-graph.md) (`/doc-graph`).
- **No semantic pass, no MCP server, no `install`, no CI job.** The code-only graph is free,
  deterministic and reads real files, so its `source_file` citations are parsed, not invented.

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| `TAOM_GRAPHIFY_OUT` | `E:\graphify\TAOM` (worktree: `E:\graphify\TAOM-wt-<folder>`) | Output root; the graph is `<root>\graphify-out\graph.json` |
| `graphify` on PATH | `C:\Users\mikew\.local\bin\graphify.exe` | Installed with `uv tool install --python 3.12 'graphifyy[leiden]'`. The 3.12 pin is load-bearing: the `leiden` extra needs Python below 3.13 |

Exit codes: `0` fresh or done, `1` stale, `2` missing, `3` environment (graphify, git or the drive
is missing: report it, never install), `4` graphify wrote into the repo, `5` another refresh holds
the lock.

## Key Files

| File | Purpose |
|------|---------|
| [tools/graphify_taom.py](../../tools/graphify_taom.py) | The wrapper: `refresh`, `status`, `report`, the query verbs, and `gate` (the hook's judge) |
| [tools/tests/test_graphify_taom.py](../../tools/tests/test_graphify_taom.py) | Out root, commands, stamp and staleness on a temp git repo, lock, contamination, the gate's command table |
| [.claude/hooks/check-graphify-usage.sh](../../.claude/hooks/check-graphify-usage.sh) | PreToolUse gate on Bash and PowerShell |
| [.claude/hooks/session-start.sh](../../.claude/hooks/session-start.sh) | Prints the graph's freshness at startup |
| [tools/test_hooks.sh](../../tools/test_hooks.sh) | Section 7f, plus the 4c, 4d and 5 rows for the gate |

## Where the workflow reads it

| Step | What runs | What it feeds |
|---|---|---|
| `/deep-review` Step 1 | `affected` on every changed public type | callers outside the diff, for the completeness and data-flow lenses; a `Blast radius:` line in the report |
| `/investigate` Phase 1 | `explain` + `affected` on the failing type | the Phase 3 pattern-match candidate list |
| `/new-feature` | `explain` + `affected` on every type the feature extends | extend-versus-modify, before the first file |
| `/research` step 6 | `affected` on the TAOM types the recommendation changes | the recommendation's caller list |
| `feature-builder`, `refactoring-specialist` | `affected` before changing or moving a type | the refactor's move set; more than five dependents means a design change |
| Codex (`.agents/skills/taom-build`, `taom-review`, `taom-research`) and AGENTS.md | the same wrapper | the same caller trace |

## Tests

- `python -m unittest discover -s tools/tests -p "test_graphify_taom.py"`: 38 tests, hermetic.
- `bash tools/test_hooks.sh`: section 7f runs the gate through the real hook on both shells.

## How to use it

```bash
python tools/graphify_taom.py status                          # fresh / STALE / MISSING
python tools/graphify_taom.py refresh --if-stale              # about 2.5 min when stale; run it in the background
python tools/graphify_taom.py affected "ICoopSessionProvider" --depth 2
python tools/graphify_taom.py explain "TaomPartySizeModel"
python tools/graphify_taom.py god-nodes --top 15
python tools/graphify_taom.py path "A" "B"
python tools/graphify_taom.py report                          # path of GRAPH_REPORT.md
```

**When a name will not resolve.** graphify mints a reference node per file for a type another file
uses, so `IModLogger` can come back "Ambiguous" with a list of candidates: rerun with the
repo-relative `.cs` path it prints, or the node id. A type name that returns no dependents can still
have some through a file-level import (a GameModel is reached only from `SubModule.cs`): retry with
its file path, `affected "Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs"`, before
concluding nothing uses it. **Engine types are not single nodes** (54 separate `MobileParty` nodes,
one per referencing file), so "which TAOM code uses engine type X" is a Grep or Serena question.

**Evidence standard.** Code-only nodes are parsed from real files, so their paths and lines are
accurate at build time. Staleness is the risk: read the files it names before acting, and treat a
STALE warning as "refresh or confirm by hand".

**On another machine.** Set `TAOM_GRAPHIFY_OUT` to a local folder. Without graphify on PATH every
verb exits 3 and the workflow steps record UNCHECKED rather than silently skipping.

## Performance

Measured on the desktop, 2026-09-26: cold extract 150 s plus 45 s clustering (3,679 files); warm
refresh through the wrapper 109 s in total; 42,984 nodes, 97,891 edges, 1,690 communities; the graph
is 75 MB. `status` measured 0.21 to 0.33 s on a quiet machine and up to 1.1 s under load (four git
calls), inside SessionStart's 4 s bound; the gate adds one Python start only when a command's raw
text contains `graphify` or a JSON escape.

## Changelog

- 2026-09-26: Wired into the workflow (ADR-012): wrapper, gate, SessionStart freshness line,
  per-worktree output roots, the absolute `GRAPHIFY_OUT` fix for the incremental stat-index leak,
  and mandatory steps in `/deep-review`, `/investigate`, `/new-feature`, `/research`, the builder
  agents and the Codex skills.

## GitHub Issue

- **Issue:** [#677](https://github.com/haterade22/TAOM/issues/677): Wire graphify into the workflow
- **Status:** Closed (resolved in b8833474)
