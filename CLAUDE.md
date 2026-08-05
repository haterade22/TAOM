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

Frontmatter hooks fire ONLY while their skill is invoked — a state file written from another context is inert (invoke `/freeze`, don't simulate it); global enforcement belongs in `.claude/settings.json`. Facts + the `/investigate` cross-skill exception: `.claude/rules/harness-facts.md` "Hook lifecycle".

### Edit scope discipline

**Every changed line should trace directly to the user's request.** Don't "improve" adjacent code, comments, or formatting just because you're already in the file. A bug fix doesn't reformat the surrounding method. A new feature doesn't rename pre-existing variables. If a refactor is worth doing, it's worth its own PR — surface it after the requested change is done, don't smuggle it in. (Source: karpathy-skills `surgical-changes`.)

**Convert vague asks into testable objectives BEFORE the first Edit.** "Fix the bug" is not a task — "write a failing test that reproduces the NRE, make it pass, verify no regressions in `TAOM.Tests`" is. State the pass/fail criterion up front so the work has a defined end. This is what `/investigate` Phase 1 and `/verify` enforce locally; the same discipline applies to any non-trivial change, not just debugging. (Source: karpathy-skills `goal-driven-execution`.)

See also `.claude/rules/think-before-coding.md` (always-load) for the assumption-surfacing companion rule that fires before the first Edit on non-trivial requests.

### Native C++ port discipline

Porting C++ from an upstream mod? "Upstream worked" != fit to ship. The 6-point audit checklist (hot-path logging, SEH filter specificity, inter-function offsets, atomic counters, SRWLock balance, `/deep-review` with C++ in scope) lives in the paths-scoped rule `.claude/rules/native-cpp-ports.md` (loads when editing `Dependencies/**/*.cpp|h` or `Main/SceneScripts/**`). RCA: `docs/reviews/rca-native-skin-fixes-port-2026-05-26.md`.

## Skills (Slash Commands)

All 42 skill **descriptions already load eagerly** into every conversation (that is the
skill-listing you see) — so a table of them here is a second, drifting copy. Invoke a skill with
`/name`; the routing table below ("Skill Routing") says *when*. Full per-skill workflow lives in
each `SKILL.md`.

### Workflow → Skill convention

**Every recurring, multi-step workflow becomes a skill** when it is (a) repeatable, (b) multi-step or chains skills, and (c) carries TAOM-specific gotchas. The skill is a thin entry point pointing at the authoritative doc. **Do NOT skill-ify** one-offs, pure reference, or single commands — descriptions load eagerly into every conversation, a permanent context tax. New skills: description <=30 words, follow `.claude/rules/external-skill-ports.md`, register in the routing table below, update CHANGELOG.

## Skill Routing (when to invoke what)

When the user's message matches one of these patterns, **proactively invoke** the listed skill via the Skill tool. The skill has structured workflows, gates, and TAOM-specific patterns that produce better results than ad-hoc help. (Note: invoking the Skill tool still respects the user's tool-permission settings — the user may see a confirmation prompt unless allowlisted.)

### Strong proactive-invoke triggers

| User intent / phrase | Invoke | Confidence gate |
|----------------------|--------|-----------------|
| "this is broken", "why isn't this working", "it was working yesterday", crash logs, stack traces, exceptions from a TAOM patch or service | **`/investigate`** — never debug ad-hoc; the Iron Law is non-negotiable | None — always |
| Native CTD: `0xC0000005` / `AccessViolationException` with the stack dying in `TaleWorlds.Native.dll` (or any native module), "crashed to desktop" with no managed culprit | **`/native-crash-triage`** — Event Log offsets discriminate sites across runs; never blind-retry a native AV | None — always. `/investigate` hands off here when the trail goes native |
| "add a new creature", "make X rideable", "new mount", a creature troop/mount feature kickoff | **`/new-creature-mount`** — warg parity is law; parity-audit-first beats per-crash debugging | Skip if the user is just sketching aloud — invoke when they say "do it" |
| Session-start hook prints "GAME VERSION DRIFT", Steam updated Bannerlord, "the game updated", deliberate engine migration | **`/engine-bump`** — preserve the decompile baseline BEFORE regenerating; compare Event Log offsets before attributing crashes to your changes | None — always, and BEFORE trusting any test run |
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
| User shares an external repo/article/skill/plugin to evaluate for adoption ("review this repo", "next repo", pastes a GitHub URL or a SKILL.md) | **`/adopt-external`** | None — always. Security-vet first: foreign `.claude/` trees get `python tools/audit_claude_config.py --root <clone> --external` BEFORE porting. |
| Before `/ship`, after editing hooks / MCP servers / `settings*.json` permissions / CLAUDE.md, or after porting external `.claude/` config | **`/security-scan`** | Skip for routine feature edits. Foreign-skill vetting uses `--root <clone> --external` (see `/adopt-external`). |
| "audit the whole repo", "what's worth doing / what should we build next", "write a handoff plan for X" | **`/improve`** | Repo-wide / proactive asks only — change-scoped C# review stays with `/deep-review`; build+test gating with `/verify`. |

