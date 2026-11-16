using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.OrcBattleVoice;

namespace TAOM.Tests.Features.OrcBattleVoice;

[TestClass]
public class OrcBattleVoiceServiceTests
{
    private const int OrcRace = 7;
    private const int UrukRace = 8;
    private const int HumanRace = 0;

    // The "human" sound class list as the engine might order it: opaque native indices, uruk_01 not first.
    private static readonly Dictionary<int, string> Engine = new()
    {
        [10] = "male_01",
        [11] = "male_02",
        [12] = "male_05",
        [13] = "female_01",
        [14] = "dwarf_01",
        [15] = "uruk_01",
        [16] = "uruk_hai_01",
    };

    /// <summary>
    /// Stands in for one agent's native voice slot. With <see cref="ReadbackFollowsSet"/> the agent's reported
    /// definition is the one last set on its visuals; without it the report never changes.
    /// </summary>
    private sealed class FakeAgent : IOrcBattleVoiceAgent
    {
        public bool IsHuman { get; set; } = true;
        public bool IsFemale { get; set; }
        public bool IsPlayerControlled { get; set; }
        public bool HasVisuals { get; set; } = true;
        public float Age { get; set; } = 30f;
        public int Race { get; set; } = OrcRace;
        public int BodyPropertiesSeed { get; set; } = 1073741824;
        public bool ReadbackFollowsSet { get; set; } = true;
        public string Current { get; set; } = "male_05";
        public readonly List<(int Index, float Pitch)> Sets = new();

        public string VoiceDefinitionName => Current;

        public void SetVoiceDefinition(int index, float pitch)
        {
            Sets.Add((index, pitch));
            if (ReadbackFollowsSet) Current = Engine.TryGetValue(index, out var name) ? name : "";
        }
    }

    private sealed class Rig
    {
        public readonly IVoiceDefinitionCatalogAdapter Catalog = Substitute.For<IVoiceDefinitionCatalogAdapter>();
        public readonly IRaceManager Races = Substitute.For<IRaceManager>();
        public readonly IOutcomeLog Outcomes = Substitute.For<IOutcomeLog>();
        public readonly IModLogger Log = Substitute.For<IModLogger>();
        public readonly OrcBattleVoiceService Service;

        public Rig(int[]? indices = null)
        {
            Catalog.GetDefinitionIndices("human").Returns(indices ?? Engine.Keys.ToArray());
            Races.GetRaceNameFromId(OrcRace).Returns("orc");
            Races.GetRaceNameFromId(UrukRace).Returns("uruk");
            Races.GetRaceNameFromId(HumanRace).Returns("human");
            Service = new OrcBattleVoiceService(Catalog, Races, Outcomes, Log);
        }
    }

    [TestMethod]
    public void OnAgentBuild_MaleOrc_GetsTheUrukVoice()
    {
        var rig = new Rig();
        var orc = new FakeAgent();

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual("uruk_01", orc.Current);
        Assert.AreEqual(15, orc.Sets.Last().Index);
    }

    [TestMethod]
    public void OnAgentBuild_SecondOrc_ReusesTheFoundIndexWithoutProbing()
    {
        var rig = new Rig();
        rig.Service.OnAgentBuild(new FakeAgent());
        var second = new FakeAgent();

        rig.Service.OnAgentBuild(second);

        Assert.AreEqual(1, second.Sets.Count, "the probe runs once per mission");
        Assert.AreEqual(15, second.Sets[0].Index);
        rig.Catalog.Received(1).GetDefinitionIndices("human");
    }

    [TestMethod]
    public void OnAgentBuild_ProbeStopsAtTheUrukVoice()
    {
        var rig = new Rig();
        var orc = new FakeAgent();

        rig.Service.OnAgentBuild(orc);

        CollectionAssert.AreEqual(new[] { 10, 11, 12, 13, 14, 15, 15 }, orc.Sets.Select(s => s.Index).ToArray());
    }

    [TestMethod]
    public void OnAgentBuild_UrukRace_IsLeftAlone()
    {
        var rig = new Rig();
        var uruk = new FakeAgent { Race = UrukRace, Current = "uruk_01" };

        rig.Service.OnAgentBuild(uruk);

        Assert.AreEqual(0, uruk.Sets.Count);
    }

