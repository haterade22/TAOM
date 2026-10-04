using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.ObjectSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Tests.Features.CultureMarketplace;

/// <summary>
/// <see cref="TownRosterAdapter.GetItemCounts"/> run for real: a real <see cref="ItemRoster"/> behind a bare
/// Settlement, and real <see cref="ItemObject"/>s in a real object manager. The service tests fake this
/// adapter, so only these prove the one-walk count itself: every modifier stack of an item summed, each count
/// in the slot of the id that asked for it, an unknown id a zero rather than a failed walk, and a walk that
/// throws partway discarding what it had counted.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class TownRosterAdapterGetItemCountsTests
{
    private IModLogger _logger = null!;
    private TownRosterAdapter _sut = null!;
    private MBObjectManager? _objects;

    [TestInitialize]
    public void Setup()
    {
        _logger = Substitute.For<IModLogger>();
        _sut = new TownRosterAdapter(_logger);
        // MBObjectManager.Instance is process-wide and null in every other test; Teardown puts it back.
        _objects = MBObjectManager.Init();
        _objects.RegisterType<ItemObject>("Item", "Items", 3u);
    }

    [TestCleanup]
    public void Teardown() => _objects?.Destroy();

    private ItemObject Item(string id) => _objects!.RegisterObject(new ItemObject(id));

    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void Stage(object target, string property, object value)
    {
        var setter = target.GetType().GetProperty(property)?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{property} lost its setter; the test cannot stage the town.");
        setter!.Invoke(target, new[] { value });
    }

    private static FieldInfo RosterField(string name)
    {
        var field = typeof(ItemRoster).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"ItemRoster.{name} is gone; the test cannot stage a failing walk.");
        return field!;
    }

    /// <summary>A settlement whose market is <paramref name="roster"/> (<c>Settlement.ItemRoster</c> is
    /// <c>Party.ItemRoster</c>).</summary>
    private static Settlement TownWith(ItemRoster roster)
    {
        var party = Bare<PartyBase>();
        Stage(party, nameof(PartyBase.ItemRoster), roster);
        var town = Bare<Settlement>();
        Stage(town, nameof(Settlement.Party), party);
        town.StringId = "town_I1";
        return town;
    }

    /// <summary>warg_brown in three stacks (plain 1, Sharp 3, Damaged 2: 6 in all) with other stock between them,
    /// warg_dark 4 and bread 7; warg_albino is a known item with none in stock.</summary>
    private Settlement TownStock()
    {
        var brown = Item("warg_brown");
        var dark = Item("warg_dark");
        var bread = Item("bread");
        Item("warg_albino");
        var sharp = new ItemModifier();
        var damaged = new ItemModifier();

        var roster = new ItemRoster();
        roster.AddToCounts(new EquipmentElement(brown, sharp), 3);
        roster.AddToCounts(dark, 4);
        roster.AddToCounts(new EquipmentElement(brown, damaged), 2);
        roster.AddToCounts(bread, 7);
        roster.AddToCounts(brown, 1);
        return TownWith(roster);
    }

    [TestMethod]
    public void GetItemCounts_ItemSplitAcrossModifierStacks_SumsEveryStackAndNothingElse()
    {
        var counts = _sut.GetItemCounts(TownStock(), new[] { "warg_brown", "warg_dark" });

        CollectionAssert.AreEqual(new[] { 6, 4 }, counts,
            "warg_brown is 1 + 3 + 2 across three stacks, warg_dark is 4; the bread and the albino count for neither");
    }

    [TestMethod]
    public void GetItemCounts_IdsInAnyOrder_EachCountLandsInTheSlotOfItsId()
    {
        // The roster order is brown, dark, brown, bread, brown; the request order differs and repeats an id.
        var counts = _sut.GetItemCounts(TownStock(), new[] { "bread", "warg_brown", "warg_dark", "warg_brown" });

        CollectionAssert.AreEqual(new[] { 7, 6, 4, 6 }, counts);
    }

    [TestMethod]
    public void GetItemCounts_UnknownEmptyNullAndUnstockedIds_AreZeroAndKeepTheirSlot()
    {
        // A null id must be refused before the object manager's dictionary lookup: that lookup throws on null,
        // and the failure path would then zero the real counts as well.
        var counts = _sut.GetItemCounts(TownStock(),
            new[] { "warg_brown", "ghost", "", null!, "warg_albino", "warg_dark" });

        CollectionAssert.AreEqual(new[] { 6, 0, 0, 0, 0, 4 }, counts);
        _logger.DidNotReceiveWithAnyArgs().LogError(default!);
    }

    [TestMethod]
    public void GetItemCounts_WalkThrowsPartway_LogsOneLineAndReturnsZeroesForEveryId()
    {
        var roster = new ItemRoster();
        roster.AddToCounts(Item("warg_brown"), 3);
        // The roster now claims one stack more than its backing array holds, so the walk counts the 3 at slot 0,
        // passes the empty slots and then throws at the first slot past the array.
        var data = (ItemRosterElement[]?)RosterField("_data").GetValue(roster);
        RosterField("_count").SetValue(roster, data!.Length + 1);

        var counts = _sut.GetItemCounts(TownWith(roster), new[] { "warg_brown", "warg_dark" });

        CollectionAssert.AreEqual(new[] { 0, 0 }, counts, "a failed walk returns zeroes, not the 3 it had counted");
        _logger.Received(1).LogError(Arg.Is<string>(line =>
            line.StartsWith("[CultureMarketplace] GetItemCounts('warg_brown','warg_dark' @ town_I1) failed: ")));
    }
}
