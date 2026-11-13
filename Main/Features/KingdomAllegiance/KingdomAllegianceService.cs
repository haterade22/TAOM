using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.Execution;

namespace TAOM.Features.KingdomAllegiance;

/// <summary>
/// Kingdom Allegiance (docs/features/kingdom-allegiance.md, #1111). Vanilla's AI diplomacy (<c>DiplomaticBartersBehavior</c>) lets
/// any clan join, defect to or hire on with any realm, and its new-game relation seeding rolls friendships blind to the Free/Evil
/// line. This service holds both rules over <see cref="FactionSideRules.AreOpposed"/>: Free against Evil is opposed, and a Neutral
/// side on either end is nobody's opponent.
/// <para>
/// Only AI clans are bound. A clan a human plays, a realm a human leads, and either one in doubt under co-op
/// (<see cref="PlayerControl.Unknown"/>) keep vanilla's score, so the player keeps the choice. A co-op peer deferring to the host
/// and the switch off also keep vanilla. The service keeps no per-campaign state.
/// </para>
/// </summary>
public sealed class KingdomAllegianceService
{
    /// <summary>The value vanilla's own <c>GetScoreOfClanToJoinKingdom</c> gives a realm's ruling clan: never join.</summary>
    public const float VetoScore = -100000000f;

    internal const string VetoCounterId = "taom.kingdom-allegiance.join-vetoed";
    internal const string FlipCheckId = "taom.kingdom-allegiance.friendships-flipped";

    private readonly IKingdomAllegianceSettingsProvider _settings;
    private readonly IAlignmentService _alignment;
    private readonly ICoopSessionProvider _coop;
    private readonly IOutcomeLog _outcomes;
    private readonly OutcomeCounter _vetoes;

    public KingdomAllegianceService(IKingdomAllegianceSettingsProvider settings, IAlignmentService alignment,
        ICoopSessionProvider coop, IOutcomeLog outcomes)
    {
        _settings = settings;
        _alignment = alignment;
        _coop = coop;
        _outcomes = outcomes;
        _vetoes = outcomes.Counter(VetoCounterId);
    }

    /// <summary>
    /// For <c>GetScoreOfClanToJoinKingdom</c> and <c>GetScoreOfMercenaryToJoinKingdom</c>: <see cref="VetoScore"/> when an AI clan
    /// would join a realm of the opposed side, else null, so vanilla's own score stands. The clan's own realm is never vetoed:
    /// vanilla's mercenary leave score subtracts that realm's join score, and clans that already serve stay.
    /// </summary>
    public float? JoinVeto(AllegianceFacts? facts)
    {
        if (facts == null || facts.AlreadyServes
            || facts.Clan != PlayerControl.NotPlayer || facts.KingdomLeader != PlayerControl.NotPlayer)
            return null;
        if (_coop.ShouldDeferToHost || !_settings.Enabled)
            return null;
        if (!FactionSideRules.AreOpposed(_alignment.ResolveSide(facts.ClanSide), _alignment.ResolveSide(facts.KingdomSide)))
            return null;
        _vetoes.Pass();
        return VetoScore;
    }

    /// <summary>
    /// Run once, after vanilla's new-game seeding: every positive relation between two lords of opposed sides becomes a negative
    /// relation of the same size. Same-side rivalries and Neutral lords stay as rolled. Returns the number of pairs flipped.
    /// </summary>
    public int FlipOpposedFriendships(IAllegianceRelationAdapter lords)
    {
        if (_coop.ShouldDeferToHost || !_settings.Enabled)
        {
            _outcomes.Skip(FlipCheckId, "", _coop.ShouldDeferToHost ? "reason=defer-to-host" : "reason=switch-off");
            return 0;
        }

        try
        {
            var ids = lords.ReadLords();
            var sides = new FactionSide[ids.Count];
            for (var i = 0; i < sides.Length; i++)
                sides[i] = _alignment.ResolveSide(ids[i]);

            var opposed = 0;
            var flipped = 0;
            for (var i = 0; i < sides.Length; i++)
            {
                for (var j = i + 1; j < sides.Length; j++)
                {
                    if (!FactionSideRules.AreOpposed(sides[i], sides[j]))
                        continue;
                    opposed++;
                    var relation = lords.ReadRelation(i, j);
                    if (relation <= 0)
                        continue;
                    lords.WriteRelation(i, j, -relation);
                    flipped++;
                }
            }

            _outcomes.Pass(FlipCheckId, "", $"lords={sides.Length} opposed={opposed} flipped={flipped}");
            return flipped;
        }
        finally
        {
            // The adapter is a process singleton: drop the campaign's heroes once the run ends, fault or not.
            lords.Release();
        }
    }
}
