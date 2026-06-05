using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Spider.Hooks;

namespace TAOM.Tests.Features.Spider;

/// <summary>
/// Binding-verification for the two PRIVATE Mission methods the detached spider spawn reflects
/// (Mission.CreateAgent, Mission.BuildAgent). If a Bannerlord engine bump renames/re-signs either, this
/// fails fast in CI instead of the feature silently fail-opening to the humanoid anchor in-game. See
/// docs/reviews/rca-spider-troop-2026-06-04.md + the /verify-bindings skill.
/// </summary>
[TestClass]
public class SpiderDetachedAgentSpawnerBindingTests
{
    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ReflectedMissionMethods_Resolve_AgainstInstalledEngine()
    {
        Assert.IsTrue(
            SpiderDetachedAgentSpawner.BindingsResolved,
            "Mission.CreateAgent and/or Mission.BuildAgent did not bind via AccessTools against the " +
            "installed engine — the detached spider spawn would fail-open to the humanoid anchor. " +
            "Re-verify the private signatures in v1.4.5 and update SpiderDetachedAgentSpawner.");
    }
}
