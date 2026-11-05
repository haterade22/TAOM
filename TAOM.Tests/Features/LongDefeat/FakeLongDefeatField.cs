using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.LongDefeat.Domain;

namespace TAOM.Tests.Features.LongDefeat;

/// <summary>
/// A field with no engine: it counts what it is told to spawn and answers reads from its public numbers. A soldier takes the
/// slot of its index in its batch, as the engine does, so a later batch reuses an earlier one's slots. Agents are strings:
/// "A:troop" is an attacker, "D:troop" a defender and "P" the player.
/// </summary>
internal sealed class FakeLongDefeatField : ILongDefeatBattlefieldAdapter
{
    private readonly List<LdPoint> _defenderPoints = new();

    public List<string> Calls { get; } = new();
    public bool BootOk { get; set; } = true;
    public int HiddenGates { get; set; } = 1;
    public LdPoint? Keep { get; set; } = new LdPoint(138.4f, 159.5f, 32.5f);
    public LdBootFacts Facts { get; set; } = new(true, true, true, false, true, true, true);
    public int AttackersStanding { get; set; }
    public int AttackersFleeing { get; set; }

    /// <summary>What a keep read reports: the attackers and the defenders inside the keep zone.</summary>
    public int AttackersAtKeep { get; set; }

    public int DefendersAtKeep { get; set; }

    /// <summary>Raised once per attacker <see cref="Fell"/> removes, as the engine raises the removal for each felled soldier.</summary>
    public Action? AttackerRemoved { get; set; }
    public int Defenders { get; set; }
    public int MainBody { get; set; } = 1;
    public int BattleSize { get; set; } = 1000;
    public bool SpawnPlayerOk { get; set; } = true;

    /// <summary>When true the defenders have walked off their slots before the next batch arrives.</summary>
    public bool SoldiersMoveOn { get; set; } = true;

    public int OnPoint { get; set; }
    public int Far { get; set; }
    public int OffMesh { get; set; }
    public int ShortBy { get; set; }
    public int FellResult { get; set; } = -1;

    /// <summary>What the last <see cref="Read"/> was asked to measure around.</summary>
    public LdPoint? LastReadKeep { get; private set; }

    public float LastReadRadius { get; private set; }

    public int DefendersFleeing { get; set; }
    public int AttackersHigh { get; set; }
    public int DefendersHigh { get; set; }
    public IReadOnlyList<LdPoint>? Sample { get; set; }

    /// <summary>When true every read throws, as an engine call that faults mid-run would.</summary>
    public bool ThrowOnRead { get; set; }

    public int Index(string call) => Calls.IndexOf(call);

    public int Count(string prefix) => Calls.Count(c => c.StartsWith(prefix, StringComparison.Ordinal));

    public bool Boot(LdPlanSizes sizes, out string problem)
    {
        Calls.Add($"Boot:{sizes.DefenderFoot}/{sizes.DefenderRanged}/{sizes.AttackerFoot}/{sizes.AttackerRanged}");
        problem = BootOk ? "" : "no deployment plan";
        return BootOk;
    }

    public int BreakHiddenGates()
    {
        Calls.Add("BreakGates");
        return HiddenGates;
    }

    public void DeployEngines() => Calls.Add("Deploy");

    public LdPoint? KeepFromScene() => Keep;

    public void PlaceFormation(FormationSlot slot, int count, bool attackers) => Calls.Add($"Place:{Side(attackers)}:{slot}:{count}");

    public void BeginBatch(FormationSlot slot, int count, bool attackers) => Calls.Add($"Begin:{Side(attackers)}:{slot}:{count}");

    /// <summary>Whether a recorded muster place has a navmesh; false makes <see cref="SetMusterFrame"/> refuse.</summary>
    public bool MusterOk { get; set; } = true;

