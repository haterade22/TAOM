using TAOM.Features;

namespace TAOM.Features.EliteEmissary;

/// <summary>Merges MCM live values (<c>TaomSettings.Instance</c>) over the config default. Mirrors
/// <c>CastleRecruitmentSettingsProvider</c> — <c>TaomSettings.Instance</c> can be null early in
/// startup / if MCM fails to load, so every read falls back safely.</summary>
public sealed class EliteEmissarySettingsProvider : IEliteEmissarySettingsProvider
{
    private readonly bool _defaultEnabled;

    public EliteEmissarySettingsProvider(IEliteEmissaryConfigProvider configProvider)
    {
        _defaultEnabled = configProvider.GetConfig().Enabled;
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableEliteEmissary ?? _defaultEnabled;

    public bool HideWhenNoResource => TaomSettings.Instance?.HideEmissaryWhenNoResource ?? true;
}
