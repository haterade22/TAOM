---
description: Verified Claude Code load semantics, hook lifecycle, and frontmatter schema. Pinned source-of-truth so future skill/rule/agent edits don't recreate already-fixed bugs.
---

<!-- NO paths: intentionally — always-load. Every fact cites a doc URL or an empirical context;
     if Claude Code changes, update THIS file first — never let other harness files drift ahead. -->

# Claude Code Harness Facts (verified)

## Skill load semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Skill **descriptions** load eagerly at conversation start, EXCEPT when the skill has `disable-model-invocation: true` — those descriptions are NOT in context. Skill **bodies** load only when the skill is invoked, regardless of model-invocation setting. | https://code.claude.com/docs/en/skills (verified 2026-04-26) | If you're auditing context overhead, count frontmatter only for the eager total — and skip the eager charge for skills with `disable-model-invocation: true`. The pre-fix `scan.sh` got the body-counting wrong and inflated the baseline 25× for skills. |
| Frontmatter fields documented as consumed (verified 2026-07-18): `name`, `description`, `when_to_use`, `argument-hint`, `arguments`, `disable-model-invocation`, `user-invocable`, `allowed-tools`, `disallowed-tools`, `model`, `effort`, `context` (=`fork`), `agent`, `hooks`, `paths`, `shell`. | https://code.claude.com/docs/en/skills (verified 2026-07-18) | `triggers:` is still NOT documented (gstack preamble field — dead weight; move phrases into `description`/`when_to_use`). `effort`, `context: fork`, `disallowed-tools`, `paths`, `model`, `arguments`, `user-invocable`, `shell` were added to the platform after the 2026-04-26 baseline — the old list omitted them, so treat them as valid, not "undocumented." |
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
| **TAOM hooks MUST fail open.** A hook's own bug must never block the user: swallow internal errors (`2>/dev/null`, `\|\| true`), and non-`deny` hooks always `exit 0`. A `deny` is only ever an *intentional* gate decision, never an accidental crash. | this repo's design (every Stop/PostToolUse hook exits 0; PreToolUse gates deny deliberately) | This is why `tools/audit_claude_config.py` (`/security-scan`) deliberately does NOT flag `\|\| true` / `2>/dev/null` / `exit 0` as exfil — they're mandated here, though upstream AgentShield flags them. Calibrate ported security rules to this. |

### Hook events + handler contract (verified 2026-07-18, https://code.claude.com/docs/en/hooks)

**30 hook events exist — TAOM uses 8.** Do NOT flag any of the following as "undocumented" (a 2026-07-18 audit wrongly did for `PostToolUseFailure` and `SubagentStart` — both are real and firing; `.claude/logs/agent-audit.log` has live `SubagentStart` entries):

`SessionStart` · `Setup` · `UserPromptSubmit` · `UserPromptExpansion` · `PreToolUse` · `PermissionRequest` · `PermissionDenied` · `PostToolUse` · `PostToolUseFailure` · `PostToolBatch` · `Notification` · `MessageDisplay` · `SubagentStart` · `SubagentStop` · `TaskCreated` · `TaskCompleted` · `Stop` · `StopFailure` · `TeammateIdle` · `InstructionsLoaded` · `ConfigChange` · `CwdChanged` · `FileChanged` · `WorktreeCreate` · `WorktreeRemove` · `Elicitation` · `ElicitationResult` · `PreCompact` · `PostCompact` · `SessionEnd`

**Handler-object fields** (beyond `matcher` / `command`): `type` (`command` | `http` | `mcp_tool` | `prompt` | `agent`), `if` (a permission-rule string like `"Bash(git commit*)"` that gates the handler at the harness level — so a `matcher: "Bash"` gate need not re-parse the command in-script to skip non-matches), `timeout`, `statusMessage` (spinner label while the hook runs), `once` (run once per session), and for command hooks `args` / `async` (background, non-blocking) / `asyncRewake` (background, wakes Claude on exit 2) / `shell` (`bash` | `powershell`).

### Timeouts and concurrency (DOC-BACKED, https://code.claude.com/docs/en/hooks, verified 2026-08-31)

These four facts are the ones the 2026-08-31 outage turned on, and none of them was written down here beforehand.

