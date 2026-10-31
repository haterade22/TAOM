using System.Collections.Generic;

namespace TAOM.Features.FordsOfWar.Domain;

/// <summary>
/// Every tunable number of a match. The engine layer builds it from match.json; <see cref="Default"/> holds the
/// shipped numbers (build-spec section 5) for tests and as the fallback when the file is missing or bad.
/// </summary>
internal sealed record FordsMatchConfig(FordsEconomyConfig Economy, FordsWaveConfig Waves,
    FordsTowerConfig Towers, FordsHeroConfig Heroes, FordsLaneConfig Lanes, FordsCaptureConfig Capture,
    IReadOnlyDictionary<string, FordsAiProfile> AiProfiles, float CountdownSeconds)
{
    public static FordsMatchConfig Default { get; } = new(
        new FordsEconomyConfig(600, 120, 0.35f, 2, 300, 250, 60, 99999),
        new FordsWaveConfig(40f, 90, 300, 6, 3),
        new FordsTowerConfig(8000, 11000, 18000, 0.15f, 4f, 3f, 15f, 20f, 2, 4f, 3, 45f, 20f),
        new FordsHeroConfig(2, 60f, 6f, 0.5f, 0.05f),
        new FordsLaneConfig(20f, 25f, 6f, 60f, 40),
        new FordsCaptureConfig(12f, 20f),
        new Dictionary<string, FordsAiProfile>
        {
            ["easy"] = new FordsAiProfile("easy", 12f, 0.8f, 0.25f, 200),
            ["normal"] = new FordsAiProfile("normal", 8f, 1.0f, 0.1f, 300),
            ["hard"] = new FordsAiProfile("hard", 5f, 1.2f, 0.0f, 400),
        },
        10f);
}
