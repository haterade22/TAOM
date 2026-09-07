using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.MarriageAlignment;

/// <summary>
/// Drift-guards for Patch81's IL assumption. The <c>[HarmonyPatch]</c> TARGET auto-enrolls in
/// <c>HarmonyPatchBindingTests</c>, but that only proves the method still exists. The transpiler
/// additionally requires that <c>Clan.All</c> is read exactly twice inside
/// <c>RomanceCampaignBehavior.CheckNpcMarriages</c> (the indexer receiver plus the <c>.Count</c>
/// argument of the single partner-clan draw). If the engine rewrites that line, the transpiler
/// self-bails and Free clans quietly go back to marrying at ~41% of their rate, with the block
/// still in force. That is a safe degradation but an invisible one, so it gets a red test rather
/// than a log line nobody reads.
/// </summary>
[TestClass]
public class MarriageAlignmentBindingTests
{
    private const string RomanceBehavior = "TaleWorlds.CampaignSystem.CampaignBehaviors.RomanceCampaignBehavior";
    private const string ClanTypeName = "TaleWorlds.CampaignSystem.Clan";
    private const string TargetMethod = "CheckNpcMarriages";

    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static System.Type RequireType(string name)
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var type = AccessTools.TypeByName(name);
        Assert.IsNotNull(type, name + " did not resolve against the installed engine.");
        return type;
    }

    private static MethodInfo RequireTarget()
    {
        var behavior = RequireType(RomanceBehavior);
        var method = AccessTools.Method(behavior, TargetMethod);
        Assert.IsNotNull(method,
            $"RomanceCampaignBehavior.{TargetMethod} did not resolve — the Patch81 transpiler target drifted.");
        return method;
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CheckNpcMarriages_IsAnInstanceMethodTakingAClan()
    {
        var method = RequireTarget();
        var clan = RequireType(ClanTypeName);

        // The transpiler emits Ldarg_1 for the considering clan. That is only correct while the
        // method is an INSTANCE method whose first parameter is the Clan.
        Assert.IsFalse(method.IsStatic,
            "CheckNpcMarriages went static — Ldarg_1 would now load the wrong argument.");
        var parameters = method.GetParameters();
        Assert.AreEqual(1, parameters.Length, "CheckNpcMarriages parameter count changed.");
        Assert.AreEqual(clan, parameters[0].ParameterType, "CheckNpcMarriages no longer takes a Clan.");
    }

    [TestMethod]
    [TestCategory("BindingVerification")]
    public void CheckNpcMarriages_ReadsClanAll_ExactlyTwice()
    {
        var method = RequireTarget();
        var clan = RequireType(ClanTypeName);

        var clanAllReads = ReadMethodBody(method)
            .Count(pair => pair.Value is MethodInfo mi
                           && mi.Name == "get_All"
                           && mi.DeclaringType == clan);

        Assert.AreEqual(2, clanAllReads,
            "Patch81 splices every Clan.All read in CheckNpcMarriages and requires exactly 2 " +
            $"(the indexer receiver and the .Count argument of the partner-clan draw); found {clanAllReads}. " +
            "The transpiler will self-bail, so cross-alignment marriages stay blocked, but the AI " +
            "partner-search steering is lost and Free clans will marry far less often.");
    }

    /// <summary>
    /// Harmony's own offline IL reader. Unlike <c>GetCurrentInstructions</c> it needs no live
    /// patching context, so this runs in the unit-test host against the installed assemblies.
    /// </summary>
    private static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBody(MethodBase method) =>
        PatchProcessor.ReadMethodBody(method);
}
