using TAOM.Core.Validation;

namespace TAOM.Features.TournamentRewards;

/// <summary>
/// The MCM "Tournaments" group. MCM's slider clamps to its attribute range but its settings file loader does not,
/// so each value is clamped here to the slider's own range (<see cref="TournamentRewardRules.MaxMultiplier"/>,
/// <see cref="TournamentRewardRules.MaxBetSetting"/>) before it reaches the rules; the rules then check each factor
/// and result again (TournamentRewardRules).
/// </summary>
public interface ITournamentRewardsSettingsProvider
{
    /// <summary>The bet cap per round; 0 or less is unlimited.</summary>
    int MaxBetPerRound { get; }

    float RenownMultiplier { get; }

    float InfluenceMultiplier { get; }
}

public sealed class TournamentRewardsSettingsProvider : ITournamentRewardsSettingsProvider
{
    private readonly TaomSettings? _settings;

    public TournamentRewardsSettingsProvider() { }

    internal TournamentRewardsSettingsProvider(TaomSettings settings) => _settings = settings;

    private TaomSettings? Settings => _settings ?? TaomSettings.Instance;

    public int MaxBetPerRound =>
        SettingClamp.Clamp(Settings?.TournamentMaxBetPerRound, 0, 0, TournamentRewardRules.MaxBetSetting);

    public float RenownMultiplier =>
        SettingClamp.Clamp(Settings?.TournamentRenownMultiplier, 1f, 0f, TournamentRewardRules.MaxMultiplier);

    public float InfluenceMultiplier =>
        SettingClamp.Clamp(Settings?.TournamentInfluenceMultiplier, 1f, 0f, TournamentRewardRules.MaxMultiplier);
}
