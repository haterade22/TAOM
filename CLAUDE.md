# CLAUDE.md

@AGENTS.md
@docs/ai-includes/orientation.md

This is Claude's harness layer. The project's rules, commands and git policy (AGENTS.md) and the
map and trap index (orientation.md) are imported above, and nothing here restates them
([ADR-011](docs/adrs/011-knowledge-delivery-tiers.md)). For a `.ai` review packet, the shared role,
evidence format and no-fix boundary override the builder workflow below: a review-only assignment
authorizes no `/ship`, no fixes and no extra paid reviewers.

## How your context is delivered

- **Always**, in every session, every custom or general-purpose subagent, and again after
  `/compact`: this file, its two imports, and the rules in `.claude/rules/` that have no `paths:`.
  The Explore and Plan agents get none of it.
- **On access**, in a session or a subagent: a rule with `paths:` loads when a matching file inside
  this repo is read, written or edited, and drops at `/compact` until the next matching access. No
  rule fires for a file outside the repo (the live Armory, TAOM_Map, the Modding Kit); the skill for
  that work carries its traps ([rules catalog](docs/reference/rules-catalog.md)).
- **On invoke:** a skill's body. After `/compact` only its first ~5,000 tokens come back.
- **Memory:** MEMORY.md loads in the main session only, on this machine only, never in a subagent.
- **Hooks** reach you only as SessionStart output, a deny reason, exit-2 stderr, a Stop hook's
  block reason or `additionalContext` beside a tool result (an ask reason goes to the user); follow
  the instruction in the message
  ([hooks catalog](docs/reference/hooks-catalog.md)).

## Where new knowledge goes

Pick the narrowest trigger that fires every time the fact is needed:

1. A machine can check it: write the gate, then one line naming it.
2. Every task, every AI client: AGENTS.md. Claude's harness only: this file.
3. In-repo files of one area: that area's `paths:` rule.
4. A procedure, or work outside the repo: that skill, near the top. A repeatable multi-step
   workflow with TAOM gotchas becomes a skill (description at most 30 words, listed below).
5. Feature or engine detail: `docs/features/`, `docs/reference/`.
6. A trap: its full text in the owning doc, plus one line in the orientation.md trap index.
7. A lesson: append to `docs/reviews/lessons/<category>.md`; promote a standing rule as one line
   into 3 or 4.
8. Uncommitted or unpushed work on this machine: a memory resume card, deleted once pushed.

Never put counts nothing computes, incident narratives, or copies of another tier's text into 2 or 3.

## Skills

The skill list is not re-sent after `/compact`; this index is. A SKILL.md is executable
instructions: follow its phases in order.

| When | Invoke | Gate |
|---|---|---|
| Crash, exception, "why is this broken" | `/investigate` | always; never debug ad hoc |
| Native AV in `TaleWorlds.Native.dll`, CTD with no managed culprit, a hang | `/native-crash-triage` | always; never blind-retry |
| `error CS####`, build won't compile | fix it within the retry budget in `docs/ai-includes/agent-operating-manual.md` | missing TaleWorlds type: `taom-src`; budget spent: `/investigate` |
| Before overriding, patching or adapting a TaleWorlds type | `pwsh tools/taom-src.ps1 path <Type>` | always |
| Before any commit touching C# or XML/XSLT, repo or live install | `/deep-review` | every such commit; skip only config and docs |
| Before claiming done | `/verify` | always |
| "Let's merge", "ready to PR" | `/ship` | runs `/verify`, `/deep-review`, `/review-codex`, then issue and docs |
| Versioning a build for players | `/release` | after `/ship`, never instead of it |
| Game updated, or the GAME VERSION DRIFT banner | `/engine-bump` | before any build or test |
| Armory sync or art drop, or the ARMORY ART DRIFT banner | `/armory-audit` | before any battle or tournament smoke |
| New creature or mount; creature animation looks wrong | `creature-mount-authoring.md`; `creature-animation-blender-mcp-workflow.md` (`docs/ai-includes/`) | once told "do it", not while sketching |
| Custom static prop (camp, landmark) without the Kit | `docs/reference/tpac-static-prop-authoring.md` | once told "do it" |
| New feature | `/new-feature`, then offer `/freeze` | once told "do it" |
| Culture, armor, lord skills | `new-culture-authoring.md`, `lord-skills-authoring.md` (`docs/ai-includes/`); `docs/features/troop-tree-revamp.md` | |
| New player-facing text | `/localize` | always |
| Patch, model or reflection bindings after an engine or patch change | the binding gate: `docs/reference/taleworlds-api-snapshot/README.md` | |
| An architectural decision | `docs/adrs/000-template.md` | |
| External repo, article or skill to adopt | `/adopt-external` | security-vet first |
| After editing hooks, settings, MCP config or CLAUDE.md | `/security-scan` | skip for routine feature edits |
| Repo-wide audit, "what next", handoff plans; execute, review, merge or resume a plans backlog | `/improve` | merge, push, issues and paid calls only on the maintainer's word |
| Offer, don't auto-invoke | `/freeze`, `/unfreeze` | |
| Never auto-invoke | `/review-codex` (paid), `/issue` (public) | |

## Subagents

- Brief every non-trivial spawn with its task and scope, and say it cannot invoke skills or spawn
  agents. `Main/IoC.cs` and `Main/SubModule.cs` are single-owner: recommend, don't edit. Signatures
  come from `pwsh tools/taom-src.ps1 path <Type>`; builds use
  `dotnet … -p:DisableModuleCopy=true -p:ModuleId=`, never `./build.ps1`. Explore and Plan also need
  the relevant rules in the prompt, or [agent-operating-manual.md](docs/ai-includes/agent-operating-manual.md).
- Parallel builders that may touch single-owner files get `isolation: "worktree"`; a sub-problem
  shared by two briefs gets one pinned solution. Fan out at most four wide, with one checker.
  Review ordering and spawn templates: [agent-teams.md](docs/ai-includes/agent-teams.md).
- Agents: `taleworlds-researcher`, `feature-builder`, `deep-reviewer` (the `/deep-review` lenses;
  Opus 5.5 at max effort is set in its definition, so never pass `model`), `debugger` (tooling and
  scripts; TAOM C# goes to `/investigate`), `error-detective` (several bugs, one root),
  `refactoring-specialist` (tests green before and after); tiers `fast-reader`, `implementer`,
  `architect` (below).
- In the VS Code panel, Stop and Escape end only the turn; background subagents keep running until
  stopped from the agent map. A stopped subagent's progress is on disk, not in its report.

## Model & Effort Selection

Each task runs on the cheapest model and lowest effort that does it well: the table, escalation and
the one-line note before each spawn are in `.claude/rules/model-effort-selection.md`. Tier agents:
`fast-reader` (Haiku/low), `implementer` (Sonnet/medium), `architect` (Opus/high).

## MCP and shell

- Symbols: Serena. Item, troop and culture refs: the `taom-moduledata` MCP, or
  `python tools/validate_moduledata.py`. Files across modules: Read and Grep. Issues: the `gh` CLI.
- MCP write tools bypass every PreToolUse gate (Serena's are denied): git goes through Bash, files
  through Edit and Write ([MCP servers](docs/reference/mcp-servers.md)).
- Bash heredocs mangle backslashes and quotes: write scripts with the Write tool. Never spell
  `python3` (a Store alias that hangs); use `python`. The PowerShell tool:
  [powershell-tool.md](docs/reference/powershell-tool.md).
