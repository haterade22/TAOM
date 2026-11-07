# graphify Code Graph

## Overview

TAOM keeps a graphify code graph of its own C#, Python, PowerShell and shell code, outside the repo
at `E:\graphify\TAOM`, and reads it at fixed points in the workflow to answer one question before
a change is made or reviewed: **what depends on this type?** `tools/graphify_taom.py` is the only
way to build or query it, and a PreToolUse hook denies graphify's raw write verbs. Decision record:
ADR-012 (1.5.x line). Issue
[#677](https://github.com/haterade22/TAOM/issues/677).

**On the 1.4.x line.** The wrapper and its tests are here, including the `gate` judge the hook calls.
The hook script `.claude/hooks/check-graphify-usage.sh`, its `tools/test_hooks.sh` rows and its
registration in `.claude/settings.json` are not: they wait for the hook registration work (plan 141,
steps D1 and D2). The workflow wiring this page describes (the `/deep-review`, `/investigate`,
`/new-feature` and `/research` steps, the agent lines, the SessionStart freshness line and ADR-012)
is not on this line yet. graphify is not installed on the maintainer's laptop. There, until it is
and `TAOM_GRAPHIFY_OUT` names a local folder, graph builds exit 3, and status and queries exit 2
while no graph exists; the default output root is the
desktop's `E:\graphify\TAOM`.

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
- **No docs graph.** Markdown topology is [doc-graph](doc-graph.md) (`tools/graph_query.py`).
- **No semantic pass, no MCP server, no `install`, no CI job.** The code-only graph is free,
  deterministic and reads real files, so its `source_file` citations are parsed, not invented.

## Configuration

| Setting | Default | Meaning |
|---|---|---|
| `TAOM_GRAPHIFY_OUT` | `E:\graphify\TAOM` (worktree: `E:\graphify\TAOM-wt-<folder>`) | Output root; the graph is `<root>\graphify-out\graph.json` |
| `TAOM_GRAPHIFY_AUTOREFRESH` | on | Set to `0` to stop `refresh --detach` from starting a background refresh (the SessionStart caller prints the reason and moves on) |
| `graphify` on PATH | `C:\Users\mikew\.local\bin\graphify.exe` | Installed with `uv tool install --python 3.12 'graphifyy[leiden]'`. The 3.12 pin is load-bearing: the `leiden` extra needs Python below 3.13 |

Exit codes: `0` fresh or done, `1` stale, `2` missing, `3` environment (graphify, git or the drive
is missing: report it, never install), `4` graphify wrote into the repo, `5` another refresh holds
the lock.

## Key Files

| File | Purpose |
|------|---------|
| [tools/graphify_taom.py](../../tools/graphify_taom.py) | The wrapper: `refresh` (and `refresh --detach`), `status`, `report`, the query verbs, `tests`, and `gate` (the hook's judge) |
| [tools/tests/test_graphify_taom.py](../../tools/tests/test_graphify_taom.py) | Out root, commands, stamp and staleness on a temp git repo, lock, contamination, the gate's command table, and a small fake `graph.json` for supertypes, candidates, answer-scoped staleness, `tests` and `--detach` (graphify itself is stubbed) |
| `.claude/hooks/check-graphify-usage.sh` | PreToolUse gate on Bash and PowerShell (1.5.x line; not on this line yet) |
| `.claude/hooks/session-start.sh` | Prints the graph's freshness at startup (1.5.x line) |
| `tools/test_hooks.sh` | Section 7f, plus the 4c, 4d and 5 rows for the gate (1.5.x line) |

## Where the workflow reads it

On the 1.5.x line:

| Step | What runs | What it feeds |
|---|---|---|
| `/deep-review` Step 1 | `affected` on every changed public type | callers outside the diff, for the completeness and data-flow lenses; a `Blast radius:` line in the report |
| `/investigate` Phase 1 | `explain` + `affected` on the failing type | the Phase 3 pattern-match candidate list |
| `/new-feature` | `explain` + `affected` on every type the feature extends | extend-versus-modify, before the first file |
| `feature-builder` | `affected` before changing or moving a type | the refactor's move set; more than five dependents means a design change |
| Codex (`.agents/skills/taom-build`, `taom-review`, `taom-research`) and AGENTS.md | the same wrapper | the same caller trace |

## Tests

- `python -m unittest discover -s tools/tests -p "test_graphify_taom.py"`: 82 tests, hermetic.
- `bash tools/test_hooks.sh`: section 7f runs the gate through the real hook on both shells.

## How to use it

```bash
python tools/graphify_taom.py status                          # fresh / STALE / MISSING
python tools/graphify_taom.py refresh --if-stale              # about 2.5 min when stale; run it in the background
python tools/graphify_taom.py refresh --if-stale --detach     # start that refresh as a detached child and return at once
python tools/graphify_taom.py affected "ICoopSessionProvider" --depth 2
python tools/graphify_taom.py affected "Foo" --no-interfaces  # graphify's own answer only
python tools/graphify_taom.py tests "TaomPartySizeModel" "Main/Features/X/Y.cs"   # prints a dotnet test filter
python tools/graphify_taom.py explain "TaomPartySizeModel"
python tools/graphify_taom.py god-nodes --top 15
python tools/graphify_taom.py path "A" "B"
python tools/graphify_taom.py report                          # path of GRAPH_REPORT.md
```

**Callers through an interface or base class (#995).** graphify's `affected` on a class lists the
class's own dependents, and a service is usually reached through its interface, so the production
callers were missing (`affected "FiefSiegeParticipationService"` listed only its test file). After
graphify answers, the wrapper reads `graph.json` once, finds the target's outgoing `implements`,
`inherits` and `extends` links whose target is defined in a TAOM file, runs `affected` on each
supertype's `.cs` path (up to six, side by side) and prints the extra lines under
`Via <IFoo> (<path>):`, minus lines already printed. External engine types have no source file in
the graph and are skipped. `--no-interfaces` turns this off. If `graph.json` cannot be read, one
`NOTE:` line says so and graphify's own answer stands. The cost is a second graphify call (each
reads the whole graph, about 3 s), so a class with a supertype takes about 6 s instead of 3 s.

**Member callers.** graphify's `affected` stops at the type node and never walks to its members, so
a caller that only calls an interface's methods and never names the interface is invisible to it
(`FiefGrantFactsBuilder.Build()` and `FiefGrantingCampaignBehavior.SyncData()` call
`IFiefSiegeParticipationService` members through a field or parameter). The wrapper fills that gap
from the `graph.json` it already loaded, with no extra graphify call: for the target and each
supertype it takes the type's member nodes (outgoing `method` links), then every `calls` or
`references` link into one of them, and prints
`Member callers of <Type>:` followed by `- <caller> [calls <member>] <file>:<line>`, minus lines
already printed and minus the type's own members calling each other. `--no-interfaces` skips the
supertypes but keeps the target's own member callers. Calls through a lambda or a string-keyed
registration are not links in the graph, so they stay a Grep question.

**Which tests to run (#995).** `tests <type-or-file>...` takes the dependents of each input
through the same path (depth 2, `--depth N`), keeps the ones in `TAOM.Tests/`, maps them to test
class names and prints one line, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
--filter "FullyQualifiedName~A|FullyQualifiedName~B"`. It exists because the full suite took 123 s
wall time on 2026-10-24 (25,930 tests, 1 m 42 s of test time), too long to run after every edit; the
filter is the part of it the change can reach. It also follows each production file named in a
member-callers block (up to eight) with one more `affected`, so tests that reach a type through
the code that calls its members count. It is a lower bound: the graph misses DI by string and
lambda calls, so the full suite stays the pre-review run. When no test is reached it
prints a `NOTE:` and exits 0.

**Background refresh (#995).** `refresh --detach` starts the same refresh as a detached child
(output in `<out>\graphify-out\autorefresh.log`, outside the repo) and returns in about 1 s with
`graphify_taom: background refresh started (pid N, log <path>).` It prints its reason and starts
nothing when the graph is fresh, another refresh holds the lock, `TAOM_GRAPHIFY_AUTOREFRESH=0`, a
Bannerlord process is running, or that check (`tasklist`, 1.5 s limit) timed out or failed. Every
such case exits 0. A bad output root, or no graphify on PATH, exits 3. `.claude/hooks/session-start.sh` calls it when `status` reports STALE and prints
its one line, so a stale graph rebuilds while the session starts. The log is moved to
`autorefresh.log.1` once it passes 1 MB.

**When a name will not resolve.** graphify mints a reference node per file for a type another file
uses, so `IModLogger` can come back "Ambiguous" with a list of candidates: rerun with the
repo-relative `.cs` path it prints, or the node id. When graphify prints `No unique node match`,
the wrapper searches the graph itself and prints up to ten candidates as `id  label
source_file:source_location` (labels equal to the name or `name.cs` first, defining nodes before
reference stubs, then labels that contain it). If exactly one file defines the name, it retries
once with that path and says so in one line (`affected "IFiefSiegeParticipationService"` now
resolves this way). A type name that returns no dependents can still
have some through a file-level import (a GameModel is reached only from `SubModule.cs`): retry with
its file path, `affected "Main/Features/CulturalFeats/Models/TaomPartySizeModel.cs"`, before
concluding nothing uses it. **Engine types are not single nodes** (54 separate `MobileParty` nodes,
one per referencing file), so "which TAOM code uses engine type X" is a Grep or Serena question.

**Evidence standard.** Code-only nodes are parsed from real files, so their paths and lines are
accurate at build time. Staleness is the risk. A stale query now prints, on stderr,
`STALE in this answer: <files>: Read them directly before acting.` for the changed files that
appear in the answer, then one line with the total count and the refresh command. A stale answer
exits as before (graphify's own code). Treat any STALE line as "refresh or confirm by hand".

**On another machine.** Set `TAOM_GRAPHIFY_OUT` to a local folder. Without graphify on PATH, `refresh`
exits 3; its `--if-stale` and `--detach` forms exit 0 first when the graph is already fresh. `status`
needs no graphify: it exits 0, 1 or 2, and 3 only when staleness cannot be checked. `report` exits 2
while no graph exists. The queries and `tests` look for the graph first, so they exit 2 with no graph
and 3 with a graph but no graphify. The workflow steps, once they land, record UNCHECKED on any of
these rather than silently skipping.

## Performance

Measured on the desktop, 2026-09-26: cold extract 150 s plus 45 s clustering (3,679 files); warm
refresh through the wrapper 109 s in total; 42,984 nodes, 97,891 edges, 1,690 communities; the graph
was 75 MB then and measured 150 MB on 2026-10-24 (80,420 nodes, 185,479 links, loaded in
about 0.9 s by the wrapper while graphify's own call takes about 3 s). `status` measured 0.21 to 0.33 s on a quiet machine and up to 1.1 s under load (four git
calls), inside SessionStart's 4 s bound; the gate adds one Python start only when a command's raw
text contains `graphify` or a JSON escape.

## Changelog

- 2026-10-24: `affected` adds callers through implemented interfaces and base classes (`Via ...`
  blocks, `--no-interfaces`), lists candidates and retries once on a name that does not resolve,
  adds `Member callers of <Type>:` blocks computed from the loaded graph, and scopes the STALE
  warning to the files in the answer. New `tests` verb prints the filtered
  `dotnet test` line, and `refresh --detach` starts a background refresh (#995).
- 2026-09-26: Wired into the workflow (ADR-012): wrapper, gate, SessionStart freshness line,
  per-worktree output roots, the absolute `GRAPHIFY_OUT` fix for the incremental stat-index leak,
  and mandatory steps in `/deep-review`, `/investigate`, `/new-feature`, `/research`, the builder
  agents and the Codex skills.

## GitHub Issue

- **Issue:** [#677](https://github.com/haterade22/TAOM/issues/677): Wire graphify into the workflow
- **Status:** Closed (resolved in b8833474)

---
