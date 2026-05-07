using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.Messengers;
using TAOM.Features.Messengers.Domain;

namespace TAOM.Tests.Features.Messengers;

[TestClass]
public class MessengerStateStoreTests
{
    private IModLogger _logger = null!;
    private MessengerStateStore _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new MessengerStateStore(_logger);
    }

    private static PendingMessenger Make(string id, double dispatchDays = 1.0, float x = 1.5f, float y = 2.5f, bool arrived = false) =>
        new PendingMessenger(id, dispatchDays, new MapCoord(x, y), arrived);

    [TestMethod]
    public void Add_NewMessenger_StoredAndCountIncreases()
    {
        _sut.Add(Make("h1"));
        Assert.AreEqual(1, _sut.Count);
        Assert.IsTrue(_sut.Contains("h1"));
    }

    [TestMethod]
    public void Add_DuplicateHeroId_ReplacesExisting()
    {
        _sut.Add(Make("h1", dispatchDays: 1.0));
        _sut.Add(Make("h1", dispatchDays: 2.0));

        Assert.AreEqual(1, _sut.Count);
        Assert.AreEqual(2.0, _sut.Get("h1").DispatchTimeDays, 0.0001);
    }

    [TestMethod]
    public void Add_NullMessenger_NoOp()
    {
        _sut.Add(null);
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Add_EmptyHeroId_NoOp()
    {
        _sut.Add(new PendingMessenger("", 0.0, MapCoord.Zero, false));
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Remove_ExistingHero_ReturnsTrueAndRemoves()
    {
        _sut.Add(Make("h1"));

        var removed = _sut.Remove("h1");

        Assert.IsTrue(removed);
        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Remove_NonexistentHero_ReturnsFalse()
    {
        var removed = _sut.Remove("nope");
        Assert.IsFalse(removed);
    }

    [TestMethod]
    public void Get_ExistingHero_ReturnsMessenger()
    {
        var m = Make("h1");
        _sut.Add(m);

        var fetched = _sut.Get("h1");

        Assert.AreSame(m, fetched);
    }

    [TestMethod]
    public void Get_NonexistentHero_ReturnsNull()
    {
        Assert.IsNull(_sut.Get("nope"));
    }

    [TestMethod]
    public void Contains_EmptyId_ReturnsFalse()
    {
        Assert.IsFalse(_sut.Contains(""));
        Assert.IsFalse(_sut.Contains(null));
    }

    [TestMethod]
    public void GetAll_MultipleEntries_ReturnsAll()
    {
        _sut.Add(Make("h1"));
        _sut.Add(Make("h2"));
        _sut.Add(Make("h3"));

        var all = _sut.GetAll();

        Assert.AreEqual(3, all.Count);
    }

    [TestMethod]
    public void Clear_RemovesAll()
    {
        _sut.Add(Make("h1"));
        _sut.Add(Make("h2"));

        _sut.Clear();

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Serialize_Deserialize_RoundtripPreservesAllFields()
    {
        var original = Make("h1", dispatchDays: 12.345, x: 7.5f, y: 11.25f, arrived: true);
        _sut.Add(original);
        _sut.Add(Make("h2", dispatchDays: 0.99, x: -3.5f, y: 0.5f, arrived: false));

        var serialized = _sut.Serialize();

        var restored = new MessengerStateStore(_logger);
        restored.Deserialize(serialized);

        Assert.AreEqual(2, restored.Count);
        var roundtrip = restored.Get("h1");
        Assert.AreEqual(12.345, roundtrip.DispatchTimeDays, 0.0001);
        Assert.AreEqual(7.5f, roundtrip.Position.X, 0.0001f);
        Assert.AreEqual(11.25f, roundtrip.Position.Y, 0.0001f);
        Assert.IsTrue(roundtrip.Arrived);
    }

    [TestMethod]
    public void Deserialize_NullData_ClearsExisting()
    {
        _sut.Add(Make("h1"));

        _sut.Deserialize(null);

        Assert.AreEqual(0, _sut.Count);
    }

    [TestMethod]
    public void Deserialize_NaNDispatchDays_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "h_nan", "NaN|1.5|2.5|0" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(0, _sut.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("h_nan")));
    }

    [TestMethod]
    public void Deserialize_InfinityPosition_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "h_inf", "1.5|Infinity|2.5|0" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(0, _sut.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("h_inf")));
    }

    [TestMethod]
    public void Deserialize_NegativeInfinityPosition_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "h_neginf", "1.5|1.5|-Infinity|0" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(0, _sut.Count);
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("h_neginf")));
    }

    [TestMethod]
    public void Deserialize_MalformedEntry_DropsAndLogs()
    {
        var data = new Dictionary<string, string>
        {
            { "h_good", "1.5|1.5|2.5|0" },
            { "h_bad", "garbage" },
        };

        _sut.Deserialize(data);

        Assert.AreEqual(1, _sut.Count);
        Assert.IsTrue(_sut.Contains("h_good"));
        Assert.IsFalse(_sut.Contains("h_bad"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("h_bad")));
    }

    [TestMethod]
    public void Deserialize_ReplacesExistingState()
    {
        _sut.Add(Make("old"));

        var data = new Dictionary<string, string> { { "h_new", "1.5|1.5|2.5|0" } };
        _sut.Deserialize(data);

        Assert.IsFalse(_sut.Contains("old"));
        Assert.IsTrue(_sut.Contains("h_new"));
    }
}
