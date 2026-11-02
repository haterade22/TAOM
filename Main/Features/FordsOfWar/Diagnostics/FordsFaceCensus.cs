using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

/// <summary>Where a unit stands among the navigation face groups of a tags map, from its own lane's point of view.</summary>
internal enum FordsFaceClass
{
    Own,
    Shared,
    Foreign,
    Top,
    Off,
}

/// <summary>Live unit-seconds one stream spent on each class of face, and on foot or mounted.</summary>
internal sealed class FordsFaceStreamEvidence
{
    public double Own;
    public double Shared;
    public double Foreign;
    public double Top;
    public double Off;
    public double Foot;
    public double Mounted;
}

/// <summary>The census of a run: per stream (<c>W0</c>, <c>E2</c>), the last look for the ground line and when the next lines are due.</summary>
internal sealed class FordsFaceEvidence
{
    public Dictionary<string, FordsFaceStreamEvidence> Streams = new();
    public double LastMt = -1.0;
    public double NextPrintMt = FordsFaceCensus.PrintSeconds;
    [JsonIgnore] public List<FordsFaceSample> Latest = new();
}

/// <summary>
/// The pure part of the face census. The shipped tags scene numbers its face groups 0 shared, 1 to 3 the lanes (lane n is group
/// n + 1) and 4 the tower tops; a unit on another lane's group is foreign. Info only, so a different scene's numbering costs a
/// misleading line, never a verdict.
/// </summary>
internal static class FordsFaceCensus
{
    internal const double PrintSeconds = 30.0;
    internal const int SharedId = 0;
    internal const int TopId = 4;

    internal static FordsFaceClass Classify(int lane, int faceId)
    {
        if (faceId < 0)
            return FordsFaceClass.Off;
        if (faceId == SharedId)
            return FordsFaceClass.Shared;
        if (faceId == TopId)
            return FordsFaceClass.Top;
        return faceId == lane + 1 ? FordsFaceClass.Own : FordsFaceClass.Foreign;
    }

    internal static string StreamKey(MatchSide side, int lane) => (side == MatchSide.West ? "W" : "E") + lane.ToString(CultureInfo.InvariantCulture);

    /// <summary>Adds <paramref name="dt"/> seconds for each sampled unit.</summary>
    internal static void Add(FordsFaceEvidence evidence, IReadOnlyList<FordsFaceSample> samples, double dt)
    {
        if (dt <= 0.0)
            return;

        foreach (var s in samples)
        {
            var key = StreamKey(s.Side, s.Lane);
            if (!evidence.Streams.TryGetValue(key, out var stream))
                evidence.Streams[key] = stream = new FordsFaceStreamEvidence();

            switch (Classify(s.Lane, s.FaceId))
            {
                case FordsFaceClass.Own: stream.Own += dt; break;
                case FordsFaceClass.Shared: stream.Shared += dt; break;
                case FordsFaceClass.Foreign: stream.Foreign += dt; break;
                case FordsFaceClass.Top: stream.Top += dt; break;
                default: stream.Off += dt; break;
            }

            if (s.Mounted)
                stream.Mounted += dt;
            else
                stream.Foot += dt;
        }
    }

    /// <summary>The body of a <c>[Lane]</c> line: West streams first, then East, each by lane.</summary>
    internal static IEnumerable<string> StreamLines(FordsFaceEvidence evidence) =>
        evidence.Streams.OrderBy(p => p.Key[0] == 'W' ? 0 : 1).ThenBy(p => p.Key.Length).ThenBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => "faces " + p.Key + " own=" + FordsLogLine.F1(p.Value.Own) + " shared=" + FordsLogLine.F1(p.Value.Shared)
                + " foreign=" + FordsLogLine.F1(p.Value.Foreign) + " top=" + FordsLogLine.F1(p.Value.Top) + " off=" + FordsLogLine.F1(p.Value.Off)
                + " foot=" + FordsLogLine.F1(p.Value.Foot) + " mounted=" + FordsLogLine.F1(p.Value.Mounted));

    /// <summary>The body of the <c>[Ground]</c> line: the latest look's heights as min/median/max, a dash for a value nobody has.</summary>
    internal static string GroundLine(IReadOnlyList<FordsFaceSample> latest, int offNavMoves) =>
        "units=" + latest.Count.ToString(CultureInfo.InvariantCulture)
        + " aboveTerrain=" + Spread(latest.Select(s => s.AboveTerrain))
        + " belowNav=" + Spread(latest.Select(s => s.BelowNav))
        + " offNavMoves=" + offNavMoves.ToString(CultureInfo.InvariantCulture);

    private static string Spread(IEnumerable<float> values)
    {
        var v = values.Where(x => !float.IsNaN(x) && !float.IsInfinity(x)).OrderBy(x => x).ToList();
        if (v.Count == 0)
            return "-/-/-";

        var median = v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2f;
        return FordsLogLine.F1(v[0]) + "/" + FordsLogLine.F1(median) + "/" + FordsLogLine.F1(v[v.Count - 1]);
    }
}
