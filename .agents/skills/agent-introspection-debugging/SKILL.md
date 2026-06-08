---
name: agent-introspection-debugging
description: Structured 4-phase self-debug for failing agent runs (looping, drifting, burning tokens). Capture, diagnose, contained recovery, report. Complements /investigate (which is for code bugs); this skill is for harness/agent failures.
allowed-tools:
  - Bash
  - Read
  - Grep
  - Glob
  - AskUserQuestion
---

# /agent-introspection-debugging — Self-Debug for Failing Agent Runs

Adapted from [affaan-m/everything-Codex/skills/agent-introspection-debugging](https://github.com/affaan-m/everything-Codex/tree/main/skills/agent-introspection-debugging).

This is the **harness/agent failure** companion to `/investigate` (TAOM-specific code bugs). When the failing thing is the agent itself — looping, drifting, burning tokens without progress — use this skill to debug the agent before debugging the code.

## When to use

- An agent run hits its retry budget (e.g., `/build-fix` 4-attempt limit)
- A subagent (`/deep-review`, `feature-builder`, `codex-rescue`) reports failures with no useful output
- Multiple consecutive tool calls return errors with no forward progress
- Context growth or drift is degrading output quality (e.g., responses become repetitive, instructions ignored)
- File-system or environment state mismatch between expectation and reality
- Tool failures that look likely-recoverable with a smaller corrective action

## When NOT to use

- For TAOM code bugs (Harmony failures, GameModel misbehavior, etc.) → use `/investigate`
- For build errors that the retry budget hasn't yet exhausted → use `/build-fix`
- For verifying completed work → use `/deep-review`
- When you don't actually know what's failing → first surface the symptoms via plain investigation, *then* invoke this skill once you have specifics

## Four-Phase Loop

### Phase 1: Failure Capture

Before retrying anything, record the failure precisely. Most agent failures are made worse by re-running blind — the second attempt has the same context as the first plus a wasted run.

Capture:

| Item | Why |
|------|-----|
| **What was the agent attempting?** (one sentence) | Frames everything else |
| **What went wrong?** (exact error, last useful tool result, last meaningful agent output) | Distinguishes "tool error" from "model drift" |
| **What had the agent done in the N tool calls before failure?** (high level, not full transcript) | Catches context exhaustion / loop patterns |
| **What did the agent output OR plan to output that didn't make sense?** | Catches drift before re-running re-derives the same wrong plan |
| **Token usage / context %** (if visible from status line) | Distinguishes "out of room" from "stuck on logic" |

Output of Phase 1: a single paragraph plus the literal failure string. Don't skip — Phase 2 needs it.

### Phase 2: Diagnose Against Known Patterns

Match the captured failure against this checklist before forming a new hypothesis:

| Pattern | Signature | Recovery |
|---------|-----------|----------|
| **Retry-budget exhaustion** | Same error text repeating across 3+ attempts | The fix isn't in the local file — escalate to `/investigate` for root cause, or to user |
| **Stale-file mismatch** | Edit tool returns "string not found" but the string is visibly there | Re-Read the file (per `csharp-architecture.md` stale-file rule); something modified it since the last Read |
| **Context exhaustion** | Output becomes truncated, repetitive, or skips instructions | `/compact` or invoke `/context-save` then start a fresh session |
| **Tool not found / env failure** | `command not found`, `permission denied`, `path does not exist` | Per `environment-failures.md`: report, don't fix — escalate to user |
| **Subagent loop** | A spawned agent returned an output that triggers another spawn of the same agent | Stop spawning. Address whatever the agent's output asks for in the parent context. |
| **Inline-hook block** | Edit blocked by `[freeze]` deny message | The freeze boundary is doing its job; either widen scope (`/freeze` again with broader path), `/unfreeze`, or accept that this edit is out of scope |
| **MCP server unhealthy** | MCP tool calls timeout or return MCP-level errors | Check `mcp-health-check.sh` log; the hook will block calls to flagged servers for 60s. Wait or invoke a different tool. |
| **Permission prompt loop** | User keeps being asked the same permission | Add the action to `.Codex/settings.local.json` allowlist via `/update-config` |
| **Hallucinated tool / API** | Agent calls a tool name or API that doesn't exist | Tool typo OR agent confused about available tools. Stop, list available tools, invoke correctly. |
| **Plan drift** | Agent's stated plan doesn't match what it's doing | Re-state the original task explicitly; if the agent rejects it, hand control back to user |

If the failure matches a pattern, jump to Phase 3 with the corresponding recovery. If no pattern matches, proceed to Phase 3 with a fresh hypothesis.

### Phase 3: Contained Recovery

Apply the smallest corrective action. **Do not retry the original failing action with the same context** — that's the loop trap.

Bias toward:
- **Smaller scope** — if the agent failed on 5 files, retry on 1
- **Fresh context** — if context exhausted, save state then start clean (`/context-save` → new session → `/context-restore`)
- **Different tool** — if Edit failed, try Write. If Bash failed, try a smaller scoped command. If Glob failed, try Grep.
- **Human in the loop** — if 3 hypotheses fail, stop and surface to user with full Phase 1+2 output. The cost of a 30-second user clarification is much less than a fourth wrong attempt.

The recovery action MUST be something genuinely different from the original failing attempt — not a slight rewording of the same approach.

### Phase 4: Introspection Report

Whether the recovery succeeded or escalated to user, output a structured debug report:

```
AGENT INTROSPECTION REPORT
====================================
Original task:    [what the agent was trying to do]
Failure mode:     [from Phase 2's table, or "novel pattern: <description>"]
Captured signal:  [the literal error or output that surfaced the failure]
Recovery taken:   [the contained action from Phase 3]
Outcome:          RECOVERED | ESCALATED_TO_USER | STILL_BLOCKED
Cost saved:       [rough estimate: how many wrong-direction tool calls would have happened without this introspection]
Pattern to add:   [if Phase 2 had no matching pattern, propose what to add to the table]
====================================
```

If the failure mode was novel and recurring is plausible, propose adding it to:
- This SKILL.md's Phase 2 table (most patterns)
- `.Codex/rules/harness-facts.md` (if it's a load-semantic / lifecycle finding)
- `feedback_*.md` memory entry (if it's a TAOM-specific lesson)

## Pair with

- `/investigate` — for code bugs; this skill is for agent/harness bugs. The boundary is: if a fresh agent in a fresh session would hit the same problem, it's a code bug → `/investigate`. If only THIS agent's drift causes it, it's an agent failure → THIS skill.
- `/freeze` / `/unfreeze` — Phase 3 recoveries that scope-lock or release
- `/context-save` / `/context-restore` — Phase 3 recoveries for context exhaustion
- `/compact` — same
- `/codex-rescue` — when the in-session agent can't recover, hand off to Codex (out-of-band, fresh context)

## Notes

- This skill is read-only by design. It does NOT make code changes — recoveries either re-attempt with smaller scope or escalate.
- The Phase 2 table is the most important asset; extend it whenever you encounter a novel pattern. The table loses value if it becomes stale.
- Don't run this skill recursively — if introspection itself is failing, escalate immediately to the user.
