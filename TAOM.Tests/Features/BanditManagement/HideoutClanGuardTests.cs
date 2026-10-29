using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.BanditManagement;

namespace TAOM.Tests.Features.BanditManagement;

/// <summary>
/// The two decisions behind Patch125 (#1063): a bandit clan whose culture owns no hideout, and a hideout whose
/// culture has no bandit clan. Strings stand in for the engine's CultureObject keys; the guard compares by the
/// dictionary's own comparer and by reference, exactly as vanilla does.
/// </summary>
[TestClass]
public class HideoutClanGuardTests
{
    [TestMethod]
    public void HasHideoutList_CultureWithHideouts_ReturnsTrue()
    {
        var hideouts = new Dictionary<string, List<string>> { ["gundabad_raiders"] = new List<string> { "hideout_mountain_1" } };
        Assert.IsTrue(HideoutClanGuard.HasHideoutList(hideouts, "gundabad_raiders"));
    }

    [TestMethod]
    public void HasHideoutList_CultureWithoutHideouts_ReturnsFalse()
    {
        // TAOM with an older TAOM_Map: the feral clan exists, its dens do not, so vanilla's indexer would throw.
        var hideouts = new Dictionary<string, List<string>> { ["gundabad_raiders"] = new List<string> { "hideout_mountain_1" } };
        Assert.IsFalse(HideoutClanGuard.HasHideoutList(hideouts, "feral_orcs"));
    }

    [TestMethod]
    public void HasHideoutList_NullCultureOrDictionary_ReturnsFalse()
    {
        var hideouts = new Dictionary<string, List<string>>();
        Assert.IsFalse(HideoutClanGuard.HasHideoutList(hideouts, null));
        Assert.IsFalse(HideoutClanGuard.HasHideoutList<string, List<string>>(null, "feral_orcs"));
    }

    [TestMethod]
    public void HasClanOfCulture_MatchingClanCulture_ReturnsTrue()
    {
        var feral = new object();
        Assert.IsTrue(HideoutClanGuard.HasClanOfCulture(new[] { new object(), feral }, feral));
    }

    [TestMethod]
    public void HasClanOfCulture_NoClanOfThatCulture_ReturnsFalse()
    {
        // A save made before the Feral Orcs: their dens load from XML, their clan does not.
        Assert.IsFalse(HideoutClanGuard.HasClanOfCulture(new[] { new object(), new object() }, new object()));
    }

    [TestMethod]
    public void HasClanOfCulture_NullInputs_ReturnsFalse()
    {
        Assert.IsFalse(HideoutClanGuard.HasClanOfCulture<object>(null, new object()));
        Assert.IsFalse(HideoutClanGuard.HasClanOfCulture(new[] { new object() }, null));
    }
}
