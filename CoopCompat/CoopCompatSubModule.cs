using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.CoopCompat.CareerSync;
using TAOM.CoopCompat.Decisions;
using TAOM.CoopCompat.GuestTrade;
using TAOM.CoopCompat.Handshake;
using TAOM.CoopCompat.LoadOrder;
using TAOM.CoopCompat.Momentum;
using TAOM.CoopCompat.Sessions;
using TAOM.CoopCompat.Transport;
using TAOM.Features.CoopInterop;
using TAOM.Features.CoopInterop.CareerSync;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.CoopInterop.GuestTrade;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.CoopCompat;

/// <summary>
/// Entry point of the TAOM.CoopCompat module. The engine scans every type of this DLL before any
/// OnSubModuleLoad runs, when no Coop assembly is loaded yet, so every type that needs Coop to load
/// lives in TAOM.CoopCompat.Coop.dll (TypeScanGateTests). The transport, the services, the session
/// transport and its link live in fields because Coop holds its subscriptions weakly.
/// </summary>
public sealed class CoopCompatSubModule : MBSubModuleBase
{
    private ICoopTransport? _transport;
    private SettingsHandshakeService? _handshake;
    private MomentumRelayService? _momentum;
    private GuestTradeRelayService? _guestTrade;
    private CareerSyncRelayService? _careerSync;
    private bool _startTried;
    private ICoopSessionTransport? _sessions;
    private LongDefeatSessionLink? _sessionLink;
    private bool _sessionsTried;

    protected override void OnSubModuleLoad() => Log("[CoopCompat] module loaded");

    // Career sync sends a client's waiting career report once per frame, so one career action goes out as one report.
    protected override void OnApplicationTick(float dt) => _careerSync?.Tick();

    // Long Defeat co-op (#1280) starts from the main menu, before any campaign, so the session launcher starts here,
    // after every module's OnSubModuleLoad.
    protected override void OnBeforeInitialModuleScreenSetAsRoot()
    {
        if (_sessionsTried) return;
        _sessionsTried = true;
        DecisionTypeRegistration.TryRegister(Log);
        try
        {
            StartSessions();
            var port = IoC.Resolve<ILongDefeatCoopPort>();
            _sessionLink = new LongDefeatSessionLink(_sessions!, port);
            port.Attach(_sessionLink);
            Log("[CoopCompat] session launcher subscribed");
        }
        catch (Exception ex)
        {
            _sessions = null;
            _sessionLink = null;
            Log("[CoopCompat] sessions off, Coop is not usable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        if (game.GameType is not Campaign || gameStarterObject is not CampaignGameStarter starter) return;

        StartOnce();
        if (_momentum != null)
            starter.AddBehavior(new MomentumRelayCampaignBehavior(_momentum));
    }

    // At the first campaign start, when every module's OnSubModuleLoad has run, so Coop's assemblies
    // resolve whatever the module order. On the dedicated server, DedicatedServer.Windows installs that
    // resolver in its own OnSubModuleLoad. The server loads its campaign before it accepts players.
    private void StartOnce()
    {
        if (_startTried) return;
        _startTried = true;
        DecisionTypeRegistration.TryRegister(Log);
        try
        {
            StartTransport();
            BuildServices(_transport!);
            Log("[CoopCompat] transport subscribed");
        }
        catch (Exception ex)
        {
            // A Coop assembly missing or changed: off for this process, one line, the campaign goes on.
            _transport = null;
            _handshake = null;
            _momentum = null;
            _guestTrade = null;
            _careerSync = null;
            Log("[CoopCompat] off, Coop is not usable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // This and StartSessions are the only methods that name a Coop-backed type; NoInlining keeps the JIT from resolving it at the call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StartTransport()
    {
        var transport = new CoopTransport();
        transport.Start();
        _transport = transport;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StartSessions()
    {
        var sessions = new CoopSessionTransport();
        sessions.Start();
        _sessions = sessions;
    }

    private void BuildServices(ICoopTransport transport)
    {
        var logger = IoC.Resolve<IModLogger>();
        var outcomes = IoC.Resolve<IOutcomeLog>();
        var coopSettings = new CoopSettingsProvider();
        var loadOrder = new LoadOrderCheckService(new ActiveModulesAdapter(), () => coopSettings.LoadOrderWarning, logger);
        _handshake = new SettingsHandshakeService(transport, () => SettingsFingerprint.ComputeAcross(TaomSettingsPages.All()),
            BuildIdentity.FromLoadedAssemblies(), loadOrder, new InformationManagerSettingsNotice(), logger, outcomes);
        _momentum = new MomentumRelayService(transport, IoC.Resolve<IMomentumStateStore>(), logger, outcomes,
            () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, () => CampaignTime.Now.ToHours);
        _guestTrade = new GuestTradeRelayService(transport, IoC.Resolve<GuestTradeClient>(), IoC.Resolve<GuestTradeHost>(), logger);
        _careerSync = new CareerSyncRelayService(transport, IoC.Resolve<CareerSyncClient>(), IoC.Resolve<CareerSyncHost>(), logger);
    }

    private static void Log(string message)
    {
        try
        {
            IoC.Resolve<IModLogger>().LogInfo(message);
        }
        catch (Exception)
        {
            // Logging must never stop the module list from loading.
        }
    }
}
