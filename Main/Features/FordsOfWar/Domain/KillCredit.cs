namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>What <c>OnUnitKilled</c> did with a death.</summary>
internal enum KillCredit
{
    Unregistered,
    AfterEnd,
    KillerUnknown,
    SameSide,
    Paid,
}
