using System.IO;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar;
using TAOM.Features.FordsOfWar.Autorun;

namespace TAOM.Client.Features.FordsOfWar;

/// <summary>
/// The engine half of the Fords of War autorun (<see cref="FordsAutorunDriver"/>). It lives in TAOM.Client because the exit goes
/// through <see cref="FordsOfWarGameManager"/>, and a dedicated server never loads that assembly, so no server can be armed. Only
/// the client's application tick calls it, so every call below runs on the main thread.
/// </summary>
internal sealed class FordsAutorunHost : IFordsAutorunHost
{
    private readonly FordsSetupPrompt _prompt;
    private readonly string _outFolder;
    private readonly IModLogger _logger;

    public FordsAutorunHost(FordsAutorunPlan plan, FordsSetupPrompt prompt, IModLogger logger)
    {
        _prompt = prompt;
        _outFolder = plan.OutFolder;
        _logger = logger;
    }

    // The initial menu is the active state while no game exists; during the load and the match Game.Current is set.
    public bool AtMainMenu => GameStateManager.Current?.ActiveState is InitialState && Game.Current == null;

    public bool HideAnyInquiry()
    {
        if (!InformationManager.IsAnyInquiryActive())
            return false;

        InformationManager.HideInquiry();
        return true;
    }

    public bool Start(FordsMatchSetup setup) => _prompt.Start(setup, "autorun");

    public void ReturnToMainMenu() => FordsOfWarGameManager.ReturnToMainMenu(_logger);

    public void WriteFile(string name, string text)
    {
        Directory.CreateDirectory(_outFolder);
        var path = Path.Combine(_outFolder, name);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, text);
        if (File.Exists(path))
            File.Replace(tmp, path, null);
        else
            File.Move(tmp, path);
    }

    public void Quit() => TaleWorlds.Engine.Utilities.QuitGame();
}
