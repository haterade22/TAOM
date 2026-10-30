namespace TAOM.Features.ShadowfaxScreens.Clan;

/// <summary>
/// The service the Clan fast open hooks reach, handed over by the module's static initialisation. The helper the
/// <c>RefreshValues</c> transpiler swaps in names <c>ClanPartiesVM</c>, a client-only type, so it lives in the [ClientOnly]
/// patch class <see cref="Hooks.ClanPartiesVM_RefreshValues_ClanFastOpen_Patch"/> (ADR-013).
/// </summary>
internal static class ClanFastOpenCalls
{
    /// <summary>The Harmony category of the Clan hooks.</summary>
    internal const string PatchCategory = "Patch127_ShadowfaxClan";

    /// <summary>How many sites the transpiler swapped on its last run; 0 until it ran, and 0 when it soft-failed.</summary>
    internal static int LastSwapped { get; set; }

    internal static ClanScreenFastPathService? Service { get; private set; }

    internal static void Initialize(ClanScreenFastPathService? service) => Service = service;
}
