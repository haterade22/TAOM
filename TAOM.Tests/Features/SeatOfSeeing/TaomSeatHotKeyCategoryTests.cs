using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.RealmBorders.UI;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.SeatOfSeeing;

/// <summary>
/// Pins the engine contracts that decide whether the Seat's key is rebindable at all, the way TaomCareerHotKeyCategoryTests does
/// for the career key: a wrong id mislabels the Options row, a small slot count throws on construction, a wrong main category
/// hides the key, and a context registered on a layer throws on an id above 115.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomSeatHotKeyCategoryTests
{
    private TaomSeatHotKeyCategory _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TaomSeatHotKeyCategory();

    private GameKey[] RegisteredKeys => _sut.RegisteredGameKeys.Where(k => k != null).ToArray();

    [TestMethod]
    public void Ctor_RegistersTheKeysOfTheBuiltStages_AtTheContractsIds()
    {
        Assert.AreEqual(530, TaomSeatHotKeyCategory.TakeTheSeatKeyId);
        Assert.AreEqual(531, TaomSeatHotKeyCategory.KeepToKeyId);
        Assert.AreEqual(532, TaomSeatHotKeyCategory.TakeThisSoldierKeyId);
        Assert.AreEqual(533, TaomSeatHotKeyCategory.QuickenTimeKeyId);
        Assert.AreEqual(534, TaomSeatHotKeyCategory.SlowTimeKeyId);
        Assert.AreEqual(535, TaomSeatHotKeyCategory.StayTimeKeyId);
        Assert.AreEqual(536, TaomSeatHotKeyCategory.HideTheInterfaceKeyId);
        CollectionAssert.AreEqual(new[] { 530, 531, 532, 533, 534, 535, 536 }, RegisteredKeys.Select(k => k.Id).ToArray());
    }

    [TestMethod]
    public void Defaults_AreTheContractsKeys()
    {
        var defaults = RegisteredKeys.ToDictionary(k => k.Id, k => k.KeyboardKey.InputKey);

        Assert.AreEqual(InputKey.Q, defaults[530]);
        Assert.AreEqual(InputKey.Insert, defaults[531]);
        Assert.AreEqual(InputKey.Delete, defaults[532]);
        Assert.AreEqual(InputKey.PageUp, defaults[533]);
        Assert.AreEqual(InputKey.PageDown, defaults[534]);
        Assert.AreEqual(InputKey.Home, defaults[535]);
        Assert.AreEqual(InputKey.End, defaults[536]);
    }

    [TestMethod]
    public void EveryDefault_IsUnboundInTheCategoriesTheMissionSceneLayerReads()
    {
        // MissionScreen registers six categories on its layer (389-394), the order handler adds MissionOrderHotkeyCategory, and the chat
        // log view adds ChatLogHotKeyCategory on the same input; one physical key then acts twice. B was key 36 of
        // GenericCampaignPanelsGameKeyCategory, which would print "Can't access banner window right now!" on every press (build-spec 3).
        // GenericPanelGameKeyCategory is left out on purpose: it holds Q and E for a panel's tab change, but only panel layers read
        // them (SwitchToPreviousTab), and vanilla's own use key is E, so Q is free in a mission.
        var categories = new GameKeyContext[]
        {
            new GenericGameKeyContext(),
            new GenericCampaignPanelsGameKeyCategory(),
            new ScoreboardHotKeyCategory(),
            new CombatHotKeyCategory(),
            new TaleWorlds.Engine.InputSystem.CheatsHotKeyCategory(),
            new MissionOrderHotkeyCategory(),
            new ChatLogHotKeyCategory(),
        };

        foreach (var category in categories)
        {
            var bound = category.RegisteredGameKeys.Where(k => k?.KeyboardKey != null).Select(k => k.KeyboardKey.InputKey)
                .Concat(category.RegisteredHotKeys.SelectMany(h => h.Keys.Where(k => k != null).Select(k => k.InputKey))).ToList();
            foreach (var key in RegisteredKeys)
                CollectionAssert.DoesNotContain(bound, key.KeyboardKey.InputKey,
                    category.GetType().Name + " already binds the default of key " + key.Id);
        }
    }

    [TestMethod]
    public void TheDefaults_AreDistinctFromEachOther()
    {
        var keys = RegisteredKeys.Select(k => k.KeyboardKey.InputKey).ToList();

        Assert.AreEqual(keys.Count, keys.Distinct().Count());
    }

    [TestMethod]
    public void EveryKeyId_IsAtLeastTotalGameKeyCount_SoOptionsLabelsDoNotCollideWithVanilla()
    {
        foreach (var key in RegisteredKeys)
            Assert.IsTrue(key.Id >= (int)GameKeyDefinition.TotalGameKeyCount, key.StringId + " would reuse a vanilla key's label");
    }

    [TestMethod]
    public void KeyId_DoesNotCollideWithTheOtherTaomCategories()
    {
        var others = new[]
        {
            TaomTimeControlHotKeyCategory.FastForwardKeyId, TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
            TaomTimeControlHotKeyCategory.TurboKeyId, TaomCareerHotKeyCategory.AbilityActivationKeyId,
            TaomRealmBordersHotKeyCategory.ToggleBordersKeyId, TaomRealmBordersHotKeyCategory.CycleMapModeKeyId,
        };

        foreach (var id in new[] { TaomSeatHotKeyCategory.TakeTheSeatKeyId, TaomSeatHotKeyCategory.KeepToKeyId, TaomSeatHotKeyCategory.TakeThisSoldierKeyId,
                     TaomSeatHotKeyCategory.QuickenTimeKeyId, TaomSeatHotKeyCategory.SlowTimeKeyId, TaomSeatHotKeyCategory.StayTimeKeyId, TaomSeatHotKeyCategory.HideTheInterfaceKeyId })
            CollectionAssert.DoesNotContain(others, id);
    }

    [TestMethod]
    public void RegisteredSlotCount_ExceedsLargestKeyId()
    {
        Assert.IsTrue(_sut.RegisteredGameKeys.Count > RegisteredKeys.Max(k => k.Id), "RegisterGameKey is an indexed write and would throw");
    }

    [TestMethod]
    public void EveryKey_UsesActionMainCategory_SoTheOptionsScreenRendersIt()
    {
        foreach (var key in RegisteredKeys)
            Assert.AreEqual(GameKeyMainCategories.ActionCategory, key.MainCategoryId, key.StringId);
    }

    [TestMethod]
    public void ContextType_IsDefault_AndTheGroupIdMatchesTheCategoryId()
    {
        Assert.AreEqual(GameKeyContext.GameKeyContextType.Default, _sut.Type);
        foreach (var key in RegisteredKeys)
            Assert.AreEqual(TaomSeatHotKeyCategory.CategoryId, key.GroupId);
    }

    [TestMethod]
    public void TheContext_IsNeverRegisteredOnALayer()
    {
        // InputContext.RegisterHotKeyCategory sizes its slot list from the first category on the layer (116 entries),
        // so an id of 530 would throw (AbilityInputAdapter.cs:32-37). The key is read through GameKey and the scene layer's own input.
        foreach (var path in RepoPaths.CSharpFiles("Main", "Client").Where(f => f.Contains("SeatOfSeeing")))
        {
            var code = RepoPaths.StripComments(File.ReadAllText(path));
            Assert.IsFalse(code.Contains("RegisterHotKeyCategory"), path);
        }
    }

    [TestMethod]
    public void EveryKey_HasNameAndDescriptionStringsInGlobalStrings()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "global_strings.xml");
        var ids = XDocument.Load(path).Root!.Elements("string").Select(e => (string)e.Attribute("id")!).ToList();

        foreach (var key in RegisteredKeys)
        {
            var suffix = TaomSeatHotKeyCategory.CategoryId + "_" + key.Id;
            CollectionAssert.Contains(ids, "str_key_name." + suffix);
            CollectionAssert.Contains(ids, "str_key_description." + suffix);
        }
    }

    [TestMethod]
    public void EveryNameRow_CarriesTheTaomSuffixOfItsSiblings()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "global_strings.xml");
        var rows = XDocument.Load(path).Root!.Elements("string").ToList();

        Assert.IsTrue(RegisteredKeys.Count() >= 7, "the keys of stages S1 to S6 are 530 to 536");
        foreach (var key in RegisteredKeys)
        {
            var id = "str_key_name." + TaomSeatHotKeyCategory.CategoryId + "_" + key.Id;
            var row = rows.Single(e => (string)e.Attribute("id")! == id);
            StringAssert.EndsWith((string)row.Attribute("text")!, "(TAOM)", id);
        }
    }

    [TestMethod]
    public void SeatHotKeys_WithNoRegisteredCategory_ReadsInvalid_SoTheKeyIsNeverPressed()
    {
        // No TAOM category is registered in a unit-test process, so the reader falls through to Invalid without reaching Input.
        var keys = new SeatHotKeys();

        Assert.AreEqual(InputKey.Invalid, keys.TakeTheSeat);
        Assert.AreEqual(InputKey.Invalid, keys.TakeTheSeat, "a miss latches; a second read gives the same answer");
        Assert.AreEqual(InputKey.Invalid, keys.KeepTo);
        Assert.AreEqual(InputKey.Invalid, keys.TakeThisSoldier);
        Assert.AreEqual(InputKey.Invalid, keys.HideTheInterface);
        Assert.AreEqual(InputKey.Invalid, keys.QuickenTime);
        Assert.AreEqual(InputKey.Invalid, keys.SlowTime);
        Assert.AreEqual(InputKey.Invalid, keys.StayTime);
    }
}
