namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>A hero a commander can field. HeroId is an NPCCharacter id.</summary>
internal sealed record HeroOffer(string HeroId, int Price);
