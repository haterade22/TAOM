namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A pack of troops a commander can buy for one lane.</summary>
internal sealed record UnitOffer(string OfferId, string TroopId, UnitRole Role, int PackSize, int PackPrice);
