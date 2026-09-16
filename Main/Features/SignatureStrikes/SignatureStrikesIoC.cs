using DryIoc;
using TAOM.Features.SignatureStrikes.Hooks;

namespace TAOM.Features.SignatureStrikes;

public static class SignatureStrikesIoC
{
    public static void RegisterSignatureStrikesFeature(IContainer container)
    {
        container.Register<ISignatureStrikesConfigProvider, SignatureStrikesConfigProvider>(Reuse.Singleton);
        container.Register<ISignatureStrikesSettingsProvider, SignatureStrikesSettingsProvider>(Reuse.Singleton);
        container.Register<ISignatureStrikeRegistry, SignatureStrikeRegistry>(Reuse.Singleton);
        container.Register<ISignatureStrikeService, SignatureStrikeService>(Reuse.Singleton);
        // Singleton so TaomCombatMechanicsModel and the mission logic probe the same roster; the
        // logic clears it at mission start and end (its session-reset story).
        container.Register<ISignatureAgentRoster, SignatureAgentRoster>(Reuse.Singleton);
    }
}
