using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.ModuleManager;
using TAOM.Core.Logging;

namespace TAOM.CoopCompat.LoadOrder;

/// <summary>
/// The engine's active modules. ModuleHelper.GetActiveModules walks the loaded modules in the order
/// InitializeModules added them, which is the launcher's load order. Each module's SubModule.xml is read from
/// its folder, as the crash report's ModuleListCollector does. Read once: the list cannot change while the game runs.
/// </summary>
public sealed class ActiveModulesAdapter : ILoadOrderAdapter
{
    private readonly IModLogger _logger;
    private IReadOnlyList<ModuleEntry>? _modules;

    public ActiveModulesAdapter(IModLogger logger) => _logger = logger;

    public IReadOnlyList<ModuleEntry> ActiveModules() => _modules ??= Read();

    private IReadOnlyList<ModuleEntry> Read()
    {
        var list = new List<ModuleEntry>();
        try
        {
            foreach (var module in ModuleHelper.GetActiveModules() ?? new List<ModuleInfo>())
            {
                if (string.IsNullOrEmpty(module?.Id)) continue;
                list.Add(new ModuleEntry(module!.Id, module.Name ?? module.Id, module.IsOfficial, HasObjectData(module.FolderPath)));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[CoopCompat] load order: could not read the module list: " + ex.GetType().Name + ": " + ex.Message);
        }

        return list;
    }

    private static bool HasObjectData(string? folder)
    {
        try
        {
            if (string.IsNullOrEmpty(folder)) return false;
            var manifest = Path.Combine(folder, "SubModule.xml");
            return File.Exists(manifest) && LoadOrderRules.DeclaresObjectData(File.ReadAllText(manifest));
        }
        catch (Exception)
        {
            // swallow-ok: an unreadable manifest counts as a module without object data, so it is not compared.
            return false;
        }
    }
}
