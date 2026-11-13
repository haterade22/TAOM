using System;
using System.Collections.Generic;
using System.Linq;

namespace TAOM.Features.HallSeats.Domain;

/// <summary>One lord the lord's hall would spawn, as the planner sees it. <see cref="Token"/> is the engine's location character, opaque here.</summary>
public sealed class HallSeatCandidate
{
    public HallSeatCandidate(object token, string heroId, bool mustKeep, bool ownerClan, bool clanLeader, int clanTier, float clanRenown)
    {
        Token = token ?? throw new ArgumentNullException(nameof(token));
        HeroId = heroId ?? string.Empty;
        MustKeep = mustKeep;
        OwnerClan = ownerClan;
        ClanLeader = clanLeader;
        ClanTier = clanTier;
        ClanRenown = clanRenown;
    }

    public object Token { get; }

    public string HeroId { get; }

    /// <summary>Always spawns, even past the seat count: the player clan, the owner, the ruler, the governor, a quest giver or quest target, the lord the player asked to see.</summary>
    public bool MustKeep { get; }

    /// <summary>A member of the settlement owner's clan.</summary>
    public bool OwnerClan { get; }

    public bool ClanLeader { get; }

    public int ClanTier { get; }

    public float ClanRenown { get; }
}

/// <summary>The planner's answer: who spawns and who stays out of the hall for this visit.</summary>
public sealed class HallSeatPlan
{
    internal HallSeatPlan(int seats, IReadOnlyCollection<object> heldBack, int seated)
    {
        Seats = seats;
        HeldBack = new HashSet<object>(heldBack, ReferenceEqualityComparer.Instance);
        Seated = seated;
    }

    /// <summary>Lord seats the scene has after the other characters take theirs.</summary>
    public int Seats { get; }

    /// <summary>The lords that do not spawn this visit. They stay in the settlement and in the hall's character list.</summary>
    public IReadOnlyCollection<object> HeldBack { get; }

    public int Seated { get; }

    public bool IsHeldBack(object token) => token != null && ((HashSet<object>)HeldBack).Contains(token);

    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();

        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);

        public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}

/// <summary>
/// Picks which lords take the lord's hall's free lord spots (docs/features/hall-seats.md). Engine-free.
/// Seats = free lord spots minus what the hall's other characters will take. Every must-keep lord spawns; the rest fill
/// the remaining seats in order: owner clan first, then clan leaders, then higher clan tier, then more clan renown, then
/// hero id (a stable order, so the same visit seats the same lords).
/// </summary>
public static class HallSeatPlanner
{
    public static HallSeatPlan Plan(IReadOnlyList<HallSeatCandidate> lords, int freeLordSpots, int spotsTakenByOthers)
    {
        if (lords == null)
            throw new ArgumentNullException(nameof(lords));

        var seats = Math.Max(0, freeLordSpots - Math.Max(0, spotsTakenByOthers));
        var mustKeep = lords.Count(l => l.MustKeep);
        var open = Math.Max(0, seats - mustKeep);

        var ranked = lords.Where(l => !l.MustKeep)
            .OrderByDescending(l => l.OwnerClan)
            .ThenByDescending(l => l.ClanLeader)
            .ThenByDescending(l => l.ClanTier)
            .ThenByDescending(l => l.ClanRenown)
            .ThenBy(l => l.HeroId, StringComparer.Ordinal)
            .ToList();

        var heldBack = ranked.Skip(open).Select(l => l.Token).ToList();
        return new HallSeatPlan(seats, heldBack, lords.Count - heldBack.Count);
    }
}
