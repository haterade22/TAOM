using TAOM.Features.AdvancedCombat;
using TAOM.Features.Stamina.Domain;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.Stamina.Hooks;

/// <summary>
/// What code outside the feature asks of stamina (#1497), one call per seam so each caller stays a one-line delegate
/// (gamemodels.md rule 4). Two stat models call <see cref="ApplyStats"/>: the campaign
/// <c>TaomAgentStatCalculateModel</c> and the Custom Battle <c>TaomCustomBattleAgentStatCalculateModel</c>. The two
/// player controllers call <see cref="StripPlayerInput"/> at the end of their input build: vanilla's
/// <c>MissionMainAgentController</c> through Patch171, and TAOM's own <c>AutonomousMovementPlayerController</c>. Until
/// StaminaModule sets <see cref="Runtime"/>, and in a mission where stamina is not armed, every call does nothing.
/// Each call looks the soldier up by reference before it reads anything from the engine.
/// </summary>
public static class StaminaHooks
{
    internal static StaminaRuntime? Runtime { get; set; }

    /// <summary>Any thread, wherever Agent.UpdateAgentProperties runs. A mount has no cell, so it is never changed.</summary>
    public static void ApplyStats(Agent agent, AgentDrivenProperties properties)
    {
        var runtime = Runtime;
        if (runtime == null || !runtime.Armed || agent == null)
            return;
        if (!runtime.Store.TryGet(agent, out var cell))
            return;
        var band = cell.Band;
        if (band == StaminaBand.Fresh)
            return;
        StaminaStatApplier.Apply(properties, runtime.Tuning.EffectsOf(band), agent.HasMount);
    }

    /// <summary>
    /// Main thread, from the pre-mission tick, right after a controller wrote the player's input for this frame and
    /// before the engine reads it. Strips only a soldier the mission tick put on the player lock path: Exhausted,
    /// controlled by a player, owned by this peer and not using a game object.
    /// </summary>
    public static void StripPlayerInput(Agent? agent)
    {
        var runtime = Runtime;
        if (runtime == null || !runtime.Armed || agent == null)
            return;
        if (!runtime.Store.TryGet(agent, out var cell) || cell.AppliedPath != StaminaLockPath.PlayerInput)
            return;
        var movement = (uint)agent.MovementFlags;
        var events = (uint)agent.EventControlFlags;
        var strippedMovement = StaminaInputRules.StripMovement(movement);
        var strippedEvents = StaminaInputRules.StripEvents(events, agent.HasMount, agent.IsInWater());
        if (strippedMovement == movement && strippedEvents == events)
            return;
        MissionThreadGuard.NoteCall("StaminaHooks.StripPlayerInput", runtime.Warn);
        if (strippedMovement != movement)
            agent.MovementFlags = (Agent.MovementControlFlag)strippedMovement;
        if (strippedEvents != events)
            agent.EventControlFlags = (Agent.EventControlFlag)strippedEvents;
        runtime.Telemetry.CountPlayerStrip();
    }
}
