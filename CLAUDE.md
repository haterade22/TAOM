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
- **On access:** a rule with `paths:` loads when a matching file in this checkout is read, written
  or edited, and drops at `/compact` until the next matching access. No rule fires outside this
  checkout. In a sibling worktree a hook names the rule files a file matches: read them when it
  does. For the live Armory, TAOM_Map and the Modding Kit, the skill for that work carries its traps.
- **On invoke:** a skill's body. After `/compact` only its first ~5,000 tokens come back.
- **Hooks** reach you as SessionStart output, a deny or block reason, or `additionalContext` beside
  a tool result (an ask reason goes to the user): follow the instruction in the message.
- The sourced facts: [harness-loading.md](.claude/rules/harness-loading.md), the
  [rules catalog](docs/reference/rules-catalog.md), the [hooks catalog](docs/reference/hooks-catalog.md).

## Where new knowledge goes

Pick the narrowest trigger that fires every time the fact is needed:

1. A machine can check it: write the gate, then one line naming it.
2. Every task, every AI client: AGENTS.md. Claude's harness only: this file.
3. In-repo files of one area: that area's `paths:` rule.
4. A procedure, or work outside the repo: that skill, near the top. A repeatable multi-step
   workflow with TAOM gotchas becomes a skill (description at most 30 words, listed below).
5. Feature or engine detail: `docs/features/`, `docs/reference/`.
6. A trap: its full text in the owning doc, plus one line in its area's trap sheet
   (`.claude/rules/traps/`); a line in the orientation.md trap index only when no sheet's paths
   reach the work.
7. A lesson: append to `docs/reviews/lessons/<category>.md`; promote a standing rule as one line
   into 3 or 4.
8. Uncommitted or unpushed work on this machine: a memory resume card with its dead ends,
   deleted once pushed.

Never put counts nothing computes, incident narratives, or copies of another tier's text into 2 or 3.
A count in a feature doc or generated header that the code can change names the command that prints
it, or gives its date.

## Skills

A SKILL.md is executable instructions: follow its phases in order. Each skill's description, listed
in every session, states its trigger and its gate: when the trigger matches, invoke the skill before
any other step. What no description carries:

- **Reflex gates:** `/investigate` on any crash, exception or "why is this broken" (never debug ad
  hoc); `/deep-review` before every commit touching C# or XML/XSLT; `/verify` before claiming done;
  `/localize` for new player-facing text.
- **Build error** (`error CS####`): fix it within the retry budget in
  `docs/ai-includes/agent-operating-manual.md`. Missing TaleWorlds type: `taom-src`. Budget spent:
  `/investigate`.
- **Authoring with no skill** (a creature or mount, a static prop, a culture, armor, weapons, lord
  skills, engine bindings after a bump, an ADR): find the procedure in
  [doc-lookup.md](docs/reference/doc-lookup.md) and follow it once told "do it", not while sketching.
- **Offer, don't auto-invoke:** `/freeze`, `/unfreeze`. **Never auto-invoke:** `/review-codex`
  (paid), `/issue` (public), `/manage` (public, starts workers).

## Subagents

- Brief every non-trivial spawn with its task and scope, and say it cannot invoke skills or spawn
  agents. `Main/IoC.cs` and `Main/SubModule.cs` are single-owner: recommend, don't edit. Signatures
  come from `pwsh tools/taom-src.ps1 path <Type>`; builds use
  `dotnet … -p:DisableModuleCopy=true -p:ModuleId=`, never `./build.ps1`. Explore and Plan also need
  the relevant rules in the prompt, or [agent-operating-manual.md](docs/ai-includes/agent-operating-manual.md).
- Fan out at most four wide, with one checker. A sibling worktree for each parallel builder that may
  touch single-owner files, one pinned solution for a sub-problem two briefs share, review ordering
  and spawn templates: [agent-teams.md](docs/ai-includes/agent-teams.md).
- In the VS Code panel, Stop and Escape end only the turn; background subagents keep running until
  stopped from the agent map. A stopped subagent's progress is on disk, not in its report.

## MCP and shell

- Symbols: Grep. Item, troop and culture refs: the `taom-moduledata` MCP, or
  `python tools/validate_moduledata.py`. Files across modules: Read and Grep. Issues: the `gh` CLI.
- MCP write tools bypass every PreToolUse gate: git goes through Bash, files
  through Edit and Write ([MCP servers](docs/reference/mcp-servers.md)).
- The Bash tool halves `\\`, and a command over 7,500 characters fails (`check-bash-length.sh`):
  write scripts with the Write tool. Never spell
  `python3` (a Store alias that hangs); use `python`. Never `sed -i` a CRLF file, and test for one
  with `file <path>`: `grep -c $'\r'` reports 0 here. The PowerShell tool:
  [powershell-tool.md](docs/reference/powershell-tool.md).
