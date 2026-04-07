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
        if (kingdomId == null)
        {
            RemoveItem();
            return;
        }

        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null)
        {
            RemoveItem();
            return;
        }

        EnsureItemAdded();

        var amount = _service.GetCurrentAmount(hero.StringId);
        var intAmount = (int)amount;
        _resourceItem.IntValue = intAmount;
        _resourceItem.HasWarning = amount <= 0f;

        if (intAmount != _lastAmount)
        {
            _resourceItem.Value = intAmount.ToString();
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

        // VisualId stays "special_resource" — the SpecialResourceSpriteWidget
        // detects this sentinel and loads the correct sprite dynamically.
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
        if (kingdomId == null) return result;

        var resource = _config.GetByKingdomId(kingdomId);
        if (resource == null) return result;

        var amount = _service.GetCurrentAmount(hero.StringId);
        var ownedTowns = CountOwnedTowns(hero);
        var dailyEarning = _service.GetDailyEarning(kingdomId, ownedTowns);

        result.Add(new TooltipProperty(resource.DisplayName, $"{amount:F0} / {resource.Cap:F0}", 0));
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
