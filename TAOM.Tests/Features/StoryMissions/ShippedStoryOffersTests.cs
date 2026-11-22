using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.Execution;
using TAOM.Features.StoryMissions.Config;
using TAOM.Features.StoryMissions.Domain;
using TAOM.Features.WarChronicle.Events;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.StoryMissions;

/// <summary>
/// The shipped story offers (#1105 slice 4): every <c>story.mission</c> in the REAL <c>war_chronicle.json</c> names a defence the REAL
/// <c>story_missions.json</c> loads, and none names a <c>story.attackMission</c> until the maintainer decides the key (#1409 step 6). The
/// chronicle's loader cannot see the catalog, and an unknown id is silent until the window opens. The side gate reads each kingdom of a story window from the alignment
/// table, where an unlisted id is silently Neutral, so every one must be listed.
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
    public void EveryStoryBlock_ItsMissionIsADefence_AndOnlyTheBreakingNamesAnAttack()
    {
        var paths = Substitute.For<IPathService>();
        paths.ModuleDataPath.Returns(ModuleData);
        var catalog = new StoryMissionCatalog(paths, Substitute.For<IModLogger>());

        foreach (var definition in Events().Where(d => d.Story != null))
        {
            Assert.IsTrue(catalog.TryGet(definition.Story!.MissionId, out var mission), $"'{definition.Id}' offers unknown story mission '{definition.Story.MissionId}'");
            Assert.AreEqual(StorySide.Defending, mission.Side, $"'{definition.Id}': story.mission is the defence");
            if (definition.Story.HeroId != null)
            {
                // A hero story (#1105 slice 3) is fought beside the hero or against him, so it names both missions.
                Assert.IsTrue(catalog.TryGet(definition.Story.AttackMissionId ?? string.Empty, out var hunt), $"'{definition.Id}' offers unknown attack mission '{definition.Story.AttackMissionId}'");
                Assert.AreEqual(StorySide.Attacking, hunt.Side, $"'{definition.Id}': story.attackMission is the attack");
                Assert.IsNotNull(mission.MustLive, $"'{definition.Id}': the defence is decided by its hero");
                Assert.AreEqual(mission.MustLive, hunt.MustLive, $"'{definition.Id}': both missions are decided by the same hero");
                continue;
            }

            // The launcher opens helms_deep_attack, but the Hornburg window does not offer it yet (#1409 step 6, slice 5 of #1105).
            // Adding the key replaces this line with the row's check.
            Assert.IsNull(definition.Story.AttackMissionId, $"'{definition.Id}' names an attackMission before slice 5");
        }
    }

    [TestMethod]
    public void TheBreaking_OffersTheTwoParthGalenMissions_AndDecidesBoromir()
    {
        var breaking = Events().Single(d => d.Id == "breaking");

        Assert.AreEqual("lord_1_75", breaking.Story?.HeroId);
        Assert.AreEqual("parth_galen_save", breaking.Story!.MissionId);
        Assert.AreEqual("parth_galen_kill", breaking.Story.AttackMissionId);
        Assert.AreEqual(10, breaking.WindowDays);
        Assert.AreEqual("empire_w", breaking.Defender, "Gondor's side stands with Boromir");
        CollectionAssert.AreEqual(new[] { "isengard" }, breaking.Attackers.ToArray());
        Assert.IsNotNull(breaking.Averted?.Text);
        Assert.IsNotNull(breaking.Occurred?.Text);
    }

    [TestMethod]
    public void EveryStoryWindowKingdom_IsListedInTheAlignmentTable()
    {
        var table = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(ModuleData, "execution", "alignment.json")));
        var story = Events().Where(d => d.Story != null).ToList();

        Assert.IsTrue(story.Count > 0, "the scan found no story window");
        foreach (var definition in story)
        foreach (var kingdom in definition.Attackers.Concat(new[] { definition.Defender! }))
            Assert.IsNotNull(table[kingdom], $"'{definition.Id}': kingdom '{kingdom}' is not in execution/alignment.json, so the side gate (GetKingdomSide, no culture fallback) would read it Neutral");
    }

    [TestMethod]
    public void TheHornburg_IsAFreeFortressUnderAnEvilHost()
    {
        // The gate's worked case (StorySideGateTests): a good player defends Helm's Deep, an evil player attacks it.
        var table = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(ModuleData, "execution", "alignment.json")));
        var hornburg = Events().Single(d => d.Id == "hornburg");

        Assert.AreEqual("free", (string?)table[hornburg.Defender!]);
        Assert.AreEqual("evil", (string?)table[hornburg.Attackers[0]]);
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
