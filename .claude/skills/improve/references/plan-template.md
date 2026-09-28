# Handoff Plan Template (TAOM)

<!-- Ported from shadcn/improve @ 5428507 (2026-06-12), MIT (c) 2026 shadcn.
     Commands, git workflow, and conventions recalibrated to TAOM. -->

Every plan is written for an executor that has **zero context**: it has not seen the run, the audit,
the other plans or any conversation. Assume it follows explicit instructions well and is weak at
filling gaps, recovering from ambiguity, or knowing when to stop. Writers get a verified brief from the
orchestrator; a fresh cold reviewer then reads the plan as that executor would, and a reviser fixes what
it finds (`plans.js`).

Three properties make a plan executable:

1. **Self-contained context**: paths, code excerpts, conventions and commands are all in the file.
2. **Verification gates**: every step ends with a command and its expected result, so the executor
   never has to judge whether it succeeded.
3. **Hard boundaries and escape hatches**: an explicit out-of-scope list, and STOP conditions instead of
   improvisation when reality does not match the plan.

File naming: `plans/NNN-short-slug.md`, numbered in recommended execution order, continuing the index's
numbering.

**TAOM additions every plan carries:**

- **TDD**: the failing test comes before the implementation (AGENTS.md "TDD").
- **Blast radius**: for every C# type the plan changes, the output of
  `python tools/graphify_taom.py affected "<Type>" --depth 2`, quoted in Current state
  (AGENTS.md "Blast radius first").
- **Engine facts**: signatures quoted from `pwsh tools/taom-src.ps1 path <Type>`; the executor never
  guesses TaleWorlds behaviour.
