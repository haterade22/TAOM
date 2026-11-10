using System;
using System.IO;
using System.Xml.Linq;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TAOM.Features.Realms.Domain.Config;

namespace TAOM.Features.Realms;

/// <summary>Reads ModuleData/realms_titles.xml, falling back to the built-in defaults.</summary>
internal static class RealmsConfigLoader
{
    public const string ModuleId = "TAOM";
    public const string FileName = "realms_titles.xml";

    public static RealmsConfig Load()
    {
        string path;
        try
        {
            path = Path.Combine(ModuleHelper.GetModuleFullPath(ModuleId), "ModuleData", FileName);
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] module path not found, using default title setup: {ex.Message}");
            return RealmsConfig.Defaults();
        }

        if (!File.Exists(path))
        {
            Debug.Print($"[TAOM.Realms] {path} is missing, using default title setup.");
            return RealmsConfig.Defaults();
        }

        try
        {
            var result = RealmsConfigXml.Parse(XDocument.Load(path));
            foreach (var warning in result.Warnings)
                Debug.Print($"[TAOM.Realms] {FileName}: {warning}");
            return result.Config;
        }
        catch (Exception ex)
        {
            Debug.Print($"[TAOM.Realms] {FileName} could not be read, using default title setup: {ex.Message}");
            return RealmsConfig.Defaults();
        }
    }
}
