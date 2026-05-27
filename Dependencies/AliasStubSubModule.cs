using System;
using System.Threading;
using TaleWorlds.MountAndBlade;
using TAOM.Dependencies.Foundation;

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
        // Proof-of-life. Unconditional DiagLog write so we KNOW the ctor ran even if
        // the Interlocked guard returns early below or any TrySwallow target silently
        // fails. Caught 2026-05-27 — previous build's ctor was invisible in diag.log
        // and we couldn't tell if it ran at all.
        try { DiagLog.Log(Tag, "ctor entered"); } catch { /* nothing we can do */ }

        // Single-instance guard across all 4 stubs — first stub to construct
        // installs the shields; subsequent stub ctors are no-ops on this path.
        if (Interlocked.Exchange(ref _earlyDetectionRan, 1) != 0)
        {
            try { DiagLog.Log(Tag, "ctor: subsequent instance, skipping early-phase shims"); } catch { }
            return;
        }

        TrySwallow(SubModule.InstallAssemblyResolveHandler, "ctor/AssemblyResolve");
        TrySwallow(IncompatibleModDetector.RunEarlyPhase, "ctor/IncompatEarly");
        // NOTE: CollectAssemblyTypesShim.Install + SubModuleConstructionGuard.Install
        // are intentionally NOT called here — they need Harmony fully initialised,
        // which isn't reliable at stub-ctor time. They run from
        // Dependencies/SubModule.OnSubModuleLoad alongside PatchShield + SaveShield
        // (proven-good timing). Moved 2026-05-27 after observing silent ctor logs.
    }

    protected override void OnSubModuleLoad()
    {
        base.OnSubModuleLoad();
        // Defence-in-depth: re-install in OnSubModuleLoad in case the ctor path
        // raced or was skipped on the early-detection guard. InstallAssemblyResolveHandler
        // is itself idempotent (Interlocked.CompareExchange gate).
        TrySwallow(SubModule.InstallAssemblyResolveHandler, "OnSubModuleLoad/AssemblyResolve");
        TrySwallow(
            () => DiagLog.Log(Tag, $"alias stub OnSubModuleLoad complete: {GetType().Assembly.GetName().Name}"),
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
            // Dual-log: DiagLog (visible in diag.log immediately) + EarlyLog
            // (drained into Main's FileLogger later). Without DiagLog here, ctor-time
            // shim exceptions were silent — caught 2026-05-27.
            try { DiagLog.LogCaught(Tag, where, ex); } catch { }
            try { EarlyLog.Error($"[{Tag}] {where}: {ex.Message}"); } catch { }
        }
    }
}
