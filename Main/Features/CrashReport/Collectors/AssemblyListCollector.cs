using System;
using System.Collections.Generic;
using System.Linq;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class AssemblyListCollector
{
    public AssemblyInventorySnapshot Collect()
    {
        var list = new List<AssemblySnapshot>();
        System.Reflection.Assembly[] assemblies;
        try { assemblies = AppDomain.CurrentDomain.GetAssemblies(); }
        catch { return new AssemblyInventorySnapshot(Array.Empty<AssemblySnapshot>()); }

        foreach (var a in assemblies)
        {
            bool dynamic = false;
            try { dynamic = a.IsDynamic; } catch { }
            if (dynamic) continue;

            string name = "(unknown)"; string version = "0.0.0.0"; string? location = null; bool inGac = false;
            try
            {
                var n = a.GetName();
                name = n.Name ?? "(unnamed)";
                version = n.Version?.ToString() ?? "0.0.0.0";
            }
            catch { }
            try { location = a.Location; } catch { /* some dynamic asms throw */ }
            try { inGac = a.GlobalAssemblyCache; } catch { }

            list.Add(new AssemblySnapshot(name, version, location, inGac));
        }
        return new AssemblyInventorySnapshot(list.OrderBy(x => x.Name, StringComparer.Ordinal).ToList());
    }
}
