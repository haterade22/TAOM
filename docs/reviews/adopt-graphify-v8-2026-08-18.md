# External adoption review: graphify v8 trial install

**Date:** 2026-08-18
**Source:** [github.com/Graphify-Labs/graphify](https://github.com/Graphify-Labs/graphify), branch `v8`, PyPI `graphifyy` 0.9.46 (released 2026-08-17), Apache-2.0
**Disposition:** Keep installed as an ad-hoc C# analysis tool. Reject as the cross-domain graph and as a `doc_graph.py` replacement, because it fails TAOM's XML and XSLT requirement. No harness wiring.
**Supersedes:** [adopt-graphify-2026-06-08.md](./adopt-graphify-2026-06-08.md), which reviewed the predecessor repo `safishamsi/graphify` under MIT.

## Why this was re-opened

The 2026-06-08 review ported three ideas into `tools/doc_graph.py` and rejected the rest. Two things changed. The upstream became a different project: new org, branch `v8`, 1,480 commits, relicensed Apache-2.0, and grown to roughly 24 tree-sitter languages including C# and PowerShell, Leiden clustering, an MCP server, and several verbs that did not exist in June. Separately, the subset TAOM did adopt has decayed, because nothing runs it. `grep` for `graph_query` outside `docs/` returns eight files: the CLI, its shared core `doc_graph.py`, its tests, the `/doc-graph` SKILL.md that fronts it, `AGENTS.md`, `tools/README.md`, and two `plans/_audit/` harvest entries that only record it as undocumented. Zero hooks, zero CI: nothing executes it.

| Doc-graph metric | 2026-06-08 | 2026-08-18 |
|---|---|---|
| nodes / edges | 314 / 490 | 537 / 986 |
| components | 70 | 156 (one 371-node giant, 152 singletons) |
| bridges | 129 | 136 |
| orphans | 64 | **153** |

Those are the counts at the moment of the trial. Writing this review and its cross-links moved nodes and edges (538 / 998 by the end of the session) while leaving components at 156 and orphans at 153, which is the point: the volatile figures drift with any docs commit, and the structural ones do not move until somebody actually links the isolates in.

Unlike June, this was a **trial install**, on an explicit decision to lift the earlier content-egress rejection. The tool was installed in an isolated `uv` venv, run only from a scratchpad with `--out` pointed outside the repo, and no `install` subcommand was ever executed.

## The XML and XSLT requirement

Stated by the maintainer on 2026-08-18, and it is the acceptance criterion this review is judged
against: **any knowledge-graph direction TAOM adopts must cover XML and XSLT.** That is not a
preference. It is where the mod's content actually lives.

The requirement spans **three modules, not one**. TAOM's own content is in the repo, but `TAOM_Map`
and `LOTRLOME_Armory` live in the game install and are **live and unversioned**, which is precisely
why they matter: a module reinstall silently reverts edits there, and the repo's
`Main/_Module/ModuleData/settlements.xml` is a documented stale shadow of the live TAOM_Map file.

| Source | Location | XML | XSLT | Authority |
|---|---|---|---|---|
| TAOM | `E:\repos\TAOM` (repo) | 338 | 8 | source of truth |
| TAOM_Map | game install `Modules/TAOM_Map` | 313 | 1 | **LIVE, unversioned**; repo copy is a stale shadow |
| LOTRLOME_Armory | game install `Modules/LOTRLOME_Armory` | 406 | 7 | **LIVE, unversioned** |
| **total to cover** | | **1,057** | **16** | |

Within TAOM's own 338, 259 are under `Main/_Module/ModuleData` and 145 are localization language
files. The deployed `Modules/TAOM` copy (324 XML, 8 XSLT) is build output and must not be counted
twice; the repo is authoritative for it.

A graph that cannot see those files cannot answer the questions TAOM actually asks, such as which
troop, item, culture, party template, feature doc and service are all involved in one change.
graphify sees none of them, so it fails the requirement outright. The rest of this review measures
by how much, and the closing section sets out what meeting the requirement would take.

## Install notes that matter

- `uv tool install --python 3.12 'graphifyy[mcp,pdf,leiden,anthropic,svg,watch]'`. **The 3.12 pin is load-bearing.** The machine runs Python 3.14, and the `leiden` extra pins `graspologic` to Python below 3.13, so a default install silently drops community detection with no error.
- There is a `claude-cli` backend that shells out to the local `claude` CLI on a Pro/Max subscription, so the semantic layer runs with no API key. Reported cost on our run: `$0.0000`.
- `graphify claude install` writes a section into **CLAUDE.md plus a PreToolUse hook**; `codex install` writes into **AGENTS.md**. TAOM guards both paths (`config-protection.sh`, `check-claude-files-tracked.sh`). Not run, and should not be.
- `extract --out` defaults to **the target directory**, so omitting it creates `graphify-out/` inside the scanned repo. Always pass it.

## What was measured

Three runs. Nothing was written into the repo by any of them, verified by a `git status --porcelain` diff before and after and by searching the tree for `graphify-out`.

| Run | Target | Mode | Time | Result |
|---|---|---|---|---|
| B | `Main/` | `--code-only` | 43s | 1,775 files, 12,655 nodes, 27,118 edges, 0 tokens |
| C | repo root | `--code-only` | 72s | 2,679 files, 26,346 nodes, 66,252 edges, 0 tokens |
| Slice | 10 `.cs` + 3 `.md` + 3 `.xml` | `--backend claude-cli` | 153s | 140 nodes, 253 edges, 10 communities, 98,924 in / 8,265 out tokens |

### Q1 Coverage: the XML mass is invisible

Run C nodes by extension: `.cs` 17,079, `.py` 3,813, `.ps1` 99, `.sh` 81, `.xaml` 49, `.cpp` 39, `.json` 33, `.csproj` 33, `.h` 15, `.js` 14, `.sln` 2. **Zero `.xml`, zero `.xslt`.**

This is not a parsing failure, it is by design. `detect.py:45` `CODE_EXTENSIONS` includes `.csproj`, `.sln`, `.xaml` and `.razor`, which are XML dialects, but not generic `.xml`. `DOC_EXTENSIONS` is `.md .mdx .qmd .skill .txt .rst .html .yaml .yml`, also without `.xml`. The only occurrence of `.xml` anywhere in `detect.py` is in `_SECRET_PRONE_DATA_EXTS`, an **exclusion** list. Run B reported `497 file(s) not classified ... AbilityHUD.xml, AgentStatus.xml, ArmyComposition.xml (+491 more)`; run C reported 567. TAOM's 259 ModuleData XML (145 of them localization files under `Languages/`) and its 8 XSLT transforms are never collected at all.

### Q4 Cross-domain: zero edges, for a mundane reason

This was the only capability that would have justified adopting a new tool, since Serena already owns C# symbols and `taom_schema` owns game-data ids. The slice run put C#, its feature docs, and ModuleData XML in one corpus. Edge endpoints by domain:

| Endpoint pair | count |
|---|---|
| CODE to CODE | 153 |
| DOCS to DOCS | 37 |
| **CODE to DOCS** | **0** |
| any XML | 0 |

The graph is two disconnected islands. The cause is not conceptual, it is entity resolution: the AST layer emits a node labelled `TaomPartySizeModel`, and the semantic layer emits a node labelled `TaomPartySizeModel (Party Size)`. Different label, different id, no merge. Across the slice there were **0 exact label matches and 3 matches that appear only after stripping the model's parenthetical annotation** (`TaomFoodConsumptionModel (Food Relief)`, `TaomPartySizeModel (Party Size)`, `TaomCulturalFeats (Static Singleton)`). All 3 hyperedges were DOCS-only. There is no normalization pass between the two layers.

### Q2 It is not a doc_graph.py replacement

Code files each get a file-level node with `contains` edges. **Markdown files get none:** 10 `.cs` file nodes, 0 `.md` file nodes. graphify models concepts extracted *from* docs, never documents and the links between them, so it structurally cannot answer "which docs are orphaned" or "which single link joins two clusters". That is exactly what `graph_query.py metrics` exists for. The two tools answer different questions and neither substitutes for the other.

### Q3 C# analysis: genuinely good, partly duplicative

`explain TaomPartySizeModel` returned the inheritance (`DefaultPartySizeLimitModel`), all four injected services, and its methods, each with file and line and an `EXTRACTED` tag, in about 15 lines. `affected ICoopSessionProvider --depth 2` returned about 32 typed nodes. Ground truth by grep is 52 files, of which 16 are `.dll`/`.pdb` build artifacts, so 36 real `.cs`. Recall is close and the answer shape is cleaner than grep, which returns binaries as noise.

`god-nodes` on `Main/`: IModLogger 399 edges, TAOM.Adapters 382, TAOM.Core.Logging 370, VolunteerRecruitmentService 77, TAOM.Features.CareerSystem.Domain 70. Serena provides none of this.

One caution: a query for `IHeroAdapter` returned nothing, and that is **correct**. No such type exists in `Main/`; CLAUDE.md uses the name as illustrative shorthand. The real adapters are `IAgentAdapter`, `ICareerHeroAdapter`, `IBannerHeroAdapter` and others. Worth fixing in CLAUDE.md separately.

### Q5 Cost

AST extraction is free and fast: 0 tokens, 43s for 1,775 files, 72s for the whole repo. The semantic layer is not. Three markdown files cost 98,924 input and 8,265 output tokens in 153s, roughly 33k input tokens per doc. Extrapolated to the 763 markdown files under `docs/` that is on the order of 25M input tokens, and via `claude-cli` (concurrency forced to 1) many hours. Treat that as an order-of-magnitude estimate, not a measurement: chunking batches multiple docs into one 60k-token request, and an API-key backend runs 4 chunks concurrently.

graphify's own `benchmark` on `Main/` reports 639,750 words, about 853,000 tokens naive, roughly 24,183 tokens per graph query, a 35.3x reduction. The June review dismissed the headline ratio as corpus-dependent and near 1x on small corpora. On a corpus of TAOM's actual size the number is real, though it is the tool measuring itself.

### The June critique still stands, and upstream now has a diagnostic for it

The 2026-06-08 review noted that graphify stores a directed graph in an undirected NetworkX structure. The clustered `graph.json` still serializes `"directed": false, "multigraph": false`. Upstream shipped `diagnose multigraph` for exactly this. On our run B graph it reports 27,118 raw edges reduced to 23,716 post-build, 3,398 dangling-endpoint edges, and 47 undirected same-endpoint collapses.

## Verdict

**Reject** as the unified cross-domain graph. It fails the XML and XSLT requirement outright, and even the docs-to-C# join it *should* be able to make produces zero edges.

**Reject** as a `doc_graph.py` replacement. No document nodes, no link topology, no orphan detection.

**Keep installed, unwired,** as an ad-hoc C# analysis tool. `affected` (reverse blast radius), `god-nodes`, and `explain` over `Main/` cost nothing, take 43 seconds, and answer questions Serena does not. Invoke it by hand from a scratchpad. Do not run any `install` subcommand, do not register the MCP server (a standing token cost for a low-frequency tool, same reasoning as June), and do not add it to CI.

**One idea worth porting later:** run C surfaced **869 `rationale_for` edges**, graphify's treatment of `NOTE:` and `WHY:` comments as first-class nodes linked to the code they explain. TAOM has a strong written-rationale culture (ADRs, RCAs, lessons files, `Constraint:` and `Rejected:` commit trailers) and nothing makes any of it queryable. That is a real gap and a candidate for a future `doc_graph.py` phase. Not built here.

## Meeting the XML and XSLT requirement

Two paths exist. They are not equally good.

**Path 1, teach graphify to read XML and XSLT.** Mechanically feasible. The licence is Apache-2.0, so
a fork or an upstream contribution is allowed, and `ARCHITECTURE.md:75-82` gives a five-step recipe
(extractor module, suffix registration in `extract()` and `collect_files()`, `CODE_EXTENSIONS` in
`detect.py`, `_WATCHED_EXTENSIONS` in `watch.py`, fixtures). There is precedent for extractors that
are not tree-sitter grammars at all: `sln.py`, `json_config.py`, and `pascal_forms.py` are
format-specific parsers, so an `xml.py` would not be a novel shape.

**It still would not deliver the answer.** The entity-resolution defect measured above is upstream of
the file-format question. Adding XML nodes to a tool that already fails to join its C# nodes to its
doc nodes produces a *third* disconnected island, not a cross-domain graph. Path 1 also buys
permanent maintenance against a project sitting at 1,480 commits and 200-plus releases, for a
component TAOM would be the only consumer of.

**Path 2, build the join in-house. Recommended.** TAOM already owns two of the three resolvers, both
pure stdlib and both importable today:

| Layer | Owner today | State |
|---|---|---|
| doc to doc | [`tools/doc_graph.py`](../../tools/doc_graph.py) | Working. BFS, degrees, components, bridges over the doc-link graph |
| XML ids, cross-module | [`tools/taom_query.py`](../../tools/taom_query.py) + [`tools/taom_schema.py`](../../tools/taom_schema.py) | **Substantially working already.** Resolves `Item.`, `NPCCharacter.`, `Culture.`, `PartyTemplate.` refs, takes a `--game-modules` root, and walks vanilla plus `LOTRLOME_Armory`, `Alliance.Wargs`, `ADOD_Beasts`, `NavalDLC` |
| C# symbols | Serena MCP, and graphify as an ad-hoc aid | Working, external |
| **the joins between them** | **nothing** | **the actual gap** |

That second row is the important correction: TAOM is **not** starting from zero on cross-module XML.
`taom_schema.py` already knows the live-versus-shadow trap, already walks the game Modules folder, and
already handles TAOM_Map's settlement-strip with a load-order walk through Native, SandBoxCore,
SandBox, CustomBattle and TAOM_Map. The remaining gaps are specific and small enough to name:

1. **`LOTRLOME_Armory` is walked for items only.** Troop and party-template roots are limited to
   `SandBoxCore`, `SandBox`, `Native`, `StoryMode`, `CustomBattle`, so Armory-defined troops and
   templates are invisible to those registries.
2. **`TAOM_Map` is walked only for settlements and cultures**, not for its other 313 XML files.
3. **XSLT is modelled by exactly one regex.** `_SETTLEMENT_STRIP_RE` matches only an empty
   `<xsl:template match="Settlement"/>`. The other 15 XSLT files across the three modules are
   unparsed, including the culture party-template blocks whose inheritance semantics CLAUDE.md
   already flags as a live trap.
4. **Nothing joins XML ids to feature docs or to the C# services that consume them.**

So the work is a joining layer, three registry-root extensions, and genuine XSLT parsing. It is not a
new graph engine, and it is not a dependency. Both existing tools are pure stdlib, return plain dicts,
and are unit-tested, which is the reason the June review chose stdlib in the first place. This is
scoped as a proposal here and is **not built**; it needs its own issue, its own TDD cycle, and an
ADR-010 phase.

## What this does not change

The 153 orphans and 156 components are a genuine finding about TAOM's knowledge base, independent of graphify. `/doc-graph` shipped in June and has never been run since. Wiring `graph_query.py metrics` into a Stop hook or the CI validate job with a ratchet, then working the isolates down, is the higher-value piece of work and is still open.

## Deliverables

- This review.
- A `graphify` row in [provenance-register.md](../reference/provenance-register.md). The register had **no** graphify row despite `tools/doc_graph.py` deriving from it since June, so this backfills a real gap as well as recording the trial.
- No repo code or config changed. All trial artifacts live in the session scratchpad.

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/adrs/010-knowledge-base-architecture.md](../adrs/010-knowledge-base-architecture.md)
- [docs/ai-includes/external-repo-adoption.md](../ai-includes/external-repo-adoption.md)
- [docs/features/doc-graph.md](../features/doc-graph.md)
- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/reference/provenance-register.md](../reference/provenance-register.md)
- [docs/reviews/adopt-graphify-2026-06-08.md](./adopt-graphify-2026-06-08.md)

<!-- backlinks-end -->
