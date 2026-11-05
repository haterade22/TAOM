using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.MissionDiagnostic;

namespace TAOM.Tests.Features.MissionDiagnostic;

// The action-set census logs one line per (action set, race, sex) per mission. The boundary pre-filters every agent
// on an integer key (action set index, race id, sex) so that only a combination not yet seen pays for the native
// name marshal and the agent name; the line itself and its first-sighting order must not change.
[TestClass]
public class MissionDiagnosticServiceTests
{
    private IModLogger _logger = null!;
    private MissionDiagnosticService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new MissionDiagnosticService(_logger);
    }

    [TestMethod]
    public void TryMarkActionSetKey_FirstSighting_TrueThenFalse()
    {
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
        Assert.IsFalse(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
    }

    [TestMethod]
    public void TryMarkActionSetKey_AnyPartDiffers_IsANewKey()
    {
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
        Assert.IsTrue(_sut.TryMarkActionSetKey(13, 3, isFemale: false), "another action set");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 4, isFemale: false), "another race");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: true), "the other sex");
        Assert.IsTrue(_sut.TryMarkActionSetKey(12, -1, isFemale: false), "no character");
    }

    [TestMethod]
    public void ResetForNewMission_ForgetsTheKeys()
    {
        _sut.TryMarkActionSetKey(12, 3, isFemale: false);

        _sut.ResetForNewMission();

        Assert.IsTrue(_sut.TryMarkActionSetKey(12, 3, isFemale: false));
    }

    [TestMethod]
    public void LogActionSetSeen_FirstAgentOfACombination_WritesTheUnchangedLineOnce()
    {
        _sut.LogActionSetSeen("as_human_warrior", "elf", true, "Legolas", "elf_archer", "elf_monster");
        _sut.LogActionSetSeen("as_human_warrior", "elf", true, "Tauriel", "elf_scout", "elf_monster");

        _logger.Received(1).LogInfo(Arg.Any<string>());
        _logger.Received(1).LogInfo("[MissionDiag] ActionSet 'as_human_warrior' used by race='elf' female=True " +
            "monster='elf_monster' (first agent: 'Legolas' char='elf_archer')");
    }

    // taom_debug.log is the crash-triage record (plan 030, DECISIONS D6): the census names its configuration when the
    // window opens and its totals when it closes, so a log shows how many agent checks the pre-filter absorbed.
    [TestMethod]
    public void LogActionSetCensusOpened_WritesTheConfigurationHeader()
    {
        _sut.LogActionSetCensusOpened(5f);

        _logger.Received(1).LogInfo("[MissionDiag] ActionSet census open: window=5.0s, one line per " +
            "(action set, race, sex); names are read only for a new (action set index, race id, sex) key");
    }

    [TestMethod]
    public void LogActionSetCensusClosed_ReportsChecksNewKeysAndLines()
    {
        _sut.TryMarkActionSetKey(12, 3, isFemale: false);
        _sut.TryMarkActionSetKey(12, 3, isFemale: false);
        _sut.TryMarkActionSetKey(13, 3, isFemale: false);
        _sut.LogActionSetSeen("as_human_warrior", "human", false, "Aragorn", "aragorn", "human");
        _sut.LogActionSetSeen("as_human_warrior", "human", false, "Boromir", "boromir", "human");
        _sut.LogActionSetSeen("as_dwarf_warrior", "dwarf", false, "Gimli", "gimli", "dwarf");

        _sut.LogActionSetCensusClosed();

        _logger.Received(1).LogInfo("[MissionDiag] ActionSet census closed: agentChecks=3 newKeys=2 lines=2");
    }

    [TestMethod]
    public void ResetForNewMission_ZeroesTheCensusTotals()
    {
        _sut.TryMarkActionSetKey(12, 3, isFemale: false);
        _sut.LogActionSetSeen("as_human_warrior", "human", false, "Aragorn", "aragorn", "human");

        _sut.ResetForNewMission();
        _sut.LogActionSetCensusClosed();

        _logger.Received(1).LogInfo("[MissionDiag] ActionSet census closed: agentChecks=0 newKeys=0 lines=0");
    }
}
