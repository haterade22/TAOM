using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.MarshalsWord.Domain;

namespace TAOM.Tests.Features.MarshalsWord;

/// <summary>The engine side of throwers on an advance, as a test sets it: each company's order, census, gates and place, and every table that was published.</summary>
internal sealed class FakeMarshalThrowers : IMarshalThrowerAdapter
{
    internal readonly Dictionary<int, ThrowerCompany> Companies = new();
    internal readonly Dictionary<int, ThrowerCensus> Censuses = new();
    internal readonly Dictionary<int, ThrowerGates> GatesOf = new();
    internal readonly Dictionary<int, float?> Ranges = new();
    internal readonly List<int> RangeReads = new();
    internal readonly List<int> CensusReads = new();
    internal readonly List<ThrowerPoint[]> Tables = new();

    public double Now { get; set; }

    public bool PlayerCommands { get; set; } = true;

    public ThrowerCompany Company(int company) => Companies.TryGetValue(company, out var state) ? state : default;

    public ThrowerCensus Census(int company)
    {
        CensusReads.Add(company);
        return Censuses.TryGetValue(company, out var census) ? census : default;
    }

    public float? Range(int company)
    {
        RangeReads.Add(company);
        return Ranges.TryGetValue(company, out var range) ? range : null;
    }

    public ThrowerGates Gates(int company) => GatesOf[company];

    public void Publish(IReadOnlyList<ThrowerPoint> points) => Tables.Add(points.ToArray());

    internal ThrowerPoint[] Latest => Tables.Count == 0 ? System.Array.Empty<ThrowerPoint>() : Tables[Tables.Count - 1];

    /// <summary>A company of forty javelin soldiers on an Advance, a hundred metres short of an enemy company, in a field battle.</summary>
    internal ThrowerGates Standard(int company, float y = 0f)
    {
        Companies[company] = new ThrowerCompany(HasUnits: true, OnAdvance: true, AiControlled: false);
        Censuses[company] = new ThrowerCensus(40, 40, 120);
        Ranges[company] = 80f;
        var gates = new ThrowerGates(
            SettingOn: true, FieldBattle: true, Deployment: false, Advance: true, AiControlled: false, Held: false, OnFireMode: false, HoldFire: false,
            OpenArrangement: true, RangedBranch: false, HasTarget: true, Range: null, OwnDepth: 6f, Own: new GroundPoint(0f, y), Target: new GroundPoint(0f, 100f),
            TargetDepth: 6f);
        GatesOf[company] = gates;
        return gates;
    }

    internal void MoveTo(int company, float y) => GatesOf[company] = GatesOf[company] with { Own = new GroundPoint(0f, y) };
}
