using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.LoreGreetings;

namespace TAOM.Tests.Features.LoreGreetings;

/// <summary>
/// One test per validation rule of <c>lore_greetings/lore_greetings.json</c> (config-providers.md). The provider is fail-soft: a bad
/// person or greeting is dropped with a warning, and a missing or broken file gives an empty table, so every greeting stays as it was.
/// </summary>
[TestClass]
public class LoreGreetingsConfigProviderTests
{
    private string _tempDir = null!;
    private string _featureDir = null!;
    private IModLogger _logger = null!;
    private LoreGreetingsConfigProvider _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_LoreGreetings_" + Path.GetRandomFileName());
        _featureDir = Path.Combine(_tempDir, LoreGreetingsConfigProvider.FolderName);
        Directory.CreateDirectory(_featureDir);

        var pathService = Substitute.For<IPathService>();
        pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();
        _sut = new LoreGreetingsConfigProvider(pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void Write(string persons, string greetings) =>
        File.WriteAllText(Path.Combine(_featureDir, LoreGreetingsConfigProvider.FileName),
            "{ \"persons\": [" + persons + "], \"greetings\": [" + greetings + "] }");

    private const string Gimli = @"{ ""id"": ""gimli"", ""female"": false, ""characters"": [""taom_hero_gimli"", ""named_companion_gimli""] }";
    private const string Dain = @"{ ""id"": ""dain"", ""female"": false, ""characters"": [""lord_E1_1""] }";
    private const string DainToGimli = @"{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": ""Gimli!"", ""hostile"": ""Gimli, against me?"", ""first"": ""Gimli, at last!"" }";

    [TestMethod]
    public void GetTable_ValidFile_LoadsPersonsAndLines_WithNoWarning()
    {
        Write(Gimli + "," + Dain, DainToGimli);

        var table = _sut.GetTable();

        Assert.AreEqual("gimli", table.PersonOf(new[] { "named_companion_gimli" })?.Id);
        Assert.AreEqual("dain", table.PersonOf(new[] { "lord_E1_1" })?.Id);
        var lines = table.LinesFor("dain", "gimli");
        Assert.AreEqual("Gimli!", lines?.Text);
        Assert.AreEqual("Gimli, against me?", lines?.Hostile);
        Assert.AreEqual("Gimli, at last!", lines?.First);
        Assert.AreEqual(1, table.PairCount);
        _logger.DidNotReceiveWithAnyArgs().LogWarning(default!);
    }

    [TestMethod]
    public void GetTable_IsReadOnce()
    {
        Write(Gimli + "," + Dain, DainToGimli);
        var first = _sut.GetTable();
        File.Delete(Path.Combine(_featureDir, LoreGreetingsConfigProvider.FileName));

        Assert.AreSame(first, _sut.GetTable());
    }

