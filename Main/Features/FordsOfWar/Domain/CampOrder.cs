namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A camp's creatures go out to fight (Alerted) or back to the centre of the camp.</summary>
internal sealed record CampOrder(string CampId, bool Alerted);
