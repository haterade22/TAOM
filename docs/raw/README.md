# docs/raw/ — source material ingest layer

This is the **raw materials** layer of TAOM's knowledge base (see [ADR-010](../adrs/010-knowledge-base-architecture.md)). Anything here is **unstructured source**, not curated wiki content. The compiled wiki layer lives in [docs/research/](../research/), and is generated from this directory by the `/knowledge-compile` skill.

Karpathy's pattern: `raw/` is where you drop papers, web clippings, screenshots, and decompilation notes; the LLM does the work of summarizing and cross-linking them into navigable wiki nodes.

## What goes here

Organize by **topic subdirectory**:

```
docs/raw/
├── tolkien/                    Lore for authoring authenticity
│   └── <topic>/                e.g. middle-earth-geography/, race-cosmology/
├── bannerlord-engine/          Decompilation notes, blog posts, modder forum threads, vanilla mechanics deep-dives
│   └── <topic>/                e.g. mission-lifecycle/, agent-rendering/
└── mod-design/                 External mod design references
    └── <topic>/                e.g. lotraom-comparisons/, keyforce-spec-evolutions/
```

**Accept** anything that informs design but isn't yet polished for `docs/features/`:

- Web-clipped articles (Obsidian Web Clipper exports work fine; markdown with image references)
- Excerpts from Tolkien letters, lexicons, atlases (with attribution; quote fair-use lengths)
- Screenshots of in-game phenomena (e.g. ranger silhouette references, banner ratio examples)
- Decompiled engine snippets that aren't yet referenced by a feature doc
- External mod author specs (e.g. KEYforce armor authoring conventions)
- Conversations / forum threads / Discord screenshots, with the human source named

**Reject** these — they belong elsewhere:

- Anything in active use by code → goes in `Main/_Module/ModuleData/` or `docs/features/<x>.md`
- Decompiled DLL contents → already cached at `E:\Decompiled_Bannerlord\` and `~/.taom-src/`. Don't duplicate.
- Copyrighted full chapters or unattributed bulk text. Fair-use excerpts with citation only.
- Generated build artifacts, logs, crashes → use `docs/reviews/` (RCAs) or attach to a GitHub issue.

## Conventions

- **Filenames** — kebab-case, descriptive. `numenorean-bloodline-fragments.md`, not `notes1.md`.
- **Attribution** — every file should name its source on line 1 or in frontmatter (`Source: <url> (clipped 2026-05-27)`). Knowledge without provenance is rumor.
- **Images** — keep alongside the markdown they belong to. Reference with relative paths. Large binaries (>500KB) — see "Binary policy" below.
- **No bot edits** — the `/knowledge-compile` skill **reads** this directory but does not modify it. If a raw doc needs fixing, fix it by hand.

## Binary policy

- Images, PDFs, screenshots **under 500KB**: commit directly.
- Anything larger: prefer text excerpts. If the binary itself is needed, see `.gitignore` — large binaries under `docs/raw/` are gitignored by default and should be stored out of repo (e.g. in your local Obsidian vault) with a reference text file in `docs/raw/<topic>/` that names the external file and a one-paragraph summary.
- PDFs containing copyrighted material: do not commit; extract relevant excerpts to a `.md` summary file with attribution.

## How the compile step works

1. Drop source material into `docs/raw/<topic>/`.
2. Run `/knowledge-compile <topic>` (or `python tools/compile_research.py <topic>` for the inventory-only step).
3. The skill produces `docs/research/<topic>.md` — a wiki node with summary, citations, cross-references to existing feature docs and memory entries, and an open-questions section.
4. The output joins the navigable wiki: `/lint-docs` validates it, `/build-backlinks` wires footers.

You can re-run the compile any time material is added — the resulting `research/<topic>.md` overwrites cleanly (it's bot-authored output).

## See also

- [docs/research/README.md](../research/README.md) — the output layer
- [ADR-010](../adrs/010-knowledge-base-architecture.md) — architecture decision
- [tools/compile_research.py](../../tools/compile_research.py) — inventory + cross-reference probe
- `.claude/skills/knowledge-compile/SKILL.md` — the LLM-driven compile workflow

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/research/README.md](../research/README.md)

<!-- backlinks-end -->
