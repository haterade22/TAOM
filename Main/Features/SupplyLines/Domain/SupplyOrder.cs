using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.SaveSystem;

namespace TAOM.Features.SupplyLines.Domain;

/// <summary>Where a supply order can stand. The source module carried three more values
/// (Arrived, PartiallyDelivered, Cancelled) that nothing ever assigned; they are not ported.</summary>
public enum SupplyOrderStatus
{
    Ordered = 0,
    InTransit = 1,
    Delivered = 2,
    Lost = 3,
}

public enum SupplyEscortOption
{
    None = 0,
    Mercenaries = 1,
    Companion = 2,
}

/// <summary>
/// One resupply order: goods and troops bought from a settlement (or a friendly lord), carried to
/// the player by a spawned caravan party.
///
/// <para>Persisted via <see cref="SupplyLinesSaveDefiner"/>. Travel progress is never stored:
/// <see cref="DispatchTime"/> plus <see cref="PlannedHours"/> derive it, so a mid-transit save
/// resumes exactly (the FieldCamp/Refuge build timers use the same shape).</para>
/// </summary>
public sealed class SupplyOrder
{
    [SaveableField(101)] public string OrderId;
    [SaveableField(102)] public string SourceSettlementId;
    [SaveableField(103)] public string SourceHeroId;
    [SaveableField(104)] public Dictionary<string, int> Goods = new Dictionary<string, int>();
    [SaveableField(105)] public Dictionary<string, int> Recruits = new Dictionary<string, int>();
    [SaveableField(106)] public int Status;
    [SaveableField(107)] public int Escort;
    [SaveableField(108)] public string EscortHeroId;
    [SaveableField(109)] public string CaravanPartyId;
    [SaveableField(110)] public int TotalPaid;
    [SaveableField(111)] public CampaignTime DispatchTime;
    [SaveableField(112)] public float PlannedHours;

    public SupplyOrderStatus StatusEnum
    {
        get => (SupplyOrderStatus)Status;
        set => Status = (int)value;
    }

    public SupplyEscortOption EscortEnum
    {
        get => (SupplyEscortOption)Escort;
        set => Escort = (int)value;
    }

    public bool IsFromLord => !string.IsNullOrEmpty(SourceHeroId);

    /// <summary>Elapsed travel time over planned, unclamped. 1.0 = nominally arrived.</summary>
    public float ElapsedFraction()
    {
        if (PlannedHours <= 0f)
            return 1f;
        return (float)DispatchTime.ElapsedHoursUntilNow / PlannedHours;
    }
}
