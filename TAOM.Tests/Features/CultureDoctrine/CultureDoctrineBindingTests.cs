using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureDoctrine.Domain;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CultureDoctrine;

/// <summary>
/// Drift guards for the culture-doctrine feature (#608) against the installed engine. The feature
/// carries no Harmony patch; its whole engine contract is inheritance and public calls, and every
/// one of them is pinned here because a rename would surface as a JIT-time resolution failure on
/// the async AI thread, past any try/catch the feature owns.
///
/// - The nine vanilla field tactics must stay public, unsealed, constructible from a <c>Team</c>
///   and overriding <c>GetTacticWeight</c>: the doctrine wrappers subclass them.
/// - <c>Team</c> must keep its public tactic-list methods and <c>TeamAIComponent</c> its
///   <c>_currentTactic</c> field (the one reflection read, for the status line).
/// - <c>MissionCombatantsLogic</c> must keep the public combatant accessors the doctrine resolves
///   each side's culture and Tactics skill from.
/// - The behaviour types and parameter fields the Phase B plans set must exist, and
///   <c>TacticalPosition</c> must keep its runtime constructor.
/// </summary>
[TestClass]
public class CultureDoctrineBindingTests
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

    private static Type Mb(string simpleName) => Resolve("TaleWorlds.MountAndBlade." + simpleName);

    private static Type? MemberType(Type type, string name)
    {
        var property = AccessTools.Property(type, name);
        if (property != null) return property.PropertyType;
        var field = AccessTools.Field(type, name);
        return field?.FieldType;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VanillaFieldTactics_ArePublicUnsealedTeamConstructibleAndOverrideGetTacticWeight()
    {
        RequireGame();

        var team = Mb("Team");
        var tacticComponent = Mb("TacticComponent");
        var baseWeight = AccessTools.Method(tacticComponent, "GetTacticWeight");
        Assert.IsNotNull(baseWeight, "TacticComponent.GetTacticWeight is gone.");
        Assert.IsTrue(baseWeight.IsVirtual && baseWeight.IsFamilyOrAssembly,
            "TacticComponent.GetTacticWeight is no longer protected internal virtual; a subclass in TAOM's assembly could not override it.");

        foreach (var id in DoctrineTacticIds.All.Where(DoctrineTacticIds.IsVanilla))
        {
            var name = DoctrineTacticIds.EngineTypeName(id);
            var type = Mb(name);
            Assert.IsTrue(type.IsPublic && !type.IsSealed && !type.IsAbstract, name + " must stay public, unsealed and concrete for the doctrine wrapper.");
            Assert.IsTrue(tacticComponent.IsAssignableFrom(type), name + " no longer derives from TacticComponent.");
            Assert.IsNotNull(type.GetConstructor(new[] { team }), name + " lost its (Team) constructor.");

            var weight = type.GetMethod("GetTacticWeight", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.IsNotNull(weight, name + " no longer overrides GetTacticWeight; a multiplier on the base would scale 0.");
            Assert.AreEqual(typeof(float), weight!.ReturnType);
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TacticCharge_IsTheTypeMakeDecisionFallsBackTo()
    {
        RequireGame();

        // TeamAIComponent.MakeDecision: `item is TacticCharge` when no enemy formation remains
        // (TeamAIComponent.cs:280). The doctrine's Charge wrapper derives from it, so it qualifies.
        var charge = Mb("TacticCharge");
        var makeDecision = AccessTools.Method(Mb("TeamAIComponent"), "MakeDecision");
        Assert.IsNotNull(makeDecision, "TeamAIComponent.MakeDecision is gone.");
        Assert.IsTrue(PatchProcessor.ReadMethodBody(makeDecision).Any(i => Equals(i.Value, charge)),
            "MakeDecision no longer references TacticCharge; re-read the forced-charge branch before trusting the wrapper.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Team_TacticListMethods_ArePublic()
    {
        RequireGame();

        var team = Mb("Team");
        var tacticComponent = Mb("TacticComponent");
        AssertPublicInstance(team, "AddTacticOption", tacticComponent);
        AssertPublicInstance(team, "ClearTacticOptions");
        AssertPublicInstance(team, "ResetTactic");
        Assert.IsNotNull(MemberType(team, "TeamAI"), "Team.TeamAI is gone.");
        Assert.IsNotNull(MemberType(team, "HasTeamAi"), "Team.HasTeamAi is gone.");
        Assert.IsNotNull(MemberType(team, "Side"), "Team.Side is gone.");
        Assert.IsNotNull(MemberType(team, "FormationsIncludingEmpty"), "Team.FormationsIncludingEmpty is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TeamAIComponent_CurrentTacticField_StillExists()
    {
        RequireGame();

        var field = AccessTools.Field(Mb("TeamAIComponent"), "_currentTactic");
        Assert.IsNotNull(field, "TeamAIComponent._currentTactic is gone; the status line has nothing to read.");
        Assert.AreEqual(Mb("TacticComponent"), field.FieldType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void MissionCombatantsLogic_PublicCombatantAccessors_StillExist()
    {
        RequireGame();

        var logic = Mb("MissionCombatantsLogic");
        var combatant = Resolve("TaleWorlds.Core.IBattleCombatant");
        AssertPublicInstance(logic, "GetAllCombatants");
        AssertPublicInstance(logic, "GetCultureForPlayerSide");
        AssertPublicInstance(logic, "SupportsAllyTeamOnPlayerSide", combatant.MakeByRefType());

        Assert.IsNotNull(AccessTools.Method(combatant, "GetNumberOfMissionReadyTroops"), "IBattleCombatant.GetNumberOfMissionReadyTroops is gone.");
        Assert.IsNotNull(AccessTools.Method(combatant, "GetTacticsSkillAmount"), "IBattleCombatant.GetTacticsSkillAmount is gone.");
        Assert.IsNotNull(MemberType(combatant, "BasicCulture"), "IBattleCombatant.BasicCulture is gone.");
        Assert.IsNotNull(MemberType(combatant, "Side"), "IBattleCombatant.Side is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CaravanTacticsHandler_StillExistsUnderTheNameTheRuleMatches()
    {
        RequireGame();

        var handler = Resolve("SandBox.Missions.MissionLogics." + TAOM.Features.CultureDoctrine.Hooks.CaravanTacticsRule.HandlerTypeName);
        Assert.IsTrue(Mb("MissionLogic").IsAssignableFrom(handler), "the caravan handler is no longer a MissionLogic; the by-name presence test would still match, re-read its EarlyStart.");
        Assert.IsNotNull(AccessTools.Method(handler, "EarlyStart"), "MissionCaravanOrVillagerTacticsHandler.EarlyStart is gone; re-read where vanilla adds the caravan DefensiveLine.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Mission_Members_StillExist()
    {
        RequireGame();

        var mission = Mb("Mission");
        Assert.AreEqual(typeof(bool), MemberType(mission, "IsFieldBattle"), "Mission.IsFieldBattle is gone or no longer a bool; the field-battle gate has nothing to read.");
        Assert.IsNotNull(MemberType(mission, "Teams"), "Mission.Teams is gone.");
        Assert.IsNotNull(MemberType(mission, "PlayerTeam"), "Mission.PlayerTeam is gone.");
        Assert.IsNotNull(MemberType(mission, "PlayerAllyTeam"), "Mission.PlayerAllyTeam is gone.");
        // Phase D's formation-routing seam; pinned now so its disappearance is noticed early.
        Assert.IsNotNull(mission.GetEvent("GetAgentTroopClass_Override"), "Mission.GetAgentTroopClass_Override event is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TacticComponent_ProtectedSurface_TheTaomTacticsBuildOn()
    {
        RequireGame();

        var tacticComponent = Mb("TacticComponent");
        var formation = Mb("Formation");
        foreach (var field in new[] { "_mainInfantry", "_archers", "_leftCavalry", "_rightCavalry", "_rangedCavalry" })
        {
            var f = AccessTools.Field(tacticComponent, field);
            Assert.IsNotNull(f, "TacticComponent." + field + " is gone.");
            Assert.IsTrue(f.IsFamily, "TacticComponent." + field + " is no longer protected.");
            Assert.AreEqual(formation, f.FieldType);
        }
        var reapply = AccessTools.Field(tacticComponent, "IsTacticReapplyNeeded");
        Assert.IsNotNull(reapply, "TacticComponent.IsTacticReapplyNeeded is gone.");
        Assert.IsTrue(reapply.IsFamily);

        var created = AccessTools.Property(tacticComponent, "AreFormationsCreated");
        Assert.IsNotNull(created, "TacticComponent.AreFormationsCreated is gone.");
        Assert.IsTrue(created.GetMethod.IsFamily, "TacticComponent.AreFormationsCreated is no longer protected.");

        AssertFamilyInstance(tacticComponent, "AssignTacticFormations1121");
        AssertFamilyInstance(tacticComponent, "ManageFormationCounts", typeof(int), typeof(int), typeof(int), typeof(int));
        var manage = AccessTools.Method(tacticComponent, "ManageFormationCounts", Type.EmptyTypes);
        Assert.IsNotNull(manage, "TacticComponent.ManageFormationCounts() is gone.");
        Assert.IsTrue(manage.IsVirtual && manage.IsFamily, "ManageFormationCounts() is no longer protected virtual.");

        var tick = AccessTools.Method(tacticComponent, "TickOccasionally");
        Assert.IsTrue(tick != null && tick.IsPublic && tick.IsVirtual, "TacticComponent.TickOccasionally is no longer public virtual.");
        var defaults = AccessTools.Method(tacticComponent, "SetDefaultBehaviorWeights", new[] { formation });
        Assert.IsTrue(defaults != null && defaults.IsPublic && defaults.IsStatic, "TacticComponent.SetDefaultBehaviorWeights(Formation) is no longer public static.");
        var advantage = AccessTools.Method(tacticComponent, "CalculateNotEngagingTacticalAdvantage", new[] { Mb("TeamQuerySystem") });
        Assert.IsTrue(advantage != null && advantage.IsStatic && advantage.IsFamily, "TacticComponent.CalculateNotEngagingTacticalAdvantage(TeamQuerySystem) is no longer protected static.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void FormationAI_BehaviourWeightSurface_StillExists()
    {
        RequireGame();

        var ai = Mb("FormationAI");
        var behaviorComponent = Mb("BehaviorComponent");
        var set = AccessTools.Method(ai, "SetBehaviorWeight");
        Assert.IsTrue(set != null && set.IsPublic && set.IsGenericMethodDefinition, "FormationAI.SetBehaviorWeight<T> is gone.");
        var get = AccessTools.Method(ai, "GetBehavior");
        Assert.IsTrue(get != null && get.IsPublic && get.IsGenericMethodDefinition, "FormationAI.GetBehavior<T> is gone.");
        AssertPublicInstance(ai, "ResetBehaviorWeights");
        AssertPublicInstance(ai, "AddAiBehavior", behaviorComponent);
        Assert.AreEqual(behaviorComponent, MemberType(ai, "ActiveBehavior"), "FormationAI.ActiveBehavior is gone or changed type.");
        Assert.IsNotNull(MemberType(ai, "Side"), "FormationAI.Side is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlanBehaviourTypes_ExistAndDeriveFromBehaviorComponent()
    {
        RequireGame();

        var behaviorComponent = Mb("BehaviorComponent");
        foreach (var name in new[]
        {
            "BehaviorCharge", "BehaviorTacticalCharge", "BehaviorAdvance", "BehaviorHoldHighGround", "BehaviorDefend",
            "BehaviorDefensiveRing", "BehaviorFireFromInfantryCover", "BehaviorSkirmish", "BehaviorSkirmishLine",
            "BehaviorScreenedSkirmish", "BehaviorProtectFlank", "BehaviorCavalryScreen", "BehaviorFlank", "BehaviorVanguard",
            "BehaviorHorseArcherSkirmish", "BehaviorMountedSkirmish", "BehaviorPullBack", "BehaviorRegroup", "BehaviorReserve",
            "BehaviorRetreat", "BehaviorStop",
        })
        {
            var type = Mb(name);
            Assert.IsTrue(behaviorComponent.IsAssignableFrom(type), name + " no longer derives from BehaviorComponent.");
        }
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void PlanBehaviourParameterFields_ArePublicAndTyped()
    {
        RequireGame();

        var formation = Mb("Formation");
        var tacticalPosition = Mb("TacticalPosition");
        var side = AccessTools.Inner(Mb("FormationAI"), "BehaviorSide");
        Assert.IsNotNull(side, "FormationAI.BehaviorSide is gone.");

        AssertPublicField(Mb("BehaviorHoldHighGround"), "RangedAllyFormation", formation);
        AssertPublicField(Mb("BehaviorProtectFlank"), "FlankSide", side);
        AssertPublicField(Mb("BehaviorDefend"), "TacticalDefendPosition", tacticalPosition);
        AssertPublicField(Mb("BehaviorDefend"), "DefensePosition", Resolve("TaleWorlds.Engine.WorldPosition"));
        AssertPublicField(Mb("BehaviorDefensiveRing"), "TacticalDefendPosition", tacticalPosition);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TacticalPosition_RuntimeConstructor_StillExists()
    {
        RequireGame();

        var tacticalPosition = Mb("TacticalPosition");
        var ctor = tacticalPosition.GetConstructors().FirstOrDefault(c =>
        {
            var p = c.GetParameters();
            return p.Length == 7
                && p[0].ParameterType == Resolve("TaleWorlds.Engine.WorldPosition")
                && p[1].ParameterType == Resolve("TaleWorlds.Library.Vec2")
                && p[2].ParameterType == typeof(float)
                && p[3].ParameterType == typeof(float)
                && p[4].ParameterType == typeof(bool)
                && p[4].Name == "isInsurmountable";
        });
        Assert.IsNotNull(ctor, "TacticalPosition(WorldPosition, Vec2, float width, float slope, bool isInsurmountable, ...) is gone; the archer ring has no position to hand BehaviorDefensiveRing.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void QuerySystemMembers_TheWeightSnapshotsRead_StillExist()
    {
        RequireGame();

        var team = Mb("TeamQuerySystem");
        foreach (var name in new[]
        {
            "MemberCount", "EnemyUnitCount", "InfantryRatio", "RangedRatio", "CavalryRatio", "RangedCavalryRatio",
            "RemainingPowerRatio", "EnemyRangedRatio", "EnemyRangedCavalryRatio",
        })
            Assert.IsNotNull(MemberType(team, name), "TeamQuerySystem." + name + " is gone.");

        var formation = Mb("FormationQuerySystem");
        foreach (var name in new[] { "HighGroundCloseToForeseenBattleGround", "IsInfantryFormation", "IsRangedFormation", "IsCavalryFormation", "HasShieldUnitRatio", "MovementSpeedMaximum" })
            Assert.IsNotNull(MemberType(formation, name), "FormationQuerySystem." + name + " is gone.");
    }

    private static void AssertPublicInstance(Type type, string name, params Type[] parameters)
    {
        var method = AccessTools.Method(type, name, parameters);
        Assert.IsNotNull(method, type.Name + "." + name + " is gone.");
        Assert.IsTrue(method.IsPublic && !method.IsStatic, type.Name + "." + name + " is no longer a public instance method.");
    }

    private static void AssertFamilyInstance(Type type, string name, params Type[] parameters)
    {
        var method = AccessTools.Method(type, name, parameters);
        Assert.IsNotNull(method, type.Name + "." + name + " is gone.");
        Assert.IsTrue(method.IsFamily && !method.IsStatic, type.Name + "." + name + " is no longer a protected instance method.");
    }

    private static void AssertPublicField(Type type, string name, Type fieldType)
    {
        var field = AccessTools.Field(type, name);
        Assert.IsNotNull(field, type.Name + "." + name + " is gone.");
        Assert.IsTrue(field.IsPublic, type.Name + "." + name + " is no longer public; the plan applier cannot set it.");
        Assert.AreEqual(fieldType, field.FieldType, type.Name + "." + name + " changed type.");
    }
}
