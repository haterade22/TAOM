using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TAOM.Features.LifeDeathOption;

namespace TAOM.Tests.Features.LifeDeathOption;

[TestClass]
public class LifeDeathOptionServiceTests
{
    private sealed class FakeModules : ILoadedModuleIds
    {
        private readonly string[] _ids;
        public FakeModules(params string[] ids) { _ids = ids; }
        public IReadOnlyList<string> Get() => _ids;
    }

    [TestMethod]
    public void ShouldOffer_OfficialModuleNotLoaded_ReturnsTrue()
    {
        var service = new LifeDeathOptionService(new FakeModules("Native", "SandBoxCore", "Sandbox", "TAOM"));
        Assert.IsTrue(service.ShouldOffer());
    }

    [TestMethod]
    public void ShouldOffer_OfficialModuleLoaded_ReturnsFalse()
    {
        var service = new LifeDeathOptionService(new FakeModules("Native", "BirthAndDeath", "TAOM"));
        Assert.IsFalse(service.ShouldOffer());
    }

    [TestMethod]
    public void ShouldOffer_OfficialModuleIdInOtherCase_ReturnsFalse()
    {
        var service = new LifeDeathOptionService(new FakeModules("birthanddeath"));
        Assert.IsFalse(service.ShouldOffer());
    }

    [TestMethod]
    public void ShouldOffer_IdThatOnlyContainsTheOfficialId_ReturnsTrue()
    {
        var service = new LifeDeathOptionService(new FakeModules("BirthAndDeath.Extras"));
        Assert.IsTrue(service.ShouldOffer());
    }
}
