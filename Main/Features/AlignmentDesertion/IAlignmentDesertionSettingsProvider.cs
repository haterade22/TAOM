namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Live (MCM-over-JSON) settings surface for alignment desertion. Lets the pure
/// <see cref="AlignmentDesertionService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IAlignmentDesertionSettingsProvider
{
    bool IsEnabled { get; }
    float Rate { get; }
    bool ApplyToAi { get; }
    bool ApplyToPlayer { get; }
    bool ApplyToParties { get; }
    bool ApplyToGarrisons { get; }
}
