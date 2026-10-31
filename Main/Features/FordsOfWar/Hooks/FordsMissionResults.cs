using System;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar.Hooks;

/// <summary>The result a finished or failed Fords of War mission hands the engine's mission-ended poll.</summary>
internal static class FordsMissionResults
{
    internal static MissionResult For(Mission mission, bool playerWon)
    {
        try
        {
            return playerWon ? MissionResult.CreateSuccessful(mission) : MissionResult.CreateDefeated(mission);
        }
        catch (Exception)
        {
            return new MissionResult(); // the factories read the player's team, which a mission that never got its teams lacks
        }
    }
}
