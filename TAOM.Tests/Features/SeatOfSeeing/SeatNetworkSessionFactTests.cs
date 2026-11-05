using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Client.Features.SeatOfSeeing;
using TAOM.Features.CoopInterop;
using TAOM.Features.SeatOfSeeing.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// The network fact behind the Seat's policy and Marshal's Word: a co-op battle runs its own mission with the engine's session flag false,
/// so the fact is the engine's session or a live co-op session, on a host and on a client alike.
/// </summary>
[TestClass]
public class SeatNetworkSessionFactTests
{
    private static ICoopSessionProvider Coop(bool active)
    {
        var coop = Substitute.For<ICoopSessionProvider>();
        coop.IsSessionActive.Returns(active);
        return coop;
    }

    [DataTestMethod]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    [DataRow(true, true, true)]
    public void InNetworkSession_IsTheEnginesSessionOrALiveCoopSession(bool engine, bool coop, bool expected)
    {
        Assert.AreEqual(expected, SeatMissionClassifier.InNetworkSession(engine, Coop(coop)));
    }

    [TestMethod]
    public void InNetworkSession_AsksTheMembersThatAreTrueOnAHostAndOnAClient_NotTheAuthorityOnes()
    {
        var coop = Coop(true);

        SeatMissionClassifier.InNetworkSession(false, coop);

        _ = coop.Received().IsSessionActive;
        _ = coop.DidNotReceive().IsAuthority;
        _ = coop.DidNotReceive().IsCoopClient;
    }

    [TestMethod]
    public void TheMissionFacts_ReadTheEnginesSessionAndTheCoopSessionThroughThatFunction()
    {
        var source = System.Text.RegularExpressions.Regex.Replace(
            RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatMissionClassifier.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(source, "InNetworkSession(GameNetwork.IsSessionActive, coop), foreignCamera);");
    }

    [TestMethod]
    public void TheClassifier_TakesTheProviderAsAParameter_AndNeverReachesForTheLocator()
    {
        var source = RepoPaths.ReadSource("Client/Features/SeatOfSeeing/SeatMissionClassifier.cs", stripComments: true);

        Assert.IsFalse(source.Contains("IoC.Resolve"), "a static helper takes what it reads");
        Assert.IsFalse(source.Contains("ICoopSessionProvider?"), "every caller passes the resolved provider");
    }

    [DataTestMethod]
    [DataRow("Client/Features/SeatOfSeeing/SeatView.cs")]
    [DataRow("Client/Features/MarshalsWord/MarshalView.cs")]
    public void EachView_ResolvesTheProviderOnceAtStart_AndHoldsTheProviderNotItsAnswer(string path)
    {
        var source = System.Text.RegularExpressions.Regex.Replace(RepoPaths.ReadSource(path, stripComments: true), @"\s+", " ");
        var start = source.Substring(source.IndexOf("public override void OnMissionScreenInitialize()", System.StringComparison.Ordinal));

        Assert.AreEqual(1, System.Text.RegularExpressions.Regex.Matches(source, @"IoC\.Resolve<ICoopSessionProvider>\(\)").Count);
        Assert.IsTrue(start.Contains("var coop = IoC.Resolve<ICoopSessionProvider>();"), "a resolve failure lands in the view's own could-not-start catch");
        Assert.IsFalse(source.Contains("IsSessionActive"), "the provider's answer is read live at each decision");
    }

    [TestMethod]
    public void MarshalsWord_TakesItsNetworkFactFromTheSameFacts_SoACoopBattleKeepsItOff()
    {
        var source = System.Text.RegularExpressions.Regex.Replace(
            RepoPaths.ReadSource("Client/Features/MarshalsWord/MarshalView.cs", stripComments: true), @"\s+", " ");

        StringAssert.Contains(source, "var facts = SeatMissionClassifier.ReadFacts(mission, coop, logger); return SeatMissionPolicy.Decide(facts).Orders && !facts.NetworkSession;");
    }

    [TestMethod]
    public void ACoopBattle_GetsTheCameraAndNoSimulation_ThroughThePolicy()
    {
        var facts = new SeatMissionFacts(SeatMissionKind.Battle, SeatMissionClassifier.InNetworkSession(false, Coop(true)), ForeignCamera: false);

        var permissions = SeatMissionPolicy.Decide(facts);

        Assert.IsTrue(permissions.Camera);
        Assert.IsFalse(permissions.FightOn);
        Assert.IsFalse(permissions.Time);
        Assert.IsFalse(permissions.TakeSoldier);
    }
}
