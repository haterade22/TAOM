# Rules Catalog — `.claude/rules/`

> Extracted from CLAUDE.md 2026-08-05 (eager-context diet round 2). CLAUDE.md keeps the
> `paths:`-convention note + a stub; this file holds the full rule → scope → content table.
> Source of truth for each row is the rule file's own frontmatter — update BOTH when a rule's
> `paths:` or description changes (`/skill-stocktake` checks for drift).

## Load convention

A rule with a `paths:` array loads **conditionally** when a matching file is opened. A rule
**without** `paths:` (omit the field entirely) loads **at conversation start** for every session.
`paths: ["**/*"]` is NOT the same as omitting `paths:` — the former is still conditional under
the rule loader. Doc-backed facts: `.claude/rules/harness-facts.md` "Rule loader (memory) semantics".

## Always-load rules (no `paths:` — full text in context every session)

| Rule | Content |
|------|---------|
| `environment-failures.md` | Report environment failures (missing tools, paths, MCP down). Don't auto-fix infra. |
| `harness-facts.md` | Pinned Claude Code load semantics, hook lifecycle, rule loader rules with doc URLs. Source-of-truth for harness behavior. |
| `simplicity-criterion.md` | Yes/No matrix for evaluating whether a change is worth keeping. Tiny gain + ugly code is rejected; deletions that hold parity always win. |
| `think-before-coding.md` | Surface load-bearing assumptions before the first Edit; ask if uncertain. Don't ask on trivial/mechanical work. Lightweight design pass (one question at a time, propose 2-3 approaches) for open-ended work. Reuse-before-write ladder (engine API → existing service/adapter → one-line delegation → minimal new code) before writing new code. |
| `evidence-over-claims.md` | Verify a review finding before implementing it; never sycophantically agree; no "done" claim without fresh verification output (subagent self-reports don't count). |
| `response-style.md` | Open every reply with scrutiny, not agreement (challenge / name the gap when load-bearing); tag every response `[Certain]`/`[Likely]`/`[Guessing]`. |
| `ai-prose-style.md` | Keep AI-writing tells (significance inflation, vague attributions, rule-of-three, filler, generic conclusions) out of produced prose (commits, CHANGELOG, issues, docs, RCAs). Carves out TAOM's em-dash/boldface house style. Full reference + deep-clean: `/humanizer`. |

## Path-scoped rules (load when a matching file is opened)

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
| `external-skill-ports.md` | `.claude/skills/**/SKILL.md` | Authoring a skill from scratch + per-field checklist for porting from external suites (gstack, etc.). |
| `hook-authoring.md` | `.claude/hooks/**` | Hook authoring conventions: sibling-mirroring, two-stage git-commit matcher, amend handling, log rotation |
| `native-cpp-ports.md` | `Dependencies/**/*.cpp\|h`, `Main/SceneScripts/**` | 6-point C++ port audit (hot-path logging, SEH specificity, offsets, atomics, SRWLock, C++ deep-review) |
| `moduledata-validation.md` | `troops/`, `characters/`, `equipmentsets/`, `taom_spcultures.xml`, `taom_partyTemplates.xml`, `named_companions/`, wanderers + education templates, `tools/schemas/*.json` | Run `python tools/validate_moduledata.py` before committing ModuleData edits; schemas are source-of-truth |
| `vanilla-data-comparison.md` | `**/settlements.xml`, `**/sp_battle_scenes.xml`, `**/spcultures.xml`, `**/taom_spcultures.xml`, `**/spclans.xml`, `**/spkingdoms.xml`, `**/*.xslt` | Compare against current installed vanilla before modifying mirrored data. Vanilla renames/removes scenes & re-schemas XML between versions → stale TAOM refs crash. Scene-ref audit tools + post-bump checklist. |
