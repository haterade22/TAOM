using System.Reflection;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Hooks;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// The battle-exit postfix only ever turns a true vanilla answer into false, and it fails open: a fault
/// leaves vanilla's answer in place and is logged once (lessons/harmony-il.md). No test calls
/// IoC.Configure, so each test installs its own container in IoC's private field and puts the old one
/// back; the patch's service cache and log-once latch are reset too. The postfix runs no engine code,
/// so no game assemblies are needed.
/// </summary>
[TestClass]
public class MapEventHelperCanMainPartyLeaveBattlePatchTests
{
    private static readonly FieldInfo? ContainerField =
        typeof(TAOM.IoC).GetField("_container", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? ServiceField =
        typeof(MapEventHelperCanMainPartyLeaveBattlePatch).GetField("_menuService", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly FieldInfo? FaultField =
        typeof(MapEventHelperCanMainPartyLeaveBattlePatch).GetField("_faultLogged", BindingFlags.NonPublic | BindingFlags.Static);

    private object? _savedContainer;

    [TestInitialize]
    public void Setup()
    {
        Assert.IsNotNull(ContainerField, "IoC._container moved: update this test");
        Assert.IsNotNull(ServiceField, "the patch's _menuService cache moved: update this test");
        Assert.IsNotNull(FaultField, "the patch's _faultLogged latch moved: update this test");
        _savedContainer = ContainerField!.GetValue(null);
        ServiceField!.SetValue(null, null);
        FaultField!.SetValue(null, false);
    }

    [TestCleanup]
    public void Cleanup()
    {
        ContainerField?.SetValue(null, _savedContainer);
        ServiceField?.SetValue(null, null);
        FaultField?.SetValue(null, false);
    }

    [TestMethod]
    public void Postfix_VanillaFalse_StaysFalseAndResolvesNothing()
    {
        // A container that cannot resolve anything: any resolve attempt would log, so the logger proves it.
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger);
        ContainerField!.SetValue(null, container);
        var result = false;

        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);

        Assert.IsFalse(result);
        Assert.IsNull(ServiceField!.GetValue(null), "vanilla false must return before the service is resolved");
        logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void Postfix_ServiceSaysBlock_TurnsTrueIntoFalse()
    {
        var service = Substitute.For<IEnlistmentMenuService>();
        service.ShouldBlockBattleExit().Returns(true);
        using var container = new Container();
        container.RegisterInstance(service);
        ContainerField!.SetValue(null, container);
        var result = true;

        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Postfix_ServiceAllows_KeepsTrue()
    {
        var service = Substitute.For<IEnlistmentMenuService>();
        service.ShouldBlockBattleExit().Returns(false);
        using var container = new Container();
        container.RegisterInstance(service);
        ContainerField!.SetValue(null, container);
        var result = true;

        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);

        Assert.IsTrue(result);
        service.Received(1).ShouldBlockBattleExit();
    }

    [TestMethod]
    public void Postfix_ServiceCannotBeResolved_KeepsTrueAndLogsOnce()
    {
        var logger = Substitute.For<IModLogger>();
        using var container = new Container();
        container.RegisterInstance(logger); // the menu service is not registered, so resolving it throws
        ContainerField!.SetValue(null, container);
        var result = true;

        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);
        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);
        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);

        Assert.IsTrue(result, "vanilla's answer stands");
        logger.Received(1).LogError(NSubstitute.Arg.Is<string>(s => s.Contains("CanMainPartyLeaveBattleCommonCondition")));
    }

    [TestMethod]
    public void Postfix_NoContainerAtAll_KeepsTrueWithoutThrowing()
    {
        ContainerField!.SetValue(null, null);
        var result = true;

        MapEventHelperCanMainPartyLeaveBattlePatch.Postfix(ref result);

        Assert.IsTrue(result, "even the log line's own failure must not escape the patch");
    }
}
