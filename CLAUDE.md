# CLAUDE.md

Bannerlord 1.4 total conversion mod (TAOM - Tales From the Age of Men)

> **Target: Bannerlord 1.4.6** (installed game version; pinned in `.claude/pinned-game-version.txt`). The codebase migrated from v1.3.15 (landed 2026-05-22) onto the 1.4.x line and tracks the installed engine, which advanced to **v1.4.6** (the spider/elephant native-crash work + `Patch47`–`Patch50` are 1.4.6-specific). The `E:\Decompiled_Bannerlord\` baseline dump is still **v1.4.5** — one version behind installed — so `ilspycmd` against the installed 1.4.6 DLLs is authoritative for signatures. Migration audit trail: [`docs/migration/TRACKING.md`](docs/migration/TRACKING.md); original plan: [`docs/migration/v1.4.x-overview.md`](docs/migration/v1.4.x-overview.md).

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
| **Research First** | Never guess TaleWorlds behavior - check `E:\Decompiled_Bannerlord\` for concepts, but **verify signatures via `ilspycmd` on installed DLLs** (the `E:\Decompiled_Bannerlord\` dump is v1.4.5 while the installed game is **v1.4.6**; `ilspycmd` on the installed DLLs is authoritative) |
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
| `/verify-bindings [check\|refresh\|full]` | Verify all Harmony patch / GameModel / reflection bindings resolve against the installed engine + refresh the committed API snapshot. Use after an engine bump or patch/GameModel change. |
| `/ship [feature]` | Orchestrate the MANDATORY completion sequence: `/verify` → `/deep-review` → fix → `/review-codex` → close issue → docs → CHANGELOG. C# features touching ≥2 files only. |
| `/new-culture [id]` | Author/revamp a culture's armor + troop tree + recruitment end-to-end (follows `docs/ai-includes/new-culture-authoring.md`). |
| `/lord-skills [name\|culture]` | Assign lore-driven lord skills + traits via the TAOM SkillSet system (`docs/ai-includes/lord-skills-authoring.md`). |
| `/localize [c#\|xml\|xslt]` | Propagate new player-facing text through the 12-language localization pipeline. |
| `/author-armor [culture]` | Author LOTRLOME armor items + swap troop rosters (enforces canonical-folder + cover-attribute rules). |
| `/finish-branch [branch] [base]` | Integrate a merge-ready branch into trunk: FF-check → merge → regenerate backlinks → CHANGELOG → delete branch (local+remote) → push (confirm). Post-`/ship`; TAOM trunk-based, not Git Flow. |
| `/adopt-external [url]` | Review an external repo/article and fold the useful parts into TAOM: security-vet first (when the candidate ships `.claude/` skills/config, run `tools/audit_claude_config.py --root <clone> --external` on the foreign tree BEFORE porting) → map novel-vs-duplicative → tiered recommendation → port (never install) → adversarial review → commit. Follows `docs/ai-includes/external-repo-adoption.md`. |
| `/security-scan` | Audit a Claude config for committed secrets, over-broad permissions, hook exfiltration, MCP risk, hidden-unicode injection. Default = TAOM's own (`.claude/`, `.mcp.json`, `settings*.json`, `CLAUDE.md`); `--root <dir> --external` vets a FOREIGN/untrusted skill (SkillSpector-derived threat categories + Python-AST + clean-room YARA at full severity) — use during `/adopt-external`'s security-vet. Runs `tools/audit_claude_config.py`. |
| `/doc-graph [explain\|path\|metrics]` | Query + audit the docs/ knowledge graph (`tools/graph_query.py`): `explain` a doc's links, `path` between two docs, `metrics` (god nodes / bridges / orphans). Topology, not search. ADR-010 Phase 5; sibling of the `/lint-docs` + `/knowledge-compile` doc-tooling layer. |
| `/improve [quick\|deep\|category\|branch\|next\|plan\|review-plan\|execute\|reconcile] [--issues]` | Whole-repo improvement audit (10 categories incl. game-data integrity) → vetted prioritized findings → self-contained handoff plans in `plans/` for cheaper executors. Advisor never edits source. Ported from shadcn/improve (MIT). |
| `/native-crash-triage` | Root-cause native CTDs (AV in TaleWorlds.Native.dll) without symbols: Event Log fault offsets → `tools/native_crash_triage.py` (pdata bounds, string maps, caller chains) → mixed-mode debugger protocol. The 3-sites-in-one-day v1.4.6 method. |
| `/new-creature-mount [name]` | Author a rideable creature mount end-to-end per `docs/ai-includes/creature-mount-authoring.md` (elephant+spider-proven): assets → Monster/action/usage XML → C# BT → parity-audit-first validation. |
| `/refine-creature-anim` | Refine/author creature locomotion + mounted-rider clips in a live Blender-MCP session per `docs/ai-includes/creature-animation-blender-mcp-workflow.md`: extract→analyze_gait→re-phase/damp→export Kit-ready FBX. GUI-only; hands off to `/new-creature-mount` for Kit-compile + in-game test. |
| `/engine-bump` | Respond to a Bannerlord version change: preserve decompile baseline → regen + managed diff → `/verify-bindings` + parity audits → control battles. Session-start hook warns on drift (pin: `.claude/pinned-game-version.txt`). |
| `/humanizer` | Deep-clean AI-writing tells out of a finished prose artifact (doc/RCA/issue/commit body) via the full 33-pattern reference. Carves out TAOM's em-dash/boldface/inline-header house style. Always-on companion: `.claude/rules/ai-prose-style.md`. Ported from blader/humanizer (MIT). |

### Workflow → Skill convention

**Every recurring, multi-step workflow we develop becomes a skill.** When you find yourself running a process that is (a) **repeatable**, (b) **multi-step or chains other skills**, and (c) **carries TAOM-specific gotchas worth encoding**, author a `.claude/skills/<name>/SKILL.md` for it before the knowledge evaporates into a one-off transcript. The skill is a thin entry point — trigger, ordered steps, key tools, top gotchas — that **points to** the authoritative doc (`docs/ai-includes/*`, etc.) rather than duplicating it.

**Do NOT skill-ify** one-offs, pure reference docs, or single-command operations — skill descriptions load **eagerly** into every conversation + Task spawn (see `.claude/rules/harness-facts.md`), so each skill is a permanent context tax. The filter is the three-part test above; when in doubt, leave it a doc and let `/skill-stocktake` flag it if it recurs.

New skills must: keep `description` ≤30 words, follow the frontmatter rules in `.claude/rules/external-skill-ports.md`, register in the table above, and update `CHANGELOG.md` (the pre-commit hook enforces the last one).

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
| User shares an external repo/article "to see what we can adopt", "review this repo", "next repo", pastes a GitHub URL, OR shares an external skill / SKILL.md / Claude plugin / hook / MCP server to evaluate for adoption into TAOM | **`/adopt-external`** | None — always. It runs the security-vet-first → map → tier → port-never-install → review → commit cycle. When the candidate ships `.claude/` skills/config, the security-vet phase MUST run `python tools/audit_claude_config.py --root <clone> --external` (SkillSpector categories at full severity) on the foreign tree BEFORE porting. |
| Before `/ship`, after editing a hook / MCP server / `settings*.json` permission / `CLAUDE.md`, OR after pulling/porting external `.claude/` config (skill, hook, MCP) into TAOM | **`/security-scan`** | Only when config/hooks/permissions changed, shipping, or external config was pulled — skip for routine feature edits. To vet a foreign skill BEFORE adoption, run `audit_claude_config.py --root <clone> --external` (see `/adopt-external`). |
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
| `moduledata-validation.md` | `troops/troops_*.xml`, `characters/*.xml`, `equipmentsets/*.xml`, `taom_spcultures.xml`, `taom_partyTemplates.xml`, `named_companions/*.xml`, `taom_wanderers.xml`, `taom_education_character_templates.xml`, `tools/schemas/*.json` | Run `python tools/validate_moduledata.py` before committing ModuleData edits; schemas are source-of-truth; add new NPCCharacter-def files to `taom_npccharacter.json` applies_to. |
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

Pure read-only research agents (`Explore`, `Plan`) need only items 1–2 + scope; building/editing agents need all five.

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
| Consult / append the master lessons-learned record (every `/deep-review`, `/review-codex`, and RCA lesson, by subsystem) | [docs/reviews/LESSONS-LEARNED.md](./docs/reviews/LESSONS-LEARNED.md) — **read the relevant category before touching a subsystem; append a `### rule / Why missed / Prevent / Source` entry after every RCA.** Indexed from the harness memory (`~/.claude/projects/.../memory/MEMORY.md` → feature/topic files); see that `memory/README.md` for the routing rule |
| Review or rebaseline troop skill balance per culture (the baseline + `CULTURAL_MODS` curve, the read-only overview, the exclusion list) | [troop-skill-balance.md](./docs/features/troop-skill-balance.md) — `analyze_troop_balance.py` (read-only overview) + `rebalance_troops.py` (`--dry-run`/`--apply`); run the overview first, inspect downward deltas (they can signal a too-weak modifier, not over-tuned troops) |
| Review LORD stats + the perks each lord's skills unlock, per culture (read-only) | [lord-perk-review.md](./docs/features/lord-perk-review.md) — `extract_perks.py` (perk catalog) + `analyze_lord_balance.py` (per-culture HTML); authoritative skills resolve via `skill_template`→`taom_lord_skill_sets.xml`, NOT the inline `<skills>`; no single-formula parity (two lord-skill systems) |
| Check migration status | [migration/TRACKING.md](./docs/migration/TRACKING.md) |
| Audit/fix scene refs after a version bump (battle-near-place crashes) | [scene-reference-audit.md](./docs/reference/scene-reference-audit.md) — `audit_scene_names.py` + `audit_battle_scenes.py` + `remap_stale_scene_names.py`; vanilla renames/removes scenes between versions |
| Compare TAOM data against current vanilla before editing mirrored XML | [.claude/rules/vanilla-data-comparison.md](./.claude/rules/vanilla-data-comparison.md) — settlements/sp_battle_scenes/spcultures/xslt; auto-loads when those files are edited |
| Validate ModuleData cross-refs/ids before committing (broken item/troop refs, unknown culture, dup ids, civilian type) | [moduledata-validation.md](./docs/features/moduledata-validation.md) — run `python tools/validate_moduledata.py`; schema-driven, supersedes the per-culture ref validators; auto-loaded rule + pre-commit hook wire it in |
| Update BUTR/MCM/ButterLib dependencies | [migration/dr3-maintenance.md](./docs/migration/dr3-maintenance.md) — version pinning, Steam Workshop fallback, smoke test, risk scenarios |
| Use agent teams | [agent-teams.md](./docs/ai-includes/agent-teams.md) |
| Brief/spawn a subagent correctly | [agent-operating-manual.md](./docs/ai-includes/agent-operating-manual.md) — execution model (can't invoke skills), tool catalog, what to recommend |
| Author a new culture's armor + troop tree (end-to-end) | [new-culture-authoring.md](./docs/ai-includes/new-culture-authoring.md) — phases, helpers, color convention, iteration loops |
| Add or fix lord skills + traits (any culture, any canonical character) | [lord-skills-authoring.md](./docs/ai-includes/lord-skills-authoring.md) — TAOM SkillSet system, archetype catalog, per-NPC override recipes, gotchas |
| Add a rideable creature mount end-to-end (assets → Monster/action/usage XML → C# BT → validation) | [creature-mount-authoring.md](./docs/ai-includes/creature-mount-authoring.md) — elephant+spider-distilled; warg = reference implementation; 1.4.6 lookup-hardening (total key coverage, no `CanAttack`, `actt_dash` jump start, 45-row jump tables); 17-gotcha index; `tools/audit_mount_parity.py` |
| Replace/refine a mount's FBX/tpac assets (constant animation refinement) | [creature-mount-authoring.md](./docs/ai-includes/creature-mount-authoring.md) → "REPLACING FBX / TPAC FILES" — back up, then the 4 silent-break failure modes (dropped skeleton → riderless, dropped `quad_movement` → AV, orphaned binding, un-split mesh). **MANDATORY post-deploy gate: `python tools/verify_mount_assets.py <creature>`** before battle-testing. Skeleton-drop fix: re-BUNDLE via `tools/tpac_skeleton_inject.py` (NOT `tpac_skeleton_extract.py` — a standalone skeleton tpac CRASHES the engine; spider 2026-06-14) |
| Refine/author creature locomotion or mounted-rider animation clips in Blender (not data/XML) | [creature-animation-blender-mcp-workflow.md](./docs/ai-includes/creature-animation-blender-mcp-workflow.md) — Blender 5.1.2 slotted-action API, the `harness.py` toolkit, in-place authoring, gait biomechanics (elephant lateral / spider tetrapod), `quad_movement` Kit-compile boundary, composite rider-fit verification |
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
| Understand settlement food / prosperity / hearth / caravans (why garrisons starve) | [settlement-economy-food-prosperity.md](./docs/reference/engine/settlement-economy-food-prosperity.md) — food = production − consumption (`Prosperity/40` + `garrison/20`), village food caps at 18/day, the prosperity death-spiral, garrisons don't starve-to-death, caravans don't feed towns, + the `TaomSettlementFoodModel` Troop-Weight fix |
| Understand the quest/issue system, or convert vanilla quests to LOTR | [issue-and-quest-system.md](./docs/reference/engine/issue-and-quest-system.md) (engine A-to-Z: `IssueBase`/`QuestBase`/`IssueManager`/`QuestManager`, the 43-issue sandbox set, `RemoveBehaviors<T>` suppression, `SpecialQuestType` auto-cancel rule) + [lotr-issues.md](./docs/features/lotr-issues.md) (**IMPLEMENTED** 2026-06-20 — 43 vanilla procedural issues suppressed + replaced by 43 LOTR issues via XML-config + 3 generic templates: DeliverGoods/DeliverPersonnel/Combat; `LotrIssueSuppression.RemoveBehaviors<T>`; SaveableTypeDefiner base 726900801; per-issue disposition matrix) |
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
| ShaderPrecompilation | `Main/Features/ShaderPrecompilation/` (pre-compile shaders main-menu option — `ShaderPrecompileRunner` walks an all-characters battle then each TAOM `_forceatmo` scene so character + terrain/atmosphere shaders compile up front, eliminating first-encounter stutter + the battle-load `d3dcompiler` CTD. `ShaderPrecompileCrashGuard` auto-skips scenes that hard-crash the process on load (GPU-specific native AV, #287) via a surviving inflight marker; `PrecompileSceneProvider.DefaultScenes` mirrors the live `precompile_scenes.txt` (fallback-drift fixed 2026-06-25 — the disabled `pbr_terrain` crashers stay commented in both). MCM "Graphics/Shader Precompilation": `EnableShaderPrecompilation` (master — live-hides the menu option, blocks new walks; a running walk finishes) + `EnableScenePassPrecompilation` (off = safe all-characters pass only, the escape hatch for affected GPUs while the native shader-compile guard is built). Native guard (Phases 1-3) gated on a real fault offset from an affected machine; root cause confirmed `normalize()`-of-zero in `pbr_terrain` (`terrain_pixel_functions.rsh:818`) but the shader source is engine-global (unshippable as a module override). See `docs/features/shader-precompilation.md`.) |
| PartyIconScale | `Main/Features/PartyIconScale/` (`Patch53` transpiler rewrites the two hardcoded `0.3f` scale literals in `MobilePartyVisual.AddCharacterToPartyIcon` — leader figure + mount — into a `call PartyIconScaleConfig.GetScale()` so campaign-map party icons honour the MCM "Map Figure Scale" slider, default 0.15 = half vanilla. Pure `PartyIconScaleTranspiler` (synthetic-IL tested) + static IL-call-target `PartyIconScaleConfig` (validated `Resolve` + `GetScale` boundary). See `docs/features/party-icon-scale.md`.) |
| NativeSkinFixes | `Main/Features/NativeSkinFixes/` (managed wrapper for `TAOM.NativeSkinFixes.dll` — covers_head morph fix + hair/beard cloth simulation. 3 P/Invoke interop classes + installer. Editor-mode skip. Loaded from `OnBeforeInitialModuleScreenSetAsRoot`, uninstalled from `OnSubModuleUnloaded`. C++ source at `Dependencies/NativeSkinFixes.NativeHooks/`. See `docs/features/native-skin-fixes.md`.) |
| SiegeDefense | `Main/Features/Siege/` (timed defense events when watched factions are besieged; config-driven watched factions, CampaignTime deadline, relation+influence reward on arrival) |
| SpecialResources | `Main/Features/SpecialResources/` (11 resources across 18 kingdoms — War Spoils/Gems/Castar/Marks/Elven Wine/Lake Fish/War Drums/Tribal Relics/Dunlending Ale/Plunder/War Banners; XML-driven with many-to-one kingdom/culture mappings, shared balance, pending transaction upgrades, desertion at 0, notifications, Patch26, composite `heroId:resourceId` storage) |
| CareerSystem | `Main/Features/CareerSystem/` (career/class progression — 50 careers across 16 cultures; XML-driven career defs, mutation calculator registry, passive service with GameModel integration, ability system, career screen UI via UIExtenderEx, level-based tier gating, SyncData persistence, CC career selection stage, archetype-driven starting equipment override at CC finalize — `CareerArchetype` enum + `ICareerArchetypeService` backed by single static map in `CareerSystemIoC` shared with the ability executor registry; `ICareerStartingEquipmentService` applies `player_career_{culture}_{archetype}_{f\|m}` roster on top of culture default via `FillFrom` slot-merge — non-cavalry archetypes need explicit empty Horse/HorseHarness overrides; Gondor authored end-to-end as proof of life, other 15 cultures fall through gracefully to culture default until authored) |
| SettlementGuards | `Main/Features/SettlementGuards/` (per-settlement guard customization — XML-driven guard troop pools with settlement→clan→culture fallback, spawn-point filtering, weighted random selection, per-culture spear mapping; Harmony prefixes on private GuardsCampaignBehavior methods) |
| NamedCompanions | `Main/Features/NamedCompanions/` (18 lore companions as recruitable wanderers — Aragorn/Legolas/Gimli/etc; `is_hero="true"` + `occupation="Wanderer"`, JSON config for spawn settlements, vanilla dialog integration, race persistence via existing HeroRace system) |
| RevoltTuning | `Main/Features/RevoltTuning/` (JSON-tunable soft-nerf of vanilla revolt mechanic for LOTR's frequent settlement flips; raises loyalty thresholds + dampens different-culture penalties; semantic validation rejects out-of-range / sign-flipped values; consumed by `TaomSettlementLoyaltyModel`) |
| SettlementFood | `Main/Features/SettlementFood/` (`TaomSettlementFoodModel : DefaultSettlementFoodModel` — fixes the Troop-Weight garrison food leak by reading the RAW garrison `MemberRoster.TotalManCount` instead of the Patch17-weighted `NumberOfAllMembers` for the garrison consumption term (elite garrisons ate 2–3× intended → chronic starvation), the global getter staying weighted so AI strength + garrison capacity are unchanged. Also exposes vanilla's hardcoded food constants as MCM/JSON knobs: garrison + prosperity consumption divisors, town/castle base food, per-village multiplier, flat bonus, storage caps — production knobs siege-gated (vanilla zeroes production under siege); base/village knobs are absolute REPLACEMENT values (default = vanilla, tune both ways), only `flatFoodBonus` is purely additive. Thin model → pure `SettlementFoodService` (delta math: `(weighted−raw)/divisor` garrison correction always-on + siege-gated production deltas) → `TownFoodSnapshot.FromTown` boundary (ADR-002/007). Validated `SettlementFoodConfigProvider` (divisors ≥ 1, floats finite ≥ 0, invalid → vanilla default + warning). MCM "Settlement Food → Enable Settlement Food Tuning" (on by default; off = vanilla engine math). Defaults = vanilla, so the only out-of-box change is the garrison fix. See `docs/features/settlement-food.md` + `docs/reference/engine/settlement-economy-food-prosperity.md`.) |
| AlignmentRecruitment | `Main/Features/AlignmentRecruitment/` (block recruiting volunteers at an enemy-aligned settlement — Free vs Evil per `IAlignmentService` / `execution/alignment.json`, keyed by **kingdom StringId**. Single `TaomVolunteerModel.MaximumIndexHeroCanRecruitFromHero` override returns -1 (the engine's own "recruit nothing from this notable" signal, as it does for negative relation) for both the player recruit UI + AI lords — no Harmony. Recruiter = `buyerHero.Clan.Kingdom.StringId`; source = `sellerHero.CurrentSettlement.MapFaction.StringId` (Codex-verified equivalent to vanilla `MapFaction`, incl. mercenaries). Symmetric (default) or good-rejects-evil mode + independent player/AI gates (`applyToPlayer` + `applyToAi`, each default on) via `recruitment_alignment/recruitment_alignment_config.json` + MCM "World/Recruitment Alignment" (master + Only-Good-Rejects-Evil + Apply-To-Player + Apply-To-AI-Lords). A player can exempt themselves while AI stays gated (or vice versa); master toggle off = vanilla for everyone. Neutral (Umbar/Shaghana/Abanissa) never blocks; garrison auto-recruit + AI map-recruit are inherently same-kingdom so never trigger. Pure `RecruitmentAlignmentService` + validating config/settings providers per ADR-002/007. Issue #286. See `docs/features/alignment-recruitment.md`.) |
| NavalTravel | `Main/Features/NavalTravel/` (**PARKED 2026-06-26 — DISABLED at the wiring level**: the `TaomPartyNavigationModel` registration + `Patch54`/`Patch57` are commented out in `SubModule.cs` and `enabled` defaults to false (JSON/DTO/MCM), because TAOM_Map's navmesh isn't set up for naval travel (no naval region navmesh → engine can't route at sea, #120). With nothing registered the game uses vanilla `DefaultPartyNavigationModel` + vanilla navmesh regardless of any persisted MCM toggle. All code, tests, and the input/crash fixes are preserved; **RE-ENABLE = uncomment the 3 `SubModule.cs` blocks + flip the `enabled` defaults back to true.** The description below is the design for when active. — sail across OPEN SEA without the Naval DLC — **sea-only by design**, issue #296. `TaomPartyNavigationModel : DefaultPartyNavigationModel` is a faithful port of NavalDLC's internal `NavalPartyNavigationModel` with the ship-ownership capability gate swapped for a TAOM config/MCM gate → grants naval capability + makes water terrain navigable; the base engine then does water pathing + embark/disembark natively (the same `PartyNavigationModel` drives `DisableUnwalkableNavigationMeshes`, so water navmesh stays enabled — no navmesh patch). **Sailing is player-initiated** because the auto-pathfinder always prefers a land/bridge route (region-switch costs are 0 but a land route is shorter/found-first): hold `sailModifierKey` (default LeftAlt, read via `Input.IsKeyDownImmediate` — the buffered `IsKeyDown` misses the held key when polled from the model, outside the map's input layer) + click water → the model's `CanPlayerNavigateToPosition` allows the water target, the party routes to the coast, and the engine's embark transition (`NavigationHelper.GetEmbarkAndDisembarkDataForPlayer`, fired in `MobileParty`'s tick) carries it onto the water; disembark is automatic. **Boat visual** = `Patch54_NavalTravelBoatVisual` (the base game renders no ship at sea — NavalDLC.View-only; TAOM adds the base-game `boat_sail_on` mesh via two `MobilePartyVisual` Postfixes — `OnTransitionEnded` + `AddMobileIconComponents`). Thin model → pure `INavalTravelService` (`HasNavalCapability` at-sea-grace + army-leader inheritance, `ShouldRenderBoat`, naval-terrain set) → MCM-over-JSON settings → validated config `naval_travel/naval_travel_config.json`. MCM "World/Naval Travel" (Enable + Apply-To-Player + Apply-To-AI). Rivers self-exclude (water-`10` channel + impassable mountain-`7` banks + bridges). Naval settlement-distance/AI routing unsupported — engine gates the `Naval`/`All` navigation caches behind a NavalDLC map/module (#120). Codex-reviewed (army-inheritance HIGH + at-sea-grace MED fixed). KNOWN INTERACTION: flipping naval capability on globally drives `CaravanPartyComponent.CacheName` to look up NavalDLC-only convoy strings → re-provided in `taom_module_strings.xml`. **`Patch57_NavalAtSeaLandRescueGuard`** (crash report 2026-06-25) prevents a native-AV CTD: an at-sea party activates the vanilla `AIMoveToNearestLandBehavior` (dormant in vanilla TAOM), whose native cross-region land-pathfind AVs (`0xC0000005` reading `0x4`) on TAOM_Map's missing naval region navmesh (#120) — a Prefix skips it while the feature is enabled (native AV ≠ Finalizer-catchable; prevent-the-call like Patch47/48). End-to-end sail + boat render in-game-pending. See `docs/features/naval-travel.md`.) |
| NazgulFamily | `Main/Features/NazgulFamily/` (the nine Ringwraiths — Witch-King `lord_1_15`, Khamûl `lord_1_48`, + 7 Nazgûl — are undead and take no spouse/parents/children. **Two layers.** (1) **Data strip** in `characters/heroes.xslt`: vanilla `heroes.xml` wires the nine into a self-contained Hero-level family graph (Witch-King↔`lord_1_16` + children `lord_1_155/28/38`; Khamûl↔`lord_1_48_1` + children `lord_1_48_2/48_3`); the 9 wraith Hero templates now strip `spouse`/`father`/`mother` (matching the convention ~150 other lord templates already follow), so a NEW campaign seeds them family-free. Family is Hero-level — `lords.xslt` transforms `NPCCharacter` defs that carry NO family, the trap the first cut fell into. (2) `TaomMarriageModel : DefaultMarriageModel` blocks any FUTURE runtime marriage — `IsCoupleSuitableForMarriage` (the `MarriageAction.ApplyInternal` chokepoint) + `IsSuitableForMarriage` (via `Hero.CanMarry`) return false for wraiths; no spouse ⇒ no children. `NazgulFamilyBehavior` (`OnSessionLaunched`) is the legacy-save fallback: nulls Spouse + clears the `ExSpouses` residual via reflection on the private `_exSpouses` (mirrors the engine's full sever), removes the wraith from each ex-parent's `Children` (the `Father`/`Mother` setters are asymmetric on null), clears `Children` — a no-op on a post-strip new campaign. Child-gen already excludes both wraith cultures (`mordor` + `dolguldur`). `INazgulRegistry` = lore-fixed 9-id compiled constant (Khamûl is statted as an orc-warrior so a skill_template-only scope misses him — he's canonically the 2nd of the Nine); no MCM toggle. Deep-reviewed (7-dim + critic; the critic caught the lords-vs-heroes-xslt premise inversion + the omitted Khamûl). See `docs/features/nazgul-family.md`.) |
| Messengers | `Main/Features/Messengers/` (paid messenger dispatch from encyclopedia + dialog hook; travels for N days, arrival inquiry opens conversation mission with settlement-vs-field routing and player-position restore; random ambush rolls; primitive-dict SyncData; UIExtenderEx prefab extension on `EncyclopediaHeroPage`; ported from LOTRAOM 1.2.12 with 1.3.15 API drift applied; TAOM-owned `MapCoord` keeps service free of TaleWorlds types per ADR-007) |
| QuickActions | `Main/Features/QuickActions/` (replaces inventory "Sell All" button with a 4-option menu — Sell Damaged / Sell Low Value / Unequip All / vanilla; ported from external `TransferbuttonMenu` 1.2.x with v1.3.15 verification removing 8-probe + 5-probe reflection chains; `IInventoryVMAdapter` consolidates inventory VM surface for future EquipPresets reuse; `Patch34_SellAllItemsMenu` Prefix uses thread-static bypass flag so "Sell All (Vanilla)" re-enters vanilla `TransferAll` unmodified; `TrySellItem` sets `SPItemVM.TransactionCount = StackAmount` before invoke for full-stack sells; `TryUnequipAllPlayerSlots` routes through `InventoryLogic.TransferCommand` so vanilla `AfterTransfer` rebuilds rows; per-save `IsSearchAvailable` toggle via `InventorySearchCampaignBehavior` SyncData + Postfix on `SPInventoryVM.RefreshCallbacks`) |
| SmartCavalryAI | `Main/Features/SmartCavalryAI/` (player-team cavalry coordinated line-charge state machine — `ICavalryChargeService` drives Idle→Forming→Charging→PassingThrough→Reforming + Rerouting branch; `Patch31_FormationSetMovementOrder` Postfix; `ICavalryCommandAdapter` + `IBattlefieldQueryAdapter` wrap TaleWorlds APIs; `SmartCavalryRecursionGuard` thread-local depth counter prevents postfix re-entry; v1.3.15 confirmed `SetPositioning`/`SetMovementDirection` are public — no reflection needed; ported with 2 inherited bugs fixed: hardcoded reform-strictness 0.5f override + ungated HUD spam; 4 Codex adversarial findings fixed including NaN propagation through Clamp + cross-feature collision with MixedFormations) |
| CultureMarketplace | `Main/Features/CultureMarketplace/` (daily LOTRLOME item injection into town markets by owner culture — `ICultureItemPoolService` auto-derives culture→items from `MBObjectManager` at first tick + ID-prefix fallback for shields missing `culture=` attribute; optional `culture_marketplace_config.xml` for per-culture blacklist + weight boosts with `FiniteFloatValidator`-guarded weights; `ICultureMarketplaceInjectionService` does weighted-random K=6 draws per `DailyTickSettlementEvent` with per-town distinct-item cap of 60; dynamic owner culture (`town.OwnerClan?.Culture?.StringId`) so conquest immediately shifts market identity; `IItemPoolAdapter` + `ITownRosterAdapter` per ADR-007; no Harmony patch — `Settlement.ItemRoster.AddToCounts(EquipmentElement, int)` is the modifier-preserving entry point; no SyncData, items live in vanilla roster serialization) |
| CultureConversion | `Main/Features/CultureConversion/` (conquered town/castle + bound villages GRADUALLY adopt the new owner's culture — `OnSettlementOwnerChanged` starts a hold-timer toward the owner culture (gated on `IVolunteerRecruitmentService.HasCulturePool` + optional player-owned/loyalty gates); `DailyTickEvent` completes it after `RequiredHoldDays`, setting `Settlement.Culture` on town+villages and clearing notable `VolunteerTypes` so recruits repopulate from the new pool. `Settlement.Culture` is NOT engine-saved → completed overrides persist in `CultureConversionStore` (composite-string SyncData, R-format+NaN reject like `PendingMessenger`) and re-apply on `OnGameLoadedEvent`. `SettlementConversionRecord` captures the ORIGINAL culture once so reconquest-back-to-original removes the override (restores vanilla same-culture loyalty). Recruitment integration: TAOM recruitment ignores `settlement.Culture` (keys on ~81 hard-coded settlement pools), so `VolunteerContext` gained `IsConvertedSettlement`+`SettlementCultureId` (set by `VolunteerContextAdapter` via `ICultureConversionStore.IsConverted` on settlement OR bound parent) and `GetVolunteerTroopId` resolves `CultureMap[SettlementCultureId]` before settlement/clan pools for converted fiefs. Thin behavior → pure `CultureConversionService` → `ICultureConversionAdapter` (Settlement.Find/Culture/BoundVillages/Town.Loyalty/VolunteerTypes) per ADR-002/007. Config `culture_conversion/culture_conversion_config.json` + MCM "Culture Conversion" group. See `docs/features/culture-conversion.md`.) |
| Scene scripts | `Main/SceneScripts/` (engine-discovered `ScriptComponentBehavior` subclasses for map authors; CS_Road procedural mesh generator + Roads/ pure helpers; clean-room ports from external inspiration via `docs/scene-scripts/specs/` + ATTRIBUTION.md procedure) |
| EditorCacheRebuild | `Main/Features/EditorCacheRebuild/` (parallel + incremental + resumable settlement distance cache rebuild — singleplayer MCM trigger only. `TaomSettings.RebuildDistanceCacheAction` boundary lambda → `IRuntimeCacheRebuildService.Trigger()` → `Task.Run` → `ICampaignSessionAdapter.CreateDefaultRuntimeCacheAdapter()` → `CacheBuilderService.Build` → atomic `File.Replace` write with `.prev` backup → round-trip verification gating success popup. `NavigationCacheAdapter` reflection chain wraps `NavigationCache<Settlement>` via `SandBoxNavigationCache`. `ParallelPhase1Builder` + `ParallelPhase2Builder` use `Parallel.For` + `ConcurrentQueue` with locked dict writes; `SmokeTestGate` validates serial-vs-parallel pathfind equivalence at build start; `CheckpointSerializer` saves state between phases for crash recovery; `SettlementDiffer` + `ChangedSettlementsFilter` enable incremental Phase 1 when ≤30 settlements changed; `ValidationReportWriter` emits per-build JSON; ~108hr full vanilla rebuild → ~7min on TAOM's 863 settlements. Comprehensive logging with build-correlation IDs, environment snapshot, scene CRCs, per-phase memory deltas, first-pair heartbeats, atomic-write integrity diagnostics. Legacy editor-mode Harmony patch removed — was blocked by 3rd-party mod compatibility in editor mode and dormant in singleplayer. Despite the "Editor" in the name, this is now a runtime-only feature; rename deferred. NavalDLC port support tracked at #120.) |
| Warg Combat | `Main/Features/Warg/` (BT elements, WargAttackService, WargMissionBehavior) |
| Giant Spider | `Main/Features/Spider/` (Dol Guldur giant spider as a **ridden mount** — `taom_spider_creature` goblin rider + `spider_mount_a` Horse-slot item; vanilla cavalry spawn, NO spawn patch. Per-agent `SpiderBehaviorTree` directional attacks (pounce + left/right swipe by enemy bearing, elephant-mirrored; bite-collision on the front legs joint40-44; lethal damage + 20% per-hit crit) attached by `SpiderMissionBehavior` keyed on `Monster.StringId=="spider"`; pure `SpiderAttackService` with warg-pattern rider damage attribution; mount-lock in `TaomAgentStatCalculateModel`. Data lives in LOTRLOME_Armory: Monster `num_paces=6`/`Mountable`/`rider_sit_bone=chest_m`, `as_spider` (quadrupedal, + `_town_and_village`/`_map` children, explicit canter binding) + full-mount-surface usage set in the ROOT `action_sets.xml`/`monster_usage_sets.xml` (live via `project.mbproj` standard `soln_*` ids ONLY — subfolder copies are superseded), L/R-split meshes (≤38 bones/half vs the ~40 per-mesh palette) + `<AdditionalMeshes>`. **CRITICAL lesson: movement clips MUST carry the `quad_movement` tag + step points in `_anm.tpac`** — untagged gait clips AV (+0x10) on first tick in every mount context (the 2026-06-10/11 RCA; 4 clips byte-patched on the source elephant clip template, originals `*.bak-untagged`). Detached-combatant architecture (Patch45 spawn-swap + wield guards) DELETED 2026-06-10. Proven in battle 2026-06-11; v1.4.6 river-battle green 2026-06-12; directional attacks + lethal damage/crit + front-leg bite-collision + Patch47/48 dismount guards 2026-06-15. See `docs/features/spider.md` (arch + full RCA) + `spider-skeleton-animation-pipeline.md`.) |
| War Elephant | `Main/Features/Elephant/` (Harad war elephant: ridden mount that auto-attacks, modeled on the warg. **Per-agent behavior tree** `ElephantBehaviorTree` + `BehaviorTreeElements/` mirrors the warg pattern — boundary nodes hold the raw `Agent` and delegate pure decisions to the TaleWorlds-free `ElephantAttackService` (`ShouldEngage`/`IsOffCooldown`/`ComputeInflictedDamage`); NO adapter expansion, NO new IoC reg (the BT attaches inside `ElephantMissionBehavior` via `BTRegister`, nodes resolve the service lazily like `WargAttackTask`). **Deterministic cooldown attack model** (replaces the source mod's random per-tick roll): enemy-in-range → trample if off 10s cooldown → else L/R tusk swing by enemy bearing if off 4s cooldown → else idle (engine mount AI continues). Clip roles VERIFIED by Blender trajectory analysis: `act_elephant_attack_3/4`=trample thrash, `_1`=left swing, `_2`=right swing (named consts in `ElephantConfig`; `ElephantAttackActions` eager-resolves them + Index-compares for the already-attacking gate; `Initialize` logs if any → `act_none`, the Armory-drift "slide" guard). **Howdah** (`TaomHowdahMachine` + `TaomHowdahStandingPoint` + `Main/_Module/Prefabs/taom_howdah_agent.xml`) instantiates + tracks the elephant — **crew force-spawn + spine bone-tracking are DEFERRED-disabled** (both confirmed "slide" sources: physics bodies inside the elephant collision capsule; re-enable with a shared-`FaceGroupId` crew/floor collision fix). Mount-lock (`CanAgentRideMount=false`+`MountDifficulty=999`) lives in `TaomAgentStatCalculateModel`. Rider troop `harad_elephant_rider` (`troops_harad.xml`, level 51, `Culture.aserai`) recruitable ONLY by `clan_aserai_1` (Ayerikkä) via `VolunteerRecruitmentService.InitializeHaradClans`. Mount-lock + the structural trample/tusk mechanic are a behavioral port of the source mod; the cooldown cadence and the per-kind randomized **damage** (trample 50-100, tusk 50-75, ×0.25 on shield block, per-victim roll) are TAOM's own rebalance, not the donor's per-tick ~20-fixed roll. ider `harad_elephant_rider` is a bow archer (primary spear → 2nd `bodkin_arrows_b` quiver, 2026-06-15) so it hits ground targets from the elephant's back. Data authored for 1.4.5. See `docs/features/elephant.md` (incl. "Slide root-cause isolation" + "Behavior tree"). Issue #278.) |
| BanditManagement | `Main/Features/BanditManagement/` (LOTR bandit culture replacement + PlayerProgress-scaled hideout density + party sizes. `TaomBanditDensityModel : DefaultBanditDensityModel` overrides 6 properties — hideouts/faction, **initial-hideouts/faction (early-game density lever, vanilla 7 → default 14)**, parties/hideout, min-to-infest, first-fight + boss-fight troop counts. `Cap`/`Scale` helpers are `internal static` (tested via InternalsVisibleTo) and floor at vanilla even when an MCM cap is set below the vanilla base. `Patch39_BanditPartySize` Postfix scales bandit party rosters toward stack `MaxValue`. `Patch40_HideoutDescription` Postfix on private `HideoutCampaignBehavior.game_menu_hideout_place_on_init` + `IHideoutDescriptionService` replace vanilla's "(Undefined hideout type)" placeholder with themed LOTR text per bandit culture (5 `taom_hideout_desc_*` keys). 5 LOTR bandit cultures in `taom_spcultures.xml` (dunland_raiders, rhun_raiders, harad_raiders, gundabad_raiders, umbar_corsairs) + 5 matching bandit clan rows in `characters/clans.xml` + 10 party templates in `taom_partyTemplates.xml` (raider + boss per culture). 99 hideouts in external `TAOM_Map` module migrated via `tools/migrate_hideouts_to_lotr.py --apply --backup` — 2-attr swap (`culture=` + display name). The 5 vanilla hideout-bandit clans (`sea_raiders`, `mountain_bandits`, `forest_bandits`, `desert_bandits`, `steppe_bandits`) are stripped via empty-template rules in `spclans.xslt` — without this, vanilla `BanditSpawnCampaignBehavior.GetInfestedHideoutCount(Clan)` throws KNFE on new-game because the migration leaves those clans with no hideouts of their cultures. Vanilla `looters` clan is KEPT (its `StringId == "looters"` is hardcoded in `DefaultBanditDensityModel`, and looter spawning is on a separate code path). `TAOM_Map/SubModule.xml` now declares `<DependedModule Id="TAOM"/>`. 7 MCM knobs (GroupOrder=35; caps default to 100 hideouts/faction + 3 parties/hideout, min-infest 1, initial 14) + JSON defaults at `bandit_management/bandit_scaling_config.json`. Pure-math service, NaN-guarded, vanilla floor enforced (cap can't push below vanilla). See `docs/features/bandit-management.md`.) |
| CastleRecruitment | `Main/Features/CastleRecruitment/` (Patch42 — player + AI recruit volunteer troops from **castles**, previously towns/villages only. `CastleRecruitmentBehavior` (thin event router) spawns castle notables via `HeroCreator.CreateNotable` on new-game + save-load (retrofits existing saves) + daily maintenance — **castle-safe occupations only** (GangLeader/Headman/Merchant/Artisan; never RuralNotable, which NREs on a castle's null `Settlement.Village`); `CastleNotableMaintainer.FillCastleVolunteers` mirrors vanilla's daily fill with a castle-safe **pure** slot probability because vanilla `DefaultVolunteerModel.GetDailyVolunteerProductionProbability` (and `GetBasicVolunteer`'s rural path) NRE for castles; registers a `recruit_volunteers` option on the vanilla `"castle"` menu (the recruit screen + `CanMainHeroDoSettlementAction(RecruitTroops)` are already settlement-type-agnostic and true for castles); suppresses issues/quests for castle notables via `CampaignEvents.CanHaveCampaignIssuesEvent` (relations untouched — benign side effect: castle notables also never despawn via `CheckAndMakeNotableDisappear`, which is gated by `CanHaveCampaignIssues`). Castle notables draw culture-correct troops from the existing `VolunteerRecruitmentService` `castle_*` pools (zero new pool data — those pools previously only fed castle-bound villages). AI half via `Patch42` (2 transpilers + postfix). Pure `CastleRecruitmentService` owns the decisions (occupation round-robin GangLeader→Headman→Merchant→Artisan, slot-probability curve, toggles). MCM "Castle Recruitment" group (`EnableCastleRecruitment` / `EnableCastleRecruitmentAi` / `CastleNotablesPerCastle` 1-5 default 3) + `castle_recruitment/castle_recruitment_config.json`. Disabling = inert (existing castle notables stay in the save). See `docs/features/castle-recruitment.md` + RCA `docs/reviews/rca-castle-recruitment-2026-05-31.md`.) |
| LotrIssues | `Main/Features/LotrIssues/` (replaces ALL 43 vanilla procedural issues with 43 TAOM-authored LOTR issues. Generic-template + XML-config architecture, NO Harmony patch, NO GameModel override. `LotrIssueSuppression.SuppressAll` in `SubModule.OnGameStart` removes all 43 vanilla issue behaviors via guarded `RemoveBehaviors<T>` while keeping the host `IssuesCampaignBehavior` so `OnCheckForIssueEvent` still fires. `taom_lotr_issues.xml` (43 configs) → validating `LotrIssueConfigProvider` (skips-invalid-and-warns, `FiniteFloatValidator`) → pure `ILotrIssueService` (eligibility, count/reward math, `ApplyRewards`) → thin `LotrIssuesCampaignBehavior` (one `OnCheckForIssueEvent` listener; the `LotrIssueDefinition` rides into the constructed issue via `PotentialIssueData.RelatedObject`) → 3 mechanic templates (each `IssueBase` + paired `QuestBase`): **DeliverGoods** [14] (accumulate N of an `item:<id>` trade good, dialog turn-in), **DeliverPersonnel** [2] (hand over N bandit prisoners from `PrisonRoster`), **Combat** [27, `variant=`] (event-driven count, auto-completes on N: `DefeatRaids` [24, won battles via `OnPlayerBattleEndEvent`], `CaptureLords` [1, at-war lord via `HeroPrisonerTaken`], `WinTournaments` [2, via `TournamentFinished`]). Sealed types behind `ILotrIssueGiverAdapter`/`ILotrIssueRewardAdapter` (ADR-002/007). `LotrIssueSaveableTypeDefiner` base 726900801, localIds 101-106 (3 issue/quest pairs), clear of CareerQuest 726900802; issue-attached quests leave `SpecialQuestType` empty (survive `OnGameLoaded` via the issue-link branch). Localization: `taom_lotr_issue_strings.xml` (308 keys; defaults also embed inline so text renders pre-translation), registered as a GameText node + 8th `<LanguageFile>` across 12 languages. New-campaign feature (a pre-suppression save keeps in-flight vanilla issues whose behavior is gone). Known v1 limitation: all 27 Combat configs share `typeof(CombatLotrIssue)` → engine throttles them as one issue-type bucket (plan Risk #5). Issue #291. See `docs/features/lotr-issues.md`.) |
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
| SettlementFood config | `Main/_Module/ModuleData/settlement_food/settlement_food_config.json` (8 food knobs: garrison/prosperity consumption divisors, town/castle base food, per-village multiplier, flat bonus, storage caps; ships at vanilla values; validated — divisors ≥ 1, floats finite ≥ 0, invalid reverts to vanilla default; singleton-cached → app restart to reload) |
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
| `TaomVolunteerModel` | `DefaultVolunteerModel` | `MaxVolunteerTier => 6` (vanilla 4) + alignment-gated recruitment (`MaximumIndexHeroCanRecruitFromHero` returns -1 to block recruiting at an enemy-aligned settlement — AlignmentRecruitment feature) |
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
| `TaomPartyNavigationModel` | `DefaultPartyNavigationModel` | **PARKED 2026-06-26 — NOT registered (commented out in `SubModule.cs`, #120/#296); vanilla model is used.** Naval travel: grants naval capability + makes water terrain navigable (faithful port of NavalDLC's `NavalPartyNavigationModel`, ship-gate → config/MCM gate); the same model drives `DisableUnwalkableNavigationMeshes` so water navmesh stays enabled. Sailing is player-initiated via `sailModifierKey` (NavalTravel feature, #296) |
| `TaomMarriageModel` | `DefaultMarriageModel` | NazgulFamily: the 9 Ringwraith lord ids (Witch-King + Khamûl + 7 Nazgûl) are ineligible for marriage so they never gain a spouse/children — overrides `IsSuitableForMarriage` + `IsCoupleSuitableForMarriage` (false for wraiths); everything non-wraith falls through to vanilla. Paired with the `heroes.xslt` family strip + `NazgulFamilyBehavior` retro-clear |

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
| `Patch17_TroopWeight` | Troop weight system (heavy troops cost more party-size budget) + phantom-wounded display fix | `PartyBase.NumberOfAllMembers`/`NumberOfRegularMembers` getters, `RecruitmentVM`, `PartyVM`; **+4 display Postfixes** (`CampaignUIHelper.GetMainPartyHealthTooltip`/`GetPartyHealthTooltip`, `GameMenuPartyItemVM.RefreshCounts`, `Helpers.PartyBaseHelper.GetPartySizeText`) that rewrite battle-ready/wounded with a weighted split so the weight surplus no longer renders as phantom wounds. Display-only — `NumberOfHealthyMembers` intentionally NOT weighted (it feeds gameplay). See `docs/features/troop-weight-system.md`. |
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
| `Patch40_HideoutDescription` | Themed LOTR hideout encounter descriptions — Postfix re-sets the `HIDEOUT_DESCRIPTION` GameText var for TAOM's 5 bandit cultures, replacing vanilla's hardcoded-culture default "(Undefined hideout type)". Delegates to `IHideoutDescriptionService` (string→string, ADR-007 clean); runs before menu render so the lazy `{HIDEOUT_DESCRIPTION}` substitution picks up the override. Only `hideout_place` shows the var; `hideout_after_wait` needs no patch. | `HideoutCampaignBehavior.game_menu_hideout_place_on_init` (private, Postfix) |
| `Patch42_CastleRecruitment` | Castle troop recruitment — AI half (player menu + notable spawn/fill + issue suppression live in `CastleRecruitmentBehavior`, not Harmony). Two transpilers swap the single `!settlement.IsCastle` AI-scoring gate for a runtime toggle (`CastleAiToggle.IsCastleAndAiDisabled`, same Settlement→bool stack shape) so AI lords score + travel to castles like towns; a postfix invokes the private `CheckRecruiting` for an AI party in a non-besieged castle (bound once to an open delegate — no per-call alloc). Transpilers pin to the FIRST `get_IsCastle` + require a nearby anchor (`GetAvailableWageBudget` / `IsSettlementSuitableForVisitingCondition`), else fail-safe to vanilla. | `AiVisitSettlementBehavior.AiHourlyTick` (Transpiler), `AiVisitSettlementBehavior.FillSettlementsToVisitWithDistancesAsDays` (Transpiler), `RecruitmentCampaignBehavior.HourlyTickParty` (Postfix) |
| `Patch44_CCNameAutofill` | Pre-fills the CC Review-stage "Enter your name" field with a culture-appropriate first name when blank — Postfix on `CharacterCreationReviewStageVM`'s 6-arg constructor calls the VM's own public `ExecuteRandomizeName()` (draws from `SelectedCulture` + `Hero.MainHero.IsFemale`) only when `Name` is empty, so a typed name is never clobbered and the field stays editable. Runs at the Review stage because gender is finalized there. Companion to the family-name fix in `FactionMap.CultureSettingService` (assigns `Hero.MainHero.Culture` before `SetSelectedCulture` so the clan name comes from the selected culture's `<clan_names>`, not the stale default — that part is a service edit, not a patch). | `CharacterCreationReviewStageVM..ctor` (Postfix) |
| `Patch46_TournamentDwarfDismount` | Dwarf tournament dismount — Postfix on the public `TournamentFightMissionController.PrepareForMatch` (SandBox.dll). The horse comes from the culture tournament *weapon template* (`CultureObject.TournamentTeamTemplatesFor*Participant` / `tournament_template_empire_*`) cloned into `participant.MatchEquipment`, NOT from `GetParticipantArmor` (which only fills armor slots 5–9 via `AddRandomClothes`). The postfix iterates `____match` (the private `_match` field; **four** underscores = Harmony's `___` prefix + `_match`) teams/participants and, for any participant whose race `ITournamentService.ShouldDismountInTournament` returns true (currently dwarves — custom skeleton clips inside the mount), clears `EquipmentIndex.Horse` + `HorseHarness` (`AddEquipmentToSlotWithoutAgent(slot, EquipmentElement.Invalid)`). Single chokepoint covers both the visual spawn (`SpawnAgentWithRandomItems`) and AI `Simulate`. Keyed on race (not culture) — catches a dwarf in any town + the player. Decision uses validate-before-lookup via `IRaceManager` (`IsValidRaceId`→`GetRaceNameFromId`→case-insensitive `dwarf`); resolves race through the same `IRaceManager` as `EyeHeightAdjustmentHook`, plus the `IsValidRaceId` guard that hook lacks. Lazy-resolve like Patch40. | `TournamentFightMissionController.PrepareForMatch` (Postfix) |
| `Patch47_SpiderDeathDismount` | Spider rider-death AV mitigation. A rider dying seated on the non-vanilla spider mount AVs inside native `Agent.Die` (1.4.6 melee-death: Die-path reads float-bits-as-index from a corrupted action record, debugger-proven). Prefix hard-dismounts via the engine's private `SetMountAgent(null)` (cached `AccessTools`) so the rider dies the proven on-foot death; a dying spider frees its rider first. Spider-only; body try/catch'd. | `Agent.Die` (Prefix) |
| `Patch48_SpiderHitDismountGuard` | Non-lethal sibling of Patch47 (debugger-proven + in-game-confirmed 2026-06-15). A finite real-melee `CanDismount` hit on a SURVIVING mounted Spider Rider AVs inside native `Agent.HandleBlowAux` reading `0x3` (`MeleeHitCallback -> Mission.RegisterBlow -> Agent.RegisterBlow -> HandleBlow -> HandleBlowAux`). Same broken non-vanilla mounted-dismount path; Patch47 only covers death. Prefix strips `BlowFlags.CanDismount` when the victim's mount is the spider Monster -> native dismount never fires, rider stays on the locked mount, damage still applies. Delegates `IsSpiderMonster` to `ISpiderAttackService`. Spider-only (elephant mahout latent). | `Agent.HandleBlowAux` (private, Prefix) |
| `Patch49_ArmyGatheringNreGuard` | Map-tick CTD guard (crash report 2026-06-17). Vanilla `Army.FindBestGatheringSettlementAndMoveTheLeader` NREs at `settlement.GatePosition` (Army.cs:726, v1.4.6) when a besieger army can't resolve a gathering fortification, or at `Kingdom.Settlements` (line 659) when the army leader's clan is kingdomless — fired from `Army.OnSiegeStarted` during an AI siege start. No TAOM patch is on the stack; `Patch22_ArmyTargeting`'s aggressive cross-map siege steering just makes it more reachable. Finalizer swallows ONLY `NullReferenceException` → the army skips relocating its gathering leader this tick (vanilla already null-guards `AiBehaviorObject` downstream at Army.cs:480-490/564) and re-plans next tick. Lives in `Main/Features/ArmyTargeting/Hooks/`. | `Army.FindBestGatheringSettlementAndMoveTheLeader` (private, Finalizer) |
| `Patch50_DropFlaggedItemGuard` | Warg-on-warg bite NRE guard (crash report 2026-06-17; caught, non-fatal log spam). The shared synthetic-bite path (`CustomAttacksUtils.TakeDamage` → `Mission.RegisterBlow` → `Agent.HandleBlow` → `Mission.OnAgentHit`) calls `affectedAgent.CheckToDropFlaggedItem()` (Mission.cs:5609) on the victim; when the victim is a non-vanilla mount (a warg biting another warg) it passes the `CanWieldWeapon` guard but `Equipment[wieldedIndex].Item` is null → `.ItemFlags` NRE (Agent.cs:3595). Finalizer swallows ONLY `NullReferenceException`; damage is applied upstream in `HandleBlow` so the bite still lands, and the only skipped effect (a flagged-item drop) doesn't apply to a mount. Covers warg + spider. Lives in `Main/Features/AdvancedCombat/Hooks/`. | `Agent.CheckToDropFlaggedItem` (public, Finalizer) |
| `Patch53_PartyIconScale` | Campaign-map party-icon figure/mount scale. Transpiler rewrites the two hardcoded `0.3f` scale literals in `MobilePartyVisual.AddCharacterToPartyIcon` (people = `ldc.r4 0.3`→`callvirt Scale`; mount = `ldc.r4 0.3`→`mul`) into a `call PartyIconScaleConfig.GetScale()` so both honour the MCM "Map Figure Scale" slider (default 0.15 = half vanilla 0.30, range 0.05–1.0; `FiniteFloatValidator`-guarded). Stack-neutral in-place swap (labels preserved); animation-math `/0.3f` (`div`) literals not matched; missing-site fail-safe (warn, keep vanilla, never throw). Static IL-call-target pattern mirrors `CastleAiToggle`. Coexists with the BannerColorPersistence Postfix on the same method. `Main/Features/PartyIconScale/`. See `docs/features/party-icon-scale.md`. | `MobilePartyVisual.AddCharacterToPartyIcon` (private, Transpiler) |
| `Patch54_NavalTravelBoatVisual` | **PARKED 2026-06-26 — not applied (commented out in `SubModule.cs`, #120/#296).** Renders an at-sea party as a boat — the base game renders NO ship at sea without `NavalDLC.View` (it omits the leader figure + adds nothing). Two Postfixes share `UpdateBoat`: `OnTransitionEnded` drives add/remove on the embark/disembark (the at-sea change does NOT trigger an icon rebuild, so the rebuild hook alone never saw it), `AddMobileIconComponents` re-adds on rebuild. Adds the base-game `Native` `boat_sail_on` mesh (also `map_icon_ship`; no DLC) scaled `boatScale` to the party's `StrategicEntity`, tag-idempotent (`taom_naval_boat`). `Main/Features/NavalTravel/Hooks/`. NavalTravel feature #296. | `MobilePartyVisual.OnTransitionEnded` + `.AddMobileIconComponents` (Postfix ×2, SandBox.View) |
| `Patch56_SceneNotificationVisualGuard` | Become-king (and sibling) cinematic CTD guard (crash reports 2026-06-24/25 — become ruler of a kingdom). Becoming ruler raises the engine's `BecomeKingSceneNotificationItem` (`scn_become_king_notification`, from `DefaultCutscenesCampaignBehavior.OnKingdomDecisionConcluded`), which renders ~20 culture characters through the raw scene-notification path `GauntletSceneNotification.OpenScene` → `PopupSceneSpawnPoint.InitializeWithAgentVisuals`. That engine method derefs the human `AgentVisuals` with NO null guard (`PopupSceneSpawnPoint.cs:91/92` + the unconditional else `:108/109` — `_humanAgentVisuals.GetEquipment().Clone(false)`); the mount IS guarded (foot characters), so the asymmetry is the engine bug. One character's null/unbuildable visual NREs (managed `System.NullReferenceException`, HResult 0x80004003) → CTD. **Finalizer** on the private `OpenScene` swallows ONLY `NullReferenceException` (returning to `OnTick` lets `:135` `_isPendingSceneLoad=false` run → no re-crash loop), so a cinematic that CAN render still plays and one that would crash aborts. **Deferred close** (deep-review MED): the finalizer does NOT call `HideSceneNotification()` synchronously — that would release input/focus only for `OnTick:127-129` to re-lock them one line after the swallowed `OpenScene` returns (campaign-map soft-lock). Instead it raises a `CloseRequested` flag consumed by a sibling **Postfix on `OnTick`** that runs `MBInformationManager.HideSceneNotification()` AFTER the OnTick body, so the input/focus release wins. Generic by design — also covers KingdomCreated/JoinKingdom/Marriage/death notifications. Fourth raw custom-race/visual render path (after Patch55). Registered in `SubModule.OnGameInitializationFinished` (campaign-only cinematic). Companion **diagnostic Prefix** on `PopupSceneSpawnPoint.InitializeWithAgentVisuals` replicates the engine's own first derefs (`GetCopyAgentVisualsData()` then `GetEquipment()`) and logs which fails (pure logging) so the next occurrence self-identifies the culprit. `Main/Features/HeroRace/Hooks/`. | `GauntletSceneNotification.OpenScene` (private, Finalizer) + `.OnTick` (Postfix, deferred close) + `PopupSceneSpawnPoint.InitializeWithAgentVisuals` (public, diagnostic Prefix) |
| `Patch57_NavalAtSeaLandRescueGuard` | **PARKED 2026-06-26 — not applied (commented out in `SubModule.cs`); unnecessary while the model is unregistered (nothing reaches sea), needed again on re-enable.** Native-AV CTD guard for NavalTravel (#296; crash report 2026-06-25). Enabling naval travel lets a party reach `IsCurrentlyAtSea`, which activates the vanilla `AIMoveToNearestLandBehavior.AiHourlyTick` (inert in vanilla TAOM — nothing ever reaches sea). It calls the native cross-region land-pathfind `MapScene.GetNearestFaceCenterForPositionWithPath` (`maxDist=MapDiagonal/2`, `excludedFaceIds=GetInvalidTerrainTypesForNavigationType(All)={7,13,14,21,22}`), which dereferences the naval region-map navmesh **TAOM_Map never builds** (#120) → `0xC0000005` reading `0x4` on the hourly AI tick, for ANY at-sea party (AI, and the player once sailing works). A native AV is a corrupted-state exception a managed Finalizer can't reliably catch (unlike Patch49/50's managed-NRE finalizers), so the fix is the **prevent-the-call Prefix** pattern of Patch47/48: skip the behavior while the feature is enabled. Player disembark is unaffected (it routes through `CanPlayerNavigateToPosition`, not this behavior); non-at-sea parties already early-return, so the only behavioral change is preventing the crash. Targets the internal vanilla type by name (`AccessTools.TypeByName`, drift-safe: a bind failure logs + no-ops rather than failing module load); decision = pure `INavalTravelService.ShouldSuppressAtSeaLandRescue` (= `IsEnabled`). `Main/Features/NavalTravel/Hooks/`. | `AIMoveToNearestLandBehavior.AiHourlyTick` (internal, Prefix) |
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

**Always use `taom-src` first.** It runs `ilspycmd` against the installed v1.4.6 DLLs and caches under `~/.taom-src/v1.4.6/` (the script auto-detects the version from `Version.xml`, so older `~/.taom-src/v1.4.5/` + `v1.3.15/` caches remain on disk but are unused). The v1.4.5 dump at `E:\Decompiled_Bannerlord\` is fine for browsing namespaces/patterns, but it is one version behind installed; for authoritative method signatures always prefer `taom-src` against the installed (v1.4.6) DLLs.

| Step | Action | When |
|------|--------|------|
| 0. **[Engine process docs](./docs/reference/engine/)** | Pre-filtered, TAOM-relevant, file:line-cited docs for 19 engine subsystems | **First** for "how does X work" questions (lifecycle, formation, mount/rider, campaign-mission seam, heartbeat, spawn pipeline). Saves raw decompile time when the process is already documented. |
| 1. **`pwsh tools/taom-src.ps1 path <Type>`** | One command — decompiles the installed (v1.4.6) DLL on cache miss, returns absolute path | **For signature verification** (Harmony patch, GameModel override, adapter, API call) — authoritative; run after you understand the process conceptually |
| 2. **Browse `E:\Decompiled_Bannerlord\`** | `Read` / `Grep` / `find` against the v1.4.5 dump (see folder layout below) | Finding which DLL a class lives in, exploring a namespace tree |
| 3. **ILSpy MCP** | `mcp__ilspy__decompile_type` / `mcp__ilspy__list_types` | Fallback if `taom-src` fails (e.g., need a full DLL type listing) |

See `.claude/skills/taom-src/SKILL.md` for full usage. Composes with standard tools:
```bash
rg "GetCharacterWage" $(pwsh tools/taom-src.ps1 path TaleWorlds.CampaignSystem.GameComponents.DefaultPartyWageModel)
```

**Decompiled source layout** (`E:\Decompiled_Bannerlord\` — for browsing only, never signatures):

> ⚠️ **The category folders below are the SHIPPING-CLIENT decompile — they STRIP editor-only code.** Editor-only
> managed types (`EditorGame`, `MBEditor`, `AnimalSpawnSettings`, `VertexAnimator`, FBX-import / scene / animation
> authoring) live ONLY in the **wEditor** build of the *same-named* DLLs — **"absent from this dump" ≠ "doesn't
> exist."** Lookup order: **shipping → if missing, the editor build → if still missing, it's native (Qt/C++).** For
> both builds side-by-side use the dual-build decompile at `E:\Decompiled_Bannerlord\{_shipping_build,_editor_build}\`
> (regen: `tools/decompile_bannerlord.ps1`); inspect native DLLs with `tools/pe_inspect.py`. Full map (builds,
> managed-vs-native, the Mono/PhysX/Granite/DX11 engine stack, FBX→tpac pipeline):
> [docs/reference/bannerlord-engine-and-toolchain.md](docs/reference/bannerlord-engine-and-toolchain.md).

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
| `config-protection.sh` | PreToolUse (Edit\|Write) | Blocks edits to CLAUDE.md, Directory.Build.props, ADRs without explicit request |
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
| `check-native-dll-crt.sh` | PreToolUse (Bash) | Hard-blocks `git commit` when the staged vendored `TAOM.NativeSkinFixes.dll` links a dynamic/debug CRT (imports `vcruntime*`/`msvcp140*`/`ucrtbase*`/`api-ms-win-crt*` via `tools/pe_inspect.py`). Those runtime DLLs are absent on players' machines without Visual Studio → `LoadLibrary` Win32 error 126 → feature inert. The DLL must link a static CRT (Debug `/MTd`, Release `/MT`). Fail-open: not staged / no python / DLL absent never blocks. |

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

- Target: Bannerlord v1.4.6 (installed game version; the `E:\Decompiled_Bannerlord\` dump is still v1.4.5 — `ilspycmd` on the installed 1.4.6 DLLs is authoritative)
- `E:\Decompiled_Bannerlord\` holds the fresh v1.4.5 dump (re-decompiled 2026-05-22) — **this is the SHIPPING-CLIENT decompile (strips editor code).** For editor-only code use the dual-build decompile `E:\Decompiled_Bannerlord\{_shipping_build,_editor_build}\` (see the ⚠️ note under "Decompiled source layout" + [docs/reference/bannerlord-engine-and-toolchain.md](docs/reference/bannerlord-engine-and-toolchain.md)). Browse for patterns; `ilspycmd` on installed DLLs at `%BANNERLORD_GAME_DIR%\bin\Win64_Shipping_Client\` remains authoritative for signatures.
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
| `tools/extract_perks.py` | Parse the decompiled `DefaultPerks.cs` (v1.4.6) into a perk catalog: 374 perks × 18 skills × 12 tiers (levels 25→300), each pair with role + numeric bonus + `{VALUE}`-rendered effect. Output `tools/data/bannerlord_perks.json` (committed) + `tools/reports/lord-balance/perks.html`. Re-run after an engine bump. See `docs/features/lord-perk-review.md`. | `--defaultperks <path>`, `--stdout` |
| `tools/analyze_lord_balance.py` | **Read-only** per-culture lord stats + perk review (lord analog of `analyze_troop_balance.py`). Resolves authoritative skills via `skill_template`→`taom_lord_skill_sets.xml` (engine ignores inline `<skills>`); emits one HTML per culture + `index.html` — flat per-lord table (18 skills, magnitude-colored total) + every unlocked perk per lord (deduped by SkillSet) + data-quality (unresolved templates, inline/SkillSet drift). See `docs/features/lord-perk-review.md`. | `--stdout`, `--culture <name>` |
| `tools/rebalance_troops.py` | Balance troop skills onto the per-(level,group) baseline + per-culture `CULTURAL_MODS` curve. Covers all 16 cultures (incl. `goblin`/`mistymountainorcs`/`dale`); `detect_culture` routes id-based elite sub-lines to their own modifier (`iron_hills_*`→`iron_hills`, `mordor_uruk_*` Black Uruks→`mordor_uruk`, `orthanc_*`→`isengard_orthanc`) and maps `rhun_new`→`rhun`; `SKIP_TROOP_IDS` excludes `cave_troll` + `harad_elephant_rider`; militia take the L21 baseline by design (intentionally tough for siege/village defense). See `docs/features/troop-skill-balance.md`. | `--dry-run`, `--apply` |
| `tools/analyze_troop_balance.py` | **Read-only** per-culture troop balance overview (companion to `rebalance_troops.py` — imports its curve verbatim). Emits a color-coded HTML report + markdown + JSON to `tools/reports/troop-balance/` (heatmap parity matrix, outliers, upgrade/weight cross-refs, data-quality findings, **level-monotonicity check** = no lower level stronger than a higher level, militia-excluded). Run before any rebaseline. | `--outlier-threshold N`, `--stdout` |
| `tools/rebalance_armor.py` | Balance armor stats | `--dry-run`, `--apply` |
| `tools/rebalance_weapons.py` | Balance weapon stats | `--dry-run`, `--apply` |
| `tools/generate_gondor_armor.py` | Phase-1 Gondor armor item author (Anorien/MT/Osg/Cair/Ith) — writes to `lotraom-assets` | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_gondor_armor_phase2.py` | Phase-2 author for 8 missing Gondor families (Lossarnach/PG/Har/Anf/Sere/Leb/Bel/Lam) — defaults to Steam install (issue #99) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/apply_gondor_troop_revamp.py` | Mechanical EquipmentRoster swap for 107 Gondor troops + delete orphan blocks (issue #99) | `--dry-run`, `--apply` |
| `tools/validate_gondor_refs.py` | Underwear-bug gate (Gondor-only legacy): cross-checks `sk_gd_*` refs in `troops_gondor.xml` against Armory | (no flags) |
| `tools/generate_mordor_armor.py` | Mordor armor author — generic orc pool `sk_gn_orc_*` (9 helmet shapes) + `sk_md_orc_*` paint chests/pauldrons/bracers/boots (issue #211) + KEYforce Morannon sub-line `sk_md_mor_*` (92 items: Arc/Inf/Pik lines × light/med/heavy + 6 elite helmets, Pik shares Inf bracers, greaves shared). All helmets emit `hair_cover_type="all"` + `beard_cover_type="all"`; all bracers explicitly emit `covers_hands="false"`. | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_isengard_armor.py` | Isengard `sk_is_orc_*` paint helmets + `clo_urukscout_*` cloth overlays (issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_dolguldur_armor.py` | Dol Guldur `sk_dg_orc_*` paint helmets (Brt+Vgd excluded per spec; issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_erebor_armor.py` | Erebor Iron Hills `sk_dwarf_iron_*` author — parses spec at runtime; **defaults to `iron_hills/` folder** (NOT `erebor/`; issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/generate_rhun_armor.py` | Rhun final Loke-Rim elite helmets — closes the 22-item gap (issue #211) | `--dry-run`, `--apply`, `--armory-path` |
| `tools/validate_all_troop_refs.py` | **Generic multi-culture validator** (preferred over `validate_gondor_refs.py`) — cross-checks `sk_*/ar_*/clo_urukscout_*/urukscout_*` refs across all 7 culture troop XMLs (issue #211) | (no flags) |
| `tools/validate_moduledata.py` | **Unified schema-driven cross-reference + validation engine** (read-only; preferred over the two per-task validators above). Driven by `tools/schemas/*.json` (source of truth) + `tools/taom_schema.py`. Catches broken Item/NPCCharacter/Culture/PartyTemplate refs, duplicate NPC/culture/roster/Armory-item ids, missing civilian `equipmentType`, invalid `default_group`. Adopted from `TheOldRealms/TOR_Tools` (MIT). Unit-tested (`tools/tests/test_validate_moduledata.py`). See `docs/features/moduledata-validation.md` + `.claude/rules/moduledata-validation.md`. | `--game-modules`, `--moduledata`, `--json`, `--code`, `--warnings-as-errors` |
| `tools/taom_query.py` | Query API over the validation engine (`item_exists` / `troop_exists` / `culture_exists` / `find_references` / `validate` / `list_cultures`). Pure stdlib; backs the MCP server. Unit-tested (`tools/tests/test_taom_query.py`). | (library) |
| `tools/taom_mcp_server.py` | **MCP stdio server** (`taom-moduledata` in `.mcp.json`) exposing the query API as 9 tools for interactive agent use. FastMCP; needs the `mcp` SDK; restart Claude to load. | `python tools/taom_mcp_server.py` (smoke-test) |
| `tools/rollback_erebor_iron_misfile.py` | One-off cleanup script: removes mis-filed `sk_dwarf_iron_*` items from `erebor/` (used once during #211 deep-review RCA) | `--dry-run`, `--apply` |
| `tools/apply_mordor_troop_revamp.py` | Mechanical EquipmentRoster swap + 21 new orc/Nurn Warg/Black Uruk troops + 14 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_isengard_troop_revamp.py` | EquipmentRoster swap + 13 new `isengard_orc_*` troops (Section 1 of spec); `orthanc_*` line preserved (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_dolguldur_troop_revamp.py` | EquipmentRoster swap (17 refits) + 12 deletes (old Khamul stubs + berserker line); flexible indent regex (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_gundabad_troop_revamp.py` | EquipmentRoster swap + 1 new `gundabad_bolgs_ironfang` T8 + 4 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_erebor_troop_revamp.py` | EquipmentRoster swap (41 refits) + 13 new `iron_hills_noble_*` troops; 0 deletes (issue #212) | `--dry-run`, `--apply` |
| `tools/cleanup_deleted_troops_212.py` | Sweep deleted-troop refs from `taom_partyTemplates.xml`, `troop_weights.xml`, `troop_resource_costs.xml` (issue #212) | `--dry-run`, `--apply` |
| `tools/expand_party_templates_212.py` | Insert new troops into `kingdom_hero_party_<culture>_template` blocks via positional splice (issue #212) | `--dry-run`, `--apply` |
| `tools/apply_gondor_polish_224.py` | **Delta-style** Gondor equipment polish: per-slot `set`/`clear`/`replace` ops + 2 new PG cavalry NPCs + upgrade-target patch (issue #224, distinct from full-roster swap pattern) | `--dry-run`, `--apply` |
| `tools/audit_cc_bonuses.py` | Audit + rebalance character-creation skill/attribute/focus bonuses per culture. Report mode: per-stage uniformity, per-culture worst-case concentration (value-aware), vanilla-budget comparison, full menu dump. Reads the 6 `charactercreation/*_menu.json` + `cultures.json` + career eligibility from `taom_careers.xml`. `--apply` zeroes the career-stage payload + culture-base bonus to match vanilla's 5-stage budget (formatting-preserving line edits, writes `.bak`). | `--report` (default), `--out`, `--export-csv`, `--dry-run`, `--apply` |
