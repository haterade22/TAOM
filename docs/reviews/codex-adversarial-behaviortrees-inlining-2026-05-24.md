# Codex Adversarial Review — BehaviorTrees + BehaviorTreeWrapper Inlining (2026-05-24)

## Inputs reviewed

- `docs/reviews/rca-looter-battle-nre-2026-05-24.md`
- `~/.claude/projects/c--Users-mikew-source-repos-TAOM/memory/feedback_missionbehaviortype_logic_requires_missionlogic_inheritance.md`
- `CHANGELOG.md` 2026-05-24 `Fix(battle)` entry
- New inlined source under `Main/BehaviorTrees/` and `Main/BehaviorTreeWrapper/`
- Warg/Spider `OnTickAsAI` -> `OnTick` callsite changes
- Deleted vendored DLLs from git via `git show HEAD:Main/_Module/bin/Win64_Shipping_Client/*.dll`

Build note: I attempted `dotnet build Main/TAOM.csproj`, but the sandbox denied SDK access to `C:\Users\mikew\AppData\Local\Microsoft SDKs`, so this review is source/decompile based.

---

## VANILLA CODE (ilspycmd, installed v1.4.5)

DLL path used: `E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/TaleWorlds.MountAndBlade.dll`.

### 1. `TaleWorlds.MountAndBlade.Mission.AddMissionBehavior`

```csharp
public void AddMissionBehavior(MissionBehavior missionBehavior)
{
    MissionBehaviors.Add(missionBehavior);
    missionBehavior.Mission = this;
    switch (missionBehavior.BehaviorType)
    {
    case MissionBehaviorType.Logic:
        MissionLogics.Add(missionBehavior as MissionLogic);
        break;
    case MissionBehaviorType.Other:
        _otherMissionBehaviors.Add(missionBehavior);
        break;
    }
    missionBehavior.OnCreated();
}
```

### 2. `TaleWorlds.MountAndBlade.Mission.CheckMissionEnded`

```csharp
private bool CheckMissionEnded()
{
    foreach (MissionLogic missionLogic in MissionLogics)
    {
        MissionResult missionResult = null;
        if (missionLogic.MissionEnded(ref missionResult))
        {
            TaleWorlds.Library.Debug.Print("CheckMissionEnded::ended");
            MissionResult = missionResult;
            MissionEnded = true;
            MissionResultReady(missionResult);
            return true;
        }
    }
    return false;
}
```

### 3. `TaleWorlds.MountAndBlade.MissionLogic`

`MissionLogic` is still `abstract class MissionLogic : MissionBehavior`. It overrides only `BehaviorType`; it adds virtual mission-result hooks. It declares no abstract methods, so `BehaviorTreeMissionLogic : MissionLogic` does not need to implement anything beyond the hooks it already overrides from `MissionBehavior`.

```csharp
public abstract class MissionLogic : MissionBehavior
{
    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Logic;

    public virtual InquiryData OnEndMissionRequest(out bool canLeave)
    {
        canLeave = true;
        return null;
    }

    public virtual bool MissionEnded(ref MissionResult missionResult)
    {
        return false;
    }

    public virtual void OnBattleEnded()
    {
    }

    public virtual void ShowBattleResults()
    {
    }

    public virtual void OnRetreatMission()
    {
    }

    public virtual void OnSurrenderMission()
    {
    }

    public virtual void OnAutoDeployTeam(Team team)
    {
    }

    public virtual List<EquipmentElement> GetExtraEquipmentElementsForCharacter(BasicCharacterObject character, bool getAllEquipments = false)
    {
        return null;
    }

    public virtual void OnMissionResultReady(MissionResult missionResult)
    {
    }
}
```

### 4. `TaleWorlds.MountAndBlade.AgentComponent.OnTick(float)`

This confirms the v1.4.5 API surface: `OnTickAsAI(float)` is gone and `OnTick(float)` is the virtual callback.

