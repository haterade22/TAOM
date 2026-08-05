# CLAUDE.md

Bannerlord 1.4 total conversion mod (TAOM - Tales From the Age of Men)

> **Target: Bannerlord 1.4.7** (installed; pinned in `.claude/pinned-game-version.txt` — the session-start hook warns on drift → run `/engine-bump`). The `E:\Decompiled_Bannerlord\` dump matches (v1.4.7; older baselines preserved) but `ilspycmd`/`taom-src` on the installed DLLs is authoritative for signatures. Impact + history: [`docs/migration/v1.4.7-impact.md`](docs/migration/v1.4.7-impact.md) · [`TRACKING.md`](docs/migration/TRACKING.md) · [`v1.4.x-overview.md`](docs/migration/v1.4.x-overview.md).

## Commands

| Task | Command |
|------|---------|
| Build mod | `./build.ps1` |
| Build + test | `./build.ps1 -RunTests` |
| Run tests | `dotnet test TAOM.Tests` |

## Critical Rules (NEVER VIOLATE)

| Rule | Details |
|------|---------|
| **TDD Mandatory** | RED -> GREEN -> REFACTOR. Test first, always. |
| **Never Fabricate** | If you don't know, research — don't guess. State no file list / diff / count / hash / tool output / signature you have not actually read THIS turn. "I don't know yet, checking" is always acceptable; an invented fact never is. Read the proving output, confirm it's real, *then* write the doc/CHANGELOG/commit. See `.claude/rules/evidence-over-claims.md` §C. |
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior - check `E:\Decompiled_Bannerlord\` for concepts (v1.4.7 as of 2026-07-08, matching installed), but **verify signatures via `ilspycmd`/`taom-src` on installed DLLs** — the dump can lag after an engine bump; the installed DLLs are always authoritative |
| **Verify Before Reference** | Before writing `Sprite="X"` read `TAOMSpriteData.xml`. Before `PrefabExtension` injection, decompile vanilla target to check child assumptions. Before `IoC.Resolve` in hot path, use lazy cache. |
| **`/deep-review` Mandatory** | Run before EVERY commit touching C# — catches adapter violations, v1.4 incompatibilities, missing tests, data flow gaps |

## Working Discipline

Fork discipline, autonomous-loop stewardship, TodoWrite quality bar, inline-hook activation, and
edit-scope discipline moved verbatim to the always-load rule `.claude/rules/working-discipline.md`
(its full text is in context every session — this heading survives for old references).
**Native C++ ports:** the 6-point audit checklist lives in the paths-scoped rule
`.claude/rules/native-cpp-ports.md` (loads on `Dependencies/**/*.cpp|h`, `Main/SceneScripts/**`);
RCA: `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

## Skills (Slash Commands)

40 of the 42 skill **descriptions load eagerly** into every conversation (that is the
skill-listing you see; 2 are `disable-model-invocation: true`) — so a table of them here is a
second, drifting copy. Invoke a skill with `/name`; the routing table below ("Skill Routing")
says *when*. Full per-skill workflow lives in each `SKILL.md`.

### Workflow → Skill convention

**Every recurring, multi-step workflow becomes a skill** when it is (a) repeatable, (b) multi-step or chains skills, and (c) carries TAOM-specific gotchas. The skill is a thin entry point pointing at the authoritative doc. **Do NOT skill-ify** one-offs, pure reference, or single commands — descriptions load eagerly into every conversation, a permanent context tax. New skills: description <=30 words, follow `.claude/rules/external-skill-ports.md`, register in the routing table below, update CHANGELOG.

## Skill Routing (when to invoke what)

When the user's message matches one of these patterns, **proactively invoke** the listed skill via the Skill tool — the skill's structured workflow beats ad-hoc help. (What each skill *does* is in its eagerly-loaded description; this table only routes.)

### Strong proactive-invoke triggers

| User intent / phrase | Invoke | Confidence gate |
|----------------------|--------|-----------------|
| "this is broken", "why isn't this working", "it was working yesterday", crash logs, stack traces, exceptions from a TAOM patch or service | **`/investigate`** — never debug ad-hoc | None — always |
| Native CTD: `0xC0000005` / `AccessViolationException` with the stack dying in `TaleWorlds.Native.dll` (or any native module), "crashed to desktop" with no managed culprit | **`/native-crash-triage`** — never blind-retry a native AV | None — always. `/investigate` hands off here when the trail goes native |
| "add a new creature", "make X rideable", "new mount", a creature troop/mount feature kickoff | **`/new-creature-mount`** | Skip if the user is just sketching aloud — invoke when they say "do it" |
| Session-start hook prints "GAME VERSION DRIFT", Steam updated Bannerlord, "the game updated", deliberate engine migration | **`/engine-bump`** | None — always, and BEFORE trusting any test run |
| "the build won't compile", `error CS####` output, dotnet build failure | **`/build-fix`** | None — always. If error mentions a missing/renamed TaleWorlds type, hand off to `/research` first; if `/build-fix` retry budget triggers, hand off to `/investigate`. |
| "scaffold a feature", "new feature for X", "add a system that does Y" | **`/new-feature`** then offer `/freeze` to scope-lock during implementation | Skip if the user is just sketching aloud — only invoke when they say "do it" |
| "review this", "is this ready to merge", "before commit" on C# changes | **`/deep-review`** (or `/deep-review --codex` if user wants both) | **Only for C# changes touching ≥2 files OR any feature module.** For one-line fixes, XML/config/docs, skip — running 5+ agents is wasteful. |
| "I need to override DefaultXxxModel", "what's the signature of", "before touching a TaleWorlds class" | **`/research`** before editing | None — always |
| "create an issue for", "open a bug", "log this crash" | **`/issue`** | None — always |
| "check XSLT", new `.xslt` edit, "did the transform pass through correctly" | **`/xslt-check`** | None — always |
| "I'm wondering if X is in scope", "is this a side quest", "should I tackle Y in this PR" | **`/scope-check`** | None — always |
| "verify everything", "run build + tests", before claiming done | **`/verify`** | None — always |
| "split these commits", staged changes touching multiple concerns | **`/commit-split`** | None — always |
| "clean up this AI slop", over-engineered code, clearly redundant abstractions | **`/deslop`** | **Only if the code is clearly redundant (multiple similar abstractions, dead helpers).** `/deslop` is deletion-first — could remove code the user wants to keep. Default to asking first. |
| "add an ADR for", architectural decision being made | **`/new-adr`** | None — always |
| "session feels slow", "are we close to context limit", "audit my .claude/", after adding skills/agents/MCP servers | **`/context-budget`** | None — but skip when the user is in the middle of an unrelated task |
| "save my work", "save state", "save progress", "context save", before stepping away or before `/compact` | **`/context-save`** | None — always |
| "where was I", "resume", "pick up where I left off", "restore context" | **`/context-restore`** | None — always |
| Bash script bug, build infra issue, MCP server error, hook script crash — anything outside TAOM C# | **`debugger` agent** (Task tool) | None — but route TAOM C# bugs to `/investigate` instead |
| Agent loop / drift, repeated retries with no progress, context degradation | **`/agent-introspection-debugging`** | None — always |
| Multiple seemingly-unrelated bugs in same session/save, suspect shared root | **`error-detective` agent** (Task tool) | None — always |
| "this method is too long", "this class needs to be split", structural cleanup without behavior change | **`refactoring-specialist` agent** (Task tool) | Tests must be green BEFORE invoking. Tests must remain green AFTER. |
| "audit our skills", "are any skills broken", quarterly harness check | **`/skill-stocktake`** | None — diagnostic, no destructive action |
| User shares an external repo/article/skill/plugin to evaluate for adoption ("review this repo", "next repo", pastes a GitHub URL or a SKILL.md) | **`/adopt-external`** | None — always. Security-vet first: foreign `.claude/` trees get `python tools/audit_claude_config.py --root <clone> --external` BEFORE porting. |
| Before `/ship`, after editing hooks / MCP servers / `settings*.json` permissions / CLAUDE.md, or after porting external `.claude/` config | **`/security-scan`** | Skip for routine feature edits. Foreign-skill vetting uses `--root <clone> --external` (see `/adopt-external`). |
| "audit the whole repo", "what's worth doing / what should we build next", "write a handoff plan for X" | **`/improve`** | Repo-wide / proactive asks only — change-scoped C# review stays with `/deep-review`; build+test gating with `/verify`. |

### Soft suggest (offer, don't auto-invoke)

- **`/freeze`** on "only fix this" / "don't touch X" / "stay in this folder" / a many-file refactor or focused one-feature fix; **`/unfreeze`** when they're done and the boundary is still set.
- **`/investigate`** when a long manual debug session (multi-step trace, repro attempts) starts without it.
- **Ship sequence** on "let's get this merged" / "ready to PR": `/verify` → (`/codex-verify` or `/review-codex`) → close issue → update CHANGELOG.
- **`/migration-status`** on "what's the migration status" / v1.2 → v1.3 mentions.
- **`/humanizer`** when finishing a user-facing doc/issue/PR or on "humanize" — soft offer only; `ai-prose-style.md` already governs new prose as you write.

### Never auto-invoke

`/codex-verify`, `/review-codex` (cost real money — explicit user intent or the ship-sequence offer);
`/issue` (creates a public artifact); `/migration-status`, `/context-budget` (read-only diagnostics —
on request, or `/context-budget` after a major harness change).

### When the user invokes a skill explicitly

Treat the SKILL.md as executable instructions, not reference. Follow the phases in order. Don't shortcut. The phases exist because shortcuts caused the bugs that motivated the skill.

## Scoped Rules (auto-loaded by file path)

> **Convention:** A rule with a `paths:` array loads **conditionally** when a matching file is opened. A rule **without** `paths:` (omit the field entirely) loads **at conversation start** for every session. `paths: ["**/*"]` is NOT the same as omitting `paths:` — the former is still conditional under the rule loader.

