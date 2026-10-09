using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.CombatMechanics;
using TAOM.Features.CombatMechanics.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

namespace TAOM.Tests.Features.CombatMechanics;

/// <summary>
/// Custom Battle runs Combat Mechanics (#788). The campaign's damage model derives from SandBox's, which throws on a mounted
/// hit outside a campaign (<c>SandboxAgentApplyDamageModel</c> reads <c>Campaign.Current</c>), so Custom Battle gets a twin
/// on the engine's own Custom Battle base. Nothing fails at build time if the twin loses a rule or is not added, so each
/// piece is pinned: the base, the override set, the order of calls (the same hooks the campaign model calls, in its order),
/// and the one registration on a <c>BasicGameStarter</c>. The source pins run everywhere; the reflection and IL pins need the
/// installed game and are Inconclusive without it. Types are resolved by name after the guard, the
/// CombatMechanicsModelInvariantsTests shape.
/// </summary>
[TestClass]
public class CustomBattleDamageModelTests
{
    private const string TwinFullName = "TAOM.Features.CombatMechanics.Models.TaomCustomBattleDamageModel";
    private const string CampaignFullName = "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel";
    private const string TwinSource = "Main/Features/CombatMechanics/Models/TaomCustomBattleDamageModel.cs";
    private const string CampaignSource = "Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs";
    private const string Registration = "RegisterCustomBattleModels";

    private static readonly string[] ExpectedOverrides =
    {
        "ApplyDamageReductions",
        "ApplyDamageAmplifications",
        "ApplyDamageScaling",
        "DecideCrushedThrough",
        "CalculateRemainingMomentum",
        "DecideWeaponCollisionReaction",
        "DecideAgentShrugOffBlow",
        "CalculateStaggerThresholdDamage",
        "DecideAgentKnockedDownByBlow",
        "DecideAgentKnockedBackByBlow",
        "DecideMissileWeaponFlags",
        "CalculateShieldDamage",
        "GetHorseChargePenetration",
    };

    // The order each override calls the engine's base and TAOM's own code in, read off the IL. The campaign model makes the
    // same calls in the same order apart from CampaignOnly, so the twin test below compares the two.
    private static readonly Dictionary<string, string> ExpectedCallOrder = new()
    {
        ["ApplyDamageReductions"] = "base,CreatureBanditDamage.Reduce,RaceAbilityHooks.ReduceDamage",
        ["ApplyDamageAmplifications"] = "base,RaceAbilityHooks.AmplifyDamage",
        ["ApplyDamageScaling"] = "base,CreatureSiegeHooks.ScaleGateDamage",
        ["DecideCrushedThrough"] = "RaceAbilityHooks.CrushVerdict,CombatMechanicsHooks.CrushThrough,base",
        ["CalculateRemainingMomentum"] = "CombatMechanicsHooks.CleaveMomentum,base",
        ["DecideWeaponCollisionReaction"] = "base,CombatMechanicsHooks.CollisionReaction",
        ["DecideAgentShrugOffBlow"] = "base,CombatMechanicsHooks.IsUnstoppable,RaceAbilityHooks.ShrugsOff",
        ["CalculateStaggerThresholdDamage"] = "base,CombatMechanicsHooks.StaggerThreshold",
        ["DecideAgentKnockedDownByBlow"] = "SignatureStrikeVerdicts.Decide,CombatMechanicsHooks.ChargeKnockdown,base",
        ["DecideAgentKnockedBackByBlow"] = "SignatureStrikeVerdicts.Decide,base",
        ["DecideMissileWeaponFlags"] = "base,CombatMechanicsHooks.PenetrationFlags",
        ["CalculateShieldDamage"] = "base,CombatMechanicsHooks.ShieldDamage",
        ["GetHorseChargePenetration"] = "CombatMechanicsHooks.HorseChargePenetration,base",
    };

    // The TAOM calls the campaign model makes that Custom Battle must not: the Refuge reduction reads a party.
    private static readonly HashSet<string> CampaignOnly = new(StringComparer.Ordinal) { "RefugeDamageHooks.Reduce" };

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGameAssemblies()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static Type Twin => typeof(TAOM.IoC).Assembly.GetType(TwinFullName, throwOnError: true)!;
    private static Type Campaign => typeof(TAOM.IoC).Assembly.GetType(CampaignFullName, throwOnError: true)!;

