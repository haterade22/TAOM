# ADR-010: Knowledge-Base Architecture

**Status**: Accepted

**Date**: 2026-05-27

**Priority**: Standard

## Context

TAOM accumulated ~290 markdown files across `docs/` plus ~96 auto-memory feedback files plus a 705KB CHANGELOG. The structure grew organically — `docs/features/`, `docs/reviews/`, `docs/adrs/`, `docs/ai-includes/`, `docs/migration/`, `.claude/rules/`, and memory each developed strong local conventions but no unifying access pattern.

Symptoms surfaced repeatedly:

- New Claude sessions re-derived "where is the doc for X" by grepping. The 71 feature docs had no curated index.
- Cross-references were one-directional. A feature doc would cite an RCA, but the RCA had no list of features that referenced it.
- Stale version refs (`Bannerlord 1.3.15` when the current target is `1.4.5`) accumulated outside `docs/migration/`. No automated rot detection.
- Source materials (Tolkien lore notes, decompiled engine snippets, external mod design specs) had no home. They lived either inline in feature docs (bloating them) or scattered across memory entries.
- `docs/features/TEMPLATE.md` was the only structural standard. There was no equivalent for "where do raw source materials go" or "where do summarized research articles live."

Karpathy's "LLM Knowledge Bases" pattern (https://x.com/karpathy/status/posted-by-user) is a close fit for what TAOM had partly grown into: `raw/` → compiled wiki → Q&A → linting → search tools. The decision was whether to: (a) keep ad-hoc growth, (b) migrate to Obsidian-style `[[wikilinks]]` for graph view, or (c) layer a thin knowledge-base architecture on top of the existing markdown structure.

## Decision

Adopt a layered knowledge-base architecture on top of the existing `docs/` tree, with four shippable phases:

1. **`docs/INDEX.md`** — a hand-curated topical map across all feature docs, ADRs, reviews, ai-includes, and migration docs. Single entry point. Loaded by fresh sessions before grepping.
2. **`tools/lint_docs.py` + `/lint-docs` skill** — automated detection of dead markdown links, stale version refs, orphan feature docs, missing feature docs, and CHANGELOG-coverage gaps.
3. **`tools/build_backlinks.py`** — symmetric link footers. When feature A links to RCA B, RCA B grows a `## Referenced by` footer listing feature A. Bot-authored, idempotent, delimited by HTML comments.
4. **`docs/raw/` + `docs/research/` + `/knowledge-compile` skill** — `raw/` is the ingest layer for unstructured source materials (web clippings, lore refs, decompilation notes). `research/` is the LLM-promoted wiki layer derived from `raw/`. The skill drives the compile step.

**Link syntax**: continue using markdown `[text](path)` links throughout. Do **not** migrate to Obsidian `[[wikilinks]]`. GitHub renders markdown natively; Claude tools (Read, Edit, Grep) handle markdown links uniformly; conversion across 290+ files has high cost and no clear benefit at TAOM's scale. Auto-memory files retain their existing `[[name]]` convention (the auto-memory system specifically supports it).

**Out of scope for this ADR**:
- No vector store or RAG layer. Karpathy notes the wiki scale (~100 articles, ~400K words) works without one; TAOM is comparable.
- No conversion of CHANGELOG.md into wiki form. It stays as a chronological release log.
- No automated rewriting of existing feature docs by Claude. The `/knowledge-compile` skill is for net-new material in `raw/ → research/`, not for editing canonical feature docs.

### Amendment (2026-06-08): Phase 5 — graph analytics over the link graph

