namespace TAOM.Adapters;

/// <summary>
/// One freshly built agent as the orc battle voice feature reads and changes it (ADR-007). Short-lived: the
/// mission behavior builds one per <c>OnAgentBuild</c> call and drops it.
/// </summary>
public interface IOrcBattleVoiceAgent
{
    bool IsHuman { get; }

    bool IsFemale { get; }

    /// <summary>The main agent, or an agent a player controls.</summary>
    bool IsPlayerControlled { get; }

    /// <summary>False when the agent has no <c>MBAgentVisuals</c> to carry a voice. An agent without visuals is skipped.</summary>
    bool HasVisuals { get; }

    /// <summary>The agent's age in years (<c>Agent.Age</c>, from its body properties): a child is below adult age.</summary>
    float Age { get; }

    /// <summary>The troop's race index (<c>BasicCharacterObject.Race</c>), or -1 without a character.</summary>
    int Race { get; }

    /// <summary><c>Agent.BodyPropertiesSeed</c>: the soldier's pitch seed. Hero agents and Long Defeat / Fords of War soldiers carry seed 0, so they share one pitch.</summary>
    int BodyPropertiesSeed { get; }

    /// <summary>The name of the voice definition the engine reports for the agent (<c>Agent.GetAgentVoiceDefinition</c>).</summary>
    string VoiceDefinitionName { get; }

    /// <summary>Sets the agent's voice definition by its native index (<c>MBAgentVisuals.SetVoiceDefinitionIndex</c>).</summary>
    void SetVoiceDefinition(int index, float pitch);
}
