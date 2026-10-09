// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), frame-buffer-guard and frame-buffer-watch.
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using DryIoc;
using MCM.Abstractions.Attributes;
using MCM.Abstractions.Attributes.v2;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.CrashReport;
using TAOM.Features.SkeletonBuffer;
using TAOM.Features.SkeletonBuffer.Hooks;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SkeletonBuffer;

/// <summary>
/// Wiring guard for the skeleton buffer guard and watch: the module is listed once, installs only at the first
/// main menu, adds the watch to every mission, resolves from a fresh container, its two MCM toggles, and that the native
/// write code lives in one adapter file.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SkeletonBufferWiringTests
{
    private Container _container = null!;
    private readonly SkeletonBufferModule _sut = new SkeletonBufferModule();

    [TestInitialize]
    public void Setup()
    {
        _container = new Container();
        _container.RegisterInstance(Substitute.For<IModLogger>());
        _container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        _sut.RegisterServices(_container);
    }

    [TestCleanup]
    public void Cleanup() => _container.Dispose();

    [TestMethod]
    public void FeatureModules_ListTheModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<SkeletonBufferModule>().Count());
    }

    [TestMethod]
    public void Module_IsEnabledAndHasNoHarmonyPatch()
    {
        Assert.AreEqual("SkeletonBuffer", _sut.Id);
        Assert.IsNull(_sut.ParkedReason);
        Assert.IsFalse(_sut.OwnsSaveData);
        Assert.AreEqual(0, _sut.PatchCategories.Count);
        Assert.AreEqual(0, _sut.CampaignBehaviors.Count);
        Assert.AreEqual(0, _sut.GameModels.Count);
    }

    [TestMethod]
    public void Module_DeclaresTheWatchBehaviorOnce()
    {
        Assert.AreEqual(1, _sut.MissionBehaviors.Count);
        Assert.AreEqual(typeof(SkeletonBufferWatchMissionBehavior), _sut.MissionBehaviors[0].BehaviorType);
    }

    [TestMethod]
    public void RegisterServices_TheGuardAndTheWatchResolveAsSingletons()
    {
        var guard = _container.Resolve<ISkeletonBufferGuardService>();
        var watch = _container.Resolve<SkeletonBufferWatchService>();

        Assert.IsInstanceOfType(guard, typeof(SkeletonBufferGuardService));
        Assert.AreSame(guard, _container.Resolve<ISkeletonBufferGuardService>());
        Assert.AreSame(watch, _container.Resolve<SkeletonBufferWatchService>());
        Assert.IsInstanceOfType(_container.Resolve<ISkeletonBufferMemoryAdapter>(), typeof(SkeletonBufferMemoryAdapter));
        Assert.IsInstanceOfType(_container.Resolve<ISkeletonBufferEngineAdapter>(), typeof(SkeletonBufferEngineAdapter));
    }

    [TestMethod]
    public void OnPhase_InstallsOnlyAtMainMenu()
    {
        var guard = Substitute.For<ISkeletonBufferGuardService>();
        _container.RegisterInstance(guard, IfAlreadyRegistered.Replace);

        _sut.OnPhase(ApplyPhase.ProcessLoad, _container);
        _sut.OnPhase(ApplyPhase.GameInit, _container);
        _sut.OnPhase(ApplyPhase.FirstMission, _container);
        guard.DidNotReceive().Install();

        _sut.OnPhase(ApplyPhase.MainMenu, _container);
        guard.Received(1).Install();
    }

    [TestMethod]
    public void Behavior_EndsTheMissionOnEndMissionAndOnRemoveBehavior_AndNeverOverridesOnBehaviorInitialize()
    {
        var type = typeof(SkeletonBufferWatchMissionBehavior);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        Assert.IsNull(type.GetMethod("OnBehaviorInitialize", flags), "never fires for a behavior TAOM adds (#606)");
        Assert.IsNotNull(type.GetMethod("OnRemoveBehavior", flags));
        Assert.IsNotNull(type.GetMethod("OnEndMission", flags));

        var source = RepoPaths.ReadSource("Main/Features/SkeletonBuffer/Hooks/SkeletonBufferWatchMissionBehavior.cs", stripComments: true);
        Assert.IsTrue(Regex.IsMatch(source,
                @"public\s+override\s+void\s+OnRemoveBehavior\s*\(\s*\)\s*\{\s*_watch\.End\s*\(\s*\)\s*;\s*base\.OnRemoveBehavior\s*\(\s*\)\s*;\s*\}"),
            "OnRemoveBehavior must call _watch.End() and then base.OnRemoveBehavior(), and nothing else.");
        StringAssert.Contains(source, "protected override void OnEndMission() => _watch.End();");
    }

    [TestMethod]
    public void Settings_GuardIsOnAndNeedsARestart_WatchIsOnAndDoesNot()
    {
        var guard = typeof(CrashReportSettings).GetProperty("SkeletonBufferGuard");
        var watch = typeof(CrashReportSettings).GetProperty("SkeletonBufferWatch");
        Assert.IsNotNull(guard);
        Assert.IsNotNull(watch);

        var settings = new CrashReportSettings();
        Assert.IsTrue((bool)guard!.GetValue(settings)!);
        Assert.IsTrue((bool)watch!.GetValue(settings)!);
        var guardAttribute = guard.GetCustomAttribute<SettingPropertyBoolAttribute>()!;
        var watchAttribute = watch.GetCustomAttribute<SettingPropertyBoolAttribute>()!;
        Assert.AreEqual("Skeleton Buffer Guard", guardAttribute.DisplayName);
        Assert.AreEqual("Skeleton Buffer Watch", watchAttribute.DisplayName);
        Assert.IsTrue(guardAttribute.RequireRestart, "the guard installs once, at the first main menu");
        Assert.IsFalse(watchAttribute.RequireRestart);
        Assert.IsFalse(string.IsNullOrWhiteSpace(guardAttribute.HintText));
        Assert.IsFalse(string.IsNullOrWhiteSpace(watchAttribute.HintText));
        Assert.AreEqual("Master", guard.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
        Assert.AreEqual("Master", watch.GetCustomAttribute<SettingPropertyGroupAttribute>()!.GroupName);
    }

    [TestMethod]
    public void Settings_SitAfterSurviveMissionStartFailures()
    {
        int OrderOf(string name) =>
            typeof(CrashReportSettings).GetProperty(name)!.GetCustomAttribute<SettingPropertyBoolAttribute>()!.Order;

        Assert.IsTrue(OrderOf("SkeletonBufferGuard") > OrderOf("SurviveMissionStartFailures"));
        Assert.IsTrue(OrderOf("SkeletonBufferWatch") > OrderOf("SkeletonBufferGuard"));
    }

    [TestMethod]
    public void Settings_AreInstrumentationForCoopParity()
    {
        foreach (var name in new[] { "SkeletonBufferGuard", "SkeletonBufferWatch" })
            Assert.IsFalse(CoopSettingsRelevance.IsSimulationRelevant(typeof(CrashReportSettings).GetProperty(name)!), name);
    }

    [TestMethod]
    public void SettingsProvider_NoMcmInstance_DefaultsBothOn()
    {
        var provider = new SkeletonBufferSettingsProvider();

        Assert.IsTrue(provider.GuardEnabled);
        Assert.IsTrue(provider.WatchEnabled);
    }

    // TAOM's first write to engine code lives in one adapter file; a second one would escape the review that file gets.
    [TestMethod]
    public void NativeWriteCode_LivesInOneAdapterFileOnly()
    {
        const string nativeWrite = @"VirtualProtect|VirtualAlloc|VirtualFree|WriteProcessMemory|FlushInstructionCache";
        const string theAdapter = "Adapters/SkeletonBufferMemoryAdapter.cs";
        Assert.IsTrue(Regex.IsMatch(RepoPaths.ReadSource("Main/" + theAdapter, stripComments: true), nativeWrite),
            "the floor: the pattern must match the adapter itself, or an empty offender list proves nothing");

        var mainDir = RepoPaths.RepoPath("Main");
        var offenders = Directory.GetFiles(mainDir, "*.cs", SearchOption.AllDirectories)
            .Select(f => f.Substring(mainDir.Length + 1).Replace('\\', '/'))
            .Where(f => f != theAdapter)
            .Where(f => Regex.IsMatch(RepoPaths.ReadSource("Main/" + f, stripComments: true), nativeWrite))
            .ToList();

        CollectionAssert.AreEqual(new string[0], offenders, "native write calls outside the one adapter: " + string.Join(", ", offenders));
    }

    [TestMethod]
    public void Service_TakesAdaptersOnly()
    {
        foreach (var path in new[]
                 {
                     "Main/Features/SkeletonBuffer/SkeletonBufferGuardService.cs",
                     "Main/Features/SkeletonBuffer/SkeletonBufferWatchService.cs",
                     "Main/Features/SkeletonBuffer/SkeletonBufferSignature.cs",
                     "Main/Features/SkeletonBuffer/SkeletonBufferCave.cs",
                     "Main/Features/SkeletonBuffer/SkeletonBufferWatchState.cs",
                     "Main/Features/SkeletonBuffer/SkeletonBufferLines.cs",
                 })
        {
            var source = RepoPaths.ReadSource(path, stripComments: true);
            Assert.IsFalse(Regex.IsMatch(source, @"using\s+TaleWorlds\.|DllImport|Marshal\."), path + " must not touch TaleWorlds types or P/Invoke");
        }
    }

    [TestMethod]
    public void NewKeys_AreRegisteredInTheModuleStrings()
    {
        var strings = RepoPaths.ReadSource("Main/_Module/ModuleData/taom_module_strings.xml");

        StringAssert.Contains(strings, "id=\"taom_skeleton_buffer_warning\"");
    }
}
