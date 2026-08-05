using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentRecordTests
{
    private static EnlistmentRecord EnlistedRecord(EnlistmentState state = EnlistmentState.EnlistedAttached)
    {
        return new EnlistmentRecord
        {
            State = state,
            EnlistedHeroId = "main_hero",
            CommanderHeroId = "lord_1_1",
            EnlistedAtDay = 120.25,
            ContractEndDay = 485.25,
        };
    }

    [TestMethod]
    public void Serialize_RoundTrip_PreservesAllFields()
    {
        var record = EnlistedRecord();
        record.GraceEndsAtDay = 130.5;
        record.PetitionCommanderId = "lord_2_2";

        var parsed = ParseOrFail(record.Serialize());

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
        Assert.AreEqual("main_hero", parsed.EnlistedHeroId);
        Assert.AreEqual("lord_1_1", parsed.CommanderHeroId);
        Assert.AreEqual("lord_2_2", parsed.PetitionCommanderId);
        Assert.AreEqual(120.25, parsed.EnlistedAtDay);
        Assert.AreEqual(485.25, parsed.ContractEndDay);
        Assert.AreEqual(130.5, parsed.GraceEndsAtDay);
    }

    [TestMethod]
    public void Serialize_FreshNotEnlisted_RoundTripsToNotEnlisted()
    {
        var parsed = ParseOrFail(new EnlistmentRecord().Serialize());

        Assert.AreEqual(EnlistmentState.NotEnlisted, parsed.State);
        Assert.IsNull(parsed.EnlistedHeroId);
        Assert.IsNull(parsed.CommanderHeroId);
        Assert.IsNull(parsed.ContractEndDay);
    }

    [TestMethod]
    public void Serialize_EnlistedBattleState_CoercesToEnlistedAttached()
    {
        // Battle/encounter reality is re-derived from the engine at load — the transient
        // state must never persist.
        var record = EnlistedRecord(EnlistmentState.EnlistedBattle);

        var parsed = ParseOrFail(record.Serialize());

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
    }

    [TestMethod]
    public void Serialize_DischargingState_CoercesToEnlistedAttached()
    {
        var record = EnlistedRecord(EnlistmentState.Discharging);

        var parsed = ParseOrFail(record.Serialize());

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
    }

    [TestMethod]
    public void TryParse_UnknownKeys_IgnoredForForwardCompat()
    {
        var serialized = EnlistedRecord().Serialize() + ";futureKey=futureValue";

        var parsed = ParseOrFail(serialized);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
        Assert.AreEqual("lord_1_1", parsed.CommanderHeroId);
    }

    [TestMethod]
    public void TryParse_MissingOptionalFields_DefaultsToNull()
    {
        var parsed = ParseOrFail("state=2;heroId=main_hero;commanderId=lord_1_1");

        Assert.IsNull(parsed.EnlistedAtDay);
        Assert.IsNull(parsed.ContractEndDay);
        Assert.IsNull(parsed.GraceEndsAtDay);
        Assert.IsNull(parsed.PetitionCommanderId);
    }

    [DataTestMethod]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    [DataRow("-Infinity")]
    [DataRow("garbage")]
    public void TryParse_NonFiniteContractEnd_DropsFieldKeepsState(string badValue)
    {
        var serialized = $"state=2;heroId=main_hero;commanderId=lord_1_1;contractEndDay={badValue}";

        var parsed = ParseOrFail(serialized);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
        Assert.IsNull(parsed.ContractEndDay);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void TryParse_NullOrEmpty_ReturnsFalse(string serialized)
    {
        Assert.IsFalse(EnlistmentRecord.TryParse(serialized, out _));
    }

    [TestMethod]
    public void TryParse_MissingStateKey_ReturnsFalse()
    {
        Assert.IsFalse(EnlistmentRecord.TryParse("heroId=main_hero;commanderId=lord_1_1", out _));
    }

    [DataTestMethod]
    [DataRow("state=99")]
    [DataRow("state=-1")]
    [DataRow("state=notanumber")]
    public void TryParse_InvalidStateValue_ReturnsFalse(string serialized)
    {
        Assert.IsFalse(EnlistmentRecord.TryParse(serialized, out _));
    }

    [TestMethod]
    public void TryParse_EnlistedWithoutCommanderId_ReturnsFalse()
    {
        // An enlisted-family record without its identities cannot be faithfully restored;
        // the caller falls back to a fresh NotEnlisted record and the load normalizer's
        // ownerless-hidden-party guard rescues the world state.
        Assert.IsFalse(EnlistmentRecord.TryParse("state=2;heroId=main_hero", out _));
    }

    [TestMethod]
    public void TryParse_EnlistedWithoutHeroId_ReturnsFalse()
    {
        Assert.IsFalse(EnlistmentRecord.TryParse("state=2;commanderId=lord_1_1", out _));
    }

    [TestMethod]
    public void TryParse_PetitionWithoutPetitionCommanderId_ReturnsFalse()
    {
        Assert.IsFalse(EnlistmentRecord.TryParse("state=1", out _));
    }

    [TestMethod]
    public void TryParse_PersistedEnlistedBattle_CoercedToAttachedOnParse()
    {
        // Defense in depth: EnlistedBattle should never reach the wire, but a hand-edited
        // or pre-fix save must still load safely.
        var parsed = ParseOrFail("state=3;heroId=main_hero;commanderId=lord_1_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
    }

    [TestMethod]
    public void TryParse_PersistedDischarging_CoercedToAttachedOnParse()
    {
        var parsed = ParseOrFail("state=7;heroId=main_hero;commanderId=lord_1_1");

        Assert.AreEqual(EnlistmentState.EnlistedAttached, parsed.State);
    }

    [TestMethod]
    public void IsEnlisted_TrueForAllEnlistedFamilyStates()
    {
        foreach (var state in new[]
        {
            EnlistmentState.EnlistedAttached,
            EnlistmentState.EnlistedBattle,
            EnlistmentState.EnlistedDetachedOnDuty,
            EnlistmentState.EnlistedPlayerCaptive,
            EnlistmentState.CommanderUnavailable,
        })
        {
            Assert.IsTrue(EnlistedRecord(state).IsEnlisted, $"Expected IsEnlisted for {state}");
        }
    }

    [DataTestMethod]
    [DataRow(EnlistmentState.NotEnlisted)]
    [DataRow(EnlistmentState.PetitionPending)]
    public void IsEnlisted_FalseForNonServiceStates(EnlistmentState state)
    {
        var record = new EnlistmentRecord { State = state, PetitionCommanderId = "lord_1_1" };

        Assert.IsFalse(record.IsEnlisted);
    }

    [TestMethod]
    public void Reset_ClearsAllFieldsBackToFreshNotEnlisted()
    {
        var record = EnlistedRecord();
        record.GraceEndsAtDay = 130.5;
        record.PetitionCommanderId = "lord_2_2";

        record.Reset();

        Assert.AreEqual(EnlistmentState.NotEnlisted, record.State);
        Assert.IsNull(record.EnlistedHeroId);
        Assert.IsNull(record.CommanderHeroId);
        Assert.IsNull(record.PetitionCommanderId);
        Assert.IsNull(record.EnlistedAtDay);
        Assert.IsNull(record.ContractEndDay);
        Assert.IsNull(record.GraceEndsAtDay);
    }

    private static EnlistmentRecord ParseOrFail(string serialized)
    {
        Assert.IsTrue(EnlistmentRecord.TryParse(serialized, out var record), $"TryParse failed for: {serialized}");
        return record;
    }
}
