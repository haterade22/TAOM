namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Live (MCM-over-JSON) settings surface for the marriage-alignment gate. Lets the pure
/// <see cref="MarriageAlignmentService"/> stay free of MCM + JSON plumbing.
/// </summary>
public interface IMarriageAlignmentSettingsProvider
{
    bool IsEnabled { get; }
    bool ApplyToAi { get; }
    bool ApplyToPlayer { get; }
    bool SteerAiPartnerSearch { get; }
}
