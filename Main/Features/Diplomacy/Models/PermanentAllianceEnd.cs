namespace TAOM.Features.Diplomacy.Models;

/// <summary>What <c>IDiplomacyService.EndPermanentAlliance</c> did (#1105 slice 5).</summary>
public enum PermanentAllianceEnd
{
    /// <summary>The pair is recorded as broken and the engine alliance is gone (or the two were never allied).</summary>
    Ended,

    /// <summary>The pair was already recorded as broken.</summary>
    AlreadyEnded,

    /// <summary>The pair has no Permanent row in diplomacy.json: nothing changed.</summary>
    NotPermanent,

    /// <summary>The pair is recorded as broken, but the engine still reports them allied after its EndAlliance.</summary>
    StillAllied,
}
