using System;
using System.Collections.Generic;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Map.MapBar;
using TaleWorlds.Core.ViewModelCollection.Information;
using TaleWorlds.Library;
using TaleWorlds.Localization;
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
            _lastAmount = intAmount;
        }

        // Red the day BEFORE troops walk, not after: vanilla lights the gold item on
        // `Gold + projected daily change < 0` (MapInfoVM.UpdatePlayerInfo), and the equivalent here is
        // the desertion trigger itself, balance + net <= 0 with upkeep troops in the party. Same cost
        // class as vanilla's per-refresh CalculateClanGoldChange: one roster walk of dictionary lookups.
        var breakdown = _service.GetDailyBreakdown(hero.StringId, kingdomId, cultureId,
            PartyUpkeepReader.CountOwnedTowns(hero), PartyUpkeepReader.Collect(hero.PartyBelongedTo, _config));
        var warning = breakdown.UpkeepLines.Count > 0 && amount + breakdown.Net <= 0f;
        if (warning != _lastWarning)
        {
            _resourceInfo.HasWarning = warning;
            _lastWarning = warning;
        }

        _baseInitialized = true;
    }

    private bool _lastWarning;

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
        var ownedTowns = PartyUpkeepReader.CountOwnedTowns(hero);
        // The SAME breakdown the daily tick applies. The first version of this tooltip built its own
        // EMPTY troop list here, so "Elite upkeep" could never render and "Net" was always just income;
        // players read that as "no upkeep" while the tick drained them every day (#558).
        var breakdown = _service.GetDailyBreakdown(hero.StringId, kingdomId, cultureId, ownedTowns,
            PartyUpkeepReader.Collect(hero.PartyBelongedTo, _config));

        result.Add(new TooltipProperty(resource.DisplayName, $"{amount:F0} / {resource.Cap:F0}", 0,
            onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.Title));

        var currentTier = _service.GetCurrentTier(hero.StringId, kingdomId, cultureId);
        if (currentTier != null)
        {
            result.Add(new TooltipProperty(Label("{=taom_res_tt_tier}Tier"),
                new TextObject("{=taom_res_tt_tier_value}{LEVEL} ({NAME})")
                    .SetTextVariable("LEVEL", currentTier.Level)
                    .SetTextVariable("NAME", currentTier.Name).ToString(), 0));
            result.Add(new TooltipProperty("", currentTier.Description, 0,
                onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.MultiLine));
        }
        else if (resource.TierThresholds.Count > 0)
        {
            var nextTier = resource.TierThresholds[0];
            result.Add(new TooltipProperty(Label("{=taom_res_tt_next_tier}Next tier at"),
                new TextObject("{=taom_res_tt_next_tier_value}{THRESHOLD} ({NAME})")
                    .SetTextVariable("THRESHOLD", Amount(nextTier.Threshold))
                    .SetTextVariable("NAME", nextTier.Name).ToString(), 0));
        }

        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false,
            TooltipProperty.TooltipPropertyFlags.DefaultSeperator));

        result.Add(new TooltipProperty(Label("{=taom_res_tt_daily_change}Daily change"), "", 0,
            onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.RundownSeperator));
        result.Add(new TooltipProperty(
            new TextObject("{=taom_res_tt_income}Income ({TOWNS} towns)").SetTextVariable("TOWNS", ownedTowns).ToString(),
            "+" + Amount(breakdown.Earning), 0));

        if (breakdown.UpkeepLines.Count > 0)
        {
            result.Add(new TooltipProperty(
                new TextObject("{=taom_res_tt_upkeep}Elite upkeep ({TYPES} troop types)")
                    .SetTextVariable("TYPES", breakdown.UpkeepLines.Count).ToString(),
                "-" + Amount(breakdown.Upkeep), 0));

            // One row per troop type, in the extended (Alt) view: this is the answer to "how much
            // is going out to upkeep", and it is what the whole feature had no way to show.
            foreach (var line in breakdown.UpkeepLines)
            {
                result.Add(new TooltipProperty(
                    new TextObject("{=taom_res_tt_troop_line}{TROOP} x{COUNT}")
                        .SetTextVariable("TROOP", TroopName(line.TroopId))
                        .SetTextVariable("COUNT", line.Count).ToString(),
                    "-" + Amount(line.Total), 0, onlyShowWhenExtended: true));
            }
        }

        result.Add(new TooltipProperty(Label("{=taom_res_tt_net}Net"), Signed(breakdown.Net), 0));

        var daysLeft = breakdown.DaysUntilDepleted(amount);
        if (daysLeft != null)
        {
            result.Add(new TooltipProperty(Label("{=taom_res_tt_depleted_in}Depleted in"),
                new TextObject("{=taom_res_tt_days}{DAYS} days").SetTextVariable("DAYS", daysLeft.Value).ToString(), 0));
        }
        else if (amount <= 0f && breakdown.UpkeepLines.Count > 0 && breakdown.Net <= 0f)
        {
            // The tick adds the net BEFORE it tests the balance, so at zero with income covering upkeep
            // nothing deserts; the notice shows only when the icon is red for the same reason, and it
            // states the rule rather than claiming a loss in progress (Codex, review 95, F3).
            result.Add(new TooltipProperty("",
                new TextObject("{=taom_res_tt_deserting}Elite troops desert each day while you have no {RESOURCE}")
                    .SetTextVariable("RESOURCE", resource.DisplayName).ToString(),
                0, onlyShowWhenExtended: false, TooltipProperty.TooltipPropertyFlags.MultiLine));
        }

        result.Add(new TooltipProperty("", "", 0, onlyShowWhenExtended: false,
            TooltipProperty.TooltipPropertyFlags.DefaultSeperator));
        result.Add(new TooltipProperty(Label("{=taom_res_tt_per_battle}Per battle"), "+" + Amount(resource.PerBattleVictoryBase), 0));
        result.Add(new TooltipProperty(Label("{=taom_res_tt_per_raid}Per raid"), "+" + Amount(resource.PerRaid), 0));
        result.Add(new TooltipProperty(Label("{=taom_res_tt_per_siege}Per siege"), "+" + Amount(resource.PerSiegeVictory), 0));
        result.Add(new TooltipProperty(Label("{=taom_res_tt_per_prisoner}Per prisoner"), "+" + Amount(resource.PerPrisoner), 0));

        return result;
    }

    // TooltipProperty takes strings only (no TextObject overload in v1.4.8), so every label is
    // rendered here; the {=key} tag is what makes the row translatable, TextWidget never parses it.
    private static string Label(string template) => new TextObject(template).ToString();

    private static string Amount(float value) => SpecialResourceMessages.FormatAmount(value);

    private static string Signed(float value) => value >= 0f ? "+" + Amount(value) : Amount(value);

    private static string TroopName(string troopId) =>
        CharacterObject.Find(troopId)?.Name?.ToString() ?? troopId;
}