    public bool SetMusterFrame(FormationSlot slot, MusterFrame frame)
    {
        Calls.Add($"Muster:{slot}:{frame.X.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        return MusterOk;
    }

    /// <summary>Every batch the field was asked to spawn, with the side, so a test can read what the run put on it (a health factor).</summary>
    public List<(bool Attackers, SoldierBatch Batch)> Spawned { get; } = new();

    public LdSpawnReport SpawnBatch(SoldierBatch batch, bool attackers)
    {
        Calls.Add($"Spawn:{Side(attackers)}:{batch.TroopId}:{batch.Count}");
        Spawned.Add((attackers, batch));
        var made = Math.Max(0, batch.Count - ShortBy);
        var points = Enumerable.Range(batch.FirstIndex, made).Select(i => new LdPoint(i, attackers ? 50 : 0, 0)).ToList();
        if (attackers)
            AttackersStanding += made;
        else
        {
            Defenders += made;
            _defenderPoints.AddRange(points);
        }

        return new LdSpawnReport(points, Far, OffMesh, OnPoint);
    }

    public IReadOnlyList<LdPoint> StandingDefenderPoints() => SoldiersMoveOn ? Array.Empty<LdPoint>() : _defenderPoints.ToList();

    public void EndBatch(FormationSlot slot, bool attackers) => Calls.Add($"End:{Side(attackers)}:{slot}");

    /// <summary>The loadout each lord spawn was given, in order (null for a run with no class).</summary>
    public List<LdLoadout?> Loadouts { get; } = new();

    /// <summary>What <see cref="ReadKit"/> answers. Null reads back exactly the last loadout, with no mount for a foot class.</summary>
    public LdKitFacts? Kit { get; set; }

    public bool SpawnPlayer(bool respawn, LdLoadout? loadout)
    {
        Calls.Add(loadout == null ? $"SpawnPlayer:{respawn}" : $"SpawnPlayer:{respawn}:{loadout.ClassId}");
        Loadouts.Add(loadout);
        return SpawnPlayerOk;
    }

    public LdKitFacts ReadKit()
    {
        Calls.Add("ReadKit");
        var last = Loadouts.LastOrDefault();
        return Kit ?? new LdKitFacts(last != null, last?.Slots ?? Array.Empty<LdKitSlot>(), Array.Empty<string>(), last?.Mounted ?? false);
    }

    public void FinishInitialSpawn() => Calls.Add("Finish");

    public void RefreshAttackerTactic() => Calls.Add("Refresh");

    public void OrderCharge() => Calls.Add("Charge");

    public void RestoreDefenderTactic() => Calls.Add("Restore");

    public LdRest RestResult { get; set; }

    public LdRest RestDefenders(float share)
    {
        Calls.Add("Rest:" + share.ToString(System.Globalization.CultureInfo.InvariantCulture));
        return RestResult;
    }

    public int Fell(int count)
    {
        Calls.Add($"Fell:{count}");
        var felled = FellResult >= 0 ? FellResult : count;
        AttackersStanding = Math.Max(0, AttackersStanding - felled);
        for (var i = 0; i < felled; i++)
            AttackerRemoved?.Invoke();
        return felled;
    }

    public LdReading Read(LdPoint? keep, float radius, float heightBand, bool countKeep)
    {
        if (ThrowOnRead)
            throw new InvalidOperationException("the field read failed");
        LastReadKeep = keep;
        LastReadRadius = radius;
        if (countKeep)
            Calls.Add("ReadKeep");
        return new LdReading(AttackersStanding, AttackersFleeing, Defenders, countKeep ? AttackersAtKeep : 0, countKeep ? DefendersAtKeep : 0, MainBody, BattleSize, DefendersFleeing, AttackersHigh,
            DefendersHigh);
    }

    public IReadOnlyList<LdPoint> AttackersNearest(LdPoint? keep) => Sample ?? Array.Empty<LdPoint>();

    public void AdoptMain(bool aiDriven) => Calls.Add($"Adopt:{aiDriven}");

    public LdBootFacts BootFacts() => Facts;

    public string DescribeEngines() => "[Engines] none";

    public string DescribePlayer() => "main=fake";

    public string DescribeFormations() => "ai_formations=3 of 3";

    public LdRemoval Classify(object affected, bool loss)
    {
        var text = affected as string ?? "";
        if (!loss)
            return default;
        if (text == "P")
            return new LdRemoval(LdRemovalKind.Player, "");
        if (text.StartsWith("A:", StringComparison.Ordinal))
            return new LdRemoval(LdRemovalKind.Attacker, text.Substring(2));
        return text.StartsWith("D:", StringComparison.Ordinal) ? new LdRemoval(LdRemovalKind.Defender, text.Substring(2)) : default;
    }

    public bool IsAttacker(object? agent) => agent is string s && s.StartsWith("A:", StringComparison.Ordinal);

    public void Clear() => Calls.Add("Clear");

    private static string Side(bool attackers) => attackers ? "att" : "def";
}
