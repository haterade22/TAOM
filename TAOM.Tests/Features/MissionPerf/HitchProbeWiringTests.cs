using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MissionPerf;
using TAOM.Features.MissionPerf.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MissionPerf;

/// <summary>
/// Source and attribute pins for the Patch98 wiring: the frame boundary moved from Patch97's OnPreTick prefix
/// into Patch98's, ahead of the probe's own pre-tick bracket; the profiler installer hands over to the probe
/// installer once; and every Patch98 bracket encloses every other patch on its method (prefix first,
/// finalizer last) and pairs its call through Harmony's <c>__state</c>. None of this runs without the game, so it
/// is pinned here.
/// </summary>
[TestClass]
public class HitchProbeWiringTests
{
    private const string HooksDir = "Main/Features/MissionPerf/Hooks/";

    private static int Count(string text, string needle) => Regex.Matches(text, Regex.Escape(needle)).Count;

    [TestMethod]
    public void Patch98_OnPreTickPrefix_CallsTheFrameBoundaryBeforeTheProbe()
    {
        var src = RepoPaths.ReadSource(HooksDir + "Patch98_HitchProbe.cs", stripComments: true);

        Assert.AreEqual(1, Count(src, "MissionTickProfilerHooks.OnFrameBoundary()"));
        var boundary = src.IndexOf("MissionTickProfilerHooks.OnFrameBoundary()");
        var probe = src.IndexOf("HitchProbeHooks.OnPreTickEnter(");
        Assert.IsTrue(probe > boundary, "The frame boundary closes the previous frame before the probe opens the next.");
    }

    [TestMethod]
    public void Patch98_WaitTickCompletionClass_IsDeclaredBeforeTheOnPreTickClass()
    {
        // Assembly order follows declaration order within the file; see the note in Patch98_HitchProbe.cs.
        var src = RepoPaths.ReadSource(HooksDir + "Patch98_HitchProbe.cs", stripComments: true);
        var wait = src.IndexOf("class Mission_WaitTickCompletion_HitchProbe_Patch");
        var preTick = src.IndexOf("class Mission_OnPreTick_HitchProbe_Patch");
        Assert.IsTrue(wait >= 0 && preTick > wait, "WaitTickCompletion must be detoured before OnPreTick's replacement compiles.");
    }

    [TestMethod]
    public void Patch97_NoLongerOwnsTheFrameBoundary()
        => Assert.IsFalse(RepoPaths.ReadSource(HooksDir + "Patch97_MissionTickProfiler.cs", stripComments: true).Contains("OnFrameBoundary"));

    [TestMethod]
    public void ProfilerInstaller_CallsTheProbeInstaller_Once()
        => Assert.AreEqual(1, Count(RepoPaths.ReadSource(HooksDir + "MissionTickProfilerInstaller.cs", stripComments: true),
            "HitchProbeInstaller.Install("));

    [TestMethod]
    public void ProbeBrackets_HaveFirstPrefixAndLastFinalizer()
    {
        // The five classes by name: enumerating the assembly's types would load the game's view assemblies,
        // which this no-game test does not have. HitchProbeBindingTests counts the category's classes.
        var classes = new[]
        {
            typeof(Mission_WaitTickCompletion_HitchProbe_Patch), typeof(Mission_OnPreTick_HitchProbe_Patch),
            typeof(Mission_OnTick_HitchProbe_Patch), typeof(ManagedScriptHolder_TickComponents_HitchProbe_Patch),
            typeof(Mission_SpawnAgent_HitchProbe_Patch),
        };
        foreach (var patch in classes)
        {
            Assert.IsTrue(patch.GetCustomAttributes<HarmonyPatchCategory>().Any(c => c.info.category == HitchProbeInstaller.Category), patch.Name);
            var prefix = patch.GetMethod("Prefix");
            var finalizer = patch.GetMethod("Finalizer");
            Assert.IsNotNull(prefix, patch.Name);
            Assert.IsNotNull(finalizer, patch.Name);
            Assert.AreEqual(Priority.First, prefix.GetCustomAttribute<HarmonyPriority>()?.info.priority, patch.Name + ".Prefix");
            Assert.AreEqual(Priority.Last, finalizer.GetCustomAttribute<HarmonyPriority>()?.info.priority, patch.Name + ".Finalizer");
            Assert.AreEqual(typeof(void), prefix.ReturnType, patch.Name + ".Prefix must be void: a bool prefix is one Harmony can skip.");
            Assert.AreEqual(typeof(void), finalizer.ReturnType, patch.Name + ".Finalizer must be void to keep Harmony's rethrow.");
        }
    }

    [TestMethod]
    public void ProbeBrackets_PairEachCallThroughHarmonysState_OutOnThePrefix_RefOnTheFinalizer()
    {
        // The finalizer tells its own call's prefix from a stripped or skipped one by the state Harmony starts at 0
        // in every call; by ref, so the call it closed stays closed when Harmony reruns the finalizers.
        var classes = new[]
        {
            typeof(Mission_WaitTickCompletion_HitchProbe_Patch), typeof(Mission_OnPreTick_HitchProbe_Patch),
            typeof(Mission_OnTick_HitchProbe_Patch), typeof(ManagedScriptHolder_TickComponents_HitchProbe_Patch),
            typeof(Mission_SpawnAgent_HitchProbe_Patch),
        };
        foreach (var patch in classes)
        {
            var prefix = patch.GetMethod("Prefix")!.GetParameters();
            var finalizer = patch.GetMethod("Finalizer")!.GetParameters();
            Assert.AreEqual(1, prefix.Length, patch.Name + ".Prefix takes only the state");
            Assert.AreEqual(1, finalizer.Length, patch.Name + ".Finalizer takes only the state");
            Assert.AreEqual("__state", prefix[0].Name, patch.Name + ".Prefix");
            Assert.AreEqual("__state", finalizer[0].Name, patch.Name + ".Finalizer");
            Assert.IsTrue(prefix[0].IsOut, patch.Name + ".Prefix sets the state");
            Assert.IsFalse(finalizer[0].IsOut, patch.Name + ".Finalizer reads and closes it");
            Assert.AreEqual(typeof(ProbeState).MakeByRefType(), prefix[0].ParameterType, patch.Name + ".Prefix");
            Assert.AreEqual(typeof(ProbeState).MakeByRefType(), finalizer[0].ParameterType, patch.Name + ".Finalizer");
        }
    }
}
