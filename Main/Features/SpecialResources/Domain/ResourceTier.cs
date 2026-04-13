namespace TAOM.Features.SpecialResources.Domain;

/// <summary>
/// Represents a single tier milestone for a special resource.
/// When a hero's resource amount reaches <see cref="Threshold"/> they unlock
/// the named tier and its associated gameplay effects.
/// </summary>
public sealed class ResourceTier
{
    /// <summary>Ordinal level (1 = lowest, N = highest).</summary>
    public int Level { get; }

    /// <summary>Display name shown in the UI (e.g. "Journeyman Smith").</summary>
    public string Name { get; }

    /// <summary>Resource amount required to reach this tier.</summary>
    public float Threshold { get; }

    /// <summary>Tooltip text describing the gameplay bonus at this tier.</summary>
    public string Description { get; }

    public ResourceTier(int level, string name, float threshold, string description)
    {
        Level = level;
        Name = name;
        Threshold = threshold;
        Description = description;
    }
}
