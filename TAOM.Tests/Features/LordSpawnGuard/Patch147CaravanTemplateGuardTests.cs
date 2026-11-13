using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LordSpawnGuard;
using TAOM.Features.LordSpawnGuard.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.LordSpawnGuard;

/// <summary>
/// #482: vanilla <c>SpawnCaravan</c> dereferences a null template when no caravan template fits
/// the merchant's settlement. Pins the hazard rule (<see cref="CaravanTemplateGuard"/>), the
/// finalizer's filter (only an NRE on a flagged call is contained) and the target binding.
/// </summary>
[TestClass]
public class Patch147CaravanTemplateGuardTests
{
    private static readonly int[] Land = { 0, 0 };
    private static readonly int[] Naval = { 2 };
    private static readonly int[] Mixed = { 0, 3 };

    [TestMethod]
    public void AllLandTemplates_InPortTown_IsHazard()
    {
        // The issue's main case: a fully populated list that matches nothing at a port.
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(true, true, Land, Land));
    }

    [TestMethod]
    public void AllLandTemplates_InLandTown_IsSafe()
    {
        Assert.IsFalse(CaravanTemplateGuard.CanFailToResolve(true, false, Land, Land));
    }

    [TestMethod]
    public void MixedTemplates_AreSafeEitherWay()
    {
        Assert.IsFalse(CaravanTemplateGuard.CanFailToResolve(true, true, Mixed, Mixed));
        Assert.IsFalse(CaravanTemplateGuard.CanFailToResolve(true, false, Mixed, Mixed));
    }

    [TestMethod]
    public void NavalOnlyTemplates_InLandTown_IsHazard()
    {
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(true, false, Naval, Naval));
    }

    [TestMethod]
    public void EmptyOrMissingList_IsHazard()
    {
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(true, false, Land, new int[0]));
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(true, false, null, Land));
    }

    [TestMethod]
    public void EliteListAloneUnusable_IsHazard()
    {
        // The elite roll is random inside vanilla, so either list being unusable can crash.
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(true, true, Mixed, Land));
    }

    [TestMethod]
    public void NoCurrentSettlement_IsHazard()
    {
        // The predicate reads hero.CurrentSettlement unguarded.
        Assert.IsTrue(CaravanTemplateGuard.CanFailToResolve(false, false, Mixed, Mixed));
    }

    [TestMethod]
    public void Finalizer_NreOnFlaggedCall_IsContained()
    {
        Assert.IsNull(Patch147_CaravanTemplateGuard.Finalizer(new NullReferenceException(), null, true));
    }

    [TestMethod]
    public void Finalizer_NreOnUnflaggedCall_Propagates()
    {
        var nre = new NullReferenceException("unrelated");
        Assert.AreSame(nre, Patch147_CaravanTemplateGuard.Finalizer(nre, null, false));
    }

    [TestMethod]
    public void Finalizer_OtherExceptionOnFlaggedCall_Propagates()
    {
        var ioe = new InvalidOperationException("unrelated");
        Assert.AreSame(ioe, Patch147_CaravanTemplateGuard.Finalizer(ioe, null, true));
    }

    [TestMethod]
    public void Finalizer_NoException_PassesThroughNull()
    {
        Assert.IsNull(Patch147_CaravanTemplateGuard.Finalizer(null, null, true));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SpawnCaravan_BindingResolves_AgainstInstalledEngine()
    {
        if (!GameAssemblies.EnsureLoaded())
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var type = AccessTools.TypeByName("TaleWorlds.CampaignSystem.CampaignBehaviors.CaravansCampaignBehavior");
        Assert.IsNotNull(type, "CaravansCampaignBehavior did not resolve; Patch147's target type is gone.");

        var method = AccessTools.Method(type, "SpawnCaravan");
        Assert.IsNotNull(method, "SpawnCaravan did not resolve; Patch147 would apply to nothing.");

        var first = method.GetParameters().FirstOrDefault();
        Assert.IsNotNull(first, "SpawnCaravan takes no parameters; signature drifted.");
        Assert.AreEqual("Hero", first.ParameterType.Name, "SpawnCaravan's first parameter is no longer a Hero.");
        Assert.AreEqual("hero", first.Name, "Harmony binds the prefix and finalizer `hero` argument by name.");
    }
}
