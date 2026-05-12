# CLAUDE.md

Bannerlord 1.3 total conversion mod (TAOM - Tales From the Age of Men)

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
| **No `#region`** | Use class decomposition (ADR-003) |
| **No `[Obsolete]`** | Migrate all usage in same PR (ADR-004) |
| **No `#if DEBUG`** | Except IoC.cs registration (ADR-005) |
| **Adapter Pattern** | Services use `IHeroAdapter` etc, NEVER `Hero` etc (ADR-007) |
| **Thin Entry Points** | <150 lines, delegate to services (ADR-002) |
| **Research First** | Never guess TaleWorlds behavior - check `E:\Decompiled_Bannerlord\` for concepts, but **verify signatures via `ilspycmd` on installed DLLs** (decompiled folder is v1.4, installed is v1.3.15) |
| **Verify Before Reference** | Before writing `Sprite="X"` read `TAOMSpriteData.xml`. Before `PrefabExtension` injection, decompile vanilla target to check child assumptions. Before `IoC.Resolve` in hot path, use lazy cache. |
| **`/deep-review` Mandatory** | Run before EVERY commit touching C# — catches adapter violations, v1.3 incompatibilities, missing tests, data flow gaps |

## Working Discipline

### Fork discipline (parallel agents)

When you spawn agents in parallel (`Agent` tool, multiple invocations in one message, or implicit forks), each runs in an isolated context you cannot peek at.

- **Don't Read or `tail` an in-progress fork's output file.** The completion signal arrives as a real tool result in a later turn — wait for it.
- **Don't fabricate or predict fork results.** Saying "the agent probably found X" is hallucinating completion. Either it returned, or you don't know yet.
- **Don't re-do the fork's work in parallel just because it's slow.** That's two contexts solving the same problem; results will diverge.
- The completion notification is a user-role tool result, not text you write — wait, then read what came back, then synthesize.

### Autonomous-loop stewardship (`/loop`, `/schedule`, background runs)

When you're invoked autonomously (no fresh user prompt), the trust model is *continue established work, do not initiate new work*.

- Continue what's already in motion: failing CI, open review threads, in-progress feature with clear next step, scheduled cleanup of a flagged TODO.
- Do not invent new tasks ("I noticed we could also refactor X"). Save that for the next interactive turn.
- Reversible local actions (edits, tests, builds) are fine. Irreversible / shared-state actions (push, PR open, branch delete, comment post) require explicit prior authorization for this run.
- If the transcript doesn't make the next step obvious, stop and report — don't guess.
- **NEVER STOP to ask permission to continue.** Once the autonomous run is established, do not pause to ask "should I keep going?" / "is this a good stopping point?". The user may be away. End the loop only when the established work is genuinely complete or genuinely blocked. If you run out of obvious next steps, think harder — re-read the transcript for missed angles, recombine prior near-misses — before stopping. (Source: karpathy/autoresearch `program.md`.)
- **Crash judgment.** Trivial bug (typo, missing import, transient flake) → fix and retry. Idea fundamentally broken (wrong approach, root assumption violated) → record the outcome (commit message, CHANGELOG, log) and move on. Don't iterate on a doomed approach hoping it will start working.

### TodoWrite quality bar

Items are atomic, verb-led, ≤14 words, describe outcomes not steps. No scaffolding entries. One item = one PR-worthy unit of work.

- Good: "Add retry budget rule to feature-builder agent"
- Bad: "Open feature-builder.md", "Read existing rules", "Think about wording"

If a task has internal sub-steps, those go in your head or as conversation, not as todos. Only add a todo when shipping it would genuinely move the user-visible needle.

### Inline-hook activation (skills with `hooks:` frontmatter)

Hooks declared in a SKILL.md `hooks:` block are scoped to that skill's lifecycle — **they only fire while the skill is invoked.** This has three concrete consequences:

1. **State files alone are inert.** Writing `.claude/tmp/freeze/freeze-dir.txt` from a non-`/freeze` agent does nothing — the freeze hook is not active. To engage the boundary, **invoke `/freeze`** (via the Skill tool). Other agents that recommend scope-locking must direct the user to `/freeze`, not simulate it.
2. **Cross-skill hook reuse is intentional and explicit.** `/investigate` re-declares the freeze hook in its own SKILL.md frontmatter so writing the state file from inside `/investigate` works (its hooks are active). Don't extend this pattern blindly to other skills — copy the hook block deliberately.
3. **Global enforcement belongs in `.claude/settings.json`.** If you need a hook that fires regardless of which skill is active, declare it there. Inline-frontmatter hooks are the right tool for opt-in / scoped behavior, not unconditional safety nets.

### Edit scope discipline

**Every changed line should trace directly to the user's request.** Don't "improve" adjacent code, comments, or formatting just because you're already in the file. A bug fix doesn't reformat the surrounding method. A new feature doesn't rename pre-existing variables. If a refactor is worth doing, it's worth its own PR — surface it after the requested change is done, don't smuggle it in. (Source: karpathy-skills `surgical-changes`.)

**Convert vague asks into testable objectives BEFORE the first Edit.** "Fix the bug" is not a task — "write a failing test that reproduces the NRE, make it pass, verify no regressions in `TAOM.Tests`" is. State the pass/fail criterion up front so the work has a defined end. This is what `/investigate` Phase 1 and `/verify` enforce locally; the same discipline applies to any non-trivial change, not just debugging. (Source: karpathy-skills `goal-driven-execution`.)

See also `.claude/rules/think-before-coding.md` (always-load) for the assumption-surfacing companion rule that fires before the first Edit on non-trivial requests.

## Skills (Slash Commands)

| Command | Purpose |
|---------|---------|
| `/research [Class]` | Decompile and analyze TaleWorlds classes |
| `/new-feature [Name]` | Scaffold a new feature module with IoC, services, tests |
| `/issue [bug\|feature\|crash] [desc]` | Create a GitHub issue with all required TAOM sections |
| `/xslt-check [file]` | Validate XSLT against SandBoxCore vanilla XML |
| `/migration-status` | Check v1.2 -> v1.3 migration progress |
| `/scope-check [change]` | Assess whether a proposed change fits current work context |
| `/build-fix [error]` | Incrementally fix dotnet build errors, one at a time, minimal diffs |
| `/verify [quick\|full]` | Run build + test + git status and produce pass/fail report |
| `/deslop [path]` | Regression-safe C# AI-slop cleanup: deletion-first, tests-first |
| `/new-adr [name]` | Scaffold an auto-numbered ADR with context pre-filled from git log + CHANGELOG |
| `/commit-split` | Group changed files by concern and commit each group atomically |
| `/deep-review [feature]` | Launch 5+ agents: standards, compat, efficiency, completeness, data flow (8 trace categories incl. sprite verification + vanilla interaction safety). No agent limit. |
| `/deep-review [feature] --codex` | Full review: Codex independent pre-review + 5+ Claude agents + adaptive expansion |
| `/codex-verify [feature]` | Dispatch independent Codex verification job in background |
| `/review-codex` | Auto-detect what was built, write Codex prompt, guide dispatch, verify results + implement fixes |
| `/context-budget [--verbose]` | Audit token consumption across `.claude/` (agents, skills, rules, MCP, CLAUDE.md). Recommend trims. |
| `/freeze` | Hard-block all Edit/Write outside a chosen directory for the rest of the session. Pair with `/unfreeze` to release. |
| `/unfreeze` | Release the directory edit lock set by `/freeze`. |
| `/investigate` | Systematic 6-phase root-cause debugging. Auto-engages `/freeze` to lock debug scope. Iron Law: no fixes without root cause. |
| `/agent-introspection-debugging` | 4-phase self-debug for failing AGENT runs (looping, drifting, burning tokens). Complements `/investigate` (which is for code bugs). |
| `/context-save` | Snapshot working context (git, in-flight tasks, decisions, files) to `.claude/state/context/` so a future session can resume without losing progress. |
| `/context-restore` | Load the most recent (or named) snapshot from `/context-save`. |
| `/skill-stocktake` | Quality audit of installed skills + agents. Quick scan (recent only) or `full`. Catches decay (broken refs, stale paths, bloated descriptions). |

## Skill Routing (when to invoke what)

When the user's message matches one of these patterns, **proactively invoke** the listed skill via the Skill tool. The skill has structured workflows, gates, and TAOM-specific patterns that produce better results than ad-hoc help. (Note: invoking the Skill tool still respects the user's tool-permission settings — the user may see a confirmation prompt unless allowlisted.)

### Strong proactive-invoke triggers

| User intent / phrase | Invoke | Confidence gate |
|----------------------|--------|-----------------|
| "this is broken", "why isn't this working", "it was working yesterday", crash logs, stack traces, exceptions from a TAOM patch or service | **`/investigate`** — never debug ad-hoc; the Iron Law is non-negotiable | None — always |
| "the build won't compile", `error CS####` output, dotnet build failure | **`/build-fix`** | None — always. If error mentions a missing/renamed TaleWorlds type, hand off to `/research` first; if `/build-fix` retry budget triggers, hand off to `/investigate`. |
| "scaffold a feature", "new feature for X", "add a system that does Y" | **`/new-feature`** then offer `/freeze` to scope-lock during implementation | Skip if the user is just sketching aloud — only invoke when they say "do it" |
| "review this", "is this ready to merge", "before commit" on C# changes | **`/deep-review`** (or `/deep-review --codex` if user wants both) | **Only for C# changes touching ≥2 files OR any feature module.** For one-line fixes, XML/config/docs, skip — running 5+ agents is wasteful. |
| "I need to override DefaultXxxModel", "what's the signature of", "before touching a TaleWorlds class" | **`/research`** before editing — never guess signatures | None — always |
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

