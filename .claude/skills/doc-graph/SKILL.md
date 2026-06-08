---
name: doc-graph
description: Use when navigating or auditing the docs/ knowledge graph — explain a doc's links, find the path between two docs, or surface god nodes / bridges / orphans.
allowed-tools: Bash, Read
argument-hint: [explain <doc> | path <a> <b> | metrics]
---

# Doc-Graph

Query + audit TAOM's documentation knowledge graph via `tools/graph_query.py`. This is [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md) Phase 5 (graph analytics over the link graph). **Authoritative reference + interpretation guide:** [docs/features/doc-graph.md](../../../docs/features/doc-graph.md) — read it for what the metrics mean and the scope boundaries; this skill is just the entry point.

The tool is deterministic and offline (reads only the markdown links already in `docs/`). `.md` nodes only — Serena owns C# symbols, the taom-moduledata MCP owns game-data refs.

## When to use

Two modes — **Navigate** (mid-task, token-cheap) and **Audit** (periodic KB hygiene). Concrete triggers:

- Orienting in an unfamiliar subsystem before touching it → `explain X`
- Checking whether two areas are already documented as related → `path A B --directed`
- Sizing blast radius before renaming / splitting / deleting a doc → `explain X`
- After a batch of docs lands (migration, `/knowledge-compile`, feature wave) — did any ship orphaned? → `metrics`
- Periodic hygiene pass — god nodes / bridges / orphans, like `/skill-stocktake` for docs → `metrics --top 15`

**Not** for full-text search (grep / INDEX.md), C# symbols (Serena), or game-data refs (taom-moduledata MCP). Full use-case table + "when NOT to use it": [docs/features/doc-graph.md](../../../docs/features/doc-graph.md#when-this-is-the-ideal-tool-use-cases).

## Steps

### Navigate

```bash
python tools/graph_query.py explain <doc>                 # inbound + outbound neighbours
python tools/graph_query.py path <a> <b> --directed       # forward link chain (prefer --directed)
```
Name a doc by slug (`career-system`), filename, or repo-relative path. **Use `--directed` for `path`** — undirected routes through the INDEX.md super-hub and reports a misleading "2 hops through INDEX" for almost any pair.

### Audit

1. Run `python tools/graph_query.py metrics --top 15` (or `--summary` for counts only). Read the output.
2. Summarize for the user, with the interpretation from the feature doc:
   - **God nodes** — INDEX.md on top is expected (curated hub); a *feature* doc on top may want splitting.
   - **Bridges** — `INDEX.md — features/X.md` means X has no peer cross-links; reinforce it from a sibling doc/RCA.
   - **Orphans** — no inbound or outbound `.md` link; link into INDEX/a sibling, or delete.
3. Act on a couple of high-value findings, then re-run `tools/build_backlinks.py` + `/lint-docs` to refresh + confirm, and re-run `metrics` to verify the signal moved.

## Gotchas

- This is **diagnostic** — do not auto-edit docs in bulk. Surface findings; the user picks what to fix.
- Don't reach for the deferred `--infer` / memory-layer / MCP ideas — they're documented as future phases in the feature doc and were intentionally not built.
- Subagents can't invoke this skill, but they can run `python tools/graph_query.py …` directly (verbs accept `--json`).

## See also

- [docs/features/doc-graph.md](../../../docs/features/doc-graph.md) — authoritative reference
- [docs/reviews/adopt-graphify-2026-06-08.md](../../../docs/reviews/adopt-graphify-2026-06-08.md) — why this exists (graphify adoption)
- [/lint-docs](../lint-docs/SKILL.md) · [/knowledge-compile](../knowledge-compile/SKILL.md) — the rest of the ADR-010 doc-tooling layer
