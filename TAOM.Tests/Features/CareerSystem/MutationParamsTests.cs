using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CareerSystem.Mutations;

namespace TAOM.Tests.Features.CareerSystem;

[TestClass]
public class MutationParamsTests
{
    private static MutationParams Make(string key, string value) =>
        new MutationParams(new Dictionary<string, string> { [key] = value });

    [TestMethod]
    public void GetFloat_ValidValue_ReturnsParsed()
    {
        Assert.AreEqual(2.5f, Make("value", "2.5").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_MissingKey_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("other", "2.5").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_UnparseableValue_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "6s").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_NegativeFiniteValue_ReturnsParsed()
    {
        // A negative factor is a legitimate authoring choice: the guard rejects only non-finite values.
        Assert.AreEqual(-2.5f, Make("value", "-2.5").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_NaN_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "NaN").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_PositiveInfinity_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "Infinity").GetFloat("value", -99f));
    }

    [TestMethod]
    public void GetFloat_NegativeInfinity_ReturnsDefault()
    {
        Assert.AreEqual(-99f, Make("value", "-Infinity").GetFloat("value", -99f));
    }
}
