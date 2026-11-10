namespace TAOM.Features.Realms.Domain.Ledger;

public enum PendingKind
{
    /// <summary>Waiting for the named clan's next leader.</summary>
    ClanLeader,

    /// <summary>Waiting for the named kingdom's next ruler.</summary>
    RealmRuler,
}

/// <summary>
/// A title whose heir the game decides after the holder's death: clan leadership and kingdom rule are
/// settled by the engine a moment later, so the title waits here until that leader exists.
/// </summary>
public sealed class PendingSuccession
{
    public PendingSuccession(string titleId, PendingKind kind, string referenceId, string? previousHolderId)
    {
        TitleId = titleId;
        Kind = kind;
        ReferenceId = referenceId;
        PreviousHolderId = previousHolderId;
    }

    public string TitleId { get; }
    public PendingKind Kind { get; }

    /// <summary>Clan id for <see cref="PendingKind.ClanLeader"/>, kingdom id for <see cref="PendingKind.RealmRuler"/>.</summary>
    public string ReferenceId { get; }

    public string? PreviousHolderId { get; }
}
