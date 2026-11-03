using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;
using TAOM.Features.LongDefeat;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The view list for the "TaomLongDefeat" mission (#1079): the player-agent list of <c>HostReviewMissionViews</c>, without its campaign
/// view and its deployment views (this mission has no deployment phase), plus the siege engine markers. Every static method on a
/// <c>[ViewCreatorModule]</c> class must carry <c>[ViewMethod]</c> (ViewCreatorManager.cs:63-66).
/// </summary>
[ViewCreatorModule]
public static class LongDefeatMissionViews
{
    [ViewMethod(LongDefeatMission.MissionName)]
    public static MissionView[] OpenLongDefeatViews(Mission mission) =>
        new MissionView[]
        {
            ViewCreator.CreateMissionSingleplayerEscapeMenu(false),
            ViewCreator.CreateMissionAgentLabelUIHandler(mission),
            ViewCreator.CreateOptionsUIHandler(),
            ViewCreator.CreateMissionOrderUIHandler(),
            new OrderTroopPlacer(null),
            ViewCreator.CreateMissionAgentStatusUIHandler(mission),
            ViewCreator.CreateMissionMainAgentEquipmentController(mission),
            ViewCreator.CreateMissionMainAgentCheerBarkControllerView(mission),
            ViewCreator.CreateMissionAgentLockVisualizerView(mission),
            ViewCreator.CreateMissionBoundaryCrossingView(),
            new MissionBoundaryWallView(),
            ViewCreator.CreateMissionFormationMarkerUIHandler(mission),
            new MissionFormationTargetSelectionHandler(),
            ViewCreator.CreateMissionSpectatorControlView(mission),
            ViewCreator.CreateMissionSiegeEngineMarkerView(mission),
            new MissionItemContourControllerView(),
            new MissionAgentContourControllerView(),
            new LongDefeatUnattendedView(),
        };
}
