namespace TAOM.Features.ArmourAcquisition;

/// <summary>
/// "A Lord's Harness Unclaimed", the event route to lord kit (docs/features/armour-acquisition.md): after a
/// battle the player's side won against at least one enemy lord, off cooldown, a rare roll finds his
/// household's harness among the spoils. A TAOM inquiry event with its cooldown in SyncData rather than a
/// vanilla IncidentManager incident, whose saved cooldown dictionary would hold TAOM objects in the save.
/// </summary>
public static class LordHarnessEventPolicy
{
    public static bool ShouldTrigger(bool playerWon, int defeatedLordCount, int today, int? lastEventDay,
        int cooldownDays, float chance, double roll)
    {
        if (!playerWon || defeatedLordCount <= 0)
            return false;
        if (lastEventDay.HasValue && today - lastEventDay.Value < cooldownDays)
            return false;
        // Positive requirement: a NaN roll or chance fails it. Compared in float, the chance's own precision:
        // widened to double, 0.1f is 0.1000000015 and a roll of 0.1 would slip under it.
        return (float)roll < chance;
    }
}
