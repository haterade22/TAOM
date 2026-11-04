using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

[TestClass]
public class LongDefeatBestRunStoreTests
{
    private string _dir = null!;

    [TestInitialize]
    public void Init() => _dir = Path.Combine(Path.GetTempPath(), "taom_ld_best_" + Guid.NewGuid().ToString("N"));

    [TestCleanup]
    public void Clean()
    {
        if (Directory.Exists(_dir))
            Directory.Delete(_dir, true);
    }

    private LongDefeatBestRunStore Store() => new(_dir);

    [TestMethod]
    public void Load_WithNoFile_IsNull() => Assert.IsNull(Store().Load("helms"));

    [TestMethod]
    public void Save_ThenLoad_RoundTripsPerFortress()
    {
        var store = Store();

        Assert.IsTrue(store.TrySave("helms", new BestRun(4, 321), out var error), error);
        Assert.IsTrue(store.TrySave("minas", new BestRun(1, 9), out error), error);

        Assert.AreEqual(new BestRun(4, 321), store.Load("helms"));
        Assert.AreEqual(new BestRun(1, 9), store.Load("minas"));
        Assert.IsNull(store.Load("other"));
    }

    [TestMethod]
    public void Save_ReplacesOneRow_AndKeepsAnotherFortressesRowAndLeavesNoTempFile()
    {
        var store = Store();
        store.TrySave("helms", new BestRun(2, 50), out _);
        store.TrySave("minas", new BestRun(7, 700), out _);

        store.TrySave("helms", new BestRun(3, 80), out _);

        Assert.AreEqual(new BestRun(3, 80), store.Load("helms"));
        Assert.AreEqual(new BestRun(7, 700), store.Load("minas"));
        Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
    }

    [TestMethod]
    public void Save_WhenTheTempWriteFails_ReturnsFalse_AndLeavesTheOldFileReadable()
    {
        var store = Store();
        store.TrySave("helms", new BestRun(2, 50), out _);
        Directory.CreateDirectory(store.FilePath + ".tmp");

        Assert.IsFalse(store.TrySave("helms", new BestRun(9, 900), out var error));

        Assert.IsFalse(string.IsNullOrEmpty(error));
        Assert.AreEqual(new BestRun(2, 50), store.Load("helms"));
    }

    [TestMethod]
    public void Load_WithBadJson_IsNull_AndNeverThrows()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, LongDefeatBestRunStore.FileName), "{ not json");

        Assert.IsNull(Store().Load("helms"));
    }

    [TestMethod]
    public void Save_OverBadJson_ReplacesItWithAReadableFile()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, LongDefeatBestRunStore.FileName), "][");
        var store = Store();

        Assert.IsTrue(store.TrySave("helms", new BestRun(1, 2), out var error), error);

        Assert.AreEqual(new BestRun(1, 2), store.Load("helms"));
    }

    [DataTestMethod]
    [DataRow("{\"helms\":{\"waves\":NaN,\"kills\":5}}")]
    [DataRow("{\"helms\":{\"waves\":3,\"kills\":Infinity}}")]
    [DataRow("{\"helms\":{\"waves\":3.5,\"kills\":5}}")]
    [DataRow("{\"helms\":{\"waves\":-1,\"kills\":5}}")]
    [DataRow("{\"helms\":{\"waves\":\"3\",\"kills\":5}}")]
    [DataRow("{\"helms\":{\"waves\":3}}")]
    [DataRow("{\"helms\":{\"waves\":99999999999,\"kills\":5}}")]
    [DataRow("{\"helms\":7}")]
    [DataRow("[1,2]")]
    public void Load_RejectsARowThatIsNotWholeNonNegativeNumbers(string json)
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, LongDefeatBestRunStore.FileName), json);

        Assert.IsNull(Store().Load("helms"));
    }

    [TestMethod]
    public void Save_WhenTheFolderCannotBeMade_ReturnsFalseWithTheError()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "blocker");
        File.WriteAllText(blocker, "x");
        var store = new LongDefeatBestRunStore(Path.Combine(blocker, "inside"));

        Assert.IsFalse(store.TrySave("helms", new BestRun(1, 1), out var error));
        Assert.IsFalse(string.IsNullOrEmpty(error));
    }

    [TestMethod]
    public void AnArmedFolder_NeverTouchesTheDefaultOne()
    {
        var armed = Store();
        var other = new LongDefeatBestRunStore(Path.Combine(_dir, "default"));
        other.TrySave("helms", new BestRun(9, 900), out _);

        armed.TrySave("helms", new BestRun(1, 1), out _);

        Assert.AreEqual(new BestRun(9, 900), other.Load("helms"));
        Assert.AreEqual(new BestRun(1, 1), armed.Load("helms"));
    }
}
