namespace TAOM.Features.CoopInterop;

/// <summary>
/// "Is a co-op session live right now, and do I own the simulation?"
///
/// DELIBERATELY SEPARATE FROM <see cref="ICoopPresenceProvider"/>. That one answers a
/// PROCESS-CONSTANT question — "is a co-op module enabled in this launcher session" — which is
/// decided before the game starts and is the only co-op fact safe to read near a Harmony
/// patch-application site. This one answers a SESSION-VARYING question: a player can enable the
/// Coop module and then play solo all evening, host, disconnect, and load a singleplayer save, all
/// in one process. <c>CoopPresence</c>'s own class docs draw exactly this line and say
/// session-varying facts belong in a separate service.
///
/// Read these LIVE at the point of use; never cache the result in a field. Coop's patches persist
/// for the whole process (<c>GameInterface.UnpatchAll()</c> has an empty body) and its host flag is
/// sticky, so a value cached at load time is wrong for the rest of the session.
/// </summary>
public interface ICoopSessionProvider
{
    /// <summary>A co-op session container is currently live (client or host).</summary>
    bool IsSessionActive { get; }

    /// <summary>
    /// This peer may run authoritative campaign logic. True in singleplayer, true on a co-op host,
    /// false on a co-op client. Gate every world-mutating global-tick handler on this.
    /// </summary>
    bool IsAuthority { get; }

    /// <summary>A co-op session is live AND this peer is a client. Never true in singleplayer.</summary>
    bool IsCoopClient { get; }
}
