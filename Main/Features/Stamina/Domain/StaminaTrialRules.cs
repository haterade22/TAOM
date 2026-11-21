using System;
using System.Globalization;

namespace TAOM.Features.Stamina.Domain;

/// <summary>One read of the stamina layer in the battle: the gate, how many soldiers hold a cell, the hero's cell, the refresh queue, the strips and the run threshold.</summary>
internal readonly record struct StaminaCensus(bool Armed, string Gate, int Tracked, int Soldiers, bool HeroCell,
    int RefreshesWaiting, long AiStrips, float RunSpeed);

/// <summary>One read of a group of soldiers. <c>Releases</c> and <c>Defends</c> are cumulative since the groups were chosen.</summary>
internal readonly record struct StaminaGroupSample(int Live, int Runners, float SpeedSum, int Locked,
    long Releases, long Defends, float ChanceSum, float DefensivenessSum);

/// <summary>
/// The reads of one group over a span of time: counts since the window's first sample, soldier seconds, and the means of the
/// frames added. A mean over no frame is not a number, so a pass term on it fails.
/// </summary>
internal sealed class StaminaWindow
{
    private readonly StaminaGroupSample _start;
    private float _last;
    private StaminaGroupSample _now;
    private long _liveSum;
    private float _shareSum;
    private int _shareFrames;
    private float _speedSum;
    private float _chanceSum;
    private float _defensivenessSum;

    internal StaminaWindow(StaminaGroupSample start, float at)
    {
        _start = start;
        _now = start;
        _last = at;
    }

    internal void Add(StaminaGroupSample now, float at)
    {
        SoldierSeconds += now.Live * Math.Max(0f, at - _last);
        _last = at;
        _now = now;
        _liveSum += now.Live;
        _speedSum += now.SpeedSum;
        _chanceSum += now.ChanceSum;
        _defensivenessSum += now.DefensivenessSum;
        if (now.Live > 0)
        {
            _shareSum += (float)now.Runners / now.Live;
            _shareFrames++;
        }

        if (now.Locked > 0)
            LockedFrames++;
    }

    internal long Releases => _now.Releases - _start.Releases;

    internal long Defends => _now.Defends - _start.Defends;

    internal float SoldierSeconds { get; private set; }

    /// <summary>Releases for each soldier second; not a number at 0 soldier seconds.</summary>
    internal float Rate => SoldierSeconds > 0f ? Releases / SoldierSeconds : float.NaN;

    /// <summary>The mean over the frames with a soldier of the share that ran.</summary>
    internal float RunnerShare => _shareFrames > 0 ? _shareSum / _shareFrames : float.NaN;

    internal float MeanSpeed => _liveSum > 0 ? _speedSum / _liveSum : float.NaN;

    /// <summary>Frames in which at least one soldier of the group was locked.</summary>
    internal int LockedFrames { get; private set; }

    internal float MeanChance => _liveSum > 0 ? _chanceSum / _liveSum : float.NaN;

    internal float MeanDefensiveness => _liveSum > 0 ? _defensivenessSum / _liveSum : float.NaN;
}

/// <summary>
/// The times, the counting rules and the pass rules of the stamina steps of the command trial (#1506,
/// docs/features/stamina.md). Every predicate states a positive requirement, so a value that is no number fails it.
/// </summary>
internal static class StaminaTrialRules
{
    /// <summary>The runner turns <c>hud_*.bmp</c> into a PNG (tools/run_fords_autorun.py), so the name starts with it.</summary>
    internal const string GatePicture = "hud_stamina_gate.bmp";

    internal const float BattleWaitSeconds = 20f;
    internal const float GatePictureSeconds = 3f;
    internal const float GateWaitSeconds = 15f;
    internal const float GateTrackedShare = 0.95f;

    /// <summary>The smallest group a step judges: fewer soldiers say nothing about a group.</summary>
    internal const int MinGroup = 8;

    internal const float RunnersWaitSeconds = 10f;
    internal const float BaseWindowSeconds = 2f;
    internal const float ApplyLimitSeconds = 3f;
    internal const float WalkWindowSeconds = 4f;
    internal const float BaseRunnerShare = 0.3f;
    internal const float ExhaustedRunnerShareMax = 0.1f;
    internal const float MovingSpeedMin = 0.5f;
    internal const float SlowerThanControl = 0.9f;
    internal const int WalkContactReleases = 3;

    internal const float ContactBlockSeconds = 3f;
    internal const float ContactWaitSeconds = 90f;
    internal const int ContactReleases = 5;
    internal const float SettleSeconds = 1.5f;
    internal const float FightMinSeconds = 10f;
    internal const float FightMaxSeconds = 30f;
    internal const int ControlEnoughReleases = 80;
    internal const int ControlMinReleases = 10;
    internal const float RestoreLimitSeconds = 10f;
    internal const int RestoreEnoughReleases = 3;

