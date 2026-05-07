using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.SaveSystem;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Tests.Features.EquipPresets;

[TestClass]
public class HoNEquipmentPresetTests
{
    // === Constructor / defaults ===

    [TestMethod]
    public void Default_ItemsAndCivilianItems_AreNonNullEmptyLists()
    {
        var p = new HoNEquipmentPreset();
        Assert.IsNotNull(p.Items);
        Assert.IsNotNull(p.CivilianItems);
        Assert.AreEqual(0, p.Items.Count);
        Assert.AreEqual(0, p.CivilianItems.Count);
    }

    [TestMethod]
    public void Default_NameAndId_AreEmptyNotNull()
    {
        var p = new HoNEquipmentPreset();
        Assert.AreEqual(string.Empty, p.Name);
        Assert.AreEqual(string.Empty, p.Id);
    }

    [TestMethod]
    public void Default_FlagsAreFalse()
    {
        var p = new HoNEquipmentPreset();
        Assert.IsFalse(p.IncludesMount);
        Assert.IsFalse(p.IncludesCivilianEquipment);
    }

    // === SaveableProperty attribute presence + indexes ===

    [TestMethod]
    public void SaveableProperty_IndexesMatchDecompiledSource()
    {
        // Index numbers are part of the save format — changing them breaks all existing saves.
        // These are matched verbatim to the decompiled source (HoNEquipmentPreset:1207-1299+).
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.Name), 1);
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.Id), 2);
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.Items), 3);
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.CivilianItems), 4);
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.IncludesMount), 5);
        AssertSaveableIndex(typeof(HoNEquipmentPreset), nameof(HoNEquipmentPreset.IncludesCivilianEquipment), 6);
    }

    [TestMethod]
    public void SaveableProperty_ItemReferenceIndexes()
    {
        AssertSaveableIndex(typeof(HoNPresetItemReference), nameof(HoNPresetItemReference.SlotIndex), 1);
        AssertSaveableIndex(typeof(HoNPresetItemReference), nameof(HoNPresetItemReference.ItemStringId), 2);
        AssertSaveableIndex(typeof(HoNPresetItemReference), nameof(HoNPresetItemReference.ItemModifierStringId), 3);
    }

    // === HoNPresetItemReference defaults + null-safety ===

    [TestMethod]
    public void ItemRef_DefaultStringsNotNull()
    {
        var r = new HoNPresetItemReference();
        Assert.AreEqual(string.Empty, r.ItemStringId);
        Assert.AreEqual(string.Empty, r.ItemModifierStringId);
    }

    [TestMethod]
    public void ItemRef_NullArgs_CoercedToEmpty()
    {
        var r = new HoNPresetItemReference(0, null!, null!);
        Assert.AreEqual(string.Empty, r.ItemStringId);
        Assert.AreEqual(string.Empty, r.ItemModifierStringId);
    }

    private static void AssertSaveableIndex(System.Type type, string propertyName, int expectedIndex)
    {
        var prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.IsNotNull(prop, $"{type.Name}.{propertyName} not found");
        var attr = prop!.GetCustomAttributes(typeof(SaveablePropertyAttribute), false)
                       .Cast<SaveablePropertyAttribute>().FirstOrDefault();
        Assert.IsNotNull(attr, $"{type.Name}.{propertyName} missing [SaveableProperty]");
        Assert.AreEqual(expectedIndex, attr!.LocalSaveId, $"{type.Name}.{propertyName} expected SaveableProperty index {expectedIndex}");
    }
}
