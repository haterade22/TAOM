using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EconomyDiagnostics;

namespace TAOM.Tests.Features.EconomyDiagnostics;

/// <summary>
/// Pins the outermost-wins rule for the gold-flow tag.
///
/// It matters because the flows we most want to tell apart share their inner plumbing: a caravan
/// sale and an AI lord loot sale both funnel through `SellItemsAction`, and a villager delivery calls
/// `ChangeGold` directly. Whichever caller is outermost is the one that identifies the flow, so a
/// nested generic tag must not overwrite a specific outer one.
/// </summary>
[TestClass]
public class TownGoldFlowScopeTests
{
    [TestInitialize]
    public void Setup() => TownGoldFlowScope.Reset();

    [TestCleanup]
    public void Cleanup() => TownGoldFlowScope.Reset();

    [TestMethod]
    public void Current_WithNoScopeEntered_IsOther()
    {
        Assert.AreEqual(TownGoldFlow.Other, TownGoldFlowScope.Current);
    }

    [TestMethod]
    public void TryEnter_WhenFree_ClaimsTheTagAndReportsOwnership()
    {
        var owned = TownGoldFlowScope.TryEnter(TownGoldFlow.VillagerDelivery);

        Assert.IsTrue(owned);
        Assert.AreEqual(TownGoldFlow.VillagerDelivery, TownGoldFlowScope.Current);
    }

    /// <summary>
    /// The core rule: a caravan sale (outer) passing through `SellItemsAction` (inner) must stay
    /// labelled as a caravan sale, otherwise every specific flow collapses into the generic bucket.
    /// </summary>
    [TestMethod]
    public void TryEnter_WhenAlreadyHeld_DoesNotOverwriteTheOuterTag()
    {
        TownGoldFlowScope.TryEnter(TownGoldFlow.VillagerDelivery);

        var owned = TownGoldFlowScope.TryEnter(TownGoldFlow.Trade);

        Assert.IsFalse(owned);
        Assert.AreEqual(TownGoldFlow.VillagerDelivery, TownGoldFlowScope.Current);
    }

    /// <summary>
    /// The inner scope exiting must not clear a tag it never owned — otherwise the remainder of the
    /// outer call would be recorded as `Other`.
    /// </summary>
    [TestMethod]
    public void Exit_WithoutOwnership_LeavesTheOuterTagIntact()
    {
        TownGoldFlowScope.TryEnter(TownGoldFlow.VillagerDelivery);
        var innerOwned = TownGoldFlowScope.TryEnter(TownGoldFlow.Trade);

        TownGoldFlowScope.Exit(innerOwned);

        Assert.AreEqual(TownGoldFlow.VillagerDelivery, TownGoldFlowScope.Current);
    }

    [TestMethod]
    public void Exit_WithOwnership_ReleasesTheTag()
    {
        var owned = TownGoldFlowScope.TryEnter(TownGoldFlow.Trade);

        TownGoldFlowScope.Exit(owned);

        Assert.AreEqual(TownGoldFlow.Other, TownGoldFlowScope.Current);
    }

    /// <summary>
    /// A Harmony postfix does not run when the patched method throws. Without a hard reset the tag
    /// would stay stuck after one engine exception and mislabel every subsequent flow.
    /// </summary>
    [TestMethod]
    public void Reset_AfterAnUnreleasedScope_ClearsTheStuckTag()
    {
        TownGoldFlowScope.TryEnter(TownGoldFlow.Trade);

        TownGoldFlowScope.Reset();

        Assert.AreEqual(TownGoldFlow.Other, TownGoldFlowScope.Current);
        Assert.IsTrue(TownGoldFlowScope.TryEnter(TownGoldFlow.DailyMint), "tag must be claimable again");
    }
}