- **Convention pointers**: the ADRs and rules that bind the change, one line each (for example ADR-007
  adapters, `gamemodels.md` no inline branching); the executor has not read them.
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` edits appear in Scope
  as exact edits, or the plan says "recommend, don't edit".
- **Protected files** (`.claude/settings.json`, `.claude/settings.local.json`, `Directory.Build.props`,
  `docs/adrs/*.md`): a Step 0 for the maintainer with a check the executor runs first. The orchestrator
  collects every Step 0 of a plan set and asks once.
- **No worktree or branch names**: the executor works in the worktree and branch it was given.
- **Orchestrator steps**: what an executor cannot do (it cannot invoke skills) is listed for the
  orchestrator: the issue, `/localize` for new player-facing text, the feature doc and its feature-map
  row (AGENTS.md "Documentation duty").
- **Dependent plans** name the plan and the branch whose tip they build on.

---

## Template

```markdown
# Plan NNN: <imperative title: what will be true after this plan>

> **Executor instructions**: Follow this plan step by step. Run every verification command and
> confirm the expected result before moving on. If anything in "STOP conditions" occurs, stop and
> report; do not improvise. Work in the worktree and on the branch you were given. The orchestrator
> keeps `plans/README.md`; do not edit it.
>
> **Drift check (run first)**: `git diff --stat <planned-at SHA>..HEAD -- <in-scope paths>`. If an
> in-scope file changed since this plan was written, compare the "Current state" excerpts with the
> live code; a mismatch is a STOP condition.

## Status

- **Priority**: P1 | P2 | P3
- **Effort**: S | M | L
- **Risk**: LOW | MED | HIGH
- **Depends on**: plans/NNN-*.md, built on the tip of its branch (or "none")
- **Category**: bug | security | perf | tests | tech-debt | migration | dx | docs | data | direction
- **Planned at**: commit `<short SHA>`, <YYYY-MM-DD>
- **Baseline at that commit**: dotnet <totals line>, failing: <names and why, or none>; Python suite
  failing: <names, or none>
- **Issue**: <#NNN, or "filed by the orchestrator before execution">

## Why this matters

Two to five sentences: the problem, its concrete cost, and what improves when this lands. Intent is what
lets a correct judgment call happen when a detail is off.

## Current state

The facts the executor needs, inlined, never "as discussed" or "see the audit":

- The relevant files, each with one line on its role:
  `Main/Features/X/XService.cs`: owns the decision logic; the bug is at lines 40 to 60.
- Excerpts of the code as it exists at the planned-at commit, with `file:line` markers, from the
  writer's own reads.
- The conventions that apply, with one exemplar: "Services never touch TaleWorlds types directly
  (ADR-007); see `Main/Adapters/AgentAdapter.cs` and its use in
  `Main/Features/Warg/WargAttackService.cs`. Match it."
- Engine facts: signatures quoted from `taom-src`, null behaviour, save-compat constraints.
- Blast radius: the `graphify_taom.py affected` output for each changed type.

## Step 0: the maintainer's edit (only when a protected file changes)

The exact edit, file by file, for the maintainer to make before dispatch, and the check the executor
runs first: `<command>` gives `<expected>`. If it does not, STOP: Step 0 is not done.

## Commands you will need

| Purpose | Command | Expected on success |
|---|---|---|
| Build | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=` | exit 0, 0 errors |
| Tests | `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` | the baseline's totals plus the new tests |
| One test class | the same, plus `--filter "FullyQualifiedName~XServiceTests"` | the named tests run; a filter matching nothing proves nothing |
| Python tools | `python -B -m unittest discover -s tools/tests -t .` | the baseline's failure set, no new names |
| Data | `python tools/validate_moduledata.py` | 0 ERRORs |
| Hooks (hook plans) | `CLAUDE_PROJECT_DIR="<worktree>" timeout 1500 bash tools/test_hooks.sh > "<log>" 2>&1`, run with `run_in_background` (it outlasts a foreground call) | "N passed, 0 failed" under Summary in the log, once it finishes |
| Bindings (patch plans) | the binding gate from `docs/ai-includes/agent-operating-manual.md` | all pass |

Both flags go on build AND test, and prefix dotnet with the TEMP and TMP your dispatch rules give.
Never `./build.ps1`: it deploys into the game install.

## Scope

**In scope** (the only files you modify):
- `Main/Features/X/XService.cs`
- `TAOM.Tests/Features/X/XServiceTests.cs`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs`: single-owner; if a registration is needed, STOP and report the exact line.
- Any save-format change (a new SyncData field) not listed above.

## Git workflow

- Commit on the branch you were given; never push or open a PR.
- Subject `<type>(<scope>): <version> - <description>`, at most 72 characters, `<version>` being the
  `<Version>` in `Main/_Module/SubModule.xml` (a hook refuses any other). Example:
  `fix(settlements): <version> - guard the null Village on castles`.
- The body is the changelog entry: what changed and why, for a reader of the release note, wrapped at
  72. Never edit `CHANGELOG.md`. No AI attribution trailer. Optional trailers: `Constraint:`,
  `Rejected:`, `Not-tested:`, `Research:`, `Save-compat:`.

## Steps

### Step 1: record the base

Run the full suite before any edit and write its totals and failing tests into your report.

**Verify**: the totals match "Baseline at that commit" above, or the difference is explained.

### Step 2: <imperative title; for a fix, usually the failing test>

What to do, precisely, naming exact files and symbols. Give the target code shape when it is
load-bearing. For a RED step: build the project that holds the test, run it with a named filter, and
name the diagnostics or assertion the failure must show, never the compiler's exact text.

**Verify**: `<command>` gives `<expected output>`.

### Step 3: ...

(Each step small enough to verify on its own, ordered so the tree is never broken between steps.)

## Test plan

- New tests, in which file, covering which cases: the happy path, the regression this plan fixes, named
  edge cases, and one test per (input x branch) cell for dispatch logic.
- The existing test to use as the pattern: "model after
  `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs`".
- What cannot be tested (live Harmony invocation, engine calls): only those lines, named for the
  commit's `Not-tested:` trailer.
- A deletion proves its RED in the report (the test that fails before the deletion), not with a
  permanent test that something is absent.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] The build command exits 0
- [ ] The test command matches the baseline plus the new tests for <X>, which pass
- [ ] `git grep -n "<old pattern>"` over the whole repo, tests included, returns nothing
- [ ] `git status --porcelain` lists only in-scope files
- [ ] Every comment, doc line and test oracle this plan supplied was re-checked against the code it
      describes

## STOP conditions

Stop and report (do not improvise) if:

- The code at the "Current state" locations does not match the excerpts.
- A step's verification fails twice after a reasonable fix.
- The fix seems to need an out-of-scope file, especially `IoC.cs`, `SubModule.cs`, the csproj or a
  protected file.
- A TaleWorlds signature or behaviour differs from "Current state"; report the mismatch, do not
  decompile and improvise.
- The assumption "<key assumption>" is false.

## Orchestrator steps (not the executor's)

- Issue: <#NNN, or file it before dispatch>.
- `/localize` for the new player-facing text: <keys>.
- `docs/features/<name>.md` and its row in `docs/reference/feature-map.md`.

## After merge: the maintainer's actions

<pull; restart sessions if hooks or settings changed; a file this plan untracks is deleted from his
checkout on pull; or "none">

## Maintenance notes

- What future changes will interact with this.
- What the review should probe (name the spots).
- Any follow-up deferred out of this plan, and why.
```

---

## Index file: `plans/README.md`

Kept by the orchestrator only; executors never edit it. Statuses are set with
`python tools/improve_ctl.py status plans/README.md <num> "<text>"`, which changes one cell and nothing
else.

```markdown
# Implementation Plans

Generated by /improve. A working backlog, not the knowledge base (feature docs live in docs/features/).
Execute in the order below unless dependencies say otherwise.

## Execution order and status

| Plan | Title | Priority | Effort | Category | Depends on | Status |
|------|-------|----------|--------|----------|------------|--------|
| [NNN](NNN-slug.md) | ... | P1 | S | tests | none | TODO |

Status values, each with a one-line reason or reference: TODO | IN PROGRESS | STOPPED | READY (branch
and tip) | PARTIAL | MERGED (commit, issue) | BLOCKED | REJECTED.

## Dependency notes

- NNN builds on the tip of MMM's branch because <reason>.

## Valid but unplanned

- <finding>: <one line>, from <run file>. Re-triaged by the next run before it audits again.

## Findings considered and rejected

- <finding>: not worth doing because <one line>, so nobody re-audits it.
```

## Cold review checklist (the reviewer in `plans.js`)

- Could an executor that has never seen this repo carry it out with only the plan and the repo?
- Does every excerpt match the code at the planned-at commit? List each mismatch.
- For a plan that ships code, lift its code blocks into scratch and run its RED set and cases; for a
  safety gate, simulate the planned gate under both tool names and report any verdict that loosens.
- Split findings into blocking (the executor would fail or ship a defect) and non-blocking.

## Quality bar: check before finishing each plan

- Every step names exact files and symbols, and every verification is a command with an expected
  result, never "make sure it works".
- A failing test precedes each C# implementation step; the binding ADRs are named with one line each.
- The STOP conditions name this plan's real risks, not boilerplate.
- A reviewer reading only "Why this matters" and "Done criteria" understands what they are approving.
- No secret values: locations and credential types only. No local absolute paths.
- "Planned at" is filled in and the drift-check paths match Scope.
- Supplied comments, doc lines and test oracles are drafts the executor re-verifies
  ([lesson](../../../../docs/reviews/lessons/build-tooling-workflow.md), "Text an `/improve` plan
  supplies").
- Every history or engine claim a prescribed sentence makes was re-derived before it was written
  ([lesson](../../../../docs/reviews/lessons/misc.md), "A handoff plan's prescribed sentence is a
  draft").
- The stale-claim grep covers the whole repo, tests included, by distinctive words ([lesson](../../../../docs/reviews/lessons/misc.md),
  "A plan's stale-claim grep is a floor").
- A new permitted case amends every general rule it is an exception to ([lesson](../../../../docs/reviews/lessons/misc.md),
  "A new permitted case amends every general rule").
- A pointer to a procedure points at the knowledge base, never at a plan ([lesson](../../../../docs/reviews/lessons/misc.md),
  "A pointer to a procedure").
- The RED step builds the project holding the test, names its filter, and names the diagnostics it
  needs rather than the compiler's text ([lessons](../../../../docs/reviews/lessons/misc.md), "A plan's
  RED step builds the project" and "A plan's RED check names the diagnostics").
- A deletion's RED lives in the log, and an "untestable" waiver covers only the engine-bound lines
  ([lessons](../../../../docs/reviews/lessons/testing-qa.md), "A deletion's RED step lives in the log"
  and "A boundary's 'untestable' waiver").
- Retiring a duty searches for the duty, not its phrases ([lesson](../../../../docs/reviews/lessons/build-tooling-workflow.md),
  "Retiring a duty").
- A dependent plan is re-anchored after its prerequisite's review lands.
- The prose has no em or en dash.
