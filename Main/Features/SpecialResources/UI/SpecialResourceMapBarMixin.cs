using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.SpecialResources.UI;

[ViewModelMixin("RefreshValues")]
internal class SpecialResourceMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;
    private MapInfoItemVM _resourceItem;
    private bool _itemAdded;
    private int _lastAmount = -1;
    private Domain.SpecialResource _lastResource;
    private string _lastKingdomId;
    private string _lastCultureId;

    public SpecialResourceMapBarMixin(MapInfoVM viewModel) : base(viewModel)
    {
        _service = IoC.Resolve<ISpecialResourceService>();
        _config = IoC.Resolve<ISpecialResourceConfigProvider>();
    }

    public override void OnRefresh()
    {
        if (Campaign.Current == null)
            return;

        var hero = Hero.MainHero;
        if (hero == null)
            return;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        var cultureId = hero.Culture?.StringId;

        if (kingdomId != _lastKingdomId || cultureId != _lastCultureId)
        {
            _lastResource = _service.ResolveResource(kingdomId, cultureId);
            _lastKingdomId = kingdomId;
            _lastCultureId = cultureId;
        }

        var resource = _lastResource;

        if (resource == null)
        {
            RemoveItem();
            return;
        }

        EnsureItemAdded();

        var amount = _service.GetCurrentAmount(hero.StringId, kingdomId, cultureId);
        var intAmount = (int)amount;
        _resourceItem.IntValue = intAmount;
        _resourceItem.HasWarning = amount <= 0f;

        if (intAmount != _lastAmount)
        {
            var tier = _service.GetCurrentTier(hero.StringId, kingdomId, cultureId);
            _resourceItem.Value = tier != null
                ? $"{intAmount} ({tier.Name})"
                : intAmount.ToString();
            _lastAmount = intAmount;
        }
    }

    public override void OnFinalize()
    {
        RemoveItem();
    }

    private void EnsureItemAdded()
    {
        if (_itemAdded && _resourceItem != null)
            return;

        _resourceItem = new MapInfoItemVM("special_resource", GetTooltipProperties);

        if (ViewModel is MapInfoVM mapInfo)
        {
            mapInfo.SecondaryInfoItems.Add(_resourceItem);
            _itemAdded = true;
        }
    }

    private void RemoveItem()
    {
        if (!_itemAdded || _resourceItem == null)
            return;

        if (ViewModel is MapInfoVM mapInfo)
        {
            mapInfo.SecondaryInfoItems.Remove(_resourceItem);
            _itemAdded = false;
        }
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

        result.Add(new TooltipProperty(resource.DisplayName, $"{amount:F0} / {resource.Cap:F0}", 0));

        var currentTier = _service.GetCurrentTier(hero.StringId, kingdomId, cultureId);
        if (currentTier != null)
        {
            result.Add(new TooltipProperty("Tier", $"{currentTier.Level} — {currentTier.Name}", 0));
            result.Add(new TooltipProperty("Effect", currentTier.Description, 0));
        }
        else if (resource.TierThresholds.Count > 0)
        {
            var nextTier = resource.TierThresholds[0];
            result.Add(new TooltipProperty("Next tier at", $"{nextTier.Threshold:F0} ({nextTier.Name})", 0));
        }

        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.DefaultSeperator));
        result.Add(new TooltipProperty("Daily from towns", $"+{dailyEarning:F1}", 0));
        result.Add(new TooltipProperty("Per battle victory", $"+{resource.PerBattleVictoryBase:F0}", 0));
        result.Add(new TooltipProperty("Per raid", $"+{resource.PerRaid:F0}", 0));
        result.Add(new TooltipProperty("Per siege victory", $"+{resource.PerSiegeVictory:F0}", 0));
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
