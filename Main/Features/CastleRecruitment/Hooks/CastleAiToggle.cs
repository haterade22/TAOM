using TaleWorlds.CampaignSystem.Settlements;

namespace TAOM.Features.CastleRecruitment.Hooks;

/// <summary>
/// Runtime toggle consulted by the AI-scoring transpilers. The transpilers replace the single
/// <c>settlement.IsCastle</c> call inside <c>AiVisitSettlementBehavior</c>'s
/// <c>!settlement.IsCastle &amp;&amp; ...</c> recruitment gates with a call to
/// <see cref="IsCastleAndAiDisabled"/> — identical stack shape (Settlement → bool), but the result
/// is forced to <c>false</c> when AI castle recruitment is enabled, so the <c>!IsCastle</c> gate
/// passes for castles. When disabled it returns the real <c>IsCastle</c>, preserving vanilla scoring.
/// This keeps the feature MCM-toggleable at runtime without re-patching.
/// </summary>
public static class CastleAiToggle
{
    private static ICastleRecruitmentSettingsProvider? _settings;

    public static void Initialize(ICastleRecruitmentSettingsProvider settings) => _settings = settings;

    public static bool IsCastleAndAiDisabled(Settlement settlement)
    {
        bool aiOn = (_settings?.IsEnabled ?? false) && (_settings?.IsAiEnabled ?? false);
        return !aiOn && settlement.IsCastle;
    }
}
