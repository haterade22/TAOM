using System;
using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;

namespace TAOM.Features.FactionUI.Campaign
{
    // Samples the live campaign clock; the bound time supplies the standalone preview.
    public class CampaignTimeDialWidget : Widget
    {
        public double DayTime { get; set; }

        private BrushWidget? _night;
        private double _displayHour;
        private bool _initialized;
        private const double ResponseSeconds = 0.25;

        public CampaignTimeDialWidget(UIContext context) : base(context) { }

        public static float DaylightBlend(double hour, double sunrise, double sunset)
        {
            // Two-hour transitions centred on the campaign's actual sunrise and sunset.
            double dawn = SmoothStep((hour - sunrise + 1.0) / 2.0);
            double dusk = SmoothStep((sunset - hour + 1.0) / 2.0);
            return (float)(dawn * dusk);
        }

        private static double SmoothStep(double value)
        {
            value = Math.Max(0.0, Math.Min(1.0, value));
            return value * value * (3.0 - 2.0 * value);
        }

        public static double SmoothHour(double current, double target, double hoursInDay, float dt)
        {
            // Follow the shortest arc across midnight, with the same response at every frame rate.
            double difference = (target - current + hoursInDay * 1.5) % hoursInDay - hoursInDay * 0.5;
            double next = current + difference * (1.0 - Math.Exp(-dt / ResponseSeconds));
            return (next % hoursInDay + hoursInDay) % hoursInDay;
        }

        protected override void OnLateUpdate(float dt)
        {
            base.OnLateUpdate(dt);
            if (_night == null) _night = (BrushWidget)FindChild("NightTrees");

            double hoursInDay = TaleWorlds.CampaignSystem.CampaignTime.HoursInDay;
            double sunrise = TaleWorlds.CampaignSystem.CampaignTime.SunRise;
            double sunset = TaleWorlds.CampaignSystem.CampaignTime.SunSet;
            // MapTimeControlVM.Time refreshes at 5–10Hz; the live clock advances every frame.
            double targetHour = TaleWorlds.CampaignSystem.CampaignTime.Now.ToHours;
            targetHour = (targetHour % hoursInDay + hoursInDay) % hoursInDay;
            if (!_initialized)
            {
                _displayHour = targetHour;
                _initialized = true;
            }
            else
            {
                _displayHour = SmoothHour(_displayHour, targetHour, hoursInDay, dt);
            }
            double hour = _displayHour;
            // ImageWidget renders its Brush, so Widget.AlphaFactor does not fade this layer.
            if (_night != null) _night.Brush.AlphaFactor = 1f - DaylightBlend(hour, sunrise, sunset);

        }
    }
}
