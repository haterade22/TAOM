using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TAOM.Features.SpecialResources.Hooks;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// #1492: the held upgrades come from the engine's history tuple (source troop, upgrade target, count). Both troops are
/// CharacterObject, so a swap of Item1 and Item2 would compile and re-queue the cheaper source troop's cost.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")] // builds CharacterObject and PartyScreenData
public class PartyScreenHeldUpgradesTests
{
    [TestMethod]
    public void Read_HistoryTuple_TakesTheUpgradeTargetAndTheCount()
    {
        var data = new PartyScreenData();
        data.UpgradedTroopsHistory.Add(new Tuple<CharacterObject, CharacterObject, int>(
            new CharacterObject { StringId = "mordor_orc" },
            new CharacterObject { StringId = "mordor_uruk_captain" },
            3));

        var entries = PartyScreenHeldUpgrades.Read(data);

        Assert.AreEqual(1, entries.Count);
        Assert.AreEqual("mordor_uruk_captain", entries[0].TroopId);
        Assert.AreEqual(3, entries[0].Count);
    }

    [TestMethod]
    public void Read_NoData_ReturnsNothing() =>
        Assert.AreEqual(0, PartyScreenHeldUpgrades.Read((PartyScreenData?)null).Count);
}