| Fact | Why it matters |
|---|---|
| **All hooks matching an event run in PARALLEL.** | Wall-clock for an event is the MAX of its hooks, not the sum. Nine hooks at `timeout: 5` cost up to 5s per tool call, not 45s. |
| **An omitted `timeout` defaults to 600 SECONDS** for command hooks (exceptions: `UserPromptSubmit` / `PreModelSwitch` / `PostModelSwitch` 30s, `MessageDisplay` 10s, `SessionEnd` a 1.5s shared budget). | This is the number that produced the 20.0-minute stalls: a wedged Bash call paid one 600s PreToolUse batch plus one 600s PostToolUse batch = 1200s. The first write-up assumed 60s and got near-enough the right answer for the wrong reason. **Every registration must carry an explicit `timeout`.** |
| **A timed-out hook is KILLED and its output DISCARDED.** On `PreToolUse` the tool then proceeds through the normal permission flow (fail-open); no user-visible signal is documented. | So for a gate, "killed" and "passed cleanly" are the same observable event. A registered timeout is therefore a KILL, never a budget: bound slow work *inside* the script under the registered value, where an overrun can still speak. See `hook-authoring.md` "A timeout must be measured against the SLOW path". |
| **Async hooks (`async: true`) are exempt from timeout enforcement entirely.** | Do not reach for `async` to dodge a timeout on a gate: a non-blocking hook cannot return a `permissionDecision`, so it cannot gate anything. |

**NOT documented, do not assume either way:** whether the `env` block in `settings.json` reaches command-hook processes. The docs say only that hooks inherit the parent environment (minus `OTEL_*`). TAOM sets `TAOM_PYBIN` there and `_pybin.sh` validates and probes it rather than trusting it, so the pin is an optimisation that degrades to discovery if it never arrives. There is also **no documented `timeout` knob for `statusLine`**, which is why `.claude/statusline.sh` must stay cheap by construction (it was 457ms per repaint until 2026-08-31; now ~119ms).

**Event-placement facts we act on:** a pure per-session logger belongs on `SessionEnd`, NOT `Stop` — `Stop` fires every turn (that is what let `session-log.md` reach 137 KB with a 2.8 MB rotated `.1` before the 2026-07-18 move). Per-turn *reminders* (changelog/deep-review/verification-evidence) correctly stay on `Stop`.

**The `if:` migration is DEFERRED by decision.** TAOM's 8 PreToolUse Bash gates still use bare `matcher: "Bash"` + the in-script two-stage git-commit matcher (below). `if:` supersedes that hand-rolled matcher, but a mis-scoped `if:` silently disables a load-bearing gate, so the migration needs a prove-each-gate pass, not a bulk flip. `WorktreeCreate` / `WorktreeRemove` are wired nowhere: `WorktreeCreate`'s command hook is expected to PRINT the worktree path on stdout, so a passive logger there could redirect creation — surface stale worktrees read-only from `session-start.sh` instead (done 2026-07-18).

## Rule loader (memory) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| Rules WITHOUT a `paths:` field load at conversation start (always-on). | https://code.claude.com/docs/en/memory (verified 2026-04-26) | This is how `harness-facts.md`, `environment-failures.md`, and `csharp-architecture.md` etc. behave. |
| Rules WITH any `paths:` field (any glob, including `paths: ["**/*"]`) load **conditionally** — only when a file matching the glob is opened. | docs above (verified 2026-04-26) | `paths: ["**/*"]` is NOT a synonym for "always-load". To make a rule unconditional, omit `paths:` entirely. The pre-fix `environment-failures.md` had this wrong. |

## Memory file (MEMORY.md) semantics

