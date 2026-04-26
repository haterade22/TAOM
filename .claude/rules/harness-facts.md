---
description: Verified Claude Code load semantics, hook lifecycle, and frontmatter schema. Pinned source-of-truth so future skill/rule/agent edits don't recreate already-fixed bugs.
---

<!--
This rule has NO `paths:` field intentionally — see scoped-rules convention
in CLAUDE.md. It is loaded at every conversation start.

Each fact below is sourced from official Claude Code docs (URLs provided)
or from a specific TAOM bug we shipped and fixed. When you see "verified
2026-04-26" that means a Codex review pass cited the upstream doc by URL
and the assertion held up against current behavior. If Claude Code changes,
update this file as the FIRST step — never let other harness files drift
ahead of this one.
-->

# Claude Code Harness Facts (verified)

## Skill load semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Skill **descriptions** load eagerly at conversation start. Skill **bodies** (everything after frontmatter) load only when the skill is invoked. | https://code.claude.com/docs/en/skills (verified 2026-04-26) | If you're auditing context overhead, count frontmatter only for the eager total. The pre-fix `scan.sh` got this wrong and inflated the baseline 25× for skills. |
| Frontmatter fields documented as consumed: `name`, `description`, `allowed-tools`, `hooks`, `argument-hint`, `disable-model-invocation`, `when_to_use`. | docs above | `triggers:` is NOT documented. Other suites (gstack) use it for their own preamble; in Claude Code it's dead weight. Move trigger phrases into `description` or `when_to_use`. |
| Skill description should be ≤30 words. It loads on every Task spawn. | empirical / scan.sh flag | We've trimmed `/freeze` and `/investigate` twice for description creep. The bloat comes back when phrases get pasted in during edits — keep an eye on word count. |
| Skills with `disable-model-invocation: true` are user-only (no proactive invoke). | docs above | Use this for skills that cost money or create public artifacts. We currently apply it implicitly via routing-table "Never auto-invoke" tier rather than via frontmatter. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Agent **descriptions** load into the Task tool's tool-definition context for every Task spawn. Agent **bodies** load only when that specific agent is spawned. | docs (skills + Task tool) | Same eager/lazy split as skills. `scan_agents` had the same body-counting bug as `scan_skills` — caught in RCA. |
| Agent description should be ≤30 words. Loaded into every Task tool spawn. | empirical | Bloated agent descriptions tax every Task call, not just when that agent is used. |

## Hook lifecycle

| Fact | Source | Why we care |
|------|--------|-------------|
| Hooks declared in `.claude/settings.json` are **global** — they fire for every tool call matching the matcher, regardless of which skill is active. | https://code.claude.com/docs/en/hooks (verified 2026-04-26) | Use settings.json hooks for unconditional safety nets (build check, push validation). |
| Hooks declared inline in a skill's `SKILL.md` `hooks:` frontmatter are **scoped to that skill's lifecycle** — they fire only while the skill is invoked. | docs above (verified 2026-04-26) | This is what `/freeze` does. Crucial corollary: writing the `freeze-dir.txt` state file from a non-`/freeze`, non-`/investigate` context does NOT activate the hook. The state file alone is inert. |
| `/investigate` re-declares `/freeze`'s PreToolUse hook in its own SKILL.md frontmatter. This is intentional — it lets `/investigate` write the state file and have the hook fire under its own activation. | this repo's design | Don't extend this pattern blindly. Copy the inline hook block to another skill ONLY when that skill genuinely needs the same behavior, with explicit reasoning. |
| Hook scripts read tool-input JSON from stdin and emit JSON to stdout: `{}` to allow, `{"permissionDecision":"deny","message":"..."}` to block, `{"permissionDecision":"ask",...}` to prompt. Malformed JSON typically results in fail-open (allow). | docs above + check-freeze.sh test cycle | Always escape backslashes (`\` → `\\`) and quotes (`"` → `\"`) in any path interpolated into the JSON message. Windows paths routinely contain backslashes; unescaped output crashes the parser silently. |

## Rule loader (memory) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Rules WITHOUT a `paths:` field load at conversation start (always-on). | https://code.claude.com/docs/en/memory (verified 2026-04-26) | This is how `harness-facts.md`, `environment-failures.md`, and `csharp-architecture.md` etc. behave. |
| Rules WITH any `paths:` field (any glob, including `paths: ["**/*"]`) load **conditionally** — only when a file matching the glob is opened. | docs above (verified 2026-04-26) | `paths: ["**/*"]` is NOT a synonym for "always-load". To make a rule unconditional, omit `paths:` entirely. The pre-fix `environment-failures.md` had this wrong. |

## Memory file (MEMORY.md) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| MEMORY.md is loaded at the start of every conversation. Cap is whichever binds first: first ~200 lines OR first ~25KB. | https://code.claude.com/docs/en/memory (verified 2026-04-26) | Counts toward the eager startup baseline. `scan_memory()` should enforce both caps in the token estimate. |
| MEMORY.md lives at `~/.claude/projects/<project-slug>/memory/MEMORY.md`. The slug is the absolute repo path with drive letter lowercased + `--` + path with `/` and `\` replaced by `-`. | empirical (verified by reading filesystem 2026-04-26) | When auditing memory across projects, derive the exact slug from `cygpath -w "$REPO_ROOT"` then transform — substring matching on basename is ambiguous when multiple project slugs share a substring (TAOM, TAOM-Online, taommod). |

## Gitignore blast radius

| Fact | Source | Why we care |
|------|--------|-------------|
| `git check-ignore -v <path>` is the authoritative check for "is this file gitignored". Reading `.gitignore` and grepping is unreliable (multiple files, negation rules, parent dir patterns). | git docs + 2026-04-26 deep-review | The pre-fix `check-freeze.sh` was excluded by `.gitignore`'s `bin/` line (intended for `Main/bin/` .NET output) and shipped as a non-functional skill. Always run `git check-ignore` against any new file under `.claude/` before assuming it'll commit. |
| Generic patterns in `.gitignore` (`bin/`, `obj/`, `*.cache`, `tmp/`, `node_modules/`) match anywhere in the tree, not just at the repo root. | git docs | When introducing a new directory under `.claude/`, prefer descriptive names (`scripts/`, `state/`) over generic ones (`bin/`, `tmp/`, `cache/`). |

## What this rule changes about how you work

When you write or modify any skill, agent, rule, or hook in `.claude/`:

1. **If the change relies on Claude Code load behavior** (eager vs lazy, hook lifecycle, rule loader scoping, frontmatter consumption) — verify against this file's facts. If this file disagrees with what you intended, update this file FIRST (with a doc citation) before changing the harness.
2. **If you're porting a skill from an external suite** (gstack, everything-claude-code, etc.) — see `.claude/rules/external-skill-ports.md` for the per-field validation checklist.
3. **If you're committing changes touching `.claude/`** — the pre-commit hook `check-changelog-changed.sh` will fail if CHANGELOG.md isn't also updated. The hook `check-claude-files-tracked.sh` will warn if any new file under `.claude/skills/`, `.claude/agents/`, or `.claude/rules/` is gitignored.

## Last verified: 2026-04-26

This file is the source of truth for harness behavior in TAOM. Update the "Last verified" date and add new facts whenever a Codex review or experiment confirms something not yet captured here.
