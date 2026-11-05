using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.SaveLoadDiagnostics.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SaveLoadDiagnostics;

/// <summary>
/// Drift-guards for Patch61 bindings that <c>HarmonyPatchBindingTests</c> does NOT cover.
/// That suite invokes each hook's <c>TargetMethod(s)</c> and reddens on target drift — but the
/// container hook additionally binds the <c>ContainerHeaderLoadData</c> PROPERTY by name for its
/// GraphFault attribution (a rename degrades every container stamp to saveId='&lt;null&gt;'
/// type='&lt;unresolved&gt;' with all tests green), and every hook logging a SaveId depends on
/// <c>SaveId.GetStringId()</c> existing. These are the red tests that catch those renames offline.
/// </summary>
[TestClass]
public class SaveLoadDiagnosticsBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ContainerLoadData_TypeMethodsAndHeaderProperty_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = ContainerLoadData_Fill_Patch.ResolveContainerLoadDataType();
        Assert.IsNotNull(type, "TaleWorlds.SaveSystem.Load.ContainerLoadData did not resolve — container GraphFault hooks dead.");

        foreach (var name in ContainerLoadData_Fill_Patch.PatchedMethodNames)
            Assert.IsNotNull(AccessTools.Method(type, name),
                $"ContainerLoadData.{name} did not resolve — that container fault site would be uninstrumented.");

        Assert.IsNotNull(AccessTools.Property(type, "ContainerHeaderLoadData"),
            "ContainerLoadData.ContainerHeaderLoadData property did not resolve — every container GraphFault " +
            "stamp silently degrades to saveId='<null>' type='<unresolved>' (the attribution this feature exists for).");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignBehaviorDataStore_BothDirections_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = CampaignBehaviorDataStore_LoadBehaviorData_Patch.ResolveStoreType();
        Assert.IsNotNull(type, "TaleWorlds.CampaignSystem.CampaignBehaviorDataStore did not resolve — per-behavior SyncData attribution dead.");

        foreach (var name in CampaignBehaviorDataStore_LoadBehaviorData_Patch.PatchedMethodNames)
            Assert.IsNotNull(AccessTools.Method(type, name),
                $"CampaignBehaviorDataStore.{name} did not resolve — that direction's behavior attribution would be uninstrumented.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ArchiveDeserializer_LoadFrom_ResolvesAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = ArchiveDeserializer_LoadFrom_Patch.ResolveDeserializerType();
        Assert.IsNotNull(type, "TaleWorlds.SaveSystem.ArchiveDeserializer did not resolve — archive-parse attribution dead.");
        Assert.IsNotNull(AccessTools.Method(type, "LoadFrom", new[] { typeof(byte[]) }),
            "ArchiveDeserializer.LoadFrom(byte[]) did not resolve — archive-parse attribution dead.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SaveId_GetStringId_ResolvesAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.SaveSystem.Definition.SaveId");
        Assert.IsNotNull(type, "SaveId did not resolve.");
        Assert.IsNotNull(AccessTools.Method(type, "GetStringId"),
            "SaveId.GetStringId() did not resolve — every saveId= log field and the UnknownSaveId dedup key would regress to type names.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SaveContext_CollectObjectsAndCountFields_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.SaveSystem.Save.SaveContext");
        Assert.IsNotNull(type, "SaveContext did not resolve.");
        Assert.IsNotNull(AccessTools.Method(type, "CollectObjects", Type.EmptyTypes),
            "SaveContext.CollectObjects() did not resolve: the SaveCollected probe would not apply.");
        Assert.AreEqual(typeof(List<object>), AccessTools.Field(type, "_childObjects")?.FieldType,
            "SaveContext._childObjects changed: objects= would disappear from SaveCollected.");
        Assert.AreEqual(typeof(List<object>), AccessTools.Field(type, "_childContainers")?.FieldType,
            "SaveContext._childContainers changed: containers= would disappear from SaveCollected.");
        Assert.AreEqual(typeof(List<string>), AccessTools.Field(type, "_strings")?.FieldType,
            "SaveContext._strings changed: strings= would disappear from SaveCollected.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LoadContext_LoadAndCountFields_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.SaveSystem.Load.LoadContext");
        Assert.IsNotNull(type, "LoadContext did not resolve.");
        var loadData = AccessTools.TypeByName("TaleWorlds.SaveSystem.LoadData");
        Assert.IsNotNull(loadData, "LoadData did not resolve.");
        Assert.IsNotNull(AccessTools.Method(type, "Load", new[] { loadData, typeof(bool) }),
            "LoadContext.Load(LoadData, bool) did not resolve: the LoadCounts probe would not apply.");
        foreach (var name in new[] { "_objectCount", "_stringCount", "_containerCount" })
            Assert.AreEqual(typeof(int), AccessTools.Field(type, name)?.FieldType,
                $"LoadContext.{name} changed: its count would disappear from LoadCounts.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CampaignBehaviorDataStore_RecordFields_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var store = CampaignBehaviorDataStore_LoadBehaviorData_Patch.ResolveStoreType();
        Assert.IsNotNull(store, "CampaignBehaviorDataStore did not resolve.");
        Assert.IsNotNull(AccessTools.Field(store, "_behaviorDict"),
            "CampaignBehaviorDataStore._behaviorDict did not resolve: per-behavior sizes would stop being recorded.");
        var record = AccessTools.Inner(store, "BehaviorSaveData");
        Assert.IsNotNull(record, "CampaignBehaviorDataStore.BehaviorSaveData did not resolve.");
        Assert.AreEqual(typeof(Dictionary<string, object>), AccessTools.Field(record, "_records")?.FieldType,
            "BehaviorSaveData._records changed: per-behavior sizes would stop being recorded.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MBObjectBase_IsRegisteredGetter_ResolvesAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        Assert.IsNotNull(AccessTools.PropertyGetter(typeof(TaleWorlds.ObjectSystem.MBObjectBase), "IsRegistered"),
            "MBObjectBase.IsRegistered getter did not resolve: the census would stop counting unregistered copies.");
    }
}
