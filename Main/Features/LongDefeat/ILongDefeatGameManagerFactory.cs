namespace TAOM.Features.LongDefeat;

/// <summary>
/// Builds the game manager that loads The Long Defeat. The manager derives from a CustomBattle type, so the real factory lives in
/// TAOM.Client (ADR-013); core registers <see cref="NullLongDefeatGameManagerFactory"/> and the client composition replaces it.
/// </summary>
internal interface ILongDefeatGameManagerFactory
{
    /// <summary>
    /// Builds the manager for <paramref name="setup"/> and starts loading it. False when no client is present. It starts the game
    /// itself because the menu adapter's signature may name no engine class (ADR-007, <c>ServiceSignatureGateTests</c>).
    /// </summary>
    bool Start(LongDefeatSetup setup);
}
