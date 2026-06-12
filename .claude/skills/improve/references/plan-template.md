# Handoff Plan Template (TAOM)

<!-- Ported from shadcn/improve @ 5428507 (2026-06-12), MIT (c) 2026 shadcn.
     Commands, git workflow, and conventions recalibrated to TAOM. -->

Every plan is written for an executor model that has **zero context**: it has not seen the advisor session, the audit, the other plans, or any prior conversation. It may be a smaller/cheaper model. Assume it is competent at following explicit instructions and weak at filling gaps, recovering from ambiguity, or knowing when to stop.

Three properties make a plan executable by a weaker model:

1. **Self-contained context** — everything needed is in the file: paths, code excerpts, conventions, commands.
2. **Verification gates** — every step ends with a command and its expected result. The executor never has to *judge* whether it succeeded.
3. **Hard boundaries and escape hatches** — explicit out-of-scope list, and "STOP and report" conditions instead of letting the model improvise when reality doesn't match the plan.

File naming: `plans/NNN-short-slug.md`, numbered in recommended execution order.

**TAOM additions every plan must carry:**

- **TDD for C# work**: steps order the failing test BEFORE the implementation (RED→GREEN→REFACTOR is a Critical Rule, not a preference).
- **Issue-first**: a GitHub issue must exist before the implementation lands (CLAUDE.md mandate). The plan's Status block records it, or the first step is "confirm/create the issue" (executors don't create issues — the orchestrator does).
- **Convention pointers**: name the specific ADRs/rules that bind this change (e.g. ADR-007 adapters, `gamemodels.md` no-inline-branching) with one-line summaries — the executor has not read them.
- **Single-owner files**: `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` edits are listed explicitly in scope or the plan says "recommend, don't edit".

---

## Template

