using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.TournamentRewards;
using TAOM.Features.TournamentRewards.Hooks;

namespace TAOM.Tests.Features.TournamentRewards;

/// <summary>
/// The bet postfix keeps the bet UI working whatever goes wrong: a fault leaves vanilla's cap in place, and it is
/// logged (lessons/harmony-il.md: a catch in a patch that exists so the game keeps running needs a log line), once,
/// because the tournament UI reads the cap repeatedly. No test calls IoC.Configure, so each test installs its own
/// container in IoC's private field and puts the old one back; the patch's service cache and latch are reset too.
/// </summary>
[TestClass]
public class Patch96TournamentMaxBetTests
{
    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? ServiceField =
        typeof(Patch96_TournamentMaxBet).GetField("_service", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? WarnedField =
        typeof(Patch96_TournamentMaxBet).GetField("_warned", BindingFlags.NonPublic | BindingFlags.Static);

    private object? _savedContainer;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        Assert.IsNotNull(ServiceField, "Patch96_TournamentMaxBet._service moved: update this test");
        Assert.IsNotNull(WarnedField, "Patch96_TournamentMaxBet._warned (the log-once latch) is missing");
        _savedContainer = ContainerField!.GetValue(null);
        ServiceField!.SetValue(null, null);
        WarnedField!.SetValue(null, false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        ServiceField?.SetValue(null, null);
        WarnedField?.SetValue(null, false);
    }

    [TestMethod]
    public void Postfix_ServiceResolves_AppliesTheConfiguredCap()
    {
        var settings = Substitute.For<ITournamentRewardsSettingsProvider>();
        settings.MaxBetPerRound.Returns(500);
        using var container = new Container();
        container.RegisterInstance(new TournamentRewardsService(
            settings, Substitute.For<ITournamentRewardsConfigProvider>(), Substitute.For<IHeroSkillXpAdapter>()));
        ContainerField!.SetValue(null, container);
        var result = 300; // vanilla with Deep Pockets

        Patch96_TournamentMaxBet.Postfix(ref result);

        Assert.AreEqual(1000, result, "the cap of 500 keeps the perk's doubling");
    }

    [TestMethod]
    public void Postfix_ServiceCannotBeResolved_KeepsVanillasCapAndWarnsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger); // the rewards service is not registered, so resolving it throws
        ContainerField!.SetValue(null, container);
        var result = 150;

        Patch96_TournamentMaxBet.Postfix(ref result);
        Patch96_TournamentMaxBet.Postfix(ref result);
        Patch96_TournamentMaxBet.Postfix(ref result);

        Assert.AreEqual(150, result, "vanilla's cap stands");
        logger.Received(1).LogWarning(NSubstitute.Arg.Is<string>(s => s.Contains("max-bet")));
    }

    [TestMethod]
    public void Postfix_NoContainerAtAll_KeepsVanillasCapWithoutThrowing()
    {
        ContainerField!.SetValue(null, null);
        var result = 150;

        Patch96_TournamentMaxBet.Postfix(ref result);

        Assert.AreEqual(150, result, "even the log line's own failure must not escape the patch");
    }
}
