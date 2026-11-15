using TaleWorlds.CampaignSystem;

namespace TAOM.Features.CheatMenu;

/// <summary>
/// The "Cheats" button for one map navigation handler (ADR-013: the element type is client-only). Core registers
/// <see cref="NullCheatMenuNavigationElementFactory"/>; TAOM.Client replaces it. The handler is passed as an object: its
/// type lives in a client-only assembly, which a core signature must not name (ServerJitGateTests).
/// </summary>
public interface ICheatMenuNavigationElementFactory
{
    INavigationElement? Create(object handler);
}

/// <summary>No button (a dedicated server, or a client where TAOM.Client did not load).</summary>
public sealed class NullCheatMenuNavigationElementFactory : ICheatMenuNavigationElementFactory
{
    public INavigationElement? Create(object handler) => null;
}
