using System;
using System.Collections.Generic;
using System.IO;
using TaleWorlds.ModuleManager;

namespace TAOM.CoopCompat.LoadOrder;

/// <summary>
/// The engine's active modules. ModuleHelper.GetActiveModules walks the loaded modules in the order
/// InitializeModules added them, which is the launcher's load order. Each module's SubModule.xml is read from
/// its folder, as the crash report's ModuleListCollector does. Kept after the first good read: the list cannot change
/// while the game runs. A failed read throws and is not kept, so the caller logs it and the next call reads again,
/// instead of an empty list that would read as "no order sent" or as a match.
/// </summary>
public sealed class ActiveModulesAdapter : ILoadOrderAdapter
{
    private IReadOnlyList<ModuleEntry>? _modules;

    public IReadOnlyList<ModuleEntry> ActiveModules() => _modules ??= Read();

    private static IReadOnlyList<ModuleEntry> Read()
    {
        var list = new List<ModuleEntry>();
        foreach (var module in ModuleHelper.GetActiveModules() ?? new List<ModuleInfo>())
        {
            if (string.IsNullOrEmpty(module?.Id)) continue;
            list.Add(new ModuleEntry(module!.Id, module.Name ?? module.Id, module.IsOfficial, HasObjectData(module.FolderPath)));
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
