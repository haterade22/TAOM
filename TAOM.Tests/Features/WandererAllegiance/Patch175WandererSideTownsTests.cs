using System;
using System.Linq;
using System.Reflection;
using DryIoc;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;
using TAOM.Features.TroopProgression;
using TAOM.Features.WandererAllegiance;
using TAOM.Features.WandererAllegiance.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.WandererAllegiance;

/// <summary>Patch175: the scope counter, the module wiring, and the engine bindings of the three targets.</summary>
[TestClass]
public class Patch175WandererSideTownsTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestCleanup]
    public void Reset() => Patch175_WandererSideEnter.Depth = 0;

    // ---- the scope counter --------------------------------------------------------------

    [TestMethod]
    public void Scope_PrefixOpens_FinalizerCloses()
    {
        Patch175_WandererSideScope.Prefix();
        Assert.AreEqual(1, Patch175_WandererSideEnter.Depth);
        Patch175_WandererSideScope.Finalizer();
        Assert.AreEqual(0, Patch175_WandererSideEnter.Depth);
    }

    [TestMethod]
    public void Scope_FinalizerWithoutItsPrefix_StaysAtZero()
    {
        // PatchShield strips prefixes but never finalizers.
        Patch175_WandererSideScope.Finalizer();
        Assert.AreEqual(0, Patch175_WandererSideEnter.Depth);
    }

    [TestMethod]
    public void Scope_IsThreadStatic_SoAnotherThreadNeverSeesIt()
    {
        Patch175_WandererSideScope.Prefix();
        var seenElsewhere = -1;
        var thread = new System.Threading.Thread(() => seenElsewhere = Patch175_WandererSideEnter.Depth);
        thread.Start();
        thread.Join();
        Assert.AreEqual(0, seenElsewhere);
    }

    [TestMethod]
    public void EnterPrefix_OutsideTheScope_ReadsNothingAndLeavesTheArgument()
    {
        // Depth is 0 and no adapter is set: the prefix must return before touching either argument.
        Settlement? settlement = null;
        Patch175_WandererSideEnter.Prefix(null!, null!, ref settlement!);
        Assert.IsNull(settlement);
    }

    // ---- wiring --------------------------------------------------------------------------

    [TestMethod]
    public void TheModule_DeclaresTheCategoryAtGameInit_AndStillOwnsNoSaveData()
    {
        var module = new WandererAllegianceModule();

        Assert.IsFalse(module.OwnsSaveData);
        Assert.AreEqual(1, module.PatchCategories.Count);
        Assert.AreEqual("Patch175_WandererSideTowns", module.PatchCategories[0].Category);
        Assert.AreEqual(ApplyPhase.GameInit, module.PatchCategories[0].Phase);
    }

    [TestMethod]
    public void TheModule_RegistersThePlacementServiceAndAdapter()
    {
        var container = new Container();
        new WandererAllegianceModule().RegisterServices(container);
        container.RegisterInstance(Substitute.For<TAOM.Core.Logging.IModLogger>());
        container.RegisterInstance(Substitute.For<IAlignmentService>());
        container.RegisterInstance(Substitute.For<IRandomProvider>());
        container.RegisterInstance(Substitute.For<ICoopSessionProvider>());
        container.RegisterInstance(Substitute.For<IDedicatedServerProvider>());
        container.RegisterInstance(Substitute.For<IWandererAllegianceSettingsProvider>(), IfAlreadyRegistered.Replace);

        Assert.IsNotNull(container.Resolve<WandererTownSideService>());
        Assert.IsNotNull(container.Resolve<WandererTownAdapter>());
        Assert.AreSame(container.Resolve<WandererTownSideService>(), container.Resolve<WandererTownSideService>());
    }

    // ---- engine bindings -----------------------------------------------------------------

    private static MethodInfo EngineMethod(string typeName, string method, BindingFlags flags)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(typeName);
        Assert.IsNotNull(type, typeName);
        var matches = type.GetMethods(flags).Where(m => m.Name == method).ToArray();
        Assert.AreEqual(1, matches.Length, typeName + "." + method + " must resolve to exactly one method (Patch175).");
        return matches[0];
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void EnterSettlementAction_ApplyInternal_HasTheNamedParametersTheEnterPrefixBinds()
    {
        var target = EngineMethod("TaleWorlds.CampaignSystem.Actions.EnterSettlementAction", "ApplyInternal",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.AreEqual(typeof(void), target.ReturnType);
        var parameters = target.GetParameters().ToDictionary(p => p.Name!, p => p.ParameterType.FullName);
        Assert.AreEqual("TaleWorlds.CampaignSystem.Hero", parameters["hero"]);
        Assert.AreEqual("TaleWorlds.CampaignSystem.Party.MobileParty", parameters["mobileParty"]);
        Assert.AreEqual("TaleWorlds.CampaignSystem.Settlements.Settlement", parameters["settlement"]);

        var prefix = typeof(Patch175_WandererSideEnter).GetMethod("Prefix")!;
        Assert.AreEqual(typeof(void), prefix.ReturnType, "a void prefix never skips vanilla");
        Assert.IsTrue(prefix.GetParameters().Single(p => p.Name == "settlement").ParameterType.IsByRef);
        Assert.AreEqual(target, Patch175_WandererSideEnter.TargetMethod());
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void EnterTarget_IsTooLargeForTheClrToInline_WhileTheWrapperIsNot()
    {
        var target = EngineMethod("TaleWorlds.CampaignSystem.Actions.EnterSettlementAction", "ApplyInternal",
            BindingFlags.NonPublic | BindingFlags.Static);
        var wrapper = EngineMethod("TaleWorlds.CampaignSystem.Actions.EnterSettlementAction", "ApplyForCharacterOnly",
            BindingFlags.Public | BindingFlags.Static);

        Assert.IsTrue(target.GetMethodBody()!.GetILAsByteArray()!.Length > 100,
            "ApplyInternal is small enough to inline now: patch a method the CLR cannot inline (lessons/harmony-il.md, #1189).");
        Assert.IsTrue(wrapper.GetMethodBody()!.GetILAsByteArray()!.Length <= 32,
            "ApplyForCharacterOnly grew past the inline size: it may be patched directly again.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ScopeTargets_BothResolve_ToTheCompanionsBehaviorsPrivateMethods()
    {
        var create = EngineMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.CompanionsCampaignBehavior",
            "CreateCompanionAndAddToSettlement", BindingFlags.Instance | BindingFlags.NonPublic);
        var swap = EngineMethod("TaleWorlds.CampaignSystem.CampaignBehaviors.CompanionsCampaignBehavior",
            "SwapCompanions", BindingFlags.Instance | BindingFlags.NonPublic);

        var targets = Patch175_WandererSideScope.TargetMethods().ToArray();
        CollectionAssert.AreEquivalent(new[] { create, swap }, targets);
    }
}
