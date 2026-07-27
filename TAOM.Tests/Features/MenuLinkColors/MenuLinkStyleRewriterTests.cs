using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.MenuLinkColors;

namespace TAOM.Tests.Features.MenuLinkColors;

[TestClass]
public class MenuLinkStyleRewriterTests
{
    private IEncyclopediaCultureLookup _lookup = null!;
    private IMenuBrushStyleProbe _brush = null!;
    private IModLogger _logger = null!;
    private MenuLinkStyleRewriter _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _lookup = Substitute.For<IEncyclopediaCultureLookup>();
        _brush = Substitute.For<IMenuBrushStyleProbe>();
        _brush.DefinesStyle(Arg.Any<string>()).Returns(true);
        _logger = Substitute.For<IModLogger>();
        _sut = new MenuLinkStyleRewriter(_lookup, _brush, _logger);
    }

    /// <summary>Reproduces the exact markup shape emitted by TaleWorlds.Core.HyperlinkTexts.</summary>
    private static string Link(string style, string href, string name) =>
        $"<a style=\"{style}\" href=\"event:{href}\"><b>{name}</b></a>";

    [TestMethod]
    public void Rewrite_SettlementLink_SupportedCulture_SwapsStyleName()
    {
        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");

        var result = _sut.Rewrite(Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul"));

        Assert.AreEqual(Link("Link.Taom.mordor", "Settlement-town_MM1", "Minas Morgul"), result);
    }

    [TestMethod]
    public void Rewrite_HeroLink_SupportedCulture_SwapsStyleName()
    {
        _lookup.GetCultureId("Hero-lord_witchking").Returns("mordor");

        var result = _sut.Rewrite(Link("Link.Hero", "Hero-lord_witchking", "Witch-King of Angmar"));

        Assert.AreEqual(Link("Link.Taom.mordor", "Hero-lord_witchking", "Witch-King of Angmar"), result);
    }

    [TestMethod]
    public void Rewrite_KingdomLink_SupportedCulture_SwapsStyleName()
    {
        _lookup.GetCultureId("Kingdom-mordor").Returns("mordor");

        var result = _sut.Rewrite(Link("Link.Kingdom", "Kingdom-mordor", "Mordor"));

        Assert.AreEqual(Link("Link.Taom.mordor", "Kingdom-mordor", "Mordor"), result);
    }

    [TestMethod]
    public void Rewrite_ClanLink_UsesFactionPrefix_SwapsStyleName()
    {
        // Clan is registered in the object manager under element name "Faction", not "Clan"
        // (Campaign.cs RegisterType<Clan>("Faction", ...)), so the href prefix differs from the type.
        _lookup.GetCultureId("Faction-clan_nazgul").Returns("mordor");

        var result = _sut.Rewrite(Link("Link.Clan", "Faction-clan_nazgul", "Nazgûl"));

        Assert.AreEqual(Link("Link.Taom.mordor", "Faction-clan_nazgul", "Nazgûl"), result);
    }

    [TestMethod]
    public void Rewrite_MixedCultures_EachLinkUsesItsOwnObjectsCulture()
    {
        // A Gondorian lord holding a captured Mordor town: three links, two colors.
        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");
        _lookup.GetCultureId("Hero-lord_captain").Returns("gondor");
        _lookup.GetCultureId("Kingdom-gondor").Returns("gondor");

        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul")
                   + " is governed by "
                   + Link("Link.Hero", "Hero-lord_captain", "Some Captain")
                   + ", a captain of "
                   + Link("Link.Kingdom", "Kingdom-gondor", "Gondor")
                   + ".";

        var result = _sut.Rewrite(text);

        var expected = Link("Link.Taom.mordor", "Settlement-town_MM1", "Minas Morgul")
                       + " is governed by "
                       + Link("Link.Taom.gondor", "Hero-lord_captain", "Some Captain")
                       + ", a captain of "
                       + Link("Link.Taom.gondor", "Kingdom-gondor", "Gondor")
                       + ".";
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Rewrite_UnsupportedCulture_LeavesStyleUnchanged()
    {
        // Bandit cultures have no authored brush style; emitting one would render them as plain
        // body text because Brush.GetStyleOrDefault silently falls back to Default.
        _lookup.GetCultureId("Settlement-hideout_1").Returns("dunland_raiders");
        var text = Link("Link.Settlement", "Settlement-hideout_1", "Raider Camp");

        Assert.AreEqual(text, _sut.Rewrite(text));
    }

    [TestMethod]
    public void Rewrite_UnresolvableLink_LeavesStyleUnchanged()
    {
        _lookup.GetCultureId(Arg.Any<string>()).Returns((string)null);
        var text = Link("Link.Settlement", "Settlement-does_not_exist", "Nowhere");

        Assert.AreEqual(text, _sut.Rewrite(text));
    }

    [TestMethod]
    public void Rewrite_LookupThrows_ReturnsOriginalTextUnchanged()
    {
        _lookup.GetCultureId(Arg.Any<string>()).Throws(new InvalidOperationException("engine not ready"));
        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul");

        Assert.AreEqual(text, _sut.Rewrite(text));
    }

    [TestMethod]
    public void Rewrite_LookupThrowsRepeatedly_WarnsOnceNotEveryMenuRefresh()
    {
        _lookup.GetCultureId(Arg.Any<string>()).Throws(new InvalidOperationException("engine not ready"));
        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul");

        _sut.Rewrite(text);
        _sut.Rewrite(text + " ");
        _sut.Rewrite(text + "  ");

        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_TextWithoutLinks_ReturnsSameInstance()
    {
        const string text = "The town is prosperous. The people look well-fed.";

        Assert.AreSame(text, _sut.Rewrite(text));
        _lookup.DidNotReceive().GetCultureId(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_NullOrEmpty_ReturnsInput()
    {
        Assert.IsNull(_sut.Rewrite(null));
        Assert.AreEqual(string.Empty, _sut.Rewrite(string.Empty));
    }

    [TestMethod]
    public void Rewrite_MalformedMarkup_DoesNotThrowAndLeavesTextUnchanged()
    {
        var cases = new[]
        {
            "<a style=\"Link.Settlement\" href=\"event:",
            "<a style=\"Link.Settlement\">no href</a>",
            "<a href=\"event:Settlement-town_MM1\">no style</a>",
            "<a style=\"Link.Settlement\" href=\"event:NoSeparator\"><b>x</b></a>",
            "<a style=\"\" href=\"event:Settlement-town_MM1\"><b>x</b></a>"
        };

        foreach (var text in cases)
            Assert.AreEqual(text, _sut.Rewrite(text), $"Unexpected rewrite of: {text}");
    }

    [TestMethod]
    public void Rewrite_NonLinkStyle_IsIgnored()
    {
        // Only Link.* styles are ours to retint; anything else passes through untouched.
        _lookup.GetCultureId(Arg.Any<string>()).Returns("mordor");
        var text = Link("Highlight", "Settlement-town_MM1", "Minas Morgul");

        Assert.AreEqual(text, _sut.Rewrite(text));
    }

    [TestMethod]
    public void Rewrite_ConceptUnitShipAndGenericLinks_AreUntouched()
    {
        // These link types have no owning faction, so there is nothing to colour them by.
        // Link.Ship in particular has no style defined by ANY brush in the game — emitting a
        // TAOM name for it would be inventing colour where vanilla shows none.
        _lookup.GetCultureId(Arg.Any<string>()).Returns("mordor");

        foreach (var style in new[] { "Link", "Link.Concept", "Link.Unit", "Link.Ship" })
        {
            var text = Link(style, "Concept-conversation", "parley");
            Assert.AreEqual(text, _sut.Rewrite(text), $"Style {style} should not be rewritten.");
        }
    }

    [TestMethod]
    public void Rewrite_BrushMissingTheStyle_KeepsVanillaStyleAndWarnsOnce()
    {
        // A module later in load order can replace GUI/Brushes/GameMenu.xml wholesale. Emitting a
        // style the live brush lacks would render the link as black body text with no diagnostic.
        _brush.DefinesStyle(Arg.Any<string>()).Returns(false);
        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");
        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul");

        Assert.AreEqual(text, _sut.Rewrite(text));
        Assert.AreEqual(text + " ", _sut.Rewrite(text + " "));
        _logger.Received(1).LogWarning(Arg.Any<string>());
    }

    [TestMethod]
    public void Rewrite_AlreadyRewrittenText_IsStable()
    {
        // The patched setter stores the rewritten string, and GameMenuVM compares the incoming
        // value against it. A non-idempotent rewrite would fire OnPropertyChanged every refresh.
        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");
        var once = _sut.Rewrite(Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul"));

        Assert.AreEqual(once, _sut.Rewrite(once));
    }

    [TestMethod]
    public void Rewrite_SameTextAfterCultureChanged_ReflectsTheNewCulture()
    {
        // Regression guard. An earlier revision memoised the last (input, output) pair keyed on the
        // menu string — but the answer depends on the linked object's CULTURE, and the menu text is
        // byte-identical before and after a culture conversion or a load of a different save. That
        // returned a stale faction colour with nothing to indicate it was wrong.
        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul");

        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");
        Assert.AreEqual(Link("Link.Taom.mordor", "Settlement-town_MM1", "Minas Morgul"), _sut.Rewrite(text));

        _lookup.GetCultureId("Settlement-town_MM1").Returns("gondor");
        Assert.AreEqual(Link("Link.Taom.gondor", "Settlement-town_MM1", "Minas Morgul"), _sut.Rewrite(text));
    }

    [TestMethod]
    public void Rewrite_SameTextAfterBrushBecameAvailable_StopsFallingBackToVanilla()
    {
        // Same staleness class as above, via the brush probe: the UI can report a style missing on
        // one call and present on a later one. A cache keyed on the text would latch the fallback.
        var text = Link("Link.Settlement", "Settlement-town_MM1", "Minas Morgul");
        _lookup.GetCultureId("Settlement-town_MM1").Returns("mordor");

        _brush.DefinesStyle(Arg.Any<string>()).Returns(false);
        Assert.AreEqual(text, _sut.Rewrite(text));

        _brush.DefinesStyle(Arg.Any<string>()).Returns(true);
        Assert.AreEqual(Link("Link.Taom.mordor", "Settlement-town_MM1", "Minas Morgul"), _sut.Rewrite(text));
    }
}
