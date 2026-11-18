using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.CampaignStartOptions;

/// <summary>
/// After a LOADED campaign starts its session: when a value the campaign pinned differs from MCM now, say once
/// that the campaign keeps its own start choices. A new campaign never gets it (its own screen just set them).
/// </summary>
public sealed class CampaignStartMismatchNotice
{
    private readonly ICampaignStartOptions _campaign;
    private readonly ICampaignStartDefaults _defaults;
    private readonly IAdvancedStartDataAdapter _data;
    private readonly ICampaignStartNoticeAdapter _notice;
    private readonly IModLogger _logger;

    public CampaignStartMismatchNotice(
        ICampaignStartOptions campaign, ICampaignStartDefaults defaults, IAdvancedStartDataAdapter data,
        ICampaignStartNoticeAdapter notice, IModLogger logger)
    {
        _campaign = campaign;
        _defaults = defaults;
        _data = data;
        _notice = notice;
        _logger = logger;
    }

    /// <summary>True when the message was shown.</summary>
    public bool ShowIfDifferent()
    {
        if (!_data.IsAvailable || _data.IsNewCampaign) return false;
        if (!CampaignStartMismatch.Differs(_campaign, _defaults.Current())) return false;

        _notice.ShowCampaignKeepsStartChoices();
        _logger.LogInfo("CampaignStartOptions.Mismatch: a pinned start choice differs from the MCM value now; showed the notice");
        return true;
    }
}
