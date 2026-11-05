using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Infrastructure.Dependencies;

// Pins PatchShieldPolicy's agent-tick entry against the installed engine. Patch91 brackets
// Mission.TickAgentsAndTeamsImp for every player; a shield finalizer there could swallow a missing-API
// exception before `tickCompleted = true` (v1.4.8 Mission.cs:3613) and leave the next frame's
// WaitTickCompletion spinning. An engine rename would make the string entry match nothing and bring
// the shield back silently; this test fails instead.
[TestClass]
public class PatchShieldAgentTickExclusionBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AgentTickTarget_IsOnPatchShieldsExclusionList()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var target = AccessTools.Method(typeof(Mission), "TickAgentsAndTeamsImp", new[] { typeof(float), typeof(bool) });

        Assert.IsNotNull(target, "Mission.TickAgentsAndTeamsImp(float, bool) did not resolve on the installed engine");
        Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target.DeclaringType?.FullName, target.Name),
            $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
    }
}
