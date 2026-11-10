using System.Collections.Generic;

namespace TAOM.Features.Realms;

/// <summary>What the coronation's event window shows. The banner travels as its code so no engine type crosses the seam.</summary>
public sealed class CoronationPopup
{
    public CoronationPopup(string heading, string text, string? image, string button, string? bannerCode, IReadOnlyList<string> lines)
    {
        Heading = heading;
        Text = text;
        Image = image;
        Button = button;
        BannerCode = bannerCode;
        Lines = lines;
    }

    public string Heading { get; }
    public string Text { get; }

    /// <summary>The illustration's sprite name, or null to show the new house's banner instead.</summary>
    public string? Image { get; }

    public string Button { get; }
    public string? BannerCode { get; }
    public IReadOnlyList<string> Lines { get; }
}

/// <summary>
/// Opens the Realms windows that live in the client (ADR-013). Core registers <see cref="NullRealmsPopupAdapter"/> and
/// TAOM.Client's composition replaces it, so a dedicated server crowns Aragorn without a window.
/// </summary>
public interface IRealmsPopupAdapter
{
    /// <summary>Queues the coronation's event window; it opens on the next frame the map can show it.</summary>
    void ShowCoronation(CoronationPopup popup);
}

/// <summary>There is no window to open (a dedicated server, or a client where TAOM.Client did not load).</summary>
public sealed class NullRealmsPopupAdapter : IRealmsPopupAdapter
{
    public void ShowCoronation(CoronationPopup popup)
    {
    }
}
