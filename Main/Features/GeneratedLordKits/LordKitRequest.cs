namespace TAOM.Features.GeneratedLordKits;

/// <summary>
/// The hero a kit is being chosen for. <see cref="InMinorFactionClan"/> reads the clan, not
/// <c>Hero.IsMinorFactionHero</c>: the engine sets that flag only after <c>CreateSpecialHero</c> returns, while
/// the clan is already assigned when the equipment is chosen.
/// </summary>
public sealed record LordKitRequest(string CultureId, int Race, bool IsFemale, bool IsCivilian,
    bool IsLord, bool IsAdult, bool InMinorFactionClan);
