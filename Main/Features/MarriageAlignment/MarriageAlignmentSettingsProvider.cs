using TAOM.Features;
using TAOM.Features.CampaignStartOptions;

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

    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    private readonly ICampaignStartOptions _campaign;

    public MarriageAlignmentSettingsProvider(IMarriageAlignmentConfigProvider configProvider, ICampaignStartOptions campaign)
    {
        _defaults = configProvider.GetConfig();
        _campaign = campaign;
    }

    // The campaign's Alignment rules preset (Strict or Relaxed) first; Standard pins nothing and follows MCM.
    public bool IsEnabled => _campaign.AlignmentRules?.MarriageBlock ?? NewCampaignDefault;

    public bool NewCampaignDefault => Settings?.EnableMarriageAlignment ?? _defaults.Enabled;

    public bool ApplyToAi => Settings?.EnableMarriageAlignmentAi ?? _defaults.ApplyToAi;

    public bool ApplyToPlayer => Settings?.EnableMarriageAlignmentPlayer ?? _defaults.ApplyToPlayer;

    public bool SteerAiPartnerSearch =>
        Settings?.MarriageAlignmentSteerAiSearch ?? _defaults.SteerAiPartnerSearch;
}
