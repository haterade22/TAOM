using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.MountAndBlade;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.StagedSiege;
using TAOM.Features.StagedSiege.Domain;
using TAOM.Features.StagedSiege.Hooks;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.StagedSiege;

/// <summary>
/// The spawn-settings prefix's own body, run for real (no Harmony is applied in the test host: the prefix is a plain static
/// method). The armed service is built over a substituted adapter; the spawn logic and the mission are uninitialized engine
/// objects, which is all the prefix reads (the mission as a token, the timer field it writes).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class StagedSiegeSpawnSettingsPatchTests
{
    private const string Scene = "sturgia_castle_siege_001";

    private object _mission = null!;
    private StagedSiegeService _service = null!;
    private DefaultBattleMissionAgentSpawnLogic _logic = null!;
    private MissionSpawnSettings _original;

    [ClassInitialize]
    public static void Init(TestContext _) => GameAssemblies.EnsureLoaded();

    [TestInitialize]
    public void Setup()
    {
        StagedSiegeSlot.ClearArmed();
        _mission = FormatterServices.GetUninitializedObject(typeof(Mission));
        var adapter = Substitute.For<IStagedSiegeMissionAdapter>();
        adapter.MissionToken.Returns(_mission);
        adapter.SceneName.Returns(Scene);
        adapter.IsSiegeBattle.Returns(true);
        adapter.IsCampaignWallsAssault.Returns(true);
        adapter.PlayerSide.Returns((StagedSide?)StagedSide.Attacker);
        var catalog = StagedSiegeSceneCatalog.FromText(StagedSiegeSceneCatalogTests.PilotText, Substitute.For<IModLogger>());
        _service = new StagedSiegeService(adapter, catalog, Substitute.For<IModLogger>());
        _logic = SpawnLogicOf(_mission);
        _original = new MissionSpawnSettings(
            MissionSpawnSettings.InitialSpawnMethod.BattleSizeAllocating,
            MissionSpawnSettings.ReinforcementTimingMethod.GlobalTimer,
            MissionSpawnSettings.ReinforcementSpawnMethod.Fixed,
            7f, 0.1f, 0.2f, 0.3f, 4, 0.4f, 0.5f, 1.7f, 0.66f);
    }

    [TestCleanup]
    public void Cleanup() => StagedSiegeSlot.ClearArmed();

    private static DefaultBattleMissionAgentSpawnLogic SpawnLogicOf(object mission)
    {
        var logic = (DefaultBattleMissionAgentSpawnLogic)FormatterServices.GetUninitializedObject(typeof(DefaultBattleMissionAgentSpawnLogic));
        typeof(MissionBehavior).GetProperty(nameof(MissionBehavior.Mission))!.SetValue(logic, mission);
        return logic;
    }

    private static object? Timer(DefaultBattleMissionAgentSpawnLogic logic) =>
        typeof(DefaultBattleMissionAgentSpawnLogic).GetField("_customReinforcementSpawnTimer", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(logic);

    private static void AssertUnchanged(MissionSpawnSettings expected, MissionSpawnSettings actual)
    {
        Assert.AreEqual(expected.InitialTroopsSpawnMethod, actual.InitialTroopsSpawnMethod);
        Assert.AreEqual(expected.ReinforcementTroopsTimingMethod, actual.ReinforcementTroopsTimingMethod);
        Assert.AreEqual(expected.ReinforcementTroopsSpawnMethod, actual.ReinforcementTroopsSpawnMethod);
        Assert.AreEqual(expected.ReinforcementBatchPercentage, actual.ReinforcementBatchPercentage, 1e-6f);
        Assert.AreEqual(expected.MaximumReinforcementWaveCount, actual.MaximumReinforcementWaveCount);
    }

    [TestMethod]
    public void Prefix_NothingArmed_Untouched()
    {
        var settings = _original;

        StagedSiegeSpawnSettingsPatch.Prefix(_logic, ref settings);

        AssertUnchanged(_original, settings);
        Assert.IsNull(Timer(_logic));
    }

    [TestMethod]
    public void Prefix_OtherMission_Untouched()
    {
        Assert.IsTrue(_service.TryArm(true));
        var settings = _original;

        StagedSiegeSpawnSettingsPatch.Prefix(SpawnLogicOf(FormatterServices.GetUninitializedObject(typeof(Mission))), ref settings);

        AssertUnchanged(_original, settings);
    }

    [TestMethod]
    public void Prefix_Armed_SwapsKeepsAdvantageAndRatio()
    {
        Assert.IsTrue(_service.TryArm(true));
        var settings = _original;

        StagedSiegeSpawnSettingsPatch.Prefix(_logic, ref settings);

        Assert.AreEqual(MissionSpawnSettings.ReinforcementSpawnMethod.Balanced, settings.ReinforcementTroopsSpawnMethod);
        Assert.AreEqual(MissionSpawnSettings.ReinforcementTimingMethod.CustomTimer, settings.ReinforcementTroopsTimingMethod);
        Assert.AreEqual(0.05f, settings.ReinforcementBatchPercentage, 1e-6f);
        Assert.AreEqual(0.166f, settings.DesiredReinforcementPercentage, 1e-6f);
        Assert.AreEqual(0, settings.MaximumReinforcementWaveCount, "the wave count option no longer truncates");
        Assert.AreEqual(_original.InitialTroopsSpawnMethod, settings.InitialTroopsSpawnMethod);
        Assert.AreEqual(10f, settings.GlobalReinforcementInterval, 1e-6f, "the larger of the incoming 7 s and the 10 s trickle");
        Assert.AreEqual(_original.ReinforcementWavePercentage, settings.ReinforcementWavePercentage, 1e-6f);
        Assert.AreEqual(1.7f, settings.DefenderAdvantageFactor, 1e-6f, "the siege's advantage factor stays vanilla's");
        Assert.AreEqual(0.66f, settings.MaximumBattleSideRatio, 1e-6f, "the siege's side ratio stays vanilla's");
    }

    [TestMethod]
    public void Prefix_Armed_KeepsALongerIncomingInterval()
    {
        // BattleEndLogic's rout check reads this interval plus 3 s: it must never be shorter than the staged trickle.
        Assert.IsTrue(_service.TryArm(true));
        var longer = new MissionSpawnSettings(
            MissionSpawnSettings.InitialSpawnMethod.BattleSizeAllocating,
            MissionSpawnSettings.ReinforcementTimingMethod.GlobalTimer,
            MissionSpawnSettings.ReinforcementSpawnMethod.Fixed,
            25f, 0.1f, 0.2f, 0.3f, 4, 0.4f, 0.5f, 1.7f, 0.66f);

        StagedSiegeSpawnSettingsPatch.Prefix(_logic, ref longer);

        Assert.AreEqual(25f, longer.GlobalReinforcementInterval, 1e-6f);
    }

    [TestMethod]
    public void Prefix_Armed_InstallsTimer()
    {
        Assert.IsTrue(_service.TryArm(true));
        var settings = _original;

        StagedSiegeSpawnSettingsPatch.Prefix(_logic, ref settings);

        Assert.IsInstanceOfType(Timer(_logic), typeof(StagedReinforcementTimer));
    }

    [TestMethod]
    public void Prefix_InstanceThatIsNotTheSpawnLogic_LeavesSettingsAlone_AndNothingEscapes()
    {
        Assert.IsTrue(_service.TryArm(true));
        var settings = _original;

        StagedSiegeSpawnSettingsPatch.Prefix("not a spawn logic", ref settings);

        AssertUnchanged(_original, settings);
    }
}
