using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.BattleLoadDiagnostics;
using TAOM.Features.BattleLoadDiagnostics.Domain;

namespace TAOM.Tests.Features.BattleLoadDiagnostics;

// Covers the agent-build blind window closed on 2026-08-02.
//
// A Dunland tournament CTD (reporter FESTERLITTLE) ended on `AgentEquipOk agent#0 'Musician'`
// with nothing after it. Two facts made that log unresolvable:
//   1. AgentEquipOk brackets Agent.EquipItemsFromSpawnEquipment, but Mission.BuildAgent keeps
//      going for another ~14 lines of native work (InitializeAgentRecord, BatchLastLodMeshes,
//      PreloadForRendering, SetActionChannel, InitializeComponents) with no stamp. A death there
//      is indistinguishable from a death between agents.
//   2. The Begin line named the character but not its race/monster/action set — and a
//      `musician_dunland` agent has no code path that spawns it into a TournamentFight mission
//      at all, so "which engine method built this agent" was the question the log could not answer.
[TestClass]
public class AgentBuildDiagnosticsTests
{
    private IModLogger _logger = null!;
    private IBattleLoadDiagnosticsSettingsProvider _settings = null!;
    private IEquipmentDumpFormatter _formatter = null!;
    private BattleLoadDiagnosticsService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _settings = Substitute.For<IBattleLoadDiagnosticsSettingsProvider>();
        _formatter = Substitute.For<IEquipmentDumpFormatter>();
        _formatter.Format(Arg.Any<EquipmentSnapshot>()).Returns(new List<string>());
        _settings.IsEnabled.Returns(true);
        _sut = new BattleLoadDiagnosticsService(_logger, _settings, _formatter);
    }

    private static EquipmentSnapshot Snap(
        string? race = "human",
        string? monsterId = "human_settlement",
        string? actionSetName = "as_human_villager",
        string? spawnOrigin = "TournamentFightMissionController.SpawnAgentWithRandomItems") =>
        new EquipmentSnapshot(
            0, "Musician", "musician_dunland", "empire",
            new[] { new EquipmentSlotSnapshot("Body", "sleeveless_padded_coat", null, null, null, null, "BodyArmor") },
            race, monsterId, actionSetName, spawnOrigin);

    // ---- identity on the Begin line (fires BEFORE the crash, so it must carry the payload) ----

    [TestMethod]
    public void LogAgentEquipBegin_IncludesRaceMonsterAndActionSet()
    {
        _sut.LogAgentEquipBegin(Snap());

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("race='human'") &&
            s.Contains("monster='human_settlement'") &&
            s.Contains("actionSet='as_human_villager'")));
    }

    [TestMethod]
    public void LogAgentEquipBegin_IncludesSpawnOrigin()
    {
        _sut.LogAgentEquipBegin(Snap());

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("from=TournamentFightMissionController.SpawnAgentWithRandomItems")));
    }

    // The identity fields come from engine getters that throw on a half-built agent, so the
    // adapter hands back nulls rather than failing the capture. The line must still emit —
    // a Begin line is the only durable proof the agent existed at all.
    [TestMethod]
    public void LogAgentEquipBegin_WithNoIdentity_StillEmitsAndOmitsEmptyTokens()
    {
        _sut.LogAgentEquipBegin(Snap(race: null, monsterId: null, actionSetName: null, spawnOrigin: null));

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("AgentEquipBegin") &&
            s.Contains("char='musician_dunland'") &&
            !s.Contains("from=")));
    }

    [TestMethod]
    public void LogAgentEquipBegin_PreservesExistingContract()
    {
        _sut.LogAgentEquipBegin(Snap());

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("agent#0") && s.Contains("'Musician'") &&
            s.Contains("culture='empire'") && s.Contains("slots=1")));
    }

    // ---- AgentBuildDone: the stamp that closes Mission.BuildAgent's native tail ----

    [TestMethod]
    public void LogAgentBuildDone_EmitsPhaseWithAgentIdentity()
    {
        _sut.LogAgentBuildDone(0, "Musician");

        _logger.Received().LogInfo(Arg.Is<string>(s =>
            s.Contains("AgentBuildDone") && s.Contains("agent#0") && s.Contains("'Musician'")));
    }

    [TestMethod]
    public void LogAgentBuildDone_WhenDisabled_WritesNothing()
    {
        _settings.IsEnabled.Returns(false);
        _sut.LogAgentBuildDone(0, "Musician");
        _logger.DidNotReceive().LogInfo(Arg.Any<string>());
    }

    // Durability is the whole point: DEBUG is the async path and a native crash drops it.
    // Same contract LogTaomBehaviorAdded is pinned by.
    [TestMethod]
    public void LogAgentBuildDone_UsesDurableInfoNotDebug()
    {
        _sut.LogAgentBuildDone(3, "Wainrider");

        _logger.Received().LogInfo(Arg.Is<string>(s => s.Contains("AgentBuildDone")));
        _logger.DidNotReceive().LogDebug(Arg.Is<string>(s => s.Contains("AgentBuildDone")));
    }

    // Ordering is the diagnostic: Begin -> Ok -> BuildDone. An Ok with no BuildDone localizes the
    // fault to Mission.BuildAgent's native tail, which is precisely what the crash log could not say.
    [TestMethod]
    public void AgentPhases_EmitInBeginOkBuildDoneOrder()
    {
        _sut.LogAgentEquipBegin(Snap());
        _sut.LogAgentEquipOk(0, "Musician");
        _sut.LogAgentBuildDone(0, "Musician");

        Received.InOrder(() =>
        {
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("AgentEquipBegin")));
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("AgentEquipOk")));
            _logger.LogInfo(Arg.Is<string>(s => s.Contains("AgentBuildDone")));
        });
    }
}
