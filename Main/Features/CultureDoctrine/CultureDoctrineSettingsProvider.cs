namespace TAOM.Features.CultureDoctrine;

public sealed class CultureDoctrineSettingsProvider : ICultureDoctrineSettingsProvider
{
    private readonly ICultureDoctrineConfigProvider _config;

    // Read per agent stat update by TaomAgentStatCalculateModel (through CultureAggressionService): cached
    // on the first non-null read, read through (BattleBalanceSettingsProvider pattern).
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public CultureDoctrineSettingsProvider(ICultureDoctrineConfigProvider config)
    {
        _config = config;
    }

    internal CultureDoctrineSettingsProvider(ICultureDoctrineConfigProvider config, TaomSettings settings)
        : this(config) => _settings = settings;

    public bool IsEnabled =>
        (Settings?.EnableCultureDoctrine ?? false) && _config.GetCatalog().Enabled;

    public bool IsDebug => Settings?.CultureDoctrineDebug ?? false;

    public bool IsMoraleEnabled => IsEnabled && (Settings?.CultureDoctrineMorale ?? false);

    public bool IsAggressionEnabled => IsEnabled && (Settings?.CultureDoctrineAggression ?? false);
}
