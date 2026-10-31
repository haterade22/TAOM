using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Tests.Features.FordsOfWar;

[TestClass]
public class FordsMatchPlanTests
{
    private const string Scene = "taom_rohan_battle_001_forceatmo";

    private static FordsConfigCatalog Catalog(IModLogger logger) =>
        FordsConfigCatalog.FromTexts(null, FordsConfigFixtures.Factions, FordsConfigFixtures.Maps, logger);

    [TestMethod]
    public void Resolve_KnownSceneAndFactions_BuildsThePlan()
    {
        var logger = Substitute.For<IModLogger>();

        var plan = FordsMatchPlan.Resolve(new FordsMatchSetup(Scene, "gondor", "mordor", "hard", 4), Catalog(logger), logger);

        Assert.IsNotNull(plan);
        Assert.AreEqual("gondor", plan!.Factions[MatchSide.West].FactionId);
        Assert.AreEqual("mordor", plan.Factions[MatchSide.East].FactionId);
        Assert.AreEqual(Scene, plan.Map.SceneId);
        Assert.AreEqual("hard", plan.AiProfile);
        Assert.AreEqual(4, plan.LaneCount);
        Assert.AreEqual(4, plan.Map.Options.LaneCount);
    }

    [TestMethod]
    public void Resolve_LaneCountOutsideOneToFour_IsClamped()
    {
        var logger = Substitute.For<IModLogger>();
        var catalog = Catalog(logger);

        Assert.AreEqual(4, FordsMatchPlan.Resolve(new FordsMatchSetup(Scene, "gondor", "mordor", "normal", 9), catalog, logger)!.LaneCount);
        Assert.AreEqual(1, FordsMatchPlan.Resolve(new FordsMatchSetup(Scene, "gondor", "mordor", "normal", 0), catalog, logger)!.LaneCount);
    }

    [TestMethod]
    public void Resolve_UnknownProfile_FallsBackToNormal()
    {
        var logger = Substitute.For<IModLogger>();

        var plan = FordsMatchPlan.Resolve(new FordsMatchSetup(Scene, "gondor", "mordor", "legendary", 3), Catalog(logger), logger);

        Assert.AreEqual("normal", plan!.AiProfile);
    }

    [TestMethod]
    public void Resolve_UnknownScene_ReturnsNullAndLogsAnError()
    {
        var logger = Substitute.For<IModLogger>();

        var plan = FordsMatchPlan.Resolve(new FordsMatchSetup("no_such_scene", "gondor", "mordor", "normal", 3), Catalog(logger), logger);

        Assert.IsNull(plan);
        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("no_such_scene")));
    }

    [TestMethod]
    public void Resolve_UnknownFaction_ReturnsNullAndLogsAnError()
    {
        var logger = Substitute.For<IModLogger>();

        var plan = FordsMatchPlan.Resolve(new FordsMatchSetup(Scene, "gondor", "nowhere", "normal", 3), Catalog(logger), logger);

        Assert.IsNull(plan);
        logger.Received(1).LogError(Arg.Is<string>(m => m.Contains("nowhere")));
    }
}
