using System;
using TaleWorlds.CampaignSystem;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;

namespace TAOM.Features.KingdomAllegiance.Models;

/// <summary>
/// Boundary reader (ADR-007): turns the clan and realm of one join score into <see cref="AllegianceFacts"/> for
/// <c>TaomDiplomacyModel</c>. The clan is sided by its own id and culture, the realm by its own (<see cref="FactionSides"/>); who is a
/// player comes from the co-op-aware <see cref="IPlayerControlQuery"/>, never from <c>Clan.PlayerClan</c>. Main thread, a few times per clan
/// per day, so a fault goes to <see cref="IOutcomeLog.Swallowed"/>: the full exception once, then a count in the session summary.
/// <see cref="Read"/> is virtual so a model test can stand in for it.
/// </summary>
public class AllegianceFactsReader
{
    internal const string Feature = "KingdomAllegiance";
    internal const string ReadSite = "AllegianceFactsReader.Read";

    private readonly IPlayerControlQuery _players;
    private readonly IOutcomeLog _outcomes;

    public AllegianceFactsReader(IPlayerControlQuery players, IOutcomeLog outcomes)
    {
        _players = players;
        _outcomes = outcomes;
    }

    /// <summary>The facts, or null when either side is missing or a read faults, so vanilla's score stands.</summary>
    public virtual AllegianceFacts? Read(Clan? clan, Kingdom? kingdom)
    {
        try
        {
            if (clan == null || kingdom == null)
                return null;
            return new AllegianceFacts(
                FactionSides.Of(clan),
                FactionSides.Of(kingdom),
                _players.OfClan(clan),
                _players.OfHero(kingdom.Leader),
                clan.Kingdom == kingdom);
        }
        catch (Exception ex)
        {
            _outcomes.Swallowed(Feature, ReadSite, ex);
            return null;
        }
    }
}
