namespace TAOM.Adapters;

/// <summary>
/// Every write the Seat makes to the hero's <c>Agent</c> itself (build-spec 5.5). The agent is always <c>Mission.Current.MainAgent</c>,
/// read at call time and never held, because the main agent changes.
/// </summary>
internal interface ISeatHeroAdapter
{
    /// <summary>
    /// Clears what the controller last wrote on the main agent (movement flags, movement input, event control flags) and the head
    /// camera mode, because nothing rewrites them while the controller gate is shut. A no-op with no active main agent. Call it from the
    /// mission tick only: vanilla writes these fields from <c>OnPreMissionTick</c>, and the screen tick may overlap the agent tick.
    /// </summary>
    void StandFast();
}
