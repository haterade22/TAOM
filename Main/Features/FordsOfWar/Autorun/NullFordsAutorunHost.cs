namespace TAOM.Features.FordsOfWar.Autorun;

/// <summary>
/// Core's default autorun host (ADR-013): there is no menu, no game and nothing to quit. TAOM.Client replaces it on a rendering
/// client; a dedicated server never builds the driver, so none of this is ever called there.
/// </summary>
internal sealed class NullFordsAutorunHost : IFordsAutorunHost
{
    public bool AtMainMenu => false;

    public bool HideAnyInquiry() => false;

    public bool Start(FordsMatchSetup setup) => false;

    public void ReturnToMainMenu()
    {
    }

    public void WriteFile(string name, string text)
    {
    }

    public void TakeScreenshot(string name)
    {
    }

    public void Quit()
    {
    }
}
