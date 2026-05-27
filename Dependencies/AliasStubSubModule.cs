using System;
using System.Threading;
using TaleWorlds.MountAndBlade;

namespace TAOM.Dependencies;

/// <summary>
/// Loaded by each of the four BUTR alias stub SubModule.xml files
/// (Bannerlord.Harmony / .UIExtenderEx / .ButterLib / .MBOptionScreen).
/// Performs idempotent installation of the AssemblyResolve handler via an
/// Interlocked.Exchange gate. All work is try/catch-wrapped so a LauncherEx-time
/// exception is logged but never escapes — BetaDeps's v0.7.2 → v0.7.5 evolution
/// showed that uncaught exceptions in stub-module ctors break BLSE's
/// drag-to-reorder operations on every other mod in the launcher.
///
/// BetaDeps parity (DR3 Phase 4 — 2026-05-25). Ports the design of
/// BetaDeps.Foundation.AliasStubSubModule. Future phases will append additional
/// shield installs (IncompatibleModDetector.RunEarlyPhase,
/// CollectAssemblyTypesShim.Install, SubModuleConstructionGuard.Install) to the
/// ctor as those classes are implemented in Dependencies/Foundation/.
/// </summary>
public class AliasStubSubModule : MBSubModuleBase
{
    private const string Tag = "AliasStub";

    private static int _earlyDetectionRan;

    public AliasStubSubModule()
    {
        // Single-instance guard across all 4 stubs — first stub to construct
        // installs the shields; subsequent stub ctors are no-ops on this path.
        if (Interlocked.Exchange(ref _earlyDetectionRan, 1) != 0)
            return;

        TrySwallow(SubModule.InstallAssemblyResolveHandler, "ctor/AssemblyResolve");
        // Phase C will append here:
        //   TrySwallow(IncompatibleModDetector.RunEarlyPhase, "ctor/IncompatEarly");
        //   TrySwallow(CollectAssemblyTypesShim.Install, "ctor/CollectAssemblyTypesShim");
        //   TrySwallow(SubModuleConstructionGuard.Install, "ctor/SubModuleGuard");
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        // Defence-in-depth: re-install in OnSubModuleLoad in case the ctor path
        // raced or was skipped on the early-detection guard. InstallAssemblyResolveHandler
        // is itself idempotent (Interlocked.CompareExchange gate).
        TrySwallow(SubModule.InstallAssemblyResolveHandler, "OnSubModuleLoad/AssemblyResolve");
        TrySwallow(
            () => EarlyLog.Info($"[{Tag}] alias stub loaded: {GetType().Assembly.GetName().Name}"),
            "OnSubModuleLoad/Log");
    }

    private static void TrySwallow(Action action, string where)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            try
            {
                EarlyLog.Error($"[{Tag}] {where}: {ex.Message}");
            }
            catch
            {
                // Logger itself failed — swallow to prevent ctor escape (BetaDeps v0.7.5 rationale)
            }
        }
    }
}