### Soft suggest (offer, don't auto-invoke)

| Situation | Suggest |
|-----------|---------|
| User says "only fix this", "don't touch X", "stay in this folder", "I'm starting a refactor across many files", or starting a focused fix on one feature | Offer **`/freeze`**: *"Want me to scope-lock edits to `<dir>` so I can't drift?"* |
| User starts a long debug session manually (multi-step trace, repro attempts) without invoking `/investigate` | Suggest **`/investigate`**: *"This looks like root-cause debugging — want to use `/investigate`? It auto-locks scope and enforces the Iron Law."* |
| Done with the focused fix; freeze boundary still active. Triggers: "I'm done with that", "release the boundary", "let me work elsewhere now", "remove the freeze" | Offer **`/unfreeze`**: *"Boundary still set to `<dir>`. Release it?"* |
| About to ship a feature, "let's get this merged", "ready to PR", "send it" | Offer the ship sequence: **`/verify`** → (`/codex-verify` or `/review-codex`) → close issue → update CHANGELOG |
| User asks "what's the migration status" or mentions v1.2 → v1.3 work | Offer **`/migration-status`** |
| Finishing a user-facing doc / README / longform issue or PR body, or the user asks to "humanize" / "clean up the writing" on a finished artifact | Offer **`/humanizer`**: *"Want me to deep-clean the AI-writing tells out of this? (keeps the em-dash/boldface house style)"* — soft offer only; `ai-prose-style.md` already governs new prose as you write |

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

Full catalog (rule → scope → content, 15 path-scoped + 7 always-load):
**[`docs/reference/rules-catalog.md`](docs/reference/rules-catalog.md)**. The always-load rules'
full text is already in context every session; the path-scoped ones load when you open a matching file.

## Custom Agents

| Agent | Purpose |
|-------|---------|
| `taleworlds-researcher` | Decompile and analyze TaleWorlds DLLs |
| `feature-builder` | Build features following TAOM architecture |
| `debugger` | Generic debugging for non-TAOM-specific issues (tooling, scripts, CI). Use `/investigate` for TAOM C# bugs. |
| `error-detective` | Cross-system error correlation when one root cause manifests as multiple symptoms across features. |
| `refactoring-specialist` | Behavior-preserving structural refactoring (extract/rename/move). Use `/deslop` for redundant-code deletion. |

### Briefing subagents (spawn-prompt convention)

