using System;
using System.Collections.Generic;
using DryIoc;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace TAOM.Composition;

/// <summary>How a report point shows the modules that faulted since the last one.</summary>
internal enum FaultNotice
{
    /// <summary>Nothing receives a notice yet: keep the faults for the main-menu inquiry.</summary>
    Hold,

    /// <summary>
    /// An inquiry: GauntletQueryManager queues it and the initial screen does not clear it, unlike
    /// the chat log, which the initial screen clears after the splash video.
    /// </summary>
    Inquiry,

    /// <summary>A red chat line: in a game the chat log exists and nothing clears it first.</summary>
    ChatLine,
}

/// <summary>
/// The one line each SubModule hook calls: the engine-facing half of <see cref="ModuleRunner"/>.
/// Each method runs the modules for its phase and then reports any module that faulted, in the one
/// notice a player can see at that point (<see cref="NoticeFor"/>; the startup inquiry rule in
/// docs/reviews/lessons/localization-ui.md): faults from IoC.Configure and OnSubModuleLoad wait for
/// the main-menu inquiry, in-game faults get a red line. Nothing here throws, except the runner's
/// deliberate fail-closed throw for a save-owning module at campaign start. Every factory runs
/// before anything is handed to the engine, so a module whose factory throws adds nothing. The
/// public entry points read IoC; the overloads that take the runner and resolver are the tested seam.
/// </summary>
internal static class FeatureModuleHooks
{
    internal static void RunPhase(ApplyPhase phase, Func<string, bool> tryPatchCategory) =>
        RunPhase(IoC.Modules, IoC.Resolver, phase, tryPatchCategory);

    internal static void RunPhase(ModuleRunner? runner, IResolver? resolver, ApplyPhase phase,
        Func<string, bool> tryPatchCategory)
    {
        if (runner == null || resolver == null) return;

        runner.RunPhase(phase, tryPatchCategory, resolver);
        ReportFaults(runner, NoticeFor(phase));
    }

    /// <summary>
    /// ProcessLoad runs in OnSubModuleLoad, before Native builds the chat log and the inquiry manager,
    /// so its faults (and those of service registration and static initialisation, which run in
    /// IoC.Configure even earlier) are held. MainMenu runs once per process in the first
    /// OnBeforeInitialModuleScreenSetAsRoot, where only an inquiry survives the splash video. GameInit
    /// and FirstMission run inside a game, where the chat log receives a red line.
    /// </summary>
    internal static FaultNotice NoticeFor(ApplyPhase phase) => phase switch
    {
        ApplyPhase.ProcessLoad => FaultNotice.Hold,
        ApplyPhase.MainMenu => FaultNotice.Inquiry,
        _ => FaultNotice.ChatLine,
    };

    /// <summary>
    /// OnGameStart: campaign behaviors and campaign models on a CampaignGameStarter; Custom Battle
    /// models on a BasicGameStarter (the same split RegisterCustomBattleModels makes).
    /// </summary>
    internal static void AddGameStartContent(IGameStarter gameStarter) =>
        AddGameStartContent(IoC.Modules, IoC.Resolver, gameStarter);

    internal static void AddGameStartContent(ModuleRunner? runner, IResolver? resolver, IGameStarter gameStarter)
    {
        if (runner == null || resolver == null) return;

        if (gameStarter is CampaignGameStarter campaignStarter)
        {
            runner.RunCampaignStart(module =>
            {
                var behaviors = new List<CampaignBehaviorBase>();
                foreach (var decl in module.CampaignBehaviors)
                    behaviors.Add(decl.Create(resolver));
                var models = CreateModels(module, ModelTarget.Campaign, resolver);

                foreach (var behavior in behaviors)
                    campaignStarter.AddBehavior(behavior);
                foreach (var (decl, model) in models)
                    decl.Add(campaignStarter, model);
            });
        }
        else if (gameStarter is BasicGameStarter basicStarter)
        {
            runner.Run("custom battle start", includeParked: false, failClosed: false, module =>
            {
                foreach (var (decl, model) in CreateModels(module, ModelTarget.CustomBattle, resolver))
                    decl.Add(basicStarter, model);
            });
        }

        // OnGameStart runs during game loading, after Module.OnBeforeGameStart's ClearAllMessages.
        ReportFaults(runner, FaultNotice.ChatLine);
    }

    /// <summary>OnMissionBehaviorInitialize: hands each behavior to SubModule's AddTaomBehavior, which stamps [BattleLoad].</summary>
    internal static void AddMissionBehaviors(Mission mission, Action<MissionBehavior> addTaomBehavior) =>
        AddMissionBehaviors(IoC.Modules, IoC.Resolver, mission, addTaomBehavior);

    internal static void AddMissionBehaviors(ModuleRunner? runner, IResolver? resolver, Mission mission,
        Action<MissionBehavior> addTaomBehavior)
    {
        if (runner == null || resolver == null) return;

        runner.Run("mission start", includeParked: false, failClosed: false, module =>
        {
            var behaviors = new List<MissionBehavior>();
            foreach (var decl in module.MissionBehaviors)
                behaviors.Add(decl.Create(mission, resolver));
            foreach (var behavior in behaviors)
                addTaomBehavior(behavior);
        });

        ReportFaults(runner, FaultNotice.ChatLine);
    }

    private static List<(GameModelDecl Decl, GameModel Model)> CreateModels(
        TaomFeatureModule module, ModelTarget target, IResolver resolver)
    {
        var models = new List<(GameModelDecl Decl, GameModel Model)>();
        foreach (var decl in module.GameModels)
        {
            if (decl.Target == target)
                models.Add((decl, decl.Create(resolver)));
        }

        return models;
    }

    // Hold leaves the runner's list untouched, so the next report point (the main-menu inquiry)
    // still names every earlier fault. The inquiry is built exactly like plan 009's startup report in
    // SubModule.ReportPatchFailures; when both have something to say, GauntletQueryManager queues
    // them one after the other (this one first, because the runner call precedes 009's report).
    private static void ReportFaults(ModuleRunner runner, FaultNotice notice)
    {
        if (notice == FaultNotice.Hold) return;

        var summary = runner.TakeFaultSummary();
        if (summary == null) return;

        try
        {
            if (notice == FaultNotice.Inquiry)
                InformationManager.ShowInquiry(new InquiryData(
                    "TAOM", summary, true, false, "OK", string.Empty, null, null));
            else
                InformationManager.DisplayMessage(new InformationMessage(summary, Colors.Red));
        }
        catch (Exception ex)
        {
            // The notice must never break the phase; the [Module] log line already names the fault.
            runner.Log($"[Module] fault notice not shown: {ex.Message}");
        }
    }
}
