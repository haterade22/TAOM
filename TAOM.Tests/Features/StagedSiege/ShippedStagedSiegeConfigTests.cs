using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>The REAL <c>siege/staged_siege.json</c> the module ships, read the way the game reads it.</summary>
[TestClass]
public class ShippedStagedSiegeConfigTests
{
    private const string Scene = "sturgia_castle_siege_001";

    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static StagedSiegeSceneCatalog Load(IModLogger logger)
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        return new StagedSiegeSceneCatalog(paths, logger);
    }

    [TestMethod]
    public void ShippedFile_Exists()
    {
        Assert.IsTrue(File.Exists(Path.Combine(ModuleData, "siege", "staged_siege.json")));
    }

    [TestMethod]
    public void ShippedFile_LoadsPilotWithNoWarnings()
    {
        var logger = Substitute.For<IModLogger>();

        var catalog = Load(logger);

        Assert.AreEqual(1, catalog.SceneCount);
        Assert.IsTrue(catalog.TryGetScene(Scene, out _));
        logger.DidNotReceive().LogWarning(Arg.Any<string>());
        logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void ShippedFile_PilotMatchesTheRecords()
    {
        Assert.IsTrue(Load(Substitute.For<IModLogger>()).TryGetScene(Scene, out var plan));

        AssertPoint(plan.Zone(StageId.A), 559.6358f, 554.8117f, 67.8682861f);
        AssertPoint(plan.Zone(StageId.B), 555.8358f, 612.657166f, 80.72999f);
        AssertPoint(plan.Zone(StageId.C), 556.5566f, 650.799255f, 85.54184f);
        AssertPoint(plan.Zone(StageId.D), 522.4266f, 658.0432f, 89.89123f);

        AssertFrame(plan.Detachment, 558.3057f, 593.3017f, 77.92095f, 0.0932610855f, -0.9956417f);
        AssertFrame(plan.DefenderFrame(StageId.B)!.Value, 553.6959f, 627.306946f, 80.5601044f, 0.102272816f, -0.9947564f);
        AssertFrame(plan.DefenderFrame(StageId.C)!.Value, 529.451538f, 636.070251f, 85.41135f, 0.8701325f, 0.492817879f);
        AssertFrame(plan.DefenderFrame(StageId.D)!.Value, 518.0589f, 661.8975f, 90.25453f, 0.7023726f, -0.711809456f);
    }

    [TestMethod]
    public void ShippedFile_TunablesAreDefaults()
    {
        var t = Load(Substitute.For<IModLogger>()).Tunables;
        var d = StagedSiegeTunables.Default;

        Assert.AreEqual(d.CaptureRadius, t.CaptureRadius);
        Assert.AreEqual(d.HeightBand, t.HeightBand);
        Assert.AreEqual(d.MinAttackers, t.MinAttackers);
        Assert.AreEqual(d.DominanceRatio, t.DominanceRatio);
        Assert.AreEqual(d.CaptureSeconds, t.CaptureSeconds);
        Assert.AreEqual(d.DecaySeconds, t.DecaySeconds);
        Assert.AreEqual(d.ReconcileSeconds, t.ReconcileSeconds);
        Assert.AreEqual(d.DetachmentShare, t.DetachmentShare);
        Assert.AreEqual(d.DetachmentCap, t.DetachmentCap);
        Assert.AreEqual(d.TrickleSeconds, t.TrickleSeconds);
        Assert.AreEqual(d.RefillBurstSeconds, t.RefillBurstSeconds);
        Assert.AreEqual(d.RefillBurstIntervalSeconds, t.RefillBurstIntervalSeconds);
        Assert.AreEqual(d.ReinforcementBatchPercentage, t.ReinforcementBatchPercentage);
        Assert.AreEqual(d.DesiredReinforcementPercentage, t.DesiredReinforcementPercentage);
        CollectionAssert.AreEqual(d.ReserveShares.Attacker, t.ReserveShares.Attacker);
        CollectionAssert.AreEqual(d.ReserveShares.DefenderPlayerAttacks, t.ReserveShares.DefenderPlayerAttacks);
        CollectionAssert.AreEqual(d.ReserveShares.DefenderPlayerDefends, t.ReserveShares.DefenderPlayerDefends);
    }

    [TestMethod]
    public void ShippedFile_PointsNameNoRadius_SoTheCaptureRadiusTunableReachesThem()
    {
        var text = File.ReadAllText(Path.Combine(ModuleData, "siege", "staged_siege.json"));

        // Scoped to the pilot's points: a later scene may name its own radius on purpose (the doc allows it).
        var pilotPoints = (JObject)JObject.Parse(text)["scenes"]![Scene]!["points"]!;
        foreach (var point in pilotPoints.Properties())
            Assert.IsNull(point.Value["radius"], $"pilot point {point.Name} names a radius, which would shadow the captureRadius tunable");
        var edited = text.Replace("\"captureRadius\": 8", "\"captureRadius\": 12");
        Assert.AreNotEqual(text, edited);
        var catalog = StagedSiegeSceneCatalog.FromText(edited, Substitute.For<IModLogger>());
        Assert.IsTrue(catalog.TryGetScene(Scene, out var plan));
        foreach (var id in new[] { StageId.A, StageId.B, StageId.C, StageId.D })
            Assert.AreEqual(12f, plan.Zone(id).Radius, id.ToString());
    }

    [TestMethod]
    public void TheDefaults_AreTheValuesTheSpecNames()
    {
        var d = StagedSiegeTunables.Default;

        Assert.AreEqual(8f, d.CaptureRadius);
        Assert.AreEqual(3, d.MinAttackers);
        Assert.AreEqual(20f, d.CaptureSeconds);
        Assert.AreEqual(30f, d.DecaySeconds);
        Assert.AreEqual(0.5f, d.ReconcileSeconds);
        Assert.AreEqual(60, d.DetachmentCap);
        Assert.AreEqual(0.166f, d.DesiredReinforcementPercentage);
        CollectionAssert.AreEqual(new[] { 0.20f, 0.30f, 0.25f, 0.25f }, d.ReserveShares.Attacker);
    }

    private static void AssertPoint(StageZone zone, float x, float y, float z)
    {
        Assert.AreEqual(x, zone.Center.X);
        Assert.AreEqual(y, zone.Center.Y);
        Assert.AreEqual(z, zone.Center.Z);
        Assert.AreEqual(StagedSiegeTunables.Default.CaptureRadius, zone.Radius, "no point names a radius: the tunable's default applies");
    }

    private static void AssertFrame(StageFrame frame, float x, float y, float z, float fx, float fy)
    {
        Assert.AreEqual(x, frame.Position.X);
        Assert.AreEqual(y, frame.Position.Y);
        Assert.AreEqual(z, frame.Position.Z);
        Assert.AreEqual(fx, frame.Facing.X, 1e-4f);
        Assert.AreEqual(fy, frame.Facing.Y, 1e-4f);
    }
}
