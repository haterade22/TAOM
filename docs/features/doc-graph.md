# Doc-Graph Analytics

## Overview

`tools/graph_query.py` turns the markdown links already present in `docs/` into a queryable directed graph and answers three questions grep + [INDEX.md](../INDEX.md) cannot: **what is a doc's neighbourhood** (`explain`), **how do two docs connect** (`path`), and **where is the knowledge base structurally weak** (`metrics` — god nodes, bridges, orphans). It is [ADR-010](../adrs/010-knowledge-base-architecture.md) **Phase 5: graph analytics over the existing link graph** — the deterministic, offline counterpart to the curated INDEX and the backlink footers.

## Why This Exists

TAOM's knowledge base is ~300 cross-linked docs plus 90+ memory files. The existing tooling ([lint_docs.py](../../tools/lint_docs.py), [build_backlinks.py](../../tools/build_backlinks.py), INDEX.md) curates and lints that web but never lets you *interrogate its shape*.

- **Before:** to learn how `cultural-feats` relates to `career-system` you open both docs and read. To find over-connected hubs or fragile single-link joins, you can't — grep sees lines, not topology.
- **The graphify prompt:** the external tool [safishamsi/graphify](https://github.com/safishamsi/graphify) (reviewed in [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)) builds a queryable code/doc knowledge graph with `explain`/`path` verbs and "god node" / "bridge" metrics. Most of it duplicates TAOM tooling (Serena owns C# symbols; `taom_schema` owns game-data refs; `lint_docs` owns dead-link/orphan detection) or contradicts ADR-010 (Obsidian/HTML/RAG were explicitly rejected). The three ideas that *don't* duplicate anything — query verbs, graph metrics, confidence-tagging — are what this tool adopts, applied to the doc-link graph `lint_docs` already builds and discards.
- **Without it:** KB hygiene stays invisible. An orphaned feature doc (nothing links it) or a star-topology (everything reachable only through INDEX.md) is never surfaced until a session can't find something.

## Architecture

### Design challenge

[lint_docs.py](../../tools/lint_docs.py) already computes an inbound-reference index (`build_inbound_reference_index`) but uses it only for degree-0 orphan detection, then throws it away. The challenge was to expose query + metric capability **without** copy-pasting that link-parsing logic (the markdown-link regex + resolver + code-fence skipping), without new dependencies, and without contradicting ADR-010's no-viz / no-RAG / markdown-links-not-wikilinks decisions. (`build_backlinks.py` already imports the same parser from `lint_docs`; it re-implements only the index *walk* with extra filtering — so the one thing this tool copies is the single `strip_backlinks_region` helper, isolated to avoid a circular import.)

### Solution approach

A shared library, [tools/doc_graph.py](../../tools/doc_graph.py), imports the link primitives from `lint_docs` (the single source of the markdown-link regex, code-fence skipping, link resolution, and exemption rules) and adds graph construction + algorithms. [tools/graph_query.py](../../tools/graph_query.py) is a thin CLI over it. Everything is pure Python stdlib: `collections.deque` BFS for `path`, union-find for components, naive remove-and-recount for bridges. No NetworkX, no LLM, no network — the graph is built only from links that already resolve on disk.

```
docs/**/*.md
     |
 lint_docs primitives (LINK_RE, resolve_link, code-fence skip, exemptions)   <- reused, not forked
     |
 doc_graph.build_edges  ->  directed {source -> {targets}}      (.md only; footers stripped; self-edges dropped)
     |                          |                    |
 build_inbound (transpose)   undirected_adj      degrees / connected_components / bridges
     |                          |
 explain_node / shortest_path / graph_metrics  (return dicts -> --json is free)
     |
 graph_query.py CLI  (explain | path | metrics)
```

### Why these scope boundaries (non-goals)

These are enforced in the module docstrings and matter as much as the features:

- **`.md` nodes only.** Docs may *link to* `.cs`, but source code is never a node — **Serena** owns the C# symbol graph.
- **No game-data graph** — `taom_schema.py` / `taom_query.py` / the taom-moduledata MCP own item/troop/culture references.
- **No LLM, no embeddings, no inferred edges** — edges are literal links only. Deterministic and offline.
- **No HTML/D3/Obsidian/Neo4j export, no on-disk graph cache** — ADR-010 rejected viz + wikilinks; `build_backlinks.py` owns the only persisted graph artifact (the footers). The graph rebuilds from disk each run (sub-second).
- **Not the deferred `search_docs.py`.** ADR-010 deferred a naive full-text search engine ("grep is fast enough"). This is *graph analytics*, not search — it answers topology questions grep genuinely cannot, so it does not reopen that decision.

## Configuration

None. The tool reads `docs/` directly and takes no config file. Behaviour is controlled by CLI flags (`--json`, `--top N`, `--summary`, `--directed`). The eligible-doc filter reuses `lint_docs.is_dead_link_exempt` so archived docs, Codex/lint transcripts, and `TEMPLATE.md` are excluded from the graph (the same set lint/backlinks skip).

## Key Files

| File | Purpose |
|------|---------|
| [tools/doc_graph.py](../../tools/doc_graph.py) | Shared library: edge extraction, BFS, degrees, components, bridges, metrics, node resolution. Imports link primitives from `lint_docs`. |
| [tools/graph_query.py](../../tools/graph_query.py) | CLI: `explain` / `path` / `metrics`, output rendering + `--json`. |
| [tools/tests/test_graph_query.py](../../tools/tests/test_graph_query.py) | 17 hermetic tests over a synthetic fixture graph. |
| [tools/lint_docs.py](../../tools/lint_docs.py) | Source of the reused link parser + exemptions (not modified). |
| [tools/build_backlinks.py](../../tools/build_backlinks.py) | Owns the `strip_backlinks_region` footer logic (copied once into `doc_graph` to avoid a circular import; see the TODO there). |

## Dependencies

- Python 3 stdlib only (`argparse`, `collections`, `json`, `pathlib`, `re`, `sys`). No third-party packages.
- `tools/lint_docs.py` (imported for link parsing). If lint_docs moves, update the `import lint_docs` in `doc_graph.py`.

## Tests

- [tools/tests/test_graph_query.py](../../tools/tests/test_graph_query.py) — 17 tests, hermetic (synthetic docs tree in a tempdir; never reads the live `docs/` tree, per memory `feedback_mirror_table_drifts_from_production`). Covers edge extraction (fence/inline-code/non-md/http/fragment handling), footer dedup, eligibility filter, inbound/degrees, `explain`, BFS `path` (directed + undirected + no-path + symmetry), components, **bridges** (the highest-value metric), god-node ranking, orphans, and node resolution (slug/filename/relpath + ambiguity).
- Run: `python -m unittest discover -s tools/tests -p "test_*.py"`

## When this is the ideal tool (use-cases)

Reach for doc-graph when the question is about the **shape** of the documentation — how docs relate, what's central, what's disconnected — not about their **content** (content search is grep / [INDEX.md](../INDEX.md)). Concretely:

| Situation / trigger | Command | What it gives you |
|---|---|---|
| **Orienting in an unfamiliar subsystem** before you touch it — "what docs surround the career system?" | `explain career-system` | The doc's whole neighbourhood (what references it + what it links out to) in ~15 lines — instead of opening 9 docs to reconstruct it. |
| **"Are these two areas already related?"** before building a feature that spans both | `path A B --directed` | The real link chain, or "no path" — which tells you they're documented in isolation (a genuine gap, or genuinely unrelated). |
| **Pre-refactor blast radius** — about to rename / move / split / delete a doc | `explain X` | Everything that references X (inbound) = what you'll orphan if you remove it. A **feature doc** that ranks as a god node is a signal it covers too much and wants splitting. |
| **After a batch of docs lands** (`/knowledge-compile`, a migration, a feature wave) — did any ship disconnected? | `metrics` (orphans) | The docs that exist but nothing links — so no future session or agent will ever find them. |
| **Periodic KB hygiene** — like `/skill-stocktake`, but for docs | `metrics --top 15` | God nodes (split candidates), bridges (fragile single-link joins), orphans (dead / mis-filed docs) in one pass. |
| **Finding the front door to a cluster** — "where do I start reading about X?" | `metrics` god-node ranking | The most-referenced doc in an area is its natural entry point. |
| **A token-conscious subagent** answering "what's related to X?" | `explain X --json` | A compact machine-readable neighbourhood instead of reading X's full doc and chasing its links — the context-budget lever. |
| **Pre-merge / pre-release cross-link check** | `metrics` (bridges) | `INDEX.md → X` bridges flag docs reachable **only** through the index; if that one entry is ever dropped, X vanishes from navigation. |

These compose: a typical audit is `metrics` → `explain` the worst orphan / god node → act → re-run. See [adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md) for the first real run, which surfaced TAOM's star-topology-around-INDEX.md and 64 isolated docs.

### When it's NOT the right tool

| You actually want… | Use instead |
|---|---|
| "Which doc mentions term Z?" (full-text search) | `grep` / [INDEX.md](../INDEX.md) — this is topology, not search (ADR-010 deferred a `search_docs.py` for exactly this). |
| C# type / method / reference relationships | **Serena** MCP. |
| Item / troop / culture / party-template references | `tools/taom_query.py` / the taom-moduledata MCP. |
| "Where's the doc for feature X?" — a one-off lookup | [INDEX.md](../INDEX.md), faster than building the graph. |

## How to use it

### Navigate (mid-task, token-cheap)

Instead of opening several docs to learn how they connect, ask the graph:

```bash
python tools/graph_query.py explain career-system          # who links here + what it links out to
python tools/graph_query.py path character-creation faction-map --directed   # real forward link chain
```

A node is named by bare slug (`career-system`), filename (`career-system.md`), or repo-relative path (`features/career-system.md`). Ambiguous names list their candidates. **Prefer `--directed` for `path`** — undirected paths route through the INDEX.md super-hub (it links ~140 docs), so almost any two docs are "2 hops apart through INDEX," which is rarely the insight you want.

### Audit (periodic KB hygiene)

```bash
python tools/graph_query.py metrics --summary     # one-line counts
python tools/graph_query.py metrics --top 15      # god nodes + bridges + orphans
```

Interpret the output (snapshot 2026-06-08: 314 nodes, 490 edges, 70 components, 129 bridges, 64 orphans):

- **God nodes** = highest-degree docs. The top one is normally `docs/INDEX.md` itself (the deliberate curated hub — *expected, not a problem*). A *feature* doc near the top may be doing too much and want splitting.
- **Bridges** = a single link whose removal disconnects two clusters. The common pattern `INDEX.md — features/X.md` means feature X is reachable **only** through INDEX — it has no peer cross-links. Reinforce by linking it from a related feature doc / RCA.
- **Orphans** = docs with no inbound *or* outbound `.md` link (e.g. a feature doc nobody references). Either link it into INDEX.md / a sibling doc, or delete it. This complements `lint_docs`'s feature-only orphan check (which keys on inbound from any doc).

Then route fixes through the existing pipeline — edit the docs, re-run [build_backlinks.py](../../tools/build_backlinks.py) to refresh footers, `/lint-docs` to confirm clean — and re-run `metrics` to verify the signal improved.

### Agent / session entry points

- **Sessions / the orchestrator:** invoke the `/doc-graph` skill ([.claude/skills/doc-graph/SKILL.md](../../.claude/skills/doc-graph/SKILL.md)).
- **Subagents** can't invoke skills, but they *can* run `python tools/graph_query.py …` directly — the CLI is the agent-facing surface. Verbs accept `--json` for machine consumption.

## Future phases (deferred — documented so the growth path isn't lost)

These were considered and intentionally **not** built in v1 (scope + ROI; see the adoption review):

- **`--infer` confidence-tagged edges** — graphify tags edges `EXTRACTED` vs `INFERRED`. The literal links here are all `EXTRACTED`. An `INFERRED` layer (keyword-overlap candidates, reusing `compile_research.extract_keywords`) could surface *latent* relationships, rendered distinctly and excluded from metrics by default. Deferred: keyword overlap is noisy and `/knowledge-compile` already does human-audited semantic linking.
- **Memory-layer ingestion** — the out-of-repo memory files (`[[wikilinks]]` + markdown) could be a second labelled subgraph. Deferred: the memory dir path is harness-coupled (project-slug encoding of cwd), the syntax is mixed, and `[[ ]]` targets can dangle. If built: opt-in (`--include-memory`), best-effort path derivation, failure-tolerant (skip + warn, never crash).
- **MCP exposure** — the verbs already return dicts, so wrapping them in a stdio MCP server (like `taom_mcp_server.py`) is trivial. Deferred: an always-loaded MCP is a standing token cost ([context-budget](../../.claude/skills/context-budget/SKILL.md)) for a low-frequency tool; the CLI is the right surface until usage proves otherwise.

## Changelog

- 2026-06-08 — Wired `/doc-graph` into the discoverability surfaces (CLAUDE.md skills table, agent-operating-manual tool catalog, AGENTS.md Key Paths) so subagents and the Codex reviewer find it.
- 2026-06-08 — Shipped the doc-graph tool (`tools/doc_graph.py` + `tools/graph_query.py`): `explain`/`path`/`metrics` verbs over the doc-link graph, pure-stdlib, `.md` nodes only; codified as the `/doc-graph` skill. ADR-010 Phase 5, issue #276.

## GitHub Issue

- **Issue:** [#276](https://github.com/haterade22/TAOM/issues/276) — Doc-graph analytics (ADR-010 Phase 5)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](../ai-includes/agent-operating-manual.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)

<!-- backlinks-end -->
