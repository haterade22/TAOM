using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.Core;
using TAOM.Adapters;
using TAOM.Tests.Migration;

namespace TAOM.Tests.Features.FieldCommission;

/// <summary>
/// #1544: a promoted companion's civilian set came out typed Battle (the engine's neutral fallback
/// roster declares no civilian flag), and vanilla's inventory Cancel then wrote that set over the
/// battle kit. <c>MakeCivilian</c> retypes the set in place and keeps its twelve slots.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class CivilianKitTypeTests
{
    private static bool _gameLoaded;

    [ClassInitialize]
    public static void Init(TestContext _) => _gameLoaded = GameAssemblies.EnsureLoaded();

    private static void RequireGame()
    {
        if (!_gameLoaded)
            Assert.Inconclusive("Game assemblies not loaded: " + string.Join("; ", GameAssemblies.Diagnostics));
    }

    [TestMethod]
    public void MakeCivilian_BattleTypedSet_BecomesCivilianAndKeepsItsSlots()
    {
        RequireGame();
        // Real gear in several slots: an empty kit would pass an implementation that erases the slots
        // (Codex pre-review, #1544). Each element carries a modifier, a cosmetic item and the quest flag.
        var target = new Equipment(Equipment.EquipmentType.Battle);
        var modifier = new ItemModifier();
        var cosmetic = new ItemObject("cosmetic_tabard");
        target[EquipmentIndex.Weapon0] = new EquipmentElement(new ItemObject("given_javelins"), modifier);
        target[EquipmentIndex.Body] = new EquipmentElement(new ItemObject("given_armour"), modifier, cosmetic);
        target[EquipmentIndex.ExtraWeaponSlot] = new EquipmentElement(new ItemObject("given_banner"), null, null, isQuestItem: true);
        var before = new Equipment(target);

        Assert.IsTrue(HeroCommissionAdapter.MakeCivilian(target));

        Assert.IsTrue(target.IsCivilian);
        for (var slot = 0; slot < Equipment.EquipmentSlotLength; slot++)
        {
            Assert.AreSame(before[slot].Item, target[slot].Item, "item, slot " + slot);
            Assert.AreSame(before[slot].ItemModifier, target[slot].ItemModifier, "modifier, slot " + slot);
            Assert.AreSame(before[slot].CosmeticItem, target[slot].CosmeticItem, "cosmetic, slot " + slot);
            Assert.AreEqual(before[slot].IsQuestItem, target[slot].IsQuestItem, "quest flag, slot " + slot);
        }
        Assert.IsNotNull(target[EquipmentIndex.Body].Item, "the fixture must hold real gear");
    }

    [TestMethod]
    public void MakeCivilian_AlreadyCivilian_ReportsNoChange()
    {
        RequireGame();
        var target = new Equipment(Equipment.EquipmentType.Civilian);

        Assert.IsFalse(HeroCommissionAdapter.MakeCivilian(target));
        Assert.IsTrue(target.IsCivilian);
    }

    [TestMethod]
    public void MakeCivilian_NullSet_ReportsNoChange()
    {
        Assert.IsFalse(HeroCommissionAdapter.MakeCivilian(null));
    }
}
