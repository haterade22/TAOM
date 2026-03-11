namespace TAOM.Features.FactionMap.Hooks;

public class CultureStageViewFinalizeHook : IOnCultureStageViewFinalize
{
    public void OnFinalize()
    {
        CultureStageViewCreatedHook.CurrentVM?.OnFinalize();
        CultureStageViewCreatedHook.Cleanup();
    }
}
