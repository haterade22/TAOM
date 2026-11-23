using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.Diplomacy.Models;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.Diplomacy;

/// <summary>
/// The permanent alliances a story ended (#1105 slice 5, docs/features/diplomacy.md "Broken permanent alliances"): saved under one
/// additive key, restored before the session-launch enforcement, and cleared by a new campaign.
/// </summary>
[TestClass]
public class DiplomacyBehaviorPersistenceTests
{
    private IAllianceAdapter _alliance = null!;

    private DiplomacyService NewService()
    {
        var provider = Substitute.For<IDiplomacyConfigProvider>();
        provider.LoadConfig().Returns(new DiplomacyConfig
        {
            Relationships = new List<KingdomRelationship>
            {
                new() { KingdomA = "isengard", KingdomB = "empire_s", Tier = AllianceTier.Permanent },
                new() { KingdomA = "erebor", KingdomB = "sturgia", Tier = AllianceTier.Permanent },
            },
        });
        _alliance = Substitute.For<IAllianceAdapter>();
        return new DiplomacyService(provider, _alliance, Substitute.For<IAlignmentService>(), Substitute.For<IModLogger>());
    }

    private static DiplomacyBehavior Behavior(DiplomacyService service) =>
        new DiplomacyBehavior(service, Substitute.For<IModLogger>(), Substitute.For<ICoopSessionProvider>());

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void SyncData_SaveThenLoad_RestoresTheBrokenPairBeforeEnforcementRuns()
    {
        var before = NewService();
        var saving = new EngineDataStore { Loading = false };
        var firstBehavior = Behavior(before);
        before.EndPermanentAlliance("isengard", "empire_s", "test");
        firstBehavior.SyncData(saving);
        Assert.IsTrue(saving.Has("_taom_brokenAlliances"));

        var after = NewService();
        var loading = new EngineDataStore { Loading = true };
        loading.Seed("_taom_brokenAlliances", saving.Get<List<string>>("_taom_brokenAlliances"));
        var behavior = Behavior(after);
        behavior.SyncData(loading);
        behavior.OnSessionLaunched();

        Assert.IsTrue(after.IsBrokenAlliance("empire_s", "isengard"));
        _alliance.DidNotReceive().StartAlliance("isengard", "empire_s");
        _alliance.Received().StartAlliance("erebor", "sturgia");
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void SyncData_ASaveWithoutTheKey_LoadsAnEmptySet()
    {
        var service = NewService();
        var behavior = Behavior(service);

        behavior.SyncData(new EngineDataStore { Loading = true });

        Assert.AreEqual(0, service.BrokenAlliancesForSave().Count);
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void SyncData_Saving_WritesTheSortedPairs()
    {
        var service = NewService();
        var behavior = Behavior(service);
        service.EndPermanentAlliance("isengard", "empire_s", "test");
        var store = new EngineDataStore { Loading = false };

        behavior.SyncData(store);

        CollectionAssert.AreEqual(new[] { "empire_s|isengard" }, store.Get<List<string>>("_taom_brokenAlliances").ToArray());
    }

    [TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase
    [TestMethod]
    public void Constructor_ANewCampaign_ClearsThePairsOfTheCampaignBefore()
    {
        var service = NewService();
        service.EndPermanentAlliance("isengard", "empire_s", "test");

        Behavior(service);

        Assert.IsFalse(service.IsBrokenAlliance("isengard", "empire_s"));
    }
}
