using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;
using TAOM.Features.CoopInterop;
using TAOM.Features.Diplomacy;
using TAOM.Features.LotrIssues;
using TAOM.Features.NamedCompanions;
using TAOM.Features.NazgulFamily;
using TaleWorlds.CampaignSystem;

namespace TAOM.Tests.Features.CoopInterop;

/// <summary>
/// #1086: a co-op join is a save load, so a client runs every load and launch handler. These behaviors write shared
/// world state there (heroes placed, banners set, family links cut, issues cancelled or added, an alliance formed),
/// which is the server's work. Most tests run the handler as a client and check that the world-writing call never
/// happens, then with authority that it does, so the gate and not a missing campaign is what stops it. The issue
/// sweep needs a live campaign to reach its write, so its test counts calls into the sweep instead.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]   // constructs CampaignBehaviorBase descendants
public class ClientWorldWriteGateTests
{
    private static ICoopSessionProvider Coop(bool isAuthority)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsAuthority.Returns(isAuthority);
        coop.ShouldDeferToHost.Returns(!isAuthority);
        return coop;
    }

    private static object? Invoke(object behavior, string handler, params object?[] args) =>
        behavior.GetType().GetMethod(handler, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(behavior, args);

    [DataTestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 1)]
    public void NamedCompanions_PlacedOnLoadOnlyWithAuthority(bool isAuthority, int calls)
    {
        var service = Substitute.For<INamedCompanionService>();

        Invoke(new NamedCompanionBehavior(service, Coop(isAuthority)), "OnGameLoaded", new object?[] { null });

        service.Received(calls).EnsureCompanionsPlaced();
    }

    [DataTestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 1)]
    public void Banners_InjectedOnLoadOnlyWithAuthority(bool isAuthority, int calls)
    {
        var service = Substitute.For<IBannerInjectionService>();
        var behavior = new BannerInjectionBehavior(service, Substitute.For<IBannerExclusionService>(), Coop(isAuthority));

        Invoke(behavior, "OnGameLoaded", new object?[] { null });

        service.Received(calls).InjectBanners();
    }

    [TestMethod]
    public void NazgulFamily_OnACoopClient_TouchesNoHero()
    {
        var registry = Substitute.For<INazgulRegistry>();

        // With authority the handler reads Hero.AllAliveHeroes, which throws with no campaign in the test host.
        Invoke(new NazgulFamilyBehavior(registry, Coop(false), Substitute.For<IModLogger>()), "OnSessionLaunched",
            new object?[] { null });

        registry.DidNotReceiveWithAnyArgs().IsWraith(default!);
    }

    [TestMethod]
    public void NazgulFamily_WithAuthority_ReadsTheHeroes()
    {
        var behavior = new NazgulFamilyBehavior(Substitute.For<INazgulRegistry>(), Coop(true), Substitute.For<IModLogger>());

        Assert.ThrowsException<TargetInvocationException>(() => Invoke(behavior, "OnSessionLaunched", new object?[] { null }));
    }

    [DataTestMethod]
    [DataRow(false, 0)]
    [DataRow(true, 1)]
    public void LotrIssues_NewIssueDataOnlyWithAuthority(bool isAuthority, int calls)
    {
        var service = Substitute.For<ILotrIssueService>();
        var behavior = new LotrIssuesCampaignBehavior(service, Coop(isAuthority), Substitute.For<IModLogger>());
        var hero = (Hero)FormatterServices.GetUninitializedObject(typeof(Hero));

        Invoke(behavior, "OnCheckForIssue", hero);

        service.ReceivedWithAnyArgs(calls).GetEligibleIssues(default!);
    }

    [TestMethod]
    public void LotrIssues_LoadSweep_RunsOnlyWithAuthority()
    {
        // With no campaign the sweep finds no issue manager either way, so the test counts calls into it.
        var client = new SweepCountingIssues(Coop(false));
        var server = new SweepCountingIssues(Coop(true));

        // The handler is private to the base type, so reflection must ask the base type for it.
        var onGameLoaded = typeof(LotrIssuesCampaignBehavior).GetMethod("OnGameLoaded", BindingFlags.Instance | BindingFlags.NonPublic)!;
        onGameLoaded.Invoke(client, new object?[] { null });
        onGameLoaded.Invoke(server, new object?[] { null });

        Assert.AreEqual(0, client.Sweeps);
        Assert.AreEqual(1, server.Sweeps);
    }

    private sealed class SweepCountingIssues : LotrIssuesCampaignBehavior
    {
        public int Sweeps;

        public SweepCountingIssues(ICoopSessionProvider coop)
            : base(Substitute.For<ILotrIssueService>(), coop, Substitute.For<IModLogger>()) { }

        internal override void SweepLingeringVanillaIssues() => Sweeps++;
    }

    [TestMethod]
    public void AllianceProposal_ACoopGuestNeverSeesTheLine()
    {
        var service = Substitute.For<IDiplomacyService>();
        var behavior = new PlayerAllianceProposalBehavior(service, Coop(false), Substitute.For<IModLogger>());

        Assert.IsFalse(behavior.DialogCondition_CanProposeAlliance());
        service.DidNotReceiveWithAnyArgs().CanPlayerProposeAlliance(default!, default!);
    }
}
