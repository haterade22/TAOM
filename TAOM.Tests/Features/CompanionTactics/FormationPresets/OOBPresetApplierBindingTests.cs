using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Selector;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.CompanionTactics.FormationPresets;

/// <summary>
/// Pins the vanilla surface <c>OOBPresetApplier</c> and <c>FormationPresetLayout</c> rely on (v1.5.4 at the time of
/// writing): the public members it reads and calls, the DeploymentFormationClass numbers the saved presets hold, the
/// order in which vanilla's accept-captain handler clears the old assignment, and the callbacks that wire the static
/// selection and accept handlers. The applier uses public members only, so a rename or a changed type fails here
/// before it fails in a battle.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class OOBPresetApplierBindingTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(Microsoft.VisualStudio.TestTools.UnitTesting.TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded) Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private const BindingFlags Public = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public;
    private const BindingFlags NonPublic = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

    private static void AssertProperty(Type owner, string name, Type expected, bool settable = false)
    {
        var property = owner.GetProperty(name, Public);
        Assert.IsNotNull(property, $"{owner.Name}.{name} did not resolve as a public property.");
        Assert.AreEqual(expected, property.PropertyType, $"{owner.Name}.{name} type");
        Assert.IsNotNull(property.GetGetMethod(), $"{owner.Name}.{name} has a public getter");
        if (settable) Assert.IsNotNull(property.GetSetMethod(), $"{owner.Name}.{name} has a public setter");
    }

    private static void AssertMethod(Type owner, string name, Type returnType)
    {
        var method = owner.GetMethod(name, Public, null, Type.EmptyTypes, null);
        Assert.IsNotNull(method, $"{owner.Name}.{name}() did not resolve as a public method.");
        Assert.AreEqual(returnType, method.ReturnType, $"{owner.Name}.{name} return type");
    }

    private static List<CodeInstruction> Body(string type, string method) =>
        PatchProcessor.GetOriginalInstructions(typeof(OrderOfBattleVM).Assembly.GetType(type)!.GetMethod(method, NonPublic | Public)!);

    // ---- The OOB view model ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OrderOfBattleVM_ExposesTheMembersTheApplierReads()
    {
        RequireGame();

        AssertProperty(typeof(OrderOfBattleVM), "FormationsFirstHalf", typeof(MBBindingList<OrderOfBattleFormationItemVM>));
        AssertProperty(typeof(OrderOfBattleVM), "FormationsSecondHalf", typeof(MBBindingList<OrderOfBattleFormationItemVM>));
        AssertProperty(typeof(OrderOfBattleVM), "UnassignedHeroes", typeof(MBBindingList<OrderOfBattleHeroItemVM>));
        AssertProperty(typeof(OrderOfBattleVM), "IsPlayerGeneral", typeof(bool));
        AssertMethod(typeof(OrderOfBattleVM), "ExecuteClearHeroSelection", typeof(void));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OrderOfBattleFormationItemVM_ExposesTheMembersTheApplierReadsAndCalls()
    {
        RequireGame();

        var item = typeof(OrderOfBattleFormationItemVM);
        AssertProperty(item, "Formation", typeof(Formation));
        AssertProperty(item, "HasFormation", typeof(bool)); // read by OOBCaptainAutoAssigner, the applier's sibling
        AssertProperty(item, "HasCaptain", typeof(bool));
        AssertProperty(item, "Captain", typeof(OrderOfBattleHeroItemVM));
        AssertProperty(item, "HeroTroops", typeof(MBBindingList<OrderOfBattleHeroItemVM>));
        AssertProperty(item, "IsAdjustable", typeof(bool));
        AssertProperty(item, "FormationClassSelector", typeof(SelectorVM<OrderOfBattleFormationClassSelectorItemVM>));
        AssertMethod(item, "GetOrderOfBattleClass", typeof(DeploymentFormationClass));
        AssertMethod(item, "ExecuteAcceptCaptain", typeof(void));
        AssertMethod(item, "ExecuteAcceptHeroTroops", typeof(void));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OrderOfBattleHeroItemVM_ExposesTheAgentAndTheStaticSelectionCallback()
    {
        RequireGame();

        var agent = typeof(OrderOfBattleHeroItemVM).GetField("Agent", Public);
        Assert.IsNotNull(agent, "OrderOfBattleHeroItemVM.Agent did not resolve as a public field.");
        Assert.AreEqual(typeof(Agent), agent.FieldType);

        var callback = typeof(OrderOfBattleHeroItemVM).GetField("OnHeroSelection", Public);
        Assert.IsNotNull(callback, "OrderOfBattleHeroItemVM.OnHeroSelection did not resolve as a public static field.");
        Assert.IsTrue(callback.IsStatic);
        Assert.AreEqual(typeof(Action<OrderOfBattleHeroItemVM>), callback.FieldType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void ClassSelector_ExposesTheItemListTheSelectedIndexAndTheItemsClass()
    {
        RequireGame();

        var selector = typeof(SelectorVM<OrderOfBattleFormationClassSelectorItemVM>);
        AssertProperty(selector, "ItemList", typeof(MBBindingList<OrderOfBattleFormationClassSelectorItemVM>));
        AssertProperty(selector, "SelectedIndex", typeof(int), settable: true);

        var formationClass = typeof(OrderOfBattleFormationClassSelectorItemVM).GetField("FormationClass", Public);
        Assert.IsNotNull(formationClass, "OrderOfBattleFormationClassSelectorItemVM.FormationClass did not resolve as a public field.");
        Assert.AreEqual(typeof(DeploymentFormationClass), formationClass.FieldType);
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void DeploymentFormationClass_KeepsTheNumbersSavedPresetsAndTheSiegeMappingHold()
    {
        RequireGame();

        Assert.AreEqual(0, (int)DeploymentFormationClass.Unset);
        Assert.AreEqual(1, (int)DeploymentFormationClass.Infantry);
        Assert.AreEqual(2, (int)DeploymentFormationClass.Ranged);
        Assert.AreEqual(3, (int)DeploymentFormationClass.Cavalry);
        Assert.AreEqual(4, (int)DeploymentFormationClass.HorseArcher);
        Assert.AreEqual(5, (int)DeploymentFormationClass.InfantryAndRanged);
        Assert.AreEqual(6, (int)DeploymentFormationClass.CavalryAndHorseArcher);
    }

    private static string? HandlerOf(Dictionary<string, string> assigned, string key) =>
        assigned.TryGetValue(key, out var handler) ? handler : null;

    // ---- The order of vanilla's own steps ----

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void OnFormationAcceptCaptain_ClearsTheHeroOldAssignmentBeforeAssigningTheCaptain()
    {
        RequireGame();

        var calls = Body("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM", "OnFormationAcceptCaptain")
            .Where(ci => ci.operand is MethodBase).Select(ci => ((MethodBase)ci.operand).Name).ToList();

        var clear = calls.IndexOf("ClearHeroAssignment");
        var assign = calls.IndexOf("AssignCaptain");

        Assert.IsTrue(clear >= 0 && assign > clear, "OnFormationAcceptCaptain calls: " + string.Join(", ", calls));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    [TestCategory("RequiresGameIL")]
    public void InitializeFormationCallbacks_AssignsTheSelectionAndBothAcceptCallbacks()
    {
        RequireGame();

        var body = Body("TaleWorlds.MountAndBlade.ViewModelCollection.OrderOfBattle.OrderOfBattleVM", "InitializeFormationCallbacks");
        var assigned = new Dictionary<string, string>();
        for (var i = 2; i < body.Count; i++)
        {
            if (body[i].opcode != OpCodes.Stsfld || body[i].operand is not FieldInfo field) continue;
            if (body[i - 2].opcode == OpCodes.Ldftn && body[i - 2].operand is MethodBase handler)
                assigned[field.DeclaringType!.Name + "." + field.Name] = handler.Name;
        }

        Assert.AreEqual("OnHeroSelection", HandlerOf(assigned, "OrderOfBattleHeroItemVM.OnHeroSelection"));
        Assert.AreEqual("OnFormationAcceptCaptain", HandlerOf(assigned, "OrderOfBattleFormationItemVM.OnAcceptCaptain"));
        Assert.AreEqual("OnFormationAcceptHeroTroops", HandlerOf(assigned, "OrderOfBattleFormationItemVM.OnAcceptHeroTroops"));
    }
}
