using System;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

public sealed class GoldTransferAdapter : IGoldTransferAdapter
{
    private readonly IModLogger _logger;

    public GoldTransferAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public int GetHeroGold(string heroId)
    {
        try
        {
            return FindHero(heroId)?.Gold ?? 0;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] GetHeroGold('{heroId}') failed: {ex.Message}");
            return 0;
        }
    }

    public int TransferToPlayer(string fromHeroId, int amount)
    {
        if (amount <= 0)
            return 0;
        try
        {
            var from = FindHero(fromHeroId);
            var player = Hero.MainHero;
            if (from == null || player == null)
                return 0;

            var moved = Math.Min(amount, Math.Max(0, from.Gold));
            if (moved <= 0)
                return 0;

            GiveGoldAction.ApplyBetweenCharacters(from, player, moved, disableNotification: true);
            return moved;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"[Enlistment] TransferToPlayer('{fromHeroId}', {amount}) failed: {ex.Message}");
            return 0;
        }
    }

    private static Hero FindHero(string heroId)
    {
        if (string.IsNullOrEmpty(heroId))
            return null;
        return Campaign.Current?.CampaignObjectManager?.Find<Hero>(heroId);
    }
}
