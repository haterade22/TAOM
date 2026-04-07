using System.Collections.Generic;
using System.Linq;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.SpecialResources.UI;

[ViewModelMixin("RefreshValues")]
internal class SpecialResourceMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    private MapInfoItemVM _resourceItem;
    private bool _itemAdded;

    public SpecialResourceMapBarMixin(MapInfoVM viewModel) : base(viewModel)
    {
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

        var config = IoC.Resolve<ISpecialResourceConfigProvider>();
        var resource = config.GetByKingdomId(kingdomId);
        if (resource == null)
        {
            RemoveItem();
            return;
        }

        EnsureItemAdded(resource.IconSpriteName);

        var service = IoC.Resolve<ISpecialResourceService>();
        var amount = service.GetCurrentAmount(hero.StringId);
        _resourceItem.IntValue = (int)amount;
        _resourceItem.Value = $"{amount:F0}";
        _resourceItem.HasWarning = amount <= 0f;
    }

    public override void OnFinalize()
    {
        RemoveItem();
    }

    private void EnsureItemAdded(string iconSpriteName)
    {
        if (_itemAdded && _resourceItem != null)
            return;

        _resourceItem = new MapInfoItemVM("special_resource", GetTooltipProperties);
        _resourceItem.SetOverriddenVisualId(iconSpriteName);

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
        var result = new List<TooltipProperty>();

        var hero = Hero.MainHero;
        if (hero == null) return result;

        var kingdomId = hero.Clan?.Kingdom?.StringId;
        if (kingdomId == null) return result;

        var config = IoC.Resolve<ISpecialResourceConfigProvider>();
        var resource = config.GetByKingdomId(kingdomId);
        if (resource == null) return result;

        var service = IoC.Resolve<ISpecialResourceService>();
        var amount = service.GetCurrentAmount(hero.StringId);
        var ownedTowns = hero.Clan?.Settlements?.Count(s => s.IsTown) ?? 0;
        var dailyEarning = service.GetDailyEarning(kingdomId, ownedTowns);

        result.Add(new TooltipProperty(resource.DisplayName, $"{amount:F0} / {resource.Cap:F0}", 0));
        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.DefaultSeperator));
        result.Add(new TooltipProperty("Daily from towns", $"+{dailyEarning:F1}", 0));
        result.Add(new TooltipProperty("Per battle victory", $"+{resource.PerBattleVictoryBase:F0}", 0));
        result.Add(new TooltipProperty("Per raid", $"+{resource.PerRaid:F0}", 0));
        result.Add(new TooltipProperty("Per siege victory", $"+{resource.PerSiegeVictory:F0}", 0));
        result.Add(new TooltipProperty("Per prisoner", $"+{resource.PerPrisoner:F0}", 0));

        return result;
    }
}
