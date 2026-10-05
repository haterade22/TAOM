using System;
using System.Reflection;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.CampaignSystem.TournamentGames;
using TaleWorlds.Core;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class TournamentJoinAdapter : ITournamentJoinAdapter
{
    private const string Tag = "[TournamentRewards]";

    // TournamentGame.Prize is { get; private set; } in v1.5.3; pinned by TournamentRewardsBindingTests.
    internal static readonly MethodInfo? PrizeSetter =
        AccessTools.PropertySetter(typeof(TournamentGame), nameof(TournamentGame.Prize));

    private readonly IModLogger _logger;

    public TournamentJoinAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public TournamentJoinSnapshot? GetCurrentTournament()
    {
        try
        {
            var town = Settlement.CurrentSettlement?.Town;
            var tournament = town == null ? null : Campaign.Current?.TournamentManager?.GetTournamentGame(town);
            if (tournament == null)
                return null;
            var townId = town!.Settlement.StringId;
            var seedKey = $"{townId}:{Math.Round(tournament.CreationTime.ToHours * 1000d):0}";
            var prize = tournament.Prize;
            return new TournamentJoinSnapshot(townId, town.Culture?.StringId, seedKey, prize?.StringId,
                prize == null ? (int?)null : (int)prize.Tier);
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} could not read the tournament to join ({ex.GetType().Name}: {ex.Message}); joining as vanilla");
            return null;
        }
    }

    public bool SetPrize(string townId, string itemId)
    {
        try
        {
            var settlement = Settlement.Find(townId);
            var tournament = settlement?.Town == null ? null : Campaign.Current?.TournamentManager?.GetTournamentGame(settlement.Town);
            var item = Game.Current?.ObjectManager?.GetObject<ItemObject>(itemId);
            if (tournament == null || item == null || PrizeSetter == null)
            {
                _logger.LogWarning($"{Tag} could not set the prize '{itemId}' in {townId} (tournament {tournament != null}, item {item != null}, setter {PrizeSetter != null})");
                return false;
            }
            PrizeSetter.Invoke(tournament, new object[] { item });
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"{Tag} setting the prize '{itemId}' in {townId} failed ({ex.GetType().Name}: {ex.Message}); the advertised prize stands");
            return false;
        }
    }
}
