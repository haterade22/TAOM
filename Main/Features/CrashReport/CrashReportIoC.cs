using DryIoc;
using TAOM.Features.CrashReport.Adapters;
using TAOM.Features.CrashReport.Collectors;
using TAOM.Features.CrashReport.Rendering;

namespace TAOM.Features.CrashReport;

public static class CrashReportIoC
{
    public static void RegisterCrashReportFeature(IContainer container)
    {
        // Collectors — instantiated as singletons because they're stateless or hold ring buffers.
        container.Register<IdentityCollector>(Reuse.Singleton);
        container.Register<ModuleListCollector>(Reuse.Singleton);
        container.Register<AssemblyListCollector>(Reuse.Singleton);
        container.Register<HarmonyCorrelationCollector>(Reuse.Singleton);
        container.Register<MissionStateCollector>(Reuse.Singleton);
        container.Register<McmSettingsCollector>(Reuse.Singleton);
        container.Register<ProcessEnvironmentCollector>(Reuse.Singleton);
        container.Register<GpuDisplayCollector>(Reuse.Singleton);

        // Ring buffers — singletons because hooks push into them across the session.
        container.Register<CampaignEventsBuffer>(Reuse.Singleton);
        container.Register<FrameTimingBuffer>(Reuse.Singleton);

        // CampaignStateCollector depends on a campaign-events snapshot lambda;
        // we wire it directly off CampaignEventsBuffer.Snapshot.
        container.RegisterDelegate<CampaignStateCollector>(r =>
            new CampaignStateCollector(r.Resolve<CampaignEventsBuffer>().Snapshot),
            Reuse.Singleton);

        // TaomStateCollector accepts feature-state lambdas; v1 stubs all of them.
        // Future PRs wire ICareerService etc. into here.
        container.RegisterDelegate<TaomStateCollector>(_ => new TaomStateCollector(),
            Reuse.Singleton);

        // LogTailCollector needs the IModLogger path.
        container.RegisterDelegate<LogTailCollector>(r =>
            new LogTailCollector(() => r.Resolve<TAOM.Core.Logging.IModLogger>().LogFilePath),
            Reuse.Singleton);

        // Renderers + bundle writer.
        container.Register<PlainTextCrashReportRenderer>(Reuse.Singleton);
        container.Register<JsonCrashReportRenderer>(Reuse.Singleton);
        container.Register<CrashBundleWriter>(Reuse.Singleton);

        // Bundle throttle — dedup by signature + session cap + cooldown. Without it a
        // per-tick crash spams hundreds of taom_crash_*.zip. Constants (not MCM): one
        // bundle per distinct crash, at most 25/session, >=30s between writes.
        container.RegisterDelegate<CrashBundleThrottle>(_ =>
            new CrashBundleThrottle(
                maxBundlesPerSession: 25,
                minInterval: System.TimeSpan.FromSeconds(30),
                clock: () => System.DateTime.UtcNow),
            Reuse.Singleton);

        // ButterLib reflection adapter.
        container.Register<IButterLibExceptionHandlerAdapter, ButterLibExceptionHandlerAdapter>(Reuse.Singleton);

        // Service.
        container.Register<ICrashReportService, CrashReportService>(Reuse.Singleton);

        // Hooks — registered as singletons so SubModule.OnSubModuleLoad can wire them
        // up and they live the full session.
        container.Register<TAOM.Features.CrashReport.Hooks.AppDomainExceptionHook>(Reuse.Singleton);
        container.Register<TAOM.Features.CrashReport.Hooks.Native2ManagedPatcher>(Reuse.Singleton);
    }
}
