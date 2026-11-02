using System.Collections.Generic;
using System.Linq;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Features.FordsOfWar.Diagnostics;

internal sealed partial class FordsRunReport
{
    /// <summary>
    /// One 1 Hz look at the units' footing on a tags map: adds the seconds since the last look to the census and, every 30 mission
    /// seconds, writes one <c>[Lane] faces</c> line per stream and one <c>[Ground]</c> line. Info only: no verdict reads it.
    /// </summary>
    internal void Faces(IReadOnlyList<FordsFaceSample> samples) => Guard(nameof(Faces), () =>
    {
        var run = _run;
        if (run == null)
            return;

        var faces = run.Faces;
        var dt = faces.LastMt < 0.0 ? 0.0 : run.Mt - faces.LastMt;
        faces.LastMt = run.Mt;
        FordsFaceCensus.Add(faces, samples, dt);
        faces.Latest = samples.ToList();
        if (run.Mt < faces.NextPrintMt)
            return;

        faces.NextPrintMt = run.Mt + FordsFaceCensus.PrintSeconds;
        foreach (var body in FordsFaceCensus.StreamLines(faces))
            Info("Lane", body);
        Info("Ground", FordsFaceCensus.GroundLine(faces.Latest, run.Lanes.Sum(l => l.OffNavmesh)));
    });
}
