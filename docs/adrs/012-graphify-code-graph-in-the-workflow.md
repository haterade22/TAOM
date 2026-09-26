# ADR-012: The graphify Code Graph Is Part of the Workflow

**Status**: Accepted

**Date**: 2026-09-26

**Priority**: Standard

## Context

graphify (PyPI `graphifyy`, Apache-2.0) builds a C# and Python code graph that answers three
questions Serena does not: the reverse blast radius of changing a type (`affected`), the
architectural hubs (`god-nodes`), and a one-screen neighbourhood of a class (`explain`). The
2026-08-18 v8 review ([adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md))
kept it installed but **wired into nothing**, to be run by hand from a scratchpad, and recorded
that disposition so it would "not be re-litigated a third time".

Five weeks later that disposition had produced the outcome the same review measured for
`/doc-graph`: nothing used it. The only graph ever built had lived in a session scratchpad and was
gone, so rebuilding it meant re-deriving the command line from the review. Every raw verb also
carries a silent trap: `extract` without `--out` writes into the scanned repo, and on 2026-09-26 an
incremental `extract` **with** `--out` still wrote `graphify-out/cache/stat-index.json` there (the
file 8318e346 once committed); `update` discards about 5,100 external-type nodes; `cluster-only`
and `label` call an LLM unless told not to; the `install` verbs rewrite CLAUDE.md, AGENTS.md,
`.claude/settings.json` or git hooks. A tool that is only safe when every flag is remembered, and
is used only when someone remembers it exists, is neither safe nor used.

On 2026-09-26 the maintainer decided graphify is to be used in every relevant workflow and run the
same correct way every time (#677).

## Decision

graphify becomes a standing part of TAOM's workflow, used and refreshed through one wrapper:

1. **`tools/graphify_taom.py` owns every graphify write.** `refresh` runs `extract --code-only`
   then `cluster-only --no-label --no-viz` into `E:\graphify\TAOM` (a linked worktree gets
   `E:\graphify\TAOM-wt-<name>`; `TAOM_GRAPHIFY_OUT` overrides), pins an absolute `GRAPHIFY_OUT`
   so no cache can land in the repo, refuses an output root inside the repo, checks the repo for
   any `graphify-out` afterwards, and writes a stamp. Query verbs pass through with `--graph`
   pinned and a staleness warning.
2. **`.claude/hooks/check-graphify-usage.sh` enforces it** on Bash and PowerShell: raw graphify
   write verbs, install verbs and `graphify-mcp` are denied with a reason naming the wrapper;
   query verbs and commands that only mention graphify are allowed.
3. **Refresh happens at the point of use.** SessionStart prints the graph's freshness. Each
   workflow step that reads the graph first runs `refresh --if-stale` (about 2.5 minutes when
   stale, in the background).
4. **The graph is read at fixed points**: `/deep-review` Step 1 (callers outside the diff for the
   completeness and data-flow lenses), `/investigate` Phase 1, `/new-feature` before the first
   file, `/research` step 6, the `feature-builder` and `refactoring-specialist` agents, and for
   every AI client through AGENTS.md and the `.agents/skills` routing.

What does **not** change from the v8 review: graphify is not the cross-domain graph. It reads no
XML or XSLT, so troop, item, culture and party-template questions stay with the taom-moduledata MCP
and `tools/validate_moduledata.py`. It is not a `doc_graph.py` replacement. No semantic pass, no MCP
server, no `install`.

## Consequences

### Positive
- A change's dependents are listed with file and line before the change is made or reviewed,
  instead of when a caller breaks.
- The traps are closed mechanically rather than by memory: the wrapper pins the flags, the gate
  denies the raw verbs, and the contamination check stops a leak from being stamped as a good build.
- The graph is reproducible: one command rebuilds it, and its stamp says what it was built from.

### Negative
- A stale graph costs about 2.5 minutes to refresh, and a workflow step waits on it unless it runs
  in the background.
- A new PreToolUse gate on every Bash and PowerShell call that mentions graphify (about 0.1 s of
  Python when it fires; a raw-text prefilter skips every other call).
- Dependence on an upstream project with fast churn (200-plus releases). An upgrade can move a
  flag or the cache layout; the wrapper's tests and its contamination check are the tripwires.
- The graph lives on the desktop's E: drive. Another machine sets `TAOM_GRAPHIFY_OUT`, or gets
  exit 3 and a report.

### Neutral
- Engine types are external nodes minted once per referencing file (54 `MobileParty` nodes), so
  "which TAOM code uses engine type X" stays a Grep or Serena question.
- A name can be ambiguous between a type and a per-file reference node; the fix is to rerun with
  the repo-relative `.cs` path graphify lists.

## Alternatives Considered

### Alternative 1: Keep it unwired (the v8 disposition)
- **Pros**: no hook, no wrapper, no standing cost.
- **Cons**: measured to mean "unused"; every run depends on remembering four flags; the only graph
  was lost with its scratchpad.
- **Why rejected**: the maintainer's decision of 2026-09-26, on that evidence.

### Alternative 2: Background auto-refresh from an async SessionStart hook
- **Pros**: the graph is fresh without anyone asking.
- **Cons**: several concurrent sessions would race on one output directory, needing a lock plus an
  atomic directory swap; a refresh would run in sessions that never read the graph.
- **Why rejected**: point-of-use refresh with a lock gives the same freshness where it matters.

### Alternative 3: Wrapper without a gate
- **Pros**: no new hook.
- **Cons**: nothing stops `graphify update` or a raw `extract`, which is how 8318e346 happened.
- **Why rejected**: a machine-checkable rule gets its gate (ADR-011, tier 1).

## Examples

### Good (Follows This ADR)

```bash
python tools/graphify_taom.py refresh --if-stale
python tools/graphify_taom.py affected "ICoopSessionProvider" --depth 2
python tools/graphify_taom.py explain "Main/Core/Logging/IModLogger.cs"
```

### Bad (Violates This ADR)

```bash
graphify update E:/graphify/TAOM                 # drops about 5,100 external-type nodes
graphify extract . --code-only                   # no --out: writes graphify-out/ into the repo
graphify claude install                          # rewrites CLAUDE.md and settings.json
```

## Migration Strategy

None for code. The graph was rebuilt through the wrapper on 2026-09-26; a graph built by hand has
no stamp and reports MISSING until the first `refresh`.

## References

- Issue [#677](https://github.com/haterade22/TAOM/issues/677)
- [docs/features/graphify-code-graph.md](../features/graphify-code-graph.md): how to use it
- [docs/reviews/adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md): the
  measurements and the disposition this ADR reverses
- graphify's `cache.py` `_stat_index_file` / `_ensure_stat_index` (v0.9.46): why an absolute
  `GRAPHIFY_OUT` is required

## Related ADRs

- [ADR-010](./010-knowledge-base-architecture.md): Knowledge-Base Architecture. Its 2026-08-18
  amendment recorded graphify as adopted for nothing; this ADR supersedes that part only, and
  ADR-010's 2026-09-26 amendment points here.
- [ADR-011](./011-knowledge-delivery-tiers.md): Knowledge Delivery Tiers. The gate is tier 1; the
  AGENTS.md row is tier 2.
