using System.Reflection;
using SandBox.Objects.Usables;
using TaleWorlds.Engine;

namespace TAOM.Adapters;

public sealed class MusicianGroupSuppressionAdapter : IMusicianGroupSuppressionAdapter
{
    private static readonly FieldInfo TrackEventField =
        typeof(MusicianGroup).GetField("_trackEvent", BindingFlags.Instance | BindingFlags.NonPublic);

    public void StopAndReleaseVanillaTrack(object musicianGroup)
    {
        if (musicianGroup == null || TrackEventField == null)
            return;

        var trackEvent = TrackEventField.GetValue(musicianGroup) as SoundEvent;
        if (trackEvent == null)
            return;

        try
        {
            if (trackEvent.IsPlaying())
                trackEvent.Stop();
        }
        catch
        {
        }

        try
        {
            trackEvent.Release();
        }
        catch
        {
        }

        try
        {
            TrackEventField.SetValue(musicianGroup, null);
        }
        catch
        {
        }
    }
}
