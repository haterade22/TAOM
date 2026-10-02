using System.IO;
using TAOM.Core.Infrastructure;

namespace TAOM.Features.FactionUI;

/// <summary>
/// Where Kysaro's front-end assets live inside TAOM's own module (#704). His module read TAOM's data
/// through a hard-coded <c>Modules/TAOM</c> path; merged, everything resolves from the module root.
/// </summary>
public sealed class FactionUIPaths
{
    public FactionUIPaths(IPathService pathService)
    {
        Root = pathService.ModuleRootPath;
        ConfigDirectory = Path.Combine(pathService.ModuleDataPath, "FactionUI");
    }

    public string Root { get; }

    public string ConfigDirectory { get; }

    public string RuntimeSprites => Path.Combine(Root, "GUI", "FactionUI", "RuntimeSprites");

    public string RuntimeFonts => Path.Combine(Root, "GUI", "FactionUI", "RuntimeFonts");

    public string LoadingScreens => Path.Combine(Root, "GUI", "FactionUI", "LoadingScreens");

    /// <summary>One sub-folder per menu video. Deliberately not <c>Videos/initial_menu</c>, which vanilla
    /// picks its own menu videos from (<c>MBInitialScreenBase.RefreshScene</c>, v1.5.3).</summary>
    public string MenuVideos => Path.Combine(Root, "Videos", "FactionUI", "menu");

    public string SplashVideo => Path.Combine(Root, "Videos", "FactionUI", "splash", "splash_pc.ivf");

    public string SplashAudio => Path.Combine(Root, "Videos", "FactionUI", "splash", "splash.ogg");
}
