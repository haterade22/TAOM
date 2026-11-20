using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.Stamina.Domain;

namespace TAOM.Tests.Features.SeatOfSeeing;

// The stamina battle of the stand-in (#1506): a hundred soldiers with a cell, groups the steps choose and force, and the releases, defends,
// speeds and locks each group shows. A cumulative count is the floor of what accrued: rate x live x seconds, summed frame by frame.
internal sealed partial class CommandTrialWorld
{
    internal bool StaminaArmed = true;
    internal string GateText = "open (MCM=True, fords=False, combatType=Combat, teamAI=Battle)";
    internal float TrackedShare = 1f;
    internal bool HeroCell = true;
    internal bool RefreshesStayQueued;
    internal bool LockApplies = true;
    internal float RunnerShare = 0.8f;
    internal float ExhaustedRunnerShare;
    internal float ExhaustedSpeed = 1.5f;
    /// <summary>When set, the speed of the walk's control half while the other half is exhausted (the whole formation slowed).</summary>
    internal float? ControlSpeedBesideTheExhausted;
    /// <summary>Releases each second of the control group in the walk; 0 is no contact.</summary>
    internal float ControlReleasesDuringWalk;
    internal float ContactAfterSeconds = 6f;
    /// <summary>When set, every group stops attacking this many seconds after the groups were chosen (the lines broke).</summary>
    internal float? ContactEndsAfterSeconds;
    /// <summary>Releases for each soldier second of the control group.</summary>
    internal float ControlRate = 0.1f;
    internal float TiredFactor = 0.7f;
    internal float ExhaustedRate;
    internal float ExhaustedDefendRate;
    internal bool TiredLocks;
    /// <summary>Releases for each soldier second of the exhausted group once it is given back.</summary>
    internal float ReleasesAfterAuto = 0.1f;
    internal int GroupSize = 30;

    internal int StaminaReadsOutsideTheMissionTick;
    internal int ReleasedForced;
    internal readonly List<string> StaminaForced = new();
    internal readonly List<string> StaminaChoices = new();

    private const float BaseSpeed = 5f;
    private const float LockDelaySeconds = 0.5f;

    private sealed class StaminaTestGroup
    {
        internal StaminaBand? Forced;
        internal float ForcedAt = float.NaN;
        internal bool Given;
        internal double Releases;
        internal double Defends;
    }

    private StaminaTestGroup[]? _staminaGroups;
    private bool _staminaEnemies;
    private float _staminaChosenAt;
    private float _lastForceAt = float.NegativeInfinity;

    /// <summary>How many groups hold a forced band now.</summary>
    internal int ForcedNow => _staminaGroups?.Count(g => g.Forced != null) ?? 0;

    private bool Charged => ChargeOrders > 0 && !float.IsNaN(_crewChargedAt) && Time - _crewChargedAt >= 1f;

    private bool Contact => Time - _staminaChosenAt >= ContactAfterSeconds && !(ContactEndsAfterSeconds.HasValue && Time - _staminaChosenAt >= ContactEndsAfterSeconds.Value);

    private void NoteStaminaRead()
    {
        if (!InMissionTick)
            StaminaReadsOutsideTheMissionTick++;
    }

    private bool Applied(StaminaTestGroup group, StaminaBand band, bool works) =>
        group.Forced == band && works && Time - group.ForcedAt >= LockDelaySeconds;

    public StaminaCensus ReadStamina()
    {
        NoteStaminaRead();
        var waiting = ForcedNow > 0 && (RefreshesStayQueued || Time - _lastForceAt < 0.7f) ? 5 : 0;
        var strips = (long)(Time * 10f);
        return new StaminaCensus(StaminaArmed, GateText, (int)(100 * TrackedShare), 100, HeroCell, waiting, strips, 3f);
    }

    public int ChooseStaminaGroups(bool enemies)
    {
        NoteStaminaRead();
        _staminaEnemies = enemies;
        _staminaChosenAt = Time;
        _staminaGroups = Enumerable.Range(0, enemies ? 3 : 2).Select(_ => new StaminaTestGroup()).ToArray();
        StaminaChoices.Add(enemies ? "enemies" : "allies");
        return GroupSize;
    }

    public StaminaGroupSample SampleStaminaGroup(int group)
    {
        NoteStaminaRead();
        var g = _staminaGroups![group];
        var exhausted = Applied(g, StaminaBand.Exhausted, LockApplies);
        var tiredLocked = Applied(g, StaminaBand.Tired, TiredLocks);
        var locked = exhausted || tiredLocked ? GroupSize : 0;
        var share = exhausted ? ExhaustedRunnerShare : Charged ? RunnerShare : 0f;
        var slowedWithThem = !_staminaEnemies && ControlSpeedBesideTheExhausted.HasValue && Applied(_staminaGroups![0], StaminaBand.Exhausted, LockApplies);
        var speed = exhausted ? ExhaustedSpeed : slowedWithThem ? ControlSpeedBesideTheExhausted!.Value : Charged ? BaseSpeed : 0f;
        var chance = exhausted ? 0.05f : g.Forced == StaminaBand.Tired ? 0.35f : 0.5f;
        return new StaminaGroupSample(GroupSize, (int)Math.Round(share * GroupSize), speed * GroupSize, locked,
            (long)g.Releases, (long)g.Defends, chance * GroupSize, 0.2f * GroupSize);
    }

    public int ForceStaminaGroup(int group, StaminaBand band)
    {
        NoteStaminaRead();
        Note("force-stamina");
        var g = _staminaGroups![group];
        g.Forced = band;
        g.ForcedAt = Time;
        _lastForceAt = Time;
        StaminaForced.Add(group + ":" + band);
        return GroupSize;
    }

    public int StaminaReleaseForced()
    {
        ReleasedForced++;
        var released = ForcedNow * GroupSize;
        if (_staminaGroups != null)
        {
            foreach (var g in _staminaGroups)
            {
                if (g.Forced != null)
                    g.Given = true;
                g.Forced = null;
            }
        }

        return released;
    }

    public int CountStaminaSoldiers(bool enemies)
    {
        NoteStaminaRead();
        return enemies ? 280 : 120;
    }

    private double RateOf(int index, StaminaTestGroup group)
    {
        if (!_staminaEnemies)
            return 0d;
        if (!Contact)
            return 0d;
        if (group.Forced == StaminaBand.Exhausted)
            return Applied(group, StaminaBand.Exhausted, LockApplies) ? ExhaustedRate : ControlRate;
        if (group.Forced == StaminaBand.Tired)
            return ControlRate * TiredFactor;
        return index == 0 && group.Given ? ReleasesAfterAuto : ControlRate;
    }

    // Once per mission tick, before the script's: the counts of every chosen group grow by what the frame brings.
    private void StaminaFrame(float dt)
    {
        if (_staminaGroups == null)
            return;
        for (var index = 0; index < _staminaGroups.Length; index++)
        {
            var group = _staminaGroups[index];
            if (_staminaEnemies)
            {
                group.Releases += RateOf(index, group) * GroupSize * dt;
                var defends = group.Forced == StaminaBand.Exhausted && Applied(group, StaminaBand.Exhausted, LockApplies) ? ExhaustedDefendRate : 0f;
                group.Defends += defends * GroupSize * dt;
            }
            else if (index == 1)
            {
                group.Releases += ControlReleasesDuringWalk * dt;
            }
        }
    }
}
