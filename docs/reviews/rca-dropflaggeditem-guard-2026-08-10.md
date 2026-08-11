# RCA — Patch50 DropFlaggedItemGuard Prefix (2026-08-10)

**Scope:** `/deep-review` of the Prefix added to `Patch50_DropFlaggedItemGuard`, which stops TAOM's
synthetic creature bites from making vanilla `Agent.CheckToDropFlaggedItem` throw an NRE on every
warg engagement. 5 agents, no Codex pass.

**Top line: zero functional defects in the shipped code. Every confirmed finding was a false claim
in prose I wrote about the code** — three in the patch doc-comment and registry, one in the co-op
registry reason string. The code did what it should; the documentation described a world I had not
actually verified. That is the pattern worth extracting, because inaccurate patch documentation is
what sent the *previous* investigation of this same bug down a wrong path for eight weeks.

A fifth finding — a real gap in creature target-selection — is pre-existing, out of this
changeset's scope, and its recommended fix was **wrong**; see #5.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|-----|-----|----------|------------|-------------------|
| 1 | MED | Patch doc + registry described `Mission.OnAgentHit` as *the* call site. The engine has **three**: `Agent.OnMount` (:12142), `Agent.OnDismount` (:12167), `Mission.OnAgentHit` (:57869). The Prefix runs on every mount/dismount too. | Documentation / scope | I traced the path from the crash stack backwards and stopped when I reached the frame I was debugging. Never asked the inverse question: *who else calls the method I am patching?* | New lesson (below). Grep every call site of a patched method before writing when it runs. |
| 2 | MED | Co-op registry reason claimed "both branches end in 'no item dropped'". False — `DropFlaggedItemGuard`'s own remarks document a divergence where vanilla drops the item and the skip does not. | Self-contradiction | Wrote the co-op justification from the *intent* of the guard, not by re-reading the divergence note I had written 20 minutes earlier in the same changeset. | Fixed. When two artifacts in one changeset describe the same behavior, diff them against each other before shipping. |
| 2b | LOW | The *replacement* justification then cited a `DropOnAnyAction` census scoped to `Native`/`SandBoxCore`/`SandBox`/`CustomBattle`/`StoryMode`. Two of five files carrying the flag sit outside that set (`NavalDLC`, `SandBoxCoreMP`). | Scoped search | I inherited the agents' module list instead of globbing every installed module. Both reviewing agents made the same omission — one caught `NavalDLC` on a late background sweep, neither found `SandBoxCoreMP`. | Fixed by re-censusing all modules, which produced a *structural* claim instead of a name list: all 41 items carrying the flag are `Type="Thrown"`. Prefer an invariant over an enumeration — it does not rot when a module is added. |
| 3 | LOW | Doc-comment said the null-`Equipment` agent was "half-built **or mid-teardown**". `Agent.Clear` (:5194) zeroes every native pointer but does **not** null `Equipment`, and `Equipment` is assigned exactly once. The window is spawn-time only. | Unverified inference | "Character is null" → I reached for both plausible causes and wrote both rather than checking which was real. | Fixed. Verified `Agent.Clear` and the single assignment site. |
| 4 | LOW | Doc claimed the Finalizer "covers whatever the predicate fails to anticipate — including a shape neither observation has shown us." Its reach is NRE-only; an `AccessViolationException` from the zeroed pointer getters is deliberately rethrown. | Overstated safety claim | Described what I wanted the backstop to be worth instead of reading the filter I had left in place. | Fixed, with the reason stated: an AV should reach the crash reporter, not be absorbed. |
| 5 | HIGH (pre-existing, NOT this changeset) | No filter in the creature-attack pipeline excludes an agent that is `IsActive()` but not yet built. `SpatialGrid.cs:20`, `AgentAdapter.CustomAttack`, `BoneCheck`, `WargAttackService`, `SpiderAttackService`, `ElephantLikeAttackTasks`, `CustomAttacksUtils.TakeDamage` all check only liveness. | Root cause vs symptom | Not missed — flagged in the plan and CHANGELOG as the open question. The Prefix treats the symptom deliberately. | Own issue. **The reviewing agent's proposed fix is wrong** — see below. |

## The agent's recommended fix for #5 does not work