### Soft suggest (offer, don't auto-invoke)

| Situation | Suggest |
|-----------|---------|
| User says "only fix this", "don't touch X", "stay in this folder", "I'm starting a refactor across many files", or starting a focused fix on one feature | Offer **`/freeze`**: *"Want me to scope-lock edits to `<dir>` so I can't drift?"* |
| User starts a long debug session manually (multi-step trace, repro attempts) without invoking `/investigate` | Suggest **`/investigate`**: *"This looks like root-cause debugging — want to use `/investigate`? It auto-locks scope and enforces the Iron Law."* |
| Done with the focused fix; freeze boundary still active. Triggers: "I'm done with that", "release the boundary", "let me work elsewhere now", "remove the freeze" | Offer **`/unfreeze`**: *"Boundary still set to `<dir>`. Release it?"* |
| About to ship a feature, "let's get this merged", "ready to PR", "send it" | Offer the ship sequence: **`/verify`** → (`/codex-verify` or `/review-codex`) → close issue → update CHANGELOG |
| User asks "what's the migration status" or mentions v1.2 → v1.3 work | Offer **`/migration-status`** |

### Never auto-invoke

| Skill | Why |
|-------|-----|
| `/codex-verify`, `/review-codex` | Cost real money — explicit user intent only (or via the ship-sequence offer above) |
| `/issue` | Creates a public artifact — explicit user intent only |
| `/migration-status` | Read-only diagnostic — only when user asks |
| `/context-budget` | Diagnostic — only when user asks or after a major harness change |

### When the user invokes a skill explicitly

Treat the SKILL.md as executable instructions, not reference. Follow the phases in order. Don't shortcut. The phases exist because shortcuts caused the bugs that motivated the skill.

## Scoped Rules (auto-loaded by file path)

> **Convention:** A rule with a `paths:` array loads **conditionally** when a matching file is opened. A rule **without** `paths:` (omit the field entirely) loads **at conversation start** for every session. `paths: ["**/*"]` is NOT the same as omitting `paths:` — the former is still conditional under the rule loader.

