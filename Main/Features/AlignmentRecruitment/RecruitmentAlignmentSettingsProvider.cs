using TAOM.Features;

namespace TAOM.Features.AlignmentRecruitment;

/// <summary>
/// Merges MCM live values (<c>TaomSettings.Instance</c>) over JSON defaults. Mirrors
/// <c>CastleRecruitmentSettingsProvider</c>. <c>TaomSettings.Instance</c> can be null very early in
/// startup or if MCM fails to load — the <c>?? default</c> fallback keeps every read safe.
/// </summary>
public sealed class RecruitmentAlignmentSettingsProvider : IRecruitmentAlignmentSettingsProvider
{
    private readonly RecruitmentAlignmentConfig _defaults;

    public RecruitmentAlignmentSettingsProvider(IRecruitmentAlignmentConfigProvider configProvider)
    {
        _defaults = configProvider.GetConfig();
    }

    public bool IsEnabled => TaomSettings.Instance?.EnableAlignmentRecruitment ?? _defaults.Enabled;

    public bool ApplyToAi => TaomSettings.Instance?.EnableAlignmentRecruitmentAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => TaomSettings.Instance?.EnableAlignmentRecruitmentPlayer ?? _defaults.ApplyToPlayer;

    public bool GoodRejectsEvilOnly =>
        TaomSettings.Instance?.AlignmentRecruitmentGoodRejectsEvilOnly ?? _defaults.GoodRejectsEvilOnly;
}
