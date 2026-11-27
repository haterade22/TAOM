using DryIoc;
using TAOM.Adapters;

namespace TAOM.Features.LoreGreetings;

/// <summary>Registers Lore Greetings through <see cref="LoreGreetingsModule"/>.</summary>
internal static class LoreGreetingsIoC
{
    internal static void RegisterLoreGreetingsFeature(IRegistrator registrator)
    {
        registrator.Register<ILoreGreetingsConfigProvider, LoreGreetingsConfigProvider>(Reuse.Singleton);
        registrator.Register<ILoreGreetingsAdapter, LoreGreetingsAdapter>(Reuse.Singleton);
        registrator.Register<LoreGreetingsService>(Reuse.Singleton);
    }
}