```csharp
public abstract class AgentComponent
{
    protected readonly Agent Agent;

    protected AgentComponent(Agent agent)
    {
        Agent = agent;
    }

    public virtual void Initialize()
    {
    }

    public virtual void OnTick(float dt)
    {
    }

    public virtual void OnTickParallel(float dt)
    {
    }

    // ...
}
```

Additional vanilla evidence relevant to Finding 1: `Agent.Tick(float)` automatically calls every attached component's `OnTick(dt)`.

```csharp
public void Tick(float dt)
{
    if (_changedFormationPosition.IsValid)
    {
        // ...
    }
    if (IsActive())
    {
        foreach (AgentComponent component in _components)
        {
            component.OnTick(dt);
        }
        if (Mission.AllowAiTicking && IsAIControlled)
        {
            TickAsAI();
        }
    }
    else if (MissionPeer?.ControlledAgent == this && !IsCameraAttachable())
```

And `Mission` calls mission behaviors before the agent/component tick later in the same frame:

```csharp
for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
{
    MissionBehaviors[num2].OnMissionTick(dt);
}
// ...
if (doAsyncAITick)
{
    TickAgentsAndTeamsAsync(dt);
}
else
{
    TickAgentsAndTeamsImp(dt, tickPaused: false);
}
```

### 5. `TaleWorlds.MountAndBlade.MissionBehavior.OnEndMissionInternal`

The method is still public virtual on `MissionBehavior`, therefore overridable and reachable through the `MissionLogic` inheritance chain.

```csharp
public abstract class MissionBehavior : IMissionBehavior
{
    public Mission Mission { get; internal set; }

    public IInputContext DebugInput => Input.DebugInput;

    public abstract MissionBehaviorType BehaviorType { get; }

    // ...

    public virtual void OnEndMissionInternal()
    {
        OnEndMission();
    }

    protected virtual void OnEndMission()
    {
    }

    // ...
}
```

Vanilla mission shutdown calls it on every `MissionBehavior`, not only `MissionLogic`, so the cleanup hook still fires after changing the base class:

```csharp
foreach (MissionBehavior missionBehavior in MissionBehaviors)
{
    missionBehavior.OnEndMissionInternal();
}
```

---

## Known Suspects — verdicts

1. **`_tempMatched` shared-list re-entrancy — CONFIRMED-SAFE for current handlers.** `NotifyAll` calls `BannerlordBTListener.Notify` -> `BTListener.Notify` -> `IBTNotifiable.HandleNotification`. `ConstantEventListener.HandleNotification` only delegates to the concrete `Notify`; current concrete listeners (`WargTryToGoRage`, `OnWargDied`, `OnSpiderDied`) mutate blackboard/log/controller state but do not call `FindCalledListeners`, mission `OnAgentXxx`, or `RunTree`. `BTEventDecorator.HandleNotification` calls concrete `Notify`, sets parent status to `ReceivedEvent`, assigns `Tree.NodeReceivingEvent`, and sets `Tree.ShouldRunNextTick = true`; those are plain field/property writes and do **not** immediately re-evaluate the tree. Re-evaluation happens later through the agent component tick. If future listeners call `Tree.RunTree()` or synthesize mission events synchronously from `Notify`, the contract must be revisited.

2. **`SharedRandom` thread-safety — DISPUTED as a current race.** The only `BehaviorTreeAgentComponent` constructions are in `WargMissionBehavior.OnMissionTick`, `WargMissionBehavior.OnAgentBuild`, `SpiderMissionBehavior.OnMissionTick`, and `SpiderMissionBehavior.OnAgentBuild`. Those are `MissionBehavior` callbacks on the main mission path. Vanilla worker-thread code (`TWParallel.For` -> `Agent.TickParallel` -> component `OnTickParallel`) does not construct components, and this component does not override `OnTickParallel`. `System.Random` would be unsafe if constructors are ever moved to parallel agent code, but the current call graph does not do that.

