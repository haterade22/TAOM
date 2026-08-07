using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.MapEvents;
using TaleWorlds.CampaignSystem.Party;

namespace TAOM.Tests.Features.Enlistment;

/// <summary>
/// Binding pins for the two engine events the real-time service loop depends on. Both are resolved
/// by name against the installed assemblies, so an engine bump that renames or reshapes either one
/// fails here rather than silently disabling the pump in-game — which is precisely how the original
/// battle-join bug stayed invisible.
/// </summary>
[TestClass]
[TestCategory("BindingVerification")]
public class EnlistmentTickBindingTests
{
    private static PropertyInfo Event(string name) =>
        typeof(CampaignEvents).GetProperty(name, BindingFlags.Public | BindingFlags.Static);

    [TestMethod]
    public void TickEvent_ExistsAndCarriesTheFrameDelta()
    {
        var prop = Event(nameof(CampaignEvents.TickEvent));

        Assert.IsNotNull(prop, "CampaignEvents.TickEvent is gone — the maintenance pump has no campaign source.");
        Assert.AreEqual(typeof(IMbEvent<float>), prop.PropertyType,
            "TickEvent must carry the frame delta; the pump's shared budget is accumulated from it.");
    }

    /// <summary>
    /// The zero-poll answer to a commander joining an ALREADY-RUNNING battle.
    /// <c>CampaignEventDispatcher.OnMapEventStarted</c> is dispatched exactly once, as the last
    /// statement of <c>MapEvent.Initialize</c> — it announces battle CREATION only. This event is
    /// dispatched from <c>MapEvent.AddInvolvedPartyInternal</c> and is the only edge that sees a
    /// late join.
    /// </summary>
    [TestMethod]
    public void OnPartyAddedToMapEventEvent_ExistsAndCarriesThePartyThatJoined()
    {
        var prop = Event(nameof(CampaignEvents.OnPartyAddedToMapEventEvent));

        Assert.IsNotNull(prop,
            "CampaignEvents.OnPartyAddedToMapEventEvent is gone — a commander joining a running battle becomes invisible again.");
        Assert.AreEqual(typeof(IMbEvent<PartyBase>), prop.PropertyType,
            "The event must carry the joining PartyBase so the pump can match it against the commander's party.");
    }

    [TestMethod]
    public void MapEventStarted_StillCarriesTheEventAndBothSideLeaders()
    {
        // The edge that already worked — pinned so the existing join path cannot break silently.
        var prop = Event(nameof(CampaignEvents.MapEventStarted));

        Assert.IsNotNull(prop);
        Assert.AreEqual(typeof(IMbEvent<MapEvent, PartyBase, PartyBase>), prop.PropertyType);
    }
}
