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
| TaleWorlds Researcher | Read-only (decompile DLLs) | `ilspycmd` |
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
docs/ai-includes/taleworlds-research-guide.md first. Use ilspycmd to decompile DLLs at
%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client. Your job is to:
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

Read docs/ai-includes/taleworlds-research-guide.md first.

Your job: decompile TaleWorlds DLLs and document findings.
Tool: ilspycmd "%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\{DLL}" -t "Namespace.Class"

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

<!-- backlinks-end -->
