using System.Collections.Generic;
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
    public void Serialize_RoundTrip_PreservesTheShoreLeaveAndServiceWarFields()
    {
        // SAVE COMPAT. These three landed with the 2026-08-11 field fixes and are the kind of field
        // whose absence is silent: a war list that fails to round-trip means the discharge unwinds
        // nothing and the player keeps his commander's wars for the rest of the campaign, with no
        // error anywhere.
        var record = EnlistedRecord();
        record.OnTownLeave = true;
        record.MirroredWars = new List<string> { "empire", "sturgia" };
        record.EnemiesAtOath = new List<string> { "vlandia" };
        record.OathFactionId = "player_clan";

        var parsed = ParseOrFail(record.Serialize());

        Assert.IsTrue(parsed.OnTownLeave);
        CollectionAssert.AreEqual(new List<string> { "empire", "sturgia" }, parsed.MirroredWars);
        CollectionAssert.AreEqual(new List<string> { "vlandia" }, parsed.EnemiesAtOath);

        // The pin is what stops a discharge making peace on behalf of a faction that never declared
        // the war. If it fails to round-trip, a save/load mid-service silently restores the exact
        // asymmetry it exists to prevent.
        Assert.AreEqual("player_clan", parsed.OathFactionId);
    }

    [TestMethod]
    public void Deserialize_ASavePredatingTheseFields_YieldsEmptyListsNotNull()
    {
        // The upgrade path. Every existing save has no mirroredWars/enemiesAtOath/onTownLeave key,
        // and the discharge path enumerates both lists unguarded — a null here is an NRE on the
        // first discharge after updating, for every player mid-service.
        var legacy = "state=2;heroId=main_hero;commanderId=lord_1_1;enlistedDay=120.25";

        var parsed = ParseOrFail(legacy);

        Assert.IsFalse(parsed.OnTownLeave);
        Assert.IsNotNull(parsed.MirroredWars);
        Assert.IsNotNull(parsed.EnemiesAtOath);
        Assert.AreEqual(0, parsed.MirroredWars.Count);
        Assert.AreEqual(0, parsed.EnemiesAtOath.Count);

        // No pin on an older save. Null (not "") so ServiceDiplomacyService can tell "we never
        // recorded one" from "we recorded a faction whose id is empty", and unwind as before.
        Assert.IsNull(parsed.OathFactionId);
    }

    [TestMethod]
    public void Reset_ClearsTheShoreLeavePassAndTheWarLists()
    {
        // The maintenance pump early-returns on !IsEnlisted, so it can never revoke a pass after
        // discharge. Reset() is therefore the only thing standing between a stale OnTownLeave and a
        // NEXT enlistment that silently suspends the settlement redirect from its first day.
        var record = EnlistedRecord();
        record.OnTownLeave = true;
        record.MirroredWars = new List<string> { "empire" };
        record.EnemiesAtOath = new List<string> { "vlandia" };
        record.OathFactionId = "player_clan";

        record.Reset();

        Assert.IsFalse(record.OnTownLeave);
        Assert.AreEqual(0, record.MirroredWars.Count);
        Assert.AreEqual(0, record.EnemiesAtOath.Count);
        Assert.IsNull(record.OathFactionId);
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

    [TestMethod]
    public void RoundTrip_PendingAttachmentAndRetryBudget_Survive()
    {
        var record = new EnlistmentRecord
        {
            State = EnlistmentState.EnlistedAttached,
            EnlistedHeroId = "main_hero",
            CommanderHeroId = "lord_1_1",
            PendingCommanderAttachment = true,
            NextAttachRetryAtHours = 3301.5,
        };

        Assert.IsTrue(EnlistmentRecord.TryParse(record.Serialize(), out var parsed));
        Assert.IsTrue(parsed.PendingCommanderAttachment);
        Assert.AreEqual(3301.5, parsed.NextAttachRetryAtHours.Value, 1e-9);
    }

    [TestMethod]
    public void TryParse_LegacyRecordWithoutNewKeys_DefaultsToFalseAndNull()
    {
        // Saves written before the pump existed must load and behave as 'retry immediately'.
        Assert.IsTrue(EnlistmentRecord.TryParse("state=2;heroId=main_hero;commanderId=lord_1_1", out var parsed));

        Assert.IsFalse(parsed.PendingCommanderAttachment);
        Assert.IsNull(parsed.NextAttachRetryAtHours);
    }

    [TestMethod]
    public void Serialize_OmitsTheNewKeysWhenUnset()
    {
        var record = new EnlistmentRecord
        {
            State = EnlistmentState.EnlistedAttached,
            EnlistedHeroId = "main_hero",
            CommanderHeroId = "lord_1_1",
        };

        var serialized = record.Serialize();

        StringAssert.DoesNotMatch(serialized, new System.Text.RegularExpressions.Regex("pendingAttach"));
        StringAssert.DoesNotMatch(serialized, new System.Text.RegularExpressions.Regex("nextAttachHour"));
    }

    [TestMethod]
    public void TryParse_NonFiniteRetryStamp_DropsToNullRatherThanFreezingEveryRetry()
    {
        // A NaN stamp would compare false against every future hour and freeze attach retries for
        // the rest of the campaign — the field-level NaN gate must drop it.
        Assert.IsTrue(EnlistmentRecord.TryParse(
            "state=2;heroId=main_hero;commanderId=lord_1_1;nextAttachHour=NaN", out var parsed));

        Assert.IsNull(parsed.NextAttachRetryAtHours);
    }

    [TestMethod]
    public void Reset_ClearsTheRetryBudget()
    {
        var record = new EnlistmentRecord { PendingCommanderAttachment = true, NextAttachRetryAtHours = 10.0 };

        record.Reset();

        Assert.IsFalse(record.PendingCommanderAttachment);
        Assert.IsNull(record.NextAttachRetryAtHours);
    }

}
