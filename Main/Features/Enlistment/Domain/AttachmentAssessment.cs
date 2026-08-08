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

    /// <summary>
    /// The commander's column has entered a settlement and the player is still outside it.
    /// Follow him in — standing invisibly at the gate for the whole stop is what service
    /// looked like before this existed.
    /// </summary>
    SettlementFollowRequired = 4,

    /// <summary>
    /// The player is inside a settlement the commander is not in. Leave BEFORE anything else
    /// happens: a party whose <c>CurrentSettlement</c> points at one place while it joins a
    /// battle somewhere else is in two places at once, and for a joining defender the engine
    /// rewrites a siege assault off exactly that field.
    /// </summary>
    SettlementExitRequired = 5,
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
