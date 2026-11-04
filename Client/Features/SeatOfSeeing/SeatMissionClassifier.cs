using System;
using System.Reflection;
using SandBox.Missions.MissionLogics.Arena;
using SandBox.Missions.MissionLogics.Hideout;
using SandBox.Tournaments.MissionLogics;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Core.Logging;
using TAOM.Features.SeatOfSeeing.Domain;

namespace TAOM.Client.Features.SeatOfSeeing;

/// <summary>
/// Turns a live mission into the <see cref="SeatMissionFacts"/> the policy decides from (build-spec section 4). One entry,
/// <see cref="ReadFacts"/>, which <c>SeatView</c> calls the first time the Seat is asked to enter and <c>MarshalView</c> calls at each
/// decision to go live, so the foreign-camera line can appear with no Seat key pressed. The kind comes from
/// presence tests, never from a mission name: a hideout, tournament or arena controller, a campaign location, then the combat type
/// and the team AI. <c>Mission.Mode</c> reads StartUp in a town, so it proves nothing alone. The foreign-camera rule (5.6) walks the
/// mission's views once and names the type that owns the camera in one log line.
/// </summary>
internal static class SeatMissionClassifier
{
    internal static SeatMissionFacts ReadFacts(Mission mission, IModLogger logger)
    {
        var probe = new SeatMissionProbe(
            Hideout: mission.GetMissionBehavior<HideoutMissionController>() != null
                     || mission.GetMissionBehavior<HideoutAmbushMissionController>() != null,
            Tournament: mission.GetMissionBehavior<TournamentBehavior>() != null,
            Arena: mission.GetMissionBehavior<ArenaPracticeFightMissionController>() != null,
            CampaignLocation: CampaignMission.Current?.Location != null,
            Combat: mission.CombatType == Mission.MissionCombatType.Combat,
            TeamBattle: mission.IsFieldBattle || mission.IsSiegeBattle || mission.IsSallyOutBattle,
            BattleMode: mission.Mode == MissionMode.Battle || mission.Mode == MissionMode.Deployment);
        return new SeatMissionFacts(SeatMissionKindRules.Classify(probe), GameNetwork.IsSessionActive, ForeignCameraOwns(mission, logger));
    }

    /// <summary>True when a view of the mission declares its camera override outside TaleWorlds and SandBox; the first one is named in the log.</summary>
    private static bool ForeignCameraOwns(Mission mission, IModLogger logger)
    {
        foreach (var behavior in mission.MissionBehaviors)
        {
            if (behavior is not MissionView view || !OwnsTheCamera(view.GetType()))
                continue;

            logger.LogInfo($"[SeatOfSeeing] another view owns the camera, the Seat stays out: {view.GetType().FullName}");
            return true;
        }

        return false;
    }

    /// <summary>True when the view type declares <c>UpdateOverridenCamera</c> outside TaleWorlds and SandBox. The Seat's own view never does.</summary>
    internal static bool OwnsTheCamera(Type view)
    {
        var declaring = view.GetMethod(nameof(MissionView.UpdateOverridenCamera), BindingFlags.Instance | BindingFlags.Public,
            null, new[] { typeof(float) }, null)?.DeclaringType;
        return SeatForeignCameraRule.IsForeign(declaring?.Assembly.GetName().Name, view == typeof(SeatView));
    }
}
