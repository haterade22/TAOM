using System;
using System.Collections.Generic;
using System.IO;
using TAOM.Core.Logging;
using TAOM.Features.CrashReport.Adapters;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Features.CrashReport.Domain;
using TAOM.Features.CrashReport.Rendering;
using TAOM.Features.CrashReport.UI;

namespace TAOM.Features.CrashReport;

public sealed class CrashReportService : ICrashReportService
{
    private readonly IModLogger _logger;
    private readonly IdentityCollector _identity;
    private readonly ModuleListCollector _modules;
    private readonly AssemblyListCollector _assemblies;
    private readonly HarmonyCorrelationCollector _harmony;
    private readonly CampaignStateCollector _campaign;
    private readonly MissionStateCollector _mission;
    private readonly TaomStateCollector _taomState;
    private readonly McmSettingsCollector _mcm;
    private readonly ProcessEnvironmentCollector _process;
    private readonly GpuDisplayCollector _gpu;
    private readonly LogTailCollector _logTail;
    private readonly FrameTimingBuffer _frameTiming;
    private readonly PlainTextCrashReportRenderer _plainRenderer;
    private readonly JsonCrashReportRenderer _jsonRenderer;
    private readonly CrashBundleWriter _bundleWriter;
    private readonly IButterLibExceptionHandlerAdapter _butterLib;
    private readonly CrashBundleThrottle _throttle;

    [ThreadStatic] private static bool _handling;

    public bool IsHandling => _handling;

    public CrashReportService(
        IModLogger logger,
        IdentityCollector identity,
        ModuleListCollector modules,
        AssemblyListCollector assemblies,
        HarmonyCorrelationCollector harmony,
        CampaignStateCollector campaign,
        MissionStateCollector mission,
        TaomStateCollector taomState,
        McmSettingsCollector mcm,
        ProcessEnvironmentCollector process,
        GpuDisplayCollector gpu,
        LogTailCollector logTail,
        FrameTimingBuffer frameTiming,
        PlainTextCrashReportRenderer plainRenderer,
        JsonCrashReportRenderer jsonRenderer,
        CrashBundleWriter bundleWriter,
        IButterLibExceptionHandlerAdapter butterLib,
        CrashBundleThrottle throttle)
    {
        _logger = logger;
        _identity = identity;
        _modules = modules;
        _assemblies = assemblies;
        _harmony = harmony;
        _campaign = campaign;
        _mission = mission;
        _taomState = taomState;
        _mcm = mcm;
        _process = process;
        _gpu = gpu;
        _logTail = logTail;
        _frameTiming = frameTiming;
        _plainRenderer = plainRenderer;
        _jsonRenderer = jsonRenderer;
        _bundleWriter = bundleWriter;
        _butterLib = butterLib;
        _throttle = throttle;
    }

