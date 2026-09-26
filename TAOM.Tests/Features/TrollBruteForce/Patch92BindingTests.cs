using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TrollBruteForce.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TrollBruteForce;

/// <summary>
/// Drift guard for Patch92's layout scope: it finds its targets by name, so an engine rename would leave the order
/// preview and the deployment placement at human width with nothing failing. v1.5.3 has three public
/// <c>GetUnitPositionWithIndexAccordingToNewOrder</c> overloads and <c>GetUnitSpawnFrameWithIndex</c>.
/// </summary>
[TestClass]
public class Patch92BindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SimulationScope_FindsEveryPublicLayoutEntryPoint()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var names = Patch92_TrollFormationSpacingSimulation.TargetMethods().Select(m => m.Name).ToList();

        Assert.AreEqual(3, names.Count(n => n == "GetUnitPositionWithIndexAccordingToNewOrder"), string.Join(", ", names));
        Assert.AreEqual(1, names.Count(n => n == "GetUnitSpawnFrameWithIndex"), string.Join(", ", names));
    }
}
