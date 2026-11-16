using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.OrcBattleVoice.Hooks;

/// <summary>
/// Thin boundary for the orc battle voice: it hands each freshly built or re-equipped agent and the mission end to
/// <see cref="OrcBattleVoiceService"/> and never throws. After the first exception it stops for the mission
/// with one warning; the battle itself goes on.
/// </summary>
public sealed class OrcBattleVoiceMissionBehavior : MissionLogic, IMissionListener
{
    private readonly OrcBattleVoiceService _service;
    private readonly IModLogger _logger;
    private readonly Action<string> _warn;
    private bool _failed;
    private bool _listening;

    public OrcBattleVoiceMissionBehavior(OrcBattleVoiceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _warn = message => _logger.LogWarning(message);
    }

    // v1.5.7 Mission.SpawnAgent runs `missionBehavior3.OnAgentBuild(agent, ...)` over every behavior after
    // BuildAgent(agent, agentBuildData) (Mission.cs:4389-4392). BuildAgent has by then run
    // EquipItemsFromSpawnEquipment, whose AddSkinMeshes builds the skin from the race and body properties
    // (Agent.cs:4594, 5453-5458), and InitializeAgentRecord (Mission.cs:4054), so the voice set here
    // comes after the engine's own pick. Vanilla sets a mount's voice at the same stage, from the
    // OnEquipItemsFromSpawnEquipment listener (Agent.cs:4599 -> MissionScreen -> MountVisualCreator).
    public override void OnAgentBuild(Agent agent, Banner banner)
    {
        if (_failed || agent == null) return;
        try
        {
            if (MissionThreadGuard.NoteCall("OrcBattleVoiceMissionBehavior.OnAgentBuild", _warn)) return;
            _service.OnAgentBuild(new OrcBattleVoiceAgent(agent));
        }
        catch (Exception ex)
        {
            Fail("OnAgentBuild", ex);
        }
    }

    // Every in-place skin rebuild ends in Agent.EquipItemsFromSpawnEquipment, whose AddSkinMeshes re-picks the skin
    // voice natively (Agent.cs:4594) and then calls Mission.OnEquipItemsFromSpawnEquipment (Agent.cs:4599), which
    // reaches every IMissionListener (Mission.cs:3267-3272). That covers the banner bearer update, the hideout
    // stealth equipment, the inventory screen for main-party heroes, the body generator and the Coop inventory
    // and barber handlers, none of which raise OnAgentBuild again, so the native re-pick undoes the orc voice.
    // The listener fires before InitializeAgentRecord on a spawn, which is why OnAgentBuild stays too.
    // AfterStart, not OnBehaviorInitialize, which never fires for a behavior added from
    // OnMissionBehaviorInitialize (#606).
    public override void AfterStart()
    {
        base.AfterStart();
        if (_failed) return;
        try
        {
            if (Mission != null && !_listening)
            {
                Mission.AddListener(this);
                _listening = true;
            }
        }
        catch (Exception ex)
        {
            Fail("AfterStart", ex);
        }
    }

    public void OnEquipItemsFromSpawnEquipment(Agent agent, Agent.CreationType creationType)
    {
        if (_failed || agent == null) return;
        try
        {
            if (MissionThreadGuard.NoteCall("OrcBattleVoiceMissionBehavior.OnEquipItemsFromSpawnEquipment", _warn)) return;
            _service.OnAgentBuild(new OrcBattleVoiceAgent(agent));
        }
        catch (Exception ex)
        {
            Fail("OnEquipItemsFromSpawnEquipment", ex);
        }
    }

    public void OnEquipItemsFromSpawnEquipmentBegin(Agent agent, Agent.CreationType creationType) { }

    // MissionLogic.OnEndMission is protected and does the stop; the listener's public one has nothing to add.
    void IMissionListener.OnEndMission() { }

    public void OnMissionModeChange(MissionMode oldMissionMode, bool atStart) { }

    public void OnConversationCharacterChanged() { }

    public void OnResetMission() { }

    public void OnDeploymentPlanMade(Team team, bool isFirstPlan) { }

    protected override void OnEndMission() => Stop();

    public override void OnRemoveBehavior()
    {
        Stop();
        base.OnRemoveBehavior();
    }

    private void Stop()
    {
        _service.End();
        if (!_listening) return;
        _listening = false;
        try { Mission?.RemoveListener(this); }
        catch { /* swallow-ok: the mission is ending, a failed removal has no effect */ }
    }

    private void Fail(string site, Exception ex)
    {
        if (_failed) return;
        _failed = true;
        try { _logger.LogWarning($"[OrcBattleVoice] {site} failed, orc voice off for this mission: {ex.GetType().Name} 0x{ex.HResult:X8}"); }
        catch { /* swallow-ok: diagnostic only, the logger itself failed */ }
    }
}