    [TestMethod]
    public void OnAgentBuild_HumanRace_IsLeftAlone()
    {
        var rig = new Rig();
        var man = new FakeAgent { Race = HumanRace };

        rig.Service.OnAgentBuild(man);

        Assert.AreEqual(0, man.Sets.Count);
        Assert.AreEqual("male_05", man.Current);
    }

    [TestMethod]
    public void OnAgentBuild_FemaleOrc_KeepsHerVoice()
    {
        var rig = new Rig();
        var orc = new FakeAgent { IsFemale = true, Current = "female_01" };

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(0, orc.Sets.Count, "uruk_01 is a male voice; the adult female orc skin keeps its own pool");
    }

    [TestMethod]
    public void OnAgentBuild_PlayerOrc_KeepsTheVoiceChosenInCharacterCreation()
    {
        var rig = new Rig();
        var player = new FakeAgent { IsPlayerControlled = true };

        rig.Service.OnAgentBuild(player);

        Assert.AreEqual(0, player.Sets.Count);
    }

    [TestMethod]
    public void OnAgentBuild_OrcChild_KeepsItsVoice()
    {
        var rig = new Rig();
        var child = new FakeAgent { Age = 9f };

        rig.Service.OnAgentBuild(child);

        Assert.AreEqual(0, child.Sets.Count, "uruk_01 is an adult male voice");
        rig.Catalog.DidNotReceive().GetDefinitionIndices(Arg.Any<string>());
    }

    [DataTestMethod]
    [DataRow(17.9f, false)]
    [DataRow(18f, true)]
    [DataRow(float.NaN, false)]
    public void OnAgentBuild_AdultAgeBoundary(float age, bool expectSet)
    {
        var rig = new Rig();
        var orc = new FakeAgent { Age = age };

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(expectSet, orc.Sets.Count > 0, $"age {age}: only an agent of {OrcBattleVoiceService.AdultAge} or older gets uruk_01");
    }

    [TestMethod]
    public void OnAgentBuild_SecondApplyOnTheSameOrc_IsANoOp()
    {
        var rig = new Rig();
        var orc = new FakeAgent();
        rig.Service.OnAgentBuild(orc);
        int sets = orc.Sets.Count;

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(sets, orc.Sets.Count);
        Assert.AreEqual("uruk_01", orc.Current);
    }

    [TestMethod]
    public void OnAgentBuild_OrcRevertedByASkinRebuild_GetsTheUrukVoiceAgainWithoutProbing()
    {
        var rig = new Rig();
        var orc = new FakeAgent();
        rig.Service.OnAgentBuild(orc);
        orc.Current = "male_03"; // vanilla's banner bearer refresh re-picked the skin voice
        int sets = orc.Sets.Count;

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(sets + 1, orc.Sets.Count, "one set, no second probe");
        Assert.AreEqual(15, orc.Sets.Last().Index);
        Assert.AreEqual("uruk_01", orc.Current);
        rig.Catalog.Received(1).GetDefinitionIndices("human");
    }

    [TestMethod]
    public void OnAgentBuild_NonHumanoid_IsLeftAlone()
    {
        var rig = new Rig();
        var mount = new FakeAgent { IsHuman = false };

        rig.Service.OnAgentBuild(mount);

        Assert.AreEqual(0, mount.Sets.Count);
    }

