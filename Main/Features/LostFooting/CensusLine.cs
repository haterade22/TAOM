using System.Globalization;
using System.Text;
using TAOM.Core.Validation;
using TAOM.Features.LostFooting.Domain;

namespace TAOM.Features.LostFooting;

/// <summary>
/// The text of every Lost Footing log line. One place, so the shape testers read stays pinned. Numbers use the
/// invariant culture and a number the engine could not give reads <c>n/a</c>.
/// </summary>
public static class CensusLine
{
    private const string Tag = "[LostFooting]";

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static string Start(SceneFacts scene, int agentCount) =>
        $"{Tag} start scene={scene.SceneId} type={scene.MissionType} water={Num(scene.WaterLevel, "0.00")} agents={agentCount.ToString(Culture)}";

    public static string Class(StrandClass c, float clock, LostFootingTally tally, int tracked)
    {
        var sb = new StringBuilder(Tag);
        sb.Append(" census t=").Append(clock.ToString("0", Culture)).Append("s class=").Append(Name(c));
        sb.Append(" stranded=").Append(tally.Count(c).ToString(Culture));
        if (c == StrandClass.OffMesh) sb.Append(" confirmed=").Append(tally.Confirmed.ToString(Culture));
        foreach (var k in LostFootingTally.Kinds)
            AppendKind(sb, KindName(k), tally.Count(c, k));
        sb.Append(" tracked=").Append(tracked.ToString(Culture));
        return sb.ToString();
    }

    public static string Sample(StrandClass c, StrandEntry e, float now)
    {
        var sb = new StringBuilder(Tag);
        sb.Append(" sample class=").Append(Name(c)).Append(" kind=").Append(KindName(e.Kind));
        sb.Append(" x=").Append(Num(e.X, "0.0")).Append(" y=").Append(Num(e.Y, "0.0")).Append(" z=").Append(Num(e.Z, "0.0"));
        sb.Append(" depth=").Append(Num(e.Depth, "0.00"));
        sb.Append(" secs=").Append(Num(now - e.StrandedSince, "0.0"));
        sb.Append(" toFormation=").Append(Num(e.DistToFormation, "0.0")).Append(" toDest=").Append(Num(e.DistToDestination, "0.0"));
        sb.Append(" unreachable=").Append(e.Unreachable ? "yes" : "no");
        if (c == StrandClass.OffMesh) AppendDetail(sb, e.Detail);
        return sb.ToString();
    }

    public static string Summary(LostFootingMissionState s)
    {
        var sb = new StringBuilder(Tag);
        sb.Append(" summary scene=").Append(s.SceneId).Append(" time=").Append(s.Clock.ToString("0", Culture)).Append('s');
        sb.Append(" episodes=").Append(s.Episodes.ToString(Culture));
        sb.Append(" recovered=").Append(s.Recovered.ToString(Culture)).Append(" removed=").Append(s.Removed.ToString(Culture));
        sb.Append(" atEnd=").Append(s.AtEnd.ToString(Culture)).Append(" longest=").Append(Num(s.LongestEpisode, "0.0")).Append('s');
        sb.Append(" peak offmesh=").Append(s.PeakOffMesh.ToString(Culture)).Append(" confirmed=").Append(s.PeakConfirmed.ToString(Culture));
        sb.Append(" waterstill=").Append(s.PeakWaterStill.ToString(Culture)).Append(" landbelowwater=").Append(s.PeakLandBelow.ToString(Culture));
        foreach (var k in LostFootingTally.Kinds)
            AppendKind(sb, KindName(k), s.EpisodeKinds[(int)k]);
        sb.Append(" cost probes=").Append(s.Probes.ToString(Culture)).Append(" totalMs=").Append(s.TotalMs.ToString("0.00", Culture));
        sb.Append(" maxTickMs=").Append(s.MaxTickMs.ToString("0.00", Culture));
        return sb.ToString();
    }

    public static string Hotspot(int rank, HotCell cell, float cellMeters)
    {
        var x = cell.CellX * cellMeters;
        var y = cell.CellY * cellMeters;
        return $"{Tag} hotspot {rank.ToString(Culture)} x={Num(x, "0.#")}..{Num(x + cellMeters, "0.#")} " +
               $"y={Num(y, "0.#")}..{Num(y + cellMeters, "0.#")} agentSeconds={Num(cell.AgentSeconds, "0.0")}";
    }

    private static void AppendKind(StringBuilder sb, string name, int count) => sb.Append(' ').Append(name).Append('=').Append(count.ToString(Culture));

    private static void AppendDetail(StringBuilder sb, OffMeshDetail? d)
    {
        if (d == null) return;

        sb.Append(" confirmed=").Append(d.Confirmed ? "yes" : "no").Append(" dynamic=").Append(d.OnDynamicNavMesh ? "yes" : "no");
        if (!d.HasSnap)
        {
            sb.Append(" snap=none");
            return;
        }

        sb.Append(" snap=").Append(Num(d.SnapDistance, "0.0")).Append(" snapOnMesh=").Append(d.SnapHasNavMesh ? "yes" : "no");
    }

    private static string Num(float value, string format) =>
        FiniteFloatValidator.IsFinite(value) ? value.ToString(format, Culture) : "n/a";

    private static string Name(StrandClass c) =>
        c == StrandClass.OffMesh ? "offmesh" : c == StrandClass.WaterStill ? "waterstill" : "landbelowwater";

    private static string KindName(AgentKind k)
    {
        switch (k)
        {
            case AgentKind.Soldier: return "soldier";
            case AgentKind.Router: return "router";
            case AgentKind.RiderlessMount: return "mount";
            case AgentKind.Creature: return "creature";
            case AgentKind.UsingObject: return "object";
            default: return "other";
        }
    }
}
