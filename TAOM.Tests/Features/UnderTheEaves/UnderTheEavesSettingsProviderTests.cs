using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features;
using TAOM.Features.UnderTheEaves;

namespace TAOM.Tests.Features.UnderTheEaves;

/// <summary>
/// #849: the one MCM toggle. It is read for every sight query, so the hot-path read-through rule (one cached settings
/// reference, no other member resolving the MCM instance) is pinned in CampaignHotPathSettingsProvidersTests.
/// </summary>
[TestClass]
public class UnderTheEavesSettingsProviderTests
{
    [TestMethod]
    public void Enabled_NoMcm_FallsBackToTheCompiledDefault()
    {
        // The test host has no MCM, so Instance is null: the fallback is the compiled default, read from the property itself.
        Assert.AreEqual(new TaomSettings().ForestSightPenaltyEnabled, new UnderTheEavesSettingsProvider().Enabled);
        Assert.IsTrue(new UnderTheEavesSettingsProvider().Enabled);
    }

    [TestMethod]
    public void Setting_NeedsNoRestart_AndLivesUnderTheMapUiPage_NextToHiddenHosts()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.ForestSightPenaltyEnabled))!;
        var text = string.Join("|", property.GetCustomAttributesData().Select(a => a.ToString()));

        StringAssert.Contains(text, "Map UI/{=taom_mcm_group_hidden_hosts}Hidden Hosts");
        StringAssert.Contains(text, "RequireRestart = False");
        StringAssert.Contains(text, "Order = 1");
    }

    [TestMethod]
    public void Setting_HasItsOwnOrder_InTheHiddenHostsGroup()
    {
        var orders = typeof(TaomSettings).GetProperties()
            .Select(p => (Property: p, Text: string.Join("|", p.GetCustomAttributesData().Select(a => a.ToString()))))
            .Where(x => x.Text.Contains("\"{=taom_mcm_group_map_ui}Map UI/{=taom_mcm_group_hidden_hosts}Hidden Hosts\""))
            .Select(x => System.Text.RegularExpressions.Regex.Match(x.Text, @"\bOrder = (\d+)").Groups[1].Value)
            .ToList();

        Assert.AreEqual(2, orders.Count, "Hidden Hosts and Under the Eaves share the group");
        Assert.AreEqual(orders.Count, orders.Distinct().Count(), "no two settings share an Order: " + string.Join(",", orders));
    }

    [TestMethod]
    public void TheHint_SaysWhatVanillaDoes_WhoIsAffected_WhatIsExempt_AndThatTheTooltipHasNoForestLine()
    {
        var property = typeof(TaomSettings).GetProperty(nameof(TaomSettings.ForestSightPenaltyEnabled))!;
        var text = string.Join("|", property.GetCustomAttributesData().Select(a => a.ToString()));

        StringAssert.Contains(text, "never hides your party from anyone", "vanilla forest hides no one: the target side makes others harder for YOU to see");
        StringAssert.Contains(text, "Only your own party");
        StringAssert.Contains(text, "Keen Sight");
        StringAssert.Contains(text, "inside a settlement");
        StringAssert.Contains(text, "tooltip has no line for the forest");
        Assert.IsFalse(text.Contains('\u2014') || text.Contains('\u2013'), "no em or en dash in player-facing prose");
    }
}
