using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace TAOM.Adapters;

/// <summary>
/// <see cref="IMissionMusicAdapter"/> over <see cref="SoundEvent"/> and <see cref="MBMusicManager"/> (v1.5.7). The track plays on
/// <c>event:/music</c>, the event the engine's own music channels use, as a 2D programmer sound with no scene, as those channels are made; the
/// <c>module_sounds.xml</c> categories have no music category in v1.5.7, so none of them can carry a track. The silenced slot is vanilla's
/// <c>MusicSilencedMissionView</c> pattern: <c>DeactivateCurrentMode</c>, then <c>OnSilencedMusicHandlerInit</c> with a handler that does nothing,
/// so the campaign handler is not ticked and starts no theme. The slot has no owner check, and while it is set it outranks the campaign handler
/// (<c>MBMusicManager.CheckActiveHandler</c>), so the caller releases it on every exit. Never <c>PauseMusicManagerSystem</c>: that is one
/// process-wide flag that only the main menu and the end of a video clear. Never <c>SoundEvent.Release</c> on a playing event: <c>Stop</c> sets
/// the id to -1, so the release would act on id -1 (SoundEvent.cs:112-119, 152-163). An event that ends by itself is released by native at its
/// next sound update. A <c>Stop</c> before then queues its id a second time, which the release ignores; a <c>Stop</c> after it finds the id gone
/// and does nothing. Native ids come from a counter, so an old id never names a new event.
/// </summary>
public sealed class MissionMusicAdapter : IMissionMusicAdapter
{
    private const string MusicEvent = "event:/music";

    private SoundEvent? _event;
    private SilencedHandler? _handler;

    public bool TryPlay(string fullPath)
    {
        Stop();
        var created = SoundEvent.CreateEventFromExternalFile(MusicEvent, fullPath, null, is3d: false, isBlocking: false);
        if (created == null || !created.IsValid)
            return false;

        _event = created;
        return created.Play();
    }

    public bool IsStopped => _event == null || !_event.IsValid || _event.IsStopped();

    public void Stop()
    {
        var playing = _event;
        _event = null;
        playing?.Stop();
    }

    public bool Silence()
    {
        var manager = MBMusicManager.Current;
        if (manager == null)
            return false;

        _handler = new SilencedHandler();
        manager.DeactivateCurrentMode();
        manager.OnSilencedMusicHandlerInit(_handler);
        return true;
    }

    public void Unsilence()
    {
        if (_handler == null)
            return;

        _handler = null;
        MBMusicManager.Current?.OnSilencedMusicHandlerFinalize();
    }

    // The silenced slot's tenant: vanilla ticks the active handler from MBMusicManager.Update, and this one starts nothing.
    private sealed class SilencedHandler : IMusicHandler
    {
        bool IMusicHandler.IsPausable => true;

        void IMusicHandler.OnUpdated(float dt) { }
    }
}
