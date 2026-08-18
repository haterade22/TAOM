# External adoption review: graphify → TAOM doc-graph analytics

**Date:** 2026-06-08
**Source:** [github.com/safishamsi/graphify](https://github.com/safishamsi/graphify) (MIT)
**Superseded by:** [adopt-graphify-v8-2026-08-18.md](./adopt-graphify-v8-2026-08-18.md). The org and licence stated throughout this review are as of 2026-06-08 and are now stale: the project moved to [Graphify-Labs/graphify](https://github.com/Graphify-Labs/graphify) and relicensed to Apache-2.0, keeping `LICENSE-MIT` and a `NOTICE` covering contributions made before the relicensing. Terms of record: [provenance-register.md](../reference/provenance-register.md).
**Disposition:** Tier-2 concept port. Adopt 3 ideas as a deterministic stdlib tool; reject the rest. Lands as [ADR-010](../adrs/010-knowledge-base-architecture.md) Phase 5.
**Process:** [/adopt-external](../../.claude/skills/adopt-external/SKILL.md) (security-vet → map novel-vs-duplicative → tiered recommendation → port-never-install → review).

## What graphify is

A Python CLI + Claude Code skill that turns a folder into a queryable knowledge graph. Code is parsed locally via tree-sitter AST; docs/PDFs/images are sent to an LLM for semantic extraction. It exposes `query` / `path` / `explain` verbs, reports "god nodes" (highest-degree) and "surprising connections" (cluster bridges), tags every edge `EXTRACTED` / `INFERRED` / `AMBIGUOUS`, runs Leiden community detection, and emits interactive HTML/D3, an Obsidian vault, and NetworkX JSON. Incremental via SHA256 cache; optional MCP server + git post-commit hook. Headline benchmark: "71.5× fewer tokens per query vs reading raw files" — corpus-dependent (their own README notes ~1× on small corpora).

## Security vet (gate passed — but informs the port)

- **MIT licensed, no telemetry, no analytics** (verified on the repo). Query logs are local (`~/.cache/graphify-queries.log`), disable-able.
- **`security.py` is decent on the edges it covers:** SSRF-safe URL fetching (http/https only, per-redirect re-validation, size caps), output-dir path confinement, label sanitization (control-char strip, length cap, HTML-escape for pyvis).
- **But it has no secret redaction and no content filtering before LLM submission** — doc/image bytes go to the configured model verbatim. For a private mod with unreleased content that is a (minor) egress surface.
- **Therefore: we do not install it and do not route any TAOM content through it.** The port is deterministic and offline — it reads only literal links already in the repo, never calls an LLM or the network, and adds no dependency. (`/adopt-external` posture: port the concept, never install the tool.)

## Novel vs. duplicative

The honest finding is that **most of graphify already exists in TAOM, and several pieces contradict a standing ADR.**

| graphify capability | TAOM disposition |
|---|---|
| Code AST graph (tree-sitter, 28 languages) | **Duplicative** — Serena MCP owns C# symbol navigation (`find_symbol`, `find_referencing_symbols`). |
| Cross-reference graph over structured data | **Duplicative** — `tools/taom_schema.py` + `tools/taom_query.py` + the taom-moduledata MCP own item/troop/culture/party-template refs (`find_references`). |
| Dead-link / orphan detection | **Duplicative** — `tools/lint_docs.py`. |
| Reverse-link footers ("Referenced by") | **Duplicative** — `tools/build_backlinks.py`. |
| Curated topical map | **Duplicative** — `docs/INDEX.md` (deliberately hand-curated, per ADR-010). |
| Obsidian vault / HTML-D3 / Neo4j / GraphML export | **Rejected by ADR-010** — wikilinks + viz + docs-site generators were all explicitly considered and rejected. |
| LLM semantic extraction at build time | **Poor fit** — content egress + inference noise; `/knowledge-compile` already does human-audited semantic linking. |
| **`explain` / `path` query verbs** | **NOVEL** — TAOM has no way to interrogate the doc graph's shape. |
| **God-node / bridge metrics** | **NOVEL** — `lint_docs` builds the inbound index but uses it only for degree-0 orphan detection, then discards it. |
| **Confidence-tagging (`EXTRACTED`/`INFERRED`)** | **NOVEL as a philosophy** — maps onto `.claude/rules/evidence-over-claims.md`; adopted as a documented future phase, not built in v1. |

## What we adopted (v1)

A shared library `tools/doc_graph.py` + a CLI `tools/graph_query.py`, pure stdlib, that operate over the doc-link graph `lint_docs` already builds:

1. **`explain <doc>`** — a doc's inbound + outbound neighbourhood (token-cheap navigation).
2. **`path <a> <b>`** — BFS shortest connection chain (directed or undirected).
3. **`metrics`** — god nodes (degree centrality), **bridges** (single-edge cluster joins — the grep-impossible KB-hygiene signal), component count, and isolated orphans.

It reuses `lint_docs`'s link parser rather than copy-pasting it (`build_backlinks` already imports the same parser and re-implements only the index *walk*; the sole piece copied here is the one `strip_backlinks_region` helper, to avoid a circular import). 17 hermetic tests. Full design + interpretation guide: [docs/features/doc-graph.md](../features/doc-graph.md).

**First live run already paid for itself** (snapshot 2026-06-08): 314 nodes / 490 edges / **70 components** / **129 bridges** / **64 orphans**. The signal: the KB is a *star topology* around INDEX.md — 129 of the bridges are `INDEX.md → features/X.md`, i.e. most feature docs have no peer cross-links and hang off the curated index alone. 64 docs are fully isolated (e.g. `features/clan-heraldry.md` — a real feature doc nothing references). None of this is visible to grep or to `lint_docs`'s feature-only orphan check.

## What we rejected, and why

- **Installing graphify / adding tree-sitter / NetworkX / pyvis** — violates port-never-install; adds dependencies that route content through an LLM. Rejected.
- **C# code graph** — Serena owns it. **Game-data graph** — `taom_schema` owns it. Building either here would duplicate + add token overhead. Rejected.
- **LLM-inferred edges at build time, embeddings, Leiden community detection** — content egress, inference noise, and the stdlib-clean boundary stops at community detection. `INFERRED` edges are deferred as an opt-in, metrics-excluded future phase only.
- **HTML/D3 viz, Obsidian vault, Neo4j/GraphML** — ADR-010 rejected viz + wikilinks for documented reasons (GitHub renders markdown; the audience is Claude + Mike, not external browsers). Rejected.
- **Always-loaded MCP server** — standing token cost for a low-frequency tool ([context-budget](../../.claude/skills/context-budget/SKILL.md)). The verbs return dicts so an MCP wrapper is trivial *later* if usage proves it out. Deferred.
- **The `query` verb** — graphify's free-text `query` overlaps grep + INDEX.md. `explain`'s fuzzy resolver already covers "find the doc about X." Cut.
- **Memory-layer ingestion** — out-of-repo, harness-coupled path, mixed `[[ ]]`/markdown syntax, dangling links. Deferred (opt-in + failure-tolerant if ever built).

## Critique of graphify itself (for the record)

- The "71.5×" headline is corpus-dependent and ~1× on small corpora — the real number for TAOM's ~300 docs is unmeasured and probably modest. The *navigation* and *metrics* value is real regardless of the token-ratio claim.
- It stores a directed graph in an **undirected** NetworkX `Graph`, recording direction in `_src`/`_tgt` node attrs — a hack that loses true `DiGraph` semantics and collapses parallel edges.
- No secret redaction before LLM submission (above).
- PyPI namesquat (`graphifyy` while `graphify` is "reclaimed") signals immaturity.

## Process note — registration location

`/doc-graph` and `graph_query.py` are **not** added to the CLAUDE.md skill/tool tables. The entire ADR-010 doc-tooling layer (`lint_docs`, `build_backlinks`, `compile_research`, `/lint-docs`, `/knowledge-compile`) lives in ADR-010 + feature docs + the skill registry, **not** CLAUDE.md's game-feature tables — confirmed by grep (zero hits). Registering doc-graph the same way keeps it consistent with its siblings and avoids churning a config-protected file. It is discoverable via ADR-010 Phase 5, [docs/INDEX.md](../INDEX.md) (Infrastructure & tooling), the feature doc, and the eager skill list. _(This is a deliberate deviation from the approved plan, which predated the grep finding.)_

## Deep-review findings & fixes (2026-06-08)

`/deep-review` ran 3 focused agents (tooling correctness, completeness, docs/skill standards — the 5 C#-centric core agents were N/A for a Python+docs changeset). Verdict: **COMPLETE**, 0 HIGH, **1 MED + 5 LOW, all fixed in-session.**

| # | Sev | Finding | Fix | Why missed |
|---|-----|---------|-----|------------|
| 1 | MED | `resolve_node` rejected the `features/career-system.md` partial-path form that the CLI docstring + the user-facing "no doc matches" error message both advertised; only stem / filename / full `docs/...` path worked. Compounded by a latent bug — `ld.rel()` only normalizes `\`→`/` for paths under `REPO_ROOT`. | Added path-aligned trailing-segment matching (`/`-anchored, so `system` won't match `filesystem`) + normalize separators in `resolve_node`. The advertised form now works rather than weakening the docs. | The 17 tests used a tempdir fixture; `ld.rel()` returns an **absolute** path for tempdir files, so the relpath-resolution branch was structurally unreachable in tests — the production-only branch shipped untested. Same shape as `feedback_mirror_table_drifts_from_production` (hermetic fixtures can't exercise `REPO_ROOT`-relative code) + `feedback_user_facing_promise_must_match_code` (docs promised a form the code didn't support). |
| 2-4 | LOW | Stale "sub-second" bridges comment (really ~1s at 316 nodes); missing tests for `start==goal` identity path and mutual bidirectional links. | Comment corrected; 3 tests added (113 total). | — |
| 5-6 | LOW | Two doc inaccuracies: "build_backlinks forked the parser" (it *imports* it; only `strip_backlinks_region` was copied) and an imprecise ADR-010 "Alternatives 1 & 2" cross-ref. | Reworded in feature doc, this review, CHANGELOG, and the ADR amendment. | Narrative shorthand written faster than verified against the actual `build_backlinks` imports. |

**Lesson (codify):** when a function's behavior forks on `ld.rel()` normalization (or any `REPO_ROOT`-relative logic), a tempdir fixture exercises only the not-under-root branch. Either build the fixture under `REPO_ROOT` or assert the branch explicitly — otherwise the production path ships untested. Finding #1 was both an untested branch *and* a docs-vs-code promise mismatch in one.

## Deliverables

- `tools/doc_graph.py`, `tools/graph_query.py`, `tools/tests/test_graph_query.py` (17 tests)
- [docs/features/doc-graph.md](../features/doc-graph.md) — authoritative reference + workflow
- [.claude/skills/doc-graph/SKILL.md](../../.claude/skills/doc-graph/SKILL.md) — the repeatable-workflow entry point
- [ADR-010](../adrs/010-knowledge-base-architecture.md) Phase 5 amendment
- This review + CHANGELOG + INDEX.md entry

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md)
- [docs/features/doc-graph.md](../features/doc-graph.md)
- [docs/INDEX.md](../INDEX.md)

<!-- backlinks-end -->