    public string? HandleException(Exception exception, string originatingPatchTarget)
    {
        if (_handling) return null;  // re-entry guard — never recurse
        _handling = true;
        try
        {
            // Defer BUTR's own exception window on every capture (Codex review #46 MED-04
            // fix). ButterLib `Disable()` is idempotent and `CanBeSwitchedAtRuntime => true`
            // so a user re-enabling ButterLib at runtime via MCM would have left us with a
            // stale `_butterLibSuspended=true` flag and we'd never re-disable. Calling
            // TrySuspend every crash makes the coexistence guarantee sticky.
            if (CrashReportSettings.Instance?.SuspendButterLibHandler ?? true)
            {
                try { _butterLib.TrySuspend(); } catch { }
            }

            // Dedup chokepoint. The capture sources (9 per-tick Harmony Finalizers, the
            // AppDomain hook, the battle-load watchdog, native callback shims) all funnel
            // here, so a crash that recurs every frame would otherwise write a fresh bundle
            // each tick — re-zipping an ever-growing taom_debug.log. Compute the cheap
            // signature (reads the frozen stack only; no engine collectors) and let the
            // throttle decide. On suppression: one log line, no ComposeContext / bundle /
            // notify — this kills both the disk spam and the growing-log feedback loop.
            var earlyStack = StackFrameSnapshotBuilder.FromException(exception);
            string earlySignature = CrashSignatureCalculator.Compute(
                exception?.GetType().FullName ?? "(unknown)", originatingPatchTarget, earlyStack);
            var admission = _throttle.Admit(earlySignature);
            if (admission.Decision != CrashBundleDecision.WriteBundle)
            {
                _logger.LogError(
                    $"[CrashReport] {admission.Decision} {CrashSignatureCalculator.Short(earlySignature)} " +
                    $"({exception?.GetType().Name ?? "(unknown)"} @ {originatingPatchTarget}) " +
                    $"occurrence #{admission.Occurrence} — bundle suppressed");
                return null;
            }

            // Reduced-capture mode for off-main-thread captures (Codex review #46 MED-03).
            // AppDomainExceptionHook tags off-main-thread exceptions on `ex.Data`; when set,
            // skip Mission/Campaign reads (not thread-safe) and skip the UI inquiry (vanilla
            // ShowInquiry invokes subscribers synchronously — unsafe from worker threads).
            bool offMainThread = IsOffMainThread(exception);

            var failures = new List<CollectorFailure>();
            var context = ComposeContext(exception, originatingPatchTarget, failures, offMainThread);

            string plain = SafeRender(() => _plainRenderer.Render(context), failures, nameof(_plainRenderer)) ?? "(plain render failed)";
            string json = SafeRender(() => _jsonRenderer.Render(context), failures, nameof(_jsonRenderer)) ?? "{}";

            WriteToLog(plain);
            string outputDir = ResolveOutputDirectory();
            string? bundlePath = null;
            if (CrashReportSettings.Instance?.WriteCrashBundle ?? true)
            {
                try { bundlePath = _bundleWriter.Write(context, plain, json, outputDir); }
                catch (Exception ex) { _logger.LogError($"[CrashReport] bundle write failed: {ex.GetType().Name}: {ex.Message}"); }
            }

            if (!offMainThread)
            {
                try { CrashNotifier.Notify(context.Exception?.Type ?? "(unknown)", bundlePath, _logger.LogFilePath); }
                catch (Exception ex) { _logger.LogWarning($"[CrashReport] notifier failed: {ex.GetType().Name}: {ex.Message}"); }
            }
            else
            {
                _logger.LogWarning("[CrashReport] off-main-thread capture; UI inquiry skipped to avoid render-thread invocation");
            }

            return bundlePath;
        }
        catch (Exception ex)
        {
            try { _logger.LogError($"[CrashReport] HandleException itself threw: {ex.GetType().Name}: {ex.Message}"); } catch { }
            return null;
        }
        finally
        {
            _handling = false;
        }
    }

    private static bool IsOffMainThread(Exception ex)
    {
        try
        {
            return ex?.Data != null
                && ex.Data.Contains(Hooks.AppDomainExceptionHook.OffMainThreadDataKey)
                && ex.Data[Hooks.AppDomainExceptionHook.OffMainThreadDataKey] is bool b
                && b;
        }
        catch { return false; }
    }

