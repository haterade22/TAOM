using System.IO;
using TAOM.Features.FactionUI.Campaign;

namespace TAOM.Client.Features.EventWindow;

/// <summary>
/// The story event illustrations: loose 720x960 PNGs in <c>GUI/EventArt</c>, loaded at runtime by
/// <see cref="EncyclopediaPictureCache"/> (no sprite sheet). An event names its picture by file name
/// (<c>"image": "taom_event_khazaddum.png"</c>).
/// </summary>
internal static class EventArt
{
    /// <summary>The module's event art folder.</summary>
    public static string Directory => Path.Combine(CampaignPresentation.ModuleRoot, "GUI", "EventArt");

    /// <summary>
    /// The full path of the event's picture, or null when it names none, names anything but a bare file name in
    /// <paramref name="directory"/>, or the file is missing: the window then shows the banner, or the story alone.
    /// </summary>
    public static string? Resolve(string? image, string directory)
    {
        // A path separator is an invalid file name character, so the picture never leaves the folder; "." and ".."
        // name directories, which File.Exists rejects.
        if (string.IsNullOrWhiteSpace(image) || image!.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;
        var path = Path.Combine(directory, image);
        return File.Exists(path) ? path : null;
    }
}
