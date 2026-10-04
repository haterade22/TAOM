namespace TAOM.Features.TimeAcceleration;

public interface ITimeControlAdapter
{
    bool IsCampaignActive { get; }
    bool IsMenuOpen { get; }

    /// <summary>
    /// The open game menu is a WAIT menu (time flows inside it). Vanilla's MapBar time buttons
    /// work inside such a menu unless the time control is locked, and nowhere else while a menu
    /// is open; the button commands mirror that gate.
    /// </summary>
    bool IsWaitMenuActive { get; }

    bool IsTimeControlLocked { get; }
    float SpeedUpMultiplier { get; set; }
    int TimeControlMode { get; set; }

    /// <summary>
    /// The engine's own simplification of the mode (<c>Campaign.GetSimplifiedTimeControlMode</c>), read-only: a
    /// Stoppable mode reads Stop while the main party is waiting, because <c>TickMapTime</c> then gives the frame no
    /// campaign time; the party-wait fast-forward reads as the plain unstoppable one and FastForwardStop as Stop.
    /// Stop when no campaign is running.
    /// </summary>
    int SimplifiedTimeControlMode { get; }

    void SetTimeSpeed(int mode);
}
