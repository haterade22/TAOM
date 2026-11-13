using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TAOM.Core.Logging;
using TAOM.CoopCompat.Handshake;
using TAOM.CoopCompat.Momentum;
using TAOM.CoopCompat.Transport;
using TAOM.Features.CoopInterop;
using TAOM.Features.WarOfTheRingMomentum;

namespace TAOM.CoopCompat;

/// <summary>
/// Entry point of the TAOM.CoopCompat module. The engine scans every type of this DLL before any
/// OnSubModuleLoad runs, when no Coop assembly is loaded yet, so every type that needs Coop to load
/// lives in TAOM.CoopCompat.Coop.dll (TypeScanGateTests). The transport and both services live in
/// fields because Coop holds its subscriptions weakly.
/// </summary>
public sealed class CoopCompatSubModule : MBSubModuleBase
{
    private ICoopTransport? _transport;
    private SettingsHandshakeService? _handshake;
    private MomentumRelayService? _momentum;
    private bool _startTried;

    protected override void OnSubModuleLoad() => Log("[CoopCompat] module loaded");

    protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
    {
        if (game.GameType is not Campaign || gameStarterObject is not CampaignGameStarter starter) return;

        StartOnce();
        if (_momentum != null)
            starter.AddBehavior(new MomentumRelayCampaignBehavior(_momentum));
    }

    // At the first campaign start, when every module's OnSubModuleLoad has run, so Coop's assemblies
    // resolve whatever the module order. On the dedicated server, DedicatedServer.Windows installs that
    // resolver in its own OnSubModuleLoad.
    private void StartOnce()
    {
        if (_startTried) return;
        _startTried = true;
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
            Log("[CoopCompat] off, Coop is not usable: " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    // The only method that names a Coop-backed type; NoInlining keeps the JIT from resolving it at the call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private void StartTransport()
    {
        var transport = new CoopTransport();
        transport.Start();
        _transport = transport;
    }

    private void BuildServices(ICoopTransport transport)
    {
        var logger = IoC.Resolve<IModLogger>();
        var outcomes = IoC.Resolve<IOutcomeLog>();
        _handshake = new SettingsHandshakeService(transport, () => SettingsFingerprint.ComputeAcross(TaomSettingsPages.All()),
            BuildIdentity.FromLoadedAssemblies(), new InformationManagerSettingsNotice(), logger, outcomes);
        _momentum = new MomentumRelayService(transport, IoC.Resolve<IMomentumStateStore>(), logger, outcomes,
            () => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency, () => CampaignTime.Now.ToHours);
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
