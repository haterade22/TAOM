using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.NazgulFamily;

namespace TAOM.Tests.Features.NazgulFamily;

[TestClass]
public class NazgulRegistryTests
{
    private NazgulRegistry _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new NazgulRegistry();

    [DataTestMethod]
    [DataRow("lord_1_15")]   // Witch-King of Angmar
    [DataRow("lord_1_155")]  // The Dark Marshall
    [DataRow("lord_1_16")]   // The Knight of Umbar
    [DataRow("lord_1_28")]   // The Betrayer
    [DataRow("lord_1_38")]   // the Undying
    [DataRow("lord_1_48")]   // Khamûl the Easterling — second of the Nine
    [DataRow("lord_1_48_1")] // the Tainted
    [DataRow("lord_1_48_2")] // the Shadow of Northmen
    [DataRow("lord_1_48_3")] // the Shadow of Umbar
    public void IsWraith_WraithId_ReturnsTrue(string id)
        => Assert.IsTrue(_sut.IsWraith(id));

    [DataTestMethod]
    [DataRow("lord_1_17")]  // Sauron — a Dark Lord, but not a Ringwraith (out of scope)
    [DataRow("lord_1_75")]  // Boromir
    [DataRow("lord_L1_1")]  // Galadriel
    [DataRow("lord_1_1")]   // a Dunland lord
    [DataRow("lord_1_4")]   // genuine prefix of lord_1_48 — must NOT match (exact-set, not prefix)
    [DataRow("lord_1_485")] // genuine superstring of lord_1_48 — must NOT match
    [DataRow("lord_1_1_5")]
    public void IsWraith_NonWraithId_ReturnsFalse(string id)
        => Assert.IsFalse(_sut.IsWraith(id));

    [TestMethod]
    public void IsWraith_Null_ReturnsFalse() => Assert.IsFalse(_sut.IsWraith(null!));

    [TestMethod]
    public void IsWraith_Empty_ReturnsFalse() => Assert.IsFalse(_sut.IsWraith(""));

    [TestMethod]
    public void IsWraith_CaseInsensitive_ReturnsTrue() => Assert.IsTrue(_sut.IsWraith("LORD_1_15"));
}
