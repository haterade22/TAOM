using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;
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

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EveryPatch92Target_IsOnPatchShieldsHotMethodList()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // PatchShield skips these by declaring type and name (PatchShieldPolicy.ExcludedTargetMethods). A target
        // added to Patch92, or renamed by an engine bump, and missing from that list would pay the per-call
        // __originalMethod finalizer on a per-unit hot path (the #331 cost), with every other test still green.
        MethodInfo getter = AccessTools.PropertyGetter(typeof(Formation), nameof(Formation.UnitDiameter));
        Assert.IsNotNull(getter, "Formation.UnitDiameter getter");

        foreach (MethodBase m in Patch92_TrollFormationSpacingSimulation.TargetMethods().Append(getter))
        {
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(m.DeclaringType?.FullName, m.Name),
                $"{m.DeclaringType?.FullName}.{m.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TeamField_StillPublicOnFormation()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        // The postfix reads __instance.Team == null with no try/catch (a field read cannot throw), so a
        // rename here would fail this test rather than throw inside the getter for every unit every frame.
        var field = AccessTools.Field(typeof(Formation), "Team");

        Assert.IsNotNull(field, "Formation.Team must still exist under that name");
        Assert.IsTrue(field.IsPublic, "Formation.Team must stay public");
    }
}
