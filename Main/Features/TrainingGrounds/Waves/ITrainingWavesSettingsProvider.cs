namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>The live (MCM over default) switch for the wave fight at the training master.</summary>
public interface ITrainingWavesSettingsProvider
{
    bool Enabled { get; }
}
