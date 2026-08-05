namespace TAOM.Features.Enlistment.Domain;

public enum AttachmentStatus
{
    /// <summary>Parked and healthy — nothing to do.</summary>
    Attached = 0,

    /// <summary>Service state says attached but the party isn't parked — park it.</summary>
    AttachRequired = 1,

    /// <summary>Commander's party is in a map event the player hasn't joined — battle layer takes over.</summary>
    BattleJoinRequired = 2,

    /// <summary>No attachment action may run; see <see cref="AttachmentBlockReason"/>.</summary>
    Blocked = 3,
}

public enum AttachmentBlockReason
{
    None = 0,

    /// <summary>State is not EnlistedAttached/EnlistedBattle — duty, captivity, and grace modes own their own presence.</summary>
    NotInAttachableState = 1,

    /// <summary>Vanilla captivity owns the party — never touch it.</summary>
    PlayerCaptive = 2,

    /// <summary>Player is in a map event the commander isn't part of — let it resolve.</summary>
    PlayerInForeignMapEvent = 3,

    /// <summary>Commander missing/dead/party-less — the reconciler moves to CommanderUnavailable.</summary>
    CommanderPartyMissing = 4,
}

/// <summary>Result of the single pure attachment computation that replaced the donor's five overlapping predicates.</summary>
public sealed class AttachmentAssessment
{
    public AttachmentStatus Status { get; }
    public AttachmentBlockReason BlockReason { get; }

    public AttachmentAssessment(AttachmentStatus status, AttachmentBlockReason blockReason = AttachmentBlockReason.None)
    {
        Status = status;
        BlockReason = blockReason;
    }
}
