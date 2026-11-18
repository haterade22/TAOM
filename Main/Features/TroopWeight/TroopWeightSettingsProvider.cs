using TAOM.Features.CampaignStartOptions;

namespace TAOM.Features.TroopWeight;

public sealed class TroopWeightSettingsProvider : ITroopWeightSettingsProvider
{
    private readonly ICampaignStartOptions _campaign;
    private TaomSettings? _settings;

    // Read on party-size, party-screen and recruitment paths: take the MCM reference once, never resolve Instance per call.
    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public TroopWeightSettingsProvider(ICampaignStartOptions campaign)
    {
        _campaign = campaign;
    }

    public bool IsEnabled => _campaign.TroopWeight ?? NewCampaignDefault;

    public bool NewCampaignDefault => Settings?.EnableTroopWeight ?? true;
}
