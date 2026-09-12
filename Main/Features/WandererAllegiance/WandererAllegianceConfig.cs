namespace TAOM.Features.WandererAllegiance;

/// <summary>
/// JSON DTO for <c>wanderer_allegiance/wanderer_allegiance_config.json</c>. Loaded and validated by
/// <see cref="WandererAllegianceConfigProvider"/>; MCM (<c>TaomSettings</c>) overrides both fields at
/// runtime via <see cref="WandererAllegianceSettingsProvider"/>.
/// </summary>
public class WandererAllegianceConfig
{
    public const string ScopeAllWanderers = "AllWanderers";
    public const string ScopeNamedCompanionsOnly = "NamedCompanionsOnly";

    /// <summary>Master toggle. When false, every wanderer hires as in vanilla.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// <see cref="ScopeAllWanderers"/> or <see cref="ScopeNamedCompanionsOnly"/>. The provider
    /// normalises case and reverts anything else to the default with a warning: the consumer
    /// branches on this string, so a typo must not silently pick a mode.
    /// </summary>
    public string Scope { get; set; } = ScopeAllWanderers;
}
