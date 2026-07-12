# Context Budget Baseline — 2026-07-12 (post-repo-reorg)

Recorded after the 2026-07-11/12 repo reorg (CLAUDE.md decomposition + rules split). Supersedes the
2026-04-25 baseline (archived at `docs/archive/research-prompts-2026-04/context-budget-baseline.md`).
Method: `bash .claude/skills/context-budget/scan.sh` — with the `scan_rules` eager/conditional split
fixed this date (pre-fix it counted ALL rules as eager, +18K phantom tokens).

## Eager (startup) baseline

| Component | Count | Eager tok | If-invoked (lazy) |
|---|--:|--:|--:|
| CLAUDE.md | 1 | 14,173 | — |
| Rules (always-load: 7 of 22) | 22 | 8,880 | 18,242 (paths-gated) |
| Skill descriptions | 42 | 1,643 | 48,512 (bodies) |
| Agent descriptions | 5 | 246 | 4,291 (bodies) |
| MEMORY.md | 1 | 1,114 | — |
| **Subtotal excl. MCP** | | **~26,056** | |
| MCP servers (heuristic, IF schemas load eagerly) | 7 (~116 tools) | 59,400 | — |
| **Scan total** | | **~85,456** | worst-case ~136K |

## The MCP number is conditional — read this before acting on it

The 59.4K figure assumes every MCP tool schema loads at startup (~500 tok/tool heuristic).
**Empirical (2026-07-12, VSCode-extension session):** MCP tools were DEFERRED behind ToolSearch —
schemas load on demand, eager cost ≈ 0. Whether Mike's other session types (CLI, older versions)
defer is unverified. If a session shows MCP tools as immediately callable without ToolSearch,
the 59.4K applies and MCP is 70% of the eager baseline — then the levers are:
- `github` + `imagine` (HTTP, currently unauthenticated → tools dead weight either way),
- `ilspy` overlaps `taom-src` (the documented lookup order prefers `taom-src`; ilspy is the fallback),
- `filesystem`/`git` wrap operations Bash covers.
Disabling servers is a `settings.local.json` / `.mcp.json` decision — user's call, not automated.

## Comparison

| Metric | 2026-04-25 | pre-reorg 2026-07-11 | now |
|---|--:|--:|--:|
| CLAUDE.md | 5,622 tok / 503 lines | ~26–43K tok est / 893 lines / 174 KB | **14,173 tok / ~735 lines / 91 KB** |
| Always-load rules | (not split out) | ~13.7K tok est (7 rules, 55 KB) | **8,880 tok (7 rules, 46 KB)** |
| Skills | 15 | 42 | 42 (2 over-cap descriptions fixed) |
| MCP servers | 5 | 7 | 7 (deferral observed — see above) |
| Eager excl. MCP | — | ~45–60K tok est | **~26K tok** |

## Regrowth guards

- CLAUDE.md: `tools/lint_docs.py` budget (100 KB hard / 95 KB warn / 400-char rows / 600-char prose),
  ENFORCED via `--fail-on-drift` in the `check-doc-config-drift.sh` pre-commit hook.
- Skill descriptions ≤30 words: flagged by this scan + `/skill-stocktake`.
- Re-baseline here after: adding an MCP server, a new always-load rule, or ±10 KB on CLAUDE.md.
