namespace TAOM.Adapters;

/// <summary>
/// One mission's own music track, and the vanilla music system's silenced slot for the same mission (#1105). Holds the engine's sound event and
/// silenced handler between a play and its release, so one instance belongs to one mission and is never a container singleton.
/// </summary>
public interface IMissionMusicAdapter
{
    /// <summary>Stops and drops any event this adapter holds, then creates a new 2D event for the file on the engine's music event and starts it.
    /// True means the engine accepted the start; the file itself is opened later, in the engine's sound callback, and a file that does not decode
    /// reports nothing back. A replay goes through here too: native releases an event that ended by itself, so the old event cannot play again.</summary>
    bool TryPlay(string fullPath);

    /// <summary>True when no event exists or the engine reports it stopped.</summary>
    bool IsStopped { get; }

    /// <summary>Stops the event and drops it. Safe with no event.</summary>
    void Stop();

    /// <summary>Stops vanilla music with its 3 s fade and takes the silenced slot, as vanilla's MusicSilencedMissionView does. False with no music manager.</summary>
    bool Silence();

    /// <summary>Gives the silenced slot back, so the campaign handler restarts vanilla music. Safe when this adapter holds no slot.</summary>
    void Unsilence();
}
