using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CultureConversion.Domain;

namespace TAOM.Tests.Features.CultureConversion;

[TestClass]
public class SettlementConversionRecordTests
{
    [TestMethod]
    public void EffectiveCultureId_NoOverride_ReturnsOriginal()
    {
        var record = new SettlementConversionRecord("town_ES1", "gondor");
        Assert.AreEqual("gondor", record.EffectiveCultureId);
        Assert.IsFalse(record.IsConverted);
    }

    [TestMethod]
    public void EffectiveCultureId_WithOverride_ReturnsApplied()
    {
        var record = new SettlementConversionRecord("town_ES1", "gondor", appliedCultureId: "mordor");
        Assert.AreEqual("mordor", record.EffectiveCultureId);
        Assert.IsTrue(record.IsConverted);
    }

    [TestMethod]
    public void HasPending_RequiresBothStartAndTarget()
    {
        var record = new SettlementConversionRecord("town_ES1", "gondor");
        Assert.IsFalse(record.HasPending);

        record.StartPending(100.0, "mordor");
        Assert.IsTrue(record.HasPending);

        record.ClearPending();
        Assert.IsFalse(record.HasPending);
    }

    [TestMethod]
    public void Serialize_Parse_RoundtripPreservesAllFields()
    {
        var record = new SettlementConversionRecord("town_ES1", "gondor",
            appliedCultureId: "mordor", pendingStartDays: 123.456, pendingTargetCultureId: "isengard");

        var serialized = record.Serialize();
        Assert.IsTrue(SettlementConversionRecord.TryParse("town_ES1", serialized, out var restored));

        Assert.AreEqual("gondor", restored.OriginalCultureId);
        Assert.AreEqual("mordor", restored.AppliedCultureId);
        Assert.AreEqual(123.456, restored.PendingStartDays.Value, 0.0001);
        Assert.AreEqual("isengard", restored.PendingTargetCultureId);
    }

    [TestMethod]
    public void Serialize_Parse_NoOverrideNoPending_Roundtrips()
    {
        var record = new SettlementConversionRecord("town_ES1", "gondor");

        Assert.IsTrue(SettlementConversionRecord.TryParse("town_ES1", record.Serialize(), out var restored));

        Assert.AreEqual("gondor", restored.OriginalCultureId);
        Assert.IsNull(restored.AppliedCultureId);
        Assert.IsFalse(restored.HasPending);
    }

    [TestMethod]
    public void TryParse_EmptyOriginal_ReturnsFalse()
    {
        Assert.IsFalse(SettlementConversionRecord.TryParse("town_ES1", "|mordor||", out _));
    }

    [TestMethod]
    public void TryParse_WrongSegmentCount_ReturnsFalse()
    {
        Assert.IsFalse(SettlementConversionRecord.TryParse("town_ES1", "gondor|mordor", out _));
    }

    [TestMethod]
    public void TryParse_NaNPendingStart_DropsPendingButKeepsOverride()
    {
        // original=gondor, applied=mordor, pendingStart=NaN, pendingTarget=isengard
        Assert.IsTrue(SettlementConversionRecord.TryParse("town_ES1", "gondor|mordor|NaN|isengard", out var record));

        Assert.AreEqual("mordor", record.AppliedCultureId, "Applied override must survive a malformed pending timer.");
        Assert.IsFalse(record.HasPending, "A NaN pending start must be dropped.");
    }

    [TestMethod]
    public void TryParse_InfinityPendingStart_DropsPending()
    {
        Assert.IsTrue(SettlementConversionRecord.TryParse("town_ES1", "gondor||Infinity|mordor", out var record));
        Assert.IsFalse(record.HasPending);
        Assert.IsFalse(record.IsConverted);
    }
}
