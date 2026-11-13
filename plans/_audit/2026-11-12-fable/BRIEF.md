# BRIEF: /improve run 2026-11-12-fable

Read this file first. It applies to every agent of this run.

## Who you are, and the baseline

You are one agent of an `/improve` run on TAOM. The maintainer asked for a repo-wide review that leads
to refactors of code, processes, workflows and documentation. The goals are readability,
maintainability, performance, effectiveness and efficiency. **Functionality must stay the same.**

- Baseline: `7087fa633` on `bannerlord-1.5.x`, version `v2.0.38`, 2026-11-12.
- The code under study is the detached worktree your prompt names. It is at the baseline. Do not read
  the main checkout's working tree as evidence.
- This run's changes go on the branch `improve/2026-11-12-run`. Nothing goes to the trunk.

## What this run wants from an audit lane

A finding is useful to this run when a later executor can fix it **without changing behaviour**:

- duplicated logic that can share one implementation;
- dead code, unreachable scaffolds, never-read fields, tools nobody calls;
- a file or class that mixes several reasons to change, with a safe first extraction;
- a measured or countable cost (per frame, per agent, per tick, at load, per tool call, per test run);
- a process step, hook, skill, rule or doc that costs time or context and can be made cheaper;
- a doc that is wrong, duplicated or stale, where the fix is a deletion or a correction.

A bug you find on the way is still worth one finding, marked `Plan candidate: no (behaviour change)`,
so that the orchestrator can open an issue. Do not hunt for bugs: no lane of this run owns
correctness, security in code, or game data.

## Evidence standard

- Cite `path:line` that you read this run, at the baseline.
- Measure, and say how, so that a checker can run the same command. A count comes from a command.
  A timing from this desktop is not a player's timing (audit-playbook "Measure, then rank").
- Try to refute each finding before you write it. Ask: is it decided, is it by design, is there a
  caller you did not find?
- An empty search proves absence only in the scope you searched. Name the scope.
- UNVERIFIED is an honest answer. A guess is not.

## Finding format

Use the format in `.claude/skills/improve/references/audit-playbook.md` "Finding format", in your
findings file. In the structured result, return one row per finding with the same id.

## Skip list: other sessions' work in flight

Do not propose changes to these areas. Other sessions hold uncommitted or unmerged work there, or run
them live tonight. A refactor there would collide.

- `Main/Features/HeroDefence`, `Client/Features/HeroDefence`, `TAOM.Tests/Features/HeroDefence`
  (branch `feat/hero-defence`, not on the trunk).
- `Main/Features/FieldBallista`, `TAOM.Tests/Features/FieldBallista` (uncommitted, another worktree).
- `Main/Features/CoopInterop`, `CoopCompat/`, `TAOM.CoopCompat.Tests/` (a live co-op test run tonight).
- `Main/Features/FordsOfWar`, `Client/Features/FordsOfWar` (a live Fords match run tonight).
- `Main/Features/LongDefeat` and its client and test folders (phases 2 to 5 in progress).
- `Main/SubModule.cs`, `Main/IoC.cs`, `Client/ClientSubModule.cs`, `Client/TaomClientComposition.cs`,
  `Main/Composition/FeatureModules.cs`: single-owner or edited elsewhere. A finding may recommend an
  edit there, but mark it `Risk: HIGH (single-owner file)`.
- `Main/_Module/ModuleData/**` and the live `TAOM_Map` and `LOTRLOME_Armory` installs: game data is out
  of this run's scope.
- `Main/_Module/ModuleData/Languages/**`: the project lead's translation files.
- `.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`, `docs/adrs/*.md`:
  the maintainer's files. A finding may propose an exact edit for him; no executor makes it.

## Decided tradeoffs (pointers; a decided thing is not a finding)

- `.ai/review-reference.md` "Intentional Patterns (Do NOT flag these)".
- `docs/ai-includes/orientation.md`, the trap index.
- `docs/adrs/` (ADR-002 entry points, ADR-003 no regions, ADR-004 no Obsolete, ADR-005 no `#if`,
  ADR-007 adapters, ADR-011 knowledge tiers).