| Rule | Scope | Content |
|------|-------|---------|
| `xslt.md` | `**/*.xslt` | XSLT passthrough, SandBoxCore reference |
| `adapters.md` | `Main/Adapters/**` | Adapter pattern, research-first |
| `tests.md` | `TAOM.Tests/**` | TDD, naming, AAA pattern, coverage |
| `xml-data.md` | `ModuleData/**/*.xml` | NPC naming, region codes, formatting |
| `troops.md` | `troops/**`, `taom_partyTemplates.xml`, `TroopProgression/**` | Troop checklist, races, party templates, save compat |
| `harmony-patches.md` | `Main/**/Hooks/**` | Patch types, thin entry points, thread-local state |
| `gamemodels.md` | `Main/Features/**/*Model.cs` | GameModel override pattern, base class rules, registration |
| `csharp-patterns.md` | `Main/**/*.cs` | Hook/Strategy/GameModel patterns quick reference |
| `csharp-architecture.md` | `Main/**/*.cs` | Layer stack, IoC lifetimes, non-negotiable rules, stale-file re-read |
| `gui-ui.md` | `*Mixin*.cs`, `*Prefab*.cs`, `*Widget*.cs`, `*VM.cs`, `GUI/**` | Sprite verification, UIExtenderEx safety, ViewModel bindings |
| `environment-failures.md` | _(no `paths:` — always-load)_ | Report environment failures (missing tools, paths, MCP down). Don't auto-fix infra. |
| `harness-facts.md` | _(no `paths:` — always-load)_ | Pinned Claude Code load semantics, hook lifecycle, rule loader rules with doc URLs. Source-of-truth for harness behavior. |
| `simplicity-criterion.md` | _(no `paths:` — always-load)_ | Yes/No matrix for evaluating whether a change is worth keeping. Tiny gain + ugly code is rejected; deletions that hold parity always win. |
| `think-before-coding.md` | _(no `paths:` — always-load)_ | Surface load-bearing assumptions before the first Edit; ask if uncertain. Don't ask on trivial/mechanical work. |
| `external-skill-ports.md` | `.claude/skills/**/SKILL.md` | Per-field validation checklist when porting skills from external suites (gstack, etc.). |

## Custom Agents

| Agent | Purpose |
|-------|---------|
| `taleworlds-researcher` | Decompile and analyze TaleWorlds DLLs |
| `feature-builder` | Build features following TAOM architecture |
| `debugger` | Generic debugging for non-TAOM-specific issues (tooling, scripts, CI). Use `/investigate` for TAOM C# bugs. |
| `error-detective` | Cross-system error correlation when one root cause manifests as multiple symptoms across features. |
| `refactoring-specialist` | Behavior-preserving structural refactoring (extract/rename/move). Use `/deslop` for redundant-code deletion. |

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

| Need to... | Read |
|------------|------|
| Write tests / TDD workflow | [tdd-enforcement.md](./docs/ai-includes/tdd-enforcement.md) |
| Research TaleWorlds mechanics | [taleworlds-research-guide.md](./docs/ai-includes/taleworlds-research-guide.md) |
| Debug / iterate on problem | [iterative-problem-solving.md](./docs/ai-includes/iterative-problem-solving.md) |
| Compare multiple approaches | [multi-approach-validation.md](./docs/ai-includes/multi-approach-validation.md) |
| Understand architecture | [architecture.md](./docs/ai-includes/architecture.md) |
| Check design patterns | [patterns.md](./docs/ai-includes/patterns.md) |
| Work with GUI/sprites/UI | [gui-sprite-system.md](./docs/features/gui-sprite-system.md) |
| Check ADR rules | [docs/adrs/](./docs/adrs/README.md) |
| Ensure code quality | [code-quality.md](./docs/ai-includes/code-quality.md) |
| Check migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |
| Use agent teams | [agent-teams.md](./docs/ai-includes/agent-teams.md) |
| Plan future GameModel overrides | [roadmap.md](./docs/roadmap.md) |

## Key Paths

