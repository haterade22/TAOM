# Spawning parallel agents

How to spawn parallel agents on the TAOM project, and the Agent Teams feature behind them.

---

## What Are Agent Teams?

Agent Teams let a **lead session** coordinate multiple **teammate sessions** working in parallel. Each teammate is an independent Claude Code instance with its own context, tools, and permissions.

Key concepts:
- **Lead** — creates the team, spawns teammates, assigns tasks, integrates results
- **Teammates** — autonomous agents that own specific directories/tasks
- **Shared task list** — all agents read/write tasks at `~/.claude/tasks/{team-name}/`
- **Mailbox messaging** — agents communicate via `SendMessage` (DMs) or `broadcast`

Teams are enabled via the env var `CLAUDE_CODE_EXPERIMENTAL_AGENT_TEAMS=1` in `~/.claude/settings.json`.

---

## File Ownership Rules

### Directory Ownership
- Each `Main/Features/{FeatureName}/` directory belongs to **one teammate**
- `TAOM.Tests/Features/{FeatureName}/` mirrors the feature owner (or is owned by a dedicated Test Dev)
- `Main/_Module/ModuleData/` — XML/XSLT Dev owns this
- `Main/Adapters/` — coordinate through lead if a feature needs new adapters

### Single-Owner Shared Files (Lead Integrates Last)
These files are convergence points. Only the **lead** should edit them:

| File | Why |
|------|-----|
| `Main/IoC.cs` | All DI registrations converge here |
| `Main/SubModule.cs` | Entry point — thin wrapper |

Teammates report needed changes to these files via message; the lead integrates.

### Shared Working Tree
Sessions and teammates share one working tree. The git rules for that (commit before a rebase, never
pop a stash, stage explicit paths, reconstruct shared files in the index) are in
[git-and-commits.md](git-and-commits.md).

### Build Artifacts
- **Never run `./build.ps1` from two agents simultaneously** — build output goes to `out/` and will conflict
- Use `dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=` or `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` for isolated compilation during development

---

## Subagent review ordering (verify spec before quality)

