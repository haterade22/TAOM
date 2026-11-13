using System.Collections.Generic;
using SandBox.View.Missions;
using TaleWorlds.CampaignSystem;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TaleWorlds.MountAndBlade.View.MissionViews.Order;
using TaleWorlds.MountAndBlade.View.MissionViews.Singleplayer;
using TAOM.Features.StoryMissions;

namespace TAOM.Client.Features.StoryMissions;

/// <summary>
/// The view list for the "TaomStoryMission" mission (#1105). It is vanilla's siege list with no deployment
/// (<c>SandBoxMissionViews.OpenSiegeMissionNoDeployment</c>, v1.5.7) with some views left out, plus <c>MissionFaceCacheView</c>, which campaign lists keep.
/// Left out, and why:
/// - <c>MissionPreloadView</c>: it reads <c>MapEvent.PlayerMapEvent</c> with no null check, and this mission has no map event.
/// - <c>MusicBattleMissionView</c>: with vanilla's siege arguments (true, false) it reads <c>DefaultBattleMissionAgentSpawnLogic</c>, which this mission does
///   not add. A mission whose row names a track gets <see cref="StoryMusicMissionView"/> instead, which silences vanilla music for that mission
///   only; a mission with no track keeps the campaign's music, as the campaign handler stays active under a mission with no music view.
/// - The scoreboard, the kill feed, the equipment drop view, the conversation camera, <c>MissionSingleplayerViewHandler</c> and the battle spectator
///   view: the Long Defeat list leaves them out too. Nobody has checked yet whether this mission needs them.
/// - The Long Defeat HUD: it reads that mode's session, and it offers a shop and a button that leaves to the main menu. Its two refusals are not
///   needed here. Photo Mode stays allowed, because this list has the Photo Mode view, which holds the only exit from Photo Mode. The focus-loss
///   Escape refusal is for the Long Defeat's environment-variable autorun, which a story mission does not use.
/// A list with the singleplayer escape menu must also have the Photo Mode view or a view that refuses Photo Mode (PhotoModeEscapeMenuGateTests).
/// </summary>
[ViewCreatorModule]
public static class StoryMissionViews
{
    [ViewMethod(StoryMission.MissionName)]
    public static MissionView[] OpenStoryViews(Mission mission)
    {
        var views = new List<MissionView>
        {
            new MissionCampaignView(),
            ViewCreator.CreateMissionSingleplayerEscapeMenu(CampaignOptions.IsIronmanMode),
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
            ViewCreator.CreatePhotoModeView(),
            new MissionItemContourControllerView(),
            new MissionAgentContourControllerView(),
            new MissionFaceCacheView(),
        };
        var music = StoryMusicMissionView.ForMission(mission);
        if (music != null)
            views.Add(music);

        return views.ToArray();
    }
}
