using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.DwarfAttacks;

// The in-game evidence for the dwarf attacks (#1087, tools/ingame_checks/1087.toml). An attack is a per-hit event,
// so each one only counts; the [Check] line is written once per attack kind for the process, on the first landed hit
// or on the first attack whose clip never played (csharp-architecture.md "Outcome cost").

namespace TAOM.Tests.Features.DwarfAttacks;

[TestClass]
public class DwarfAttackChecksTests
{
    private IOutcomeLog _log = null!;
    private DwarfAttackChecks _checks = null!;

    [TestInitialize]
    public void Setup()
    {
        _log = Substitute.For<IOutcomeLog>();
        var real = new OutcomeLog(Substitute.For<IModLogger>());
        _log.Counter(Arg.Any<string>()).Returns(ci => real.Counter(ci.Arg<string>()));
        _checks = new DwarfAttackChecks(_log);
    }

    [TestMethod]
    public void RecordHit_FirstOfEachKind_WritesOnePassLine()
    {
        _checks.RecordHit(DwarfAttack.HeadButt, "Dwarf", "Orc", 1.1f, 0.35f, 0.1f, 18, false);
        _checks.RecordHit(DwarfAttack.HeadButt, "Dwarf", "Orc", 1.2f, 0.36f, 0.1f, 18, false);
        _checks.RecordHit(DwarfAttack.DropKick, "Dwarf", "Orc", 1.4f, 0.38f, 1.3f, 25, true);

        _log.Received(1).Pass(DwarfAttackChecks.HeadButtId, "", Arg.Any<string>());
        _log.Received(1).Pass(DwarfAttackChecks.DropKickId, "", Arg.Any<string>());
    }

    [TestMethod]
    public void RecordHit_Detail_NamesWhatTheSmokeNeeds()
    {
        _checks.RecordHit(DwarfAttack.DropKick, "Gimli", "Uruk", 1.42f, 0.38f, 1.29f, 25, true);

        _log.Received(1).Pass(DwarfAttackChecks.DropKickId, "",
            "dwarf=Gimli victim=Uruk distance=1.42 progress=0.38 travelled=1.29 damage=25 knockdown=1");
    }

    [TestMethod]
    public void RecordHit_EveryHit_Counts()
    {
        var checks = new DwarfAttackChecks(new OutcomeLog(Substitute.For<IModLogger>()));
        checks.RecordHit(DwarfAttack.HeadButt, "a", "b", 1f, 0.4f, 0f, 18, false);
        checks.RecordHit(DwarfAttack.HeadButt, "a", "b", 1f, 0.4f, 0f, 18, false);
        checks.RecordNoVictim(DwarfAttack.HeadButt);

        Assert.AreEqual(2, checks.Counter(DwarfAttack.HeadButt).Passes);
        Assert.AreEqual(1, checks.Counter(DwarfAttack.HeadButt).Fails);
        Assert.AreEqual(0, checks.Counter(DwarfAttack.DropKick).Passes);
    }

    [TestMethod]
    public void RecordNeverPlayed_WritesOneFailLinePerKind()
    {
        _checks.RecordNeverPlayed(DwarfAttack.DropKick, "Dwarf");
        _checks.RecordNeverPlayed(DwarfAttack.DropKick, "Dwarf");

        _log.Received(1).Fail(DwarfAttackChecks.DropKickId, "", Arg.Any<string>());
        _log.DidNotReceive().Fail(DwarfAttackChecks.HeadButtId, Arg.Any<string>(), Arg.Any<string>());
    }

    // Cut short is normal in a melee (pass); never played is a data fault (fail). Both only count.
    [TestMethod]
    public void CutShortAndNeverPlayed_CountOnTheirOwnCounter()
    {
        var log = new OutcomeLog(Substitute.For<IModLogger>());
        var checks = new DwarfAttackChecks(log);
        checks.RecordCutShort(DwarfAttack.DropKick);
        checks.RecordCutShort(DwarfAttack.DropKick);
        checks.RecordNeverPlayed(DwarfAttack.DropKick, "Dwarf");

        OutcomeCounter cutShort = log.Counter(DwarfAttackChecks.DropKickId + "-cut-short");
        Assert.AreEqual(2, cutShort.Passes);
        Assert.AreEqual(1, cutShort.Fails);
        Assert.AreEqual(0, checks.Counter(DwarfAttack.DropKick).Passes);
        Assert.AreEqual(0, log.Counter(DwarfAttackChecks.HeadButtId + "-cut-short").Passes);
    }

    [TestMethod]
    public void RecordNoVictim_WritesNoLine()
    {
        _checks.RecordNoVictim(DwarfAttack.HeadButt);
        _checks.RecordCutShort(DwarfAttack.HeadButt);

        _log.DidNotReceiveWithAnyArgs().Pass(default!, default!, default!);
        _log.DidNotReceiveWithAnyArgs().Fail(default!, default!, default!);
    }
}
