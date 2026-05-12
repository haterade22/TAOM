using TAOM.Core.Logging;

namespace TAOM.Adapters;

/// <summary>
/// Wraps the few <see cref="TaleWorlds.CampaignSystem.Campaign"/> static accessors that the
/// runtime cache rebuild needs. Exists primarily to give <see cref="TAOM.Features.EditorCacheRebuild.RuntimeCacheRebuildService"/>
/// a mockable seam so unit tests don't need to spin up a real campaign session.
/// </summary>
public interface ICampaignSessionAdapter
{
    /// <summary>
    /// True iff the singleplayer session is initialized enough for cache construction —
    /// <c>Campaign.Current != null</c> AND <c>Campaign.Current.MapSceneWrapper != null</c>.
    /// </summary>
    /// <param name="reason">
    /// When false, a short human-readable explanation (used for log lines / popups).
    /// When true, empty.
    /// </param>
    bool IsReadyForRebuild(out string reason);

    /// <summary>
    /// Constructs a runtime <c>NavigationCache&lt;Settlement&gt;</c> instance (via
    /// <c>SandBoxNavigationCache(NavigationType.Default)</c>) bound to the current campaign's
    /// <c>MapSceneWrapper</c>, wrapped in <see cref="INavigationCacheAdapter"/>.
    /// </summary>
    /// <remarks>
    /// Caller must have verified <see cref="IsReadyForRebuild"/> returned true on the same thread
    /// (or earlier). The factory does no defensive null-check — it would NRE on an inactive campaign.
    /// </remarks>
    INavigationCacheAdapter CreateDefaultRuntimeCacheAdapter(IModLogger? logger);

    /// <summary>
    /// Best-effort diagnostic snapshot of campaign-session state. Used only for log lines —
    /// individual fields can be empty/-1 if their backing collection is unavailable, never throws.
    /// </summary>
    CampaignSnapshot GetSnapshot();
}
