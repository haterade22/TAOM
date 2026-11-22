using TAOM.Features.TrainingGrounds.Waves.Domain;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>The bet table of the wave fight: training_waves.json over the compiled default.</summary>
internal interface ITrainingWavesConfigProvider
{
    WaveTable GetTable();
}
