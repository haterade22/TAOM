using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Client;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.SeatOfSeeing.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Patch140_CtrlNumberTransfer (design section 3): the prefix on <c>MissionOrderVM.OnTroopFormationSelected(int formationTroopIndex)</c>. Harmony
/// injects the prefix's parameter by NAME, so the engine's parameter name is pinned by the binding gate; with the command mouse off the prefix lets
/// vanilla run and stores nothing.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class SeatCtrlNumberPatchWiringTests
{
    private const string EngineType = "TaleWorlds.MountAndBlade.ViewModelCollection.Order.MissionOrderVM";

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        SeatPresence.Set(false);
        SeatPresence.TakeTransfer();
    }

    [TestCleanup]
    public void Cleanup()
    {
        SeatPresence.Set(false);
        SeatPresence.TakeTransfer();
    }

    private static MethodInfo Target()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(EngineType, throwOnError: false)).FirstOrDefault(t => t != null);
        Assert.IsNotNull(type, EngineType + " is not in the installed engine");
        var info = typeof(Patch140_CtrlNumberTransfer).GetCustomAttributes<HarmonyPatch>(inherit: false).Single().info;
        Assert.AreEqual(type, info.declaringType);
        Assert.AreEqual("OnTroopFormationSelected", info.methodName);
        var method = AccessTools.DeclaredMethod(info.declaringType, info.methodName, info.argumentTypes);
        Assert.IsNotNull(method, "OnTroopFormationSelected did not resolve in the installed engine");
        return method!;
    }

    [TestMethod]
    public void ThePatch_HasOneTargetAndTheSeatCategory_AndLivesInTheClient()
    {
        var patch = typeof(Patch140_CtrlNumberTransfer);

        Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length);
        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false).Cast<HarmonyPatchCategory>().Select(c => c.info.category);
        CollectionAssert.Contains(categories.ToList(), SeatOfSeeingModule.PatchCategory);
        Assert.AreEqual(typeof(TaomClientComposition).Assembly, patch.Assembly);
        Assert.AreEqual("TAOM.Features.SeatOfSeeing.Hooks", patch.Namespace);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TheTarget_IsThePublicIntMethod_WithTheParameterNameTheSkipPrefixBindsBy()
    {
        var target = Target();

        Assert.IsTrue(target.IsPublic);
        Assert.AreEqual(typeof(void), target.ReturnType);
        CollectionAssert.AreEqual(new[] { "formationTroopIndex" }, target.GetParameters().Select(p => p.Name).ToArray());
        Assert.AreEqual(typeof(int), target.GetParameters()[0].ParameterType);

        var prefix = typeof(Patch140_CtrlNumberTransfer).GetMethod("Prefix")!;
        Assert.AreEqual(typeof(bool), prefix.ReturnType, "a skip prefix");
        CollectionAssert.AreEqual(new[] { "formationTroopIndex" }, prefix.GetParameters().Select(p => p.Name).ToArray());
    }

    [TestMethod]
    public void Prefix_WithTheCommandMouseOff_RunsVanillaAndStoresNothing()
    {
        SeatPresence.Set(true);
        SeatPresence.SetCommandMouse(false);

        var runsVanilla = Patch140_CtrlNumberTransfer.Prefix(0);

        Assert.IsTrue(runsVanilla);
        Assert.AreEqual(-1, SeatPresence.TakeTransfer());
    }

    [TestMethod]
    public void Request_IsTakenOnce_AndForgottenWhenTheCommandMouseStops()
    {
        SeatPresence.Set(true);
        SeatPresence.SetCommandMouse(true);
        SeatPresence.RequestTransfer(3);

        Assert.AreEqual(3, SeatPresence.TakeTransfer());
        Assert.AreEqual(-1, SeatPresence.TakeTransfer(), "taken once");

        SeatPresence.RequestTransfer(5);
        SeatPresence.SetCommandMouse(false);
        Assert.AreEqual(-1, SeatPresence.TakeTransfer(), "a stale request never acts after the command mouse stops");
    }

    [TestMethod]
    public void Request_IsForgottenWhenTheSeatIsLeft()
    {
        SeatPresence.Set(true);
        SeatPresence.SetCommandMouse(true);
        SeatPresence.RequestTransfer(2);

        SeatPresence.Set(false);

        Assert.AreEqual(-1, SeatPresence.TakeTransfer());
    }
}
