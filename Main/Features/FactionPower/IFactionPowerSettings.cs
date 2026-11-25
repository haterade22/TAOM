using System.Collections.Generic;

namespace TAOM.Features.FactionPower;

/// <summary>
/// The faction power values of the campaign now loaded, saved with the campaign. Every answer is 1 (100 percent) for a null or
/// unknown culture, for a culture at 100, and while no campaign is loaded.
/// </summary>
public interface IFactionPowerSettings
{
    float Multiplier(string? cultureId);

    int Percent(string? cultureId);

    /// <summary>The cultures not at 100 and their percent.</summary>
    IReadOnlyDictionary<string, int> Snapshot();

    /// <summary>Forgets the values and reads the campaign now in place (none: empty), so one campaign never inherits another's.</summary>
    void Reload();

    /// <summary>Writes one value into the campaign. False, and nothing changed, for an unknown culture, a percent outside 50 to 200, or no campaign.</summary>
    bool TrySet(string cultureId, int percent);
}
