using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.EditorCacheRebuild.Validation;

namespace TAOM.Tests.Features.EditorCacheRebuild.Validation;

[TestClass]
public class ValidationReportWriterTests
{
    private string _tempDir = null!;
    private IModLogger _logger = null!;
    private ValidationReportWriter _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_ValidationReport_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new ValidationReportWriter(_logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private static ValidationReport MakeReport() => new()
    {
        Timestamp = new DateTime(2026, 5, 12, 14, 32, 0, DateTimeKind.Utc),
        Mode = "full",
        DurationSeconds = 1800.5,
        SettlementsTotal = 863,
        FortificationsTotal = 204,
        NavigationType = "Default",
        Phase1 = new PhaseReport { DurationSeconds = 600, PairsComputed = 372093 },
        Phase2 = new PhaseReport { DurationSeconds = 1200, NeighborPairsAdded = 1500, FortificationsConsidered = 204 },
        SmokeTest = new SmokeTestReportData { Outcome = "Passed", PairsTested = 10, MaxDistanceDelta = 0.00001f },
    };

    [TestMethod]
    public void Write_ValidPath_CreatesFile()
    {
        var path = Path.Combine(_tempDir, "report.json");

        _sut.Write(path, MakeReport());

        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void Write_ProducesParseableJson()
    {
        var path = Path.Combine(_tempDir, "report.json");

        _sut.Write(path, MakeReport());

        var json = File.ReadAllText(path);
        var parsed = JObject.Parse(json);
        Assert.AreEqual("full", (string?)parsed["Mode"]);
        Assert.AreEqual(863, (int)parsed["SettlementsTotal"]!);
        Assert.AreEqual(204, (int)parsed["FortificationsTotal"]!);
        Assert.AreEqual(372093, (int)parsed["Phase1"]!["PairsComputed"]!);
        Assert.AreEqual("Passed", (string?)parsed["SmokeTest"]!["Outcome"]);
    }

    [TestMethod]
    public void Write_NestedDirectory_Created()
    {
        var path = Path.Combine(_tempDir, "deep", "nested", "report.json");

        _sut.Write(path, MakeReport());

        Assert.IsTrue(File.Exists(path));
    }

    [TestMethod]
    public void Write_EmptyPath_NoOpAndLogsDebug()
    {
        _sut.Write("", MakeReport());
        _sut.Write(null!, MakeReport());

        _logger.Received().LogDebug(Arg.Is<string>(s => s.Contains("empty file path")));
    }

    [TestMethod]
    public void Write_InvalidPath_LogsErrorButDoesNotThrow()
    {
        var invalidPath = Path.Combine(_tempDir, new string('a', 300) + ".json");

        _sut.Write(invalidPath, MakeReport());

        _logger.Received().LogError(Arg.Any<string>());
    }
}
