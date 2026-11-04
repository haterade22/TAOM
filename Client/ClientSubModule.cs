using System;
using Bannerlord.UIExtenderEx;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Client.Features.LongDefeat;
using TAOM.Client.Features.SeatOfSeeing.Trial;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.FordsOfWar.Autorun;
using TAOM.Features.FordsOfWar.Diagnostics;
using TAOM.Features.LongDefeat;
using TAOM.Features.LongDefeat.Autorun;

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
    private FordsAutorunDriver? _autorun;
    private LongDefeatAutorunDriver? _longDefeatAutorun;
    private CommandTrialAutostart? _commandTrial;
    private bool _autorunResolved;
    private bool _autorunFaulted;

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

    // The Fords of War autorun (docs/features/fords-of-war.md), the Long Defeat autorun and the Seat's field trial
    // (docs/features/seat-of-seeing.md): unarmed, which is every player session, this is one bool and a null check per driver per tick.
    // The plan is resolved on the first tick (IoC.Configure ran in OnSubModuleLoad) and the driver only when it is armed, so an unarmed
    // session builds no autorun driver (the Long Defeat catalog itself is built on the first menu draw, by LongDefeatMenuEntry). A fault is logged once.
    protected override void OnApplicationTick(float dt)
    {
        base.OnApplicationTick(dt);
        try
        {
            if (!_autorunResolved)
            {
                _autorunResolved = true;
                _autorun = FordsAutorunStart.Driver(IoC.Resolve<FordsAutorunPlan>(), IoC.Resolve<FordsAutorunDriver>, IoC.Resolve<IModLogger>());
                _longDefeatAutorun = LongDefeatAutorunStart.Driver(IoC.Resolve<LongDefeatAutorunPlan>(), IoC.Resolve<LongDefeatAutorunDriver>, IoC.Resolve<IModLogger>());
                _commandTrial = CommandTrialAutostart.Armed(IoC.Resolve<IModLogger>());
            }

            _autorun?.Tick(dt);
            _longDefeatAutorun?.Tick(dt);
            _commandTrial?.Tick(dt);
        }
        catch (Exception ex)
        {
            if (_autorunFaulted)
                return;

            _autorunFaulted = true;
            try
            {
                IoC.Resolve<IModLogger>().LogError($"[Autorun] an autorun tick failed (Fords of War or Long Defeat) (further failures are not logged): {ex}");
            }
            catch
            {
                // swallow-ok: the logger itself failed; this runs in the engine's tick, which must not throw.
            }
        }
    }
}