Full catalog (rule → scope → content, 15 path-scoped + 7 always-load):
**[`docs/reference/rules-catalog.md`](docs/reference/rules-catalog.md)**. The always-load rules'
full text is already in context every session; the path-scoped ones load when you open a matching file.

## Custom Agents

5 custom agents in `.claude/agents/` (taleworlds-researcher, feature-builder, debugger,
error-detective, refactoring-specialist — descriptions load eagerly; each body carries its own
execution model). **Briefing convention, MANDATORY for every non-trivial spawn prompt** (subagents
do NOT reliably inherit CLAUDE.md or the rules): include **(1)** "Read
[docs/ai-includes/agent-operating-manual.md](./docs/ai-includes/agent-operating-manual.md) first",
**(2)** "you cannot invoke skills or spawn agents — recommend them in your report", **(3)** the
relevant tool reminder (`pwsh tools/taom-src.ps1 path <Type>` for signatures;
`dotnet … -p:DisableModuleCopy=true`, not `./build.ps1`), **(4)** explicit scope
(`Main/IoC.cs` / `Main/SubModule.cs` are single-owner — recommend, don't edit), **(5)** which
convention docs to read, **(6)** for PARALLEL builders: pin shared sub-problems once
(`.claude/rules/harness-facts.md` "Parallel builder briefs"). Read-only agents (`Explore`/`Plan`)
need 1–2 + scope. Implement-then-review dispatch follows the two-stage ordering in
[agent-teams.md](./docs/ai-includes/agent-teams.md#subagent-review-ordering-verify-spec-before-quality).

## Model Routing

| Task | Model | Why |
|------|-------|-----|
| Architecture decisions, complex design | **Opus** | Deepest reasoning for trade-off analysis |
| Feature implementation, code review | **Sonnet** | Best coding model, fast enough for iteration |
| Lightweight research, documentation, exploration | **Haiku** | 90% of Sonnet capability at 3x cost savings |
| Explore agents (codebase search) | **Haiku** | Read-only search doesn't need full reasoning |
| Plan agents (design work) | **Sonnet** | Needs coding awareness for implementation plans |

> **Haiku 3 deprecation:** April 19, 2026. The `haiku` alias already maps to `claude-haiku-4-5` — no action needed.

## Doc Lookup

**Start here:** [docs/INDEX.md](./docs/INDEX.md) — curated topical map. Task-oriented "Need
to… / Read" lookup (all 46 rows): **[`docs/reference/doc-lookup.md`](docs/reference/doc-lookup.md)**.
Topology queries: `/doc-graph`; architecture: [ADR-010](./docs/adrs/010-knowledge-base-architecture.md).
Lessons-learned: read the relevant `docs/reviews/lessons/<category>.md` BEFORE touching a
subsystem; append after every RCA ([index](./docs/reviews/LESSONS-LEARNED.md)).

## Localization

12 languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) × 3 modules ≈ 10K strings/lang.
PL is hand-translated; the other 11 are AI first-draft (`tools/translate_with_claude.py`).
**New player-facing text → wrap `{=KEY}default`, register, translate, validate: `/localize`** +
[TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md). Overrides in
`tools/translation_overrides/<lang>.json` always win over the LLM.
Full file/path map (source XMLs, per-language files, tools, cache, validation test):
[`docs/reference/localization-map.md`](docs/reference/localization-map.md).

## Key Paths

Full feature/component map (74 rows): **[`docs/reference/feature-map.md`](docs/reference/feature-map.md)**.
Layout: `Main/` (.NET Framework 4.7.2) · `Main/Features/<Name>/` · `TAOM.Tests/` ·
`Main/_Module/ModuleData/<feature>/` · adapters `Main/Adapters/` · core `Main/Core/`.
`.claude/` tree: `skills/`, `rules/`, `agents/`, `hooks/`; Codex config `.codex/config.toml`,
instructions `AGENTS.md`.

### Traps (read before touching these)

| Trap | Detail |
|------|--------|
| **TAOM_Map settlements** | `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` is LIVE; the repo's `Main/_Module/ModuleData/settlements.xml` is a **STALE SHADOW** (edits don't reach the game). Live renames: `tools/Apply-MapVillageNames.py`. |
| **NavalTravel** | PARKED 2026-06-26, DISABLED at the wiring level (#120/#296). Re-enable = 3 `SubModule.cs` blocks. |
| **NativeSkinFixes** | PARKED 2026-07-08, DISABLED at the wiring level. |
| **Elephant howdah / bone-tracking** | DEFERRED-disabled (slide sources). |
| **Vendored DLLs** | `Main/_Module/bin/Win64_Shipping_Client/` = allowlist (`MinHook.x64.dll`, `TAOM.NativeSkinFixes.dll`). Do NOT vendor `MCMv5.dll`. |
| **BehaviorTreeMissionLogic** | `: MissionLogic`, NOT `MissionBehavior` — regression rule, `docs/reviews/rca-looter-battle-nre-2026-05-24.md`. |
| **Armory** | dep is `LOTRLOME_Armory` (NOT `Armory_2`). Item defs under `.../LOTRLOME_Armory/ModuleData/LOTRLOME_items/<folder>/`. A root-level `<action>` (parented by `<action_sets>`, not an `<action_set>`) loads on the client but kills a dedicated server on boot — gate with `tools/audit_action_set_parity.py`. |
| **Co-op gating** | Three different questions, never interchangeable: `ICoopPresenceProvider.IsCoopActive` (a co-op mod is loaded), `ICoopSessionProvider.IsAuthority`/`ShouldDeferToHost` (may this peer mutate shared state), `IDedicatedServerProvider.IsDedicatedServer` (our binaries folder). `docs/features/coop-interop.md`. |
| **Console commands** | A `[CommandLineArgumentFunction]` method with the wrong shape throws inside the engine's unguarded discovery loop, past a native boundary with no managed backstop — a startup hazard, not just a broken console. Duplicate names drop silently. Route through `TaomConsole`; `ConsoleCommandBindingTests` pins it. `docs/features/dev-console.md`. |
| **Landless cultures** | A culture owning no settlement makes vanilla `SpawnLordParty`'s unguarded `Settlement.All.First(culture)` throw on the daily clan tick — CTD, no TAOM frame. Reachable: `TAOM_Map/settlements.xslt` strips ALL vanilla settlements. `Patch65_LandlessCultureSpawnGuard` guards it, `validate_moduledata.py`'s `LANDLESS_CULTURE` gates it. `docs/features/lord-spawn-guard.md`. |
| **Enlisted service** | A hidden + inactive MainParty parked on a lord is the legitimate enlisted state, not a bug. Presence is an OUTPUT of the state machine; only `DischargeService` ends service (restores presence first). `Patch66` rewrites menus. `docs/features/enlistment.md`. |

## Architecture (One-liner)

**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

## GameModel Overrides

Full registry (vanilla model -> TAOM override -> purpose), all rows:
**[`docs/reference/gamemodel-registry.md`](docs/reference/gamemodel-registry.md)**.
Override pattern + base-class + registration rules: `.claude/rules/gamemodels.md`
(loads on `Main/Features/**/*Model.cs`).

**Not registered:** `TaomPartyNavigationModel` — PARKED 2026-06-26 (#120/#296), vanilla model in use.

## Harmony Patch Categories

62 categories mapping a stack trace to its owning feature -> exact target -> status.
**Full table (category -> feature -> target -> status) + rationale / history / RCAs:
[`docs/reference/harmony-patch-registry.md`](docs/reference/harmony-patch-registry.md)** — grep the
failing type there. This is the crash-triage lookup; `/investigate` + `/native-crash-triage`
Phase 1 both read it. Patch-authoring rules (incl. the `MovementOrder`-postfix deferred-category
mandate): `.claude/rules/harmony-patches.md` (loads on `Main/**/Hooks/**`).

Non-obvious statuses worth knowing without opening the registry:

| Category | Status |
|----------|--------|
| `Patch15_BannerLayerLimit` | DISABLED (engine-native since v1.4.7; the transpiler self-bails) |
| `Patch63` | Used by TWO categories — `Patch63_BannerBearerSpawnGuard` + `Patch63_BlowDiagnostics`. Distinct strings, so Harmony is fine; the number is not a unique key |
| `Patch54_NavalTravelBoatVisual`, `Patch57_NavalAtSeaLandRescueGuard` | PARKED 2026-06-26 (#120/#296) |
| `Patch_MissionTime_SetMovementOrder` | **Shared deferred category — ANY postfix with `MovementOrder` in its signature MUST route through it** |

## Codex Integration

Codex = an independent verifier via the local `codex` CLI, dispatched directly by the skills
(`codex exec -c project_doc_max_bytes=65536 - < prompt > out`, background — the flag is REQUIRED,
see the skill bodies + `.claude/rules/harness-facts.md`). **Both cost money — explicit intent
only** (or via the `/ship` sequence). `/codex-verify` (5–20 min) · `/review-codex` (10–45 min) ·
`/deep-review --codex`. Pre-flight `codex login status`; instructions in `AGENTS.md`.
Mandatory completion sequence + dispatch contract:
[`docs/reference/codex-integration.md`](docs/reference/codex-integration.md) +
[completion-workflow.md](./docs/ai-includes/completion-workflow.md).

## Agent Teams

Use when work can be parallelized. See [agent-teams.md](./docs/ai-includes/agent-teams.md).

**Rules:** All Critical Rules apply to every teammate. `IoC.cs`/`SubModule.cs` are single-owner. Never run `./build.ps1` from two agents simultaneously.

## Documentation Requirements (MANDATORY)

- **CHANGELOG.md every session.** ADRs for architectural decisions (`docs/adrs/`); migration tasks in `docs/migration/TRACKING.md`.
- **CLAUDE.md:** new features add their row to `docs/reference/feature-map.md`, NOT here — here only a Traps/registry row a crash-triage reader needs, <=400 chars; prose goes in the feature doc (linter-enforced budget).
- **Every feature/bug/crash gets a GitHub issue**, created BEFORE implementation (retroactive only as repair); exhaustive body, labeled, closed when verified. **Every completed feature gets `docs/features/<name>.md`** (from TEMPLATE.md) — detailed enough that a future session needs ZERO re-decompilation.
- **Completion workflow (every C# feature, no exceptions):** `/verify` -> `/deep-review` + fix -> `/review-codex` + verify/fix -> self-review pass -> final `/verify` -> issue + feature doc + CHANGELOG. `/ship` orchestrates it; issue-body templates + 13-step sequence: [completion-workflow.md](docs/ai-includes/completion-workflow.md); process docs `docs/reviews/REVIEW-GUIDE.md` + `REVIEW-LOG.md`.

## Commits

50/72 rule. No AI attribution. Example: `feat: add garrison patrol calculation`

**Optional trailers** (add when relevant — each on its own line after the blank line):

| Trailer | When to use | Example |
|---------|------------|---------|
| `Constraint:` | TaleWorlds limitation blocked the ideal solution | `Constraint: Hero is sealed, can't subclass` |
| `Rejected:` | Alternative approach considered and dropped | `Rejected: Prefix patch — fires too early before state init` |
| `Not-tested:` | Parts that can't be unit tested | `Not-tested: Harmony patch invocation (requires live game)` |
| `Research:` | What was decompiled to inform this change | `Research: DefaultPartyWageModel.GetCharacterWage` |
| `Save-compat:` | Save file impact | `Save-compat: New field — safe, defaults to 0 on load` |

## MCP Servers

9 servers (7 project via `.mcp.json` incl. `imagine`, 2 user). Full server table, configuration,
and research lookup-order detail: **[`docs/reference/mcp-servers.md`](docs/reference/mcp-servers.md)**.

### MCP Usage Guide

| Task | Use This MCP | Instead Of |
|------|-------------|------------|
| Navigate C# symbols, find references | **Serena** (`find_symbol`, `get_symbols_overview`) | Grep for class names |
| Research TaleWorlds classes | **Read/Grep** `E:\Decompiled_Bannerlord\` first, **ilspy** MCP as fallback | On-demand decompilation |
| Read files across Bannerlord modules | **filesystem** (`read_file`, `search_files`) | Bash `cat` on long paths |
| Git blame, diff analysis | **git** (`git_blame`, `git_diff`) | `git` via Bash |
| Create/close GitHub issues | **GitHub** | `gh` via Bash |
| Validate/query TAOM ModuleData (does item/troop/culture id exist? what references id X? are there broken refs?) | **taom-moduledata** MCP (`validate_moduledata`, `item_exists`, `troop_exists`, `culture_exists`, `find_references`, `list_cultures`) — or `python tools/validate_moduledata.py` if the MCP isn't loaded | Grep ModuleData / hand-rolling a one-off ref validator |
| Research before implementing | **`taom-src path <Type>`** for signatures, **Read/Grep** decompiled source for browsing patterns, **Serena** for symbol nav, **ilspy** MCP as fallback | Manual decompilation workflow |

### TaleWorlds Research — Lookup Order

**Always use `taom-src` first** — authoritative signatures from the installed DLLs. The order:
**0.** [engine process docs](./docs/reference/engine/) for "how does X work" → **1.** `pwsh tools/taom-src.ps1 path <Type>`
for signature verification → **2.** browse `E:\Decompiled_Bannerlord\` (shipping-client decompile — editor-only
types are stripped, "absent" != "doesn't exist") → **3.** ILSpy MCP as fallback.
Full detail (compose examples, dual-build layout, DLL paths, configuration): [`docs/reference/mcp-servers.md`](docs/reference/mcp-servers.md).

## Hooks

24 hook registrations across 9 events (+ the `/freeze` inline hook). Full catalog (hook → event → purpose):
[`docs/reference/hooks-catalog.md`](docs/reference/hooks-catalog.md). Authoring conventions:
`.claude/rules/hook-authoring.md` (loads on `.claude/hooks/**`); durable lifecycle facts +
the verified 30-event list + handler contract: `.claude/rules/harness-facts.md` "Hook lifecycle".
**The Hook Response Contracts below are mandatory — read them.**

## Hook Response Contracts

When these hooks fire, Claude must respond as specified — not just read the output.

| Hook | Expected Response |
|------|------------------|
| `post-compact.sh` | Immediately `Read` MEMORY.md and each file listed under "Files in flight" before resuming work. Do not continue from transcript memory alone — the file is the source of truth. |
| `session-start.sh` ("GAME VERSION DRIFT" warning) | Surface the drift to the user FIRST and invoke `/engine-bump` before any build/test/crash-attribution work. Do not dismiss it — every test run on an unacknowledged engine bump produces misattributable evidence (the 1.4.6 lesson). |
| `detect-docs-gaps.sh` | Mention the gap list once to the user ("I noticed these features have no feature doc: ..."). Do NOT auto-create docs. Wait for user direction — they may have a reason the gap exists. |
| `validate-push.sh` (blocked) | Never retry with `--no-verify` or downgrade to a non-force push silently. Explain the block and ask the user whether to push to a non-protected branch instead. |
| `block-dangerous-git.sh` (ask) | When it prompts, approve ONLY if you intend to discard uncommitted/unpushed work; otherwise commit or stash first, then proceed. Don't auto-approve. |
| `check-verification-evidence.sh` | Run the build/test it names (`./build.ps1 -RunTests`) and read the output before claiming the work is done — don't dismiss the reminder. If the build genuinely can't run (env failure), say so explicitly per `environment-failures.md`; don't silently ignore it. |

## Status Line

`.claude/statusline.sh` renders `ctx: N% | model | branch | Ns/Nu/Nt` (staged/unstaged/untracked counts, omitted when clean). Registered in `settings.json → statusLine`.

## Notes

- Use `/reload-plugins` to pick up new or modified skills without restarting Claude Code

- Target: Bannerlord v1.4.7 (installed game version; the `E:\Decompiled_Bannerlord\` dump is v1.4.7 as of 2026-07-08 — `ilspycmd` on the installed 1.4.7 DLLs is authoritative)
- `E:\Decompiled_Bannerlord\` — v1.4.7 dump (category tree = shipping-client, strips editor code; dual-build `{_shipping_build,_editor_build}` for editor types; older baselines preserved). Details: [bannerlord-engine-and-toolchain.md](docs/reference/bannerlord-engine-and-toolchain.md); installed DLLs stay authoritative for signatures.
- Historical migration notes (1.2 → 1.3, 1.3 → 1.4) — see `docs/migration/`
- No git actions unless explicitly asked

## PowerShell Tool (Windows)

Opt-in preview (v2.1.78+): runs PowerShell natively instead of via Git Bash. Enable with
`"CLAUDE_CODE_USE_POWERSHELL_TOOL": "1"` in the settings.json env block. Details + the
`defaultShell` / hook `shell:` / skill `shell:` knobs + limitations:
[`docs/reference/powershell-tool.md`](docs/reference/powershell-tool.md).

## Equipment & Armory

Dependency is **`LOTRLOME_Armory`** (NOT `Armory_2`). **Before authoring an item: grep ALL
`LOTRLOME_items/*/` for the id prefix — a different folder = silent duplicate-ID shadowing.**
Canonical-folder table + validation + CC facegen rule: **`/author-armor`** +
[`docs/reference/armory-guide.md`](docs/reference/armory-guide.md); shield `item_usage`/offhand-bone
rules (+ the two misspelled `body_name`s that must NOT be "fixed"):
[`docs/reference/armory-shield-audit.md`](docs/reference/armory-shield-audit.md).

## Rebalancing & Data Tools

Full catalog with per-tool gotchas: [`tools/README.md`](tools/README.md) (Validation · Save-repair · Content Generation · Rebalancing · Lords & Equipment · Troop revamps · Settlements · Localization · One-offs). Preferred validators: `python tools/validate_moduledata.py` (+ the `taom-moduledata` MCP server). Balance passes: run the read-only `analyze_*` overview BEFORE any `rebalance_* --apply`.
