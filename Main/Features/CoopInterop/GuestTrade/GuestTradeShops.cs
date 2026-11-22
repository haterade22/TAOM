using System.Collections.Generic;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.Arachnophobia;
using TAOM.Features.ArmourAcquisition;
using TAOM.Features.ArmourAcquisition.Domain;
using TAOM.Features.CultureMarketplace;
using TAOM.Features.EliteEmissary;
using TAOM.Features.SpecialResources;

namespace TAOM.Features.CoopInterop.GuestTrade;

/// <summary>
/// Builds TAOM's own <see cref="EliteEmissaryService"/>, <see cref="ArmouryUpgradeService"/> and
/// <see cref="LordsLadderService"/> for one guest's hero
/// (#1329), with that hero's adapters in place of the main hero's. Everything else is the singletons the host's own
/// screens use, so a guest pays the prices and meets the rules the host would.
/// </summary>
public sealed class GuestTradeShops : IGuestTradeShops
{
    private readonly IEliteEmissaryConfigProvider _emissaryConfig;
    private readonly IEliteEmissarySettingsProvider _emissarySettings;
    private readonly ISpecialResourceService _resources;
    private readonly ISpecialResourceConfigProvider _resourceConfig;
    private readonly ISpecialResourceSettingsProvider _goldOption;
    private readonly IOutcomeLog _outcomes;
    private readonly IArachnophobiaService _arachnophobia;
    private readonly IArmourGateService _gate;
    private readonly IArmourAcquisitionConfigProvider _armourConfig;
    private readonly ArmouryLevelService _levels;
    private readonly IArmouryTownAdapter _towns;
    private readonly ISpecialResourceSpender _spender;
    private readonly ArmourAcquisitionState _ladderState;
    private readonly ICultureMarketplaceConfigProvider _marketplace;
    private readonly IArmourAcquisitionSettingsProvider _armourSettings;
    private readonly IModLogger _logger;

    public GuestTradeShops(IEliteEmissaryConfigProvider emissaryConfig, IEliteEmissarySettingsProvider emissarySettings,
        ISpecialResourceService resources, ISpecialResourceConfigProvider resourceConfig, ISpecialResourceSettingsProvider goldOption,
        IOutcomeLog outcomes, IArachnophobiaService arachnophobia, IArmourGateService gate, IArmourAcquisitionConfigProvider armourConfig,
        ArmouryLevelService levels, IArmouryTownAdapter towns, ISpecialResourceSpender spender, ArmourAcquisitionState ladderState,
        ICultureMarketplaceConfigProvider marketplace, IArmourAcquisitionSettingsProvider armourSettings, IModLogger logger)
    {
        _emissaryConfig = emissaryConfig;
        _emissarySettings = emissarySettings;
        _resources = resources;
        _resourceConfig = resourceConfig;
        _goldOption = goldOption;
        _outcomes = outcomes;
        _arachnophobia = arachnophobia;
        _gate = gate;
        _armourConfig = armourConfig;
        _levels = levels;
        _towns = towns;
        _spender = spender;
        _ladderState = ladderState;
        _marketplace = marketplace;
        _armourSettings = armourSettings;
        _logger = logger;
    }

    public IEliteEmissaryService Emissary(GuestTradeHero hero) =>
        new EliteEmissaryService(_emissaryConfig, _emissarySettings, _resources, _resourceConfig, hero.Party, hero.Player,
            _goldOption, _outcomes, _arachnophobia, _logger);

    public bool HasArmoury(string settlementId) => _gate.IsActive && _towns.IsTown(settlementId);

    public int ArmouryLevel(string settlementId) => _levels.GetTownLevel(settlementId);

    public IReadOnlyList<UpgradeOffer> ArmouryOffers(GuestTradeHero hero, int armouryLevel) =>
        Armoury(hero).BuildOffers(armouryLevel);

    public UpgradeBlock Upgrade(GuestTradeHero hero, UpgradeOffer offer) => Armoury(hero).Execute(offer);

    public bool LadderOpen => _gate.IsActive && _armourSettings.CoopGuestLadderEnabled;

    public int LadderLordLevel => _armourConfig.GetConfig().LordLevel;

    public LordsLadderService Ladder(GuestTradeHero hero) => new(_ladderState, _armourConfig, _gate, hero.Player, _marketplace);

    private ArmouryUpgradeService Armoury(GuestTradeHero hero) => new(_gate, _armourConfig, hero.Player, _spender);
}
