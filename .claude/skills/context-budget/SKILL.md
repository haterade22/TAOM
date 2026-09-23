---
name: context-budget
description: Audit context window consumption across TAOM agents, skills, rules, MCP servers, and CLAUDE.md. Report token estimates, flag bloat, recommend trims.
argument-hint: [optional: --verbose for per-file breakdown]
---

# Context Budget

Measures what TAOM's setup puts into Claude's context before the first user message, and what every
custom subagent spawn pays. The budgets it checks come from
[ADR-011](../../../docs/adrs/011-knowledge-delivery-tiers.md). Adapted from
[affaan-m/everything-claude-code](https://github.com/affaan-m/everything-claude-code/tree/main/skills/context-budget).

## When to use

- Before optimizing the harness, and after adding an agent, skill, rule, hook, plugin or MCP server.
- When a session hits compaction earlier than expected.
- To re-baseline `docs/context-budget-baseline.md`.

## Phase 1: run the scan

```bash
bash .claude/skills/context-budget/scan.sh            # summary
bash .claude/skills/context-budget/scan.sh --verbose  # per-file breakdown
```

## Phase 2: read the report

| Line | What it counts |
|---|---|
| Eager baseline | CLAUDE.md with its `@`-imports, skill and agent descriptions, rules without `paths:`, MCP tool names, plugin descriptions, MEMORY.md (first 200 lines, cut at 25 KB) |
| Per custom-agent spawn | CLAUDE.md, its imports and the rules without `paths:`; custom and general-purpose agents load these on every spawn, and no subagent loads MEMORY.md |
| Worst case | the baseline plus every skill and agent body |
| MCP "if eager" | what the schemas would cost; they stay deferred unless `ENABLE_TOOL_SEARCH=false` |

## Phase 3: decide

The caps are constants in `tools/lint_docs.py`; `python tools/lint_docs.py --context-budget-json`
prints their values with the files they measure, and `scan.sh` reads that rather than keeping its
own copy.

| Question | Threshold | Action |
|---|---|---|
| Is CLAUDE.md with its imports too big? | `ENTRY_DOCS_MAX_BYTES`, or a file over `ENTRY_DOC_MAX_LINES` | Route detail to its owning doc, a path rule or a skill (ADR-011) |
| Are the always-load rules too big? | `UNSCOPED_RULES_MAX_BYTES` for the rules without `paths:` together | Compress, or give the rule a `paths:` scope |
| Is a path-scoped rule too big? | `SCOPED_RULE_MAX_BYTES` | Move incident narratives to lessons or an RCA |
| Is MEMORY.md drifting? | over 40 lines or 4 KB (ADR-011), or a dead link | Memory holds resume cards only; project facts go to the repo |
| Is a description bloated? | over 30 words | Trim it; descriptions load on every Task spawn |
| Are MCP schemas eager? | `ENABLE_TOOL_SEARCH=false` | Unset it, or drop servers that wrap a CLI |

The same constants gate every commit that touches an entry doc or a rule (the commit hook runs
`lint_docs.py --drift-only`) and every push (`.github/workflows/doc-budget.yml`); this scan adds the
per-spawn view, MCP and memory.

## Phase 4: record the baseline

Write the numbers into `docs/context-budget-baseline.md` with the date and what changed.

## Token estimation

Eager markdown is estimated at bytes/4: words×1.3 undercounted text dense with paths and code, putting
the 49 KB CLAUDE.md of 2026-09-22 at 8.5K tokens instead of about 12K. Descriptions use words×1.3, a
deferred MCP tool name ~15 tokens, a loaded schema ~500. Treat the numbers as ordinal: which item is
biggest, not an exact count.

## Notes

- Skill bodies are lazy, and after `/compact` each invoked skill returns capped at 5,000 tokens, so
  the most important instructions go at the top of a SKILL.md.
- Hooks cost nothing in context, but a hook that prints outside a visible channel does nothing
  either (`harness-facts.md` "Hook lifecycle").
- Per-session knobs that are not measured here: cap thinking tokens for routine work, and
  `/compact` at a logical break rather than at the wall.
