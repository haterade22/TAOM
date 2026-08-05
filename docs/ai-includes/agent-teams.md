# Agent Teams Guide

How to use Claude Code Agent Teams for parallel work on the TAOM project.

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

## TAOM Team Compositions

TAOM is a single Bannerlord mod, so team compositions are based on **directory ownership** within the mod, not separate components.

| Role | Owns | Build/Verify Command |
|------|------|---------------------|
| Feature Dev | `Main/Features/{Name}/` | `dotnet build Main` |
| Test Dev | `TAOM.Tests/` | `dotnet test TAOM.Tests` |
| XML/XSLT Dev | `Main/_Module/ModuleData/` | N/A (validated at runtime) |
| TaleWorlds Researcher | Read-only (decompile DLLs) | `pwsh tools/taom-src.ps1 path <Type>` (ilspycmd fallback) |
| Reviewer | Read-only | N/A |

Typical team size: **2-3 teammates** plus the lead. More than 4 rarely helps for a single-mod project.

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

### Build Artifacts
- **Never run `./build.ps1` from two agents simultaneously** — build output goes to `out/` and will conflict
- Use `dotnet build Main` or `dotnet test TAOM.Tests` for isolated compilation during development

---

## Best Use Cases

### 1. New Feature + Tests in Parallel

**Scenario:** Adding a new feature like `PartySpeed` or `CombatBonus`.

| Agent | Task |
|-------|------|
| Feature Dev | Implements `Main/Features/NewFeature/` (hooks, services, engines) |
| Test Dev | Writes tests in `TAOM.Tests/Features/NewFeature/` |
| Lead | Coordinates, integrates `IoC.cs` registrations, runs final build |

**Spawn prompt (Feature Dev):**
```
You are a Feature Developer for the TAOM Bannerlord mod. Read docs/ai-includes/architecture.md
and docs/ai-includes/tdd-enforcement.md first. Your scope is Main/Features/PartySpeed/ only.
Follow the adapter pattern (ADR-007). Do NOT edit IoC.cs or SubModule.cs — report needed
registrations to the lead via message. Build with: dotnet build Main
```

**Spawn prompt (Test Dev):**
```
You are a Test Developer for the TAOM Bannerlord mod. Read docs/ai-includes/tdd-enforcement.md
and docs/ai-includes/testing-guide.md first. Your scope is TAOM.Tests/Features/PartySpeed/ only.
Use MSTest + NSubstitute. Mock all adapters. 100% service/engine coverage required (ADR-008).
Run tests with: dotnet test TAOM.Tests
```

### 2. Research + Implementation

**Scenario:** Implementing a feature that depends on undocumented TaleWorlds behavior.

| Agent | Task |
|-------|------|
| Researcher | Decompiles TaleWorlds DLLs, documents findings, reports via message |
| Feature Dev | Waits for research, then implements against verified APIs |

**Spawn prompt (Researcher):**
```
You are a TaleWorlds Researcher for the TAOM Bannerlord mod. Read
docs/ai-includes/agent-operating-manual.md + taleworlds-research-guide.md first; you cannot
invoke skills (recommend them instead). Use `pwsh tools/taom-src.ps1 path <Type>` (primary;
ilspycmd fallback) to decompile the v1.4.5 DLLs. Your job is to:
1. Decompile the requested classes
2. Document method signatures, null handling, event timing
3. Report findings to the lead via message
Do NOT write any mod code. Read-only research only.
```

### 3. C# + XML/XSLT in Parallel

**Scenario:** Adding a new game mechanic that needs both C# service code and XML configuration/XSLT transforms.

| Agent | Task |
|-------|------|
| Feature Dev | Implements C# services in `Main/Features/` |
| XML Dev | Creates/modifies XSLT in `Main/_Module/ModuleData/` |

**Spawn prompt (XML/XSLT Dev):**
```
You are an XML/XSLT Developer for the TAOM Bannerlord mod. Your scope is
Main/_Module/ModuleData/ only. You work on XSLT transformations (spkingdoms.xslt,
spcultures.xslt, etc.) and XML configuration files. Do NOT edit C# files.
```

### 4. Multi-Aspect Code Review

**Scenario:** Reviewing a large PR or feature for quality.

| Agent | Task |
|-------|------|
| Architecture Reviewer | Checks ADR compliance, layer violations, adapter pattern |
| Test Reviewer | Checks test coverage, test quality, mocking patterns |
| Security Reviewer | Checks OWASP top 10, input validation, injection risks |

