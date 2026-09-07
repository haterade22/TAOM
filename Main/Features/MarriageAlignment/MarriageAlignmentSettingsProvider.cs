using TAOM.Features;

namespace TAOM.Features.MarriageAlignment;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <see cref="AlignmentRecruitment.RecruitmentAlignmentSettingsProvider"/>.
/// <c>TaomSettings.Instance</c> can be null very early in startup or if MCM fails to load, so the
/// <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class MarriageAlignmentSettingsProvider : IMarriageAlignmentSettingsProvider
{
    private readonly MarriageAlignmentConfig _defaults;

    public MarriageAlignmentSettingsProvider(IMarriageAlignmentConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableMarriageAlignment ?? _defaults.Enabled;

    public bool ApplyToAi => TaomSettings.Instance?.EnableMarriageAlignmentAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => TaomSettings.Instance?.EnableMarriageAlignmentPlayer ?? _defaults.ApplyToPlayer;

    public bool SteerAiPartnerSearch =>
        TaomSettings.Instance?.MarriageAlignmentSteerAiSearch ?? _defaults.SteerAiPartnerSearch;
}
