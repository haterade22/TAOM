namespace TAOM.Features.StagedSiege.UI;

/// <summary>
/// Hands the run report what the HUD showed (#1054): the <c>letter:kind</c> pair of each refresh and the point letter its marker sat
/// over. It remembers the last of each, so a refresh four times a second and a marker placed every frame reach the slot's lock only
/// when something changed. One per view model, main thread only; plain values, so it needs no engine.
/// </summary>
internal sealed class StagedSiegeHudRecorder
{
    private object? _token;
    private string? _lastPair;
    private string? _lastMarker;

    public void Shown(object token, string letter, HudObjectiveKind kind)
    {
        _token = token;
        var pair = letter + ":" + kind;
        if (pair == _lastPair)
            return;
        _lastPair = pair;
        StagedSiegeSlot.RecordHud(token, pair);
    }

    /// <summary>The marker is on screen over <paramref name="letter"/>. Ignored before the first <see cref="Shown"/>, which names the mission.</summary>
    public void Marker(bool visible, string letter)
    {
        if (!visible || _token == null || letter.Length == 0 || letter == _lastMarker)
            return;
        _lastMarker = letter;
        StagedSiegeSlot.RecordHudMarker(_token, letter);
    }

    /// <summary>The line the view logs when the strip or the objective changes (without the tag).</summary>
    public static string Describe(string strip, string objective) => $"HUD: strip \"{strip}\", objective \"{objective}\"";
}
