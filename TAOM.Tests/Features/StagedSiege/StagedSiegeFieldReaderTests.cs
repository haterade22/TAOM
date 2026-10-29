using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The seam Creature Siege Role asks: does the staged plan steer this side right now.</summary>
[TestClass]
public class StagedSiegeFieldReaderTests
{
    private object _token = null!;
    private StagedSiegeService _service = null!;
    private readonly StagedSiegeFieldReader _reader = new();

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        _token = new object();
        var adapter = Substitute.For<IStagedSiegeMissionAdapter>();
        adapter.MissionToken.Returns(_token);
        _service = new StagedSiegeService(adapter, StagedSiegeSceneCatalog.FromText(null, Substitute.For<IModLogger>()),
            Substitute.For<IModLogger>());
    }

    [TestCleanup]
    public void Cleanup() => StagedSiegeSlot.ClearArmed();

    private void PublishAt(StagedSide player, StageId? stage, bool finished = false)
    {
        var machine = new StageMachine(player);
        if (finished)
            machine.Force(null);
        else if (stage.HasValue)
            machine.Force(stage);
        StagedSiegeSlot.Arm(_service);
        StagedSiegeSlot.Publish(StageSnapshot.Build(_token, StagedSiegeFixtures.LinePlan(), machine, 0f, CaptureStatus.Idle));
    }

    [TestMethod]
    public void NothingPublished_OwnsNothing()
    {
        Assert.IsFalse(_reader.OwnsField(_token, StagedSide.Attacker));
        Assert.IsFalse(_reader.OwnsField(_token, StagedSide.Defender));
    }

    [TestMethod]
    public void AnotherMission_OwnsNothing()
    {
        PublishAt(StagedSide.Attacker, StageId.C);

        Assert.IsFalse(_reader.OwnsField(new object(), StagedSide.Attacker));
        Assert.IsFalse(_reader.OwnsField(new object(), StagedSide.Defender));
    }

    [TestMethod]
    public void Finished_OwnsNothing()
    {
        PublishAt(StagedSide.Attacker, null, finished: true);

        Assert.IsFalse(_reader.OwnsField(_token, StagedSide.Attacker));
        Assert.IsFalse(_reader.OwnsField(_token, StagedSide.Defender));
    }

    [DataTestMethod]
    [DataRow(StageId.A, StagedSide.Attacker, true)]
    [DataRow(StageId.A, StagedSide.Defender, false)]
    [DataRow(StageId.B, StagedSide.Attacker, false)]
    [DataRow(StageId.B, StagedSide.Defender, false)]
    [DataRow(StageId.C, StagedSide.Attacker, true)]
    [DataRow(StageId.C, StagedSide.Defender, true)]
    [DataRow(StageId.D, StagedSide.Attacker, true)]
    [DataRow(StageId.D, StagedSide.Defender, true)]
    public void EachStageAndSide_FollowsTheStagePlan(StageId stage, StagedSide side, bool owns)
    {
        PublishAt(StagedSide.Attacker, stage);

        Assert.AreEqual(owns, _reader.OwnsField(_token, side));
        Assert.AreEqual(owns, StagePlan.ModeFor(stage, side) != TacticMode.Vanilla, "the table in StagePlan is the single source");
    }
}
