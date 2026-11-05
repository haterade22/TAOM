using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.InputSystem;
using TaleWorlds.MountAndBlade;
using TAOM.Features.CareerSystem.Abilities;
using TAOM.Features.MarshalsWord;
using TAOM.Features.RealmBorders.UI;
using TAOM.Features.SeatOfSeeing;
using TAOM.Features.TimeAcceleration;
using TAOM.Tests.Infrastructure;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>
/// Pins the engine contracts that decide whether Marshal's Word's keys are rebindable at all, the way the Seat's category test does: a wrong id
/// mislabels the Options row, a small slot count throws on construction, a wrong main category hides the key, and a context registered on a
/// layer throws on an id above 115. The held keys are the contract's accepted exception to the scene-layer collision rule (section 3).
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TaomMarshalHotKeyCategoryTests
{
    private TaomMarshalHotKeyCategory _sut = null!;

    [TestInitialize]
    public void Setup() => _sut = new TaomMarshalHotKeyCategory();

    private GameKey[] RegisteredKeys => _sut.RegisteredGameKeys.Where(k => k != null).ToArray();

    [TestMethod]
    public void Ctor_RegistersExactlyTheSixKeys_AtIds540To545()
    {
        Assert.AreEqual(540, TaomMarshalHotKeyCategory.QueueKeyId);
        Assert.AreEqual(541, TaomMarshalHotKeyCategory.MarkKeyId);
        Assert.AreEqual(542, TaomMarshalHotKeyCategory.KeepWidthsKeyId);
        Assert.AreEqual(543, TaomMarshalHotKeyCategory.FireModeKeyId);
        Assert.AreEqual(544, TaomMarshalHotKeyCategory.WordKeyId);
        Assert.AreEqual(545, TaomMarshalHotKeyCategory.HoldLineKeyId);
        CollectionAssert.AreEqual(new[] { 540, 541, 542, 543, 544, 545 }, RegisteredKeys.Select(k => k.Id).ToArray());
    }

    [TestMethod]
    public void Defaults_AreTheContractsKeys_TheHeldModifiersAndTheThreePressedKeys()
    {
        Assert.AreEqual(InputKey.LeftShift, RegisteredKeys.Single(k => k.Id == 540).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.LeftControl, RegisteredKeys.Single(k => k.Id == 541).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.LeftAlt, RegisteredKeys.Single(k => k.Id == 542).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.Comma, RegisteredKeys.Single(k => k.Id == 543).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.M, RegisteredKeys.Single(k => k.Id == 544).KeyboardKey.InputKey);
        Assert.AreEqual(InputKey.Period, RegisteredKeys.Single(k => k.Id == 545).KeyboardKey.InputKey);
    }

    [TestMethod]
    public void EveryPressedDefault_IsUnboundInTheCategoriesTheMissionSceneLayerReads()
    {
        // The Seat's rule (contract section 3): a pressed key's default stays clear of every key the scene layer reads, or one press acts twice.
        // The held keys are the accepted exception, so only 543, 544 and 545 loop here. GenericPanelGameKeyCategory is left out for the Seat's reason.
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
            foreach (var key in RegisteredKeys.Where(k => k.Id == 543 || k.Id == 544 || k.Id == 545))
                CollectionAssert.DoesNotContain(bound, key.KeyboardKey.InputKey, category.GetType().Name + " already binds the default of key " + key.Id);
        }
    }

    [TestMethod]
    public void TheDefaults_AreDistinctFromEachOther()
    {
        var keys = RegisteredKeys.Select(k => k.KeyboardKey.InputKey).ToList();

        Assert.AreEqual(keys.Count, keys.Distinct().Count());
    }

    [TestMethod]
    public void TheHeldDefaults_SharePhysicalKeysWithTwoVanillaGameKeys_ByTheNumbersTheFeatureDocQuotes()
    {
        // Left Shift is game key 24 (Zoom) and Left Alt game key 5 (ShowIndicators); the contract accepts both side effects because a held
        // key acts only while the panel is open (build-spec 3). Read the engine's numbers, not the compiled constants, so an engine
        // renumbering makes the feature doc's description stale visibly.
        Assert.AreEqual(24, System.Convert.ToInt32(System.Enum.Parse(typeof(GameKeyDefinition), "Zoom")));
        Assert.AreEqual(5, System.Convert.ToInt32(System.Enum.Parse(typeof(GameKeyDefinition), "ShowIndicators")));
    }

    [TestMethod]
    public void EveryKeyId_IsAtLeastTotalGameKeyCount_SoOptionsLabelsDoNotCollideWithVanilla()
    {
        foreach (var key in RegisteredKeys)
            Assert.IsTrue(key.Id >= (int)GameKeyDefinition.TotalGameKeyCount, key.StringId + " would reuse a vanilla key's label");
    }

    [TestMethod]
    public void KeyIds_DoNotCollideWithTheOtherTaomCategories()
    {
        var others = new[]
        {
            TaomTimeControlHotKeyCategory.FastForwardKeyId, TaomTimeControlHotKeyCategory.ExtraFastForwardKeyId,
            TaomTimeControlHotKeyCategory.TurboKeyId, TaomCareerHotKeyCategory.AbilityActivationKeyId,
            TaomRealmBordersHotKeyCategory.ToggleBordersKeyId, TaomRealmBordersHotKeyCategory.CycleMapModeKeyId,
            TaomSeatHotKeyCategory.TakeTheSeatKeyId,
        };

        foreach (var key in RegisteredKeys)
            CollectionAssert.DoesNotContain(others, key.Id);
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
            Assert.AreEqual(TaomMarshalHotKeyCategory.CategoryId, key.GroupId);
    }

    [TestMethod]
    public void TheContext_IsNeverRegisteredOnALayer()
    {
        foreach (var path in RepoPaths.CSharpFiles("Main", "Client").Where(f => f.Contains("MarshalsWord")))
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
            var suffix = TaomMarshalHotKeyCategory.CategoryId + "_" + key.Id;
            CollectionAssert.Contains(ids, "str_key_name." + suffix);
            CollectionAssert.Contains(ids, "str_key_description." + suffix);
        }
    }

    [TestMethod]
    public void TheNameRows_CarryTheTaomSuffixOfTheirSiblings()
    {
        var path = RepoPaths.RepoPath("Main", "_Module", "ModuleData", "global_strings.xml");
        var rows = XDocument.Load(path).Root!.Elements("string").ToList();

        foreach (var id in new[] { 540, 541, 542, 543, 544 })
        {
            var row = rows.Single(e => (string)e.Attribute("id")! == "str_key_name.TaomMarshalHotKeyCategory_" + id);
            StringAssert.EndsWith((string)row.Attribute("text")!, "(TAOM)");
        }
    }

    [TestMethod]
    public void MarshalHotKeys_WithNoRegisteredCategory_ReadsInvalid_SoNoKeyIsEverDown()
    {
        // No TAOM category is registered in a unit-test process, so the reader falls through to Invalid without reaching Input.
        var keys = new MarshalHotKeys();

        Assert.AreEqual(InputKey.Invalid, keys.Queue);
        Assert.AreEqual(InputKey.Invalid, keys.Mark);
        Assert.AreEqual(InputKey.Invalid, keys.KeepWidths);
        Assert.AreEqual(InputKey.Invalid, keys.FireMode);
        Assert.AreEqual(InputKey.Invalid, keys.Word);
        Assert.AreEqual(InputKey.Invalid, keys.Queue, "a miss latches; a second read gives the same answer");
    }
}
