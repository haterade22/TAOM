using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>
/// Invalidates mobile parties' cached member size limits, recomputed lazily on the next read.
/// PartyBase.PartySizeLimit caches the model result keyed on MemberRoster.VersionNo, so a change to
/// anything the model reads (an MCM slider, a career pick) stays invisible until some roster event
/// bumps that counter. Settlement parties need no sweep.
/// </summary>
public interface IPartySizeCacheInvalidator
{
    /// <summary>Every mobile party: for a setting that moves the limit of all of them.</summary>
    void InvalidateAll();

    /// <summary>
    /// Only the parties led by one of <paramref name="heroIds"/> (and the leader party of their army),
    /// for a career refresh, which can only change the limit of a party its hero leads.
    /// </summary>
    void InvalidateLedBy(ICollection<string> heroIds);
}
