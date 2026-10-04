using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// The one line the map profiler's speed class reads the engine through. Every speed test fakes
/// <see cref="ITimeControlAdapter"/> and sets <c>SimplifiedTimeControlMode</c> itself, so pointing the adapter back at
/// the raw <c>Campaign.TimeControlMode</c> (the bug FOR-MIKE 16r fixed: a Stoppable mode while the main party waits
/// advances no campaign time, yet read as Play or FF) would compile and keep every such test green. The getter is
/// pinned by its IL instead, the way the repo pins other engine-facing bodies.
/// </summary>
[TestClass]
public class TimeControlAdapterBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SimplifiedTimeControlMode_Getter_CallsTheEnginesSimplifiedModeNotTheRawOne()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var getter = typeof(TimeControlAdapter).GetProperty(nameof(ITimeControlAdapter.SimplifiedTimeControlMode))?.GetGetMethod();
        Assert.IsNotNull(getter, "TimeControlAdapter.SimplifiedTimeControlMode has no getter.");

        var called = PatchProcessor.GetOriginalInstructions(getter!).Select(ci => ci.operand).OfType<MethodBase>().ToList();

        Assert.IsTrue(called.Any(m => m.DeclaringType == typeof(Campaign) && m.Name == nameof(Campaign.GetSimplifiedTimeControlMode)),
            "The getter no longer calls Campaign.GetSimplifiedTimeControlMode.");
        Assert.IsFalse(called.Any(m => m.DeclaringType == typeof(Campaign) && m.Name == "get_" + nameof(Campaign.TimeControlMode)),
            "The getter reads the raw Campaign.TimeControlMode, so a waiting Stoppable mode would read as Play or FF.");
    }
}
