using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Domain;
using TAOM.Features.StoryMissions.Config;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// A story mission's <c>heroes</c> list (#1366). The row itself is the shop's row without a price, read by the same code
/// (<c>LongDefeatHeroesReaderTests</c> pins the shared part); these tests cover what the story adds: no key, not an array, and the cap of three.
/// </summary>
[TestClass]
public class StoryHeroesReaderTests
{
    private static int Warnings(IModLogger logger) => logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IModLogger.LogWarning));

    private static HeroSpawn[] Read(string? json, out IModLogger logger)
    {
        logger = Substitute.For<IModLogger>();
        var token = json == null ? null : JToken.Parse(json);
        return StoryHeroesReader.Read(token, "m1", new LongDefeatConfigScope(logger, "story_missions.json")).ToArray();
    }

    private static string Row(string id, string troop, string extra = "") => "{\"id\":\"" + id + "\",\"troop\":\"" + troop + "\"" + extra + "}";

    [TestMethod]
    public void NoKey_GivesNoHero_AndNoWarning()
    {
        var heroes = Read(null, out var logger);

        Assert.AreEqual(0, heroes.Length);
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void NotAnArray_Warns_AndGivesNoHero()
    {
        var heroes = Read("{\"id\":\"aragorn\"}", out var logger);

        Assert.AreEqual(0, heroes.Length);
        Assert.AreEqual(1, Warnings(logger));
        logger.Received().LogWarning(Arg.Is<string>(m => m.Contains("m1.heroes") && m.Contains("not an array")));
    }

    [TestMethod]
    public void ARowWithABadId_Warns_AndIsDropped()
    {
        var heroes = Read("[" + Row("Bad Id", "t_a") + "," + Row("gimli", "t_g") + "]", out var logger);

        Assert.AreEqual("gimli", heroes.Single().Id);
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void AMissingTroop_Warns_AndIsDropped()
    {
        var heroes = Read("[{\"id\":\"aragorn\"}," + Row("gimli", "t_g") + "]", out var logger);

        Assert.AreEqual("gimli", heroes.Single().Id);
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void HealthFactorOutOfRange_Warns_AndIsDropped()
    {
        var heroes = Read("[" + Row("aragorn", "t_a", ",\"healthFactor\":11") + "," + Row("gimli", "t_g", ",\"healthFactor\":3.0") + "]", out var logger);

        Assert.AreEqual(new HeroSpawn("gimli", "t_g", 3f), heroes.Single());
        Assert.AreEqual(1, Warnings(logger));
    }

    [TestMethod]
    public void NoHealthFactor_DefaultsToOne()
    {
        var heroes = Read("[" + Row("aragorn", "t_a") + "]", out var logger);

        Assert.AreEqual(new HeroSpawn("aragorn", "t_a", 1f), heroes.Single());
        Assert.AreEqual(0, Warnings(logger));
    }

    [TestMethod]
    public void ADuplicateIdOrTroop_KeepsTheFirst_WithAWarning()
    {
        var heroes = Read("[" + Row("aragorn", "t_a") + "," + Row("aragorn", "t_x") + "," + Row("legolas", "T_A") + "]", out var logger);

        Assert.AreEqual("aragorn", heroes.Single().Id);
        Assert.AreEqual(2, Warnings(logger));
    }

    [TestMethod]
    public void AStoryRow_NeedsNoPrice()
    {
        var heroes = Read("[" + Row("aragorn", "t_a") + "]", out _);

        Assert.AreEqual(1, heroes.Length);
    }

    [TestMethod]
    public void MoreThanThree_KeepsTheFirstThree_WithOneWarning()
    {
        var heroes = Read("[" + Row("a", "t_a") + "," + Row("b", "t_b") + "," + Row("c", "t_c") + "," + Row("d", "t_d") + "," + Row("e", "t_e") + "]", out var logger);

        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, heroes.Select(h => h.Id).ToArray());
        Assert.AreEqual(1, Warnings(logger));
    }
}