A subagent runs in its own context with a **strict tool allowlist** and does NOT reliably inherit this CLAUDE.md, the `.claude/rules`, or skill descriptions (the Claude Code docs don't guarantee it). The 5 custom agents above carry their own "Execution model" block; **ad-hoc agents (`Explore` / `Plan` / `general-purpose`) have no body — so YOU, the orchestrator, must put the briefing in the spawn prompt.** Every non-trivial Task/Agent prompt should include:

1. **"Read [docs/ai-includes/agent-operating-manual.md](./docs/ai-includes/agent-operating-manual.md) first"** — the execution model + tool/skill catalog.
2. **"You cannot invoke skills or spawn agents — recommend them in your report; the orchestrator runs them."** (No subagent has the `Skill`/`Task` tool.)
3. **The relevant tool reminder** — e.g. "for TaleWorlds signatures use `pwsh tools/taom-src.ps1 path <Type>`"; "build/test with `dotnet … -p:DisableModuleCopy=true`, not `./build.ps1`".
4. **Explicit scope** — which dirs/files are in bounds; `Main/IoC.cs` / `Main/SubModule.cs` are single-owner (recommend edits, don't make them).
5. **Which convention docs to read** — point at the specific `docs/ai-includes/*` or `.claude/rules/*` for the task, since they may not have auto-loaded.
6. **For PARALLEL builders: pin shared sub-problems once.** Any sub-problem appearing in ≥2 briefs (id normalization, NaN handling, validation invariants, hot-path patterns) gets ONE prescribed solution in the shared contracts — per-builder judgment diverges at the seams. Rule: `.claude/rules/harness-facts.md` "Parallel builder briefs"; the CombatMechanics seam findings behind it: `docs/ai-includes/agent-teams.md` "Case studies".

Pure read-only research agents (`Explore`, `Plan`) need only items 1–2 + scope; building/editing agents need all five (six when fanning out in parallel).

When you dispatch a subagent to **implement then review** work, follow the two-stage review ordering (spec compliance before code quality) in [agent-teams.md](./docs/ai-includes/agent-teams.md#subagent-review-ordering-verify-spec-before-quality).

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

**Start here:** [docs/INDEX.md](./docs/INDEX.md) — curated topical map across the feature docs,
ADRs, reviews, ai-includes, and migration docs. Topology queries: `/doc-graph`. Knowledge-base
architecture: [ADR-010](./docs/adrs/010-knowledge-base-architecture.md).

| Need to... | Read |
|------------|------|
| Write tests / TDD workflow | [tdd-enforcement.md](./docs/ai-includes/tdd-enforcement.md) |
| Understand architecture / patterns | [architecture.md](./docs/ai-includes/architecture.md) · [patterns.md](./docs/ai-includes/patterns.md) |
| Research TaleWorlds mechanics | [taleworlds-research-guide.md](./docs/ai-includes/taleworlds-research-guide.md) |
| Engine process ("how does X work") | [docs/reference/engine/](./docs/reference/engine/) — 19 subsystems |
| Brief / spawn a subagent | [agent-operating-manual.md](./docs/ai-includes/agent-operating-manual.md) |
| Lessons-learned (read before touching a subsystem; append after every RCA) | [LESSONS-LEARNED.md](./docs/reviews/LESSONS-LEARNED.md) → `docs/reviews/lessons/<category>.md` |
| Add/update translations | [TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md) |
| Author culture armor / troop tree | [new-culture-authoring.md](./docs/ai-includes/new-culture-authoring.md) |
| Add a rideable creature mount | [creature-mount-authoring.md](./docs/ai-includes/creature-mount-authoring.md) |
| Migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |

Full task-oriented lookup (all 46 rows — lord skills, weapon creation, armory pipeline, scene
refs, lord perks, settlement economy, mesh-ref validation, co-op + dedicated server, every
engine-process doc):
**[`docs/reference/doc-lookup.md`](docs/reference/doc-lookup.md)**.

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

| Doc | When to update | Path |
|-----|---------------|------|
| **CHANGELOG.md** | Every session | `CHANGELOG.md` |
| **CLAUDE.md** | New features add their row to `docs/reference/feature-map.md` (Key Paths holds no per-feature rows since the Tier-2 restructure) — here, only a Traps/registry row a crash-triage reader needs, <=400 chars; prose goes in the feature doc (linter-enforced budget) | `CLAUDE.md` |
| **ADRs** | Architectural decisions | `docs/adrs/` |
| **Migration tracking** | Migration tasks | `docs/migration/TRACKING.md` |
| **GitHub Issues** | Every feature, bug, crash, system fix | `gh issue create/close` |
| **Feature docs** | Every completed feature | `docs/features/<name>.md` |

## GitHub Issue & Knowledge Base Requirements (MANDATORY)

- **Every feature/bug/crash gets a GitHub issue**, created BEFORE implementation (retroactive only as repair). Exhaustive bodies — bug: Problem/Analysis/Solution/Files/Testing; feature: Motivation/Design/Implementation/Testing. Label, reference in commits, `gh issue close` when verified.
- **Every completed feature gets `docs/features/<name>.md`** (from TEMPLATE.md), detailed enough that a future session needs ZERO decompilation or re-analysis for conceptual understanding.
- **Completion workflow (every C# feature, no exceptions):** `/verify` -> `/deep-review` + fix (HIGH in-session) -> `/review-codex` (auto-dispatch) + verify/fix -> `/review-codex` self-review pass -> final `/verify` -> issue (must exist BEFORE the closing commit) + feature doc + CHANGELOG. `/ship` orchestrates it; full templates + 13-step sequence: [completion-workflow.md](docs/ai-includes/completion-workflow.md).
- **Process docs:** `docs/reviews/REVIEW-GUIDE.md` (prompt templates), `docs/reviews/REVIEW-LOG.md` (scoring history).

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
| **taom-moduledata** | Project | Query TAOM ModuleData integrity (validate, item/troop/culture exists, find-references, list cultures/schemas) — wraps `tools/taom_query.py`. Needs the `mcp` SDK; restart Claude to load. See `docs/features/moduledata-validation.md`. | `.mcp.json` |
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
| Validate/query TAOM ModuleData (does item/troop/culture id exist? what references id X? are there broken refs?) | **taom-moduledata** MCP (`validate_moduledata`, `item_exists`, `troop_exists`, `culture_exists`, `find_references`, `list_cultures`) — or `python tools/validate_moduledata.py` if the MCP isn't loaded | Grep ModuleData / hand-rolling a one-off ref validator |
| Research before implementing | **`taom-src path <Type>`** for signatures, **Read/Grep** decompiled source for browsing patterns, **Serena** for symbol nav, **ilspy** MCP as fallback | Manual decompilation workflow |

### TaleWorlds Research — Lookup Order

**Always use `taom-src` first.** It runs `ilspycmd` against the installed DLLs (version auto-detected from `Version.xml`) and caches under `~/.taom-src/<version>/`. The `E:\Decompiled_Bannerlord\` dump matches the pin and is fine for browsing namespaces/patterns; for authoritative signatures prefer `taom-src` against the installed DLLs (the dump can lag after an engine bump).

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](./docs/reference/engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles the installed (v1.4.7) DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call) — authoritative; run after you understand the process conceptually |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the dump | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |

See `.claude/skills/taom-src/SKILL.md` for full usage. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout:** `E:\Decompiled_Bannerlord\` category tree = the SHIPPING-CLIENT decompile (STRIPS editor-only code — "absent from the dump" != "doesn't exist"; editor-only types live in the `{_shipping_build,_editor_build}` dual-build). Folder map, builds, native-DLL inspection: [bannerlord-engine-and-toolchain.md](docs/reference/bannerlord-engine-and-toolchain.md).

**DLL path** (for ILSpy MCP fallback): `E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client\` (shipping). **Editor build = `…\bin\Win64_Shipping_wEditor\`** — same-named DLLs with editor-only types compiled in.

### Configuration

Project-level MCP servers (Serena, GitHub, filesystem, git, ilspy, taom-moduledata) are configured in `.mcp.json` at the project root and must be listed in `.claude/settings.local.json → enabledMcpjsonServers` to be trusted. (`taom-moduledata` is TAOM-authored — `tools/taom_mcp_server.py` — and requires the `mcp` Python SDK; a Claude restart is needed to pick up a newly-added server.) User-level servers (sequential-thinking, context7) are configured in `~/.claude/.mcp/user.json` and enabled globally.

## Hooks

25 hooks across 9 events. Full catalog (hook → event → purpose):
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

Armory dependency is **`LOTRLOME_Armory`** (NOT `Armory_2`). Item defs live under
`.../LOTRLOME_Armory/ModuleData/LOTRLOME_items/<folder>/`.
**Before authoring an item: grep ALL `LOTRLOME_items/*/` for the id prefix — the first folder
that already holds that prefix is the canonical home; a different folder = silent duplicate-ID
shadowing** (e.g. `sk_dwarf_iron_*` lives in `iron_hills/`, not `erebor/`). Full canonical-folder
table + Gondor prefixes + CC facegen rule: **`/author-armor`** +
[`docs/reference/armory-guide.md`](docs/reference/armory-guide.md).
Validation: `python tools/validate_all_troop_refs.py` (missing item IDs → characters in underwear).
**Shields:** `item_usage="hand_shield"` requires `ForceAttachOffHandPrimaryItemBone`, `item_usage="shield"` requires `ForceAttachOffHandSecondaryItemBone` — never both. Two `body_name`s in `LOTRAOM_shields.xml` look mistyped and must NOT be "fixed" (the asset is packaged under the misspelling): [`docs/reference/armory-shield-audit.md`](docs/reference/armory-shield-audit.md).

## Rebalancing & Data Tools

Full catalog with per-tool gotchas: [`tools/README.md`](tools/README.md) (Validation · Save-repair · Content Generation · Rebalancing · Lords & Equipment · Troop revamps · Settlements · Localization · One-offs). Preferred validators: `python tools/validate_moduledata.py` (+ the `taom-moduledata` MCP server). Balance passes: run the read-only `analyze_*` overview BEFORE any `rebalance_* --apply`.
