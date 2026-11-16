using System;
using System.Collections.Generic;
using System.IO;
using DryIoc;
using TAOM.Adapters;
using TAOM.Composition;
using TAOM.Core.Logging;
using TAOM.Features.CoopInterop;
using TaleWorlds.Library;

namespace TAOM.Features.StartupAlerts;

/// <summary>
/// Setup problems the player must fix outside the game, shown once at the main menu in one inquiry:
/// War Sails loaded with TAOM, and separate copies of the four libraries TAOM.Dependencies provides
/// (its alias stubs carry the same module Ids, so the v1.5.7 launcher lists both and can't tick them
/// apart). MainMenu is the first point where an inquiry survives the splash video, and it runs once per
/// process. Skipped on a dedicated server. Each check fails open on its own; nothing here throws.
/// </summary>
internal sealed class StartupAlertsModule : TaomFeatureModule
{
    public override string Id => "StartupAlerts";

    public override void RegisterServices(IRegistrator registrator)
    {
        registrator.Register<IModuleFolderReader, ModuleFolderReader>(Reuse.Singleton);
        registrator.Register<ILoadedModuleIds, LoadedModuleIds>(Reuse.Singleton);
        registrator.Register<BundledLibraryCopyService>(Reuse.Singleton);
    }

    public override void OnPhase(ApplyPhase phase, IResolver resolver)
    {
        if (phase != ApplyPhase.MainMenu) return;
        if (resolver.Resolve<IDedicatedServerProvider>().IsDedicatedServer) return;

        var logger = resolver.Resolve<IModLogger>();
        var paragraphs = new List<string>();
        Run(logger, "War Sails check", () => CheckWarSails(resolver, logger, paragraphs));
        Run(logger, "library copy check", () => CheckLibraryCopies(resolver, logger, paragraphs));
        StartupAlertsEntry.Show(logger, paragraphs);
    }

    private static void Run(IModLogger logger, string name, Action check)
    {
        try
        {
            check();
        }
        catch (Exception e)
        {
            logger.LogWarning($"[StartupAlerts] {name} skipped: {e.GetType().Name}");
        }
    }

    private static void CheckWarSails(IResolver resolver, IModLogger logger, List<string> paragraphs)
    {
        if (!WarSailsCheck.IsLoaded(resolver.Resolve<ILoadedModuleIds>().Get())) return;
        logger.LogWarning($"[StartupAlerts] {WarSailsCheck.WarSailsModuleId} is loaded with TAOM");
        paragraphs.Add(StartupAlertsEntry.WarSailsParagraph());
    }

    private static void CheckLibraryCopies(IResolver resolver, IModLogger logger, List<string> paragraphs)
    {
        var modulesFolder = Path.GetFullPath(Path.Combine(BasePath.Name, "Modules"));
        var workshopFolder = BundledLibraryCopyService.WorkshopContentFolder(BasePath.Name);
        var copies = resolver.Resolve<BundledLibraryCopyService>().FindCopies(modulesFolder, workshopFolder);

        logger.LogInfo($"[StartupAlerts] {copies.Count} separate library copies found in {modulesFolder} and {workshopFolder}");
        foreach (var copy in copies)
            logger.LogWarning($"[StartupAlerts] {copy.Id} {copy.Version} ({(copy.InWorkshop ? "Workshop" : "Modules")}): {copy.Folder}");

        if (copies.Count > 0) paragraphs.Add(StartupAlertsEntry.LibraryCopiesParagraph(copies));
    }
}
