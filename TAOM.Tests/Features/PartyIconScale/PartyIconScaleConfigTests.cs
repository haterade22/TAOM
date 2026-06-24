using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.PartyIconScale;

namespace TAOM.Tests.Features.PartyIconScale;

[TestClass]
public class PartyIconScaleConfigTests
{
    private const float Delta = 1e-6f;

    [TestMethod]
    public void Resolve_ValidMidRange_ReturnsValue()
    {
        Assert.AreEqual(0.2f, PartyIconScaleConfig.Resolve(0.2f), Delta);
    }

    [TestMethod]
    public void Resolve_DefaultSliderValue_ReturnsItself()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default,
            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Default), Delta);
    }

    [TestMethod]
    public void Resolve_AtMinBoundary_ReturnsValue()
    {
        Assert.AreEqual(PartyIconScaleConfig.Min,
            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Min), Delta);
    }

    [TestMethod]
    public void Resolve_AtMaxBoundary_ReturnsValue()
    {
        Assert.AreEqual(PartyIconScaleConfig.Max,
            PartyIconScaleConfig.Resolve(PartyIconScaleConfig.Max), Delta);
    }

    [TestMethod]
    public void Resolve_Null_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(null), Delta);
    }

    [TestMethod]
    public void Resolve_NaN_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(float.NaN), Delta);
    }

    [TestMethod]
    public void Resolve_PositiveInfinity_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default,
            PartyIconScaleConfig.Resolve(float.PositiveInfinity), Delta);
    }

    [TestMethod]
    public void Resolve_NegativeInfinity_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default,
            PartyIconScaleConfig.Resolve(float.NegativeInfinity), Delta);
    }

    [TestMethod]
    public void Resolve_BelowMin_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(0.01f), Delta);
    }

    [TestMethod]
    public void Resolve_AboveMax_ReturnsDefault()
    {
        Assert.AreEqual(PartyIconScaleConfig.Default, PartyIconScaleConfig.Resolve(2.0f), Delta);
    }
}
