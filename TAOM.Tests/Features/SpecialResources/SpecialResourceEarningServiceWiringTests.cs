using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// SpecialResourceEarningService is registered Transient, so every Resolve builds a new instance. One instance
/// per campaign is the intended wiring: SubModule resolves it once, inside the method that builds the
/// behavior, gives that instance to the behavior and to Patch144, and nothing else resolves a second copy.
/// </summary>
[TestClass]
public class SpecialResourceEarningServiceWiringTests
{
    private const string Resolve = "Resolve<SpecialResourceEarningService>(";

    [TestMethod]
    public void EarningService_IsResolvedOnceInSubModule_AndNowhereElse()
    {
        var holders = RepoPaths.CSharpFiles()
            .Where(path => RepoPaths.StripComments(RepoPaths.Text(path)).Contains(Resolve))
            .Select(path => Path.GetFileName(path))
            .ToList();

        CollectionAssert.AreEqual(new[] { "SubModule.cs" }, holders, "only SubModule resolves the earning service");

        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        Assert.AreEqual(1, subModule.Split(new[] { Resolve }, StringSplitOptions.None).Length - 1,
            "SubModule resolves it once per campaign start");

        var method = subModule.IndexOf("private static void RegisterSpecialResourcesAndCareers(", StringComparison.Ordinal);
        var resolve = subModule.IndexOf(Resolve, StringComparison.Ordinal);
        var behavior = subModule.IndexOf("new SpecialResourcesBehavior(", StringComparison.Ordinal);
        var patchInit = subModule.IndexOf("CoopUpgradeChargePatch.Initialize(", StringComparison.Ordinal);
        var nextMethod = subModule.IndexOf("private static void ", method + 1, StringComparison.Ordinal);
        Assert.IsTrue(method >= 0 && method < resolve && resolve < behavior && behavior < patchInit
                      && (nextMethod < 0 || patchInit < nextMethod),
            "RegisterSpecialResourcesAndCareers resolves it once, then gives that instance to the behavior and to Patch144");
    }
}
