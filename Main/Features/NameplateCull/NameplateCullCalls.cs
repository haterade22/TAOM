// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), nameplate-cull.
namespace TAOM.Features.NameplateCull;

/// <summary>The service Patch104 reaches, handed over by the module's static initialisation.</summary>
internal static class NameplateCullCalls
{
    internal static INameplateCullService? Service { get; private set; }

    internal static void Initialize(INameplateCullService? service) => Service = service;
}
