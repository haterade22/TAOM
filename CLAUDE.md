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

Full workflows live in each skill's SKILL.md (descriptions already load eagerly — this table is routing only).

| Command | Purpose |
|---------|---------|
| `/research [Class]` | Decompile + analyze TaleWorlds classes |
| `/new-feature [Name]` | Scaffold feature module (IoC, services, tests) |
| `/issue [bug\|feature\|crash]` | GitHub issue with required TAOM sections |
| `/xslt-check [file]` | Validate XSLT against vanilla XML |
| `/migration-status` | v1.2 -> v1.3 migration progress |
| `/scope-check [change]` | Scope-creep assessment |
| `/build-fix [error]` | Incremental build-error fixes, minimal diffs |
| `/verify [quick\|full]` | Build + test + git pass/fail report |
| `/deslop [path]` | Deletion-first AI-slop cleanup (tests green first) |
| `/new-adr [name]` | Scaffold auto-numbered ADR |
| `/commit-split` | Group changes by concern, commit atomically |
| `/deep-review [feature]` | 5+ parallel review agents; `--codex` adds a Codex pre-review |
| `/codex-verify [feature]` | Lightweight Codex verification (5-20 min) |
| `/review-codex` | Heavyweight Codex adversarial review (10-45 min) |
| `/context-budget [--verbose]` | Token-consumption audit across `.claude/` |
| `/freeze` / `/unfreeze` | Hard directory edit lock / release |
| `/investigate` | 6-phase root-cause debugging (Iron Law; auto-freeze) |
| `/agent-introspection-debugging` | Self-debug failing agent runs |
| `/context-save` / `/context-restore` | Snapshot / restore working context |
| `/skill-stocktake` | Skill + agent quality audit |
| `/verify-bindings [mode]` | Verify Harmony/GameModel/reflection bindings vs installed engine |
| `/ship [feature]` | Orchestrate the mandatory completion sequence |
| `/new-culture [id]` | Culture armor + troop tree + recruitment end-to-end |
| `/lord-skills [name\|culture]` | Lore-driven lord skills + traits (SkillSet system) |
| `/localize [c#\|xml\|xslt]` | 12-language localization propagation |
| `/author-armor [culture]` | LOTRLOME armor items + roster swaps (canonical-folder + cover rules) |
| `/finish-branch [branch] [base]` | Integrate a merge-ready branch into trunk |
| `/adopt-external [url]` | External adoption: security-vet (`audit_claude_config.py --root <clone> --external` BEFORE porting) -> map -> tier -> port-never-install -> review |
| `/security-scan` | Claude-config security audit — own tree, or `--root <dir> --external` for a foreign skill |
| `/doc-graph [mode]` | Docs knowledge-graph topology queries |
| `/improve [mode]` | Whole-repo improvement audit -> handoff plans in `plans/` |
| `/native-crash-triage` | Root-cause native CTDs without symbols |
| `/new-creature-mount [name]` | Rideable creature end-to-end (warg parity is law) |
| `/refine-creature-anim` | Creature locomotion/rider clips in live Blender-MCP |
| `/engine-bump` | Bannerlord version-change response (baseline-preserve first) |
| `/humanizer` | Deep-clean AI-writing tells from a finished prose artifact |

### Workflow → Skill convention

