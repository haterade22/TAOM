# Claude Code and VS Code update review (2026-10-08)

Review of three months of Claude Code and VS Code releases (2026-07-01 to 2026-10-08) against
TAOM's harness, following the [ECC re-review](adopt-ecc-2026-09-29.md). Mike approved or declined
every item one at a time.

## Coverage

- **Claude Code:** 87 versions, 2.1.198 (2026-07-01) to 2.1.294 (2026-10-08); dates from the npm
  registry `time` map. The raw CHANGELOG was keyword-filtered by category, not read line by line;
  every entry an item below depends on was re-grepped from the live CHANGELOG on 2026-10-08.
  Installed: CLI 2.1.288, VS Code extension 2.1.292 (`harness-facts.md` recorded 2.1.241).
- **VS Code:** 15 weekly releases, 1.127 (2026-07-01) to 1.141 (2026-10-07), read through a
  summarizing fetch. Installed: 1.140. C# Dev Kit release notes were not found.
- **TAOM harness:** settings at three scopes, 25 hook registrations, 46 skills, 9 agents, 25 rules,
  `.mcp.json`, `.vscode/`, the 134-file auto memory and the harness docs.

## Decisions

| # | Proposal | Decision | Evidence |
|---|---|---|---|
| P1 | Refresh harness facts to the installed version | Approved; done | `harness-facts.md` rows re-sourced against the memory, sub-agents, hooks, env-vars and tools-reference docs (2026-10-08). The research agent's summary that a hook `ask` is now denied in bypass mode was wrong: the hooks docs say an ask prompts, and only a `-p` run denies it, so the existing EMPIRICAL row stands and that probe was dropped. CLAUDE.md: rules load on read, write or edit; `additionalContext` added to the hook channels; the VS Code Stop and Escape behaviour. `powershell-tool.md` rewritten (the tool is on by default; a Bash deny rule turns it off). Memory topics `interrupts-kill-running-subagents` and `hook-ask-stalls-bypass` updated |
| P2 | Raise the Codex background timeout to 2 h | Approved; `review-codex` only, since the skill usage audit removed `codex-verify`; done, plus `/deep-review` Step 0, which had the same 600000 | The tools reference narrows the risk: the time limit applies to unattended runs (`-p`, SDK, CI, cloud) from v2.1.288; from v2.1.285 to v2.1.287 it applied to every session. Interactive VS Code sessions on 2.1.292 have no limit, which fits the 2026-09-30 and 2026-10-01 Codex runs finishing |
| P3 | Default un-tiered subagents to Sonnet by env | Revised, approved: no env var; one line in `model-effort-selection.md` that built-in agents carry no tier; done | The sub-agents docs: "Setting `CLAUDE_CODE_SUBAGENT_MODEL` by itself doesn't change the model the built-in Explore and Plan subagents run on", so the original proposal would not have reached Explore |
| P4 | Machine-enforce "never auto-invoke" skills | Revised, approved, then superseded: the [skill usage audit](skill-usage-audit-2026-10-08.md) removed both `codex-verify` and `migration-status`, so nothing is left to flag | |
| P5 | Prune the skill listing with `/skill-doctor` and `prompt-audit` | Approved; narrowed by the [skill usage audit](skill-usage-audit-2026-10-08.md), which retires the TAOM skills. Left: the synced claude.ai skills and the prompt-audit findings, each decided once Mike has run both commands | `/skill-doctor` (2026-10-08): 12 synced claude.ai skills at 0 uses cost about 2,660 tokens a turn; set to `"off"` in `.claude/settings.local.json` `skillOverrides` (TAOM on this machine only; `anthropic-skills:docs`, used once, stays). The key form `anthropic-skills:<name>` is unconfirmed until the next `/skill-doctor` run shows them gone. The "userSettings" duplicates it listed were five generic user commands in `~/.claude/commands/` (2026-04-14, `codex-verify` 2026-05-25); a project skill wins a name clash, but `/codex-verify`, removed from the project, still ran the old command through the now-disabled Codex plugin. All five moved to `~/.claude/commands-archive-2026-10-08/` on Mike's word (the user-settings `review-init: off` override is now inert) |
| P6 | Status line shows rate-limit usage | Approved, then dropped: Mike works in the VS Code panel, where the status line does not run | A temporary capture line in `statusline.sh` wrote no payload across several tool calls in a VS Code session, and neither the statusline nor the VS Code docs mention the panel. The capture line was removed (`statusline.sh` identical to HEAD). Fields for a later terminal use: `rate_limits.five_hour.used_percentage`, `rate_limits.seven_day.used_percentage`; the script's unscoped `used_percentage` read would then need scoping to `context_window` |
| P7 | Disable nested subagent spawns | Approved; done | `CLAUDE_CODE_MAX_SUBAGENT_SPAWN_DEPTH=1` in `.claude/settings.json` `env`. The env-vars docs: "set `1` to turn nesting off"; the concurrency cap was left alone because past it the Agent tool refuses a spawn rather than queueing it. Live check owed after a restart: a `/improve` Workflow run still starts its agents |
| P8 | Memory sweep: stale resume cards, dead links | Approved; done where git could prove it | Six cards said NOT pushed while `git branch -r --contains` showed their commit on `origin/bannerlord-1.5.x` (`0e75ec50`, `d51e4302`, `998f054c`, `473e4ccc`, `f75288eb`, `cc8fc002`): moved to `shipped-owed-followups` with "push" dropped. Nine index links pointed at files that no longer exist (README, two features, four topics, two more cards): the text kept, the link removed. Issue-number matches were too loose to close cards that named no commit, so those stay. Other sessions' live cards were not touched. MEMORY.md 18,866 to 18,383 bytes, still over its 17 KB target because new cards landed the same day |
| P9 | VS Code workspace fixes, plus updates | Approved; repo part done | "Run Tests" task uses the AGENTS.md non-deploying form; `.vscode/mcp.json.example` deleted (no code read it; its one doc mention in `mcp-servers.md` removed). VS Code 1.141 and extension 2.1.294 are Mike's updates |
| P10 | `/build-fix` tries VS Code diagnostics before a build | Declined: the skill usage audit removes `/build-fix`, and 301 build errors in 49 sessions were fixed without it (longest failing run 5), so the build loop is no measured problem | |
| P11 | Plugin and permission config cleanup | Approved, repo and user scope; user `env` left alone (it serves other projects); done | Project `enabledPlugins`: the six plugins set to `false` rather than deleted, so an install record cannot switch one back on; the now-redundant `settings.local.json` block removed. User settings: MCP allow globs for `sequential-thinking`, `context7`, `github`, `filesystem` and `git` removed (`serena` and `ilspy` kept), the four phantom `claude-code-templates` plugins removed, and a duplicate `outputStyle` key dropped. `config-protection.sh` blocked the edits; on Mike's word the override file existed only for the edits. `mcp-servers.md` corrected: `~/.claude/.mcp/user.json` is not read. All three files parse; `audit_claude_config.py` exit 0 |

