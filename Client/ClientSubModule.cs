using Bannerlord.UIExtenderEx;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Diagnostics;

namespace TAOM.Client;

/// <summary>
/// The TAOM module's client-only submodule (ADR-013). Its manifest tags (DedicatedServerType none,
/// IsNoRenderModeElement false) keep a dedicated server from loading it. The engine runs this
/// constructor before any submodule's OnSubModuleLoad, so it only hands core the client registrations
/// and does nothing that needs the game. Its first OnGameStart registers TAOM's UIExtenderEx extensions,
/// all of which live in this assembly; the engine runs it after core's (TAOM is listed first), so
/// IoC.Configure has run.
/// </summary>
public sealed class ClientSubModule : MBSubModuleBase
{
    // UIExtenderEx keys its instances and its Harmony ids by this name ("bannerlord.uiextender.ex.viewmodels.TAOM"),
    // which HarmonyCensusReportBuilder and PatchShieldPolicy match, so it stays "TAOM" in the client.
    private const string UiExtenderModuleName = "TAOM";

    private UIExtender? _uiExtender;

    public ClientSubModule()
    {
        ClientComposition.Current = new TaomClientComposition();
    }

    // Not OnSubModuleLoad: Register compiles every mixin target, and the .NET Framework JIT may then run
    // CampaignUIHelper's static initializer, which reads GameTexts. Every game type calls OnGameStart from inside
    // Game.Initialize, after GameTexts.Initialize (Game.cs:300-301) but before the models and default objects
    // exist. Before that, the initializer throws and .NET keeps CampaignUIHelper broken for the whole process.
    // Every prefab target is a campaign or mission movie, and no mixin target exists before a game, so nothing
    // is missed. Once only: UIExtenderEx refuses a second Register.
    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        base.OnGameStart(game, gameStarterObject);
        if (_uiExtender != null)
            return;

        _uiExtender = UIExtender.Create(UiExtenderModuleName);
        ClientUiExtensions.Register(_uiExtender, typeof(ClientSubModule).Assembly);
        _uiExtender.Enable();
    }

    // Module.cs 536-549 and 790-798: every return to the main menu (a game ends and the global state stack is empty) runs this on every
    // submodule, and the first boot runs it too, where MainMenuReturned finds no closed run and does nothing. Fords of War writes its
    // final verdict pass here. MainMenuStep keeps a fault from throwing into the engine's tick. IoC.Configure ran in OnSubModuleLoad.
    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        base.OnBeforeInitialModuleScreenSetAsRoot();
        MainMenuStep.Run("fords of war final report", () => IoC.Resolve<FordsRunReport>().MainMenuReturned(), () => IoC.Resolve<IModLogger>());
    }
}
