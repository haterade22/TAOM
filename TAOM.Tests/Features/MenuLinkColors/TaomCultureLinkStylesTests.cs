using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.MenuLinkColors;

namespace TAOM.Tests.Features.MenuLinkColors;

[TestClass]
public class TaomCultureLinkStylesTests
{
    [TestMethod]
    public void StyleNameFor_KnownCulture_ReturnsTaomPrefixedName()
    {
        Assert.AreEqual("Link.Taom.mordor", TaomCultureLinkStyles.StyleNameFor("mordor"));
        Assert.AreEqual("Link.Taom.gondor", TaomCultureLinkStyles.StyleNameFor("gondor"));
    }

    [TestMethod]
    public void IsSupported_MainCulture_ReturnsTrue()
    {
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("mordor"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("rivendell"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("mistymountainorcs"));
    }

    [TestMethod]
    public void IsSupported_VanillaRemappedCulture_ReturnsTrue()
    {
        // TAOM reuses these six vanilla culture ids for Dunlendings, Haradrim, Rohirrim,
        // Easterlings, Barding and Variag respectively — they own real settlements.
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("empire"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("aserai"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("vlandia"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("khuzait"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("sturgia"));
        Assert.IsTrue(TaomCultureLinkStyles.IsSupported("battania"));
    }

    [TestMethod]
    public void IsSupported_BanditCulture_ReturnsFalse()
    {
        // Bandit cultures deliberately keep vanilla's link colors — they rarely own a settlement.
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("dunland_raiders"));
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("umbar_corsairs"));
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("mirkwood_stalkers"));
    }

    [TestMethod]
    public void IsSupported_NullOrEmptyOrUnknown_ReturnsFalse()
    {
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported(null));
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported(""));
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("   "));
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("not_a_culture"));
    }

    [TestMethod]
    public void IsSupported_IsCaseSensitive_MatchingCultureStringIds()
    {
        // Culture StringIds are lowercase in taom_spcultures.xml; an uppercase variant is a typo,
        // not a match — matching it would silently emit a style name the brush XML never defines.
        Assert.IsFalse(TaomCultureLinkStyles.IsSupported("Mordor"));
    }

    [TestMethod]
    public void SupportedCultureIds_ContainsTwentyDistinctIds()
    {
        var ids = TaomCultureLinkStyles.SupportedCultureIds.ToList();

        Assert.AreEqual(20, ids.Count);
        Assert.AreEqual(ids.Count, ids.Distinct().Count(), "Duplicate culture id in the supported set.");
        CollectionAssert.AllItemsAreNotNull(ids);
    }
}
