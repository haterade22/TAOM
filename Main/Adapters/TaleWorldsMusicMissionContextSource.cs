using System;
using TaleWorlds.MountAndBlade;
using TAOM.Features.Music;

namespace TAOM.Adapters;

public sealed class TaleWorldsMusicMissionContextSource : IMusicMissionContextSource
{
    private readonly IMusicTavernContextSource _tavernContextSource;

    public TaleWorldsMusicMissionContextSource(IMusicTavernContextSource tavernContextSource)
    {
        _tavernContextSource = tavernContextSource;
    }

    public MusicMissionContextState Capture(string fallbackCultureId)
    {
        try
        {
            var mission = Mission.Current;
            if (mission == null || mission.IsFinalized)
                return MusicMissionContextState.Inactive;

            var sceneId = SafeRead(() => mission.SceneName);
            var siege = SafeRead(() => mission.IsSiegeBattle);
            var battle = SafeRead(() => mission.IsFieldBattle || mission.IsSallyOutBattle || mission.IsNavalBattle);
            var tavern = !siege
                && !battle
                && SafeRead(() => _tavernContextSource != null && _tavernContextSource.IsInTavernContext);

            return new MusicMissionContextState(
                true,
                siege,
                battle,
                false,
                tavern,
                null,
                sceneId,
                BuildReason(siege, battle, tavern, sceneId));
        }
        catch
        {
            return MusicMissionContextState.Inactive;
        }
    }

    private static string BuildReason(bool siege, bool battle, bool tavern, string sceneId)
    {
        var prefix = siege ? "mission_siege" : battle ? "mission_battle" : tavern ? "mission_tavern" : "mission_unclassified";
        return string.IsNullOrWhiteSpace(sceneId) ? prefix : prefix + ":" + sceneId.Trim();
    }

    private static T SafeRead<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return default;
        }
    }
}
