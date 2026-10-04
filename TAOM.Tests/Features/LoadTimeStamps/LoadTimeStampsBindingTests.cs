using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.ObjectSystem;
using TAOM.Composition;
using TAOM.Dependencies.Foundation;
using TAOM.Features.LoadTimeStamps;
using TAOM.Features.LoadTimeStamps.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LoadTimeStamps;

/// <summary>
/// Drift guards for the load-time stamps' patches. Harmony binds the prefix and finalizer
/// parameters by name, so the names are pinned, not only the types. Every target must also be on
/// PatchShield's exclusion list: a shield finalizer there would swallow a missing-API exception that
/// propagates today, and the stamps promise to leave the methods' behaviour alone.
/// </summary>
[TestClass]
public class LoadTimeStampsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static readonly Type[] LoadXmlPatches =
    {
        typeof(MBObjectManager_LoadXML_StampPatch), typeof(MBObjectManager_CreateMergedXmlFile_StampPatch),
    };

    private static readonly Type[] LifecyclePatches =
    {
        typeof(CampaignEventDispatcher_OnNewGameCreated_StampPatch), typeof(CampaignEventDispatcher_OnGameEarlyLoaded_StampPatch),
        typeof(CampaignEventDispatcher_OnGameLoaded_StampPatch), typeof(CampaignEventDispatcher_OnSessionStart_StampPatch),
        typeof(CampaignEventDispatcher_OnAfterSessionStart_StampPatch),
    };

    private static readonly string[] LifecycleMethods =
    {
        "OnNewGameCreated", "OnGameEarlyLoaded", "OnGameLoaded", "OnSessionStart", "OnAfterSessionStart",
    };

    // The target the class's merged [HarmonyPatch] attributes name, honouring an argument-type array
    // (LoadXML's), as Harmony resolves it.
    internal static MethodBase? TargetOf(Type patch)
    {
        var info = HarmonyMethod.Merge(patch.GetCustomAttributes<HarmonyPatch>().Select(a => a.info).ToList());
        return AccessTools.Method(info.declaringType, info.methodName, info.argumentTypes);
    }

    internal static void AssertOnPatchShieldsExclusionList(IEnumerable<Type> patches)
    {
        foreach (var patch in patches)
        {
            var target = TargetOf(patch);
            Assert.IsNotNull(target, patch.Name + " names no resolvable target");
            Assert.IsTrue(PatchShieldPolicy.IsExcludedTargetMethod(target!.DeclaringType?.FullName, target.Name),
                $"{target.DeclaringType?.FullName}.{target.Name} must be in PatchShieldPolicy.ExcludedTargetMethods");
        }
    }

    private static void AssertCategory(IEnumerable<Type> patches, string category)
    {
        foreach (var patch in patches)
        {
            var categories = patch.GetCustomAttributes<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.AreEqual(new[] { category }, categories, patch.Name + " carries the wrong category");
        }

        var declared = new LoadTimeStampsModule().PatchCategories.Where(d => d.Category == category).ToList();
        Assert.AreEqual(1, declared.Count, "the module must declare " + category + " once");
        Assert.AreEqual(ApplyPhase.ProcessLoad, declared[0].Phase, category + " must apply at OnSubModuleLoad");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadXML_IsTheOnlyMethodOfThatName_WithTheParameterNamesThePrefixBinds()
    {
        RequireGame();

        var methods = typeof(MBObjectManager).GetMethods(AccessTools.allDeclared).Where(m => m.Name == "LoadXML").ToList();
        Assert.AreEqual(1, methods.Count, "MBObjectManager has more or fewer than one LoadXML; re-derive the stamp's target");

        var method = methods[0];
        CollectionAssert.AreEqual(new[] { "id", "isDevelopment", "gameType", "skipXmlFilterForEditor" },
            method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(string), typeof(bool), typeof(string), typeof(bool) },
            method.GetParameters().Select(p => p.ParameterType).ToArray());
        Assert.AreEqual(typeof(void), method.ReturnType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CreateMergedXmlFile_IsStatic_WithTheParameterNamesTheFinalizerBinds()
    {
        RequireGame();

        var methods = typeof(MBObjectManager).GetMethods(AccessTools.allDeclared).Where(m => m.Name == "CreateMergedXmlFile").ToList();
        Assert.AreEqual(1, methods.Count, "CreateMergedXmlFile gained or lost an overload");

        var method = methods[0];
        Assert.IsTrue(method.IsStatic, "CreateMergedXmlFile is no longer static");
        CollectionAssert.AreEqual(new[] { "toBeMerged", "xsltList", "skipValidation" },
            method.GetParameters().Select(p => p.Name).ToArray());
        CollectionAssert.AreEqual(new[] { typeof(List<Tuple<string, string>>), typeof(List<string>), typeof(bool) },
            method.GetParameters().Select(p => p.ParameterType).ToArray());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadXmlPatches_CarryTheirCategory_AndTheModuleDeclaresIt()
    {
        RequireGame();

        AssertCategory(LoadXmlPatches, LoadTimeStampsModule.LoadXmlCategory);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadXmlTargets_AreOnPatchShieldsExclusionList()
    {
        RequireGame();

        AssertOnPatchShieldsExclusionList(LoadXmlPatches);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignEventDispatcher_LifecycleMethods_AreSingleOverloadsTakingTheStarter()
    {
        RequireGame();

        foreach (var name in LifecycleMethods)
        {
            var methods = typeof(CampaignEventDispatcher).GetMethods(AccessTools.allDeclared).Where(m => m.Name == name).ToList();
            Assert.AreEqual(1, methods.Count, "CampaignEventDispatcher." + name + " gained or lost an overload");
            var parameters = methods[0].GetParameters();
            Assert.AreEqual(1, parameters.Length, name + " no longer takes one parameter");
            Assert.AreEqual(typeof(CampaignGameStarter), parameters[0].ParameterType, name + " no longer takes the CampaignGameStarter");
        }

        CollectionAssert.AreEqual(LifecycleMethods, LifecyclePatches.Select(p => TargetOf(p)?.Name).ToArray(),
            "a lifecycle patch targets the wrong dispatcher method");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignEvents_LifecycleEventAccessors_HaveTheListenerTypes()
    {
        RequireGame();

        foreach (var name in new[]
                 {
                     "OnNewGameCreatedEvent", "OnNewGameCreatedPartialFollowUpEndEvent", "OnGameEarlyLoadedEvent",
                     "OnGameLoadedEvent", "OnSessionLaunchedEvent", "OnAfterSessionLaunchedEvent",
                 })
        {
            var property = typeof(CampaignEvents).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(property, "CampaignEvents." + name + " is gone");
            Assert.AreEqual(typeof(IMbEvent<CampaignGameStarter>), property!.PropertyType, name);
        }

        var followUp = typeof(CampaignEvents).GetProperty("OnNewGameCreatedPartialFollowUpEvent", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(followUp, "CampaignEvents.OnNewGameCreatedPartialFollowUpEvent is gone");
        Assert.AreEqual(typeof(IMbEvent<CampaignGameStarter, int>), followUp!.PropertyType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LifecyclePatches_CarryTheirCategory_AndTheModuleDeclaresIt()
    {
        RequireGame();

        AssertCategory(LifecyclePatches, LoadTimeStampsModule.LifecycleCategory);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LifecycleTargets_AreOnPatchShieldsExclusionList()
    {
        RequireGame();

        AssertOnPatchShieldsExclusionList(LifecyclePatches);
    }
}
