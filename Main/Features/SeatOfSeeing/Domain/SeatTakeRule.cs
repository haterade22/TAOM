using System.Collections.Generic;
using TAOM.Core.Validation;

namespace TAOM.Features.SeatOfSeeing.Domain;

/// <summary>Why a soldier cannot be taken.</summary>
internal enum SeatTakeRefusal
{
    None,
    NotHuman,
    NotActive,
    OtherTeam,
    UsingObject,
    Retreating,
    FadingOut,
    Wounded,
    Hero,
    PlatformCrew,
    NoFormation,
}

/// <summary>
/// What the screen adapter read about one soldier. <paramref name="HeroOfPlayersParty"/> is true only in a campaign, for a hero whose origin
/// is the player's own party; with no campaign it is false for everyone. <paramref name="PlatformCrew"/> is a howdah or mumak crew member,
/// whose deck rules assume the AI. <paramref name="InFormation"/> is false for a soldier who belongs to no formation.
/// </summary>
internal readonly record struct SeatSoldierFacts(
    bool Human, bool Active, bool OnPlayerTeam, bool UsingObject, bool Retreating, bool FadingOut, float HealthShare,
    bool Hero, bool HeroOfPlayersParty, bool PlatformCrew, bool InFormation);

/// <summary>One soldier of a kept company: how far he is from the kept point, and whether he may be taken.</summary>
internal readonly record struct SeatTakeCandidate(float DistanceSquared, SeatTakeRefusal Refusal);

/// <summary>What a take did, for the session's check lines and messages.</summary>
internal enum SeatTakeOutcome
{
    Taken,
    HeroAlive,
    Nobody,
    Refused,
}

internal enum SeatTakeKind
{
    Soldier,
    Hero,
}

internal readonly record struct SeatTakeResult(SeatTakeOutcome Outcome, SeatTakeRefusal Refusal, SeatTakeKind Kind, int Formations)
{
    internal static SeatTakeResult Taken(SeatTakeKind kind, int formations) => new(SeatTakeOutcome.Taken, SeatTakeRefusal.None, kind, formations);

    internal static SeatTakeResult HeroAlive() => new(SeatTakeOutcome.HeroAlive, SeatTakeRefusal.None, SeatTakeKind.Soldier, 0);

    internal static SeatTakeResult Nobody() => new(SeatTakeOutcome.Nobody, SeatTakeRefusal.None, SeatTakeKind.Soldier, 0);

    internal static SeatTakeResult Refused(SeatTakeRefusal refusal) => new(SeatTakeOutcome.Refused, refusal, SeatTakeKind.Soldier, 0);
}

/// <summary>TAOM's own eligibility rule for taking a soldier after the hero falls (build-spec section 9).</summary>
internal static class SeatTakeRule
{
    /// <summary>The share of health a soldier needs, as in the engine's own take (Mission.cs:6397).</summary>
    internal const float MinimumHealthShare = 0.25f;

    internal static SeatTakeRefusal Check(in SeatSoldierFacts facts)
    {
        if (!facts.Human)
            return SeatTakeRefusal.NotHuman;
        if (!facts.Active)
            return SeatTakeRefusal.NotActive;
        if (!facts.OnPlayerTeam)
            return SeatTakeRefusal.OtherTeam;
        if (facts.UsingObject)
            return SeatTakeRefusal.UsingObject;
        if (facts.Retreating)
            return SeatTakeRefusal.Retreating;
        if (facts.FadingOut)
            return SeatTakeRefusal.FadingOut;
        if (facts.PlatformCrew)
            return SeatTakeRefusal.PlatformCrew;
        if (!facts.InFormation)
            return SeatTakeRefusal.NoFormation;
        if (facts.Hero && !facts.HeroOfPlayersParty)
            return SeatTakeRefusal.Hero;
        return FiniteFloatValidator.IsFinite(facts.HealthShare) && facts.HealthShare >= MinimumHealthShare ? SeatTakeRefusal.None : SeatTakeRefusal.Wounded;
    }

    internal static string Reason(SeatTakeRefusal refusal) => refusal switch
    {
        SeatTakeRefusal.NotHuman => "not-human",
        SeatTakeRefusal.NotActive => "not-active",
        SeatTakeRefusal.OtherTeam => "other-team",
        SeatTakeRefusal.UsingObject => "using-object",
        SeatTakeRefusal.Retreating => "retreating",
        SeatTakeRefusal.FadingOut => "fading-out",
        SeatTakeRefusal.Wounded => "wounded",
        SeatTakeRefusal.Hero => "hero",
        SeatTakeRefusal.PlatformCrew => "platform-crew",
        SeatTakeRefusal.NoFormation => "no-formation",
        _ => "none",
    };

    /// <summary>The index of the closest candidate who may be taken, or -1; a distance that is not a number is never the closest.</summary>
    internal static int Nearest(IReadOnlyList<SeatTakeCandidate> candidates)
    {
        var best = -1;
        var bestDistance = float.MaxValue;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (candidate.Refusal != SeatTakeRefusal.None || !(candidate.DistanceSquared < bestDistance))
                continue;
            best = i;
            bestDistance = candidate.DistanceSquared;
        }

        return best;
    }
}
