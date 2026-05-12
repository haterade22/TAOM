using System;

namespace TAOM.Features.EditorCacheRebuild.Validation;

public class ValidationReport
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Mode { get; set; } = "full";
    public double DurationSeconds { get; set; }
    public bool Cancelled { get; set; }

    public int SettlementsTotal { get; set; }
    public int FortificationsTotal { get; set; }
    public string NavigationType { get; set; } = "";

    public PhaseReport Phase1 { get; set; } = new();
    public PhaseReport Phase2 { get; set; } = new();
    public SmokeTestReportData SmokeTest { get; set; } = new();
}

public class PhaseReport
{
    public double DurationSeconds { get; set; }
    public int PairsComputed { get; set; }
    public int NeighborPairsAdded { get; set; }
    public int FortificationsConsidered { get; set; }
}

public class SmokeTestReportData
{
    public string Outcome { get; set; } = "";
    public int PairsTested { get; set; }
    public float MaxDistanceDelta { get; set; }
    public string? Reason { get; set; }
}
