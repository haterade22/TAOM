using System;
using System.Collections.Generic;
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
    public int DriftRun;
    public int MaxDrift;
    public bool DriftFlagged;
    public double MeleeNearUnitSeconds;
    public double? FellMt;
}

internal sealed class FordsVolleyEvidence
{
    public FordsVolleyItemInfo? Item;
    public bool ItemReported;
    public bool ReachShort;
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
