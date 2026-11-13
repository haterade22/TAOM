namespace TAOM.Features.KingdomAllegiance;

/// <summary>
/// The "Kingdom Allegiance" MCM switch. The default lives here so the property initializer on <see cref="TaomSettings"/> and the
/// fallback before MCM is up cannot drift apart. The settings reference is taken lazily and read through, so a change applies at
/// once; a null (MCM not up yet) is not cached.
/// </summary>
public sealed class KingdomAllegianceSettingsProvider : IKingdomAllegianceSettingsProvider
{
    public const bool DefaultEnabled = true;

    private TaomSettings? _settings;

    public KingdomAllegianceSettingsProvider()
    {
    }

    internal KingdomAllegianceSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool Enabled => From(Settings);

    /// <summary>The setting, or the compiled default when MCM has not built its settings yet.</summary>
    public static bool From(TaomSettings? settings) => settings?.KingdomAllegianceEnabled ?? DefaultEnabled;
}
