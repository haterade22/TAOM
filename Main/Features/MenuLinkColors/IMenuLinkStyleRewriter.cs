namespace TAOM.Features.MenuLinkColors;

/// <summary>
/// Retints the hyperlinks in a game-menu body string by swapping the engine's generic
/// <c>Link.Settlement</c> / <c>Link.Hero</c> / <c>Link.Kingdom</c> / <c>Link.Clan</c> style names
/// for TAOM's per-culture ones.
/// </summary>
public interface IMenuLinkStyleRewriter
{
    /// <summary>
    /// Returns <paramref name="menuText"/> with every resolvable link's style name replaced by the
    /// culture-specific style. Input is returned unchanged when it holds no links, when an object
    /// cannot be resolved, or when its culture has no authored style.
    /// </summary>
    string? Rewrite(string? menuText);
}
