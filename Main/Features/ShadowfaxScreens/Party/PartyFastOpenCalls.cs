namespace TAOM.Features.ShadowfaxScreens.Party;

/// <summary>The service the Party Fast Open patches reach, handed over by the module's static initialisation.</summary>
internal static class PartyFastOpenCalls
{
    /// <summary>The Harmony category of the Party hooks.</summary>
    internal const string PatchCategory = "Patch129_ShadowfaxParty";

    internal static PartyScreenFastPathService? Service { get; private set; }

    internal static void Initialize(PartyScreenFastPathService? service) => Service = service;
}
