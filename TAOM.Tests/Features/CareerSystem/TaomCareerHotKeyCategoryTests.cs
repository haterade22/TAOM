using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.CareerSystem;

/// <summary>
/// Pins the engine contracts that decide whether the career ability key is rebindable at all. Every
/// one of them fails silently or only in a live game: a wrong id mislabels the Options row, a small
/// slot count throws on construction, and a wrong MainCategoryId hides the key from the Keybindings
/// screen entirely. Mirrors TaomTimeControlHotKeyCategoryTests, which pins the same contracts for
/// the campaign-map time controls.
/// </summary>
[TestClass]
public class TaomCareerHotKeyCategoryTests
{
    private TaomCareerHotKeyCategory _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TaomCareerHotKeyCategory();
    }

    private GameKey[] RegisteredKeys =>
        _sut.RegisteredGameKeys.Where(k => k != null).ToArray();

    [TestMethod]
    public void Ctor_RegistersTheAbilityActivationKey()
    {
        // Arrange + Act in Setup

        // Assert
        CollectionAssert.AreEquivalent(
            new[] { TaomCareerHotKeyCategory.AbilityActivationKeyId },
            RegisteredKeys.Select(k => k.Id).ToArray());
    }

    [TestMethod]
    public void EveryKeyId_IsAtLeastTotalGameKeyCount_SoOptionsLabelsDoNotCollideWithVanilla()
    {
        // KeyOptionVM builds its localization id from ((GameKeyDefinition)gameKey.Id).ToString(),
        // NOT from StringId. An id below TotalGameKeyCount resolves to a vanilla enum NAME and would
        // reuse vanilla's str_key_name entry; at or above it, the id renders as a bare number.
        foreach (var key in RegisteredKeys)
        {
            Assert.IsTrue(
                key.Id >= (int)GameKeyDefinition.TotalGameKeyCount,
                $"GameKey {key.StringId} has id {key.Id}, below TotalGameKeyCount "
                + $"({(int)GameKeyDefinition.TotalGameKeyCount}). Its Options label would collide "
                + "with a vanilla game key localization id.");
        }
    }

    [TestMethod]
    public void KeyId_DoesNotCollideWithTheTimeControlCategoryIds()
    {
        // Both categories are TAOM's and both are hand-numbered. The ids live in different contexts
        // so a clash would not throw, but it would make the two features indistinguishable in a
        // BannerlordGameKeys.xml diff and in any future shared tooling.
        var timeControlIds = new[]
        {
            TaomTimeControlHotKeyCategory.FastForwardKeyId,
            TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
            TaomTimeControlHotKeyCategory.TurboKeyId,
        };

        CollectionAssert.DoesNotContain(
            timeControlIds, TaomCareerHotKeyCategory.AbilityActivationKeyId);
    }

    [TestMethod]
    public void RegisteredSlotCount_ExceedsLargestKeyId()
    {
        // GameKeyContext.RegisterGameKey is an INDEXED write into a list pre-filled with
        // gameKeysCount nulls, so a count at or below the largest id throws
        // ArgumentOutOfRangeException at construction time.
        var largestId = RegisteredKeys.Max(k => k.Id);

        Assert.IsTrue(
            _sut.RegisteredGameKeys.Count > largestId,
            $"Context was sized for {_sut.RegisteredGameKeys.Count} slots but the largest key id is "
            + $"{largestId}. RegisterGameKey would throw.");
    }

    [TestMethod]
    public void EveryKey_UsesActionMainCategory_SoTheOptionsScreenRendersIt()
    {
        // GameKeyOptionCategoryVM only renders keys whose MainCategoryId is in the fixed allowlist
        // returned by OptionsProvider.GetGameKeyCategoriesList. ActionCategory is the in-mission
        // group on that list, which is why this needs no Harmony patch.
        foreach (var key in RegisteredKeys)
        {
            Assert.AreEqual(
                GameKeyMainCategories.ActionCategory, key.MainCategoryId,
                $"GameKey {key.StringId} would not appear in Options > Keybindings.");
        }
    }

    [TestMethod]
    public void ContextType_IsDefault_SoTheOptionsScreenRendersIt()
    {
        // The same VM skips any context whose Type is not Default.
        Assert.AreEqual(GameKeyContext.GameKeyContextType.Default, _sut.Type);
    }

    [TestMethod]
    public void EveryKey_GroupIdMatchesCategoryId_SoLocalizationIdsResolve()
    {
        // The lookup is str_key_name.<GroupId>_<id>.
        foreach (var key in RegisteredKeys)
        {
            Assert.AreEqual(TaomCareerHotKeyCategory.CategoryId, key.GroupId);
        }
    }

    [TestMethod]
    public void Default_IsV_SoRebindingShipsNoBehaviourChange()
    {
        // The ability key was hardcoded to V before it was rebindable. A player who never opens
        // Options must not notice this change.
        Assert.AreEqual(
            InputKey.V,
            RegisteredKeys.Single(k => k.Id == TaomCareerHotKeyCategory.AbilityActivationKeyId)
                .KeyboardKey.InputKey);
    }

    [TestMethod]
    public void EveryShippedKey_HasANonNullKeyboardKey()
    {
        // AbilityInputAdapter.BoundKey reads gameKey?.KeyboardKey?.InputKey. If the shipped default
        // ever became Invalid, KeyboardKey would be null and the ability would be silently
        // unactivatable rather than merely rebound.
        foreach (var key in RegisteredKeys)
        {
            Assert.IsNotNull(key.KeyboardKey, $"GameKey {key.StringId} ships unbound.");
        }
    }

    [TestMethod]
    public void GameKeyClearedInOptions_KeepsANonNullKeyHoldingInvalid()
    {
        // The shape a player actually produces: GameKeyOptionVM.OnDone calls
        // Key.ChangeKey(InputKey.Invalid) on the EXISTING Key rather than nulling it. So a null check
        // alone would miss a cleared binding, which is why BoundKey compares the InputKey too.
        var key = RegisteredKeys.Single(
            k => k.Id == TaomCareerHotKeyCategory.AbilityActivationKeyId);
        key.KeyboardKey.ChangeKey(InputKey.Invalid);

        Assert.IsNotNull(key.KeyboardKey);
        Assert.AreEqual(InputKey.Invalid, key.KeyboardKey.InputKey);
    }

    [TestMethod]
    public void GameKeyConstructedUnbound_LeavesKeyboardKeyNull()
    {
        // The OTHER shape an unbound key takes, and the reason BoundKey needs the `?.KeyboardKey?`
        // guard rather than only comparing the InputKey: the GameKey ctor stores null instead of a
        // Key wrapping Invalid, so a cleared-binding check alone would NRE on a key that shipped
        // unbound.
        var unbound = new GameKey(
            TaomCareerHotKeyCategory.AbilityActivationKeyId, "Probe",
            TaomCareerHotKeyCategory.CategoryId,
            InputKey.Invalid, GameKeyMainCategories.ActionCategory);

        Assert.IsNull(unbound.KeyboardKey);
    }

    [TestMethod]
    public void EveryKey_HasNameAndDescriptionStringsInGlobalStrings()
    {
        // The Options screen resolves each row through Module.CurrentModule.GlobalTextManager, which
        // is populated ONLY by scanning modules for the literal path ModuleData/global_strings.xml.
        // A missing row is not an error: it renders the raw lookup id in the keybinding list.
        var path = Path.Combine(
            FindRepoRoot(), "Main", "_Module", "ModuleData", "global_strings.xml");
        Assert.IsTrue(File.Exists(path), $"global_strings.xml not found at {path}");

        var ids = XDocument.Load(path).Root
            .Elements("string")
            .Select(e => (string)e.Attribute("id"))
            .ToList();

        foreach (var key in RegisteredKeys)
        {
            var suffix = $"{TaomCareerHotKeyCategory.CategoryId}_{key.Id}";
            Assert.IsTrue(
                ids.Contains($"str_key_name.{suffix}"),
                $"global_strings.xml has no str_key_name.{suffix}; the Options row would show that "
                + "raw id instead of a name.");
            Assert.IsTrue(
                ids.Contains($"str_key_description.{suffix}"),
                $"global_strings.xml has no str_key_description.{suffix}.");
        }
    }

    [TestMethod]
    public void AbilityInputAdapter_WithNoRegisteredCategory_ReportsNotPressed()
    {
        // HotKeyManager holds no TAOM category in a unit-test process, so the adapter must fall
        // through to Invalid and return false WITHOUT reaching the static Input.IsKeyPressed. This is
        // the same latch-on-miss the map adapter uses: registration happens once in
        // OnSubModuleLoad, so a miss means the keys are never coming and retrying cannot help.
        var adapter = new AbilityInputAdapter();

        Assert.IsFalse(adapter.IsActivationKeyPressed());
        Assert.AreEqual(string.Empty, adapter.ActivationKeyName);
    }

    [TestMethod]
    public void AbilityInputAdapter_ActivationKeyName_IsStableAcrossRepeatedReads()
    {
        // The label is read once per frame by MissionAgentStatusCareerMixin, so it is cached against
        // the key it was built for. Pin that repeated reads agree: a cache that returned a different
        // answer on the second read would flicker the chip every frame.
        var adapter = new AbilityInputAdapter();

        var first = adapter.ActivationKeyName;
        var second = adapter.ActivationKeyName;

        Assert.AreEqual(first, second);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TAOM.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new FileNotFoundException("TAOM.sln not found walking upward from cwd");
    }
}
