---
name: knowledge-compile
description: Compile source material from docs/raw/<topic>/ into a navigable wiki node at docs/research/<topic>.md. Use when source-material has accumulated and needs summarization with cross-refs to existing feature docs / RCAs / memory.
argument-hint: <topic-path-under-docs/raw>
context: fork
agent: general-purpose
---

# Knowledge Compile

Reads `docs/raw/<topic>/`, derives a wiki node at `docs/research/<topic>.md` with summary + sources + cross-references + key claims + open questions. Implements Phase 4 of [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md).

**Input:** topic path under `docs/raw/` (e.g. `tolkien/middle-earth-geography`).
**Output:** `docs/research/<basename>.md` where `<basename>` is the leaf component (`middle-earth-geography.md`).

## What this skill does (and doesn't)

**Does:**
- Inventories `docs/raw/<topic>/` recursively.
- Greps `docs/features/`, `docs/reviews/`, `docs/ai-includes/`, `docs/adrs/` for keyword matches to seed cross-references.
- Synthesizes a wiki node following the structure documented in `docs/research/README.md`.
- Validates the output by running `/lint-docs --quick` after writing.
- Regenerates backlinks so the new node is wired in immediately.

**Doesn't:**
- Modify anything in `docs/raw/`. Source material is read-only from this skill's perspective.
- Edit existing feature docs, ADRs, or RCAs. Cross-references go in the research node, not retroactively in target docs (backlinks handle the reverse direction).
- Fetch new material from the web. The user is responsible for populating `docs/raw/` (via Obsidian Web Clipper or manual curation).

## Phases

### Phase 1: Inventory

Run the briefing probe:

```bash
python tools/compile_research.py "$ARGUMENTS"
```

This emits a markdown briefing with:
- Full file inventory of `docs/raw/<topic>/`
- Keyword-matched candidate cross-references from existing docs
- A skeleton outline for the target research node

Read the output. If `docs/raw/<topic>/` is empty, **stop** and tell the user to add source material first.

### Phase 2: Read the raw material

Open each file in `docs/raw/<topic>/`. For text files, read the full contents (these should be small — large binaries don't belong in raw/, per `docs/raw/README.md`). Note for each:
- Provenance (who/what is the source, when)
- Key claims it makes
- Anything that contradicts other sources in the same topic dir

### Phase 3: Audit the candidate cross-references

The probe's keyword-match candidates are *candidates*, not facts. For each:
- Skim the candidate doc and confirm the topic is genuinely relevant.
- Drop candidates that share only superficial keyword overlap.
- Add cross-references the probe missed — check memory file references (memory lives at the auto-memory path; reference memory entries by filename in prose).

### Phase 4: Draft the research node

Write `docs/research/<basename>.md` following the structure in `docs/research/README.md`:

```markdown
# <Title-Case Topic>

> Source materials: `docs/raw/<topic>/` — <N> files, last compiled <YYYY-MM-DD>

## Summary

<1-2 paragraphs: what does the raw material collectively say>

## Sources

- `docs/raw/<topic>/<file>` — <one-line description>
- ...

## Cross-references

- [features/<name>.md](../features/<name>.md) — <how the topic connects>
- [reviews/rca-<name>-<date>.md](../reviews/rca-<name>-<date>.md) — <past lesson>
- Memory: `feedback_<name>` — <relevant captured guidance>

## Key claims

- **<claim>** — source: `docs/raw/<topic>/<file>`. <Brief rationale.>
- ...

## Open questions

- <Gap in the source material>
- <Contradiction between sources>
- <Follow-up research that would strengthen this node>
```

Rules:
- Every claim must cite which raw file it came from (line-level if relevant). Knowledge without provenance is rumor.
- Cross-references use markdown links to existing repo files. Memory entries are mentioned by filename in prose (don't link to per-user home paths — `tools/lint_docs.py` will flag those).
- The **Open questions** section is the most important. It's how the next compile pass knows what to focus on.

### Phase 5: Wire it into the wiki

After writing the file:

```bash
python tools/lint_docs.py --quick
python tools/build_backlinks.py --dry-run
```

If lint reports dead links in the new node, fix them before applying backlinks. Then:

```bash
python tools/build_backlinks.py
```

### Phase 6: Surface the result

Tell the user:
- The path of the new research node
- How many sources it summarized
- How many cross-references it wired
- The open-questions list (so they can add raw material to address gaps)

Do **not** commit. Leave that to the user.

## Re-running on an existing topic

If `docs/research/<topic>.md` already exists, the skill overwrites it cleanly. The user can hand-edit the **Open questions** section between compile runs to refine focus; everything else is bot-authored and can be regenerated.

## Out of scope (don't do these here)

- Don't fetch from the web. Web material goes through Obsidian Web Clipper into `docs/raw/<topic>/` first.
- Don't modify existing feature docs. If you find that a research node reveals a gap in a feature doc, surface it to the user; let them update the feature doc by hand.
- Don't lint outside the new node. Phase 5's lint pass is bounded to the dead-link probe; full-tree lint is `/lint-docs`.

## See also

- [docs/raw/README.md](../../../docs/raw/README.md) — input layer rules
- [docs/research/README.md](../../../docs/research/README.md) — output layer conventions
- [ADR-010](../../../docs/adrs/010-knowledge-base-architecture.md)
- `tools/compile_research.py` — the inventory probe this skill drives