3. **`OnEndMissionInternal` clear-then-Dispose ordering — DISPUTED as a current lifecycle bug.** Vanilla `Mission.EndMissionInternal` synchronously iterates `MissionBehaviors` on the main thread, calls `OnEndMissionInternal`, and only afterwards calls `agent.OnRemove()` / `agent.OnDelete()`. There is no concurrent subscription window between `actions.Clear()` and `Dispose()` in this call path. Clearing before `Dispose()` is also compatible with later `BehaviorTreeAgentComponent.OnAgentRemoved` calls because `Dispose()` sets `_disposed = true`, making `DisposeTree` a no-op after the dictionaries are already clear.

4. **`NotifyAll` iterator vs `UnSubscribe` — CONFIRMED-SAFE for current handlers.** `_tempMatched` is a snapshot list distinct from the backing `actions[key]` lists, so even a same-notification unsubscribe would not mutate the list being iterated. The direct global-list loops would be vulnerable to synchronous unsubscription, but current global event decorators do not unsubscribe in `Notify`; `BTEventDecorator` only marks `ReceivedEvent` and unsubscribes later when the tree processes that event in `RemoveDecorator`/`RemoveDecorators`. Current constant listeners do not call `UnSubscribe` either.

5. **`OnEndMissionInternal` on `MissionLogic` chain — CONFIRMED.** `MissionLogic` inherits `MissionBehavior` and does not shadow/seal `OnEndMissionInternal`; the virtual remains accessible and is called through `MissionBehaviors` for every behavior.

6. **Primary-constructor conversion of `RandomSelector`/`Selector`/`Sequence` — CONFIRMED.** Decompiling the deleted `BehaviorTrees.dll` from `git show HEAD:...` shows `RandomSelector(BehaviorTree tree, string name, AbstractDecorator? decorator = null, List<BTNode>? children = null, int weight = 100) : BTControlNode(tree, name, decorator, children, weight)` and `Selector(BehaviorTree tree, string name, List<BTNode>? children = null, AbstractDecorator? decorator = null, int weight = 100) : BTControlNode(tree, name, decorator, children, weight)`. The inlined plain constructors preserve the same parameter order, defaults, and `base(...)` forwarding. `Sequence` was already a plain constructor in the decompile and remains equivalent.

---

## Hot-path safety analysis (E1-E7)

| Fix | Verdict | Analysis |
|---|---|---|
| E1 `_dtArgs` cached array in `OnMissionTick` | CONFIRMED-SAFE | Current tick decorators' `Notify(object[] data)` implementations are no-ops; `BTEventDecorator.HandleNotification` does not retain the array. The array is consumed synchronously. |
| E2 `EmptyArgs = Array.Empty<object>()` | CONFIRMED-SAFE | Empty notifications cannot mutate elements because length is zero; current empty-arg listeners do not retain the array. |
| E3 `_tempMatched` cached list | CONFIRMED-SAFE for current handlers | See Known Suspect 1. The current notify path does not re-enter `FindCalledListeners`; `ShouldRunNextTick` is just a flag. |
| E4 replacing `List.ForEach` closures with loops | CONFIRMED-SAFE with current listener behavior | No semantic change for snapshot lists. Direct global-list loops rely on current listeners not unsubscribing synchronously; they do not. |
| E5 clearing `actions`/`tickListeners`/`trees` on mission end | CONFIRMED-SAFE | Vanilla mission shutdown is synchronous, and later agent component cleanup is guarded by wrapper disposal. |
| E6 `GetBehaviorTree` `TryGetValue` | CONFIRMED-SAFE | Same null/current-logic behavior with one dictionary lookup instead of `ContainsKey` + indexer. |
| E7 static `SharedRandom` | CONFIRMED-SAFE in current construction path; watch future changes | Construction is from main-thread mission callbacks, not from `TickParallel`. If component construction moves to worker-thread spawning later, add a lock or use thread-local RNG. |

---

