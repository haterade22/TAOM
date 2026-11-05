using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Diagnostics;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TAOM.Features.LostFooting.Domain;
using TAOM.Features.LostFooting.Hooks;

namespace TAOM.Features.LostFooting;

/// <summary>
/// Lost Footing, phase 1 (#894, docs/features/lost-footing.md): a census of battle agents that are stranded, off the
/// navigation mesh or stuck in water, written to the log so the cause can be measured before anything is rescued. One
/// mission behavior per mission holds all the state; there is no Harmony patch, no saved data, and no write to the
/// battle. The rescue is phase 2 and is not built.
/// </summary>
internal sealed class LostFootingModule : TaomFeatureModule
{
    private static readonly MissionBehaviorDecl[] Missions =
    {
        MissionBehaviorDecl.Of((mission, r) =>
        {
            var coop = r.Resolve<ICoopSessionProvider>();
            var logger = r.Resolve<IModLogger>();
            return new LostFootingMissionLogic(
                new LostFootingService(
                    r.Resolve<ILostFootingAdapter>(),
                    r.Resolve<ILostFootingSettingsProvider>(),
                    logger,
                    new LostFootingTunables(),
                    StopwatchStampClock.Instance,
                    () => LostFootingMissionGate.IsEligible(mission, coop)),
                logger);
        }),
    };

    public override string Id => "LostFooting";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<ILostFootingSettingsProvider, LostFootingSettingsProvider>(Reuse.Singleton);
        registrator.Register<ILostFootingAdapter, LostFootingAdapter>(Reuse.Singleton);
    }

    public override IReadOnlyList<MissionBehaviorDecl> MissionBehaviors => Missions;
}
