using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.FactionPower;

namespace TAOM.Tests.Features.FactionPower;

[TestClass]
public class FactionPowerPresetsTests
{
    private static readonly IReadOnlyList<FactionPowerEntry> Catalog = new[]
    {
        new FactionPowerEntry("mordor", "Mordor", "evil"),
        new FactionPowerEntry("isengard", "Isengard", "evil"),
        new FactionPowerEntry("gondor", "Gondor", "free"),
        new FactionPowerEntry("rohan", "Rohan", "free"),
        new FactionPowerEntry("khand", "Khand", "neutral"),
    };

    private static Dictionary<string, int> Resolve(string id, params string[] present)
        => FactionPowerPresets.Resolve(id, Catalog, present)!.ToDictionary(p => p.Key, p => p.Value);

    [TestMethod]
    public void Standard_PinsNothing()
    {
        Assert.AreEqual(0, Resolve(FactionPowerPresets.Standard).Count);
    }

    [TestMethod]
    public void EvilPlus50_WritesExactlyTheEvilCulturesAt150()
    {
        var values = Resolve(FactionPowerPresets.EvilPlus50);

        CollectionAssert.AreEquivalent(new[] { "mordor", "isengard" }, values.Keys.ToArray());
        Assert.IsTrue(values.Values.All(v => v == 150));
    }

    [TestMethod]
    public void FreePlus50_WritesExactlyTheFreeCulturesAt150()
    {
        var values = Resolve(FactionPowerPresets.FreePlus50);

        CollectionAssert.AreEquivalent(new[] { "gondor", "rohan" }, values.Keys.ToArray());
        Assert.IsTrue(values.Values.All(v => v == 150));
    }

    [TestMethod]
    public void NeutralCultures_AreNeverTouchedByASidePreset()
    {
        Assert.IsFalse(Resolve(FactionPowerPresets.EvilPlus50).ContainsKey("khand"));
        Assert.IsFalse(Resolve(FactionPowerPresets.FreePlus50).ContainsKey("khand"));
    }

    [TestMethod]
    public void ResetAll_Writes100ToEveryPresentKey_AndNothingElse()
    {
        var values = Resolve(FactionPowerPresets.ResetAll, "mordor", "gondor");

        CollectionAssert.AreEquivalent(new[] { "mordor", "gondor" }, values.Keys.ToArray());
        Assert.IsTrue(values.Values.All(v => v == 100));
    }

    [TestMethod]
    public void ResetAll_WithNoKeysPresent_WritesNothing()
    {
        Assert.AreEqual(0, Resolve(FactionPowerPresets.ResetAll).Count);
    }

    [TestMethod]
    public void AnUnknownPreset_ResolvesToNull()
    {
        Assert.IsNull(FactionPowerPresets.Resolve("evil500", Catalog, new string[0]));
        Assert.IsNull(FactionPowerPresets.Resolve(null, Catalog, new string[0]));
    }

    [TestMethod]
    public void ThePresetId_IsCaseInsensitive()
    {
        Assert.IsNotNull(FactionPowerPresets.Resolve("EVIL50", Catalog, new string[0]));
    }
}