```markdown
# Plan NNN: <Imperative title — what will be true after this plan>

> **Executor instructions**: Follow this plan step by step. Run every
> verification command and confirm the expected result before moving to the
> next step. If anything in the "STOP conditions" section occurs, stop and
> report — do not improvise. When done, update the status row for this plan
> in `plans/README.md` — unless a reviewer dispatched you and told you they
> maintain the index.
>
> **Drift check (run first)**: `git diff --stat <planned-at SHA>..HEAD -- <in-scope paths>`
> If any in-scope file changed since this plan was written, compare the
> "Current state" excerpts against the live code before proceeding; on a
> mismatch, treat it as a STOP condition.

## Status

- **Priority**: P1 | P2 | P3
- **Effort**: S | M | L
- **Risk**: LOW | MED | HIGH
- **Depends on**: plans/NNN-*.md (or "none")
- **Category**: bug | security | perf | tests | tech-debt | migration | dx | docs | data | direction
- **Planned at**: commit `<short SHA>`, <YYYY-MM-DD>
- **Issue**: <GitHub issue URL, or "create before implementation lands — orchestrator">

## Why this matters

2–5 sentences. The problem, its concrete cost, and what improves when this
lands. Written so the executor (and a human reviewer) understands the intent —
intent is what lets a correct judgment call happen when a detail is off.

## Current state

The facts the executor needs, inlined — never "as discussed" or "see audit":

- The relevant files, each with one line on its role:
  - `Main/Features/X/XService.cs` — owns the decision logic; contains the bug (lines 40–60)
- Excerpts of the code as it exists today (short, with `file:line` markers),
  enough that the executor can confirm it's looking at the right thing.
- The repo conventions that apply here, with a pointer to one exemplar file:
  "Services never touch TaleWorlds types directly (ADR-007) — see
  `Main/Adapters/AgentAdapter.cs` (`IAgentAdapter`) and its use in
  `Main/Features/Warg/WargAttackService.cs`. Match it."
- Engine facts the executor can't discover safely: verified signatures
  (from `pwsh tools/taom-src.ps1 path <Type>` during planning — quote them),
  null-behavior gotchas, save-compat constraints. The executor must NOT
  guess TaleWorlds behavior.

## Commands you will need

| Purpose   | Command                                                  | Expected on success |
|-----------|----------------------------------------------------------|---------------------|
| Build     | `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` | exit 0, 0 errors    |
| Tests     | `dotnet test TAOM.Tests -p:DisableModuleCopy=true`        | all pass            |
| Data      | `python tools/validate_moduledata.py`                     | 0 ERRORs            |

(Exact commands verified during recon, not guessed. `-p:DisableModuleCopy=true`
is required on build AND test — the tests project builds Main, whose post-build
target otherwise deploys to the game install. NEVER `./build.ps1` from an
executor — same deploy, and it must not run concurrently.)

## Scope

**In scope** (the only files you should modify):
- `Main/Features/X/XService.cs`
- `TAOM.Tests/Features/X/XServiceTests.cs`

**Out of scope** (do NOT touch, even though they look related):
- `Main/IoC.cs` — single-owner; if a registration change is needed, STOP and
  report the exact line to add instead of editing.
- Any save-format change (new SyncData fields) not explicitly listed.

## Git workflow

- Branch: work in the dispatched worktree's branch; do NOT push or open a PR.
- Commits: 50/72 rule, imperative, no AI attribution
  (e.g. `fix(x): guard null Village on castle settlements`).
  Optional trailers when relevant: `Constraint:`, `Rejected:`, `Not-tested:`,
  `Research:`, `Save-compat:`.

## Steps

### Step 1: <imperative title — for C# fixes this is usually the failing test>

What to do, precisely. Reference exact files/symbols. Include the target code
shape when it's load-bearing (the pattern to produce, not necessarily every
line).

**Verify**: `<command>` → <expected output>

### Step 2: ...

(Each step small enough to verify independently. Order steps so the codebase
is never broken between steps when possible.)

## Test plan

- New tests to write, in which file, covering which cases (happy path, the
  specific regression this plan fixes, named edge cases — enumerate the
  (input × branch) cells for dispatch logic).
- Which existing test to use as the structural pattern:
  "model after `TAOM.Tests/Features/Warg/WargAttackServiceTests.cs`".
- What is structurally untestable (live Harmony invocation, engine calls) —
  name it for the commit's `Not-tested:` trailer.
- Verification: `dotnet test TAOM.Tests -p:DisableModuleCopy=true` → all pass, including N new tests.

## Done criteria

Machine-checkable. ALL must hold:

- [ ] `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true` exits 0
- [ ] `dotnet test TAOM.Tests -p:DisableModuleCopy=true` exits 0; new tests for <X> exist and pass
- [ ] `grep -rn "<old pattern>" Main/` returns no matches
- [ ] No files outside the in-scope list are modified (`git status`)
- [ ] `plans/README.md` status row updated

## STOP conditions

Stop and report back (do not improvise) if:

- The code at the locations in "Current state" doesn't match the excerpts
  (the codebase has drifted since this plan was written).
- A step's verification fails twice after a reasonable fix attempt.
- The fix appears to require touching an out-of-scope file (especially
  `IoC.cs` / `SubModule.cs` / csproj).
- A TaleWorlds signature or behavior differs from what "Current state"
  documents — do not decompile-and-improvise; report the mismatch.
- You discover the assumption "<key assumption>" is false.

## Maintenance notes

For the human/agent who owns this code after the change lands:

- What future changes will interact with this.
- What a reviewer should scrutinize (the orchestrator runs `/deep-review`
  before commit for C# changes ≥2 files — name the spots it should probe).
- Any follow-up explicitly deferred out of this plan (and why).
```

---

## Index file: `plans/README.md`

Written once by the advisor after all plans, updated by executors:

```markdown
# Implementation Plans

Generated by /improve on <date>. Working backlog — NOT knowledge base
(feature docs live in docs/features/). Execute in the order below unless
dependencies say otherwise. Each executor: read the plan fully before
starting, honor its STOP conditions, and update your row when done.

## Execution order & status

| Plan | Title | Priority | Effort | Depends on | Status |
|------|-------|----------|--------|------------|--------|
| 001  | ...   | P1       | S      | —          | TODO   |

Status values: TODO | IN PROGRESS | DONE | BLOCKED (one-line reason) |
REJECTED (one-line rationale)

## Dependency notes

- 002 requires 001 because <reason>.

## Findings considered and rejected

- <finding>: not worth doing because <one line>. (So nobody re-audits it.)
```

## Quality bar — check before finishing each plan

- Could a model that has never seen this repo execute this with only the plan file and the repo? If any step requires knowledge from the advisor session, inline that knowledge.
- Is every verification a command with an expected result, not a judgment ("make sure it works")?
- Does every step name exact files and symbols, not "the relevant module"?
- For C# work: does a failing test precede the implementation step? Are the binding rules (ADRs) named with one-line summaries?
- Are the STOP conditions specific to this plan's actual risks, not boilerplate?
- Would a reviewer reading only "Why this matters" + "Done criteria" understand what they're approving?
- No secret values anywhere in the file — locations and credential types only.
- "Planned at" SHA is filled in and the drift-check paths match the Scope section.
