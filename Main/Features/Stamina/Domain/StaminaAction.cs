namespace TAOM.Features.Stamina.Domain;

/// <summary>A soldier's own action that costs stamina and pauses his regeneration.</summary>
public enum StaminaAction
{
    None = 0,
    Swing,
    BowShot,
    CrossbowShot,
    Throw,
    Jump,
    Kick,
    Bash,
}

/// <summary>How a defender stopped a blow. It costs stamina and does not pause regeneration.</summary>
public enum StaminaBlock
{
    WeaponParry,
    ShieldMelee,
    ShieldMissile,
}

/// <summary>What the mission tick saw of one soldier since it last looked at him. Plain values: the rules need no engine type.</summary>
public readonly struct StaminaSample
{
    public StaminaSample(float now, float dt, bool running, bool blocking, float bowHeldSeconds)
    {
        Now = now;
        Dt = dt;
        Running = running;
        Blocking = blocking;
        BowHeldSeconds = bowHeldSeconds;
    }

    /// <summary>Mission time of this look.</summary>
    public float Now { get; }

    /// <summary>Seconds since the last look, already capped so a long frame gives no burst.</summary>
    public float Dt { get; }

    /// <summary>On foot, not using a game object, faster than the run threshold.</summary>
    public bool Running { get; }

    /// <summary>Holding a block.</summary>
    public bool Blocking { get; }

    /// <summary>Seconds of this interval a player held a drawn bow past the grace time.</summary>
    public float BowHeldSeconds { get; }
}
