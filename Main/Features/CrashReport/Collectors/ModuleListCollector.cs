using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using TaleWorlds.ModuleManager;
using TAOM.Features.CrashReport.Domain;

namespace TAOM.Features.CrashReport.Collectors;

public sealed class ModuleListCollector
{
    public ModuleInventorySnapshot Collect()
    {
        var active = SafeGetActiveModules();
        var idToIndex = active.Select((m, i) => (m, i)).ToDictionary(x => x.m.Id, x => x.i, StringComparer.Ordinal);

        var modules = new List<ModuleSnapshot>(active.Count);
        for (int i = 0; i < active.Count; i++)
        {
            var info = active[i];
            string id = SafeRead(() => info.Id) ?? "(unknown)";
            string version = SafeRead(() => info.Version.ToString()) ?? "(unknown)";
            bool official = SafeReadBool(() => info.IsOfficial);
            string? manifestPath = SafeRead(() => info.FolderPath) is { } folder ? Path.Combine(folder, "SubModule.xml") : null;
            string? dllPath = ResolveMainDllPath(info);
            string sha1 = DllHasher.Sha1OfFile(dllPath);
            var declaredDeps = ReadDeclaredDependencies(manifestPath);
            bool inversion = declaredDeps.Any(dep => idToIndex.TryGetValue(dep, out var depIdx) && depIdx > i);
            var xmlFiles = ReadXmlFilesList(manifestPath);

            modules.Add(new ModuleSnapshot(
                Id: id,
                Version: version,
                LoadIndex: i,
                IsOfficial: official,
                MainDllSha1: sha1,
                MainDllPath: dllPath,
                ManifestPath: manifestPath,
                DeclaredDependencies: declaredDeps,
                HasDependencyOrderInversion: inversion,
                XmlFilePaths: xmlFiles));
        }
        return new ModuleInventorySnapshot(modules);
    }

    private static IReadOnlyList<ModuleInfo> SafeGetActiveModules()
    {
        try { return ModuleHelper.GetActiveModules()?.ToList() ?? new List<ModuleInfo>(); }
        catch { return new List<ModuleInfo>(); }
    }

    private static string? ResolveMainDllPath(ModuleInfo info)
    {
        try
        {
            var folder = info.FolderPath;
            if (string.IsNullOrEmpty(folder)) return null;
            // Convention: <folder>/bin/Win64_Shipping_Client/<Id>.dll
            var candidate = Path.Combine(folder, "bin", "Win64_Shipping_Client", info.Id + ".dll");
            if (File.Exists(candidate)) return candidate;
            // Some mods ship under different names — return the first .dll under bin/Win64_Shipping_Client.
            var binDir = Path.Combine(folder, "bin", "Win64_Shipping_Client");
            if (Directory.Exists(binDir))
            {
                var first = Directory.EnumerateFiles(binDir, "*.dll").FirstOrDefault();
                if (first != null) return first;
            }
            return null;
        }
        catch { return null; }
    }

    private static IReadOnlyList<string> ReadDeclaredDependencies(string? manifestPath)
    {
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath)) return Array.Empty<string>();
        try
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var deps = new List<string>();
            foreach (XmlNode node in doc.SelectNodes("//DependedModule") ?? (XmlNodeList)doc.CreateDocumentFragment().ChildNodes)
            {
                var idAttr = node.Attributes?["Id"]?.Value;
                if (!string.IsNullOrEmpty(idAttr)) deps.Add(idAttr);
            }
            return deps;
        }
        catch { return Array.Empty<string>(); }
    }

    private static IReadOnlyList<string> ReadXmlFilesList(string? manifestPath)
    {
        if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath)) return Array.Empty<string>();
        try
        {
            var doc = new XmlDocument();
            doc.Load(manifestPath);
            var paths = new List<string>();
            foreach (XmlNode node in doc.SelectNodes("//XmlNode/XmlName") ?? (XmlNodeList)doc.CreateDocumentFragment().ChildNodes)
            {
                var pathAttr = node.Attributes?["path"]?.Value;
                if (!string.IsNullOrEmpty(pathAttr)) paths.Add(pathAttr);
            }
            return paths;
        }
        catch { return Array.Empty<string>(); }
    }

    private static T? SafeRead<T>(Func<T?> f) where T : class { try { return f(); } catch { return null; } }
    private static bool SafeReadBool(Func<bool> f) { try { return f(); } catch { return false; } }
}
