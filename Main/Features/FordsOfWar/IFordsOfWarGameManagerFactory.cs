using TaleWorlds.MountAndBlade;

namespace TAOM.Features.FordsOfWar;

/// <summary>
/// Builds the game manager that loads a Fords of War match. The manager derives from a CustomBattle type, so the real factory
/// lives in TAOM.Client (ADR-013), like <c>IShaderPrecompileGameManagerFactory</c>; it returns <see cref="MBGameManager"/>
/// because the CustomBattle assembly is absent on a headless server. Core registers <see cref="NullFordsOfWarGameManagerFactory"/>
/// and the client composition replaces it. Null means there is no client implementation: the prompt tells the player.
/// </summary>
internal interface IFordsOfWarGameManagerFactory
{
    MBGameManager? Create(FordsMatchSetup setup);
}
