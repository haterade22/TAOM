using System;
using TAOM.Features.Enlistment.Content.Domain;
using TAOM.Features.TroopProgression;

namespace TAOM.Features.Enlistment.Duties;

public class DutyRotationPolicy : IDutyRotationPolicy
{
    /// <summary>Roll precision — 1000 gives 0.1% granularity over the config's [0,1] chance floats.</summary>
    private const int RollPrecision = 1000;
    private const float PressureChanceBonus = 0.15f;
    private const float TrustChanceBonusPerPoint = 0.01f;

    private readonly IRandomProvider _random;

    public DutyRotationPolicy(IRandomProvider random)
    {
        _random = random;
    }

    public bool ShouldOfferDuty(SchedulerConfig scheduler, int daysServed, int trust, bool pressure, double nowDays, double? lastOfferDay)
    {
        if (scheduler == null || daysServed < scheduler.MinDaysBeforeFirstOffer)
            return false;

        if (!lastOfferDay.HasValue)
            return true;

        var daysSince = nowDays - lastOfferDay.Value;
        var cooldown = pressure ? scheduler.OfferCooldownDaysPressure : scheduler.OfferCooldownDaysQuiet;
        if (daysSince < cooldown)
            return false;

        var guaranteed = pressure ? scheduler.GuaranteedOfferDaysPressure : scheduler.GuaranteedOfferDaysQuiet;
        if (daysSince >= guaranteed)
            return true;

        var chance = scheduler.BaseOfferChance
            + (pressure ? PressureChanceBonus : 0f)
            + Math.Max(0, trust) * TrustChanceBonusPerPoint;
        chance = Math.Min(scheduler.MaxOfferChance, Math.Max(scheduler.BaseOfferChance, chance));

        var roll = _random.Next(RollPrecision);
        return roll < (int)(chance * RollPrecision);
    }

    public bool ShouldRollIncident(SchedulerConfig scheduler, int daysServed, double nowDays, double? lastIncidentDay)
    {
        if (scheduler == null || daysServed < scheduler.IncidentMinDaysServed)
            return false;

        if (lastIncidentDay.HasValue && nowDays - lastIncidentDay.Value < scheduler.IncidentCooldownDays)
            return false;

        return true;
    }
}
