using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.TimeAcceleration;

namespace TAOM.Tests.Features.TimeAcceleration;

/// <summary>
/// Pins the three engine contracts that decide whether TAOM's time-control keys work at all. Every
/// one of them fails silently or only in a live game, which is why they are pinned here:
/// a wrong id breaks the Options label, a small count throws on construction, and a wrong
/// MainCategoryId hides the keys from the Keybindings screen entirely.
/// </summary>
[TestClass]
public class TaomTimeControlHotKeyCategoryTests
{
    private TaomTimeControlHotKeyCategory _sut;

    [TestInitialize]
    public void Setup()
    {
        _sut = new TaomTimeControlHotKeyCategory();
    }

    private GameKey[] RegisteredKeys =>
        _sut.RegisteredGameKeys.Where(k => k != null).ToArray();

    [TestMethod]
    public void Ctor_RegistersTheThreeTimeControlKeys()
    {
        // Arrange + Act in Setup

        // Assert
        CollectionAssert.AreEquivalent(
            new[]
            {
                TaomTimeControlHotKeyCategory.FastForwardKeyId,
                TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
                TaomTimeControlHotKeyCategory.TurboKeyId,
            },
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
                $"GameKey '{key.StringId}' has id {key.Id}, below TotalGameKeyCount "
                + $"({(int)GameKeyDefinition.TotalGameKeyCount}). Its Options label would collide "
                + "with a vanilla game key's localization id.");
        }
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
    public void EveryKey_UsesCampaignMapMainCategory_SoTheOptionsScreenRendersIt()
    {
        // GameKeyOptionCategoryVM only renders keys whose MainCategoryId is in the fixed allowlist
        // returned by OptionsProvider.GetGameKeyCategoriesList. CampaignMapCategory is on it, which
        // is why this needs no Harmony patch.
        foreach (var key in RegisteredKeys)
        {
            Assert.AreEqual(
                GameKeyMainCategories.CampaignMapCategory, key.MainCategoryId,
                $"GameKey '{key.StringId}' would not appear in Options > Keybindings.");
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
            Assert.AreEqual(TaomTimeControlHotKeyCategory.CategoryId, key.GroupId);
        }
    }

    [TestMethod]
    public void Defaults_MatchTheKeysTaomShippedBeforeRebindingExisted()
    {
        // Existing players keep their muscle memory: Space, E, and Ctrl+Space (the Ctrl half is
        // gated in the service, not here).
        Assert.AreEqual(InputKey.Space, KeyById(TaomTimeControlHotKeyCategory.FastForwardKeyId));
        Assert.AreEqual(InputKey.E, KeyById(TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId));
        Assert.AreEqual(InputKey.Space, KeyById(TaomTimeControlHotKeyCategory.TurboKeyId));
    }

    [TestMethod]
    public void KeyIds_AreDistinct()
    {
        // RegisterGameKey writes by index, so a duplicated id silently overwrites the earlier key
        // rather than failing.
        var ids = RegisteredKeys.Select(k => k.Id).ToArray();
        Assert.AreEqual(ids.Length, ids.Distinct().Count());
    }

    [TestMethod]
    public void EveryShippedKey_HasANonNullKeyboardKey()
    {
        // MapInputAdapter.BoundKey reads gameKey?.KeyboardKey?.InputKey. If a shipped default ever
        // became Invalid, KeyboardKey would be null (see the next test) and the action would be
        // silently dead rather than merely rebound.
        foreach (var key in RegisteredKeys)
        {
            Assert.IsNotNull(key.KeyboardKey, $"GameKey '{key.StringId}' ships unbound.");
        }
    }

    [TestMethod]
    public void GameKeyConstructedUnbound_LeavesKeyboardKeyNull()
    {
        // One of the TWO shapes an unbound key takes, and the reason BoundKey needs a null-guard:
        // the GameKey ctor stores null rather than a Key wrapping Invalid.
        var unbound = new GameKey(
            TaomTimeControlHotKeyCategory.TurboKeyId, "Probe",
            TaomTimeControlHotKeyCategory.CategoryId,
            InputKey.Invalid, GameKeyMainCategories.CampaignMapCategory);

        Assert.IsNull(unbound.KeyboardKey);
    }

    [TestMethod]
    public void GameKeyClearedInOptions_KeepsANonNullKeyHoldingInvalid()
    {
        // The OTHER shape, and the one a player actually produces: GameKeyOptionVM.OnDone calls
        // Key.ChangeKey(InputKey.Invalid) on the EXISTING Key rather than nulling it. So a null check
        // alone would miss a cleared binding, which is why BoundKey compares the InputKey too.
        var key = RegisteredKeys.Single(k => k.Id == TaomTimeControlHotKeyCategory.TurboKeyId);
        key.KeyboardKey.ChangeKey(InputKey.Invalid);

        Assert.IsNotNull(key.KeyboardKey);
        Assert.AreEqual(InputKey.Invalid, key.KeyboardKey.InputKey);
    }

    private InputKey KeyById(int id) =>
        RegisteredKeys.Single(k => k.Id == id).KeyboardKey.InputKey;
}
