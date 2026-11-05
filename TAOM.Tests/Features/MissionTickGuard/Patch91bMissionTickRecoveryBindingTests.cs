using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MissionTickGuard.Hooks;
using TAOM.Tests.Infrastructure;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MissionTickGuard;

/// <summary>
/// The engine facts Patch91b's safety rests on (v1.4.8), read from the original IL, and its three
/// registration points. A red test here means re-derive the recovery, not adjust the test.
/// </summary>
[TestClass]
public class Patch91bMissionTickRecoveryBindingTests
{
    private const BindingFlags Declared = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    private static List<KeyValuePair<OpCode, object>> Body(MethodBase m) => PatchProcessor.ReadMethodBody(m).ToList();

    private static bool StoresFlag(KeyValuePair<OpCode, object> op) =>
        op.Key == OpCodes.Stfld && op.Value is FieldInfo f && f.Name == "tickCompleted" && f.DeclaringType == typeof(Mission);

    private static bool Calls(KeyValuePair<OpCode, object> op, string name) =>
        (op.Key == OpCodes.Call || op.Key == OpCodes.Callvirt) && op.Value is MethodBase m && m.Name == name;

    private static MethodInfo OnTick() =>
        AccessTools.Method(typeof(Mission), nameof(Mission.OnTick), new[] { typeof(float), typeof(float), typeof(bool), typeof(bool) });

