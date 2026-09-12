using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Reflection pins for the one seam that keeps the Order of Battle screen shut for an enlisted
/// soldier (#576). The engine's <c>BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle()</c>
/// is NON-virtual and caches the answer once per mission; only the protected
/// <c>CanPlayerSideDeployWithOrderOfBattleAux()</c> is overridable, and every deployment reader
/// (<c>DeploymentMissionController.SetupTeams</c>, <c>AssignPlayerRoleInTeamMissionController.OnPlayerTeamDeployed</c>,
/// <c>GeneralsAndCaptainsAssignmentLogic.OnTeamDeployed</c>) goes through that one cached bool.
/// If either half of that shape drifts on an engine bump, the model silently stops closing the
/// screen and the captain slot is back. These fail offline instead.
///
/// Types are resolved by name after the game-assembly guard: a typeof() on the model would load
/// SandBox.dll at JIT time, before the Inconclusive guard can run (CombatMechanicsModelInvariantsTests
/// precedent).
/// </summary>
[TestClass]
public class TaomBattleInitializationModelInvariantsTests
{
    private const string ModelFullName = "TAOM.Features.Enlistment.Models.TaomBattleInitializationModel";
    private const string SandboxModelFullName = "SandBox.GameComponents.SandboxBattleInitializationModel";
    private const string BaseModelFullName = "TaleWorlds.MountAndBlade.ComponentInterfaces.BattleInitializationModel";
    private const string AuxName = "CanPlayerSideDeployWithOrderOfBattleAux";
    private const string GateName = "CanPlayerSideDeployWithOrderOfBattle";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static Type Model => typeof(TAOM.IoC).Assembly.GetType(ModelFullName, throwOnError: true);

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Model_DerivesFromSandboxBattleInitializationModel()
    {
        RequireGame();
        var sandbox = AccessTools.TypeByName(SandboxModelFullName);
        Assert.IsNotNull(sandbox, "SandboxBattleInitializationModel did not resolve in the installed engine.");

        Assert.IsTrue(sandbox!.IsAssignableFrom(Model),
            "TaomBattleInitializationModel must derive from SandboxBattleInitializationModel so GetAllAvailableTroopTypes and the sally-out rule stay vanilla.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Model_DeclaresOnlyTheAuxOverride()
    {
        RequireGame();

        var declared = Model
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        Assert.AreEqual(1, declared.Count,
            $"The model must declare exactly one method. Declared: [{string.Join(", ", declared.Select(m => m.Name))}]");

        var aux = declared[0];
        Assert.AreEqual(AuxName, aux.Name);
        Assert.IsTrue(aux.IsFamily && aux.IsVirtual, "The Aux override must stay protected and virtual (an override).");
        Assert.AreNotEqual(Model, aux.GetBaseDefinition().DeclaringType, "The Aux method must be a real override, not a shadow the engine never calls.");
        Assert.AreEqual(typeof(bool), aux.ReturnType);
        Assert.AreEqual(0, aux.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Engine_CanPlayerSideDeployWithOrderOfBattle_IsPublicNonVirtualParameterless()
    {
        RequireGame();
        var baseModel = AccessTools.TypeByName(BaseModelFullName);
        Assert.IsNotNull(baseModel, "BattleInitializationModel did not resolve in the installed engine.");

        var gate = baseModel!.GetMethod(GateName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(gate, "BattleInitializationModel.CanPlayerSideDeployWithOrderOfBattle is gone; the deployment readers no longer share one cached gate.");
        Assert.IsFalse(gate!.IsVirtual,
            "CanPlayerSideDeployWithOrderOfBattle turned virtual. The per-mission cache lives in this wrapper; re-evaluate whether the enlisted gate still belongs on the Aux method.");
        Assert.AreEqual(typeof(bool), gate.ReturnType);
        Assert.AreEqual(0, gate.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Engine_Aux_IsProtectedVirtualOnTheSandboxModel()
    {
        RequireGame();
        var sandbox = AccessTools.TypeByName(SandboxModelFullName);
        Assert.IsNotNull(sandbox);

        var aux = sandbox!.GetMethod(AuxName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(aux, "SandboxBattleInitializationModel no longer declares CanPlayerSideDeployWithOrderOfBattleAux; the override would bind to nothing.");
        Assert.IsTrue(aux!.IsFamily && aux.IsVirtual, "The engine Aux must be protected virtual for TaomBattleInitializationModel to override it.");
        Assert.AreEqual(typeof(bool), aux.ReturnType);
        Assert.AreEqual(0, aux.GetParameters().Length);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Engine_GetAllAvailableTroopTypes_IsPublic_AndLeftToVanilla()
    {
        RequireGame();
        var sandbox = AccessTools.TypeByName(SandboxModelFullName);
        Assert.IsNotNull(sandbox);

        var troopTypes = sandbox!.GetMethod("GetAllAvailableTroopTypes", BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(troopTypes, "SandboxBattleInitializationModel.GetAllAvailableTroopTypes is gone.");

        var ours = Model.GetMethod("GetAllAvailableTroopTypes", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNull(ours, "The model must not override GetAllAvailableTroopTypes; the enlisted gate is the only thing it changes.");
    }
}
