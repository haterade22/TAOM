using DryIoc;

namespace TAOM.Features.TimeAcceleration;

public static class TimeAccelerationIoC
{
    public static void RegisterTimeAccelerationFeature(IContainer container)
    {
        container.Register<IMapInputAdapter, MapInputAdapter>(Reuse.Singleton);
        container.Register<ITimeControlAdapter, TimeControlAdapter>(Reuse.Singleton);
        container.Register<ITimeAccelerationSettingsProvider, TimeAccelerationSettingsProvider>(Reuse.Singleton);
        container.Register<ITimeAccelerationService, TimeAccelerationService>(Reuse.Singleton);
    }
}
