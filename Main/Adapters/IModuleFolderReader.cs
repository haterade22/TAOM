using System.Collections.Generic;

namespace TAOM.Adapters;

/// <summary>Reads module folders on disk: the game's Modules folder and the Steam Workshop content folder.</summary>
public interface IModuleFolderReader
{
    /// <summary>The full paths of the folders directly in the root; empty when the root is missing.</summary>
    IReadOnlyList<string> ListFolders(string root);

    /// <summary>The text of the folder's SubModule.xml, or null when the folder has none.</summary>
    string? ReadSubModuleXml(string folder);
}
