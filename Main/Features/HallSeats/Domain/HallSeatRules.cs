using System;
using System.Collections.Generic;

namespace TAOM.Features.HallSeats.Domain;

/// <summary>One usable machine listed under a lord tag: its engine identity (opaque, compared by reference) and its free usable points.</summary>
public sealed class HallSeatMachine
{
    public HallSeatMachine(object machine, int freePoints)
    {
        Machine = machine ?? throw new ArgumentNullException(nameof(machine));
        FreePoints = freePoints;
    }

    public object Machine { get; }

    public int FreePoints { get; }
}

/// <summary>Plain-data rules of Hall Seats (docs/features/hall-seats.md). The adapter reads the engine and passes facts in.</summary>
public static class HallSeatRules
{
    /// <summary>
    /// Seats in the lord tags: the distinct machines that still have a free point, each counted once however many tags
    /// list it and however many points it has. Vanilla lists a machine under every tag its entity carries and one spawn
    /// marks the whole machine used (<c>GetInitialFrameForSpawnTag</c>), so a machine seats one agent.
    /// </summary>
    public static int CountSeats(IEnumerable<IReadOnlyList<HallSeatMachine>> machinesPerTag)
    {
        if (machinesPerTag == null)
            throw new ArgumentNullException(nameof(machinesPerTag));

        var seen = new HashSet<object>(HallSeatPlan.ReferenceEqualityComparer.Instance);
        var seats = 0;
        foreach (var tag in machinesPerTag)
        {
            foreach (var machine in tag)
            {
                if (machine.FreePoints > 0 && seen.Add(machine.Machine))
                    seats++;
            }
        }

        return seats;
    }

    /// <summary>A lord who always spawns, even past the seat count: any one term is enough.</summary>
    public static bool IsMustKeep(bool playerClan, bool settlementOwner, bool kingdomLeader, bool governor,
        bool conversationTarget, bool hasIssue, bool questGiver, bool questTarget) =>
        playerClan || settlementOwner || kingdomLeader || governor || conversationTarget || hasIssue || questGiver || questTarget;
}
