using TaleWorlds.CampaignSystem;

namespace TAOM.Features.TimeAcceleration;

public class TimeControlAdapter : ITimeControlAdapter
{
    public bool IsCampaignActive => Campaign.Current != null;

    public bool IsMenuOpen => Campaign.Current?.CurrentMenuContext != null;

    public bool IsTimeControlLocked => Campaign.Current?.TimeControlModeLock ?? false;

    public float SpeedUpMultiplier
    {
        get => Campaign.Current?.SpeedUpMultiplier ?? 1f;
        set { if (Campaign.Current != null) Campaign.Current.SpeedUpMultiplier = value; }
    }

    public CampaignTimeControlMode TimeControlMode
    {
        get => Campaign.Current?.TimeControlMode ?? CampaignTimeControlMode.Stop;
        set { if (Campaign.Current != null) Campaign.Current.TimeControlMode = value; }
    }

    public void SetTimeSpeed(int mode)
    {
        Campaign.Current?.SetTimeSpeed(mode);
    }
}
