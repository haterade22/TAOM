using TaleWorlds.CampaignSystem;

namespace TAOM.Features.TimeAcceleration;

public class TimeControlAdapter : ITimeControlAdapter
{
    public bool IsCampaignActive => Campaign.Current != null;

    public bool IsMenuOpen => Campaign.Current?.CurrentMenuContext != null;

    // The same chain vanilla MapTimeControlVM.ExecuteTimeControlChange reads (v1.4.8).
    public bool IsWaitMenuActive => Campaign.Current?.CurrentMenuContext?.GameMenu?.IsWaitActive ?? false;

    public bool IsTimeControlLocked => Campaign.Current?.TimeControlModeLock ?? false;

    public float SpeedUpMultiplier
    {
        get => Campaign.Current?.SpeedUpMultiplier ?? 1f;
        set { if (Campaign.Current != null) Campaign.Current.SpeedUpMultiplier = value; }
    }

    public int TimeControlMode
    {
        get => (int)(Campaign.Current?.TimeControlMode ?? CampaignTimeControlMode.Stop);
        set { if (Campaign.Current != null) Campaign.Current.TimeControlMode = (CampaignTimeControlMode)value; }
    }

    public void SetTimeSpeed(int mode)
    {
        Campaign.Current?.SetTimeSpeed(mode);
    }
}
