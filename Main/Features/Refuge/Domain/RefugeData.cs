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
/// <para><see cref="MilitiaAdded"/>/<see cref="MilitiaTroopId"/> persist the rally bookkeeping.
/// The source kept it in a transient dictionary, so a save mid-battle baked the militia into the
/// garrison forever, and removal deleted every troop of that type including ones the player had
/// garrisoned. Removal now takes min(recorded, present).</para>
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
}
