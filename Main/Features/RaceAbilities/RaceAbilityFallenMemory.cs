using System;
using System.Collections.Generic;
using TAOM.Features.RaceAbilities.Domain;

namespace TAOM.Features.RaceAbilities;

// Recent deaths, so a soldier can sense that a kinsman fell near him (the KinFell trigger). Main thread
// only. Kin means the same team and either the same ability profile or a race the soldier's profile names
// as kin (orcs and goblins swarm together). Generic over the team so tests need no engine.
public sealed class RaceAbilityFallenMemory<TTeam> where TTeam : class
{
    public const int Capacity = 256;

    // A trigger's event window is capped at 30 s, so nothing older can matter.
    public const float MemorySeconds = 30f;

    private readonly List<Record> _records = new List<Record>();

    public int Count => _records.Count;

    public void Remember(float x, float y, TTeam team, int race, RaceAbilityProfile? profile, float at)
    {
        if (_records.Count >= Capacity)
            _records.RemoveAt(0);
        _records.Add(new Record(x, y, team, race, profile, at));
    }

    // A NaN clock keeps nothing: a stale memory is worse than a lost one.
    public void Forget(float now)
    {
        for (var i = _records.Count - 1; i >= 0; i--)
            if (!(now - _records[i].At <= MemorySeconds))
                _records.RemoveAt(i);
    }

    public void SenseKin(TTeam team, RaceAbilityProfile profile, ICollection<int> kinRaces, float x, float y, List<FallenSense> into)
    {
        foreach (var record in _records)
        {
            if (!ReferenceEquals(record.Team, team))
                continue;
            if (!ReferenceEquals(record.Profile, profile) && !kinRaces.Contains(record.Race))
                continue;
            var dx = record.X - x;
            var dy = record.Y - y;
            into.Add(new FallenSense((float)Math.Sqrt(dx * dx + dy * dy), record.At));
        }
    }

    public void Clear() => _records.Clear();

    private readonly struct Record
    {
        public Record(float x, float y, TTeam team, int race, RaceAbilityProfile? profile, float at)
        {
            X = x;
            Y = y;
            Team = team;
            Race = race;
            Profile = profile;
            At = at;
        }

        public float X { get; }

        public float Y { get; }

        public TTeam Team { get; }

        public int Race { get; }

        public RaceAbilityProfile? Profile { get; }

        public float At { get; }
    }
}
