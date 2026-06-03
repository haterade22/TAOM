using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.CultureConversion;
using TAOM.Features.CultureConversion.Domain;

namespace TAOM.Tests.Features.CultureConversion;

[TestClass]
public class CultureConversionStoreTests
{
    private IModLogger _logger = null!;
    private CultureConversionStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new CultureConversionStore(_logger);
    }

    [TestMethod]
    public void Put_Get_RoundTrips()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor"));

        Assert.IsTrue(_sut.TryGet("town_ES1", out var record));
        Assert.AreEqual("mordor", record.AppliedCultureId);
    }

    [TestMethod]
    public void IsConverted_AppliedOverride_True()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor"));
        Assert.IsTrue(_sut.IsConverted("town_ES1"));
    }

    [TestMethod]
    public void IsConverted_PendingOnly_False()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", pendingStartDays: 1.0, pendingTargetCultureId: "mordor"));
        Assert.IsFalse(_sut.IsConverted("town_ES1"));
    }

    [TestMethod]
    public void IsConverted_Unknown_False()
    {
        Assert.IsFalse(_sut.IsConverted("nope"));
        Assert.IsFalse(_sut.IsConverted(null));
    }

    [TestMethod]
    public void Remove_ExistingRecord_RemovesIt()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor"));

        Assert.IsTrue(_sut.Remove("town_ES1"));
        Assert.AreEqual(0, _sut.Count);
        Assert.IsFalse(_sut.IsConverted("town_ES1"));
    }

    [TestMethod]
    public void Serialize_Deserialize_RoundtripPreservesRecords()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor"));
        _sut.Put(new SettlementConversionRecord("castle_ES2", "gondor", pendingStartDays: 12.5, pendingTargetCultureId: "isengard"));

        var serialized = _sut.Serialize();

        var restored = new CultureConversionStore(_logger);
        restored.Deserialize(serialized);

        Assert.AreEqual(2, restored.Count);
        Assert.IsTrue(restored.TryGet("town_ES1", out var converted));
        Assert.AreEqual("mordor", converted.AppliedCultureId);
        Assert.IsTrue(restored.TryGet("castle_ES2", out var pending));
        Assert.AreEqual(12.5, pending.PendingStartDays.Value, 0.0001);
        Assert.AreEqual("isengard", pending.PendingTargetCultureId);
    }

    [TestMethod]
    public void Deserialize_NullData_ClearsExisting()
    {
        _sut.Put(new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor"));

        _sut.Deserialize(null);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Deserialize_MalformedEntry_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "town_good", "gondor|mordor||" },
            { "town_bad", "garbage" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(1, _sut.Count);
        Assert.IsTrue(_sut.IsConverted("town_good"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("town_bad")));
    }
}
