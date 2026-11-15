using System.Collections.Generic;
using System.IO;

namespace TAOM.Adapters;

public sealed class ModuleFolderReader : IModuleFolderReader
{
    public IReadOnlyList<string> ListFolders(string root) =>
        Directory.Exists(root) ? Directory.GetDirectories(root) : new string[0];

    public string? ReadSubModuleXml(string folder)
    {
        var path = Path.Combine(folder, "SubModule.xml");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
