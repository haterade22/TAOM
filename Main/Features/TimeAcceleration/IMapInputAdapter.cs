namespace TAOM.Features.TimeAcceleration;

public interface IMapInputAdapter
{
    bool IsMapActive { get; }

    // Named for the ACTION, not the key. These used to be IsSpacePressed / IsEKeyPressed, which
    // baked the binding into the seam itself and made it unchangeable by definition.
    bool IsFastForwardPressed { get; }
    bool IsExtraFastForwardPressed { get; }
    bool IsTurboPressed { get; }
    bool IsTurboReleased { get; }

    /// <summary>
    /// True when the fast-forward key is bound to something OTHER than vanilla's MapTimeTogglePause,
    /// so nothing else is going to change the time mode for us.
    ///
    /// Fast-forward historically set only the multiplier and let vanilla's Space handler own the mode
    /// transition. That was invisible while the two shared a key, but Campaign.TickMapTime applies
    /// SpeedUpMultiplier ONLY in the fast-forward modes, so on a rebound key the multiplier lands on
    /// a Play or Stop mode and the keypress does nothing at all.
    /// </summary>
    bool FastForwardOwnsTimeMode { get; }

    bool IsControlDown { get; }
}