## CONFIG CROSS-REFERENCE

N/A — this change has no JSON/XML config files.

---

## FINDINGS OR OBSERVATIONS

### Finding 1: [HIGH] -- `OnTick` is now invoked twice for every BT component

**File:** `Main/Features/Warg/WargMissionBehavior.cs:127` and `Main/Features/Spider/SpiderMissionBehavior.cs:152`  
**Category:** API mismatch / hot-path  
**Confidence:** HIGH

**Claim:** After the v1.4.5 rename, `BehaviorTreeAgentComponent` overrides `AgentComponent.OnTick(float)`, and vanilla `Agent.Tick(float)` automatically calls `component.OnTick(dt)` for every component attached with `agent.AddComponent(comp)`. The Warg and Spider mission behaviors still manually call `comp.OnTick(dt)` from `OnMissionTick`, and vanilla then calls the same component again later in the same mission frame via `TickAgentsAndTeamsImp`. This doubles BT evaluation/advancement for every warg/spider and doubles the hot-path work; because the trees use 10ms root delays and stateful tasks, this can advance selectors/sequences and attacks faster than intended, not just add harmless overhead.

**Vanilla evidence (if applicable):**

```csharp
public void Tick(float dt)
{
    if (IsActive())
    {
        foreach (AgentComponent component in _components)
        {
            component.OnTick(dt);
        }
        if (Mission.AllowAiTicking && IsAIControlled)
```

```csharp
for (int num2 = MissionBehaviors.Count - 1; num2 >= 0; num2--)
{
    MissionBehaviors[num2].OnMissionTick(dt);
}
// ... later in the same tick:
TickAgentsAndTeamsImp(dt, tickPaused: false);
```

**Proposed fix:** Pick one ticking owner. Prefer letting the attached `AgentComponent.OnTick` run automatically and remove the manual `comp.OnTick(dt)` calls/loops from `WargMissionBehavior` and `SpiderMissionBehavior`; alternatively, stop adding the component to the agent and keep a manual tick method that is not an `AgentComponent` override.

### Observation 2: [MEDIUM] -- RCA root-cause claim does not match the deleted DLL in git

**File:** `docs/reviews/rca-looter-battle-nre-2026-05-24.md:13`  
**Category:** inheritance / evidence mismatch  
**Confidence:** HIGH

**Claim:** The RCA says the vendored `BehaviorTreeMissionLogic` reported `BehaviorType => MissionBehaviorType.Logic`, but decompiling the deleted DLL from `git show HEAD:Main/_Module/bin/Win64_Shipping_Client/BehaviorTreeWrapper.dll` shows the getter returns integer `1`, and the installed v1.4.5 enum decompiles as `Logic, Other` (therefore `1 == Other`). With that DLL, vanilla `AddMissionBehavior` would add the behavior to `_otherMissionBehaviors`, not insert a null into `MissionLogics`. The inlined `MissionLogic` inheritance is safe, but this evidence means the documented root cause/regression test may not actually cover the users' `CheckMissionEnded` NRE unless a different DLL build was shipped to them.

**Vanilla evidence (if applicable):**

```csharp
public enum MissionBehaviorType
{
    Logic,
    Other
}
```

Deleted DLL evidence:

```csharp
public class BehaviorTreeMissionLogic : MissionBehavior
{
    public override MissionBehaviorType BehaviorType => (MissionBehaviorType)1;
}
```

IL for the deleted DLL getter:

```il
IL_0000: ldc.i4.1
IL_0001: ret
```

**Proposed fix:** Re-run the RCA against the exact DLL build the two users had installed (or preserve that binary/hash in the RCA), correct the `BehaviorType` mapping in the docs/code comment, and add a regression test that verifies both the new inheritance and the actual old-DLL evidence being guarded against.

---

## Summary

CRITICAL: 0 | HIGH: 1 | MEDIUM: 1 | LOW: 0

VERDICT: ISSUES FOUND
