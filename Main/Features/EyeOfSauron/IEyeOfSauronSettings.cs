namespace TAOM.Features.EyeOfSauron;

/// <summary>The Eye of Sauron hunt's MCM knobs (#765 M8), validated.</summary>
public interface IEyeOfSauronSettings
{
    /// <summary>The campaign's pinned choice, else MCM, else on.</summary>
    bool Enabled { get; }

    /// <summary>What a new campaign gets when the player leaves the option alone: MCM, else on.</summary>
    bool EnabledNewCampaignDefault { get; }

    /// <summary>The decayed deed score that draws a hunt.</summary>
    float Threshold { get; }

    /// <summary>Days for a deed score to halve.</summary>
    float HalfLifeDays { get; }

    int CooldownDays { get; }

    int HuntTimeoutDays { get; }

    /// <summary>
    /// The hunter's speed floor within 1.5 of the player's seeing ranges, as a multiple of the
    /// player's speed.
    /// </summary>
    float HunterSpeedMultiplier { get; }

    /// <summary>The host's target strength, as a multiple of the player's.</summary>
    float HunterStrengthRatio { get; }

    int HunterMaxTroops { get; }

    /// <summary>The longest single out-of-sight step, in map units.</summary>
    float HunterStepCap { get; }
}
