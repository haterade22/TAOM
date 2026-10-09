using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.WarChronicle.Rally;

namespace TAOM.Tests.Features.WarChronicle.Rally;

/// <summary>
/// The hysteresis, edge by edge, against the shipped defaults: tier 1 at 25% lost and back to 0 below 15%,
/// tier 2 at 50% and back to 1 below 40%.
/// </summary>
[TestClass]
public class RallyTierMachineTests
{
    private static readonly RallyConfig Config = new RallyConfig();

    // ---- from tier 0 ----

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(0.10f)]
    [DataRow(0.15f)]
    [DataRow(0.2499f)]
    public void Next_FromZeroBelowTier1Enter_StaysZero(float loss)
    {
        Assert.AreEqual(0, RallyTierMachine.Next(0, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.25f)]
    [DataRow(0.30f)]
    [DataRow(0.4999f)]
    public void Next_FromZeroAtOrAboveTier1Enter_RisesToOne(float loss)
    {
        Assert.AreEqual(1, RallyTierMachine.Next(0, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.50f)]
    [DataRow(0.75f)]
    [DataRow(1f)]
    public void Next_FromZeroAtOrAboveTier2Enter_JumpsToTwo(float loss)
    {
        Assert.AreEqual(2, RallyTierMachine.Next(0, loss, Config));
    }

    // ---- from tier 1 ----

    [DataTestMethod]
    [DataRow(0.15f)]
    [DataRow(0.20f)]
    [DataRow(0.25f)]
    [DataRow(0.4999f)]
    public void Next_FromOneInsideTheBand_StaysOne(float loss)
    {
        Assert.AreEqual(1, RallyTierMachine.Next(1, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.1499f)]
    [DataRow(0.05f)]
    [DataRow(0f)]
    public void Next_FromOneBelowTier1Exit_FallsToZero(float loss)
    {
        Assert.AreEqual(0, RallyTierMachine.Next(1, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.50f)]
    [DataRow(0.9f)]
    public void Next_FromOneAtOrAboveTier2Enter_RisesToTwo(float loss)
    {
        Assert.AreEqual(2, RallyTierMachine.Next(1, loss, Config));
    }

    // ---- from tier 2 ----

    [DataTestMethod]
    [DataRow(0.40f)]
    [DataRow(0.45f)]
    [DataRow(0.50f)]
    [DataRow(1f)]
    public void Next_FromTwoAtOrAboveTier2Exit_StaysTwo(float loss)
    {
        Assert.AreEqual(2, RallyTierMachine.Next(2, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.3999f)]
    [DataRow(0.30f)]
    [DataRow(0.15f)]
    public void Next_FromTwoBelowTier2ExitButAboveTier1Exit_FallsToOne(float loss)
    {
        Assert.AreEqual(1, RallyTierMachine.Next(2, loss, Config));
    }

    [DataTestMethod]
    [DataRow(0.1499f)]
    [DataRow(0.05f)]
    [DataRow(0f)]
    public void Next_FromTwoBelowTier1Exit_FallsStraightToZero(float loss)
    {
        Assert.AreEqual(0, RallyTierMachine.Next(2, loss, Config));
    }

    // ---- hysteresis in motion ----

    [TestMethod]
    public void Next_ALossHoveringAroundTier1Enter_DoesNotFlap()
    {
        var tier = 0;
        foreach (var loss in new[] { 0.26f, 0.24f, 0.26f, 0.20f, 0.16f, 0.24f })
            tier = RallyTierMachine.Next(tier, loss, Config);

        Assert.AreEqual(1, tier, "once risen it holds until the loss drops below the exit");
    }

    [TestMethod]
    public void Next_TheWholeClimbAndRecovery_WalksEveryTier()
    {
        var path = new System.Collections.Generic.List<int>();
        var tier = 0;
        foreach (var loss in new[] { 0.10f, 0.30f, 0.55f, 0.45f, 0.35f, 0.20f, 0.10f })
        {
            tier = RallyTierMachine.Next(tier, loss, Config);
            path.Add(tier);
        }

        CollectionAssert.AreEqual(new[] { 0, 1, 2, 2, 1, 1, 0 }, path);
    }

    // ---- degenerate inputs ----

    [DataTestMethod]
    [DataRow(0, float.NaN)]
    [DataRow(1, float.NaN)]
    [DataRow(2, float.NaN)]
    [DataRow(0, float.PositiveInfinity)]
    [DataRow(2, float.PositiveInfinity)]
    [DataRow(2, float.NegativeInfinity)]
    public void Next_ANonFiniteLoss_FailsClosedToZero(int current, float loss)
    {
        Assert.AreEqual(0, RallyTierMachine.Next(current, loss, Config));
    }

    [DataTestMethod]
    [DataRow(-1)]
    [DataRow(3)]
    [DataRow(99)]
    public void Next_AnUnknownCurrentTier_IsTreatedAsZero(int current)
    {
        Assert.AreEqual(1, RallyTierMachine.Next(current, 0.30f, Config));
        Assert.AreEqual(0, RallyTierMachine.Next(current, 0.20f, Config));
    }

    [TestMethod]
    public void Next_UsesTheConfiguredThresholds()
    {
        var config = new RallyConfig
        {
            Tier1 = new RallyTierConfig { EnterLoss = 0.10f, ExitLoss = 0.05f },
            Tier2 = new RallyTierConfig { EnterLoss = 0.20f, ExitLoss = 0.12f },
        };

        Assert.AreEqual(1, RallyTierMachine.Next(0, 0.10f, config));
        Assert.AreEqual(2, RallyTierMachine.Next(0, 0.20f, config));
        Assert.AreEqual(2, RallyTierMachine.Next(2, 0.12f, config));
        Assert.AreEqual(1, RallyTierMachine.Next(2, 0.11f, config));
        Assert.AreEqual(0, RallyTierMachine.Next(1, 0.04f, config));
    }
}
