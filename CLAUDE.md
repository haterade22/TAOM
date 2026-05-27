# CLAUDE.md

Bannerlord 1.4 total conversion mod (TAOM - Tales From the Age of Men)

> **Target: Bannerlord 1.4.5.** Migration from v1.3.15 landed 2026-05-22 (S0–S5b complete: adapters, GameModels, equipment XML migration, roster authoring). Formal validation stages S6–S12 (smoke test, per-tier feature validation, Codex review, closeout) were rolled into ongoing feature work on the `bannerlord-1.4.5` branch rather than executed as discrete gates — see [`docs/migration/TRACKING.md`](docs/migration/TRACKING.md) for the audit trail and [`docs/migration/v1.4.x-overview.md`](docs/migration/v1.4.x-overview.md) for the original plan.

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
| **Research First** | Never guess TaleWorlds behavior - check `E:\Decompiled_Bannerlord\` for concepts, but **verify signatures via `ilspycmd` on installed DLLs** (decompiled folder and installed DLLs are both v1.4.5; `ilspycmd` on installed DLLs remains authoritative) |
| **Verify Before Reference** | Before writing `Sprite="X"` read `TAOMSpriteData.xml`. Before `PrefabExtension` injection, decompile vanilla target to check child assumptions. Before `IoC.Resolve` in hot path, use lazy cache. |
| **`/deep-review` Mandatory** | Run before EVERY commit touching C# — catches adapter violations, v1.4 incompatibilities, missing tests, data flow gaps |

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

### Native C++ port discipline

**When porting C++ code from an upstream mod into TAOM (`Dependencies/*.NativeHooks/`, scene scripts, or any other vendored native), audit the port from scratch. "Upstream worked" only means "produced correct output" — it does NOT mean the port is fit to ship in TAOM.**

The recurring failure mode: architectural changes (rename functions, change export signatures, retarget output path) consume the audit budget; behavioral preservation (logging, exception handling, lock balance) flies through unaudited. Three review findings on the NativeSkinFixes port (2026-05-26) traced to this — all inherited verbatim from the upstream Nexus mod. See `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

Before you commit a C++ port, walk this checklist:

1. **Hot-path logging.** Every `OutputDebugString` / `fprintf` / `fputs` / `LogLine` call inside a function the engine calls per-frame / per-Face_mesh / per-agent must be sample-gated (atomic counter + summary on uninstall). Upstream debug logging is rarely audited; it's almost always a HIGH finding when it survives the port.
2. **SEH filter specificity.** `__except (EXCEPTION_EXECUTE_HANDLER)` is a code smell. Use `GetExceptionCode()` to narrow to the specific class you expect (typically `EXCEPTION_ACCESS_VIOLATION`); let heap corruption / stack overflow propagate to the OS crash dumper.
3. **Inter-function offsets.** Any computation like `helperFunc = mainFunc - 0xF6A0` is fragile across engine versions and must be replaced with an independent signature scan. The original NativeSkinFixes had one such offset for `NotifyPhysics`; the port replaced it with a 7th pattern.
4. **Atomic counters.** Any `static int counter` touched from a hook body is racy if the engine fires on multiple threads. Use `volatile LONG64` + `InterlockedIncrement64`.
5. **SRWLock balance.** Reads use `AcquireSRWLockShared`; writes use `AcquireSRWLockExclusive`. Verify both sides — upstream mods routinely take the exclusive lock for reads ("just in case") and silently serialise everything.
6. **`/deep-review` with C++ in scope.** The skill prompt now includes C++ HOT-PATH CHECKS and C++ Native Hook Standards blocks that fire automatically when `.cpp`/`.h` files are in the changeset (per the post-mortem on the same RCA). Run it.

This is a project-level discipline, not a one-off feature note — every future C++ port has the same risk profile. See `feedback_native_port_hot_path_audit.md`, `feedback_seh_filter_specificity.md`.

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
| `/codex-verify [feature]` | Dispatch lightweight Codex verification directly via `codex exec` (5-20 min); harness notifies on completion |
| `/review-codex` | Heavyweight adversarial review: write prompt, dispatch Codex directly via `codex exec` (10-45 min), auto-verify findings + implement fixes when notification arrives |
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

**Start here for any doc question:** [docs/INDEX.md](./docs/INDEX.md) — curated topical map across all 70 feature docs, ADRs, reviews, ai-includes, and migration docs. Knowledge-base architecture: [ADR-010](./docs/adrs/010-knowledge-base-architecture.md).

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
| Update BUTR/MCM/ButterLib dependencies | [migration/dr3-maintenance.md](./docs/migration/dr3-maintenance.md) — version pinning, Steam Workshop fallback, smoke test, risk scenarios |
| Use agent teams | [agent-teams.md](./docs/ai-includes/agent-teams.md) |
| Author a new culture's armor + troop tree (end-to-end) | [new-culture-authoring.md](./docs/ai-includes/new-culture-authoring.md) — phases, helpers, color convention, iteration loops |
| Add or fix lord skills + traits (any culture, any canonical character) | [lord-skills-authoring.md](./docs/ai-includes/lord-skills-authoring.md) — TAOM SkillSet system, archetype catalog, per-NPC override recipes, gotchas |
| Plan future GameModel overrides | [roadmap.md](./docs/roadmap.md) |
| Add or update translations | [TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md) + [tools/README.md](./tools/README.md#localization-pipeline) |

## Localization

12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) × 3 modules (TAOM, TAOM_Map, LOTRLOME_Armory) = ~10K strings per language. PL is community-hand-translated; the other 11 have AI first-draft translations (Claude Sonnet 4.5 via `tools/translate_with_claude.py`).

| Component | Location | Notes |
|-----------|----------|-------|
| **Translator-facing guide** | [docs/localization/TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md) | Full workflow, AI pipeline, manual fallback, Tolkien naming conventions |
| **Source loc XMLs** (English defaults + translator's discoverable key list) | `Main/_Module/ModuleData/taom_*_strings.xml` (×6) + `taom_xslt_strings.xml` | 7 source files, ~6,238 entries. Each entry uses `text="{=KEY}default"` format. |
| **Per-language translation files** | `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml` | 7 files per language. Engine auto-discovers via `language_data.xml`. |
| **External module translations** | `<game>/Modules/TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`, `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/<LANG>/loc_*.xml` | Not in repo (deployed straight to game install). |
| **Translation tools** | [tools/translate_with_claude.py](./tools/translate_with_claude.py), [tools/rebuild_translation_files.py](./tools/rebuild_translation_files.py), [tools/generate_translation_template.py](./tools/generate_translation_template.py), [tools/translation_status.sh](./tools/translation_status.sh) | See [tools/README.md](./tools/README.md#localization-pipeline). |
| **Overrides** (hand-curated canonical translations) | `tools/translation_overrides/<lang>.json` | E.g., Russian Tolkien names: Бродяжник, Мордор. Always wins over LLM. |
| **Cache** (machine-translated, resumable) | `tools/translation_cache/<lang>.json` | Git-tracked. Re-runs free. ~700KB-1.3MB per lang. |
| **Validation tests** | [TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs](./TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs) | Enforces 7 LanguageFile refs per language, well-formed XML, no missing files. |

**When adding new TAOM C# code that displays text to the player:** wrap with `{=KEY}default` format (e.g., `new TextObject("{=taom_my_feature_label}My Feature")`) and add the key to `taom_module_strings.xml`. Then re-run the translation tool to propagate to all 11 AI-translated languages.

**When adding new source XML files for in-game text:** add the file path to `SubModule.xml` as a `<XmlNode><XmlName id="GameText" path="..."/>` entry, add a `<LanguageFile>` reference in all 12 `language_data.xml` files, create empty stubs per language, and bump the `LanguageDataXmlTests.HaveExactlyXLanguageFiles` test count.

**When XSLT files inject new text with `{=KEY}default`:** the keys need to be harvested into `taom_xslt_strings.xml` (see commit `20713a1` for the precedent). Run `tools/translate_with_claude.py` after the harvest to propagate.

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
| NativeSkinFixes | `Main/Features/NativeSkinFixes/` (managed wrapper for `TAOM.NativeSkinFixes.dll` — covers_head morph fix + hair/beard cloth simulation. 3 P/Invoke interop classes + installer. Editor-mode skip. Loaded from `OnBeforeInitialModuleScreenSetAsRoot`, uninstalled from `OnSubModuleUnloaded`. C++ source at `Dependencies/NativeSkinFixes.NativeHooks/`. See `docs/features/native-skin-fixes.md`.) |
| SiegeDefense | `Main/Features/Siege/` (timed defense events when watched factions are besieged; config-driven watched factions, CampaignTime deadline, relation+influence reward on arrival) |
| SpecialResources | `Main/Features/SpecialResources/` (11 resources across 18 kingdoms — War Spoils/Gems/Castar/Marks/Elven Wine/Lake Fish/War Drums/Tribal Relics/Dunlending Ale/Plunder/War Banners; XML-driven with many-to-one kingdom/culture mappings, shared balance, pending transaction upgrades, desertion at 0, notifications, Patch26, composite `heroId:resourceId` storage) |
| CareerSystem | `Main/Features/CareerSystem/` (career/class progression — 50 careers across 16 cultures; XML-driven career defs, mutation calculator registry, passive service with GameModel integration, ability system, career screen UI via UIExtenderEx, level-based tier gating, SyncData persistence, CC career selection stage, archetype-driven starting equipment override at CC finalize — `CareerArchetype` enum + `ICareerArchetypeService` backed by single static map in `CareerSystemIoC` shared with the ability executor registry; `ICareerStartingEquipmentService` applies `player_career_{culture}_{archetype}_{f\|m}` roster on top of culture default via `FillFrom` slot-merge — non-cavalry archetypes need explicit empty Horse/HorseHarness overrides; Gondor authored end-to-end as proof of life, other 15 cultures fall through gracefully to culture default until authored) |
| SettlementGuards | `Main/Features/SettlementGuards/` (per-settlement guard customization — XML-driven guard troop pools with settlement→clan→culture fallback, spawn-point filtering, weighted random selection, per-culture spear mapping; Harmony prefixes on private GuardsCampaignBehavior methods) |
| NamedCompanions | `Main/Features/NamedCompanions/` (18 lore companions as recruitable wanderers — Aragorn/Legolas/Gimli/etc; `is_hero="true"` + `occupation="Wanderer"`, JSON config for spawn settlements, vanilla dialog integration, race persistence via existing HeroRace system) |
| RevoltTuning | `Main/Features/RevoltTuning/` (JSON-tunable soft-nerf of vanilla revolt mechanic for LOTR's frequent settlement flips; raises loyalty thresholds + dampens different-culture penalties; semantic validation rejects out-of-range / sign-flipped values; consumed by `TaomSettlementLoyaltyModel`) |
| Messengers | `Main/Features/Messengers/` (paid messenger dispatch from encyclopedia + dialog hook; travels for N days, arrival inquiry opens conversation mission with settlement-vs-field routing and player-position restore; random ambush rolls; primitive-dict SyncData; UIExtenderEx prefab extension on `EncyclopediaHeroPage`; ported from LOTRAOM 1.2.12 with 1.3.15 API drift applied; TAOM-owned `MapCoord` keeps service free of TaleWorlds types per ADR-007) |
| QuickActions | `Main/Features/QuickActions/` (replaces inventory "Sell All" button with a 4-option menu — Sell Damaged / Sell Low Value / Unequip All / vanilla; ported from external `TransferbuttonMenu` 1.2.x with v1.3.15 verification removing 8-probe + 5-probe reflection chains; `IInventoryVMAdapter` consolidates inventory VM surface for future EquipPresets reuse; `Patch34_SellAllItemsMenu` Prefix uses thread-static bypass flag so "Sell All (Vanilla)" re-enters vanilla `TransferAll` unmodified; `TrySellItem` sets `SPItemVM.TransactionCount = StackAmount` before invoke for full-stack sells; `TryUnequipAllPlayerSlots` routes through `InventoryLogic.TransferCommand` so vanilla `AfterTransfer` rebuilds rows; per-save `IsSearchAvailable` toggle via `InventorySearchCampaignBehavior` SyncData + Postfix on `SPInventoryVM.RefreshCallbacks`) |
| SmartCavalryAI | `Main/Features/SmartCavalryAI/` (player-team cavalry coordinated line-charge state machine — `ICavalryChargeService` drives Idle→Forming→Charging→PassingThrough→Reforming + Rerouting branch; `Patch31_FormationSetMovementOrder` Postfix; `ICavalryCommandAdapter` + `IBattlefieldQueryAdapter` wrap TaleWorlds APIs; `SmartCavalryRecursionGuard` thread-local depth counter prevents postfix re-entry; v1.3.15 confirmed `SetPositioning`/`SetMovementDirection` are public — no reflection needed; ported with 2 inherited bugs fixed: hardcoded reform-strictness 0.5f override + ungated HUD spam; 4 Codex adversarial findings fixed including NaN propagation through Clamp + cross-feature collision with MixedFormations) |
| CultureMarketplace | `Main/Features/CultureMarketplace/` (daily LOTRLOME item injection into town markets by owner culture — `ICultureItemPoolService` auto-derives culture→items from `MBObjectManager` at first tick + ID-prefix fallback for shields missing `culture=` attribute; optional `culture_marketplace_config.xml` for per-culture blacklist + weight boosts with `FiniteFloatValidator`-guarded weights; `ICultureMarketplaceInjectionService` does weighted-random K=6 draws per `DailyTickSettlementEvent` with per-town distinct-item cap of 60; dynamic owner culture (`town.OwnerClan?.Culture?.StringId`) so conquest immediately shifts market identity; `IItemPoolAdapter` + `ITownRosterAdapter` per ADR-007; no Harmony patch — `Settlement.ItemRoster.AddToCounts(EquipmentElement, int)` is the modifier-preserving entry point; no SyncData, items live in vanilla roster serialization) |
| Scene scripts | `Main/SceneScripts/` (engine-discovered `ScriptComponentBehavior` subclasses for map authors; CS_Road procedural mesh generator + Roads/ pure helpers; clean-room ports from external inspiration via `docs/scene-scripts/specs/` + ATTRIBUTION.md procedure) |
| EditorCacheRebuild | `Main/Features/EditorCacheRebuild/` (parallel + incremental + resumable settlement distance cache rebuild — singleplayer MCM trigger only. `TaomSettings.RebuildDistanceCacheAction` boundary lambda → `IRuntimeCacheRebuildService.Trigger()` → `Task.Run` → `ICampaignSessionAdapter.CreateDefaultRuntimeCacheAdapter()` → `CacheBuilderService.Build` → atomic `File.Replace` write with `.prev` backup → round-trip verification gating success popup. `NavigationCacheAdapter` reflection chain wraps `NavigationCache<Settlement>` via `SandBoxNavigationCache`. `ParallelPhase1Builder` + `ParallelPhase2Builder` use `Parallel.For` + `ConcurrentQueue` with locked dict writes; `SmokeTestGate` validates serial-vs-parallel pathfind equivalence at build start; `CheckpointSerializer` saves state between phases for crash recovery; `SettlementDiffer` + `ChangedSettlementsFilter` enable incremental Phase 1 when ≤30 settlements changed; `ValidationReportWriter` emits per-build JSON; ~108hr full vanilla rebuild → ~7min on TAOM's 863 settlements. Comprehensive logging with build-correlation IDs, environment snapshot, scene CRCs, per-phase memory deltas, first-pair heartbeats, atomic-write integrity diagnostics. Legacy editor-mode Harmony patch removed — was blocked by 3rd-party mod compatibility in editor mode and dormant in singleplayer. Despite the "Editor" in the name, this is now a runtime-only feature; rename deferred. NavalDLC port support tracked at #120.) |
| Warg Combat | `Main/Features/Warg/` (BT elements, WargAttackService, WargMissionBehavior) |
| Vendored Main-module DLLs | `Main/_Module/bin/Win64_Shipping_Client/` — `MinHook.x64.dll`, `TAOM.NativeSkinFixes.dll`. Allowlisted in `.gitignore`. `TAOM.dll` + `TAOM.pdb` stay ignored (build outputs, regenerated each build). MCMv5 is provided by TAOM.Dependencies (`Bannerlord.MBOptionScreen*.dll` + `MCM.UI.Adapter.MCMv5.dll`) + the `Bannerlord.MCM` NuGet — do NOT vendor `MCMv5.dll` here. **As of 2026-05-26, `TAOM.NativeSkinFixes.dll` C++ source is in-repo at `Dependencies/NativeSkinFixes.NativeHooks/` — rebuild with `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`; the `.vcxproj` writes directly into this bin folder. See `docs/features/native-skin-fixes.md`. As of 2026-05-24, the `BehaviorTrees.dll` and `BehaviorTreeWrapper.dll` vendored binaries are gone — their source was decompiled and inlined to `Main/BehaviorTrees/` + `Main/BehaviorTreeWrapper/` and compiles into `TAOM.dll`. RCA: `docs/reviews/rca-looter-battle-nre-2026-05-24.md`. Do NOT re-vendor; edit the inlined source instead.** |
| TAOM.Dependencies stub modules | `Stubs/Bannerlord.{Harmony,UIExtenderEx,ButterLib,MBOptionScreen}/_Module/SubModule.xml` — four alias stubs that declare the standard BUTR module IDs so third-party mods depending on those IDs are toggleable in the vanilla launcher. Each now (DR3 Phase 4 — 2026-05-27) ships a real `<SubModule>` entry referencing `TAOM.Dependencies.AliasStubSubModule` (try/catch-wrapped MBSubModuleBase). Versions use `vX.Y.99.0` strategy to satisfy any reasonable lower-bound version pin. Deployed via `DeployTAOMDependenciesStubs` MSBuild target. When a BUTR `PackageReference` BUMPS its minor (e.g., Harmony 2.4.x → 2.5.x), bump the matching stub to the new minor's `.99.0`. See `docs/migration/dr3-maintenance.md` "Stub modules" + "Maintenance rule (v99 strategy)" sections. |
| TAOM.Dependencies defensive infrastructure | `Dependencies/Foundation/` (DR3 Phase 4 — 2026-05-27). 11 classes that port BetaDeps v0.7.5.1's runtime error-tolerance framework under MIT clean-room rewrite: `RuntimeLog` (path resolver), `DiagLog` (threadsafe append-only logger to `<game>/Modules/TAOM.Dependencies/diag.log`), `ReflectionUtils` (small helpers), `VersionProbe` (Bannerlord version + branch detect), `IncompatibleModDetector` (crash-loop detection via `session-launching.marker` + `last-good-modlist.txt` diff — read-only, no XML mutation), `PatchShield` (Finalizer on every Harmony patch, catches MissingMethod/MissingField/TypeLoad trinity, auto-unpatches offending owner), `SaveShield` + `FailureRecord` + `FailedModsCatalog` (Finalizer on 10 TaleWorlds save/load/mission methods, attributes culprit via stack walk, writes to `failed-mods-catalog.txt`), `SubModuleConstructionGuard` (Harmony Finalizer on MBSubModuleBase ctors), `CollectAssemblyTypesShim` (Finalizer on `Assembly.GetTypes` to handle `ReflectionTypeLoadException`). Wired into `AliasStubSubModule.ctor` (early phase) + `Dependencies/SubModule.OnSubModuleLoad` (late phase) + `OnGameInitializationFinished` (success marker). Opt-out flags: `patchshield-disabled.flag`, `saveshield-swallow-disabled.flag` in the module dir. See `docs/migration/dr3-maintenance.md` "Defensive infrastructure" section. |
| NativeSkinFixes C++ source | `Dependencies/NativeSkinFixes.NativeHooks/` — covers_head morph fix + hair/beard cloth simulation. Standalone `.vcxproj` (NOT in `TAOM.sln`) builds `TAOM.NativeSkinFixes.dll` directly into `Main/_Module/bin/Win64_Shipping_Client/`. Hooks find their targets via byte-pattern scan of `TaleWorlds.Native.dll` at install time (no hardcoded RVAs) — patterns live in `Signatures.h`. MinHook 1.3.4 vendored under `MinHook/`. Rebuild: `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`. See `docs/features/native-skin-fixes.md`. |
| BehaviorTrees library (inlined) | `Main/BehaviorTrees/` — generic BT engine (Selector, Sequence, RandomSelector, Decorators, Tasks, blackboard). No TaleWorlds dependencies. Compiles into `TAOM.dll`. |
| BehaviorTreeWrapper library (inlined) | `Main/BehaviorTreeWrapper/` — Bannerlord/Agent bindings layered over `BehaviorTrees` (`BehaviorTreeMissionLogic`, `BehaviorTreeAgentComponent`, event-subscription listeners). `BehaviorTreeMissionLogic : MissionLogic` (NOT just `MissionBehavior` — see the regression rule in `feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md`). Compiles into `TAOM.dll`. |
| Alliance.Wargs | External module: Monster id="warg", animations, items |
| CC narrative data | `Main/_Module/ModuleData/charactercreation/` (JSON) |
| XML config | `Main/_Module/ModuleData/` |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| Custom lords XML | `Main/_Module/ModuleData/characters/lords.xml` |
| **TAOM_Map settlements (LIVE)** | `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` + `Languages/<LANG>/loc_settlements.xml` × 12. **External module — NOT in repo.** Engine-registered via `TAOM_Map/SubModule.xml`. The repo's `Main/_Module/ModuleData/settlements.xml` is a **STALE SHADOW** (last touched 2026-04-06, NOT registered in Main `SubModule.xml`, position data has diverged); existing PS scripts target it but they don't affect in-game behavior. For live display-name renames use `tools/Apply-MapVillageNames.py`. See [`docs/reference/taom-map-settlement-naming.md`](docs/reference/taom-map-settlement-naming.md). Memory: `feedback_taom_map_live_vs_stale_shadow.md`. |
| SpecialResources config | `Main/_Module/ModuleData/special_resources/` (resource defs + troop costs XML) |
| CultureMarketplace config | `Main/_Module/ModuleData/culture_marketplace/culture_marketplace_config.xml` (optional per-culture blacklist + weight-boost overrides; ships empty — auto-derive from `MBObjectManager` is the default; `FiniteFloatValidator`-guarded weights, NaN/Infinity/negative/>1000 revert to 1.0 with warning) |
| CareerSystem config | `Main/_Module/ModuleData/career_system/` (career defs + choice trees + ability templates + ability tuning XML) |
| RevoltTuning config | `Main/_Module/ModuleData/configs/revolt_tuning_config.json` (4 thresholds/penalties; validated on load) |
| Messengers config | `Main/_Module/ModuleData/messengers/messenger_config.json` (advanced tuning — `accidentChancePerHour` + `travelSpeedMultiplier`; rejects NaN/Infinity/out-of-range; player-facing knobs in MCM `TaomSettings.Messengers`) |
| CareerSystem CC config | `Main/_Module/ModuleData/charactercreation/career_menu.json` (50 career CC skill/attribute bonuses) |
| CareerSystem starter equipment | `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` (per-(culture, archetype, gender) rosters; Gondor only as of 2026-05-19) + LOTRLOME_Armory `LOTRLOME_items/<culture>/starter_armors.xml` (15 starter armor items per culture; remember `covers_legs="true"` on leg items + `covers_hands="true"` on glove items — see `feedback_lotrlome_armor_cover_attributes.md`) |
| CareerSystem sprites | `Main/_Module/GUI/SpriteParts/ui_taom_career_system/CareerSystem/` (portraits 800x400, ability icons 256x256, dedicated atlas) |
| Sprite atlas config | `Main/_Module/GUI/SpriteParts/Config.xml` (sprite category registration with `<AlwaysLoad />`) |
| SettlementGuards config | `Main/_Module/ModuleData/settlement_guards/settlement_guards_config.xml` (per-settlement guard pools, clan/culture fallbacks, spear mappings) |
| VolunteerRecruitment service | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` (per-settlement / per-clan / per-culture pools driving `TaomVolunteerModel.GetBasicVolunteer`. Static-ctor `InitializeXxx*()` methods for hand-written cultures; instance-side lazy `EnsureGondorJsonLoaded` for JSON-driven Gondor. `AddSettlementConditional` + `ConditionalSettlementMap` for state-sensitive pools — predicate evaluated per-lookup against live `VolunteerContext`. Ithil Guard at `town_ES2` only when Gondor-owned is the canonical conditional rule. Feature doc: [docs/features/volunteer-recruitment.md](./docs/features/volunteer-recruitment.md).) |
| VolunteerRecruitment Gondor JSON | `Main/_Module/ModuleData/recruitment_pools/gondor.json` (23 chance groups covering Anórien / Osgiliath / Cair Andros / Lebennin / Pelargir / Lossarnach / Belfalas / Dol Amroth / Linhir / Tolfalas / Lamedon / Calembel / Pinnath Gelin / Arndir / Blackroot Vale / Anfalas / Serelond / Lond Cirion / Harondor / Methir + conditional Ithil Guard rule. Percentages converted to integer weights via *10000; NaN/Infinity/negative entries rejected; fail-closed on unrecognised condition strings. Hand-written `InitializeGondorSettlements` kept as safety net — JSON entries overwrite at runtime; test runs where JSON is missing fall back to hand-written behaviour.) |
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
| `Patch38_SettlementNameplateFade` | Distance-based settlement nameplate fade on the campaign map — Postfix multiplies vanilla target alpha by [0,1] fade factor derived from `DistanceToCamera`. MCM-tunable near/far band (default 80..200), master toggle. Hot path (~3000 calls/sec): service captured once via `Initialize` static-field; settings provider caches `TaomSettings.Instance` reference in its ctor. | `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix) |
| `Patch_MissionTime_SetMovementOrder` | Shared deferred category for `Formation.SetMovementOrder(MovementOrder)` postfixes. Applied once from `OnMissionBehaviorInitialize` (one-shot static guard) because `MovementOrder.cctor` reads `Mission.Current.CurrentTime` — null in `OnSubModuleLoad`/`OnGameInitializationFinished`. Currently houses Patch31_SmartCavalryAI's charge handler and Patch35_CompanionTactics' `CancelStanceOnMove` postfix. **Any future patch with `MovementOrder` in its postfix signature must use this category.** | `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2) |

## Codex Integration

Codex operates as an independent verifier via the local `codex` CLI binary (`C:\Users\mikew\AppData\Roaming\npm\codex.cmd` on Windows). It shares no session context with Claude — providing a genuine second opinion.

**As of 2026-05-25, Claude dispatches Codex DIRECTLY via Bash — no terminal hand-off to the user.** Previous workflow asked the user to run `/codex:adversarial-review --background` in a separate terminal; the new flow uses `codex exec - < prompt.md > output.md 2>&1` from inside the skill (`run_in_background: true`). The user receives one notification when the background job completes and Claude continues automatically. See `.claude/skills/{codex-verify,review-codex}/SKILL.md` "Codex CLI invocation contract" for the full dispatch contract.

| Skill | Purpose | Dispatch model |
|-------|---------|----------------|
| `/codex-verify [feature]` | Lightweight verification (architectural compliance, 5-20 min) | Claude → `codex exec` via Bash, background |
| `/review-codex [feature]` | Heavyweight adversarial review (Known Suspects + vanilla decompile + RCA, 10-45 min) | Claude → `codex exec` via Bash, background |
| `/deep-review [feature] --codex` | Codex pre-review + 5+ Claude agents in parallel | Claude → `codex exec` + parallel `Agent` calls |
| `/codex:rescue [task]` | Delegate investigation to Codex (plugin-based; interactive) | Plugin/`SendMessage` (user prompt) |

**Pre-flight:** every skill that dispatches calls `codex login status` first. If not `Logged in using ChatGPT`, the skill stops and surfaces the message — the user must run `codex login` (interactive browser flow). Claude does NOT attempt to authenticate.

**Config:** `~/.codex/config.toml` (model + reasoning effort; project root has `.codex/config.toml` for project-scoped overrides if needed) | **Instructions:** `AGENTS.md` (project root)

**Completion workflow (MANDATORY for every C# feature, no exceptions):**
1. `/verify` — build + tests pass
2. `/deep-review [feature]` — 5+ parallel Claude agents
3. Fix all confirmed findings (HIGH must be fixed in-session per `.claude/skills/deep-review/SKILL.md` "HIGH findings — no silent deferrals")
4. `/review-codex [feature]` — dispatches Codex via Bash, harness notifies on completion
5. Claude auto-resumes when notification arrives — verify Codex findings, implement confirmed fixes, write Phase 3e RCA
6. `/verify` again — confirm green after fixes
7. Issue + docs + CHANGELOG + final commit

Steps 2-6 are blocking before commit. Past failure mode: the session author skipped 2 and 4 and shipped a 60-file feature with 1 HIGH + 2 MED + 3 LOW deep-review findings (see `docs/reviews/rca-crash-report-2026-05-25.md` meta-finding). With direct dispatch, there's no "I forgot to open the terminal" excuse — invoking the skill IS the dispatch.

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
  2. /deep-review [feature]         — 5+ parallel agents (standards, compat, efficiency, completeness, data-flow)
  3. Fix all confirmed findings (HIGH must fix in-session)

Phase 2: CODEX ADVERSARIAL REVIEW (Claude dispatches directly, no user terminal step)
  4. /review-codex                  — writes prompt to docs/reviews/codex-adversarial-{feature}-{date}.prompt.md
                                      AND dispatches via `codex exec - < prompt.md > output.md 2>&1` (run_in_background)
                                      AND tells the user once: "dispatched, expected window 10-45 min"
  5. (harness notifies on completion — Claude auto-resumes; no /review-codex re-invocation needed)
  6. Verify each Codex finding by reading TAOM source + decompiling vanilla targets — implement confirmed fixes

Phase 3: SELF-REVIEW (review our OWN fixes)
  7. /review-codex                  — second pass, same auto-dispatch flow against the post-fix diff
  8. (harness notifies on completion)
  9. Verify findings on our fixes, implement confirmed fixes

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
| Research before implementing | **`taom-src path <Type>`** for signatures, **Read/Grep** decompiled source for browsing patterns, **Serena** for symbol nav, **ilspy** MCP as fallback | Manual decompilation workflow |

### TaleWorlds Research — Lookup Order

**Always use `taom-src` first.** It runs `ilspycmd` against the installed v1.4.5 DLLs and caches under `~/.taom-src/v1.4.5/` (the script auto-detects the version from `Version.xml`, so old `~/.taom-src/v1.3.15/` caches remain on disk but are unused). The v1.4.5 dump at `E:\Decompiled_Bannerlord\` is fine for browsing namespaces/patterns; for authoritative method signatures still prefer `taom-src` against the installed DLLs.

| Step | Action | When |
|------|--------|------|
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles v1.4.5 DLL on cache miss, returns absolute path | **ALWAYS first** for any signature verification (Harmony patch, GameModel override, adapter, API call) |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the v1.4.5 dump (see folder layout below) | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |

See `.claude/skills/taom-src/SKILL.md` for full usage. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout** (`E:\Decompiled_Bannerlord\` — for browsing only, never signatures):

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

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\`

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

- Target: Bannerlord v1.4.5 (installed game version)
- `E:\Decompiled_Bannerlord\` holds the fresh v1.4.5 dump (re-decompiled 2026-05-22). Browse for patterns; `ilspycmd` on installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\` remains authoritative for signatures.
- Historical migration notes (1.2 → 1.3, 1.3 → 1.4) — see `docs/migration/`
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
| **Item definitions** | `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory\ModuleData\LOTRLOME_items\<folder>\` |
| **Item files per folder** | `body_armors.xml`, `head_armors.xml`, `leg_armors.xml`, `shoulder_armors.xml`, `arm_armors.xml` |
| **Global items** | `LOTRLOME_items\LOTRAOM_weapons.xml`, `LOTRAOM_shields.xml`, `LOTRAOM_horses.xml` |
| **Gondor prefix** | `sk_gd_ano_` (Anorien), `sk_gd_mns_` (Minas Tirith), `sk_gd_osg_` (Osgiliath), `sk_gd_cair_` (Cair Andros), `sk_gd_ith_` (Ithilien) |
| **KEYforce spec drops** | `E:\repos\lotraom-assets\tools\<culture>_armors_and_troops.txt` — per-culture item lists + unit progression specs |
| **CC facegen action_sets** | LIVE at `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml` (TAOM's own copy was removed 2026-05-04). Tracked snapshot at [`docs/reference/lotrlome-armory-snapshot/action_sets.xml`](docs/reference/lotrlome-armory-snapshot/action_sets.xml). Every TAOM-consumed race id MUST have `as_<race>_facegen` + `as_<race>_female_facegen` entries with the full ~106-action surface (copy `as_dwarf_facegen` verbatim, rename `id` + `base_set` only). Slim entries break post-parent CC stages. See [`docs/features/character-creation.md`](docs/features/character-creation.md#lotrlome-as_race_facegen-action_set-requirement-live-in-lotrlome_armory-not-taom) + memory `feedback_lotrlome_action_set_aliases.md`. |

### Armory folder canonical home per item-id prefix

**MANDATORY: before authoring a new item, grep ALL `LOTRLOME_items/*/` subfolders for the prefix.** The first folder that already contains items with that prefix is the canonical home. Adding items to a different folder creates runtime duplicate-ID warnings (engine silently shadows one entry). Even when the spec file is named after culture X, the canonical folder may be a sub-culture (e.g., dwarf items live in `iron_hills/`, not `erebor/`).

| Item prefix | Canonical folder | Notes |
|-------------|------------------|-------|
| `sk_gd_*` | `gondor/` | All Gondor regional items (Anorien through Lamedon) |
| `sk_md_orc_*`, `sk_gn_orc_*`, `sk_uruk_mordor_*`, `ar_ardunian_*` | `mordor/` | Generic orc pool shared across factions also lives here |
| `sk_uruk_hai_*`, `sk_is_orc_*`, `urukscout_*`, `clo_urukscout_*` | `isengard/` | |
| `sk_dg_uruk_*`, `sk_dg_orc_*` | `dol_guldur/` | |
| `sk_dg_khml_*` (Khamul) | `rhun/` | Cross-faction with Dol Guldur — lives in `rhun/` |
| `sk_gb_uruk_*` | `gundabad/` | |
| `sk_dwarf_erebor_*` | `erebor/` | Core dwarven set |
| `sk_dwarf_iron_*` | **`iron_hills/`** | NOT `erebor/` — caught in #211 deep-review (RCA: `docs/reviews/rca-multi-culture-armor-revamp-2026-05-22.md`) |
| `sk_dwarf_dain_*` | `erebor/` | Dain's set |
| `sk_rh_loke_*`, `sk_rh_drag_*` | `rhun/` | Loke-Rim + Dragon-Wrath |

**Validation:** When adding/changing equipment, always verify item IDs exist in Armory. Characters appear in underwear when items are missing. Run `python tools/validate_all_troop_refs.py` to cross-check every `sk_*/ar_*/clo_urukscout_*/urukscout_*` reference across all 7 troop XML files in one pass.

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
| `tools/validate_gondor_refs.py` | Underwear-bug gate (Gondor-only legacy): cross-checks `sk_gd_*` refs in `troops_gondor.xml` against Armory | (no flags) |
| `tools/generate_mordor_armor.py` | Mordor generic orc pool author — `sk_gn_orc_*` (9 helmet shapes) + `sk_md_orc_*` paint chests/pauldrons/bracers/boots (issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_isengard_armor.py` | Isengard `sk_is_orc_*` paint helmets + `clo_urukscout_*` cloth overlays (issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_dolguldur_armor.py` | Dol Guldur `sk_dg_orc_*` paint helmets (Brt+Vgd excluded per spec; issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_erebor_armor.py` | Erebor Iron Hills `sk_dwarf_iron_*` author — parses spec at runtime; **defaults to `iron_hills/` folder** (NOT `erebor/`; issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_rhun_armor.py` | Rhun final Loke-Rim elite helmets — closes the 22-item gap (issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/validate_all_troop_refs.py` | **Generic multi-culture validator** (preferred over `validate_gondor_refs.py`) — cross-checks `sk_*/ar_*/clo_urukscout_*/urukscout_*` refs across all 7 culture troop XMLs (issue #211) | (no flags) |
| `tools/rollback_erebor_iron_misfile.py` | One-off cleanup script: removes mis-filed `sk_dwarf_iron_*` items from `erebor/` (used once during #211 deep-review RCA) | `--dry-run`, `--apply` |
| `tools/apply_mordor_troop_revamp.py` | Mechanical EquipmentRoster swap + 21 new orc/Nurn Warg/Black Uruk troops + 14 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_isengard_troop_revamp.py` | EquipmentRoster swap + 13 new `isengard_orc_*` troops (Section 1 of spec); `orthanc_*` line preserved (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_dolguldur_troop_revamp.py` | EquipmentRoster swap (17 refits) + 12 deletes (old Khamul stubs + berserker line); flexible indent regex (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_gundabad_troop_revamp.py` | EquipmentRoster swap + 1 new `gundabad_bolgs_ironfang` T8 + 4 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_erebor_troop_revamp.py` | EquipmentRoster swap (41 refits) + 13 new `iron_hills_noble_*` troops; 0 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/cleanup_deleted_troops_212.py` | Sweep deleted-troop refs from `taom_partyTemplates.xml`, `troop_weights.xml`, `troop_resource_costs.xml` (issue #212) | `--dry-run`, `--apply` |
| `tools/expand_party_templates_212.py` | Insert new troops into `kingdom_hero_party_<culture>_template` blocks via positional splice (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_gondor_polish_224.py` | **Delta-style** Gondor equipment polish: per-slot `set`/`clear`/`replace` ops + 2 new PG cavalry NPCs + upgrade-target patch (issue #224, distinct from full-roster swap pattern) | `--dry-run`, `--apply` |
