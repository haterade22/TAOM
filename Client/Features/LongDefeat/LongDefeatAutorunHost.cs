using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;

namespace TAOM.Client.Features.LongDefeat;

/// <summary>
/// The engine half of the Long Defeat autorun (<see cref="LongDefeatAutorunDriver"/>). It lives in TAOM.Client because the exit goes
/// through <see cref="LongDefeatGameManager"/>, and a dedicated server never loads that assembly, so no server can be armed. Only the
/// client's application tick calls it, so every call below runs on the main thread.
/// </summary>
internal sealed class LongDefeatAutorunHost : ILongDefeatAutorunHost
{
    private readonly LongDefeatSetupPrompt _prompt;
    private readonly string _outFolder;
    private readonly IModLogger _logger;

    public LongDefeatAutorunHost(LongDefeatAutorunPlan plan, LongDefeatSetupPrompt prompt, IModLogger logger)
    {
        _prompt = prompt;
        _outFolder = plan.OutFolder;
        _logger = logger;
    }

    public bool AtMainMenu => GameStateManager.Current?.ActiveState is InitialState && Game.Current == null;

    public bool HideAnyInquiry()
    {
        if (!InformationManager.IsAnyInquiryActive())
            return false;

        InformationManager.HideInquiry();
        return true;
    }

    public bool Start(LongDefeatSetup setup) => _prompt.Start(setup);

    public void ReturnToMainMenu() => LongDefeatGameManager.ReturnToMainMenu(_logger);

    public void WriteFile(string name, string text)
    {
        LongDefeatFiles.WriteAtomic(Path.Combine(_outFolder, name), text);
    }

    public void TakeScreenshot(string name)
    {
        Directory.CreateDirectory(_outFolder);
        TaleWorlds.Engine.Utilities.TakeScreenshot(Path.Combine(_outFolder, name));
    }

    public void Quit() => TaleWorlds.Engine.Utilities.QuitGame();
}
