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

## Git invocation forms hooks must handle

When writing a PreToolUse(Bash) hook that filters on git subcommands, enumerate explicitly which invocation forms it must catch — substring matching `*"git commit"*` MISSES the following real-world forms (Codex review 2026-04-26 found this gap):

| Form | Purpose | Handled by `*"git commit"*` substring? |
|------|---------|----------------------------------------|
| `git commit` | Bare commit | YES |
| `git commit -m "msg"` | Commit with message | YES |
| `git commit --amend` | Amend (must NOT blanket-skip — see "amend exemptions" below) | YES |
| `git commit -F file.txt` | Commit with message file | YES |
| `git -C /path commit` | Run as if from /path (no leading `cd`) | NO — needs `*"git -"*" commit"*` |
| `git -c key=val commit` | One-time config override | NO — same |
| `git --git-dir=/path commit` | Operate on a specific git-dir | NO — would need a separate pattern |
| `git commit-tree` | Plumbing — DIFFERENT command, must NOT match | YES (false positive) — needs explicit `*"git commit-"*` rejection |
| `git commit-graph` | Plumbing — same | YES (false positive) — same |

**Reference pattern** (used by `check-changelog-changed.sh`, `check-claude-files-tracked.sh`, and `suggest-compact.sh`):

```bash
case "$COMMAND" in
    *"git commit-"*) echo '{}'; exit 0 ;;       # commit-tree etc — different command
esac
case "$COMMAND" in
    *"git commit"* | *"git -"*" commit"* ) ;;   # bare or with leading flags
    *) echo '{}'; exit 0 ;;
esac
```

**MANDATORY for any new hook that detects git commits.** Codex review #29 caught `suggest-compact.sh` shipping in `79350f2` with a bare `*"git commit"*` substring matcher — the same recursion-risk class codified after review #28. The prevention rule existed but wasn't applied to its own first user.

When you write a NEW hook (or add commit detection to an existing one), grep for `git commit` substring matches in the diff before commit. If you find one that's NOT using the two-stage pattern above, that's a regression — fix before shipping. The `/skill-stocktake` checklist now includes this check.

## Amend exemptions in pre-commit hooks (recursion-risk pattern)

Do NOT blanket-skip `git commit --amend` in pre-commit hooks. `amend` is commonly used as a workflow ("oops, forgot a file, amend it in") — that's exactly the case the hook needs to catch. Codex review 2026-04-26 caught this as prevention theater: both `check-changelog-changed.sh` and `check-claude-files-tracked.sh` originally exempted `--amend`, defeating the very gates they were supposed to enforce.

Two correct patterns depending on what the hook checks:

| Hook checks | Correct amend handling |
|-------------|------------------------|
| Files in the commit's diff (e.g., is CHANGELOG.md staged?) | Compute the **post-amend file set** as `staged ∪ HEAD` and apply the same gate. If CHANGELOG was already in HEAD's diff, it's still in the post-amend commit — the gate correctly allows. |
| Working-tree state (e.g., is a file gitignored?) | Don't exempt amend at all. Working-tree state is amend-independent — a gitignored file on disk is just as broken in an amended commit as in a fresh one. |

## Gitignore blast radius

| Fact | Source | Why we care |
|------|--------|-------------|
| `git check-ignore -v <path>` is the authoritative check for "is this file gitignored". Reading `.gitignore` and grepping is unreliable (multiple files, negation rules, parent dir patterns). | git docs + 2026-04-26 deep-review | The pre-fix `check-freeze.sh` was excluded by `.gitignore`'s `bin/` line (intended for `Main/bin/` .NET output) and shipped as a non-functional skill. Always run `git check-ignore` against any new file under `.claude/` before assuming it'll commit. |
| Generic patterns in `.gitignore` (`bin/`, `obj/`, `*.cache`, `tmp/`, `node_modules/`) match anywhere in the tree, not just at the repo root. | git docs | When introducing a new directory under `.claude/`, prefer descriptive names (`scripts/`, `state/`) over generic ones (`bin/`, `tmp/`, `cache/`). |

## What this rule changes about how you work

When you write or modify any skill, agent, rule, or hook in `.claude/`:

1. **If the change relies on Claude Code load behavior** (eager vs lazy, hook lifecycle, rule loader scoping, frontmatter consumption) — verify against this file's facts. If this file disagrees with what you intended, update this file FIRST (with a doc citation) before changing the harness.
2. **If you're porting a skill from an external suite** (gstack, everything-claude-code, etc.) — see `.claude/rules/external-skill-ports.md` for the per-field validation checklist.
3. **If you're committing changes touching `.claude/`** — the pre-commit hook `check-changelog-changed.sh` will **hard-block** the commit if CHANGELOG.md isn't in the post-commit file set (staged for new commits, staged + HEAD for amends). The hook `check-claude-files-tracked.sh` will **hard-block** if any file under `.claude/{skills,agents,rules,hooks}/` exists on disk but is gitignored or untracked. Both hooks fire on amends too — there is no blanket `--amend` exemption (a Codex review on 2026-04-26 caught this as a recursion-risk; amend is commonly used as "oops forgot a file" workflow, exactly the case the gate must catch). NOTE: these hooks fire only when Claude Code invokes Bash via the tool dispatch — they do NOT fire when a user types `git commit` directly in a shell outside Claude. They are prevention for Claude-driven commits, not a global git pre-commit hook.

