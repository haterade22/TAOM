using System.Linq;
using Common.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ProtoBuf;
using TAOM.CoopCompat.Messages;

namespace TAOM.CoopCompat.Tests.Messages;

/// <summary>A full type name is the wire id and the field numbers are the layout: renaming or moving either breaks every peer on another build.</summary>
[TestClass]
public class WireContractNamesTests
{
    private static string Layout(System.Type type) =>
        type.FullName + ": " + string.Join(", ", type.GetProperties()
            .Select(p => (p, a: p.GetCustomAttributes(typeof(ProtoMemberAttribute), false).Cast<ProtoMemberAttribute>().SingleOrDefault()))
            .Where(x => x.a != null)
            .OrderBy(x => x.a!.Tag)
            .Select(x => x.a!.Tag + "=" + x.p.Name));

    [TestMethod]
    public void Hello_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatHello: 1=ProtocolVersion, 2=TaomVersion, 3=TaomMvid, 4=CoopCompatMvid, 5=SettingsGlobal, 6=GroupNames, 7=GroupHashes, 8=Conclusive, 9=ModuleOrder",
        Layout(typeof(CoopCompatHello)));

    [TestMethod]
    public void HelloReply_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatHelloReply: 1=ProtocolVersion, 2=Verdict, 3=DivergentGroups, 4=BuildMismatch, 5=LoadOrderDiffers, 6=LoadOrderHostFirst, 7=LoadOrderHostSecond",
        Layout(typeof(CoopCompatHelloReply)));

    [TestMethod]
    public void MomentumSnapshot_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatMomentumSnapshot: 1=ProtocolVersion, 2=Sequence, 4=Keys, 5=Values",
        Layout(typeof(CoopCompatMomentumSnapshot)));

    [TestMethod]
    public void LdRunEnded_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatLdRunEnded: 1=ProtocolVersion, 2=SessionId, 3=Epoch, 4=Reason, 5=Wave, 6=WavesCleared, 7=Kills",
        Layout(typeof(CoopCompatLdRunEnded)));

    [TestMethod]
    public void TradeRequest_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatTradeRequest: 1=ProtocolVersion, 2=RequestId, 3=Action, 4=SettlementId, 5=Subject, 6=Modifier, 7=Target, 8=Quantity",
        Layout(typeof(CoopCompatTradeRequest)));

    [TestMethod]
    public void TradeReply_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatTradeReply: 1=ProtocolVersion, 2=RequestId, 3=Action, 4=Refusal, 5=Status, 6=Quantity, 7=TotalCost, 8=ResourceName, 9=ResourceId, 10=HasBalance, 11=Balance, 12=HasArmouryLevel, 13=ArmouryLevel, "
        + "14=HasLadder, 15=LadderSlot, 16=LadderReady, 17=LadderCanClaim, 18=LadderLordLevel, 19=LadderMaterialId, 20=LadderNeeded, "
        + "21=LadderCarried, 22=LadderChoices",
        Layout(typeof(CoopCompatTradeReply)));

    [TestMethod]
    public void CareerReport_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatCareerReport: 1=ProtocolVersion, 2=Sequence, 3=JoinReport, 4=HeroId, 5=CareerId, 6=Choices, 7=Tiers, 8=Flags",
        Layout(typeof(CoopCompatCareerReport)));

    [TestMethod]
    public void CareerUpdate_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatCareerUpdate: 1=ProtocolVersion, 2=Sequence, 3=HeroId, 4=CareerId, 5=Choices, 6=Tiers, 7=Flags, 8=Refused, 9=Known",
        Layout(typeof(CoopCompatCareerUpdate)));

    [TestMethod]
    public void EveryContractInTheNamespace_IsPinned()
    {
        var names = typeof(CoopCompatHello).Assembly.GetTypes()
            .Where(t => t.Namespace == "TAOM.CoopCompat.Messages" && t.IsDefined(typeof(ProtoContractAttribute), false))
            .Select(t => t.FullName).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "TAOM.CoopCompat.Messages.CoopCompatCareerReport",
            "TAOM.CoopCompat.Messages.CoopCompatCareerUpdate",
            "TAOM.CoopCompat.Messages.CoopCompatHello",
            "TAOM.CoopCompat.Messages.CoopCompatHelloReply",
            "TAOM.CoopCompat.Messages.CoopCompatLdRunEnded",
            "TAOM.CoopCompat.Messages.CoopCompatMomentumSnapshot",
            "TAOM.CoopCompat.Messages.CoopCompatTradeReply",
            "TAOM.CoopCompat.Messages.CoopCompatTradeRequest",
        }, names);
    }

    [TestMethod]
    public void Contracts_RunTheConstructorAndUseTheRightInterface()
    {
        // The constructor's initializers are what turn an empty repeated field back into an empty array.
        foreach (var t in new[] { typeof(CoopCompatHello), typeof(CoopCompatHelloReply), typeof(CoopCompatMomentumSnapshot),
                     typeof(CoopCompatLdRunEnded), typeof(CoopCompatTradeRequest), typeof(CoopCompatTradeReply),
                     typeof(CoopCompatCareerReport), typeof(CoopCompatCareerUpdate) })
        {
            var attr = (ProtoContractAttribute)t.GetCustomAttributes(typeof(ProtoContractAttribute), false).Single();
            Assert.IsFalse(attr.SkipConstructor, t.Name);
        }

        Assert.IsTrue(typeof(ICommand).IsAssignableFrom(typeof(CoopCompatHello)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatHelloReply)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatMomentumSnapshot)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatLdRunEnded)));
        Assert.IsTrue(typeof(ICommand).IsAssignableFrom(typeof(CoopCompatTradeRequest)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatTradeReply)));
        Assert.IsTrue(typeof(ICommand).IsAssignableFrom(typeof(CoopCompatCareerReport)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatCareerUpdate)));
    }
}
