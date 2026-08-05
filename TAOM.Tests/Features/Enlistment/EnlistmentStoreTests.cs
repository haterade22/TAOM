using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Enlistment;
using TAOM.Features.Enlistment.Domain;

namespace TAOM.Tests.Features.Enlistment;

[TestClass]
public class EnlistmentStoreTests
{
    private IModLogger _logger = null!;
    private EnlistmentStore _store = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _store = new EnlistmentStore(_logger);
    }

    private void MakeEnlisted()
    {
        _store.Record.State = EnlistmentState.EnlistedAttached;
        _store.Record.EnlistedHeroId = "main_hero";
        _store.Record.CommanderHeroId = "lord_1_1";
        _store.Record.EnlistedAtDay = 100.0;
        _store.Record.ContractEndDay = 465.0;
    }

    [TestMethod]
    public void Record_FreshStore_StartsNotEnlisted()
    {
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Serialize_WritesVersionAndCoreKeys()
    {
        MakeEnlisted();

        var data = _store.Serialize();

        Assert.AreEqual("1", data[EnlistmentStore.VersionKey]);
        Assert.IsTrue(data.ContainsKey(EnlistmentStore.CoreKey));
        Assert.IsTrue(data[EnlistmentStore.CoreKey].Contains("commanderId=lord_1_1"));
    }

    [TestMethod]
    public void Serialize_DischargingState_LogsError()
    {
        // Discharging is atomic and must never reach a save; serializing it means the
        // pipeline was interrupted — coerce (record-level) and surface the anomaly.
        MakeEnlisted();
        _store.Record.State = EnlistmentState.Discharging;

        _store.Serialize();

        _logger.Received().LogError(Arg.Is<string>(s => s.Contains("Discharging")));
    }

    [TestMethod]
    public void SerializeDeserialize_RoundTrip_RestoresRecord()
    {
        MakeEnlisted();

        var data = _store.Serialize();
        var restored = new EnlistmentStore(_logger);
        restored.Deserialize(data);

        Assert.AreEqual(EnlistmentState.EnlistedAttached, restored.Record.State);
        Assert.AreEqual("lord_1_1", restored.Record.CommanderHeroId);
        Assert.AreEqual(465.0, restored.Record.ContractEndDay);
    }

    [TestMethod]
    public void Deserialize_Null_ResetsToFreshNotEnlisted()
    {
        MakeEnlisted();

        _store.Deserialize(null);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        Assert.IsNull(_store.Record.CommanderHeroId);
    }

    [TestMethod]
    public void Deserialize_EmptyData_ResetsToFreshNotEnlisted()
    {
        MakeEnlisted();

        _store.Deserialize(new Dictionary<string, string>());

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
    }

    [TestMethod]
    public void Deserialize_NewerUnknownVersion_WarnsAndResets()
    {
        // Fail-safe: a save written by a future TAOM must load as not-enlisted rather
        // than misinterpret fields — never leave an ownerless hidden MainParty.
        var data = new Dictionary<string, string>
        {
            [EnlistmentStore.VersionKey] = "2",
            [EnlistmentStore.CoreKey] = "state=2;heroId=main_hero;commanderId=lord_1_1",
        };

        _store.Deserialize(data);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("version")));
    }

    [TestMethod]
    public void Deserialize_MalformedCore_WarnsAndResets()
    {
        var data = new Dictionary<string, string>
        {
            [EnlistmentStore.VersionKey] = "1",
            [EnlistmentStore.CoreKey] = "state=2;heroId=main_hero", // enlisted without commander
        };

        _store.Deserialize(data);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Deserialize_MissingCoreWithValidVersion_WarnsAndResets()
    {
        var data = new Dictionary<string, string> { [EnlistmentStore.VersionKey] = "1" };

        _store.Deserialize(data);

        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        _logger.Received().LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Clear_ResetsRecordInPlace()
    {
        // The record reference is stable — services capture IEnlistmentStore, never the
        // record instance's identity, but Clear must not orphan an existing reference.
        MakeEnlisted();
        var recordBefore = _store.Record;

        _store.Clear();

        Assert.AreSame(recordBefore, _store.Record);
        Assert.AreEqual(EnlistmentState.NotEnlisted, _store.Record.State);
        Assert.IsNull(_store.Record.EnlistedHeroId);
    }
}