- `plans/README.md` "Findings considered and rejected".
- `plans/_audit/2026-09-23-opus/DECISIONS.md`, `plans/_audit/2026-10-02-perf/DECISIONS.md`,
  `plans/_audit/2026-10-28-nogame-tests/DECISIONS.md`.
- Hooks fail open by decision (`.claude/rules/harness-facts.md`).

## Already done: do not report again

**Plans 001 to 042** are merged (index: `plans/README.md`). Among them: hot-path `IoC.Resolve` and
MCM settings caching (003, 031), mission diagnostics cost (030), creature-battle allocations (033),
PatchShield per-call cost (034), campaign hot paths (037), the module-XML merge at load (042), the
Bash hook prefilter (013), the composition-root first steps (018), the seam decision logic (026),
unreachable scaffolds (025). Plans 043 and 044 are open and have issues (#739, #1038).

**The Kimi run of 2026-11-07** (a different AI model, same request). It did one refactor and its
docs, and it is merged:

- `e99fcccc9`: `CharacterCreationBonusResolver` now holds the skill and attribute maps that
  `CareerMenuService` and `NarrativeMenuBuilder` each used to declare. The character-creation and
  career-selection docs were corrected in the same commit. Issue #1097.

Kimi also named four leads and did not work on them. They are leads for you, not facts. Extend them;
do not spend time re-deriving them:

1. Composition is in the middle of a migration: `Main/SubModule.cs` and `Main/IoC.cs` are hand-wired,
   while `Main/Composition/FeatureModules.cs` holds the newer declarative module list. Kimi's advice:
   move one leaf feature at a time with order and wiring tests.
2. `Main/Features/Refuge/RefugeService.cs` (1,448 lines) mixes book persistence and load repair,
   founding and upgrading, timed work, raids, visuals and engine party operations. Kimi's advice:
   characterize first, then extract a pure load-repair or raid-selection unit.
3. Performance claims need current measurements. Kimi found the older perf leads stale: the live code
   already caches settings and adapter factories. `IoC.Resolve` in `LogTask.Execute` and
   `TaomBTLogger.LogMessage` is on a logging path whose frequency nobody measured.
4. Repeated settings-access idioms in providers: a watch item. Kimi advised against a global base
   class, because the providers differ in fallback, validation and timing.

**Earlier audit records** you may use as leads (each row there is old, so re-read the code):
`plans/_audit/2026-09-23-opus/` (lane files, `triage-*.md` with 70 still-valid June findings,
`jscpd-digest.md`), `plans/_audit/2026-10-02-perf/REPORT.md`.

## Lanes of this run

| Lane | Owns |
|---|---|
| arch | C# architecture, duplication, dead code, god and shallow modules (playbook 5 and 6) |
| perf | C# performance in code added or changed since 2026-10-04, and anything the October perf run did not reach (playbook 3) |
| tests | Test suite quality and speed, test infrastructure, gates (playbook 4, test half of 7) |
| tools | Python tools under `tools/`: duplication, dead scripts, catalog accuracy, suite speed |
| harness | Processes and workflows: `.claude/` hooks, rules, skills, agents; `AGENTS.md`, `CLAUDE.md`, `.ai/`, `.githooks/`, `build.ps1`; always-on context cost; steps that repeat (playbook 7) |
| docs | Documentation: wrong, stale, duplicated or orphaned docs; index and map accuracy (playbook 8) |

Not audited by this run: correctness and security in code (playbook 1 and 2), game data (9),
direction (10).

## Output discipline

- Append to your one findings file as you go. A stall then loses nothing.
- Rank your findings by leverage (impact over effort, discounted by confidence and risk).
- At most 15 findings. Fewer, stronger findings are better than many weak ones. "Not worth doing" is a
  valid verdict: list those in one line each under `## Considered and rejected`.
- End the file with `## What I did not cover`.
