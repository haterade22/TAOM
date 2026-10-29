using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// One test per validation rule of <c>siege/staged_siege.json</c> (csharp-architecture.md "Config Providers MUST Validate"):
/// a bad tunable reverts to its default with a warning naming the key, a bad scene is skipped without dropping a good one,
/// and NaN or infinity is rejected before any range check.
/// </summary>
[TestClass]
public class StagedSiegeSceneCatalogTests
{
    // Single quotes keep the C# readable; Newtonsoft accepts them. Each tunable sits on its own "'key': value," so a test can
    // swap one value for a NaN or Infinity literal by text.
    internal const string PilotText = @"{
  'captureRadius': 8,
  'heightBand': 3,
  'minAttackers': 3,
  'dominanceRatio': 3,
  'captureSeconds': 20,
  'decaySeconds': 30,
  'reconcileSeconds': 0.5,
  'detachmentShare': 0.25,
  'detachmentCap': 60,
  'trickleSeconds': 10,
  'refillBurstSeconds': 20,
  'refillBurstIntervalSeconds': 1,
  'reinforcementBatchPercentage': 0.05,
  'desiredReinforcementPercentage': 0.166,
  'reserveShares': {
    'attacker': [0.20, 0.30, 0.25, 0.25],
    'defenderPlayerAttacks': [0.15, 0.25, 0.20, 0.20],
    'defenderPlayerDefends': [0.15, 0.30, 0.25, 0.30]
  },
  'scenes': {
    'sturgia_castle_siege_001': {
      'points': {
        'A': { 'x': 559.6358, 'y': 554.8117, 'z': 67.8682861, 'radius': 8 },
        'B': { 'x': 555.8358, 'y': 612.657166, 'z': 80.72999, 'radius': 8 },
        'C': { 'x': 556.5566, 'y': 650.799255, 'z': 85.54184, 'radius': 8 },
        'D': { 'x': 522.4266, 'y': 658.0432, 'z': 89.89123, 'radius': 8 }
      },
      'detachment': { 'x': 558.3057, 'y': 593.3017, 'z': 77.92095, 'fx': 0.0932610855, 'fy': -0.9956417 },
      'defenders': {
        'B': { 'x': 553.6959, 'y': 627.306946, 'z': 80.5601044, 'fx': 0.102272816, 'fy': -0.9947564 },
        'C': { 'x': 529.451538, 'y': 636.070251, 'z': 85.41135, 'fx': 0.8701325, 'fy': 0.492817879 },
        'D': { 'x': 518.0589, 'y': 661.8975, 'z': 90.25453, 'fx': 0.7023726, 'fy': -0.711809456 }
      }
    }
  }
}";

    private const string Scene = "sturgia_castle_siege_001";

    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup() => _logger = Substitute.For<IModLogger>();

    private StagedSiegeSceneCatalog Load(string? text) => StagedSiegeSceneCatalog.FromText(text, _logger);

    private static JObject Pilot() => JObject.Parse(PilotText);

    private StagedSiegeSceneCatalog Load(JObject json) => Load(json.ToString(Formatting.None));

    private static JObject SceneOf(JObject root) => (JObject)root["scenes"]![Scene]!;

    private void AssertWarnedAbout(string fragment) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains(fragment)));

    private void AssertNoWarnings() => _logger.DidNotReceive().LogWarning(Arg.Any<string>());

    private static double Read(StagedSiegeTunables t, string key) =>
        Convert.ToDouble(typeof(StagedSiegeTunables).GetProperty(char.ToUpperInvariant(key[0]) + key.Substring(1))!.GetValue(t));

    private static void AssertAllDefaults(StagedSiegeTunables t)
    {
        foreach (var p in typeof(StagedSiegeTunables).GetProperties().Where(p => p.PropertyType == typeof(float) || p.PropertyType == typeof(int)))
            Assert.AreEqual(Convert.ToDouble(p.GetValue(StagedSiegeTunables.Default)), Convert.ToDouble(p.GetValue(t)), p.Name);
    }

    // --- whole-file failures -----------------------------------------------------------------------------------------

    [TestMethod]
    public void Load_MissingFile_NoScenesDefaultsWarns()
    {
        var dir = Path.Combine(Path.GetTempPath(), "TAOM_StagedSiege_" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(dir);

            var sut = new StagedSiegeSceneCatalog(paths, _logger);

            Assert.AreEqual(0, sut.SceneCount);
            AssertAllDefaults(sut.Tunables);
            AssertWarnedAbout("not found");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [DataTestMethod]
    [DataRow("{ not json")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("{\"captureRadius\": 8.")]
    public void Load_NotJson_NoScenesDefaultsWarns(string text)
    {
        var sut = Load(text);

        Assert.AreEqual(0, sut.SceneCount);
        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout("could not be read");
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("[3]")]
    [DataRow("true")]
    [DataRow("5")]
    [DataRow("null")]
    public void Load_Array_NoScenesDefaultsWarns(string text)
    {
        var sut = Load(text);

        Assert.AreEqual(0, sut.SceneCount);
        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout("not a JSON object");
    }

    [TestMethod]
    public void Load_NullText_IsTheMissingFileCase()
    {
        var sut = Load((string?)null);

        Assert.AreEqual(0, sut.SceneCount);
        AssertWarnedAbout("not found");
    }

    [TestMethod]
    public void Load_Pilot_NoWarnings()
    {
        var sut = Load(PilotText);

        Assert.AreEqual(1, sut.SceneCount);
        Assert.IsTrue(sut.TryGetScene(Scene, out _));
        AssertAllDefaults(sut.Tunables);
        AssertNoWarnings();
        _logger.DidNotReceive().LogError(Arg.Any<string>());
    }

    [TestMethod]
    public void TryGetScene_IsCaseInsensitive()
    {
        var sut = Load(PilotText);

        Assert.IsTrue(sut.TryGetScene("STURGIA_Castle_Siege_001", out var plan));
        Assert.AreEqual(Scene, plan.Scene);
        Assert.IsFalse(sut.TryGetScene("nowhere", out _));
        Assert.IsFalse(sut.TryGetScene("", out _));
    }

    // --- tunables ----------------------------------------------------------------------------------------------------

    [DataTestMethod]
    [DataRow("captureRadius", 1.9, 30.1)]
    [DataRow("heightBand", 0.4, 10.1)]
    [DataRow("minAttackers", 0.0, 51.0)]
    [DataRow("dominanceRatio", 0.9, 10.1)]
    [DataRow("captureSeconds", 1.9, 300.1)]
    [DataRow("decaySeconds", 1.9, 600.1)]
    [DataRow("reconcileSeconds", 0.09, 2.1)]
    [DataRow("detachmentShare", -0.01, 0.51)]
    [DataRow("detachmentCap", -1.0, 201.0)]
    [DataRow("trickleSeconds", 0.9, 120.1)]
    [DataRow("refillBurstSeconds", -0.1, 120.1)]
    [DataRow("refillBurstIntervalSeconds", 0.2, 10.1)]
    [DataRow("reinforcementBatchPercentage", 0.009, 0.51)]
    [DataRow("desiredReinforcementPercentage", 0.009, 1.01)]
    public void Tunable_OutOfRange_Reverts(string key, double below, double above)
    {
        foreach (var bad in new[] { below, above })
        {
            _logger = Substitute.For<IModLogger>();
            var json = Pilot();
            json[key] = (key == "minAttackers" || key == "detachmentCap") ? new JValue((long)bad) : new JValue(bad);

            var sut = Load(json);

            AssertAllDefaults(sut.Tunables);
            AssertWarnedAbout(key);
        }
    }

    [DataTestMethod]
    [DataRow("captureRadius", 2.0, 30.0)]
    [DataRow("heightBand", 0.5, 10.0)]
    [DataRow("minAttackers", 1.0, 50.0)]
    [DataRow("dominanceRatio", 1.0, 10.0)]
    [DataRow("captureSeconds", 2.0, 300.0)]
    [DataRow("decaySeconds", 2.0, 600.0)]
    [DataRow("reconcileSeconds", 0.1, 2.0)]
    [DataRow("detachmentShare", 0.0, 0.5)]
    [DataRow("detachmentCap", 0.0, 200.0)]
    [DataRow("trickleSeconds", 1.0, 120.0)]
    [DataRow("refillBurstSeconds", 0.0, 120.0)]
    [DataRow("refillBurstIntervalSeconds", 0.25, 10.0)]
    [DataRow("reinforcementBatchPercentage", 0.01, 0.5)]
    [DataRow("desiredReinforcementPercentage", 0.01, 1.0)]
    public void Tunable_AtTheBounds_IsAccepted(string key, double low, double high)
    {
        foreach (var good in new[] { low, high })
        {
            _logger = Substitute.For<IModLogger>();
            var json = Pilot();
            json[key] = (key == "minAttackers" || key == "detachmentCap") ? new JValue((long)good) : new JValue(good);

            var sut = Load(json);

            Assert.AreEqual(good, Read(sut.Tunables, key), 1e-6, key);
            AssertNoWarnings();
        }
    }

    [TestMethod]
    public void Tunable_MissingKey_IsTheDefault_Silently()
    {
        var json = Pilot();
        json.Remove("captureSeconds");

        var sut = Load(json);

        Assert.AreEqual(20f, sut.Tunables.CaptureSeconds);
        AssertNoWarnings();
    }

    [DataTestMethod]
    [DataRow("captureSeconds")]
    [DataRow("minAttackers")]
    public void Tunable_StringNumber_Reverts(string key)
    {
        var json = Pilot();
        json[key] = "5";

        var sut = Load(json);

        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout(key);
    }

    [DataTestMethod]
    [DataRow("captureSeconds")]
    [DataRow("heightBand")]
    public void Tunable_NaN_Reverts(string key)
    {
        var text = PilotText;
        var start = text.IndexOf("'" + key + "': ", StringComparison.Ordinal);
        var end = text.IndexOf(',', start);
        text = text.Substring(0, start) + "'" + key + "': NaN" + text.Substring(end);

        var sut = Load(text);

        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout(key);
    }

    [DataTestMethod]
    [DataRow("captureSeconds", "Infinity")]
    [DataRow("decaySeconds", "-Infinity")]
    public void Tunable_Infinity_Reverts(string key, string literal)
    {
        var text = PilotText;
        var start = text.IndexOf("'" + key + "': ", StringComparison.Ordinal);
        var end = text.IndexOf(',', start);
        text = text.Substring(0, start) + "'" + key + "': " + literal + text.Substring(end);

        var sut = Load(text);

        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout(key);
    }

    [DataTestMethod]
    [DataRow("minAttackers")]
    [DataRow("detachmentCap")]
    public void Tunable_IntegerGivenFraction_Reverts(string key)
    {
        var json = Pilot();
        json[key] = 4.5;

        var sut = Load(json);

        AssertAllDefaults(sut.Tunables);
        AssertWarnedAbout(key);
    }

    [TestMethod]
    public void Tunable_ValidOverride_IsUsed()
    {
        var json = Pilot();
        json["captureSeconds"] = 45;
        json["minAttackers"] = 5;

        var sut = Load(json);

        Assert.AreEqual(45f, sut.Tunables.CaptureSeconds);
        Assert.AreEqual(5, sut.Tunables.MinAttackers);
        AssertNoWarnings();
    }

    [TestMethod]
    public void Load_AnyReversion_EmitsSummary()
    {
        var json = Pilot();
        json["captureSeconds"] = 9999;

        Assert.AreEqual(1, Load(json).SceneCount);

        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("reverted")));
        _logger.Received(2).LogWarning(Arg.Any<string>());
    }

    // --- reserve shares ----------------------------------------------------------------------------------------------

    private static JObject WithShares(string name, JToken value)
    {
        var json = Pilot();
        json["reserveShares"]![name] = value;
        return json;
    }

    [TestMethod]
    public void Shares_Valid_AreUsed()
    {
        var sut = Load(WithShares("attacker", new JArray(0.1, 0.1, 0.1, 0.1)));

        CollectionAssert.AreEqual(new[] { 0.1f, 0.1f, 0.1f, 0.1f }, sut.Tunables.ReserveShares.Attacker);
        AssertNoWarnings();
    }

    [DataTestMethod]
    [DataRow(3)]
    [DataRow(5)]
    [DataRow(0)]
    public void Shares_WrongLength_Reverts(int length)
    {
        var sut = Load(WithShares("defenderPlayerAttacks", new JArray(Enumerable.Repeat(0.1, length))));

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.DefenderPlayerAttacks, sut.Tunables.ReserveShares.DefenderPlayerAttacks);
        AssertWarnedAbout("defenderPlayerAttacks");
    }

    [TestMethod]
    public void Shares_Negative_Reverts()
    {
        var sut = Load(WithShares("attacker", new JArray(-0.1, 0.3, 0.3, 0.3)));

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.Attacker, sut.Tunables.ReserveShares.Attacker);
        AssertWarnedAbout("attacker");
    }

    [TestMethod]
    public void Shares_AboveOne_Reverts()
    {
        var sut = Load(WithShares("attacker", new JArray(1.5, 0, 0, 0)));

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.Attacker, sut.Tunables.ReserveShares.Attacker);
        AssertWarnedAbout("attacker");
    }

    [TestMethod]
    public void Shares_SumAboveOne_Reverts()
    {
        var sut = Load(WithShares("defenderPlayerDefends", new JArray(0.5, 0.5, 0.5, 0.5)));

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.DefenderPlayerDefends, sut.Tunables.ReserveShares.DefenderPlayerDefends);
        AssertWarnedAbout("defenderPlayerDefends");
    }

    [TestMethod]
    public void Shares_NaN_Reverts()
    {
        var text = PilotText.Replace("'attacker': [0.20, 0.30, 0.25, 0.25]", "'attacker': [0.20, NaN, 0.25, 0.25]");
        Assert.AreNotEqual(PilotText, text);

        var sut = Load(text);

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.Attacker, sut.Tunables.ReserveShares.Attacker);
        AssertWarnedAbout("attacker");
    }

    [TestMethod]
    public void Shares_StringElement_Reverts()
    {
        var sut = Load(WithShares("attacker", new JArray(0.2, "0.3", 0.25, 0.25)));

        CollectionAssert.AreEqual(StagedSiegeTunables.Default.ReserveShares.Attacker, sut.Tunables.ReserveShares.Attacker);
        AssertWarnedAbout("attacker");
    }

    // --- scenes ------------------------------------------------------------------------------------------------------

    private void AssertSceneSkipped(JObject json)
    {
        var sut = Load(json);

        Assert.IsFalse(sut.TryGetScene(Scene, out _), "the bad scene should have been skipped");
        AssertWarnedAbout(Scene);
    }

    [TestMethod]
    public void Scene_NotObject_Skipped()
    {
        var json = Pilot();
        json["scenes"]![Scene] = 5;

        AssertSceneSkipped(json);
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void Scene_EmptyKey_Skipped(string key)
    {
        var json = Pilot();
        json["scenes"]![key] = SceneOf(Pilot());

        var sut = Load(json);

        Assert.AreEqual(1, sut.SceneCount);
        Assert.IsFalse(sut.TryGetScene(key, out _));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow("A")]
    [DataRow("B")]
    [DataRow("C")]
    [DataRow("D")]
    public void Scene_MissingPoint_Skipped(string letter)
    {
        var json = Pilot();
        ((JObject)SceneOf(json)["points"]!).Remove(letter);

        AssertSceneSkipped(json);
    }

    [TestMethod]
    public void Scene_CoordinateNaN_Skipped()
    {
        var text = PilotText.Replace("'x': 559.6358", "'x': NaN");
        Assert.AreNotEqual(PilotText, text);

        var sut = Load(text);

        Assert.IsFalse(sut.TryGetScene(Scene, out _));
        AssertWarnedAbout(Scene);
    }

    [TestMethod]
    public void Scene_CoordinateString_Skipped()
    {
        var json = Pilot();
        SceneOf(json)["points"]!["B"]!["y"] = "612.6";

        AssertSceneSkipped(json);
    }

    [DataTestMethod]
    [DataRow(100000.5)]
    [DataRow(-100001.0)]
    public void Scene_CoordinateHuge_Skipped(double value)
    {
        var json = Pilot();
        SceneOf(json)["detachment"]!["z"] = value;

        AssertSceneSkipped(json);
    }

    [TestMethod]
    public void Scene_CoordinateAtTheLimit_Accepted()
    {
        var json = Pilot();
        SceneOf(json)["detachment"]!["z"] = 100000.0;

        Assert.IsTrue(Load(json).TryGetScene(Scene, out _));
    }

    [DataTestMethod]
    [DataRow(1.9)]
    [DataRow(30.1)]
    public void Scene_RadiusOutOfRange_Skipped(double radius)
    {
        var json = Pilot();
        SceneOf(json)["points"]!["C"]!["radius"] = radius;

        AssertSceneSkipped(json);
    }

    [TestMethod]
    public void Scene_RadiusMissing_UsesCaptureRadius()
    {
        var json = Pilot();
        json["captureRadius"] = 12;
        ((JObject)SceneOf(json)["points"]!["A"]!).Remove("radius");

        var sut = Load(json);

        Assert.IsTrue(sut.TryGetScene(Scene, out var plan));
        Assert.AreEqual(12f, plan.Zone(StageId.A).Radius);
        Assert.AreEqual(8f, plan.Zone(StageId.B).Radius);
    }

    [TestMethod]
    public void Scene_DetachmentMissing_Skipped()
    {
        var json = Pilot();
        SceneOf(json).Remove("detachment");

        AssertSceneSkipped(json);
    }

    [DataTestMethod]
    [DataRow("B")]
    [DataRow("C")]
    [DataRow("D")]
    public void Scene_DefenderFrameMissing_Skipped(string letter)
    {
        var json = Pilot();
        ((JObject)SceneOf(json)["defenders"]!).Remove(letter);

        AssertSceneSkipped(json);
    }

    [TestMethod]
    public void Scene_DefendersMissing_Skipped()
    {
        var json = Pilot();
        SceneOf(json).Remove("defenders");

        AssertSceneSkipped(json);
    }

    [DataTestMethod]
    [DataRow(0.0, 0.0)]
    [DataRow(2.0, 2.0)]
    [DataRow(0.1, 0.0)]
    public void Scene_FacingZero_Skipped(double fx, double fy)
    {
        var json = Pilot();
        SceneOf(json)["defenders"]!["B"]!["fx"] = fx;
        SceneOf(json)["defenders"]!["B"]!["fy"] = fy;

        AssertSceneSkipped(json);
    }

    [TestMethod]
    public void Scene_Facing_NormalizedAtLoad()
    {
        var json = Pilot();
        SceneOf(json)["defenders"]!["B"]!["fx"] = 1.0;
        SceneOf(json)["defenders"]!["B"]!["fy"] = 1.0;

        Assert.IsTrue(Load(json).TryGetScene(Scene, out var plan));
        var f = plan.DefenderFrame(StageId.B)!.Value.Facing;
        Assert.AreEqual(0.70710678f, f.X, 1e-5f);
        Assert.AreEqual(0.70710678f, f.Y, 1e-5f);
    }

    [TestMethod]
    public void Load_BadSceneKeepsGoodScene()
    {
        var json = Pilot();
        json["scenes"]!["broken"] = new JObject { ["points"] = new JObject() };
        json["scenes"]!["second"] = SceneOf(Pilot());

        var sut = Load(json);

        Assert.AreEqual(2, sut.SceneCount);
        Assert.IsTrue(sut.TryGetScene(Scene, out _));
        Assert.IsTrue(sut.TryGetScene("second", out _));
        Assert.IsFalse(sut.TryGetScene("broken", out _));
        AssertWarnedAbout("broken");
    }

    [TestMethod]
    public void Scene_PilotValues_AreCarriedExactly()
    {
        Assert.IsTrue(Load(PilotText).TryGetScene(Scene, out var plan));

        Assert.AreEqual(559.6358f, plan.Zone(StageId.A).Center.X);
        Assert.AreEqual(89.89123f, plan.Zone(StageId.D).Center.Z);
        Assert.AreEqual(77.92095f, plan.Detachment.Position.Z);
        Assert.AreEqual(90.25453f, plan.DefenderFrame(StageId.D)!.Value.Position.Z);
    }

    // --- the real engine path ----------------------------------------------------------------------------------------

    [TestMethod]
    public void Load_FromTheFile_ReadsOnce_AndNeverThrows()
    {
        var dir = Path.Combine(Path.GetTempPath(), "TAOM_StagedSiege_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(dir, "siege"));
        try
        {
            File.WriteAllText(Path.Combine(dir, "siege", "staged_siege.json"), PilotText);
            var paths = Substitute.For<IPathService>();
            paths.ModuleDataPath.Returns(dir);
            var sut = new StagedSiegeSceneCatalog(paths, _logger);

            Assert.AreEqual(1, sut.SceneCount);
            File.WriteAllText(Path.Combine(dir, "siege", "staged_siege.json"), "garbage");
            Assert.AreEqual(1, sut.SceneCount, "the file is read once per process");
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
