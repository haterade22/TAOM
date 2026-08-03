# RCA — BattleLoadDiagnostics agent-build instrumentation (deep review, 2026-08-03)

**Scope reviewed:** the `AgentBuildDone` phase, the `race=` / `monster=` / `actionSet=` / `from=`
tokens, and the `from=` formatter fix. Issue [#372](https://github.com/haterade22/TAOM/issues/372).
6 agents: Standards, API Compatibility, Efficiency, Completeness, Data Flow, Tooling.

**Outcome:** 2 confirmed findings, both mine, both fixed in-session. 1 HIGH refuted. 2 pre-existing
findings recorded and left out of scope. Build + 4795 tests green after the fixes.

## Findings

| # | Sev | Finding | Category | Why missed | Preventive action |
|---|-----|---------|----------|------------|-------------------|
| 1 | MED | `FaceGen.GetRaceNames()` allocates a fresh 15-element array **per agent** (`(string[])_raceNamesArray.Clone()`, MountAndBlade/FaceGen.cs:125) to read one element — 648 allocations in one observed arena load | Perf / engine-API choice | I picked the API whose *name* matched what I wanted ("race names") and never opened its body. The adjacent `GetBaseMonsterNameFromRace(int)` indexes the same array with no allocation. | Fixed: use `GetBaseMonsterNameFromRace` + `GetRaceCount()`. Rule below. |
| 2 | LOW | Comment mislabelled `Mission.cs:4041` as "action-set resolution"; `SetActionChannel(0, GetCurrentAction(0))` plays an *action* on channel 0 and does not resolve the action *set*. Propagated into the feature doc. | Doc accuracy | I paraphrased the engine line while writing the surrounding narrative instead of reading what the call does. The wrong label then got copied into `battle-load-diagnostics.md` verbatim. | Fixed in both places. Covered by the existing `evidence-over-claims` §C rule; no new rule needed. |

### Refuted — Efficiency agent's only HIGH

**Claim:** 1944 synchronous disk flushes per 648-agent load, "adding 2–20+ seconds"; recommended
downgrading `AgentBuildDone` to DEBUG or batching.

**Refuted two ways:**

1. **Mechanically.** `_logFile` is a `StreamWriter` (`FileLogger.cs:24`). `StreamWriter.Flush()`
   flushes the managed buffer into the OS file cache; it never calls `FlushFileBuffers`. The agent
   asserted *"SYSCALL TO OS (e.g. `FlushFileBuffers()` on Windows)"* without reading what `_logFile`
   is, then derived a 0.1–10 ms per-call disk-latency figure from that assumption.
2. **Empirically.** In `taom_debug_2026-08-03_12-08-46.log`, 429 agents × 3 stamps = 1287 durable
   writes span `t=+8137ms` → `t=+8282ms` — **145 ms total, ~0.11 ms per stamp**, on a 9.3 s load.
   The third stamp costs ≈48 ms, about **0.5 %** of load time.

Had this been actioned, it would have downgraded `AgentBuildDone` to DEBUG — the async path — which
is **exactly the durability the stamp exists for**: a native CTD drops the DEBUG queue, so the stamp
would have been absent from every crash log it was added to produce. A plausible-sounding perf
finding would have silently destroyed the feature.

### Recorded, out of scope (pre-existing, not introduced here)

- **ADR-007 surface violation** — `IMissionDiagnosticService.LogMissionStartSnapshot` accepts
  `IReadOnlyList<MissionBehavior>` / `IReadOnlyList<MissionLogic>`. The file is **unmodified** in
  this changeset; last touched 2026-05-24 by the original feature commit. Worth its own issue: if
  diagnostic inspection of engine state is a deliberate ADR-007 carve-out, it should be written into
  the ADR rather than existing as an undeclared exception.
- **Latch closer-gate timing** — `BattleLoadPhaseBehavior` (the loading window's only closer) is
  added only `if (battleLoadDiagSvc.IsEnabled)` at `SubModule.cs:1280`, a later and separate
  evaluation from the opener's check. A toggle flip in the gap would latch the window open until the
  next `Mission.Initialize`. Both hooks pre-date this changeset; risk is theoretical (same
  synchronous call chain). Matches `.claude/rules/harmony-patches.md` "closer coverage per opener
  path". Investigate only if a stale-open window is ever observed in a real log.

### Parallel Python tooling in the same tree (not this changeset)

The tooling agent verified `docs/reviews/rca-validator-silent-scope-2026-08-03.md` against the code:
all 5 claimed fixes are real. It found 4 new issues in the **new** `tools/audit_siege_props.py`
(MED `root.iter("game_entity")` indexing nested children as top-level prefabs — 2,921 confirmed name
collisions; MED unfiltered `Prefabs_Unused/` sweep; MED no tests for it or for the `audit_mount_parity`
section-F fix; LOW-MED unreported settlement skip counts). Those belong to that work, not this one.

Note for whoever runs the tools suite: `python -m pytest tools/tests/ -q` fails with
`ValueError: I/O operation on closed file` during pytest's capture teardown — an environment defect,
not a code failure. `pytest -s` → 54 passed; `python -m unittest discover -s tools/tests` → **270
passed**.

## Root-cause pattern

Both confirmed findings are the same mistake in two registers: **I described engine behaviour from
the name of a thing instead of from its body.** `GetRaceNames` sounded like an accessor, so I used it
without reading that it clones. `SetActionChannel` sat on the line where I expected action-set
resolution, so I labelled it that. Neither is a knowledge gap — both bodies are three lines long and
were one `ilspycmd` away.

The reviews caught this asymmetrically, and that is the interesting part. The Efficiency agent
*asked the right question* about `GetRaceNames` but marked it "INVESTIGATE — implementation not
visible in this codebase" and moved on, then spent its confidence on a HIGH it had not verified. The
Data Flow agent decompiled `Mission.cs` and caught the comment. **The agent that decompiled found
real defects; the agent that reasoned from plausibility produced one deferral and one false HIGH.**

## Why each agent missed what it missed

| Agent | Result | Why |
|---|---|---|
| 1 Standards | Passed the changeset; found the pre-existing ADR-007 surface | Correct. Its rules are structural — neither confirmed finding is a standards question. |
| 2 API Compatibility | 11/11 verified, 0 incompatible | Verified every signature against installed DLLs including the risky private `Mission.BuildAgent` binding. Its brief was *"does this resolve?"*, not *"does it allocate?"* — allocation is Agent 3's beat. Scope-correct. |
| 3 Efficiency | Found #1 but deferred it; produced a false HIGH | Deferred the one thing only a decompile could settle, then asserted a disk-flush cost it never checked. **Prompt gap:** the agent had no instruction to decompile an engine method whose cost it is judging. |
| 4 Completeness | COMPLETE | Correct. Both findings are inside code that exists and is tested. |
| 5 Data Flow | Caught #2; 10 flows all connected | The only agent that read the engine to check a claim rather than to check a signature. |
| 6 Tooling | 4 findings, all in the parallel Python work | Scope-correct. |

## Preventive actions

### 1. An engine method's COST is a decompile question, not an inference (new)

Added to `docs/reviews/lessons/adapters-taleworlds-api.md`. When a review judges the cost of an
engine call on a hot path — or when writing one — read the method body. `GetRaceNames()` vs
`GetBaseMonsterNameFromRace()` differ by one `.Clone()` and by 648 allocations per battle load, and
nothing in either name says so.

### 2. Efficiency-agent prompt gap (deep-review skill)

The Efficiency agent is asked to flag allocations in TAOM code but is never told to **decompile
engine methods it suspects**. It produced exactly the predicted failure: "verify this externally"
for the real finding, and a confident unverified HIGH elsewhere. The `/deep-review` Agent 3 prompt
should carry the same instruction Agent 2 has — *use `taom-src` / `ilspycmd` on the installed DLLs
before asserting or deferring on an engine call's cost* — and should be told that an unverified cost
claim must be reported as UNVERIFIED, never as HIGH.

### 3. A durability downgrade is a feature change, not a perf tweak

Recorded in the lessons entry. Any proposal to move a diagnostic stamp from INFO to DEBUG in this
codebase must state what happens to that stamp in a hard CTD. For `[BattleLoad]` stamps the answer
is "it disappears", which defeats the stamp's entire purpose. This is the second time the
INFO-durability contract has needed defending — `LogTaomBehaviorAdded` carries a code comment for
the same reason.

## Verification

- `./build.ps1 -RunTests` after the fixes — **4795 passed / 0 failed / 2 skipped**
- BindingVerification gate — 61 passed (enumerates `[HarmonyPatch]` types dynamically, so it covers
  the new private `Mission.BuildAgent` target)
- Live in-game: two tournament loads (648 and 429 agents) with `AgentEquipBegin` / `AgentEquipOk` /
  `AgentBuildDone` balanced 1:1:1 and `from=` printing the real caller chain
