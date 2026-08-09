using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// #441 — the assignment-to-formation table, and the engine bindings the placement rides on.
/// Support maps to null ON PURPOSE (rear-echelon fantasy keeps vanilla placement); a mapping
/// added for it later should be a deliberate design change that reddens this table first.
/// </summary>
[TestClass]
public class BattleFormationPolicyTests
{
    [TestMethod]
    public void Infantry_MapsToInfantry()
        => Assert.AreEqual(FormationClass.Infantry, BattleFormationPolicy.TargetFormationFor(ServiceAssignment.Infantry));

    [TestMethod]
    public void Archer_MapsToRanged()
        => Assert.AreEqual(FormationClass.Ranged, BattleFormationPolicy.TargetFormationFor(ServiceAssignment.Archer));

    [TestMethod]
    public void Cavalry_MapsToCavalry()
        => Assert.AreEqual(FormationClass.Cavalry, BattleFormationPolicy.TargetFormationFor(ServiceAssignment.Cavalry));

    [TestMethod]
    public void Support_IsDeliberatelyUnmapped()
        => Assert.IsNull(BattleFormationPolicy.TargetFormationFor(ServiceAssignment.Support));

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FormationPlacement_EngineBindings_ResolveAgainstInstalledEngine()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));

        var agent = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Agent");
        Assert.IsNotNull(AccessTools.PropertySetter(agent, "Formation"),
            "Agent.Formation setter missing — placement would not compile against this engine.");
        Assert.IsNotNull(AccessTools.Method(agent, "TeleportToPosition"),
            "Agent.TeleportToPosition missing — the reposition would silently not exist.");
        Assert.IsNotNull(AccessTools.PropertyGetter(agent, "IsPlayerTroop"),
            "Agent.IsPlayerTroop missing — the build-time player check would fail.");

        var formation = AccessTools.TypeByName("TaleWorlds.MountAndBlade.Formation");
        Assert.IsNotNull(AccessTools.PropertyGetter(formation, "IsPlayerTroopInFormation"),
            "Formation.IsPlayerTroopInFormation missing — the engine soldier branch this feature " +
            "completes (BehaviorComponent v1.4.7 :105) has moved.");
        Assert.IsNotNull(AccessTools.PropertyGetter(formation, "OrderGroundPosition"),
            "Formation.OrderGroundPosition missing — the reposition target has moved.");
        Assert.IsNotNull(AccessTools.PropertyGetter(formation, "OrderPositionIsValid"),
            "Formation.OrderPositionIsValid missing — the reposition guard has moved.");
    }
}
