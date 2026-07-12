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
| Skill **descriptions** load eagerly at conversation start, EXCEPT when the skill has `disable-model-invocation: true` — those descriptions are NOT in context. Skill **bodies** load only when the skill is invoked, regardless of model-invocation setting. | https://code.claude.com/docs/en/skills (verified 2026-04-26) | If you're auditing context overhead, count frontmatter only for the eager total — and skip the eager charge for skills with `disable-model-invocation: true`. The pre-fix `scan.sh` got the body-counting wrong and inflated the baseline 25× for skills. |
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
| **TAOM hooks MUST fail open.** A hook's own bug must never block the user: swallow internal errors (`2>/dev/null`, `\|\| true`), and non-`deny` hooks always `exit 0`. A `deny` is only ever an *intentional* gate decision, never an accidental crash. | this repo's design (every Stop/PostToolUse hook exits 0; PreToolUse gates deny deliberately) | This is why `tools/audit_claude_config.py` (`/security-scan`) deliberately does NOT flag `\|\| true` / `2>/dev/null` / `exit 0` as exfil — they're mandated here, though upstream AgentShield flags them. Calibrate ported security rules to this. |

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

## Parallel-port build watcher (EMPIRICAL: TAOM 2026-05-06)

An external watcher auto-comments a feature's csproj includes + `SubModule.cs`/`IoC.cs` integration (`// TEMP-SMARTCAVALRY-EXCLUDE` markers) after ANY build failure mentioning it, without distinguishing which parallel port actually broke — cascading across features. **Prevention: pass `isolation: "worktree"` on parallel Agent calls that may edit single-owner files** (see the rule below). Full symptom table + integration workaround + detection signature: `docs/ai-includes/agent-teams.md` "Case studies"; RCA `docs/reviews/rca-companiontactics-2026-05-06.md` (~2 hours lost).

## Parallel builder briefs: shared sub-problems get ONE prescribed solution (EMPIRICAL: TAOM 2026-07-02, CombatMechanics)

When fanning a feature out to parallel builder agents against shared contracts, any sub-problem that appears in MORE THAN ONE brief (id normalization, NaN handling, validation invariants, hot-path allocation patterns) must be solved once in the shared contract/foundation files — never left to per-builder judgment; independently-correct builders diverge at the seams, and per-component review structurally cannot catch it.

**Pre-dispatch checklist:** (1) list sub-problems appearing in >=2 briefs; (2) pin one solution in the shared contracts or a shared helper; (3) after integration, run a cross-consistency review over the seams (data-flow + efficiency agents), not only per-file checks. The four CombatMechanics seam findings behind this rule: `docs/ai-includes/agent-teams.md` "Case studies"; RCA `docs/reviews/rca-combat-mechanics-2026-07-02.md`.

## Worktree isolation for parallel agent runs (DOC-BACKED + EMPIRICAL)

**Rule:** when spawning multiple `Agent` calls in one message that may edit overlapping single-owner files (`Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Main/IoC.cs`, `Main/SubModule.cs`, `Directory.Build.props`), pass `isolation: "worktree"` on each call — each agent gets its own git worktree on a temporary branch, so the shared tree is never touched in parallel and the build-watcher cascade cannot fire.

**When to apply:** always for parallel edits to the files above, "parallel ports"/"multiple features in flight" requests, or parallel feature scaffolding (`feature-builder`, `/new-feature`).
**When NOT needed:** read-only agents (`Explore`, research `Plan`); a single Agent call; agents on provably disjoint feature folders that don't touch csproj/IoC/SubModule (rare — audit the file set before assuming).
**After they return:** merge each worktree branch's diff back sequentially; prune stale checkouts under `.claude/worktrees/` once merged/abandoned (4 forgotten trees cost 22 GB by 2026-07-11).

Evidence table + invocation example: `docs/ai-includes/agent-teams.md` "Case studies".

## Last verified: 2026-07-12

This file is the source of truth for harness behavior in TAOM. Update the "Last verified" date and add new facts whenever a Codex review or experiment confirms something not yet captured here. Authoring-time conventions live in their scoped rules (`hook-authoring.md`, `external-skill-ports.md`); incident write-ups live in `docs/ai-includes/agent-teams.md` + the RCAs.
