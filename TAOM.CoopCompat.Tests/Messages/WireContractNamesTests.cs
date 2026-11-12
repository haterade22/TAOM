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
        "TAOM.CoopCompat.Messages.CoopCompatHello: 1=ProtocolVersion, 2=TaomVersion, 3=TaomMvid, 4=CoopCompatMvid, 5=SettingsGlobal, 6=GroupNames, 7=GroupHashes, 8=Conclusive",
        Layout(typeof(CoopCompatHello)));

    [TestMethod]
    public void HelloReply_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatHelloReply: 1=ProtocolVersion, 2=Verdict, 3=DivergentGroups, 4=BuildMismatch",
        Layout(typeof(CoopCompatHelloReply)));

    [TestMethod]
    public void MomentumSnapshot_Layout() => Assert.AreEqual(
        "TAOM.CoopCompat.Messages.CoopCompatMomentumSnapshot: 1=ProtocolVersion, 2=Sequence, 4=Keys, 5=Values",
        Layout(typeof(CoopCompatMomentumSnapshot)));

    [TestMethod]
    public void EveryContractInTheNamespace_IsPinned()
    {
        var names = typeof(CoopCompatHello).Assembly.GetTypes()
            .Where(t => t.Namespace == "TAOM.CoopCompat.Messages" && t.IsDefined(typeof(ProtoContractAttribute), false))
            .Select(t => t.FullName).OrderBy(n => n).ToArray();

        CollectionAssert.AreEqual(new[]
        {
            "TAOM.CoopCompat.Messages.CoopCompatHello",
            "TAOM.CoopCompat.Messages.CoopCompatHelloReply",
            "TAOM.CoopCompat.Messages.CoopCompatMomentumSnapshot",
        }, names);
    }

    [TestMethod]
    public void Contracts_RunTheConstructorAndUseTheRightInterface()
    {
        // The constructor's initializers are what turn an empty repeated field back into an empty array.
        foreach (var t in new[] { typeof(CoopCompatHello), typeof(CoopCompatHelloReply), typeof(CoopCompatMomentumSnapshot) })
        {
            var attr = (ProtoContractAttribute)t.GetCustomAttributes(typeof(ProtoContractAttribute), false).Single();
            Assert.IsFalse(attr.SkipConstructor, t.Name);
        }

        Assert.IsTrue(typeof(ICommand).IsAssignableFrom(typeof(CoopCompatHello)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatHelloReply)));
        Assert.IsTrue(typeof(IEvent).IsAssignableFrom(typeof(CoopCompatMomentumSnapshot)));
    }
}
