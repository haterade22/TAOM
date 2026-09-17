namespace TAOM.Features.CultureDoctrine;

public sealed class CultureDoctrineSettingsProvider : ICultureDoctrineSettingsProvider
{
    private readonly ICultureDoctrineConfigProvider _config;

    public CultureDoctrineSettingsProvider(ICultureDoctrineConfigProvider config)
    {
        _config = config;
    }

    public bool IsEnabled =>
        (TaomSettings.Instance?.EnableCultureDoctrine ?? false) && _config.GetCatalog().Enabled;

    public bool IsDebug => TaomSettings.Instance?.CultureDoctrineDebug ?? false;

    public bool IsMoraleEnabled => IsEnabled && (TaomSettings.Instance?.CultureDoctrineMorale ?? false);

    public bool IsAggressionEnabled => IsEnabled && (TaomSettings.Instance?.CultureDoctrineAggression ?? false);
}
