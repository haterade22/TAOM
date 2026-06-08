using TAOM.Features.Music;

namespace TAOM.Adapters;

public sealed class MusicMissionContextAdapter : IMusicMissionContextAdapter
{
    private readonly IMusicMissionContextSource _source;
    private readonly ICustomBattleMusicContextService _customBattleContext;

    internal MusicMissionContextAdapter(IMusicMissionContextSource source)
        : this(source, null)
    {
    }

    public MusicMissionContextAdapter(
        IMusicMissionContextSource source,
        ICustomBattleMusicContextService customBattleContext)
    {
        _source = source;
        _customBattleContext = customBattleContext;
    }

    public MusicRouteSnapshot CaptureSnapshot(string fallbackCultureId = null)
    {
        var state = _source.Capture(fallbackCultureId);
        if (state == null || !state.IsActive)
            return MusicRouteSnapshot.Empty;

        if (!state.Siege && !state.Battle && !state.Tavern && !state.Town)
            return MusicRouteSnapshot.Empty;

        var cultureId = FirstNonEmpty(
            state.CultureId,
            CustomBattleCultureForFallback(fallbackCultureId),
            fallbackCultureId,
            MusicTrackIndex.NeutralCulture);
        var reason = FirstNonEmpty(state.Reason, BuildReason(state.SceneId), "mission");

        return new MusicRouteSnapshot(
            true,
            cultureId,
            state.Siege,
            state.Battle,
            state.Tavern,
            state.Town,
            false,
            reason);
    }

    private static string BuildReason(string sceneId)
    {
        return string.IsNullOrWhiteSpace(sceneId) ? "mission" : "mission:" + sceneId.Trim();
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return MusicTrackIndex.NeutralCulture;
    }

    private string CustomBattleCultureForFallback(string fallbackCultureId)
    {
        if (!string.IsNullOrWhiteSpace(fallbackCultureId)
            && !string.Equals(fallbackCultureId.Trim(), MusicTrackIndex.NeutralCulture, System.StringComparison.OrdinalIgnoreCase))
            return null;

        return _customBattleContext?.SelectedPlayerCultureId;
    }
}