    private ExceptionContext ComposeContext(Exception exception, string originatingPatchTarget, List<CollectorFailure> failures, bool offMainThread)
    {
        var stack = StackFrameSnapshotBuilder.FromException(exception);
        var ex = ExceptionFrameBuilder.Build(exception);
        string signature = CrashSignatureCalculator.Compute(
            exception?.GetType().FullName ?? "(unknown)",
            originatingPatchTarget,
            stack);

        var identity = Safe(() => _identity.Collect(originatingPatchTarget), failures, "Identity")
                       ?? new IdentitySnapshot("?", "?", "?", "?", originatingPatchTarget, "?");
        var modules = Safe(() => _modules.Collect(), failures, "Modules") ?? new ModuleInventorySnapshot(Array.Empty<ModuleSnapshot>());
        var assemblies = Safe(() => _assemblies.Collect(), failures, "Assemblies") ?? new AssemblyInventorySnapshot(Array.Empty<AssemblySnapshot>());
        var harmony = Safe(() => _harmony.CollectFromException(exception, stack), failures, "Harmony") ?? new HarmonyCorrelationSnapshot(Array.Empty<StackFramePatchInfo>(), Array.Empty<HarmonyOwnerSummary>(), 0);
        // Off-main-thread: skip Mission/Campaign reads; they're not thread-safe under
        // engine mid-mutation. Codex review #46 MED-03. The CollectorFailures list gets
        // an explicit "skipped" marker so the renderer shows why these sections are empty.
        CampaignSnapshot? campaign;
        MissionSnapshot? mission;
        if (offMainThread)
        {
            campaign = null;
            mission = null;
            failures.Add(new CollectorFailure("Campaign", "OffMainThreadSkipped", "Campaign/Mission collectors skipped — capture fired on a non-main thread; engine state reads are not thread-safe."));
            failures.Add(new CollectorFailure("Mission", "OffMainThreadSkipped", "see Campaign failure"));
        }
        else
        {
            campaign = Safe(() => _campaign.Collect(), failures, "Campaign");
            mission = Safe(() => _mission.Collect(), failures, "Mission");
        }
        var taom = Safe(() => _taomState.Collect(), failures, "TaomState") ?? new TaomStateSnapshot(null, Array.Empty<SpecialResourceEntry>(), null, null, null);
        var mcm = Safe(() => _mcm.Collect(), failures, "Mcm") ?? new McmSettingsSnapshot(Array.Empty<McmProviderSnapshot>());
        var process = Safe(() => _process.CollectProcess(), failures, "Process") ?? new ProcessSnapshot(0, 0, 0, 0, 0, 0, 0, 0, 0, new ThrowingThreadSnapshot(0, null, false, "Unknown"));
        var gpu = Safe(() => _gpu.CollectGpu(), failures, "Gpu") ?? new GpuSnapshot(Array.Empty<GpuAdapterEntry>());
        var display = Safe(() => _gpu.CollectDisplay(), failures, "Display") ?? new DisplaySnapshot(0, 0, 0, false, 0);
        var os = Safe(() => _process.CollectOs(), failures, "Os") ?? new OsSnapshot("?", "?", false, 0, "?", "?", "?", "?");
        var appdomain = Safe(() => _process.CollectAppDomain(), failures, "AppDomain") ?? new AppDomainSnapshot("?", "?", null, false);
        var envvars = Safe(() => _process.CollectEnvVars(), failures, "EnvVars") ?? Array.Empty<EnvVarEntry>();
        var perf = Safe(() => _frameTiming.Snapshot(), failures, "Performance") ?? new FrameTimingSnapshot(Array.Empty<float>(), 0d, 0d, 0);
        var logs = Safe(() => _logTail.Collect(), failures, "Logs") ?? new LogTailSnapshot(_logger.LogFilePath, Array.Empty<string>(), null, Array.Empty<string>(), null, Array.Empty<string>());

        return new ExceptionContext(
            CapturedAtUtc: DateTime.UtcNow,
            CrashSignature: signature,
            Identity: identity,
            Exception: ex,
            StackFrames: stack,
            Harmony: harmony,
            Modules: modules,
            Assemblies: assemblies,
            Campaign: campaign,
            Mission: mission,
            Taom: taom,
            Mcm: mcm,
            Process: process,
            Gpu: gpu,
            Display: display,
            Os: os,
            AppDomain: appdomain,
            EnvVars: envvars,
            Performance: perf,
            Logs: logs,
            CollectorFailures: failures);
    }

    private static T? Safe<T>(Func<T?> f, List<CollectorFailure> failures, string name) where T : class
    {
        try { return f(); }
        catch (Exception ex)
        {
            failures.Add(new CollectorFailure(name, ex.GetType().Name, ex.Message));
            return null;
        }
    }

    private static string? SafeRender(Func<string> f, List<CollectorFailure> failures, string name)
    {
        try { return f(); }
        catch (Exception ex)
        {
            failures.Add(new CollectorFailure(name, ex.GetType().Name, ex.Message));
            return null;
        }
    }

    private void WriteToLog(string plain)
    {
        try
        {
            // Each line gets a per-line timestamp via the logger; preserve sectioning.
            foreach (var line in plain.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                _logger.LogError("[CrashReport] " + line);
        }
        catch { }
    }

    private static string ResolveOutputDirectory()
    {
        try
        {
            var dir = Path.Combine(Environment.CurrentDirectory, "Logs");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch { return Environment.CurrentDirectory; }
    }
}
