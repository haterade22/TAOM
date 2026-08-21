using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;
using TAOM.Core.Infrastructure;
using TaleWorlds.Core;

namespace TAOM.Tests.Features.HeroRace;

[TestClass]
public class EyeHeightAdjustmentHookTests
{
    private EyeHeightAdjustmentHook _sut;
    private IRaceManager _raceManager;
    private IFaceGenAdapter _faceGenAdapter;
    private IHeroRaceSettingsProvider _settings;
    private IReflectionService _reflection;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        _faceGenAdapter = Substitute.For<IFaceGenAdapter>();
        _settings = Substitute.For<IHeroRaceSettingsProvider>();
        _settings.EyeHeightEnabled.Returns(true);
        _settings.DwarfEyeHeightAdjuster.Returns(EyeHeightAdjustment.DefaultAdjuster);
        _logger = Substitute.For<IModLogger>();
        // The real reflection service, not a substitute: these tests are about whether the two
        // backing-field writes actually land on a Monster.
        _reflection = new ReflectionService(Substitute.For<IModLogger>());
        _sut = new EyeHeightAdjustmentHook(_raceManager, _faceGenAdapter, _settings, _reflection, _logger);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_RaceZero_ReturnsEarlyWithoutAction()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, 0);

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_NegativeRace_ReturnsEarlyWithoutAction()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, -1);

        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_WhenAdapterReturnsNullMonster_LogsInitErrorAndReturnsEarly()
    {
        Monster monster = default;
        _raceManager.GetRaceNameFromId(1).Returns("elf");

        _sut.OnGetBaseMonsterFromRace(ref monster, 1);

        _logger.Received(1).LogError(Arg.Is<string>(s => s.Contains("Failed to initialize _defaultMonster")));
        _raceManager.DidNotReceive().GetRaceNameFromId(Arg.Any<int>());
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_AfterInitFailure_RetriesOnNextCall()
    {
        Monster monster = default;

        _sut.OnGetBaseMonsterFromRace(ref monster, 1);
        _sut.OnGetBaseMonsterFromRace(ref monster, 2);

        // Should retry init on each call until successful
        _faceGenAdapter.Received(2).GetBaseMonsterFromRace(0);
    }

    // ---------------------------------------------------------------------------------------
    // The capture / restore state machine. Previously untested on the stated grounds that a
    // TaleWorlds Monster cannot be constructed in a test host. It can: Monster declares no
    // constructor and MBObjectBase has a public parameterless one (verified against 1.4.8).
    // ---------------------------------------------------------------------------------------

    private const string StandingField = "<StandingEyeHeight>k__BackingField";
    private const string CrouchField = "<CrouchEyeHeight>k__BackingField";

    private const int DwarfRaceId = 3;
    private const float HumanStanding = 1.65f;
    private const float HumanCrouch = 1.10f;
    private const float DwarfVanillaStanding = 1.20f;
    private const float DwarfVanillaCrouch = 0.80f;

    private Monster MakeMonster(string stringId, float standing, float crouch)
    {
        var monster = new Monster { StringId = stringId };
        _reflection.SetFieldValue(monster, StandingField, standing);
        _reflection.SetFieldValue(monster, CrouchField, crouch);
        return monster;
    }

    private void ArrangeDwarf()
    {
        _raceManager.GetRaceNameFromId(DwarfRaceId).Returns("dwarf");
        _faceGenAdapter.GetBaseMonsterFromRace(0)
            .Returns(MakeMonster("human_monster", HumanStanding, HumanCrouch));
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_EnabledDwarf_LowersEyeHeightRelativeToTheHumanBaseline()
    {
        ArrangeDwarf();
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);

        Assert.AreEqual(HumanStanding + EyeHeightAdjustment.DefaultAdjuster, dwarf.StandingEyeHeight, 0.0001f);
        Assert.AreEqual(HumanCrouch + EyeHeightAdjustment.DefaultAdjuster, dwarf.CrouchEyeHeight, 0.0001f);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_NonDwarfRace_IsLeftAlone()
    {
        _raceManager.GetRaceNameFromId(4).Returns("elf");
        _faceGenAdapter.GetBaseMonsterFromRace(0)
            .Returns(MakeMonster("human_monster", HumanStanding, HumanCrouch));
        var elf = MakeMonster("elf_monster", 1.80f, 1.20f);

        _sut.OnGetBaseMonsterFromRace(ref elf, 4);

        Assert.AreEqual(1.80f, elf.StandingEyeHeight, 0.0001f);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_ToggledOff_RestoresTheCapturedVanillaPair()
    {
        ArrangeDwarf();
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);   // mutates
        _settings.EyeHeightEnabled.Returns(false);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);   // restores

        Assert.AreEqual(DwarfVanillaStanding, dwarf.StandingEyeHeight, 0.0001f);
        Assert.AreEqual(DwarfVanillaCrouch, dwarf.CrouchEyeHeight, 0.0001f);
    }

    // The trap the capture ordering exists to avoid: if the capture ever ran AFTER the write, the
    // stored "vanilla" pair would be this hook's own output and toggling off would bake the modded
    // height in permanently. Two enables before the disable would hide an ordering bug that one
    // would not.
    [TestMethod]
    public void OnGetBaseMonsterFromRace_RepeatedEnablesThenDisable_StillRestoresTrueVanilla()
    {
        ArrangeDwarf();
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        _settings.EyeHeightEnabled.Returns(false);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);

        Assert.AreEqual(DwarfVanillaStanding, dwarf.StandingEyeHeight, 0.0001f,
            "The captured pair must be the engine value, never a value this hook wrote.");
    }

    // Starting with the toggle already off must capture the true vanilla pair, so a later enable and
    // disable round-trips rather than latching whatever was there at first sight.
    [TestMethod]
    public void OnGetBaseMonsterFromRace_StartsDisabledThenEnabledThenDisabled_RoundTrips()
    {
        ArrangeDwarf();
        _settings.EyeHeightEnabled.Returns(false);
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        Assert.AreEqual(DwarfVanillaStanding, dwarf.StandingEyeHeight, 0.0001f);

        _settings.EyeHeightEnabled.Returns(true);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        Assert.AreEqual(HumanStanding + EyeHeightAdjustment.DefaultAdjuster, dwarf.StandingEyeHeight, 0.0001f);

        _settings.EyeHeightEnabled.Returns(false);
        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);
        Assert.AreEqual(DwarfVanillaStanding, dwarf.StandingEyeHeight, 0.0001f);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_SliderMoved_TracksTheNewValue()
    {
        ArrangeDwarf();
        _settings.DwarfEyeHeightAdjuster.Returns(-0.5f);
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);

        Assert.AreEqual(HumanStanding - 0.5f, dwarf.StandingEyeHeight, 0.0001f);
    }

    // A stale json2 file can hold anything. The clamp is on the path, so an out-of-range slider
    // value cannot reach the engine.
    [TestMethod]
    public void OnGetBaseMonsterFromRace_SliderOutOfRange_IsClampedNotApplied()
    {
        ArrangeDwarf();
        _settings.DwarfEyeHeightAdjuster.Returns(-99f);
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);

        Assert.IsTrue(dwarf.StandingEyeHeight >= EyeHeightAdjustment.MinEyeHeight,
            $"Eye height {dwarf.StandingEyeHeight} fell below the safe floor.");
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_NaNSlider_LeavesTheMonsterUntouched()
    {
        ArrangeDwarf();
        _settings.DwarfEyeHeightAdjuster.Returns(float.NaN);
        var dwarf = MakeMonster("dwarf_monster", DwarfVanillaStanding, DwarfVanillaCrouch);

        _sut.OnGetBaseMonsterFromRace(ref dwarf, DwarfRaceId);

        // ClampAdjuster turns a non-finite slider into the shipped default rather than failing shut.
        Assert.AreEqual(HumanStanding + EyeHeightAdjustment.DefaultAdjuster, dwarf.StandingEyeHeight, 0.0001f);
    }

    [TestMethod]
    public void OnGetBaseMonsterFromRace_NullMonster_DoesNotThrow()
    {
        ArrangeDwarf();
        Monster nothing = null;

        _sut.OnGetBaseMonsterFromRace(ref nothing, DwarfRaceId);

        _logger.DidNotReceive().LogError(Arg.Is<string>(s => s.Contains("Failed to adjust")));
    }
}
