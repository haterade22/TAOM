using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Domain;
using TAOM.Features.HeroRace;
using TAOM.Features.HeroRace.Configuration;
using TaleWorlds.Library;
using static TAOM.Features.HeroRace.Configuration.RacePositionConfig;

namespace TAOM.Tests.Features.HeroRace;

// Pins the rule that decides which config row an entity in the character tableau reads:
//
//   the CHARACTER always reads "<race>", the MOUNT always reads "mount_<race>",
//   and swapping their places changes only which spawn origin the caller passes in.
//
// The deleted CharacterTableauService chose the row by PLACE instead, so swapping handed the horse
// the rider's offsets. That was never observable (the service was dead code) but it is wrong against
// shipped data: cave_troll has a plain row with Zoom -4.0 and no mount row, so place-based selection
// would have pushed a horse four metres away and left the troll unframed.
[TestClass]
public class TableauPositionServiceTests
{
    private const int DwarfRaceId = 3;

    private IRaceManager _raceManager;
    private IRacePositionStore _store;
    private TableauPositionService _sut;

    [TestInitialize]
    public void Setup()
    {
        _raceManager = Substitute.For<IRaceManager>();
        _store = Substitute.For<IRacePositionStore>();
        _raceManager.IsValidRaceId(DwarfRaceId).Returns(true);
        _raceManager.GetRaceNameFromId(DwarfRaceId).Returns("dwarf");
        _sut = new TableauPositionService(_raceManager, _store);
    }

    private static RacePositionConfigItem Item(float h, float v, float z)
        => new RacePositionConfigItem { Race = "dwarf", Horizontal = h, Vertical = v, Zoom = z };

    private static Vec3 Base() => new Vec3(10f, 20f, 30f);

    // --- Axis mapping ------------------------------------------------------------------------

    // Horizontal -> y, Vertical -> z, Zoom -> x. Deliberately not the intuitive order; it is
    // camera-relative naming inherited from the config format. This is now the only pin on that
    // mapping, so getting it wrong here fails silently in-game rather than loudly in CI.
    [TestMethod]
    public void TryGetOrigin_Character_AppliesCameraRelativeAxisMapping()
    {
        _store.ResolveAvatar("dwarf").Returns(Item(1f, 2f, 3f));

        var applied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Character, out var origin);

