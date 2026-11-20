using TAOM.Features.AdvancedCombat;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// The AI half of the Exhausted lock (#1497): while <see cref="Set"/> has switched it on, it takes the attack, defend,
/// kick and (on foot) jump out of the input the AI just wrote for this soldier. The engine calls it when it sets an
/// AI soldier's input, once the soldier's AI input callback is on (AiInputCallbackClaims). The calling thread is not
/// known, so it is written for any thread: it reads three volatile flags the mission tick writes and the thread
/// guard's volatile thread id, and no other state. Bit arithmetic and one counter: nothing here can throw
/// into native code. The ticker puts it on the soldier as the last component on each entry to Exhausted, so its strip
/// runs after any component that adds an input (Marshal's Word's hold adds an attack).
/// </summary>
internal sealed class StaminaInputComponent : AgentComponent
{
    private readonly StaminaTelemetry _telemetry;
    private volatile bool _deny;
    private volatile bool _mounted;
    private volatile bool _inWater;

    public StaminaInputComponent(Agent agent, StaminaTelemetry telemetry)
        : base(agent)
    {
        _telemetry = telemetry;
    }

    /// <summary>Main thread, from the mission tick, on every look at the soldier: where he is now decides whether he keeps the jump.</summary>
    internal void Set(bool deny, bool mounted, bool inWater)
    {
        _mounted = mounted;
        _inWater = inWater;
        _deny = deny;
    }

    public override void OnAIInputSet(ref Agent.EventControlFlag eventFlag, ref Agent.MovementControlFlag movementFlag, ref Vec2 inputVector)
    {
        if (!_deny)
            return;
        var movement = (uint)movementFlag;
        var events = (uint)eventFlag;
        var strippedMovement = StaminaInputRules.StripMovement(movement);
        var strippedEvents = StaminaInputRules.StripEvents(events, _mounted, _inWater);
        if (strippedMovement == movement && strippedEvents == events)
            return;
        movementFlag = (Agent.MovementControlFlag)strippedMovement;
        eventFlag = (Agent.EventControlFlag)strippedEvents;
        // Which thread native calls this on is not known (spike S2 of #1497): the count answers it.
        _telemetry.CountAiStrip(MissionThreadGuard.IsOnMainThread);
    }
}