**Every recurring, multi-step workflow becomes a skill** when it is (a) repeatable, (b) multi-step or chains skills, and (c) carries TAOM-specific gotchas. The skill is a thin entry point pointing at the authoritative doc. **Do NOT skill-ify** one-offs, pure reference, or single commands — descriptions load eagerly into every conversation, a permanent context tax. New skills: description <=30 words, follow `.claude/rules/external-skill-ports.md`, register in the table above, update CHANGELOG.

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
| `think-before-coding.md` | _(no `paths:` — always-load)_ | Surface load-bearing assumptions before the first Edit; ask if uncertain. Don't ask on trivial/mechanical work. Lightweight design pass (one question at a time, propose 2-3 approaches) for open-ended work. Reuse-before-write ladder (engine API → existing service/adapter → one-line delegation → minimal new code) before writing new code. |
| `evidence-over-claims.md` | _(no `paths:` — always-load)_ | Verify a review finding before implementing it; never sycophantically agree; no "done" claim without fresh verification output (subagent self-reports don't count). |
| `response-style.md` | _(no `paths:` — always-load)_ | Open every reply with scrutiny, not agreement (challenge / name the gap when load-bearing); tag every response `[Certain]`/`[Likely]`/`[Guessing]`. |
| `ai-prose-style.md` | _(no `paths:` — always-load)_ | Keep AI-writing tells (significance inflation, vague attributions, rule-of-three, filler, generic conclusions) out of produced prose (commits, CHANGELOG, issues, docs, RCAs). Carves out TAOM's em-dash/boldface house style. Full reference + deep-clean: `/humanizer`. |
| `external-skill-ports.md` | `.claude/skills/**/SKILL.md` | Authoring a skill from scratch + per-field checklist for porting from external suites (gstack, etc.). |
| `hook-authoring.md` | `.claude/hooks/**` | Hook authoring conventions: sibling-mirroring, two-stage git-commit matcher, amend handling, log rotation |
| `native-cpp-ports.md` | `Dependencies/**/*.cpp\|h`, `Main/SceneScripts/**` | 6-point C++ port audit (hot-path logging, SEH specificity, offsets, atomics, SRWLock, C++ deep-review) |
| `moduledata-validation.md` | `troops/`, `characters/`, `equipmentsets/`, `taom_spcultures.xml`, `taom_partyTemplates.xml`, `named_companions/`, wanderers + education templates, `tools/schemas/*.json` | Run `python tools/validate_moduledata.py` before committing ModuleData edits; schemas are source-of-truth |
| `vanilla-data-comparison.md` | `**/settlements.xml`, `**/sp_battle_scenes.xml`, `**/spcultures.xml`, `**/taom_spcultures.xml`, `**/spclans.xml`, `**/spkingdoms.xml`, `**/*.xslt` | Compare against current installed vanilla before modifying mirrored data. Vanilla renames/removes scenes & re-schemas XML between versions → stale TAOM refs crash. Scene-ref audit tools + post-bump checklist. |

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

**Start here for any doc question:** [docs/INDEX.md](./docs/INDEX.md) — curated topical map across all 90 feature docs, ADRs, reviews, ai-includes, and migration docs. Knowledge-base architecture: [ADR-010](./docs/adrs/010-knowledge-base-architecture.md).

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
| Consult / append the master lessons-learned record | [LESSONS-LEARNED.md](./docs/reviews/LESSONS-LEARNED.md) (index) → `docs/reviews/lessons/<category>.md` — **read the category file before touching a subsystem; append the `### rule / Why missed / Prevent / Source` entry there after every RCA** |
| Review or rebaseline troop skill balance per culture | [troop-skill-balance.md](./docs/features/troop-skill-balance.md) — run the read-only overview (`analyze_troop_balance.py`) BEFORE `rebalance_troops.py`; downward deltas can signal a too-weak modifier, not over-tuned troops |
| Review LORD stats + the perks each lord's skills unlock, per culture (read-only) | [lord-perk-review.md](./docs/features/lord-perk-review.md) — `extract_perks.py` (perk catalog) + `analyze_lord_balance.py` (per-culture HTML); authoritative skills resolve via `skill_template`→`taom_lord_skill_sets.xml`, NOT the inline `<skills>`; no single-formula parity (two lord-skill systems) |
| Check migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |
| Audit/fix scene refs after a version bump (battle-near-place crashes) | [scene-reference-audit.md](./docs/reference/scene-reference-audit.md) — `audit_scene_names.py` + `audit_battle_scenes.py` + `remap_stale_scene_names.py`; vanilla renames/removes scenes between versions |
| Compare TAOM data against current vanilla before editing mirrored XML | [.claude/rules/vanilla-data-comparison.md](./.claude/rules/vanilla-data-comparison.md) — settlements/sp_battle_scenes/spcultures/xslt; auto-loads when those files are edited |
| Convert a purchased UE/FBX asset kit into a Bannerlord kit (Rivendell/Tents precedent) | [ue-to-bannerlord-asset-pipeline.md](./docs/reference/ue-to-bannerlord-asset-pipeline.md) — UE headless export, Blender normalize (Store-app launcher, staleness gotchas), spec-gloss conversion, `_mtl.tpac` generation (checksum unvalidated), t_-material naming exception |
| Validate ModuleData cross-refs/ids before committing (broken item/troop refs, unknown culture, dup ids, civilian type) | [moduledata-validation.md](./docs/features/moduledata-validation.md) — run `python tools/validate_moduledata.py`; schema-driven, supersedes the per-culture ref validators; auto-loaded rule + pre-commit hook wire it in |
| Update BUTR/MCM/ButterLib dependencies | [migration/dr3-maintenance.md](./docs/migration/dr3-maintenance.md) — version pinning, Steam Workshop fallback, smoke test, risk scenarios |
| Use agent teams | [agent-teams.md](./docs/ai-includes/agent-teams.md) |
| Brief/spawn a subagent correctly | [agent-operating-manual.md](./docs/ai-includes/agent-operating-manual.md) — execution model (can't invoke skills), tool catalog, what to recommend |
| Author a new culture's armor + troop tree (end-to-end) | [new-culture-authoring.md](./docs/ai-includes/new-culture-authoring.md) — phases, helpers, color convention, iteration loops |
| Add or fix lord skills + traits (any culture, any canonical character) | [lord-skills-authoring.md](./docs/ai-includes/lord-skills-authoring.md) — TAOM SkillSet system, archetype catalog, per-NPC override recipes, gotchas |
| Add a rideable creature mount end-to-end (assets → Monster/action/usage XML → C# BT → validation) | [creature-mount-authoring.md](./docs/ai-includes/creature-mount-authoring.md) — elephant+spider-distilled; warg = reference implementation; 1.4.6 lookup-hardening (total key coverage, no `CanAttack`, `actt_dash` jump start, 45-row jump tables); 17-gotcha index; `tools/audit_mount_parity.py` |
| Replace/refine a mount's FBX/tpac assets | [creature-mount-authoring.md](./docs/ai-includes/creature-mount-authoring.md) → "REPLACING FBX / TPAC FILES" — back up, 4 silent-break failure modes, **MANDATORY post-deploy gate `python tools/verify_mount_assets.py <creature>`**; skeleton-drop fix = re-BUNDLE via `tpac_skeleton_inject.py` (a standalone skeleton tpac CRASHES the engine) |
| Refine/author creature locomotion or rider clips in Blender (not data/XML) | [creature-animation-blender-mcp-workflow.md](./docs/ai-includes/creature-animation-blender-mcp-workflow.md) — Blender 5.1.2 slotted-action API, `harness.py` toolkit, gait biomechanics, the `quad_movement` Kit-compile boundary |
| Create a craftable weapon (FBX → tpac → 4 XML files, by hand) | [weapon-creation-workflow.md](./docs/ai-includes/weapon-creation-workflow.md) — manual Step A–Z; per-piece schema, `bo_` collision convention, brace/couch/pike, **bows/shields = no decimals** rule. Automated alternative: [weapon-xml-pipeline.md](./docs/features/weapon-xml-pipeline.md) |
| Plan future GameModel overrides | [roadmap.md](./docs/roadmap.md) |
| Add or update translations | [TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md) + [tools/README.md](./tools/README.md#localization-pipeline) |
| Understand MBSubModuleBase lifecycle or Harmony patch registration | [submodule-lifecycle-and-harmony.md](./docs/reference/engine/submodule-lifecycle-and-harmony.md) — when callbacks fire, patch kinds (Prefix/Postfix/Transpiler), deferred-apply gotcha, managed vs native boundary |
| Understand the campaign→mission seam (encounters → battles) | [campaign-to-mission-bridge.md](./docs/reference/engine/campaign-to-mission-bridge.md) — `EncounterManager`→`MapEvent`→`MissionState.OpenNew`, the single managed↔native handoff; AI auto-resolve without a Mission |
| Understand campaign object graph (Hero/Clan/Kingdom/MobileParty/Settlement) | [campaign-object-graph.md](./docs/reference/engine/campaign-object-graph.md) — sealed types, nav-property gotchas, `Settlement.Culture` not engine-saved, castle `.Village==null` NRE |
| Debug agent spawn crashes / non-humanoid creature spawn / `AddSkinMeshes` | [agent-spawn-and-render-pipeline.md](./docs/reference/engine/agent-spawn-and-render-pipeline.md) — `FromCharacterObj` vs `FromHorseObj` (skips skin meshes), `AgentBuildData`, `AgentVisuals` |
| Understand mount/rider runtime or creature seating (howdah, riderless spider) | [mount-and-rider-runtime.md](./docs/reference/engine/mount-and-rider-runtime.md) — two-phase `EventControlFlag` mount, `RiderSitBone`, `UsableMachine` howdah `StandingPoint` seating |
| Understand formation geometry, team AI, or `AutoGenerated.dll` DivideByZero | [formations-and-team-ai.md](./docs/reference/engine/formations-and-team-ai.md) — count-division sites (lines 756/1295/1428/1449), `_MT` threading, spider DivideByZero lead |
| Understand DailyTick / CampaignTime / party AI / staggered AI ticks | [campaign-tick-time-and-party-ai.md](./docs/reference/engine/campaign-tick-time-and-party-ai.md) — heartbeat loop, `CampaignTime` struct, `TickPartialHourlyAi` stagger |
| Understand settlement food / prosperity / hearth / caravans (why garrisons starve) | [settlement-economy-food-prosperity.md](./docs/reference/engine/settlement-economy-food-prosperity.md) — the food math, village caps, prosperity death-spiral, + the `TaomSettlementFoodModel` Troop-Weight fix |
| Understand the quest/issue system, or convert vanilla quests to LOTR | [issue-and-quest-system.md](./docs/reference/engine/issue-and-quest-system.md) (engine A-to-Z) + [lotr-issues.md](./docs/features/lotr-issues.md) (**IMPLEMENTED** 2026-06-20 — 43 vanilla issues suppressed + replaced via XML-config + 3 generic templates) |
| Browse all engine process docs | [docs/reference/engine/](./docs/reference/engine/) — full arc: campaign heartbeat → object graph → encounter seam → mission lifecycle → agent spawn → formation/team AI → mount/rider → combat stats → usable machines → UI → save/object system → GameModel → scene/script → campaign behaviors → items → module integration → settlement economy |

## Localization

12 supported languages (BR, CNs, CNt, DE, FR, IT, JP, KO, PL, RU, SP, TR) × 3 modules (TAOM, TAOM_Map, LOTRLOME_Armory) = ~10K strings per language. PL is community-hand-translated; the other 11 have AI first-draft translations (Claude Sonnet 4.5 via `tools/translate_with_claude.py`).

| Component | Location | Notes |
|-----------|----------|-------|
| **Translator-facing guide** | [docs/localization/TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md) | Full workflow, AI pipeline, manual fallback, Tolkien naming conventions |
| **Source loc XMLs** (English defaults + translator's discoverable key list) | `Main/_Module/ModuleData/taom_*_strings.xml` (×7, incl. `taom_xslt_strings.xml`) + `named_companions/named_companion_strings.xml` | 8 source files. Each entry uses `text="{=KEY}default"` format. |
| **Per-language translation files** | `Main/_Module/ModuleData/Languages/<LANG>/std_taom_*.xml` | 8 files per language. Engine auto-discovers via `language_data.xml`. |
| **External module translations** | `<game>/Modules/TAOM_Map/ModuleData/Languages/<LANG>/loc_settlements.xml`, `<game>/Modules/LOTRLOME_Armory/ModuleData/Languages/<LANG>/loc_*.xml` | Not in repo (deployed straight to game install). |
| **Translation tools** | [tools/translate_with_claude.py](./tools/translate_with_claude.py), [tools/rebuild_translation_files.py](./tools/rebuild_translation_files.py), [tools/generate_translation_template.py](./tools/generate_translation_template.py), [tools/translation_status.sh](./tools/translation_status.sh) | See [tools/README.md](./tools/README.md#localization-pipeline). |
| **Overrides** (hand-curated canonical translations) | `tools/translation_overrides/<lang>.json` | E.g., Russian Tolkien names: Бродяжник, Мордор. Always wins over LLM. |
| **Cache** (machine-translated, resumable) | `tools/translation_cache/<lang>.json` | Git-tracked. Re-runs free. ~700KB-1.3MB per lang. |
| **Validation tests** | [TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs](./TAOM.Tests/Infrastructure/Localization/LanguageDataXmlTests.cs) | Enforces 8 LanguageFile refs per language, well-formed XML, no missing files. |

- **New C# player-facing text:** wrap `{=KEY}default`, add to `taom_module_strings.xml`, re-run the translation tool. **New source XML text files:** SubModule GameText node + `<LanguageFile>` x12 + stubs + bump the `LanguageDataXmlTests` count. **XSLT-injected `{=KEY}` text:** harvest into `taom_xslt_strings.xml` (precedent `20713a1`), then translate. Full workflow: `/localize` + [TRANSLATOR_GUIDE.md](./docs/localization/TRANSLATOR_GUIDE.md).

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
| CombatMechanics | `Main/Features/CombatMechanics/` (combat feel pack via `TaomCombatMechanicsModel` — crush-through/cleave/unstoppable/charge-knockdown/shield-pen + race modifiers; 4 pure services + validated config `combat_mechanics/combat_mechanics_config.json`; see the GameModel table row + `docs/features/combat-mechanics.md`) |
| CulturalFeats | `Main/Features/CulturalFeats/` (TaomCulturalFeats, 16 GameModel overrides) |
| CustomBattles | `Main/Features/CustomBattles/` (Custom battle factions, commanders, troops) |
| Arena | `Main/Features/Arena/` (TaomTournamentModel — per-participant culture armor) |
| MainMenuCustomizer | `Main/Features/MainMenuCustomizer/` (hide Campaign, rename Sandbox → "Enter The Age Of Men") |
| ShaderPrecompilation | `Main/Features/ShaderPrecompilation/` — main-menu shader pre-compile walk (all-characters battle + TAOM `_forceatmo` scenes) that kills first-encounter stutter + the battle-load `d3dcompiler` CTD; crash-skip guard + MCM master/scene-pass toggles (#287). See [shader-precompilation.md](docs/features/shader-precompilation.md) |
| PartyIconScale | `Main/Features/PartyIconScale/` — `Patch53` transpiler swaps both `0.3f` literals in `MobilePartyVisual.AddCharacterToPartyIcon` (leader figure + mount) for `PartyIconScaleConfig.GetScale()`, so map party icons honour the MCM "Map Figure Scale" slider (default 0.15 = half vanilla). See [party-icon-scale.md](docs/features/party-icon-scale.md) |
| SkipCampaignIntro | `Main/Features/SkipCampaignIntro/` — `Patch58` Prefix on `SandBoxGameManager.OnLoadFinished` skips the vanilla campaign intro video on a NEW game → straight into character creation; save-loads untouched, fail-safe to vanilla; hardcoded always-skip, no MCM toggle. See [skip-campaign-intro.md](docs/features/skip-campaign-intro.md) |
| NativeSkinFixes | `Main/Features/NativeSkinFixes/` — managed P/Invoke wrapper for `TAOM.NativeSkinFixes.dll` (covers_head morph + hair/beard cloth). **PARKED 2026-07-08 — OFF by default, DISABLED at the wiring level**; re-enable = uncomment the install branch in `SubModule.cs` + flip the default back to `true`. See [native-skin-fixes.md](docs/features/native-skin-fixes.md) |
| SiegeDefense | `Main/Features/Siege/` (timed defense events when watched factions are besieged; config-driven watched factions, CampaignTime deadline, relation+influence reward on arrival) |
| SpecialResources | `Main/Features/SpecialResources/` (11 resources across 18 kingdoms — War Spoils/Gems/Castar/Marks/Elven Wine/Lake Fish/War Drums/Tribal Relics/Dunlending Ale/Plunder/War Banners; XML-driven with many-to-one kingdom/culture mappings, shared balance, pending transaction upgrades, desertion at 0, notifications, Patch26, composite `heroId:resourceId` storage) |
| CareerSystem | `Main/Features/CareerSystem/` — career/class progression: 50 XML-driven careers across 16 cultures (passives wired into GameModels, ability system, UIExtenderEx career screen, SyncData persistence, CC career selection + archetype-driven starting equipment). See [career-system.md](docs/features/career-system.md) |
| SettlementGuards | `Main/Features/SettlementGuards/` (per-settlement guard customization — XML-driven guard troop pools with settlement→clan→culture fallback, spawn-point filtering, weighted random selection, per-culture spear mapping; Harmony prefixes on private GuardsCampaignBehavior methods) |
| NamedCompanions | `Main/Features/NamedCompanions/` (18 lore companions as recruitable wanderers — Aragorn/Legolas/Gimli/etc; `is_hero="true"` + `occupation="Wanderer"`, JSON config for spawn settlements, vanilla dialog integration, race persistence via existing HeroRace system) |
| RevoltTuning | `Main/Features/RevoltTuning/` (JSON-tunable soft-nerf of vanilla revolt mechanic for LOTR's frequent settlement flips; raises loyalty thresholds + dampens different-culture penalties; semantic validation rejects out-of-range / sign-flipped values; consumed by `TaomSettlementLoyaltyModel`) |
| SettlementFood | `Main/Features/SettlementFood/` — `TaomSettlementFoodModel` fixes the Troop-Weight garrison food leak (garrison term reads the raw count) + MCM/JSON food knobs (consumption divisors, base/village/flat production, storage caps); defaults = vanilla, only out-of-box change = the garrison fix. See [settlement-food.md](docs/features/settlement-food.md) |
| AlignmentRecruitment | `Main/Features/AlignmentRecruitment/` — blocks volunteer recruitment at enemy-aligned settlements (Free vs Evil, kingdom StringId) via a single `TaomVolunteerModel` -1 override, no Harmony; Symmetric/GoodRejectsEvil modes + independent player/AI MCM gates. Issue #286. See [alignment-recruitment.md](docs/features/alignment-recruitment.md) |
| NavalTravel | `Main/Features/NavalTravel/` — sail the open sea without the Naval DLC (player-initiated via sail key, sea-only), #296. **PARKED 2026-06-26 — DISABLED at the wiring level** (no naval navmesh on TAOM_Map, #120); RE-ENABLE = uncomment the 3 `SubModule.cs` blocks + flip the `enabled` defaults back to true. See [naval-travel.md](./docs/features/naval-travel.md) |
| NazgulFamily | `Main/Features/NazgulFamily/` — the nine Ringwraiths (Witch-King `lord_1_15`, Khamûl `lord_1_48`, + 7) take no spouse/parents/children: `heroes.xslt` data strip + `TaomMarriageModel` runtime marriage block + `NazgulFamilyBehavior` legacy-save clear; no MCM toggle. See [nazgul-family.md](./docs/features/nazgul-family.md) |
| Messengers | `Main/Features/Messengers/` — paid messenger dispatch (encyclopedia + dialog); N-day travel, arrival conversation with settlement-vs-field routing, ambush rolls, primitive-dict SyncData, UIExtenderEx `EncyclopediaHeroPage` button; LOTRAOM port. See [messengers.md](docs/features/messengers.md) |
| QuickActions | `Main/Features/QuickActions/` — 4-option inventory "Sell All" menu (Sell Damaged / Sell Low Value / Unequip All / vanilla via thread-static bypass) + per-save inventory-search toggle; `IInventoryVMAdapter` shared with EquipPresets. See [quick-actions.md](docs/features/quick-actions.md) |
| SmartCavalryAI | `Main/Features/SmartCavalryAI/` — player-team cavalry coordinated line-charge state machine (`ICavalryChargeService` + `Patch31_FormationSetMovementOrder` Postfix, recursion-guarded; adapters wrap TaleWorlds APIs). See [smart-cavalry-ai.md](docs/features/smart-cavalry-ai.md) |
| CultureMarketplace | `Main/Features/CultureMarketplace/` — daily LOTRLOME item injection into town markets keyed to owner culture (K=6 weighted draws per `DailyTickSettlementEvent`, per-town roster cap, conquest shifts market identity next tick; no Harmony, no SyncData). See [culture-marketplace.md](docs/features/culture-marketplace.md) |
| CultureConversion | `Main/Features/CultureConversion/` — conquered fiefs + bound villages gradually adopt the new owner's culture after a hold-timer: `Settlement.Culture` flip persisted via SyncData (not engine-saved), notable replacement (#325), converted-settlement recruitment branch, MCM "Culture Conversion". See [culture-conversion.md](docs/features/culture-conversion.md) |
| CaravanTrade | `Main/Features/CaravanTrade/` — caravans range past the local town cluster, trade across the Free-vs-Evil war per `WarTradePolicy`, and carry fuller baskets; 4 `Patch59` postfixes + 2 `TaomCaravanModel` overrides → pure `ICaravanTradeService`; master-off = exact vanilla, save-clean (#329). See [caravan-trade.md](docs/features/caravan-trade.md) |
| WarOfTheRingMomentum | `Main/Features/WarOfTheRingMomentum/` (+ `UI/`) — Evil-vs-Good war-progress meter (#327): decaying event-fed momentum, on-map ratio bar + popup, opt-in victory, chunked-SyncData persistence (the v2.0.9 save-corruption fix). See [war-of-the-ring-momentum.md](docs/features/war-of-the-ring-momentum.md) |
| SaveLoadDiagnostics | `Main/Features/SaveLoadDiagnostics/` — always-on `[SaveLoad]` save/load lifecycle logging (Patch61, 15 hooks) stamping the exact failing type/SaveId/chunk to `Logs/taom_debug_*.log`; offline triage/repair via `tools/inspect_sav.py` + `tools/repair_sav_strings.py`. See [save-load-diagnostics.md](docs/features/save-load-diagnostics.md) |
| Scene scripts | `Main/SceneScripts/` (engine-discovered `ScriptComponentBehavior` subclasses for map authors; CS_Road procedural mesh generator + Roads/ pure helpers; clean-room ports from external inspiration via `docs/scene-scripts/specs/` + ATTRIBUTION.md procedure) |
| EditorCacheRebuild | `Main/Features/EditorCacheRebuild/` — parallel + incremental + resumable settlement distance-cache rebuild (~108 hr vanilla → ~7 min on 863 settlements), singleplayer MCM trigger only; runtime-only despite the "Editor" name (rename deferred); NavalDLC port tracked at #120. See [editor-cache-rebuild.md](docs/features/editor-cache-rebuild.md) |
| Warg Combat | `Main/Features/Warg/` (BT elements, WargAttackService, WargMissionBehavior) |
| Giant Spider | `Main/Features/Spider/` — Dol Guldur giant spider as a ridden mount (`taom_spider_creature` rider + `spider_mount_a`, vanilla cavalry spawn, no spawn patch); directional-attack `SpiderBehaviorTree` + Patch47/48 dismount guards; data in LOTRLOME_Armory (movement clips MUST carry `quad_movement`). See [spider.md](docs/features/spider.md) |
| War Elephant | `Main/Features/Elephant/` — Harad ridden mount that auto-attacks (warg-pattern BT on shared `Main/Features/ElephantLike/` nodes, #305; trample/tusk cooldowns); mount-lock in `TaomAgentStatCalculateModel`; howdah crew + bone-tracking DEFERRED-disabled (slide sources — re-enable per "Slide root-cause isolation"). Issue #278. See [elephant.md](docs/features/elephant.md) |
| Mumakil | `Main/Features/Mumakil/` — giant Oliphaunt: 3×-scale War Elephant clone minus the howdah (shared `ElephantLike` BT via `MumakilCombat.Profile`, elephant rig/clips reused, size = Horse-item `body_length="300"`); Monster/item/mesh live in external LOTRLOME_Armory. See [mumakil.md](docs/features/mumakil.md) |
| BanditManagement | `Main/Features/BanditManagement/` — LOTR bandit culture replacement (5 cultures) + PlayerProgress-scaled hideout density/party sizes (`TaomBanditDensityModel` + `Patch39`/`Patch40`); MCM + `bandit_management/bandit_scaling_config.json`, vanilla floor enforced. See [bandit-management.md](docs/features/bandit-management.md) |
| CastleRecruitment | `Main/Features/CastleRecruitment/` — Patch42: player + AI recruit volunteer troops from castles (spawned castle-safe notables drawing `castle_*` pools; MCM "Castle Recruitment" + `castle_recruitment/castle_recruitment_config.json`; disable = inert). See [castle-recruitment.md](docs/features/castle-recruitment.md) |
| LotrIssues | `Main/Features/LotrIssues/` — replaces all 43 vanilla procedural issues with 43 XML-configured LOTR issues via 3 generic templates (DeliverGoods/DeliverPersonnel/Combat); no Harmony patch or GameModel override; new-campaign feature. Issue #291. See [lotr-issues.md](docs/features/lotr-issues.md) |
| Vendored Main-module DLLs | `Main/_Module/bin/Win64_Shipping_Client/` — allowlisted vendored binaries `MinHook.x64.dll` + `TAOM.NativeSkinFixes.dll` (`TAOM.dll` + `TAOM.pdb` stay ignored; do NOT vendor `MCMv5.dll` here — MCMv5 comes from TAOM.Dependencies + the `Bannerlord.MCM` NuGet). See [dr3-maintenance.md](docs/migration/dr3-maintenance.md) |
| TAOM.Dependencies stub modules | `Stubs/Bannerlord.{Harmony,UIExtenderEx,ButterLib,MBOptionScreen}/_Module/SubModule.xml` — four alias stubs at the standard BUTR module IDs (v99 version strategy; deployed via `DeployTAOMDependenciesStubs`) so third-party mods stay toggleable in the vanilla launcher. See [dr3-maintenance.md](docs/migration/dr3-maintenance.md) |
| TAOM.Dependencies defensive infrastructure | `Dependencies/Foundation/` — 11-class runtime error-tolerance layer (PatchShield/SaveShield/crash-loop detection; BetaDeps v0.7.5.1 clean-room port; opt-out flags in the module dir). See [dr3-maintenance.md](docs/migration/dr3-maintenance.md) |
| NativeSkinFixes C++ source | `Dependencies/NativeSkinFixes.NativeHooks/` — standalone `.vcxproj` (not in `TAOM.sln`) building `TAOM.NativeSkinFixes.dll` into `Main/_Module/bin/Win64_Shipping_Client/`; byte-pattern scan targets in `Signatures.h`; rebuild `pwsh Dependencies/NativeSkinFixes.NativeHooks/Build.ps1`. See [native-skin-fixes.md](docs/features/native-skin-fixes.md) |
| BehaviorTrees library (inlined) | `Main/BehaviorTrees/` — generic BT engine (Selector, Sequence, RandomSelector, Decorators, Tasks, blackboard). No TaleWorlds dependencies. Compiles into `TAOM.dll`. |
| BehaviorTreeWrapper library (inlined) | `Main/BehaviorTreeWrapper/` — Bannerlord/Agent bindings over `BehaviorTrees` (`BehaviorTreeMissionLogic`, `BehaviorTreeAgentComponent`, listeners). `BehaviorTreeMissionLogic : MissionLogic` (NOT just `MissionBehavior` — regression rule, see `docs/reviews/rca-looter-battle-nre-2026-05-24.md`). Compiles into `TAOM.dll`. |
| Alliance.Wargs | External module: Monster id="warg", animations, items |
| CC narrative data | `Main/_Module/ModuleData/charactercreation/` (JSON) |
| XML config (per-feature) | `Main/_Module/ModuleData/<feature>/` — every feature's config/data paths + validation rules live in its `docs/features/<name>.md` Configuration section (sprites: `Main/_Module/GUI/SpriteParts/` + [gui-sprite-system.md](docs/features/gui-sprite-system.md)) |
| XSLT files | `Main/_Module/ModuleData/*.xslt` |
| Custom lords XML | `Main/_Module/ModuleData/characters/lords.xml` |
| **TAOM_Map settlements (LIVE)** | `<game>/Modules/TAOM_Map/ModuleData/settlements.xml` — **external module, NOT in repo**; the repo's `Main/_Module/ModuleData/settlements.xml` is a **STALE SHADOW** (edits don't affect the game). Live renames: `tools/Apply-MapVillageNames.py`. See [taom-map-settlement-naming.md](docs/reference/taom-map-settlement-naming.md) |
| CareerSystem starter equipment | `Main/_Module/ModuleData/equipmentsets/taom_career_starting_equipment.xml` (per-(culture, archetype, gender) rosters) + LOTRLOME_Armory `LOTRLOME_items/<culture>/starter_armors.xml` — career starting kits; cover-attribute rule (`covers_legs`/`covers_hands`) in career-system.md. See [starting-equipment-tuning.md](docs/features/starting-equipment-tuning.md) |
| VolunteerRecruitment service | `Main/Features/TroopProgression/VolunteerRecruitmentService.cs` (+ per-culture partials under `RecruitmentPools/VolunteerRecruitmentService.<Culture>.cs`, #308) — settlement/clan/culture volunteer pools + conditional pools driving `TaomVolunteerModel.GetBasicVolunteer`. See [volunteer-recruitment.md](./docs/features/volunteer-recruitment.md) |
| TaleWorlds DLLs | `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client` |
| Decompiled source | `E:\Decompiled_Bannerlord\` (pre-decompiled, organized by category) |
| CI/CD | `.github/workflows/build.yml` |
| One-off scripts (finished) | `tools/oneoff/` — one-off migration/authoring scripts move here when done; `tools/` keeps only living tools (see `tools/README.md` § One-offs) |
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
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) + alignment-gated recruitment (`MaximumIndexHeroCanRecruitFromHero` returns -1 to block recruiting at an enemy-aligned settlement — AlignmentRecruitment feature) |
| `TaomArmyManagementModel` | `DefaultArmyManagementCalculationModel` | Culture army influence award/cost feats |
| `TaomPartySpeedModel` | `DefaultPartySpeedCalculatingModel` | Culture forest speed + Rohan infantry speed feats + career PartyMovementSpeed passive |
| `TaomSettlementProsperityModel` | `DefaultSettlementProsperityModel` | Culture hearth growth feats |
| `TaomSettlementMilitiaModel` | `DefaultSettlementMilitiaModel` | Culture veteran militia feats |
| `TaomBuildingConstructionModel` | `DefaultBuildingConstructionModel` | Culture construction speed feats |
| `TaomVillageProductionModel` | `DefaultVillageProductionCalculatorModel` | Culture production feats |
| `TaomCaravanModel` | `DefaultCaravanModel` | Umbar caravan cost feat (CulturalFeats) + CaravanTrade basket-diversity overrides (`GetInitialTradeGold` floor, `GetMaxGoldToSpendOnOneItemCategory`) |
| `TaomBattleRewardModel` | `DefaultBattleRewardModel` | Umbar renown feat + career BattleRenownGain passive |
| `TaomPartyTroopUpgradeModel` | `DefaultPartyTroopUpgradeModel` | Mounted recruit cost feats (Isengard, Rohan) + career TroopUpgradeCost passive |
| `TaomPartySizeModel` | `DefaultPartySizeLimitModel` | Party size feats (Mordor, Gundabad, DG, Isengard, Gondor) + career PartySize passive + **TroopWeight elite-tax limit deflation** (2026-07-11 count→limit rework: counts read raw, the LIMIT shrinks). See `docs/features/troop-weight-system.md` |
| `TaomFoodConsumptionModel` | `DefaultMobilePartyFoodConsumptionModel` | Food consumption feats (elves, Dol Guldur) |
| `TaomSettlementLoyaltyModel` | `DefaultSettlementLoyaltyModel` | Settlement loyalty feats (Gondor, Erebor, elves, Rohan) + JSON-tunable revolt thresholds + dampened different-culture penalties (RevoltTuning feature) |
| `TaomSettlementFoodModel` | `DefaultSettlementFoodModel` | Fixes the Troop-Weight garrison food leak (garrison term uses RAW count, not the weighted `NumberOfAllMembers`) + MCM/JSON-tunable food knobs (consumption divisors, base/village/flat production, storage caps); SettlementFood feature |
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
| `TaomPartyNavigationModel` | `DefaultPartyNavigationModel` | **PARKED 2026-06-26 — NOT registered** (#120/#296; vanilla model in use). Naval travel: naval capability + water-navigable terrain, player-initiated sailing. Re-enable steps + design: `docs/features/naval-travel.md` |
| `TaomMarriageModel` | `DefaultMarriageModel` | NazgulFamily: the 9 Ringwraiths are marriage-ineligible (`IsSuitableForMarriage` + `IsCoupleSuitableForMarriage` false for wraiths); non-wraiths fall through to vanilla. See `docs/features/nazgul-family.md` |
| `TaomSettlementEconomyModel` | `DefaultSettlementEconomyModel` | Tunable town market-gold regen, ONLY `GetTownGoldChange` (#317 — shipped base 25000 vs vanilla 10000 so drained markets recover; castles never reach it). See `docs/features/settlement-economy.md` |
| `TaomCombatMechanicsModel` | `TaomAgentApplyDamageModel` (abstract) → `SandboxAgentApplyDamageModel` | CombatMechanics feel pack in the one `AgentApplyDamageModel` slot: crush-through-block, cleave, stagger immunity, charge knockdown, shield pen, per-race modifiers; career damage passives inherited. See `docs/features/combat-mechanics.md` |

## Harmony Patch Categories

Thin routing table — category → feature → exact target (maps a stack trace to its owner) → status. **Full rationale/history/RCAs per patch: [`docs/reference/harmony-patch-registry.md`](docs/reference/harmony-patch-registry.md).** Patch-authoring rules (incl. the `MovementOrder`-postfix deferred-category mandate) live in the scoped rule `.claude/rules/harmony-patches.md`.

| Category | Feature | Target | Status |
|----------|---------|--------|--------|
| `Patch0_BattleScenes` | Battle scenes | `Campaign.InitializeScenes` | DISABLED |
| `Patch1_FirstTimeInit` | First-time initialization | Various | active |
| `Patch2_RefreshTableau` | Banner tableau refresh | Various | active |
| `Patch3_SetRace` | Race assignment | Various | active |
| `Patch4_CharacterSpawner` | Character spawning | Various | active |
| `Patch5_FaceGen` | Face generation | Various | active |
| `Patch6_BannerEditor` | Banner editor | Various | active |
| `Patch7_FactionMap` | Faction map | Various | active |
| `Patch8_SiegeCampGuard` | Siege camp guard | Various | active |
| `Patch9_RaceFilter` | Culture-restricted race dropdown on CC | `FaceGenVM.Refresh` | active |
| `Patch10_WeatherBoundsGuard` | Weather bounds clamping | `DefaultMapWeatherModel` | active |
| `Patch11_Diplomacy` | Diplomacy system | Various | active |
| `Patch12_WarOfTheRing` | War of the Ring | Various | active |
| `Patch13_RaceAge` | NOP vanilla's same-race birth assert (mixed-race births are normal in TAOM) | `HeroCreator.DeliverOffSpring` (Transpiler) | active |
| `Patch14_Execution` | Execution system | Various | active |
| `Patch15_BannerLayerLimit` | Banner layer limit | Various | DISABLED (engine-native since v1.4.7) |
| `Patch16_AtmospherePersistence` | Forced-atmosphere scenes | `Mission.Initialize` | active |
| `Patch17_TroopWeight` | TroopWeight shed-on-upgrade (elite tax lives in `TaomPartySizeModel` since 2026-07-11) | `PartyUpgraderCampaignBehavior.UpgradeReadyTroops` (Postfix) | active |
| `Patch18_CulturalFeats` | Custom culture feat registration | `Campaign.InitializeDefaultCampaignObjects` | active |
| `Patch19_CustomBattles` | Custom battle TAOM factions/commanders/troops | `CustomBattleData`, `CustomBattleHelper`, `BannerlordMissions` | active |
| `Patch20_NarrativeHorseGuard` | Suppress CC narrative horse crashes for no-mount cultures | `CharacterCreationCampaignBehavior`, `CharacterCreationNarrativeStageView` | active |
| `Patch21_ShaderPrecompilation` | Loading-screen shader progress text | `LoadingWindowViewModel` | active |
| `Patch22_ArmyTargeting` | Border proximity floor for priority-list targets | `AiMilitaryBehavior` | active |
| `Patch23_BannerColorPersistence` | Player clan colors everywhere (UI + 3D battle + conversation) | 16 targets across `CampaignUIHelper`/`SandBoxUIHelper`/party+inventory VMs/`Mission`/`Banner`/`AgentVisuals.Create`/`MapConversationTableau` — full list in the registry | active |
| `Patch24_BannerDriftGuard` | Block vanilla banner color drift during War of the Ring | `Clan.UpdateBannerColorsAccordingToKingdom`, `Clan.UpdateBannerColor` | active |
| `Patch25_LocalizationOverride` | Let English module_strings overrides of vanilla `{=ID}` tokens apply | `MBTextManager.GetLocalizedText` (Prefix) | active |
| `Patch26_SpecialResources` | Per-kingdom resource gating + transactional spending | `PartyCharacterVM.InitializeUpgrades`, `PartyScreenLogic.UpgradeTroop`, `PartyScreenLogic.AddCommand` | active |
| `Patch27_CareerSystem` | Career screen opening + ability V-key activation | `ViewModel.ExecuteCommand`, `AgentStatCalculateModel.UpdateAgentStats` | active |
| `Patch28_SettlementGuards` | Per-settlement guard injection + per-culture spear mapping + excluded-race guard scrub (#346) | `GuardsCampaignBehavior.TakeGuardAgentDataFromGarrisonTroopList` (manual), `GuardsCampaignBehavior.GetSuitableSpear` (manual), `GuardsCampaignBehavior.InitializeGarrisonCharacters` (manual, Postfix) | active |
| `Patch29_CCBodyProperties` | Per-culture default BodyProperties on CC + body re-apply | `CharacterCreationContent.SetSelectedCulture`, `CharacterCreationCultureStageVM.OnCultureSelection`, `CharacterCreationNarrativeStageView.RefreshAgentVisuals` | active |
| `Patch30_MixedFormations` | Mixed ranged/melee formation layout (hot path, vanilla fall-through) | `Formation.GetOrderPositionOfUnit` (Prefix) | active |
| `Patch31_SmartCavalryAI` | Player-cavalry coordinated line-charge state machine | `Formation.SetMovementOrder` (Postfix, deferred — see `Patch_MissionTime_SetMovementOrder`) | active |
| `Patch33_EquipPresets` | Equipment-preset overlay on the inventory screen | `SPInventoryVM.RefreshValues` (Postfix), `GauntletInventoryScreen.OnInitialize` (Postfix) / `.OnFinalize` (Prefix) | active |
| `Patch34_QuickActions` | Inventory "Sell All" multi-action menu | `SPInventoryVM.ExecuteSellAllItems` (Prefix), `SPInventoryVM` ctor (Postfix), `SPInventoryVM.RefreshCallbacks` (Postfix), `SPInventoryVM.OnFinalize` (Postfix) | active |
| `Patch35_CompanionTactics` | Companion role prefixes (party/OOB) + OOB formation-preset overlay | `PartyCharacterVM.RefreshValues`, `OrderOfBattleHeroItemVM.RefreshValues`, `OrderOfBattleVM` ctor/finalize, OOB UI handler tick/finalize (+ manual tooltip Postfix; movement postfix in the shared deferred category) | active |
| `Patch36_FiefManagement` | F6 fief-management screen (custom GameState) | `MapScreen.OnFrameTick` (Postfix), `GameStateScreenManager.CreateScreen` (Prefix) | active |
| `Patch37_CrashReport` | Crash-capture pipeline (Priority-800 Finalizers -> `CrashReportPatchHelper`) | 9 engine-lifecycle Finalizers (`Managed.ApplicationTick`, `ScreenManager.Tick`, `Mission.Tick`, ...) | active |
| `Patch38_SettlementNameplateFade` | Distance-based settlement nameplate fade (hot path ~3000/s) | `SettlementNameplateWidget.DetermineTargetAlphaValue` (Postfix) | active |
| `Patch39_BanditPartySize` | Scale bandit initial rosters by PlayerProgress (cap = stack MaxValue) | `DefaultPartySizeLimitModel.FindAppropriateInitialRosterForMobileParty` (Postfix) | active |
| `Patch40_HideoutDescription` | Themed LOTR hideout encounter descriptions | `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix) | active |
| `Patch41_McmLayoutFix` | Flip MCM options screen to top-to-bottom layout (#252) | UIExtenderEx `WidgetFactoryManager.CreateAndRegister` (Postfix) | active |
| `Patch42_CastleRecruitment` | Castle troop recruitment — AI half | `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler), `AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays` (Transpiler), `RecruitmentCampaignBehavior.HourlyTickParty` (Postfix) | active |
| `Patch43_BattleLoadDiagnostics` | `[BattleLoad]` phase stamps: attack->playable + OpenNew->Initialize segments + mission-exit lifecycle + stall watchdog | 14 hooks (`PlayerEncounter.Start`, `MissionState.OpenNew`, `MissionState.LoadMission`, `Utilities.ClearOldResourcesAndObjects`, `Mission.AfterStart`, `MapState.OnTick`, ...) | active |
| `Patch44_CCNameAutofill` | Pre-fill CC Review-stage name field (culture-appropriate) | `CharacterCreationReviewStageVM..ctor` (Postfix) | active |
| `Patch46_TournamentDwarfDismount` | Dwarf tournament dismount (race-keyed) | `TournamentFightMissionController.PrepareForMatch` (Postfix) | active |
| `Patch47_SpiderDeathDismount` | Spider rider-death native-AV guard | `Agent.Die` (Prefix) | active |
| `Patch48_SpiderHitDismountGuard` | Spider surviving-rider dismount-AV guard (Patch47 sibling) | `Agent.HandleBlowAux` (private, Prefix) | active |
| `Patch49_ArmyGatheringNreGuard` | Army-gathering map-tick NRE guard + `[SiegeDiag]` diagnostics | `Army.FindBestGatheringSettlementAndMoveTheLeader` (private, Finalizer) | active |
| `Patch50_DropFlaggedItemGuard` | Warg-on-warg bite NRE guard | `Agent.CheckToDropFlaggedItem` (public, Finalizer) | active |
| `Patch51_RecruitmentResourceGate` | Special-resource affordability gate on the recruit Done button | `RecruitmentVM.RefreshPartyProperties` (Postfix) | active |
| `Patch53_PartyIconScale` | Campaign-map party-icon figure/mount scale (MCM slider) | `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler) | active |
| `Patch54_NavalTravelBoatVisual` | NavalTravel at-sea boat mesh | `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View) | PARKED 2026-06-26 (#120/#296) |
| `Patch55_BasicTableauRaceGuard` | Render-safe race coercion for Save/Load preview (custom-race native AV, #295) | `BasicCharacterTableau.RefreshCharacterTableau` (private, Prefix) | active |
| `Patch56_SceneNotificationVisualGuard` | Become-king cinematic CTD guard (null AgentVisuals) | `GauntletSceneNotification.OpenScene` (private, Finalizer) + `.OnTick` (Postfix, deferred close) + `PopupSceneSpawnPoint.InitializeWithAgentVisuals` (diagnostic Prefix) | active |
| `Patch57_NavalAtSeaLandRescueGuard` | At-sea land-pathfind native-AV guard | `AIMoveToNearestLandBehavior.AiHourlyTick` (internal, Prefix) | PARKED 2026-06-26 (#120/#296) |
| `Patch58_SkipCampaignIntro` | Skip vanilla campaign intro video on NEW game (always-on) | `SandBoxGameManager.OnLoadFinished` (public override, Prefix) | active |
| `Patch59_CaravanTrade` | Caravan range/war-gate/basket levers | `CaravansCampaignBehavior.CanTradeWith` + `.GetTradeScoreForTown` + `.GetDistanceLimitVeryFarAsDaysForNavigationType` + `.CalculateBudgetFactor` (all private, Postfix ×4) | active |
| `Patch60_TournamentExitMovieRelease` | Tournament-exit movie release (#331 round 1; canary `ReleaseMovie=Nms`) | `MissionGauntletTournamentView.OnMissionScreenFinalize` (public override, SandBox.GauntletUI.dll, Prefix+Postfix) | active |
| `Patch61_SaveLoadDiagnostics` | Always-on `[SaveLoad]` lifecycle logging (15 hooks) | save/load pipeline Finalizers/Postfixes (see the feature doc) | active |
| `Patch61_SaveLoadDiagnostics_ArchiveParse` | Archive-chunk parse-fault stamps (truncation vs corruption) | `ArchiveDeserializer.LoadFrom` (internal, void Finalizer, Priority.First) | active |
| `Patch61_SaveLoadDiagnostics_BehaviorData` | Names WHICH behavior's SyncData failed | `CampaignBehaviorDataStore.LoadBehaviorData`/`.SaveBehaviorData` (internal, void Finalizer) | active |
| `Patch61_SaveLoadDiagnostics_ContainerFill` | Container (dict/list SyncData) load-fault stamps | `ContainerLoadData.InitializeReaders`/`FillCreatedObject`/`Read`/`FillObject` (internal, void Finalizers) | active |
| `Patch62_MovieReleaseAvGuard` | Tournament-exit heap-corruption AV → logged movie leak (#339) | `GauntletMovie.Release` (public, Finalizer, AV-only) | active |
| `Patch_MissionTime_SetMovementOrder` | Shared deferred category — ANY postfix with `MovementOrder` in its signature MUST use it | `Formation.SetMovementOrder(MovementOrder)` (Postfix ×2) | active |
| `Late_ActionSetOverride` | Race-aware action-set name resolution (null monster -> human; vanilla fall-through) | `ActionSetCode.GenerateActionSetNameWithSuffix` (Prefix) | active |
| `Late_Transpiler` | Race-appropriate `_facegen` action set in the face-gen preview | `BodyGeneratorView.RefreshCharacterEntityAux` (Transpiler) | active |

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
| **CLAUDE.md** | New features add ONE <=400-char Key Paths row + registry/table rows; prose goes in the feature doc, never the table (linter-enforced budget) | `CLAUDE.md` |
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

| Hook | Event | Purpose |
|------|-------|---------|
| `check-build-before-commit.sh` | PreToolUse (Bash) | Blocks `git commit` if build fails |
| `notify-csharp-edit.sh` | PostToolUse (Edit\|Write) | Logs C# file modifications |
| `check-changelog-updated.sh` | Stop | Reminds to update CHANGELOG.md |
| `session-start.sh` | SessionStart | Prints branch, recent commits, CHANGELOG summary on startup. **Also warns loudly on game-version drift** (installed `Version.xml` vs `.claude/pinned-game-version.txt`) → run `/engine-bump`. |
| `pre-compact.sh` | PreCompact | Dumps modified files list before context compaction |
| `log-agent.sh` | SubagentStart | Audit logs agent invocations to `.claude/logs/agent-audit.log` |
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to Directory.Build.props, settings*.json, ADRs without explicit request. CLAUDE.md removed from the protected list 2026-07-02 (user decision — solo dev; the agent maintains CLAUDE.md as living documentation) |
| `suggest-compact.sh` | PreToolUse (*) | Suggests `/compact` after 50 tool calls, then every 25 |
| `mcp-health-check.sh` | PreToolUse (mcp__*) | Blocks MCP calls to servers marked unhealthy in last 60s |
| `mcp-health-mark.sh` | PostToolUseFailure (mcp__*) | Marks MCP server unhealthy after failed tool call, 60s backoff |
| `check-deep-review.sh` | Stop | Reminds to run `/deep-review` if real work was done |
| `post-compact.sh` | PostCompact | Reminds Claude to re-read MEMORY.md + in-flight files after compaction |
| `detect-docs-gaps.sh` | SessionStart | Flags `Main/Features/<X>` directories with no matching `docs/features/*.md` |
| `validate-push.sh` | PreToolUse (Bash) | Warns on push to master/main; hard-blocks force push to protected branches |
| `block-dangerous-git.sh` | PreToolUse (Bash) | Prompts (`ask`) before work-destroying git ops (`reset --hard`, `clean -f`, `branch -D`, `checkout`/`restore` discard, `stash drop/clear`). Segment-anchored; excludes push (validate-push owns it); fail-open. |
| `check-changelog-changed.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when `.claude/`, `CLAUDE.md`, or `AGENTS.md` is staged but `CHANGELOG.md` is not. Catches the recurring "forgot to update CHANGELOG" process violation. |
| `check-claude-files-tracked.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when files exist on disk under `.claude/{skills,agents,rules,hooks}/` but are gitignored or untracked. Catches the gitignore-blast bug (`bin/check-freeze.sh` shipped non-functional in efbde5b). |
| `session-stop.sh` | Stop | Appends commits + modified files to `.claude/logs/session-log.md` |
| `mark-verification-run.sh` | PostToolUse (Bash) | Touches `.claude/logs/.verification-ran` when `dotnet build`/`dotnet test`/`build.ps1` runs. Feeds the verification Stop hook. |
| `check-verification-evidence.sh` | Stop | Reminds to build/test when a `.cs` file changed but no verification ran since the last edit. Enforces `.claude/rules/evidence-over-claims.md`. |
| `check-moduledata-validation.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when staged `Main/_Module/ModuleData/**/*.xml` fails the ERROR-severity checks of `tools/validate_moduledata.py` (broken Item/NPCCharacter ref, unknown culture, duplicate id). Fail-open: missing python / game install / validator crash never blocks. Warnings don't block — run the tool to see them. |
| `check-native-dll-crt.sh` | PreToolUse (Bash) | Hard-blocks commit when the staged `TAOM.NativeSkinFixes.dll` links a dynamic/debug CRT (absent on player machines → `LoadLibrary` error 126); must link static CRT (`/MT`). Fail-open |
| `check-doc-config-drift.sh` | PreToolUse (Bash) | Hard-blocks commit on config-example drift, version mismatch vs the pin, or a CLAUDE.md budget violation, via `tools/lint_docs.py --fail-on-drift`. Fail-open |

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
| **CC facegen action_sets** | LIVE at `E:\Steam\...\LOTRLOME_Armory\ModuleData\action_sets.xml`; tracked snapshot `docs/reference/lotrlome-armory-snapshot/`. Every TAOM race id needs full-surface `as_<race>_facegen` + `_female_facegen` (copy `as_dwarf_facegen`; slim entries break post-parent CC). See [character-creation.md](docs/features/character-creation.md) |

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

## Rebalancing & Data Tools

Full catalog with per-tool gotchas: [`tools/README.md`](tools/README.md) (Validation · Save-repair · Content Generation · Rebalancing · Lords & Equipment · Troop revamps · Settlements · Localization · One-offs). Preferred validators: `python tools/validate_moduledata.py` (+ the `taom-moduledata` MCP server). Balance passes: run the read-only `analyze_*` overview BEFORE any `rebalance_* --apply`.
