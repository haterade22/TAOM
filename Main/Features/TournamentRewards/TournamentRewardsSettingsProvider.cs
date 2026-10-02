namespace TAOM.Features.TournamentRewards;

/// <summary>The MCM "Tournaments" group. Values are validated where they are used (TournamentRewardRules).</summary>
public interface ITournamentRewardsSettingsProvider
{
    /// <summary>The bet cap per round; 0 or less is unlimited.</summary>
    int MaxBetPerRound { get; }

    float RenownMultiplier { get; }

    float InfluenceMultiplier { get; }
}

public sealed class TournamentRewardsSettingsProvider : ITournamentRewardsSettingsProvider
{
    public int MaxBetPerRound => TaomSettings.Instance?.TournamentMaxBetPerRound ?? 0;

    public float RenownMultiplier => TaomSettings.Instance?.TournamentRenownMultiplier ?? 1f;

    public float InfluenceMultiplier => TaomSettings.Instance?.TournamentInfluenceMultiplier ?? 1f;
}
