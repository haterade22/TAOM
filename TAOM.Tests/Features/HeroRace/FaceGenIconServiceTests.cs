using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class FaceGenIconServiceTests
{
    private FaceGenIconService _sut;
    private IModulePathAdapter _modulePathAdapter;
    private IModLogger _logger;
    private string _testDataDir;

    private const string TestSkinsXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
<skins>
    <race id=""dwarf"">
        <skin gender=""0"">
            <hair_meshes group_id=""7"">
                <hair_mesh>
                    <style_tags><style_tag name=""Bald"" /></style_tags>
                </hair_mesh>
                <hair_mesh name=""dwarf_hair_a"" cover_type1=""dwarf_hair_a"">
                    <style_tags><style_tag name=""Ponytail"" /></style_tags>
                </hair_mesh>
                <hair_mesh name=""dwarf_hair_b"" cover_type1=""dwarf_hair_b"">
                    <style_tags><style_tag name=""LongAndBushy"" /></style_tags>
                </hair_mesh>
            </hair_meshes>
            <beard_meshes>
                <beard_mesh>
                    <style_tags><style_tag name=""Cleanshaven"" /></style_tags>
                </beard_mesh>
                <beard_mesh name=""sk_dwarf_beard_a_01"" cover_type1=""sk_dwarf_beard_a_01"" />
                <beard_mesh name=""sk_dwarf_beard_a_02"" cover_type1=""sk_dwarf_beard_a_02"" />
            </beard_meshes>
        </skin>
        <skin gender=""1"">
            <hair_meshes group_id=""7"">
                <hair_mesh>
                    <style_tags><style_tag name=""Bald"" /></style_tags>
                </hair_mesh>
                <hair_mesh name=""dwarf_female_hair_a"" cover_type1=""dwarf_female_hair_a"">
                    <style_tags><style_tag name=""Short"" /></style_tags>
                </hair_mesh>
            </hair_meshes>
            <beard_meshes>
                <beard_mesh>
                    <style_tags><style_tag name=""Cleanshaven"" /></style_tags>
                </beard_mesh>
            </beard_meshes>
        </skin>
    </race>
</skins>";

    [TestInitialize]
    public void Setup()
    {
        _modulePathAdapter = Substitute.For<IModulePathAdapter>();
        _logger = Substitute.For<IModLogger>();

        _testDataDir = Path.Combine(Path.GetTempPath(), "TAOM_Tests_" + Path.GetRandomFileName());
        Directory.CreateDirectory(_testDataDir);
        var moduleDataDir = Path.Combine(_testDataDir, "ModuleData");
        Directory.CreateDirectory(moduleDataDir);
        File.WriteAllText(Path.Combine(moduleDataDir, "skins.xml"), TestSkinsXml);

        _modulePathAdapter.GetModuleFullPath("LOTRLOME_Armory").Returns(_testDataDir + Path.DirectorySeparatorChar);
        _sut = new FaceGenIconService(_modulePathAdapter, _logger);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_testDataDir))
            Directory.Delete(_testDataDir, true);
    }

    [TestMethod]
    public void GetBeardName_ValidIndex_ReturnsName()
    {
        var result = _sut.GetBeardName(1, 0, 0);

        Assert.AreEqual("sk_dwarf_beard_a_01", result);
    }

    [TestMethod]
    public void GetBeardName_SecondBeard_ReturnsCorrectName()
    {
        var result = _sut.GetBeardName(2, 0, 0);

        Assert.AreEqual("sk_dwarf_beard_a_02", result);
    }

    [TestMethod]
    public void GetBeardName_CleanShavenIndex_ReturnsNull()
    {
        var result = _sut.GetBeardName(0, 0, 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetBeardName_OutOfBoundsIndex_ReturnsNull()
    {
        var result = _sut.GetBeardName(99, 0, 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetHairName_ValidIndex_ReturnsName()
    {
        var result = _sut.GetHairName(1, 0, 0);

        Assert.AreEqual("dwarf_hair_a", result);
    }

    [TestMethod]
    public void GetHairName_SecondHair_ReturnsCorrectName()
    {
        var result = _sut.GetHairName(2, 0, 0);

        Assert.AreEqual("dwarf_hair_b", result);
    }

    [TestMethod]
    public void GetHairName_BaldIndex_ReturnsNull()
    {
        var result = _sut.GetHairName(0, 0, 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetHairName_OutOfBoundsIndex_ReturnsNull()
    {
        var result = _sut.GetHairName(99, 0, 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetHairName_FemaleGender_ReturnsFemaleHair()
    {
        var result = _sut.GetHairName(1, 0, 1);

        Assert.AreEqual("dwarf_female_hair_a", result);
    }

    [TestMethod]
    public void GetBeardName_FemaleGender_CleanShavenOnly_ReturnsNull()
    {
        var result = _sut.GetBeardName(0, 0, 1);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetBeardName_MissingSkinsXml_ReturnsNullAndLogsError()
    {
        _modulePathAdapter.GetModuleFullPath("LOTRLOME_Armory").Returns("C:/NonExistent/Path/");
        var sut = new FaceGenIconService(_modulePathAdapter, _logger);

        var result = sut.GetBeardName(1, 0, 0);

        Assert.IsNull(result);
        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("skins.xml")));
    }

    [TestMethod]
    public void GetBeardName_CalledTwice_LoadsXmlOnlyOnce()
    {
        _sut.GetBeardName(1, 0, 0);
        _sut.GetBeardName(2, 0, 0);

        _modulePathAdapter.Received(1).GetModuleFullPath("LOTRLOME_Armory");
    }

    [TestMethod]
    public void GetHairName_CalledAfterGetBeardName_DoesNotReloadXml()
    {
        _sut.GetBeardName(1, 0, 0);
        _sut.GetHairName(1, 0, 0);

        _modulePathAdapter.Received(1).GetModuleFullPath("LOTRLOME_Armory");
    }

    [TestMethod]
    public void GetBeardName_NegativeIndex_ReturnsNull()
    {
        var result = _sut.GetBeardName(-1, 0, 0);

        Assert.IsNull(result);
    }

    [TestMethod]
    public void GetHairName_NegativeIndex_ReturnsNull()
    {
        var result = _sut.GetHairName(-1, 0, 0);

        Assert.IsNull(result);
    }
}