    // --- source pins (run everywhere) ---------------------------------------------------------------------------------------

    [TestMethod]
    public void Source_DerivesFromTheCustomBattleBase_NeverFromSandBox()
    {
        var source = RepoPaths.ReadSource(TwinSource, stripComments: true);

        StringAssert.Contains(source, "public class TaomCustomBattleDamageModel : CustomAgentApplyDamageModel");
        Assert.IsFalse(source.Contains("Sandbox"), "the Sandbox model throws on a mounted hit outside a campaign (SandboxAgentApplyDamageModel :641-644)");
        Assert.IsFalse(source.Contains("TaomAgentApplyDamageModel"), "career passives read heroes; they stay campaign-only");
        Assert.IsFalse(source.Contains("Refuge"), "the Refuge reduction reads a party; it stays campaign-only");
    }

    // The campaign model's override set is the specification: an override added there without its twin (even a pass-through
    // that only adds a hook later) fails here, instead of Custom Battle quietly keeping the engine's answer.
    [TestMethod]
    public void Source_OverridesExactlyWhatTheCampaignModelOverrides()
    {
        var twin = DeclaredOverrides(TwinSource);
        var campaign = DeclaredOverrides(CampaignSource);

        CollectionAssert.AreEqual(campaign, twin, "the twin and the campaign model must override the same methods");
        CollectionAssert.AreEqual(campaign, ExpectedOverrides.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            "ExpectedOverrides is the list the reflection and IL rows run over; keep it equal to the campaign model's");
    }

    [TestMethod]
    public void Source_StaysUnderTheEntryPointCeiling()
    {
        var lines = File.ReadAllLines(RepoPaths.RepoPath(TwinSource.Split('/'))).Length;

        Assert.IsTrue(lines < 150, $"TaomCustomBattleDamageModel.cs is {lines} lines, over ADR-002's 150 for an entry point.");
    }

    [DataTestMethod]
    [DataRow("ApplyDamageReductions", "base.ApplyDamageReductions(in attackInformation, in collisionData, baseDamage)")]
    [DataRow("ApplyDamageReductions", "CreatureBanditDamage.Reduce(in attackInformation, in collisionData, result)")]
    [DataRow("ApplyDamageReductions", "RaceAbilityHooks.ReduceDamage(in attackInformation, in collisionData, result)")]
    [DataRow("ApplyDamageAmplifications", "RaceAbilityHooks.AmplifyDamage(in attackInformation, in collisionData,")]
    [DataRow("ApplyDamageAmplifications", "base.ApplyDamageAmplifications(in attackInformation, in collisionData, baseDamage)")]
    [DataRow("ApplyDamageScaling", "CreatureSiegeHooks.ScaleGateDamage(in attackInformation, in collisionData,")]
    [DataRow("ApplyDamageScaling", "base.ApplyDamageScaling(in attackInformation, in collisionData, baseDamage)")]
    [DataRow("DecideCrushedThrough", "RaceAbilityHooks.CrushVerdict(attackerAgent, defenderAgent, strikeType, isPassiveUsageHit)")]
    [DataRow("DecideCrushedThrough", "_combat.CrushThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)")]
    [DataRow("DecideCrushedThrough", "base.DecideCrushedThrough(attackerAgent, defenderAgent, totalAttackEnergy, attackDirection, strikeType, defendItem, isPassiveUsageHit)")]
    [DataRow("CalculateRemainingMomentum", "_combat.CleaveMomentum(attacker, originalMomentum, in collisionData)")]
    [DataRow("CalculateRemainingMomentum", "base.CalculateRemainingMomentum(originalMomentum, in b, in collisionData, attacker, victim, in attackerWeapon, isCrushThrough)")]
    [DataRow("DecideWeaponCollisionReaction", "base.DecideWeaponCollisionReaction(in registeredBlow, in collisionData, attacker, defender, in attackerWeapon, isFatalHit, isShruggedOff, momentumRemaining, out colReaction)")]
    [DataRow("DecideWeaponCollisionReaction", "_combat.CollisionReaction(attacker, momentumRemaining, in collisionData, colReaction)")]
    [DataRow("DecideAgentShrugOffBlow", "base.DecideAgentShrugOffBlow(victimAgent, in collisionData, in blow)")]
    [DataRow("DecideAgentShrugOffBlow", "_combat.IsUnstoppable(victimAgent, in collisionData)")]
    [DataRow("DecideAgentShrugOffBlow", "RaceAbilityHooks.ShrugsOff(victimAgent)")]
    [DataRow("CalculateStaggerThresholdDamage", "_combat.StaggerThreshold(defenderAgent, base.CalculateStaggerThresholdDamage(defenderAgent, in blow))")]
    [DataRow("DecideAgentKnockedDownByBlow", "SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: true)")]
    [DataRow("DecideAgentKnockedDownByBlow", "_combat.ChargeKnockdown(attackerAgent, victimAgent, in collisionData, in blow)")]
    [DataRow("DecideAgentKnockedDownByBlow", "base.DecideAgentKnockedDownByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow)")]
    [DataRow("DecideAgentKnockedBackByBlow", "SignatureStrikeVerdicts.Decide(_signatureStrikes, _signatureRoster, attackerAgent, victimAgent, in collisionData, in blow, knockdown: false)")]
    [DataRow("DecideAgentKnockedBackByBlow", "base.DecideAgentKnockedBackByBlow(attackerAgent, victimAgent, in collisionData, attackerWeapon, in blow)")]
    [DataRow("DecideMissileWeaponFlags", "base.DecideMissileWeaponFlags(attackerAgent, in missileWeapon, ref missileWeaponFlags)")]
    [DataRow("DecideMissileWeaponFlags", "_combat.PenetrationFlags(in missileWeapon, missileWeaponFlags)")]
    [DataRow("CalculateShieldDamage", "_combat.ShieldDamage(in attackInformation, base.CalculateShieldDamage(in attackInformation, baseDamage))")]
    [DataRow("GetHorseChargePenetration", "_combat.HorseChargePenetration() ?? base.GetHorseChargePenetration()")]
    public void Source_HandsEachSeamItsOwnAgents(string overrideName, string call)
    {
        StringAssert.Contains(OverrideText(overrideName), call, $"{overrideName} must call {call}");
    }

