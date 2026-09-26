using System.Collections.Generic;
using TAOM.Core.Collections;
using TAOM.Core.Logging;
using TAOM.Features.AdvancedCombat;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.TrollBruteForce;

/// <summary>
/// Feeds <see cref="TrollClipLog"/>: twice a second it lists the active trolls, and every tick it reads each one's
/// channel 0 and 1 actions, resolving an action's bound clip (<c>MBActionSet.GetActionAnimationName</c>) only when
/// the pair changed. A held agent is read only while <see cref="AgentSlotIdentity.IsCurrentOccupant"/> says the slot
/// is still its own (#592). So the cost per troll per tick is that slot check, one interop call
/// (<c>Mission.FindAgentWithIndex</c>), plus the liveness and two channel reads.
/// Main thread only (OnMissionTick). Boundary code (raw Agent); game-tested per ADR-008.
/// Temporary: it serves the hill troll clip tuning pass. Once that pass ends, delete this file,
/// <see cref="TrollClipLog"/>, TrollClipLogTests.cs and the four wiring lines in
/// <see cref="TrollBruteForceMissionBehavior"/>.
/// </summary>
public sealed class TrollClipTrace
{
    private const float RefreshSeconds = 0.5f;

    private readonly ITrollBruteForceService _service;
    private readonly IModLogger _logger;
    private readonly TrollClipLog _log = new();
    private readonly List<Agent> _trolls = new();
    private readonly Dictionary<Agent, (ActionIndexCache C0, ActionIndexCache C1)> _last = new(ReferenceIdentity.Instance);
    private float _nextRefresh;

    public TrollClipTrace(ITrollBruteForceService service, IModLogger logger)
    {
        _service = service;
        _logger = logger;
    }

    public void Tick(Mission mission)
    {
        if (mission.CurrentTime >= _nextRefresh)
        {
            _nextRefresh = mission.CurrentTime + RefreshSeconds;
            _trolls.Clear();
            foreach (Agent agent in mission.Agents)
                if (_service.IsBruteForceTroll(agent.Monster?.StringId)) _trolls.Add(agent);
        }

        foreach (Agent troll in _trolls)
        {
            if (!troll.IsActive() || !AgentSlotIdentity.IsCurrentOccupant(troll)) continue;
            ActionIndexCache c0 = troll.GetCurrentAction(0);
            ActionIndexCache c1 = troll.GetCurrentAction(1);
            if (_last.TryGetValue(troll, out var last) && last.C0 == c0 && last.C1 == c1) continue;
            _last[troll] = (c0, c1);
            Note(troll, c0);
            Note(troll, c1);
        }
    }

    public void Clear()
    {
        _trolls.Clear();
        _last.Clear();
        _log.Clear();
        _nextRefresh = 0f;
    }

    private void Note(Agent troll, in ActionIndexCache action)
    {
        if (action == ActionIndexCache.act_none) return;
        string? line = _log.FirstPlay(troll.Monster?.StringId, action.GetName(),
            MBActionSet.GetActionAnimationName(troll.ActionSet, action));
        if (line != null) _logger.LogInfo(line);
    }
}
