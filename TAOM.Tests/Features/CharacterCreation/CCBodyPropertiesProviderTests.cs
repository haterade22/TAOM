using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.CharacterCreation;

namespace TAOM.Tests.Features.CharacterCreation;

[TestClass]
public class CCBodyPropertiesProviderTests
{
    private const string SampleKey =
        "0005280140001242947E068A709500460C7250703EB70F135C85021887733A070089B6030822BA9000000000000000000000000000000000000000003F1C7002";

    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private CCBodyPropertiesProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_CCBP_" + Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(_tempDir, "charactercreation"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new CCBodyPropertiesProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void GetBodyPropertiesXml_MissingFile_ReturnsNullAndLogsWarning()
    {
        var result = _sut.GetBodyPropertiesXml("vlandia");

        Assert.IsNull(result);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_ConfiguredCulture_ReturnsInnerBodyPropertiesXml()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia"">
    <BodyProperties version=""4"" key=""{SampleKey}"" />
  </Culture>
</CCBodyProperties>");

        var result = _sut.GetBodyPropertiesXml("vlandia");

        Assert.IsNotNull(result);
        StringAssert.StartsWith(result, "<BodyProperties");
        StringAssert.Contains(result, SampleKey);
    }

    [TestMethod]
    public void GetBodyPropertiesXml_NotConfigured_ReturnsNull()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        var result = _sut.GetBodyPropertiesXml("sturgia");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetBodyPropertiesXml_NullOrEmptyCultureId_ReturnsNull()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        Assert.IsNull(_sut.GetBodyPropertiesXml(null));
        Assert.IsNull(_sut.GetBodyPropertiesXml(""));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_CaseInsensitiveCultureId_Matches()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""Vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        Assert.IsNotNull(_sut.GetBodyPropertiesXml("vlandia"));
        Assert.IsNotNull(_sut.GetBodyPropertiesXml("VLANDIA"));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_CultureMissingId_LoggedAndSkipped()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        Assert.IsNotNull(_sut.GetBodyPropertiesXml("vlandia"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("missing id")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_CultureMissingBodyPropertiesElement_LoggedAndSkipped()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""empty_culture"" />
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        Assert.IsNull(_sut.GetBodyPropertiesXml("empty_culture"));
        Assert.IsNotNull(_sut.GetBodyPropertiesXml("vlandia"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty_culture") && s.Contains("BodyProperties")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_BodyPropertiesMissingKey_LoggedAndSkipped()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""nokey""><BodyProperties version=""4"" /></Culture>
</CCBodyProperties>");

        Assert.IsNull(_sut.GetBodyPropertiesXml("nokey"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("nokey") && s.Contains("key")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_BodyPropertiesEmptyKey_LoggedAndSkipped()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""empty_key""><BodyProperties version=""4"" key="""" /></Culture>
</CCBodyProperties>");

        Assert.IsNull(_sut.GetBodyPropertiesXml("empty_key"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("empty_key") && s.Contains("key")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_BodyPropertiesWrongHexLength_LoggedAndSkipped()
    {
        WriteConfig(@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""shortkey""><BodyProperties version=""4"" key=""ABC123"" /></Culture>
</CCBodyProperties>");

        Assert.IsNull(_sut.GetBodyPropertiesXml("shortkey"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("shortkey") && s.Contains("128")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_DuplicateCultureId_LastWinsAndLogs()
    {
        var firstKey = SampleKey;
        var secondKey = SampleKey.Replace('0', 'F'); // distinct but valid length

        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{firstKey}"" /></Culture>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{secondKey}"" /></Culture>
</CCBodyProperties>");

        var result = _sut.GetBodyPropertiesXml("vlandia");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, secondKey);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("vlandia") && s.Contains("duplicate")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_MalformedXml_ReturnsNullAndLogsError()
    {
        WriteConfig("<<< not xml >>>");

        var result = _sut.GetBodyPropertiesXml("vlandia");

        Assert.IsNull(result);
        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Failed to parse")));
    }

    [TestMethod]
    public void GetBodyPropertiesXml_CachesResult_SecondCallDoesNotRereadFile()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia""><BodyProperties version=""4"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        _ = _sut.GetBodyPropertiesXml("vlandia");

        File.Delete(Path.Combine(_tempDir, "charactercreation", "cc_body_properties.xml"));

        var second = _sut.GetBodyPropertiesXml("vlandia");
        Assert.IsNotNull(second, "Second call must return cached result even after file deletion");
    }

    [TestMethod]
    public void GetBodyPropertiesXml_PreservesAgeWeightBuildAttributes()
    {
        WriteConfig($@"<?xml version=""1.0"" encoding=""utf-8""?>
<CCBodyProperties>
  <Culture id=""vlandia""><BodyProperties version=""4"" age=""25.5"" weight=""0.6"" build=""0.7"" key=""{SampleKey}"" /></Culture>
</CCBodyProperties>");

        var result = _sut.GetBodyPropertiesXml("vlandia");

        Assert.IsNotNull(result);
        StringAssert.Contains(result, "age=\"25.5\"");
        StringAssert.Contains(result, "weight=\"0.6\"");
        StringAssert.Contains(result, "build=\"0.7\"");
    }

    private void WriteConfig(string xml)
    {
        File.WriteAllText(
            Path.Combine(_tempDir, "charactercreation", "cc_body_properties.xml"), xml);
    }
}
