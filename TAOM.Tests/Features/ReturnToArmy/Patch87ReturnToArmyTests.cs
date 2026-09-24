using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.ReturnToArmy;
using TAOM.Features.ReturnToArmy.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.ReturnToArmy;

/// <summary>
/// Patch87 gives an army member who is NOT physically attached to the army a way out of a town or
/// castle (#566). Vanilla's "Return to Army" only ever leaves a village; in a fortification it
/// switches to the army wait menu, whose first tick bounces a foreign-faction town into "You are
/// waiting in X" and a same-faction one into a wait that nothing ends, and "Leave" is hidden for
/// every non-leader member. So the player is stuck.
///
/// Two halves, the Patch84 shape. <see cref="ReturnToArmyRules.Decide"/> is pure and carries the
/// whole policy, so it is tested directly. Everything else the patch stands on is an engine detail
/// it cannot see change: the private consequence it prefixes, and the public members the leave
/// sequence calls. Those are pinned as binding drift-guards. The leave itself needs a live
/// campaign (a PlayerEncounter and a captured settlement) and is verified in game, per the feature
/// doc.
/// </summary>
[TestClass]
public class Patch87ReturnToArmyTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static System.Type EngineType(string fullName)
    {
        var type = AccessTools.TypeByName(fullName);
        Assert.IsNotNull(type, fullName + " did not resolve.");
        return type!;
    }

    private static System.Type BehaviorType() =>
        EngineType("TaleWorlds.CampaignSystem.CampaignBehaviors.PlayerTownVisitCampaignBehavior");

    // ---- the decision ----------------------------------------------------------------------

    [TestMethod]
    public void Decide_NotInArmy_RunsVanilla()
    {
        // Vanilla's own condition hides the option here, so this row is unreachable in play. It is
        // pinned so a future caller cannot make the prefix act on a party that has no army.
        Assert.AreEqual(
            ReturnToArmyRules.Verdict.RunVanilla,
            ReturnToArmyRules.Decide(inArmy: false, isArmyLeader: false, attachedToArmy: false, inVillage: false));
    }

    [TestMethod]
    public void Decide_ArmyLeader_RunsVanilla()
    {
        // The leader is never offered "Return to Army" (the condition requires LeaderParty !=
        // MainParty), and the leader's exit is the ordinary "Leave" option vanilla keeps for him.
        Assert.AreEqual(
            ReturnToArmyRules.Verdict.RunVanilla,
            ReturnToArmyRules.Decide(inArmy: true, isArmyLeader: true, attachedToArmy: false, inVillage: false));
    }

    [TestMethod]
    public void Decide_AttachedMember_RunsVanilla()
    {
        // The case vanilla designed the option for: the army is resting in this settlement and the
        // player is merged into it. Waiting with the army is correct, and the leader's departure
        // (LeaveSettlementAction on the leader) finishes the player's encounter for him.
        Assert.AreEqual(
            ReturnToArmyRules.Verdict.RunVanilla,
            ReturnToArmyRules.Decide(inArmy: true, isArmyLeader: false, attachedToArmy: true, inVillage: false));
    }

    [TestMethod]
    public void Decide_UnattachedMemberInVillage_RunsVanilla()
    {
        // Vanilla's village branch already calls LeaveSettlement + Finish. Doing it twice would run
        // Finish on an encounter that no longer exists.
        Assert.AreEqual(
            ReturnToArmyRules.Verdict.RunVanilla,
            ReturnToArmyRules.Decide(inArmy: true, isArmyLeader: false, attachedToArmy: false, inVillage: true));
    }

    [TestMethod]
    public void Decide_UnattachedMemberInTownOrCastle_LeavesSettlement()
    {
        // #566 exactly: Army set, AttachedTo null, standing in Orthanc.
        Assert.AreEqual(
            ReturnToArmyRules.Verdict.LeaveSettlement,
            ReturnToArmyRules.Decide(inArmy: true, isArmyLeader: false, attachedToArmy: false, inVillage: false));
    }

    [TestMethod]
    public void Decide_LeavesOnlyForAnUnattachedMemberOutsideAVillage()
    {
        // The invariant behind all of the above, stated once over the whole input space: exactly
        // one of the sixteen combinations may skip vanilla.
        var leaves = 0;
        foreach (var inArmy in new[] { true, false })
        foreach (var isLeader in new[] { true, false })
        foreach (var attached in new[] { true, false })
        foreach (var inVillage in new[] { true, false })
        {
            var verdict = ReturnToArmyRules.Decide(inArmy, isLeader, attached, inVillage);
            var expectLeave = inArmy && !isLeader && !attached && !inVillage;

            Assert.AreEqual(
                expectLeave ? ReturnToArmyRules.Verdict.LeaveSettlement : ReturnToArmyRules.Verdict.RunVanilla,
                verdict,
                $"inArmy={inArmy} isLeader={isLeader} attached={attached} inVillage={inVillage}");

            if (verdict == ReturnToArmyRules.Verdict.LeaveSettlement)
                leaves++;
        }

        Assert.AreEqual(1, leaves, "exactly one input combination may skip vanilla");
    }

    // ---- the bindings ----------------------------------------------------------------------

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Target_StillResolves_AsAPrivateInstanceMethod_WithOneMenuCallbackArgsParameter()
    {
        RequireGame();

        var type = BehaviorType();
        const string name = "game_menu_return_to_army_on_consequence";

        var method = AccessTools.Method(type, name);
        Assert.IsNotNull(method, name + " did not resolve; the prefix would never apply and #566 is back.");
        Assert.IsFalse(method!.IsStatic, name + " became static; the prefix's binding assumptions changed.");
        Assert.IsTrue(method.IsPrivate, name + " is no longer private; the registry entry and this test's name describe a private target.");

        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, name + " arity drifted.");
        Assert.AreEqual("MenuCallbackArgs", parameters[0].ParameterType.Name, name + " no longer takes MenuCallbackArgs.");

        var overloads = type.GetMethods(AccessTools.all).Count(m => m.Name == name);
        Assert.AreEqual(1, overloads,
            name + " gained an overload; [HarmonyPatch] by name is now ambiguous and must name the signature.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TargetsCondition_StillResolves()
    {
        RequireGame();

        // The consequence is only reachable through this condition (Army != null and the player is
        // not its leader). If vanilla restructures the pair, re-derive the decision table.
        Assert.IsNotNull(
            AccessTools.Method(BehaviorType(), "game_menu_return_to_army_on_condition"),
            "game_menu_return_to_army_on_condition is gone; the routing that reaches the patched consequence has changed.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void VanillaConsequence_StillReachesTheWaitMenuSwitchAndTheVillageLeave()
    {
        RequireGame();

        // Call PRESENCE only: this proves the vanilla body has not been rewritten out from under the
        // decision table, not which branch each call sits on. The branch shape (leave for villages
        // only) is re-read at each engine bump, per the registry entry.
        var called = CalledNames(AccessTools.Method(BehaviorType(), "game_menu_return_to_army_on_consequence")!);

        CollectionAssert.IsSubsetOf(
            new[] { "SwitchToMenu", "LeaveSettlement", "Finish", "get_IsVillage" },
            called.ToList(),
            "vanilla's Return-to-Army consequence no longer calls what the decision table assumes it does.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void LeaveSequence_EngineMembers_StillResolve()
    {
        RequireGame();

        var mobileParty = EngineType("TaleWorlds.CampaignSystem.Party.MobileParty");
        var settlement = EngineType("TaleWorlds.CampaignSystem.Settlements.Settlement");
        var army = EngineType("TaleWorlds.CampaignSystem.Army");
        var encounter = EngineType("TaleWorlds.CampaignSystem.Encounters.PlayerEncounter");
        var campaign = EngineType("TaleWorlds.CampaignSystem.Campaign");
        var saveHandler = EngineType("TaleWorlds.CampaignSystem.SaveHandler");

        // What the prefix READS to decide.
        Assert.IsNotNull(AccessTools.PropertyGetter(mobileParty, "Army"), "MobileParty.Army getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(mobileParty, "AttachedTo"), "MobileParty.AttachedTo getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(mobileParty, "CurrentSettlement"), "MobileParty.CurrentSettlement getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(army, "LeaderParty"), "Army.LeaderParty getter is gone.");
        Assert.IsNotNull(AccessTools.PropertyGetter(settlement, "IsVillage"), "Settlement.IsVillage getter is gone.");

        // What the leave sequence CALLS, in vanilla's own order.
        var position = AccessTools.Property(mobileParty, "Position");
        Assert.IsNotNull(position?.GetSetMethod(true), "MobileParty.Position lost its setter.");
        var gate = AccessTools.Property(settlement, "GatePosition");
        Assert.IsNotNull(gate?.GetGetMethod(true), "Settlement.GatePosition getter is gone.");
        Assert.AreEqual(position!.PropertyType, gate!.PropertyType,
            "MobileParty.Position and Settlement.GatePosition no longer share a type; the gate assignment would not compile.");

        var leave = AccessTools.Method(encounter, "LeaveSettlement", System.Type.EmptyTypes);
        Assert.IsNotNull(leave, "PlayerEncounter.LeaveSettlement() is gone.");
        Assert.IsTrue(leave!.IsStatic, "PlayerEncounter.LeaveSettlement is no longer static.");

        var finish = AccessTools.Method(encounter, "Finish", new[] { typeof(bool) });
        Assert.IsNotNull(finish, "PlayerEncounter.Finish(bool) is gone.");
        Assert.IsTrue(finish!.IsStatic, "PlayerEncounter.Finish is no longer static.");
        Assert.IsTrue(finish.GetParameters()[0].HasDefaultValue,
            "PlayerEncounter.Finish's forcePlayerOutFromSettlement lost its default; the prefix calls it with none.");
        Assert.AreEqual(true, finish.GetParameters()[0].DefaultValue,
            "PlayerEncounter.Finish's default flipped; the prefix relies on forcePlayerOutFromSettlement defaulting to true.");

        var hold = AccessTools.Method(mobileParty, "SetMoveModeHold", System.Type.EmptyTypes);
        Assert.IsNotNull(hold, "MobileParty.SetMoveModeHold() is gone.");

        var handler = AccessTools.PropertyGetter(campaign, "SaveHandler");
        Assert.IsNotNull(handler, "Campaign.SaveHandler getter is gone.");
        Assert.AreEqual(saveHandler, handler!.ReturnType, "Campaign.SaveHandler changed type.");
        Assert.IsNotNull(AccessTools.Method(saveHandler, "SignalAutoSave", System.Type.EmptyTypes),
            "SaveHandler.SignalAutoSave() is gone.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Patch_IsRegisteredInAllThreePlaces()
    {
        // A patch needs all three or it is dead code with no error, warning or log line: the target
        // attribute, the category attribute, and a matching PatchCategory call in SubModule.cs
        // (lessons/harmony-il.md, Patch39).
        var patch = typeof(Patch87_ReturnToArmy);

        var target = patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false);
        Assert.AreEqual(1, target.Length, "Patch87_ReturnToArmy lost its [HarmonyPatch] target attribute.");

        var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
            .Cast<HarmonyPatchCategory>()
            .Select(c => c.info.category)
            .ToList();
        CollectionAssert.Contains(categories, "Patch87_ReturnToArmy",
            "Patch87_ReturnToArmy lost its [HarmonyPatchCategory]; SubModule's PatchCategory call would apply nothing.");

        var subModule = Path.Combine(FindRepoRoot(), "Main", "SubModule.cs");
        Assert.IsTrue(File.Exists(subModule), $"SubModule.cs not found at {subModule}");

        var source = File.ReadAllText(subModule);
        StringAssert.Contains(source, "TryPatchCategory(\"Patch87_ReturnToArmy\")",
            "SubModule.cs no longer applies Patch87_ReturnToArmy; the patch is dead code.");
        StringAssert.Contains(source, "Patch87_ReturnToArmy.Initialize(",
            "SubModule.cs no longer initialises Patch87; the prefix runs without a logger and its diagnostics vanish.");
        StringAssert.Contains(source, "Patch87_ReturnToArmy.ResetForUnload()",
            "SubModule.cs no longer resets Patch87 on unload; a stale logger survives a reload.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Prefix_StillCallsTheDecisionAndTheLeaveSequence()
    {
        // The prefix takes sealed engine types and cannot be run here, so this pins the two things a
        // one-character edit could silently remove: the call into the pure decision, and the four
        // engine calls that make up vanilla's own leave. Call presence, not control flow.
        var prefix = AccessTools.Method(typeof(Patch87_ReturnToArmy), "Prefix");
        Assert.IsNotNull(prefix, "Patch87_ReturnToArmy.Prefix is gone.");
        Assert.AreEqual(typeof(bool), prefix!.ReturnType, "the prefix must return bool to be able to skip vanilla.");

        var called = CalledNames(prefix);

        CollectionAssert.IsSubsetOf(
            new[] { "Decide", "LeaveSettlement", "Finish", "SetMoveModeHold", "SignalAutoSave" },
            called.ToList(),
            "Patch87's prefix no longer calls the decision or the full leave sequence.");
    }

    private static System.Collections.Generic.HashSet<string> CalledNames(MethodBase method)
    {
        var il = method.GetMethodBody()?.GetILAsByteArray();
        Assert.IsNotNull(il, method.Name + " has no readable IL body.");

        var names = new System.Collections.Generic.HashSet<string>(
            IlCallScanner.ExtractCalledMethods(method, il!).Select(m => m.Name), System.StringComparer.Ordinal);

        Assert.AreNotEqual(0, names.Count, method.Name + " resolved no calls; the scan failed, not the method.");
        return names;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
