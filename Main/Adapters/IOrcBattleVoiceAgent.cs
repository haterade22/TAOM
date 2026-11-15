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

    /// <summary>False when the agent has no <c>MBAgentVisuals</c> to carry a voice (a headless server).</summary>
    bool HasVisuals { get; }

    /// <summary>The troop's race index (<c>BasicCharacterObject.Race</c>), or -1 without a character.</summary>
    int Race { get; }

    /// <summary><c>Agent.BodyPropertiesSeed</c>: stable per soldier, unlike <c>Agent.Index</c>.</summary>
    int BodyPropertiesSeed { get; }

    /// <summary>The name of the voice definition the engine reports for the agent (<c>Agent.GetAgentVoiceDefinition</c>).</summary>
    string VoiceDefinitionName { get; }

    /// <summary>Sets the agent's voice definition by its native index (<c>MBAgentVisuals.SetVoiceDefinitionIndex</c>).</summary>
    void SetVoiceDefinition(int index, float pitch);
}
