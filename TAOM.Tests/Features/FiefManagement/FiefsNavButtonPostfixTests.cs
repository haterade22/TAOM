using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Features.FiefManagement.Hooks;
using TAOM.Features.FiefManagement.UI;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FiefManagement;

/// <summary>
/// The postfix on <c>MapNavigationHandler.OnCreateElements</c> runs inside the handler's constructor,
/// so it holds no handler state; its whole decision is "append our element once, at the end, and never
/// throw". That decision is a pure function over <see cref="INavigationElement"/> and is tested here
/// with fakes (the real element needs a live Campaign to construct).
/// </summary>
[TestClass]
public class FiefsNavButtonPostfixTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static INavigationElement Fake(string id)
    {
        var element = Substitute.For<INavigationElement>();
        element.StringId.Returns(id);
        return element;
    }

    private static INavigationElement[] Vanilla() => new[]
    {
        Fake("escape_menu"), Fake("character_developer"), Fake("inventory"), Fake("party"),
        Fake("quest"), Fake("clan"), Fake("kingdom"),
    };

    [TestMethod]
    public void AppendOnce_VanillaSeven_AppendsExactlyOneAtTheEnd()
    {
        var vanilla = Vanilla();
        var ours = Fake(TaomFiefsNavigationElement.ElementId);

        var result = Patch36_MapNavigationElements.AppendOnce(vanilla, () => ours);

        Assert.AreEqual(8, result.Length);
        Assert.AreSame(ours, result[7]);
        CollectionAssert.AreEqual(vanilla, result.Take(7).ToArray(), "vanilla order must be untouched");
    }

    [TestMethod]
    public void AppendOnce_CalledTwiceOnTheResult_AddsNothingTheSecondTime()
    {
        var made = 0;
        INavigationElement Make() { made++; return Fake(TaomFiefsNavigationElement.ElementId); }

        var once = Patch36_MapNavigationElements.AppendOnce(Vanilla(), Make);
        var twice = Patch36_MapNavigationElements.AppendOnce(once, Make);

        Assert.AreEqual(8, twice.Length);
        Assert.AreEqual(1, twice.Count(e => e.StringId == TaomFiefsNavigationElement.ElementId));
        Assert.AreEqual(1, made, "the factory must not run when the element is already present");
        Assert.AreSame(once, twice);
    }

    [TestMethod]
    public void AppendOnce_AfterAnotherModInsertsItsOwn_StillAppendsOurs()
    {
        // NavalDLC inserts manage_fleet at index 3 AFTER the base call returns; a third-party element
        // in the array must not stop ours from being appended.
        var withForeign = Vanilla().Concat(new[] { Fake("some_other_mod") }).ToArray();
        var ours = Fake(TaomFiefsNavigationElement.ElementId);

        var result = Patch36_MapNavigationElements.AppendOnce(withForeign, () => ours);

        Assert.AreEqual(9, result.Length);
        Assert.AreSame(ours, result[8]);
    }

    [TestMethod]
    public void AppendOnce_NullArray_ReturnsNullWithoutBuilding()
    {
        var built = false;

        var result = Patch36_MapNavigationElements.AppendOnce(null!, () => { built = true; return Fake("x"); });

        Assert.IsNull(result);
        Assert.IsFalse(built);
    }

    [TestMethod]
    public void AppendOnce_FactoryReturnsNull_KeepsTheOriginalArray()
    {
        var vanilla = Vanilla();

        var result = Patch36_MapNavigationElements.AppendOnce(vanilla, () => null!);

        Assert.AreSame(vanilla, result);
    }

    [TestMethod]
    public void Postfix_WhenTheElementCannotBeBuilt_LeavesVanillasArrayAndDoesNotThrow()
    {
        // No Campaign exists in the test host, so building the real element throws (MapNavigationElementBase's
        // constructor reads Campaign.Current). The postfix runs inside the handler constructor: that throw must
        // not escape, and __result must stay the very array vanilla built.
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
        var vanilla = Vanilla();
        var postfix = typeof(Patch36_MapNavigationElements).GetMethod("Postfix", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(postfix, "Patch36_MapNavigationElements.Postfix is gone.");
        var args = new object?[] { null, vanilla };

        postfix!.Invoke(null, args);

        Assert.AreSame(vanilla, args[1], "a failed build must leave __result as vanilla's array");
        Assert.AreEqual(7, vanilla.Length);
    }

    [TestMethod]
    public void AppendOnce_DoesNotMutateTheInputArray()
    {
        var vanilla = Vanilla();

        Patch36_MapNavigationElements.AppendOnce(vanilla, () => Fake(TaomFiefsNavigationElement.ElementId));

        Assert.AreEqual(7, vanilla.Length);
    }
}