| Fact | Source | Why we care |
|------|--------|-------------|
| MEMORY.md is loaded at the start of every conversation. Cap is whichever binds first: first ~200 lines OR first ~25KB. | https://code.claude.com/docs/en/memory (verified 2026-04-26) | Counts toward the eager startup baseline. `scan_memory()` should enforce both caps in the token estimate. |
| MEMORY.md lives at `~/.claude/projects/<project-slug>/memory/MEMORY.md`. The Claude Code memory docs only say `<project>` "is derived from the git repository". The exact derivation (drive letter lowercased + `--` + path with `/` and `\` replaced by `-`) is **empirical, not doc-backed** — observed on Windows + Git Bash on 2026-04-26. The format may differ on other platforms or change in future Claude Code versions. | https://code.claude.com/docs/en/memory + empirical | When auditing memory across projects, derive the candidate slug from `cygpath -w "$REPO_ROOT"` then transform — and fall back to substring matching if the derived slug doesn't match an actual directory. Substring matching alone on basename is ambiguous when multiple project slugs share a substring (TAOM, TAOM-Online, taommod), so prefer derived-then-fallback over fallback-only. |
| `autoMemoryDirectory` (settings.json) accepts ONLY an absolute path or a `~/`-prefixed path; from project/local settings it's honored only after the workspace-trust dialog. A RELATIVE value (e.g. `.claude/memory`) is silently ignored — the harness falls back to the default `~/.claude/projects/<slug>/memory/`. | https://code.claude.com/docs/en/settings + https://code.claude.com/docs/en/memory#storage-location (verified 2026-08-05) | TAOM shipped `"autoMemoryDirectory": ".claude/memory"` — silently ignored, so the tracked `.claude/memory/` copy went stale (March) while the live memory accrued at the default path, and `post-compact.sh` pointed rehydration at the stale copy. Key removed + hook repointed + tracked copy deleted 2026-08-05 (all six stale facts verified present in live memory / repo rules first). |

## Hook authoring conventions -> `.claude/rules/hook-authoring.md`

Writing or modifying a `.claude/hooks/` script? The authoring conventions — mirror-the-sibling's-FULL-convention-set, the two-stage git-commit matcher (handles `git -C ... commit`, rejects `commit-tree`), amend-exemption patterns, and log-rotation for appending hooks — live in the paths-scoped rule `.claude/rules/hook-authoring.md`, which loads automatically when a hook file is opened. The durable lifecycle facts (fail-open mandate, JSON output contract, settings-vs-frontmatter scoping) stay in the tables above.

## Gitignore blast radius

| Fact | Source | Why we care |
|------|--------|-------------|
| `git check-ignore -v <path>` is the authoritative check for "is this file gitignored". Reading `.gitignore` and grepping is unreliable (multiple files, negation rules, parent dir patterns). | git docs + 2026-04-26 deep-review | The pre-fix `check-freeze.sh` was excluded by `.gitignore`'s `bin/` line (intended for `Main/bin/` .NET output) and shipped as a non-functional skill. Always run `git check-ignore` against any new file under `.claude/` before assuming it'll commit. |
| Generic patterns in `.gitignore` (`bin/`, `obj/`, `*.cache`, `tmp/`, `node_modules/`) match anywhere in the tree, not just at the repo root. | git docs | When introducing a new directory under `.claude/`, prefer descriptive names (`scripts/`, `state/`) over generic ones (`bin/`, `tmp/`, `cache/`). |

## What this rule changes about how you work

When you write or modify any skill, agent, rule, or hook in `.claude/`:

1. **If the change relies on Claude Code load behavior** (eager vs lazy, hook lifecycle, rule loader scoping, frontmatter consumption) — verify against this file's facts. If this file disagrees with what you intended, update this file FIRST (with a doc citation) before changing the harness.
2. **If you're porting a skill from an external suite** (gstack, everything-claude-code, etc.) — see `.claude/rules/external-skill-ports.md` for the per-field validation checklist, which now begins with a security-vet of the foreign tree (`python tools/audit_claude_config.py --root <dir> --external`, SkillSpector-derived threat categories at full severity).
3. **If you're committing changes touching `.claude/`** — the pre-commit hook `check-changelog-changed.sh` will **hard-block** the commit if CHANGELOG.md isn't in the post-commit file set (staged for new commits, staged + HEAD for amends). The hook `check-claude-files-tracked.sh` will **hard-block** if any file under `.claude/{skills,agents,rules,hooks}/` exists on disk but is gitignored or untracked. Both hooks fire on amends too — there is no blanket `--amend` exemption (a Codex review on 2026-04-26 caught this as a recursion-risk; amend is commonly used as "oops forgot a file" workflow, exactly the case the gate must catch). NOTE: these hooks fire only when Claude Code invokes Bash via the tool dispatch — they do NOT fire when a user types `git commit` directly in a shell outside Claude. They are prevention for Claude-driven commits, not a global git pre-commit hook.

4. **When running `/review-codex` or any review skill** — Phase 3e (Root Cause Analysis) applies to **EVERY confirmed bug**, not just HIGH ones. Conflating severity with importance for RCA means we patch LOW symptoms but never extract the systemic lesson — and the same category of bug ships again in the next commit. The skill's literal text is: *"Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features."* Review #28 caught us shortcutting this — we ran RCA only for the HIGH+MED bypass, not for the 4 LOWs and 2 MEDs that also had real "why missed" stories.

5. **When writing facts in this file** (or any rule that asserts behavior) — every fact must explicitly cite either a doc URL (DOC-BACKED) or an observation context (EMPIRICAL: where, when, by whom). Vague "verified" claims without source attribution age into wrong assumptions. Example: the project-slug derivation rule was originally presented as fact; Codex caught that the Claude Code memory docs only say `<project>` "is derived from the git repository" — the exact format is empirical-on-Windows, not doc-backed.

## Parallel-agent case studies → `docs/ai-includes/agent-teams.md` "Case studies"

The build-watcher cascade (2026-05-06), the parallel-builder-brief seam rule (2026-07-02,
CombatMechanics), and worktree isolation for parallel agent runs moved there 2026-08-05 — they
fire only when spawning parallel agents, not on every turn. The operative one-liner stays here:
**parallel Agent calls that may edit single-owner files (csproj / `IoC.cs` / `SubModule.cs` /
`Directory.Build.props`) pass `isolation: "worktree"`, and any sub-problem appearing in >=2
builder briefs gets ONE pinned solution in the shared contracts.**

## Last verified: 2026-08-05

This file is the source of truth for harness behavior in TAOM. Update the "Last verified" date and add new facts whenever a Codex review or experiment confirms something not yet captured here. Authoring-time conventions live in their scoped rules (`hook-authoring.md`, `external-skill-ports.md`); incident write-ups live in `docs/ai-includes/agent-teams.md` + the RCAs.
