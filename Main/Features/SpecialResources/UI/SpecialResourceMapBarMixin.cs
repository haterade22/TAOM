using System;
using System.Collections.Generic;
using HarmonyLib;
using TAOM.Core.UI;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;

namespace TAOM.Features.SpecialResources.UI;

/// <summary>
/// Injects a special-resource MapInfoItemVM into MapInfoVM.SecondaryInfoItems.
/// Replaces the UIExtenderEx BaseViewModelMixin with direct Harmony patches +
/// TAOM's ViewModelMixinSupport infrastructure.
/// </summary>
internal static class SpecialResourceMapBarMixin
{
    [HarmonyPatchCategory("Patch26_SpecialResources")]
    [HarmonyPatch(typeof(MapInfoVM), MethodType.Constructor)]
    internal static class MapInfoVM_Constructor_Patch
    {
        static void Postfix(MapInfoVM __instance)
        {
            var data = ViewModelMixinSupport.GetOrCreate(__instance, () => new MixinData(__instance));
            ViewModelMixinSupport.InjectBindings(__instance, data, typeof(MixinData));
            data.OnRefresh();
        }
    }

    [HarmonyPatchCategory("Patch26_SpecialResources")]
    [HarmonyPatch(typeof(MapInfoVM), nameof(MapInfoVM.RefreshValues))]
    internal static class MapInfoVM_RefreshValues_Patch
    {
        static void Postfix(MapInfoVM __instance)
        {
            if (ViewModelMixinSupport.TryGet<MixinData>(__instance, out var data))
                data.OnRefresh();
        }
    }

    internal sealed class MixinData
    {
        private readonly WeakReference<MapInfoVM> _vmRef;
        private readonly ISpecialResourceService _service;
        private MapInfoItemVM _resourceItem;
        private bool _itemAdded;
        private int _lastAmount = -1;

        public MixinData(MapInfoVM vm)
        {
            _vmRef = new WeakReference<MapInfoVM>(vm);
            _service = IoC.Resolve<ISpecialResourceService>();
        }

        public void OnRefresh()
        {
            if (Campaign.Current == null)
                return;

            var hero = Hero.MainHero;
            if (hero == null)
                return;

            var kingdomId = hero.Clan?.Kingdom?.StringId;
            var cultureId = hero.Culture?.StringId;
            var resource = _service.ResolveResource(kingdomId, cultureId);

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
                _resourceItem.Value = intAmount.ToString();
                _lastAmount = intAmount;
            }
        }

        public void OnFinalize()
        {
            RemoveItem();
        }

        private void EnsureItemAdded()
        {
            if (_itemAdded && _resourceItem != null)
                return;

            _resourceItem = new MapInfoItemVM("special_resource", GetTooltipProperties);

            if (_vmRef.TryGetTarget(out var mapInfo))
            {
                mapInfo.SecondaryInfoItems.Add(_resourceItem);
                _itemAdded = true;
            }
        }

        private void RemoveItem()
        {
            if (!_itemAdded || _resourceItem == null)
                return;

            if (_vmRef.TryGetTarget(out var mapInfo))
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
}