When an orchestrator dispatches fresh subagents to implement and then review work — via Agent Teams, individual `Agent` calls, or a `Workflow` pipeline — apply this ordering (adapted from obra/superpowers' `subagent-driven-development`, 2026-05-29):

1. **Two-stage review — spec compliance FIRST, code quality SECOND.** Stage 1 confirms the implementation does exactly what was asked: nothing missing, nothing extra (an unrequested addition is a finding too). Stage 2 reviews quality / maintainability / ADR compliance. **Running the quality review before spec compliance is confirmed wastes the pass** — quality feedback on code that solves the wrong problem is throwaway.
2. **Give each subagent complete context in the spawn prompt; never make it re-read your session.** This is already TAOM's briefing convention (CLAUDE.md "Subagents"): the orchestrator puts task text, scope, and the docs-to-read in the prompt. A reviewer subagent gets the description + the plan/requirement + the diff (or `BASE_SHA..HEAD_SHA`), not "go figure out what changed."
3. **Fix → re-review → repeat until approved.** The implementer subagent fixes findings; the reviewer re-checks the fix. Don't skip the re-review — an unverified fix is an unverified claim (`.claude/rules/evidence-over-claims.md`), and a returned "✅ done" is a claim to verify, not evidence.
4. **Severity triage:** fix Critical/HIGH in-session (TAOM's `/deep-review` already mandates this), fix Important before proceeding, note Minor for later.

This composes with the `Workflow` tool: a `pipeline()` can implement in stage 1, run spec-compliance review in stage 2, and code-quality review in stage 3 — each item flows through without a barrier. The `parallel()` adversarial-verify pattern in the Workflow docs is the fan-out form of step 3 above.

### Workflow script gotchas

- **`parallel()` and `pipeline()` take functions, not promises.** `parallel([agent(a), agent(b)])` throws "expects an array of functions"; write `parallel([() => agent(a), () => agent(b)])`, and do the same for a `parallel` nested inside a `pipeline()` stage. The agents that already ran keep their results in the run journal (`workflowProgress[]` and the output file), so a thunk bug costs the aggregation, not the finders' work.
- **`run_in_background` is not a Workflow parameter.** The tool always runs in the background and notifies on completion; passing it is an input validation error.
- **Resume is cheap.** Edit the persisted script file (the launch result names its path) and call `Workflow({scriptPath, resumeFromRunId})`: unchanged `agent()` calls return their cached results and only the fixed stages rerun.
- **Keep the refute lens in a find-then-verify run.** One verifier to confirm and one to refute split a contested finding and filtered the false positives in the run that taught this (2026-06-25).

---

## Spawn Prompt Templates

> **Every spawn prompt must also carry the subagent briefing** (CLAUDE.md "Subagents"): the task and its scope, *"you cannot invoke skills or spawn agents; recommend them in your report"*, and `pwsh tools/taom-src.ps1 path <Type>` for signatures. Custom and general-purpose agents already load CLAUDE.md, AGENTS.md and the unscoped rules; Explore and Plan load none of them, so their prompt also starts with *"Read [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md) first"*. The templates below assume that preamble.

### Feature Developer
```
You are a Feature Developer for the TAOM Bannerlord mod.

Read these docs first:
- docs/ai-includes/architecture.md (layer architecture)
- .claude/rules/tests.md (TDD workflow; it loads when you open a test)
- docs/ai-includes/patterns.md (design patterns)

Your scope: Main/Features/{FEATURE_NAME}/ only.
Test scope: TAOM.Tests/Features/{FEATURE_NAME}/ only.

Rules:
- Follow adapter pattern (ADR-007): use ICareerHeroAdapter etc, never Hero directly
- Thin entry points (ADR-002): max 150 lines, delegate to services
- No #region, no [Obsolete], no #if outside IoC.cs
- Do NOT edit IoC.cs or SubModule.cs — report needed registrations to lead
- Lessons: {LESSONS} (the lead names the lesson headings and their files that apply to this task);
  read each before the first edit, and list in your report what you did about each one
- Docs move with the code: update the feature's docs/features/<feature-name>.md as you change it,
  and grep docs/ for every type or member you add, change, delete or rename
- A value from config: validate its range, and compute anything derived from it in long and clamp it
- A result message must be true on every branch of the action it reports; a saved marker has one
  writer that replaces the old one

Build: dotnet build TAOM.sln -p:DisableModuleCopy=true -p:ModuleId=
Test: dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=
```

---

## Briefing convention: point, never paraphrase (MANDATORY for port work)

A builder brief may cite source files and line ranges; it must NOT paraphrase what those lines do
when the behaviour is load-bearing. Builders implement briefs faithfully, so a wrong paraphrase
ships with perfect fidelity to the error. Phrase behaviour only as a refutable hypothesis: "the
orchestrator believes lines N-M do X; verify before implementing and report if wrong." Twice now a
paraphrase shipped a wrong port (Patch72's place-vs-entity mapping; the camps ambush battle-start
dropped on a misstated brief) and both were caught only by reviewers who were DENIED the narrative:
port reviews therefore give reviewers code and diff only, plus a source-fidelity dimension holding
the original sources. RCAs: `rca-herorace-patch72-2026-08-21.md`, `rca-yotthani-camps-2026-08-23.md`.

## Case studies

> Moved verbatim from `.claude/rules/harness-facts.md` 2026-08-05 (eager-context diet round 2) —
> these fire only when spawning parallel agents, so they belong here, not in the every-turn load.
> harness-facts keeps a one-row pointer. This is the section its pointers cite.


### Parallel-port build watcher (EMPIRICAL: TAOM 2026-05-06)

An external watcher auto-comments a feature's csproj includes + `SubModule.cs`/`IoC.cs` integration (`// TEMP-SMARTCAVALRY-EXCLUDE` markers) after ANY build failure mentioning it, without distinguishing which parallel port actually broke, cascading across features. **Prevention: give each parallel builder that may edit single-owner files its own worktree** (see "Worktree isolation" below). RCA `docs/reviews/rca-companiontactics-2026-05-06.md` (~2 hours lost).

### Parallel builder briefs: shared sub-problems get ONE prescribed solution (EMPIRICAL: TAOM 2026-07-02, CombatMechanics)

When fanning a feature out to parallel builder agents against shared contracts, any sub-problem that appears in MORE THAN ONE brief (id normalization, NaN handling, validation invariants, hot-path allocation patterns) must be solved once in the shared contract/foundation files — never left to per-builder judgment; independently-correct builders diverge at the seams, and per-component review structurally cannot catch it.

**Pre-dispatch checklist:** (1) list sub-problems appearing in >=2 briefs; (2) pin one solution in the shared contracts or a shared helper; (3) after integration, run a cross-consistency review over the seams (data-flow + efficiency agents), not only per-file checks. RCA `docs/reviews/rca-combat-mechanics-2026-07-02.md` (the four CombatMechanics seam findings).

**Conventions are shared sub-problems too** (second occurrence, Shadowfax Screens 2026-10-19): pin in the brief what every sibling must do the same way, not only the shared API: the failure policy for an unreadable setting or a throwing dependency, test category tags (`RequiresGame`, `RequiresGameIL`), one category constant used by module and hooks, log and detail line formats, what a timing window spans, facade and wiring-test shape. Four builders given one timer API and one settings provider still diverged on all six. RCA `docs/reviews/rca-shadowfax-screens-speed-2026-10-19.md`.

### Budget: fan-out width and checkers

Fan-out width exhausts the session limit: spawn at most four builders at once (CLAUDE.md "Subagents"). One checker over the integrated result replaces a verifier per file; per-file verifiers multiply the spend and still miss the seams between files.

### Worktree isolation for parallel agent runs

**Rule:** when several agents may edit overlapping single-owner files (`Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Main/IoC.cs`, `Main/SubModule.cs`, `Directory.Build.props`), give each parallel builder its own worktree: `git worktree add -b <branch> E:/repos/taom-<name> <commit>`. Pass the absolute path in the brief and `cd` into it in every Bash call. Put the area rules in the brief too, because path rules do not fire in a sibling worktree. The shared tree is then never touched in parallel, so the build-watcher cascade cannot fire.

**`isolation: "worktree"`:** it refused on 2026-10-31 when the session cwd spelled `e:` while git reported `E:`. A refused run can leave locked worktrees under `.claude/worktrees`, so check there after a refusal.

**When to apply:** always for parallel edits to the files above, "parallel ports"/"multiple features in flight" requests, or parallel feature scaffolding (`feature-builder`, `/new-feature`).
**When NOT needed:** read-only agents (`Explore`, research `Plan`); a single Agent call; agents on provably disjoint feature folders that don't touch csproj/IoC/SubModule (rare — audit the file set before assuming).
**After they return:** merge each worktree branch's diff back sequentially, then remove the worktrees and their branches once merged or abandoned (4 forgotten trees cost 22 GB by 2026-07-11).

---

## Related Guides

- [architecture.md](./architecture.md): Overview, client seam and folders
- [patterns.md](./patterns.md): Thread-local and cache patterns
- [taleworlds-research-guide.md](./taleworlds-research-guide.md) — Decompilation workflow
- [code-quality.md](./code-quality.md): The nullable ratchet
- [testing-guide.md](./testing-guide.md): Shipped-data tests

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)

<!-- backlinks-end -->