    [TestMethod]
    public void OnAgentBuild_NoVisuals_IsLeftAlone()
    {
        var rig = new Rig();
        var headless = new FakeAgent { HasVisuals = false };

        rig.Service.OnAgentBuild(headless);

        Assert.AreEqual(0, headless.Sets.Count);
        rig.Catalog.DidNotReceive().GetDefinitionIndices(Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_OrcAlreadyOnTheUrukVoice_IsNotTouched()
    {
        var rig = new Rig();
        var orc = new FakeAgent { Current = "uruk_01" };

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(0, orc.Sets.Count);
    }

    [TestMethod]
    public void OnAgentBuild_UrukVoiceNotInstalled_RestoresTheOriginalVoiceAndStopsForTheMission()
    {
        var rig = new Rig(new[] { 10, 11, 12, 13 });
        var orc = new FakeAgent();

        rig.Service.OnAgentBuild(orc);
        var second = new FakeAgent();
        rig.Service.OnAgentBuild(second);

        Assert.AreEqual("male_05", orc.Current, "the probed agent goes back to the voice it was built with");
        Assert.AreEqual(12, orc.Sets.Last().Index);
        Assert.AreEqual(0, second.Sets.Count, "no second probe in the same mission");
        rig.Outcomes.Received(1).Fail(OrcBattleVoiceService.CheckId, "uruk-voice-missing", Arg.Any<string>());
        rig.Outcomes.DidNotReceive().Pass(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_ReadbackDoesNotFollowTheSetter_StopsAtTheFirstRepeatAndReportsFail()
    {
        var rig = new Rig();
        var orc = new FakeAgent { ReadbackFollowsSet = false };

        rig.Service.OnAgentBuild(orc);
        var second = new FakeAgent { ReadbackFollowsSet = false };
        rig.Service.OnAgentBuild(second);

        // Index 10 reports male_05, index 11 reports male_05 again: the readback is not tied to the setter,
        // so the name map is worthless and probing stops there.
        CollectionAssert.AreEqual(new[] { 10, 11 }, orc.Sets.Select(s => s.Index).ToArray());
        Assert.AreEqual(0, second.Sets.Count);
        rig.Outcomes.Received(1).Fail(OrcBattleVoiceService.CheckId, "readback-not-following", Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_EmptyCatalog_ReportsFailAndSetsNothing()
    {
        var rig = new Rig(Array.Empty<int>());
        var orc = new FakeAgent();

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(0, orc.Sets.Count);
        rig.Outcomes.Received(1).Fail(OrcBattleVoiceService.CheckId, "no-human-voices", Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_Applied_ReportsPassOnce()
    {
        var rig = new Rig();

        rig.Service.OnAgentBuild(new FakeAgent());
        rig.Service.OnAgentBuild(new FakeAgent());

        rig.Outcomes.Received(1).Pass(OrcBattleVoiceService.CheckId, "", Arg.Is<string>(d => d.Contains("voice=uruk_01")));
        rig.Outcomes.DidNotReceive().Fail(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_SetDoesNotTake_ReportsFail()
    {
        var rig = new Rig();
        rig.Service.OnAgentBuild(new FakeAgent());
        var stubborn = new FakeAgent { ReadbackFollowsSet = false };

        rig.Service.OnAgentBuild(stubborn);

        rig.Outcomes.Received(1).Fail(OrcBattleVoiceService.CheckId, "set-not-taken", Arg.Is<string>(d => d.Contains("reported=male_05")));
    }

    [TestMethod]
    public void OnAgentBuild_DifferentFailReasons_EachReachTheOutcomeLogWithTheirOwnKey()
    {
        // IOutcomeLog writes one line per (id, result, key) per process, so a shared key would hide the second reason.
        var missing = new Rig(new[] { 10, 11, 12 });
        var taken = new Rig();
        var shared = missing.Outcomes;
        var second = new OrcBattleVoiceService(taken.Catalog, taken.Races, shared, taken.Log);

        missing.Service.OnAgentBuild(new FakeAgent());
        second.OnAgentBuild(new FakeAgent());
        second.OnAgentBuild(new FakeAgent { ReadbackFollowsSet = false });

        shared.Received(1).Fail(OrcBattleVoiceService.CheckId, "uruk-voice-missing", Arg.Any<string>());
        shared.Received(1).Fail(OrcBattleVoiceService.CheckId, "set-not-taken", Arg.Any<string>());
        shared.DidNotReceive().Fail(OrcBattleVoiceService.CheckId, "", Arg.Any<string>());
    }

    [TestMethod]
    public void OnAgentBuild_AfterEnd_DoesNothing()
    {
        var rig = new Rig();
        rig.Service.End();
        var orc = new FakeAgent();

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(0, orc.Sets.Count);
    }

    [DataTestMethod]
    [DataRow(0, 0f)]
    [DataRow(1073741824, 0.5f)]
    [DataRow(-1073741824, 0.5f)]
    [DataRow(int.MaxValue, 1f)]
    [DataRow(int.MinValue, 1f)]
    public void PitchFor_MapsTheSeedLikeVanillaMounts(int seed, float expected)
    {
        Assert.AreEqual(expected, OrcBattleVoiceService.PitchFor(seed), 1e-6f);
    }

    [TestMethod]
    public void OnAgentBuild_PitchComesFromTheSoldiersSeed()
    {
        var rig = new Rig();
        var orc = new FakeAgent { BodyPropertiesSeed = 1073741824 };

        rig.Service.OnAgentBuild(orc);

        Assert.AreEqual(0.5f, orc.Sets.Last().Pitch, 1e-6f);
    }
}
