using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TAOM.Adapters;

namespace TAOM.Features.Music.Hooks;

public sealed class MusicCampaignBehavior : CampaignBehaviorBase
{
    private readonly IMusicCampaignContextAdapter _campaignContext;
    private readonly IMusicPlaybackService _playback;

    private float _timerSeconds;
    private bool _missionActive;

    public MusicCampaignBehavior(
        IMusicCampaignContextAdapter campaignContext,
        IMusicPlaybackService playback)
    {
        _campaignContext = campaignContext;
        _playback = playback;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
        CampaignEvents.OnMissionStartedEvent.AddNonSerializedListener(this, OnMissionStarted);
        CampaignEvents.OnMissionEndedEvent.AddNonSerializedListener(this, OnMissionEnded);
        CampaignEvents.TickEvent.AddNonSerializedListener(this, OnCampaignTickEvent);
    }

    public override void SyncData(IDataStore dataStore)
    {
    }

    internal MusicPlaybackResult OnCampaignTick(float dt)
    {
        if (_missionActive)
        {
            var inactiveResult = MusicPlaybackResult.None("mission_active");
            MusicRuntimeSmokeTrace.PlaybackResult("campaign", inactiveResult);
            return inactiveResult;
        }

        _timerSeconds += SanitizeDelta(dt);
        var campaignSnapshot = _campaignContext.CaptureSnapshot();
        var result = _playback.Update(MusicRouteSnapshot.Empty, campaignSnapshot, _timerSeconds);
        MusicRuntimeSmokeTrace.PlaybackResult("campaign", result);
        return result;
    }

    internal void OnMissionStarted(IMission mission)
    {
        _missionActive = true;
        MusicRuntimeSmokeTrace.PlaybackResult("campaign", _playback.Stop("mission_started"));
    }

    internal void OnMissionEnded(IMission mission)
    {
        _missionActive = false;
    }

    internal void OnSessionLaunched(CampaignGameStarter starter)
    {
        _timerSeconds = 0f;
        _missionActive = false;
        // Clear any stale back-off state from a previous session loaded in the same process.
        _playback.Stop("session_launched");
        // Stop the vanilla psai theme so TAOM can claim a channel on the first tick.
        // The Patch46_Music StartTheme prefix will prevent psai from restarting after this.
        VanillaMusicManagerSuppressionHelper.StopVanillaPsaiMusic();
    }

    private void OnCampaignTickEvent(float dt)
    {
        OnCampaignTick(dt);
    }

    private static float SanitizeDelta(float dt)
    {
        if (float.IsNaN(dt) || float.IsInfinity(dt) || dt < 0f)
            return 0f;

        return dt;
    }
}
