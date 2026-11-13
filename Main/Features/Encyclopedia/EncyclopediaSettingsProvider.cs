namespace TAOM.Features.Encyclopedia;

/// <summary>
/// Production wire-up of <see cref="IEncyclopediaSettingsProvider"/> reading from the MCM
/// <c>TaomSettings</c> singleton. Phase 9b #145.
/// </summary>
public class EncyclopediaSettingsProvider : IEncyclopediaSettingsProvider
{
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool ShowAllEncyclopediaCharacters => Settings?.ShowAllEncyclopediaCharacters ?? true;
}
