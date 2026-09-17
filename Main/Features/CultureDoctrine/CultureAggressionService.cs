using TAOM.Features.CultureDoctrine.Domain;

namespace TAOM.Features.CultureDoctrine;

public sealed class CultureAggressionService : ICultureAggressionService
{
    private readonly ICultureDoctrineConfigProvider _config;
    private readonly ICultureDoctrineSettingsProvider _settings;

    public CultureAggressionService(ICultureDoctrineConfigProvider config, ICultureDoctrineSettingsProvider settings)
    {
        _config = config;
        _settings = settings;
    }

    public CultureAggression Profile(string? cultureId) =>
        _settings.IsAggressionEnabled ? _config.GetCatalog().Resolve(cultureId).Aggression : CultureAggression.Vanilla;
}
