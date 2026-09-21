using DryIoc;
using TAOM.Adapters;
using TAOM.Features.CultureConversion.GarrisonSwap;

namespace TAOM.Features.CultureConversion;

public static class CultureConversionIoC
{
    public static void RegisterCultureConversionFeature(IContainer container)
    {
        container.Register<ICultureConversionConfigProvider, CultureConversionConfigProvider>(Reuse.Singleton);
        container.Register<ICultureConversionSettingsProvider, CultureConversionSettingsProvider>(Reuse.Singleton);
        container.Register<ICultureConversionStore, CultureConversionStore>(Reuse.Singleton);
        container.Register<ICultureConversionAdapter, CultureConversionAdapter>(Reuse.Singleton);

        // Garrison + militia re-manning, invoked from ApplyConversion. The adapter is a singleton
        // because it caches the per-culture troop index for the process.
        container.Register<IGarrisonCultureSwapAdapter, GarrisonCultureSwapAdapter>(Reuse.Singleton);
        container.Register<ITroopCultureMapper, TroopCultureMapper>(Reuse.Singleton);
        container.Register<IGarrisonCultureSwapService, GarrisonCultureSwapService>(Reuse.Singleton);

        container.Register<ICultureConversionService, CultureConversionService>(Reuse.Singleton);
    }
}
