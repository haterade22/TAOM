using TAOM.Adapters;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.ArmyTargeting;

/// <summary>
/// Keeps the ArmyTargeting singletons honest across a campaign's lifetime.
///
/// <para>Both <c>IMapReachAdapter</c> and <c>ITargetScoreContextFactory</c> are registered
/// <c>Reuse.Singleton</c> in a container built once in <c>OnSubModuleLoad</c>, so they outlive any
/// one campaign. Two consequences needed a real lifecycle hook rather than a heuristic:</para>
///
/// <list type="bullet">
/// <item>The reach cache keys on a faction's fief COUNT, which cannot see a same-count exchange:
/// one fortification lost and another gained in the same transfer leaves the count unchanged while
/// every cached distance is now measured against the wrong anchor set. This listens to the transfer
/// itself and invalidates both the losing and the gaining faction.</item>
/// <item>Cleanup was call-driven, so a finalized campaign's <c>Settlement</c> graph stayed reachable
/// from a process-lifetime singleton until the next campaign happened to score a target. On a build
/// whose open crash report is a 20.3 GB commit, adding a root that survives
/// <c>Campaign.OnDestroy</c> is not acceptable.</item>
/// </list>
///
/// <para>Holds no state of its own, so <c>SyncData</c> is empty and nothing here touches a save.</para>
/// </summary>
public class ArmyTargetingLifecycleBehavior : CampaignBehaviorBase
{
    private readonly IMapReachAdapter _reach;

    public ArmyTargetingLifecycleBehavior(IMapReachAdapter reach)
    {
        _reach = reach;
    }

    public override void RegisterEvents()
    {
        CampaignEvents.OnSettlementOwnerChangedEvent.AddNonSerializedListener(this, OnSettlementOwnerChanged);
        CampaignEvents.OnNewGameCreatedEvent.AddNonSerializedListener(this, (_) => _reach.Reset());
        CampaignEvents.OnGameLoadedEvent.AddNonSerializedListener(this, (_) => _reach.Reset());
    }

    public override void SyncData(IDataStore dataStore)
    {
        // No persisted state: every cache here is derived and rebuilt on demand.
    }

    /// <summary>
    /// Invalidates the reach cache for both sides of a fief transfer. Ownership at this point has
    /// already moved, so the losing side is read from <paramref name="oldOwner"/> rather than from
    /// the settlement.
    /// </summary>
    internal void OnSettlementOwnerChanged(
        Settlement settlement,
        bool openToClaim,
        Hero newOwner,
        Hero oldOwner,
        Hero capturerHero,
        ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail detail)
    {
        // `?.` throughout: these are computed properties on sealed types and can throw before a
        // plain null check runs (adapters.md).
        _reach.InvalidateFaction(newOwner?.MapFaction?.StringId);
        _reach.InvalidateFaction(oldOwner?.MapFaction?.StringId);
        _reach.InvalidateFaction(settlement?.MapFaction?.StringId);
    }
}
