using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.EditorCacheRebuild.Caching;

namespace TAOM.Tests.Features.EditorCacheRebuild.Caching;

[TestClass]
public class SortedPathKeyTests
{
    [TestMethod]
    public void Ctor_DifferentIds_SortsLexicographically()
    {
        var key = new SortedPathKey("zulu", false, "alpha", false);
        Assert.AreEqual("alpha", key.Id1);
        Assert.AreEqual("zulu", key.Id2);
    }

    [TestMethod]
    public void Ctor_AlreadySorted_PreservesOrder()
    {
        var key = new SortedPathKey("alpha", false, "zulu", false);
        Assert.AreEqual("alpha", key.Id1);
        Assert.AreEqual("zulu", key.Id2);
    }

    [TestMethod]
    public void Ctor_SameIdPortBeforeGate_VanillaCanonical()
    {
        // Vanilla NavigationCacheElement<T>.Sort places the PORT entry first when ids match.
        var key = new SortedPathKey("settlement_x", true, "settlement_x", false);
        Assert.AreEqual("settlement_x", key.Id1);
        Assert.IsTrue(key.IsPort1);
        Assert.IsFalse(key.IsPort2);
    }

    [TestMethod]
    public void Ctor_SameIdGateFirst_SwapsToPortFirst()
    {
        var key = new SortedPathKey("settlement_x", false, "settlement_x", true);
        Assert.AreEqual("settlement_x", key.Id1);
        Assert.IsTrue(key.IsPort1);
        Assert.IsFalse(key.IsPort2);
    }

    [TestMethod]
    public void Equals_SymmetricInput_ProducesSameKey()
    {
        var a = new SortedPathKey("alpha", false, "zulu", true);
        var b = new SortedPathKey("zulu", true, "alpha", false);
        Assert.AreEqual(a, b);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void Equals_DifferentPortFlags_NotEqual()
    {
        var a = new SortedPathKey("alpha", false, "zulu", false);
        var b = new SortedPathKey("alpha", true, "zulu", false);
        Assert.AreNotEqual(a, b);
    }

    [TestMethod]
    public void Ctor_SameIdSameGateGate_Canonicalized()
    {
        // Codex Finding 6 (P3): degenerate self-pair coverage.
        // Vanilla Sort: cmp==0 && !isPort1 ⇒ swap=false; entries identical so order is well-defined.
        var key = new SortedPathKey("settlement_x", false, "settlement_x", false);
        Assert.AreEqual("settlement_x", key.Id1);
        Assert.AreEqual("settlement_x", key.Id2);
        Assert.IsFalse(key.IsPort1);
        Assert.IsFalse(key.IsPort2);
    }

    [TestMethod]
    public void Ctor_SameIdSamePortPort_Canonicalized()
    {
        // Codex Finding 6 (P3): degenerate self-pair coverage for port-port case.
        var key = new SortedPathKey("settlement_x", true, "settlement_x", true);
        Assert.AreEqual("settlement_x", key.Id1);
        Assert.AreEqual("settlement_x", key.Id2);
        Assert.IsTrue(key.IsPort1);
        Assert.IsTrue(key.IsPort2);
    }
}
