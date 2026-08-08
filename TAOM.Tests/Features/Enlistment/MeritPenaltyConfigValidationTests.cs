using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment.Content;
using TAOM.Features.Enlistment.Content.Domain;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Both merit penalties are directional — the scorer SUBTRACTS them — so a negative value in
/// enlistment_config.json silently inverts the field into a reward. A negative leftFieldPenalty
/// would hand the bonus straight back to the walkout that penalty exists to stop, which is the
/// exact defect it was added to fix.
/// </summary>
[TestClass]
public class MeritPenaltyConfigValidationTests
{
    private string _dir = null!;
    private IPathService _paths = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _dir = Path.Combine(Path.GetTempPath(), "taom_merit_cfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_dir, "enlistment"));
        _paths = Substitute.For<IPathService>();
        _paths.ModuleDataPath.Returns(_dir);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private MeritScoringConfig LoadMeritScoring(string json)
    {
        File.WriteAllText(Path.Combine(_dir, "enlistment", "enlistment_config.json"), json);
        return new EnlistmentContentConfigProvider(_paths, _logger).GetConfig().MeritScoring;
    }

    [TestMethod]
    public void GetConfig_NegativeLeftFieldPenalty_RevertsWithWarning()
    {
        var scoring = LoadMeritScoring("{\"meritScoring\":{\"leftFieldPenalty\":-30}}");

        Assert.AreEqual(30, scoring.LeftFieldPenalty, "A negative penalty would REWARD walking out.");
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("leftFieldPenalty")));
    }

    [TestMethod]
    public void GetConfig_NegativeFellEarlyPenalty_RevertsWithWarning()
    {
        var scoring = LoadMeritScoring("{\"meritScoring\":{\"fellEarlyPenalty\":-10}}");

        Assert.AreEqual(10, scoring.FellEarlyPenalty);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("fellEarlyPenalty")));
    }

    [TestMethod]
    public void GetConfig_RetunedLeftFieldPenalty_IsPreserved()
    {
        var scoring = LoadMeritScoring("{\"meritScoring\":{\"leftFieldPenalty\":45}}");

        Assert.AreEqual(45, scoring.LeftFieldPenalty, "A legitimate retune must survive validation.");
    }
}
