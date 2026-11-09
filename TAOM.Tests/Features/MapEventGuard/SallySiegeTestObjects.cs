using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.Siege;
using TaleWorlds.Library;

namespace TAOM.Tests.Features.MapEventGuard;

/// <summary>Identity-only engine objects for the sally-out guard, in the shape of VillageRaidReaderTests.</summary>
internal static class SallySiegeTestObjects
{
    internal static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    internal static void SetProperty(object target, string name, object? value)
    {
        var setter = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetSetMethod(nonPublic: true);
        Assert.IsNotNull(setter, $"{target.GetType().Name}.{name} lost its setter; the test cannot stage the siege.");
        setter!.Invoke(target, new[] { value });
    }

    private static void SetField(object target, string name, object? value)
    {
        var field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(field, $"{target.GetType().Name}.{name} is gone; the test cannot stage the siege.");
        field!.SetValue(target, value);
    }

    internal static MapEvent Battle(MapEvent.BattleTypes type = MapEvent.BattleTypes.FieldBattle)
    {
        var mapEvent = Bare<MapEvent>();
        if (type == MapEvent.BattleTypes.FieldBattle)
        {
            SetProperty(mapEvent, nameof(MapEvent.Component), Bare<FieldBattleEventComponent>());
            return mapEvent;
        }

        var component = Bare<SiegeOutsideEventComponent>();
        SetField(component, "_eventType", type);
        SetProperty(mapEvent, nameof(MapEvent.Component), component);
        return mapEvent;
    }

    private static MobileParty PartyIn(MapEvent? mapEvent, TroopRosterElement[] entries)
    {
        var mobile = Bare<MobileParty>();
        var party = Bare<PartyBase>();
        SetProperty(mobile, nameof(MobileParty.Party), party);
        SetProperty(party, nameof(PartyBase.MobileParty), mobile);

        if (mapEvent != null)
        {
            var side = Bare<MapEventSide>();
            SetField(side, "_mapEvent", mapEvent);
            SetField(party, "_mapEventSide", side);
        }

        var roster = Bare<TroopRoster>();
        var list = new MBList<TroopRosterElement>();
        list.AddRange(entries);
        SetField(roster, "_troopRosterElements", list);
        SetProperty(party, nameof(PartyBase.MemberRoster), roster);
        return mobile;
    }

    internal static (Settlement Settlement, MobileParty Main) Scene(MapEvent? leaderBattle, MapEvent playerBattle, TroopRosterElement[] leaderRoster)
    {
        var camp = Bare<BesiegerCamp>();
        SetField(camp, "_leaderParty", PartyIn(leaderBattle, leaderRoster));
        var siege = Bare<SiegeEvent>();
        SetField(siege, nameof(SiegeEvent.BesiegerCamp), camp);
        var settlement = Bare<Settlement>();
        SetProperty(settlement, nameof(Settlement.SiegeEvent), siege);
        return (settlement, PartyIn(playerBattle, Array.Empty<TroopRosterElement>()));
    }

    internal static TroopRosterElement HeroEntry(KillCharacterAction.KillCharacterActionDetail mark)
    {
        var hero = Bare<Hero>();
        SetProperty(hero, nameof(Hero.DeathMark), mark);
        var character = Bare<CharacterObject>();
        SetField(character, "_heroObject", hero);
        return new TroopRosterElement(character) { Number = 1 };
    }

    internal static TroopRosterElement TroopEntry(int number) =>
        new TroopRosterElement(Bare<CharacterObject>()) { Number = number };
}
