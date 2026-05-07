NEEDS FIXES — HIGH: 0 | MEDIUM: 1 | LOW: 3

Verified against current code in `fbfd25a..5df21ea`, plus Claude Code docs for skills/hooks/memory: https://code.claude.com/docs/en/skills , https://code.claude.com/docs/en/hooks , https://code.claude.com/docs/en/memory

## A. PER-FIX VERDICT TABLE

| # | Fix complete? | Root cause addressed? | Regression introduced? | Notes |
|---|---|---|---|---|
| H1 | Yes | Yes | Yes | `.claude/skills/context-budget/scan.sh:51-60` extracts only the first frontmatter block; no frontmatter returns `0`, frontmatter-only counts only frontmatter, and later `---` blocks are ignored. `scan_agents()` and `scan_skills()` now charge eager totals from frontmatter at `.claude/skills/context-budget/scan.sh:96-157`, and `TOTAL`/`WORST_CASE` math is correct at `.claude/skills/context-budget/scan.sh:367-371`. Regression: the `Lazy tok` column prints full-file totals, not lazy delta, at `.claude/skills/context-budget/scan.sh:108-109`, `.claude/skills/context-budget/scan.sh:143-144`, `.claude/skills/context-budget/scan.sh:391-392`. |
| H2 | Yes | Yes | No | `feature-builder` now requires explicit `/freeze` invocation at `.claude/agents/feature-builder.md:78-80`. The lifecycle rule is clearly documented in `CLAUDE.md:56-62`, and the `/investigate` carveout is explicit rather than blanket at `AGENTS.md:77-84` and `.claude/skills/investigate/SKILL.md:71-86`. In changed callsites, only `/freeze` sets the file at `.claude/skills/freeze/SKILL.md:63-66`, `/investigate` sets it with its own active hooks at `.claude/skills/investigate/SKILL.md:76-78`, `/unfreeze` clears it at `.claude/skills/unfreeze/SKILL.md:13-18`, and `check-freeze.sh` reads it at `.claude/skills/freeze/check-freeze.sh:19-20`. |
| M1 | Yes | Yes | No | `.claude/rules/environment-failures.md:1-10` now omits `paths:` entirely and explains why. `CLAUDE.md:135-149` correctly distinguishes omit-vs-glob and labels `environment-failures.md` as always-load only when `paths:` is absent. |
| M2 | Yes | Yes | No | `.claude/skills/freeze/check-freeze.sh:28-33` now preserves internal spaces by using `IFS= read -r`. Reading only the first line is intentional for a single-path state file. In bash, `read` still populates the variable on EOF-without-newline, so `|| true` avoids aborting while preserving content. A trailing space would still remain and break matching, but the writers at `.claude/skills/freeze/SKILL.md:63-66` and `.claude/skills/investigate/SKILL.md:76-78` do not emit one. |
| M3 | Partial | Partial | Yes | `scan_memory()` was added at `.claude/skills/context-budget/scan.sh:183-218`, but the locator is ambiguous: it matches any slug containing `TAOM` via `.claude/skills/context-budget/scan.sh:193-199`, and this machine has multiple matches under `C:\Users\mikew\.claude\projects\...` (`c--Users-mikew-source-repos-TAOM`, `c--Users-mikew-source-repos-TAOM-Online`, `e--repos-taommod`). Also, the 25KB cap is computed but not enforced in the word count at `.claude/skills/context-budget/scan.sh:202-209`. |
| L1 | Yes | Yes | No | `triggers:` is gone from `/freeze` and `/investigate`; phrases were preserved in `description:` at `.claude/skills/freeze/SKILL.md:3` and `.claude/skills/investigate/SKILL.md:3`. No other skill frontmatter in `.claude/skills/` still contains `triggers:`. Both descriptions are now 31 words, which is above the prior ~25-word target, but the unsupported field itself is removed. |
| L2 | Partial | Partial | Yes | `filesystem=13` is updated at `.claude/skills/context-budget/scan.sh:295-303`, and the change-note says to update sources when server counts change at `.claude/skills/context-budget/scan.sh:290-298`. The filesystem comment matches the upstream README tool list (13 tools) at https://github.com/modelcontextprotocol/servers/blob/main/src/filesystem/README.md . But the `ilspy` comment says `~8 decompile tools` at `.claude/skills/context-budget/scan.sh:297-304`, while the current package docs list 4 tools (`decompile_assembly`, `list_types`, `generate_diagrammer`, `get_assembly_info`): https://pypi.org/project/ilspy-mcp-server/ . The `serena` and `git` comment counts remain heuristic rather than verified exact counts. |

## B. NEW FINDINGS

[MEDIUM] `.claude/skills/context-budget/scan.sh:193-199` — MEMORY locator can select the wrong project — `find "$HOME/.claude/projects" ... -path "*${repo_base}*/MEMORY.md" | head -1` matches multiple local slugs for this workspace (`...TAOM`, `...TAOM-Online`, `...taommod`), so the scanner can charge another project's memory file — Fix: derive the exact Claude project slug from the full repo path, or compare normalized project-root metadata instead of basename substring matching.

[LOW] `.claude/skills/context-budget/scan.sh:202-209` — 25KB cap is not actually enforced — `capped_bytes` is computed but never used; token estimation always counts `head -200`, so large `MEMORY.md` files are overcounted whenever the byte cap binds before the line cap — Fix: estimate from the first `min(200 lines, 25KB)` slice.

[LOW] `.claude/skills/context-budget/scan.sh:108-109`, `.claude/skills/context-budget/scan.sh:143-144`, `.claude/skills/context-budget/scan.sh:370-371`, `.claude/skills/context-budget/scan.sh:391-392`, `docs/context-budget-baseline.md:84-96` — `Lazy tok` is mislabeled — the table prints full file totals for agents/skills, while `WORST_CASE` correctly adds only `(lazy - eager)`; the column reads like incremental lazy overhead but is not — Fix: either relabel the column to `If invoked total`, or print only the lazy delta.

[LOW] `.claude/skills/context-budget/scan.sh:293-297` — MCP source comments are still partly inaccurate — the new comment block improves provenance, but `ilspy` is still documented as `~8` tools while current package docs expose 4 tools — Fix: correct the comment or mark that entry explicitly `UNVERIFIED`.

## C. PROCESS COMPLIANCE

- `CHANGELOG.md` update: NON-COMPLIANT. `git diff fbfd25a..5df21ea -- CHANGELOG.md` is empty, while `CLAUDE.md:334-342` says every session must update `CHANGELOG.md`.
- GitHub issue update: UNVERIFIED. I found no repo-local evidence in `fbfd25a..5df21ea` that an issue was created or updated; if this happened externally, it is not auditable from the commit contents alone.
- Counter math: INCONSISTENT. `AGENTS.md:40` says `26 reviews, 64 bugs found`, but `docs/reviews/REVIEW-LOG.md:50-52` says review 26 added `7 confirmed ... + 1 missed by Codex` and the total is `65 bugs found`. Starting from the previous `57`, the correct new total is `65`, not `64`.

## D. RECOMMENDED FOLLOW-UP

Third fix commit is warranted.

1. Fix `scan_memory()` to resolve the exact project memory file and to enforce the 25KB cap in the token estimate.
2. Correct the `Lazy tok` labeling in both `scan.sh` and `docs/context-budget-baseline.md`.
3. Correct or explicitly mark `UNVERIFIED` the remaining MCP source comments, especially `ilspy`.
4. Add the missing `CHANGELOG.md` entry and reconcile the `AGENTS.md` bug counter with `docs/reviews/REVIEW-LOG.md`.
