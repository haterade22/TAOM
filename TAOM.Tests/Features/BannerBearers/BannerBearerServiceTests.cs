using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.Core;
using TAOM.Core.Domain;
using TAOM.Core.Logging;
using TAOM.Features.BannerBearers;
using TAOM.Features.BannerBearers.Domain;

namespace TAOM.Tests.Features.BannerBearers;

[TestClass]
public class BannerBearerServiceTests
{
    private IBannerBearerConfigProvider _configProvider = null!;
    private IRaceManager _raceManager = null!;
    private IModLogger _logger = null!;
    private BannerBearerConfig _config = null!;
    private BannerBearerService _sut = null!;

    // Race ids are FaceGen indices (skins.xml merge order), not fixed constants —
    // these are arbitrary stand-ins, which is exactly how the service sees them.
    private const int HumanId = 0;
    private const int DwarfId = 1;
    private const int OrcId = 2;
    private const int ElfId = 3;
    private const int UrukId = 4;
    private const int GoblinId = 5;
    private const int CaveTrollId = 6;
    private const int HillTrollId = 7;
    private const int SauronId = 8;
    private const int UnknownId = 99;

    [TestInitialize]
    public void Setup()
    {
        _config = new BannerBearerConfig();
        _configProvider = Substitute.For<IBannerBearerConfigProvider>();
        _configProvider.GetConfig().Returns(_ => _config);

        _raceManager = Substitute.For<IRaceManager>();
        var names = new Dictionary<int, string>
        {
            { HumanId, "human" },
            { DwarfId, "dwarf" },
            { OrcId, "orc" },
            { ElfId, "elf" },
            { UrukId, "uruk" },
            { GoblinId, "goblin" },
            { CaveTrollId, "cave_troll" },
            { HillTrollId, "hill_troll" },
            { SauronId, "sauron" },
        };
        foreach (var kv in names)
        {
            _raceManager.IsValidRaceId(kv.Key).Returns(true);
            _raceManager.GetRaceNameFromId(kv.Key).Returns(kv.Value);
        }
        _raceManager.IsValidRaceId(UnknownId).Returns(false);
        // RaceManager.GetRaceNameFromId coerces unknown ids to "human" — the fallback trap
        // this service must not fall into (csharp-architecture.md "Validate Before Lookup").
        _raceManager.GetRaceNameFromId(UnknownId).Returns("human");
        foreach (var name in names.Values) _raceManager.IsValidRaceName(name).Returns(true);
        // The compiled default ExcludedRaces list also names these; they are real races in
        // LOTRLOME_Armory/skins.xml but have no id constant here.
        foreach (var name in new[] { "nazghul", "saruman" }) _raceManager.IsValidRaceName(name).Returns(true);

        _logger = Substitute.For<IModLogger>();
        _sut = new BannerBearerService(_configProvider, _raceManager, _logger);
    }

    // ---- GetDesiredBearerCount ------------------------------------------------