| Component | Path |
|-----------|------|
| Mod code | `Main/` (.NET Framework 4.7.2) |
| Mod tests | `TAOM.Tests/` (MSTest + NSubstitute) |
| Features | `Main/Features/` |
| Adapters | `Main/Adapters/` |
| Core | `Main/Core/` |
| CharacterCreation | `Main/Features/CharacterCreation/` |
| AtmospherePersistence | `Main/Features/AtmospherePersistence/` |
| AdvancedCombat | `Main/Features/AdvancedCombat/` (SpatialGrid, BoneCollision, CustomAttacks) |
| CulturalFeats | `Main/Features/CulturalFeats/` (TaomCulturalFeats, 16 GameModel overrides) |
| CustomBattles | `Main/Features/CustomBattles/` (Custom battle factions, commanders, troops) |
| Arena | `Main/Features/Arena/` (TaomTournamentModel — per-participant culture armor) |
| MainMenuCustomizer | `Main/Features/MainMenuCustomizer/` (hide Campaign, rename Sandbox → "Enter The Age Of Men") |
| ShaderPrecompilation | `Main/Features/ShaderPrecompilation/` (pre-compile shaders menu option, eliminates in-game stutter) |
| SiegeDefense | `Main/Features/Siege/` (timed defense events when watched factions are besieged; config-driven watched factions, CampaignTime deadline, relation+influence reward on arrival) |
| SpecialResources | `Main/Features/SpecialResources/` (11 resources across 18 kingdoms — War Spoils/Gems/Castar/Marks/Elven Wine/Lake Fish/War Drums/Tribal Relics/Dunlending Ale/Plunder/War Banners; XML-driven with many-to-one kingdom/culture mappings, shared balance, pending transaction upgrades, desertion at 0, notifications, Patch26, composite `heroId:resourceId` storage) |
| CareerSystem | `Main/Features/CareerSystem/` (career/class progression — 50 careers across 16 cultures; XML-driven career defs, mutation calculator registry, passive service with GameModel integration, ability system, career screen UI via UIExtenderEx, level-based tier gating, SyncData persistence, CC career selection stage) |
| SettlementGuards | `Main/Features/SettlementGuards/` (per-settlement guard customization — XML-driven guard troop pools with settlement→clan→culture fallback, spawn-point filtering, weighted random selection, per-culture spear mapping; Harmony prefixes on private GuardsCampaignBehavior methods) |
| NamedCompanions | `Main/Features/NamedCompanions/` (18 lore companions as recruitable wanderers — Aragorn/Legolas/Gimli/etc; `is_hero="true"` + `occupation="Wanderer"`, JSON config for spawn settlements, vanilla dialog integration, race persistence via existing HeroRace system) |
| RevoltTuning | `Main/Features/RevoltTuning/` (JSON-tunable soft-nerf of vanilla revolt mechanic for LOTR's frequent settlement flips; raises loyalty thresholds + dampens different-culture penalties; semantic validation rejects out-of-range / sign-flipped values; consumed by `TaomSettlementLoyaltyModel`) |
| Messengers | `Main/Features/Messengers/` (paid messenger dispatch from encyclopedia + dialog hook; travels for N days, arrival inquiry opens conversation mission with settlement-vs-field routing and player-position restore; random ambush rolls; primitive-dict SyncData; UIExtenderEx prefab extension on `EncyclopediaHeroPage`; ported from LOTRAOM 1.2.12 with 1.3.15 API drift applied; TAOM-owned `MapCoord` keeps service free of TaleWorlds types per ADR-007) |
| QuickActions | `Main/Features/QuickActions/` (replaces inventory "Sell All" button with a 4-option menu — Sell Damaged / Sell Low Value / Unequip All / vanilla; ported from external `TransferbuttonMenu` 1.2.x with v1.3.15 verification removing 8-probe + 5-probe reflection chains; `IInventoryVMAdapter` consolidates inventory VM surface for future EquipPresets reuse; `Patch34_SellAllItemsMenu` Prefix uses thread-static bypass flag so "Sell All (Vanilla)" re-enters vanilla `TransferAll` unmodified; `TrySellItem` sets `SPItemVM.TransactionCount = StackAmount` before invoke for full-stack sells; `TryUnequipAllPlayerSlots` routes through `InventoryLogic.TransferCommand` so vanilla `AfterTransfer` rebuilds rows; per-save `IsSearchAvailable` toggle via `InventorySearchCampaignBehavior` SyncData + Postfix on `SPInventoryVM.RefreshCallbacks`) |
| SmartCavalryAI | `Main/Features/SmartCavalryAI/` (player-team cavalry coordinated line-charge state machine — `ICavalryChargeService` drives Idle→Forming→Charging→PassingThrough→Reforming + Rerouting branch; `Patch31_FormationSetMovementOrder` Postfix; `ICavalryCommandAdapter` + `IBattlefieldQueryAdapter` wrap TaleWorlds APIs; `SmartCavalryRecursionGuard` thread-local depth counter prevents postfix re-entry; v1.3.15 confirmed `SetPositioning`/`SetMovementDirection` are public — no reflection needed; ported with 2 inherited bugs fixed: hardcoded reform-strictness 0.5f override + ungated HUD spam; 4 Codex adversarial findings fixed including NaN propagation through Clamp + cross-feature collision with MixedFormations) |
| EditorCacheRebuild | `Main/Features/EditorCacheRebuild/` (parallel + incremental + resumable settlement distance cache rebuild — two entry points share one builder pipeline. **Primary path:** singleplayer MCM trigger — `TaomSettings.RebuildDistanceCacheAction` boundary lambda → `IRuntimeCacheRebuildService.Trigger()` → `Task.Run` → `new SandBoxNavigationCache(Default)` + `NavigationCacheAdapter` + `CacheBuilderService.Build` + atomic write with `.prev` backup + round-trip verification. **Legacy path:** editor button — `Patch37_CacheBuildOverride` patches `NavigationCache<SettlementRecord>.GenerateCacheData()` via dynamically-built closed generic since `SettlementRecord` is `private sealed nested` in `SandBox.View.Map.SettlementPositionScript`. `NavigationCacheAdapter` is reflection-driven and T-agnostic (works for both `NavigationCache<Settlement>` runtime and `NavigationCache<SettlementRecord>` editor); `ParallelPhase1Builder` + `ParallelPhase2Builder` use `Parallel.For` + `ConcurrentQueue` with locked dict writes; `SmokeTestGate` validates serial-vs-parallel pathfind equivalence at build start; `CheckpointSerializer` saves state between phases for crash recovery; `SettlementDiffer` + `ChangedSettlementsFilter` enable incremental Phase 1 when ≤30 settlements changed; `ValidationReportWriter` emits per-build JSON; ~108hr full rebuild → ~30min target. Comprehensive logging with build-correlation IDs, environment snapshot, scene CRCs, per-phase memory deltas, first-pair heartbeats, atomic-write integrity diagnostics.) |
| Warg Combat | `Main/Features/Warg/` (BT elements, WargAttackService, WargMissionBehavior) |
| BT DLLs | `Main/_Module/bin/Win64_Shipping_Client/BehaviorTrees.dll`, `BehaviorTreeWrapper.dll` |
| Alliance.Wargs | External module: Monster id="warg", animations, items |
| CC narrative data | `Main/_Module/ModuleData/charactercreation/` (JSON) |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| Custom lords XML | `Main/_Module/ModuleData/characters/lords.xml` |
| SpecialResources config | `Main/_Module/ModuleData/special_resources/` (resource defs + troop costs XML) |
| CareerSystem config | `Main/_Module/ModuleData/career_system/` (career defs + choice trees + ability templates + ability tuning XML) |
| RevoltTuning config | `Main/_Module/ModuleData/configs/revolt_tuning_config.json` (4 thresholds/penalties; validated on load) |
| Messengers config | `Main/_Module/ModuleData/messengers/messenger_config.json` (advanced tuning — `accidentChancePerHour` + `travelSpeedMultiplier`; rejects NaN/Infinity/out-of-range; player-facing knobs in MCM `TaomSettings.Messengers`) |
| CareerSystem CC config | `Main/_Module/ModuleData/charactercreation/career_menu.json` (50 career CC skill/attribute bonuses) |
| CareerSystem sprites | `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/` (portraits 800x400, ability icons 256x256, dedicated atlas) |
| Sprite atlas config | `Main/_Module/GUI/SpriteParts/Config.xml` (sprite category registration with `<AlwaysLoad />`) |
| SettlementGuards config | `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml` (per-settlement guard pools, clan/culture fallbacks, spear mappings) |
| NamedCompanions config | `Main/_Module/ModuleData/named_companions/` (companion defs XML, spawn config JSON, backstory strings XML) |
| StartupResources config | `Main/_Module/ModuleData/startup_resources/startup_resources_config.xml` |
| CCBodyProperties config | `Main/_Module/ModuleData/charactercreation/cc_body_properties.xml` (per-culture default BodyProperties for CC preview; 128-hex key per culture) |
| TaleWorlds DLLs | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client` |
| Decompiled source | `E:\Decompiled_Bannerlord\` (pre-decompiled, organized by category) |
| CI/CD | `.github/workflows/build.yml` |
| Shared build props | `Directory.Build.props` |
| Skills | `.claude/skills/` |
| Rules | `.claude/rules/` |
| Agents | `.claude/agents/` |
| Codex config | `.codex/config.toml` |
| Codex instructions | `AGENTS.md` (project root) |

## Architecture (One-liner)

**Mod**: `[HarmonyPatch/GameModel/CampaignBehavior]` -> `IHookInterface` -> `Service` -> `IAdapter` (sealed types)

## GameModel Overrides

| GameModel | Overrides | Purpose |
|-----------|-----------|---------|
| `TaomCharacterStatsModel` | `DefaultCharacterStatsModel` | `MaxCharacterTier => 10` (vanilla 6) |
| `TaomPartyWageModel` | `DefaultPartyWageModel` | Extended tier wages (T0-T10) + culture wage/garrison/Rohan mounted feats + career TroopWages passive |
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence award/cost feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest speed + Rohan infantry speed feats + career PartyMovementSpeed passive |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat + career BattleRenownGain passive |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats (Isengard, Rohan) + career TroopUpgradeCost passive |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats (Mordor, Gundabad, DG, Isengard, Gondor) + career PartySize passive |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats (Gondor, Erebor, elves, Rohan) + JSON-tunable revolt thresholds + dampened different-culture penalties (RevoltTuning feature) |
| `TaomPartyMoraleModel` | `DefaultPartyMoraleModel` | Party morale feats (Gondor, Rohan, Erebor, elves) + career TroopMorale passive |
| `TaomSmithingModel` | `DefaultSmithingModel` | Smithing energy cost feats (Erebor, Isengard) + career EnchantmentCostReduction passive |
| `TaomClanFinanceModel` | `DefaultClanFinanceModel` | Tariff income feat (Umbar) |
| `TaomRaidModel` | `DefaultRaidModel` | Raid damage feats (Mordor, Gundabad, Isengard) + career TroopDamage passive |
| `TaomMilitaryPowerModel` | `DefaultMilitaryPowerModel` | Configurable T7-T10 troop power (MCM + JSON) |
| `TaomCombatSimulationModel` | `DefaultCombatSimulationModel` | Configurable blunt/cut damage ratio per battle type (MCM) |
| `TaomPartyHealingModel` | `DefaultPartyHealingModel` | Cultural survival bonuses (JSON per-faction death chance multiplier) |
| `TaomTournamentModel` | `DefaultTournamentModel` | Per-participant culture armor + culture-specific prize pools (Tierf-based) for regular and elite rewards |
| `TaomAgeModel` | `DefaultAgeModel` | Race-appropriate lifespans (elven immortality, dwarf/hobbit aging) |
| `TaomPregnancyModel` | `DefaultPregnancyModel` | Race-appropriate pregnancy durations |
| `TaomHeroCreationModel` | `DefaultHeroCreationModel` | Race-aware hero creation defaults |
| `TaomAllianceModel` | `DefaultAllianceModel` | Racial enmity constraints on alliance formation |
| `TaomKingdomDecisionPermissionModel` | `DefaultKingdomDecisionPermissionModel` | Culture/race-based decision permission rules |
| `TaomDiplomacyModel` | `DefaultDiplomacyModel` | Custom diplomacy logic for LOTR faction relationships |
| `TaomExecutionRelationModel` | `DefaultExecutionRelationModel` | Culture-specific relation penalties for executions |
| `TaomInformationRestrictionModel` | `DefaultInformationRestrictionModel` | Encyclopedia visibility restrictions per settings |
| `TaomSiegeEventModel` | `DefaultSiegeEventModel` | Adds Trebuchet to defender siege engine options (for Minas Tirith et al.); preserves vanilla Fire-variant perk gating |
| `TaomTargetScoreModel` | `DefaultTargetScoreCalculatingModel` | Besieger army: commitment stickiness (4×), faction priority lists, strength gate bypass per faction, distance compensation; `Patch22_ArmyTargeting` border proximity floor |

## Harmony Patch Categories

| Category | Feature | Target |
|----------|---------|--------|
| `Patch0_BattleScenes` | Battle scenes (DISABLED) | `Campaign.InitializeScenes` |
| `Patch1_FirstTimeInit` | First-time initialization | Various |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various |
| `Patch3_SetRace` | Race assignment | Various |
| `Patch4_CharacterSpawner` | Character spawning | Various |
| `Patch5_FaceGen` | Face generation | Various |
| `Patch6_BannerEditor` | Banner editor | Various |
| `Patch7_FactionMap` | Faction map | Various |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various |
| `Patch9_RaceFilter` | Culture-restricted race dropdown on CC | `FaceGenVM.Refresh` |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` |
| `Patch11_Diplomacy` | Diplomacy system | Various |
| `Patch12_WarOfTheRing` | War of the Ring | Various |
| `Patch14_Execution` | Execution system | Various |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` |
| `Patch17_TroopWeight` | Troop weight system | `PartyBase`, `TroopRoster` |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` |
| `Patch19_CustomBattles` | Custom battle TAOM factions/commanders/troops | `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions` |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes for no-mount cultures | `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView` |
| `Patch21_ShaderPrecompilation` | Loading screen shader progress text | `LoadingWindowViewModel` |
| `Patch22_ArmyTargeting` | Border proximity floor for priority-list targets | `AiMilitaryBehavior` |
| `Patch23_BannerColorPersistence` | UI color persistence + 3D battle + conversation — player clan colors everywhere | `CampaignUIHelper`, `SandBoxUIHelper`, `SPInventoryVM`, `PartyVM`, `HeroViewModel`, `PartyCharacterVM`, `ClanPartyItemVM`, `Mission`, `CampaignSceneNotificationHelper`, `Banner`, `BannerEditorView`, `Agent.EquipItemsFromSpawnEquipment`, `AgentVisuals.Create` (manual), `MapConversationTableau` (manual ×2), `OrderOfBattleHeroItemVM` |
| `Patch24_BannerDriftGuard` | Block vanilla banner color drift during War of the Ring | `Clan.UpdateBannerColorsAccordingToKingdom`, `Clan.UpdateBannerColor` |
| `Patch26_SpecialResources` | Per-kingdom resource gating + transactional spending | `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand` |
| `Patch27_CareerSystem` | Career screen opening + ability V-key activation (3 archetypes: Infantry/Ranged/Cavalry, 50 careers, XML-tunable) | `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats` |
| `Patch28_SettlementGuards` | Per-settlement guard troop injection + per-culture spear mapping (manual patches) | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual) |
| `Patch29_CCBodyProperties` | Per-culture default BodyProperties on CC screen + culture-stage-VM body re-apply + career menu player body sync | `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals` |
| `Patch31_SmartCavalryAI` | Coordinated line-charge state machine on player cavalry (Forming→Charging→PassingThrough→Reforming + Rerouting branch); recursion-guarded. **Note:** the `Formation.SetMovementOrder` Postfix lives in the shared `Patch_MissionTime_SetMovementOrder` category (see below). | `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`) |
| `Patch34_QuickActions` | Inventory "Sell All" multi-action menu + active-VM capture + per-save search-toggle apply + thread-static bypass for vanilla re-entry | `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix capture), `SPInventoryVM.RefreshCallbacks` (Postfix search-apply), `SPInventoryVM.OnFinalize` (Postfix clear) |
| `Patch_MissionTime_SetMovementOrder` | Shared deferred category for `Formation.SetMovementOrder(MovementOrder)` postfixes. Applied once from `OnMissionBehaviorInitialize` (one-shot static guard) because `MovementOrder.cctor` reads `Mission.Current.CurrentTime` — null in `OnSubModuleLoad`/`OnGameInitializationFinished`. Currently houses Patch31_SmartCavalryAI's charge handler and Patch35_CompanionTactics' `CancelStanceOnMove` postfix. **Any future patch with `MovementOrder` in its postfix signature must use this category.** | `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2) |
| `Patch37_EditorCacheRebuild` | Legacy editor-button cache rebuild override (kept as fallback path). Patches `NavigationCache<SettlementRecord>.GenerateCacheData()` (Prefix returns false). Target resolved via `Type.GetType("SandBox.View.Map.SettlementPositionScript+SettlementRecord, SandBox.View")` + `MakeGenericType`. The patch *attaches* in singleplayer too (the `SettlementRecord` type is loaded with `SandBox.View.dll`), but the patched method `NavigationCache<SettlementRecord>.GenerateCacheData()` is only invoked by the editor's `ComputeAndSaveSettlementDistanceCache` button — never called outside editor mode, so the patch is **dormant in singleplayer**. The runtime path (singleplayer MCM trigger) operates on the disjoint closed generic `NavigationCache<Settlement>` via `RuntimeCacheRebuildService` — no interaction between the two paths. | `NavigationCache<SettlementRecord>.GenerateCacheData()` (Prefix) |

