using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>
/// Everything one run recorded: replaced whole at each BeginRun, so every cap and counter starts fresh. Only BCL,
/// diagnostics and domain types, never an engine object. <see cref="Lanes"/> is laid out by <c>LaneStreams.Index</c>.
/// </summary>
internal sealed class FordsRunEvidence
{
    public int Run;
    public string Trigger = "";
    public string Setup = "";
    public string Started = "";
    public double Mt;
    public FordsMatchFacts? Facts;
    public List<FordsStageMark> Stages = new();
    public FordsFailure? Failure;
    public FordsStartEvidence Start = new();
    public FordsSpawnEvidence Spawn = new();
    public List<FordsLaneEvidence> Lanes = new();             // created at MatchStarted, one per side x lane
    public FordsKillEvidence Kills = new();
    public Dictionary<string, FordsTowerEvidence> Towers = new();   // created at MatchStarted, before Patch131 is installed
    public FordsVolleyEvidence Volley = new();
    public List<FordsHeroMark> Heroes = new();
    public Dictionary<string, FordsPointEvidence> Points = new();
    public FordsEndEvidence End = new();
    public FordsPerfEvidence Perf = new();
    public FordsOptionsEvidence Options = new();
    public FordsClientEvidence Client = new();
    public FordsWargEvidence Warg = new();
    public FordsFaceEvidence Faces = new();
    public FordsExceptionEvidence Exceptions = new();
    public int[] Logged = new int[(int)FordsLogCategory.Count];
    public bool Live;
    public bool Closed;
    public int FinalPasses;
}

internal sealed class FordsStageMark
{
    public string Name = "";
    public double Wall;
    public double Mt;
    public string Detail = "";
}

internal sealed class FordsFailure
{
    public string Route = "";
    public string Reason = "";
    public double Wall;
    public double Mt;
}

internal sealed class FordsStartEvidence
{
    public int TowersExpected;
    public int TowersBuilt;
    public int KeepsBuilt;
    public int IdsChecked;
    public List<string> MissingIds = new();
    public FordsLayoutEvidence Layout = new();

    /// <summary>The tower and keep numbers the match plays with, as the rules read them (see <c>FordsRunReport.TowerConfig</c>).</summary>
    public string TowerConfig = "";
}

/// <summary>The layout the director reported at match start (see <see cref="FordsLayoutFacts"/>). Reported stays false until it did.</summary>
internal sealed class FordsLayoutEvidence
{
    public bool Reported;
    public string Scene = "";
    public List<int> LaneWaypoints = new();
    public List<string> BuiltTowerIds = new();
    public List<string> MissingTowerIds = new();
    public bool KeepOpenWest;
    public bool KeepOpenEast;
}

internal sealed class FordsSpawnEvidence
{
    public int Spawned;
    public int Failed;
    public int PeakBacklog;
    public Dictionary<string, int> SpawnedByTroop = new();
    public Dictionary<string, int> FailedByTroop = new();
}

internal sealed class FordsLaneEvidence
{
    public string Side = "";
    public int Lane;
    public double? FirstFront;
    public double? FirstSpawnMt;
    public double MaxGain;
    public double LiveSeconds;
    public string LastCommand = "None";
    public int HoldSamples;
    public int InFireSamples;
    public int InFireRun;
    public int MaxInFireRun;
    public double HoldFrontSum;
    public double MinHoldFront = double.MaxValue;
    /// <summary>The tower that covered the lane during its longest in-fire run (the run behind <see cref="MaxInFireRun"/>).</summary>
    public string? FireTower;
    public string? RunTower;

    /// <summary>Every lane command of the stream by <c>LaneCommandKind</c> name, uncapped (the log keeps only the first 30 lines).</summary>
    public Dictionary<string, int> CommandsGiven = new();
    public Dictionary<string, int> CommandsRefused = new();
    public Dictionary<string, double> FirstGivenMt = new();

    /// <summary>Commands the adapter refused because the target was off the navmesh (one per command, not per retry), with the first target refused and when the last one was.</summary>
    public int OffNavmesh;
    public double? FirstOffNavmeshX;
    public double? FirstOffNavmeshY;
    public double? LastOffNavmeshMt;

    /// <summary>Live unit-seconds and the alarmed share of them (units spawned alarmed should stay so).</summary>
    public double UnitSeconds;
    public double AlarmedUnitSeconds;

    /// <summary>
    /// The ladder from an AttackTower order to a hit, per stream: while the stream's applied command was AttackTower at a standing
    /// tower, its live unit-seconds, the ones whose combat flags carried AttackEntity, and the melee ones within 4 m of that
    /// tower; then the blows its units landed on that tower (of which Applied took hit points) and on that tower's gunners. A blow
    /// on any other tower, or while the stream has no AttackTower order, is not on the ladder.
    /// </summary>
    public string? AttackTarget;

