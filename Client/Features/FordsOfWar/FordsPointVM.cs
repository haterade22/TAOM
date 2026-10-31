using System;
using TaleWorlds.Library;
using TAOM.Features.FordsOfWar.Domain;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// One capture point as a read-only row: who holds it from the commander's side (yours, enemy, neutral; the swatch takes the
/// same blue and red as the tower bars), and while a capture runs who is taking it (or whose progress is slipping back) and how far it
/// has got, with a mark when both sides stand on it. Points pay income to their owner, so the panel shows them; it gives no command on them.
/// </summary>
public sealed class FordsPointVM : FordsViewModel
{
    private const string NeutralColor = "#8C8C8CFF";

    private string _title = string.Empty;
    private string _statusText = string.Empty;
    private string _contestedText = string.Empty;
    private string _ownerColor = NeutralColor;

    [DataSourceProperty]
    public string Title
    {
        get => _title;
        private set => Set(ref _title, value, nameof(Title));
    }

    [DataSourceProperty]
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value, nameof(StatusText));
    }

    [DataSourceProperty]
    public string ContestedText
    {
        get => _contestedText;
        private set => Set(ref _contestedText, value, nameof(ContestedText));
    }

    [DataSourceProperty]
    public string OwnerColor
    {
        get => _ownerColor;
        private set => Set(ref _ownerColor, value, nameof(OwnerColor));
    }

    internal void Update(int number, MatchView.CapturePointView point, MatchSide own)
    {
        Title = FordsHudText.PointOwner(number, point.Owner, own);
        StatusText = point.Capturer is { } capturer ? CaptureLine(capturer == own, point) : string.Empty;
        ContestedText = point.Contested ? FordsHudText.PointContested() : string.Empty;
        OwnerColor = point.Owner == null ? NeutralColor : point.Owner == own ? FordsTowerBarVM.OwnColor : FordsTowerBarVM.EnemyColor;
    }

    /// <summary>"Taking it" while the progress rises or is held by a contest; "slipping back" while it decays with no one pressing it.</summary>
    private static string CaptureLine(bool byYou, MatchView.CapturePointView point)
    {
        var percent = Math.Max(0, Math.Min(100, (int)Math.Round(point.Progress * 100f)));
        return point.Rising || point.Contested ? FordsHudText.PointTaking(byYou, percent) : FordsHudText.PointSlipping(byYou, percent);
    }
}
