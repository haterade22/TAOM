using TAOM.Features.CampaignStartOptions;

namespace TAOM.Features.HostilePeace;

/// <summary>The four switches, read live at each question: the MCM group "World/Hostile Peace" when MCM is up, else the JSON.</summary>
public interface IHostilePeaceSettingsProvider
{
    bool BlockCaravanTrade { get; }

    bool BlockTradeAgreements { get; }

    bool LimitTownAccess { get; }

    /// <summary>The campaign's Alignment rules preset (Strict or Relaxed) first; Standard follows MCM, else JSON.</summary>
    bool AllowPlayerAlliancesWithAnyone { get; }

    /// <summary>What a new campaign gets when its alignment preset is Standard: MCM, else JSON.</summary>
    bool AllowPlayerAlliancesNewCampaignDefault { get; }
}

/// <summary>
/// MCM over JSON, the War of the Ring precedent (<c>WarOfTheRingService.GetEffectiveEnabled</c>): MCM's value when its settings
/// exist, else <c>hostile_peace.json</c>. The defaults live here so the <see cref="TaomSettings"/> initializers and the JSON class
/// cannot drift apart. The settings reference is taken lazily and a null (MCM not up yet) is not cached.
/// </summary>
public sealed class HostilePeaceSettingsProvider : IHostilePeaceSettingsProvider
{
    public const bool DefaultBlockCaravanTrade = true;
    public const bool DefaultBlockTradeAgreements = true;
    public const bool DefaultLimitTownAccess = true;
    public const bool DefaultAllowPlayerAlliancesWithAnyone = false;

    private readonly IHostilePeaceConfigProvider _config;
    private readonly ICampaignStartOptions _campaign;
    private TaomSettings? _settings;

    public HostilePeaceSettingsProvider(IHostilePeaceConfigProvider config, ICampaignStartOptions campaign)
    {
        _config = config;
        _campaign = campaign;
    }

    internal HostilePeaceSettingsProvider(IHostilePeaceConfigProvider config, ICampaignStartOptions campaign, TaomSettings settings)
    {
        _config = config;
        _campaign = campaign;
        _settings = settings;
    }

    private TaomSettings? Settings => _settings ??= TaomSettings.Instance;

    public bool BlockCaravanTrade => Settings?.HostilePeaceBlockCaravanTrade ?? _config.Config.BlockCaravanTrade;

    public bool BlockTradeAgreements => Settings?.HostilePeaceBlockTradeAgreements ?? _config.Config.BlockTradeAgreements;

    public bool LimitTownAccess => Settings?.HostilePeaceLimitTownAccess ?? _config.Config.LimitTownAccess;

    public bool AllowPlayerAlliancesWithAnyone =>
        _campaign.AlignmentRules?.AllianceWithAnyone ?? AllowPlayerAlliancesNewCampaignDefault;

    public bool AllowPlayerAlliancesNewCampaignDefault =>
        Settings?.AllowPlayerAlliancesWithAnyone ?? _config.Config.AllowPlayerAlliancesWithAnyone;
}
