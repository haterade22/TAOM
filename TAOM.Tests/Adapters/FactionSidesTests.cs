using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Features.Execution;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Adapters;

/// <summary>
/// <c>FactionSides</c> is the one place a <see cref="SideIds"/> pair is built from engine objects (#957). The test
/// process has no campaign, so the members that read a hero or the player run their guards only; what each one reads
/// from a hero is pinned on the comment-stripped source, because a <c>Hero</c> cannot be built here.
/// </summary>
[TestClass]
public class FactionSidesTests
{
    private static IFaction Faction(string? id)
    {
        var faction = Substitute.For<IFaction>();
        faction.StringId.Returns(id!);
        faction.Culture.Returns((CultureObject)null!);
        return faction;
    }

    [TestMethod]
    public void Of_NullFaction_IsNone()
    {
        Assert.AreSame(SideIds.None, FactionSides.Of(null));
    }

    [TestMethod]
    public void Of_AFactionWithoutACulture_KeepsItsIdAndAnEmptyCulture()
    {
        var side = FactionSides.Of(Faction("empire_w"));

        Assert.AreEqual(new SideIds("empire_w", string.Empty), side);
    }

    [TestMethod]
    public void Of_AFactionWithANullId_ReadsEmpty()
    {
        var side = FactionSides.Of(Faction(null));

        Assert.AreEqual(SideIds.None, side);
    }

    [TestMethod]
    public void OfHero_NullHero_IsNone()
    {
        Assert.AreSame(SideIds.None, FactionSides.OfHero(null));
    }

    [TestMethod]
    public void OfPlayer_NoCampaign_IsNone_AndNeverReadsTheCampaignStatics()
    {
        // Clan.PlayerClan dereferences Campaign.Current (Clan.cs:276, v1.4.8), which is null here (every Custom Battle).
        Assert.IsNull(Campaign.Current);

        Assert.AreSame(SideIds.None, FactionSides.OfPlayer());
    }

    [TestMethod]
    public void OfHero_ReadsTheClansMapFaction_NeverTheHeroesMapFaction()
    {
        // Hero.MapFaction gives a clanless wanderer the home town's realm (Hero.cs:567-590, v1.4.8), so a wanderer would be sided
        // by a realm that is not its own. A hero in a clan reads the clan's map faction; a clanless one its own culture.
        var src = RepoPaths.ReadSource("Main/Adapters/FactionSides.cs", stripComments: true);
        var start = src.IndexOf("public static SideIds OfHero(", System.StringComparison.Ordinal);
        var body = src.Substring(start, src.IndexOf("public static SideIds OfPlayer(", System.StringComparison.Ordinal) - start);

        StringAssert.Contains(body, "hero.Clan");
        StringAssert.Contains(body, "Of(clan.MapFaction)");
        StringAssert.Contains(body, "new SideIds(string.Empty, hero.Culture?.StringId ?? string.Empty)");
        Assert.IsFalse(Regex.IsMatch(body, @"hero\.MapFaction"), "OfHero must not read Hero.MapFaction");
    }

    [TestMethod]
    public void OfPlayer_GuardsTheCampaignBeforeReadingThePlayerClan()
    {
        var src = RepoPaths.ReadSource("Main/Adapters/FactionSides.cs", stripComments: true);
        var start = src.IndexOf("public static SideIds OfPlayer(", System.StringComparison.Ordinal);
        var body = src.Substring(start);

        var guard = body.IndexOf("Campaign.Current == null", System.StringComparison.Ordinal);
        var read = body.IndexOf("Clan.PlayerClan", System.StringComparison.Ordinal);
        Assert.IsTrue(guard >= 0, "OfPlayer has no Campaign.Current null guard");
        Assert.IsTrue(guard < read, "OfPlayer must test Campaign.Current before it reads Clan.PlayerClan");
        StringAssert.Contains(body, "Of(Clan.PlayerClan?.MapFaction)");
    }
}
