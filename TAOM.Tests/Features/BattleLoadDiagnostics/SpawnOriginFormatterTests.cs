using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BattleLoadDiagnostics;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

// The formatter answers "which engine method built this agent" in one log token.
// It exists because location characters (a Musician on the reporter's machine, a Townsman on the
// in-house repro) appear in a TournamentFight mission whose 13 behaviors contain no
// MissionAgentHandler and whose participant roster (FightTournamentGame.GetParticipantCharacters)
// cannot select one — so no known code path explains them.
//
// The first cut shipped useless: it filtered on namespaces while the caller passed short type
// names, so nothing was ever skipped and all four slots went to our own prefix plus Harmony's
// generated wrappers. Live output was:
//   from=Agent_..._Patch.CaptureSpawnOrigin <- Agent_..._Patch.Prefix
//        <- .TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment_Patch2
//        <- .TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1
// Every frame noise; the real caller pushed past the limit. Hence: full names in, `_PatchN`
// wrappers normalised rather than dropped (they STAND IN for the real frame), and dedupe.
[TestClass]
public class SpawnOriginFormatterTests
{
    [TestMethod]
    public void Format_ShortensToTypeAndMethod()
    {
        var result = SpawnOriginFormatter.Format(
            new[]
            {
                "TaleWorlds.MountAndBlade.Mission.BuildAgent",
                "SandBox.Missions.MissionLogics.MissionAgentHandler.SpawnLocationCharacters",
            },
            maxFrames: 4);

        Assert.AreEqual("Mission.BuildAgent <- MissionAgentHandler.SpawnLocationCharacters", result);
    }

    // A Harmony wrapper REPLACES the original frame on the stack. Dropping it would lose the very
    // method we are trying to name, so the suffix is stripped and the frame kept.
    [TestMethod]
    public void Format_NormalisesHarmonyWrapperSuffix()
    {
        var result = SpawnOriginFormatter.Format(
            new[] { "TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1" }, maxFrames: 4);

        Assert.AreEqual("Mission.BuildAgent", result);
    }

    // The wrapper and the original both appear once Harmony is in the chain. One entry, not two.
    [TestMethod]
    public void Format_CollapsesConsecutiveDuplicatesAfterNormalising()
    {
        var result = SpawnOriginFormatter.Format(
            new[]
            {
                "TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment_Patch2",
                "TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment",
                "TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1",
            },
            maxFrames: 4);

        Assert.AreEqual("Agent.EquipItemsFromSpawnEquipment <- Mission.BuildAgent", result);
    }

    // The regression that made the first cut useless: our own frames must go, and the filter has
    // to see the namespace the caller actually supplies.
    [TestMethod]
    public void Format_SkipsOwnDiagnosticFramesByFullName()
    {
        var result = SpawnOriginFormatter.Format(
            new[]
            {
                "TAOM.Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.CaptureSpawnOrigin",
                "TAOM.Features.BattleLoadDiagnostics.Hooks.Agent_EquipItemsFromSpawnEquipment_BattleLoad_Patch.Prefix",
                "TaleWorlds.MountAndBlade.Agent.EquipItemsFromSpawnEquipment_Patch2",
                "TaleWorlds.MountAndBlade.Mission.BuildAgent_Patch1",
                "TaleWorlds.MountAndBlade.Mission.SpawnAgent",
                "SandBox.Missions.MissionLogics.MissionAgentHandler.SpawnLocationCharacters",
            },
            maxFrames: 4);

        Assert.AreEqual(
            "Agent.EquipItemsFromSpawnEquipment <- Mission.BuildAgent <- Mission.SpawnAgent <- MissionAgentHandler.SpawnLocationCharacters",
            result);
    }

    [TestMethod]
    public void Format_SkipsHarmonyInternals()
    {
        var result = SpawnOriginFormatter.Format(
            new[] { "HarmonyLib.Traverse.GetValue", "TaleWorlds.MountAndBlade.Mission.SpawnAgent" },
            maxFrames: 4);

        Assert.AreEqual("Mission.SpawnAgent", result);
    }

    [TestMethod]
    public void Format_TruncatesToMaxFrames()
    {
        var result = SpawnOriginFormatter.Format(new[] { "N.A.x", "N.B.y", "N.C.z" }, maxFrames: 2);
        Assert.AreEqual("A.x <- B.y", result);
    }

    [TestMethod]
    public void Format_SkipsNullAndBlankFrames()
    {
        var result = SpawnOriginFormatter.Format(
            new[] { null, "  ", "TaleWorlds.MountAndBlade.Mission.SpawnAgent" }, maxFrames: 3);
        Assert.AreEqual("Mission.SpawnAgent", result);
    }

    // A frame with no dot (a dynamic method with no declaring type) is kept verbatim rather than
    // dropped — an unrecognised name still beats a silently shorter chain.
    [TestMethod]
    public void Format_KeepsUnqualifiedFrameVerbatim()
    {
        Assert.AreEqual("lambda_method", SpawnOriginFormatter.Format(new[] { "lambda_method" }, maxFrames: 3));
    }

    // Every degenerate input yields empty so the caller omits the token entirely rather than
    // logging `from=` with nothing after it.
    [TestMethod]
    public void Format_WithNothingUsable_ReturnsEmpty()
    {
        Assert.AreEqual(string.Empty, SpawnOriginFormatter.Format(null, maxFrames: 3));
        Assert.AreEqual(string.Empty, SpawnOriginFormatter.Format(new string?[0], maxFrames: 3));
        Assert.AreEqual(string.Empty, SpawnOriginFormatter.Format(new[] { "HarmonyLib.X.y" }, maxFrames: 3));
        Assert.AreEqual(string.Empty, SpawnOriginFormatter.Format(new[] { "N.A.x" }, maxFrames: 0));
    }
}
