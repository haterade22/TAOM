# Doc-Graph Analytics

## Overview

`tools/graph_query.py` turns the markdown links already present in `docs/` into a queryable directed graph and answers three questions grep + [INDEX.md](../INDEX.md) cannot: **what is a doc's neighbourhood** (`explain`), **how do two docs connect** (`path`), and **where is the knowledge base structurally weak** (`metrics` — god nodes, bridges, orphans). It is [ADR-010](../adrs/010-knowledge-base-architecture.md) **Phase 5: graph analytics over the existing link graph** — the deterministic, offline counterpart to the curated INDEX and the backlink footers.

## Why This Exists

TAOM's knowledge base is ~300 cross-linked docs plus 90+ memory files. The existing tooling ([lint_docs.py](../../tools/lint_docs.py), [build_backlinks.py](../../tools/build_backlinks.py), INDEX.md) curates and lints that web but never lets you *interrogate its shape*.

- **Before:** to learn how `cultural-feats` relates to `career-system` you open both docs and read. To find over-connected hubs or fragile single-link joins, you can't — grep sees lines, not topology.
- **The graphify prompt:** the external tool [graphify](https://github.com/Graphify-Labs/graphify) (reviewed in [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md), then re-tested by trial install in [adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md); it was `safishamsi/graphify` under MIT at the time of the port and is now `Graphify-Labs/graphify` under Apache-2.0) builds a queryable code/doc knowledge graph with `explain`/`path` verbs and "god node" / "bridge" metrics. Most of it duplicates TAOM tooling (Serena owns C# symbols; `taom_schema` owns game-data refs; `lint_docs` owns dead-link/orphan detection) or contradicts ADR-010 (Obsidian/HTML/RAG were explicitly rejected). The three ideas that *don't* duplicate anything (query verbs, graph metrics, confidence-tagging) are what this tool adopts, applied to the doc-link graph `lint_docs` already builds and discards.
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

These compose: a typical audit is `metrics` → `explain` the worst orphan / god node → act → re-run. See [adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md) for the first real run, which surfaced TAOM's star-topology-around-INDEX.md and 64 isolated docs. The 2026-08-18 re-run is in [adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md), which found the isolates had reached 153, unnoticed because nothing had run `metrics` since.

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

Interpret the output (snapshot 2026-06-08: 314 nodes, 490 edges, 70 components, 129 bridges, 64 orphans; re-measured 2026-08-18: 538 nodes, 998 edges, 156 components, 134 bridges, **153 orphans**):

- **God nodes** = highest-degree docs. The top one is normally `docs/INDEX.md` itself (the deliberate curated hub — *expected, not a problem*). A *feature* doc near the top may be doing too much and want splitting.
- **Bridges** = a single link whose removal disconnects two clusters. The common pattern `INDEX.md — features/X.md` means feature X is reachable **only** through INDEX — it has no peer cross-links. Reinforce by linking it from a related feature doc / RCA.
- **Orphans** = docs with no inbound *or* outbound `.md` link (e.g. a feature doc nobody references). Either link it into INDEX.md / a sibling doc, or delete it. This complements `lint_docs`'s feature-only orphan check (which keys on inbound from any doc).

Then route fixes through the existing pipeline — edit the docs, re-run [build_backlinks.py](../../tools/build_backlinks.py) to refresh footers, `/lint-docs` to confirm clean — and re-run `metrics` to verify the signal improved.

**The 2026-08-18 re-measure is the cautionary case.** Ten weeks after the snapshot above, orphans had gone 64 → 153 and components 70 → 156, and not because anyone degraded the docs: the KB grew (314 → 537 nodes) while nothing re-ran this tool, so the isolates grew about twice as fast as the KB itself. `graph_query` was referenced in zero `.claude/hooks/` scripts and zero CI jobs, so `metrics` had not been run since the day it shipped. **That is now closed:** `tools/check_doc_graph_ratchet.py` gates orphans and components against `tools/doc_graph_baseline.json` in the `validate-xml` CI job. Working the isolates down and lowering the baseline is the remaining work. Measurements: [adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md).

### Agent / session entry points

- **Sessions / the orchestrator:** invoke the `/doc-graph` skill ([.claude/skills/doc-graph/SKILL.md](../../.claude/skills/doc-graph/SKILL.md)).
- **Subagents** can't invoke skills, but they *can* run `python tools/graph_query.py …` directly — the CLI is the agent-facing surface. Verbs accept `--json` for machine consumption.

## Future phases (deferred — documented so the growth path isn't lost)

These were considered and intentionally **not** built in v1 (scope + ROI; see the adoption review):

- **`--infer` confidence-tagged edges** — graphify tags edges `EXTRACTED` vs `INFERRED`. The literal links here are all `EXTRACTED`. An `INFERRED` layer (keyword-overlap candidates, reusing `compile_research.extract_keywords`) could surface *latent* relationships, rendered distinctly and excluded from metrics by default. Deferred: keyword overlap is noisy and `/knowledge-compile` already does human-audited semantic linking.
- **Memory-layer ingestion** — the out-of-repo memory files (`[[wikilinks]]` + markdown) could be a second labelled subgraph. Deferred: the memory dir path is harness-coupled (project-slug encoding of cwd), the syntax is mixed, and `[[ ]]` targets can dangle. If built: opt-in (`--include-memory`), best-effort path derivation, failure-tolerant (skip + warn, never crash).
- **MCP exposure** — the verbs already return dicts, so wrapping them in a stdio MCP server (like `taom_mcp_server.py`) is trivial. Deferred: an always-loaded MCP is a standing token cost ([context-budget](../../.claude/skills/context-budget/SKILL.md)) for a low-frequency tool; the CLI is the right surface until usage proves otherwise.

### Not to be confused with graphify itself

This tool and the external graphify answer different questions and neither substitutes for the other.
**doc-graph is for markdown topology** (which docs are orphaned, how two docs connect, which is a
hub), is deterministic and offline, and models `.md` nodes only. **graphify is for C# structure**
(blast radius, architectural hubs). Its **parser** mints no markdown file node at all, so it cannot
model doc-to-doc topology or answer an orphan question. The 27 `.md`-labelled nodes in its full graph
are every one `_origin: null`, invented by the semantic layer from prose rather than parsed, which is
the same caveat that applies to its 13 XML-looking nodes. graphify is also installed but wired into nothing, and
its node citations are not reliable. Reach for it via
[adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md) "How to actually use
it"; reach for this tool via `/doc-graph`.

### Raised by the 2026-08-18 graphify v8 trial

- **Rationale edges.** graphify treats `NOTE:` and `WHY:` comments as first-class nodes linked to the
  code they explain, and found **869** such edges in TAOM. TAOM writes rationale constantly (ADRs,
  RCAs, the lessons files, `Constraint:` and `Rejected:` commit trailers) and none of it is
  queryable. This is the most valuable idea the trial surfaced. Not built.
- **The XML and XSLT requirement, which this tool does not meet either.** TAOM's graph is now
  required to span 1,057 XML and 16 XSLT files across the repo, the live `TAOM_Map`, and the live
  `LOTRLOME_Armory`. That is **in tension with the "No game-data graph" non-goal above**, which was
  written when `taom_schema.py` was the sole owner of game-data refs and nothing needed to join the
  two. The non-goal should be revisited deliberately when that phase is written, not quietly
  dropped. See the [ADR-010 2026-08-18 amendment](../adrs/010-knowledge-base-architecture.md).
- **Nothing ran this tool, and that is now fixed.** `graph_query.py` appeared in zero hooks and zero
  CI jobs, and between 2026-06-08 and 2026-08-18 the isolated-doc count went from 64 to 153 with
  nobody noticing. `tools/check_doc_graph_ratchet.py` plus `tools/doc_graph_baseline.json` now gate
  it in the `validate-xml` job. It passes today with no slack, and has not yet had a regression to
  catch: the committed baseline was lowered to 152 orphans and 155 components on 2026-08-21, after
  this session's cross-links closed one of each. Lowering it further is the work; the gate only ever
  stops it going back up.

## Changelog

- 2026-08-18: Re-tested the upstream by trial install (graphify v8). Nothing adopted; `doc_graph.py` unchanged. Recorded the rationale-edge idea and the XML/XSLT requirement above, and the measured decay (64 to 153 orphans, 70 to 156 components) that follows from nothing invoking this tool. Review: [adopt-graphify-v8-2026-08-18.md](../reviews/adopt-graphify-v8-2026-08-18.md).
- 2026-06-08 — Wired `/doc-graph` into the discoverability surfaces (CLAUDE.md skills table, agent-operating-manual tool catalog, AGENTS.md Key Paths) so subagents and the Codex reviewer find it.
- 2026-06-08 — Shipped the doc-graph tool (`tools/doc_graph.py` + `tools/graph_query.py`): `explain`/`path`/`metrics` verbs over the doc-link graph, pure-stdlib, `.md` nodes only; codified as the `/doc-graph` skill. ADR-010 Phase 5, issue #276.

## GitHub Issue

- **Issue:** [#276](https://github.com/haterade22/TAOM/issues/276) — Doc-graph analytics (ADR-010 Phase 5)
- **Status:** Closed

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/ai-includes/agent-operating-manual.md](../ai-includes/agent-operating-manual.md)
- [docs/features/doc-health-linter.md](./doc-health-linter.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)

<!-- backlinks-end -->
