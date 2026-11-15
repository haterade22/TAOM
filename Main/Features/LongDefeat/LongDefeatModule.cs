using DryIoc;
using TAOM.Adapters;
using TAOM.Core.Logging;
using TAOM.Composition;
using TAOM.Features.LongDefeat.Autorun;
using TAOM.Features.LongDefeat.Coop;
using TAOM.Features.LongDefeat.Hooks;

namespace TAOM.Features.LongDefeat;

/// <summary>The Long Defeat (#1079, docs/features/long-defeat.md): no mission behavior, patch or save data; TAOM.Client replaces the null game-manager factory (ADR-013).</summary>
internal sealed class LongDefeatModule : TaomFeatureModule
{
    public override string Id => "LongDefeat";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ILongDefeatGameManagerFactory, NullLongDefeatGameManagerFactory>(Reuse.Singleton);
        registrator.Register<LongDefeatConfigCatalog>(Reuse.Singleton);
        registrator.Register<LongDefeatSession>(Reuse.Singleton);
        registrator.RegisterDelegate(r => LongDefeatRunRecords.ForGame(r.Resolve<IModLogger>().LogFilePath), Reuse.Singleton);
        registrator.Register<ILongDefeatMenuAdapter, LongDefeatMenuAdapter>(Reuse.Singleton);
        registrator.Register<LongDefeatSetupPrompt>(Reuse.Singleton);
        registrator.Register<LongDefeatMenuEntry>(Reuse.Singleton);
        registrator.Register<LongDefeatMissionLauncher>(Reuse.Singleton);
        // Co-op (#1280): TAOM.CoopCompat resolves the port and attaches its Coop link. The start path is resolved at the call, because the
        // launcher the prompt reaches takes the runs.
        registrator.Register<LongDefeatCoopState>(Reuse.Singleton);
        registrator.RegisterDelegate(r => new LongDefeatCoopRuns(r.Resolve<LongDefeatCoopState>(), setup => r.Resolve<LongDefeatSetupPrompt>().Start(setup),
            r.Resolve<ILongDefeatMenuAdapter>(), r.Resolve<IModLogger>()), Reuse.Singleton);
        registrator.RegisterDelegate<ILongDefeatCoopPort>(r => r.Resolve<LongDefeatCoopRuns>(), Reuse.Singleton);
        registrator.Register<LongDefeatCoopMenuEntry>(Reuse.Singleton);
        registrator.RegisterDelegate(_ => LongDefeatAutorunPlan.Resolve(), Reuse.Singleton);
        registrator.Register<ILongDefeatAutorunHost, NullLongDefeatAutorunHost>(Reuse.Singleton);
        registrator.Register<LongDefeatAutorunDriver>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        // The engine calls the first-screen hook with no try and again on every tick while it throws (#807), so the step goes
        // through MainMenuStep. Adding the option is guarded by its id, so a repeat adds nothing.
        if (phase == ApplyPhase.MainMenu)
        {
            MainMenuStep.Run("long defeat menu option", () => resolver.Resolve<LongDefeatMenuEntry>().Install(),
                () => resolver.Resolve<IModLogger>());
            MainMenuStep.Run("long defeat co-op menu option", () => resolver.Resolve<LongDefeatCoopMenuEntry>().Install(),
                () => resolver.Resolve<IModLogger>());
        }
    }
}
