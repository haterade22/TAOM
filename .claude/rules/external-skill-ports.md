---
paths:
  - ".claude/skills/**/SKILL.md"
description: How to author a skill from scratch, plus the per-field checklist for porting one from an external suite (gstack, etc.). Prevents bad-description and port-drift bugs.
---

# Authoring & Porting Skills — Validation Checklist

This rule covers two cases: **authoring a skill from scratch** (§ Authoring a skill from scratch) and **porting one from an external suite** (§ Porting from an external suite + the field/hook/script checklists below).

## Authoring a skill from scratch

Before authoring, confirm the skill should exist: CLAUDE.md "Where new knowledge goes" item 4 is the gate. Never skill-ify a one-off, pure reference or a single command; descriptions load eagerly. Two more tests from obra/superpowers worth applying: **was the technique non-obvious to you?** and **would you reference it across multiple tasks?** If a plain doc or a CLAUDE.md line would do, write that instead. A skill that only wraps a script is a description tax ([skill usage audit](../../docs/reviews/skill-usage-audit-2026-10-08.md)).

### Description = *when to use*, not *what it does*

An unquoted `: ` in a YAML description drops the whole frontmatter: use a comma or parentheses, then run `bash tools/test_hooks.sh`.

Write `description:` as triggering conditions, ideally starting with "Use when…". The agent reads the description to decide whether to invoke; a workflow *summary* makes it follow the summary instead of opening the skill body.

- Good: *"Use when a culture's troop tree or armor set needs authoring or revamping end-to-end."*
- Bad: *"Scaffolds armor XML, swaps rosters, then validates."* (summarizes the body)

> Cap: 30 words (`tools/test_hooks.sh` 3b). Do not expand toward upstream's 1024 characters.

### Body structure

A skill holds the steps and the traps of its procedure; history and long reference go in a doc it points to. Keep a body under 20 KB: after `/compact` only about 5,000 tokens of a body return, roughly 20 KB of prose, so put what must survive first.

End every step on a completion criterion the agent can check; a vague bound invites stopping early. Test each sentence: does it change what the model does by default? If not, delete the whole sentence. State the target behaviour first, and keep a prohibition for a hard guardrail paired with its target. (From mattpocock/skills `writing-for-agents`, MIT; the source has no automated eval for these.)

### Cross-references

Every `/name` a skill body cites resolves to `.claude/skills/<name>/SKILL.md`, and every rule, ADR and `docs/` path it cites exists. A skill cites no memory file: `python tools/lint_docs.py --fail-on-drift` fails on a `feedback_` token (`dead-memory-ref`). `python tools/lint_docs.py --quick` checks markdown links only, not a bare `/name`.

### Anti-patterns (from real upstream review)

- Dated narratives rot. Keep one sentence of cause when it shows why a step cannot be skipped; the story goes in the owning doc or RCA.
- Generic placeholder labels (`step1`, `helper2`).
- Multi-runtime hedging (*"on Cursor do X, on Copilot do Y"*) — TAOM targets Claude Code; write for it.

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

The root may be a plugin repo, a marketplace or one skill folder; the whole tree is read. Compare "N files scanned" with the tree; `UNCHECKED`
(exit 3) means nothing was vetted.

`--external` raises TAOM's six SkillSpector-derived regex categories (`excessive-agency`, `memory-poisoning`, `prompt-leakage`, `tool-misuse`, `rogue-agent`, `output-handling`) from advisory to full severity for an untrusted tree; the Python-AST scan (`ast-exec`) and clean-room YARA layer (`yara-*`) fire at full severity regardless of `--external`. Resolve every CRITICAL/HIGH (or consciously reject the source) before porting any text. This is the automated supplement to — not a replacement for — the manual read in `external-repo-adoption.md` § Security pass (which also covers the heavyweight static-only NVIDIA SkillSpector option for deeper LLM-intent / taint / CVE coverage, run isolated and never installed). Full detail: [`docs/reviews/adopt-skillspector-2026-06-22.md`](../../docs/reviews/adopt-skillspector-2026-06-22.md).

## Frontmatter field check

For every field in the upstream skill's frontmatter, verify it appears in **`.claude/rules/harness-facts.md`** ("Skill load semantics") as documented and consumed by current Claude Code. `tools/test_hooks.sh` 3b fails a key off the allowlist. A documented key missing from it goes into `SKILL_KEYS` and into harness-facts.md "Skill load semantics", with a doc URL. An undocumented key is dropped.

**Specific killshots from prior reviews:**

Move an upstream `triggers:` list into `description`. Drop `version:` and `preamble-tier:` (`bash tools/test_hooks.sh` section 3b fails them).

## Hook block check

If the upstream skill declares `hooks:` in frontmatter:

1. **Confirm the lifecycle** (`harness-facts.md` "Hook lifecycle").
2. **Verify hook command paths.** The upstream may use a path like `~/.gstack/...` or `${CLAUDE_PLUGIN_DATA}/...` — these don't exist in TAOM. Use `${CLAUDE_PROJECT_DIR}/.claude/skills/<skill>/<script>.sh` for project-local scripts.
3. **Confirm the matcher names.** `Edit`, `Write` and `NotebookEdit` for files. A shell gate registers as `Bash|PowerShell` (`harness-facts.md`). Do not trust an upstream's casing.

## Hook script check

If the port includes a shell script:

1. Name folders descriptively (`harness-facts.md` "Gitignore blast radius"); `check-claude-files-tracked.sh` blocks a miss.
2. **JSON output safety.** If the script emits JSON to stdout (PreToolUse hook contract), escape backslashes and quotes in any interpolated path. Windows paths routinely contain `\`; raw paths produce invalid JSON and silently fail-open. See `.claude/skills/freeze/check-freeze.sh::_json_escape` for reference.
3. **State file reads.** Use `IFS= read -r VAR < FILE` to preserve internal whitespace. `tr -d '[:space:]'` is a footgun that strips internal spaces too: Steam install paths contain spaces.
4. **Path normalization.** On Windows + Git Bash, paths arrive in three styles: `C:\Users\...` (Windows), `/c/Users/...` (Git Bash), `C:/Users/...` (mixed). Use `cygpath -u` if available; case-insensitive comparison via `shopt -s nocasematch` for boundary checks.

## Hardcoded value check

Verify every copied constant against its source before committing it, and prefer computing it. A value nothing recomputes names its source beside it.

## Process check

After porting:

1. **Run `bash tools/context_budget_scan.sh --verbose`**: confirm the new skill appears with reasonable eager (frontmatter) and lazy (body) tokens. Description over 30 words gets flagged.
2. **Describe the port in the commit body**: it is the changelog entry (`/release` generates `CHANGELOG.md` from commit bodies).
3. **Offer `/review-codex`** for a non-trivial port (paid, on the maintainer's word): Codex catches the lifecycle and load-semantic mistakes first ports make.
4. **Re-run `/security-scan`** on TAOM's own tree after the port lands. The SkillSpector regex categories run advisory (INFO) on a self-audit; the loud, full-severity pass is the foreign-tree `--external` run you did in "Security-vet FIRST" above — don't conflate the two.