    [TestMethod]
    public void GetDesiredBearerCount_FeatureDisabled_ReturnsZero()
    {
        // NOTE: the service returning 0 when disabled is correct, but the MODEL must NOT pass
        // that 0 to the engine — it defers to base instead, so a formation bannered by vanilla's
        // hero-captain path still gets vanilla's bearer. See TaomBattleBannerBearersModel.
        _config.Enabled = false;

        Assert.AreEqual(0, _sut.GetDesiredBearerCount(200, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_BelowMinimumTroopCount_ReturnsZero()
    {
        _config.MinimumFormationTroopCount = 4;

        Assert.AreEqual(0, _sut.GetDesiredBearerCount(3, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_AtMinimumTroopCount_ReturnsOne()
    {
        _config.MinimumFormationTroopCount = 4;

        Assert.AreEqual(1, _sut.GetDesiredBearerCount(4, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_SmallFormationAboveMinimum_ReturnsAtLeastOne()
    {
        _config.InfantryBannerPerSoldiers = 20;

        // 10 / 20 == 0 by integer division; every eligible formation still gets one.
        Assert.AreEqual(1, _sut.GetDesiredBearerCount(10, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_LargeFormation_ScalesWithSize()
    {
        _config.InfantryBannerPerSoldiers = 20;
        _config.MaxBearersPerFormation = 6;

        Assert.AreEqual(3, _sut.GetDesiredBearerCount(60, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_VeryLargeFormation_CapsAtMaxBearers()
    {
        _config.InfantryBannerPerSoldiers = 20;
        _config.MaxBearersPerFormation = 4;

        Assert.AreEqual(4, _sut.GetDesiredBearerCount(1000, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_MaxBearersAboveEngineCap_ClampsToSix()
    {
        // Engine arrangement tables are RelativeFormationPosition[6]; never ask for more.
        _config.InfantryBannerPerSoldiers = 1;
        _config.MaxBearersPerFormation = 50;

        Assert.AreEqual(6, _sut.GetDesiredBearerCount(1000, FormationClass.Infantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_ClassRatioZero_ReturnsZero()
    {
        _config.RangedBannerPerSoldiers = 0;

        Assert.AreEqual(0, _sut.GetDesiredBearerCount(200, FormationClass.Ranged));
    }

    [TestMethod]
    public void GetDesiredBearerCount_UsesPerClassRatio()
    {
        _config.InfantryBannerPerSoldiers = 20;
        _config.CavalryBannerPerSoldiers = 10;
        _config.MaxBearersPerFormation = 6;

        Assert.AreEqual(2, _sut.GetDesiredBearerCount(40, FormationClass.Infantry));
        Assert.AreEqual(4, _sut.GetDesiredBearerCount(40, FormationClass.Cavalry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_HorseArcherAndOtherClasses_UseOwnRatios()
    {
        _config.HorseArcherBannerPerSoldiers = 10;
        _config.OtherBannerPerSoldiers = 5;
        _config.MaxBearersPerFormation = 6;

        Assert.AreEqual(3, _sut.GetDesiredBearerCount(30, FormationClass.HorseArcher));
        // Anything outside the four default classes falls to the "Other" ratio.
        Assert.AreEqual(6, _sut.GetDesiredBearerCount(30, FormationClass.HeavyInfantry));
    }

    [TestMethod]
    public void GetDesiredBearerCount_NegativeUnitCount_ReturnsZero()
    {
        Assert.AreEqual(0, _sut.GetDesiredBearerCount(-5, FormationClass.Infantry));
    }

    // ---- IsRaceAllowed --------------------------------------------------------

    [TestMethod]
    public void IsRaceAllowed_CaveTroll_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsRaceAllowed(CaveTrollId));
    }

    [TestMethod]
    public void IsRaceAllowed_HillTroll_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsRaceAllowed(HillTrollId));
    }

    [TestMethod]
    public void IsRaceAllowed_NamedUniqueRace_ReturnsFalse()
    {
        Assert.IsFalse(_sut.IsRaceAllowed(SauronId));
    }

    [TestMethod]
    public void IsRaceAllowed_PlayableRaces_ReturnsTrue()
    {
        // The headline requirement: a bearer keeps its own race, so every ordinary
        // battlefield race must be eligible to raise a banner.
        Assert.IsTrue(_sut.IsRaceAllowed(HumanId), "human");
        Assert.IsTrue(_sut.IsRaceAllowed(DwarfId), "dwarf");
        Assert.IsTrue(_sut.IsRaceAllowed(OrcId), "orc");
        Assert.IsTrue(_sut.IsRaceAllowed(ElfId), "elf");
        Assert.IsTrue(_sut.IsRaceAllowed(UrukId), "uruk");
        Assert.IsTrue(_sut.IsRaceAllowed(GoblinId), "goblin");
    }

    [TestMethod]
    public void IsRaceAllowed_InvalidRaceId_ReturnsFalseAndDoesNotTrustFallbackName()
    {
        // GetRaceNameFromId(99) coerces to "human", which is NOT excluded — so a naive
        // lookup-then-check would wrongly allow it. Validate before lookup, fail closed.
        Assert.IsFalse(_sut.IsRaceAllowed(UnknownId));
    }

    [TestMethod]
    public void IsRaceAllowed_ExcludedRaceDifferentCase_ReturnsFalse()
    {
        _config.ExcludedRaces = new List<string> { "CAVE_TROLL" };

        Assert.IsFalse(_sut.IsRaceAllowed(CaveTrollId));
    }

    [TestMethod]
    public void IsRaceAllowed_EmptyExclusionList_AllowsEveryKnownRace()
    {
        _config.ExcludedRaces = new List<string>();

        Assert.IsTrue(_sut.IsRaceAllowed(CaveTrollId));
    }

    [TestMethod]
    public void IsRaceAllowed_NullEntryInExclusionList_DoesNotThrow()
    {
        _config.ExcludedRaces = new List<string> { null!, "cave_troll" };

        Assert.IsFalse(_sut.IsRaceAllowed(CaveTrollId));
        Assert.IsTrue(_sut.IsRaceAllowed(OrcId));
    }

    // ---- ResolveBannerItemId --------------------------------------------------

    [TestMethod]
    public void ResolveBannerItemId_MappedCulture_ReturnsMappedItem()
    {
        _config.CultureBanners = new Dictionary<string, string> { { "gondor", "standard_of_duty_t1" } };

        Assert.AreEqual("standard_of_duty_t1", _sut.ResolveBannerItemId("gondor"));
    }

    [TestMethod]
    public void ResolveBannerItemId_UnmappedCulture_ReturnsDefault()
    {
        _config.CultureBanners = new Dictionary<string, string>();
        _config.DefaultBannerItemId = "standard_of_duty_t1";

        Assert.AreEqual("standard_of_duty_t1", _sut.ResolveBannerItemId("some_unknown_culture"));
    }

    [TestMethod]
    public void ResolveBannerItemId_NullCulture_ReturnsDefault()
    {
        _config.DefaultBannerItemId = "standard_of_duty_t1";

        Assert.AreEqual("standard_of_duty_t1", _sut.ResolveBannerItemId(null));
    }

    [TestMethod]
    public void ResolveBannerItemId_EmptyDefaultAndUnmappedCulture_ReturnsNull()
    {
        _config.CultureBanners = new Dictionary<string, string>();
        _config.DefaultBannerItemId = "";

        Assert.IsNull(_sut.ResolveBannerItemId("some_unknown_culture"));
    }

    [TestMethod]
    public void ResolveBannerItemId_CultureMappedToEmpty_ReturnsNull()
    {
        // Explicit opt-out: map a culture to "" to give it no banner at all.
        _config.CultureBanners = new Dictionary<string, string> { { "looters", "" } };

        Assert.IsNull(_sut.ResolveBannerItemId("looters"));
    }

    [TestMethod]
    public void ResolveBannerItemId_FeatureDisabled_ReturnsNull()
    {
        _config.Enabled = false;

        Assert.IsNull(_sut.ResolveBannerItemId("gondor"));
    }

    // ---- ResolveMajorityCultureId (Codex review 2026-07-16 MED) ---------------
    // Formation.GetFirstUnit() is Arrangement.GetAllUnits()[0] — not a culture owner. A
    // mixed-culture formation must not fly whichever standard landed in slot 0.

    [TestMethod]
    public void ResolveMajorityCultureId_SingleCulture_ReturnsIt()
    {
        Assert.AreEqual("gondor", _sut.ResolveMajorityCultureId(new[] { "gondor", "gondor", "gondor" }));
    }

    [TestMethod]
    public void ResolveMajorityCultureId_MixedCulture_ReturnsMajorityNotFirst()
    {
        // Slot 0 is a lone Rohirrim in an otherwise Gondorian line — the formation flies Gondor.
        var ids = new[] { "vlandia", "gondor", "gondor", "gondor" };

        Assert.AreEqual("gondor", _sut.ResolveMajorityCultureId(ids));
    }

    [TestMethod]
    public void ResolveMajorityCultureId_Tie_IsDeterministicRegardlessOfOrder()
    {
        // A 50/50 formation must pick the same standard every deployment, never one that
        // depends on arrangement order.
        var a = _sut.ResolveMajorityCultureId(new[] { "vlandia", "gondor" });
        var b = _sut.ResolveMajorityCultureId(new[] { "gondor", "vlandia" });

        Assert.AreEqual(a, b);
        Assert.AreEqual("gondor", a, "ordinal tie-break should pick the lexicographically first id");
    }

    [TestMethod]
    public void ResolveMajorityCultureId_EmptyOrNull_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveMajorityCultureId(new string?[0]));
        Assert.IsNull(_sut.ResolveMajorityCultureId(null!));
    }

    [TestMethod]
    public void ResolveMajorityCultureId_NullAndEmptyEntries_AreIgnored()
    {
        var ids = new[] { null, "", "gondor", null };

        Assert.AreEqual("gondor", _sut.ResolveMajorityCultureId(ids));
    }

    [TestMethod]
    public void ResolveMajorityCultureId_AllEntriesNull_ReturnsNull()
    {
        Assert.IsNull(_sut.ResolveMajorityCultureId(new string?[] { null, null }));
    }

    // ---- IsFormationGroupAllowed (infantry-only gate, 2026-07-16 feedback) -----
    // A bearer swaps its weapons for a banner + sidearm, so converting an archer or cavalry
    // troop wastes it. Only troops whose default_group is Infantry may be picked.

    [TestMethod]
    public void IsFormationGroupAllowed_DefaultConfig_AllowsInfantryOnly()
    {
        Assert.IsTrue(_sut.IsFormationGroupAllowed(FormationClass.Infantry), "infantry");
        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Ranged), "ranged");
        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Cavalry), "cavalry");
        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.HorseArcher), "horse archer");
    }

    [TestMethod]
    public void IsFormationGroupAllowed_ConfiguredClass_Allowed()
    {
        _config.AllowedFormationGroups = new List<string> { "Infantry", "Cavalry" };

        Assert.IsTrue(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
        Assert.IsTrue(_sut.IsFormationGroupAllowed(FormationClass.Cavalry));
        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Ranged));
    }

    [TestMethod]
    public void IsFormationGroupAllowed_CaseInsensitive()
    {
        _config.AllowedFormationGroups = new List<string> { "infantry" };

        Assert.IsTrue(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
    }

    [TestMethod]
    public void IsFormationGroupAllowed_EmptyList_AllowsNothing()
    {
        // A genuinely empty list means no class is eligible. The provider reverts an empty
        // shipped list to the default, but the service must honour whatever it is handed.
        _config.AllowedFormationGroups = new List<string>();

        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
    }

    [TestMethod]
    public void IsFormationGroupAllowed_NullList_AllowsNothing()
    {
        _config.AllowedFormationGroups = null!;

        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
    }

    [TestMethod]
    public void IsFormationGroupAllowed_UnknownEntryIgnored_DoesNotThrow()
    {
        _config.AllowedFormationGroups = new List<string> { "Infantry", "NotAClass", null! };

        Assert.IsTrue(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Ranged));
    }

    [TestMethod]
    public void IsFormationGroupAllowed_FeatureDisabled_AllowsNothing()
    {
        _config.Enabled = false;

        Assert.IsFalse(_sut.IsFormationGroupAllowed(FormationClass.Infantry));
    }

    // ---- ExcludedRaces validation (Codex review 2026-07-16 LOW) ---------------

    [TestMethod]
    public void IsRaceAllowed_UnknownExcludedRaceName_WarnsOnce()
    {
        // A typo'd exclusion fails OPEN — it never matches, so the race it was meant to bar stays
        // eligible. Nothing at load time can catch it (FaceGen isn't populated yet), so warn here.
        _config.ExcludedRaces = new List<string> { "cave_trol" };
        _raceManager.IsValidRaceName("cave_trol").Returns(false);

        _sut.IsRaceAllowed(OrcId);
        _sut.IsRaceAllowed(DwarfId);

        _logger.Received(1).LogWarning(Arg.Is<string>(m => m.Contains("cave_trol")));
    }

    [TestMethod]
    public void IsRaceAllowed_AllExcludedRaceNamesKnown_DoesNotWarn()
    {
        _sut.IsRaceAllowed(OrcId);

        _logger.DidNotReceive().LogWarning(Arg.Any<string>());
    }

    // ---- HasRenderableHeraldry (siege native-CTD guard, 2026-07-23) -----------
    // SetFormationBanner forces a native banner-tableau rebuild that renders the bearer's heraldry
    // (agent.Origin.Banner). When that heraldry has no BannerData — a null origin Banner or an empty
    // BannerDataList, reported by the MissionLogic as a count of 0 — the rebuild has nothing to draw
    // and access-violates (0xC0000005 @ TaleWorlds.Native.dll+0x28ac0e; the 100%-repro siege
    // OrderOfBattle CTD at sturgia_town_c). The engine picks the bearer by priority from ALL
    // candidates, not slot 0, so ANY zero-count candidate must fail the whole formation.
    // See rca-banner-bearers-siege-ctd-2026-07-23.md.

    [TestMethod]
    public void HasRenderableHeraldry_AllCandidatesHaveData_ReturnsTrue()
    {
        // A homogeneous formation of real clan/kingdom troops: every coat of arms has several
        // BannerData entries (background + icons).
        Assert.IsTrue(_sut.HasRenderableHeraldry(new[] { 3, 4, 3 }));
    }

    [TestMethod]
    public void HasRenderableHeraldry_SingleCandidateWithData_ReturnsTrue()
    {
        Assert.IsTrue(_sut.HasRenderableHeraldry(new[] { 1 }));
    }

    [TestMethod]
    public void HasRenderableHeraldry_AnyCandidateZero_ReturnsFalse()
    {
        // THE MIXED-ORIGIN CRASH CASE: slot 0 has a valid banner, but a custom-faction troop with a
        // null origin Banner (count 0) is present and could be the priority-picked bearer → skip.
        Assert.IsFalse(_sut.HasRenderableHeraldry(new[] { 3, 0, 5 }));
    }

    [TestMethod]
    public void HasRenderableHeraldry_AllCandidatesZero_ReturnsFalse()
    {
        Assert.IsFalse(_sut.HasRenderableHeraldry(new[] { 0, 0 }));
    }

    [TestMethod]
    public void HasRenderableHeraldry_EmptyCandidateSet_ReturnsFalse()
    {
        // Nothing to confirm renderable (e.g. all units detached and no first unit) → skip, not assign.
        Assert.IsFalse(_sut.HasRenderableHeraldry(new int[0]));
    }

    [TestMethod]
    public void HasRenderableHeraldry_NullList_ReturnsFalse()
    {
        Assert.IsFalse(_sut.HasRenderableHeraldry(null!));
    }

    [TestMethod]
    public void HasRenderableHeraldry_NegativeCount_ReturnsFalse()
    {
        // Defensive: a count should never be negative, but any non-positive entry must fail the gate.
        Assert.IsFalse(_sut.HasRenderableHeraldry(new[] { 3, -1 }));
    }

    // ---- Pass-through ---------------------------------------------------------

    [TestMethod]
    public void IsEnabled_ReflectsConfig()
    {
        _config.Enabled = true;
        Assert.IsTrue(_sut.IsEnabled);

        _config.Enabled = false;
        Assert.IsFalse(_sut.IsEnabled);
    }

    [TestMethod]
    public void MinimumFormationTroopCount_ReflectsConfig()
    {
        _config.MinimumFormationTroopCount = 7;

        Assert.AreEqual(7, _sut.MinimumFormationTroopCount);
    }
}
