namespace TAOM.Features.Encyclopedia;

/// <summary>
/// Production wire-up of <see cref="IEncyclopediaSettingsProvider"/> reading from the MCM
/// <c>TaomSettings</c> singleton. Phase 9b #145.
/// </summary>
public class EncyclopediaSettingsProvider : IEncyclopediaSettingsProvider
{
    public bool ShowAllEncyclopediaCharacters => TaomSettings.Instance?.ShowAllEncyclopediaCharacters ?? true;
}
