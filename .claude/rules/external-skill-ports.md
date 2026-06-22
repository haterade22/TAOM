---
paths:
  - ".claude/skills/**/SKILL.md"
description: How to author a skill from scratch, plus the per-field checklist for porting one from an external suite (gstack, etc.). Prevents bad-description and port-drift bugs.
---

# Authoring & Porting Skills — Validation Checklist

This rule covers two cases: **authoring a skill from scratch** (§ Authoring a skill from scratch) and **porting one from an external suite** (§ Porting from an external suite + the field/hook/script checklists below).

## Authoring a skill from scratch

Before authoring, confirm the skill *should* exist. CLAUDE.md "Workflow → Skill convention" already gives the gate (repeatable + multi-step/chains-skills + TAOM-specific gotchas) and the "do NOT skill-ify" filter (one-offs, pure reference, single commands — descriptions load eagerly, so each skill is a permanent context tax). Two more tests from obra/superpowers worth applying: **was the technique non-obvious to you?** and **would you reference it across multiple tasks?** If a plain doc or a CLAUDE.md line would do, write that instead.

### Description = *when to use*, not *what it does*

Write `description:` as triggering conditions, ideally starting with "Use when…". The agent reads the description to decide whether to invoke; a workflow *summary* makes it follow the summary instead of opening the skill body.

- Good: *"Use when a culture's troop tree or armor set needs authoring or revamping end-to-end."*
- Bad: *"Scaffolds armor XML, swaps rosters, then validates."* (summarizes the body)

> **CRITICAL divergence from the upstream source.** obra/superpowers permits descriptions up to **1024 characters** because they enumerate triggers verbatim. **TAOM caps descriptions at ≤30 words** (`harness-facts.md` — descriptions load eagerly into every session AND every Task spawn). Adopt the *"Use when…" framing*, NOT the length. Do not "fix" a short TAOM description by expanding it toward the upstream's. `/context-budget` and `/skill-stocktake` flag >30-word descriptions.

### Naming

Active voice, verb-first or gerund: `new-culture`, `finish-branch`, `lint-cleanup-loop` — not `culture-creation`, `branch-finisher`. Matches TAOM's existing skill names.

### Body structure

A skill is a thin entry point, not a manual. Per CLAUDE.md it should "point to the authoritative doc (`docs/ai-includes/*`) rather than duplicate it." Keep it to: Overview / When to Use / ordered Steps or Phases / top Gotchas. Inline code only when short; long reference belongs in a doc.

### Flowcharts only for non-obvious decisions

Use a decision tree only where branching wrong or stopping early causes errors (e.g. `/investigate`'s phase gating). Never wrap linear instructions, reference tables, or code blocks in a flowchart.

### Anti-patterns (from real upstream review)

- Dated narrative examples (*"In session 2026-05-03 we…"*) — they rot; state the rule, not the war story.
- Code embedded inside a flowchart node.
- Generic placeholder labels (`step1`, `helper2`).
- Multi-runtime hedging (*"on Cursor do X, on Copilot do Y"*) — TAOM targets Claude Code; write for it.

### Bulletproofing a discipline skill against rationalization (highest-value technique)

The strongest idea in superpowers is treating a *discipline* skill like code under TDD:

1. **RED** — run the target scenario WITHOUT the skill loaded; record the exact rationalizations the agent uses to dodge the discipline (*"I'll add the test after"*, *"this finding is obviously right"*).
2. **GREEN** — write the minimal skill text that closes those specific excuses.
3. **REFACTOR** — re-run, catch the new excuses it invents, add explicit counters. Capture them in a small **rationalization table** (excuse → why it's wrong).

TAOM's discipline rules (`evidence-over-claims`, `think-before-coding`, `simplicity-criterion`, TDD) were written rule-first, not excuse-first. Use this method when authoring the next discipline rule or hardening an existing one — a rule that states its principle only once is easy to rationalize around.

### Don't amplify a rule — point to it

When a skill needs to invoke a discipline that already lives in a rule (`evidence-over-claims.md`, `simplicity-criterion.md`, etc.), **link to the rule and add only the skill-specific delta** — the one thing that's different *here*. Do NOT restate the rule's rationale; that's amplification, not enforcement, and it taxes context on every invocation (`simplicity-criterion.md`). If skill B merely invokes skill A, don't duplicate A's guidance in B — point at A. (RCA `docs/reviews/rca-superpowers-enforcement-2026-05-29.md`: skill prose restating `evidence-over-claims.md` was flagged as "enforcement theater"; the `ship`→`deep-review` duplication was the clearest case.)

## Porting from an external suite

When you copy a skill from another repo (gstack, everything-claude-code, awesome-claude-code-subagents, etc.) into `.claude/skills/`, every frontmatter field, every hook reference, and every behavioral assumption must be validated against current Claude Code semantics. **Other suites target their own runtimes; their conventions don't necessarily transfer.**

We've shipped four port-drift bugs across three review passes (`triggers:` field unsupported, inline-hook activation conflated with state-file presence, `paths: ["**/*"]` treated as always-load, hardcoded MCP tool counts copied without verifying). The pattern is "trusted the upstream because it worked there."

## Security-vet FIRST (before trusting any field)

A foreign skill is untrusted code until vetted — porting its frontmatter/hooks/scripts means running its instructions. Before the field checks below, run the foreign tree through TAOM's auditor:

```bash
python tools/audit_claude_config.py --root <path-to-foreign-skill> --external
```

`--external` raises TAOM's six SkillSpector-derived regex categories (`excessive-agency`, `memory-poisoning`, `prompt-leakage`, `tool-misuse`, `rogue-agent`, `output-handling`) from advisory to full severity for an untrusted tree; the Python-AST scan (`ast-exec`) and clean-room YARA layer (`yara-*`) fire at full severity regardless of `--external`. Resolve every CRITICAL/HIGH (or consciously reject the source) before porting any text. This is the automated supplement to — not a replacement for — the manual read in `external-repo-adoption.md` § Security pass (which also covers the heavyweight static-only NVIDIA SkillSpector option for deeper LLM-intent / taint / CVE coverage, run isolated and never installed). Full detail: [`docs/reviews/adopt-skillspector-2026-06-22.md`](../../docs/reviews/adopt-skillspector-2026-06-22.md).

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
4. **Re-run `/security-scan`** on TAOM's own tree after the port lands. The SkillSpector regex categories run advisory (INFO) on a self-audit; the loud, full-severity pass is the foreign-tree `--external` run you did in "Security-vet FIRST" above — don't conflate the two.

## Lessons from the Tier 1 adoption (the canonical port-drift case study)

Three review passes found 19 issues total. The categories that recurred:

- **6 wrong-API-assumption bugs** — `scan.sh` body counting, hook lifecycle, rule paths semantics, frontmatter schema. Now pinned in `harness-facts.md`.
- **3 process violations** — CHANGELOG missed twice; counter math off by one. Now caught by pre-commit hook.
- **1 gitignore blast** (HIGH) — `bin/` swept up `check-freeze.sh`. Now caught by pre-commit hook + naming rule above.
- **3 stale hardcoded values** — MCP filesystem 12→13, ilspy 8→4, descriptions creeping back to 31w. Now tagged EXACT vs HEURISTIC; description bloat lint added.

Each round found fewer (8 → 7 → 4) suggesting the harness improvements work. Don't skip these checks — they exist because we paid for them.
