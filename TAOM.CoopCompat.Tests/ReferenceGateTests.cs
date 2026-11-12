using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TAOM.CoopCompat.Tests;

/// <summary>
/// Both module DLLs may name only the framework, the engine, TAOM, each other and the public Coop
/// assemblies, so no private dedicated-server assembly can creep into their references.
/// </summary>
[TestClass]
public class ReferenceGateTests
{
    private static readonly string[] AllowedExact =
    {
        "mscorlib", "TAOM", "TAOM.CoopCompat.Coop", "Autofac", "Common", "GameInterface", "Coop.Core", "protobuf-net", "protobuf-net.Core", "LiteNetLib",
    };

    private static bool Allowed(string name) =>
        AllowedExact.Contains(name) || name.StartsWith("System") || name.StartsWith("TaleWorlds.");

    private static readonly System.Reflection.Assembly[] ModuleAssemblies =
    {
        typeof(CoopCompatSubModule).Assembly, typeof(TAOM.CoopCompat.Transport.CoopTransport).Assembly,
    };

    [TestMethod]
    public void BuiltAssemblies_ReferenceOnlyAllowedAssemblies()
    {
        var referenced = ModuleAssemblies.SelectMany(a => a.GetReferencedAssemblies())
            .Select(a => a.Name!).Distinct().OrderBy(n => n).ToList();

        var stray = referenced.Where(n => !Allowed(n)).ToList();

        CollectionAssert.AreEqual(new string[0], stray, "Unexpected assembly references: " + string.Join(", ", stray));
    }

    [TestMethod]
    public void BuiltAssemblies_NeverReferenceDedicatedServerAssemblies()
    {
        var referenced = ModuleAssemblies.SelectMany(a => a.GetReferencedAssemblies()).Select(a => a.Name!);

        Assert.IsFalse(referenced.Any(n => n.StartsWith("DedicatedServer")));
    }
}
