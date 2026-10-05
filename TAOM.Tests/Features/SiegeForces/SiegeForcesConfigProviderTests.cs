using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.SiegeForces;

namespace TAOM.Tests.Features.SiegeForces;

/// <summary>
/// One test per validation rule (csharp-architecture.md "Config Providers MUST Validate"). The file holds one
/// switch, <c>startOversizedUnticked</c>, and every way it can be wrong reverts to true with a warning: a missing
/// file, text that is not JSON, JSON that is not an object, a missing key (a typo'd key is a missing key) and a
/// value that is not a JSON boolean. The provider reads once per process, so an edit needs a restart.
/// </summary>
[TestClass]
public class SiegeForcesConfigProviderTests
{
    private string _tempDir = null!;
    private string _siegeDir = null!;
    private IModLogger _logger = null!;
    private SiegeForcesConfigProvider _sut = null!;

    private string ConfigPath => Path.Combine(_siegeDir, "siege_forces.json");

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_SiegeForces_" + Path.GetRandomFileName());
        _siegeDir = Path.Combine(_tempDir, "siege");
        Directory.CreateDirectory(_siegeDir);
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new SiegeForcesConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string json) => File.WriteAllText(ConfigPath, json);

    private void AssertWarnedAbout(string fragment) =>
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains(fragment)));

    [TestMethod]
    public void StartOversizedUnticked_FileMissing_IsTrue_AndWarns()
    {
        Assert.IsTrue(_sut.StartOversizedUnticked);
        AssertWarnedAbout("not found");
    }

    [DataTestMethod]
    [DataRow("{ not json")]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("{\"startOversizedUnticked\": tru")]
    public void StartOversizedUnticked_NotJson_IsTrue_AndWarns(string text)
    {
        Write(text);

        Assert.IsTrue(_sut.StartOversizedUnticked);
        AssertWarnedAbout("could not be read");
    }

    [DataTestMethod]
    [DataRow("[]")]
    [DataRow("[true]")]
    [DataRow("true")]
    [DataRow("5")]
    [DataRow("null")]
    [DataRow("\"startOversizedUnticked\"")]
    public void StartOversizedUnticked_JsonThatIsNotAnObject_IsTrue_AndWarns(string text)
    {
        Write(text);

        Assert.IsTrue(_sut.StartOversizedUnticked);
        AssertWarnedAbout("not a JSON object");
    }

    [DataTestMethod]
    [DataRow("{}")]
    [DataRow("{ \"startOversizedUntick\": false }")]
    [DataRow("{ \"StartOversizedUnticked\": false }")]
    [DataRow("{ \"somethingElse\": false }")]
    public void StartOversizedUnticked_KeyMissing_IsTrue_AndWarns(string text)
    {
        // A typo'd key is a missing key: the default holds, and the warning names the key a player should have typed.
        Write(text);

        Assert.IsTrue(_sut.StartOversizedUnticked);
        AssertWarnedAbout("startOversizedUnticked");
    }

    [DataTestMethod]
    [DataRow("{ \"startOversizedUnticked\": \"false\" }")]
    [DataRow("{ \"startOversizedUnticked\": \"true\" }")]
    [DataRow("{ \"startOversizedUnticked\": 0 }")]
    [DataRow("{ \"startOversizedUnticked\": 1 }")]
    [DataRow("{ \"startOversizedUnticked\": null }")]
    [DataRow("{ \"startOversizedUnticked\": [] }")]
    [DataRow("{ \"startOversizedUnticked\": {} }")]
    public void StartOversizedUnticked_ValueNotABoolean_IsTrue_AndWarns(string text)
    {
        // Newtonsoft would coerce "false" and 0 to false: a hand edit that quotes the value must not flip the switch.
        Write(text);

        Assert.IsTrue(_sut.StartOversizedUnticked);
        AssertWarnedAbout("not a boolean");
    }

    [TestMethod]
    public void StartOversizedUnticked_False_IsFalse_WithNoWarning()
    {
        Write("{ \"startOversizedUnticked\": false }");

        Assert.IsFalse(_sut.StartOversizedUnticked);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void StartOversizedUnticked_True_IsTrue_WithNoWarning()
    {
        Write("{ \"startOversizedUnticked\": true }");

        Assert.IsTrue(_sut.StartOversizedUnticked);
        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void StartOversizedUnticked_AnExtraKey_DoesNotChangeTheAnswer()
    {
        Write("{ \"startOversizedUnticked\": false, \"someFutureKnob\": 3 }");

        Assert.IsFalse(_sut.StartOversizedUnticked);
    }

    [TestMethod]
    public void StartOversizedUnticked_TheFileHasABom_IsStillRead()
    {
        File.WriteAllText(ConfigPath, "{ \"startOversizedUnticked\": false }", new System.Text.UTF8Encoding(true));

        Assert.IsFalse(_sut.StartOversizedUnticked);
    }

    [TestMethod]
    public void StartOversizedUnticked_ReadsTheFileOnce_SoAnEditNeedsARestart()
    {
        Write("{ \"startOversizedUnticked\": false }");
        Assert.IsFalse(_sut.StartOversizedUnticked);

        Write("{ \"startOversizedUnticked\": true }");

        Assert.IsFalse(_sut.StartOversizedUnticked, "a Reuse.Singleton provider caches for the whole process");
    }

    [TestMethod]
    public void StartOversizedUnticked_AFallbackIsAlsoCached_AndWarnsOnce()
    {
        Assert.IsTrue(_sut.StartOversizedUnticked);
        Assert.IsTrue(_sut.StartOversizedUnticked);
        Assert.IsTrue(_sut.StartOversizedUnticked);

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void StartOversizedUnticked_ThePathServiceThrows_IsTrue_AndWarns_NeverThrows()
    {
        // A total function: a faulted Lazy rethrows its exception forever, which would disable the picker's defaults.
        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_ => throw new System.InvalidOperationException("no module root"));
        var sut = new SiegeForcesConfigProvider(pathService, _logger);

        Assert.IsTrue(sut.StartOversizedUnticked);
        Assert.IsTrue(sut.StartOversizedUnticked);
        _logger.Received(1).LogWarning(Arg.Is<string>(s => s.Contains("could not be read")));
    }

    [TestMethod]
    public void StartOversizedUnticked_ALoadedValue_IsLoggedOnce()
    {
        Write("{ \"startOversizedUnticked\": false }");

        _ = _sut.StartOversizedUnticked;
        _ = _sut.StartOversizedUnticked;

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.Contains("startOversizedUnticked") && s.Contains("False")));
    }
}
