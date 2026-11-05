using System;
using System.Threading;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Features.MarshalsWord;

/// <summary>The archers' fire state of the player's companies by <c>Formation.Index</c>, sized for every formation class, with their shot counters.</summary>
internal static class MarshalFireTable
{
    private const int Size = (int)FormationClass.NumberOfAllFormations;

    private static readonly FireMode[] Modes = new FireMode[Size];
    private static readonly double[] WordTimes = NewWordTimes();
    private static readonly int[] ShotsWhileHolding = new int[Size];
    private static readonly int[] ShotsAfterWord = new int[Size];
    private static readonly int[] ShotsFree = new int[Size];
    private static readonly int[] ShotsBeforeFirstWord = new int[Size];
    private static readonly int[] ShotsAfterOther = new int[Size];
    private static readonly bool[] Held = new bool[Size];
    private static readonly int[] BlowsDealt = new int[Size];
    private static readonly int[] StrikeBlowsDealt = new int[Size];
    private static readonly ShotWatch<Agent> Watch = new();
    private static volatile bool _live;
    private static int _watched = -1;

    internal static int Companies => Size;

    internal static void Begin()
    {
        Reset();
        _live = true;
    }

    /// <summary>The safe-to-repeat clear of both teardown paths: no native call.</summary>
    internal static void Clear()
    {
        _live = false;
        Reset();
    }

    internal static FireMode ModeOf(int company) => InRange(company) ? Modes[company] : FireMode.Free;

    /// <summary>The time of the latest word for the company, or negative infinity when none was given since the mode last changed.</summary>
    internal static double WordTimeOf(int company) => InRange(company) ? WordTimes[company] : double.NegativeInfinity;

    internal static void SetMode(int company, FireMode mode)
    {
        if (!InRange(company))
            return;
        Modes[company] = mode;
        WordTimes[company] = double.NegativeInfinity;
    }

    /// <summary>Hold the line is on for the company (build-spec 9.2). Written on the main thread only, like the mode; the blow counter reads it as one plain bool from any thread.</summary>
    internal static bool HoldOf(int company) => InRange(company) && Held[company];

    internal static void SetHold(int company, bool on)
    {
        if (InRange(company))
            Held[company] = on;
    }

    /// <summary>Melee blows the company's soldiers have dealt this mission, counted by <see cref="OnBlow"/>; a soldier of a held company who stands outside the line deals none that count.</summary>
    internal static int BlowsOf(int company) => InRange(company) ? Volatile.Read(ref BlowsDealt[company]) : 0;

    /// <summary>Melee blows the company's wounded soldiers have dealt inside their strike back window, which the held line's flag is meant to allow (build-spec 9.2).</summary>
    internal static int StrikeBlowsOf(int company) => InRange(company) ? Volatile.Read(ref StrikeBlowsDealt[company]) : 0;

    /// <summary>
    /// Counts one blow dealt by a player-team soldier to a soldier of another team, melee only. Any thread: native chooses the hit callback's thread, so there is
    /// no lock, no allocation and no log line. Formation.Index repeats across teams, so the team is tested first.
    /// </summary>
    internal static void OnBlow(Agent? affector, Agent? affected, bool isMissile)
    {
        if (!_live || isMissile || affector == null || affected == null || affector.Team == affected.Team)
            return;
        var formation = affector.Formation;
        if (formation == null || affector.Team != Mission.Current?.PlayerTeam)
            return;
        var component = affector.GetComponent<MarshalWordComponent>();
        var inTheLine = component != null && component.HoldQualifies;
        if (component != null && MarshalHoldLineRules.BlowIsStrikeBack(inTheLine, component.WoundedAt, Mission.Current.CurrentTime))
            CountStrikeBlow(formation.Index);
        else if (MarshalHoldLineRules.BlowCountsForLine(inTheLine, HoldOf(formation.Index)))
            CountBlow(formation.Index);
    }

    internal static void CountStrikeBlow(int company)
    {
        if (InRange(company))
            Interlocked.Increment(ref StrikeBlowsDealt[company]);
    }

    internal static void CountBlow(int company)
    {
        if (InRange(company))
            Interlocked.Increment(ref BlowsDealt[company]);
    }

