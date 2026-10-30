using System.Collections.Generic;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Features.CoopInterop;
using TAOM.Features.ShadowfaxScreens.Clan;
using TAOM.Features.ShadowfaxScreens.Diagnostics;
using TAOM.Features.ShadowfaxScreens.Inventory;
using TAOM.Features.ShadowfaxScreens.Party;
using TAOM.Features.ShadowfaxScreens.Quests;

namespace TAOM.Features.ShadowfaxScreens;

/// <summary>
/// Shadowfax Screens, screen open speed (docs/features/shadowfax-screens.md): the shared settings provider and the
/// [ScreenOpen] timer, plus the four screen categories (clan, inventory, party, quest log), each applied at GameInit.
/// IStampClock is registered by LoadTimeStampsModule, which precedes this module in the list. Each screen's builder
/// appends its own block to RegisterServices and InitializeStatics.
/// </summary>
internal sealed class ShadowfaxScreensModule : TaomFeatureModule
{
    private static readonly PatchCategoryDecl[] Categories =
    {
        new(ClanFastOpenCalls.PatchCategory, ApplyPhase.GameInit),
        new(ShadowfaxInventoryCalls.PatchCategory, ApplyPhase.GameInit),
        new(PartyFastOpenCalls.PatchCategory, ApplyPhase.GameInit),
        new(QuestLogDeferCalls.Category, ApplyPhase.GameInit),
    };

    public override string Id => "ShadowfaxScreens";

    public override IReadOnlyList<PatchCategoryDecl> PatchCategories => Categories;

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IShadowfaxScreensSettingsProvider, ShadowfaxScreensSettingsProvider>(Reuse.Singleton);
        registrator.Register<ScreenOpenTimer>(Reuse.Singleton);

        // Clan fast open (Clan/): needs ICoopPresenceProvider (CoopInteropIoC) and IStampClock, both registered elsewhere.
        registrator.Register<ClanScreenFastPathService>(Reuse.Singleton);

        // Inventory fast open.
        registrator.Register<InventoryOpenFastPathService>(Reuse.Singleton);

        // Party fast open.
        registrator.Register<PartyScreenFastPathService>(Reuse.Singleton);

        // Quest log fast open.
        registrator.Register<IQuestJournalAdapter, NullQuestJournalAdapter>(Reuse.Singleton); // TAOM.Client replaces it
        registrator.Register<QuestLogDeferService>(Reuse.Singleton);
    }

    public override void InitializeStatics(IResolver resolver)
    {
        ScreenOpenTiming.Initialize(resolver.Resolve<ScreenOpenTimer>());
        ClanFastOpenCalls.Initialize(resolver.Resolve<ClanScreenFastPathService>());

        // Inventory fast open.
        ShadowfaxInventoryCalls.Initialize(resolver.Resolve<InventoryOpenFastPathService>());

        // Party fast open.
        PartyFastOpenCalls.Initialize(resolver.Resolve<PartyScreenFastPathService>());

        // Quest log fast open.
        QuestLogDeferCalls.Initialize(resolver.Resolve<QuestLogDeferService>());
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.GameInit) return;

        // A dedicated server never indexes the [ClientOnly] hooks (ADR-013) and holds the null quest adapter, so both lines
        // below would report an install failure that did not happen.
        if (resolver.Resolve<IDedicatedServerProvider>().IsDedicatedServer) return;

        // The Clan category has just applied: report whether its transpiler found the one call site.
        resolver.Resolve<ClanScreenFastPathService>().LogInstall(ClanFastOpenCalls.LastSwapped);

        // The Quest log's category has just applied: bind the adapter, which checks its four patches are attached.
        resolver.Resolve<QuestLogDeferService>().Install();
    }
}