    /// <summary>The tower the stream's applied command names as of the last 1 Hz look; null when the look saw no AttackTower order. Gates the blow counts.</summary>
    [JsonIgnore] public string? CurrentAttackTarget;
    public double AttackUnitSeconds;
    public double AttackFlaggedUnitSeconds;
    public double AttackContactUnitSeconds;
    public long TowerBlows;
    public long AppliedBlows;
    public long GunnerBlows;

    /// <summary>
    /// The march judgement, from the 1 Hz looks at the stream's own units: the looks at which its applied command was a MoveTo with a
    /// next standing enemy tower, and those of them at which more than half its live units stood more than 10 m past that tower
    /// (the tower should have been attacked). <see cref="MaxPastRun"/> is the longest run of such looks in a row; two fail the part.
    /// The worst look's share of units and its tower, and the mission time the second look in a row was reached, are kept.
    /// </summary>
    public int MarchSamples;
    public int PastSamples;
    public int MaxPastRun;
    public double MaxPastShare;
    public string? PastTower;
    public double? FirstPastMt;

    /// <summary>The run of such looks in a row so far; not part of the report.</summary>
    [JsonIgnore] public int PastRun;

    /// <summary>When the last <c>[LaneState]</c> line was written; not part of the report.</summary>
    [JsonIgnore] public double LastStateMt = double.NegativeInfinity;
}

/// <summary>Credit keys are "&lt;side&gt;:&lt;KillCredit&gt;" where the side is the opponent of the one that lost the unit, so "West:Paid" is West earning gold.</summary>
internal sealed class FordsKillEvidence
{
    public int DeathsWest;
    public int DeathsEast;
    public Dictionary<string, int> Credit = new();
}

internal sealed class FordsTowerEvidence
{
    internal static readonly int BlowClasses = Enum.GetNames(typeof(TowerBlowClass)).Length;
    internal static readonly int BlowReasons = Enum.GetNames(typeof(TowerDamageReason)).Length;

    public string Owner = "";
    public int Lane;
    public string Kind = "";
    public long[,] Blows = new long[BlowClasses, BlowReasons];
    public long SideUnknownApplied;

    /// <summary>Blows on this tower so far, which caps its trace lines; not part of the report.</summary>
    [JsonIgnore] public int BlowsSampled;

    /// <summary>Melee probe blows on this tower so far that did no damage and that did some, which cap its probe trace lines; not part of the report.</summary>
    [JsonIgnore] public int ProbeZeroSampled;
    [JsonIgnore] public int ProbeHitSampled;
    public int DriftRun;
    public int MaxDrift;
    public bool DriftFlagged;
    public double MeleeNearUnitSeconds;

    /// <summary>Melee unit-seconds within contact range of this tower, over the looks at which a stream was ordered at it; the divisor of the damage per contact second.</summary>
    public double ContactUnitSeconds;

    /// <summary>Blows by an enemy whose victim was one of this tower's gunners (they take no damage, so they never reach <see cref="Blows"/>).</summary>
    public long GunnerBlowsMelee;
    public long GunnerBlowsRanged;
    public double? FellMt;

    /// <summary>The lowest hit points the rules held for the tower at a 1 Hz look; null before the first look.</summary>
    public int? MinRulesHp;

    /// <summary>Damage the rules applied, by attacker class (indexed by <see cref="TowerBlowClass"/>), and what regeneration gave back.</summary>
    public long[] AppliedDamage = new long[BlowClasses];
    public long HealedTotal;

    /// <summary>Blows on the tower and on its gunners by the side that struck them (indexed by <c>MatchSide</c>).</summary>
    public long[] BlowsBySide = new long[LaneStreams.Sides];
    public long[] GunnerBlowsBySide = new long[LaneStreams.Sides];

    /// <summary>
    /// The raw damage of every melee blow before the rules decided (so a zero can be told from a small number), as raw value to
    /// count. A blow with no weapon and one from the tower's own side are left out.
    /// Values past <see cref="MaxRawValues"/> distinct ones are counted in <see cref="MeleeRawOverflow"/> and left out of the summary.
    /// </summary>
    public SortedDictionary<int, int> MeleeRaw = new();
    public int MeleeRawOverflow;

    /// <summary>
    /// Blows the thrust damage floor turned into damage (the engine's raw damage was 0 and the rules applied some), and the damage
    /// they applied after the other factors. Also counted in <see cref="Blows"/> and <see cref="AppliedDamage"/>; the engine's raw
    /// damage stays 0 in <see cref="MeleeRaw"/> and <see cref="MeleeProbe"/>.
    /// </summary>
    public long FlooredBlows;
    public long FlooredDamage;