## Codex Integration

Codex operates as an independent verifier via the `codex-plugin-cc` Claude Code plugin. It shares no session context with Claude — providing a genuine second opinion.

| Command | Purpose |
|---------|---------|
| `/codex-verify [feature]` | Background Codex verification while Claude builds |
| `/deep-review [feature] --codex` | Full review: Codex + 4 Claude agents |
| `/codex:adversarial-review` | Challenge specific decisions |
| `/codex:rescue [task]` | Delegate investigation to Codex |
| `/codex:status` | Check background job progress |
| `/codex:result` | Retrieve completed results |

**Config:** `.codex/config.toml` | **Instructions:** `AGENTS.md` (project root)

**Enhanced completion workflow:**
1. `/verify` -> 2. `/codex-verify` (background) -> 3. Continue building -> 4. `/codex:result` -> 5. Fix CRITICAL/HIGH -> 6. `/deep-review` -> 7. Issue + docs + CHANGELOG

## Agent Teams

Use when work can be parallelized. See [agent-teams.md](./docs/ai-includes/agent-teams.md).

**Rules:** All Critical Rules apply to every teammate. `IoC.cs`/`SubModule.cs` are single-owner. Never run `./build.ps1` from two agents simultaneously.

## Documentation Requirements (MANDATORY)

