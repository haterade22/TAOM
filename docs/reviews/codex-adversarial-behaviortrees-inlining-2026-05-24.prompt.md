# Codex Adversarial Review — BehaviorTrees + BehaviorTreeWrapper Inlining (2026-05-24)

You are reviewing a TAOM commit that does THREE things in one session:

1. **Decompiled two vendored DLLs** (`Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` ~1300 LOC and `BehaviorTrees.dll` ~980 LOC) and **inlined the source into TAOM.dll**. Both vendored binaries are deleted. The libraries had no upstream source repo; this rebuild gives TAOM full ownership for the first time.
2. **Fixed a NRE-on-every-battle bug:** the vendored `BehaviorTreeMissionLogic` inherited `MissionBehavior` while reporting `BehaviorType => Logic`. Vanilla `Mission.AddMissionBehavior` does `MissionLogics.Add(missionBehavior as MissionLogic)` -- the cast returns null, null lands in `_missionLogics`, and `Mission.CheckMissionEnded` NREs every tick. Two users on `bannerlord-1.4.5` crashed on first looter battle. The fix is `BehaviorTreeMissionLogic : MissionLogic`.
3. **Cleaned up 7 perf findings inherited from the vendored DLL** (deep-review surfaced them after the rebuild made them visible). E1 HIGH, E2-E6 MED, E7 LOW. ALL fixed in the same session.

Your job: **be adversarial.** Find what Claude + deep-review missed. Prior Codex pre-reviews have caught HIGH bugs that Claude shipped past 4-5 deep-review agents.

## TAOM ID CHEATSHEET (use ONLY these)

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

(This review has NO ID-dependent work, but the cheatsheet is here for completeness.)

## READ FIRST

