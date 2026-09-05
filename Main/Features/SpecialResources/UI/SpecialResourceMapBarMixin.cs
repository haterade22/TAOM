using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TAOM.Core.Logging;

namespace TAOM.Features.SpecialResources.UI;

[ViewModelMixin("Refresh")]
internal class SpecialResourceMapBarMixin : BaseViewModelMixin<MapInfoVM>
{
    private readonly ISpecialResourceService _service;
    private readonly ISpecialResourceConfigProvider _config;
    private readonly IModLogger _logger;

    // Rate limiting, keyed per exception type rather than latched once. Both members below run per
    // map-bar refresh / per hover, so an unguarded log call would spam the file at frame rate. A
    // single bool would be worse than spam though: it logs the first failure and hides every later
    // DIFFERENT one for the life of the process, so fixing the first failure makes a second one
    // invisible rather than revealing it. Keying on the exception type keeps the volume bounded while
    // still reporting a genuinely new fault.
    private readonly HashSet<string> _refreshFailuresLogged = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _tooltipFailuresLogged = new HashSet<string>(StringComparer.Ordinal);

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
        _logger = IoC.Resolve<IModLogger>();
        _resourceInfo = new MapInfoItemVM("special_resource", GetTooltipProperties);
    }

    /// <summary>
    /// Both engine entry points are wrapped because a throw from either reaches no TAOM log: these
    /// are invoked by the engine, and nothing on either path writes to our logger, so the only
    /// symptom would be a bar that stops producing tooltips with the log silent. Diagnosing the
    /// 2026-09-03 "no tooltips on a pre-made hero" report cost two wrong root causes precisely
    /// because nothing on this path could speak.
    ///
    /// Be clear that this CHANGES behaviour rather than only observing it. Before, an exception
    /// unwound into the engine's own handling; now it becomes a logged no-op. That is the intended
    /// trade for a mixin injected into a vanilla ViewModel (a mod's optional extra row should not
    /// take the map bar down with it), but it is a behaviour change, not pure instrumentation.
    /// </summary>
    public override void OnRefresh()
    {
        try
        {
            OnRefreshCore();
        }
        catch (Exception ex)
        {
            if (!_refreshFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)")) return;
            _logger?.LogError($"[SpecialResources] map-bar mixin OnRefresh threw, so the bar's refresh "
                            + $"is being aborted by this feature every tick. Root cause: {ex}");
        }
    }

    private void OnRefreshCore()
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

        // Add to SecondaryInfoItems once (works with vanilla MapInfoItemVM)
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
        try
        {
            return GetTooltipPropertiesCore();
        }
        catch (Exception ex)
        {
            if (_tooltipFailuresLogged.Add(ex.GetType().FullName ?? "(unknown)"))
            {
                _logger?.LogError($"[SpecialResources] map-bar tooltip callback threw while building "
                                + $"hint content. Root cause: {ex}");
            }

            // An empty list, never a rethrow: this callback is invoked from inside the engine's hover
            // dispatch, so an escaping exception takes the hint system with it rather than this row.
            return new List<TooltipProperty>();
        }
    }

    private List<TooltipProperty> GetTooltipPropertiesCore()
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