| Doc | When to update | Path |
|-----|---------------|------|
| **CHANGELOG.md** | Every session | `CHANGELOG.md` |
| **CLAUDE.md** | New files, paths, patterns | `CLAUDE.md` |
| **ADRs** | Architectural decisions | `docs/adrs/` |
| **Migration tracking** | Migration tasks | `docs/migration/TRACKING.md` |
| **GitHub Issues** | Every feature, bug, crash, system fix | `gh issue create/close` |
| **Feature docs** | Every completed feature | `docs/features/<name>.md` |

## GitHub Issue & Knowledge Base Requirements (MANDATORY)

### GitHub Issues — Create for ALL Work

Every feature, bug fix, crash fix, or system change MUST have a GitHub issue. No exceptions.

**When to create:**
- Starting a new feature → create issue BEFORE implementation
- Fixing a bug/crash → create issue documenting the problem FIRST
- Completing a fix that was done without an issue → create issue retroactively with full details

**Issue content — be exhaustive:**

For **bug/crash fixes**, the issue body MUST include:
1. **Problem** — exact error message, stack trace, reproduction steps
2. **Analysis** — root cause investigation, what was examined, why it happened
3. **Solution** — what was changed and WHY that approach was chosen
4. **Files changed** — list of modified files with one-line descriptions
5. **Testing** — how the fix was verified

For **features**, the issue body MUST include:
1. **Motivation** — why this feature exists, what problem it solves
2. **Design** — architecture decisions, alternatives considered
3. **Implementation** — key files, patterns used, configuration
4. **Testing** — test coverage, how to verify it works

**Lifecycle:**
- Label issues appropriately (`bug`, `feature`, `crash`, `enhancement`)
- Reference the issue number in commits when possible
- **Close the issue** with `gh issue close` when the work is complete and verified

**Commands:** Use `gh issue create` and `gh issue close` via Bash.

### Feature Documentation — `docs/features/`

Every completed feature MUST have a documentation file at `docs/features/<feature-name>.md`. This is the **knowledge base** that prevents future sessions from re-analyzing solved problems.

**Use template:** `docs/features/TEMPLATE.md`

**Sections required:**
- Overview — what it does in 2-3 sentences
- Why This Exists — the problem it solves, with specific examples
- Architecture — design challenge, solution approach, component diagram
- Configuration — config files, data formats, current values
- Key Files — table of all files with their purpose
- Dependencies — what it relies on
- Tests — test file locations and coverage summary
- How-To — common operations (e.g., "How to add a new X")
- Performance — any optimization notes (if applicable)

**Existing examples:** `docs/features/race-age-system.md`, `docs/features/offspring-race-inheritance.md`

