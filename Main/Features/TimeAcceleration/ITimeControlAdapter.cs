namespace TAOM.Features.TimeAcceleration;

public interface ITimeControlAdapter
{
    bool IsCampaignActive { get; }
    bool IsMenuOpen { get; }
    bool IsTimeControlLocked { get; }
    float SpeedUpMultiplier { get; set; }
    int TimeControlMode { get; set; }
    void SetTimeSpeed(int mode);
}
