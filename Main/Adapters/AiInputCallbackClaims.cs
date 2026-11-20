using TaleWorlds.MountAndBlade;
using TAOM.Core.Collections;

namespace TAOM.Adapters;

/// <summary>
/// The one writer of the engine's AI input callback flag (Agent.SetHasOnAiInputSetCallback). The flag is one switch per
/// agent, and the engine then calls every component on that agent (Agent.cs:1654-1666). Two features need it:
/// Marshal's Word, for the archers' draw hold, and stamina, for an exhausted soldier's input (#1497). Each claims and
/// releases under its own owner bit, and the flag is on while either holds a claim, so one feature that lets a soldier
/// go cannot switch the callback off under the other. No code calls the engine setter directly
/// (MarshalMarkCaptureAndShotTableTests and StaminaWiringTests pin that). Main thread only, from a mission tick.
/// </summary>
internal static class AiInputCallbackClaims
{
    internal const int MarshalsWord = 1;
    internal const int Stamina = 2;

    private static readonly OwnerClaimLedger<Agent> Ledger = new OwnerClaimLedger<Agent>();

    /// <summary>For a live agent that owns its slot. Switches the callback on with the first claim.</summary>
    internal static void Claim(Agent agent, int owner)
    {
        if (Ledger.Claim(agent, owner))
            agent.SetHasOnAiInputSetCallback(true);
    }

    /// <summary>For a live agent that owns its slot. Switches the callback off with the last claim.</summary>
    internal static void Release(Agent agent, int owner)
    {
        if (Ledger.Release(agent, owner))
            agent.SetHasOnAiInputSetCallback(false);
    }

    /// <summary>
    /// For an agent that left the mission or lost its slot: drops the claim and makes no engine call, because a deleted
    /// agent's handle answers for the slot's next tenant (#592).
    /// </summary>
    internal static void Forget(Agent agent, int owner) => Ledger.Release(agent, owner);

    /// <summary>At a mission's end: drops every claim of the owner and makes no engine call.</summary>
    internal static void DropOwner(int owner) => Ledger.DropOwner(owner);
}
