namespace TAOM.Features.CastleRecruitment;

/// <summary>
/// Pure scan over a notable's volunteer slots, with no TaleWorlds types so it is unit-testable. The AI
/// scoring gate (<c>CastleAiToggle</c>) opens only for castles where this finds a volunteer.
/// </summary>
public static class CastleVolunteerScan
{
    /// <summary>Matches the own-faction slot count in vanilla's
    /// <c>AiVisitSettlementBehavior.GetApproximateVolunteersCanBeRecruitedDataFromSettlement</c>:
    /// only slots 0..3 feed the recruit score; slots 4 and 5 are not counted.
    /// This is the estimate's slot count, not a lord's recruitable range. Vanilla's 0.25 floor fires when
    /// its own estimate is zero, so 4 is the exact trigger for own-faction lords (a lord at relation 0-4
    /// recruits only slots 0-2, as at a town). A lord of a non-kingdom faction counts 2 slots at a foreign
    /// castle, so a castle stocked only in slots 2-3 still gives him the 0.16 floor.</summary>
    public const int OwnFactionSlotCount = 4;

    public static bool HasVolunteerInScoredSlots<T>(T?[]? slots) where T : class
    {
        if (slots == null) return false;
        int count = slots.Length < OwnFactionSlotCount ? slots.Length : OwnFactionSlotCount;
        for (int i = 0; i < count; i++)
        {
            if (slots[i] != null) return true;
        }
        return false;
    }
}