All reviewers are **read-only** — they report findings to the lead via message.

### 5. Parallel Independent Features

**Scenario:** Two unrelated features being developed simultaneously.

| Agent | Task |
|-------|------|
| Feature Dev A | `Main/Features/PartySpeed/` + `TAOM.Tests/Features/PartySpeed/` |
| Feature Dev B | `Main/Features/CombatBonus/` + `TAOM.Tests/Features/CombatBonus/` |
| Lead | Coordinates, integrates IoC.cs, runs final build |

---

## Subagent review ordering (verify spec before quality)

When an orchestrator dispatches fresh subagents to implement and then review work — via Agent Teams, individual `Agent` calls, or a `Workflow` pipeline — apply this ordering (adapted from obra/superpowers' `subagent-driven-development`, 2026-05-29):

1. **Two-stage review — spec compliance FIRST, code quality SECOND.** Stage 1 confirms the implementation does exactly what was asked: nothing missing, nothing extra (an unrequested addition is a finding too). Stage 2 reviews quality / maintainability / ADR compliance. **Running the quality review before spec compliance is confirmed wastes the pass** — quality feedback on code that solves the wrong problem is throwaway.
2. **Give each subagent complete context in the spawn prompt — never make it re-read your session.** This is already TAOM's "Briefing subagents" convention (CLAUDE.md): the orchestrator puts task text, scope, and the docs-to-read in the prompt. A reviewer subagent gets the description + the plan/requirement + the diff (or `BASE_SHA..HEAD_SHA`), not "go figure out what changed."
3. **Fix → re-review → repeat until approved.** The implementer subagent fixes findings; the reviewer re-checks the fix. Don't skip the re-review — an unverified fix is an unverified claim (`.claude/rules/evidence-over-claims.md`), and a returned "✅ done" is a claim to verify, not evidence.
4. **Severity triage:** fix Critical/HIGH in-session (TAOM's `/deep-review` already mandates this), fix Important before proceeding, note Minor for later.

This composes with the `Workflow` tool: a `pipeline()` can implement in stage 1, run spec-compliance review in stage 2, and code-quality review in stage 3 — each item flows through without a barrier. The `parallel()` adversarial-verify pattern in the Workflow docs is the fan-out form of step 3 above.

---

## When NOT to Use Agent Teams

| Scenario | Use Team? | Why |
|----------|-----------|-----|
| Single-file bug fix | No | Overhead exceeds benefit |
| Sequential A-then-B task | No | No parallelism opportunity |
| Multiple agents editing same file | No | Merge conflicts |
| Quick XML tweak | No | Trivial change |
| Single feature + its tests | Maybe | Only if feature and tests can truly proceed in parallel |
| Two independent features + tests | **Yes** | Natural parallelism |
| Research-heavy feature implementation | **Yes** | Researcher + implementer pattern |

**Rule of thumb:** If the work fits in one directory and takes less than ~15 minutes of agent time, just do it in the main session.

---

## Integration with TAOM Rules

All CLAUDE.md Critical Rules apply to **every teammate equally**:

| Rule | Team Impact |
|------|-------------|
| **TDD Mandatory** | Both Feature Dev and Test Dev follow RED-GREEN-REFACTOR. Coordinate who writes the failing test first. |
| **Adapter Pattern** | Feature Dev creating new adapters must coordinate with lead before modifying `Main/Adapters/` |
| **Research First** | Researcher agent uses `ilspycmd` and reports findings via message; implementer waits before coding against unverified APIs |
| **No #region / [Obsolete] / #if DEBUG** | Every teammate follows these — no exceptions |
| **Thin Entry Points** | Patches stay under 150 lines — teammate creating patches must self-enforce |

---

## Task Sizing Guidelines

| Size | Example | Verdict |
|------|---------|---------|
| Too small | "Rename a variable", "Fix a typo" | Just do it in main session |
| Good (15-60 min) | "Implement PartySpeed service + tests" | One teammate task |
| Good | "Research how TaleWorlds handles X" | One researcher task |
| Too large | "Implement entire race system" | Break into 3-6 sub-tasks |

Target: **3-6 tasks per teammate per session**. Fewer means the tasks are too large; more means they should be consolidated.

---

## Spawn Prompt Templates

> **Every spawn prompt must also carry the subagent briefing** (CLAUDE.md "Briefing subagents"): start with *"Read [docs/ai-includes/agent-operating-manual.md](./agent-operating-manual.md) first; you cannot invoke skills or spawn agents — recommend them in your report"*, and use `pwsh tools/taom-src.ps1 path <Type>` for signatures. The templates below assume that preamble.

### Feature Developer
```
You are a Feature Developer for the TAOM Bannerlord mod.

Read these docs first:
- docs/ai-includes/architecture.md (layer architecture)
- docs/ai-includes/tdd-enforcement.md (TDD workflow)
- docs/ai-includes/patterns.md (design patterns)

Your scope: Main/Features/{FEATURE_NAME}/ only.
Test scope: TAOM.Tests/Features/{FEATURE_NAME}/ only.

Rules:
- Follow adapter pattern (ADR-007): use IHeroAdapter etc, never Hero directly
- Thin entry points (ADR-002): max 150 lines, delegate to services
- No #region, no [Obsolete], no #if DEBUG
- Do NOT edit IoC.cs or SubModule.cs — report needed registrations to lead

Build: dotnet build Main
Test: dotnet test TAOM.Tests
```

### TaleWorlds Researcher
```
You are a TaleWorlds Researcher for the TAOM Bannerlord mod.

Read docs/ai-includes/agent-operating-manual.md AND docs/ai-includes/taleworlds-research-guide.md first.
You cannot invoke skills or spawn agents — recommend them in your report.

Your job: decompile TaleWorlds v1.4.5 DLLs and document findings.
Primary tool: pwsh tools/taom-src.ps1 path <FullTypeName>   (cache-aware; prints a .cs path to grep)
Fallback: ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\{DLL}" -t "Namespace.Class"

Key DLLs:
- TaleWorlds.CampaignSystem.dll (campaign logic, diplomacy)
- TaleWorlds.CampaignSystem.ViewModelCollection.dll (UI ViewModels)
- TaleWorlds.Core.dll (core game types)
- TaleWorlds.MountAndBlade.dll (battle/mission logic)

Report findings to lead via message. Include:
- Method signatures
- Null handling (TextObject.Empty vs null?)
- Event timing (when fired vs when state changes)
- Collection safety (ToList() copies needed?)

Do NOT write mod code. Read-only research only.
```

### Code Reviewer
```
You are a Code Reviewer for the TAOM Bannerlord mod.

Read these docs first:
- docs/ai-includes/architecture.md
- docs/ai-includes/code-quality.md
- docs/adrs/README.md

Review scope: {FILES_OR_DIRECTORIES}

Check for:
- ADR compliance (thin entry points, adapter pattern, no #region, etc.)
- Test coverage (100% for services/engines per ADR-008)
- Security (OWASP top 10, no command injection, no XSS)
- Architecture violations (sealed types leaking through interfaces)

Report findings to lead via message. You are read-only — do NOT edit files.
```

---

## Windows-Specific Notes

- **In-process mode** is the default on Windows (tmux/iTerm2 split-pane mode is not available)
- To be explicit, use CLI flag: `claude --teammate-mode in-process`
- **Keyboard shortcuts:**
  - `Shift+Up/Down` — cycle through teammate views
  - `Enter` — view a teammate's full session
  - `Escape` — interrupt a teammate's current turn
  - `Ctrl+T` — toggle the shared task list
  - `Shift+Tab` — cycle focus between teammates

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| **Build contention** | Only one agent runs `./build.ps1` at a time. Use `dotnet build Main` or `dotnet test TAOM.Tests` for isolated compilation. |
| **IoC.cs conflicts** | Lead owns this file. Teammates report needed registrations via message to lead. |
| **Adapter file conflicts** | Coordinate new adapter creation through lead. Only one agent adds to `Main/Adapters/` at a time. |
| **Permission prompts** | Each teammate gets its own permission prompts in its own context. |
| **Teammate appears stuck** | Send a message to the teammate. Idle status is normal — it means they're waiting for input. |
| **Which agents were used?** | Check `.claude/logs/agent-audit.log` — the `log-agent.sh` SubagentStart hook logs every agent invocation with timestamp and type. |
| **SubModule.cs conflicts** | Lead owns this file. If a feature needs new behavior registrations, report to lead. |

---

## Limitations

- **No session resumption** — if a teammate crashes, its context is lost (the `pre-compact.sh` hook helps the lead session recover by dumping modified file lists before context compaction)
- **No file locking** — agents can overwrite each other's changes if ownership isn't respected
- **No nested teams** — a teammate cannot spawn its own team
- **One team per session** — the lead manages one team at a time
- **Cost scales linearly** — each teammate uses its own context window and API tokens
- **Experimental feature** — behavior may change between Claude Code versions

---

## Cost Considerations

Agent teams multiply token usage. Use this decision framework:

| Question | If Yes | If No |
|----------|--------|-------|
| Can the work be parallelized? | Consider teams | Don't use teams |
| Will it save significant wall-clock time? | Worth the cost | Probably not |
| Are there 2+ truly independent work streams? | Good candidate | Do sequentially |
| Is the task complex enough to justify setup? | Use teams | Main session is fine |

**General guideline:** If you'd otherwise spend 30+ minutes on sequential work that has 2+ independent tracks, teams are worth the cost.

---

## Case studies

> Moved verbatim from `.claude/rules/harness-facts.md` 2026-08-05 (eager-context diet round 2) —
> these fire only when spawning parallel agents, so they belong here, not in the every-turn load.
> harness-facts keeps a one-row pointer. This is the section its pointers cite.

### Parallel-port build watcher (EMPIRICAL: TAOM 2026-05-06)

An external watcher auto-comments a feature's csproj includes + `SubModule.cs`/`IoC.cs` integration (`// TEMP-SMARTCAVALRY-EXCLUDE` markers) after ANY build failure mentioning it, without distinguishing which parallel port actually broke — cascading across features. **Prevention: pass `isolation: "worktree"` on parallel Agent calls that may edit single-owner files** (see "Worktree isolation" below). RCA `docs/reviews/rca-companiontactics-2026-05-06.md` (~2 hours lost).

### Parallel builder briefs: shared sub-problems get ONE prescribed solution (EMPIRICAL: TAOM 2026-07-02, CombatMechanics)

When fanning a feature out to parallel builder agents against shared contracts, any sub-problem that appears in MORE THAN ONE brief (id normalization, NaN handling, validation invariants, hot-path allocation patterns) must be solved once in the shared contract/foundation files — never left to per-builder judgment; independently-correct builders diverge at the seams, and per-component review structurally cannot catch it.

**Pre-dispatch checklist:** (1) list sub-problems appearing in >=2 briefs; (2) pin one solution in the shared contracts or a shared helper; (3) after integration, run a cross-consistency review over the seams (data-flow + efficiency agents), not only per-file checks. RCA `docs/reviews/rca-combat-mechanics-2026-07-02.md` (the four CombatMechanics seam findings).

### Worktree isolation for parallel agent runs (DOC-BACKED + EMPIRICAL)

**Rule:** when spawning multiple `Agent` calls in one message that may edit overlapping single-owner files (`Main/TAOM.csproj`, `TAOM.Tests/TAOM.Tests.csproj`, `Main/IoC.cs`, `Main/SubModule.cs`, `Directory.Build.props`), pass `isolation: "worktree"` on each call — each agent gets its own git worktree on a temporary branch, so the shared tree is never touched in parallel and the build-watcher cascade cannot fire.

**When to apply:** always for parallel edits to the files above, "parallel ports"/"multiple features in flight" requests, or parallel feature scaffolding (`feature-builder`, `/new-feature`).
**When NOT needed:** read-only agents (`Explore`, research `Plan`); a single Agent call; agents on provably disjoint feature folders that don't touch csproj/IoC/SubModule (rare — audit the file set before assuming).
**After they return:** merge each worktree branch's diff back sequentially; prune stale checkouts under `.claude/worktrees/` once merged/abandoned (4 forgotten trees cost 22 GB by 2026-07-11).

---

## Related Guides

- [architecture.md](./architecture.md) — Layer architecture and patterns
- [tdd-enforcement.md](./tdd-enforcement.md) — TDD workflow
- [patterns.md](./patterns.md) — Design patterns (Hook, Strategy, Builder)
- [taleworlds-research-guide.md](./taleworlds-research-guide.md) — Decompilation workflow
- [code-quality.md](./code-quality.md) — Clean code principles
- [testing-guide.md](./testing-guide.md) — Testing patterns

---

<!-- backlinks-start auto-generated; edit lint_docs.py / build_backlinks.py to change -->

## Referenced by

- [docs/INDEX.md](../INDEX.md)
- [docs/reference/doc-lookup.md](../reference/doc-lookup.md)
- [docs/research/karpathy-autoresearch.md](../research/karpathy-autoresearch.md)

<!-- backlinks-end -->
