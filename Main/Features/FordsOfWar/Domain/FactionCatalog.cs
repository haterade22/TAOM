using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

internal sealed record FactionCatalog(string FactionId, string CultureId,
    IReadOnlyList<UnitOffer> Units, IReadOnlyList<HeroOffer> Heroes, IReadOnlyList<FreeWaveTier> FreeWaves);
