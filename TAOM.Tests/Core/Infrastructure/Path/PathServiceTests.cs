using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;

namespace TAOM.Tests.Core.Infrastructure.Path;

[TestClass]
public class PathServiceTests
{
    private PathService _sut;
    private IModulePathAdapter _modulePathAdapter;

    [TestInitialize]
    public void Setup()
    {
        _modulePathAdapter = Substitute.For<IModulePathAdapter>();
        _modulePathAdapter.GetModuleFullPath("TAOM").Returns("C:/Game/Modules/TAOM/");
        _sut = new PathService(_modulePathAdapter);
    }

    [TestMethod]
    public void ModuleRootPath_ReturnsAdapterPath()
    {
        var result = _sut.ModuleRootPath;

        Assert.AreEqual("C:/Game/Modules/TAOM/", result);
    }

    [TestMethod]
    public void ModuleDataPath_AppendsModuleData()
    {
        var result = _sut.ModuleDataPath;

        Assert.AreEqual("C:/Game/Modules/TAOM/ModuleData/", result);
    }

    [TestMethod]
    public void ConfigPath_AppendsConfigs()
    {
        var result = _sut.ConfigPath;

        Assert.AreEqual("C:/Game/Modules/TAOM/ModuleData/configs/", result);
    }

    [TestMethod]
    public void ModuleRootPath_CallsAdapterWithCorrectModuleName()
    {
        _ = _sut.ModuleRootPath;

        _modulePathAdapter.Received(1).GetModuleFullPath("TAOM");
    }
}
