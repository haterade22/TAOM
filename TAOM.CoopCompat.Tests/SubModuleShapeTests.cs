using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// The transport interface the services hold names no Coop type, so a service never needs Coop to
/// compile against or to load (TypeScanGateTests covers every type of the submodule DLL).
/// </summary>
[TestClass]
public class SubModuleShapeTests
{
    private static readonly string[] CoopAssemblies =
    {
        "Common", "GameInterface", "Coop.Core", "LiteNetLib", "Autofac", "protobuf-net", "protobuf-net.Core",
    };

    private static bool NamesCoop(Type type) =>
        CoopAssemblies.Contains(type.Assembly.GetName().Name)
        || (type.IsGenericType && type.GetGenericArguments().Any(NamesCoop));

    [TestMethod]
    public void TransportInterface_NamesNoCoopType_InAnySignature()
    {
        var type = typeof(TAOM.CoopCompat.Transport.ICoopTransport);
        var signatureTypes = type.GetMethods().SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Concat(type.GetProperties().Select(p => p.PropertyType))
            .Concat(type.GetEvents().Select(e => e.EventHandlerType!));

        Assert.IsFalse(signatureTypes.Any(NamesCoop));
        Assert.IsFalse(type.GetInterfaces().Any(NamesCoop));
    }

    [TestMethod]
    public void SubModule_HasATransportField_OfTheInterfaceType()
    {
        var field = typeof(CoopCompatSubModule).GetField("_transport", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field);
        Assert.AreEqual(typeof(TAOM.CoopCompat.Transport.ICoopTransport), field!.FieldType);
    }

    // #1346: a dedicated server reaches only the campaign start; a client must register at the main menu,
    // before it joins. Dropping either call refuses players at join or hands the vote to the host's AI.
    [TestMethod]
    public void SubModule_RegistersDecisionTypes_AtTheMainMenuAndAtTheFirstCampaignStart()
    {
        var text = System.IO.File.ReadAllText(RepoFiles.Path("CoopCompat/CoopCompatSubModule.cs"));

        foreach (var hook in new[] { "void OnBeforeInitialModuleScreenSetAsRoot()", "void StartOnce()" })
        {
            var start = text.IndexOf(hook, StringComparison.Ordinal);
            Assert.IsTrue(start >= 0, hook + " is gone");
            var end = text.IndexOf("\n    }", start, StringComparison.Ordinal);
            StringAssert.Contains(text.Substring(start, end - start), "DecisionTypeRegistration.TryRegister(", hook);
        }
    }

    // Career sync: a client sends one career report per action, at the next frame; without the tick nothing goes out.
    [TestMethod]
    public void SubModule_TicksTheCareerRelay_EveryFrame_AndBuildsIt_WithTheOtherRelays()
    {
        var text = System.IO.File.ReadAllText(RepoFiles.Path("CoopCompat/CoopCompatSubModule.cs"));

        StringAssert.Contains(text, "protected override void OnApplicationTick(float dt) => _careerSync?.Tick();");
        var build = text.IndexOf("private void BuildServices(", StringComparison.Ordinal);
        Assert.IsTrue(build >= 0 && text.IndexOf("new CareerSyncRelayService(", build, StringComparison.Ordinal) > build);
        StringAssert.Contains(text, "_careerSync = null;");
    }

    [TestMethod]
    public void SubModule_StaysUnder150Lines()
    {
        var lines = System.IO.File.ReadAllLines(RepoFiles.Path("CoopCompat/CoopCompatSubModule.cs")).Length;

        Assert.IsTrue(lines < 150, lines + " lines (ADR-002)");
    }
}