    private static List<int> IndexesOf(List<KeyValuePair<OpCode, object>> body, Func<KeyValuePair<OpCode, object>, bool> match) =>
        Enumerable.Range(0, body.Count).Where(i => match(body[i])).ToList();

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnTick_IsThePublicNonVirtualFourArgumentMethod()
    {
        RequireGame();
        var method = OnTick();
        Assert.IsNotNull(method, "Mission.OnTick(float, float, bool, bool) is gone: re-derive Patch91b against the new engine");
        Assert.IsTrue(method.IsPublic, "Mission.OnTick is no longer public: Patch91b's nameof binding no longer holds");
        Assert.IsFalse(method.IsStatic, "Mission.OnTick became static: Patch91b reads __instance");
        Assert.IsFalse(method.IsVirtual, "Mission.OnTick became virtual: an override would escape Patch91b");
        Assert.AreEqual(typeof(void), method.ReturnType, "Mission.OnTick no longer returns void");
        Assert.AreEqual(1, typeof(Mission).GetMethods(Declared).Count(m => m.Name == "OnTick"),
            "Mission.OnTick gained an overload: re-derive which one the frame calls");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TickAgentsAndTeamsAsync_IsPublicWithOneFloat()
    {
        RequireGame();
        var method = AccessTools.Method(typeof(Mission), "TickAgentsAndTeamsAsync", new[] { typeof(float) });
        Assert.IsNotNull(method, "Mission.TickAgentsAndTeamsAsync(float) is gone: Patch91b has no launch mark");
        Assert.IsTrue(method.IsPublic, "TickAgentsAndTeamsAsync is no longer public");
        Assert.IsFalse(method.IsStatic, "TickAgentsAndTeamsAsync became static");
        Assert.AreEqual(typeof(void), method.ReturnType, "TickAgentsAndTeamsAsync no longer returns void");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void TickCompleted_IsAPrivateInstanceBool_ThatResolvesAsAFieldRef()
    {
        RequireGame();
        var field = typeof(Mission).GetField("tickCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, "Mission.tickCompleted is gone: the handshake Patch91b repairs has changed");
        Assert.IsTrue(field.IsPrivate, "Mission.tickCompleted is no longer private");
        Assert.AreEqual(typeof(bool), field.FieldType, "Mission.tickCompleted is no longer a bool");
        Assert.IsNotNull(AccessTools.FieldRefAccess<Mission, bool>("tickCompleted"),
            "Mission.tickCompleted no longer resolves as a field ref: Patch91b would never arm");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnTick_ClearsTheFlagOnce_ThenRunsTheBehaviours_ThenStartsTheAgentTick()
    {
        RequireGame();
        var body = Body(OnTick());

        var stores = IndexesOf(body, StoresFlag);
        Assert.AreEqual(1, stores.Count, "Mission.OnTick no longer clears tickCompleted exactly once: re-derive Patch91b against the new engine");
        int clear = stores[0];
        Assert.AreEqual(OpCodes.Ldc_I4_0, body[clear - 1].Key, "Mission.OnTick's one write to tickCompleted is no longer a clear: re-derive Patch91b");

        int behaviours = body.FindIndex(op => Calls(op, "OnMissionTick"));
        Assert.IsTrue(behaviours > clear, "Mission.OnTick no longer runs OnMissionTick after the clear: a throw there no longer hangs the same way");

        var async = IndexesOf(body, op => Calls(op, "TickAgentsAndTeamsAsync"));
        var inline = IndexesOf(body, op => Calls(op, "TickAgentsAndTeamsImp"));
        Assert.AreEqual(1, async.Count, "Mission.OnTick no longer starts TickAgentsAndTeamsAsync exactly once: the launch mark is wrong");
        Assert.AreEqual(1, inline.Count, "Mission.OnTick no longer calls TickAgentsAndTeamsImp exactly once: re-derive Patch91b");
        Assert.IsTrue(async[0] > behaviours, "the agent-tick launch moved before the behaviours: restoring would overlap a running job");
        Assert.IsTrue(inline[0] > behaviours, "the inline agent tick moved before the behaviours: re-derive Patch91b");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void OnlyTheAgentTickSetsTheFlag_AndNoOtherMethodWritesIt()
    {
        RequireGame();
        var imp = AccessTools.Method(typeof(Mission), "TickAgentsAndTeamsImp", new[] { typeof(float), typeof(bool) });
        Assert.IsNotNull(imp, "Mission.TickAgentsAndTeamsImp(float, bool) is gone: nothing sets tickCompleted the way Patch91b assumes");
        var impBody = Body(imp);
        var impStores = IndexesOf(impBody, StoresFlag);
        Assert.AreEqual(1, impStores.Count, "TickAgentsAndTeamsImp no longer writes tickCompleted exactly once: re-derive Patch91b");
        Assert.AreEqual(OpCodes.Ldc_I4_1, impBody[impStores[0] - 1].Key, "TickAgentsAndTeamsImp's write to tickCompleted no longer sets it");

        int read = 0, writes = 0;
        foreach (var method in typeof(Mission).GetMethods(Declared))
        {
            if (method.IsAbstract || method.IsGenericMethodDefinition || method.GetMethodBody() == null) continue;
            List<KeyValuePair<OpCode, object>> body;
            try { body = Body(method); }
            catch (Exception) { continue; }
            read++;
            writes += body.Count(StoresFlag);
        }

        Assert.IsTrue(read >= 200, $"only {read} Mission methods were readable: this scan proves nothing");
        Assert.AreEqual(2, writes, "another Mission method now writes tickCompleted: re-derive Patch91b against the new engine");
    }

    [TestMethod]
    public void PatchClasses_AreRegisteredInAllThreePlaces()
    {
        foreach (var patch in new[] { typeof(Mission_OnTick_TickRecovery_Patch), typeof(Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch) })
        {
            Assert.AreEqual(1, patch.GetCustomAttributes(typeof(HarmonyPatch), inherit: false).Length, $"{patch.Name} lost its target");
            var categories = patch.GetCustomAttributes(typeof(HarmonyPatchCategory), inherit: false)
                .Cast<HarmonyPatchCategory>().Select(c => c.info.category).ToList();
            CollectionAssert.Contains(categories, "Patch91b_MissionTickRecovery", $"{patch.Name} lost its category");
        }

        var source = RepoPaths.ReadSource("Main/SubModule.cs", stripComments: true);
        const string apply = "TryPatchCategory(\"Patch91b_MissionTickRecovery\");";
        const string arm = "MissionTickRecoveryWiring.VerifyAndArm(";
        int patch91 = source.IndexOf("TryPatchCategory(\"Patch91_MissionTickStall\");", StringComparison.Ordinal);
        int at = source.IndexOf(apply, StringComparison.Ordinal);
        int armed = source.IndexOf(arm, StringComparison.Ordinal);
        Assert.IsTrue(patch91 >= 0 && at > patch91, "Patch91b must be applied through TryPatchCategory after Patch91");
        Assert.IsTrue(armed > at, "VerifyAndArm must run after the category applies, or it finds no patches and stays off");
        Assert.AreEqual(at, source.LastIndexOf(apply, StringComparison.Ordinal), "Patch91b applied twice");
        Assert.AreEqual(armed, source.LastIndexOf(arm, StringComparison.Ordinal), "VerifyAndArm called twice");

        var types = AccessTools.GetTypesFromAssembly(typeof(Mission_OnTick_TickRecovery_Patch).Assembly).ToList();
        int launchClass = types.IndexOf(typeof(Mission_TickAgentsAndTeamsAsync_LaunchMark_Patch));
        int onTickClass = types.IndexOf(typeof(Mission_OnTick_TickRecovery_Patch));
        Assert.IsTrue(launchClass >= 0 && launchClass < onTickClass,
            "the launch target must be patched before OnTick: it can inline into an OnTick rebuilt first, which loses the mark");
    }
}
