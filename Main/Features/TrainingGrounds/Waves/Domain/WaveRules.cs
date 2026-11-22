namespace TAOM.Features.TrainingGrounds.Waves.Domain;

/// <summary>
/// The rules around the table: the smallest stake, the hard ceiling on any stake, whether a town allows one run a day, and the
/// seconds before the player is taken back to the master and between refills of the field.
/// </summary>
internal sealed record WaveRules(int MinStake, int HardCeiling, bool OneRunPerTownPerDay, int ReturnSeconds, int RefillSeconds)
{
    public static WaveRules Default { get; } = new(10, 15_000, true, 5, 3);
}
