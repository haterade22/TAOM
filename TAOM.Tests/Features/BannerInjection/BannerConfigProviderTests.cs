using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.BannerInjection;

namespace TAOM.Tests.Features.BannerInjection;

[TestClass]
public class BannerConfigProviderTests
{
    private string _tempDir;
    private IPathService _pathService;
    private IModLogger _logger;
    private BannerConfigProvider _sut;

    [TestInitialize]
    public void Setup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_tempDir);
        Directory.CreateDirectory(Path.Combine(_tempDir, "characters"));

        _pathService = Substitute.For<IPathService>();
        _pathService.ModuleDataPath.Returns(_tempDir);
        _logger = Substitute.For<IModLogger>();

        _sut = new BannerConfigProvider(_pathService, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestMethod]
    public void GetKingdomBannerKeys_ParsesXmlAttributes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "taom_spkingdoms.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<Kingdoms>
    <Kingdom id=""kingdom_erebor"" banner_key=""11.4.4.4345"" />
    <Kingdom id=""kingdom_gondor"" banner_key=""11.8.8.4345"" />
</Kingdoms>");

        var result = _sut.GetKingdomBannerKeys();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("11.4.4.4345", result["kingdom_erebor"]);
        Assert.AreEqual("11.8.8.4345", result["kingdom_gondor"]);
    }

    [TestMethod]
    public void GetKingdomBannerKeys_ParsesXsltWithXslAttribute()
    {
        File.WriteAllText(Path.Combine(_tempDir, "spkingdoms.xslt"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""Kingdom[@id='kingdom_rohan']"">
        <xsl:copy>
            <xsl:apply-templates select=""@*[local-name() != 'name' and local-name() != 'banner_key']""/>
            <xsl:attribute name=""name"">Rohan</xsl:attribute>
            <xsl:attribute name=""banner_key"">11.184.14.4922</xsl:attribute>
            <xsl:apply-templates select=""node()""/>
        </xsl:copy>
    </xsl:template>
</xsl:stylesheet>");

        var result = _sut.GetKingdomBannerKeys();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("11.184.14.4922", result["kingdom_rohan"]);
    }

    [TestMethod]
    public void GetKingdomBannerKeys_ParsesXsltWithInlineAttributes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "spkingdoms.xslt"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""Kingdom[@id='kingdom_gondor']"">
        <Kingdom id=""kingdom_gondor""
            name=""Gondor""
            banner_key=""11.8.8.4345""
            primary_banner_color=""0xff591645"" />
    </xsl:template>
</xsl:stylesheet>");

        var result = _sut.GetKingdomBannerKeys();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("11.8.8.4345", result["kingdom_gondor"]);
    }

    [TestMethod]
    public void GetClanBannerKeys_ParsesXmlAttributes()
    {
        File.WriteAllText(Path.Combine(_tempDir, "characters", "clans.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<Factions>
    <Faction id=""clan_gondor_1"" banner_key=""11.92.92.3000"" />
</Factions>");

        var result = _sut.GetClanBannerKeys();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("11.92.92.3000", result["clan_gondor_1"]);
    }

    [TestMethod]
    public void GetClanBannerKeys_ParsesXsltTemplates()
    {
        File.WriteAllText(Path.Combine(_tempDir, "spclans.xslt"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""Faction[@id='clan_empire_north_1']"">
        <xsl:copy>
            <xsl:apply-templates select=""@*[local-name() != 'name' and local-name() != 'banner_key']""/>
            <xsl:attribute name=""name"">Blaidd-luth</xsl:attribute>
            <xsl:attribute name=""banner_key"">11.24.4.4922</xsl:attribute>
            <xsl:apply-templates select=""node()""/>
        </xsl:copy>
    </xsl:template>
</xsl:stylesheet>");

        var result = _sut.GetClanBannerKeys();

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("11.24.4.4922", result["clan_empire_north_1"]);
    }

    [TestMethod]
    public void GetClanBannerKeys_MergesXmlAndXslt()
    {
        File.WriteAllText(Path.Combine(_tempDir, "characters", "clans.xml"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<Factions>
    <Faction id=""clan_gondor_1"" banner_key=""11.92.92.3000"" />
</Factions>");
        File.WriteAllText(Path.Combine(_tempDir, "spclans.xslt"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""Faction[@id='clan_empire_north_1']"">
        <xsl:copy>
            <xsl:apply-templates select=""@*[local-name() != 'name' and local-name() != 'banner_key']""/>
            <xsl:attribute name=""name"">Blaidd-luth</xsl:attribute>
            <xsl:attribute name=""banner_key"">11.24.4.4922</xsl:attribute>
            <xsl:apply-templates select=""node()""/>
        </xsl:copy>
    </xsl:template>
</xsl:stylesheet>");

        var result = _sut.GetClanBannerKeys();

        Assert.AreEqual(2, result.Count);
        Assert.AreEqual("11.92.92.3000", result["clan_gondor_1"]);
        Assert.AreEqual("11.24.4.4922", result["clan_empire_north_1"]);
    }

    [TestMethod]
    public void GetClanBannerKeys_MissingFile_LogsWarningAndContinues()
    {
        var result = _sut.GetClanBannerKeys();

        Assert.AreEqual(0, result.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("not found")));
    }

    [TestMethod]
    public void GetClanBannerKeys_XsltTemplateWithoutBannerKey_Skipped()
    {
        File.WriteAllText(Path.Combine(_tempDir, "spclans.xslt"), @"<?xml version=""1.0"" encoding=""utf-8""?>
<xsl:stylesheet version=""1.0"" xmlns:xsl=""http://www.w3.org/1999/XSL/Transform"">
    <xsl:template match=""Faction[@id='player_faction']"">
        <xsl:copy>
            <xsl:apply-templates select=""@*[local-name() != 'initial_home_settlement']""/>
            <xsl:attribute name=""initial_home_settlement"">Settlement.town_K1</xsl:attribute>
            <xsl:apply-templates select=""node()""/>
        </xsl:copy>
    </xsl:template>
</xsl:stylesheet>");

        var result = _sut.GetClanBannerKeys();

        Assert.AreEqual(0, result.Count);
    }
}
