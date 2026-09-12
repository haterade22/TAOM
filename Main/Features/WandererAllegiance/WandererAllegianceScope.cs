namespace TAOM.Features.WandererAllegiance;

/// <summary>Which wanderers the allegiance rule applies to.</summary>
public enum WandererAllegianceScope
{
    /// <summary>Every wanderer, by the culture on the hero.</summary>
    AllWanderers,

    /// <summary>Only the lore characters listed in <c>named_companions/named_companion_config.json</c>.</summary>
    NamedCompanionsOnly,
}
