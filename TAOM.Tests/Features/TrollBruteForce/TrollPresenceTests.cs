using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.TrollBruteForce;

namespace TAOM.Tests.Features.TrollBruteForce;

// The troll mission behavior runs its formation-spacing and clip trackers (two agent scans twice a second) only once
// the mission has built a Brute Force troll. The latch decides that; a mission with no troll never sets it.
[TestClass]
public class TrollPresenceTests
{
    private const string NoTrollLine =
        "[TrollBruteForce] No Brute Force troll built this mission: formation spacing and clip trace never ran";

    private IModLogger _logger = null!;
    private TrollPresence _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new TrollPresence(new TrollBruteForceService(), _logger);
    }

    [TestMethod]
    public void Seen_BeforeAnyAgent_IsFalse()
    {
        Assert.IsFalse(_sut.Seen);
    }

    [TestMethod]
    public void Note_OtherMonsters_LeaveItUnset()
    {
        _sut.Note(null);
        _sut.Note("human");
        _sut.Note("cave_troll_settlement");

        Assert.IsFalse(_sut.Seen);
    }

    [TestMethod]
    public void Note_ABattleTroll_SetsIt_AndALaterHumanDoesNotClearIt()
    {
        _sut.Note("hill_troll");
        _sut.Note("human");

        Assert.IsTrue(_sut.Seen);
    }

    [TestMethod]
    public void Clear_AtMissionEnd_UnsetsIt()
    {
        _sut.Note("cave_troll");

        _sut.Clear();

        Assert.IsFalse(_sut.Seen);
    }

    // taom_debug.log (plan 030, DECISIONS D6): the gate says once when it opens and once when it never did.
    [TestMethod]
    public void Note_FirstBattleTroll_LogsOnceNamingTheMonster()
    {
        _sut.Note("human");
        _sut.Note("cave_troll");
        _sut.Note("hill_troll");

        _logger.Received(1).LogInfo(Arg.Any<string>());
        _logger.Received(1).LogInfo(
            "[TrollBruteForce] First Brute Force troll built ('cave_troll'): formation spacing and clip trace start ticking");
    }

    [TestMethod]
    public void Clear_NoTrollThisMission_LogsWhyTheTrackersNeverRan()
    {
        _sut.Note("human");

        _sut.Clear();

        _logger.Received(1).LogInfo(NoTrollLine);
    }

    [TestMethod]
    public void Clear_AfterATroll_WritesNoSkipLine_AndTheNextMissionLogsItsFirstTrollAgain()
    {
        _sut.Note("cave_troll");
        _sut.Clear();
        _sut.Note("hill_troll");

        _logger.DidNotReceive().LogInfo(NoTrollLine);
        _logger.Received(1).LogInfo(
            "[TrollBruteForce] First Brute Force troll built ('hill_troll'): formation spacing and clip trace start ticking");
    }
}
