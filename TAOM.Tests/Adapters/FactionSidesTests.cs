using System.Reflection;
using System.Runtime.Serialization;
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
/// process has no campaign, so the members that read the player run their guards only. A hero's clan and realm are
/// staged on bare engine objects (uninitialized, private fields set directly) where a test needs them; the rest is pinned on
/// the comment-stripped source.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
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
    public void OfCharacter_NullCharacter_IsNone()
    {
        Assert.AreSame(SideIds.None, FactionSides.OfCharacter(null));
    }

    [TestMethod]
    public void OfPlayer_NoCampaign_IsNone_AndNeverReadsTheCampaignStatics()
    {
        // Clan.PlayerClan dereferences Campaign.Current (Clan.cs:284), which is null here (every Custom Battle).
        Assert.IsNull(Campaign.Current);

        Assert.AreSame(SideIds.None, FactionSides.OfPlayer());
    }

    [TestMethod]
    public void OfHero_ReadsTheClansMapFaction_NeverTheHeroesMapFaction()
    {
        // Hero.MapFaction gives a clanless wanderer the home town's realm (Hero.cs:569-591), so a wanderer would be sided
        // by a realm that is not its own. A hero in a clan reads the clan's map faction; a clanless one its own culture.
        var src = RepoPaths.ReadSource("Main/Adapters/FactionSides.cs", stripComments: true);
        var start = src.IndexOf("public static SideIds OfHero(", System.StringComparison.Ordinal);
        var body = src.Substring(start, src.IndexOf("public static SideIds OfOwnClan(", System.StringComparison.Ordinal) - start);

        StringAssert.Contains(body, "hero.Clan");
        StringAssert.Contains(body, "Of(clan.MapFaction)");
        StringAssert.Contains(body, "new SideIds(string.Empty, hero.Culture?.StringId ?? string.Empty)");
        Assert.IsFalse(Regex.IsMatch(body, @"hero\.MapFaction"), "OfHero must not read Hero.MapFaction");
    }

    // --- A lord sided by their own clan (#1111, #1114), on bare engine objects: private fields set directly, no campaign ---

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static CultureObject Culture(string id)
    {
        var culture = Bare<CultureObject>();
        culture.StringId = id;
        return culture;
    }

    private static void SetField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, target.GetType().Name + "." + name + " is gone; the staging needs a new field name.");
        field!.SetValue(target, value);
    }

    /// <summary>A hero whose clan (culture <paramref name="clanCulture"/>) serves a realm of culture <paramref name="realmCulture"/>.</summary>
    private static Hero LordServing(string clanId, string clanCulture, string realmId, string realmCulture)
    {
        var kingdom = Bare<Kingdom>();
        kingdom.StringId = realmId;
        typeof(Kingdom).GetProperty(nameof(Kingdom.Culture))!.SetValue(kingdom, Culture(realmCulture));

        var clan = Bare<Clan>();
        clan.StringId = clanId;
        clan.Culture = Culture(clanCulture);
        SetField(clan, "_kingdom", kingdom);

        var hero = Bare<Hero>();
        hero.Culture = Culture(clanCulture);
        SetField(hero, "_clan", clan);
        return hero;
    }

    [TestMethod]
    public void OfOwnClan_AnOrcClanServingGondor_IsTheClansOwnPair()
    {
        var hero = LordServing("orc_clan", "mordor", "empire_w", "gondor");

        var side = FactionSides.OfOwnClan(hero);

        Assert.AreEqual("orc_clan", side.FactionId);
        Assert.AreEqual("mordor", side.CultureId);
    }

    [TestMethod]
    public void OfHero_TheSameLord_IsTheRealmsPair()
    {
        // The contrast that makes the test above mean something: the realm-based pair of the same staged lord.
        var hero = LordServing("orc_clan", "mordor", "empire_w", "gondor");

        var side = FactionSides.OfHero(hero);

        Assert.AreEqual("empire_w", side.FactionId);
        Assert.AreEqual("gondor", side.CultureId);
    }

    [TestMethod]
    public void OfOwnClan_AClanlessHero_IsTheHerosOwnCulture()
    {
        var hero = Bare<Hero>();
        hero.Culture = Culture("dunland");

        var side = FactionSides.OfOwnClan(hero);

        Assert.AreEqual(string.Empty, side.FactionId);
        Assert.AreEqual("dunland", side.CultureId);
    }

    [TestMethod]
    public void OfOwnClan_NullHero_IsNone()
    {
        Assert.AreSame(SideIds.None, FactionSides.OfOwnClan(null));
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
