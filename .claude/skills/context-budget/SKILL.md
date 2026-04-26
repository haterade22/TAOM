---
name: context-budget
description: Audit context window consumption across TAOM agents, skills, rules, MCP servers, and CLAUDE.md. Report token estimates, flag bloat, recommend trims.
argument-hint: [optional: --verbose for per-file breakdown]
---

# Context Budget

Quantifies what TAOM's `.claude/` setup consumes from the context window before a single user message is processed. Drives every other context optimization decision (e.g., whether two-layer skill injection or three-layer compression are urgent or premature).

Adapted from [affaan-m/everything-claude-code](https://github.com/affaan-m/everything-claude-code/tree/main/skills/context-budget).

## When to Use

- Baseline diagnostic before optimizing the harness
- Session feels sluggish or hits compaction earlier than expected
- After adding agents, skills, rules, hooks, or MCP servers
- Planning to add more components and want to know if there's room

## How It Works

### Phase 1: Run the scan

```bash
bash .claude/skills/context-budget/scan.sh
```

This walks every `.claude/` component, the `.mcp.json`, and `CLAUDE.md`, then prints a structured report.

For a per-file breakdown, run with `--verbose`:

```bash
bash .claude/skills/context-budget/scan.sh --verbose
```

### Phase 2: Read the report

Sample structure:

```
TAOM Context Budget Report
===========================

Total estimated baseline overhead: ~XX,XXX tokens
Context model: Claude Opus 4.7 (1M)
Effective available context: ~XXX,XXX tokens (XX% headroom)

Component Breakdown:
+-----------------+-------+-----------+
| Component       | Count | Tokens    |
+-----------------+-------+-----------+
| CLAUDE.md       | 1     | ~X,XXX    |
| Agents          | N     | ~X,XXX    |
| Skills          | N     | ~X,XXX    |
| Rules           | N     | ~X,XXX    |
| MCP servers     | N     | ~XX,XXX   |
| Hooks (shell)   | N     | ~X,XXX    |
+-----------------+-------+-----------+

Issues Found (N):
[ranked by token savings]

Top 3 Optimizations:
1. [action] -> save ~X,XXX tokens
2. [action] -> save ~X,XXX tokens
3. [action] -> save ~X,XXX tokens
```

### Phase 3: Decide

Use the report to answer:

| Question | Threshold | Action |
|----------|-----------|--------|
| Are skills loading bodies into base context? | Skills total >10K tokens | Strongly consider two-layer skill injection |
| Are MCP servers dominating overhead? | MCP >50% of total | Audit for CLI-replaceable servers (gh, git wrappers) |
| Is CLAUDE.md too long? | >300 lines | Move repeating rules into scoped `.claude/rules/*.md` files |
| Are agent descriptions bloated? | >30 words in any agent | Tighten frontmatter — descriptions are loaded into every Task spawn |
| Are individual files heavy? | Skill >400 lines, Agent >200, Rule >100 | Split or reference an external doc |

### Phase 4: Record baseline

After the first run on a clean session, record numbers in `docs/context-budget-baseline.md`. Re-run after any harness change to detect creep.

## Token Estimation

The scanner uses simple heuristics:

- **Prose markdown** (skills, agents, rules, CLAUDE.md): `words × 1.3`
- **Code-heavy files** (.sh hooks, JSON): `chars / 4`
- **MCP tools**: `~500 tokens` per declared tool, fixed estimate
- **MCP server overhead**: `~200 tokens` per server (config + metadata)

These match Anthropic's published rough tokenizer behavior to within ~10%. Good enough for budget decisions; not exact.

## What's Counted

| Path | What it represents |
|------|-------------------|
| `CLAUDE.md` | Always loaded into every session |
| `.claude/agents/*.md` | Agent descriptions loaded with every Task tool spawn (full body loaded only when invoked) |
| `.claude/skills/*/SKILL.md` | Skills (loaded names; bodies on demand if Claude Code skill cache is two-layer — verify this!) |
| `.claude/rules/*.md` | Scoped rules; loaded conditionally based on file glob, but counted as worst-case |
| `.claude/hooks/*.sh` | Hook scripts — not loaded into context, but counted to surface candidates for consolidation |
| `.mcp.json` | MCP server count + estimated tool count overhead |

## What's NOT Counted

- User message history (variable)
- System prompt boilerplate from Claude Code itself (fixed, ~3-5K tokens)
- Memory file contents (loaded on demand)
- Plan files
- Tool call results during a session

## Best Practices

- **MCP is usually the biggest lever.** Each tool schema costs ~500 tokens. A 30-tool server eats more than every skill combined.
- **Agent descriptions are loaded always.** Even if the agent is never invoked, its description sits in the Task tool context for every spawn decision.
- **Verbose mode is for debugging.** Don't run it for routine audits — it drowns the signal.
- **Audit after changes.** Run after adding any agent/skill/MCP server to catch creep early.
- **Re-baseline quarterly.** Token tokenizer drift, model context window changes, and harness churn make the baseline file age fast.

## Common Findings (Expected for TAOM)

These are likely results based on inventory at install time (14 skills, 2 agents, 10 rules, 16 hook scripts, 5 MCP servers, 503-line CLAUDE.md):

- **MCP overhead dominant.** Serena alone has ~20+ tools (find_symbol, get_symbols_overview, find_referencing_symbols, etc.). Filesystem MCP plus git/github push the count over 50 tools. Expect MCP to be 40-60% of total overhead.
- **CLAUDE.md substantial.** 503 lines * ~10 words/line * 1.3 ~ 6,500 tokens.
- **Skills moderate.** 14 skills * average ~70 lines = ~12K tokens — tractable but worth verifying if Claude Code loads bodies eagerly.
- **Rules load contingent on globs.** Not all loaded every session, but counted at worst case.

If MCP dominates and CLAUDE.md is large, the highest-leverage trim is usually MCP server pruning, not skill refactoring.

## Notes

This skill is adapted for TAOM's layout (skills-as-directories, .mcp.json at project root). The token estimates are conservative — actual context cost varies by Claude Code version, MCP transport overhead, and tokenizer revision. Treat numbers as ordinal (which is biggest) not cardinal (exact byte count).
