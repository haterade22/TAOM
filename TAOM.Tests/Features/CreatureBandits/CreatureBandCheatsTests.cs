using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Features.CreatureBandits.Cheats;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.CreatureBandits;

/// <summary>
/// `taom.spawn_creature_band` (#694 testing). A typo must be an error, never a default (a mistyped kind quietly
/// spawning the other creature is the parsed-but-unresolvable trap, dev-console.md "Argument parsing"), and the spawn
/// is Tier C (a party saved with the campaign), so nothing spawns without the literal `confirm`.
/// </summary>
[TestClass]
public class CreatureBandCheatsTests
{
    private static string Source => RepoPaths.ReadSource("Main/Features/CreatureBandits/Cheats/CreatureBandCheats.cs", stripComments: true);

    [TestMethod]
    [DataRow("trolls", true, DisplayName = "trolls")]
    [DataRow("Trolls", true, DisplayName = "any case")]
    [DataRow("broods", false, DisplayName = "broods")]
    public void TryParse_KnownKind_ReturnsKindWithoutConfirm(string kind, bool expectTrolls)
    {
        Assert.IsTrue(CreatureBandCheats.TryParse(new List<string> { kind }, out bool trolls, out bool confirmed));
        Assert.AreEqual(expectTrolls, trolls);
        Assert.IsFalse(confirmed, "no confirm token: a dry run");
    }

    [TestMethod]
    [DataRow("trolls", true)]
    [DataRow("broods", false)]
    public void TryParse_ConfirmToken_ReturnsConfirmed(string kind, bool expectTrolls)
    {
        Assert.IsTrue(CreatureBandCheats.TryParse(new List<string> { kind, "confirm" }, out bool trolls, out bool confirmed));
        Assert.AreEqual(expectTrolls, trolls);
        Assert.IsTrue(confirmed);
    }

    [TestMethod]
    [DataRow(new string[0], DisplayName = "no kind")]
    [DataRow(new[] { "troll" }, DisplayName = "typo")]
    [DataRow(new[] { "spiders" }, DisplayName = "not a documented kind")]
    [DataRow(new[] { "trolls", "yes" }, DisplayName = "not the confirm token")]
    [DataRow(new[] { "trolls", "Confirm" }, DisplayName = "the token is literal")]
    [DataRow(new[] { "trolls", "" }, DisplayName = "a doubled space")]
    [DataRow(new[] { "trolls", "confirm", "extra" }, DisplayName = "extra argument")]
    public void TryParse_AnythingElse_ReturnsFalse(string[] args)
        => Assert.IsFalse(CreatureBandCheats.TryParse(new List<string>(args), out _, out _));

    [TestMethod]
    public void Command_SpawnsWithinSight_AndKeepsEveryRefusal()
    {
        // A small band is spotted only inside about 0.6 of the player's sight (0.4 in forest): a quarter always shows.
        StringAssert.Contains(Source, "NavigationHelper.FindPointAroundPosition(player.Position, MobileParty.NavigationType.Default, player.SeeingRange / 4f)");
        // MobileParty.SiegeEvent sees only a besieger; PlayerSiege also sees a player defending inside a besieged town.
        StringAssert.Contains(Source, "if (player.MapEvent != null || PlayerSiege.PlayerSiegeEvent != null)");
        StringAssert.Contains(Source, "if (!Campaign.Current.GameStarted || Hero.MainHero.IsPrisoner)");
    }
}
