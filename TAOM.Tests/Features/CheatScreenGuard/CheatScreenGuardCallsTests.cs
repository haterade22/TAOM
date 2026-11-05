using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CheatScreenGuard;
using TAOM.Features.CheatScreenGuard.Hooks;
using TAOM.Features.CheatScreenGuard.Models;

namespace TAOM.Tests.Features.CheatScreenGuard;

/// <summary>The swapped-in read: the real flag first, and vanilla's answer on any doubt.</summary>
[TestClass]
public class CheatScreenGuardCallsTests
{
    [TestCleanup]
    public void Cleanup() => CheatScreenGuardCalls.Initialize(null);

    [TestMethod]
    public void Decide_CheatModeOff_ReturnsFalseAndNeverCallsTheService()
    {
        var service = Substitute.For<ICheatScreenGuardService>();
        CheatScreenGuardCalls.Initialize(service);

        Assert.IsFalse(CheatScreenGuardCalls.Decide(false, CheatScreen.Inventory));

        service.DidNotReceive().ShowCheatList(Arg.Any<CheatScreen>());
    }

    [TestMethod]
    public void Decide_NoService_ReturnsTheVanillaAnswer()
        => Assert.IsTrue(CheatScreenGuardCalls.Decide(true, CheatScreen.PartyScreen));

    [TestMethod]
    public void Decide_ServiceSaysTrue_ReturnsTrue()
    {
        var service = Substitute.For<ICheatScreenGuardService>();
        service.ShowCheatList(CheatScreen.Inventory).Returns(true);
        CheatScreenGuardCalls.Initialize(service);

        Assert.IsTrue(CheatScreenGuardCalls.Decide(true, CheatScreen.Inventory));
    }

    [TestMethod]
    public void Decide_ServiceSaysFalse_ReturnsFalse()
    {
        var service = Substitute.For<ICheatScreenGuardService>();
        service.ShowCheatList(CheatScreen.PartyScreen).Returns(false);
        CheatScreenGuardCalls.Initialize(service);

        Assert.IsFalse(CheatScreenGuardCalls.Decide(true, CheatScreen.PartyScreen));
    }

    [TestMethod]
    public void Decide_ServiceThrows_ReturnsTheVanillaAnswer()
    {
        var service = Substitute.For<ICheatScreenGuardService>();
        service.ShowCheatList(Arg.Any<CheatScreen>()).Returns(_ => throw new InvalidOperationException("boom"));
        CheatScreenGuardCalls.Initialize(service);

        Assert.IsTrue(CheatScreenGuardCalls.Decide(true, CheatScreen.Inventory));
    }
}