**Rule:** If a future session needs to understand a feature, the doc should contain enough detail that ZERO decompilation, code reading, or re-analysis is needed for the conceptual understanding. Code reading is only for the current state of the implementation.

### Completion Workflow (MANDATORY — every feature, no exceptions)

Before closing out any feature or fix, run this FULL sequence:

```
Phase 1: BUILD & INTERNAL REVIEW
  1. /verify                        — build + tests pass
  2. /deep-review [feature]         — 4 parallel agents (standards, compat, efficiency, completeness)
  3. Fix issues from deep-review

Phase 2: CODEX ADVERSARIAL REVIEW
  4. /review-codex                  — auto-detects what was built, writes Codex prompt
  5. Dispatch to Codex              — /codex:adversarial-review --background (terminal)
  6. /review-codex                  — detects review file, verifies findings, implements fixes

Phase 3: SELF-REVIEW (review our OWN fixes)
  7. /review-codex                  — detects fix changes, writes new Codex prompt for our fixes
  8. Dispatch to Codex              — /codex:adversarial-review --background (terminal)
  9. /review-codex                  — verifies findings on our fixes, implements confirmed fixes

Phase 4: CLOSE OUT
  10. /verify                       — final build + tests pass
  11. Create/close GitHub issue with full details
        ↑ Issue must exist BEFORE the closing commit, not after.
          Codex review #28 caught us creating issue #92 retroactively for
          b7e7188. The pre-commit hook only enforces CHANGELOG, not issue
          creation — discipline is on the author. Pattern: open the issue
          when starting the work, reference it in commit messages, close
          it with the final commit.
  12. Write/update docs/features/<name>.md
  13. Update CHANGELOG.md
```

**Do not skip any phase.** Phase 2 catches bugs Claude misses (43 found in codebase review). Phase 3 catches bugs in our fixes (already caught IsFemale field targeting wrong type, shaghana/abanissa alignment mismatch). Each phase exists because the previous one proved insufficient.

**Process docs:** `docs/reviews/REVIEW-GUIDE.md` (prompt templates), `docs/reviews/REVIEW-LOG.md` (scoring history)

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

