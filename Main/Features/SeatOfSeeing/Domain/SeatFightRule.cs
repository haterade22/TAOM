using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>What the mission tick owes the hero's hand-off.</summary>
internal enum SeatFightAction
{
    None,
    Begin,
    End,
}

/// <summary>Why the hero could not be handed to the AI.</summary>
internal enum SeatFightRefusal
{
    None,
    MissionEnded,
    NoHero,
    NotPlayerControlled,
}

/// <summary>What the hero adapter reads about the mission and the main agent before a hand-off.</summary>
internal readonly record struct SeatFightFacts(bool MissionEnded, bool MissionEnding, bool HeroActive, bool HeroIsPlayerControlled);

/// <summary>The decisions of the hero hand-off (build-spec section 8).</summary>
internal static class SeatFightRule
{
    /// <summary>Real seconds between two looks for a retreat order on the handed hero.</summary>
    internal const float RetreatCheckSeconds = 1f;

    /// <summary>Tries a failed hand-back gets in all, the first one included.</summary>
    internal const int HandBackTries = 3;

    /// <summary>The hero should be on the AI: the Seat is up, no fault has latched and this entry chose to fight on.</summary>
    internal static bool Wanted(SeatState state, bool faulted, bool chosen) => state != SeatState.Off && !faulted && chosen;

    internal static SeatFightAction Owed(bool wanted, bool actual) =>
        wanted == actual ? SeatFightAction.None : wanted ? SeatFightAction.Begin : SeatFightAction.End;

    internal static SeatFightRefusal BeginRefusal(in SeatFightFacts facts)
    {
        if (facts.MissionEnded || facts.MissionEnding)
            return SeatFightRefusal.MissionEnded;
        if (!facts.HeroActive)
            return SeatFightRefusal.NoHero;
        return facts.HeroIsPlayerControlled ? SeatFightRefusal.None : SeatFightRefusal.NotPlayerControlled;
    }

    internal static string Reason(SeatFightRefusal refusal) => refusal switch
    {
        SeatFightRefusal.MissionEnded => "mission-ended",
        SeatFightRefusal.NoHero => "no-hero",
        SeatFightRefusal.NotPlayerControlled => "not-player",
        _ => "none",
    };

    /// <summary>True before the first check and then once a real second; a time that is not a number is never due.</summary>
    internal static bool RetreatCheckDue(float now, float last) =>
        FiniteFloatValidator.IsFinite(now) && (!FiniteFloatValidator.IsFinite(last) || now - last >= RetreatCheckSeconds);
}
