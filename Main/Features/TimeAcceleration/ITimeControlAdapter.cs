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
    void SetTimeSpeed(int mode);
}
