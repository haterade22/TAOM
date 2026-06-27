using TAOM.Features;

namespace TAOM.Features.AlignmentDesertion;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>RecruitmentAlignmentSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null very early
/// in startup or if MCM fails to load — the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class AlignmentDesertionSettingsProvider : IAlignmentDesertionSettingsProvider
{
    private readonly AlignmentDesertionConfig _defaults;

    public AlignmentDesertionSettingsProvider(IAlignmentDesertionConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableAlignmentDesertion ?? _defaults.Enabled;

    public float Rate => TaomSettings.Instance?.AlignmentDesertionRate ?? _defaults.Rate;

    public bool ApplyToAi => TaomSettings.Instance?.EnableAlignmentDesertionAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => TaomSettings.Instance?.EnableAlignmentDesertionPlayer ?? _defaults.ApplyToPlayer;

    public bool ApplyToParties => TaomSettings.Instance?.EnableAlignmentDesertionParties ?? _defaults.ApplyToParties;

    public bool ApplyToGarrisons => TaomSettings.Instance?.EnableAlignmentDesertionGarrisons ?? _defaults.ApplyToGarrisons;
}
