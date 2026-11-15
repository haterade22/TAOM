using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using TAOM.Adapters;

namespace TAOM.Features.StartupAlerts;

/// <summary>A separate copy of a library TAOM.Dependencies already provides.</summary>
public sealed class BundledLibraryCopy
{
    public BundledLibraryCopy(string id, string name, string version, string folder, bool inWorkshop)
    {
        Id = id;
        Name = name;
        Version = version;
        Folder = folder;
        InWorkshop = inWorkshop;
    }

    public string Id { get; }

    /// <summary>The module's own Name, or its Id when it has none.</summary>
    public string Name { get; }

    /// <summary>The module's own Version text, or empty.</summary>
    public string Version { get; }

    public string Folder { get; }

    /// <summary>True for a Steam Workshop item, false for a folder in the game's Modules folder.</summary>
    public bool InWorkshop { get; }
}

/// <summary>
/// Finds installed copies of the four libraries TAOM.Dependencies provides (Harmony, ButterLib,
/// UIExtenderEx, MCM) that are not TAOM's own alias stubs. It checks folders on disk, not launcher
/// ticks: the v1.5.7 launcher stores ticks by module Id (LauncherModsVM.LoadSubModules), so a stub
/// and a Workshop copy of one Id share one tick. A stub is recognised by its SubModule naming
/// TAOM.Dependencies.dll (Stubs/&lt;ID&gt;/_Module/SubModule.xml). Never throws: a folder or root
/// that can't be read is skipped.
/// </summary>
public sealed class BundledLibraryCopyService
{
    /// <summary>Bannerlord's Steam app id; Workshop items live in workshop\content\261550.</summary>
    private const string BannerlordAppId = "261550";

    private const string StubDllName = "TAOM.Dependencies.dll";

    private static readonly string[] BundledIds =
    {
        "Bannerlord.Harmony",
        "Bannerlord.ButterLib",
        "Bannerlord.UIExtenderEx",
        "Bannerlord.MBOptionScreen",
    };

    private readonly IModuleFolderReader _reader;

    public BundledLibraryCopyService(IModuleFolderReader reader)
    {
        _reader = reader;
    }

    /// <summary>Copies in the Modules folder first, then in the Workshop folder (when given).</summary>
    public IReadOnlyList<BundledLibraryCopy> FindCopies(string modulesFolder, string? workshopFolder)
    {
        var copies = new List<BundledLibraryCopy>();
        Scan(modulesFolder, inWorkshop: false, copies);
        if (workshopFolder != null) Scan(workshopFolder, inWorkshop: true, copies);
        return copies;
    }

    /// <summary>
    /// Steam keeps an app's Workshop items in the library that holds the game:
    /// &lt;library&gt;\steamapps\common\&lt;game&gt; and &lt;library&gt;\steamapps\workshop\content\261550.
    /// </summary>
    public static string WorkshopContentFolder(string gameRoot) =>
        Path.GetFullPath(Path.Combine(gameRoot.TrimEnd('/', '\\'), "..", "..", "workshop", "content", BannerlordAppId));

    /// <summary>The copy a SubModule.xml describes, or null for a stub, another module or unreadable XML.</summary>
    public static BundledLibraryCopy? Classify(string folder, string subModuleXml, bool inWorkshop)
    {
        XElement root;
        try
        {
            root = XDocument.Parse(subModuleXml).Root!;
        }
        catch (XmlException)
        {
            return null;
        }

        var id = ValueOf(root.Element("Id"));
        if (id == null || !BundledIds.Contains(id, StringComparer.OrdinalIgnoreCase)) return null;

        var isStub = root.Descendants("DLLName")
            .Any(d => string.Equals(ValueOf(d), StubDllName, StringComparison.OrdinalIgnoreCase));
        if (isStub) return null;

        return new BundledLibraryCopy(id, ValueOf(root.Element("Name")) ?? id, ValueOf(root.Element("Version")) ?? string.Empty,
            folder, inWorkshop);
    }

    private static string? ValueOf(XElement? element) => (string?)element?.Attribute("value");

    private void Scan(string root, bool inWorkshop, List<BundledLibraryCopy> copies)
    {
        IReadOnlyList<string> folders;
        try
        {
            folders = _reader.ListFolders(root);
        }
        catch (Exception)
        {
            return;
        }

        foreach (var folder in folders)
        {
            try
            {
                var xml = _reader.ReadSubModuleXml(folder);
                var copy = xml == null ? null : Classify(folder, xml, inWorkshop);
                if (copy != null) copies.Add(copy);
            }
            catch (Exception)
            {
                // A locked or unreadable folder is skipped; the check must never stop the game.
            }
        }
    }
}
