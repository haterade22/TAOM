using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TAOM.Adapters;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.Core;
using TaleWorlds.Library;

namespace TAOM.Tests.Adapters;

/// <summary>
/// #954: <c>VillageRaidReader.IsRaiding</c> against a staged raid. The commander is the raid's
/// attacker; the answer turns on who defends: the village alone (raiding), or also a mobile lord
/// party with healthy troops (a relief lord, so not raiding). Engine objects are built bare, as the
/// Refuge and FiefGranting tests do.
/// </summary>
[TestClass]
[TestCategory("RequiresGame")]
public class VillageRaidReaderTests
{
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    private static void SetProperty(object target, string name, object value)
    {
        var setter = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{name} lost its setter; the test cannot stage the raid.");
        setter!.Invoke(target, new[] { value });
    }

    private static void SetField(object target, string name, object value)
    {
        var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone; the test cannot stage the raid.");
        field!.SetValue(target, value);
    }

    private static PartyBase PartyOf(MobileParty mobile)
    {
        var party = Bare<PartyBase>();
        SetProperty(party, nameof(PartyBase.MobileParty), mobile);
        if (mobile != null)
            SetProperty(mobile, nameof(MobileParty.Party), party);
        return party;
    }

    private static PartyBase MobileLord(bool lord, int healthy)
    {
        var mobile = Bare<MobileParty>();
        SetProperty(mobile, nameof(MobileParty.IsLordParty), lord);
        var roster = Bare<TroopRoster>();
        SetField(roster, "_totalRegulars", healthy);
        var party = PartyOf(mobile);
        SetProperty(party, nameof(PartyBase.MemberRoster), roster);
        return party;
    }

    private static MapEventSide SideOf(MapEvent mapEvent, BattleSideEnum side, params PartyBase[] parties)
    {
        var members = new MBList<MapEventParty>();
        foreach (var party in parties)
        {
            var member = Bare<MapEventParty>();
            SetProperty(member, nameof(MapEventParty.Party), party);
            members.Add(member);
        }
        var s = Bare<MapEventSide>();
        SetField(s, "_battleParties", members);
        SetField(s, "_mapEvent", mapEvent);
        SetProperty(s, nameof(MapEventSide.MissionSide), side);
        foreach (var party in parties)
            SetField(party, "_mapEventSide", s);
        return s;
    }

    /// <summary>A raid with the commander attacking and the given parties defending; the commander can
    /// be placed on the defender side instead (shape e).</summary>
    private static MobileParty Raid(bool commanderDefends, params PartyBase[] defenders)
    {
        var mapEvent = Bare<MapEvent>();
        SetField(mapEvent, "_mapEventType", MapEvent.BattleTypes.Raid);

        var commander = PartyOf(Bare<MobileParty>());
        var sides = new MapEventSide[2];
        SetField(mapEvent, "_sides", sides);
        var allDefenders = commanderDefends ? Concat(defenders, commander) : defenders;
        sides[0] = SideOf(mapEvent, BattleSideEnum.Defender, allDefenders);
        sides[1] = SideOf(mapEvent, BattleSideEnum.Attacker, commanderDefends ? new PartyBase[0] : new[] { commander });
        return commander.MobileParty;
    }

    private static PartyBase[] Concat(PartyBase[] a, PartyBase b)
    {
        var all = new PartyBase[a.Length + 1];
        a.CopyTo(all, 0);
        all[a.Length] = b;
        return all;
    }

    private static PartyBase Village() => PartyOf(null);

    [TestMethod]
    public void IsRaiding_VillageAloneDefends_ReturnsTrue()
        => Assert.IsTrue(VillageRaidReader.IsRaiding(Raid(false, Village())));

    [TestMethod]
    public void IsRaiding_LordWithHealthyTroopsDefends_ReturnsFalse()
        => Assert.IsFalse(VillageRaidReader.IsRaiding(Raid(false, Village(), MobileLord(lord: true, healthy: 20))));

    [TestMethod]
    public void IsRaiding_LordWithNoHealthyTroopsDefends_ReturnsTrue()
        => Assert.IsTrue(VillageRaidReader.IsRaiding(Raid(false, Village(), MobileLord(lord: true, healthy: 0))));

    [TestMethod]
    public void IsRaiding_NonLordMobilePartyDefends_ReturnsTrue()
        => Assert.IsTrue(VillageRaidReader.IsRaiding(Raid(false, Village(), MobileLord(lord: false, healthy: 20))));

    [TestMethod]
    public void IsRaiding_CommanderOnDefenderSide_ReturnsFalse()
        => Assert.IsFalse(VillageRaidReader.IsRaiding(Raid(true, Village())));

    [TestMethod]
    public void IsRaiding_EngineReadThrows_FailsOpenToFalse()
    {
        // A missing defender side makes the engine read throw; the contract is false, which joins.
        var commander = Raid(false, Village());
        ((MapEventSide[])typeof(MapEvent).GetField("_sides", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(commander.MapEvent))[0] = null;
        Assert.IsFalse(VillageRaidReader.IsRaiding(commander));
    }
}