    [TestMethod]
    public void Source_AsksTheRaceVerdictBeforeTheCombatRules()
    {
        var text = OverrideText("DecideCrushedThrough");

        Assert.IsTrue(text.IndexOf("RaceAbilityHooks.CrushVerdict(", StringComparison.Ordinal)
                      < text.IndexOf("_combat.CrushThrough(", StringComparison.Ordinal),
            "a defender standing fast must hold before the troll or skill rules can crush through");
        Assert.IsTrue(text.IndexOf("_combat.CrushThrough(", StringComparison.Ordinal)
                      < text.IndexOf("base.DecideCrushedThrough(", StringComparison.Ordinal),
            "the combat rules decide before the engine's own flag-and-energy rule");
    }

    // --- registration ---------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void Registration_AddsTheTwinOnABasicGameStarter_AndNeverOnACampaignStarter()
    {
        var body = RegistrationBody();

        StringAssert.Contains(body, "gameStarterObject is CampaignGameStarter || !(gameStarterObject is BasicGameStarter basicStarter)");
        var guard = body.IndexOf("return;", StringComparison.Ordinal);
        var add = body.IndexOf("basicStarter.AddModel<AgentApplyDamageModel>(new TaomCustomBattleDamageModel(", StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0 && add > guard, "the twin must be added on the BasicGameStarter, after the campaign starter has returned");
        Assert.IsFalse(body.Contains("campaignStarter"), "a CampaignGameStarter never reaches this method");
        Assert.IsFalse(body.Contains("TaomCombatMechanicsModel"), "the Sandbox-derived campaign model throws on a mounted hit in Custom Battle");
    }

    [TestMethod]
    public void Registration_ResolvesTheSameCollaboratorsTheCampaignRegistrationDoes()
    {
        var body = RegistrationBody();
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var campaign = subModule.Substring(subModule.IndexOf("campaignStarter.AddModel<AgentApplyDamageModel>(new TaomCombatMechanicsModel(", StringComparison.Ordinal));
        campaign = campaign.Substring(0, campaign.IndexOf(");", StringComparison.Ordinal));

        foreach (var resolve in new[]
                 {
                     "IoC.Resolve<Features.CombatMechanics.Hooks.CombatMechanicsHooks>()",
                     "IoC.Resolve<Features.SignatureStrikes.ISignatureStrikeService>()",
                     "IoC.Resolve<Features.SignatureStrikes.Hooks.ISignatureAgentRoster>()",
                 })
        {
            StringAssert.Contains(campaign, resolve, "the campaign registration changed; mirror it");
            StringAssert.Contains(body, resolve);
        }
    }

