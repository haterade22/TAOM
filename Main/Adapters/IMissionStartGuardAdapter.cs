// Adapted from yotthani's VanillaTuning (MIT, (c) 2026 yotthani), mission-start-guard.
namespace TAOM.Adapters;

/// <summary>The one engine fact the mission-start guard needs: the on-screen message.</summary>
public interface IMissionStartGuardAdapter
{
    /// <summary>
    /// One localized line in the message log: the battle goes on without <paramref name="call"/> of
    /// <paramref name="module"/>, which threw <paramref name="exceptionType"/>.
    /// </summary>
    void ShowNotice(string module, string call, string exceptionType);
}
