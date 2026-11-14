using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The shipped story offers (#1105 slice 4): every <c>story.mission</c> in the REAL <c>war_chronicle.json</c> names a defence the REAL
/// <c>story_missions.json</c> loads. The chronicle's loader cannot see the catalog, and an unknown id is silent until the window opens.
/// </summary>
[TestClass]
public class ShippedStoryOffersTests
{
    private static string ModuleData => RepoPaths.RepoPath("Main", "_Module", "ModuleData");

    private static IReadOnlyList<ChronicleEventDefinition> Events()
    {
        var warnings = new List<string>();
        var events = ChronicleConfigValidator.Parse(File.ReadAllText(Path.Combine(ModuleData, "war_chronicle", "war_chronicle.json")), warnings);
        Assert.AreEqual(0, warnings.Count, string.Join("\n", warnings));
        return events;
    }

    [TestMethod]
    public void EveryStoryMission_IsALoadedDefence()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var catalog = new StoryMissionCatalog(paths, Substitute.For<IModLogger>());

        foreach (var definition in Events().Where(d => d.Story != null))
        {
            Assert.IsTrue(catalog.TryGet(definition.Story!.MissionId, out var mission), $"'{definition.Id}' offers unknown story mission '{definition.Story.MissionId}'");
            Assert.AreEqual(StorySide.Defending, mission.Side, $"'{definition.Id}': the player can take only the defending side");
        }
    }

    [TestMethod]
    public void TheHornburg_OffersTheHelmsDeepDefence_AndHoldsByFate()
    {
        var hornburg = Events().Single(d => d.Id == "hornburg");

        Assert.AreEqual("helms_deep_defend", hornburg.Story?.MissionId);
        Assert.AreEqual(ChronicleEventState.Held, hornburg.Story!.Fate);
        StringAssert.StartsWith(hornburg.Story.FateText, "{=taom_wc_hornburg_fate}");
    }
}
