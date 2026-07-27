namespace TAOM.Adapters;

/// <summary>
/// Asks the live GauntletUI brush whether a named text style actually exists.
///
/// Exists because <c>Brush.GetStyleOrDefault</c> falls back to the brush's <c>Default</c> style
/// with no error, so emitting a style name the brush does not define renders the text in the
/// default colour — black body text on TAOM's parchment — with nothing in the log. The brush file
/// can go missing at runtime for a reason no offline test can see: <c>ResourceDepot</c> keys GUI
/// files by module-relative path and the last module in load order replaces the whole file.
/// </summary>
public interface IMenuBrushStyleProbe
{
    /// <summary>
    /// True when the game-menu body brush defines <paramref name="styleName"/>. Returns false
    /// rather than throwing when the UI resources are not loaded yet.
    /// </summary>
    bool DefinesStyle(string styleName);
}