    [TestMethod]
    public void SubModule_NamesTheTwinOnce_AndKeepsTheCampaignRegistration()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.AreEqual(1, Regex.Matches(subModule, @"\bTaomCustomBattleDamageModel\b").Count, "one registration, in RegisterCustomBattleModels");
        Assert.AreEqual(1, Regex.Matches(subModule, @"campaignStarter\.AddModel<AgentApplyDamageModel>\(new TaomCombatMechanicsModel\(").Count);
    }

    [TestMethod]
    public void RegisterCustomBattleModels_RunsOutsideTheCampaignBranch_BeforeTheModuleStep()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        var call = subModule.IndexOf(Registration + "(gameStarterObject);", StringComparison.Ordinal);
        var campaign = subModule.IndexOf("if (gameStarterObject is CampaignGameStarter campaignStarter)", StringComparison.Ordinal);
        var modules = subModule.IndexOf("FeatureModuleHooks.AddGameStartContent(gameStarterObject);", StringComparison.Ordinal);

        Assert.IsTrue(call >= 0 && call < campaign && campaign < modules, "the Custom Battle step must precede the campaign branch and the module step");
    }

    // --- reflection and IL (need the installed game) --------------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Type_DerivesFromTheEnginesCustomBattleModel_AndFromNoSandBoxType()
    {
        RequireGameAssemblies();

        Assert.AreEqual(typeof(CustomAgentApplyDamageModel), Twin.BaseType);
        Assert.IsTrue(Twin.IsPublic && !Twin.IsSealed && !Twin.IsAbstract);
        for (var b = Twin.BaseType; b != null; b = b.BaseType)
            Assert.IsFalse(b.FullName!.StartsWith("SandBox.", StringComparison.Ordinal), $"{b.FullName} would throw outside a campaign");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Type_DeclaresExactlyTheExpectedOverrides_EachOfAnEngineVirtual()
    {
        RequireGameAssemblies();

        var methods = Twin.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ToList();

        CollectionAssert.AreEqual(ExpectedOverrides.OrderBy(n => n, StringComparer.Ordinal).ToArray(),
            methods.Select(m => m.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        foreach (var method in methods)
            Assert.AreNotSame(method, method.GetBaseDefinition(), $"{method.Name} shadows the engine's method instead of overriding it");
    }

    [DataTestMethod]
    [TestCategory("BindingVerification")]
    [DynamicData(nameof(OverrideNames), DynamicDataSourceType.Method)]
    public void EachOverride_CallsTheEnginesBaseOnce_AndTheHooksInTheDocumentedOrder(string name)
    {
        RequireGameAssemblies();

        var sequence = string.Join(",", CallSequence(Twin, name));

        Assert.AreEqual(ExpectedCallOrder[name], sequence, $"{name}: the base and hook calls, in IL order");
    }

    [DataTestMethod]
    [TestCategory("BindingVerification")]
    [DynamicData(nameof(OverrideNames), DynamicDataSourceType.Method)]
    public void EachOverride_MakesTheSameHookCallsInTheSameOrder_AsTheCampaignModel(string name)
    {
        RequireGameAssemblies();

        var campaign = CallSequence(Campaign, name).Where(call => !CampaignOnly.Contains(call));

        Assert.AreEqual(string.Join(",", campaign), string.Join(",", CallSequence(Twin, name)),
            $"{name}: the twin must mirror the campaign model's calls and their order");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CallSequence_SeesACallIntoATaomServiceInterface_SoItCannotMissAHookItDoesNotKnow()
    {
        // Positive control for the classifier: the career parent's reduction step calls ICareerAgentStatService, a TAOM type
        // that no hand-written hook list would name. A classifier that skips it would also skip a new hook in either model.
        RequireGameAssemblies();
        var parent = typeof(TAOM.IoC).Assembly.GetType("TAOM.Features.CareerSystem.Models.TaomAgentApplyDamageModel", throwOnError: true)!;

        CollectionAssert.Contains(CallSequence(parent, "ApplyDamageReductions"), "ICareerAgentStatService.CalculateDamageReduction");
    }

    public static IEnumerable<object[]> OverrideNames() => ExpectedOverrides.Select(n => new object[] { n });

    // --- behaviour that needs no live agent ---------------------------------------------------------------------------------

    private static (object Model, ICombatMechanicsSettingsProvider Settings, ICreatureCombatService Creature) BuildTwin()
    {
        var settings = Substitute.For<ICombatMechanicsSettingsProvider>();
        var creature = Substitute.For<ICreatureCombatService>();
        var hooks = new CombatMechanicsHooks(Substitute.For<ICrushThroughService>(), Substitute.For<IChargeKnockdownService>(),
            creature, Substitute.For<IShieldPenetrationService>(), settings);
        return (Activator.CreateInstance(Twin, hooks, null, null)!, settings, creature);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void HorseChargePenetration_FeatureOn_IsTheSetting_AndOff_IsCustomBattlesOwn0Point4()
    {
        RequireGameAssemblies();
        var (model, settings, _) = BuildTwin();
        var agent = new AgentApplyDamageModelAccess(model);

        settings.ChargeKnockdownEnabled.Returns(true);
        settings.ChargeHorsePenetration.Returns(0.55f);
        Assert.AreEqual(0.55f, agent.HorseChargePenetration());

        settings.ChargeKnockdownEnabled.Returns(false);
        Assert.AreEqual(0.4f, agent.HorseChargePenetration(), "off falls through to the Custom Battle base");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void RemainingMomentum_WhenTheCreatureServiceAnswers_IsThatAnswer()
    {
        RequireGameAssemblies();
        var (model, _, creature) = BuildTwin();
        creature.CalculateCleaveMomentum(Arg.Any<string>(), Arg.Any<float>(), Arg.Any<bool>()).Returns(7.5f);

        var momentum = new AgentApplyDamageModelAccess(model).RemainingMomentum(3f);

        Assert.AreEqual(7.5f, momentum);
    }

    // --- helpers ----------------------------------------------------------------------------------------------------------------

    // The override's calls in IL order: "base" for the call to the base class's method of the same name, and
    // "Type.Method" for every call into TAOM's own assembly, whatever its namespace, so a hook nobody listed cannot be missed.
    private static List<string> CallSequence(Type model, string name)
    {
        var method = model.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(method, $"{model.Name} does not declare {name}");

        var sequence = new List<string>();
        foreach (var call in IlCallScanner.ExtractCalledMethods(method!, method!.GetMethodBody()!.GetILAsByteArray()!))
        {
            var type = call.DeclaringType!;
            if (call.Name == name && type != model && type.IsAssignableFrom(model))
                sequence.Add("base");
            else if (type.Assembly == model.Assembly)
                sequence.Add(type.Name + "." + call.Name);
        }

        return sequence;
    }

    private static string[] DeclaredOverrides(string path)
        => Regex.Matches(RepoPaths.ReadSource(path, stripComments: true), @"public override \S+ (\w+)\(").Cast<Match>()
            .Select(m => m.Groups[1].Value).OrderBy(n => n, StringComparer.Ordinal).ToArray();

    private static string OverrideText(string name)
    {
        var source = RepoPaths.ReadSource(TwinSource, stripComments: true);
        var start = source.IndexOf($" {name}(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, $"TaomCustomBattleDamageModel no longer declares {name}");
        var end = source.IndexOf("public override", start, StringComparison.Ordinal);
        return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
    }

    private static string RegistrationBody()
    {
        var subModule = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        var start = subModule.IndexOf("private static void " + Registration + "(", StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, Registration + " is gone");
        var end = subModule.IndexOf("private static void", start + 20, StringComparison.Ordinal);
        return subModule.Substring(start, end - start);
    }

    // The engine's AgentApplyDamageModel methods the behaviour tests call, through the base type, so the test does not name the
    // twin (a typeof of it would load the module assembly's bases before the game guard).
    private sealed class AgentApplyDamageModelAccess
    {
        private readonly TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel _model;

        public AgentApplyDamageModelAccess(object model) => _model = (TaleWorlds.MountAndBlade.ComponentInterfaces.AgentApplyDamageModel)model;

        public float HorseChargePenetration() => _model.GetHorseChargePenetration();

        public float RemainingMomentum(float original)
        {
            var blow = default(Blow);
            var collision = default(AttackCollisionData);
            var weapon = default(MissionWeapon);
            return _model.CalculateRemainingMomentum(original, in blow, in collision, null!, null!, in weapon, false);
        }
    }
}
