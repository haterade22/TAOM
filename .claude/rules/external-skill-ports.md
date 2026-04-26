---
paths:
  - ".claude/skills/**/SKILL.md"
description: Per-field validation checklist when porting a skill from an external suite (gstack, everything-claude-code, etc.). Prevents port-drift bugs caught in 2026-04-26 reviews.
---

# Porting Skills From External Suites — Validation Checklist

When you copy a skill from another repo (gstack, everything-claude-code, awesome-claude-code-subagents, etc.) into `.claude/skills/`, every frontmatter field, every hook reference, and every behavioral assumption must be validated against current Claude Code semantics. **Other suites target their own runtimes; their conventions don't necessarily transfer.**

We've shipped four port-drift bugs across three review passes (`triggers:` field unsupported, inline-hook activation conflated with state-file presence, `paths: ["**/*"]` treated as always-load, hardcoded MCP tool counts copied without verifying). The pattern is "trusted the upstream because it worked there."

## Frontmatter field check

For every field in the upstream skill's frontmatter, verify it appears in **`.claude/rules/harness-facts.md`** as documented-and-consumed by current Claude Code. As of 2026-04-26 the documented fields are:

`name`, `description`, `allowed-tools`, `hooks`, `argument-hint`, `disable-model-invocation`, `when_to_use`

Anything else is either undocumented (drop it) or might be consumed (verify with a doc URL before keeping).

**Specific killshots from prior reviews:**

| Upstream field | TAOM disposition | Reason |
|---|---|---|
| `triggers:` | DROP | Not in Claude Code skill schema. gstack uses it for its own preamble. Move trigger phrases into `description`. |
| `version:` | OPTIONAL | Not consumed by Claude Code; harmless metadata. Keep if useful for tracking, drop if it's noise. |
| `preamble-tier:` | DROP | gstack-specific. |
| `model:` (skill-level) | VERIFY | Consumed by Claude Code for some configurations; check current docs before using. |

## Hook block check

If the upstream skill declares `hooks:` in frontmatter:

1. **Confirm the lifecycle assumption.** Per `harness-facts.md`: hooks declared in skill frontmatter only fire while the skill is invoked. If the skill (or its prose body) tells the user to "just write the state file" from another context, the hook will NOT fire — the state file alone is inert. Either invoke the skill explicitly, or move the hook to `.claude/settings.json` for global activation.
2. **Verify hook command paths.** The upstream may use a path like `~/.gstack/...` or `${CLAUDE_PLUGIN_DATA}/...` — these don't exist in TAOM. Use `${CLAUDE_PROJECT_DIR}/.claude/skills/<skill>/<script>.sh` for project-local scripts.
3. **Confirm the matcher names.** `Edit`, `Write`, `Bash`, `NotebookEdit` are correct as of 2026-04-26. Don't trust an upstream's matcher casing without checking.

## Hook script check

If the port includes a shell script:

1. **Avoid generic directory names** like `bin/`, `tmp/`, `cache/`, `obj/`, `node_modules/`. These are routinely caught by repo-wide gitignore patterns (`.gitignore:2 bin/` cost us a working `/freeze` skill on the first commit). Prefer descriptive names: keep the script directly in the skill dir, or use `scripts/`.
2. **Run `git check-ignore -v`** against the script after creating it. If it's ignored, RENAME the directory (don't add a gitignore exception — the underlying name choice is the bug).
3. **JSON output safety.** If the script emits JSON to stdout (PreToolUse hook contract), escape backslashes and quotes in any interpolated path. Windows paths routinely contain `\`; raw paths produce invalid JSON and silently fail-open. See `.claude/skills/freeze/check-freeze.sh::_json_escape` for reference.
4. **State file reads.** Use `IFS= read -r VAR < FILE` to preserve internal whitespace. `tr -d '[:space:]'` is a footgun that strips internal spaces too — Steam install paths contain spaces.
5. **Path normalization.** On Windows + Git Bash, paths arrive in three styles: `C:\Users\...` (Windows), `/c/Users/...` (Git Bash), `C:/Users/...` (mixed). Use `cygpath -u` if available; case-insensitive comparison via `shopt -s nocasematch` for boundary checks.

## Hardcoded value check

For every hardcoded constant the upstream uses (tool counts, file size caps, version numbers):

1. **Verify against the actual source** before copying. Don't assume the upstream's count is current.
2. **Tag in comments** as EXACT (counted from source) or HEURISTIC (estimate from upstream docs). Future maintainers need to know which to re-verify.
3. **Add a re-verify trigger.** If the value comes from a downstream dependency (e.g., MCP server tool count), note the source URL in the comment and recheck quarterly or whenever the dependency version changes.

## Process check

After porting:

1. **Run `bash .claude/skills/context-budget/scan.sh --verbose`** — confirm the new skill appears with reasonable eager (frontmatter) and lazy (body) tokens. Description over 30 words gets flagged.
2. **Update CHANGELOG.md** in the same commit. The pre-commit hook `check-changelog-changed.sh` enforces this for `.claude/` changes.
3. **Commit + run `/codex-verify`** for any non-trivial port — Codex catches the lifecycle and load-semantic mistakes Claude tends to make on first port.

## Lessons from the Tier 1 adoption (the canonical port-drift case study)

Three review passes found 19 issues total. The categories that recurred:

- **6 wrong-API-assumption bugs** — `scan.sh` body counting, hook lifecycle, rule paths semantics, frontmatter schema. Now pinned in `harness-facts.md`.
- **3 process violations** — CHANGELOG missed twice; counter math off by one. Now caught by pre-commit hook.
- **1 gitignore blast** (HIGH) — `bin/` swept up `check-freeze.sh`. Now caught by pre-commit hook + naming rule above.
- **3 stale hardcoded values** — MCP filesystem 12→13, ilspy 8→4, descriptions creeping back to 31w. Now tagged EXACT vs HEURISTIC; description bloat lint added.

Each round found fewer (8 → 7 → 4) suggesting the harness improvements work. Don't skip these checks — they exist because we paid for them.
