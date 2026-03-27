using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.AdvancedCombat.Services;

public class SpatialGridDebugService : ISpatialGridDebugService
{
    public void RenderDebugVisualization()
    {
        if (Agent.Main == null || !Input.IsKeyDown(InputKey.LeftAlt))
            return;

        List<Agent> nearbyAllAgents = SpatialGrid.Instance.GetNearAliveAgentsInRange(20f, Agent.Main);
        foreach (Agent agent in nearbyAllAgents)
        {
            if (!agent.IsActive())
                continue;
            MBDebug.RenderDebugSphere(agent.Position, 0.1f, Colors.Blue.ToUnsignedInteger());
        }
    }
}
