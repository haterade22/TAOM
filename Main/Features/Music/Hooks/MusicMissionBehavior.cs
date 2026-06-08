using TaleWorlds.MountAndBlade;
using TAOM.Adapters;

namespace TAOM.Features.Music.Hooks;

public sealed class MusicMissionBehavior : MissionBehavior
{
    private readonly IMusicMissionContextAdapter _missionContext;
    private readonly IMusicCampaignContextAdapter _campaignContext;
    private readonly IMusicPlaybackService _playback;
    private readonly ICustomBattleMusicContextService _customBattleContext;
    private readonly IMusicTavernContextSource _tavernContextSource;

    public override MissionBehaviorType BehaviorType => MissionBehaviorType.Other;

    public MusicMissionBehavior()
        : this(
            IoC.Resolve<IMusicMissionContextAdapter>(),
            IoC.Resolve<IMusicCampaignContextAdapter>(),
            IoC.Resolve<IMusicPlaybackService>(),
            IoC.Resolve<ICustomBattleMusicContextService>(),
            IoC.Resolve<IMusicTavernContextSource>())
    {
    }

    internal MusicMissionBehavior(
        IMusicMissionContextAdapter missionContext,
        IMusicCampaignContextAdapter campaignContext,
        IMusicPlaybackService playback,
        ICustomBattleMusicContextService customBattleContext = null,
        IMusicTavernContextSource tavernContextSource = null)
    {
        _missionContext = missionContext;
        _campaignContext = campaignContext;
        _playback = playback;
        _customBattleContext = customBattleContext;
        _tavernContextSource = tavernContextSource;
    }

    public override void OnMissionTick(float dt)
    {
        base.OnMissionTick(dt);
        OnMissionRuntimeTick(Mission?.CurrentTime ?? 0f);
    }

    internal MusicPlaybackResult OnMissionRuntimeTick(float timerSeconds)
    {
        var campaignSnapshot = _campaignContext.CaptureSnapshot();
        var missionSnapshot = _missionContext.CaptureSnapshot(campaignSnapshot.CultureId);
        var result = _playback.Update(missionSnapshot, campaignSnapshot, timerSeconds);
        MusicRuntimeSmokeTrace.PlaybackResult("mission", result);
        return result;
    }

    protected override void OnEndMission()
    {
        base.OnEndMission();
        ClearMissionContexts();
        MusicRuntimeSmokeTrace.PlaybackResult("mission", _playback.Stop("mission_ended"));
    }

    public override void OnRemoveBehavior()
    {
        base.OnRemoveBehavior();
        ClearMissionContexts();
        MusicRuntimeSmokeTrace.PlaybackResult("mission", _playback.Stop("mission_behavior_removed"));
    }

    private void ClearMissionContexts()
    {
        try
        {
            _tavernContextSource?.ExitTavernContext();
        }
        catch
        {
        }

        try
        {
            _customBattleContext?.ClearSelectedCulture();
        }
        catch
        {
        }
    }
}
