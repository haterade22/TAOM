# Agent Operating Manual

**Audience:** any subagent spawned in the TAOM project (the custom agents in `.claude/agents/`, and ad-hoc `Explore` / `Plan` / `general-purpose` agents). Read this at the start of your run. It tells you the execution model, which tools to run and how, and which skills exist (so you can *recommend* them — see below).

> **Why this doc exists:** a subagent runs in its own context with a strict tool allowlist. Claude Code does **not** guarantee that the project's `CLAUDE.md`, the `.claude/rules/`, or the skill descriptions reach you. Do not assume you inherited them — this manual + your own agent definition are your reliable source of truth.

---

## 1. Execution model (the rules that constrain you)

1. **You can only use the tools in your `tools:` frontmatter.** It's an enforced allowlist with no fallback. If you reach for a tool you don't have, it fails.
2. **You cannot invoke skills (slash commands), and you cannot spawn other agents.** None of the TAOM custom agents are granted the `Skill` or `Task` tool. So when a job calls for a skill, **you recommend it — you do not run it.** Put the recommendation in your final report and let the orchestrator (the main session) invoke it. Examples you will commonly want to recommend:
   - build won't compile → recommend **`/build-fix`**
   - a TAOM C# bug / "why is this broken" → recommend **`/investigate`**
   - scope-locking edits to one dir → recommend **`/freeze`**
   - pre-merge review of a finished feature → recommend **`/deep-review`** / **`/ship`**
   - an engine-binding or signature concern → recommend **`/verify-bindings`** / **`/research`**
3. **Report, don't guess, on environment failures.** Missing tool, broken path, MCP down, unset `BANNERLORD_GAME_DIR` → state it and stop; don't try to self-heal infra (`.claude/rules/environment-failures.md`).
4. **Stay in your lane.** Respect the scope your spawn prompt gave you. `Main/IoC.cs` and `Main/SubModule.cs` are single-owner convergence files — recommend the edit, let the orchestrator make it.
5. **Have a retry budget.** Same file + same error across ~3 attempts → stop and report what you tried; don't whack-a-mole.

---

## 2. Tool catalog — how to actually execute (via your Bash tool)

| Need | Command | Notes |
|------|---------|-------|
| **TaleWorlds signature / decompile** | `pwsh tools/taom-src.ps1 path <FullTypeName>` | **PRIMARY.** Decompiles the installed **v1.4.5** DLL on cache miss, prints an absolute `.cs` path. Compose: `rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)` |
| Browse engine source for patterns | `Read`/`Grep` under `E:\Decompiled_Bannerlord\` | Now a **v1.4.5** dump (re-decompiled 2026-05-22). Fine for browsing; for authoritative signatures still prefer `taom-src`. |
| Decompile fallback | `ilspycmd "<dll>" -t "<Type>"` or the `ilspy` MCP | Only if `taom-src` fails. |
| **Build** | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | Use this, NOT `./build.ps1`, during agent work (avoids `out/` contention). |
| **Test** | `dotnet test TAOM.Tests/TAOM.Tests.csproj -p:DisableModuleCopy=true` | Add `--filter "FullyQualifiedName~X"` to narrow. |
| Engine-binding gate | `dotnet test TAOM.Tests/TAOM.Tests.csproj --filter "TestCategory=BindingVerification"` | Verifies patch/GameModel/reflection bindings resolve against the installed engine. |
| Troop equipment refs | `python tools/validate_all_troop_refs.py` | Underwear-bug gate across all 7 culture troop XMLs. |
| API signature snapshot | `pwsh tools/snapshot_api_surface.ps1 [-Check]` | Regenerate / verify the committed v1.4.5 signature snapshot. |
| Doc health | `python tools/lint_docs.py --summary` | Dead links / stale-version refs / orphan docs. |
| Full tool list | see [`tools/README.md`](../../tools/README.md) | Generators, rebalancers, localization, faction-map, etc. |

**Target engine version is Bannerlord v1.4.5.** Anything in an agent prompt or doc that still says "v1.3.15" is stale — trust the installed DLLs + `taom-src`.

---

## 3. Skill catalog — what to RECOMMEND (you can't invoke these)

Grouped by purpose. Authoritative list + when-to-use routing: the **Skills** + **Skill Routing** tables in [`CLAUDE.md`](../../CLAUDE.md).

- **Build/debug:** `/build-fix` (compile errors, minimal diffs), `/investigate` (root-cause C# debugging), `/agent-introspection-debugging` (failing agent runs).
- **Build/verify a change:** `/verify` (build+test+git), `/verify-bindings` (engine API bindings + snapshot), `/deep-review` (multi-agent review), `/review-codex` + `/codex-verify` (Codex — costs money), `/ship` (full completion sequence).
- **Research:** `/research` (decompile + analyze a TaleWorlds class), `/taom-src` (one-shot signature lookup), `/xslt-check` (XSLT passthrough).
- **Authoring:** `/new-feature`, `/new-culture`, `/lord-skills`, `/author-armor`, `/localize`, `/new-adr`, `/issue`.
- **Scope/hygiene:** `/freeze` + `/unfreeze` (edit lock), `/scope-check`, `/deslop`, `/commit-split`, `/context-save` + `/context-restore`, `/context-budget`, `/skill-stocktake`.
- **Adoption/security:** `/adopt-external` (review an external repo/article and fold useful parts into TAOM — if your job is evaluating an outside source, recommend this), `/security-scan` (audit TAOM's own Claude config for secrets / permission / hook-exfil / MCP risk via `tools/audit_claude_config.py`).

When your job hits one of these situations, finish your analysis and **name the skill in your report** so the orchestrator runs it.

---

## 4. Where the project conventions live (read on demand)

Don't assume these reached you — read the relevant one when your task touches it:

- Architecture / layers: [`architecture.md`](./architecture.md) · patterns: [`patterns.md`](./patterns.md)
- TDD workflow: [`tdd-enforcement.md`](./tdd-enforcement.md) · testing: [`testing-guide.md`](./testing-guide.md)
- Adapter pattern (ADR-007), thin entry points (ADR-002), no #region/[Obsolete]/#if DEBUG (ADR-003/004/005): [`docs/adrs/`](../adrs/README.md)
- TaleWorlds research workflow: [`taleworlds-research-guide.md`](./taleworlds-research-guide.md)
- The `.claude/rules/*.md` (csharp-architecture, harmony-patches, gamemodels, adapters, tests, xslt, gui-ui, troops, xml-data) — read the one matching the files you're editing.
