using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
public class FactionPowerStoreTests
{
    private FakeAdvancedStartData _data = null!;
    private IModLogger _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _data = new FakeAdvancedStartData();
        _logger = Substitute.For<IModLogger>();
    }

    private FactionPowerStore Create() =>
        new FactionPowerStore(_data, new FakeFactionPowerCatalog("mordor", "gondor", "rohan"), _logger);

    [TestMethod]
    public void NoKeys_GivesOneForEveryCulture_AnOlderSave()
    {
        var store = Create();

        Assert.AreEqual(1f, store.Multiplier("mordor"));
        Assert.AreEqual(1f, store.Multiplier("gondor"));
        Assert.AreEqual(0, store.Snapshot().Count);
    }

    [TestMethod]
    public void AStoredPercent_IsReadAsAMultiplier()
    {
        _data.Values["taom_fp_mordor"] = 150;

        var store = Create();

        Assert.AreEqual(1.5f, store.Multiplier("mordor"));
        Assert.AreEqual(150, store.Percent("mordor"));
        Assert.AreEqual(1f, store.Multiplier("gondor"));
    }

    [TestMethod]
    public void AStoredHundred_IsExactlyOne_AndNotInTheSnapshot()
    {
        _data.Values["taom_fp_mordor"] = 100;

        var store = Create();

        Assert.AreEqual(1f, store.Multiplier("mordor"));
        Assert.AreEqual(0, store.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow(49)]
    [DataRow(201)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    [DataRow(0)]
    public void AnOutOfRangePercent_IsIgnoredWithAWarning_NeverClamped(int stored)
    {
        _data.Values["taom_fp_mordor"] = stored;

        var store = Create();

        Assert.AreEqual(1f, store.Multiplier("mordor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("mordor")));
    }

    [DataTestMethod]
    [DataRow(50)]
    [DataRow(200)]
    public void TheBounds_AreAccepted(int stored)
    {
        _data.Values["taom_fp_mordor"] = stored;

        Assert.AreEqual(stored / 100f, Create().Multiplier("mordor"));
    }

    [TestMethod]
    public void AValueOfTheWrongType_IsIgnored()
    {
        _data.Values["taom_fp_mordor"] = "150";

        Assert.AreEqual(1f, Create().Multiplier("mordor"));
    }

    [TestMethod]
    public void AnUnknownCultureKey_IsNeverRead()
    {
        _data.Values["taom_fp_atlantis"] = 150;

        var store = Create();

        Assert.AreEqual(1f, store.Multiplier("atlantis"));
        Assert.AreEqual(0, store.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void ANullOrEmptyKey_GivesOne(string? key)
    {
        _data.Values["taom_fp_mordor"] = 150;

        Assert.AreEqual(1f, Create().Multiplier(key));
    }

    [TestMethod]
    public void ANewerVersion_StillReadsTheKeysItKnows_WithAWarning()
    {
        _data.Values["taom_fp_version"] = 99;
        _data.Values["taom_fp_mordor"] = 150;

        var store = Create();

        Assert.AreEqual(1.5f, store.Multiplier("mordor"));
        _logger.Received().LogWarning(Arg.Is<string>(s => s.Contains("99")));
    }

    [TestMethod]
    public void ReloadWithNoCampaign_Empties()
    {
        _data.Values["taom_fp_mordor"] = 150;
        var store = Create();
        Assert.AreEqual(1.5f, store.Multiplier("mordor"));

        _data.IsAvailable = false;
        store.Reload();

        Assert.AreEqual(1f, store.Multiplier("mordor"));
        Assert.AreEqual(0, store.Snapshot().Count);
    }

    [TestMethod]
    public void ASecondCampaignInOneProcess_StartsEmpty()
    {
        _data.Values["taom_fp_mordor"] = 150;
        var store = Create();
        Assert.AreEqual(1.5f, store.Multiplier("mordor"));

        _data.Values.Clear();
        store.Reload();

        Assert.AreEqual(1f, store.Multiplier("mordor"));
    }

    [TestMethod]
    public void AReadBeforeTheCampaignExists_IsNotRemembered()
    {
        _data.IsAvailable = false;
        var store = Create();
        Assert.AreEqual(1f, store.Multiplier("mordor"));

        _data.IsAvailable = true;
        _data.Values["taom_fp_mordor"] = 150;

        Assert.AreEqual(1.5f, store.Multiplier("mordor"));
    }

    [TestMethod]
    public void TrySet_OnACampaignThatNeverPinned_Writes_AndStampsTheVersion()
    {
        var store = Create();

        Assert.IsTrue(store.TrySet("mordor", 150));

        Assert.AreEqual(150, _data.Values["taom_fp_mordor"]);
        Assert.AreEqual(1, _data.Values["taom_fp_version"]);
        Assert.AreEqual(1.5f, store.Multiplier("mordor"));
        Assert.IsTrue(_data.Categories.TrueForAll(c => c == "taom_faction_power"));
    }

    [TestMethod]
    public void TrySet_OverwritesAnExistingKey()
    {
        _data.Values["taom_fp_version"] = 1;
        _data.Values["taom_fp_mordor"] = 150;
        var store = Create();

        Assert.IsTrue(store.TrySet("mordor", 75));

        Assert.AreEqual(75, _data.Values["taom_fp_mordor"]);
        Assert.AreEqual(0.75f, store.Multiplier("mordor"));
    }

    [TestMethod]
    public void TrySet_BackTo100_ReadsAsOne()
    {
        var store = Create();
        store.TrySet("mordor", 150);

        Assert.IsTrue(store.TrySet("mordor", 100));

        Assert.AreEqual(1f, store.Multiplier("mordor"));
        Assert.AreEqual(0, store.Snapshot().Count);
    }

    [DataTestMethod]
    [DataRow("atlantis", 150)]
    [DataRow("", 150)]
    [DataRow("mordor", 49)]
    [DataRow("mordor", 201)]
    public void TrySet_RefusesAnUnknownCultureOrABadPercent_AndWritesNothing(string culture, int percent)
    {
        var store = Create();

        Assert.IsFalse(store.TrySet(culture, percent));

        Assert.AreEqual(0, _data.Values.Count);
    }

    [TestMethod]
    public void TrySet_WithNoCampaign_ReturnsFalse()
    {
        _data.IsAvailable = false;

        Assert.IsFalse(Create().TrySet("mordor", 150));
    }

    [TestMethod]
    public void Reload_LogsOneLoadedLineListingTheNon100Table()
    {
        _data.Values["taom_fp_mordor"] = 150;
        _data.Values["taom_fp_gondor"] = 75;

        Create().Reload();

        _logger.Received(1).LogInfo(Arg.Is<string>(s => s.StartsWith("FactionPower.Loaded") && s.Contains("mordor=150") && s.Contains("gondor=75")));
    }

    [TestMethod]
    public void TrySet_DoesNotLogALoadedLine()
    {
        Create().TrySet("mordor", 150);

        _logger.DidNotReceive().LogInfo(Arg.Is<string>(s => s.StartsWith("FactionPower.Loaded")));
    }
}
