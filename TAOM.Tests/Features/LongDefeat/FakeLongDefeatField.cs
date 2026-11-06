using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Adapters;
using TAOM.Features.FordsOfWar.Domain;
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

    /// <summary>A troop the engine cannot spawn: its batches make nobody.</summary>
    public string? Unspawnable { get; set; }
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
        var made = batch.TroopId == Unspawnable ? 0 : Math.Max(0, batch.Count - ShortBy);
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

    /// <summary>The place each lord spawn was given, in order (null for a spawn at the engine's own frame).</summary>
    public List<LdPlace?> Places { get; } = new();

    public bool SpawnPlayer(bool respawn, LdLoadout? loadout, LdPlace? place = null)
    {
        Calls.Add(loadout == null ? $"SpawnPlayer:{respawn}" : $"SpawnPlayer:{respawn}:{loadout.ClassId}");
        Loadouts.Add(loadout);
        Places.Add(place);
        return SpawnPlayerOk;
    }

    /// <summary>What <see cref="SallyPointUsable"/> answers for a place; every point has a navmesh and lies inside the boundary by default.</summary>
    public Func<LdPlace, LdPointFacts> PointFacts { get; set; } = _ => new LdPointFacts(true, true);

    /// <summary>The points the director asked about, in order.</summary>
    public List<LdPlace> PointsAsked { get; } = new();

    public LdPointFacts SallyPointUsable(LdPlace place)
    {
        PointsAsked.Add(place);
        Calls.Add("PointCheck:" + F(place.X));
        return PointFacts(place);
    }

    /// <summary>The lord as the next read finds him: standing, his own body, mounted, inside the boundary. A teleport or a remount moves him.</summary>
    public LdLordFacts Lord { get; set; } = new(true, true, "lord_4_1", true, new LdPoint(10f, 20f, 30f), true, "Cavalry", new LdPoint(10f, 20f, 30f), 3f, 12f, "closed");

    public int LordReads { get; private set; }

    public LdLordFacts ReadLord(bool detail)
    {
        LordReads++;
        Calls.Add(detail ? "ReadLord:detail" : "ReadLord");
        return Lord;
    }

    public List<LdPlace> Teleports { get; } = new();

    public bool TeleportOk { get; set; } = true;

    public bool TeleportPlayer(LdPlace place)
    {
        Calls.Add("Teleport:" + F(place.X));
        Teleports.Add(place);
        if (TeleportOk)
            Lord = Lord with { At = place.Point };
        return TeleportOk;
    }

    public List<(LdPlace Place, LdLoadout Loadout)> Remounts { get; } = new();

    /// <summary>What a remount answers; by default it worked, the old body read Routed and was not lost, and he kept half his health.</summary>
    public LdRemount RemountResult { get; set; } = new(true, "Routed", false, 0.5f, "");

    public LdRemount RemountPlayer(LdPlace place, LdLoadout loadout)
    {
        Calls.Add("Remount:" + F(place.X));
        Remounts.Add((place, loadout));
        if (RemountResult.Done)
        {
            Lord = Lord with { At = place.Point, HasMount = true };
            MainBody++;
        }

        return RemountResult;
    }

    private readonly LongDefeatLordWatch _watch = new();

    /// <summary>Hits the director asked to classify as contact: "P" is the lord, "M" his horse, an "A:", "S:" or "C:" text an attacker.</summary>
    public bool TryContact(object? affected, object? affector, float victimHealth, out LdContact contact)
    {
        _watch.SetLord("P");
        return _watch.TryContact(affected, affector, "M", victimHealth, a => a is string s && s.Length > 1 && "ASC".Contains(s[0]) && s[1] == ':', out contact);
    }

    public int BodyNotes { get; private set; }

    public void NoteBodyEvent(object agent, string callback, string state) => BodyNotes++;

    /// <summary>The body events the next <see cref="TakeBodyEvents"/> hands back; emptied by it.</summary>
    public List<LdBodyEvent> PendingBodyEvents { get; } = new();

    public IReadOnlyList<LdBodyEvent> TakeBodyEvents()
    {
        var taken = PendingBodyEvents.ToList();
        PendingBodyEvents.Clear();
        return taken;
    }

    private static string F(float value) => value.ToString("F0", System.Globalization.CultureInfo.InvariantCulture);

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

    /// <summary>The mean health share of the defenders, as <see cref="Read"/> reports it. A heal with <see cref="RestHeals"/> set moves it.</summary>
    public float DefenderHealthMean { get; set; } = 0.6f;

    /// <summary>When true <see cref="RestDefenders"/> heals <see cref="DefenderHealthMean"/> by the share and answers from it, as the engine's write and read-back would; otherwise it answers <see cref="RestResult"/>.</summary>
    public bool RestHeals { get; set; }

    /// <summary>Added to every heal's result, as the engine's ceil on <c>Agent.Health</c> would (E23).</summary>
    public float RestCeil { get; set; }

    public LdRest RestDefenders(float share)
    {
        Calls.Add("Rest:" + share.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!RestHeals)
            return RestResult;

        var before = DefenderHealthMean;
        DefenderHealthMean = Math.Min(1f, before + share * (1f - before) + RestCeil);
        return new LdRest(Math.Max(1, Defenders), before, DefenderHealthMean);
    }

    /// <summary>Soldiers <see cref="Fell"/> has marked and the next plain attacker removals will consume as felled, as the registry's mark does for the real field.</summary>
    public int FelledMarks { get; private set; }

    public int Fell(int count)
    {
        Calls.Add($"Fell:{count}");
        var felled = FellResult >= 0 ? FellResult : count;
        FelledMarks += felled;
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
            DefendersHigh, DefenderHealthMean);
    }

    public IReadOnlyList<LdPoint> AttackersNearest(LdPoint? keep) => Sample ?? Array.Empty<LdPoint>();

    public void AdoptMain(bool aiDriven) => Calls.Add($"Adopt:{aiDriven}");

    public LdBootFacts BootFacts() => Facts;

    public string DescribeEngines() => "[Engines] none";

    public string DescribePlayer() => "main=fake";

    public string DescribeFormations() => "ai_formations=3 of 3";

    /// <summary>
    /// "A:troop" is a roster attacker, "S:troop" a summoned one and "C:troop" the commander; a trailing "!" marks the removal felled, and a plain "A:" consumes one mark <see cref="Fell"/> left. A
    /// commander removed with no loss is reported (a rout), any other non-loss removal is nothing.
    /// </summary>
    public LdRemoval Classify(object affected, bool loss)
    {
        var text = affected as string ?? "";
        var key = KeyOf(text.TrimEnd('!'));
        if (text.StartsWith("C:", StringComparison.Ordinal))
            return new LdRemoval(LdRemovalKind.Attacker, text.TrimEnd('!').Substring(2), AttackerRole.Commander, text.EndsWith("!", StringComparison.Ordinal), loss, key);
        if (!loss)
            return new LdRemoval(LdRemovalKind.None, "", Key: key);
        if (text == "P")
            return new LdRemoval(LdRemovalKind.Player, "", Key: key);
        if (text.StartsWith("A:", StringComparison.Ordinal) || text.StartsWith("S:", StringComparison.Ordinal))
        {
            var marked = text.EndsWith("!", StringComparison.Ordinal);
            if (!marked && text[0] == 'A' && FelledMarks > 0)
            {
                FelledMarks--;
                marked = true;
            }

            return new LdRemoval(LdRemovalKind.Attacker, text.TrimEnd('!').Substring(2), text[0] == 'S' ? AttackerRole.Summoned : AttackerRole.Roster, marked, true, key);
        }

        return text.StartsWith("D:", StringComparison.Ordinal) ? new LdRemoval(LdRemovalKind.Defender, text.Substring(2), Key: key) : default;
    }

    public bool IsAttacker(object? agent) => agent is string s && s.StartsWith("A:", StringComparison.Ordinal);

    private readonly Dictionary<string, AgentKey> _keys = new();

    /// <summary>How many hits the director asked this field to resolve to keys.</summary>
    public int HitAsks { get; private set; }

    /// <summary>The key the field gave an agent text on its first hit (keys count from 1), or default for a text it has not been asked about.</summary>
    public AgentKey KeyOf(string text) => _keys.TryGetValue(text, out var key) ? key : default;

    /// <summary>An attacker ("A:", "S:" or "C:" text) striking a defender ("D:" or "P"): both get a key the first time they are named, as the registry hands one out.</summary>
    public bool TryHitKeys(object? affected, object? affector, out AgentKey victim, out AgentKey attacker)
    {
        HitAsks++;
        victim = attacker = default;
        if (affected is not string hit || affector is not string hitter)
            return false;
        if (!(hit == "P" || hit.StartsWith("D:", StringComparison.Ordinal)) || !(hitter.Length > 1 && "ASC".Contains(hitter[0]) && hitter[1] == ':'))
            return false;

        victim = Key(hit);
        attacker = Key(hitter);
        return true;
    }

    private AgentKey Key(string text)
    {
        if (!_keys.TryGetValue(text, out var key))
            _keys[text] = key = new AgentKey(_keys.Count + 1);
        return key;
    }

    public void Clear() => Calls.Add("Clear");

    private static string Side(bool attackers) => attackers ? "att" : "def";
}
