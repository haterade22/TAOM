using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

namespace TAOM.Features.CareerSystem.Abilities;

/// <summary>
/// Issue #383 boundary reporter — owns the combat-log attribution line and the
/// once-per-activation "no damage component" notice. Extracted from
/// CareerPerkMissionBehavior.OnScoreHit (deep-review 2026-08-05, ADR-002 thinning);
/// constructed per mission by the behavior so the notice flag dies with the mission.
/// The bonus value is TAOM's own applied buff state (CareerAbilityBuffTracker — with
/// #377's contribution counting, entry presence means the ability window is live);
/// share math lives in the unit-tested AbilityDamageAttribution.
/// </summary>
public sealed class AbilityDamageAttributionReporter
{
    private readonly ICareerConfigProvider _config;
    private bool _zeroBonusNoticeShown;

    public AbilityDamageAttributionReporter(ICareerConfigProvider config)
    {
        _config = config;
    }

    /// <summary>Re-arms the once-per-activation zero-bonus notice.</summary>
    public void OnAbilityActivated() => _zeroBonusNoticeShown = false;

    public void ReportHit(string heroStringId, string targetName, float damagedHp)
    {
        var buff = CareerAbilityBuffTracker.GetBuff(heroStringId);
        if (buff == null) return; // no live ability window

        if (buff.DamageBonus <= 0f)
        {
            // Utility ability (draw speed / movement / reduction): saying so once per
            // activation beats silence that reads as a broken feature.
            if (!_zeroBonusNoticeShown)
            {
                _zeroBonusNoticeShown = true;
                InformationManager.DisplayMessage(new InformationMessage(
                    new TextObject("{=taom_career_dmg_none}Career ability active — this ability boosts something other than damage.").ToString(),
                    Colors.Gray));
            }
            return;
        }

        var bonusShare = AbilityDamageAttribution.ComputeBonusDamage(damagedHp, buff.DamageBonus);
        var minReportable = _config?.GetAbilityTuning()?.Global?.MinReportableBonusDamage ?? 0.5f;
        if (!AbilityDamageAttribution.ShouldReport(bonusShare, minReportable)) return;

        var line = new TextObject("{=taom_career_dmg_attrib}{TARGET}: {DMG} damage (+{BONUS} from ability)");
        line.SetTextVariable("TARGET", targetName ?? "");
        line.SetTextVariable("DMG", (int)damagedHp);
        line.SetTextVariable("BONUS", (int)System.Math.Round(bonusShare));
        InformationManager.DisplayMessage(new InformationMessage(line.ToString(), Colors.Yellow));
    }
}
