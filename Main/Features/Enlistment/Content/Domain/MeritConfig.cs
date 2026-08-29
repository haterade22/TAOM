using System.Collections.Generic;

namespace TAOM.Features.Enlistment.Content.Domain;

/// <summary>
/// Battle-merit scoring weights (the donor's mission sampler, config-typed). Score is
/// clamped 0-100: kills*killWeight (kills capped) + survival + cohesion + commander
/// proximity + engagement + role fit − fell-early penalty.
/// </summary>
public sealed class MeritScoringConfig
{
    public int KillWeight { get; set; } = 5;
    public int KillCountCap { get; set; } = 6;
    public int SurvivalWeight { get; set; } = 25;
    public int CohesionWeight { get; set; } = 15;
    public int CommanderProximityWeight { get; set; } = 10;
    public int EngagementWeight { get; set; } = 10;
    public int RoleFitBonus { get; set; } = 10;
    public int FellEarlyPenalty { get; set; } = 10;

    /// <summary>
    /// Subtracted when the player quit the field before the battle resolved — the retreat or
    /// surrender inquiry, or walking out of the battle boundary.
    ///
    /// <para>
    /// Read the ceiling carefully, because the original note here got it wrong in a way that later
    /// mattered. It said 30 was "sized to sink the best possible walkout into the bottom band",
    /// arriving at 45 by adding cohesion + commander + engagement + role fit and stopping there.
    /// That excludes kills, which <see cref="BattleMeritScorer"/> does not: only the survival term
    /// is zeroed on <see cref="MeritSample.LeftTheField"/>. With the kill cap counted the real
    /// ceiling is 30 + 15 + 10 + 10 + 10 − 30 = <b>45</b>, which is the `solid` band, not the bottom
    /// one. The sentence was true of a walkout that killed nobody and false of every other.
    /// </para>
    /// <para>
    /// Nothing rests on that ceiling any more. `EnlistmentBattlePayoutService` withholds band trust
    /// outright when the sample says LeftTheField, so a walkout cannot buy standing at any penalty
    /// value, and `MeritTrustFloorTests` pins the shipped ladder against the ceiling as well. Both
    /// exist because relying on a boundary that happens to sit above 45 is exactly how paying trust
    /// from `solid` quietly started rewarding quitters.
    /// </para>
    /// </summary>
    public int LeftFieldPenalty { get; set; } = 30;

    /// <summary>Mission sampling cadence in seconds.</summary>
    public float SampleIntervalSeconds { get; set; } = 2f;
    public float CohesionDistance { get; set; } = 25f;
    public float CommanderDistance { get; set; } = 40f;
    public float EngagementDistance { get; set; } = 35f;
}

/// <summary>One merit reward band. Provider enforces descending-unique MinScore ordering.</summary>
public sealed class MeritBand
{
    public int MinScore { get; set; }
    public int ServiceXp { get; set; }
    public int Gold { get; set; }
    public int Trust { get; set; }

    /// <summary>Renown on top of the flat per-battle base — this is what makes a good fight pay
    /// more than a bad one without turning battles into a renown farm.</summary>
    public int Renown { get; set; }

    public ReputationDomain RepDomain { get; set; } = ReputationDomain.Field;
    public int RepAmount { get; set; }

    /// <summary>Loc key suffix for the band's grade line (taom_enlist_merit_grade_{key}).</summary>
    public string GradeKey { get; set; } = "";
}

/// <summary>Trust + reputation clamps and duty-offer cadence (the army-rhythm scheduler's knobs).</summary>
public sealed class SchedulerConfig
{
    public int TrustMin { get; set; } = -10;
    public int TrustMax { get; set; } = 20;
    public int ReputationMax { get; set; } = 50;

    public int MinDaysBeforeFirstOffer { get; set; } = 3;
    public int OfferCooldownDaysQuiet { get; set; } = 4;
    public int OfferCooldownDaysPressure { get; set; } = 2;
    public int GuaranteedOfferDaysQuiet { get; set; } = 7;
    public int GuaranteedOfferDaysPressure { get; set; } = 4;
    public float BaseOfferChance { get; set; } = 0.06f;
    public float MaxOfferChance { get; set; } = 0.45f;

    /// <summary>Same duty type not re-offered within this many days unless under pressure.</summary>
    public int AntiRepeatDays { get; set; } = 5;

    public int IncidentMinDaysServed { get; set; } = 7;
    public int IncidentCooldownDays { get; set; } = 3;
}
