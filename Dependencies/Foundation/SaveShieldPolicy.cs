using System;

namespace TAOM.Dependencies.Foundation;

/// <summary>
/// The swallow decision behind <see cref="SaveShield"/>, extracted as a pure function so the full
/// input matrix can be tested without Harmony or a running game.
/// </summary>
public static class SaveShieldPolicy
{
    /// <summary>Category tag on SaveShield's save/load-chain targets.</summary>
    public const string SaveLoadCategory = "SAVE-LOAD";

    /// <summary>Category tag on SaveShield's mission-init-chain targets.</summary>
    public const string MissionInitCategory = "MISSION-INIT";

    /// <summary>
    /// Whether the finalizer should swallow the exception (return null) or let it propagate.
    ///
    /// <paramref name="swallowDisabledByFlag"/> dominates: <c>saveshield-swallow-disabled.flag</c>
    /// is the blunt "rethrow everything" escalation used during triage, and nothing overrides it.
    ///
    /// Otherwise the only change from historic behaviour is the SAVE-LOAD chain during a co-op
    /// session. Swallowing there produces a partially deserialised campaign that keeps running and
    /// looks fine; a host-authoritative co-op mod then replicates that half-built state to the
    /// other player. A loud load failure is much cheaper than two silently-corrupted saves.
    /// MISSION-INIT keeps swallowing in every case — that fault is local and non-authoritative, so
    /// rethrowing would only convert a recoverable broken battle into a CTD.
    /// </summary>
    public static bool ShouldSwallow(string? category, bool coopActive, bool swallowDisabledByFlag)
    {
        if (swallowDisabledByFlag) return false;
        if (!coopActive) return true;

        return !string.Equals(category, SaveLoadCategory, StringComparison.OrdinalIgnoreCase);
    }
}
