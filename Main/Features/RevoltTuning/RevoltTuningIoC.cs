using DryIoc;

namespace TAOM.Features.RevoltTuning;

public static class RevoltTuningIoC
{
    public static void RegisterRevoltTuningFeature(IContainer container)
    {
        container.Register<IRevoltTuningConfigProvider, RevoltTuningConfigProvider>(Reuse.Singleton);
    }
}
