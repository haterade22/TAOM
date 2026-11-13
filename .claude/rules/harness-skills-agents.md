---
paths:
  - ".claude/skills/*/SKILL.md"
  - ".claude/agents/**"
  - "tools/tests/test_claude_config_lint.py"
---

<!-- Path-scoped (ADR-011). Every fact cites a doc URL or an empirical context. If Claude Code
     changes, update THIS file first. -->

# Claude Code harness facts: skills and agents (verified)

What a subagent loads, and what survives `/compact`: `harness-loading.md`. A skill's `hooks:` register
when it is invoked and outlive it: `harness-facts.md` "Hook lifecycle".

## Skill load semantics

| Fact | Source | Why we care |
|---|---|---|
| Skill **descriptions** load eagerly unless the skill sets `disable-model-invocation: true`; **bodies** load only when invoked. | https://code.claude.com/docs/en/skills (DOC, 2026-04-26) | Count frontmatter, not bodies, for the eager total. |
| Consumed frontmatter (the skill allowlist): `name`, `description`, `when_to_use`, `argument-hint`, `arguments`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `disallowed-tools`, `model`, `effort`, `context` (`fork`), `agent`, `background`, `hooks`, `paths`, `shell`, `metadata`, `license`, `compatibility`. An unrecognised key is silently ignored, so a typo turns a setting off with no error. | https://code.claude.com/docs/en/claude-directory#frontmatter-fields-by-file (DOC, 2026-11-03, complete) | `triggers:` is not a field (a gstack preamble relic): move its phrases into `description`. `tools/test_hooks.sh` 3b fails a skill key off this list and a `name` that differs from the directory; a documented key missing here goes into 3b's `SKILL_KEYS` and this row. |
| A TAOM skill description stays at most 30 words: our own cap (the harness cap is 1,536 characters). | `tools/context_budget_scan.sh` flag (report only); settings-reference skillListing keys (DOC, 2026-11-03) | Description creep came back twice on `/freeze` and `/investigate`. |
| `disable-model-invocation: true` makes a skill user-only; when Claude tries to invoke one, it is told to ask the user to run it (v2.1.222). | skills docs (DOC); CHANGELOG 2.1.222 (DOC, 2026-10-08) | No TAOM skill sets it; the "never auto-invoke" row in CLAUDE.md is text only. |
| A `context: fork` skill runs in the background by default (v2.1.218; `background: false` opts out). A project skill named `verify` is offered to Claude to run right before each commit except docs-only and tests-only ones (v2.1.286). | CHANGELOG 2.1.218, 2.1.286 (DOC, 2026-10-08) | `/verify` is TAOM's; the harness nudge and `check-verification-evidence.sh` point the same way. |

## Agent (subagent) load semantics

| Fact | Source | Why we care |
|---|---|---|
| Agent descriptions take context wherever Claude chooses an agent (warning past 15,000 tokens combined, every agent still loads); an agent's **body** loads only when it runs. | sub-agents docs, "Understand automatic delegation" (DOC, 2026-11-03) | Same eager/lazy split as skills. |
| `model:` takes an alias (`sonnet`, `opus`, `haiku`, `fable`), a full id or `inherit`; `effort:` takes `low` to `max`, per model. Resolution: the spawn's `model` parameter, then the definition's `model:`, then `CLAUDE_CODE_SUBAGENT_MODEL`, then the main model. | https://code.claude.com/docs/en/sub-agents (DOC, 2026-09-18) | `deep-reviewer` pins Opus 5.5 (`claude-opus-5-5`, Mike 2026-09-23) at max effort; passing `model` on the spawn silently overrides that, so `/deep-review` never passes one. |
| Agent frontmatter (the agent allowlist): `name`, `description`, `tools`, `disallowedTools`, `model`, `permissionMode`, `maxTurns`, `autoCompactWindow`, `skills`, `mcpServers`, `hooks`, `memory`, `background`, `omitClaudeMd`, `effort`, `isolation`, `color`, `initialPrompt`, `experimental`. An unrecognised key is silently ignored; `claude --debug` names an unrecognised agent field (v2.1.296). | https://code.claude.com/docs/en/claude-directory#frontmatter-fields-by-file (DOC, 2026-11-03, complete) | `tools/test_hooks.sh` 3b fails an agent key off this list and a `name` that differs from the file stem; a documented key missing here goes into 3b's `AGENT_KEYS` and this row. |
| The Agent tool takes an `effort` parameter (v2.1.292). It overrides a definition's `effort:`, which overrides the session level; `CLAUDE_CODE_EFFORT_LEVEL` beats both. | CHANGELOG 2.1.292; sub-agents docs, "Choose an effort level" (DOC, 2026-11-02); EMPIRICAL 2026-11-02: `deep-reviewer` spawns with `effort: high` ran at `high` in their transcripts | `/deep-review` passes `high` on tier 2 and second-wave lenses. |
| Subagents start in the background by default (terminal; VS Code and desktop always from v2.1.302). Fork mode (`subagent_type` `fork`, `CLAUDE_CODE_FORK_SUBAGENT`; on in the terminal from v2.1.232 and in VS Code from v2.1.302) lets Claude start a subagent that inherits the whole parent conversation at the main model, so it escapes the tier rule; `Agent(fork)` in `permissions.deny` or `CLAUDE_CODE_FORK_SUBAGENT=0` turns it off. A subagent may spawn its own, up to `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH` layers below the main conversation (default 3 since v2.1.219; TAOM sets 1, so nesting is off). At most `CLAUDE_CODE_MAX_CONCURRENT_SUBAGENTS` run at once (default 20, v2.1.217); past it the Agent tool **refuses** the spawn rather than queueing it. | https://code.claude.com/docs/en/env-vars, sub-agents "Let subagents spawn their own subagents" (DOC, 2026-10-08) | A cap below `/deep-review`'s seven to nine parallel lenses would make it fail, so TAOM sets none. |

## Tool and session behaviour

| Fact | Source | Why we care |
|---|---|---|
| In an unattended session (`claude -p`, the Agent SDK, CI, a cloud session), a background Bash or PowerShell command stops at its `timeout` (default 30 min, max 2 h) and Claude is told why. An interactive session in the terminal, the desktop app or VS Code has no limit (v2.1.288+; from v2.1.285 to v2.1.287 the limit applied to every session). | https://code.claude.com/docs/en/tools-reference "Time limit for background commands" (DOC, 2026-10-08) | `/review-codex` and `/deep-review` Step 0 pass 7200000 so an unattended Codex run is not cut at 30 min. |
| In the VS Code panel, Stop and Escape end only the current turn; background agents keep running and are stopped one by one from the agent map (v2.1.286). The terminal CLI was not checked. | CHANGELOG 2.1.286 `[VSCode]` (DOC, 2026-10-08); live probe owed | CLAUDE.md "Subagents". |

Parallel agents that may edit single-owner files: see `docs/ai-includes/agent-teams.md` "Worktree
isolation for parallel agent runs", which also says when the harness refuses `isolation: "worktree"`.
