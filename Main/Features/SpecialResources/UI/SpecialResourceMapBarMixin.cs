using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;

namespace TAOM.Features.SpecialResources.UI;

[ViewModelMixin("Refresh")]
internal class SpecialResourceMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;
    private MapInfoItemVM _resourceInfo;
    private bool _itemAdded;
    private bool _baseInitialized;
    private int _lastAmount = -1;
    private Domain.SpecialResource _lastResource;
    private string _lastKingdomId;
    private string _lastCultureId;

    public SpecialResourceMapBarMixin(MapInfoVM viewModel) : base(viewModel)
    {
        _service = IoC.Resolve<ISpecialResourceService>();
        _config = IoC.Resolve<ISpecialResourceConfigProvider>();
        _resourceInfo = new MapInfoItemVM("special_resource", GetTooltipProperties);
    }

    public override void OnRefresh()
    {
        if (Campaign.Current == null) return;

        var hero = Hero.MainHero;
        if (hero == null) return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;

        if (kingdomId != _lastKingdomId || cultureId != _lastCultureId)
        {
            _lastResource = _service.ResolveResource(kingdomId, cultureId);
            _lastKingdomId = kingdomId;
            _lastCultureId = cultureId;
            _lastAmount = -1;
        }

        var resource = _lastResource;
        if (resource == null) return;

        // Add to SecondaryInfoItems once (TOR pattern — works with vanilla MapInfoItemVM)
        if (_baseInitialized && !_itemAdded && ViewModel is MapInfoVM mapInfo)
        {
            mapInfo.SecondaryInfoItems.Add(_resourceInfo);
            _itemAdded = true;
        }

        var amount = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
        var intAmount = (int)amount;

        if (intAmount != _lastAmount)
        {
            _resourceInfo.Value = intAmount.ToString();
            _resourceInfo.IntValue = intAmount;
            _resourceInfo.HasWarning = amount <= 0f;
            _lastAmount = intAmount;
        }

        _baseInitialized = true;
    }

    private List<TooltipProperty> GetTooltipProperties()
    {
        var result = new List<TooltipProperty>(8);

        var hero = Hero.MainHero;
        if (hero == null) return result;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;
        var resource = _service.ResolveResource(kingdomId, cultureId);
        if (resource == null) return result;

        var amount = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
        var ownedTowns = CountOwnedTowns(hero);
        var dailyEarning = _service.GetDailyEarning(kingdomId, cultureId, ownedTowns);
        var upkeepTroops = new List<TroopUpkeepInfo>();
        // Upkeep calculation deferred to tooltip — no hot-path cost
        var dailyUpkeep = _service.GetDailyUpkeep(upkeepTroops, hero.StringId);

        result.Add(new TooltipProperty(resource.DisplayName, $"{amount:F0} / {resource.Cap:F0}", 0,
            onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.Title));

        var currentTier = _service.GetCurrentTier(hero.StringId, kingdomId, cultureId);
        if (currentTier != null)
        {
            result.Add(new TooltipProperty("Tier", $"{currentTier.Level} — {currentTier.Name}", 0));
            result.Add(new TooltipProperty("", currentTier.Description, 0,
                onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.MultiLine));
        }
        else if (resource.TierThresholds.Count > 0)
        {
            var nextTier = resource.TierThresholds[0];
            result.Add(new TooltipProperty("Next tier at", $"{nextTier.Threshold:F0} ({nextTier.Name})", 0));
        }

        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false,
            TooltipProperty.TooltipPropertyFlags.DefaultSeperator));

        var net = dailyEarning - dailyUpkeep;
        result.Add(new TooltipProperty("Daily Change", "", 0,
            onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.RundownSeperator));
        result.Add(new TooltipProperty($"Income ({ownedTowns} towns)", $"+{dailyEarning:F1}", 0));
        if (dailyUpkeep > 0)
            result.Add(new TooltipProperty("Elite upkeep", $"-{dailyUpkeep:F1}", 0));
        result.Add(new TooltipProperty("Net", net >= 0 ? $"+{net:F1}" : $"{net:F1}", 0));

        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false,
            TooltipProperty.TooltipPropertyFlags.DefaultSeperator));
        result.Add(new TooltipProperty("Per battle", $"+{resource.PerBattleVictoryBase:F0}", 0));
        result.Add(new TooltipProperty("Per raid", $"+{resource.PerRaid:F0}", 0));
        result.Add(new TooltipProperty("Per siege", $"+{resource.PerSiegeVictory:F0}", 0));
        result.Add(new TooltipProperty("Per prisoner", $"+{resource.PerPrisoner:F0}", 0));

        return result;
    }

    private static int CountOwnedTowns(Hero hero)
    {
        var settlements = hero.Clan?.Settlements;
        if (settlements == null) return 0;

        var count = 0;
        foreach (var settlement in settlements)
            if (settlement.IsTown)
                count++;
        return count;
    }
}
