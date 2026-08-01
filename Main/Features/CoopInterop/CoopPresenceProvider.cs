using TAOM.Dependencies.Foundation;

namespace TAOM.Features.CoopInterop;

/// <inheritdoc />
public sealed class CoopPresenceProvider : ICoopPresenceProvider
{
    /// <inheritdoc />
    public bool IsCoopActive => CoopPresence.IsActive;
}
