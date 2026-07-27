using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TAOM.Adapters;
using TAOM.Core.Logging;

namespace TAOM.Features.MenuLinkColors;

/// <summary>
/// Swaps the engine's generic hyperlink style names for TAOM's per-culture ones.
///
/// <c>TaleWorlds.Core.HyperlinkTexts</c> hardcodes the style name it emits, so a settlement link is
/// <c>Link.Settlement</c> whether the town is Gondorian or Mordorian. The rich-text parser accepts
/// only a NAMED style — there is no inline colour attribute — so per-faction colour has to be done
/// by rewriting the name to one the brush XML defines.
///
/// This runs at the game-menu seam only, so the TAOM style names never reach the encyclopedia,
/// quest log or tooltips, whose brushes do not define them.
/// </summary>
public sealed class MenuLinkStyleRewriter : IMenuLinkStyleRewriter
{
    /// <summary>Cheap ordinal probe so link-free menu text skips the regex entirely.</summary>
    private const string LinkTagMarker = "<a style=\"Link";

    /// <summary>
    /// Matches the opening anchor tag exactly as <c>HyperlinkTexts</c> writes it, after the
    /// <c>TextObject</c> has resolved its variables. Anything that does not match — a truncated
    /// tag, a missing href, a non-Link style — is left alone.
    ///
    /// Only the four link types that HAVE an owning faction are matched. <c>Link.Concept</c>,
    /// <c>Link.Unit</c>, <c>Link.Ship</c> and the bare <c>Link</c> have no culture to colour by, and
    /// excluding them also makes the rewrite idempotent by construction: a <c>Link.Taom.*</c> style
    /// can never match, so re-running over already-rewritten text is a no-op.
    /// </summary>
    private static readonly Regex LinkTagRegex = new Regex(
        "<a style=\"Link\\.(?:Settlement|Hero|Kingdom|Clan)\" href=\"event:(?<link>[^\"]*)\">",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IEncyclopediaCultureLookup _lookup;
    private readonly IMenuBrushStyleProbe _brush;
    private readonly IModLogger _logger;

    /// <summary>
    /// Cached so <see cref="Regex.Replace(string, MatchEvaluator)"/> does not allocate a delegate
    /// per call. Bound once — the evaluator is stateless beyond the injected dependencies.
    /// </summary>
    private readonly MatchEvaluator _replaceStyle;

    private bool _warned;
    private bool _warnedMissingStyle;

    /// <summary>
    /// One-shot diagnostic. The feature otherwise logs only on failure, so "nothing in the log"
    /// is ambiguous between "the patch never fired" and "it fired and everything worked". This
    /// records what the first menu-open actually saw and decided, then goes quiet.
    /// </summary>
    private bool _tracedFirstPass;
    private List<string> _trace;

    public MenuLinkStyleRewriter(IEncyclopediaCultureLookup lookup, IMenuBrushStyleProbe brush, IModLogger logger)
    {
        _lookup = lookup;
        _brush = brush;
        _logger = logger;
        _replaceStyle = ReplaceStyle;
    }

    public string? Rewrite(string? menuText)
    {
        if (string.IsNullOrEmpty(menuText)) return menuText;
        if (menuText!.IndexOf(LinkTagMarker, StringComparison.Ordinal) < 0) return menuText;

        // Deliberately NOT cached. An earlier revision memoised the last (input, output) pair, but
        // the memo key is the menu STRING while the answer depends on the linked objects' CULTURE
        // — and the menu text is identical before and after a culture conversion or a load of a
        // different save. That returned a stale colour with nothing to indicate it. The setter runs
        // once per menu open, so re-resolving costs one regex pass and a handful of object lookups.
        if (!_tracedFirstPass) _trace = new List<string>();

        try
        {
            var result = LinkTagRegex.Replace(menuText, _replaceStyle);
            TraceFirstPassOnce();
            return result;
        }
        catch (Exception ex)
        {
            // Never let a retint break the menu — the player would lose the text entirely.
            WarnOnce(ex);
            return menuText;
        }
    }

    private string ReplaceStyle(Match match)
    {
        var link = match.Groups["link"].Value;
        var cultureId = _lookup.GetCultureId(link);

        // Unresolvable object, or a culture with no authored style (bandits, other mods'
        // cultures) — keep vanilla's style so the link stays coloured rather than falling back
        // to the black Default style.
        if (!TaomCultureLinkStyles.IsSupported(cultureId))
        {
            _trace?.Add($"{link} -> culture={cultureId ?? "<unresolved>"} -> KEPT VANILLA");
            return match.Value;
        }

        var styleName = TaomCultureLinkStyles.StyleNameFor(cultureId!);

        // The brush that ships in this repo may not be the brush the game loaded — a module later
        // in load order replaces GUI/Brushes/GameMenu.xml wholesale. Emitting a style it lacks
        // would silently blacken the link, so check first and keep vanilla's style if it's absent.
        if (!_brush.DefinesStyle(styleName))
        {
            _trace?.Add($"{link} -> culture={cultureId} -> STYLE '{styleName}' NOT IN BRUSH");
            WarnMissingStyleOnce(styleName);
            return match.Value;
        }

        _trace?.Add($"{link} -> culture={cultureId} -> {styleName}");
        return "<a style=\"" + styleName + "\" href=\"event:" + link + "\">";
    }

    /// <summary>
    /// Emits what the first menu-open actually saw, then goes quiet for the rest of the process.
    /// Its absence from the log is itself the answer: it means the prefix never ran.
    /// </summary>
    private void TraceFirstPassOnce()
    {
        if (_tracedFirstPass) return;
        _tracedFirstPass = true;

        var lines = _trace;
        _trace = null;
        _logger.LogInfo(lines == null || lines.Count == 0
            ? "[MenuLinkColors] First menu text processed, but it held no Settlement/Hero/Kingdom/Clan link tags."
            : $"[MenuLinkColors] First menu text: {lines.Count} link(s) — " + string.Join(" | ", lines));
    }

    /// <summary>
    /// Logs the first failure only. The setter fires on every menu refresh, so an unlogged-but-
    /// repeating fault would flood the log, and a silently-swallowed one would be invisible.
    /// </summary>
    private void WarnOnce(Exception ex)
    {
        if (_warned) return;
        _warned = true;
        _logger.LogWarning($"[MenuLinkColors] Link retint failed; menu text left unchanged: {ex.Message}");
    }

    /// <summary>
    /// The one diagnostic that distinguishes "TAOM's brush edit never reached the game" from
    /// "the feature is off". Without it a load-order clobber is invisible.
    /// </summary>
    private void WarnMissingStyleOnce(string styleName)
    {
        if (_warnedMissingStyle) return;
        _warnedMissingStyle = true;
        _logger.LogWarning(
            $"[MenuLinkColors] Brush 'GameMenu.InfoText' does not define '{styleName}'. Menu links keep " +
            "vanilla colours. Another module later in load order may be replacing TAOM's " +
            "GUI/Brushes/GameMenu.xml wholesale.");
    }
}