The data-flow agent recommended `SpatialGrid.cs:20` → `if (!agent.IsActive() || !agent.HasBeenBuilt)`.
`Agent.Build` (Agent.cs:5168) reads:

```csharp
BuildAux();
HasBeenBuilt = true;                                                                 // :5171
Controller = ...
Formation = ...
MissionGameModels.Current?.AgentStatCalculateModel.InitializeMissionEquipment(this);  // :5174
```

`HasBeenBuilt` goes true **three statements before `Equipment` exists**. The proposed filter narrows
the window; it does not close it. Any future filter must gate on `Equipment != null` (or
`Character != null`), not on `HasBeenBuilt`.

Independent corroboration that the unbuilt state is expected here: `Agent.OnMount` calls
`CheckToDropFlaggedItem()` *before* its own `if (HasBeenBuilt)` block.

## Root-cause pattern: prose confidence outrunning verification

Findings 1–4 are one failure in four costumes. Each is a sentence I wrote about engine behavior
without running the check that would confirm it, in a session where I was otherwise careful to
verify every *code* claim against the installed decompile. The asymmetry is the lesson: the
evidence bar dropped the moment I switched from writing C# to writing about C#.

This is not cosmetic in this repo. The reason this bug took from 2026-06-17 to 2026-08-10 to
diagnose is that Patch50's original doc-comment confidently asserted the victim was a mount. That
single unverified sentence is why the live debugger reading was surprising. Finding 1 was me about
to hand the next session the same gift.

`.claude/rules/evidence-over-claims.md` §C already forbids stating unverified facts. Its examples
are all tool output, counts, and signatures — the artifacts of *doing* work. Nothing in it names
doc-comments and design rationale as covered surface. That scope gap is the systemic finding.

## Why each agent missed what it missed

| Agent | On findings 1–4 | Why |
|---|---|---|
| 1 Standards | Missed all | Its checklist is structural (ADRs, regions, naming, IoC). Nothing asks whether a comment is true. Correctly reported PASS for what it was asked. |
| 2 Compatibility | Caught #4; missed #1 | Verified every API *the Prefix calls*. Call sites of the *patched* method were outside its brief. Its own reasoning on #4 was partly wrong — it claimed AVs are uncatchable CSEs; TAOM's launcher config enables `legacyCorruptedStateExceptionsPolicy` and Patch62 documents catching one. Right conclusion, wrong mechanism. |
| 3 Efficiency | Missed #1, and it mattered | Costed the Prefix at "once per agent-hit". With three call sites the real frequency is higher. Its summary also claimed a saving on unarmed agents where the Prefix returns `true` and vanilla re-reads the flags — that is +1 read, not a saving. Its own duplication section had it right and the summary contradicted it. |
| 4 Completeness | Missed #2 | Marked CHANGELOG claims "verified" citing the patch registry — a doc from the same changeset. Circular. It also asserted "the commit exists"; no commit was made. |
| 5 Data flow | Caught #1, #2, #3 | The only agent whose brief is "trace it through the whole system." Earned the review. |

Consistent with the skill's own note that Agent 5 catches what per-file review cannot. Worth
recording that three of five agents produced at least one confidently wrong statement of their own
(#2's CSE mechanism, #3's saving claim, #4's commit claim and circular sourcing) — a returned
report is a claim to verify, not evidence.

## Lesson to codify

Appended to `docs/reviews/lessons/harmony-il.md`:

> **Before documenting when a patch runs, grep every call site of the patched method.**

## Not fixed, recorded instead

- **#5** — target-filter gap. Own issue; affects warg, spider, elephant and mûmakil identically.
  Correct gate is `Equipment != null`, not `HasBeenBuilt`.
- **Finalizer swallows silently.** A third, still-unknown NRE shape would vanish with no evidence
  trail — the same discoverability failure that hid this bug. Consistent with the Patch47/48 bare-
  `catch {}` precedent, so not a regression, but a sample-gated log line (mirroring
  `CustomAttacksUtils.ReportSkippedNonFiniteBlow`) would close it.
- **Two `[Ignore]`d tests in `WargAttackServiceTests.cs`** assert the pre-2026 bone set `{23}` and
  radii `0.4f`/`0.3f`; the source now uses the 10-bone cone and `1.0f`/`0.5f`. They would fail if
  un-ignored.
