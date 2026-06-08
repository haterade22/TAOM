using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;
using TaleWorlds.ObjectSystem;

namespace TAOM.Tests.Features.Music;

[TestClass]
public class MusicContextAdapterBindingTests
{
    [TestMethod]
    public void Mission_HasExpectedV145ContextProperties()
    {
        AssertProperty(typeof(Mission), nameof(Mission.Current), typeof(Mission), isStatic: true);
        AssertProperty(typeof(Mission), nameof(Mission.IsFinalized), typeof(bool));
        AssertProperty(typeof(Mission), nameof(Mission.SceneName), typeof(string));
        AssertProperty(typeof(Mission), nameof(Mission.IsFieldBattle), typeof(bool));
        AssertProperty(typeof(Mission), nameof(Mission.IsSiegeBattle), typeof(bool));
        AssertProperty(typeof(Mission), nameof(Mission.IsSallyOutBattle), typeof(bool));
        AssertProperty(typeof(Mission), nameof(Mission.IsNavalBattle), typeof(bool));
    }

    [TestMethod]
    public void CampaignAndParty_HaveExpectedV145ContextProperties()
    {
        AssertProperty(typeof(Campaign), nameof(Campaign.Current), typeof(Campaign), isStatic: true);
        AssertProperty(typeof(Campaign), nameof(Campaign.MainParty), typeof(MobileParty));
        AssertProperty(typeof(MobileParty), nameof(MobileParty.MainParty), typeof(MobileParty), isStatic: true);
        AssertProperty(typeof(MobileParty), nameof(MobileParty.CurrentSettlement), typeof(Settlement));
        AssertProperty(typeof(MobileParty), nameof(MobileParty.MapEvent), typeof(MapEvent));
        AssertProperty(typeof(MobileParty), nameof(MobileParty.MapFaction), typeof(IFaction));
        AssertProperty(typeof(MobileParty), nameof(MobileParty.BesiegedSettlement), typeof(Settlement));
        AssertProperty(typeof(MobileParty), nameof(MobileParty.IsCurrentlyAtSea), typeof(bool));
    }

    [TestMethod]
    public void SettlementAndCulture_HaveExpectedV145ContextProperties()
    {
        AssertField(typeof(Settlement), nameof(Settlement.Culture), typeof(CultureObject));
        AssertProperty(typeof(Settlement), nameof(Settlement.CurrentSettlement), typeof(Settlement), isStatic: true);
        AssertProperty(typeof(Settlement), nameof(Settlement.IsTown), typeof(bool));
        AssertProperty(typeof(Settlement), nameof(Settlement.IsCastle), typeof(bool));
        AssertProperty(typeof(Settlement), nameof(Settlement.IsVillage), typeof(bool));
        AssertProperty(typeof(Settlement), nameof(Settlement.IsUnderSiege), typeof(bool));
        AssertProperty(typeof(IFaction), nameof(IFaction.Culture), typeof(CultureObject));
        AssertProperty(typeof(MBObjectBase), nameof(MBObjectBase.StringId), typeof(string));
        Assert.IsTrue(typeof(MBObjectBase).IsAssignableFrom(typeof(CultureObject)));
        Assert.IsTrue(typeof(MBObjectBase).IsAssignableFrom(typeof(Settlement)));
    }

    [TestMethod]
    public void MapEvent_HasExpectedV145ContextProperties()
    {
        AssertProperty(typeof(MapEvent), nameof(MapEvent.EventType), typeof(MapEvent.BattleTypes));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsFieldBattle), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsRaid), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsHideoutBattle), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsSiegeAssault), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsSallyOut), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsSiegeOutside), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsBlockade), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsBlockadeSallyOut), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsSiegeAmbush), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsFinalized), typeof(bool));
        AssertProperty(typeof(MapEvent), nameof(MapEvent.IsPlayerMapEvent), typeof(bool));
    }

    private static void AssertProperty(Type type, string name, Type propertyType, bool isStatic = false)
    {
        var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        Assert.IsNotNull(property, $"{type.FullName}.{name} is missing.");
        Assert.AreEqual(propertyType, property.PropertyType, $"{type.FullName}.{name} has unexpected type.");
        var getter = property.GetGetMethod();
        Assert.IsNotNull(getter, $"{type.FullName}.{name} has no public getter.");
        Assert.AreEqual(isStatic, getter.IsStatic, $"{type.FullName}.{name} has unexpected static/instance binding.");
    }

    private static void AssertField(Type type, string name, Type fieldType)
    {
        var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
        Assert.IsNotNull(field, $"{type.FullName}.{name} is missing.");
        Assert.AreEqual(fieldType, field.FieldType, $"{type.FullName}.{name} has unexpected type.");
    }
}
