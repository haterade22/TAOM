using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Runtime toggle consulted by the AI-scoring transpiler. The transpiler replaces the single
/// <c>settlement.IsCastle</c> call inside <c>AiVisitSettlementBehavior.AiHourlyTick</c>'s
/// <c>!settlement.IsCastle &amp;&amp; ...</c> recruitment gate with a call to
/// <see cref="IsExcludedFromRecruitScoring"/>, which has the identical stack shape (Settlement to bool).
/// True means "excluded from the recruit-desirability block at AiHourlyTick :279": every non-castle
/// returns false at once (vanilla's own <c>!IsCastle</c> result), and a castle returns true when AI
/// castle recruitment is off or no live notable holds a volunteer in slots 0-3 (<see cref="CastleVolunteerScan"/>),
/// so an empty castle keeps vanilla's score (no recruit pull, no 0.25/0.16 floor). The x1.4 castle
/// visit bonus at :327 applies to every castle regardless; the second get_IsCastle that feeds it is
/// never touched. This keeps the feature MCM-toggleable at runtime without re-patching.
/// </summary>
public static class CastleAiToggle
{
    private static ICastleRecruitmentSettingsProvider? _settings;

    public static void Initialize(ICastleRecruitmentSettingsProvider settings) => _settings = settings;

    public static bool IsExcludedFromRecruitScoring(Settlement settlement)
    {
        if (!settlement.IsCastle) return false;
        bool aiOn = (_settings?.IsEnabled ?? false) && (_settings?.IsAiEnabled ?? false);
        if (!aiOn) return true;

        var notables = settlement.Notables;
        for (int i = 0; i < notables.Count; i++)
        {
            var hero = notables[i];
            if (!hero.IsAlive) continue;
            if (CastleVolunteerScan.HasVolunteerInScoredSlots(hero.VolunteerTypes)) return false;
        }
        return true;
    }
}
