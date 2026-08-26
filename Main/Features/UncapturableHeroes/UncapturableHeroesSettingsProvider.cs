using TAOM.Features.UncapturableHeroes.Domain;

namespace TAOM.Features.UncapturableHeroes;

/// <inheritdoc cref="IUncapturableHeroesSettingsProvider"/>
public sealed class UncapturableHeroesSettingsProvider : IUncapturableHeroesSettingsProvider
{
    private readonly UncapturableHeroesConfig _defaults;

    public UncapturableHeroesSettingsProvider(IUncapturableHeroesConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableUncapturableHeroes ?? _defaults.Enabled;
}
