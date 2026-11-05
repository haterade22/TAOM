using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Adapters;

/// <summary>Every write the Seat makes to the hero's <c>Agent</c>, all from the mission tick, on <c>Mission.Current.MainAgent</c> read at call time.</summary>
internal interface ISeatHeroAdapter
{
    /// <summary>
    /// Clears what the controller last wrote on the main agent (movement flags, movement input, event control flags) and the head
    /// camera mode, because nothing rewrites them while the controller gate is shut. A no-op with no active main agent. Call it from the
    /// mission tick only: vanilla writes these fields from <c>OnPreMissionTick</c>, and the screen tick may overlap the agent tick.
    /// </summary>
    void StandFast();

    /// <summary>Hands the main agent to the engine's AI (build-spec 8.1, Begin): <c>None</c> when done, else the refusal, with nothing written.</summary>
    SeatFightRefusal BeginFighting();

    /// <summary>Takes the main agent back from the AI (build-spec 8.1, End); a no-op unless he is active and on the AI.</summary>
    void EndFighting();

    /// <summary>There is an active main agent.</summary>
    bool HeroActive();

    /// <summary>The main agent is active and its controller is the AI's: the hand-off still holds.</summary>
    bool HeroOnAi();

    /// <summary>Stops a retreat that a Retreat order or a rout started on the main agent; true when it stopped one.</summary>
    bool StopHeroRetreat();
}