    [TestMethod]
    public void GetTable_MissingFile_ReturnsAnEmptyTable_AndWarns()
    {
        var table = _sut.GetTable();

        Assert.AreEqual(0, table.PairCount);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("not found")));
    }

    [TestMethod]
    public void GetTable_BrokenJson_ReturnsAnEmptyTable_AndWarns()
    {
        File.WriteAllText(Path.Combine(_featureDir, LoreGreetingsConfigProvider.FileName), "{ not json");

        var table = _sut.GetTable();

        Assert.AreEqual(0, table.PairCount);
        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("parse")));
    }

    [TestMethod]
    public void GetTable_PersonIdNotLowerCaseWord_IsDropped()
    {
        Write(Gimli + @",{ ""id"": ""Dáin"", ""female"": false, ""characters"": [""lord_E1_1""] }", "");

        Assert.IsNull(_sut.GetTable().PersonOf(new[] { "lord_E1_1" }));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("Dáin")));
    }

    [TestMethod]
    public void GetTable_RepeatedPersonId_TheSecondIsDropped()
    {
        Write(Gimli + @",{ ""id"": ""gimli"", ""female"": false, ""characters"": [""lord_X""] }", "");

        var table = _sut.GetTable();

        Assert.AreEqual("gimli", table.PersonOf(new[] { "taom_hero_gimli" })?.Id);
        Assert.IsNull(table.PersonOf(new[] { "lord_X" }));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gimli")));
    }

    [TestMethod]
    public void GetTable_PersonWithNoGender_IsDropped()
    {
        Write(@"{ ""id"": ""dain"", ""characters"": [""lord_E1_1""] }", "");

        Assert.IsNull(_sut.GetTable().PersonOf(new[] { "lord_E1_1" }));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [TestMethod]
    public void GetTable_PersonWithNoCharacters_IsDropped()
    {
        Write(@"{ ""id"": ""dain"", ""female"": false, ""characters"": [ "" "" ] }," + Gimli, @"{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": ""Hail."" }");

        var table = _sut.GetTable();

        Assert.IsNull(table.LinesFor("dain", "gimli"));
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [TestMethod]
    public void GetTable_CharacterAlreadyInAnotherPerson_TheSecondPersonIsDropped()
    {
        Write(Gimli + @",{ ""id"": ""gimli_twin"", ""female"": false, ""characters"": [""named_companion_gimli""] }", "");

        var table = _sut.GetTable();

        Assert.AreEqual("gimli", table.PersonOf(new[] { "named_companion_gimli" })?.Id);
        Assert.AreEqual(1, table.Persons.Count);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gimli_twin")));
    }

    [TestMethod]
    public void GetTable_GreetingWithUnknownSpeaker_IsDropped()
    {
        Write(Gimli, DainToGimli);

        Assert.AreEqual(0, _sut.GetTable().PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [TestMethod]
    public void GetTable_GreetingWithUnknownListener_IsDropped()
    {
        Write(Dain, DainToGimli);

        Assert.AreEqual(0, _sut.GetTable().PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gimli")));
    }

    [TestMethod]
    public void GetTable_GreetingToOneself_IsDropped()
    {
        Write(Gimli, @"{ ""speaker"": ""gimli"", ""listener"": ""gimli"", ""text"": ""Me."" }");

        Assert.AreEqual(0, _sut.GetTable().PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("gimli")));
    }

    [TestMethod]
    public void GetTable_RepeatedPair_TheFirstIsKept()
    {
        Write(Gimli + "," + Dain, DainToGimli + @",{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": ""Again."" }");

        var table = _sut.GetTable();

        Assert.AreEqual("Gimli!", table.LinesFor("dain", "gimli")?.Text);
        Assert.AreEqual(1, table.PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain") && m.Contains("gimli")));
    }

    [TestMethod]
    public void GetTable_GreetingWithNoLine_IsDropped()
    {
        Write(Gimli + "," + Dain, @"{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": "" "" }");

        Assert.AreEqual(0, _sut.GetTable().PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [DataTestMethod]
    [DataRow("Gimli {PLAYER.NAME}!")]
    [DataRow("Gimli! [ib:closed]")]
    [DataRow("Gimli ]")]
    public void GetTable_LineWithAVariableOrAnimationMark_DropsTheGreeting(string text)
    {
        Write(Gimli + "," + Dain, @"{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": ""Hail."", ""first"": """ + text + @""" }");

        Assert.AreEqual(0, _sut.GetTable().PairCount);
        _logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("dain")));
    }

    [TestMethod]
    public void GetTable_IdsAreTrimmed()
    {
        Write(@"{ ""id"": "" dain "", ""female"": false, ""characters"": ["" lord_E1_1 ""] }," + Gimli,
            @"{ ""speaker"": "" dain"", ""listener"": ""gimli "", ""text"": "" Hail. "" }");

        var table = _sut.GetTable();

        Assert.AreEqual("dain", table.PersonOf(new[] { "lord_E1_1" })?.Id);
        Assert.AreEqual("Hail.", table.LinesFor("dain", "gimli")?.Text);
    }

    [TestMethod]
    public void GetTable_EmptyVariant_IsLeftOut_TheOthersStay()
    {
        Write(Gimli + "," + Dain, @"{ ""speaker"": ""dain"", ""listener"": ""gimli"", ""text"": ""Hail."", ""hostile"": "" "" }");

        var lines = _sut.GetTable().LinesFor("dain", "gimli");

        Assert.AreEqual("Hail.", lines?.Text);
        Assert.IsNull(lines?.Hostile);
        Assert.IsNull(lines?.First);
    }

    [TestMethod]
    public void GetTable_NullRows_AreSkipped_WithAWarning()
    {
        Write(Gimli + ",null," + Dain, DainToGimli + ",null");

        var table = _sut.GetTable();

        Assert.AreEqual(1, table.PairCount);
        Assert.AreEqual(2, table.Persons.Count);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Persons_ListsEveryLoadedPersonInFileOrder()
    {
        Write(Gimli + "," + Dain, "");

        CollectionAssert.AreEqual(new[] { "gimli", "dain" }, _sut.GetTable().Persons.Select(p => p.Id).ToArray());
    }
}
