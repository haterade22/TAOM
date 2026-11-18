using TAOM.Features.CampaignStartOptions;

namespace TAOM.Features.AiPartySize;

public sealed class AiPartyScalingSettingsProvider : IAiPartyScalingSettingsProvider
{
    private readonly ICampaignStartOptions _campaign;

    // Read on every party-size, food and wage recompute: take the MCM reference once and read through it.
    private TaomSettings? _settings;
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public AiPartyScalingSettingsProvider(ICampaignStartOptions campaign)
    {
        _campaign = campaign;
    }

    internal AiPartyScalingSettingsProvider(ICampaignStartOptions campaign, TaomSettings settings)
        : this(campaign) => _settings = settings;

    public bool Enabled => _campaign.AiPartyScaling ?? NewCampaignDefault;

    public bool NewCampaignDefault => Settings?.EnableAiPartyScaling ?? true;
}
