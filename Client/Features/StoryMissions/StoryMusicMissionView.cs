using System;
using System.IO;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.View.MissionViews;
using TAOM.Adapters;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StoryMissions;
using TAOM.Features.StoryMissions.Hooks;

namespace TAOM.Client.Features.StoryMissions;

/// <summary>
/// Plays a story mission's own track (#1105) and keeps vanilla music quiet for that mission only. The story view list adds it only when the
/// mission's row names a track, and builds a new cue and adapter for each mission. The track starts when the scene first renders (once per
/// mission screen, on the main thread). Both teardown hooks release it: the normal end, a retreat and Exit to Main Menu reach
/// <c>OnMissionScreenFinalize</c> through <c>Mission.EndMission</c>. A teardown without <c>Mission.EndMission</c> (shutdown, or a mod that
/// loads a save mid-mission; vanilla's mission escape menu has no Load) reaches only <c>OnRemoveBehavior</c>, the path vanilla's
/// <c>MusicSilencedMissionView</c> misses. The decisions are in <see cref="StoryMusicCue"/>, which never throws.
/// </summary>
public sealed class StoryMusicMissionView : MissionView
{
    private readonly StoryMusicCue _cue;

    private StoryMusicMissionView(StoryMusicCue cue) => _cue = cue;

    /// <summary>The view for a story mission that names a track, else null. A fault here leaves the mission with the music it has today.</summary>
    internal static StoryMusicMissionView? ForMission(Mission mission)
    {
        var logic = mission.GetMissionBehavior<StoryMissionLogic>();
        if (logic?.Music == null)
            return null;

        try
        {
            var path = IoC.Resolve<IPathService>().ModuleRootPath + "ModuleSounds/" + logic.Music;
            return new StoryMusicMissionView(new StoryMusicCue(new MissionMusicAdapter(), path, File.Exists, IoC.Resolve<IModLogger>(),
                IoC.Resolve<IOutcomeLog>(), logic.MissionKey));
        }
        catch (Exception ex)
        {
            Outcomes.Swallowed("StoryMissions", "music-view", ex);
            return null;
        }
    }

    public override void OnSceneRenderingStarted()
    {
        base.OnSceneRenderingStarted();
        _cue.Start();
    }

    public override void OnMissionScreenTick(float dt)
    {
        base.OnMissionScreenTick(dt);
        _cue.Tick(dt);
    }

    public override void OnMissionScreenFinalize()
    {
        _cue.Release("screen-finalize");
        base.OnMissionScreenFinalize();
    }

    public override void OnRemoveBehavior()
    {
        _cue.Release("remove-behavior");
        base.OnRemoveBehavior();
    }
}