    internal static void GiveWord(int company, double now)
    {
        if (InRange(company))
            WordTimes[company] = now;
    }

    internal static FireCounters CountersOf(int company) => InRange(company)
        ? new FireCounters(Volatile.Read(ref ShotsWhileHolding[company]), Volatile.Read(ref ShotsAfterWord[company]),
            Volatile.Read(ref ShotsFree[company]), Volatile.Read(ref ShotsBeforeFirstWord[company]), Volatile.Read(ref ShotsAfterOther[company]))
        : default;

    /// <summary>Counts every shot of the company, whatever its mode, and the distinct soldiers who loosed them, until <see cref="EndWatch"/>. The field trial's control.</summary>
    internal static void BeginWatch(int company)
    {
        Volatile.Write(ref _watched, -1);
        Watch.Reset();
        Volatile.Write(ref _watched, company);
    }

    internal static void EndWatch() => Volatile.Write(ref _watched, -1);

    internal static (int Shots, int Shooters) Watched => (Watch.Shots, Watch.Shooters);

    /// <summary>
    /// Counts one shot of a player-team soldier whose company has a mode (build-spec 7.3). Any thread: no lock, no allocation, no log line.
    /// Formation.Index repeats across teams, so the team is tested first.
    /// </summary>
    internal static void OnShot(Agent shooter)
    {
        if (!_live)
            return;
        var formation = shooter.Formation;
        if (formation == null || shooter.Team != Mission.Current?.PlayerTeam)
            return;
        var company = formation.Index;
        if (!InRange(company))
            return;
        if (company == Volatile.Read(ref _watched))
            Watch.Note(shooter);
        if (Modes[company] == FireMode.Free)
            return;

        var component = shooter.GetComponent<MarshalWordComponent>();
        Count(company, component?.Classify(Mission.Current!.CurrentTime) ?? ShotKind.Free);
    }

    /// <summary>Adds one shot of a company to the counter its kind names, and to the shots before the first word while none was given.</summary>
    internal static void Count(int company, ShotKind kind)
    {
        if (!InRange(company))
            return;
        if (double.IsNegativeInfinity(WordTimes[company]))
            Interlocked.Increment(ref ShotsBeforeFirstWord[company]);
        switch (kind)
        {
            case ShotKind.WhileHolding:
                Interlocked.Increment(ref ShotsWhileHolding[company]);
                break;
            case ShotKind.AfterWord:
                Interlocked.Increment(ref ShotsAfterWord[company]);
                break;
            case ShotKind.AfterOther:
                Interlocked.Increment(ref ShotsAfterOther[company]);
                break;
            default:
                Interlocked.Increment(ref ShotsFree[company]);
                break;
        }
    }

    private static bool InRange(int company) => company >= 0 && company < Size;

    private static void Reset()
    {
        Array.Clear(Modes, 0, Size);
        Array.Clear(Held, 0, Size);
        Array.Clear(BlowsDealt, 0, Size);
        Array.Clear(StrikeBlowsDealt, 0, Size);
        Array.Clear(ShotsWhileHolding, 0, Size);
        Array.Clear(ShotsAfterWord, 0, Size);
        Array.Clear(ShotsFree, 0, Size);
        Array.Clear(ShotsBeforeFirstWord, 0, Size);
        Array.Clear(ShotsAfterOther, 0, Size);
        Volatile.Write(ref _watched, -1);
        for (var i = 0; i < Size; i++)
            WordTimes[i] = double.NegativeInfinity;
    }

    private static double[] NewWordTimes()
    {
        var times = new double[Size];
        for (var i = 0; i < Size; i++)
            times[i] = double.NegativeInfinity;
        return times;
    }
}

/// <summary>The cumulative shot counters of one company: while a soldier held, in the two seconds after a release the word caused, in the two seconds after any other release, any other shot, and the shots before the first word.</summary>
internal readonly record struct FireCounters(int WhileHolding, int AfterWord, int Free, int BeforeFirstWord, int AfterOther = 0);

internal enum ShotKind
{
    Free,
    WhileHolding,
    AfterWord,
    AfterOther,
}
