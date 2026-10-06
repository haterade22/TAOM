using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Infrastructure;
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
    private const string ModelSource = "Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs";

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
        // SignatureStrikes (#605, 2026-09-16): a signature hero's side swing knocks the struck
        // agent back; vanilla never knocks back an ordinary swing (only a crush-through one, and never
        // one from a CanKnockDown weapon such as Sauron's mace), so this is the one place that verdict
        // can come from for a signature side swing. Every non-signature case falls through to base.
        "DecideAgentKnockedBackByBlow",
        "DecideMissileWeaponFlags",
        "CalculateShieldDamage",
        "GetHorseChargePenetration",
        // RaceAbilities (2026-10-04): a live ability's melee damage, after the career amplification the
        // parent model applies. The Custom Battle damage model carries the same call.
        "ApplyDamageAmplifications",
        // CreatureSiegeRole (2026-10-05): a creature's melee blow on a castle gate is multiplied, after the engine's own
        // scaling (base runs first). The Custom Battle damage model carries the same call.
        "ApplyDamageScaling",
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

    // ADR-002 caps an entry point at 150 lines (#737). Every feature's damage seam lands in this one slot, so each keeps
    // its glue in its own Hooks/ facade and adds a delegate line here.
    [TestMethod]
    public void ModelSource_StaysUnderTheEntryPointCeiling()
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath(ModelSource.Split('/'))).Length;

        Assert.IsTrue(lines < 150,
            $"TaomCombatMechanicsModel.cs is {lines} lines, over ADR-002's 150 for an entry point. Put the new seam's glue in its feature's Hooks/ facade and leave one delegate line here.");
    }

    // A live Agent cannot be built outside the game, so the facades' tests cannot see two agents swapped, the verdict flag
    // flipped, or the raw damage passed where base's result belongs (the refuge's origin choice is RefugeDamageHooksTests'). Each call
    // the model makes into a feature hook where such a mistake would still compile is pinned here, except
    // CreatureSiegeHooks.ScaleGateDamage, whose base-first shape CreatureSiegeRoleWiringTests pins in the IL. A base call is
    // pinned only where it is a hook's damage argument (ApplyDamageAmplifications, CalculateShieldDamage); the others hand
    // the override's own parameters through in order; the context builders inside
    // CombatMechanicsHooks moved byte-identical from HEAD (docs/reviews/rca-combat-mechanics-model-split-2026-10-05.md).
    [DataTestMethod]
    [DataRow("ApplyDamageReductions", "CreatureBandits.Hooks.CreatureBanditDamage.Reduce(in attackInformation, in collisionData, result)")]
    [DataRow("ApplyDamageReductions", "RaceAbilityHooks.ReduceDamage(in attackInformation, in collisionData, result)")]
    [DataRow("ApplyDamageAmplifications", "base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage)")]
    [DataRow("DecideCrushedThrough", "RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType, isPassiveUsageHit)")]
    [DataRow("ApplyDamageReductions", "RefugeDamageHooks.Reduce(_refugeDefense, in attackInformation, result)")]
    [DataRow("CalculateShieldDamage", "_combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage))")]
    [DataRow("DecideCrushedThrough", "_combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)")]
    [DataRow("CalculateRemainingMomentum", "_combat.CleaveMomentum(attacker, originalMomentum, in collisionData)")]
    [DataRow("DecideWeaponCollisionReaction", "_combat.CollisionReaction(attacker, momentumRemaining, in collisionData, colReaction)")]
    [DataRow("DecideAgentKnockedDownByBlow", "_combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)")]
    [DataRow("DecideAgentKnockedDownByBlow", "SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: true)")]
    [DataRow("DecideAgentKnockedBackByBlow", "SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: false)")]
    public void ModelSource_HandsEachSeamItsOwnAgents(string overrideName, string call)
    {
        StringAssert.Contains(OverrideText(overrideName), call, $"{overrideName} must call {call}");
    }

    // From the override's name to the next override, comments blanked. A base call reads ".Name(", so " Name(" finds the
    // declaration.
    private static string OverrideText(string name)
    {
        var source = RepoPaths.ReadSource(ModelSource, stripComments: true);
        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"TaomCombatMechanicsModel no longer declares {name}");
        var end = source.IndexOf("public override", start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }
}
