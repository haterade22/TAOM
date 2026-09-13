using System;
using System.Linq;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.SmartCavalryAI;

/// <summary>
/// Drift-guards for Patch31 (SmartCavalryAI, #586) against the installed engine.
///
/// - <c>Patch31_FormationSetMovementOrder</c> postfixes <c>Formation.SetMovementOrder(MovementOrder input)</c>.
///   Harmony binds the <c>input</c> parameter by NAME; a rename would leave it default and the
///   postfix would never see a charge order.
/// - The postfix compares <c>input.OrderEnum</c> against <c>MovementOrderEnum.Charge</c> and
///   <c>ChargeToTarget</c>. C# folds those to constants at compile time, so a renumbering would
///   pass a name-only check while the compiled guard compares against stale literals.
/// - The postfix body and the adapters it drives read engine members that a JIT-time resolution
///   failure would surface at the patched target, past the postfix's own try/catch. Every such
///   member is pinned here. Behaviour is covered by <c>CavalryChargeServiceTests</c>.
/// </summary>
[TestClass]
public class SmartCavalryAIBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Resolve(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type;
    }

    /// <summary>The declared type of a public property OR public field of that name, or null.
    /// Engine structs expose some members as readonly fields (<c>MovementOrder.OrderEnum</c>)
    /// and some as properties, and a rewrite between the two is invisible to callers, so the
    /// pin must accept both.</summary>
    private static Type? MemberType(Type type, string name)
    {
        var property = AccessTools.Property(type, name);
        if (property != null) return property.PropertyType;
        var field = AccessTools.Field(type, name);
        return field?.FieldType;
    }

    private static Type FormationType() => Resolve("TaleWorlds.MountAndBlade.Formation");
    private static Type MovementOrderType() => Resolve("TaleWorlds.MountAndBlade.MovementOrder");
    private static Type MissionType() => Resolve("TaleWorlds.MountAndBlade.Mission");
    private static Type WorldPositionType() => Resolve("TaleWorlds.Engine.WorldPosition");

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void SetMovementOrder_ResolvesPublicInstance_WithParameterNamedInput()
    {
        RequireGame();

        var method = AccessTools.Method(FormationType(), "SetMovementOrder", new[] { MovementOrderType() });
        Assert.IsNotNull(method, "Formation.SetMovementOrder(MovementOrder) did not resolve; Patch31 has no target.");
        Assert.IsTrue(method.IsPublic && !method.IsStatic, "the target stopped being a public instance method.");

        var overloads = FormationType().GetMethods(AccessTools.all).Count(m => m.Name == "SetMovementOrder");
        Assert.AreEqual(1, overloads, "the target gained an overload; re-check the explicit parameter type array on Patch31.");

        // Harmony injects by NAME. `input` is the postfix's own parameter name; after a rename the
        // postfix would receive default(MovementOrder) and never see a charge.
        CollectionAssert.AreEqual(new[] { "input" }, method.GetParameters().Select(p => p.Name).ToArray(),
            "parameter name changed; the postfix binds by name and would receive nothing.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MovementOrderEnum_ChargeValuesAreStillTwoAndThree()
    {
        RequireGame();

        var orderEnumType = MemberType(MovementOrderType(), "OrderEnum");
        Assert.IsNotNull(orderEnumType, "MovementOrder.OrderEnum is gone.");
        var enumType = AccessTools.Inner(MovementOrderType(), "MovementOrderEnum");
        Assert.IsNotNull(enumType, "MovementOrder.MovementOrderEnum is gone.");
        Assert.AreEqual(enumType, orderEnumType, "OrderEnum no longer returns MovementOrderEnum; the compiled comparisons are against the wrong type.");

        // Patch31 and FormationAdapter.CurrentMovementOrderType compare against these members;
        // the comparisons are constant-folded, so the numeric values are what actually ship.
        Assert.AreEqual(2, Convert.ToInt32(Enum.Parse(enumType, "Charge")), "MovementOrderEnum.Charge was renumbered.");
        Assert.AreEqual(3, Convert.ToInt32(Enum.Parse(enumType, "ChargeToTarget")), "MovementOrderEnum.ChargeToTarget was renumbered.");
        Assert.AreEqual(7, Convert.ToInt32(Enum.Parse(enumType, "Move")), "MovementOrderEnum.Move was renumbered.");
        Assert.AreEqual(9, Convert.ToInt32(Enum.Parse(enumType, "Stop")), "MovementOrderEnum.Stop was renumbered.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PostfixBodyEngineMembers_StillExist()
    {
        RequireGame();

        var formation = FormationType();
        Assert.IsNotNull(MemberType(formation, "Team"), "Formation.Team is gone.");
        Assert.IsNotNull(MemberType(formation, "CountOfUnits"), "Formation.CountOfUnits is gone.");
        Assert.IsNotNull(MemberType(formation, "CurrentPosition"), "Formation.CurrentPosition is gone.");
        Assert.IsNotNull(MemberType(formation, "FormationIndex"), "Formation.FormationIndex is gone.");

        // The gate that keeps the machine off delegated / enlisted / dead-player formations.
        var aiControlled = MemberType(formation, "IsAIControlled");
        Assert.IsNotNull(aiControlled, "Formation.IsAIControlled is gone; the player-only gate has nothing to read.");
        Assert.AreEqual(typeof(bool), aiControlled, "Formation.IsAIControlled is no longer a bool.");

        Assert.IsNotNull(MemberType(MovementOrderType(), "TargetFormation"), "MovementOrder.TargetFormation is gone.");

        var mission = MissionType();
        Assert.IsNotNull(MemberType(mission, "PlayerTeam"), "Mission.PlayerTeam is gone.");
        Assert.IsNotNull(MemberType(mission, "CurrentTime"), "Mission.CurrentTime is gone.");
        Assert.IsNotNull(MemberType(mission, "IsFieldBattle"), "Mission.IsFieldBattle is gone; the open-field gate has nothing to read.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void AdapterEngineMembers_StillExist()
    {
        RequireGame();

        var formation = FormationType();

        // FormationAdapter.IsAligned: distance from each rider to its arrangement slot, read from
        // the arrangement itself so MixedFormations' Patch30 prefix on GetOrderPositionOfUnit and
        // the engine's off-navmesh fallback to the rider's own position are both bypassed.
        var arrangementType = Resolve("TaleWorlds.MountAndBlade.IFormationArrangement");
        var unitType = Resolve("TaleWorlds.MountAndBlade.IFormationUnit");
        Assert.AreEqual(arrangementType, MemberType(formation, "Arrangement"), "Formation.Arrangement is gone or no longer an IFormationArrangement.");
        var slot = AccessTools.Method(arrangementType, "GetWorldPositionOfUnitOrDefault", new[] { unitType });
        Assert.IsNotNull(slot, "IFormationArrangement.GetWorldPositionOfUnitOrDefault(IFormationUnit) is gone; the alignment metric has no slot to measure against.");
        Assert.AreEqual(typeof(Nullable<>).MakeGenericType(WorldPositionType()), slot.ReturnType, "GetWorldPositionOfUnitOrDefault no longer returns WorldPosition?.");
        Assert.IsNotNull(MemberType(formation, "UnitsWithoutLooseDetachedOnes"), "Formation.UnitsWithoutLooseDetachedOnes is gone.");

        // Patch31's prefix/postfix pair: the previous order, and the engine's own "same order"
        // test used to tell a re-issue from a new order.
        var readonlyRef = AccessTools.Method(formation, "GetReadonlyMovementOrderReference");
        Assert.IsNotNull(readonlyRef, "Formation.GetReadonlyMovementOrderReference is gone; the prefix cannot capture the previous order.");
        var practicallySame = AccessTools.Method(MovementOrderType(), "AreOrdersPracticallySame",
            new[] { MovementOrderType(), MovementOrderType(), typeof(bool) });
        Assert.IsNotNull(practicallySame, "MovementOrder.AreOrdersPracticallySame(MovementOrder, MovementOrder, bool) is gone; the re-issue test has no engine backing.");

        // Patch31's re-issue test reads the Move position through the public accessor.
        Assert.IsNotNull(AccessTools.Method(MovementOrderType(), "GetPosition", new[] { formation }), "MovementOrder.GetPosition(Formation) is gone; the re-issue test cannot compare Move positions.");

        // CavalryCommandAdapter.IssueChargeToTarget re-sets the native target after the order,
        // because SetMovementOrder clears it; Patch31b postfixes the same setter by name.
        var setTarget = AccessTools.Method(formation, "SetTargetFormation", new[] { formation });
        Assert.IsNotNull(setTarget, "Formation.SetTargetFormation(Formation) is gone; riders would charge with target index -1 and Patch31b has no target.");
        Assert.IsTrue(setTarget.IsPublic, "Formation.SetTargetFormation is no longer public.");
        CollectionAssert.AreEqual(new[] { "targetFormation" }, setTarget.GetParameters().Select(p => p.Name).ToArray(),
            "SetTargetFormation's parameter was renamed; Patch31b binds it by name and would receive null.");
        Assert.AreEqual(1, formation.GetMethods(AccessTools.all).Count(m => m.Name == "SetTargetFormation"), "SetTargetFormation gained an overload; re-check Patch31b's explicit parameter type array.");
        Assert.IsNotNull(MemberType(formation, "TargetFormation"), "Formation.TargetFormation is gone.");

        // CavalryCommandAdapter.ApplyChargeLine installs a facing order so Formation.Tick keeps the line's direction.
        var facingOrderType = Resolve("TaleWorlds.MountAndBlade.FacingOrder");
        Assert.IsNotNull(AccessTools.Method(formation, "SetFacingOrder", new[] { facingOrderType }), "Formation.SetFacingOrder(FacingOrder) is gone.");
        var lookAt = AccessTools.Method(facingOrderType, "FacingOrderLookAtDirection", new[] { typeof(TaleWorlds.Library.Vec2) });
        Assert.IsNotNull(lookAt, "FacingOrder.FacingOrderLookAtDirection(Vec2) is gone; the reform line would face the deployment direction.");
        Assert.IsTrue(lookAt.IsStatic, "FacingOrderLookAtDirection is no longer static.");

        // BattlefieldQueryAdapter.TryGetNearestEnemyFormation (moved here from the postfix body).
        var teamType = Resolve("TaleWorlds.MountAndBlade.Team");
        Assert.IsNotNull(AccessTools.Method(teamType, "IsFriendOf", new[] { teamType }), "Team.IsFriendOf(Team) is gone.");
        Assert.IsNotNull(MemberType(teamType, "FormationsIncludingEmpty"), "Team.FormationsIncludingEmpty is gone.");
        Assert.IsNotNull(MemberType(MissionType(), "Teams"), "Mission.Teams is gone.");

        // CavalryCommandAdapter: the orders the machine issues.
        var positioning = AccessTools.Method(formation, "SetPositioning");
        Assert.IsNotNull(positioning, "Formation.SetPositioning is gone.");
        Assert.AreEqual(3, positioning.GetParameters().Length, "Formation.SetPositioning changed arity (position, direction, unitSpacing).");

        var movementOrder = MovementOrderType();
        foreach (var name in new[] { "MovementOrderCharge", "MovementOrderStop" })
        {
            var field = AccessTools.Field(movementOrder, name);
            Assert.IsNotNull(field, "MovementOrder." + name + " is gone.");
            Assert.IsTrue(field.IsStatic, "MovementOrder." + name + " is no longer static.");
            Assert.AreEqual(movementOrder, field.FieldType, "MovementOrder." + name + " is no longer a MovementOrder.");
        }
        var move = AccessTools.Method(movementOrder, "MovementOrderMove", new[] { WorldPositionType() });
        Assert.IsNotNull(move, "MovementOrder.MovementOrderMove(WorldPosition) is gone; nothing can form a line.");
        Assert.IsTrue(move.IsStatic, "MovementOrder.MovementOrderMove is no longer static.");
        var chargeTo = AccessTools.Method(movementOrder, "MovementOrderChargeToTarget", new[] { formation });
        Assert.IsNotNull(chargeTo, "MovementOrder.MovementOrderChargeToTarget(Formation) is gone.");

        // FormationAdapter.RepresentativeIsCavalry.
        var querySystem = Resolve("TaleWorlds.MountAndBlade.FormationQuerySystem");
        Assert.IsNotNull(MemberType(querySystem, "IsCavalryFormation"), "FormationQuerySystem.IsCavalryFormation is gone.");
    }
}
