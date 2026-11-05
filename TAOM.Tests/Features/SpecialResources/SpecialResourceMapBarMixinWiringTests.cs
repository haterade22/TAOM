using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static TAOM.Tests.Infrastructure.RepoPaths;

namespace TAOM.Tests.Features.SpecialResources;

/// <summary>
/// The resource entry on the map bar's secondary row. <c>MapBarVM.Initialize</c> runs the first
/// <c>MapInfoVM.Refresh</c> itself and then waits 2 s for the next one, so a mixin that skips its first refresh
/// shows the entry 2 s late on every load (#958).
/// </summary>
[TestClass]
public class SpecialResourceMapBarMixinWiringTests
{
    private const string Insert = "items.Add(_resourceInfo)";

    private static string RefreshCore()
    {
        var text = ReadSource("Main/Features/SpecialResources/UI/SpecialResourceMapBarMixin.cs", stripComments: true);
        var start = text.IndexOf("private void OnRefreshCore()", System.StringComparison.Ordinal);
        Assert.IsTrue(start >= 0, "OnRefreshCore not found");
        var end = text.IndexOf("private List<TooltipProperty> GetTooltipProperties()", start, System.StringComparison.Ordinal);
        Assert.IsTrue(end > start, "GetTooltipProperties not found after OnRefreshCore");
        return text.Substring(start, end - start);
    }

    [TestMethod]
    public void TheEntry_IsInsertedOnce_GuardedByContains()
    {
        var body = RefreshCore();

        StringAssert.Contains(body, Insert, "the insert is code, not a comment");
        StringAssert.Contains(body, "!items.Contains(_resourceInfo)", "Contains keeps one entry and re-adds it after a rebuild");
    }

    [TestMethod]
    public void NothingBeforeTheInsert_SkipsTheFirstRefresh()
    {
        var body = RefreshCore();
        var insert = body.IndexOf(Insert, System.StringComparison.Ordinal);
        Assert.IsTrue(insert >= 0);

        // Exactly the three guards may return before the insert: no campaign, no hero, no resource.
        var returns = Regex.Matches(body.Substring(0, insert), @"\breturn\s*;").Count;
        Assert.AreEqual(3, returns, "a new early return before the insert can bring back the first-refresh skip");
        Assert.IsFalse(Regex.IsMatch(body, @"\b_(baseInitialized|itemAdded)\b"), "the old one-shot flags stay deleted");
    }

    [TestMethod]
    public void TheNoResourceCheck_ComesBeforeTheInsert()
    {
        var body = RefreshCore();
        var check = body.IndexOf("if (resource == null)", System.StringComparison.Ordinal);

        Assert.IsTrue(check >= 0, "the no-resource guard exists");
        Assert.IsTrue(check < body.IndexOf(Insert, System.StringComparison.Ordinal),
            "a hero whose realm has no resource must get no entry");
    }

    [TestMethod]
    public void TheNoResourceBranch_RemovesTheEntry()
    {
        // #977: a hero who loses the resource mid-session kept a stale entry until the bar was rebuilt.
        var body = RefreshCore();
        var check = body.IndexOf("if (resource == null)", System.StringComparison.Ordinal);
        Assert.IsTrue(check >= 0);
        var branch = body.Substring(check, body.IndexOf("return;", check, System.StringComparison.Ordinal) - check);

        StringAssert.Contains(branch, "items?.Remove(_resourceInfo)", "the entry leaves the bar when there is no resource");
    }
}
