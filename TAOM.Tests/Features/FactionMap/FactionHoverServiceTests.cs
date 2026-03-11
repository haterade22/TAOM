using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Features.FactionMap;
using TAOM.Features.FactionMap.Models;

namespace TAOM.Tests.Features.FactionMap;

[TestClass]
public class FactionHoverServiceTests
{
    private IFactionRegistryService _registry;
    private FactionHoverService _sut;

    [TestInitialize]
    public void Setup()
    {
        _registry = Substitute.For<IFactionRegistryService>();
        _sut = new FactionHoverService(_registry);
    }

    [TestMethod]
    public void UpdateHover_NewFaction_ReturnsShowChange()
    {
        _registry.GetFaction("kingdom_of_gondor").Returns(new FactionData
        {
            Name = "Kingdom of Gondor",
            Color = "#4169E1FF",
        });

        var result = _sut.UpdateHover("Kingdom of Gondor");

        Assert.IsNotNull(result);
        Assert.AreEqual("Kingdom of Gondor", result.FactionName);
        Assert.IsTrue(result.ShouldShow);
    }

    [TestMethod]
    public void UpdateHover_SameFactionAgain_ReturnsNull()
    {
        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Gondor", Color = "#FFF" });

        _sut.UpdateHover("Gondor");
        var result = _sut.UpdateHover("Gondor");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void UpdateHover_DifferentFaction_ReturnsNewChange()
    {
        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Gondor", Color = "#4169E1FF" });
        _sut.UpdateHover("Gondor");

        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Rohan", Color = "#228B22FF" });
        var result = _sut.UpdateHover("Rohan");

        Assert.IsNotNull(result);
        Assert.AreEqual("Rohan", result.FactionName);
        Assert.IsTrue(result.ShouldShow);
    }

    [TestMethod]
    public void UpdateHover_EmptyAfterFaction_ReturnsHideChange()
    {
        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Gondor", Color = "#FFF" });
        _sut.UpdateHover("Gondor");

        var result = _sut.UpdateHover("");

        Assert.IsNotNull(result);
        Assert.IsFalse(result.ShouldShow);
    }

    [TestMethod]
    public void UpdateHover_NullAfterFaction_ReturnsHideChange()
    {
        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Gondor", Color = "#FFF" });
        _sut.UpdateHover("Gondor");

        var result = _sut.UpdateHover(null!);

        Assert.IsNotNull(result);
        Assert.IsFalse(result.ShouldShow);
    }

    [TestMethod]
    public void UpdateHover_EmptyToEmpty_ReturnsNull()
    {
        var result = _sut.UpdateHover("");

        Assert.IsNull(result);
    }

    [TestMethod]
    public void UpdateHover_FactionWithNoColor_UsesWhiteFallback()
    {
        _registry.GetFaction("Gondor").Returns(new FactionData { Name = "Gondor", Color = "" });
        _registry.GetAllRegionKeys().Returns(new[] { "kingdom_of_gondor" });
        _registry.GetFactionForRegion("kingdom_of_gondor").Returns(new FactionData { Name = "Gondor", Color = "" });

        var result = _sut.UpdateHover("Gondor");

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ShouldShow);
        Assert.AreEqual("#FFFFFFFF", result.ColorHex);
    }

    [TestMethod]
    public void Reset_ClearsState_NextHoverReturnsChange()
    {
        _registry.GetFaction(Arg.Any<string>()).Returns(new FactionData { Name = "Gondor", Color = "#FFF" });
        _sut.UpdateHover("Gondor");

        _sut.Reset();

        var result = _sut.UpdateHover("Gondor");
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ShouldShow);
    }
}
