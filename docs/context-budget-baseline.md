# Context Budget Baseline — 2026-08-05 (post eager-context diet round 2)

Recorded after the 2026-08-05 eager-context diet (CLAUDE.md prune round 2 + always-load rules
diet + hook fixes). Supersedes the 2026-07-12 baseline, which had drifted badly: it recorded
CLAUDE.md at 14,173 tok / 91 KB, but two subsequent restructures (2026-07-18 Tier 2, 2026-08-05
round 2) landed it at 4,717 tok / 28 KB — the old doc overstated the single largest line item 3×.
Method: `bash .claude/skills/context-budget/scan.sh` — with `scan_plugins()` added this date
(enabled plugins' skill/command descriptions were previously invisible to the scan).

## Eager (startup) baseline

| Component | Count | Eager tok | If-invoked (lazy) |
|---|--:|--:|--:|
| CLAUDE.md | 1 | 4,717 (28.4 KB) | — |
| Rules (always-load: 7 of 22) | 22 | 8,348 | 21,112 (paths-gated) |
| Skill descriptions | 42 | 1,655 | 49,940 (bodies) |
| Agent descriptions | 5 | 257 | 4,316 (bodies) |
| Plugin descriptions | 7 plugins | 259 | — |
| MEMORY.md | 1 | 1,605 | — |
| **Subtotal excl. MCP** | | **~16,841** | |
| MCP servers (heuristic, IF schemas load eagerly) | 7 (~100 tools) | 51,400 | — |
| **Scan total** | | **~68,241** | worst-case ~120K |

The always-load rule set is now 7 files / 43.5 KB: the original 5 survivors (provenance sections
stripped to `docs/reference/rule-provenance.md`), plus `working-discipline.md` (moved from
CLAUDE.md — budget-neutral) and `output-style.md` (merger of `response-style` + `ai-prose-style`).

## The MCP number is conditional — read this before acting on it

The 51.4K figure assumes every MCP tool schema loads at startup (~500 tok/tool heuristic).
**Empirical (2026-07-12 and again 2026-08-05, VSCode-extension sessions):** MCP tools were
DEFERRED behind ToolSearch — schemas load on demand, eager cost ≈ 0. If a session shows MCP tools
immediately callable without ToolSearch, the 51.4K applies and MCP dominates — then the levers are:
- `github` + `imagine` (HTTP, unauthenticated sessions → dead weight either way),
- `ilspy` overlaps `taom-src` (the documented lookup order prefers `taom-src`; ilspy is the fallback),
- `filesystem`/`git` wrap operations Bash covers.
Disabling servers is a `settings.local.json` / `.mcp.json` decision — user's call, not automated.

## Comparison

| Metric | 2026-07-12 | now (2026-08-05) |
|---|--:|--:|
| CLAUDE.md | 14,173 tok / 91 KB | **4,717 tok / 28.4 KB** (44.7 KB pre-diet same day) |
| Always-load rules | 8,880 tok (7 rules, 46 KB) | **8,348 tok** (7 rules, 43.5 KB — incl. the 4.6 KB moved in from CLAUDE.md; original-content equivalent ~38.9 KB) |
| Skills | 42 | 42 (40 eager — 2 are `disable-model-invocation`) |
| Plugins | (invisible to scan) | **7 enabled, ~259 tok measured** (`code-simplifier` disabled this date) |
| MCP servers | 7 (~116 tools est) | 7 (~100 tools — `taom-moduledata` EXACT 9, `imagine` HEURISTIC 5 added; deferral observed) |
| Eager excl. MCP | ~26,056 tok | **~16,841 tok (−35%)** |

## Regrowth guards

- CLAUDE.md: `tools/lint_docs.py` budget (46 KB hard / 44 KB warn / 400-char rows / 600-char
  prose), ENFORCED via `--fail-on-drift` in the `check-doc-config-drift.sh` pre-commit hook.
  Since 2026-08-05 the warn is report-only (it used to hard-gate — a bug); only hard violations
  block. At 28.4 KB there is ~15.6 KB of headroom before the first warning — the user declined a
  cap ratchet, so watch this line in future baselines.
- Skill descriptions ≤30 words: flagged by this scan + `/skill-stocktake`.
- Re-baseline here after: adding an MCP server or plugin, a new always-load rule, or ±10 KB on
  CLAUDE.md.