| Server | Scope | Purpose | Config |
|--------|-------|---------|--------|
| **Serena** | Project | Symbolic code navigation (C# classes, methods, references) | `.mcp.json` |
| **GitHub** | Project | PRs, issues, actions, code search | `.mcp.json` |
| **filesystem** | Project | File operations across TAOM, Bannerlord Modules, LOTRAOM assets | `.mcp.json` |
| **git** | Project | Rich git operations (diff, blame, log, branch management) | `.mcp.json` |
| **ilspy** | Project | Decompile TaleWorlds DLLs — fallback when `E:\Decompiled_Bannerlord\` doesn't have what you need | `.mcp.json` |
| **sequential-thinking** | User | Extended reasoning for complex design decisions | `~/.claude/.mcp/user.json` |
| **context7** | User | Library documentation lookup | `~/.claude/.mcp/user.json` |

### MCP Usage Guide

| Task | Use This MCP | Instead Of |
|------|-------------|------------|
| Navigate C# symbols, find references | **Serena** (`find_symbol`, `get_symbols_overview`) | Grep for class names |
| Research TaleWorlds classes | **Read/Grep** `E:\Decompiled_Bannerlord\` first, **ilspy** MCP as fallback | On-demand decompilation |
| Read files across Bannerlord modules | **filesystem** (`read_file`, `search_files`) | Bash `cat` on long paths |
| Git blame, diff analysis | **git** (`git_blame`, `git_diff`) | `git` via Bash |
| Create/close GitHub issues | **GitHub** | `gh` via Bash |
| Research before implementing | **Read/Grep** decompiled source + **Serena**, **ilspy** if needed | Manual decompilation workflow |

### TaleWorlds Research — Lookup Order

**WARNING:** `E:\Decompiled_Bannerlord\` is v1.4 but the installed game is v1.3.15. Use decompiled source for understanding concepts/patterns, but **NEVER trust its method signatures**. For signature verification, ALWAYS use `ilspycmd` on the installed DLLs.

| Step | Action | When |
|------|--------|------|
| 1. **Read decompiled source** | `Read` or `Grep` in `E:\Decompiled_Bannerlord\` | Understanding patterns, finding classes — but signatures may differ from installed v1.3.15 |
| 2. **Verify signatures** | `ilspycmd "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/<dll>" -t "<type>"` | **ALWAYS** before overriding methods, creating patches, or calling APIs |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Fallback if type not found via ilspycmd |

**Decompiled source layout** (`E:\Decompiled_Bannerlord\`):

| Folder | Contents |
|--------|----------|
| `Campaign/` | `TaleWorlds.CampaignSystem` — GameModels, behaviors, actions (1,556 files) |
| `MountAndBlade/` | `TaleWorlds.MountAndBlade` — missions, agents, game logic (1,977 files) |
| `Modules/` | `SandBox`, `StoryMode` — module behaviors, views (1,362 files) |
| `Core/` | `TaleWorlds.Core`, Library, SaveSystem, Localization (666 files) |
| `Engine/` | Engine, InputSystem, ScreenSystem, Navigation (386 files) |
| `UI/` | GauntletUI, PrefabSystem, PSAI (285 files) |
| `Network/` | Diamond, Network, PlayerServices (147 files) |
| `Platform/` | PlatformService, Achievements, ModuleManager (69 files) |
| `Launcher/` | Launcher.Library, Launcher.Steam (40 files) |
| `ThirdParty/` | Newtonsoft.Json, Steamworks.NET, jose-jwt (1,081 files) |

**Quick lookup examples:**
```bash
# Find a class
find "E:/Decompiled_Bannerlord/" -name "DefaultPartyWageModel.cs"

# Search for a method across all decompiled source
grep -r "GetCharacterWage" "E:/Decompiled_Bannerlord/Campaign/"

# Browse a namespace
ls "E:/Decompiled_Bannerlord/Campaign/TaleWorlds.CampaignSystem/TaleWorlds/CampaignSystem/GameComponents/"
```

**DLL path** (for ILSpy fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

### Configuration

Project-level MCP servers (Serena, GitHub, filesystem, git, ilspy) are configured in `.mcp.json` at the project root and must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted. User-level servers (sequential-thinking, context7) are configured in `~/.claude/.mcp/user.json` and enabled globally.

## Hooks

| Hook | Event | Purpose |
|------|-------|---------|
| `check-build-before-commit.sh` | PreToolUse (Bash) | Blocks `git commit` if build fails |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md |
| `session-start.sh` | SessionStart | Prints branch, recent commits, CHANGELOG summary on startup |
| `pre-compact.sh` | PreCompact | Dumps modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit logs agent invocations to `.claude/logs/agent-audit.log` |
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to CLAUDE.md, Directory.Build.props, ADRs without explicit request |
| `suggest-compact.sh` | PreToolUse (*) | Suggests `/compact` after 50 tool calls, then every 25 |
| `mcp-health-check.sh` | PreToolUse (mcp__*) | Blocks MCP calls to servers marked unhealthy in last 60s |
| `mcp-health-mark.sh` | PostToolUseFailure (mcp__*) | Marks MCP server unhealthy after failed tool call, 60s backoff |
| `check-deep-review.sh` | Stop | Reminds to run `/deep-review` if real work was done |
| `post-compact.sh` | PostCompact | Reminds Claude to re-read MEMORY.md + in-flight files after compaction |
| `detect-docs-gaps.sh` | SessionStart | Flags `Main/Features/<X>` directories with no matching `docs/features/*.md` |
| `validate-push.sh` | PreToolUse (Bash) | Warns on push to master/main; hard-blocks force push to protected branches |
| `check-changelog-changed.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when `.claude/`, `CLAUDE.md`, or `AGENTS.md` is staged but `CHANGELOG.md` is not. Catches the recurring "forgot to update CHANGELOG" process violation. |
| `check-claude-files-tracked.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the gitignore-blast bug (`bin/check-freeze.sh` shipped non-functional in efbde5b). |
| `session-stop.sh` | Stop | Appends commits + modified files to `.claude/logs/session-log.md` |

## Hook Response Contracts

When these hooks fire, Claude must respond as specified — not just read the output.

| Hook | Expected Response |
|------|------------------|
| `post-compact.sh` | Immediately `Read` MEMORY.md and each file listed under "Files in flight" before resuming work. Do not continue from transcript memory alone — the file is the source of truth. |
| `detect-docs-gaps.sh` | Mention the gap list once to the user ("I noticed these features have no feature doc: ..."). Do NOT auto-create docs. Wait for user direction — they may have a reason the gap exists. |
| `validate-push.sh` (blocked) | Never retry with `--no-verify` or downgrade to a non-force push silently. Explain the block and ask the user whether to push to a non-protected branch instead. |

## Status Line

`.claude/statusline.sh` renders `ctx: N% | model | branch | Ns/Nu/Nt` (staged/unstaged/untracked counts, omitted when clean). Registered in `settings.json → statusLine`.

## Notes

- Use `/reload-plugins` to pick up new or modified skills without restarting Claude Code

- Target: Bannerlord v1.3.15 (installed game version)
- **WARNING:** `E:\Decompiled_Bannerlord\` is v1.4 — DO NOT use for signature verification. Use `ilspycmd` on installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\` instead.
- Migration from v1.2 requires API changes - see `docs/migration/`
- Future: Refactor to v1.4 when game installation is updated
- No git actions unless explicitly asked

## PowerShell Tool (Windows)

Opt-in preview (requires v2.1.78+). Runs PowerShell natively instead of routing through Git Bash.

**Enable:** Add to `settings.json` env block:
```json
"CLAUDE_CODE_USE_POWERSHELL_TOOL": "1"
```

**Additional settings:**
| Setting | Location | Effect |
|---------|----------|--------|
| `"defaultShell": "powershell"` | `settings.json` | Routes `!` commands through PowerShell |
| `"shell": "powershell"` | Hook definition | Runs that hook in PowerShell |
| `shell: powershell` | Skill frontmatter | Runs code blocks in PowerShell |

**Limitations:** No auto mode, no profile loading, no sandboxing, Windows-only (not WSL), Git Bash still required to start Claude Code.

## Equipment & Armory

| Item | Details |
|------|---------|
| **Armory dependency** | `LOTRLOME_Armory` (NOT `Armory_2` — it will be deleted) |
| **Item definitions** | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<culture>\` |
| **Item files per culture** | `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `arm_armors.xml` |
| **Global items** | `LOTRLOME_items\LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml`, `LOTRAOM_horses.xml` |
| **Gondor prefix** | `sk_gd_ano_` (Anorien), `sk_gd_mns_` (Minas Tirith), `sk_gd_osg_` (Osgiliath), `sk_gd_cair_` (Cair Andros), `sk_gd_ith_` (Ithilien) |

**Validation:** When adding/changing equipment, always verify item IDs exist in Armory. Characters appear in underwear when items are missing. Cross-reference with `grep -o 'id="[^"]*"' <armory-file>` to get valid IDs.

## Rebalancing Tools

| Tool | Purpose | CLI |
|------|---------|-----|
| `tools/complete_lords_xslt.py` | Make all vanilla lord attributes explicit in XSLT | `--dry-run`, `--apply`, `--export-csv` |
| `tools/rebalance_lords.py` | Balance lord skills (XSLT + XML) via baseline + cultural mod + age | `--dry-run`, `--apply`, `--export-csv` |
| `tools/rebalance_troops.py` | Balance troop skills | `--dry-run`, `--apply` |
| `tools/rebalance_armor.py` | Balance armor stats | `--dry-run`, `--apply` |
| `tools/rebalance_weapons.py` | Balance weapon stats | `--dry-run`, `--apply` |
| `tools/generate_gondor_armor.py` | Phase-1 Gondor armor item author (Anorien/MT/Osg/Cair/Ith) — writes to `lotraom-assets` | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_gondor_armor_phase2.py` | Phase-2 author for 8 missing Gondor families (Lossarnach/PG/Har/Anf/Sere/Leb/Bel/Lam) — defaults to Steam install (issue #99) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/apply_gondor_troop_revamp.py` | Mechanical EquipmentRoster swap for 107 Gondor troops + delete orphan blocks (issue #99) | `--dry-run`, `--apply` |
| `tools/validate_gondor_refs.py` | Underwear-bug gate: cross-checks every `sk_gd_*`/`sk_dg_*` reference in `troops_gondor.xml` against Armory IDs | (no flags) |
