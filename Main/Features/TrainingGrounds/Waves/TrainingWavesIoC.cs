using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.TrainingGrounds.Waves;

/// <summary>
/// Registers the wave fight at the training master (docs/features/training-waves.md); every shared piece it reads (the purse adapter,
/// the tournament settings, the co-op providers, the outcome log, the path service) is registered by another feature.
/// </summary>
internal static class TrainingWavesIoC
{
    internal static void RegisterTrainingWavesFeature(IRegistrator container)
    {
        container.Register<ITrainingWavesConfigProvider, TrainingWavesConfigProvider>(Reuse.Singleton);
        container.Register<ITrainingWavesSettingsProvider, TrainingWavesSettingsProvider>(Reuse.Singleton);
        container.Register<ITrainingWavesAdapter, TrainingWavesAdapter>(Reuse.Singleton);
        container.Register<WaveBetService>(Reuse.Singleton);
    }
}
