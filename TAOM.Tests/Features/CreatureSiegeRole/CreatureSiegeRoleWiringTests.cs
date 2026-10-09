using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Composition;
using TAOM.Features.CreatureSiegeRole;
using TAOM.Features.CreatureSiegeRole.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CreatureSiegeRole;

/// <summary>
/// Wiring regression guard for the creature siege role (#735). Each piece fails silently when unwired: drop the module line and
/// no creature is ever moved; drop the behavior and no snapshot is published; drop a hook call from one of the four models and
/// that mode (campaign or Custom Battle) keeps a troll on a ladder queue or its gate blow at vanilla. The model overrides are
/// named by string: a <c>typeof</c> of a type deriving from a SandBox class is not discoverable in a DataRow.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CreatureSiegeRoleWiringTests
{
    private const string AgentStatModel = "TAOM.Features.CareerSystem.Models.TaomAgentStatCalculateModel";
    private const string CustomBattleAgentStatModel = "TAOM.Features.CultureDoctrine.Models.TaomCustomBattleAgentStatCalculateModel";
    private const string CombatMechanicsModel = "TAOM.Features.CombatMechanics.Models.TaomCombatMechanicsModel";
    private const string CustomBattleDamageModel = "TAOM.Features.CombatMechanics.Models.TaomCustomBattleDamageModel";

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => GameAssemblies.EnsureLoaded();

    // --- the module -------------------------------------------------------------------------------------------------------

    [TestMethod]
    public void FeatureModules_ListTheCreatureSiegeRoleModuleOnce()
    {
        Assert.AreEqual(1, FeatureModules.All.OfType<CreatureSiegeRoleModule>().Count());
    }

    [TestMethod]
    public void TheModule_IsNamedCreatureSiegeRole_IsNotParked_AndOwnsNoSaveData()
    {
        var module = new CreatureSiegeRoleModule();

        Assert.AreEqual("CreatureSiegeRole", module.Id);
        Assert.IsNull(module.ParkedReason, "the role ships on");
        Assert.IsFalse(module.OwnsSaveData, "everything lives and ends inside one battle");
    }

    [TestMethod]
    public void TheModule_AddsNoPatchAndNoModel_BecauseTheFourOverridesAreDeclaredByTheirOwnModules()
    {
        var module = new CreatureSiegeRoleModule();

        Assert.AreEqual(0, module.PatchCategories.Count, "the role reaches the engine through models, not a Harmony patch");
        Assert.AreEqual(0, module.GameModels.Count, "a second declaration of a slot would replace the existing model");
        Assert.AreEqual(0, module.CampaignBehaviors.Count);
    }

    [TestMethod]
    public void TheModule_DeclaresTheMissionBehaviorExactlyOnce()
    {
        var decls = new CreatureSiegeRoleModule().MissionBehaviors;

        Assert.AreEqual(1, decls.Count);
        Assert.AreEqual(typeof(CreatureSiegeRoleMissionBehavior), decls[0].BehaviorType);
    }

    [TestMethod]
    public void TheBehavior_IsNotAlsoAddedByHandInSubModule()
    {
        // The module's declaration is what FeatureModuleHooks.AddMissionBehaviors adds; a hand add would run it twice.
        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);

        Assert.IsFalse(source.Contains(nameof(CreatureSiegeRoleMissionBehavior)), "SubModule adds the behavior itself");
    }

    [TestMethod]
    public void TheBehavior_DoesNotOverrideOnBehaviorInitialize_WhichNeverRunsForALateAddedBehavior()
    {
        // MissionBehaviorLifecycleTests holds the ratchet for the whole assembly; this names the one behavior the role depends on.
        var declared = typeof(CreatureSiegeRoleMissionBehavior).GetMethod("OnBehaviorInitialize",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.IsNull(declared, "the setup belongs in AfterStart (#606)");
    }

    // --- all four models call the hooks, base first ---------------------------------------------------------------------

    [DataTestMethod]
    [DataRow(AgentStatModel, "GetDetachmentCostMultiplierOfAgent", "DetachmentCost")]
    [DataRow(CustomBattleAgentStatModel, "GetDetachmentCostMultiplierOfAgent", "DetachmentCost")]
    [DataRow(CombatMechanicsModel, "ApplyDamageScaling", "ScaleGateDamage")]
    [DataRow(CustomBattleDamageModel, "ApplyDamageScaling", "ScaleGateDamage")]
    public void TheModelOverride_CallsTheEnginesBaseFirst_ThenTheHookOnce(string modelName, string methodName, string hookName)
    {
        var model = typeof(TAOM.IoC).Assembly.GetType(modelName);
        Assert.IsNotNull(model, $"{modelName} is gone");
        var method = model!.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
        Assert.IsNotNull(method, $"{modelName} does not declare {methodName}");

        var calls = IlCallScanner.ExtractCalledMethods(method!, method!.GetMethodBody()!.GetILAsByteArray()!).ToList();
        var baseCall = calls.FindIndex(c => c.Name == methodName && c.DeclaringType != model && c.DeclaringType != typeof(CreatureSiegeHooks));
        var hookCall = calls.FindIndex(c => c.DeclaringType == typeof(CreatureSiegeHooks) && c.Name == hookName);

        Assert.IsTrue(baseCall >= 0, $"{modelName}.{methodName} no longer calls its engine base");
        Assert.IsTrue(hookCall > baseCall, $"{modelName}.{methodName} must call CreatureSiegeHooks.{hookName} AFTER the base, on the base's result");
        Assert.AreEqual(1, calls.Count(c => c.DeclaringType == typeof(CreatureSiegeHooks)), "exactly one hook call");
        Assert.AreEqual(1, calls.Count(c => c.Name == methodName && c.DeclaringType != typeof(CreatureSiegeHooks)), "exactly one base call");
    }

    [DataTestMethod]
    [DataRow(AgentStatModel, "GetDetachmentCostMultiplierOfAgent")]
    [DataRow(CustomBattleAgentStatModel, "GetDetachmentCostMultiplierOfAgent")]
    [DataRow(CombatMechanicsModel, "ApplyDamageScaling")]
    [DataRow(CustomBattleDamageModel, "ApplyDamageScaling")]
    public void TheModelOverride_IsStraightLine_SoItNeverSkipsTheHookOrTheBase(string modelName, string methodName)
    {
        var method = typeof(TAOM.IoC).Assembly.GetType(modelName)!
            .GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)!;
        var branches = IlCallScanner.ExtractOpCodes(method.GetMethodBody()!.GetILAsByteArray()!)
            .Where(op => op.FlowControl == FlowControl.Cond_Branch || op.FlowControl == FlowControl.Branch)
            .Select(op => op.Name)
            .ToList();

        Assert.AreEqual(0, branches.Count, $"{modelName}.{methodName} must stay a one-line delegate (gamemodels.md rule 4): {string.Join(", ", branches)}");
    }

    // --- the hook bodies the engine calls off the main thread allocate nothing --------------------------------------------

    private static readonly MethodInfo[] AiThreadHooks =
    {
        typeof(CreatureSiegeHooks).GetMethod("DetachmentCost", new[] { typeof(TaleWorlds.MountAndBlade.Agent), typeof(float) })!,
        typeof(CreatureSiegeHooks).GetMethod("ScaleGateDamage")!,
    };

    [TestMethod]
    public void TheHookEntryPoints_NamedByTheModels_ContainNoAllocatingInstruction()
    {
        foreach (var method in AiThreadHooks)
        {
            var found = IlCallScanner.ExtractOpCodes(method.GetMethodBody()!.GetILAsByteArray()!)
                .Where(op => op == OpCodes.Newobj || op == OpCodes.Newarr || op == OpCodes.Box)
                .Select(op => op.Name)
                .ToList();

            Assert.AreEqual(0, found.Count, $"CreatureSiegeHooks.{method.Name} allocates on the engine's thread: {string.Join(", ", found)}");
        }
    }

    [TestMethod]
    public void TheHookEntryPoints_AreTheMethodsTheModelsCall()
    {
        // The scan above is only worth anything if it reads the very overloads the four models reference.
        var referenced = new[] { AgentStatModel, CustomBattleAgentStatModel, CombatMechanicsModel, CustomBattleDamageModel }
            .Select(n => typeof(TAOM.IoC).Assembly.GetType(n)!)
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => m.Name == "GetDetachmentCostMultiplierOfAgent" || m.Name == "ApplyDamageScaling")
            .SelectMany(m => IlCallScanner.ExtractCalledMethods(m, m.GetMethodBody()!.GetILAsByteArray()!))
            .Where(c => c.DeclaringType == typeof(CreatureSiegeHooks))
            .Cast<MethodInfo>()
            .Distinct()
            .ToList();

        CollectionAssert.AreEquivalent(AiThreadHooks, referenced);
    }

    [TestMethod]
    public void TheAllocationScan_WouldFlagAHookThatBuildsAString()
    {
        // The control: a body that formats a string must trip the same predicate, or the scan above is vacuous.
        var offender = typeof(CreatureSiegeRoleWiringTests).GetMethod(nameof(AFormattingBody), BindingFlags.NonPublic | BindingFlags.Static)!;

        var found = IlCallScanner.ExtractOpCodes(offender.GetMethodBody()!.GetILAsByteArray()!)
            .Any(op => op == OpCodes.Newobj || op == OpCodes.Newarr || op == OpCodes.Box);

        Assert.IsTrue(found);
    }

    private static string AFormattingBody(float value) => string.Format("{0}", value);
}
