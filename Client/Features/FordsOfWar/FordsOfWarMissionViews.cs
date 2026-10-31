using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.MountAndBlade.View;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Features.FordsOfWar;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The view list for the "TaomFordsOfWar" mission (build-spec 6), cut from the lord's hall and host review lists to the views that
/// work with no main agent. Kept: the singleplayer escape menu (its exit goes through <c>MBGameManager.EndGame</c>, which is how
/// the match returns to the main menu), the boundary wall, and the options handler. Added: the commander camera and the command
/// panel. Left out, each because it reads <c>Agent.Main</c> or the campaign: the main agent equipment and cheer controllers, the
/// order bar (it refuses to open with no main agent, MissionOrderVM.cs:740-749), the order troop placer, the agent status and lock
/// views, the deployment views, the boundary crossing view, the battle scoreboard and the campaign view. The screen fade is a global
/// layer, not a mission view, so it needs no entry.
///
/// The options handler dereferences <c>Mission.GetMissionBehavior&lt;MissionOptionsComponent&gt;()</c> unguarded in
/// <c>OnMissionScreenInitialize</c>, so it is listed only when the mission carries that component; the launcher adds one, so the
/// escape menu's Options entry works (the guard keeps a mission without it from throwing). The engine finds this class by its attribute
/// in every loaded module assembly. Every static method on a <c>[ViewCreatorModule]</c> class must carry <c>[ViewMethod]</c>
/// (ViewCreatorManager.cs:63-66). The views in the list are not <c>[DefaultView]</c>: the engine builds them from this list when
/// the screen registers its views.
/// </summary>
[ViewCreatorModule]
public static class FordsOfWarMissionViews
{
    [ViewMethod(FordsOfWarMission.MissionName)]
    public static MissionView[] OpenFordsOfWarViews(Mission mission)
    {
        var views = new List<MissionView>
        {
            ViewCreator.CreateMissionSingleplayerEscapeMenu(false),
            new MissionBoundaryWallView(),
            new FordsCommanderCameraView(),
            new FordsCommandView(),
        };
        if (mission.GetMissionBehavior<MissionOptionsComponent>() != null)
            views.Add(ViewCreator.CreateOptionsUIHandler());
        return views.ToArray();
    }
}