4. **When running `/review-codex` or any review skill** — Phase 3e (Root Cause Analysis) applies to **EVERY confirmed bug**, not just HIGH ones. Conflating severity with importance for RCA means we patch LOW symptoms but never extract the systemic lesson — and the same category of bug ships again in the next commit. The skill's literal text is: *"Do NOT skip this step. The point is not just to fix bugs — it's to make the same category of bug impossible in future features."* Review #28 caught us shortcutting this — we ran RCA only for the HIGH+MED bypass, not for the 4 LOWs and 2 MEDs that also had real "why missed" stories.

5. **When writing facts in this file** (or any rule that asserts behavior) — every fact must explicitly cite either a doc URL (DOC-BACKED) or an observation context (EMPIRICAL: where, when, by whom). Vague "verified" claims without source attribution age into wrong assumptions. Example: the project-slug derivation rule was originally presented as fact; Codex caught that the Claude Code memory docs only say `<project>` "is derived from the git repository" — the exact format is empirical-on-Windows, not doc-backed.

## Parallel-port build watcher (EMPIRICAL: TAOM 2026-05-06, CompanionTactics port session)

When multiple feature ports run simultaneously in the TAOM working tree, an external watcher monitors build output and **auto-edits source files in response to build failures**. Symptoms confirmed during the CompanionTactics port:

| What gets re-added | Where | When |
|---|---|---|
| `<Compile Remove="Features\<Feature>\**\*.cs" />` | `Main/TAOM.csproj` and `TAOM.Tests/TAOM.Tests.csproj` | After ANY build failure that mentions the feature |
| Comments around `using TAOM.Features.<Feature>.*;` directives | `Main/SubModule.cs`, `Main/IoC.cs` | Same trigger |
| Comments around integration calls (`AddBehavior`, `AddMissionBehavior`, `_harmony.Patch(AccessTools.Method(...))`) | `Main/SubModule.cs` | Same trigger |
| Banner comment `// TEMP-SMARTCAVALRY-EXCLUDE: <error category>` | All of the above | Same trigger |

**Key implication:** the watcher does NOT differentiate which feature actually had the error. A build failure caused by FiefManagement (or any other parallel port in flight) can trigger exclusion of CompanionTactics if both feature names appear in the build output. The cascade is destructive: excluding feature A causes its types to vanish from the namespace, which causes references in feature B to fail-compile, which triggers the watcher to exclude B too, and so on.

**Workaround during integration:**
1. Run `git status` BEFORE making single-owner-file edits — note which other parallel ports have unstaged changes.
2. Make all source-file edits to YOUR feature first; ensure it compiles cleanly in isolation.
3. Reserve csproj + `SubModule.cs` + `IoC.cs` edits for a SINGLE atomic batch.
4. Run the build IMMEDIATELY in the same response (`dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true --verbosity quiet`).
5. If the watcher re-comments after one cycle, find ALL the cumulative comment markers (grep for `TEMP-SMARTCAVALRY-EXCLUDE`) and uncomment them in one Edit, then build IMMEDIATELY again.
6. If the watcher still wins after 2 attempts, check whether other parallel-port features have errors that are causing the cascade — fix those too, OR coordinate with the user to pause those ports.
7. If you can't get an atomic build pass, fully-qualify your integration call sites (e.g., `new Features.CompanionTactics.FormationPresets.Hooks.FormationPresetCampaignBehavior(...)`) so the using-comment cycle doesn't break compilation. **Caveat:** this only helps if the namespace itself is included in the compile (i.e., the csproj exclusion is OFF) — if the watcher also excludes the source files, FQN resolution still fails.

**Detection signature:** if you see comments matching `// TEMP-SMARTCAVALRY-EXCLUDE: <feature> parallel-port has compile errors; restore when ready.` adjacent to a using directive or integration call, the watcher has run on your feature.

**Don't:** chase the watcher with iterative re-Edits — each cycle costs ~30s and burns context. Either close the build cleanly in ONE atomic batch, or accept that the feature ships with auto-commented integration and document the manual restoration in the feature doc + CHANGELOG.

**RCA reference:** `docs/reviews/rca-companiontactics-2026-05-06.md` documents the full session lost to this watcher (~2 hours).

## Last verified: 2026-05-06

This file is the source of truth for harness behavior in TAOM. Update the "Last verified" date and add new facts whenever a Codex review or experiment confirms something not yet captured here.
