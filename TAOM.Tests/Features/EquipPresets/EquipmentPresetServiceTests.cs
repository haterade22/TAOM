using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.EquipPresets;
using TAOM.Features.EquipPresets.Models;

namespace TAOM.Tests.Features.EquipPresets;

[TestClass]
public class EquipmentPresetServiceTests
{
    private const string Hero1 = "hero_aragorn";
    private const string Hero2 = "hero_legolas";

    private IEquipPresetsSettingsProvider _settings = null!;
    private IEquipmentSlotAdapter _slots = null!;
    private IInventoryScreenAdapter _screen = null!;
    private IItemModifierLookupAdapter _modLookup = null!;
    private IModLogger _logger = null!;
    private EquipmentPresetService _sut = null!;

    [TestInitialize]
    public void Setup()
    {
        _settings = Substitute.For<IEquipPresetsSettingsProvider>();
        _slots = Substitute.For<IEquipmentSlotAdapter>();
        _screen = Substitute.For<IInventoryScreenAdapter>();
        _modLookup = Substitute.For<IItemModifierLookupAdapter>();
        _logger = Substitute.For<IModLogger>();

        _settings.IsEnabled.Returns(true);
        _settings.MaxPresetsPerCharacter.Returns(10);
        _settings.IsDebugMode.Returns(false);

        _slots.HeroExists(Arg.Any<string>()).Returns(true);
        _slots.Capture(Arg.Any<string>(), Arg.Any<bool>()).Returns(new List<EquippedSlotSnapshot>());

        _modLookup.ExistsOrEmpty(Arg.Any<string>()).Returns(true);

        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), Arg.Any<bool>())
               .Returns(LoadEquipmentResult.Empty);

        _sut = new EquipmentPresetService(_settings, _slots, _screen, _modLookup, _logger);
    }

    // === SaveCurrent: Disabled / NoActiveHero / InvalidName / MaxReached ===

    [TestMethod]
    public void SaveCurrent_FeatureDisabled_ReturnsDisabled()
    {
        _settings.IsEnabled.Returns(false);
        var result = _sut.SaveCurrent(Hero1, "TestPreset", true, false);
        Assert.AreEqual(SaveOutcome.Disabled, result);
    }

    [TestMethod]
    public void SaveCurrent_EmptyHeroId_ReturnsNoActiveHero()
    {
        var result = _sut.SaveCurrent("", "TestPreset", true, false);
        Assert.AreEqual(SaveOutcome.NoActiveHero, result);
    }

    [TestMethod]
    public void SaveCurrent_HeroDoesNotExist_ReturnsNoActiveHero()
    {
        _slots.HeroExists(Hero1).Returns(false);
        var result = _sut.SaveCurrent(Hero1, "TestPreset", true, false);
        Assert.AreEqual(SaveOutcome.NoActiveHero, result);
    }

    [TestMethod]
    public void SaveCurrent_EmptyName_ReturnsInvalidName()
    {
        var result = _sut.SaveCurrent(Hero1, "   ", true, false);
        Assert.AreEqual(SaveOutcome.InvalidName, result);
    }

    [TestMethod]
    public void SaveCurrent_NullName_ReturnsInvalidName()
    {
        var result = _sut.SaveCurrent(Hero1, null!, true, false);
        Assert.AreEqual(SaveOutcome.InvalidName, result);
    }

    [TestMethod]
    public void SaveCurrent_AtMaxPresets_ReturnsMaxReached()
    {
        _settings.MaxPresetsPerCharacter.Returns(2);
        Assert.AreEqual(SaveOutcome.Saved, _sut.SaveCurrent(Hero1, "P1", true, false));
        Assert.AreEqual(SaveOutcome.Saved, _sut.SaveCurrent(Hero1, "P2", true, false));
        Assert.AreEqual(SaveOutcome.MaxReached, _sut.SaveCurrent(Hero1, "P3", true, false));
    }

    // === SaveCurrent: capture mechanics ===

    [TestMethod]
    public void SaveCurrent_HappyPath_CallsCaptureBattleNotCivilian_WhenIncludeCivilianFalse()
    {
        _sut.SaveCurrent(Hero1, "BattleOnly", true, false);
        _slots.Received(1).Capture(Hero1, false);
        _slots.DidNotReceive().Capture(Hero1, true);
    }

    [TestMethod]
    public void SaveCurrent_IncludeCivilianTrue_CapturesBothEquipmentSets()
    {
        _sut.SaveCurrent(Hero1, "Both", true, true);
        _slots.Received(1).Capture(Hero1, false);
        _slots.Received(1).Capture(Hero1, true);
    }

    [TestMethod]
    public void SaveCurrent_IncludeMountFalse_DropsMountSlot()
    {
        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(0, "sword", ""),       // weapon slot — kept
            new(10, "horse_charger", ""), // mount slot — dropped
            new(11, "harness_chain", ""), // harness slot — dropped
        });
        _sut.SaveCurrent(Hero1, "NoMount", false, false);
        var preset = _sut.GetPresets(Hero1)[0];
        Assert.AreEqual(1, preset.Items.Count);
        Assert.AreEqual(0, preset.Items[0].SlotIndex);
        Assert.IsFalse(preset.IncludesMount);
    }

    [TestMethod]
    public void SaveCurrent_IncludeMountTrue_KeepsMountSlot()
    {
        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(10, "horse_charger", ""),
        });
        _sut.SaveCurrent(Hero1, "WithMount", true, false);
        var preset = _sut.GetPresets(Hero1)[0];
        Assert.AreEqual(1, preset.Items.Count);
        Assert.AreEqual(10, preset.Items[0].SlotIndex);
        Assert.IsTrue(preset.IncludesMount);
    }

    [TestMethod]
    public void SaveCurrent_PreservesItemModifierStringId()
    {
        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(0, "sword_two_handed", "modifier_sharp"),
        });
        _sut.SaveCurrent(Hero1, "Sharp", true, false);
        var preset = _sut.GetPresets(Hero1)[0];
        Assert.AreEqual("modifier_sharp", preset.Items[0].ItemModifierStringId);
    }

    [TestMethod]
    public void SaveCurrent_CapturesEmptySlots_AsEmptyRefs_ForLoadClearSemantics()
    {
        // H4 fix: capture must include empty-slot sentinels so a "no shield" preset can
        // unequip the shield on Load. Adapter Capture now emits one snapshot per slot 0..11
        // with empty ItemStringId for empty slots.
        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(0, "sword", ""),       // slot 0 has item
            new(1, "", ""),            // slot 1 empty (shield slot)
            new(2, "", ""),            // slot 2 empty
            new(10, "horse_charger", ""), // mount
        });
        _sut.SaveCurrent(Hero1, "Mixed", true, false);
        var preset = _sut.GetPresets(Hero1)[0];
        Assert.AreEqual(4, preset.Items.Count);
        Assert.AreEqual("sword", preset.Items[0].ItemStringId);
        Assert.AreEqual(string.Empty, preset.Items[1].ItemStringId);
        Assert.AreEqual(string.Empty, preset.Items[2].ItemStringId);
        Assert.AreEqual("horse_charger", preset.Items[3].ItemStringId);
    }

    // === GetPresets ===

    [TestMethod]
    public void GetPresets_NoEntries_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetPresets(Hero1).Count);
    }

    [TestMethod]
    public void GetPresets_EmptyHeroId_ReturnsEmpty()
    {
        Assert.AreEqual(0, _sut.GetPresets("").Count);
    }

    [TestMethod]
    public void GetPresets_KeyedByHero_PerHero()
    {
        _sut.SaveCurrent(Hero1, "H1Preset", true, false);
        _sut.SaveCurrent(Hero2, "H2Preset", true, false);
        Assert.AreEqual(1, _sut.GetPresets(Hero1).Count);
        Assert.AreEqual(1, _sut.GetPresets(Hero2).Count);
        Assert.AreNotEqual(_sut.GetPresets(Hero1)[0].Id, _sut.GetPresets(Hero2)[0].Id);
    }

    // === UpdatePreset ===

    [TestMethod]
    public void UpdatePreset_FeatureDisabled_ReturnsDisabled()
    {
        _settings.IsEnabled.Returns(false);
        Assert.AreEqual(UpdateOutcome.Disabled, _sut.UpdatePreset(Hero1, "id"));
    }

    [TestMethod]
    public void UpdatePreset_NonexistentPreset_ReturnsNotFound()
    {
        _sut.SaveCurrent(Hero1, "Real", true, false);
        Assert.AreEqual(UpdateOutcome.NotFound, _sut.UpdatePreset(Hero1, "nonexistent-id"));
    }

    [TestMethod]
    public void UpdatePreset_HappyPath_RebuildsItemList()
    {
        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(0, "sword", "")
        });
        _sut.SaveCurrent(Hero1, "Test", true, false);
        var presetId = _sut.GetPresets(Hero1)[0].Id;

        _slots.Capture(Hero1, false).Returns(new List<EquippedSlotSnapshot>
        {
            new(0, "axe", ""),
            new(1, "shield", "")
        });
        var result = _sut.UpdatePreset(Hero1, presetId);
        Assert.AreEqual(UpdateOutcome.Updated, result);
        var preset = _sut.GetPresets(Hero1)[0];
        Assert.AreEqual(2, preset.Items.Count);
        Assert.AreEqual("axe", preset.Items[0].ItemStringId);
    }

    // === LoadPresetWithReport ===

    [TestMethod]
    public void LoadPresetWithReport_FeatureDisabled_ReturnsDisabled()
    {
        _settings.IsEnabled.Returns(false);
        var report = _sut.LoadPresetWithReport(Hero1, "id");
        Assert.IsTrue(report.Disabled);
    }

    [TestMethod]
    public void LoadPresetWithReport_HeroMissing_ReturnsNoActiveHero()
    {
        _slots.HeroExists(Hero1).Returns(false);
        var report = _sut.LoadPresetWithReport(Hero1, "id");
        Assert.IsTrue(report.NoActiveHero);
    }

    [TestMethod]
    public void LoadPresetWithReport_PresetNotFound_ReturnsNotFound()
    {
        var report = _sut.LoadPresetWithReport(Hero1, "missing-id");
        Assert.IsTrue(report.NotFound);
    }

    [TestMethod]
    public void LoadPresetWithReport_HappyPath_DelegatesToScreenAdapter_AndAggregatesEquippedCount()
    {
        SeedPreset(Hero1, "Test", new[] { (0, "sword", "") }, includeCivilian: false, includeMount: true);
        _screen.LoadEquipment(
            Arg.Any<IReadOnlyList<PresetSlotRequest>>(),
            Arg.Is<bool>(b => b == false))
            .Returns(new LoadEquipmentResult(1, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>()));

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        var report = _sut.LoadPresetWithReport(Hero1, presetId);

        Assert.IsFalse(report.Disabled);
        Assert.IsFalse(report.NotFound);
        Assert.AreEqual(1, report.EquippedCount);
        Assert.AreEqual(0, report.MissingItems.Count);
    }

    [TestMethod]
    public void LoadPresetWithReport_BuildsRequestPerSavedRef_PassedToAdapter()
    {
        SeedPreset(Hero1, "Test",
            new[] { (0, "sword", ""), (3, "helmet", "modifier_fine") },
            includeCivilian: false, includeMount: true);

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        _sut.LoadPresetWithReport(Hero1, presetId);

        _screen.Received(1).LoadEquipment(
            Arg.Is<IReadOnlyList<PresetSlotRequest>>(reqs =>
                reqs.Count == 2
                && reqs[0].SlotIndex == 0 && reqs[0].ItemStringId == "sword"
                && reqs[1].SlotIndex == 3 && reqs[1].ItemStringId == "helmet"
                && reqs[1].ItemModifierStringId == "modifier_fine"),
            false);
    }

    [TestMethod]
    public void LoadPresetWithReport_ItemMissing_AggregatedFromAdapterReport()
    {
        SeedPreset(Hero1, "Test", new[] { (0, "deleted_item", "") }, includeCivilian: false, includeMount: true);
        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false)
            .Returns(new LoadEquipmentResult(0,
                missingItems: new[] { "deleted_item" },
                missingModifiers: System.Array.Empty<string>(),
                invalidSlots: System.Array.Empty<int>()));

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        var report = _sut.LoadPresetWithReport(Hero1, presetId);

        Assert.AreEqual(0, report.EquippedCount);
        Assert.AreEqual(1, report.MissingItems.Count);
        Assert.AreEqual("deleted_item", report.MissingItems[0]);
    }

    [TestMethod]
    public void LoadPresetWithReport_ModifierMissing_PreValidatedToNull_BeforeAdapterCall()
    {
        // Per .claude/rules/csharp-architecture.md "Lookup Functions With Fallbacks": service must
        // call ExistsOrEmpty BEFORE the adapter is asked to resolve. If validation fails, the
        // request is sent with a null modifier so the adapter equips bare. The service does NOT
        // add the missing modifier to the report itself — the adapter handles that path so we
        // don't double-count.
        SeedPreset(Hero1, "Test", new[] { (0, "sword", "missing_mod") }, includeCivilian: false, includeMount: true);
        _modLookup.ExistsOrEmpty("missing_mod").Returns(false);
        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false)
            .Returns(new LoadEquipmentResult(1, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>()));

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        _sut.LoadPresetWithReport(Hero1, presetId);

        // Verify: service stripped the modifier before calling the adapter.
        _screen.Received(1).LoadEquipment(
            Arg.Is<IReadOnlyList<PresetSlotRequest>>(reqs =>
                reqs.Count == 1 && reqs[0].ItemModifierStringId == string.Empty),
            false);
    }

    [TestMethod]
    public void LoadPresetWithReport_ModifierExists_PassedThroughToAdapter()
    {
        SeedPreset(Hero1, "Test", new[] { (0, "sword", "modifier_sharp") }, includeCivilian: false, includeMount: true);
        _modLookup.ExistsOrEmpty("modifier_sharp").Returns(true);
        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false)
            .Returns(new LoadEquipmentResult(1, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>()));

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        var report = _sut.LoadPresetWithReport(Hero1, presetId);

        _screen.Received(1).LoadEquipment(
            Arg.Is<IReadOnlyList<PresetSlotRequest>>(reqs =>
                reqs.Count == 1 && reqs[0].ItemModifierStringId == "modifier_sharp"),
            false);
        Assert.AreEqual(1, report.EquippedCount);
    }

    [TestMethod]
    public void LoadPresetWithReport_IncludesCivilianFalse_DoesNotCallCivilianAdapter()
    {
        var preset = SeedPreset(Hero1, "Test",
            battleItems: new[] { (0, "sword", "") },
            civilianItems: new[] { (0, "tunic", "") },
            includeCivilian: false, includeMount: true);

        _sut.LoadPresetWithReport(Hero1, preset.Id);

        _screen.Received(1).LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false);
        _screen.DidNotReceive().LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), true);
    }

    [TestMethod]
    public void LoadPresetWithReport_IncludesCivilianTrue_AppliesBothEquipmentSets()
    {
        var preset = SeedPreset(Hero1, "Test",
            battleItems: new[] { (0, "sword", "") },
            civilianItems: new[] { (0, "tunic", "") },
            includeCivilian: true, includeMount: true);

        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false)
            .Returns(new LoadEquipmentResult(1, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>()));
        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), true)
            .Returns(new LoadEquipmentResult(1, System.Array.Empty<string>(), System.Array.Empty<string>(), System.Array.Empty<int>()));

        var report = _sut.LoadPresetWithReport(Hero1, preset.Id);

        _screen.Received(1).LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false);
        _screen.Received(1).LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), true);
        Assert.AreEqual(2, report.EquippedCount);
    }

    [TestMethod]
    public void LoadPresetWithReport_EmptyItemId_PassesThroughAsClearRequest()
    {
        // H4: empty-slot clear semantics. Service forwards empty itemId to the adapter; the
        // adapter builds an unequip TransferCommand. Service does NOT call ApplySlot/ClearSlot
        // anymore (those methods were removed in the Codex fix pass).
        var preset = SeedPreset(Hero1, "Test",
            battleItems: new[] { (3, "", "") },
            includeCivilian: false, includeMount: true);

        _sut.LoadPresetWithReport(Hero1, preset.Id);

        _screen.Received(1).LoadEquipment(
            Arg.Is<IReadOnlyList<PresetSlotRequest>>(reqs =>
                reqs.Count == 1 && reqs[0].SlotIndex == 3 && reqs[0].ItemStringId == string.Empty),
            false);
    }

    [TestMethod]
    public void LoadPresetWithReport_IncludeMountFalse_FiltersOutMountSlotsFromRequests()
    {
        var preset = SeedPreset(Hero1, "Test",
            battleItems: new[] { (0, "sword", ""), (10, "horse", ""), (11, "harness", "") },
            includeCivilian: false, includeMount: false);

        _sut.LoadPresetWithReport(Hero1, preset.Id);

        _screen.Received(1).LoadEquipment(
            Arg.Is<IReadOnlyList<PresetSlotRequest>>(reqs =>
                reqs.Count == 1 && reqs[0].SlotIndex == 0),
            false);
    }

    [TestMethod]
    public void LoadPresetWithReport_AggregatesInvalidSlotsFromAdapter()
    {
        SeedPreset(Hero1, "Test", new[] { (0, "tampered_helmet_in_weapon_slot", "") },
            includeCivilian: false, includeMount: true);
        _screen.LoadEquipment(Arg.Any<IReadOnlyList<PresetSlotRequest>>(), false)
            .Returns(new LoadEquipmentResult(0,
                System.Array.Empty<string>(),
                System.Array.Empty<string>(),
                invalidSlots: new[] { 0 }));

        var presetId = _sut.GetPresets(Hero1)[0].Id;
        var report = _sut.LoadPresetWithReport(Hero1, presetId);

        Assert.AreEqual(1, report.InvalidSlots.Count);
        Assert.AreEqual(0, report.InvalidSlots[0]);
        Assert.IsTrue(report.HasIssues);
    }

    // === DeletePreset ===

    [TestMethod]
    public void DeletePreset_FeatureDisabled_ReturnsDisabled()
    {
        _settings.IsEnabled.Returns(false);
        Assert.AreEqual(DeleteOutcome.Disabled, _sut.DeletePreset(Hero1, "id"));
    }

    [TestMethod]
    public void DeletePreset_HappyPath_RemovesAndReturnsDeleted()
    {
        _sut.SaveCurrent(Hero1, "Goner", true, false);
        var presetId = _sut.GetPresets(Hero1)[0].Id;
        var result = _sut.DeletePreset(Hero1, presetId);
        Assert.AreEqual(DeleteOutcome.Deleted, result);
        Assert.AreEqual(0, _sut.GetPresets(Hero1).Count);
    }

    [TestMethod]
    public void DeletePreset_LastPresetForHero_RemovesHeroEntry()
    {
        _sut.SaveCurrent(Hero1, "OnlyOne", true, false);
        var presetId = _sut.GetPresets(Hero1)[0].Id;
        _sut.DeletePreset(Hero1, presetId);
        Assert.AreEqual(0, _sut.SerializableState.Count);
    }

    [TestMethod]
    public void DeletePreset_NonexistentPreset_ReturnsNotFound()
    {
        _sut.SaveCurrent(Hero1, "Real", true, false);
        Assert.AreEqual(DeleteOutcome.NotFound, _sut.DeletePreset(Hero1, "missing"));
    }

    // === PruneOrphanedEntries ===

    [TestMethod]
    public void PruneOrphanedEntries_RemovesHeroesNotInLiveSet()
    {
        _sut.SaveCurrent(Hero1, "alive", true, false);
        _sut.SaveCurrent(Hero2, "dead", true, false);

        var pruned = _sut.PruneOrphanedEntries(new HashSet<string> { Hero1 });

        Assert.AreEqual(1, pruned);
        Assert.AreEqual(1, _sut.GetPresets(Hero1).Count);
        Assert.AreEqual(0, _sut.GetPresets(Hero2).Count);
    }

    [TestMethod]
    public void PruneOrphanedEntries_EmptyLiveSet_PrunesNothing_DefensiveAgainstTransientLoadState()
    {
        _sut.SaveCurrent(Hero1, "preserve", true, false);
        var pruned = _sut.PruneOrphanedEntries(new HashSet<string>());
        Assert.AreEqual(0, pruned);
        Assert.AreEqual(1, _sut.GetPresets(Hero1).Count);
    }

    [TestMethod]
    public void PruneOrphanedEntries_AllHeroesAlive_PrunesNothing()
    {
        _sut.SaveCurrent(Hero1, "alive1", true, false);
        _sut.SaveCurrent(Hero2, "alive2", true, false);
        var pruned = _sut.PruneOrphanedEntries(new HashSet<string> { Hero1, Hero2 });
        Assert.AreEqual(0, pruned);
    }

    // === Restore from save ===

    [TestMethod]
    public void RestoreFromSerializableState_NullInput_GivesEmptyDictionary()
    {
        _sut.RestoreFromSerializableState(null);
        Assert.AreEqual(0, _sut.SerializableState.Count);
    }

    [TestMethod]
    public void RestoreFromSerializableState_RestoresEntries()
    {
        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [Hero1] = new List<HoNEquipmentPreset>
            {
                new() { Id = "p1", Name = "Loaded" }
            }
        };
        _sut.RestoreFromSerializableState(state);
        Assert.AreEqual(1, _sut.GetPresets(Hero1).Count);
        Assert.AreEqual("Loaded", _sut.GetPresets(Hero1)[0].Name);
    }

    // L9 fix: defensive normalization on RestoreFromSerializableState

    [TestMethod]
    public void RestoreFromSerializableState_DropsNullKeys()
    {
        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [string.Empty] = new() { new() { Id = "p1", Name = "Bad" } },
            [Hero1] = new() { new() { Id = "p2", Name = "Good" } },
        };
        _sut.RestoreFromSerializableState(state);
        Assert.AreEqual(1, _sut.SerializableState.Count);
        Assert.IsTrue(_sut.SerializableState.ContainsKey(Hero1));
    }

    [TestMethod]
    public void RestoreFromSerializableState_DropsNullPresetEntries()
    {
        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [Hero1] = new() { null!, new() { Id = "p1", Name = "Real" }, null! },
        };
        _sut.RestoreFromSerializableState(state);
        Assert.AreEqual(1, _sut.GetPresets(Hero1).Count);
        Assert.AreEqual("Real", _sut.GetPresets(Hero1)[0].Name);
    }

    [TestMethod]
    public void RestoreFromSerializableState_NullItemListsReplacedWithEmpty()
    {
        var preset = new HoNEquipmentPreset { Id = "p1", Name = "WithNulls" };
        preset.Items = null!;
        preset.CivilianItems = null!;
        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [Hero1] = new() { preset }
        };
        _sut.RestoreFromSerializableState(state);
        var loaded = _sut.GetPresets(Hero1)[0];
        Assert.IsNotNull(loaded.Items);
        Assert.IsNotNull(loaded.CivilianItems);
        Assert.AreEqual(0, loaded.Items.Count);
        Assert.AreEqual(0, loaded.CivilianItems.Count);
    }

    [TestMethod]
    public void RestoreFromSerializableState_EmptyHeroListAfterPruning_DoesNotCreateEntry()
    {
        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [Hero1] = new() { null!, null! }, // all entries null
        };
        _sut.RestoreFromSerializableState(state);
        Assert.AreEqual(0, _sut.SerializableState.Count);
    }

    // === Helper ===

    private HoNEquipmentPreset SeedPreset(string heroId, string name,
        (int slot, string item, string mod)[] battleItems,
        (int slot, string item, string mod)[]? civilianItems = null,
        bool includeCivilian = false, bool includeMount = true)
    {
        var preset = new HoNEquipmentPreset
        {
            Id = System.Guid.NewGuid().ToString("N"),
            Name = name,
            IncludesMount = includeMount,
            IncludesCivilianEquipment = includeCivilian,
        };
        foreach (var (slot, item, mod) in battleItems)
            preset.Items.Add(new HoNPresetItemReference(slot, item, mod));
        if (civilianItems != null)
            foreach (var (slot, item, mod) in civilianItems)
                preset.CivilianItems.Add(new HoNPresetItemReference(slot, item, mod));

        var state = new Dictionary<string, List<HoNEquipmentPreset>>
        {
            [heroId] = new List<HoNEquipmentPreset> { preset }
        };
        _sut.RestoreFromSerializableState(state);
        return preset;
    }
}