    /// <summary>A change into a release of a bow, a throw or a melee swing (<c>StaminaService</c> holds the engine numbers).</summary>
    internal static bool IsRelease(int before, int now) =>
        now != before && (now == StaminaService.ReleaseRanged || now == StaminaService.ReleaseThrowing || now == StaminaService.ReleaseMelee);

    /// <summary>A change into a defend from outside the defend range.</summary>
    internal static bool IsDefendEntry(int before, int now) => InDefend(now) && !InDefend(before);

    private static bool InDefend(int type) => type >= StaminaService.DefendFirst && type <= StaminaService.DefendLast;

    internal static bool GatePasses(StaminaCensus c) =>
        c.Armed && c.Gate != null && c.Gate.StartsWith("open", StringComparison.Ordinal) && c.HeroCell && c.Soldiers > 0
        && c.Tracked >= GateTrackedShare * c.Soldiers;

    internal static bool AllLocked(StaminaGroupSample s) => s.Live >= 1 && s.Locked == s.Live;

    internal static bool WalkContact(long controlReleases) => controlReleases >= WalkContactReleases;

    /// <summary>The Exhausted half must also be slower than the control half: a formation that slowed as a whole, for a reason that is not stamina, shows both halves at one speed.</summary>
    internal static bool WalkPasses(float baseShare, float exhaustedShare, float exhaustedSpeed, float controlSpeed) =>
        baseShare >= BaseRunnerShare && exhaustedShare <= ExhaustedRunnerShareMax && exhaustedSpeed >= MovingSpeedMin
        && exhaustedSpeed <= SlowerThanControl * controlSpeed;

    internal static bool FightPasses(long eReleases, long eDefends, long tReleases, int tLockedFrames, long cReleases, long restored) =>
        eReleases == 0 && eDefends == 0 && tReleases >= 1 && tLockedFrames == 0 && cReleases >= ControlMinReleases && restored >= 1;

    /// <summary><paramref name="tired"/> over <paramref name="control"/>; not a number when either is not, or the control is 0 or less.</summary>
    internal static float Ratio(float tired, float control) => control > 0f ? tired / control : float.NaN;

    internal static string GateFacts(StaminaCensus c, int pictures)
    {
        var word = (c.Gate ?? "").Split(' ')[0];
        var share = c.Soldiers > 0 ? (float)c.Tracked / c.Soldiers : float.NaN;
        return "armed=" + Flag(c.Armed) + " gate=" + (word.Length > 0 ? word : "none") + " tracked=" + Whole(c.Tracked)
            + " soldiers=" + Whole(c.Soldiers) + " share=" + Two(share) + " hero_cell=" + Flag(c.HeroCell) + " pictures=" + Whole(pictures);
    }

    internal static string WalkFacts(int forced, int control, float run, StaminaWindow baseForced, StaminaWindow baseControl,
        StaminaWindow forcedWindow, StaminaWindow controlWindow) =>
        "forced=" + Whole(forced) + " control=" + Whole(control) + " run=" + Two(run)
        + " base_share=" + Two(baseForced.RunnerShare) + " share=" + Two(forcedWindow.RunnerShare)
        + " speed=" + Two(forcedWindow.MeanSpeed) + " base_speed=" + Two(baseForced.MeanSpeed)
        + " control_speed=" + Two(controlWindow.MeanSpeed) + " control_base_speed=" + Two(baseControl.MeanSpeed)
        + " ratio=" + Two(Ratio(forcedWindow.MeanSpeed, baseForced.MeanSpeed))
        + " control_ratio=" + Two(Ratio(controlWindow.MeanSpeed, baseControl.MeanSpeed))
        + " control_releases=" + Whole(controlWindow.Releases);

    internal static string FightFacts(int exhausted, int tired, int control, StaminaWindow e, StaminaWindow t, StaminaWindow c,
        long strips, long restored, float contactAt) =>
        "exhausted=" + Whole(exhausted) + " tired=" + Whole(tired) + " control=" + Whole(control)
        + " e_releases=" + Whole(e.Releases) + " e_defends=" + Whole(e.Defends) + " t_releases=" + Whole(t.Releases)
        + " c_releases=" + Whole(c.Releases) + " t_rate=" + Two(t.Rate) + " c_rate=" + Two(c.Rate)
        + " ratio=" + Two(Ratio(t.Rate, c.Rate)) + " t_seconds=" + Two(t.SoldierSeconds) + " c_seconds=" + Two(c.SoldierSeconds)
        + " t_chance=" + Two(t.MeanChance) + " c_chance=" + Two(c.MeanChance) + " c_defensiveness=" + Two(c.MeanDefensiveness)
        + " strips=+" + Whole(strips) + " restored=" + Whole(restored) + " contact_at=" + Two(contactAt);

    private static string Flag(bool value) => value ? "true" : "false";

    private static string Whole(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Two(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