- `docs/reviews/rca-looter-battle-nre-2026-05-24.md` -- the RCA. Read this BEFORE looking at code; it tells you the bug, the fix, and the perf cleanup.
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md` -- the rule that should have caught this earlier.
- `CHANGELOG.md` (the 2026-05-24 "Fix(battle):" entry) -- the full per-file diff summary.

## Known Suspects -- CONFIRM or DISPUTE each

1. **`_tempMatched` shared-list re-entrancy.** `BehaviorTreeMissionLogic.FindCalledListeners` returns a SHARED `List<BannerlordBTListener>` cleared and refilled on every call. The "synchronous-dispatch contract" comment claims this is safe because no listener.Notify() re-enters FindCalledListeners on the same mission logic. **Prove this contract holds.** Walk every code path from `listener.Notify(args)` -- through `BannerlordBTListener.Notify`, `BTListener.Notify`, `IBTNotifiable.HandleNotification`, `BTEventDecorator.HandleNotification`, the BT tick chain. Does any path call back into `BehaviorTreeMissionLogic.OnAgentXxx` or `FindCalledListeners` synchronously? Specifically: `BTEventDecorator.HandleNotification` sets `Tree.NodeReceivingEvent` and `Tree.ShouldRunNextTick = true` -- does setting these flags cause an immediate re-evaluation that could re-enter? If so, `_tempMatched` is mid-iteration and will be silently truncated/expanded under the iterator's feet.

2. **`SharedRandom` thread-safety.** `BehaviorTreeAgentComponent` now uses a static `SharedRandom` instead of `new Random()` per agent. `System.Random` is **NOT thread-safe** -- concurrent calls can return 0 or return the same value or corrupt the internal state silently. Bannerlord's agent spawning runs from `MissionState.Tick`, which has worker threads (the `_MT` suffix convention in TaleWorlds code -- see `feedback_detect_engine_threading_via_mt_suffix.md`). **Verify: is `BehaviorTreeAgentComponent` ever constructed from worker threads?** If yes, `SharedRandom.NextDouble()` is a race condition. The original `new Random()`-per-agent was thread-safe by isolation; the "fix" may have introduced a real bug.

3. **`OnEndMissionInternal` clear-then-Dispose ordering.** Clears `actions`/`tickListeners`/`trees` BEFORE calling `BehaviorTreeBannerlordWrapper.Instance.Dispose()`. Dispose sets `CurrentMissionLogic = null` and `_disposed = true`. But what if a listener.Subscribe() is called between the Clear and the Dispose? With `actions.Clear()` having already run, a new Subscribe would create a fresh List<> and add the listener -- but Dispose() then nulls the parent reference and the listener is orphaned. Also: are there any threads still firing `OnAgentXxx` events after `OnEndMissionInternal` starts but before Dispose finishes?

4. **`NotifyAll` iterator vs Unsubscribe.** `NotifyAll` is `for (int i = 0; i < listeners.Count; i++) listeners[i].Notify(data);`. If a listener's Notify path calls `BannerlordBTListener.UnSubscribe()` (which calls `BehaviorTreeBannerlordWrapper.Instance.UnSubscribe(this)` -> `CurrentMissionLogic?.UnSubscribe(listener)` -> `actions[listener.SubscribesTo].Remove(listener)`), the underlying List<> mutates mid-iteration. If `_tempMatched` is the iterated list (it is, because that's what FindCalledListeners returns), the Remove on `actions[key]` is a DIFFERENT list -- so `_tempMatched` itself doesn't mutate, but the listener instance is now removed from `actions` while still in our iteration. Is that OK? What if the listener's Notify clears subscriptions wholesale?

5. **`OnEndMissionInternal` is a `MissionBehavior` virtual, not a `MissionLogic` virtual.** Verify with ilspycmd that `OnEndMissionInternal()` is correctly overridable on the MissionLogic chain in v1.4.5. The original DLL had this override; the rebuild preserves it. But the inheritance changed from MissionBehavior to MissionLogic -- does the v1.4.5 MissionLogic class shadow/seal/rename this method? If MissionLogic has its own end-of-mission hook that differs, our cleanup may not fire.

6. **C# 12 primary-constructor conversion of `RandomSelector`/`Selector`/`Sequence`.** The decompile had `internal class RandomSelector(BehaviorTree tree, ...) : BTControlNode(tree, name, decorator, children, weight)`. We rewrote as plain ctors with base() forwarding. Verify the converted ctors match the primary-constructor parameter binding order and defaults exactly. A missing default or reordered parameter would silently change behavior at every caller.

## File lists

**Library — new TAOM-owned source (decompiled from former vendored DLLs):**

```
Main/BehaviorTrees/BehaviorTreesCore.cs
Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs
Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs           <-- BUG FIX + perf fixes
Main/BehaviorTreeWrapper/BehaviorTreeBannerlordWrapper.cs
Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs         <-- perf fix E7
Main/BehaviorTreeWrapper/BannerlordLogger.cs
Main/BehaviorTreeWrapper/SubscriptionPossibilities.cs
Main/BehaviorTreeWrapper/Extensions.cs                         <-- perf fix E6
Main/BehaviorTreeWrapper/AbstractDecoratorsListeners/BannerlordBTListener.cs
Main/BehaviorTreeWrapper/AbstractDecoratorsListeners/BannerlordBTTickListener.cs
Main/BehaviorTreeWrapper/AbstractDecoratorsListeners/BannerlordConstantEventListener.cs
Main/BehaviorTreeWrapper/AbstractDecoratorsListeners/BannerlordEventDecorator.cs
Main/BehaviorTreeWrapper/BlackBoardClasses/IBTBannerlordBase.cs
Main/BehaviorTreeWrapper/Decorators/AlarmedDecorator.cs
Main/BehaviorTreeWrapper/Decorators/HealthBelowPercentageDecorator.cs
Main/BehaviorTreeWrapper/Decorators/HitDecorator.cs
Main/BehaviorTreeWrapper/Decorators/InPositionDecorator.cs
Main/BehaviorTreeWrapper/Decorators/WaitNSecondsTickDecorator.cs
Main/BehaviorTreeWrapper/Tasks/ClearMovementTask.cs
Main/BehaviorTreeWrapper/Tasks/HealTask.cs
Main/BehaviorTreeWrapper/Tasks/MoveToPlaceTask.cs
Main/BehaviorTreeWrapper/Tasks/PlayAnimationTask.cs
Main/BehaviorTreeWrapper/Tasks/PrintTasks.cs
Main/BehaviorTreeWrapper/Tasks/SetAiStateFlag.cs
Main/BehaviorTreeWrapper/Tasks/SleepTask.cs
Main/BehaviorTreeWrapper/Trees/PerformAnAttackTree.cs
```

**Tests:**

```
TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs
```

**Modified service files (one-line rename — OnTickAsAI -> OnTick):**

```
Main/Features/Spider/SpiderMissionBehavior.cs:152
Main/Features/Warg/WargMissionBehavior.cs:127
```

**Build wiring:**

```
Main/TAOM.csproj  -- dropped <Reference Include="BehaviorTrees"> and <Reference Include="BehaviorTreeWrapper">
```

**Deleted (gone from disk + git):**

```
Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll
Main/_Module/bin/Win64_Shipping_Client/BehaviorTrees.dll
```

## REQUIRED SECTIONS

### VANILLA CODE (decompile yourself, paste here)

Use ilspycmd against the INSTALLED v1.4.5 DLLs at `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/`. Decompile and paste verbatim:

1. `TaleWorlds.MountAndBlade.Mission.AddMissionBehavior` -- must show the `switch (BehaviorType) case Logic: MissionLogics.Add(... as MissionLogic)` to confirm the bug pattern.
2. `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded` -- must show the `foreach (MissionLogic ml in MissionLogics) ml.MissionEnded(out _)` to confirm the NRE site.
3. `TaleWorlds.MountAndBlade.MissionLogic` class declaration AND every virtual method it overrides from MissionBehavior. Note any abstract methods that BehaviorTreeMissionLogic doesn't implement.
4. `TaleWorlds.MountAndBlade.AgentComponent.OnTick(float)` virtual signature -- to confirm the v1.3->v1.4.5 rename from OnTickAsAI is correct.
5. `TaleWorlds.MountAndBlade.MissionBehavior.OnEndMissionInternal` virtual signature -- confirm it's still virtual + accessible on the MissionLogic inheritance chain.

### Hot-path safety analysis

For each of the 7 perf fixes (E1-E7), produce one of:
- CONFIRMED-SAFE: fix is correct, no edge case
- BUG: describe the edge case that breaks the fix
- DEGRADED: fix works but reintroduces a different problem

Specifically address suspects 1-4 above.

### CONFIG CROSS-REFERENCE

N/A -- this change has no JSON/XML config files.

### FINDINGS OR OBSERVATIONS

Use this exact template per finding:

```
### Finding N: [SEVERITY] -- [one-line title]

