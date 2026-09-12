using System;
using TAOM.Features;

namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over the validated JSON defaults. Mirrors
/// <see cref="MarriageAlignment.MarriageAlignmentSettingsProvider"/>; the dropdown mapping is the
/// <see cref="CaravanTrade.CaravanTradeSettingsProvider"/> shape. <c>TaomSettings.Instance</c> can be
/// null very early in startup or if MCM fails to load, so every read falls back to the JSON value.
/// </summary>
public sealed class WandererAllegianceSettingsProvider : IWandererAllegianceSettingsProvider
{
    private readonly WandererAllegianceConfig _defaults;

    public WandererAllegianceSettingsProvider(IWandererAllegianceConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableWandererAllegiance ?? _defaults.Enabled;

    public WandererAllegianceScope Scope =>
        ResolveScope(TaomSettings.Instance?.WandererAllegianceScope?.SelectedIndex) ?? ParseScope(_defaults.Scope);

    /// <summary>The MCM dropdown index to a scope; null when there is no dropdown or the index is unknown.</summary>
    internal static WandererAllegianceScope? ResolveScope(int? selectedIndex) => selectedIndex switch
    {
        0 => WandererAllegianceScope.AllWanderers,
        1 => WandererAllegianceScope.NamedCompanionsOnly,
        _ => null,
    };

    /// <summary>
    /// The validated JSON string to a scope. The config provider already normalised it to one of the
    /// two constants; anything else still reads as the default rather than throwing.
    /// </summary>
    internal static WandererAllegianceScope ParseScope(string? scope) =>
        string.Equals(scope, WandererAllegianceConfig.ScopeNamedCompanionsOnly, StringComparison.OrdinalIgnoreCase)
            ? WandererAllegianceScope.NamedCompanionsOnly
            : WandererAllegianceScope.AllWanderers;
}
