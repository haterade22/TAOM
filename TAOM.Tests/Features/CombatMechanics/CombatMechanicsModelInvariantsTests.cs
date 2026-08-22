using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CombatMechanics;

// Reflection-pins for the one-slot AgentApplyDamageModel composition (LotrIssueTemplateInvariants
// precedent): the engine holds a single AgentApplyDamageModel, so career passives survive ONLY
// while TaomCombatMechanicsModel derives from TaomAgentApplyDamageModel. A refactor that rebases
// the model onto SandboxAgentApplyDamageModel (or un-abstracts the parent back into discovery
// without a registration) must fail here, not in-game.
//
// Touching typeof(TaomCombatMechanicsModel) forces its base chain (SandBox.dll) to load, so these
// run under the BindingVerification harness like GameModelOverrideBindingTests — Inconclusive
// when game assemblies aren't available.
[TestClass]
public class CombatMechanicsModelInvariantsTests
{
    private const string ParentFullName = "TAOM.Features.CareerSystem.Models.TaomAgentApplyDamageModel";
    private const string ModelFullName = "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel";

    private static readonly string[] ExpectedOverrides =
    {
        // Refuge (#507, 2026-08-22): defender damage reduction rides the model chain here rather
        // than the source module's Harmony postfix on the same method; the auto-resolve half lives
        // in TaomCombatSimulationModel.SimulateHit, both consulting IRefugeDefenseService.
        "ApplyDamageReductions",
        "DecideCrushedThrough",
        "CalculateRemainingMomentum",
        "DecideWeaponCollisionReaction",
        "DecideAgentShrugOffBlow",
        "CalculateStaggerThresholdDamage",
        "DecideAgentKnockedDownByBlow",
        "DecideMissileWeaponFlags",
        "CalculateShieldDamage",
        "GetHorseChargePenetration",
    };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    // Types resolved by name AFTER the game-assembly guard — a typeof() operand would trigger the
    // SandBox.dll load at JIT time, before the Inconclusive guard can run.
    private static Type Parent => typeof(TAOM.IoC).Assembly.GetType(ParentFullName, throwOnError: true);
    private static Type Model => typeof(TAOM.IoC).Assembly.GetType(ModelFullName, throwOnError: true);

    private void RequireGameAssemblies()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Model_DerivesFromCareerDamageModel()
    {
        RequireGameAssemblies();

        Assert.IsTrue(Parent.IsAssignableFrom(Model),
            "TaomCombatMechanicsModel must derive from TaomAgentApplyDamageModel — career damage passives ride the single AgentApplyDamageModel slot via inheritance.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ParentModel_StaysAbstract()
    {
        RequireGameAssemblies();

        Assert.IsTrue(Parent.IsAbstract,
            "TaomAgentApplyDamageModel must stay abstract: it is registered only via a derived model, and GameModelOverrideBindingTests only exempts abstract models from the registration gate.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Model_DeclaresExactlyTheExpectedOverrides()
    {
        RequireGameAssemblies();

        var declared = Model
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.IsVirtual && m.GetBaseDefinition().DeclaringType != Model)
            .Select(m => m.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        var expected = ExpectedOverrides.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEqual(expected, declared,
            $"Override set drifted. Declared: [{string.Join(", ", declared)}] — update ExpectedOverrides deliberately if a new mechanic landed.");
    }
}
