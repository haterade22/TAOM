using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Services;

public class SpatialGridDebugService : ISpatialGridDebugService
{
    // Reused by every overlay frame; an IoC singleton, touched only from the mission tick. Emptied after each
    // frame, so the process-lifetime singleton never keeps a finished mission's agents reachable.
    private readonly List<Agent> _nearby = new();

    public void RenderDebugVisualization()
    {
        if (Agent.Main == null || !Input.IsKeyDown(InputKey.LeftAlt))
            return;

        SpatialGrid.Instance.GetNearAliveAgentsInRange(20f, Agent.Main, _nearby);
        foreach (Agent agent in _nearby)
        {
            if (!agent.IsActive())
                continue;
            MBDebug.RenderDebugSphere(agent.Position, 0.1f, Colors.Blue.ToUnsignedInteger());
        }
        _nearby.Clear();
    }
}