    /// <summary>Melee blows that struck the tower's own body, by <c>FordsRunReport.MeleeProbeKey</c> (an attacker on the tower owner's side is left out, one whose side is unknown is counted): why a raw damage is what it is.</summary>
    public SortedDictionary<string, int> MeleeProbe = new();

    internal const int MaxRawValues = 256;

    /// <summary>Count, smallest, median and largest raw damage of the melee blows recorded; all zero when there were none.</summary>
    internal FordsRawSummary MeleeRawSummary()
    {
        var count = 0;
        foreach (var n in MeleeRaw.Values)
            count += n;
        if (count == 0)
            return default;

        int? lower = null;
        int? upper = null;
        var seen = 0;
        var lowRank = (count - 1) / 2;
        var highRank = count / 2;
        foreach (var pair in MeleeRaw)
        {
            seen += pair.Value;
            if (lower == null && seen > lowRank)
                lower = pair.Key;
            if (upper == null && seen > highRank)
                upper = pair.Key;
        }

        return new FordsRawSummary(count, MeleeRaw.Keys.First(), (lower!.Value + upper!.Value) / 2.0, MeleeRaw.Keys.Last());
    }
}

/// <summary>A melee blow's raw damage as <see cref="FordsTowerEvidence.MeleeRawSummary"/> reads it.</summary>
internal readonly record struct FordsRawSummary(int Count, int Min, double Median, int Max);

internal sealed class FordsVolleyEvidence
{
    public FordsVolleyItemInfo? Item;
    public bool ItemReported;
    public bool ReachShort;
    public FordsVolleyItemInfo? Arrow;
    public bool ArrowReported;
    public bool ArrowReachShort;
    public int Launched;
    public int NoGunnerOrItem;
    public int NoTarget;
    public long HitsSeen;
    public Dictionary<string, int> Filtered = new();
    public List<FordsVolleyHit> Hits = new();
}

internal sealed class FordsHeroMark
{
    public string Kind = "";
    public string HeroId = "";
    public string Side = "";
    public double Mt;
    public int RequestLane = -1;
}

internal sealed class FordsPointEvidence
{
    public int Captures;
    public double ContestedSeconds;
    public double NearestApproach = double.MaxValue;
}

internal sealed class FordsEndEvidence
{
    public double? KeepFellMt;
    public string? Winner;
    public double? WinnerMt;
    public double? WinnerWall;
    public bool? ResultSuccess;
    public double? ResultWall;
    public string? ExitReason;
    public double? ExitDueMt;
    public double? ExitDueWall;
    public string? EndGameVia;
    public double? EndGameWall;
    public double? ClosedWall;
    public double? MenuReturnedWall;
    public bool NoticeRequested;
    public bool? NoticeOk;
    public double? NoticeAckWall;
    public bool AckAfterMenu;
    public bool NoticeUnacknowledged;
}

internal sealed class FordsPerfEvidence
{
    public List<FordsPerfWindow> Windows = new();
    public double LiveSeconds;
    public int PeakLiveWest;
    public int PeakLiveEast;
    public double PeakLiveMt;
    public int PeakAgentsAll;
}

internal sealed class FordsPerfWindow
{
    public double Mt;
    public int Frames;
    public double AvgMs;
    public double P95Ms;
    public double MaxMs;
    public int AgentsAll;
    public int AgentsActive;
    public int LiveWest;
    public int LiveEast;
}

internal sealed class FordsOptionsEvidence
{
    public bool? Component;
    public int Requested;
}

internal sealed class FordsClientEvidence
{
    public long PanInputFrames;
    public long PanMovedFrames;
    public long RotateInputFrames;
    public long TurnedFrames;
    public long ZoomInputFrames;
    public long ZoomedFrames;
    public long OwnedFrames;
    public bool CameraFailed;
    public int InsideSamples;
    public int OutsideSamples;
    public int OutsideRun;
    public int MaxOutsideRun;
    public bool PanelAttached;
    public Dictionary<string, int> Commands = new();
    public int PackBoughtLines;
    public int PauseEdges;
    public int PhotoVetoes;
    [JsonIgnore] public bool LastPaused;
}

/// <summary>Exceptions TAOM's CrashReport finalizers swallowed since the mission started (other Harmony finalizers are not heard). <see cref="Watching"/> says the hook was in place, so zero means none.</summary>
internal sealed class FordsExceptionEvidence
{
    public bool Watching;
    public int Count;
    public string? FirstType;
    public string? FirstSite;
    public double? FirstMt;
}

internal sealed class FordsWargEvidence
{
    public int Started;
    public int Restored;
    public int NullFormation;
    public int RiderInactive;
    public int Stranded;
    public int Open;
    public int Returned;
    public int NotReturned;
    public int Superseded;
    public float MaxReturnSeconds;
}
