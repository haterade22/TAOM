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
}
