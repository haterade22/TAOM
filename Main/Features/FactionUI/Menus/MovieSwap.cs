using TAOM.Features.FactionUI.Resources;

namespace TAOM.Features.FactionUI.Menus;

/// <summary>One vanilla movie replaced by a themed one, carried from the load prefix to its postfix and
/// finalizer.</summary>
public sealed class MovieSwap
{
    public MovieSwap(string originalName, string targetName, FrontEndImageGroups holds)
    {
        OriginalName = originalName;
        TargetName = targetName;
        Holds = holds;
    }

    public string OriginalName { get; }

    public string TargetName { get; }

    /// <summary>The image groups the themed movie draws, held for as long as the movie lives.</summary>
    public FrontEndImageGroups Holds { get; }

    /// <summary>False for a movie that is TAOM's own (the faction screen): if its build fails there is no
    /// vanilla movie to load instead, so the failure goes back to the code that asked for it.</summary>
    public bool HasVanillaFallback => OriginalName != TargetName;
}
