using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>Short-lived <see cref="Agent"/> view for the orc battle voice feature (<see cref="IOrcBattleVoiceAgent"/>).</summary>
public sealed class OrcBattleVoiceAgent : IOrcBattleVoiceAgent
{
    private readonly Agent _agent;

    public OrcBattleVoiceAgent(Agent agent) => _agent = agent;

    public bool IsHuman => _agent.IsHuman;

    public bool IsFemale => _agent.IsFemale;

    public bool IsPlayerControlled => _agent.IsMainAgent || _agent.IsPlayerControlled;

    // The native voice getter names a definition only while the visuals are valid (MBAgentVisuals.IsValid, v1.5.7).
    public bool HasVisuals => _agent.AgentVisuals?.IsValid() == true;

    // v1.5.7 Agent.cs:1527-1530: `return BodyPropertiesValue.Age;`
    public float Age => _agent.Age;

    public int Race => _agent.Character?.Race ?? -1;

    public int BodyPropertiesSeed => _agent.BodyPropertiesSeed;

    // v1.5.7 Agent.cs:2932-2934: `return MBAPI.IMBAgent.GetAgentVoiceDefinition(GetPtr());`, a native read.
    public string VoiceDefinitionName => _agent.GetAgentVoiceDefinition() ?? "";

    // v1.5.7 MBAgentVisuals.cs:316-318 passes straight to native `set_voice_definition_index`. Vanilla's only
    // caller is MountVisualCreator.SetVoiceDefinition (TaleWorlds.MountAndBlade.View), which gives every mount
    // its voice this way: `mBAgentVisuals.SetVoiceDefinitionIndex(voiceDefinitionIndex, voicePitch);`.
    public void SetVoiceDefinition(int index, float pitch) =>
        _agent.AgentVisuals.SetVoiceDefinitionIndex(index, pitch);
}
