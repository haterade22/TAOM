using System;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.OrcBattleVoice.Hooks;

/// <summary>
/// Thin boundary for the orc battle voice: it hands each freshly built agent and the mission end to
/// <see cref="OrcBattleVoiceService"/> and never throws. After the first exception it stops for the mission
/// with one warning; the battle itself goes on.
/// </summary>
public sealed class OrcBattleVoiceMissionBehavior : MissionLogic
{
    private readonly OrcBattleVoiceService _service;
    private readonly IModLogger _logger;
    private readonly Action<string> _warn;
    private bool _failed;

    public OrcBattleVoiceMissionBehavior(OrcBattleVoiceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
        _warn = message => _logger.LogWarning(message);
    }

    // v1.5.7 Mission.SpawnAgent runs `missionBehavior3.OnAgentBuild(agent, ...)` over every behavior after
    // BuildAgent(agent, agentBuildData) (Mission.cs:4388-4399). BuildAgent has by then run
    // EquipItemsFromSpawnEquipment, whose AddSkinMeshes builds the skin from the race and body properties
    // (Agent.cs:4594, 5453-5458), and InitializeAgentRecord (Mission.cs:4049-4050), so the voice set here
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

    protected override void OnEndMission() => _service.End();

    public override void OnRemoveBehavior()
    {
        _service.End();
        base.OnRemoveBehavior();
    }

    private void Fail(string site, Exception ex)
    {
        if (_failed) return;
        _failed = true;
        try { _logger.LogWarning($"[OrcBattleVoice] {site} failed, orc voice off for this mission: {ex.GetType().Name} 0x{ex.HResult:X8}"); }
        catch { /* swallow-ok: diagnostic only, the logger itself failed */ }
    }
}
