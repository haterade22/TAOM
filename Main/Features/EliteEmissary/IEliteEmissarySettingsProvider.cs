namespace TAOM.Features.EliteEmissary;

/// <summary>MCM-over-config-default reads for the Elite Emissary feature. Every read is null-safe
/// against <c>TaomSettings.Instance</c> (null pre-MCM / if MCM fails to load).</summary>
public interface IEliteEmissarySettingsProvider
{
    /// <summary>Master toggle. When false the menu option never appears.</summary>
    bool IsEnabled { get; }

    /// <summary>When true, the menu option is hidden at settlements whose owner faction maps to no
    /// special resource. When false the option may show disabled with an explanatory hint.</summary>
    bool HideWhenNoResource { get; }
}
