using System.Linq;
using System.Text.RegularExpressions;
using DryIoc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.CoopInterop;
using TAOM.Features.MissionPerf.AnimMemory;
using TAOM.Features.MissionPerf.AnimMemory.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MissionPerf.AnimMemory;

/// <summary>
/// Wiring guard for the animation clip memory probe: the module list adds the mission behaviour, the
/// probe is one per process, the toggle defaults on and stays out of the co-op fingerprint, and no
/// file on the path writes native memory.
/// </summary>
[TestClass]
public class AnimMemoryProbeWiringTests
{
    [TestMethod]
    public void FeatureModules_ListTheAnimMemoryProbeModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<AnimMemoryProbeModule>().Count(),
            "AnimMemoryProbeModule must be listed exactly once in Main/Composition/FeatureModules.cs.");
    }

    [TestMethod]
    public void Module_DeclaresTheMissionBehavior()
    {
        var decls = new AnimMemoryProbeModule().MissionBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(AnimMemoryProbeMissionBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void Module_RegistersTheProbeAsASingleton()
    {
        var container = new Container();
        container.RegisterInstance(Substitute.For<IModLogger>());
        new AnimMemoryProbeModule().RegisterServices(container);

        var first = container.Resolve<IAnimClipMemoryProbe>();

        Assert.AreSame(first, container.Resolve<IAnimClipMemoryProbe>());
        Assert.AreSame(first, container.Resolve<IAnimClipMemoryProbe>());
    }

    [TestMethod]
    public void Behavior_EndsTheSessionOnRemoveBehaviorToo()
    {
        // A teardown that never calls Mission.EndMission skips EndMissionInternal, so OnEndMission
        // does not run; OnMissionStateFinalize still removes every behavior (Mission.cs:2228, :4716),
        // so the summary must also be written from OnRemoveBehavior. This pins the shape of the two
        // overrides from their source text, and it runs on hosted CI, where the game-bound tests cannot;
        // it still passes with the line inside EndSession() that writes the summary deleted. The effect
        // (one summary, none repeated, no later sampling) is proven by AnimMemoryProbeMissionBehaviorTests.
        var method = typeof(AnimMemoryProbeMissionBehavior).GetMethod("OnRemoveBehavior",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
            | System.Reflection.BindingFlags.DeclaredOnly);

        Assert.IsNotNull(method, "AnimMemoryProbeMissionBehavior must override OnRemoveBehavior.");
        var source = RepoPaths.ReadSource(
            "Main/Features/MissionPerf/AnimMemory/Hooks/AnimMemoryProbeMissionBehavior.cs", stripComments: true);
        Assert.IsTrue(Regex.IsMatch(source,
                @"public\s+override\s+void\s+OnRemoveBehavior\s*\(\s*\)\s*\{\s*EndSession\s*\(\s*\)\s*;\s*base\.OnRemoveBehavior\s*\(\s*\)\s*;\s*\}"),
            "OnRemoveBehavior must call EndSession() and then base.OnRemoveBehavior(), and nothing else.");
        StringAssert.Contains(source, "protected override void OnEndMission() => EndSession();",
            "OnEndMission must end the session too.");
    }

    [TestMethod]
    public void Setting_DefaultsOn()
    {
        Assert.IsTrue(new BattleLoadDiagnosticsSettings().EnableAnimMemoryProbe);
    }

    [TestMethod]
    public void Setting_IsInstrumentation()
    {
        Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(
            typeof(BattleLoadDiagnosticsSettings).GetProperty(nameof(BattleLoadDiagnosticsSettings.EnableAnimMemoryProbe))!));
    }

    [TestMethod]
    public void Behavior_MakesNoNativeWrite()
    {
        foreach (var path in new[]
                 {
                     "Main/Features/MissionPerf/AnimMemory/Hooks/AnimMemoryProbeMissionBehavior.cs",
                     "Main/Features/MissionPerf/AnimMemory/AnimMemoryProbe.cs",
                     "Main/Adapters/NativeModuleMemoryAdapter.cs",
                 })
        {
            var source = RepoPaths.ReadSource(path, stripComments: true);
            foreach (var banned in new[] { "Marshal.Write", "VirtualProtect", "WriteProcessMemory", "unsafe" })
                Assert.IsFalse(source.Contains(banned), $"{path} must not contain {banned}: the probe only reads.");
            // Marshal.Copy writes native memory when its array comes first; only the IntPtr-source
            // overload (native to managed) may appear.
            foreach (Match copy in Regex.Matches(source, @"Marshal\.Copy\s*\(\s*([^,]*)"))
                Assert.IsTrue(copy.Groups[1].Value.StartsWith("new IntPtr("),
                    $"{path}: Marshal.Copy must read from new IntPtr(...), never write to native memory: {copy.Value}");
        }
    }
}
