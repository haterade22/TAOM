using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.Refuge.Domain;

public enum RefugeTier
{
    Refuge = 0,
    Stronghold = 1,
}

/// <summary>
/// One standing refuge, keyed in the book by its party StringId. The party itself (roster, stash,
/// prisoners, position) rides the vanilla save through <c>RefugePartyComponent</c>; this record
/// carries everything else.
///
/// <para>Build progress derives from <see cref="BuildStartTime"/> + <see cref="BuildTargetHours"/>
/// (same shape as camps and supply orders: mid-build saves resume free).</para>
///
/// <para><see cref="MilitiaAdded"/>/<see cref="MilitiaTroopId"/>/<see cref="MilitiaPreRallyCount"/>
/// persist the rally bookkeeping. The source kept it in a transient dictionary, so a save
/// mid-battle baked the militia into the garrison forever, and removal deleted every troop of
/// that type including ones the player had garrisoned. Removal now takes
/// min(recorded, present - pre-rally baseline): casualties attribute to militia first, and the
/// garrison's own stack of the same type survives.</para>
/// </summary>
public sealed class RefugeData
{
    [SaveableField(101)] public string PartyId;
    [SaveableField(102)] public int Tier;
    [SaveableField(103)] public string WardenHeroId;
    [SaveableField(104)] public bool Fortified;
    [SaveableField(105)] public bool Established;
    [SaveableField(106)] public CampaignTime FoundedTime;
    [SaveableField(107)] public bool WardenPromoted;
    [SaveableField(108)] public string PromotedFromTroopId;
    [SaveableField(109)] public bool Building;
    [SaveableField(110)] public bool BuildingUpgrade;
    [SaveableField(111)] public CampaignTime BuildStartTime;
    [SaveableField(112)] public float BuildTargetHours;
    [SaveableField(113)] public int MilitiaAdded;
    [SaveableField(114)] public string MilitiaTroopId;

    /// <summary>Garrison count of <see cref="MilitiaTroopId"/> BEFORE the rally added its stack.
    /// Stand-down removes min(MilitiaAdded, present - this), so casualties are attributed to
    /// militia first and a player-garrisoned stack of the same troop type is never deleted.</summary>
    [SaveableField(115)] public int MilitiaPreRallyCount;

    public RefugeTier TierEnum
    {
        get => (RefugeTier)Tier;
        set => Tier = (int)value;
    }

    public float BuildProgress()
    {
        if (BuildTargetHours <= 0f)
            return 1f;
        return (float)BuildStartTime.ElapsedHoursUntilNow / BuildTargetHours;
    }

    public bool IsReady => Established && !Building;

    /// <summary>An orphan party adopted on load without a book row: never established, not
    /// building, so <see cref="IsReady"/> can never become true. Dismantle is its only exit
    /// (it would otherwise consume a refuge-cap slot forever).</summary>
    public bool IsOrphanAdopted => !Established && !Building;
}
