using System;
using SandBox.Missions.MissionLogics.Arena;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Boundary implementation of <see cref="ITrainingWavesAdapter"/> (docs/features/training-waves.md). Every campaign static and
/// computed getter is read through <c>?.</c> (adapters.md): the player hero is <c>CharacterObject.PlayerCharacter?.HeroObject</c>, never
/// <c>Hero.MainHero</c>. The checks that vanilla makes before it lets the player join a fight (wounded, in disguise, a tournament in the
/// town) are the ones of <c>ArenaMasterCampaignBehavior</c>; the bet multiplier is <c>TournamentBehavior.GetMaximumBet</c>'s.
/// </summary>
public sealed class TrainingWavesAdapter : ITrainingWavesAdapter
{
    private const string Tag = "[TrainingWaves]";

    private readonly IModLogger _logger;

    public TrainingWavesAdapter(IModLogger logger)
    {
        _logger = logger;
    }

    public int Today => Campaign.Current == null ? 0 : (int)CampaignTime.Now.ToDays;

    public string? TownId
    {
        get
        {
            var settlement = Settlement.CurrentSettlement;
            return settlement != null && settlement.IsTown ? settlement.StringId : null;
        }
    }

    public bool TownHasTournament => Settlement.CurrentSettlement?.Town?.HasTournament ?? false;

    public bool PlayerWounded => Player?.IsWounded ?? false;

    public bool PlayerDisguised => Campaign.Current?.IsMainHeroDisguised ?? false;

    /// <summary>The bonus is gated before the float cast: NaN and infinity cast to int.MinValue or a platform value.</summary>
    public int DeepPocketsFactor
    {
        get
        {
            var player = Player;
            if (player == null || !player.GetPerkValue(DefaultPerks.Roguery.DeepPockets))
                return 1;

            var bonus = DefaultPerks.Roguery.DeepPockets.PrimaryBonus;
            return bonus >= 1f && bonus <= 10f ? (int)bonus : 1;
        }
    }

    public bool TalkingToPracticeMaster
    {
        get
        {
            var partner = Campaign.Current?.ConversationManager?.OneToOneConversationCharacter;
            var controller = Controller;
            return partner != null && partner.Occupation == Occupation.ArenaMaster && controller != null && !controller.IsPlayerPracticing;
        }
    }

    public bool VanillaAfterPractice => Controller?.AfterPractice ?? false;

    /// <summary>The null giver is the one vanilla's <c>TournamentBehavior.OnPlayerEliminated</c> passes for a lost bet: only the town is credited.</summary>
    public bool GiveToTown(int amount)
    {
        var settlement = Settlement.CurrentSettlement;
        if (settlement == null || amount <= 0)
            return false;

        try
        {
            GiveGoldAction.ApplyForCharacterToSettlement(null, settlement, amount);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"{Tag} Giving {amount} gold to '{settlement.StringId}' failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>The main hero without a campaign is null here, never a throw (adapters.md).</summary>
    private static Hero? Player => CharacterObject.PlayerCharacter?.HeroObject;

    private static ArenaPracticeFightMissionController? Controller => Mission.Current?.GetMissionBehavior<ArenaPracticeFightMissionController>();
}
