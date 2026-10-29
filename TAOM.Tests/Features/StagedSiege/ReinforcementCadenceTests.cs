using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>When each side may release a batch: a slow trickle, and a quick burst after every captured point.</summary>
[TestClass]
public class ReinforcementCadenceTests
{
    private static readonly StagedSiegeTunables T = StagedSiegeTunables.Default;

    [TestMethod]
    public void Due_AfterReset_FalseUntilTrickle()
    {
        var c = new ReinforcementCadence(T);
        c.Reset(StagedSide.Attacker, 100f);

        Assert.IsFalse(c.Due(StagedSide.Attacker, 100f));
        Assert.IsFalse(c.Due(StagedSide.Attacker, 100f + T.TrickleSeconds - 0.1f));
        Assert.IsTrue(c.Due(StagedSide.Attacker, 100f + T.TrickleSeconds));
    }

    [TestMethod]
    public void Due_BeforeAnyReset_False()
    {
        Assert.IsFalse(new ReinforcementCadence(T).Due(StagedSide.Defender, 1e6f));
    }

    [TestMethod]
    public void OnStageChanged_BothSidesDueNow()
    {
        var c = new ReinforcementCadence(T);
        c.Reset(StagedSide.Attacker, 0f);
        c.Reset(StagedSide.Defender, 0f);

        c.OnStageChanged(50f);

        Assert.IsTrue(c.Due(StagedSide.Attacker, 50f));
        Assert.IsTrue(c.Due(StagedSide.Defender, 50f));
    }

    [TestMethod]
    public void Fired_InBurst_NextAfterBurstInterval()
    {
        var c = new ReinforcementCadence(T);
        c.OnStageChanged(50f);

        c.Fired(StagedSide.Attacker, 51f);

        Assert.IsFalse(c.Due(StagedSide.Attacker, 51f + T.RefillBurstIntervalSeconds - 0.01f));
        Assert.IsTrue(c.Due(StagedSide.Attacker, 51f + T.RefillBurstIntervalSeconds));
    }

    [TestMethod]
    public void Fired_AfterBurst_NextAfterTrickle()
    {
        var c = new ReinforcementCadence(T);
        c.OnStageChanged(50f);
        var after = 50f + T.RefillBurstSeconds + 1f;

        c.Fired(StagedSide.Defender, after);

        Assert.IsFalse(c.Due(StagedSide.Defender, after + T.TrickleSeconds - 0.1f));
        Assert.IsTrue(c.Due(StagedSide.Defender, after + T.TrickleSeconds));
    }

    [TestMethod]
    public void Due_NaNNow_False()
    {
        var c = new ReinforcementCadence(T);
        c.OnStageChanged(0f);

        Assert.IsFalse(c.Due(StagedSide.Attacker, float.NaN));
    }
}
