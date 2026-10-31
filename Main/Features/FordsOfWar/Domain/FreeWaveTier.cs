using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>The free wave a lane gets from <paramref name="FromMinute"/> of match time until the next tier.</summary>
internal sealed record FreeWaveTier(float FromMinute, IReadOnlyList<UnitCount> Units);
