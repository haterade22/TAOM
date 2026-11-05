using System;
using System.Reflection;
using System.Runtime.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Party.PartyComponents;
using TaleWorlds.CampaignSystem.Roster;
using TaleWorlds.CampaignSystem.Settlements;
using TAOM.Core.Logging;
using TAOM.Features.AlignmentDesertion;
using TAOM.Features.AlignmentDesertion.Hooks;

namespace TAOM.Tests.Features.AlignmentDesertion;

/// <summary>
/// #750 and PR #1064: a field party or garrison in a map event loses nobody that day, as in vanilla
/// desertion. The 1.4.x line has the guard since <c>1b00d1acd</c>, and this pins it. The parties are staged
/// from uninitialized engine objects with no campaign. Past the gate the handler reads
/// <c>Clan.PlayerClan</c> (v1.4.8 <c>Clan.cs:276</c>), which throws without a campaign, so "threw" means "got
/// past the gate" and "returned" means "stopped at it". Ported from <c>ab78564e0</c>, whose 1.5.x test
/// observes a co-op seam this line does not have.
/// </summary>
[TestClass]
public class AlignmentDesertionMapEventGateTests
{
    private static T Bare<T>() => (T)FormatterServices.GetUninitializedObject(typeof(T));

    // Walks the hierarchy: a private setter or field declared on a base type is invisible from the derived type.
    private static void SetField(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(target, value); return; }
        }
        Assert.Fail($"{target.GetType().Name}.{name} is gone; the test cannot stage the party.");
    }

    private static void SetProperty(object target, string name, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var setter = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                ?.GetSetMethod(nonPublic: true);
            if (setter != null) { setter.Invoke(target, new[] { value }); return; }
        }
        Assert.Fail($"{target.GetType().Name}.{name} lost its setter; the test cannot stage the party.");
    }


    [DataTestMethod]
    [DataRow("OnDailyTickParty", true)]
    [DataRow("OnDailyTickParty", false)]
    [DataRow("OnDailyTickSettlement", true)]
    [DataRow("OnDailyTickSettlement", false)]
    public void DailyTick_PartyInAMapEvent_StopsBeforeTheOwnerRead(string handler, bool inMapEvent)
    {
        var service = Substitute.For<IAlignmentDesertionService>();
        service.IsEnabled.Returns(true);
        var sut = new AlignmentDesertionBehavior(service, Substitute.For<IModLogger>());
        var clan = Bare<Clan>();
        SetField(clan, "_kingdom", Bare<Kingdom>());
        var mobile = Bare<MobileParty>();
        var party = Bare<PartyBase>();
        SetProperty(mobile, nameof(MobileParty.Party), party);
        SetProperty(party, nameof(PartyBase.MemberRoster), Bare<TroopRoster>());
        if (inMapEvent)
        {
            var side = Bare<MapEventSide>();
            SetField(side, "_mapEvent", Bare<MapEvent>());
            SetField(party, "_mapEventSide", side);
        }

        object target;
        if (handler == "OnDailyTickParty")
        {
            var hero = Bare<Hero>();
            SetField(hero, "_clan", clan);
            var component = Substitute.For<PartyComponent>();
            component.Leader.Returns(hero);
            SetProperty(mobile, nameof(MobileParty.IsLordParty), true);
            SetField(mobile, "_partyComponent", component);
            target = mobile;
        }
        else
        {
            var garrison = Bare<GarrisonPartyComponent>();
            SetProperty(garrison, nameof(PartyComponent.MobileParty), mobile);
            var town = Bare<Town>();
            town.GarrisonPartyComponent = garrison;
            SetField(town, "_ownerClan", clan);
            var settlement = Bare<Settlement>();
            settlement.Town = town;
            target = settlement;
        }
        var method = typeof(AlignmentDesertionBehavior).GetMethod(handler, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "AlignmentDesertionBehavior." + handler);

        Exception? thrown = null;
        try { method!.Invoke(sut, new[] { target }); }
        catch (TargetInvocationException ex) { thrown = ex.InnerException; }

        if (inMapEvent)
            Assert.IsNull(thrown, "a party in a map event must return before the owner read: " + thrown);
        else
            Assert.IsInstanceOfType(thrown, typeof(NullReferenceException),
                "out of battle the handler reaches Clan.PlayerClan, which needs a live campaign");
        service.DidNotReceiveWithAnyArgs().CalculateDesertion(default!, default, default, default!);
    }
}