**File:** path/to/file.cs:LINE
**Category:** [hot-path / threading / inheritance / lifecycle / API mismatch / dead code / other]
**Confidence:** HIGH | MED | LOW

**Claim:** [what is wrong]

**Vanilla evidence (if applicable):**
[paste decompiled code block]

**Proposed fix:**
[concrete change]
```

If no findings, say so explicitly. We've had reviews try to invent findings to look productive -- don't.

## QUALITY GATES

- Every Known Suspect MUST have a CONFIRMED or DISPUTED verdict. "Need more info" is not acceptable -- if you can't reach a verdict, that itself is the finding (and explain what you couldn't access).
- Every finding referencing a vanilla method MUST include the decompiled signature pasted from ilspycmd output. No "I assume the signature is X."
- DO NOT flag patterns that match vanilla as bugs. The BT library uses `new object[] { x }` arg arrays because that's how `IBTNotifiable.HandleNotification(object[] data)` is shaped -- that's the framework API, not optional.
- DO NOT flag the inherited perf patterns (E1-E7 list) as bugs again -- those are already fixed. Focus on whether the FIXES are correct or whether new bugs were introduced.

## Prior review lessons

SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches. Hot-path threading caught race conditions in `_MT`-suffixed code paths (SmartCavalryAI Codex pass found NaN propagation through Clamp by walking the actual transform chain).

FAILURES: Codex has assumed empire=Rohan (it is Dunland). Codex has flagged vanilla-matching code as bugs. Codex has skipped hard sections by saying "out of scope." Codex has reported "found no source" when grep would have located it.

## Output

Write your review to: `docs/reviews/codex-adversarial-behaviortrees-inlining-2026-05-24.md`