5. **`tools/doc_graph.py` + `tools/graph_query.py` + `/doc-graph` skill** — query and audit the existing doc-link graph. Three verbs: `explain` (a doc's inbound/outbound neighbourhood), `path` (BFS shortest connection chain between two docs), `metrics` (god nodes = degree centrality, bridges = single-edge cluster joins, isolated orphans). Pure stdlib, deterministic, offline — it reuses the `lint_docs.py` link parser (Phase 2) and reads only links that already resolve on disk; no LLM, no new dependency.

This is the adopted-and-scoped subset of the external [graphify](https://github.com/safishamsi/graphify) tool (review: [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)) — its `explain`/`path` verbs and god-node/bridge metrics, which nothing else in TAOM provides. The rest of graphify was rejected as duplicative (Serena owns C# symbols; `taom_schema` owns game-data refs; Phase 2/3 own dead-link/orphan/backlink) or as already-rejected-here (Obsidian/wikilinks — Alternative 1; docs-site visualization generators — Alternative 2; RAG/vector store — the Decision's "Out of scope" clause; graphify's HTML/D3 + Neo4j export follow the same no-viz rationale). Confidence-tagged `INFERRED` edges, memory-layer ingestion, and MCP exposure are documented as deferred future phases in the feature doc.

**This is NOT the deferred `search_docs.py`** (below). It does not do full-text search — it answers topology questions (`how do these two docs connect`, `which doc is an over-connected hub`, `which link holds two clusters together`) that grep cannot, so it does not reopen the search-engine deferral.

## Consequences

### Positive

- Fresh Claude sessions land on `docs/INDEX.md` (linked from CLAUDE.md) instead of grepping for feature docs. Single-read orientation.
- Backlinks reveal cross-cutting concerns. From any feature doc, one can locate every RCA, ADR, ai-include, and memory entry that references it.
- The doc-health linter catches version drift, dead links, and orphan files at CI time (or as a skill invocation), so rot is detected before it compounds.
- `docs/raw/` provides a designated home for source materials. Feature docs stay focused on canonical knowledge; lore and engine notes go in their own layer.
- The architecture is incrementally adoptable. Each of the four phases is independently useful — stop after any phase if returns diminish.

### Negative

- Backlink footers add bot-authored content to every feature doc, ADR, RCA, and memory file. PR diffs will be larger when backlinks regenerate. Mitigation: delimited HTML-comment block makes them visually skippable in review, and the regeneration is idempotent.
- `docs/INDEX.md` is hand-curated and must be updated when new feature docs land. Mitigation: Phase 2 linter flags orphan feature docs (features with no inbound INDEX/CLAUDE.md/cross-doc reference).
- One more skill to maintain (`/lint-docs`, eventually `/knowledge-compile`). Mitigation: skills are thin wrappers around Python scripts; the scripts are the durable artifact.

### Neutral

- The existing memory feedback files remain unchanged. Their `[[name]]` cross-link syntax stays as-is — it's the convention the auto-memory system uses, and they live outside the repo anyway.
- Existing `docs/` subdirectories (`features/`, `reviews/`, `adrs/`, `ai-includes/`, `migration/`, `reference/`, `scene-scripts/`, `localization/`) are untouched in layout. Only `INDEX.md`, `raw/`, and `research/` are new top-level entries.

## Alternatives Considered

### Alternative 1: Migrate to Obsidian + `[[wikilinks]]`

- **Pros**: Native graph view, automatic backlinks, Obsidian Web Clipper integration (Karpathy's actual workflow).
- **Cons**: 290+ markdown files would need link-syntax rewrite. GitHub renders `[[X]]` as plain text. Claude tools treat `[[X]]` as opaque. Two link conventions in the repo (markdown in docs, `[[]]` in memory) is worse than the consistency the current repo has.
- **Why rejected**: cost of conversion outweighs graph-view benefit at TAOM's current scale. Markdown links work in every renderer we use.

### Alternative 2: Adopt a docs-site generator (Docusaurus, MkDocs, etc.)

- **Pros**: Auto-generated TOC, search, theming. Hosted browse experience.
- **Cons**: Build step, deployment surface, dependency-on-toolchain that updates faster than mod code. Adds maintenance burden. CI complexity. Doesn't help Claude — Claude reads markdown directly.
- **Why rejected**: target audience is Claude + future Mike, not external readers. Plain markdown is the universal substrate.

### Alternative 3: Just curate INDEX.md, skip the rest

- **Pros**: Lowest effort. Solves the highest-frequency complaint ("where is the doc for X").
- **Cons**: Backlinks, lint, and raw/ each solve distinct problems (cross-reference visibility, rot prevention, source-material ingest). Stopping after INDEX leaves three known gaps unaddressed.
- **Why rejected**: each phase is independently useful but they compose; together they form a self-maintaining system. Doing only INDEX is fine as a v1, but the plan commits to all four.

### Alternative 4: Treat `.claude/` as the knowledge base (skills + rules + agents)

- **Pros**: Already structured. `.claude/rules/` are scoped by path, skills have phased workflows.
- **Cons**: `.claude/` is *workflow* knowledge (how to use Claude on TAOM). The mod-domain knowledge (what TAOM does, why it does it, what RCAs taught us) lives in `docs/` + `memory/`. They're complementary, not interchangeable.
- **Why rejected**: scope confusion. This ADR targets the mod-knowledge surface; `.claude/` stays as the workflow surface.

## Examples

### Good (follows this ADR)

```markdown
<!-- docs/features/native-skin-fixes.md -->

# Native Skin Fixes

Managed wrapper for `TAOM.NativeSkinFixes.dll`. See [character-creation](./character-creation.md) for the parent CC flow.

...

<!-- backlinks-start -->
## Referenced by
- [features/character-creation.md](./character-creation.md)
- [reviews/rca-native-skin-fixes-port-2026-05-26.md](../reviews/rca-native-skin-fixes-port-2026-05-26.md)
<!-- backlinks-end -->
```

### Bad (violates this ADR)

```markdown
<!-- docs/features/native-skin-fixes.md -->

# Native Skin Fixes

See [[character-creation]] for parent flow.   <!-- Obsidian syntax — GitHub renders as text -->

[Hand-authored "see also" section that drifts as docs move]
```

```markdown
<!-- docs/raw/some-engine-deep-dive.md mixed into docs/features/ -->

# Engine Deep Dive

[A page of decompiled snippets and lore notes interleaved — belongs in docs/raw/, not docs/features/]
```

## Migration Strategy

Phased rollout, independent commits, stop-anywhere:

1. **Phase 1 (today)**: ship `docs/INDEX.md` + this ADR. No tool changes. No existing doc changes.
2. **Phase 2 (+1 day)**: ship `tools/lint_docs.py` + `.claude/skills/lint-docs/`. Run once, file report at `docs/reviews/doc-lint-<date>.md`. Fix high-priority hits.
3. **Phase 3 (+2 days)**: ship `tools/build_backlinks.py`. First run regenerates `## Referenced by` footers across ~290 files. Single bot-authored commit reviewed structurally, not line-by-line.
4. **Phase 4 (when raw material is ready)**: ship `docs/raw/README.md`, `docs/research/README.md`, `tools/compile_research.py`, `.claude/skills/knowledge-compile/`. First test compile drops a sample raw directory in and verifies the output.

5. **Phase 5 (2026-06-08)**: ship `tools/doc_graph.py` + `tools/graph_query.py` + `.claude/skills/doc-graph/` (graph analytics — see the amendment under Decision). Reuses the Phase 2 link parser; no new dependency.

A `tools/search_docs.py` (Karpathy's "naive search engine") is **deferred indefinitely**. `Grep` over `docs/` is fast enough; revisit if a real bottleneck emerges. Note: Phase 5's `graph_query.py` is **not** this — it does graph topology, not full-text search.

## References

- Karpathy, "LLM Knowledge Bases" — the source pattern (raw → compiled wiki → Q&A → linting → search → finetune)
- `.claude/plans/karpathy-constantly-posts-tips-wondrous-willow.md` — full implementation plan with file paths, effort estimates, and verification steps (local plan file, not in repo)
- [docs/INDEX.md](../INDEX.md) — Phase 1 deliverable, shipped alongside this ADR
- [docs/features/TEMPLATE.md](../features/TEMPLATE.md) — the existing feature-doc template this ADR builds on, not replaces
- [docs/reviews/REVIEW-GUIDE.md](../reviews/REVIEW-GUIDE.md) — adversarial review process this ADR integrates with (Phase 2 linter is a CI/skill counterpart)

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/README.md](./README.md)
- [docs/features/doc-graph.md](../features/doc-graph.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/raw/README.md](../raw/README.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)
- [docs/research/README.md](../research/README.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](../reviews/adopt-graphify-2026-06-08.md)

<!-- backlinks-end -->