        Assert.IsTrue(applied);
        Assert.AreEqual(10f + 3f, origin.x, "Zoom offsets x");
        Assert.AreEqual(20f + 1f, origin.y, "Horizontal offsets y");
        Assert.AreEqual(30f + 2f, origin.z, "Vertical offsets z");
    }

    // --- Row selection follows the ENTITY -----------------------------------------------------

    [TestMethod]
    public void TryGetOrigin_Character_ReadsThePlainRaceRow()
    {
        _store.ResolveAvatar("dwarf").Returns(Item(0f, 0.5f, 0f));

        Assert.IsTrue(_sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Character, out var origin));

        Assert.AreEqual(30.5f, origin.z);
        _store.DidNotReceive().ResolveAvatarMount(Arg.Any<string>());
    }

    [TestMethod]
    public void TryGetOrigin_Mount_ReadsTheMountRaceRow()
    {
        _store.ResolveAvatarMount("dwarf").Returns(Item(0f, 0.25f, 0f));

        Assert.IsTrue(_sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Mount, out var origin));

        Assert.AreEqual(30.25f, origin.z);
        _store.DidNotReceive().ResolveAvatar(Arg.Any<string>());
    }

    // --- Unconfigured races keep vanilla framing ----------------------------------------------

    [TestMethod]
    public void TryGetOrigin_RaceHasNoConfigRow_ReturnsFalseAndLeavesOriginAtBase()
    {
        _store.ResolveAvatar("dwarf").Returns((RacePositionConfigItem)null);

        var applied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Character, out var origin);

        Assert.IsFalse(applied, "An unconfigured race must keep the vanilla frame.");
        Assert.AreEqual(Base().x, origin.x);
        Assert.AreEqual(Base().y, origin.y);
        Assert.AreEqual(Base().z, origin.z);
    }

    [TestMethod]
    public void TryGetOrigin_MountRowMissingWhileRaceRowExists_DoesNotFallBackToTheRaceRow()
    {
        _store.ResolveAvatar("dwarf").Returns(Item(0f, 9f, 0f));
        _store.ResolveAvatarMount("dwarf").Returns((RacePositionConfigItem)null);

        var applied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Mount, out _);

        Assert.IsFalse(applied,
            "mount_<race> and <race> are separate rows. Falling back would move the mount by the "
            + "character offset, which is how a mounted dwarf ends up sunk into the horse.");
    }

    // --- Invalid race ids ---------------------------------------------------------------------

    // GetRaceNameFromId returns "human" for unknown ids (RaceManager fallback). Looking that up
    // would silently apply human offsets to a junk race id. The "Lookup Functions With Fallbacks"
    // rule requires validating the id BEFORE the lookup.
    [TestMethod]
    public void TryGetOrigin_InvalidRaceId_ReturnsFalseWithoutConsultingTheStore()
    {
        _raceManager.IsValidRaceId(999).Returns(false);

        var applied = _sut.TryGetOrigin(Base(), 999, TableauEntity.Character, out var origin);

        Assert.IsFalse(applied);
        Assert.AreEqual(Base().z, origin.z);
        _store.DidNotReceive().ResolveAvatar(Arg.Any<string>());
        _raceManager.DidNotReceive().GetRaceNameFromId(999);
    }

    [TestMethod]
    public void TryGetOrigin_NegativeRaceId_ReturnsFalse()
    {
        _raceManager.IsValidRaceId(-1).Returns(false);
        Assert.IsFalse(_sut.TryGetOrigin(Base(), -1, TableauEntity.Character, out _));
    }

    // The regression this rewrite exists for. A race authored with only a plain row (the natural
    // first step, and exactly how cave_troll ships) must keep that row on its character no matter
    // how the places are arranged, and must never hand it to the horse.
    [TestMethod]
    public void TryGetOrigin_RaceWithOnlyAPlainRow_NeverGivesThatRowToTheMount()
    {
        _store.ResolveAvatar("dwarf").Returns(Item(0f, -0.6f, -4.0f));
        _store.ResolveAvatarMount("dwarf").Returns((RacePositionConfigItem)null);

        var characterApplied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Character, out var characterOrigin);
        var mountApplied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Mount, out _);

        Assert.IsTrue(characterApplied, "The character keeps its own row.");
        Assert.AreEqual(30f - 0.6f, characterOrigin.z);
        Assert.IsFalse(mountApplied, "The mount has no row of its own and must stay at vanilla framing.");
    }

    // Skip-guard exhaustion: a valid id whose name comes back empty must not build a lookup key.
    [TestMethod]
    public void TryGetOrigin_ValidIdButEmptyRaceName_ReturnsFalseWithoutConsultingTheStore()
    {
        _raceManager.IsValidRaceId(7).Returns(true);
        _raceManager.GetRaceNameFromId(7).Returns(string.Empty);

        Assert.IsFalse(_sut.TryGetOrigin(Base(), 7, TableauEntity.Character, out _));
        _store.DidNotReceive().ResolveAvatar(Arg.Any<string>());
    }

    [TestMethod]
    public void TryGetOrigin_ZeroOffsetsRow_StillReportsAppliedAndIsANoOp()
    {
        _store.ResolveAvatar("dwarf").Returns(Item(0f, 0f, 0f));

        var applied = _sut.TryGetOrigin(Base(), DwarfRaceId, TableauEntity.Character, out var origin);

        Assert.IsTrue(applied);
        Assert.AreEqual(Base().x, origin.x);
        Assert.AreEqual(Base().y, origin.y);
        Assert.AreEqual(Base().z, origin.z);
    }
}
