using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed record FactionCatalog(string FactionId, string CultureId,
    IReadOnlyList<UnitOffer> Units, IReadOnlyList<HeroOffer> Heroes, IReadOnlyList<FreeWaveTier> FreeWaves,
    FactionFeats? Feats = null, IReadOnlyList<FactionTrait>? Traits = null, OfferRotation? Rotation = null, CreatureCall? Creatures = null,
    string Name = "", IReadOnlyList<string>? AiBuyCycle = null, IReadOnlyDictionary<string, float>? UpgradeCost = null)
{
    public FactionFeats FeatsOrNone => Feats ?? FactionFeats.None;

    /// <summary>Every offer this faction can ever show: the core list, each region's band and the creatures.</summary>
    public IEnumerable<UnitOffer> AllOffers =>
        Units.Concat(Rotation?.Regions.SelectMany(r => r.Units) ?? Enumerable.Empty<UnitOffer>())
            .Concat(Creatures?.Offers ?? Enumerable.Empty<UnitOffer>());
}
