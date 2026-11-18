using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.TroopWeight.Hooks;
using TAOM.Tests.Migration;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Tests.Features.TroopWeight;

/// <summary>
/// #1430: Transfer All's member budget passes through the weighted fill, and the party screen header decides once,
/// at screen open, whether a side's limit is the party's own. The transpiler runs over the installed v1.5.7 IL, so an
/// engine change that moves its anchors fails here instead of silently leaving vanilla's headcount budget.
/// </summary>
[TestClass]
public class TransferAllPatchTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static MethodInfo Target()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var target = AccessTools.DeclaredMethod(typeof(PartyScreenLogic), "TransferAllTroops", new[] { typeof(PartyScreenLogic.PartyCommand) });
        Assert.IsNotNull(target, "PartyScreenLogic.TransferAllTroops(PartyCommand) did not resolve; the Transfer All patch targets it.");
        return target!;
    }

    private static readonly MethodInfo Budget = AccessTools.Method(typeof(PartyScreenLogic_TransferAllTroops_Patch), nameof(PartyScreenLogic_TransferAllTroops_Patch.Budget));

    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Rewrite_InsertsOneBudgetCall_BeforeVanillasNothingFitsTest()
    {
        var original = PatchProcessor.GetOriginalInstructions(Target());
        var rewritten = PartyScreenLogic_TransferAllTroops_Patch.Rewrite(original, out bool applied);

        Assert.IsTrue(applied, "The Transfer All anchors were not found in the installed engine.");
        var calls = rewritten.Select((c, i) => (c, i)).Where(x => x.c.Calls(Budget)).Select(x => x.i).ToList();
        Assert.AreEqual(1, calls.Count);

        int at = calls[0];
        // ldloc num; ldarg.0; ldarg.1; call Budget; stloc num; then vanilla's ldloc num; ldc.i4.0; bgt.
        Assert.IsTrue(rewritten[at - 3].IsLdloc());
        Assert.AreEqual(OpCodes.Ldarg_0, rewritten[at - 2].opcode);
        Assert.AreEqual(OpCodes.Ldarg_1, rewritten[at - 1].opcode);
        Assert.IsTrue(rewritten[at + 1].IsStloc());
        Assert.AreEqual(rewritten[at - 3].LocalIndex(), rewritten[at + 1].LocalIndex());
        Assert.IsTrue(rewritten[at + 2].IsLdloc() && rewritten[at + 2].LocalIndex() == rewritten[at + 1].LocalIndex());
        Assert.AreEqual(OpCodes.Ldc_I4_0, rewritten[at + 3].opcode);
        Assert.IsTrue(rewritten[at + 4].opcode == OpCodes.Bgt || rewritten[at + 4].opcode == OpCodes.Bgt_S);
    }

    // Every branch that computed the budget jumps to vanilla's test; its labels must move to the inserted load so all
    // of them pass through Budget.
    [TestMethod]
    [TestCategory("RequiresGameIL")]
    [TestCategory("BindingVerification")]
    public void Rewrite_MovesTheTestsLabels_ToTheInsertedLoad()
    {
        var original = PatchProcessor.GetOriginalInstructions(Target());
        var rewritten = PartyScreenLogic_TransferAllTroops_Patch.Rewrite(original, out _);
        int at = rewritten.FindIndex(c => c.Calls(Budget));

        Assert.AreNotEqual(0, rewritten[at - 3].labels.Count, "vanilla's 'num <= 0' test is a jump target in v1.5.7");
        Assert.AreEqual(0, rewritten[at + 2].labels.Count);
    }

    [TestMethod]
    public void Rewrite_WithoutTheAnchors_LeavesTheMethodUnchanged()
    {
        var code = new List<CodeInstruction> { new(OpCodes.Ldc_I4_1), new(OpCodes.Ret) };
        var rewritten = PartyScreenLogic_TransferAllTroops_Patch.Rewrite(code, out bool applied);

        Assert.IsFalse(applied);
        CollectionAssert.AreEqual(code.Select(c => c.opcode).ToList(), rewritten.Select(c => c.opcode).ToList());
    }

    // Harmony runs a transpiler again each time its method is re-patched (PatchShield's pass does so), so the
    // missing-anchor warning is latched per process.
    [TestMethod]
    public void Transpiler_WithoutTheAnchors_WarnsOnceAcrossRepatches()
    {
        var logger = new TAOM.Tests.Features.LoadTimeStamps.RecordingLogger();
        PartyScreenLogic_TransferAllTroops_Patch.Initialize(null!, logger);
        var code = new List<CodeInstruction> { new(OpCodes.Ret) };

        PartyScreenLogic_TransferAllTroops_Patch.Transpiler(code).ToList();
        PartyScreenLogic_TransferAllTroops_Patch.Transpiler(code).ToList();

        Assert.AreEqual(1, logger.Lines.Count(l => l.StartsWith("WARN ", StringComparison.Ordinal)));
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void Initialize_ResolvesWithTheParameterThePostfixReads()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded.");
        var init = AccessTools.DeclaredMethod(typeof(PartyScreenLogic), nameof(PartyScreenLogic.Initialize));
        Assert.IsNotNull(init);
        Assert.IsNotNull(AccessTools.Property(typeof(PartyScreenLogic), nameof(PartyScreenLogic.RightPartyMembersSizeLimit)));
        Assert.IsNotNull(AccessTools.Property(typeof(PartyScreenLogic), nameof(PartyScreenLogic.LeftPartyMembersSizeLimit)));
    }

    // The header guard reads the verdict recorded at screen open, not the live limit, which moves with the first heavy
    // troop moved (#1430). The live comparison stays only as the fallback for a screen with no record.
    [TestMethod]
    public void HeaderGuard_ReadsTheVerdictRecordedAtScreenOpen()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        Assert.IsNotNull(dir);
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "Main", "Features", "TroopWeight", "Hooks", "TroopWeightDisplayHook.cs"));

        int build = src.IndexOf("private (string Label, bool IsOverCapacity)? BuildLabel(", StringComparison.Ordinal);
        Assert.AreNotEqual(-1, build);
        int end = src.IndexOf("FormatPartyListLabel(healthy", build, StringComparison.Ordinal);
        var body = src.Substring(build, end - build);

        StringAssert.Contains(body, "PartyScreenOwnLimits.IsOwnLimit(");
        StringAssert.Contains(body, "?? screenLimit == party.PartySizeLimit");
    }

    // With the verdict recorded at open, the header stays weighted while heavy troops are dragged in, so the weighted
    // frame can be over capacity while vanilla's raw test is not. The main side then raises the red tint (and so the
    // Over Limit prompt at Done) under vanilla's own mode gate for that flag (PartyVM :3069).
    [TestMethod]
    public void HeaderTint_MainSide_FollowsTheWeightedFrame_UnderVanillasModeGate()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        Assert.IsNotNull(dir);
        var src = File.ReadAllText(Path.Combine(dir!.FullName, "Main", "Features", "TroopWeight", "Hooks", "TroopWeightDisplayHook.cs"));

        int start = src.IndexOf("public void OnRefreshPartyInformation(", StringComparison.Ordinal);
        int end = src.IndexOf("var other = BuildLabel(", start, StringComparison.Ordinal);
        var main = src.Substring(start, end - start);

        StringAssert.Contains(main, "main.Value.IsOverCapacity && partyVm.AreMembersRelevantOnCurrentMode");
    }
}