## Declined without a vote

| Item | Why |
|---|---|
| Claude Mods (JS function hooks, 2.1.287+) | Rewrites 25 tested bash gates in an API that changed in 5 of the last 7 releases; Claude-only |
| `omitClaudeMd` on `fast-reader` | Saves about 7k tokens a spawn ($0.0007 on Haiku 5.5) and drops the git and evidence rules |
| Fork subagents for review lenses | Shared context breaks reviewer independence (`.ai/policy.md`) |
| Per-agent `memory:` frontmatter | Machine-local knowledge conflicts with ADR-011 |
| VS Code's own Claude agent host (1.129+) | A second Claude path; hook and rule parity unknown |
| Built-in "You should know" side agent | Token cost every turn; Stop gates and `/deep-review` cover it |
| Hook `if:` migration | Still needs a per-gate proof (unchanged from `harness-facts.md`) |
| A permanent InstructionsLoaded logger | A log nobody reads fails the simplicity criterion |

## Prompt audit

`/doctor prompt-audit` results and decisions: [prompt-audit-2026-10-08.md](prompt-audit-2026-10-08.md).
D1 (20 facts the repo contradicts) and D3 (9 dated idioms) applied; D2 (incident narratives out of 17
rules) deferred to its own session with the lessons de-dup; D4 recorded as a flag; D5 recorded.

## Follow-ups found on the way

- Prompt-audit D2: move about 45 incident narratives out of 17 rule files into the lessons files, in a
  dedicated session together with the ECC Step 11b lessons de-dup, closing with `/deep-review`.
- After Mike's restart: `/skill-doctor` should list none of the 12 `anthropic-skills:*` set off (else
  the key form is wrong); a `/improve` Workflow run should still start its agents with spawn depth 1.

- A project agent named `Explore` (`.claude/agents/Explore.md` with its own `model:`) overrides the
  built-in one (sub-agents docs, "Built-in subagents"). It would put exploration on a cheaper tier
  by machine, but it also loads CLAUDE.md and the rules like any custom agent. Not proposed here.
- `~/.claude/.mcp/user.json` (sequential-thinking, context7) is a file Claude Code does not read;
  `docs/reference/mcp-servers.md` said otherwise and is corrected. The file itself is Mike's.
- The `blender` and `substance-painter` servers are registered for `E:/repos/TAOM` in
  `~/.claude.json`, but no tool from either loaded in this session, and neither is in the failed
  list. The ECC review's owed `/mcp` check is still open.
- `config-protection.sh` blocks Claude's edits to every `settings.json` and `settings.local.json`;
  its override file name falls back to `/tmp/claude-config-override-none` because hooks do not get
  `CLAUDE_SESSION_ID` (ECC follow-up), so an override unlocks every live session at once.

- The `davinci-resolve` MCP server failed to connect on 2026-10-08 (`CONNECTION_CLOSED`). Not
  investigated; 2.1.292 made stdio servers negotiate MCP protocol 2026-07-28 by default
  (`MCP_PROTOCOL_NEGOTIATION=legacy` opts out), which is one possible cause.
