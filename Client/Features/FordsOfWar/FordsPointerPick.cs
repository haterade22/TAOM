using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Domain;
using TAOM.Features.FordsOfWar.Hooks;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The soldier the pointer names, on the screen and not on the ground: the player points at what the camera shows, on any slope. The arithmetic
/// is <see cref="FordsControlPick"/>. Each pick writes a <c>[FordsOfWar][Control] pick</c> line, the evidence if the pixels of the projection
/// and of the pointer ever disagree.
/// </summary>
internal static class FordsPointerPick
{
    internal static Agent? AgentAt(Mission mission, Camera camera, Vec2 pointer, IModLogger? logger)
    {
        var agents = new List<Agent>();
        var units = new List<FordsControlCandidate>();
        foreach (var agent in FordsAgentControl.Takeable(mission))
        {
            float x = 0f, y = 0f, w = 0f;
            MBWindowManager.WorldToScreen(camera, agent.GetChestGlobalPosition(), ref x, ref y, ref w);
            if (!(w > 0f))
                continue;

            agents.Add(agent);
            units.Add(new FordsControlCandidate(x, y, FordsUpgradeHooks.IsHero(agent.Character?.StringId)));
        }

        var reach = FordsControlPick.ReachOfScreenHeight * Screen.RealScreenResolutionHeight;
        var picked = FordsControlPick.Nearest(units, pointer.x, pointer.y, reach);
        logger?.LogInfo($"[FordsOfWar][Control] pick pointer=({pointer.x:0},{pointer.y:0}) reach={reach:0} seen={units.Count} picked={(picked < 0 ? "none" : $"({units[picked].X:0},{units[picked].Y:0})")}");
        return picked < 0 ? null : agents[picked];
    }
}
