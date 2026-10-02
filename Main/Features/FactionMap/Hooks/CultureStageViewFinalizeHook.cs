namespace TAOM.Features.FactionMap.Hooks;

public class CultureStageViewFinalizeHook : IOnCultureStageViewFinalize
{
    private readonly ICultureStageMovieOverride _movieOverride;

    public CultureStageViewFinalizeHook(ICultureStageMovieOverride movieOverride)
    {
        _movieOverride = movieOverride;
    }

    public void OnFinalize()
    {
        _movieOverride.OnCultureStageClosed();
        CultureStageViewCreatedHook.CurrentVM?.OnFinalize();
        CultureStageViewCreatedHook.Cleanup();
    }
}
