using System.Collections.Generic;

namespace TAOM.Features.Arena;

/// <summary>
/// Decides which tournament roster entrants would crash the vanilla winner panel, and how to
/// describe them in the diagnostic log.
///
/// Why this exists: <c>TournamentVM.OnTournamentEnd</c> (v1.4.7,
/// <c>SandBox.ViewModelCollection.Tournament.TournamentVM</c>) reads the winner's colours through
/// two unguarded dereferences —
/// <code>
///   if (winner.Character.IsHero) { ... hero.MapFaction.Color ... }
///   else                         { ... winner.Character.Culture.Color ... }
/// </code>
/// Either one NREs on a null. The exception surfaces as a
/// <c>TargetInvocationException</c> from <c>ViewModel.ExecuteCommand</c>, which is what a player
/// sees as "the tournament crashed".
///
/// The guard SUBSTITUTES rather than drops. Vanilla's
/// <c>FightTournamentGame.GetParticipantCharacters</c> pads the roster to exactly
/// <c>MaximumParticipantCount</c> (16) with the culture's basic/elite troop;
/// <c>TournamentBehavior.CreateParticipants</c> then copies it into a FIXED 16-slot array and
/// <c>FillParticipants</c> hands EVERY slot to <c>TournamentMatch.AddParticipant</c>, which reads
/// <c>participant.Team</c> — the null check there covers <c>.Team</c>, NOT <c>participant</c> — so a
/// short roster leaves null slots that NRE during bracket construction. Removing an entrant would
/// trade one NRE for another.
///
/// <para><b>What a Safe verdict does NOT promise.</b> <see cref="Classify"/> mirrors the winner
/// panel's two branches and nothing more. In particular a hero can be Safe on a non-null
/// <c>MapFaction</c> resolved from <c>HomeSettlement</c> while <c>Clan</c> is still null — which
/// leaves <c>Hero.ClanBanner</c> (<c>Clan?.Banner</c>) null at <c>OnTournamentEnd</c>'s
/// <c>new BannerImageIdentifierVM(Tournament.Winner.Character.HeroObject.ClanBanner, true)</c>.
/// That does not crash today only because <c>BannerImageIdentifier</c>'s constructor null-checks
/// (<c>banner != null ? banner.BannerCode : ""</c>) — an engine-side property this service does not
/// control. If that ctor ever stops null-checking it becomes a live crash site and Classify needs a
/// third verdict. Recorded because it is safe by coincidence, not by design.</para>
/// </summary>
public interface ITournamentRosterGuardService
{
    /// <summary>Classify a single entrant against the two winner-panel dereferences.</summary>
    TournamentEntrantVerdict Classify(TournamentEntrant entrant);

    /// <summary>
    /// Indices of <paramref name="roster"/> that must be substituted, in ascending order.
    /// Empty when the roster is already safe.
    /// </summary>
    IReadOnlyList<int> FindUnsafeIndices(IReadOnlyList<TournamentEntrant> roster);

    /// <summary>One-line log description of an entrant and the verdict against it.</summary>
    string Describe(TournamentEntrant entrant, TournamentEntrantVerdict verdict);
}
