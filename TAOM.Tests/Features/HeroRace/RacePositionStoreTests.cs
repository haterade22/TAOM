using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Core.Infrastructure;
using TAOM.Core.Logging;
using TAOM.Features.HeroRace;

namespace TAOM.Tests.Features.HeroRace;

// One owner for both race-framing configs. Before this store there were THREE loaders of the same
// two files (CharacterSpawnerService built its own lookup, the dead CharacterTableauService built
// another, the dead RacePositionConfigurationService built a third and merged the two files
// together). The merge was the interesting bug: the avatar and image configs carry different
// tuning for the same race on purpose, so a merged lookup framed the 2D portrait with 3D numbers
// whenever the image file lacked a row.
[TestClass]
public class RacePositionStoreTests
{
    private string _configDir;
    private IPathService _pathService;
    private IModLogger _logger;

    [TestInitialize]
    public void Setup()
    {
        _configDir = Path.Combine(Path.GetTempPath(), "taom-raceportraits-" + Path.GetRandomFileName());
        Directory.CreateDirectory(_configDir);
        _pathService = Substitute.For<IPathService>();
        _pathService.ConfigPath.Returns(_configDir + Path.DirectorySeparatorChar);
        _logger = Substitute.For<IModLogger>();
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_configDir, recursive: true); } catch { /* temp dir, best effort */ }
    }

    private void WriteConfigFile(string name, string json)
        => File.WriteAllText(Path.Combine(_configDir, name + ".json"), json);

    private RacePositionStore CreateStore() => new RacePositionStore(_pathService, _logger);

    // --- Per-file separation ------------------------------------------------------------------

    [TestMethod]
    public void ResolveAvatar_ReadsOnlyTheAvatarFile()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.15,\"Zoom\":-0.1}]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.9,\"Zoom\":0}]}");

        var store = CreateStore();

        Assert.AreEqual(0.15f, store.ResolveAvatar("dwarf").Vertical);
        Assert.AreEqual(0.9f, store.ResolveImage("dwarf").Vertical);
    }

    [TestMethod]
    public void ResolveImage_RaceOnlyInAvatarFile_ReturnsNullRatherThanBorrowingTheAvatarRow()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"orc\",\"Horizontal\":0,\"Vertical\":0.5,\"Zoom\":0}]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[]}");

        var store = CreateStore();

        Assert.IsNull(store.ResolveImage("orc"),
            "The two surfaces are tuned independently; borrowing across files is the merge bug.");
    }

    // --- Lookup semantics ---------------------------------------------------------------------

    [TestMethod]
    public void ResolveAvatar_IsCaseInsensitive()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"cave_troll\",\"Horizontal\":0,\"Vertical\":-0.6,\"Zoom\":-4.0}]}");

        var store = CreateStore();

        Assert.IsNotNull(store.ResolveAvatar("CAVE_TROLL"));
        Assert.AreEqual(-4.0f, store.ResolveAvatar("Cave_Troll").Zoom);
    }

    [TestMethod]
    public void ResolveAvatarMount_LooksUpTheMountPrefixedRow()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"mount_dwarf\",\"Horizontal\":0,\"Vertical\":0.1,\"Zoom\":0}]}");

        var store = CreateStore();

        Assert.AreEqual(0.1f, store.ResolveAvatarMount("dwarf").Vertical);
        Assert.IsNull(store.ResolveAvatar("dwarf"), "mount_dwarf must not answer a plain dwarf lookup.");
    }

    [TestMethod]
    public void ResolveAvatar_UnknownRace_ReturnsNull()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[]}");
        Assert.IsNull(CreateStore().ResolveAvatar("balrog"));
    }

    [TestMethod]
    public void ResolveAvatar_NullOrEmptyRace_ReturnsNullWithoutThrowing()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0}]}");
        var store = CreateStore();

        Assert.IsNull(store.ResolveAvatar(null));
        Assert.IsNull(store.ResolveAvatar(""));
    }

    // --- Missing / malformed files ------------------------------------------------------------

    [TestMethod]
    public void Construction_MissingFiles_YieldsEmptyStoreRatherThanThrowing()
    {
        var store = CreateStore();

        Assert.IsNull(store.ResolveAvatar("dwarf"));
        Assert.IsNull(store.ResolveImage("dwarf"));
        Assert.AreEqual(0, store.List(RacePositionSurface.Avatar).Count);
    }

    [TestMethod]
    public void Construction_MalformedJson_YieldsEmptyStoreRatherThanThrowing()
    {
        WriteConfigFile("CharacterAvatarPatch", "{ this is not json");

        var store = CreateStore();

        Assert.AreEqual(0, store.List(RacePositionSurface.Avatar).Count);
    }

    // Validation is wired at the load boundary, not only in the standalone validator.
    [TestMethod]
    public void Construction_NaNOffsetInFile_IsDroppedAtLoad()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":NaN,\"Zoom\":0}]}");

        var store = CreateStore();

        Assert.IsNull(store.ResolveAvatar("dwarf"), "A NaN row must not survive into the lookup.");
        _logger.ReceivedWithAnyArgs().LogWarning(default);
    }

    // --- Mutation + save (the tuner path) -----------------------------------------------------

    [TestMethod]
    public void GetOrAdd_UnknownRace_CreatesAZeroedRowThatResolves()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[]}");
        var store = CreateStore();

        var item = store.GetOrAdd(RacePositionSurface.Avatar, "uruk");

        Assert.IsNotNull(item);
        Assert.AreEqual("uruk", item.Race);
        Assert.AreEqual(0f, item.Vertical);
        Assert.AreSame(item, store.ResolveAvatar("uruk"), "A newly added row must be immediately live.");
    }

    [TestMethod]
    public void GetOrAdd_ExistingRace_ReturnsTheSameInstanceSoEditsAreLive()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.15,\"Zoom\":0}]}");
        var store = CreateStore();

        var item = store.GetOrAdd(RacePositionSurface.Avatar, "DWARF");
        item.Vertical = 0.42f;

        Assert.AreEqual(0.42f, store.ResolveAvatar("dwarf").Vertical);
    }

    [TestMethod]
    public void Save_ThenReload_RoundTripsEditedValues()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "cave_troll").Zoom = -4.0f;
        store.GetOrAdd(RacePositionSurface.Image, "goblin").Vertical = 0.2f;
        store.Save();
        store.Reload();

        Assert.AreEqual(-4.0f, store.ResolveAvatar("cave_troll").Zoom);
        Assert.AreEqual(0.2f, store.ResolveImage("goblin").Vertical);
        Assert.IsNull(store.ResolveImage("cave_troll"), "Save must keep the two surfaces in their own files.");
    }

    [TestMethod]
    public void Save_WritesBothFilesToTheConfigDirectory()
    {
        var store = CreateStore();
        store.GetOrAdd(RacePositionSurface.Avatar, "elf").Vertical = 0.05f;

        store.Save();

        Assert.IsTrue(File.Exists(Path.Combine(_configDir, "CharacterAvatarPatch.json")));
        Assert.IsTrue(File.Exists(Path.Combine(_configDir, "CharacterImagePatch.json")));
    }

    [TestMethod]
    public void Reload_DiscardsUnsavedEdits()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.15,\"Zoom\":0}]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "dwarf").Vertical = 5f;
        store.Reload();

        Assert.AreEqual(0.15f, store.ResolveAvatar("dwarf").Vertical);
    }

    [TestMethod]
    public void List_ReturnsRowsForTheRequestedSurfaceOnly()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0},{\"Race\":\"mount_dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0}]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0}]}");

        var store = CreateStore();

        Assert.AreEqual(2, store.List(RacePositionSurface.Avatar).Count);
        Assert.AreEqual(1, store.List(RacePositionSurface.Image).Count);
    }

    // --- Finiteness gate at the point of service ----------------------------------------------

    // The load-time validator only runs on Reload. The in-game tuner creates and edits rows
    // afterwards, and every consumer feeds a row straight to a native SetFrame, so the store
    // re-checks on the way out. It lives here rather than in one consumer so BOTH the 3D tableau
    // and the 2D portrait paths inherit it.
    [TestMethod]
    public void ResolveAvatar_RowMutatedToNaNAfterLoad_IsNotServed()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.15,\"Zoom\":0}]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "dwarf").Vertical = float.NaN;

        Assert.IsNull(store.ResolveAvatar("dwarf"),
            "A non-finite row must degrade to vanilla framing, not reach a native SetFrame.");
    }

    [TestMethod]
    public void ResolveImage_RowMutatedToInfinityAfterLoad_IsNotServed()
    {
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0}]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Image, "dwarf").Zoom = float.PositiveInfinity;

        Assert.IsNull(store.ResolveImage("dwarf"));
    }

    [TestMethod]
    public void ResolveAvatarMount_NonFiniteMountRow_IsNotServed()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"mount_dwarf\",\"Horizontal\":0,\"Vertical\":0,\"Zoom\":0}]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "mount_dwarf").Horizontal = float.NegativeInfinity;

        Assert.IsNull(store.ResolveAvatarMount("dwarf"));
    }

    // Skip-guard exhaustion for the documented null-return contract callers branch on.
    [TestMethod]
    public void GetOrAdd_NullOrWhitespaceRace_ReturnsNull()
    {
        var store = CreateStore();

        Assert.IsNull(store.GetOrAdd(RacePositionSurface.Avatar, null));
        Assert.IsNull(store.GetOrAdd(RacePositionSurface.Avatar, "   "));
    }

    // Save writes the SAME objects the tuner mutated, through an atomic swap that keeps the old file.
    [TestMethod]
    public void Save_KeepsThePreviousFileAsPrev()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[{\"Race\":\"dwarf\",\"Horizontal\":0,\"Vertical\":0.15,\"Zoom\":0}]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "dwarf").Vertical = 0.42f;
        store.Save();

        Assert.IsTrue(File.Exists(Path.Combine(_configDir, "CharacterAvatarPatch.json.prev")),
            "A save over an existing config must leave the previous file recoverable.");
        Assert.IsFalse(File.Exists(Path.Combine(_configDir, "CharacterAvatarPatch.json.tmp")),
            "The temporary file must not survive a successful swap.");
    }

    // --- Two-phase save (Codex P3) ------------------------------------------------------------

    // The pair is reloaded as a set, so a per-file swap that half-commits leaves new avatar data
    // beside old image data with nothing to say so. Both files are staged before either is swapped.
    [TestMethod]
    public void Save_LeavesNoTemporaryFilesBehind()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "dwarf").Vertical = 0.15f;
        store.GetOrAdd(RacePositionSurface.Image, "dwarf").Vertical = 0.20f;
        store.Save();

        Assert.AreEqual(0, Directory.GetFiles(_configDir, "*.tmp").Length,
            "A staged temp file survived the commit.");
    }

    [TestMethod]
    public void Save_CommitsBothSurfacesTogether()
    {
        WriteConfigFile("CharacterAvatarPatch", "{\"Items\":[]}");
        WriteConfigFile("CharacterImagePatch", "{\"Items\":[]}");
        var store = CreateStore();

        store.GetOrAdd(RacePositionSurface.Avatar, "dwarf").Vertical = 0.15f;
        store.GetOrAdd(RacePositionSurface.Image, "dwarf").Vertical = 0.20f;
        store.Save();
        store.Reload();

        Assert.AreEqual(0.15f, store.ResolveAvatar("dwarf").Vertical);
        Assert.AreEqual(0.20f, store.ResolveImage("dwarf").Vertical,
            "Both surfaces must land, or a reload silently mixes new and old data.");
    }
}
