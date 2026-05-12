using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Map.DistanceCache;
using TAOM.Features.EditorCacheRebuild.Phase1;

namespace TAOM.Tests.Features.EditorCacheRebuild.Phase1;

[TestClass]
public class ChangedSettlementsFilterTests
{
    private static ISettlementDataHolder Holder(string id)
    {
        var s = Substitute.For<ISettlementDataHolder>();
        s.StringId.Returns(id);
        return s;
    }

    [TestMethod]
    public void ShouldComputePair_S1Changed_ReturnsTrue()
    {
        var filter = new ChangedSettlementsFilter(new HashSet<string> { "a" });

        var result = filter.ShouldComputePair(Holder("a"), Holder("x"));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldComputePair_S2Changed_ReturnsTrue()
    {
        var filter = new ChangedSettlementsFilter(new HashSet<string> { "b" });

        var result = filter.ShouldComputePair(Holder("a"), Holder("b"));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldComputePair_NeitherChanged_ReturnsFalse()
    {
        var filter = new ChangedSettlementsFilter(new HashSet<string> { "z" });

        var result = filter.ShouldComputePair(Holder("a"), Holder("b"));

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void ShouldComputePair_BothChanged_ReturnsTrue()
    {
        var filter = new ChangedSettlementsFilter(new HashSet<string> { "a", "b" });

        var result = filter.ShouldComputePair(Holder("a"), Holder("b"));

        Assert.IsTrue(result);
    }

    [TestMethod]
    public void ShouldComputePair_EmptyChangedSet_AlwaysReturnsFalse()
    {
        var filter = new ChangedSettlementsFilter(new HashSet<string>());

        Assert.IsFalse(filter.ShouldComputePair(Holder("a"), Holder("b")));
    }
}
