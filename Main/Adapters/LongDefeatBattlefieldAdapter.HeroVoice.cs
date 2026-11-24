using TAOM.Core.Validation;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// The mission heroes' recorded lines (#1569), main thread only. A line plays as vanilla's conversation lines do (CampaignMissionComponent.PlayConversationSoundEvent, v1.5.7): an external file on
/// <c>event:/Extra/voiceover</c>, 2D, in the mission's scene, positioned at the main agent and not at the hero, then <c>Play</c>. It ends with <c>Stop</c>, never <c>SoundEvent.Release</c> on a playing
/// event, as <see cref="MissionMusicAdapter"/> explains. A dedicated server makes no sound.
/// </summary>
internal sealed partial class LongDefeatBattlefieldAdapter : ILongDefeatHeroVoiceAdapter
{
    private const string VoiceEvent = "event:/Extra/voiceover";

    private SoundEvent? _heroLine;

    public bool PlayHeroLine(string fullPath)
    {
        if (GameNetwork.IsDedicatedServer)
            return false;

        StopHeroLine();
        var created = SoundEvent.CreateEventFromExternalFile(VoiceEvent, fullPath, _mission.Scene, is3d: false, isBlocking: false);
        if (created == null || !created.IsValid)
            return false;

        _heroLine = created;
        if (_mission.MainAgent is { } main && Live(main))
        {
            var at = main.Position;
            if (FiniteFloatValidator.IsFinite(at.x) && FiniteFloatValidator.IsFinite(at.y) && FiniteFloatValidator.IsFinite(at.z))
                created.SetPosition(at);
        }

        return created.Play();
    }

    public void StopHeroLine()
    {
        var playing = _heroLine;
        _heroLine = null;
        playing?.Stop();
    }
}
